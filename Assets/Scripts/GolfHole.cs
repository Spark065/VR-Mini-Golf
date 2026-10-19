using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GolfHole : MonoBehaviour
{
    [Header("Efektler")]
    public AudioSource cheerSound;
    public ParticleSystem confettiEffect;

    [Header("Topun Deliğe Oturması")]
    [Tooltip("Boş bırakılırsa delik objesinin merkezi kullanılır.")]
    [SerializeField] private Transform ballRestPoint = null;
    [SerializeField, Min(0f)] private float sinkDepth = 0.08f;
    [SerializeField, Min(0f)] private float sinkDuration = 0.4f;

    [Header("Bölüm Geçişi")]
    [Tooltip("Boş bırakılırsa Meta ray uyumlu World Space Canvas çalışma anında oluşturulur.")]
    [SerializeField] private MetaLevelTransitionUI levelCompleteUI;
    [SerializeField] private bool pauseGameplayOnComplete = true;
    [SerializeField, Min(0f)] private float sceneLoadDelay = 0.2f;
    [Tooltip("Son parkurdan sonra ilk oynanabilir parkura dönmek için etkinleştirin.")]
    [SerializeField] private bool wrapToFirstScene;
    [Tooltip("Boş bırakılırsa sahnedeki VRGolfClub otomatik bulunur.")]
    [SerializeField] private VRGolfClub golfClub;

    private bool isOccupied;
    private bool isTransitioning;
    private bool ownsPause;
    private float timeScaleBeforePause = 1f;

    public bool IsOccupied => isOccupied;
    public bool IsTransitioning => isTransitioning;
    public MetaLevelTransitionUI LevelCompleteUI => levelCompleteUI;

    private void Awake()
    {
        GameManager.EnsureInstance();
        EnsureLevelCompleteUI();
    }

    private void Start()
    {
        levelCompleteUI.ShowHud(Camera.main);
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOccupied || !other.CompareTag("GolfBall")) return;

        BallController ballController = other.GetComponentInParent<BallController>();
        if (ballController == null) return;

        TrySink(ballController);
    }

    public bool TrySink(BallController ballController)
    {
        if (isOccupied || ballController == null) return false;

        isOccupied = true;
        EndClubInteraction();
        Vector3 restPosition = ballRestPoint != null
            ? ballRestPoint.position
            : transform.position + Vector3.down * sinkDepth;

        ballController.CompleteAndSink(restPosition, sinkDuration);
        Debug.Log("HOLE IN ONE! Top deliğe girdi!");

        if (cheerSound != null) cheerSound.Play();
        if (confettiEffect != null) confettiEffect.Play();

        EnsureLevelCompleteUI();
        ShowCompletionResult(ballController);
        PauseGameplay();
        return true;
    }

    private void EndClubInteraction()
    {
        if (golfClub == null)
        {
            golfClub = FindAnyObjectByType<VRGolfClub>();
        }

        if (golfClub != null)
        {
            golfClub.EndInteractionForLevelComplete();
        }
    }

    public void TryRestartGame()
    {
        if (isTransitioning || !isOccupied)
        {
            return;
        }

        int firstCourseBuildIndex = MiniGolfSceneCatalog.GetFirstCourseBuildIndex();
        if (firstCourseBuildIndex < 0)
        {
            Debug.LogWarning("Oyun yeniden başlatılamadı: oynanabilir parkur bulunamadı.", this);
            return;
        }

        BeginSceneLoad(firstCourseBuildIndex, true);
    }

    public void TryLoadNextScene()
    {
        if (isTransitioning || !isOccupied)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex < 0 || MiniGolfSceneCatalog.CourseCount == 0)
        {
            Debug.LogWarning(
                "Bölüm geçişi yapılamadı: aktif sahne Build Settings içinde etkin değil.",
                this);
            return;
        }

        if (!MiniGolfSceneCatalog.TryGetNextCourseBuildIndex(activeScene, out int nextSceneIndex))
        {
            if (!wrapToFirstScene)
            {
                Debug.Log("Oyun tamamlandı: Build Settings içinde başka bölüm yok.", this);
                return;
            }

            nextSceneIndex = MiniGolfSceneCatalog.GetFirstCourseBuildIndex();
            if (nextSceneIndex < 0)
            {
                Debug.LogWarning("Bölüm geçişi yapılamadı: oynanabilir parkur bulunamadı.", this);
                return;
            }
        }

        BeginSceneLoad(
            nextSceneIndex,
            nextSceneIndex == MiniGolfSceneCatalog.GetFirstCourseBuildIndex());
    }

    private void BeginSceneLoad(int targetSceneIndex, bool resetSessionBeforeActivation)
    {
        isTransitioning = true;
        levelCompleteUI.SetButtonInteractable(false);
        StartCoroutine(LoadSceneRoutine(targetSceneIndex, resetSessionBeforeActivation));
    }

    private IEnumerator LoadSceneRoutine(int targetSceneIndex, bool resetSessionBeforeActivation)
    {
        int sceneCount = SceneManager.sceneCountInBuildSettings;
        if (targetSceneIndex < 0 || targetSceneIndex >= sceneCount)
        {
            Debug.LogWarning(
                $"Bölüm geçişi yapılamadı: Build Settings indeksi {targetSceneIndex} geçersiz.",
                this);
            isTransitioning = false;
            levelCompleteUI.SetButtonInteractable(true);
            yield break;
        }

        if (sceneLoadDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(sceneLoadDelay);
        }

        RestoreTimeScale();
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetSceneIndex, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"Build Settings sahne indeksi {targetSceneIndex} yüklenemedi.", this);
            isTransitioning = false;
            levelCompleteUI.SetButtonInteractable(true);
            PauseGameplay();
            yield break;
        }

        if (resetSessionBeforeActivation)
        {
            loadOperation.allowSceneActivation = false;
            while (loadOperation.progress < 0.9f)
            {
                yield return null;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.BeginNewSession();
            }

            loadOperation.allowSceneActivation = true;
        }

        while (!loadOperation.isDone)
        {
            yield return null;
        }
    }

    private void EnsureLevelCompleteUI()
    {
        if (levelCompleteUI == null)
        {
            levelCompleteUI = MetaLevelTransitionUI.CreateRuntime();
        }

        levelCompleteUI.Bind(TryLoadNextScene);
    }

    private void ShowCompletionResult(BallController ballController)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        int courseCount = MiniGolfSceneCatalog.CourseCount;
        bool isFinalScene = MiniGolfSceneCatalog.IsFinalCourse(activeScene);

        GameManager manager = GameManager.Instance;
        int currentStrokes = manager != null
            ? manager.CurrentStrokeCount
            : ballController.StrokeCount;
        int totalStrokes = manager != null
            ? manager.TotalStrokeCount
            : currentStrokes;
        int finalScore = manager != null
            ? manager.SessionFinalScore
            : GameManager.CalculateFinalScore(totalStrokes);

        if (isFinalScene)
        {
            levelCompleteUI.Bind(TryRestartGame);
            int[] holeStrokes = manager != null
                ? manager.GetHoleStrokeSnapshot(courseCount)
                : new[] { -1, currentStrokes };
            levelCompleteUI.ShowFinalResult(
                Camera.main,
                holeStrokes,
                totalStrokes,
                finalScore);
            return;
        }

        levelCompleteUI.Bind(TryLoadNextScene);
        int courseIndex = MiniGolfSceneCatalog.GetCourseIndex(activeScene);
        int holeNumber = courseIndex >= 0 ? courseIndex + 1 : 1;
        levelCompleteUI.ShowHoleResult(
            Camera.main,
            holeNumber,
            currentStrokes,
            totalStrokes,
            finalScore);
    }

    private void PauseGameplay()
    {
        if (!pauseGameplayOnComplete || ownsPause)
        {
            return;
        }

        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;
        ownsPause = true;
    }

    private void RestoreTimeScale()
    {
        if (!ownsPause)
        {
            return;
        }

        Time.timeScale = timeScaleBeforePause;
        ownsPause = false;
    }
}
