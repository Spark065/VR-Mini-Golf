using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEngine;
using UnityEngine.UI;

public class VRGolfClub : MonoBehaviour
{
    [Header("Vuruş Yüzeyleri")]
    [Tooltip("Topa vurabilen Box Collider'lar. İki putter yüzünü de buraya ekleyin. Boşsa isimlerinde head/face/hit bulunan kutular otomatik seçilir.")]
    [SerializeField] private BoxCollider[] strikingColliders;
    [Tooltip("Vuruş hızı bu collider'ın merkezinden ölçülür. Boşsa vuruş yüzeylerinin ortalaması kullanılır.")]
    [SerializeField] private Collider clubHeadCollider;
    [Tooltip("Fizik motorunun rastgele ek impuls vermemesi için vuruş yüzeylerini çalışma anında Trigger yapar.")]
    [SerializeField] private bool useTriggerHitboxes = true;
    [Tooltip("Sweep sorgusunun yalnızca top katmanlarını taraması için maske. Özel GolfBall katmanı kullanılması önerilir.")]
    [SerializeField] private LayerMask golfBallLayers = ~0;
    [Tooltip("Hızlı dönmede kutu sweep'inin açısal alt adım büyüklüğü.")]
    [SerializeField, Range(2f, 30f)] private float maximumSweepAngleStep = 8f;
    [Tooltip("Sweep kutusuna eklenen küçük güvenlik payı (metre).")]
    [SerializeField, Range(0f, 0.03f)] private float sweepSkin = 0.01f;
    [Tooltip("Açıksa yalnızca grab sistemi sopayı tutuluyor olarak bildirdiğinde vuruş yapılır. Farklı XR grab sistemleri birlikte kullanılıyorsa kapalı bırakın.")]
    [SerializeField] private bool requireClubHeldForStroke;

    [Header("Mini Golf Vuruş Kalibrasyonu")]
    [Tooltip("Bu hızın altındaki temaslar vuruş sayılmaz (m/s).")]
    [SerializeField, Min(0f)] private float minimumClubHeadSpeed = 0.1f;
    [Tooltip("Bu sopa başı hızında tam güçlü putt oluşur (m/s).")]
    [SerializeField, Min(0.01f)] private float clubHeadSpeedForFullPutt = 2f;
    [Tooltip("En hafif geçerli vuruşta topun çıkış hızı (m/s).")]
    [SerializeField, Min(0f)] private float minimumBallLaunchSpeed = 0.25f;
    [Tooltip("Tam putt için top çıkış hızı (m/s). BallController.maximumLaunchSpeed değerini aşmamalı.")]
    [SerializeField, Min(0.1f)] private float maximumBallLaunchSpeed = 3.6f;
    [Tooltip("Düşük güçte hassasiyet için putt tepki eğrisi.")]
    [SerializeField, Range(1f, 3f)] private float puttResponseExponent = 1.5f;
    [Tooltip("Kontrolcü takip gürültüsünü süzer; 0,04-0,08 s mini golf için uygundur.")]
    [SerializeField, Range(0.01f, 0.15f)] private float swingSmoothingTime = 0.06f;
    [Tooltip("Mini golf kuralı: bu hızdan hızlı giden topa yeni vuruş uygulanmaz (m/s).")]
    [SerializeField, Min(0f)] private float maximumBallSpeedForStroke = 0.12f;
    [Tooltip("Sopa topa doğru ilerlemiyorsa teması reddeder. 0 yalnızca geri çekilme vuruşlarını engeller.")]
    [SerializeField, Range(-0.25f, 0.75f)] private float minimumApproachDot = 0.05f;
    [SerializeField, Min(0f)] private float hitCooldown = 0.2f;

    [Header("VR Power Bar")]
    [Tooltip("Yalnızca bu Canvas yönetilir. Render Mode = World Space olmalıdır.")]
    [SerializeField] private Canvas powerBarCanvas;
    [Tooltip("chargebar2 sprite'ını gösteren Image.")]
    [SerializeField] private Image powerBarImage;
    [Tooltip("Assets/Prefabs/chargebar2/charge bar.png (boş çerçeve).")]
    [SerializeField] private Sprite emptyPowerBarFrame;
    [Tooltip("Düşükten yükseğe: Folder 10013, 10012, ... Folder 10001.")]
    [SerializeField] private Sprite[] powerBarFrames = new Sprite[0];
    [Tooltip("Vuruştan sonra göstergenin sıfıra dönme gecikmesi (gerçek zaman, saniye).")]
    [SerializeField, Min(0f)] private float powerBarResetDelay = 0.15f;
    [Tooltip("Boş bırakılırsa sahnedeki ilk BallController çalışma anında bulunur.")]
    [SerializeField] private BallController targetBall;

