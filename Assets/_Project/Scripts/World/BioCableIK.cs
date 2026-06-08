using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
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
        private const int MinimumSegmentCount = 4;
        private const int MaximumSegmentCapacity = 24;
        private const int PredatorBiteContactCapacity = 8;
        private const byte TetherSnapReasonPredatorBite = 3;
        private const byte OxygenCutoffSeverityPredatorBite = OxygenCriticalSignal.CriticalSeverity;
        private const byte OxygenCutoffFlagPredatorBite = OxygenCriticalSignal.FlagLifeSupportCutoff;
        private const uint PredatorCableBiteSourceId = OxygenCriticalSignal.SourceBioCablePredatorBite;
#if UNITY_EDITOR
        private const string EditorDefaultCableMaterialPath = "Assets/_Project/Art/Materials/Nature/ProceduralOrganicMisc/Mat_Organic_PlantStem.mat";
#endif

        // COLD ALLOC: SpatialQueryHit[8] - fixed predator bite broadphase scratch shared by managed cable rigs - owner: BioCableIK
        private static readonly SpatialQueryHit[] s_predatorBiteHits = new SpatialQueryHit[PredatorBiteContactCapacity];
        private static int s_x001BioCableBiteSignalPushDropCount;

        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Shared authored material used by cable spark particles. Runtime fallback material creation is forbidden.")]
        private Material cableMaterial;

        [Header("── Cable Shape ─────────────────────")]
        [SerializeField, Range(MinimumSegmentCount, MaximumSegmentCapacity)]
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

        [SerializeField, Range(0f, 8f)]
        [Tooltip("World-space predator bite radius. Zero suppresses bite detection without changing cable allocation behavior.")]
        private float predatorBiteRadius = 1.1f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Minimum seconds between bite signal bursts from this cable rig.")]
        private float predatorBiteSignalCooldownSeconds = 0.35f;

        [Header("── EMP Charge Visuals ─────────────")]
        [SerializeField]
        [Tooltip("Authored or pooled spark particle root. Runtime particle-system construction is forbidden.")]
        private ParticleSystem authoredSparkParticles;

        [SerializeField]
        [Tooltip("Renderer paired with authoredSparkParticles. Runtime renderer construction is forbidden.")]
        private ParticleSystemRenderer authoredSparkRenderer;

        [SerializeField]
        [Tooltip("Optional prewarmed pool prefab for cable spark particles when no authored child is assigned.")]
        private GameObject authoredSparkPrefab;

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
        private int _pointCount;

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
        private IObjectPoolService _objectPoolService;
        private GameObject _sparkPooledInstance;
        private IObjectPoolService _sparkPooledInstancePool;
        private bool _sparkPooledInstanceOwned;
        private bool _sparkEffectResolutionAttempted;
        private float _nextPredatorBiteSignalTime;
        private float _lastSparkEmissionRate = -1f;

        private void Awake()
        {
            _splineLinkId = GetEntityId().GetHashCode();
            CacheObjectPoolServiceCold(null);
            ResolveRuntimeWiring();
            EnsureStorage();
            if (authoredSparkParticles != null || _objectPoolService != null)
                EnsureChargeEffects();
            InitializeAt(transform.position, Vector3.up);
        }

        private void OnDisable()
        {
            if (_sparkParticles != null && _sparkParticles.isPlaying)
                _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ConnectionSplineBatchRenderer.RemovePipeLink(_splineLinkId);
        }

        private void OnDestroy()
        {
            ReleaseSparkEffectToPool();
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

            for (int i = 0; i < _pointCount; i++)
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
            Vector3 safeAttractorPosition = SanitizePosition(attractorPositionWS, _points[_pointCount - 1]);
            Vector3 safeAttractorVelocity = SanitizeVelocity(attractorVelocityWS);
            Vector3 velocityBias = safeAttractorVelocity * LerpClamped(0.08f, 0.42f, clampedWrap);
            Vector3 attractorDirection = ResolveSafeDirection(safeAttractorVelocity, Vector3.forward);
            Vector3 wrapAxis = Vector3.Cross(_anchorUpWS, attractorDirection);
            float wrapAxisLengthSq = wrapAxis.sqrMagnitude;
            if (!math.isfinite(wrapAxisLengthSq) || wrapAxisLengthSq <= 0.0001f)
                wrapAxis = Vector3.Cross(_anchorUpWS, Vector3.right);
            wrapAxis = ResolveSafeDirection(wrapAxis, Vector3.up);

            for (int i = 1; i < _pointCount; i++)
            {
                float tail01 = i / (float)(_pointCount - 1);
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
            if (TryResolvePredatorCableBite(out Vector3 bitePoint, out SpatialQueryHit predatorHit))
            {
                TriggerPredatorCableBite(bitePoint, in predatorHit);
                return;
            }

            UpdateSparkAnchor();
            ApplyVisualState();
            SyncRenderer();
        }

        private bool TryResolvePredatorCableBite(out Vector3 bitePoint, out SpatialQueryHit predatorHit)
        {
            bitePoint = default;
            predatorHit = default;
            if (!_isCableActive || _points == null || _pointCount < 2)
                return false;

            float biteRadius = ResolveRange(predatorBiteRadius, 0f, 8f, 0f);
            if (biteRadius <= 0.0001f)
                return false;

            if (!TryResolveCableBroadphase(biteRadius, out Vector3 queryCenter, out float broadphaseRadius))
                return false;

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                queryCenter,
                broadphaseRadius,
                SpatialTargetKind.Bioform,
                s_predatorBiteHits);

            bool found = false;
            float bestDistanceSq = biteRadius * biteRadius;
            int safeHitCount = math.clamp(hitCount, 0, s_predatorBiteHits.Length);
            for (int i = 0; i < safeHitCount; i++)
            {
                SpatialQueryHit hit = s_predatorBiteHits[i];
                if (!(hit.Owner is IFaunaSpatialContact faunaContact) ||
                    !IsPredatorCableBiteContact(faunaContact) ||
                    !TryResolveClosestCablePoint(hit.Position, bestDistanceSq, out Vector3 candidatePoint, out float candidateDistanceSq))
                {
                    continue;
                }

                bestDistanceSq = candidateDistanceSq;
                bitePoint = candidatePoint;
                predatorHit = hit;
                found = true;
            }

            ClearPredatorBiteHits(safeHitCount);
            return found;
        }

        private bool TryResolveCableBroadphase(float biteRadius, out Vector3 queryCenter, out float queryRadius)
        {
            queryCenter = default;
            queryRadius = 0f;
            if (_points == null || _pointCount <= 0)
                return false;

            Vector3 first = SanitizePosition(_points[0], _anchorPositionWS);
            float3 min = new float3(first.x, first.y, first.z);
            float3 max = min;
            for (int i = 1; i < _pointCount; i++)
            {
                Vector3 point = SanitizePosition(_points[i], first);
                float3 p = new float3(point.x, point.y, point.z);
                min = math.min(min, p);
                max = math.max(max, p);
            }

            float3 center = (min + max) * 0.5f;
            if (!math.all(math.isfinite(center)))
                return false;

            float radiusSq = 0f;
            for (int i = 0; i < _pointCount; i++)
            {
                Vector3 point = SanitizePosition(_points[i], first);
                float3 p = new float3(point.x, point.y, point.z);
                float distanceSq = math.lengthsq(p - center);
                if (math.isfinite(distanceSq))
                    radiusSq = math.max(radiusSq, distanceSq);
            }

            float cableRadius = radiusSq > SegmentDistanceEpsilonSq
                ? radiusSq * math.rsqrt(radiusSq)
                : 0f;
            queryRadius = math.max(biteRadius, cableRadius + biteRadius);
            queryCenter = new Vector3(center.x, center.y, center.z);
            return IsFinite(queryCenter) && math.isfinite(queryRadius) && queryRadius > 0.0001f;
        }

        private static bool IsPredatorCableBiteContact(IFaunaSpatialContact contact)
        {
            return contact != null &&
                   !contact.IsDead &&
                   (contact.IsApexPredatorContact || contact.IsAggressiveContact || contact.IsLeviathanContact);
        }

        private bool TryResolveClosestCablePoint(Vector3 predatorPosition, float maxDistanceSq, out Vector3 closestPoint, out float closestDistanceSq)
        {
            closestPoint = default;
            closestDistanceSq = maxDistanceSq;
            if (!IsFinite(predatorPosition) || _points == null || _pointCount < 2)
                return false;

            bool found = false;
            for (int i = 1; i < _pointCount; i++)
            {
                Vector3 a = _points[i - 1];
                Vector3 b = _points[i];
                Vector3 ab = b - a;
                float abLengthSq = ab.sqrMagnitude;
                if (!math.isfinite(abLengthSq) || abLengthSq <= SegmentDistanceEpsilonSq)
                    continue;

                float t = Vector3.Dot(predatorPosition - a, ab) * math.rcp(abLengthSq);
                Vector3 point = a + ab * math.saturate(t);
                Vector3 delta = predatorPosition - point;
                float distanceSq = delta.sqrMagnitude;
                if (!math.isfinite(distanceSq) || distanceSq > closestDistanceSq)
                    continue;

                closestDistanceSq = distanceSq;
                closestPoint = point;
                found = true;
            }

            return found;
        }

        private void TriggerPredatorCableBite(Vector3 bitePoint, in SpatialQueryHit predatorHit)
        {
            Vector3 predatorPosition = SanitizePosition(predatorHit.Position, bitePoint);
            Vector3 recoilDirection = ResolveSafeDirection(bitePoint - predatorPosition, _anchorUpWS);
            TriggerSnapRecoil(recoilDirection * math.max(2f, ResolveSegmentLength() * 3f), 0.45f);
            SetEmpCharge(0f, 0f);
            SetCableActive(false);

            float now = Time.unscaledTime;
            float cooldown = ResolveRange(predatorBiteSignalCooldownSeconds, 0f, 2f, 0.35f);
            if (now < _nextPredatorBiteSignalTime)
                return;

            _nextPredatorBiteSignalTime = now + cooldown;
            PublishPredatorCableBiteSignals(bitePoint);
        }

        private static void PublishPredatorCableBiteSignals(Vector3 bitePoint)
        {
            uint frame = unchecked((uint)Time.frameCount);
            OxygenCriticalSignal oxygenCritical = default;
            oxygenCritical.Oxygen01 = 0f;
            oxygenCritical.SecondsRemaining = 0f;
            oxygenCritical.SourceId = PredatorCableBiteSourceId;
            oxygenCritical.Frame = frame;
            oxygenCritical.Severity = OxygenCutoffSeverityPredatorBite;
            oxygenCritical.Flags = OxygenCutoffFlagPredatorBite;
            SignalBus<OxygenCriticalSignal>.TryPushTracked(in oxygenCritical, ref s_x001BioCableBiteSignalPushDropCount);

            HypoxiaSignal hypoxia = default;
            hypoxia.Oxygen01 = 0f;
            hypoxia.SecondsRemaining = 0f;
            hypoxia.SourceId = PredatorCableBiteSourceId;
            hypoxia.Frame = frame;
            hypoxia.Severity = OxygenCutoffSeverityPredatorBite;
            hypoxia.Flags = OxygenCutoffFlagPredatorBite;
            SignalBus<HypoxiaSignal>.TryPushTracked(in hypoxia, ref s_x001BioCableBiteSignalPushDropCount);

            HapticPulseSignal haptic = default;
            haptic.LowFrequencyMotor01 = 0.9f;
            haptic.HighFrequencyMotor01 = 1f;
            haptic.DurationSeconds = 0.18f;
            haptic.PriorityFlags = HapticPulseSignal.PriorityCollision;
            SignalBus<HapticPulseSignal>.TryPushTracked(in haptic, ref s_x001BioCableBiteSignalPushDropCount);

            if (TryResolveAupFromRuntimePosition(bitePoint, out AbsoluteUniversePosition biteAup))
            {
                TetherSnappedSignal snap = default;
                snap.SnapAup = biteAup;
                snap.TetherId = PredatorCableBiteSourceId;
                snap.FrameIndex = frame;
                snap.PeakTension = 0f;
                snap.SnapThreshold = 0f;
                snap.Severity01 = 1f;
                snap.NodeCount = 0;
                snap.Reason = TetherSnapReasonPredatorBite;
                snap.Flags = OxygenCutoffFlagPredatorBite;
                SignalBus<TetherSnappedSignal>.TryPushTracked(in snap, ref s_x001BioCableBiteSignalPushDropCount);
            }
        }

        private static bool TryResolveAupFromRuntimePosition(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static void ClearPredatorBiteHits(int count)
        {
            int safeCount = math.clamp(count, 0, s_predatorBiteHits.Length);
            for (int i = 0; i < safeCount; i++)
                s_predatorBiteHits[i] = default;
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

            for (int i = 1; i < _pointCount; i++)
            {
                float tail01 = i / (float)(_pointCount - 1);
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
            float nextCharge01 = Clamp01Finite(charge01);
            float nextPulse01 = Clamp01Finite(pulse01);
            if (math.abs(_empCharge01 - nextCharge01) <= 0.0001f &&
                math.abs(_empPulse01 - nextPulse01) <= 0.0001f)
            {
                return;
            }

            _empCharge01 = nextCharge01;
            _empPulse01 = nextPulse01;
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

            for (int i = 1; i < _pointCount; i++)
            {
                float tail01 = i / (float)(_pointCount - 1);
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
            if (_isCableActive == isActive)
            {
                if (!isActive && _sparkParticles != null && _sparkParticles.isPlaying)
                    _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

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

        public void SetCableMaterialCold(Material material)
        {
            if (material == null)
                return;

            cableMaterial = material;
            if (_sparkRenderer != null && _sparkRenderer.sharedMaterial == null)
                _sparkRenderer.sharedMaterial = material;
        }

        public void PrepareForPoolReturnCold()
        {
            SetCableActive(false);
            ReleaseSparkEffectToPool();
        }

        public void ConfigureObjectPoolServiceCold(IObjectPoolService objectPoolService)
        {
            CacheObjectPoolServiceCold(objectPoolService);
            if (authoredSparkParticles != null || _objectPoolService != null)
                EnsureChargeEffects();
        }

        private void CacheObjectPoolServiceCold(IObjectPoolService objectPoolService)
        {
            ObjectPoolManager candidate = objectPoolService as ObjectPoolManager;
            ObjectPoolManager pool = candidate;
            if (!ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) &&
                !ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                pool = null;
            }

            if (ReferenceEquals(_objectPoolService, pool))
                return;

            _objectPoolService = pool;
            if (_sparkParticles == null && authoredSparkPrefab != null && pool != null)
                _sparkEffectResolutionAttempted = false;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPoolService = resolved;
                pool = resolved;
                return true;
            }

            _objectPoolService = null;
            pool = null;
            return false;
        }

        private static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)
        {
            return ObjectPoolManager.CanDespawnWithPool(pool, instance);
        }

        private void ResolveRuntimeWiring()
        {
            _currentCableColor = SanitizeColor(baseCableColor, new Color(0.12f, 0.52f, 0.46f, 0.92f));
            _currentCableRadius = ResolveCableRadius();
        }

        private void EnsureStorage()
        {
            _pointCount = Mathf.Clamp(segmentCount, MinimumSegmentCount, MaximumSegmentCapacity);
            if (_points == null ||
                _velocities == null ||
                _points.Length != MaximumSegmentCapacity ||
                _velocities.Length != MaximumSegmentCapacity)
            {
                // COLD ALLOC: Vector3[24] - cable point positions for manager-driven IK simulation - owner: BioCableIK
                _points = new Vector3[MaximumSegmentCapacity];
                // COLD ALLOC: Vector3[24] - cable point velocities for manager-driven IK simulation - owner: BioCableIK
                _velocities = new Vector3[MaximumSegmentCapacity];
            }
        }

        private void UpdateElasticRupture(float deltaTime, Vector3 attractorPositionWS, float attraction01)
        {
            if (_points == null || _pointCount <= 1)
                return;

            float safeSegmentLength = ResolveSegmentLength();
            float safeElasticStretchLimit = ResolveRange(elasticStretchLimit, 1f, 2.5f, 1.42f);
            float safeElasticBreakHoldTime = ResolveRange(elasticBreakHoldTime, 0.02f, 1f, 0.14f);
            float safeElasticBreakRecoilMultiplier = ResolveRange(elasticBreakRecoilMultiplier, 0f, 4f, 1.35f);
            float restLength = safeSegmentLength * (_pointCount - 1);
            if (restLength <= 0.0001f)
                return;

            float tailDistanceSq = (_points[_pointCount - 1] - _anchorPositionWS).sqrMagnitude;
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

            Vector3 ruptureDirection = _points[_pointCount - 1] - _points[_pointCount - 2];
            float ruptureLengthSq = ruptureDirection.sqrMagnitude;
            if (!math.isfinite(ruptureLengthSq) || ruptureLengthSq <= 0.0001f)
                ruptureDirection = attractorPositionWS - _anchorPositionWS;
            ruptureLengthSq = ruptureDirection.sqrMagnitude;
            if (!math.isfinite(ruptureLengthSq) || ruptureLengthSq <= 0.0001f)
                ruptureDirection = Vector3.up;

            _pendingElasticRuptureVelocityWS =
                SanitizeVelocity(
                    ResolveSafeDirection(ruptureDirection, Vector3.up) * (safeSegmentLength * safeElasticBreakRecoilMultiplier * LerpClamped(1f, 3.2f, overStretch01)) +
                    SanitizeVelocity(_velocities[_pointCount - 1]) * safeElasticBreakRecoilMultiplier);
            _pendingElasticRupture = true;
            _elasticBreakTimer = 0f;
        }

        private void EnsureChargeEffects()
        {
            if (_sparkParticles != null)
                return;

            if (_sparkEffectResolutionAttempted)
                return;

            _sparkEffectResolutionAttempted = true;

            if (authoredSparkParticles != null)
            {
                _sparkParticles = authoredSparkParticles;
                _lastSparkEmissionRate = -1f;
                if (authoredSparkRenderer != null)
                    _sparkRenderer = authoredSparkRenderer;
            }
            else if (authoredSparkPrefab != null)
            {
                if (!TryResolveCachedObjectPool(out IObjectPoolService pool) ||
                    !pool.HasPool(authoredSparkPrefab) ||
                    pool.GetAvailableCount(authoredSparkPrefab) <= 0)
                {
                    _sparkEffectResolutionAttempted = false;
                    return;
                }

                GameObject sparkObject = pool.Spawn(authoredSparkPrefab, transform.position, transform.rotation, false);
                if (sparkObject == null)
                {
                    _sparkEffectResolutionAttempted = false;
                    return;
                }

                if (!CanDespawnWithPool(pool, sparkObject))
                {
                    pool.Despawn(sparkObject);
                    return;
                }

                if (sparkObject.TryGetComponent(out _sparkParticles))
                {
                    _lastSparkEmissionRate = -1f;
                    _sparkPooledInstance = sparkObject;
                    _sparkPooledInstanceOwned = true;
                    _sparkPooledInstancePool = pool;
                    Transform sparkTransform = sparkObject.transform;
                    sparkTransform.SetParent(transform, false);
                    sparkTransform.localPosition = Vector3.zero;
                    sparkTransform.localRotation = Quaternion.identity;
                    sparkTransform.localScale = Vector3.one;
                }
                else
                {
                    pool.Despawn(sparkObject);
                }
            }

            if (_sparkParticles == null)
                return;

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
                _lastSparkEmissionRate = 0f;

                var shape = _sparkParticles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;

                if (_sparkParticles.isPlaying && !_isCableActive)
                    _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_sparkRenderer == null && _sparkParticles != null)
                _sparkParticles.TryGetComponent(out _sparkRenderer);

            if (_sparkRenderer != null && _sparkRenderer.sharedMaterial == null)
                _sparkRenderer.sharedMaterial = ResolveCableMaterial();
        }

        private void ReleaseSparkEffectToPool()
        {
            if (_sparkPooledInstance == null)
                return;

            IObjectPoolService pool = _sparkPooledInstanceOwned ? _sparkPooledInstancePool : null;
            if (ObjectPoolManager.TryResolvePoolForInstance(_sparkPooledInstance, pool, out IObjectPoolService ownerPool))
                ownerPool.Despawn(_sparkPooledInstance);
            else
                Destroy(_sparkPooledInstance);

            _sparkPooledInstance = null;
            _sparkPooledInstancePool = null;
            _sparkPooledInstanceOwned = false;
            _sparkParticles = authoredSparkParticles;
            _sparkRenderer = authoredSparkRenderer;
            _lastSparkEmissionRate = -1f;
            _sparkEffectResolutionAttempted =
                authoredSparkParticles == null &&
                (authoredSparkPrefab == null || _objectPoolService == null);
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
                float nextSparkEmissionRate = ResolveRange(sparkEmissionRate, 0f, 128f, 42f) * sparkGate * LerpClamped(0.25f, 1f, _empPulse01);
                if (math.abs(_lastSparkEmissionRate - nextSparkEmissionRate) > 0.001f)
                {
                    var emission = _sparkParticles.emission;
                    emission.rateOverTime = nextSparkEmissionRate;
                    _lastSparkEmissionRate = nextSparkEmissionRate;
                }
                if (!_sparkParticles.isPlaying && sparkGate > 0f)
                    _sparkParticles.Play();
                else if (_sparkParticles.isPlaying && sparkGate <= 0f)
                    _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void UpdateSparkAnchor()
        {
            if (_sparkParticles == null || _points == null || _pointCount == 0)
                return;

            int sparkIndex = Mathf.Min(2, _pointCount - 1);
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
            if (!_isCableActive || _points == null || _pointCount <= 0)
                return;

            float safeSegmentLength = ResolveSegmentLength();
            for (int i = 0; i < _pointCount; i++)
            {
                Vector3 fallback = i == 0 ? _anchorPositionWS : _points[i - 1] - _anchorUpWS * safeSegmentLength;
                _points[i] = SanitizePosition(_points[i], fallback);
            }

            Vector3 start = _points[0];
            Vector3 end = _points[_pointCount - 1];
            Vector3 startForward = _pointCount > 1 ? ResolveSafeDirection(_points[1] - start, _anchorUpWS) : _anchorUpWS;
            Vector3 endForward = _pointCount > 1 ? ResolveSafeDirection(_points[_pointCount - 2] - end, -_anchorUpWS) : -_anchorUpWS;
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

#if UNITY_EDITOR
            cableMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(EditorDefaultCableMaterialPath);
            if (cableMaterial != null)
                return cableMaterial;
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_loggedMissingCableMaterial)
            {
                _loggedMissingCableMaterial = true;
                Hecton8.Core.H8Debug.LogError("[BioCableIK] Missing cableMaterial asset. Runtime material creation is forbidden for cable rendering.", this);
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

            segmentCount = Mathf.Clamp(segmentCount, MinimumSegmentCount, MaximumSegmentCapacity);
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

            if (_initialized && _points != null)
            {
                _pointCount = Mathf.Clamp(segmentCount, MinimumSegmentCount, MaximumSegmentCapacity);
                ApplyVisualState();
                SyncRenderer();
            }
        }
#endif
    }
}
