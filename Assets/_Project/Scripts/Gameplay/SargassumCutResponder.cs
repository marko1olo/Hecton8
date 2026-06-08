// ============================================================================
// HECTON-8 — SargassumCutResponder.cs
// Drives per-renderer cut masking and leaf scrap bursts for sargassum clusters.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.World;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Applies short-lived cut impulses to sargassum renderers and optional debris particles.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Sargassum Cut Responder")]
    public sealed class SargassumCutResponder : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private static int s_x001SargassumCutResponderSignalPushDropCount;
        [Header("── Runtime Bindings ───────────────────")]
        [Tooltip("Legacy renderer list retained for prefab compatibility. Cut response is routed through SargassumCutManager global mask publishing.")]
        [SerializeField] private Renderer[] targetRenderers;

        [Header("── Cut Response ───────────────────────")]
        [Tooltip("Minimum world-space radius of the cut mask.")]
        [SerializeField, Range(0.1f, 4f)] private float minCutRadius = 0.45f;

        [Tooltip("Maximum world-space radius of the cut mask.")]
        [SerializeField, Range(0.2f, 6f)] private float maxCutRadius = 1.3f;

        [Tooltip("Minimum rigidbody speed that still counts as a meaningful cut impulse.")]
        [SerializeField, Range(0.1f, 15f)] private float cutSpeedThreshold = 2.8f;

        [Tooltip("How quickly the cut mask relaxes back to an intact state.")]
        [SerializeField, Range(0.5f, 16f)] private float cutRecoverySpeed = 5f;

        [Tooltip("Cooldown used to prevent particle spam when the same body sits inside the zone.")]
        [SerializeField, Range(0.01f, 0.5f)] private float particleCooldown = 0.08f;

        [Tooltip("Base debris amount emitted per cut burst.")]
        [SerializeField, Range(1, 64)] private int baseDebrisCount = 9;

        [Header("── Diagnostics ─────────────────────────")]
        [SerializeField] private float _debugCutStrength;
        [SerializeField] private float _debugCutRadius;
        [SerializeField] private Vector3 _debugCutPosition;
        [SerializeField] private float _debugParticleCooldown;

        private float _cutStrength;
        private float _cutRadius;
        private Vector3 _cutPositionWS;
        private SargassumCutManager _cachedCutManager;
        private AbsoluteUniversePosition _cachedRuntimeOriginAup;
        private bool _hasCachedRuntimeOriginAup;
        private bool _hotSwapRegistered;
        private uint _nextDebrisFrame;

        /// <summary>
        /// Binds the renderers and optional debris particle system used by this responder.
        /// </summary>
        /// <param name="renderers">Cluster renderers controlled by the responder.</param>
        public void Configure(Renderer[] renderers)
        {
            targetRenderers = renderers;
            ApplyCutState();
        }

        /// <summary>
        /// Registers a new cut impulse caused by a fast rigidbody passing through the cluster.
        /// </summary>
        /// <param name="positionWS">World-space cut center.</param>
        /// <param name="velocityWS">World-space cutter velocity.</param>
        /// <param name="speed">Absolute cutter speed.</param>
        public void RegisterCut(Vector3 positionWS, Vector3 velocityWS, float speed)
        {
            if (!IsFiniteVector3(positionWS) || !IsFiniteVector3(velocityWS) || !math.isfinite(speed))
                return;

            float normalizedSpeed = ResolveNormalizedCutSpeed(speed, cutSpeedThreshold);
            if (normalizedSpeed <= 0f)
                return;

            float safeMinCutRadius = ResolveMinCutRadius(minCutRadius);
            float safeMaxCutRadius = ResolveMaxCutRadius(maxCutRadius, safeMinCutRadius);
            _cutPositionWS = positionWS;
            _cutRadius = math.lerp(safeMinCutRadius, safeMaxCutRadius, normalizedSpeed);
            _cutStrength = math.lerp(0.5f, 1f, normalizedSpeed);
            PublishCutMask(positionWS, velocityWS);

            uint frame = ResolveFrameId();
            if (frame >= _nextDebrisFrame)
            {
                if (!RefreshCachedRuntimeOriginAup() ||
                    !TryResolveRuntimeAup(positionWS, out AbsoluteUniversePosition positionAup))
                    return;

                float rawQuality = HomeostasisBrain.GlobalQualityWeight;
                float quality = math.isfinite(rawQuality) ? math.saturate(rawQuality) : 1f;
                int safeBaseDebrisCount = math.max(0, baseDebrisCount);
                ushort quantity = (ushort)math.clamp(
                    (int)(math.lerp(safeBaseDebrisCount, safeBaseDebrisCount * 2.2f, normalizedSpeed) * math.lerp(0.35f, 1.4f, quality) + 0.5f),
                    0,
                    ushort.MaxValue);
                DebrisSpawnSignal debris = new DebrisSpawnSignal
                {
                    PositionAup = positionAup,
                    SpeciesHash = 0x53415247u,
                    SourceEntityId = 0u,
                    Intensity01 = normalizedSpeed,
                    DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                    Flags = DebrisSpawnSignal.FlagComputeShard,
                    Quantity = quantity
                };
                SignalBus<DebrisSpawnSignal>.TryPushTracked(in debris, ref s_x001SargassumCutResponderSignalPushDropCount);
                _nextDebrisFrame = frame + ResolveCooldownFrames(particleCooldown);
            }

            _debugCutStrength = _cutStrength;
            _debugCutRadius = _cutRadius;
            _debugCutPosition = _cutPositionWS;
            _debugParticleCooldown = EstimateCooldownSeconds(frame, _nextDebrisFrame);
        }

        private bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = _cachedRuntimeOriginAup;
            if (!_hasCachedRuntimeOriginAup)
                return false;

            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static uint ResolveFrameId()
        {
            uint currentFrame = TimeSliceScheduler.CurrentFrameId;
            return currentFrame > 0 ? (uint)currentFrame : 1u;
        }

        private static uint ResolveCooldownFrames(float cooldownSeconds)
        {
            float safeCooldown = math.isfinite(cooldownSeconds) ? math.max(0.01f, cooldownSeconds) : 0.01f;
            return (uint)math.max(1, (int)math.ceil(safeCooldown * 60f));
        }

        private static float EstimateCooldownSeconds(uint currentFrame, uint nextFrame)
        {
            if (nextFrame <= currentFrame)
                return 0f;

            return (nextFrame - currentFrame) * (1f / 60f);
        }

        private void Awake()
        {
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            ApplyCutState();
        }

        private void OnEnable()
        {
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            _cutStrength = 0f;
            _cutRadius = ResolveMinCutRadius(minCutRadius);
            _nextDebrisFrame = 0u;
            ApplyCutState();
            ClearColdDependencies();
            _cachedRuntimeOriginAup = default;
            _hasCachedRuntimeOriginAup = false;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            ClearColdDependencies();
            _cachedRuntimeOriginAup = default;
            _hasCachedRuntimeOriginAup = false;
        }

        private void ApplyCutState()
        {
            _debugCutStrength = math.isfinite(_cutStrength) ? math.saturate(_cutStrength) : 0f;
            _debugCutRadius = math.isfinite(_cutRadius) ? math.max(0f, _cutRadius) : 0f;
            _debugCutPosition = IsFiniteVector3(_cutPositionWS) ? _cutPositionWS : Vector3.zero;
            _debugParticleCooldown = EstimateCooldownSeconds(ResolveFrameId(), _nextDebrisFrame);
        }

        private void PublishCutMask(Vector3 positionWS, Vector3 velocityWS)
        {
            SargassumCutManager cutManager = _cachedCutManager;
            if (cutManager == null)
                return;

            if (!IsFiniteVector3(positionWS) ||
                !IsFiniteVector3(velocityWS) ||
                !math.isfinite(_cutRadius) ||
                !math.isfinite(_cutStrength))
                return;

            float safeMinCutRadius = ResolveMinCutRadius(minCutRadius);
            float cutRadiusWS = math.max(safeMinCutRadius, _cutRadius);
            float cutStrength01 = math.saturate(_cutStrength);
            float safeRecoverySpeed = math.isfinite(cutRecoverySpeed) ? math.max(0.5f, cutRecoverySpeed) : 0.5f;
            float recoverySeconds = math.rcp(safeRecoverySpeed);
            cutManager.RegisterExternalCut(positionWS, cutRadiusWS, cutStrength01, velocityWS, recoverySeconds);
        }

        private void CacheColdDependencies()
        {
            WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cachedCutManager);
        }

        private void ClearColdDependencies()
        {
            _cachedCutManager = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.SargassumCutRuntime)
                return;

            _cachedCutManager = currentService as SargassumCutManager;
            WorldRuntimeReferenceUtility.TryResolveSargassumCutManager(ref _cachedCutManager);
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

        private bool RefreshCachedRuntimeOriginAup()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
            {
                _cachedRuntimeOriginAup = default;
                _hasCachedRuntimeOriginAup = false;
                return false;
            }

            _cachedRuntimeOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            _hasCachedRuntimeOriginAup = _cachedRuntimeOriginAup.IsFinite();
            return _hasCachedRuntimeOriginAup;
        }

        private static float ResolveNormalizedCutSpeed(float speed, float threshold)
        {
            if (!math.isfinite(speed))
                return 0f;

            float safeThreshold = math.isfinite(threshold) ? math.max(0.001f, threshold) : 0.001f;
            float normalized = (speed - safeThreshold) / (safeThreshold * 1.4f);
            return math.isfinite(normalized) ? math.saturate(normalized) : 0f;
        }

        private static float ResolveMinCutRadius(float radius)
        {
            return math.isfinite(radius) ? math.max(0.05f, radius) : 0.45f;
        }

        private static float ResolveMaxCutRadius(float radius, float safeMinRadius)
        {
            float fallback = math.max(safeMinRadius, 1.3f);
            return math.isfinite(radius) ? math.max(safeMinRadius, radius) : fallback;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }
    }
}