    [Header("Titreşim (Haptic)")]
    [SerializeField, Range(0f, 1f)] private float maxHapticIntensity = 0.8f;
    [SerializeField, Min(0f)] private float hapticDuration = 0.1f;

    [Header("Meta Grab ve Güvenli Bekleme")]
    [Tooltip("Açıksa Meta Grabbable yalnızca tek seçim noktası kabul eder. İki elle kullanım için kapalı bırakın.")]
    [SerializeField] private bool limitToSingleMetaGrab;
    [Tooltip("Tek el sınırı açıkken ikinci seçim kontrolü ilk seçimden devralır. İki elle kullanımda uygulanmaz.")]
    [SerializeField] private bool transferOnSecondMetaGrab;
    [Tooltip("Sopanın uzaktan Distance Grab ile seçilmesini sağlar. UI Ray'in sopa tutulduğunda kısalmaması için kapalı bırakın; normal controller/hand grab çalışmaya devam eder.")]
    [SerializeField] private bool enableControllerDistanceGrab;
    [SerializeField] private bool enforceRealPutterLength = true;
    [SerializeField, Range(0.85f, 0.95f)] private float targetPutterLength = 0.90f;
    [Tooltip("Başlangıçta ve bırakıldığında sopayı bırakıldığı dünya pozunda asılı tutar.")]
    [SerializeField] private bool suspendWhenNotHeld = true;
    [Tooltip("Dinamik fizik fallback'i için putter kütlesi. Meta tutulurken Rigidbody kinematik olduğu için el takibini ağırlaştırmaz.")]
    [SerializeField, Min(0.01f)] private float clubMass = 0.45f;
    [SerializeField, Min(0f)] private float clubLinearDamping;
    [SerializeField, Min(0f)] private float clubAngularDamping = 0.05f;
    [Tooltip("Meta grab transformer ile aynı Transform'u her karede hareket ettiren zemin düzeltmesini açar. Meta Building Blocks ile doğal el takibi için kapalı tutun.")]
    [SerializeField] private bool clampHeldClubAboveGround;
    [Tooltip("Tutulan sopanın collider sınırını zeminin üzerinde tutan küçük mesafe (metre).")]
    [SerializeField, Min(0f)] private float groundClearance = 0.004f;
    [SerializeField, Min(0.05f)] private float groundProbeHeight = 0.6f;
    [SerializeField] private LayerMask clubGroundLayers = ~0;

    private const int QueryBufferSize = 16;

    private readonly RaycastHit[] castHits = new RaycastHit[QueryBufferSize];
    private readonly Collider[] overlapHits = new Collider[QueryBufferSize];
    private readonly RaycastHit[] groundHits = new RaycastHit[QueryBufferSize];

    private OVRGrabbable ovrGrabbable;
    private Grabbable metaGrabbable;
    private Rigidbody clubRigidbody;
    private Rigidbody metaGrabRigidbody;
    private Transform grabMotionRoot;
    private Collider[] clubColliders;
    private BoxPose[] previousBoxPoses;
    private BoxPose[] currentBoxPoses;
    private Vector3 previousSamplePosition;
    private Vector3 sampledSwingVelocity;
    private float previousSampleTime;
    private float lastHitTime = float.NegativeInfinity;
    private float powerBarResetTime;
    private float nextBallLookupTime;
    private bool hasPreviousBoxPoses;
    private bool wasHeld;
    private bool powerBarResetPending;
    private bool powerBarConfigurationWarningLogged;
    private bool interactionEndedForLevelComplete;
    private int displayedPowerBarFrame = int.MinValue;

    private struct BoxPose
    {
        public Vector3 Center;
        public Quaternion Rotation;
        public Vector3 HalfExtents;
    }

    private void Awake()
    {
        ovrGrabbable = GetComponent<OVRGrabbable>();
        metaGrabbable = GetComponentInParent<Grabbable>();
        clubRigidbody = GetComponent<Rigidbody>();
        metaGrabRigidbody = metaGrabbable != null
            ? metaGrabbable.GetComponent<Rigidbody>()
            : null;
        grabMotionRoot = metaGrabbable != null && metaGrabbable.Transform != null
            ? metaGrabbable.Transform
            : metaGrabbable != null ? metaGrabbable.transform : transform;
        clubColliders = GetComponentsInChildren<Collider>(true);
        ConfigureMetaGrabOwnership();
        EnforceRealWorldLength();
        ConfigureSuspendedState();

        ResolveStrikingColliders();
        ConfigureStrikingColliders();

        if (clubHeadCollider == null)
        {
            clubHeadCollider = FindClubHeadCollider();
        }

        previousBoxPoses = new BoxPose[strikingColliders.Length];
        currentBoxPoses = new BoxPose[strikingColliders.Length];
        CaptureCurrentBoxPoses(previousBoxPoses);
        hasPreviousBoxPoses = strikingColliders.Length > 0;
        ResetSwingSample();
        ResolveTargetBall();
        ConfigurePowerBar();
    }

