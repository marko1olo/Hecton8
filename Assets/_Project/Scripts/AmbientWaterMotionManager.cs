// ============================================================================
// HECTON-8 - AmbientWaterMotionManager.cs
// Centralized visual bob/sway updater. One tick for many decorative props.
//
// v1.1 OPTIMIZATIONS:
//   [FIX] Player AUP is consumed through cached IPlayerRuntimeContext only.
//   [FIX] Register: replaced Contains (O(n)) with HashSet-backed O(1) dedupe.
//   [FIX] ApplyMotion: caches worldPos from CachedTransform.position once,
//         then passes it to ShouldUpdate to avoid a second bridge position read.
//   [FIX] ShouldUpdate: accepts worldPos as a parameter; repeated .position read removed.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4900)]
    [AddComponentMenu("Hecton/Physics/Ambient Water Motion Manager")]
    public sealed class AmbientWaterMotionManager : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IBiomeMatrixEventListener, IGlobalRegistryHotSwapListener
    {
        private const float BiomeFlowBlendInvSeconds = 0.2f;
        private const float DegreesToHalfRadians = 0.008726646259971648f;
        private const float LodHysteresisMultiplier = 1.12f;
        private const float MaxVisualRotationDegrees = 24f;
        private const float MaxRuntimeDeltaTimeSeconds = 0.25f;
        private const float MaxAmbientMotionCurrentMetersPerSecond = 12f;
        private const float MaxAmbientMotionAmplitudeMeters = 2f;
        private const float MaxAmbientMotionFrequency = 8f;
        private const float MaxAmbientMotionCoupling = 2f;
        private const int MotionCapacity = 128;
        private const uint AmbientMotionRegistrationCapacityWarningHash = 0x414D5243u;
        private const uint AmbientMotionSystemContextHash = 0x414D4F54u;
        private const byte LodBandNear = 0;
        private const byte LodBandMedium = 1;
        private const byte LodBandFar = 2;
        private const byte LodBandCull = 3;
        private const byte LodBandOutside = 4;

        [Header("Observer / LOD")]
        [SerializeField] private Transform lodObserver;
        [SerializeField] private float nearDistance    = 20f;
        [SerializeField] private float mediumDistance  = 45f;
        [SerializeField] private float farDistance     = 90f;
        [SerializeField] private float cullDistance    = 150f;
        [SerializeField, Range(1, 8)]  private int mediumDivisor = 2;
        [SerializeField, Range(1, 16)] private int farDivisor    = 4;
        [SerializeField, Range(1, 32)] private int cullDivisor   = 8;

        [Header("Global")]
        [SerializeField] private float globalAmplitude = 1f;
        [SerializeField] private float globalFrequency = 1f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugActiveObjects;
        [SerializeField] private int _debugDroppedRegistrationCount;
        [SerializeField] private int _debugNearCount;
        [SerializeField] private int _debugMediumCount;
        [SerializeField] private int _debugFarCount;
        [SerializeField] private int _debugCulledCount;
        [SerializeField] private int _debugBiomeCurrentBiomeId = -1;
        [SerializeField] private Vector3 _debugBiomeCurrentVector;

        // Registered objects.
        // List handles cache-friendly iteration; HashSet provides O(1) registration dedupe.
        private readonly List<AmbientWaterMotion> _objects =
            new List<AmbientWaterMotion>(MotionCapacity); // COLD ALLOC: List<AmbientWaterMotion>[128] - active ambient-water motion registry - owner: AmbientWaterMotionManager
        private readonly HashSet<AmbientWaterMotion> _objectsSet =
            new HashSet<AmbientWaterMotion>(MotionCapacity); // COLD ALLOC: HashSet<AmbientWaterMotion>[128] - duplicate guard for ambient-water motion registry - owner: AmbientWaterMotionManager

        private float _time;
        private int   _frameCounter;
        private float _nearDistanceSqr;
        private float _mediumDistanceSqr;
        private float _farDistanceSqr;
        private float _cullDistanceSqr;
        private int _mediumFrameMask;
        private int _farFrameMask;
        private int _cullFrameMask;
        private bool _tickRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _runtimeWaterMotionCallbacksActive;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private Vector3 _biomeCurrentVector;
        private Vector3 _biomeCurrentStartVector;
        private Vector3 _biomeCurrentTargetVector;
        private float _biomeCurrentBlendElapsed;
        private bool _hasBiomeCurrentTarget;
        private float _pendingVisualDeltaTime;
        private int _droppedRegistrationCount;
        private int _lastRegistrationOverflowWarningFrame = -1;

        private static AmbientWaterMotionManager s_activeRuntime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            s_activeRuntime = null;
        }

        public int DroppedRegistrationCount => _droppedRegistrationCount;

        //  LIFECYCLE

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            RefreshDistanceThresholds();
            CacheRegistryServicesCold();
            _runtimeWaterMotionCallbacksActive = Application.isPlaying;
        }

        private void OnEnable()
        {
            _runtimeWaterMotionCallbacksActive = Application.isPlaying;
            if (TryAbortForUsableExistingRuntime())
                return;

            CacheRegistryServicesCold();
            TryRegisterService();
            if (!_serviceRegistered)
                return;

            TryRegisterHotSwapListener();
            TryRegister();
            if (_runtimeWaterMotionCallbacksActive)
                BiomeMatrixEvents.Register(this);
        }

        private void OnDisable()
        {
            _runtimeWaterMotionCallbacksActive = false;
            ResetInterruptedVisualCadence();
            BiomeMatrixEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            _runtimeWaterMotionCallbacksActive = false;
            ResetInterruptedVisualCadence();
            BiomeMatrixEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();

        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _ambientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (isActiveAndEnabled)
                    {
                        if (currentService != null)
                            TryRegister();
                    }
                    break;
            }
        }

        //  REGISTRATION - O(1) dedupe through HashSet

        public bool Register(AmbientWaterMotion motion)
        {
            if (motion == null)
                return false;

            if (_objectsSet.Contains(motion))
            {
                _debugActiveObjects = _objects.Count;
                return true;
            }

            if (_objects.Count >= MotionCapacity)
            {
                ReportRegistrationCapacityExceeded();
                _debugActiveObjects = _objects.Count;
                return false;
            }

            if (_objectsSet.Add(motion))
            {
                _objects.Add(motion);
                _debugActiveObjects = _objects.Count;
                return true;
            }

            _debugActiveObjects = _objects.Count;
            return false;
        }

        public void Unregister(AmbientWaterMotion motion)
        {
            if (motion == null) return;

            if (_objectsSet.Remove(motion))
            {
                int index = _objects.IndexOf(motion);
                if (index >= 0)
                    RemoveMotionAtSwapBack(index);
            }

            _debugActiveObjects = _objects.Count;
        }

        private void ReportRegistrationCapacityExceeded()
        {
            _droppedRegistrationCount++;
            _debugDroppedRegistrationCount = _droppedRegistrationCount;

            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            if (_lastRegistrationOverflowWarningFrame == currentFrame)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                AmbientMotionRegistrationCapacityWarningHash,
                AmbientMotionSystemContextHash,
                _droppedRegistrationCount);
            _lastRegistrationOverflowWarningFrame = currentFrame;
        }

        //  TICK

        public void Tick(float deltaTime)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
            {
                ResetInterruptedVisualCadence();
                return;
            }

            _pendingVisualDeltaTime = SanitizeDeltaTime(_pendingVisualDeltaTime) + SanitizeDeltaTime(deltaTime);
            TryRegisterLateFrame();
        }

        public void LateFrameTick()
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
            {
                ResetInterruptedVisualCadence();
                return;
            }

            float deltaTime = _pendingVisualDeltaTime > 0f ? _pendingVisualDeltaTime : SystemDispatcher.CurrentFrameDeltaTime;
            deltaTime = SanitizeDeltaTime(deltaTime);
            _pendingVisualDeltaTime = 0f;
            UpdateBiomeCurrentBlend(deltaTime);

            if (_objects.Count == 0) return;

            _frameCounter++;
            _time = AdvanceRuntimeTime(_time, deltaTime);

            _debugNearCount   = 0;
            _debugMediumCount = 0;
            _debugFarCount    = 0;
            _debugCulledCount = 0;

            // Cache observer AUP once per tick; Transform.position is presentation only.
            bool hasObserverAup = TryResolveObserverAup(out AbsoluteUniversePosition observerAup);
            float quality = ResolveGlobalQualityWeight();

            // Distance squares are resolved once per tick.
            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                AmbientWaterMotion motion = _objects[i];

                // Object may have been destroyed without OnDisable.
                if (motion == null || motion.CachedTransform == null)
                {
                    // Swap-and-pop: O(1) removal from the active list.
                    _objectsSet.Remove(motion);
                    RemoveMotionAtSwapBack(i);
                    continue;
                }

                bool hasMotionAup = motion.HasRestAup && motion.RestAup.IsFinite();
                AbsoluteUniversePosition motionAup = hasMotionAup ? motion.RestAup : default;
                if (!TryResolveRuntimeWorldPosition(motion, in motionAup, hasMotionAup, out Vector3 worldPos))
                {
                    motion.ManagerDistanceLodBand = LodBandCull;
                    _debugCulledCount++;
                    continue;
                }

                if (!ShouldUpdateAup(motion, i, motionAup, hasMotionAup, observerAup, hasObserverAup,
                                  _nearDistanceSqr, _mediumDistanceSqr, _farDistanceSqr, _cullDistanceSqr, quality))
                    continue;

                ApplyMotion(motion, worldPos);
            }

            _debugActiveObjects = _objects.Count;
        }

        private void ResetInterruptedVisualCadence()
        {
            _pendingVisualDeltaTime = 0f;
        }

        private void RemoveMotionAtSwapBack(int index)
        {
            int last = _objects.Count - 1;
            _objects[index] = _objects[last];
            _objects.RemoveAt(last);
        }

        //  SHOULD UPDATE - precomputed input, no bridge calls

        private bool ShouldUpdateAup(
            AmbientWaterMotion motion,
            int index,
            in AbsoluteUniversePosition motionAup,
            bool hasMotionAup,
            in AbsoluteUniversePosition observerAup,
            bool hasObserverAup,
            float nearSq,
            float mediumSq,
            float farSq,
            float cullSq,
            float quality)
        {
            if (!motion.AllowDistanceLod || !hasObserverAup)
            {
                motion.ManagerDistanceLodBand = LodBandNear;
                _debugNearCount++;
                return true;
            }

            if (!hasMotionAup)
            {
                motion.ManagerDistanceLodBand = LodBandMedium;
                _debugMediumCount++;
                return ((_frameCounter + index) & ResolveQualityScaledFrameMask(_mediumFrameMask, quality)) == 0;
            }

            float bias = ResolveLodBias(motion.LodBias);
            double biasSq = (double)bias * bias;
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in motionAup, in observerAup);
            byte lodBand = ResolveDistanceLodBand(
                motion.ManagerDistanceLodBand,
                distanceSq,
                biasSq,
                nearSq,
                mediumSq,
                farSq,
                cullSq);
            motion.ManagerDistanceLodBand = lodBand;

            if (lodBand == LodBandNear)
            {
                _debugNearCount++;
                return true;
            }

            if (lodBand == LodBandMedium)
            {
                _debugMediumCount++;
                return ((_frameCounter + index) & ResolveQualityScaledFrameMask(_mediumFrameMask, quality)) == 0;
            }

            if (lodBand == LodBandFar)
            {
                _debugFarCount++;
                return ((_frameCounter + index) & ResolveQualityScaledFrameMask(_farFrameMask, quality)) == 0;
            }

            _debugCulledCount++;
            return lodBand == LodBandCull
                && ((_frameCounter + index) & ResolveQualityScaledFrameMask(_cullFrameMask, quality)) == 0;
        }

        private static byte ResolveDistanceLodBand(
            byte previousBand,
            double distanceSq,
            double biasSq,
            float nearSq,
            float mediumSq,
            float farSq,
            float cullSq)
        {
            if (double.IsNaN(distanceSq) || double.IsInfinity(distanceSq) || distanceSq < 0d)
                return LodBandOutside;

            if (double.IsNaN(biasSq) || double.IsInfinity(biasSq) || biasSq <= 0d)
                biasSq = 1d;

            nearSq = ResolveDistanceLimitSqr(nearSq, 1f);
            mediumSq = math.max(ResolveDistanceLimitSqr(mediumSq, nearSq), nearSq);
            farSq = math.max(ResolveDistanceLimitSqr(farSq, mediumSq), mediumSq);
            cullSq = math.max(ResolveDistanceLimitSqr(cullSq, farSq), farSq);

            double hysteresisSq = (double)LodHysteresisMultiplier * LodHysteresisMultiplier;
            double nearLimit = (double)nearSq * biasSq;
            double mediumLimit = (double)mediumSq * biasSq;
            double farLimit = (double)farSq * biasSq;
            double cullLimit = (double)cullSq * biasSq;

            if (previousBand == LodBandNear)
                nearLimit *= hysteresisSq;
            if (distanceSq <= nearLimit)
                return LodBandNear;

            if (previousBand == LodBandMedium)
                mediumLimit *= hysteresisSq;
            if (distanceSq <= mediumLimit)
                return LodBandMedium;

            if (previousBand == LodBandFar)
                farLimit *= hysteresisSq;
            if (distanceSq <= farLimit)
                return LodBandFar;

            if (previousBand == LodBandCull)
                cullLimit *= hysteresisSq;
            return distanceSq <= cullLimit ? LodBandCull : LodBandOutside;
        }

        private static int ResolveQualityScaledFrameMask(int baseMask, float quality)
        {
            int baseDivisor = math.max(1, baseMask + 1);
            float scaledDivisor = math.lerp((float)baseDivisor, 1f, math.saturate(quality));
            return NormalizeCadenceDivisor((int)math.ceil(scaledDivisor)) - 1;
        }

        private static bool TryResolveRuntimeWorldPosition(
            AmbientWaterMotion motion,
            in AbsoluteUniversePosition motionAup,
            bool hasMotionAup,
            out Vector3 worldPos)
        {
            return hasMotionAup
                ? TryResolveRuntimePosition(in motionAup, out worldPos)
                : TryResolvePresentationRestWorldPosition(motion, out worldPos);
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition aup, out Vector3 runtimePosition)
        {
            if (!aup.IsFinite())
            {
                runtimePosition = Vector3.zero;
                return false;
            }

            float3 runtime = aup.ToRuntimeFloat3();
            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return IsFinite(runtimePosition);
        }

        private static bool TryResolvePresentationRestWorldPosition(AmbientWaterMotion motion, out Vector3 worldPosition)
        {
            Transform tr = motion.CachedTransform;
            Transform parent = tr != null ? tr.parent : null;
            worldPosition = parent != null
                ? parent.TransformPoint(motion.RestLocalPosition)
                : motion.RestLocalPosition;
            return IsFinite(worldPosition);
        }

        private bool TryResolveObserverAup(out AbsoluteUniversePosition observerAup)
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                snapshot.Aup.IsFinite())
            {
                observerAup = snapshot.Aup;
                return true;
            }

            if (playerContext != null)
            {
                observerAup = default;
                return false;
            }

            Transform observer = lodObserver;
            if (observer != null)
            {
                observerAup = AbsoluteUniversePosition.FromRuntimePosition(observer.position);
                return observerAup.IsFinite();
            }

            observerAup = default;
            return false;
        }

        private void ApplyMotion(AmbientWaterMotion motion, Vector3 worldPos)
        {
            if (!IsFinite(worldPos))
                return;

            Transform tr = motion.CachedTransform;

            float coupling = ResolveMotionCoupling(motion.CurrentCoupling);
            Vector3 current = Vector3.zero;
            if (coupling > 0.0001f)
            {
                Vector3 volumeCurrent = Vector3.zero;
                IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
                if (ambientCurrent != null)
                    ambientCurrent.TrySampleAuthoredCurrent(worldPos, out volumeCurrent);
                volumeCurrent = ClampFiniteVector(volumeCurrent, MaxAmbientMotionCurrentMetersPerSecond);

                float3 phantomCurrent = CurrentManager.SampleHorizontal(
                    new float3(worldPos.x, worldPos.y, worldPos.z),
                    _time,
                    0.018f,
                    0.12f,
                    1f);
                if (!math.all(math.isfinite(phantomCurrent)))
                    phantomCurrent = float3.zero;

                current = (volumeCurrent
                    + new Vector3(phantomCurrent.x, phantomCurrent.y, phantomCurrent.z)
                    + _biomeCurrentVector) * coupling;
                current = ClampFiniteVector(current, MaxAmbientMotionCurrentMetersPerSecond);
            }

            float currentSqrMagnitude = current.x * current.x + current.y * current.y + current.z * current.z;
            float currentMagnitude = ApproximateVectorMagnitude(current);
            Vector3 currentDir = currentSqrMagnitude > 0.0001f
                ? current * math.rsqrt(currentSqrMagnitude)
                : Vector3.forward;

            float time = SanitizeNonNegativeSeconds(_time);
            float phase = math.isfinite(motion.Phase) ? motion.Phase : 0f;
            float frequency = ResolveMotionFrequency(motion.BaseFrequency) * ResolveMotionFrequency(globalFrequency);
            float t = (time + phase) * frequency;

            Vector3 positionalAmplitude = ClampFiniteVector(motion.PositionalAmplitude, MaxAmbientMotionAmplitudeMeters);
            Vector3 angularAmplitude = ClampFiniteVector(motion.AngularAmplitude, MaxVisualRotationDegrees);
            float verticalAmplitude = ResolveMotionAmplitude(motion.VerticalAmplitude);
            float amplitude = ResolveMotionAmplitude(globalAmplitude);

            float bobY = FastTriangleSigned(t * 1.13f) * verticalAmplitude;
            float bobX = FastTriangleSigned(t * 0.91f) * positionalAmplitude.x;
            float bobZ = FastTriangleSigned(t * 1.07f + 1.5707964f) * positionalAmplitude.z;

            Vector3 offset = new Vector3(
                bobX + currentDir.x * currentMagnitude * 0.03f,
                bobY,
                bobZ + currentDir.z * currentMagnitude * 0.03f)
                * amplitude;

            float pitch = FastTriangleSigned(t * 0.87f) * angularAmplitude.x
                        + currentDir.z * currentMagnitude * 2f;
            float yaw   = FastTriangleSigned(t * 0.43f) * angularAmplitude.y;
            float roll  = FastTriangleSigned(t * 0.79f + 1.5707964f) * angularAmplitude.z
                        - currentDir.x * currentMagnitude * 3f;

            Vector3 localPosition = motion.RestLocalPosition + offset;
            Quaternion localRotation = motion.RestLocalRotation * ApproximateVisualRotation(pitch, yaw, roll);
            if (IsFinite(localPosition))
                tr.localPosition = localPosition;
            if (IsFinite(localRotation))
                tr.localRotation = localRotation;
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            float magnitude = max + (mid * 0.375f) + (min * 0.125f);
            return math.isfinite(magnitude) ? magnitude : 0f;
        }

        private static float FastTriangleSigned(float phase)
        {
            if (!math.isfinite(phase))
                return 0f;

            float triangle01 = 1f - math.abs(math.frac(phase * 0.15915494f + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static float ResolveGlobalQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static Quaternion ApproximateVisualRotation(float pitchDegrees, float yawDegrees, float rollDegrees)
        {
            if (!math.isfinite(pitchDegrees))
                pitchDegrees = 0f;
            if (!math.isfinite(yawDegrees))
                yawDegrees = 0f;
            if (!math.isfinite(rollDegrees))
                rollDegrees = 0f;

            float x = math.clamp(pitchDegrees, -MaxVisualRotationDegrees, MaxVisualRotationDegrees) * DegreesToHalfRadians;
            float y = math.clamp(yawDegrees, -MaxVisualRotationDegrees, MaxVisualRotationDegrees) * DegreesToHalfRadians;
            float z = math.clamp(rollDegrees, -MaxVisualRotationDegrees, MaxVisualRotationDegrees) * DegreesToHalfRadians;
            float invLength = math.rsqrt(1f + x * x + y * y + z * z);
            return new Quaternion(x * invLength, y * invLength, z * invLength, invLength);
        }

        private void UpdateBiomeCurrentBlend(float deltaTime)
        {
            if (!_hasBiomeCurrentTarget)
                return;

            _biomeCurrentStartVector = ClampFiniteVector(_biomeCurrentStartVector, MaxAmbientMotionCurrentMetersPerSecond);
            _biomeCurrentTargetVector = ClampFiniteVector(_biomeCurrentTargetVector, MaxAmbientMotionCurrentMetersPerSecond);
            _biomeCurrentBlendElapsed = SanitizeNonNegativeSeconds(_biomeCurrentBlendElapsed) + SanitizeDeltaTime(deltaTime);
            float t = math.saturate(_biomeCurrentBlendElapsed * BiomeFlowBlendInvSeconds);
            float smooth = t * t * (3f - 2f * t);
            float3 biomeCurrent = math.lerp(
                new float3(_biomeCurrentStartVector.x, _biomeCurrentStartVector.y, _biomeCurrentStartVector.z),
                new float3(_biomeCurrentTargetVector.x, _biomeCurrentTargetVector.y, _biomeCurrentTargetVector.z),
                smooth);
            _biomeCurrentVector = new Vector3(biomeCurrent.x, biomeCurrent.y, biomeCurrent.z);
            if (t >= 1f)
            {
                _biomeCurrentVector = _biomeCurrentTargetVector;
                _hasBiomeCurrentTarget = false;
            }

            _debugBiomeCurrentVector = _biomeCurrentVector;
        }

        private void SetBiomeCurrentTarget(HectonBiomeMatrixProfile profile)
        {
            Vector3 target = ResolveBiomeCurrentTarget(profile);

            _debugBiomeCurrentBiomeId = profile != null ? profile.matrixIndex : -1;
            if ((target - _biomeCurrentTargetVector).sqrMagnitude <= 0.000001f)
                return;

            _biomeCurrentStartVector = _biomeCurrentVector;
            _biomeCurrentTargetVector = target;
            _biomeCurrentBlendElapsed = 0f;
            _hasBiomeCurrentTarget = true;
            _debugBiomeCurrentVector = _biomeCurrentVector;
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            SetBiomeCurrentTarget(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        private void RefreshDistanceThresholds()
        {
            nearDistance = ResolveDistanceMeters(nearDistance, 1f);
            mediumDistance = math.max(ResolveDistanceMeters(mediumDistance, nearDistance), nearDistance);
            farDistance = math.max(ResolveDistanceMeters(farDistance, mediumDistance), mediumDistance);
            cullDistance = math.max(ResolveDistanceMeters(cullDistance, farDistance), farDistance);
            _nearDistanceSqr = ResolveDistanceSqr(nearDistance, 1f);
            _mediumDistanceSqr = ResolveDistanceSqr(mediumDistance, _nearDistanceSqr);
            _farDistanceSqr = ResolveDistanceSqr(farDistance, _mediumDistanceSqr);
            _cullDistanceSqr = ResolveDistanceSqr(cullDistance, _farDistanceSqr);
            _mediumFrameMask = NormalizeCadenceDivisor(mediumDivisor) - 1;
            _farFrameMask = NormalizeCadenceDivisor(farDivisor) - 1;
            _cullFrameMask = NormalizeCadenceDivisor(cullDivisor) - 1;
        }

        private static float SanitizeDeltaTime(float seconds)
        {
            if (!math.isfinite(seconds) || seconds <= 0f)
                return 0f;

            return math.min(seconds, MaxRuntimeDeltaTimeSeconds);
        }

        private static float SanitizeNonNegativeSeconds(float seconds)
        {
            return math.isfinite(seconds) && seconds > 0f ? seconds : 0f;
        }

        private static float AdvanceRuntimeTime(float currentSeconds, float deltaSeconds)
        {
            float next = SanitizeNonNegativeSeconds(currentSeconds) + SanitizeDeltaTime(deltaSeconds);
            return next > 100000f ? next - 100000f : next;
        }

        private static float ResolveMotionAmplitude(float amplitude)
        {
            return math.clamp(math.isfinite(amplitude) ? amplitude : 0f, 0f, MaxAmbientMotionAmplitudeMeters);
        }

        private static float ResolveMotionFrequency(float frequency)
        {
            return math.clamp(math.isfinite(frequency) ? frequency : 0f, 0f, MaxAmbientMotionFrequency);
        }

        private static float ResolveMotionCoupling(float coupling)
        {
            return math.clamp(math.isfinite(coupling) ? coupling : 0f, 0f, MaxAmbientMotionCoupling);
        }

        private static float ResolveLodBias(float lodBias)
        {
            return math.clamp(math.isfinite(lodBias) ? lodBias : 1f, 0.1f, 8f);
        }

        private static float ResolveDistanceMeters(float distanceMeters, float fallbackMeters)
        {
            float fallback = math.isfinite(fallbackMeters) && fallbackMeters > 0f ? fallbackMeters : 1f;
            return math.max(1f, math.isfinite(distanceMeters) ? distanceMeters : fallback);
        }

        private static float ResolveDistanceSqr(float distanceMeters, float fallback)
        {
            float safeDistance = ResolveDistanceMeters(distanceMeters, math.sqrt(math.max(1f, fallback)));
            float distanceSqr = safeDistance * safeDistance;
            return math.isfinite(distanceSqr) ? distanceSqr : math.max(1f, fallback);
        }

        private static float ResolveDistanceLimitSqr(float distanceSqr, float fallbackSqr)
        {
            float fallback = math.isfinite(fallbackSqr) && fallbackSqr > 0f ? fallbackSqr : 1f;
            return math.isfinite(distanceSqr) && distanceSqr > 0f ? distanceSqr : fallback;
        }

        private static Vector3 ResolveBiomeCurrentTarget(HectonBiomeMatrixProfile profile)
        {
            if (profile == null || !profile.hasAmbientFlowOverride)
                return Vector3.zero;

            float weight = math.saturate(math.isfinite(profile.ambientFlowOverrideWeight) ? profile.ambientFlowOverrideWeight : 0f);
            return ClampFiniteVector(profile.ambientFlowOverride * weight, MaxAmbientMotionCurrentMetersPerSecond);
        }

        private static Vector3 ClampFiniteVector(Vector3 value, float maxMagnitude)
        {
            if (!IsFinite(value))
                return Vector3.zero;

            float safeMax = math.max(0f, math.isfinite(maxMagnitude) ? maxMagnitude : 0f);
            if (safeMax <= 0f)
                return Vector3.zero;

            float sqrMagnitude = value.x * value.x + value.y * value.y + value.z * value.z;
            float maxSqr = safeMax * safeMax;
            if (!math.isfinite(sqrMagnitude))
                return Vector3.zero;
            if (sqrMagnitude <= maxSqr)
                return value;

            float scale = safeMax * math.rsqrt(sqrMagnitude);
            return value * scale;
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static bool IsFinite(Quaternion rotation)
        {
            return math.isfinite(rotation.x) &&
                   math.isfinite(rotation.y) &&
                   math.isfinite(rotation.z) &&
                   math.isfinite(rotation.w);
        }

        private static int NormalizeCadenceDivisor(int divisor)
        {
            if (divisor <= 1)
                return 1;
            if (divisor <= 2)
                return 2;
            if (divisor <= 4)
                return 4;
            if (divisor <= 8)
                return 8;
            if (divisor <= 16)
                return 16;

            return 32;
        }

        private void TryRegister()
        {
            if (_tickRegistered || !_runtimeWaterMotionCallbacksActive)
            {
                TryRegisterLateFrame();
                return;
            }

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            TryRegisterLateFrame();
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !_runtimeWaterMotionCallbacksActive)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !_runtimeWaterMotionCallbacksActive)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterAmbientWaterMotionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AmbientWaterMotion, this);
            if (_serviceRegistered)
                s_activeRuntime = this;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            AmbientWaterMotionManager active = s_activeRuntime;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsAmbientWaterMotionRuntimeUsable(active))
                {
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntime, active))
                    s_activeRuntime = null;
                if (ReferenceEquals(GlobalRegistry.AmbientWaterMotion, active))
                    GlobalRegistry.UnregisterAmbientWaterMotionRuntime(active);
            }

            AmbientWaterMotionManager registered = GlobalRegistry.AmbientWaterMotion;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsAmbientWaterMotionRuntimeUsable(registered))
            {
                s_activeRuntime = registered;
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterAmbientWaterMotionRuntime(registered);
            if (ReferenceEquals(s_activeRuntime, registered))
                s_activeRuntime = null;
            return false;
        }

        private static bool IsAmbientWaterMotionRuntimeUsable(AmbientWaterMotionManager manager)
        {
            return manager != null &&
                   manager._serviceRegistered &&
                   manager._runtimeWaterMotionCallbacksActive &&
                   manager.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterAmbientWaterMotionRuntime(this);
            _serviceRegistered = false;
            if (ReferenceEquals(s_activeRuntime, this))
                s_activeRuntime = null;
        }

        private void CacheRegistryServicesCold()
        {
            _playerRuntimeContext = Hecton8.Core.GlobalRegistry.Player;
            _ambientCurrentReadModel = Hecton8.Core.GlobalRegistry.AmbientCurrent;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !_runtimeWaterMotionCallbacksActive)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            globalAmplitude = ResolveMotionAmplitude(globalAmplitude);
            globalFrequency = ResolveMotionFrequency(globalFrequency);
            RefreshDistanceThresholds();
        }
#endif
    }
}
