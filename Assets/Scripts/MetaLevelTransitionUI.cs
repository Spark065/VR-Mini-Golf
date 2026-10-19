using System;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class MetaLevelTransitionUI : MonoBehaviour
{
    private const string NextLevelButtonLabel = "Bir Sonraki Bölüme Geç";
    private const string RestartButtonLabel = "Yeniden Başlat";
    private static readonly Vector2 CanvasSize = new Vector2(720f, 520f);

    [SerializeField] private Canvas canvas;
    [SerializeField] private Text liveStrokeText;
    [SerializeField] private Text resultStrokeText;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Text actionButtonLabel;

    [Header("VR Canvas Takibi")]
    [Tooltip("Boş bırakılırsa Main Camera veya sahnedeki ilk aktif Camera otomatik bulunur.")]
    [SerializeField] private Camera viewerCamera;
    [SerializeField] private bool followViewer = true;
    [SerializeField, Min(0.5f)] private float followDistance = 1.35f;
    [Tooltip("Oyuncunun bakış yönüne göre sağ/sol ofset (metre).")]
    [SerializeField] private float horizontalOffset;
    [Tooltip("Göz hizasına göre dikey ofset (metre). Negatif değer Canvas'ı aşağı indirir.")]
    [SerializeField] private float verticalOffset = -0.08f;
    [SerializeField, Min(0.01f)] private float positionFollowSpeed = 4f;
    [SerializeField, Min(0.01f)] private float rotationFollowSpeed = 5f;
    [Tooltip("Canvas bu yatay görüş açısının dışına çıkana kadar yerinde kalır. Bu ölü bölge küçük baş hareketlerinde titremeyi önler.")]
    [SerializeField, Range(5f, 90f)] private float maximumViewAngle = 35f;
    [Tooltip("Oyuncu yürüdüğünde Canvas hedef konumdan bu kadar uzaklaşınca yumuşak takip başlar (metre).")]
    [SerializeField, Min(0.05f)] private float maximumPositionLag = 0.35f;

    private Action buttonPressed;
    private bool isInitialized;
    private bool isRecentering;
    private bool missingCameraWarningLogged;
    private int displayedStrokeCount = -1;

    public Canvas Canvas => canvas;
    public Button NextLevelButton => nextLevelButton;

    public static MetaLevelTransitionUI CreateRuntime()
    {
        GameObject uiObject = new GameObject(
            "Mini Golf World Space UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        MetaLevelTransitionUI transitionUI = uiObject.AddComponent<MetaLevelTransitionUI>();
        transitionUI.EnsureInitialized();
        return transitionUI;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        int currentStrokeCount = GameManager.Instance.CurrentStrokeCount;
        if (displayedStrokeCount != currentStrokeCount)
        {
            SetStrokeCount(currentStrokeCount);
        }
    }

    private void LateUpdate()
    {
        if (!followViewer) return;

        Camera activeViewer = ResolveViewerCamera();
        if (activeViewer == null) return;

        GetDesiredViewerPose(activeViewer.transform, out Vector3 targetPosition, out Quaternion targetRotation);

        Vector3 flatForward = GetFlatForward(activeViewer.transform);
        Vector3 flatDirectionToCanvas = Vector3.ProjectOnPlane(
            transform.position - activeViewer.transform.position,
            Vector3.up);
        float viewAngle = flatDirectionToCanvas.sqrMagnitude > 0.0001f
            ? Vector3.Angle(flatForward, flatDirectionToCanvas.normalized)
            : 180f;
        float positionLag = Vector3.Distance(transform.position, targetPosition);
        float rotationLag = Quaternion.Angle(transform.rotation, targetRotation);

        if (!isRecentering
            && (viewAngle > maximumViewAngle
                || positionLag > maximumPositionLag
                || rotationLag > maximumViewAngle))
        {
            isRecentering = true;
        }

        if (!isRecentering) return;

        float deltaTime = Time.unscaledDeltaTime;
        float positionT = 1f - Mathf.Exp(-positionFollowSpeed * deltaTime);
        float rotationT = 1f - Mathf.Exp(-rotationFollowSpeed * deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionT);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationT);

        if (Vector3.Distance(transform.position, targetPosition) < 0.02f
            && Quaternion.Angle(transform.rotation, targetRotation) < 1f
            && viewAngle < maximumViewAngle * 0.65f)
        {
            isRecentering = false;
        }
    }

    private void OnDestroy()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveListener(HandleButtonClicked);
        }
    }

    public void Bind(Action onButtonPressed)
    {
        EnsureInitialized();
        buttonPressed = onButtonPressed;

        nextLevelButton.onClick.RemoveListener(HandleButtonClicked);
        nextLevelButton.onClick.AddListener(HandleButtonClicked);
    }

    public void ShowHud(Camera viewerCamera)
    {
        EnsureInitialized();
        PositionForViewer(viewerCamera);
        gameObject.SetActive(true);
        resultPanel.SetActive(false);
        SetStrokeCount(GameManager.Instance != null ? GameManager.Instance.CurrentStrokeCount : 0);
    }

    public void ShowHoleResult(
        Camera viewerCamera,
        int holeNumber,
        int holeStrokeCount,
        int totalStrokeCount,
        int finalScore)
    {
        EnsureInitialized();
        PositionForViewer(viewerCamera);
        SetStrokeCount(holeStrokeCount);
        resultStrokeText.text =
            $"{Mathf.Max(1, holeNumber)}. Delik Tamamlandı\n"
            + $"Delik Vuruşu: {Mathf.Max(0, holeStrokeCount)}\n"
            + $"Toplam Vuruş: {Mathf.Max(0, totalStrokeCount)}\n"
            + $"Skor: {Mathf.Max(0, finalScore)}";
        SetButtonLabel(NextLevelButtonLabel);
        nextLevelButton.interactable = true;
        gameObject.SetActive(true);
        resultPanel.SetActive(true);
    }

    public void ShowFinalResult(
        Camera viewerCamera,
        int[] holeStrokeCounts,
        int totalStrokeCount,
        int finalScore)
    {
        EnsureInitialized();
        PositionForViewer(viewerCamera);

        StringBuilder summary = new StringBuilder("FİNAL SONUCU");
        if (holeStrokeCounts != null)
        {
            for (int i = 0; i < holeStrokeCounts.Length; i++)
            {
                string strokes = holeStrokeCounts[i] >= 0
                    ? holeStrokeCounts[i].ToString()
                    : "—";
                summary.Append($"\n{i + 1}. Delik: {strokes} vuruş");
            }
        }

        summary.Append($"\nToplam Vuruş: {Mathf.Max(0, totalStrokeCount)}");
        summary.Append($"\nFinal Skor: {Mathf.Max(0, finalScore)}");

        resultStrokeText.text = summary.ToString();
        SetButtonLabel(RestartButtonLabel);
        nextLevelButton.interactable = true;
        gameObject.SetActive(true);
        resultPanel.SetActive(true);
    }

    public void SetButtonInteractable(bool interactable)
    {
        EnsureInitialized();
        nextLevelButton.interactable = interactable;
    }

    private void SetStrokeCount(int strokeCount)
    {
        displayedStrokeCount = Mathf.Max(0, strokeCount);
        liveStrokeText.text = $"Vuruş: {displayedStrokeCount}";
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;

        canvas = GetComponent<Canvas>();
        ConfigureCanvas();
        CreateDefaultUI();
        ConfigureMetaRayInteraction();
        EnsureMetaEventSystem();
        isInitialized = true;
    }

    private void ConfigureCanvas()
    {
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        RectTransform canvasRect = (RectTransform)transform;
        canvasRect.sizeDelta = CanvasSize;
        canvasRect.localScale = Vector3.one * 0.0015f;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 100f;

        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0) gameObject.layer = uiLayer;
    }

    private void CreateDefaultUI()
    {
        if (liveStrokeText != null
            && resultStrokeText != null
            && resultPanel != null
            && nextLevelButton != null)
        {
            actionButtonLabel = nextLevelButton.GetComponentInChildren<Text>(true);
            ConfigureResultLayout();
            return;
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        liveStrokeText = CreateText("Vuruş Sayısı", transform, font, 42, TextAnchor.MiddleCenter);
        RectTransform liveRect = (RectTransform)liveStrokeText.transform;
        liveRect.anchorMin = new Vector2(0.18f, 0.76f);
        liveRect.anchorMax = new Vector2(0.82f, 1f);
        liveRect.offsetMin = Vector2.zero;
        liveRect.offsetMax = Vector2.zero;
        liveStrokeText.text = "Vuruş: 0";
        liveStrokeText.color = Color.white;

        resultPanel = new GameObject(
            "Sonuç Paneli",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        resultPanel.transform.SetParent(transform, false);
        resultPanel.layer = gameObject.layer;

        RectTransform panelRect = (RectTransform)resultPanel.transform;
        panelRect.anchorMin = new Vector2(0.05f, 0f);
        panelRect.anchorMax = new Vector2(0.95f, 0.74f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        resultPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

        resultStrokeText = CreateText(
            "Toplam Vuruş",
            resultPanel.transform,
            font,
            40,
            TextAnchor.MiddleCenter);
        RectTransform resultTextRect = (RectTransform)resultStrokeText.transform;
        resultTextRect.anchorMin = new Vector2(0.08f, 0.56f);
        resultTextRect.anchorMax = new Vector2(0.92f, 0.94f);
        resultTextRect.offsetMin = Vector2.zero;
        resultTextRect.offsetMax = Vector2.zero;
        resultStrokeText.text = "Toplam Vuruş: 0";
        resultStrokeText.color = Color.white;

        GameObject buttonObject = new GameObject(
            "Bir Sonraki Bölüme Geç Button",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(resultPanel.transform, false);
        buttonObject.layer = gameObject.layer;

        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = new Vector2(0.06f, 0.08f);
        buttonRect.anchorMax = new Vector2(0.94f, 0.54f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = Color.white;
        nextLevelButton = buttonObject.GetComponent<Button>();
        nextLevelButton.targetGraphic = buttonImage;

        Text label = CreateText("Text", buttonObject.transform, font, 36, TextAnchor.MiddleCenter);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 4f);
        labelRect.offsetMax = new Vector2(-12f, -4f);
        label.text = NextLevelButtonLabel;
        label.color = new Color(0.196f, 0.196f, 0.196f, 1f);
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 20;
        label.resizeTextMaxSize = 36;
        actionButtonLabel = label;

        ConfigureResultLayout();
        resultPanel.SetActive(false);
    }

    private void ConfigureResultLayout()
    {
        RectTransform liveRect = (RectTransform)liveStrokeText.transform;
        liveRect.anchorMin = new Vector2(0.18f, 0.84f);
        liveRect.anchorMax = new Vector2(0.82f, 1f);

        RectTransform panelRect = (RectTransform)resultPanel.transform;
        panelRect.anchorMin = new Vector2(0.05f, 0f);
        panelRect.anchorMax = new Vector2(0.95f, 0.82f);

        RectTransform resultTextRect = (RectTransform)resultStrokeText.transform;
        resultTextRect.anchorMin = new Vector2(0.08f, 0.27f);
        resultTextRect.anchorMax = new Vector2(0.92f, 0.96f);
        resultTextRect.offsetMin = Vector2.zero;
        resultTextRect.offsetMax = Vector2.zero;
        resultStrokeText.fontSize = 34;
        resultStrokeText.resizeTextForBestFit = true;
        resultStrokeText.resizeTextMinSize = 22;
        resultStrokeText.resizeTextMaxSize = 34;

        RectTransform buttonRect = (RectTransform)nextLevelButton.transform;
        buttonRect.anchorMin = new Vector2(0.08f, 0.05f);
        buttonRect.anchorMax = new Vector2(0.92f, 0.24f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
    }

    private void SetButtonLabel(string label)
    {
        if (actionButtonLabel == null && nextLevelButton != null)
        {
            actionButtonLabel = nextLevelButton.GetComponentInChildren<Text>(true);
        }

        if (actionButtonLabel != null)
        {
            actionButtonLabel.text = label;
        }
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        Font font,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);
        textObject.layer = gameObject.layer;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
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
        PointableCanvasModule existingModule = FindAnyObjectByType<PointableCanvasModule>(FindObjectsInactive.Exclude);
        if (existingModule != null)
        {
            existingModule.ExclusiveMode = true;
            return;
        }

        EventSystem eventSystem = FindAnyObjectByType<EventSystem>(FindObjectsInactive.Exclude);
        GameObject eventSystemObject = eventSystem != null
            ? eventSystem.gameObject
            : new GameObject("Meta UI EventSystem", typeof(EventSystem));

        PointableCanvasModule module = eventSystemObject.AddComponent<PointableCanvasModule>();
        module.ExclusiveMode = true;
    }

    private void PositionForViewer(Camera viewerCamera)
    {
        if (viewerCamera != null)
        {
            this.viewerCamera = viewerCamera;
        }

        Camera activeViewer = ResolveViewerCamera();
        if (activeViewer == null)
        {
            return;
        }

        GetDesiredViewerPose(activeViewer.transform, out Vector3 targetPosition, out Quaternion targetRotation);
        transform.SetPositionAndRotation(targetPosition, targetRotation);
        isRecentering = false;
    }

    private Camera ResolveViewerCamera()
    {
        if (viewerCamera != null && viewerCamera.isActiveAndEnabled)
        {
            return viewerCamera;
        }

        viewerCamera = Camera.main;
        if (viewerCamera == null)
        {
            viewerCamera = FindAnyObjectByType<Camera>();
        }

        if (viewerCamera == null && !missingCameraWarningLogged)
        {
            missingCameraWarningLogged = true;
            Debug.LogWarning("Mini golf World Space Canvas oyuncu kamerasını bulamadı.", this);
        }

        return viewerCamera;
    }

    private void GetDesiredViewerPose(
        Transform viewer,
        out Vector3 targetPosition,
        out Quaternion targetRotation)
    {
        Vector3 flatForward = GetFlatForward(viewer);
        Vector3 flatRight = Vector3.Cross(Vector3.up, flatForward).normalized;

        targetPosition = viewer.position
            + flatForward * followDistance
            + flatRight * horizontalOffset
            + Vector3.up * verticalOffset;

        Vector3 facingDirection = targetPosition - viewer.position;
        if (facingDirection.sqrMagnitude < 0.0001f)
        {
            facingDirection = flatForward;
        }

        targetRotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
    }

    private static Vector3 GetFlatForward(Transform viewer)
    {
        Vector3 flatForward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.ProjectOnPlane(viewer.up, Vector3.up);
        }

        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }

        return flatForward.normalized;
    }

    private T GetOrAddComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private void HandleButtonClicked()
    {
        if (nextLevelButton == null || !nextLevelButton.interactable) return;
        buttonPressed?.Invoke();
    }
}