    private void OnEnable()
    {
        hasPreviousBoxPoses = false;
        ResetSwingSample();
        ResetAndHidePowerBar();
    }

    private void LateUpdate()
    {
        UpdateGrabState();

        if (!CanStrike())
        {
            ResetSwingSample();
        }
        else
        {
            SampleSwingVelocity();
        }

        UpdatePowerBar();
    }

    private void FixedUpdate()
    {
        if (strikingColliders == null || strikingColliders.Length == 0)
        {
            return;
        }

        CaptureCurrentBoxPoses(currentBoxPoses);

        if (hasPreviousBoxPoses && CanStrike() && Time.unscaledTime - lastHitTime >= hitCooldown)
        {
            for (int i = 0; i < currentBoxPoses.Length; i++)
            {
                if (SweepForGolfBall(previousBoxPoses[i], currentBoxPoses[i]))
                {
                    break;
                }
            }
        }

        BoxPose[] swap = previousBoxPoses;
        previousBoxPoses = currentBoxPoses;
        currentBoxPoses = swap;
        hasPreviousBoxPoses = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyStroke(other, GetClubHeadPosition());
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryApplyStroke(collision.collider, GetClubHeadPosition());
    }

    private bool SweepForGolfBall(BoxPose startPose, BoxPose endPose)
    {
        float angle = Quaternion.Angle(startPose.Rotation, endPose.Rotation);
        int segmentCount = Mathf.Max(1, Mathf.CeilToInt(angle / Mathf.Max(2f, maximumSweepAngleStep)));

        for (int segment = 0; segment < segmentCount; segment++)
        {
            float startT = segment / (float)segmentCount;
            float endT = (segment + 1f) / segmentCount;
            BoxPose segmentStart = InterpolateBoxPose(startPose, endPose, startT);
            BoxPose segmentEnd = InterpolateBoxPose(startPose, endPose, endT);

            Vector3 displacement = segmentEnd.Center - segmentStart.Center;
            float distance = displacement.magnitude;
            Vector3 halfExtents = Max(segmentStart.HalfExtents, segmentEnd.HalfExtents)
                + Vector3.one * sweepSkin;
            Quaternion queryRotation = Quaternion.Slerp(segmentStart.Rotation, segmentEnd.Rotation, 0.5f);

            if (distance > Mathf.Epsilon)
            {
                int castHitCount = Physics.BoxCastNonAlloc(
                    segmentStart.Center,
                    halfExtents,
                    displacement / distance,
                    castHits,
                    queryRotation,
                    distance,
                    golfBallLayers,
                    QueryTriggerInteraction.Ignore);

                for (int hitIndex = 0; hitIndex < castHitCount; hitIndex++)
                {
                    Vector3 clubCenterAtImpact = segmentStart.Center
                        + displacement.normalized * castHits[hitIndex].distance;
                    if (TryApplyStroke(castHits[hitIndex].collider, clubCenterAtImpact))
                    {
                        return true;
                    }
                }
            }

            int overlapCount = Physics.OverlapBoxNonAlloc(
                segmentEnd.Center,
                halfExtents,
                overlapHits,
                segmentEnd.Rotation,
                golfBallLayers,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < overlapCount; hitIndex++)
            {
                if (TryApplyStroke(overlapHits[hitIndex], segmentEnd.Center))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryApplyStroke(Collider hitCollider, Vector3 clubCenterAtImpact)
    {
        if (hitCollider == null || !CanStrike()) return false;
        if (Time.unscaledTime - lastHitTime < hitCooldown) return false;
        if (hitCollider.transform.IsChildOf(transform)) return false;

        Rigidbody ballRigidbody = hitCollider.attachedRigidbody;
        if (ballRigidbody == null)
        {
            ballRigidbody = hitCollider.GetComponentInParent<Rigidbody>();
        }

        if (ballRigidbody == null || !IsGolfBall(hitCollider, ballRigidbody)) return false;

        Vector3 currentBallVelocity = Vector3.ProjectOnPlane(ballRigidbody.linearVelocity, Vector3.up);
        if (currentBallVelocity.magnitude > maximumBallSpeedForStroke) return false;

        Vector3 planarSwingVelocity = Vector3.ProjectOnPlane(GetLatestSwingVelocity(), Vector3.up);
        float ballLaunchSpeed = CalculateBallLaunchSpeed(planarSwingVelocity.magnitude);
        if (ballLaunchSpeed <= 0f || planarSwingVelocity.sqrMagnitude < 0.0001f) return false;

        Vector3 hitDirection = planarSwingVelocity.normalized;
        Vector3 toBall = Vector3.ProjectOnPlane(ballRigidbody.worldCenterOfMass - clubCenterAtImpact, Vector3.up);
        if (toBall.sqrMagnitude > 0.0001f && Vector3.Dot(hitDirection, toBall.normalized) < minimumApproachDot)
        {
            return false;
        }

        BallController ballController = ballRigidbody.GetComponent<BallController>();
        if (ballController != null)
        {
            if (!ballController.ApplyPutt(hitDirection, ballLaunchSpeed))
            {
                return false;
            }
        }
        else
        {
            // BallController bulunmayan eski prefab'lar için güvenli fallback.
            ballRigidbody.linearVelocity = hitDirection * ballLaunchSpeed;
            ballRigidbody.angularVelocity = Vector3.zero;
            ballRigidbody.WakeUp();
        }

        lastHitTime = Time.unscaledTime;
        TriggerHapticFeedback(ballLaunchSpeed);
        SchedulePowerBarReset();
        return true;
    }

    private static bool IsGolfBall(Collider hitCollider, Rigidbody ballRigidbody)
    {
        return hitCollider.CompareTag("GolfBall") || ballRigidbody.CompareTag("GolfBall");
    }

    private void SampleSwingVelocity()
    {
        float sampleTime = Time.unscaledTime;
        float deltaTime = sampleTime - previousSampleTime;
        Vector3 currentHeadPosition = GetClubHeadPosition();

        if (deltaTime > Mathf.Epsilon)
        {
            Vector3 instantaneousVelocity = (currentHeadPosition - previousSamplePosition) / deltaTime;
            sampledSwingVelocity = SmoothSwingVelocity(sampledSwingVelocity, instantaneousVelocity, deltaTime);
        }

        previousSamplePosition = currentHeadPosition;
        previousSampleTime = sampleTime;
    }

    private Vector3 GetLatestSwingVelocity()
    {
        float deltaTime = Time.unscaledTime - previousSampleTime;
        if (deltaTime > 0.001f)
        {
            Vector3 latestVelocity = (GetClubHeadPosition() - previousSamplePosition) / deltaTime;
            return SmoothSwingVelocity(sampledSwingVelocity, latestVelocity, deltaTime);
        }

        return sampledSwingVelocity;
    }

    private float CalculateBallLaunchSpeed(float clubHeadSpeed)
    {
        if (clubHeadSpeed < minimumClubHeadSpeed) return 0f;

        float fullPuttSpeed = Mathf.Max(clubHeadSpeedForFullPutt, minimumClubHeadSpeed + 0.01f);
        float speedRatio = Mathf.InverseLerp(minimumClubHeadSpeed, fullPuttSpeed, clubHeadSpeed);
        float curvedRatio = Mathf.Pow(speedRatio, puttResponseExponent);
        return Mathf.Lerp(minimumBallLaunchSpeed, maximumBallLaunchSpeed, curvedRatio);
    }

    private Vector3 SmoothSwingVelocity(Vector3 currentVelocity, Vector3 targetVelocity, float deltaTime)
    {
        float smoothing = Mathf.Max(0.01f, swingSmoothingTime);
        float blend = 1f - Mathf.Exp(-deltaTime / smoothing);
        return Vector3.Lerp(currentVelocity, targetVelocity, blend);
    }

    private bool IsClubHeld()
    {
        if (metaGrabbable != null) return metaGrabbable.SelectingPointsCount > 0;
        if (ovrGrabbable != null) return ovrGrabbable.isGrabbed;

        // Meta grab bileşeni olmayan test sahnelerinde vuruş testi yapılabilsin.
        return true;
    }

    public void EndInteractionForLevelComplete()
    {
        if (interactionEndedForLevelComplete) return;

        interactionEndedForLevelComplete = true;

        // PointableElement.OnDisable aktif seçimlerin her biri için Cancel üretir.
        // Böylece Meta interactor seçili referansını da temizler; listeyi doğrudan
        // değiştirmek interactor tarafında stale state bırakacağı için kullanılmaz.
        if (metaGrabbable != null)
        {
            metaGrabbable.enabled = false;

            foreach (GrabInteractable interactable
                     in metaGrabbable.GetComponentsInChildren<GrabInteractable>(true))
            {
                interactable.enabled = false;
            }

            foreach (HandGrabInteractable interactable
                     in metaGrabbable.GetComponentsInChildren<HandGrabInteractable>(true))
            {
                interactable.enabled = false;
            }

            foreach (DistanceGrabInteractable interactable
                     in metaGrabbable.GetComponentsInChildren<DistanceGrabInteractable>(true))
            {
                interactable.enabled = false;
            }
        }

        if (ovrGrabbable != null)
        {
            ovrGrabbable.enabled = false;
        }

        Transform interactionRoot = metaGrabbable != null
            ? metaGrabbable.transform
            : transform;
        foreach (Collider currentCollider
                 in interactionRoot.GetComponentsInChildren<Collider>(true))
        {
            currentCollider.enabled = false;
        }

        ConfigureSuspendedBody(metaGrabRigidbody);
        if (clubRigidbody != metaGrabRigidbody)
        {
            ConfigureSuspendedBody(clubRigidbody);
        }

        wasHeld = false;
        ResetSwingSample();
        CancelInvoke(nameof(StopHaptic));
        StopHaptic();
        ResetAndHidePowerBar();
    }

    private bool CanStrike()
    {
        return !requireClubHeldForStroke || IsClubHeld();
    }

    private void ResolveStrikingColliders()
    {
        List<BoxCollider> resolved = new List<BoxCollider>();

        if (strikingColliders != null)
        {
            foreach (BoxCollider strikingCollider in strikingColliders)
            {
                AddUnique(resolved, strikingCollider);
            }
        }

        if (resolved.Count == 0 && clubHeadCollider is BoxCollider legacyBoxCollider)
        {
            AddUnique(resolved, legacyBoxCollider);
        }

        if (resolved.Count == 0)
        {
            BoxCollider[] childBoxes = GetComponentsInChildren<BoxCollider>(true);
            foreach (BoxCollider childBox in childBoxes)
            {
                string lowerName = childBox.gameObject.name.ToLowerInvariant();
                if (lowerName.Contains("head") || lowerName.Contains("face") || lowerName.Contains("hit"))
                {
                    AddUnique(resolved, childBox);
                }
            }

            if (resolved.Count == 0 && childBoxes.Length > 0)
            {
                // İsimlendirme yoksa en uçtaki kutu, putter başı için en güvenli varsayımdır.
                BoxCollider furthestBox = childBoxes[0];
                float greatestDistance = float.NegativeInfinity;
                foreach (BoxCollider childBox in childBoxes)
                {
                    float distance = (childBox.bounds.center - transform.position).sqrMagnitude;
                    if (distance > greatestDistance)
                    {
                        greatestDistance = distance;
                        furthestBox = childBox;
                    }
                }

                AddUnique(resolved, furthestBox);
            }
        }

        strikingColliders = resolved.ToArray();
    }

    private static void AddUnique(List<BoxCollider> colliders, BoxCollider candidate)
    {
        if (candidate != null && !colliders.Contains(candidate))
        {
            colliders.Add(candidate);
        }
    }

    private void ConfigureStrikingColliders()
    {
        if (!useTriggerHitboxes) return;

        foreach (BoxCollider strikingCollider in strikingColliders)
        {
            strikingCollider.isTrigger = true;
        }
    }

    private Collider FindClubHeadCollider()
    {
        if (strikingColliders != null && strikingColliders.Length == 1)
        {
            return strikingColliders[0];
        }

        return null;
    }

    private Vector3 GetClubHeadPosition()
    {
        if (clubHeadCollider != null)
        {
            return clubHeadCollider.bounds.center;
        }

        if (strikingColliders == null || strikingColliders.Length == 0)
        {
            return transform.position;
        }

        Vector3 center = Vector3.zero;
        foreach (BoxCollider strikingCollider in strikingColliders)
        {
            center += GetBoxPose(strikingCollider).Center;
        }

        return center / strikingColliders.Length;
    }

    private void CaptureCurrentBoxPoses(BoxPose[] destination)
    {
        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = GetBoxPose(strikingColliders[i]);
        }
    }

    private static BoxPose GetBoxPose(BoxCollider boxCollider)
    {
        Transform boxTransform = boxCollider.transform;
        Vector3 scale = boxTransform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        return new BoxPose
        {
            Center = boxTransform.TransformPoint(boxCollider.center),
            Rotation = boxTransform.rotation,
            HalfExtents = Vector3.Scale(boxCollider.size * 0.5f, scale)
        };
    }

    private static BoxPose InterpolateBoxPose(BoxPose start, BoxPose end, float t)
    {
        return new BoxPose
        {
            Center = Vector3.Lerp(start.Center, end.Center, t),
            Rotation = Quaternion.Slerp(start.Rotation, end.Rotation, t),
            HalfExtents = Vector3.Lerp(start.HalfExtents, end.HalfExtents, t)
        };
    }

    private static Vector3 Max(Vector3 a, Vector3 b)
    {
        return new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
    }

    private void ConfigurePowerBar()
    {
        if (powerBarCanvas == null && powerBarImage != null)
        {
            powerBarCanvas = powerBarImage.GetComponentInParent<Canvas>(true);
        }

        if (powerBarCanvas != null && powerBarCanvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning(
                "VRGolfClub power bar Canvas'i World Space olmali. Canvas yalnızca Inspector'da World Space olarak ayarlandıktan sonra VR'da doğru görünür.",
                powerBarCanvas);
        }

        ResetAndHidePowerBar();
    }

    private void UpdatePowerBar()
    {
        if (powerBarCanvas == null || powerBarImage == null)
        {
            return;
        }

        bool isHeld = IsClubHeldForPowerBar();
        bool ballCanBeHit = IsTargetBallReadyForStroke();
        bool hitCooldownFinished = Time.unscaledTime - lastHitTime >= hitCooldown;
        bool shouldBeVisible = isHeld && ballCanBeHit && hitCooldownFinished;

        if (!isHeld)
        {
            powerBarResetPending = false;
            SetPowerBarFrame(0f);
        }
        else if (powerBarResetPending && Time.unscaledTime >= powerBarResetTime)
        {
            powerBarResetPending = false;
            SetPowerBarFrame(0f);
        }

        SetPowerBarVisible(shouldBeVisible);
        if (!shouldBeVisible || powerBarResetPending)
        {
            return;
        }

        float planarClubHeadSpeed = Vector3.ProjectOnPlane(sampledSwingVelocity, Vector3.up).magnitude;
        float fullPuttSpeed = Mathf.Max(clubHeadSpeedForFullPutt, minimumClubHeadSpeed + 0.01f);
        float normalizedPower = Mathf.InverseLerp(minimumClubHeadSpeed, fullPuttSpeed, planarClubHeadSpeed);
        SetPowerBarFrame(normalizedPower);
    }

    private bool IsTargetBallReadyForStroke()
    {
        ResolveTargetBall();
        return targetBall != null
            && targetBall.CanReceivePutt
            && targetBall.PlanarSpeed <= maximumBallSpeedForStroke;
    }

    private bool IsClubHeldForPowerBar()
    {
        // Fizik testleri için IsClubHeld() fallback'i korunur; görsel ise gerçekten grab edilmeden açılmaz.
        if (metaGrabbable != null) return metaGrabbable.SelectingPointsCount > 0;
        return ovrGrabbable != null && ovrGrabbable.isGrabbed;
    }

    private void ResolveTargetBall()
    {
        if (targetBall != null || Time.unscaledTime < nextBallLookupTime)
        {
            return;
        }

        targetBall = FindAnyObjectByType<BallController>();
        nextBallLookupTime = Time.unscaledTime + 0.5f;
    }

    private void SchedulePowerBarReset()
    {
        powerBarResetPending = true;
        powerBarResetTime = Time.unscaledTime + powerBarResetDelay;
    }

    private void SetPowerBarVisible(bool visible)
    {
        if (powerBarCanvas == null)
        {
            return;
        }

        bool isWorldSpace = powerBarCanvas.renderMode == RenderMode.WorldSpace;
        powerBarCanvas.enabled = visible && isWorldSpace;

        if (visible && !isWorldSpace && !powerBarConfigurationWarningLogged)
        {
            powerBarConfigurationWarningLogged = true;
            Debug.LogWarning("VRGolfClub power bar görünmedi: Canvas Render Mode değerini World Space yapın.", powerBarCanvas);
        }
    }

    private void SetPowerBarFrame(float normalizedPower)
    {
        if (powerBarImage == null)
        {
            return;
        }

        normalizedPower = Mathf.Clamp01(normalizedPower);
        int frameIndex = -1;
        Sprite targetSprite = emptyPowerBarFrame;

        if (normalizedPower > 0f && powerBarFrames != null && powerBarFrames.Length > 0)
        {
            frameIndex = Mathf.Clamp(
                Mathf.CeilToInt(normalizedPower * powerBarFrames.Length) - 1,
                0,
                powerBarFrames.Length - 1);
            targetSprite = powerBarFrames[frameIndex];
        }

        if (displayedPowerBarFrame == frameIndex && powerBarImage.sprite == targetSprite)
        {
            return;
        }

        displayedPowerBarFrame = frameIndex;
        powerBarImage.sprite = targetSprite;
        powerBarImage.enabled = targetSprite != null;
    }

    private void ResetAndHidePowerBar()
    {
        powerBarResetPending = false;
        displayedPowerBarFrame = int.MinValue;
        SetPowerBarFrame(0f);
        SetPowerBarVisible(false);
    }

#if UNITY_EDITOR
    [ContextMenu("Power Bar/chargebar2 Sprite'larini Otomatik Ata")]
    private void AutoAssignChargeBarSprites()
    {
        const string Root = "Assets/Prefabs/chargebar2";
        emptyPowerBarFrame = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/charge bar.png");

        powerBarFrames = new Sprite[13];
        for (int i = 0; i < powerBarFrames.Length; i++)
        {
            int sourceNumber = 13 - i;
            string path = $"{Root}/charge bar/Folder 1/Folder 100{sourceNumber:00}.png";
            powerBarFrames[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void ResetSwingSample()
    {
        previousSamplePosition = GetClubHeadPosition();
        previousSampleTime = Time.unscaledTime;
        sampledSwingVelocity = Vector3.zero;
    }

    private void TriggerHapticFeedback(float ballLaunchSpeed)
    {
        float speedRatio = maximumBallLaunchSpeed > 0f ? ballLaunchSpeed / maximumBallLaunchSpeed : 0f;
        float intensity = Mathf.Clamp01(speedRatio) * maxHapticIntensity;

        if (!IsClubHeld()) return;

        if (!IsClubHeldForPowerBar()) return;

        if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
        {
            OVRInput.SetControllerVibration(intensity, intensity, OVRInput.Controller.RTouch);
        }
        else
        {
            OVRInput.SetControllerVibration(intensity, intensity, OVRInput.Controller.LTouch);
        }

        CancelInvoke(nameof(StopHaptic));
        Invoke(nameof(StopHaptic), hapticDuration);
    }

    private void UpdateGrabState()
    {
        bool isHeld = IsClubHeld();
        if (isHeld && !wasHeld)
        {
            ResetSwingSample();
        }

        if (isHeld && clampHeldClubAboveGround)
        {
            KeepHeldClubAboveGround();
        }
        else if (wasHeld)
        {
            ConfigureSuspendedState();
        }

        wasHeld = isHeld;
    }

    private void ConfigureSuspendedState()
    {
        if (!suspendWhenNotHeld) return;

        ConfigureSuspendedBody(metaGrabRigidbody);
        if (clubRigidbody != metaGrabRigidbody)
        {
            ConfigureSuspendedBody(clubRigidbody);
        }
    }

    private void ConfigureMetaGrabOwnership()
    {
        if (metaGrabbable == null)
        {
            ConfigureClubRigidbody(clubRigidbody);
            return;
        }

        if (ovrGrabbable != null)
        {
            // Bu nesnenin fizik/grab otoritesi Meta Building Blocks'tur. OVRGrabber,
            // OVRGrabbable.enabled değerini kontrol etmeden aday topladığı için yalnızca
            // disable etmek yeterli değildir; aksi halde iki sistem aynı sopayı sürer.
            Destroy(ovrGrabbable);
            ovrGrabbable = null;
        }

        int maximumGrabPoints = limitToSingleMetaGrab ? 1 : 2;
        bool resetOtherGrabs = limitToSingleMetaGrab;

        metaGrabbable.MaxGrabPoints = maximumGrabPoints;
        metaGrabbable.TransferOnSecondSelection = limitToSingleMetaGrab && transferOnSecondMetaGrab;

        foreach (GrabInteractable interactable in metaGrabbable.GetComponentsInChildren<GrabInteractable>(true))
        {
            interactable.MaxSelectingInteractors = maximumGrabPoints;
            interactable.ResetGrabOnGrabsUpdated = resetOtherGrabs;
        }

        foreach (HandGrabInteractable interactable in metaGrabbable.GetComponentsInChildren<HandGrabInteractable>(true))
        {
            interactable.MaxSelectingInteractors = maximumGrabPoints;
            interactable.ResetGrabOnGrabsUpdated = resetOtherGrabs;
        }

        if (enableControllerDistanceGrab)
        {
            ConfigureControllerDistanceGrab(maximumGrabPoints, resetOtherGrabs);
        }
        else
        {
            DisableControllerDistanceGrab();
        }

        // Bu projede bırakılan sopa havada kalıyor; Meta'nın throw hızı yazması hem gereksiz
        // hem de kinematik Rigidbody üzerinde Unity 6 uyarısı üretiyor.
        metaGrabbable.InjectOptionalThrowWhenUnselected(false);
        metaGrabbable.InjectOptionalKinematicWhileSelected(true);

        ConfigureClubRigidbody(metaGrabRigidbody);
        if (clubRigidbody != metaGrabRigidbody)
        {
            // Mevcut prefab iki Rigidbody içeriyor. Çocuk gövdeyi fizik otoritesi yapmadan
            // kinematik tutarak Meta köküyle yarışmasını engelle.
            ConfigureClubRigidbody(clubRigidbody);
        }
    }

    private void ConfigureControllerDistanceGrab(int maximumSelectingInteractors, bool resetOtherGrabs)
    {
        if (metaGrabRigidbody == null) return;

        DistanceGrabInteractable distanceInteractable =
            metaGrabbable.GetComponentInChildren<DistanceGrabInteractable>(true);

        if (distanceInteractable == null)
        {
            GrabInteractable nearGrabInteractable =
                metaGrabbable.GetComponentInChildren<GrabInteractable>(true);
            GameObject interactionObject = nearGrabInteractable != null
                ? nearGrabInteractable.gameObject
                : metaGrabbable.gameObject;
            distanceInteractable = interactionObject.AddComponent<DistanceGrabInteractable>();
        }

        distanceInteractable.InjectAllGrabInteractable(metaGrabRigidbody);
        distanceInteractable.InjectOptionalPointableElement(metaGrabbable);
        distanceInteractable.MaxSelectingInteractors = maximumSelectingInteractors;
        distanceInteractable.ResetGrabOnGrabsUpdated = resetOtherGrabs;
    }

    private void DisableControllerDistanceGrab()
    {
        foreach (DistanceGrabInteractable distanceInteractable
                 in metaGrabbable.GetComponentsInChildren<DistanceGrabInteractable>(true))
        {
            // DistanceGrabInteractor seçili/hover durumundayken Meta'nın mesafe çizgisi
            // sopanın yakın hit noktasına kısalır. Sopa için bu yolu kapatmak, normal
            // GrabInteractable ve HandGrabInteractable davranışını değiştirmeden UI Ray'i korur.
            distanceInteractable.enabled = false;
            Destroy(distanceInteractable);
        }
    }

    private void ConfigureClubRigidbody(Rigidbody body)
    {
        if (body == null) return;

        body.mass = Mathf.Max(0.01f, clubMass);
        body.linearDamping = Mathf.Max(0f, clubLinearDamping);
        body.angularDamping = Mathf.Max(0f, clubAngularDamping);
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.None;
        // Meta GrabFreeTransformer hedef Transform'u her pointer Move olayında doğrudan sürer.
        // Rigidbody interpolation eski fizik pozunu araya sokarsa sopa ele karşı koyar ve
        // görsel model grab collider'ından ayrılabilir.
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void ConfigureSuspendedBody(Rigidbody body)
    {
        if (body == null) return;

        // Unity 6, kinematik bir gövdeye hız yazıldığında uyarı üretir. Hızları yalnızca
        // gövde hâlâ dinamikken temizle, ardından bekleme durumuna geçir.
        if (!body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        ConfigureClubRigidbody(body);
        body.isKinematic = true;
    }

    private void EnforceRealWorldLength()
    {
        if (!enforceRealPutterLength) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        float currentLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (currentLength <= 0.001f) return;

        Vector3 visualCenterBeforeScale = bounds.center;
        transform.localScale *= targetPutterLength / currentLength;
        Physics.SyncTransforms();

        Renderer[] scaledRenderers = GetComponentsInChildren<Renderer>(true);
        if (scaledRenderers.Length == 0) return;

        Bounds scaledBounds = scaledRenderers[0].bounds;
        for (int i = 1; i < scaledRenderers.Length; i++)
        {
            scaledBounds.Encapsulate(scaledRenderers[i].bounds);
        }

        transform.position += visualCenterBeforeScale - scaledBounds.center;
        Physics.SyncTransforms();
    }

    private void KeepHeldClubAboveGround()
    {
        if (clubColliders == null || clubColliders.Length == 0) return;

        Bounds combinedBounds = default;
        bool hasBounds = false;
        foreach (Collider currentCollider in clubColliders)
        {
            if (currentCollider == null || !currentCollider.enabled || currentCollider.isTrigger) continue;

            if (!hasBounds)
            {
                combinedBounds = currentCollider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(currentCollider.bounds);
            }
        }

        // Vuruş collider'ları trigger ise, başlığın sınırı yine de güvenlik ölçümü sağlar.
        if (!hasBounds && clubHeadCollider != null)
        {
            combinedBounds = clubHeadCollider.bounds;
            hasBounds = true;
        }

        if (!hasBounds) return;

        Vector3 rayOrigin = combinedBounds.center + Vector3.up * groundProbeHeight;
        float rayDistance = groundProbeHeight + combinedBounds.extents.y + groundClearance;
        int hitCount = Physics.RaycastNonAlloc(
            rayOrigin,
            Vector3.down,
            groundHits,
            rayDistance,
            clubGroundLayers,
            QueryTriggerInteraction.Ignore);

        float highestSurface = float.NegativeInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform)) continue;
            if (hitCollider.GetComponentInParent<BallController>() != null) continue;

            highestSurface = Mathf.Max(highestSurface, groundHits[i].point.y);
        }

        if (highestSurface == float.NegativeInfinity) return;

        float penetration = highestSurface + groundClearance - combinedBounds.min.y;
        if (penetration > 0f)
        {
            // Düzeltmeyi çocuk sopaya değil Meta'nın gerçekten hareket ettirdiği köke uygula.
            // Böylece grab collider'ı, interactable state'i ve sopa hizası birbirinden kopmaz.
            Transform motionRoot = grabMotionRoot != null ? grabMotionRoot : transform;
            motionRoot.position += Vector3.up * penetration;
            Physics.SyncTransforms();
        }
    }

    private void StopHaptic()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
    }

    private void OnDisable()
    {
        hasPreviousBoxPoses = false;
        CancelInvoke(nameof(StopHaptic));
        StopHaptic();
        ResetAndHidePowerBar();
    }
}
