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
    public sealed class SargassumCutResponder : MonoBehaviour, ITickable, IUpdatable
    {
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

        private bool _registered;
        private float _cutStrength;
        private float _cutRadius;
        private float _particleCooldownRemaining;
        private Vector3 _cutPositionWS;

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
            float normalizedSpeed = ResolveNormalizedCutSpeed(speed, cutSpeedThreshold);
            if (normalizedSpeed <= 0f)
                return;

            _cutPositionWS = positionWS;
            _cutRadius = math.lerp(minCutRadius, maxCutRadius, normalizedSpeed);
            _cutStrength = math.max(_cutStrength, math.lerp(0.5f, 1f, normalizedSpeed));
            PublishCutMask(positionWS, velocityWS);

            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (_particleCooldownRemaining <= 0f)
            {
                double3 absolute = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(positionWS);
                float quality = math.saturate(HomeostasisBrain.GlobalQualityWeight);
                ushort quantity = (ushort)math.clamp(
                    (int)(math.lerp(baseDebrisCount, baseDebrisCount * 2.2f, normalizedSpeed) * math.lerp(0.35f, 1.4f, quality) + 0.5f),
                    0,
                    ushort.MaxValue);
                DebrisSpawnSignal debris = new DebrisSpawnSignal
                {
                    PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(absolute),
                    SpeciesHash = 0x53415247u,
                    SourceEntityId = 0u,
                    Intensity01 = normalizedSpeed,
                    DebrisKind = DebrisSpawnSignal.DebrisKindOrganicScrap,
                    Flags = DebrisSpawnSignal.FlagComputeShard,
                    Quantity = quantity
                };
                SignalBus<DebrisSpawnSignal>.TryPush(in debris);
                _particleCooldownRemaining = particleCooldown;
            }

            _debugCutStrength = _cutStrength;
            _debugCutRadius = _cutRadius;
            _debugCutPosition = _cutPositionWS;
        }

        /// <summary>
        /// Advances the cut-mask recovery state.
        /// </summary>
        /// <param name="deltaTime">Gameplay tick delta time.</param>
        public void Tick(float deltaTime)
        {
            if (_particleCooldownRemaining > 0f)
            {
                _particleCooldownRemaining -= deltaTime;
                if (_particleCooldownRemaining < 0f)
                    _particleCooldownRemaining = 0f;
            }

            if (_cutStrength <= 0.001f)
            {
                _cutStrength = 0f;
                _cutRadius = minCutRadius;
                ApplyCutState();
                UnregisterIfNeeded();
                return;
            }

            float blendT = FastRecoveryBlend(cutRecoverySpeed, deltaTime);
            _cutStrength = math.lerp(_cutStrength, 0f, blendT);
            ApplyCutState();

            _debugCutStrength = _cutStrength;
            _debugCutRadius = _cutRadius;
            _debugCutPosition = _cutPositionWS;
            _debugParticleCooldown = _particleCooldownRemaining;
        }

        private static float FastRecoveryBlend(float recoverySpeed, float deltaTime)
        {
            float x = math.max(0.01f, recoverySpeed) * math.max(0f, deltaTime);
            return math.saturate((x * (6f + x)) / (6f + (4f * x) + (x * x)));
        }

        private void Awake()
        {
            ApplyCutState();
        }

        private void OnDisable()
        {
            _cutStrength = 0f;
            _cutRadius = minCutRadius;
            _particleCooldownRemaining = 0f;
            ApplyCutState();
            UnregisterIfNeeded();
        }

        private void ApplyCutState()
        {
            _debugCutStrength = _cutStrength;
            _debugCutRadius = _cutRadius;
            _debugCutPosition = _cutPositionWS;
            _debugParticleCooldown = _particleCooldownRemaining;
        }

        private void PublishCutMask(Vector3 positionWS, Vector3 velocityWS)
        {
            SargassumCutManager cutManager = Hecton8.Core.GlobalRegistry.SargassumCut;
            if (cutManager == null)
                return;

            cutManager.RegisterExternalCut(positionWS, math.max(minCutRadius, _cutRadius), _cutStrength, velocityWS, 0.45f);
        }

        private void UnregisterIfNeeded()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private static float ResolveNormalizedCutSpeed(float speed, float threshold)
        {
            float safeThreshold = math.max(0.001f, threshold);
            float normalized = (speed - safeThreshold) / (safeThreshold * 1.4f);
            return math.isfinite(normalized) ? math.saturate(normalized) : 0f;
        }
    }
}
