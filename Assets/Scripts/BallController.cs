using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class BallController : MonoBehaviour
{
    [Header("Eski Sahne UI Bağlantıları")]
    [FormerlySerializedAs("strokeText")]
    [SerializeField] private TMP_Text legacyStrokeText;
    [FormerlySerializedAs("levelCompleteCanvas")]
    [SerializeField] private GameObject legacyLevelCompleteCanvas;

    [Header("Vuruş Partikülü")]
    [SerializeField] private ParticleSystem hitParticles;
    [SerializeField, Min(0f)] private float minimumEffectSpeed = 0.25f;
    [SerializeField, Min(0.01f)] private float fullEffectSpeed = 4.5f;
    [SerializeField, Min(1)] private int minimumParticleCount = 6;
    [SerializeField, Min(1)] private int maximumParticleCount = 28;
    [SerializeField, Min(0.001f)] private float minimumParticleSize = 0.02f;
    [SerializeField, Min(0.001f)] private float maximumParticleSize = 0.08f;

    [Header("Checkpoint ve Sınır")]
    [Min(0f)] public float stopThreshold = 0.035f;
    public float outOfBoundsY = -5f;
    [Tooltip("Son geçerli dünya konumunu yalnızca bu katmanlardaki zeminde günceller.")]
    [SerializeField] private LayerMask validGroundLayers = ~0;
    [SerializeField, Range(0f, 0.1f)] private float respawnHeightOffset = 0.015f;
    [SerializeField, Min(0.05f)] private float checkpointUpdateInterval = 0.1f;
    [SerializeField, Min(0.05f)] private float respawnCooldown = 0.5f;

    [Header("Rigidbody ve Fizik Materyali")]
    [SerializeField] private bool enforceRealGolfBallDiameter = true;
    [SerializeField, Min(0.001f)] private float targetBallDiameter = 0.04267f;
    [SerializeField, Min(0.001f)] private float ballMass = 0.04593f;
    [SerializeField, Min(0f)] private float linearDamping = 0.04f;
    [SerializeField, Min(0f)] private float angularDamping = 0.05f;
    [SerializeField, Min(1f)] private float maximumRollingAngularSpeed = 120f;
    [Tooltip("Fizik çözücüsünün topu duvarın içinden çıkarırken kullanabileceği en yüksek hız (m/s).")]
    [SerializeField, Min(0.1f)] private float maximumDepenetrationVelocity = 3f;
    [SerializeField] private PhysicsMaterial ballPhysicsMaterial;

    [Header("Yuvarlanma Fiziği")]
    [Tooltip("Vuruş API'sinin kabul edeceği en yüksek yatay top hızı (m/s).")]
    [SerializeField, Min(0.1f)] private float maximumLaunchSpeed = 3.6f;
    [Tooltip("Temel yuvarlanma yavaşlaması (m/s²).")]
    [Min(0f)] public float grassFriction = 0.42f;
    [Tooltip("Düşük hızda topun kararlı biçimde durması için temel yavaşlama çarpanı.")]
    [SerializeField, Min(0f)] private float lowSpeedFrictionMultiplier = 0.85f;
    [Tooltip("Yüksek hızda topun fazla erken ölmesini önleyen yavaşlama çarpanı.")]
    [SerializeField, Min(0f)] private float highSpeedFrictionMultiplier = 0.62f;
    [SerializeField, Min(0.05f)] private float frictionTransitionSpeed = 1.5f;
    [SerializeField, Min(0f)] private float rollingSpinResponse = 7f;
    [Tooltip("Top zemindeyken uygulanan ilave aşağı yönlü tutunma ivmesi (m/s²).")]
    [Min(0f)] public float extraGravity = 1f;
    [SerializeField] private bool preventUpwardMotion = true;
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField, Range(0.001f, 0.1f)] private float groundProbeDistance = 0.03f;

    [Header("Duvar Sekmesi")]
    [Tooltip("Kod tabanlı yumuşak sekme uygulanacak duvar katmanları.")]
    [SerializeField] private LayerMask wallLayers;
    [Tooltip("Duvar katmanı tanımlı olmasa da yataya yakın temas normallerini engel/duvar olarak algılar.")]
    [SerializeField] private bool detectWallsFromContactNormal = true;
    [SerializeField, Range(0f, 0.8f)] private float maximumWallNormalY = 0.35f;
    [Tooltip("Aynasal yansıma vektörüne uygulanan temel sekme gücü.")]
    [FormerlySerializedAs("wallRestitution")]
    [SerializeField, Range(0f, 1f)] private float wallReflectionSpeedRetention = 0.98f;
    [Tooltip("Her duvar sekmesinde kaybedilen temel yatay hız oranı.")]
    [SerializeField, Range(0f, 0.5f)] private float wallSpeedLoss = 0.06f;
    [Tooltip("Yüksek hızlı çarpışmalarda temel kayba eklenen en yüksek hız kaybı.")]
    [SerializeField, Range(0f, 0.5f)] private float highSpeedAdditionalWallLoss = 0.08f;
    [Tooltip("Ek hız kaybının tam değerine ulaştığı çarpışma hızı (m/s).")]
    [SerializeField, Min(0.1f)] private float speedForMaximumWallLoss = 3.6f;
    [Tooltip("Duvar boyunca kalan hız bileşeninden sekme anında kaybedilen oran.")]
    [SerializeField, Range(0f, 0.5f)] private float wallSurfaceFriction = 0.02f;
    [Tooltip("Bu hızın altındaki içeri yönlü temaslar yeni çarpışma sayılmaz (m/s).")]
    [SerializeField, Min(0f)] private float minimumWallImpactSpeed = 0.08f;
    [Tooltip("Sekmeden sonra topun duvardan uzaklaşan en düşük hız bileşeni (m/s).")]
    [SerializeField, Min(0f)] private float minimumWallBounceSpeed = 0.1f;
    [Tooltip("Temas sürdüğünde topu duvardan ayıran en düşük yatay hız (m/s).")]
    [SerializeField, Min(0f)] private float wallSeparationSpeed = 0.08f;
    [Tooltip("Her duvar temasında uygulanan küçük dışarı yönlü pozisyon düzeltmesi (metre).")]
    [SerializeField, Range(0f, 0.02f)] private float wallSeparationDistance = 0.003f;

    [SerializeField, HideInInspector] private int physicsTuningVersion;

    private const int GroundHitBufferSize = 8;
    private readonly RaycastHit[] groundHits = new RaycastHit[GroundHitBufferSize];

    private Rigidbody rb;
    private Collider ballCollider;
    private Vector3 initialSpawnPosition;
    private Vector3 lastCheckpoint;
    private int fallbackStrokeCount;
    private bool isMoving;
    private bool isGrounded;
    private Vector3 groundNormal = Vector3.up;
    private bool isLevelComplete;
    private Vector3 preCollisionPlanarVelocity;
    private PhysicsMaterial runtimeBallPhysicsMaterial;
    private bool hasValidCheckpoint;
    private float nextCheckpointUpdateTime;
    private float nextRespawnTime;

    public int StrokeCount => GameManager.Instance != null
        ? GameManager.Instance.CurrentStrokeCount
        : fallbackStrokeCount;

    public bool CanReceivePutt => !isLevelComplete && rb != null && !rb.isKinematic;

    public float PlanarSpeed
    {
        get
        {
            if (rb == null) return 0f;
            return Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
        }
    }

    protected virtual void Awake()
    {
        UpgradeLegacyPhysicsTuning();

        rb = GetComponent<Rigidbody>();
        ballCollider = GetComponent<Collider>();
        if (ballCollider == null)
        {
            ballCollider = GetComponentInChildren<Collider>();
        }

        EnforceRealWorldDiameter();

        rb.ResetCenterOfMass();
        rb.mass = ballMass;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.maxAngularVelocity = maximumRollingAngularSpeed;
        rb.maxDepenetrationVelocity = maximumDepenetrationVelocity;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (ballCollider != null)
        {
            if (ballPhysicsMaterial == null)
            {
                runtimeBallPhysicsMaterial = new PhysicsMaterial("MiniGolfBall Runtime")
                {
                    dynamicFriction = 0.08f,
                    staticFriction = 0.08f,
                    bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            ballCollider.sharedMaterial = ballPhysicsMaterial != null
                ? ballPhysicsMaterial
                : runtimeBallPhysicsMaterial;
        }
    }

    protected virtual void Start()
    {
        GameManager.EnsureInstance();
        initialSpawnPosition = rb.position;
        lastCheckpoint = initialSpawnPosition;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BindLegacyUI(legacyStrokeText, legacyLevelCompleteCanvas);
        }
    }

    protected virtual void Update()
    {
        if (isLevelComplete) return;

        if (transform.position.y < outOfBoundsY)
        {
            RespawnAtCheckpoint();
            return;
        }

        float planarSpeed = PlanarSpeed;
        if (planarSpeed > stopThreshold)
        {
            isMoving = true;
        }
        else if (isMoving && isGrounded)
        {
            StopPlanarMotion();
            isMoving = false;
        }
    }

    protected virtual void FixedUpdate()
    {
        if (isLevelComplete) return;

        isGrounded = CheckGrounded(out bool isOnValidGround);
        ClampUpwardVelocity();
        if (isOnValidGround && Time.unscaledTime >= nextCheckpointUpdateTime)
        {
            // Canvas/Panel hiyerarşisinden tamamen bağımsız dünya koordinatı.
            lastCheckpoint = rb.position;
            hasValidCheckpoint = true;
            nextCheckpointUpdateTime = Time.unscaledTime + checkpointUpdateInterval;
        }

        if (isGrounded)
        {
            ApplyRollingFriction();
            if (extraGravity > 0f)
            {
                rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
            }
        }

        // Unity çarpışma çözümleyicisi hızı değiştirmeden önceki yatay hızı sakla.
        // OnCollisionEnter bu örneği kullanarak gerçek geliş açısını korur.
        preCollisionPlanarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
    }

    public bool ApplyPutt(Vector3 worldDirection, float launchSpeed)
    {
        if (isLevelComplete || rb == null) return false;

        Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
        if (planarDirection.sqrMagnitude < 0.0001f) return false;

        float clampedSpeed = Mathf.Clamp(launchSpeed, 0f, maximumLaunchSpeed);
        if (clampedSpeed <= 0f) return false;

        rb.linearVelocity = planarDirection.normalized * clampedSpeed;
        rb.angularVelocity = CalculateRollingAngularVelocity(rb.linearVelocity);
        preCollisionPlanarVelocity = rb.linearVelocity;
        rb.WakeUp();

        RegisterStroke();
        PlayHitEffect(clampedSpeed);
        return true;
    }

    public void AddStroke()
    {
        if (!isLevelComplete)
        {
            RegisterStroke();
        }
    }

    public void LevelComplete()
    {
        if (isLevelComplete) return;

        isLevelComplete = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.CompleteHole();
        }
    }

    public void CompleteAndSink(Vector3 restPosition, float sinkDuration)
    {
        if (isLevelComplete) return;

        LevelComplete();
        StartCoroutine(SinkIntoHole(restPosition, sinkDuration));
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (isLevelComplete) return;

        if (other.CompareTag("OutOfBounds"))
        {
            RespawnAtCheckpoint();
            return;
        }

        GolfHole hole = other.GetComponentInParent<GolfHole>();
        if (hole != null)
        {
            hole.TrySink(this);
        }
    }

    protected virtual void OnTriggerStay(Collider other)
    {
        if (!isLevelComplete && other.CompareTag("OutOfBounds"))
        {
            RespawnAtCheckpoint();
        }
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (!isLevelComplete && collision.gameObject.CompareTag("OutOfBounds"))
        {
            RespawnAtCheckpoint();
            return;
        }

        ClampUpwardVelocity(collision);

        if (IsLayerInMask(collision.gameObject.layer, wallLayers)
            || IsWallContact(collision))
        {
            ResolveWallCollision(collision, usePreCollisionVelocity: true);
        }

    }

    protected virtual void OnCollisionStay(Collision collision)
    {
        if (!isLevelComplete && collision.gameObject.CompareTag("OutOfBounds"))
        {
            RespawnAtCheckpoint();
            return;
        }

        ClampUpwardVelocity(collision);

        if (IsLayerInMask(collision.gameObject.layer, wallLayers)
            || IsWallContact(collision))
        {
            ResolveWallCollision(collision, usePreCollisionVelocity: false);
        }
    }

    private void RegisterStroke()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterStroke();
        }
        else
        {
            fallbackStrokeCount++;
        }

        isMoving = true;
    }

    private void PlayHitEffect(float impactSpeed)
    {
        if (hitParticles == null) return;

        float normalizedStrength = Mathf.InverseLerp(
            minimumEffectSpeed,
            Mathf.Max(minimumEffectSpeed + 0.01f, fullEffectSpeed),
            impactSpeed);

        int particleCount = Mathf.RoundToInt(Mathf.Lerp(
            minimumParticleCount,
            Mathf.Max(minimumParticleCount, maximumParticleCount),
            normalizedStrength));

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            startSize = Mathf.Lerp(minimumParticleSize, maximumParticleSize, normalizedStrength)
        };

        hitParticles.Emit(emitParams, particleCount);
    }

    private void ApplyRollingFriction()
    {
        Vector3 velocity = rb.linearVelocity;
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
        float planarSpeed = planarVelocity.magnitude;
        if (planarSpeed <= 0f) return;

        float transition = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(stopThreshold, frictionTransitionSpeed, planarSpeed));
        float frictionMultiplier = Mathf.Lerp(
            lowSpeedFrictionMultiplier,
            highSpeedFrictionMultiplier,
            transition);
        float deceleration = grassFriction * frictionMultiplier;
        float nextSpeed = Mathf.MoveTowards(planarSpeed, 0f, deceleration * Time.fixedDeltaTime);

        Vector3 nextPlanarVelocity = planarVelocity.normalized * nextSpeed;
        rb.linearVelocity = new Vector3(
            nextPlanarVelocity.x,
            Mathf.Min(velocity.y, SlopeVerticalSpeed(nextPlanarVelocity, groundNormal)),
            nextPlanarVelocity.z);

        Vector3 targetAngularVelocity = CalculateRollingAngularVelocity(nextPlanarVelocity);
        float spinBlend = 1f - Mathf.Exp(-rollingSpinResponse * Time.fixedDeltaTime);
        rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, targetAngularVelocity, spinBlend);

        if (nextSpeed <= stopThreshold)
        {
            StopPlanarMotion();
        }
    }

    private void ResolveWallCollision(Collision collision, bool usePreCollisionVelocity)
    {
        if (collision.contactCount == 0) return;

        Vector3 currentPlanarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
        Vector3 incomingVelocity = usePreCollisionVelocity
            ? preCollisionPlanarVelocity
            : currentPlanarVelocity;

        if (incomingVelocity.magnitude < minimumWallImpactSpeed)
        {
            incomingVelocity = Vector3.ProjectOnPlane(collision.relativeVelocity, Vector3.up);
        }

        if (!TryGetOutwardWallNormal(collision, incomingVelocity, out Vector3 wallNormal))
        {
            return;
        }

        float incomingNormalSpeed = Vector3.Dot(incomingVelocity, wallNormal);
        Vector3 targetVelocity = currentPlanarVelocity;

        if (incomingVelocity.magnitude >= minimumWallImpactSpeed
            && incomingNormalSpeed < -minimumWallImpactSpeed)
        {
            targetVelocity = CalculateWallBounceVelocity(incomingVelocity, wallNormal);
        }

        // OnCollisionStay sırasında fizik çözücüsü topu yeniden duvara bastırırsa
        // içeri yönlü bileşeni sil ve topun teması gerçekten bırakmasını sağla.
        float outwardSpeed = Vector3.Dot(targetVelocity, wallNormal);
        if (outwardSpeed < wallSeparationSpeed)
        {
            targetVelocity += wallNormal * (wallSeparationSpeed - outwardSpeed);
        }

        SetPlanarVelocity(targetVelocity);
        rb.angularVelocity = CalculateRollingAngularVelocity(targetVelocity);
        preCollisionPlanarVelocity = targetVelocity;

        if (wallSeparationDistance > 0f)
        {
            rb.position += wallNormal * wallSeparationDistance;
        }
    }

    private Vector3 CalculateWallBounceVelocity(Vector3 incomingVelocity, Vector3 wallNormal)
    {
        Vector3 reflectedVelocity = Vector3.Reflect(incomingVelocity, wallNormal);
        float reflectedNormalSpeed = Mathf.Max(0f, Vector3.Dot(reflectedVelocity, wallNormal));
        Vector3 normalVelocity = wallNormal * reflectedNormalSpeed;
        Vector3 tangentialVelocity = reflectedVelocity - normalVelocity;
        tangentialVelocity *= 1f - Mathf.Clamp01(wallSurfaceFriction);

        float speedRatio = Mathf.InverseLerp(
            minimumWallImpactSpeed,
            Mathf.Max(minimumWallImpactSpeed + 0.01f, speedForMaximumWallLoss),
            incomingVelocity.magnitude);
        float totalSpeedLoss = Mathf.Clamp01(
            wallSpeedLoss + highSpeedAdditionalWallLoss * speedRatio);
        float speedRetention = Mathf.Clamp01(wallReflectionSpeedRetention) * (1f - totalSpeedLoss);
        Vector3 targetVelocity = (normalVelocity + tangentialVelocity) * speedRetention;

        float outwardSpeed = Vector3.Dot(targetVelocity, wallNormal);
        if (outwardSpeed < minimumWallBounceSpeed)
        {
            targetVelocity += wallNormal * (minimumWallBounceSpeed - outwardSpeed);
        }

        return targetVelocity;
    }

    private void SetPlanarVelocity(Vector3 planarVelocity)
    {
        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(
            planarVelocity.x,
            Mathf.Min(currentVelocity.y, SlopeVerticalSpeed(planarVelocity, isGrounded ? groundNormal : Vector3.up)),
            planarVelocity.z);
    }

    private bool TryGetOutwardWallNormal(
        Collision collision,
        Vector3 incomingVelocity,
        out Vector3 reflectionNormal)
    {
        reflectionNormal = Vector3.zero;
        Vector3 incomingDirection = incomingVelocity.normalized;
        float strongestOpposition = float.NegativeInfinity;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            Vector3 contactNormal = contact.normal;
            if (Mathf.Abs(contactNormal.y) > maximumWallNormalY) continue;

            Vector3 planarNormal = Vector3.ProjectOnPlane(contactNormal, Vector3.up);
            if (planarNormal.sqrMagnitude < 0.0001f) continue;

            planarNormal.Normalize();
            Vector3 wallToBall = Vector3.ProjectOnPlane(
                rb.worldCenterOfMass - contact.point,
                Vector3.up);
            if (wallToBall.sqrMagnitude > 0.0001f
                && Vector3.Dot(planarNormal, wallToBall) < 0f)
            {
                planarNormal = -planarNormal;
            }

            float opposition = -Vector3.Dot(incomingDirection, planarNormal);
            if (opposition > strongestOpposition)
            {
                strongestOpposition = opposition;
                reflectionNormal = planarNormal;
            }
        }

        return reflectionNormal.sqrMagnitude > 0.0001f;
    }

    private bool IsWallContact(Collision collision)
    {
        if (!detectWallsFromContactNormal) return false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.y) <= maximumWallNormalY)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 CalculateRollingAngularVelocity(Vector3 planarVelocity)
    {
        if (ballCollider == null) return Vector3.zero;

        Bounds bounds = ballCollider.bounds;
        float radius = Mathf.Max(
            0.001f,
            Mathf.Min(bounds.extents.x, bounds.extents.z));
        Vector3 angularVelocity = Vector3.Cross(Vector3.up, planarVelocity) / radius;
        return Vector3.ClampMagnitude(angularVelocity, maximumRollingAngularSpeed);
    }

    // Zıplamayı engeller ama rampa yüzeyine paralel yukarı hareketi serbest bırakır.
    private void ClampUpwardVelocity(Collision collision = null)
    {
        if (!preventUpwardMotion || rb == null || rb.linearVelocity.y <= 0f) return;

        Vector3 velocity = rb.linearVelocity;
        float maximumUpwardSpeed = SlopeVerticalSpeed(velocity, isGrounded ? groundNormal : Vector3.up);
        if (collision != null)
        {
            // Rampa başlangıcında yer ışını henüz düz zemini görürken temas normali rampayı gösterir.
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 contactNormal = collision.GetContact(i).normal;
                if (contactNormal.y < 0f) contactNormal = -contactNormal;
                maximumUpwardSpeed = Mathf.Max(maximumUpwardSpeed, SlopeVerticalSpeed(velocity, contactNormal));
            }
        }

        if (velocity.y <= maximumUpwardSpeed) return;

        velocity.y = maximumUpwardSpeed;
        rb.linearVelocity = velocity;
    }

    // Yüzeye paralel hareketin gerektirdiği dikey hız; düz zemin ve duvarlarda 0.
    private static float SlopeVerticalSpeed(Vector3 velocity, Vector3 surfaceNormal)
    {
        if (surfaceNormal.y < 0.5f) return 0f;

        return Mathf.Max(0f, -(velocity.x * surfaceNormal.x + velocity.z * surfaceNormal.z) / surfaceNormal.y);
    }

    private bool CheckGrounded(out bool isOnValidGround)
    {
        isOnValidGround = false;
        if (ballCollider == null) return false;

        Bounds bounds = ballCollider.bounds;
        float distance = bounds.extents.y + groundProbeDistance;
        int hitCount = Physics.RaycastNonAlloc(
            bounds.center,
            Vector3.down,
            groundHits,
            distance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        bool foundGround = false;
        float nearestGroundDistance = float.PositiveInfinity;
        groundNormal = Vector3.up;
        int uiLayer = LayerMask.NameToLayer("UI");
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHits[i].collider;
            if (hitCollider == null || hitCollider.attachedRigidbody == rb) continue;

            bool validLayer = IsLayerInMask(hitCollider.gameObject.layer, validGroundLayers);
            bool isUi = uiLayer >= 0 && hitCollider.gameObject.layer == uiLayer;
            bool isOutOfBounds = hitCollider.CompareTag("OutOfBounds");
            bool facesUp = groundHits[i].normal.y >= 0.5f;
            if (facesUp)
            {
                foundGround = true;
                if (groundHits[i].distance < nearestGroundDistance)
                {
                    nearestGroundDistance = groundHits[i].distance;
                    groundNormal = groundHits[i].normal;
                }
            }

            if (validLayer && !isUi && !isOutOfBounds && facesUp)
            {
                isOnValidGround = true;
            }
        }

        return foundGround;
    }

    private void RespawnAtCheckpoint()
    {
        if (rb == null || Time.unscaledTime < nextRespawnTime) return;

        nextRespawnTime = Time.unscaledTime + respawnCooldown;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Vector3 safePosition = hasValidCheckpoint ? lastCheckpoint : initialSpawnPosition;
        rb.position = safePosition + Vector3.up * respawnHeightOffset;
        preCollisionPlanarVelocity = Vector3.zero;
        Physics.SyncTransforms();
        rb.WakeUp();
        isMoving = false;
        nextCheckpointUpdateTime = nextRespawnTime;
    }

    private void StopPlanarMotion()
    {
        Vector3 velocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(0f, Mathf.Min(velocity.y, 0f), 0f);
        rb.angularVelocity = Vector3.zero;
        preCollisionPlanarVelocity = Vector3.zero;
    }

    private void UpgradeLegacyPhysicsTuning()
    {
        if (physicsTuningVersion < 1)
        {
            // Yalnızca önceki sürümün varsayılanlarına hâlâ yakın alanları taşır;
            // Inspector'da bilinçli biçimde değiştirilmiş değerleri ezmez.
            if (Mathf.Approximately(stopThreshold, 0.05f)) stopThreshold = 0.035f;
            if (Mathf.Approximately(maximumLaunchSpeed, 5f)) maximumLaunchSpeed = 3.6f;
            if (Mathf.Approximately(grassFriction, 0.8f)) grassFriction = 0.42f;
            if (Mathf.Approximately(lowSpeedFrictionMultiplier, 1.5f))
            {
                lowSpeedFrictionMultiplier = 0.85f;
            }

            if (Mathf.Approximately(highSpeedFrictionMultiplier, 0.55f))
            {
                highSpeedFrictionMultiplier = 0.62f;
            }

            if (Mathf.Approximately(frictionTransitionSpeed, 1.2f))
            {
                frictionTransitionSpeed = 1.5f;
            }

            if (Mathf.Approximately(extraGravity, 2f)) extraGravity = 1f;
            physicsTuningVersion = 1;
        }

        if (physicsTuningVersion < 2)
        {
            // Önceki otomatik ayarın 0.62 değeri aynasal açıyı gereğinden fazla
            // sığlaştırıyordu. Bilinçli farklı Inspector değerlerini koru.
            if (Mathf.Approximately(wallReflectionSpeedRetention, 0.62f))
            {
                wallReflectionSpeedRetention = 0.88f;
            }

            physicsTuningVersion = 2;
        }

        if (physicsTuningVersion < 3)
        {
            if (Mathf.Approximately(wallReflectionSpeedRetention, 0.88f))
            {
                wallReflectionSpeedRetention = 0.98f;
            }

            physicsTuningVersion = 3;
        }

        if (physicsTuningVersion < 4)
        {
            if (Mathf.Approximately(minimumWallImpactSpeed, 0.15f))
            {
                minimumWallImpactSpeed = 0.08f;
            }

            physicsTuningVersion = 4;
        }
    }

    private void EnforceRealWorldDiameter()
    {
        if (!enforceRealGolfBallDiameter) return;

        SphereCollider sphere = GetComponentInChildren<SphereCollider>(true);
        if (sphere == null) return;

        Vector3 worldScale = sphere.transform.lossyScale;
        float maximumScale = Mathf.Max(
            Mathf.Abs(worldScale.x),
            Mathf.Max(Mathf.Abs(worldScale.y), Mathf.Abs(worldScale.z)));
        float currentDiameter = sphere.radius * 2f * maximumScale;
        if (currentDiameter <= 0.0001f) return;

        transform.localScale *= targetBallDiameter / currentDiameter;
        Physics.SyncTransforms();
    }

    protected virtual void OnDestroy()
    {
        if (runtimeBallPhysicsMaterial != null)
        {
            Destroy(runtimeBallPhysicsMaterial);
        }
    }

    private IEnumerator SinkIntoHole(Vector3 restPosition, float sinkDuration)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        rb.isKinematic = true;

        Collider[] ballColliders = GetComponentsInChildren<Collider>();
        foreach (Collider currentCollider in ballColliders)
        {
            currentCollider.enabled = false;
        }

        Vector3 startPosition = transform.position;
        float duration = Mathf.Max(0f, sinkDuration);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float progress = duration > 0f ? Mathf.Clamp01(elapsedTime / duration) : 1f;
            float smoothProgress = progress * progress * (3f - 2f * progress);
            transform.position = Vector3.Lerp(startPosition, restPosition, smoothProgress);
            yield return null;
        }

        transform.position = restPosition;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
