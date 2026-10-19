using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private readonly MiniGolfSession session = new MiniGolfSession();

    [Header("Skor Kuralları")]
    [SerializeField, Min(0)] private int initialStrokeCount;
    [Tooltip("0 sınırsızdır. Pozitifse Canvas'ta tutulacak en yüksek vuruş sayısıdır.")]
    [SerializeField, Min(0)] private int maximumStrokeCount;
    [SerializeField, Min(0)] private int startingScore = 100;
    [SerializeField, Min(0)] private int pointsLostPerStroke = 5;

    [Header("Canlı Skor UI")]
    [SerializeField] private TMP_Text strokeText;

    [Header("Sonuç Paneli")]
    [SerializeField] private GameObject resultPanel;
    [Tooltip("Tek bir metin alanında hem vuruş hem final skoru göstermek için kullanılır.")]
    [SerializeField] private TMP_Text resultSummaryText;
    [SerializeField] private TMP_Text resultStrokeText;
    [SerializeField] private TMP_Text finalScoreText;

    [Header("Sahneler Arası Veriler")]
    [Min(0)] public int totalGlobalStrokes;

    public int CurrentStrokeCount { get; private set; }
    public bool IsHoleComplete { get; private set; }
    public int CurrentFinalScore => CalculateFinalScore(CurrentStrokeCount, startingScore, pointsLostPerStroke);
    public int TotalStrokeCount => session.TotalStrokes + (IsHoleComplete ? 0 : CurrentStrokeCount);
    public int SessionFinalScore => CalculateFinalScore(TotalStrokeCount, startingScore, pointsLostPerStroke);

    private int activeHoleIndex;

    public static GameManager EnsureInstance()
    {
        if (Instance != null) return Instance;

        GameManager existingManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (existingManager != null) return existingManager;

        GameObject managerObject = new GameObject("GameManager");
        return managerObject.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.AdoptSceneUI(this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        activeHoleIndex = ResolveHoleIndex(SceneManager.GetActiveScene());
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }

    private void Start()
    {
        ResetCurrentHole();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public bool RegisterStroke()
    {
        if (IsHoleComplete)
        {
            return false;
        }

        if (maximumStrokeCount > 0 && CurrentStrokeCount >= maximumStrokeCount)
        {
            return false;
        }

        CurrentStrokeCount++;
        RefreshUI();
        return true;
    }

    public int CompleteHole()
    {
        if (IsHoleComplete)
        {
            return SessionFinalScore;
        }

        IsHoleComplete = true;
        session.RecordHole(activeHoleIndex, CurrentStrokeCount);
        totalGlobalStrokes = session.TotalStrokes;
        RefreshUI();

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        return SessionFinalScore;
    }

    public void BeginNewSession()
    {
        session.Reset();
        totalGlobalStrokes = 0;
        ResetCurrentHole();
    }

    public bool TryGetHoleStrokes(int holeIndex, out int strokeCount)
    {
        return session.TryGetHoleStrokes(holeIndex, out strokeCount);
    }

    public int[] GetHoleStrokeSnapshot(int holeCount)
    {
        return session.CreateHoleStrokeSnapshot(holeCount);
    }

    public void ResetCurrentHole()
    {
        CurrentStrokeCount = maximumStrokeCount > 0
            ? Mathf.Min(initialStrokeCount, maximumStrokeCount)
            : initialStrokeCount;
        IsHoleComplete = false;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        RefreshUI();
    }

    [ContextMenu("Test / Vuruş Sayısını Sıfırla")]
    public void ResetStrokeCountForTesting()
    {
        ResetCurrentHole();
    }

    public static int CalculateFinalScore(int strokeCount, int baseScore = 100, int penaltyPerStroke = 5)
    {
        int safeStrokeCount = Mathf.Max(0, strokeCount);
        int safeBaseScore = Mathf.Max(0, baseScore);
        int safePenalty = Mathf.Max(0, penaltyPerStroke);
        return Mathf.Max(0, safeBaseScore - safeStrokeCount * safePenalty);
    }

    // Eski sahne bağlantılarını kırmadan UI sahipliğini GameManager'a taşır.
    public void BindLegacyUI(TMP_Text liveStrokeText, GameObject legacyResultPanel)
    {
        if (strokeText == null)
        {
            strokeText = liveStrokeText;
        }

        // Aynı TMP objesini kapatmak canlı sayacı da gizleyeceği için panel olarak alma.
        if (resultPanel == null && legacyResultPanel != null &&
            (liveStrokeText == null || legacyResultPanel != liveStrokeText.gameObject))
        {
            resultPanel = legacyResultPanel;
        }

        RefreshUI();
    }

    // Önceki API'yi kullanan başka sahneler için uyumluluk metodu.
    public void AddToTotalScore(int levelStrokes)
    {
        int holeIndex = session.TryGetHoleStrokes(activeHoleIndex, out _)
            ? session.GetFirstUnrecordedHoleIndex()
            : activeHoleIndex;
        session.RecordHole(holeIndex, levelStrokes);
        totalGlobalStrokes = session.TotalStrokes;
        RefreshUI();
    }

    public void LoadNextLevel()
    {
        if (MiniGolfSceneCatalog.TryGetNextCourseBuildIndex(
                SceneManager.GetActiveScene(),
                out int nextSceneIndex))
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            Debug.Log("Oyun bitti: yüklenecek başka sahne yok.");
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // GolfHole pauses the completed course before loading. A scene load is
        // the single authoritative resume point so pause state cannot leak into
        // the next course or a clean restart.
        Time.timeScale = 1f;
        activeHoleIndex = ResolveHoleIndex(scene);
        ResetCurrentHole();
    }

    private int ResolveHoleIndex(Scene scene)
    {
        int courseIndex = MiniGolfSceneCatalog.GetCourseIndex(scene);
        return courseIndex >= 0 ? courseIndex : session.GetFirstUnrecordedHoleIndex();
    }

    private void AdoptSceneUI(GameManager sceneManager)
    {
        if (sceneManager.strokeText != null) strokeText = sceneManager.strokeText;
        if (sceneManager.resultPanel != null) resultPanel = sceneManager.resultPanel;
        if (sceneManager.resultSummaryText != null) resultSummaryText = sceneManager.resultSummaryText;
        if (sceneManager.resultStrokeText != null) resultStrokeText = sceneManager.resultStrokeText;
        if (sceneManager.finalScoreText != null) finalScoreText = sceneManager.finalScoreText;
    }

    private void RefreshUI()
    {
        if (strokeText != null)
        {
            strokeText.SetText("Vuruş: {0}", CurrentStrokeCount);
        }

        if (resultSummaryText != null)
        {
            resultSummaryText.SetText(
                "Toplam Vuruş: {0}\nFinal Skor: {1}",
                TotalStrokeCount,
                SessionFinalScore);
        }

        if (resultStrokeText != null)
        {
            resultStrokeText.SetText("Toplam Vuruş: {0}", TotalStrokeCount);
        }

        if (finalScoreText != null)
        {
            finalScoreText.SetText("Final Skor: {0}", SessionFinalScore);
        }
    }
}
