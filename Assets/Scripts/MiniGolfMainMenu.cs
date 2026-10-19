using System.Collections;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class MiniGolfMainMenu : MonoBehaviour
{
    private static readonly Vector2 CanvasSize = new Vector2(860f, 620f);
    private const string DefaultStatus = "Parkurunu seç ve oyuna başla";

    [Header("Menü")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Text statusText;
    [SerializeField] private Button firstCourseButton;
    [SerializeField] private Button secondCourseButton;
    [SerializeField] private Button quitButton;

    [Header("Yükleme Ekranı")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text loadingStatusText;
    [SerializeField] private Text loadingPercentText;
    [SerializeField] private Image loadingProgressFill;
    [SerializeField, Min(0f)] private float minimumLoadingScreenDuration = 0.75f;

    [Header("VR Canvas Takibi")]
    [SerializeField] private Camera viewerCamera;
    [SerializeField, Min(0.5f)] private float followDistance = 1.55f;
    [SerializeField] private float verticalOffset = -0.04f;
    [SerializeField, Min(0.01f)] private float followSpeed = 7f;

    private bool initialized;
    private bool isLoading;
    private float currentProgress;
    private string targetScenePath;

    public Canvas Canvas => canvas;
    public Button FirstCourseButton => firstCourseButton;
    public Button SecondCourseButton => secondCourseButton;
    public Button QuitButton => quitButton;
    public GameObject MainPanel => mainPanel;
    public GameObject LoadingPanel => loadingPanel;
    public bool IsLoading => isLoading;
    public float CurrentProgress => currentProgress;
    public string TargetScenePath => targetScenePath;

    private void Awake()
    {
        EnsureInitialized();
        ShowMainMenu();
        PositionForViewer(immediate: true);
    }

    private void LateUpdate()
    {
        PositionForViewer(immediate: false);
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void RebuildForEditor()
    {
        EnsureInitialized();
        ShowMainMenu();
        PositionForViewer(immediate: true);
    }

    public void StartFirstCourse()
    {
        BeginLoading(MiniGolfSceneCatalog.FirstCourseScenePath);
    }

    public void StartSecondCourse()
    {
        BeginLoading(MiniGolfSceneCatalog.SecondCourseScenePath);
    }

    public void QuitGame()
    {
        if (isLoading) return;

        Debug.Log("Mini golf çıkış düğmesine basıldı.", this);
        Application.Quit();
    }

    private void EnsureInitialized()
    {
        canvas = GetComponent<Canvas>();
        ConfigureCanvas();

        if (mainPanel == null
            || statusText == null
            || firstCourseButton == null
            || secondCourseButton == null
            || quitButton == null
            || loadingPanel == null
            || loadingStatusText == null
            || loadingPercentText == null
            || loadingProgressFill == null)
        {
            CreateDefaultUI();
        }

        ConfigureMetaRayInteraction();
        EnsureMetaEventSystem();
        BindButtons();
        initialized = true;
    }

    private void ConfigureCanvas()
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 200;

        RectTransform canvasRect = (RectTransform)transform;
        canvasRect.sizeDelta = CanvasSize;
        canvasRect.localScale = Vector3.one * 0.00155f;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) SetLayerRecursively(gameObject, uiLayer);
    }

    private void CreateDefaultUI()
    {
        for (int childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
        {
            GameObject child = transform.GetChild(childIndex).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        mainPanel = CreatePanel("Ana Menü", transform, new Color(0.025f, 0.07f, 0.085f, 0.98f));
        Stretch((RectTransform)mainPanel.transform, Vector2.zero, Vector2.one);

        Image accent = CreatePanel("Üst Şerit", mainPanel.transform, new Color(0.13f, 0.72f, 0.48f, 1f))
            .GetComponent<Image>();
        SetAnchors((RectTransform)accent.transform, new Vector2(0f, 0.965f), Vector2.one);

        Text title = CreateText("Başlık", mainPanel.transform, font, 66, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetAnchors((RectTransform)title.transform, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.95f));
        title.text = "VR MINI GOLF";
        title.color = new Color(0.9f, 1f, 0.96f, 1f);

        statusText = CreateText("Durum", mainPanel.transform, font, 29, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetAnchors((RectTransform)statusText.transform, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.79f));
        statusText.text = DefaultStatus;
        statusText.color = new Color(0.65f, 0.82f, 0.78f, 1f);

        firstCourseButton = CreateButton(
            "Başlangıç Parkuru Button",
            mainPanel.transform,
            font,
            "BAŞLANGIÇ PARKURU",
            new Color(0.12f, 0.62f, 0.4f, 1f));
        SetAnchors(
            (RectTransform)firstCourseButton.transform,
            new Vector2(0.16f, 0.46f),
            new Vector2(0.84f, 0.62f));

        secondCourseButton = CreateButton(
            "Zorlu Parkur Button",
            mainPanel.transform,
            font,
            "ZORLU PARKUR",
            new Color(0.11f, 0.42f, 0.62f, 1f));
        SetAnchors(
            (RectTransform)secondCourseButton.transform,
            new Vector2(0.16f, 0.27f),
            new Vector2(0.84f, 0.43f));

        quitButton = CreateButton(
            "Çıkış Button",
            mainPanel.transform,
            font,
            "ÇIKIŞ",
            new Color(0.23f, 0.28f, 0.3f, 1f));
        SetAnchors(
            (RectTransform)quitButton.transform,
            new Vector2(0.31f, 0.105f),
            new Vector2(0.69f, 0.225f));

        Text hint = CreateText("Simulator İpucu", mainPanel.transform, font, 21, FontStyle.Normal, TextAnchor.MiddleCenter);
        SetAnchors((RectTransform)hint.transform, new Vector2(0.06f, 0.015f), new Vector2(0.94f, 0.09f));
        hint.text = "Meta XR Simulator: kontrolcü ışınını düğmeye getirip T ile seç";
        hint.color = new Color(0.55f, 0.65f, 0.67f, 1f);

        loadingPanel = CreatePanel("Yükleme Ekranı", transform, new Color(0.018f, 0.052f, 0.064f, 1f));
        Stretch((RectTransform)loadingPanel.transform, Vector2.zero, Vector2.one);

        loadingStatusText = CreateText(
            "Yükleme Durumu",
            loadingPanel.transform,
            font,
            48,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        SetAnchors(
            (RectTransform)loadingStatusText.transform,
            new Vector2(0.08f, 0.57f),
            new Vector2(0.92f, 0.75f));
        loadingStatusText.text = "PARKUR HAZIRLANIYOR";
        loadingStatusText.color = Color.white;

        GameObject progressTrack = CreatePanel(
            "İlerleme Çubuğu",
            loadingPanel.transform,
            new Color(0.11f, 0.19f, 0.21f, 1f));
        SetAnchors(
            (RectTransform)progressTrack.transform,
            new Vector2(0.14f, 0.45f),
            new Vector2(0.86f, 0.51f));

        GameObject progressFillObject = CreatePanel(
            "İlerleme Dolgusu",
            progressTrack.transform,
            new Color(0.13f, 0.72f, 0.48f, 1f));
        Stretch((RectTransform)progressFillObject.transform, Vector2.zero, Vector2.one, new Vector2(5f, 5f));
        loadingProgressFill = progressFillObject.GetComponent<Image>();
        loadingProgressFill.type = Image.Type.Filled;
        loadingProgressFill.fillMethod = Image.FillMethod.Horizontal;
        loadingProgressFill.fillOrigin = 0;
        loadingProgressFill.fillAmount = 0f;

        loadingPercentText = CreateText(
            "Yükleme Yüzdesi",
            loadingPanel.transform,
            font,
            28,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        SetAnchors(
            (RectTransform)loadingPercentText.transform,
            new Vector2(0.25f, 0.35f),
            new Vector2(0.75f, 0.44f));
        loadingPercentText.text = "%0";
        loadingPercentText.color = new Color(0.73f, 0.9f, 0.84f, 1f);

        Text loadingHint = CreateText(
            "Yükleme İpucu",
            loadingPanel.transform,
            font,
            24,
            FontStyle.Normal,
            TextAnchor.MiddleCenter);
        SetAnchors(
            (RectTransform)loadingHint.transform,
            new Vector2(0.08f, 0.17f),
            new Vector2(0.92f, 0.31f));
        loadingHint.text = "İpucu: Sopayı tutmak için Grip (U), vuruş için Trigger (T) kullan.";
        loadingHint.color = new Color(0.58f, 0.7f, 0.72f, 1f);

        loadingPanel.SetActive(false);
    }

    private void BindButtons()
    {
        UnbindButtons();
        firstCourseButton.onClick.AddListener(StartFirstCourse);
        secondCourseButton.onClick.AddListener(StartSecondCourse);
        quitButton.onClick.AddListener(QuitGame);
    }

    private void UnbindButtons()
    {
        if (firstCourseButton != null) firstCourseButton.onClick.RemoveListener(StartFirstCourse);
        if (secondCourseButton != null) secondCourseButton.onClick.RemoveListener(StartSecondCourse);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
    }

    private void BeginLoading(string scenePath)
    {
        if (isLoading) return;

        int buildIndex = MiniGolfSceneCatalog.GetBuildIndex(scenePath);
        if (buildIndex < 0)
        {
            string message = $"Parkur Build Settings içinde bulunamadı: {scenePath}";
            Debug.LogError(message, this);
            statusText.text = "Parkur bulunamadı. Build Settings'i kontrol et.";
            return;
        }

        targetScenePath = scenePath;
        StartCoroutine(LoadSceneRoutine(buildIndex));
    }

    private IEnumerator LoadSceneRoutine(int buildIndex)
    {
        isLoading = true;
        currentProgress = 0f;
        mainPanel.SetActive(false);
        loadingPanel.SetActive(true);
        UpdateProgress(0f);

        yield return null;

        float visibleSince = Time.realtimeSinceStartup;
        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        if (operation == null)
        {
            ShowLoadFailure($"Build Settings indeksi {buildIndex} yüklenemedi.");
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            UpdateProgress(Mathf.Clamp01(operation.progress / 0.9f));
            yield return null;
        }

        UpdateProgress(1f);
        while (Time.realtimeSinceStartup - visibleSince < minimumLoadingScreenDuration)
        {
            yield return null;
        }

        Time.timeScale = 1f;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.BeginNewSession();
        }

        operation.allowSceneActivation = true;
    }

    private void ShowLoadFailure(string message)
    {
        Debug.LogError(message, this);
        isLoading = false;
        targetScenePath = string.Empty;
        loadingPanel.SetActive(false);
        mainPanel.SetActive(true);
        statusText.text = "Yükleme başarısız. Unity Console'u kontrol et.";
    }

    private void ShowMainMenu()
    {
        isLoading = false;
        currentProgress = 0f;
        targetScenePath = string.Empty;
        mainPanel.SetActive(true);
        loadingPanel.SetActive(false);
        statusText.text = DefaultStatus;
        UpdateProgress(0f);
    }

    private void UpdateProgress(float progress)
    {
        currentProgress = Mathf.Clamp01(progress);
        loadingProgressFill.fillAmount = currentProgress;
        loadingPercentText.text = $"%{Mathf.RoundToInt(currentProgress * 100f)}";
    }

    private void PositionForViewer(bool immediate)
    {
        Camera activeViewer = ResolveViewerCamera();
        if (activeViewer == null) return;

        Vector3 flatForward = Vector3.ProjectOnPlane(activeViewer.transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 targetPosition = activeViewer.transform.position
            + flatForward * followDistance
            + Vector3.up * verticalOffset;
        Quaternion targetRotation = Quaternion.LookRotation(
            targetPosition - activeViewer.transform.position,
            Vector3.up);

        if (immediate || !initialized)
        {
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            return;
        }

        float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
    }

    private Camera ResolveViewerCamera()
    {
        if (viewerCamera != null && viewerCamera.isActiveAndEnabled) return viewerCamera;

        viewerCamera = Camera.main;
        if (viewerCamera == null) viewerCamera = FindAnyObjectByType<Camera>();
        return viewerCamera;
    }

    private void ConfigureMetaRayInteraction()
    {
        PlaneSurface planeSurface = GetOrAddComponent<PlaneSurface>();
        planeSurface.Facing = PlaneSurface.NormalFacing.Backward;
        planeSurface.DoubleSided = false;

        BoundsClipper boundsClipper = GetOrAddComponent<BoundsClipper>();
        boundsClipper.Position = Vector3.zero;
        boundsClipper.Size = new Vector3(CanvasSize.x, CanvasSize.y, 0.01f);

        ClippedPlaneSurface clippedSurface = GetOrAddComponent<ClippedPlaneSurface>();
        clippedSurface.InjectAllClippedPlaneSurface(planeSurface, new[] { boundsClipper });

        PointableCanvas pointableCanvas = GetOrAddComponent<PointableCanvas>();
        pointableCanvas.InjectAllPointableCanvas(canvas);

        RayInteractable rayInteractable = GetOrAddComponent<RayInteractable>();
        rayInteractable.InjectAllRayInteractable(clippedSurface);
        rayInteractable.InjectOptionalSelectSurface(planeSurface);
        rayInteractable.InjectOptionalPointableElement(pointableCanvas);
    }

    private static void EnsureMetaEventSystem()
    {
        PointableCanvasModule module = FindAnyObjectByType<PointableCanvasModule>(FindObjectsInactive.Exclude);
        if (module != null
            && module.TryGetComponent(out EventSystem moduleEventSystem)
            && moduleEventSystem.isActiveAndEnabled)
        {
            module.enabled = true;
            module.ExclusiveMode = true;
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null || !eventSystem.isActiveAndEnabled)
        {
            eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Exclude);
        }

        GameObject eventSystemObject = eventSystem != null
            ? eventSystem.gameObject
            : new GameObject("Meta UI EventSystem", typeof(EventSystem));

        module = eventSystemObject.GetComponent<PointableCanvasModule>();
        if (module == null)
        {
            module = eventSystemObject.AddComponent<PointableCanvasModule>();
        }

        module.enabled = true;
        module.ExclusiveMode = true;
    }

    private static GameObject CreatePanel(string objectName, Transform parent, Color color)
    {
        GameObject panel = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        panel.transform.SetParent(parent, false);
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return panel;
    }

    private static Text CreateText(
        string objectName,
        Transform parent,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string objectName,
        Transform parent,
        Font font,
        string label,
        Color normalColor)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.colors = new ColorBlock
        {
            normalColor = normalColor,
            highlightedColor = Color.Lerp(normalColor, Color.white, 0.16f),
            pressedColor = Color.Lerp(normalColor, Color.black, 0.22f),
            selectedColor = normalColor,
            disabledColor = new Color(0.25f, 0.28f, 0.3f, 0.7f),
            colorMultiplier = 1f,
            fadeDuration = 0.08f
        };

        Text buttonLabel = CreateText(
            "Text",
            buttonObject.transform,
            font,
            31,
            FontStyle.Bold,
            TextAnchor.MiddleCenter);
        Stretch((RectTransform)buttonLabel.transform, Vector2.zero, Vector2.one, new Vector2(18f, 8f));
        buttonLabel.text = label;
        buttonLabel.color = Color.white;
        buttonLabel.resizeTextForBestFit = true;
        buttonLabel.resizeTextMinSize = 20;
        buttonLabel.resizeTextMaxSize = 31;
        return button;
    }

    private static void Stretch(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 padding = default)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
    }

    private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        Stretch(rect, anchorMin, anchorMax);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
