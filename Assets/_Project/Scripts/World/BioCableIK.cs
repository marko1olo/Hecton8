using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Lightweight spline-like runtime cable used by abyssal bio-cable zones.
    /// Manager-driven only: no Update, no per-frame allocations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BioCableIK : MonoBehaviour
    {
        private const float SegmentDistanceEpsilonSq = 0.00000001f;
        private const float MaximumDeltaTime = 0.1f;
        private const float MaximumCableVelocity = 64f;

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Shared authored material used by cable spark particles. Runtime fallback material creation is forbidden.")]
        private Material cableMaterial;

        [Header("── Cable Shape ─────────────────────")]
        [SerializeField, Range(4, 24)]
        [Tooltip("Segment count used by the light IK chain.")]
        private int segmentCount = 12;

        [SerializeField, Min(0.1f)]
        [Tooltip("Rest distance preserved between cable points.")]
        private float segmentLength = 1.25f;

        [SerializeField, Range(0f, 32f)]
        [Tooltip("Spring pull toward the live attractor point near the scooter hull.")]
        private float attractorSpring = 9.5f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Velocity damping applied to cable segments each simulation step.")]
        private float damping = 1.45f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Additional wrap bias that coils the last third of the cable around a stuck scooter.")]
        private float wrapStrength = 1.2f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Cable width at the anchor root.")]
        private float rootWidth = 0.18f;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Cable width at the free end.")]
        private float tipWidth = 0.06f;

        [Header("── EMP Charge Visuals ─────────────")]
        [SerializeField]
        [Tooltip("Calm cable tint used when the nest is idle.")]
        private Color baseCableColor = new Color(0.12f, 0.52f, 0.46f, 0.92f);

        [SerializeField]
        [Tooltip("Hot white tint used when an EMP nest reaches charge-up state.")]
        private Color empChargeColor = new Color(0.95f, 0.98f, 1f, 0.98f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum charge before spark emission starts.")]
        private float sparkChargeThreshold = 0.28f;

        [SerializeField, Range(0f, 128f)]
        [Tooltip("Maximum spark emission rate used during EMP pre-fire charging.")]
        private float sparkEmissionRate = 42f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Extra width added while the cable is fully charged.")]
        private float empWidthBoost = 0.3f;

        [Header("── Snap Recoil ───────────────────")]
        [SerializeField, Range(0f, 4f)]
        [Tooltip("Additional damping applied after the cable has been severed and is recoiling.")]
        private float snapDamping = 1.8f;

        [SerializeField, Range(0f, 8f)]
        [Tooltip("How strongly severed cable segments continue to inherit the snap velocity during recoil.")]
        private float snapVelocityCarry = 2.6f;

        [Header("── Elastic Rupture ─────────────────")]
        [SerializeField, Range(1f, 2.5f)]
        [Tooltip("Normalized chain stretch ratio where the cable starts accumulating rupture strain.")]
        private float elasticStretchLimit = 1.42f;

        [SerializeField, Range(0.02f, 1f)]
        [Tooltip("How long the cable must stay beyond the stretch limit before it ruptures.")]
        private float elasticBreakHoldTime = 0.14f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Extra recoil multiplier injected when the cable bursts from over-tension instead of a cutter release.")]
        private float elasticBreakRecoilMultiplier = 1.35f;

        // COLD ALLOC: Vector3[24] - cable point positions for manager-driven IK simulation - owner: BioCableIK
        private Vector3[] _points;
        // COLD ALLOC: Vector3[24] - cable point velocities for manager-driven IK simulation - owner: BioCableIK
        private Vector3[] _velocities;

        private Vector3 _anchorPositionWS;
        private Vector3 _anchorUpWS = Vector3.up;
        private Vector3 _snapVelocityWS;
        private float _oscillationTime;
        private float _empCharge01;
        private float _empPulse01;
        private float _snapTimer;
        private float _snapDuration;
        private float _elasticBreakTimer;
        private float _debugStretchRatio;
        private bool _initialized;
        private bool _pendingElasticRupture;
        private bool _loggedMissingCableMaterial;
        private bool _isCableActive;
        private int _splineLinkId;
        private Color _currentCableColor = new Color(0.12f, 0.52f, 0.46f, 0.92f);
        private float _currentCableRadius = 0.12f;
        private Vector3 _pendingElasticRuptureVelocityWS;
        private ParticleSystem _sparkParticles;
        private ParticleSystemRenderer _sparkRenderer;

        private void Awake()
        {
            _splineLinkId = GetEntityId().GetHashCode();
            ResolveRuntimeWiring();
            EnsureStorage();
            EnsureChargeEffects();
            InitializeAt(transform.position, Vector3.up);
        }

        private void OnDisable()
        {
            ConnectionSplineBatchRenderer.RemovePipeLink(_splineLinkId);
        }

        private void OnDestroy()
        {
            ConnectionSplineBatchRenderer.RemovePipeLink(_splineLinkId);
        }

        /// <summary>
        /// Resets the cable chain to a stable authored anchor pose.
        /// </summary>
        public void InitializeAt(Vector3 anchorPositionWS, Vector3 anchorUpWS)
        {
            ResolveRuntimeWiring();
            EnsureStorage();

            _anchorPositionWS = SanitizePosition(anchorPositionWS, SanitizePosition(transform.position, Vector3.zero));
            _anchorUpWS = ResolveSafeDirection(anchorUpWS, Vector3.up);
            _snapVelocityWS = Vector3.zero;
            _snapTimer = 0f;
            _snapDuration = 0f;
            _elasticBreakTimer = 0f;
            _debugStretchRatio = 1f;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;

            for (int i = 0; i < _points.Length; i++)
            {
                _points[i] = _anchorPositionWS - _anchorUpWS * (ResolveSegmentLength() * i);
                _velocities[i] = Vector3.zero;
            }

            ApplyVisualState();
            SyncRenderer();
            _initialized = true;
        }

        /// <summary>
        /// Advances the cable toward the live scooter hull without requiring per-segment colliders.
        /// </summary>
        public void TickCable(
            Vector3 anchorPositionWS,
            Vector3 anchorUpWS,
            Vector3 attractorPositionWS,
            Vector3 attractorVelocityWS,
            float attraction01,
            float wrap01,
            float dt)
        {
            if (!_initialized)
                InitializeAt(anchorPositionWS, anchorUpWS);

            _anchorPositionWS = SanitizePosition(anchorPositionWS, _anchorPositionWS);
            _anchorUpWS = ResolveSafeDirection(anchorUpWS, Vector3.up);
            _points[0] = _anchorPositionWS;
            _velocities[0] = Vector3.zero;
            _snapTimer = 0f;
            _snapDuration = 0f;
            _snapVelocityWS = Vector3.zero;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;

            float deltaTime = ResolveDeltaTime(dt);
            _oscillationTime = ResolveOscillationTime(_oscillationTime, deltaTime);
            float clampedAttraction = Clamp01Finite(attraction01);
            float clampedWrap = Clamp01Finite(wrap01);
            float safeSegmentLength = ResolveSegmentLength();
            float safeAttractorSpring = ResolveRange(attractorSpring, 0f, 32f, 9.5f);
            float safeDamping = ResolveRange(damping, 0f, 4f, 1.45f);
            float safeWrapStrength = ResolveRange(wrapStrength, 0f, 3f, 1.2f);
            Vector3 safeAttractorPosition = SanitizePosition(attractorPositionWS, _points[_points.Length - 1]);
            Vector3 safeAttractorVelocity = SanitizeVelocity(attractorVelocityWS);
            Vector3 velocityBias = safeAttractorVelocity * LerpClamped(0.08f, 0.42f, clampedWrap);
            Vector3 attractorDirection = ResolveSafeDirection(safeAttractorVelocity, Vector3.forward);
            Vector3 wrapAxis = Vector3.Cross(_anchorUpWS, attractorDirection);
            float wrapAxisLengthSq = wrapAxis.sqrMagnitude;
            if (!math.isfinite(wrapAxisLengthSq) || wrapAxisLengthSq <= 0.0001f)
                wrapAxis = Vector3.Cross(_anchorUpWS, Vector3.right);
            wrapAxis = ResolveSafeDirection(wrapAxis, Vector3.up);

            for (int i = 1; i < _points.Length; i++)
            {
                float tail01 = i / (float)(_points.Length - 1);
                Vector3 restFallback = _points[i - 1] - _anchorUpWS * safeSegmentLength;
                Vector3 point = SanitizePosition(_points[i], restFallback);
                Vector3 velocity = SanitizeVelocity(_velocities[i]);

                Vector3 restPosition = _points[i - 1] - _anchorUpWS * safeSegmentLength;
                Vector3 springForce = (restPosition - point) * LerpClamped(3.6f, 6.8f, tail01);

                Vector3 toAttractor = safeAttractorPosition - point;
                Vector3 attractForce = toAttractor * (safeAttractorSpring * LerpClamped(0.2f, 1f, tail01) * clampedAttraction);

                Vector3 wrapOffset = wrapAxis * FastTriangleSineSigned(tail01 * 4.5f + _oscillationTime * 1.9f) * safeSegmentLength * 0.55f;
                Vector3 wrapForce = wrapOffset * (safeWrapStrength * clampedWrap * Mathf.SmoothStep(0f, 1f, tail01));

                Vector3 force = springForce + attractForce + wrapForce + velocityBias;
                velocity += SanitizeVelocity(force) * deltaTime;
                velocity *= Clamp01Finite(1f - safeDamping * deltaTime * 0.35f);
                velocity = SanitizeVelocity(velocity);

                point = SanitizePosition(point + velocity * deltaTime, restPosition);

                point = ConstrainSegmentToLength(_points[i - 1], point);

                _points[i] = SanitizePosition(point, restPosition);
                _velocities[i] = velocity;
            }

            UpdateElasticRupture(deltaTime, safeAttractorPosition, clampedAttraction);
            UpdateSparkAnchor();
            ApplyVisualState();
            SyncRenderer();
        }

        private static float FastTriangleSineSigned(float radians)
        {
            float cycle = math.frac((radians * 0.159154943f) + 0.25f);
            return 1f - math.abs((cycle * 4f) - 2f);
        }

        /// <summary>
        /// Advances a severed cable through its recoil window after the cutter snaps the bio-cable.
        /// </summary>
        public void TickReleased(Vector3 anchorPositionWS, Vector3 anchorUpWS, float dt)
        {
            if (!_initialized)
                InitializeAt(anchorPositionWS, anchorUpWS);

            _anchorPositionWS = SanitizePosition(anchorPositionWS, _anchorPositionWS);
            _anchorUpWS = ResolveSafeDirection(anchorUpWS, Vector3.up);
            _points[0] = _anchorPositionWS;
            _velocities[0] = Vector3.zero;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;

            float deltaTime = ResolveDeltaTime(dt);
            _oscillationTime = ResolveOscillationTime(_oscillationTime, deltaTime);
            float safeSegmentLength = ResolveSegmentLength();
            float safeDamping = ResolveRange(damping, 0f, 4f, 1.45f);
            float safeSnapDamping = ResolveRange(snapDamping, 0f, 4f, 1.8f);
            float safeSnapVelocityCarry = ResolveRange(snapVelocityCarry, 0f, 8f, 2.6f);
            float recoilGate = _snapDuration > 0.0001f ? Clamp01Finite(_snapTimer / _snapDuration) : 0f;
            if (_snapTimer > 0f)
            {
                _snapTimer -= deltaTime;
                if (_snapTimer < 0f)
                    _snapTimer = 0f;
            }

            for (int i = 1; i < _points.Length; i++)
            {
                float tail01 = i / (float)(_points.Length - 1);
                Vector3 restFallback = _points[i - 1] - _anchorUpWS * safeSegmentLength;
                Vector3 point = SanitizePosition(_points[i], restFallback);
                Vector3 velocity = SanitizeVelocity(_velocities[i]);

                Vector3 restPosition = _points[i - 1] - _anchorUpWS * safeSegmentLength;
                Vector3 springForce = (restPosition - point) * LerpClamped(2.8f, 5.4f, tail01);
                Vector3 recoilForce = SanitizeVelocity(_snapVelocityWS) * (safeSnapVelocityCarry * recoilGate * LerpClamped(0.4f, 1f, tail01));
                velocity += SanitizeVelocity(springForce + recoilForce) * deltaTime;
                velocity *= Clamp01Finite(1f - (safeDamping + safeSnapDamping * recoilGate) * deltaTime * 0.35f);
                velocity = SanitizeVelocity(velocity);

                point = SanitizePosition(point + velocity * deltaTime, restPosition);

                point = ConstrainSegmentToLength(_points[i - 1], point);

                _points[i] = SanitizePosition(point, restPosition);
                _velocities[i] = velocity;
            }

            UpdateSparkAnchor();
            ApplyVisualState();
            SyncRenderer();
        }

        /// <summary>
        /// Returns and clears the pending elastic-rupture event generated by over-tension.
        /// </summary>
        public bool ConsumeElasticRupture(out Vector3 ruptureVelocityWS)
        {
            ruptureVelocityWS = _pendingElasticRuptureVelocityWS;
            bool hadEvent = _pendingElasticRupture;
            _pendingElasticRupture = false;
            _pendingElasticRuptureVelocityWS = Vector3.zero;
            return hadEvent;
        }

        /// <summary>
        /// Updates the deterministic EMP pre-fire visual state without allocating materials or gradients.
        /// </summary>
        public void SetEmpCharge(float charge01, float pulse01)
        {
            _empCharge01 = Clamp01Finite(charge01);
            _empPulse01 = Clamp01Finite(pulse01);
            ApplyVisualState();
        }

        /// <summary>
        /// Injects a sever snap impulse so the cable visibly recoils instead of disappearing on release.
        /// </summary>
        public void TriggerSnapRecoil(Vector3 recoilVelocityWS, float duration)
        {
            EnsureStorage();
            _snapVelocityWS = SanitizeVelocity(recoilVelocityWS);
            _snapDuration = ResolveRange(duration, 0.1f, 2f, 0.1f);
            _snapTimer = _snapDuration;

            for (int i = 1; i < _velocities.Length; i++)
            {
                float tail01 = i / (float)(_velocities.Length - 1);
                _velocities[i] = SanitizeVelocity(_velocities[i] + _snapVelocityWS * LerpClamped(0.35f, 1f, tail01));
            }
        }

        /// <summary>
        /// Returns true while the severed cable still has active recoil motion to render.
        /// </summary>
        public bool HasTransientMotion => _snapTimer > 0.0001f;

        /// <summary>
        /// Enables or disables the cable renderer without destroying runtime storage.
        /// </summary>
        public void SetCableActive(bool isActive)
        {
            _isCableActive = isActive;

            if (!isActive && _sparkParticles != null && _sparkParticles.isPlaying)
                _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (!isActive)
            {
                ConnectionSplineBatchRenderer.RemovePipeLink(_splineLinkId);
                _pendingElasticRupture = false;
                _pendingElasticRuptureVelocityWS = Vector3.zero;
                _elasticBreakTimer = 0f;
                return;
            }

            SyncRenderer();
        }

        private void ResolveRuntimeWiring()
        {
            _currentCableColor = SanitizeColor(baseCableColor, new Color(0.12f, 0.52f, 0.46f, 0.92f));
            _currentCableRadius = ResolveCableRadius();
        }

        private void EnsureStorage()
        {
            int clampedSegmentCount = Mathf.Clamp(segmentCount, 4, 24);
            if (_points == null || _points.Length != clampedSegmentCount)
            {
                // COLD ALLOC: Vector3[24] - cable point positions for manager-driven IK simulation - owner: BioCableIK
                _points = new Vector3[clampedSegmentCount];
                // COLD ALLOC: Vector3[24] - cable point velocities for manager-driven IK simulation - owner: BioCableIK
                _velocities = new Vector3[clampedSegmentCount];
            }
        }

        private void UpdateElasticRupture(float deltaTime, Vector3 attractorPositionWS, float attraction01)
        {
            if (_points == null || _points.Length <= 1)
                return;

            float safeSegmentLength = ResolveSegmentLength();
            float safeElasticStretchLimit = ResolveRange(elasticStretchLimit, 1f, 2.5f, 1.42f);
            float safeElasticBreakHoldTime = ResolveRange(elasticBreakHoldTime, 0.02f, 1f, 0.14f);
            float safeElasticBreakRecoilMultiplier = ResolveRange(elasticBreakRecoilMultiplier, 0f, 4f, 1.35f);
            float restLength = safeSegmentLength * (_points.Length - 1);
            if (restLength <= 0.0001f)
                return;

            float tailDistanceSq = (_points[_points.Length - 1] - _anchorPositionWS).sqrMagnitude;
            float attractorDistanceSq = (attractorPositionWS - _anchorPositionWS).sqrMagnitude;
            if (!math.isfinite(tailDistanceSq) || !math.isfinite(attractorDistanceSq))
            {
                _elasticBreakTimer = 0f;
                _debugStretchRatio = 1f;
                return;
            }

            float effectiveDistanceSq = math.max(tailDistanceSq, attractorDistanceSq);
            float stretchRatioSq = effectiveDistanceSq / math.max(restLength * restLength, 0.0001f);
            float stretchRatioEstimate = 0.5f * (stretchRatioSq + 1f);
            if (!math.isfinite(stretchRatioEstimate))
            {
                _elasticBreakTimer = 0f;
                _debugStretchRatio = 1f;
                return;
            }

            _debugStretchRatio = stretchRatioEstimate;
            if (stretchRatioEstimate <= safeElasticStretchLimit)
            {
                _elasticBreakTimer = 0f;
                return;
            }

            float overStretch01 = Clamp01Finite((stretchRatioEstimate - safeElasticStretchLimit) / math.max(safeElasticStretchLimit, 0.001f));
            _elasticBreakTimer += ResolveDeltaTime(deltaTime) * LerpClamped(0.35f, 1.35f, Clamp01Finite(attraction01 + overStretch01));
            if (_elasticBreakTimer < safeElasticBreakHoldTime || _pendingElasticRupture)
                return;

            Vector3 ruptureDirection = _points[_points.Length - 1] - _points[_points.Length - 2];
            float ruptureLengthSq = ruptureDirection.sqrMagnitude;
            if (!math.isfinite(ruptureLengthSq) || ruptureLengthSq <= 0.0001f)
                ruptureDirection = attractorPositionWS - _anchorPositionWS;
            ruptureLengthSq = ruptureDirection.sqrMagnitude;
            if (!math.isfinite(ruptureLengthSq) || ruptureLengthSq <= 0.0001f)
                ruptureDirection = Vector3.up;

            _pendingElasticRuptureVelocityWS =
                SanitizeVelocity(
                    ResolveSafeDirection(ruptureDirection, Vector3.up) * (safeSegmentLength * safeElasticBreakRecoilMultiplier * LerpClamped(1f, 3.2f, overStretch01)) +
                    SanitizeVelocity(_velocities[_velocities.Length - 1]) * safeElasticBreakRecoilMultiplier);
            _pendingElasticRupture = true;
            _elasticBreakTimer = 0f;
        }

        private void EnsureChargeEffects()
        {
            if (_sparkParticles != null)
                return;

            Transform existing = transform.Find("CableSparkFX");
            GameObject sparkObject;
            if (existing != null)
            {
                sparkObject = existing.gameObject;
            }
            else
            {
                // COLD ALLOC: GameObject[1] - persistent EMP spark child for abyssal cable charge-up visuals - owner: BioCableIK
                sparkObject = new GameObject("CableSparkFX");
                sparkObject.transform.SetParent(transform, false);
                sparkObject.transform.localPosition = Vector3.zero;
                sparkObject.transform.localRotation = Quaternion.identity;
                sparkObject.transform.localScale = Vector3.one;
            }

            if (!_sparkParticles && !sparkObject.TryGetComponent(out _sparkParticles))
            {
                // COLD ALLOC: Component[1] - persistent particle system used for EMP nest pre-fire sparks - owner: BioCableIK
                _sparkParticles = sparkObject.AddComponent<ParticleSystem>();
            }

            if (_sparkParticles != null)
            {
                var main = _sparkParticles.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = 0.18f;
                main.startSpeed = 0.45f;
                main.startSize = 0.08f;
                main.maxParticles = 48;
                main.startColor = new Color(0.92f, 0.96f, 1f, 0.92f);

                var emission = _sparkParticles.emission;
                emission.enabled = true;
                emission.rateOverTime = 0f;

                var shape = _sparkParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                if (!_sparkParticles.isPlaying)
                    _sparkParticles.Play();
            }

            if (_sparkRenderer == null && _sparkParticles != null)
                _sparkParticles.TryGetComponent(out _sparkRenderer);

            if (_sparkRenderer != null && _sparkRenderer.sharedMaterial == null)
                _sparkRenderer.sharedMaterial = ResolveCableMaterial();
        }

        private void ApplyVisualState()
        {
            float chargeBlend = Clamp01Finite(_empCharge01 * LerpClamped(0.35f, 1f, _empPulse01));
            Color safeBaseColor = SanitizeColor(baseCableColor, new Color(0.12f, 0.52f, 0.46f, 0.92f));
            Color safeChargeColor = SanitizeColor(empChargeColor, new Color(0.95f, 0.98f, 1f, 0.98f));
            _currentCableColor = Color.Lerp(safeBaseColor, safeChargeColor, chargeBlend);
            _currentCableRadius = ResolveCableRadius() * (1f + ResolveRange(empWidthBoost, 0f, 4f, 0.3f) * chargeBlend);

            if (_sparkParticles != null)
            {
                float safeSparkThreshold = ResolveRange(sparkChargeThreshold, 0f, 1f, 0.28f);
                float sparkGate = Clamp01Finite((_empCharge01 - safeSparkThreshold) / math.max(1f - safeSparkThreshold, 0.001f));
                var emission = _sparkParticles.emission;
                emission.rateOverTime = ResolveRange(sparkEmissionRate, 0f, 128f, 42f) * sparkGate * LerpClamped(0.25f, 1f, _empPulse01);
                if (!_sparkParticles.isPlaying && sparkGate > 0f)
                    _sparkParticles.Play();
                else if (_sparkParticles.isPlaying && sparkGate <= 0f && !_isCableActive)
                    _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void UpdateSparkAnchor()
        {
            if (_sparkParticles == null || _points == null || _points.Length == 0)
                return;

            int sparkIndex = Mathf.Min(2, _points.Length - 1);
            Transform sparkTransform = _sparkParticles.transform;
            Vector3 sparkPosition = SanitizePosition(_points[sparkIndex], _anchorPositionWS);
            if (IsFinite(sparkPosition))
                sparkTransform.position = sparkPosition;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private static Vector3 SanitizePosition(Vector3 value, Vector3 fallback)
        {
            if (IsFinite(value))
                return value;

            return IsFinite(fallback)
                ? fallback
                : Vector3.zero;
        }

        private static Vector3 SanitizeVelocity(Vector3 value)
        {
            if (!math.isfinite(value.x) || !math.isfinite(value.y) || !math.isfinite(value.z))
                return Vector3.zero;

            float lengthSq = value.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0f)
                return Vector3.zero;

            float maxVelocitySq = MaximumCableVelocity * MaximumCableVelocity;
            if (lengthSq <= maxVelocitySq)
                return value;

            return value * (MaximumCableVelocity * math.rsqrt(lengthSq));
        }

        private static Color SanitizeColor(Color value, Color fallback)
        {
            if (math.isfinite(value.r) && math.isfinite(value.g) && math.isfinite(value.b) && math.isfinite(value.a))
                return value;

            return math.isfinite(fallback.r) &&
                   math.isfinite(fallback.g) &&
                   math.isfinite(fallback.b) &&
                   math.isfinite(fallback.a)
                ? fallback
                : Color.white;
        }

        private static float Clamp01Finite(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float ResolveRange(float value, float minimum, float maximum, float fallback)
        {
            float safeValue = math.isfinite(value) ? value : fallback;
            return math.clamp(safeValue, minimum, maximum);
        }

        private static float ResolveDeltaTime(float deltaTime)
        {
            return ResolveRange(deltaTime, 0f, MaximumDeltaTime, 0f);
        }

        private static float ResolveOscillationTime(float currentTime, float deltaTime)
        {
            float nextTime = (math.isfinite(currentTime) ? currentTime : 0f) + deltaTime;
            return math.isfinite(nextTime) && nextTime <= 4096f ? nextTime : 0f;
        }

        private float ResolveSegmentLength()
        {
            return ResolveRange(segmentLength, 0.1f, 128f, 1.25f);
        }

        private float ResolveCableRadius()
        {
            float safeRoot = ResolveRange(rootWidth, 0.01f, 1f, 0.18f);
            float safeTip = ResolveRange(tipWidth, 0.01f, safeRoot, 0.06f);
            return math.max(0.005f, (safeRoot + safeTip) * 0.25f);
        }

        private static float LerpClamped(float from, float to, float t)
        {
            return from + (to - from) * math.saturate(t);
        }

        private Vector3 ConstrainSegmentToLength(Vector3 previousPoint, Vector3 point)
        {
            float safeSegmentLength = ResolveSegmentLength();
            Vector3 safePreviousPoint = SanitizePosition(previousPoint, _anchorPositionWS);
            Vector3 safePoint = SanitizePosition(point, safePreviousPoint - _anchorUpWS * safeSegmentLength);
            Vector3 toPrevious = safePoint - safePreviousPoint;
            float distanceSq = toPrevious.sqrMagnitude;
            if (math.isfinite(distanceSq) && distanceSq > SegmentDistanceEpsilonSq)
                return safePreviousPoint + toPrevious * (safeSegmentLength * math.rsqrt(distanceSq));

            return safePreviousPoint - _anchorUpWS * safeSegmentLength;
        }

        private static Vector3 ResolveSafeDirection(Vector3 direction, Vector3 fallback)
        {
            if (!IsFinite(direction))
                direction = fallback;

            float lengthSq = direction.sqrMagnitude;
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return SanitizePosition(fallback, Vector3.up);

            float invLength = math.rsqrt(lengthSq);
            return direction * invLength;
        }

        private void SyncRenderer()
        {
            if (!_isCableActive || _points == null)
                return;

            float safeSegmentLength = ResolveSegmentLength();
            for (int i = 0; i < _points.Length; i++)
            {
                Vector3 fallback = i == 0 ? _anchorPositionWS : _points[i - 1] - _anchorUpWS * safeSegmentLength;
                _points[i] = SanitizePosition(_points[i], fallback);
            }

            Vector3 start = _points[0];
            Vector3 end = _points[_points.Length - 1];
            Vector3 startForward = _points.Length > 1 ? ResolveSafeDirection(_points[1] - start, _anchorUpWS) : _anchorUpWS;
            Vector3 endForward = _points.Length > 1 ? ResolveSafeDirection(_points[_points.Length - 2] - end, -_anchorUpWS) : -_anchorUpWS;
            SplineDescriptor descriptor = LogisticsPipeBuilder.CreateSocketDescriptor(
                start,
                end,
                startForward,
                endForward,
                _currentCableRadius,
                PipeRenderFlags.None);
            ConnectionSplineBatchRenderer.SubmitPipeLink(_splineLinkId, descriptor, _currentCableColor);
        }

        private Material ResolveCableMaterial()
        {
            if (cableMaterial != null)
                return cableMaterial;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedMissingCableMaterial)
            {
                _loggedMissingCableMaterial = true;
                Debug.LogError("[BioCableIK] Missing cableMaterial asset. Runtime material creation is forbidden for cable rendering.", this);
            }
#endif

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            segmentCount = Mathf.Clamp(segmentCount, 4, 24);
            segmentLength = ResolveRange(segmentLength, 0.1f, 128f, 1.25f);
            attractorSpring = ResolveRange(attractorSpring, 0f, 32f, 9.5f);
            damping = ResolveRange(damping, 0f, 4f, 1.45f);
            wrapStrength = ResolveRange(wrapStrength, 0f, 3f, 1.2f);
            rootWidth = ResolveRange(rootWidth, 0.01f, 1f, 0.18f);
            tipWidth = ResolveRange(tipWidth, 0.01f, rootWidth, 0.06f);
            sparkChargeThreshold = Clamp01Finite(sparkChargeThreshold);
            sparkEmissionRate = ResolveRange(sparkEmissionRate, 0f, 128f, 42f);
            empWidthBoost = ResolveRange(empWidthBoost, 0f, 4f, 0.3f);
            snapDamping = ResolveRange(snapDamping, 0f, 4f, 1.8f);
            snapVelocityCarry = ResolveRange(snapVelocityCarry, 0f, 8f, 2.6f);
            elasticStretchLimit = ResolveRange(elasticStretchLimit, 1f, 2.5f, 1.42f);
            elasticBreakHoldTime = ResolveRange(elasticBreakHoldTime, 0.02f, 1f, 0.14f);
            elasticBreakRecoilMultiplier = ResolveRange(elasticBreakRecoilMultiplier, 0f, 4f, 1.35f);

            if (_initialized && _points != null && _points.Length == Mathf.Clamp(segmentCount, 4, 24))
            {
                ApplyVisualState();
                SyncRenderer();
            }
        }
#endif
    }
}
