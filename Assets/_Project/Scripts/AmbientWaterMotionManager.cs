// ============================================================================
// HECTON-8 - AmbientWaterMotionManager.cs
// Centralized visual bob/sway updater. One tick for many decorative props.
//
// v1.1 OPTIMIZATIONS:
//   [FIX] TryResolveObserver: throttles player resolve until observer exists.
//         GameBootstrapper/player resolve is skipped each frame while unresolved.
//   [FIX] Register: replaced Contains (O(n)) with HashSet-backed O(1) dedupe.
//   [FIX] ApplyMotion: caches worldPos from CachedTransform.position once,
//         then passes it to ShouldUpdate to avoid a second bridge position read.
//   [FIX] ShouldUpdate: accepts worldPos as a parameter; repeated .position read removed.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Bootstrap;
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
        private const int MotionCapacity = 128;
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
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IAmbientCurrentReadModel _ambientCurrentReadModel;
        private Vector3 _biomeCurrentVector;
        private Vector3 _biomeCurrentStartVector;
        private Vector3 _biomeCurrentTargetVector;
        private float _biomeCurrentBlendElapsed;
        private bool _hasBiomeCurrentTarget;
        private float _pendingVisualDeltaTime;

        // Observer resolve cooldown.
        // If no observer is assigned or found, avoid hitting bootstrap every frame.
        private float _observerResolveTimer;
        private const float ObserverResolveCooldown = 2f;
        private static AmbientWaterMotionManager s_activeRuntime;

        //  LIFECYCLE

        private void Awake()
        {
            AmbientWaterMotionManager registered = GlobalRegistry.AmbientWaterMotion;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            RefreshDistanceThresholds();
            CacheRegistryServicesCold();
            // Resolve once during startup; later retries are throttled.
            TryResolveObserver(force: true);
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            TryRegisterService();
            if (Application.isPlaying)
                BiomeMatrixEvents.Register(this);
        }

        private void OnDisable()
        {
            BiomeMatrixEvents.Unregister(this);
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
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
                    if (currentService == null)
                    {
                        _tickRegistered = false;
                        _lateFrameRegistered = false;
                        break;
                    }

                    if (isActiveAndEnabled)
                    {
                        TryUnregister();
                        TryRegister();
                    }
                    break;
            }
        }

        //  REGISTRATION - O(1) dedupe through HashSet

        public void Register(AmbientWaterMotion motion)
        {
            if (motion == null) return;

            // HashSet.Add returns false for existing entries: O(1) instead of O(n) Contains.
            if (_objectsSet.Add(motion))
                _objects.Add(motion);

            _debugActiveObjects = _objects.Count;
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

        //  TICK

        public void Tick(float deltaTime)
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return;

            _pendingVisualDeltaTime += math.max(0f, deltaTime);
            TryRegisterLateFrame();
        }

        public void LateFrameTick()
        {
            if (HectonFloatingOrigin.IsShiftInProgress)
                return;

            float deltaTime = _pendingVisualDeltaTime > 0f ? _pendingVisualDeltaTime : SystemDispatcher.CurrentFrameDeltaTime;
            _pendingVisualDeltaTime = 0f;
            UpdateBiomeCurrentBlend(deltaTime);

            if (_objects.Count == 0) return;

            _frameCounter++;
            _time += deltaTime;
            if (_time > 100000f) _time -= 100000f;

            // Observer lookup is cooled down; this does not run every frame.
            _observerResolveTimer -= deltaTime;
            if (_observerResolveTimer <= 0f)
            {
                TryResolveObserver(force: false);
            }

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

                bool hasMotionAup = motion.HasRestAup;
                AbsoluteUniversePosition motionAup = hasMotionAup ? motion.RestAup : default;
                Vector3 worldPos = hasMotionAup
                    ? ResolveRuntimePosition(in motionAup)
                    : ResolvePresentationRestWorldPosition(motion);

                if (!ShouldUpdateAup(motion, i, motionAup, hasMotionAup, observerAup, hasObserverAup,
                                  _nearDistanceSqr, _mediumDistanceSqr, _farDistanceSqr, _cullDistanceSqr, quality))
                    continue;

                ApplyMotion(motion, worldPos);
            }

            _debugActiveObjects = _objects.Count;
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

            float bias = math.max(0.1f, motion.LodBias);
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

        private static Vector3 ResolveRuntimePosition(in AbsoluteUniversePosition aup)
        {
            float3 runtime = aup.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private static Vector3 ResolvePresentationRestWorldPosition(AmbientWaterMotion motion)
        {
            Transform tr = motion.CachedTransform;
            Transform parent = tr != null ? tr.parent : null;
            return parent != null
                ? parent.TransformPoint(motion.RestLocalPosition)
                : motion.RestLocalPosition;
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

            var playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                AbsoluteUniversePosition currentAup = playerMovement.CurrentAup;
                if (currentAup.IsFinite())
                {
                    observerAup = currentAup;
                    return true;
                }
            }

            observerAup = default;
            return false;
        }

        private void ApplyMotion(AmbientWaterMotion motion, Vector3 worldPos)
        {
            Transform tr = motion.CachedTransform;

            float coupling = math.max(0f, motion.CurrentCoupling);
            Vector3 current = Vector3.zero;
            if (coupling > 0.0001f)
            {
                Vector3 volumeCurrent = Vector3.zero;
                IAmbientCurrentReadModel ambientCurrent = _ambientCurrentReadModel;
                if (ambientCurrent != null)
                    ambientCurrent.TrySampleAuthoredCurrent(worldPos, out volumeCurrent);

                float3 phantomCurrent = CurrentManager.SampleHorizontal(
                    new float3(worldPos.x, worldPos.y, worldPos.z),
                    _time,
                    0.018f,
                    0.12f,
                    1f);

                current = (volumeCurrent
                    + new Vector3(phantomCurrent.x, phantomCurrent.y, phantomCurrent.z)
                    + _biomeCurrentVector) * coupling;
            }

            float currentSqrMagnitude = current.x * current.x + current.y * current.y + current.z * current.z;
            float currentMagnitude = ApproximateVectorMagnitude(current);
            Vector3 currentDir = currentSqrMagnitude > 0.0001f
                ? current * math.rsqrt(currentSqrMagnitude)
                : Vector3.forward;

            float t = (_time + motion.Phase)
                    * math.max(0f, motion.BaseFrequency * globalFrequency);

            float bobY = FastTriangleSigned(t * 1.13f) * motion.VerticalAmplitude;
            float bobX = FastTriangleSigned(t * 0.91f) * motion.PositionalAmplitude.x;
            float bobZ = FastTriangleSigned(t * 1.07f + 1.5707964f) * motion.PositionalAmplitude.z;

            Vector3 offset = new Vector3(
                bobX + currentDir.x * currentMagnitude * 0.03f,
                bobY,
                bobZ + currentDir.z * currentMagnitude * 0.03f)
                * globalAmplitude;

            float pitch = FastTriangleSigned(t * 0.87f) * motion.AngularAmplitude.x
                        + currentDir.z * currentMagnitude * 2f;
            float yaw   = FastTriangleSigned(t * 0.43f) * motion.AngularAmplitude.y;
            float roll  = FastTriangleSigned(t * 0.79f + 1.5707964f) * motion.AngularAmplitude.z
                        - currentDir.x * currentMagnitude * 3f;

            tr.localPosition = motion.RestLocalPosition + offset;
            tr.localRotation = motion.RestLocalRotation * ApproximateVisualRotation(pitch, yaw, roll);
        }

        private static float ApproximateVectorMagnitude(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.125f);
        }

        private static float FastTriangleSigned(float phase)
        {
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

            _biomeCurrentBlendElapsed += math.max(0f, deltaTime);
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
            Vector3 target = Vector3.zero;
            if (profile != null && profile.hasAmbientFlowOverride)
                target = profile.ambientFlowOverride * math.saturate(profile.ambientFlowOverrideWeight);

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

        //  OBSERVER RESOLVE - cooled down, not every frame

        /// <param name="force">true ignores cooldown during startup.</param>
        private void TryResolveObserver(bool force = false)
        {
            // Existing observer: no lookup.
            if (lodObserver != null) return;

            // Cooldown still active and not forced: skip.
            if (!force && _observerResolveTimer > 0f) return;

            // Observer not found now; wait ObserverResolveCooldown seconds.
            _observerResolveTimer = ObserverResolveCooldown;

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                lodObserver = playerTransform;
        }

        private void RefreshDistanceThresholds()
        {
            _nearDistanceSqr = nearDistance * nearDistance;
            _mediumDistanceSqr = mediumDistance * mediumDistance;
            _farDistanceSqr = farDistance * farDistance;
            _cullDistanceSqr = cullDistance * cullDistance;
            _mediumFrameMask = NormalizeCadenceDivisor(mediumDivisor) - 1;
            _farFrameMask = NormalizeCadenceDivisor(farDivisor) - 1;
            _cullFrameMask = NormalizeCadenceDivisor(cullDivisor) - 1;
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
            if (_tickRegistered || !Application.isPlaying)
            {
                TryRegisterLateFrame();
                return;
            }

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            TryRegisterLateFrame();
        }

        private void TryRegisterLateFrame()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
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
            if (_serviceRegistered || !Application.isPlaying)
                return;

            AmbientWaterMotionManager registered = GlobalRegistry.AmbientWaterMotion;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterAmbientWaterMotionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.AmbientWaterMotion, this);
            if (_serviceRegistered)
                s_activeRuntime = this;
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
            if (_hotSwapRegistered || !Application.isPlaying)
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
            if (nearDistance   < 1f)              nearDistance   = 1f;
            if (mediumDistance < nearDistance)    mediumDistance = nearDistance;
            if (farDistance    < mediumDistance)  farDistance    = mediumDistance;
            if (cullDistance   < farDistance)     cullDistance   = farDistance;
            if (globalAmplitude < 0f)             globalAmplitude = 0f;
            if (globalFrequency < 0f)             globalFrequency = 0f;
            RefreshDistanceThresholds();
        }
#endif
    }
}
