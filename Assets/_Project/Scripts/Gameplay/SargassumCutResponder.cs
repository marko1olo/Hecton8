// ============================================================================
// HECTON-8 — SargassumCutResponder.cs
// Drives per-renderer cut masking and leaf scrap bursts for sargassum clusters.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Core;
    using UnityEngine;

    /// <summary>
    /// Applies short-lived cut impulses to sargassum renderers and optional debris particles.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Sargassum Cut Responder")]
    public sealed class SargassumCutResponder : MonoBehaviour, ITickable, IUpdatable
    {
        private static readonly int InteractionPositionId = Shader.PropertyToID("_InteractionPosition");
        private static readonly int InteractionRadiusId = Shader.PropertyToID("_InteractionRadius");
        private static readonly int InteractionCutStrengthId = Shader.PropertyToID("_InteractionCutStrength");

        [Header("── Runtime Bindings ───────────────────")]
        [Tooltip("Cluster renderers that receive the cut-mask MaterialPropertyBlock.")]
        [SerializeField] private Renderer[] targetRenderers;

        [Tooltip("Optional particle system used for leaf scrap bursts.")]
        [SerializeField] private ParticleSystem leafDebrisParticles;

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

        private MaterialPropertyBlock _mpb;
        private bool _registered;
        private float _cutStrength;
        private float _cutRadius;
        private float _particleCooldownRemaining;
        private Vector3 _cutPositionWS;

        /// <summary>
        /// Binds the renderers and optional debris particle system used by this responder.
        /// </summary>
        /// <param name="renderers">Cluster renderers controlled by the responder.</param>
        /// <param name="debrisParticles">Optional debris particle system.</param>
        public void Configure(Renderer[] renderers, ParticleSystem debrisParticles)
        {
            targetRenderers = renderers;
            leafDebrisParticles = debrisParticles;
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
            float normalizedSpeed = Mathf.InverseLerp(cutSpeedThreshold, cutSpeedThreshold * 2.4f, speed);
            if (normalizedSpeed <= 0f)
                return;

            _cutPositionWS = positionWS;
            _cutRadius = Mathf.Lerp(minCutRadius, maxCutRadius, normalizedSpeed);
            _cutStrength = Mathf.Max(_cutStrength, Mathf.Lerp(0.5f, 1f, normalizedSpeed));
            ApplyCutState();

            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;

            if (leafDebrisParticles != null && _particleCooldownRemaining <= 0f)
            {
                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = leafDebrisParticles.transform.InverseTransformPoint(positionWS),
                    velocity = velocityWS * 0.18f,
                    startSize = Mathf.Lerp(0.05f, 0.14f, normalizedSpeed)
                };

                int emitCount = Mathf.RoundToInt(Mathf.Lerp(baseDebrisCount, baseDebrisCount * 2.2f, normalizedSpeed));
                leafDebrisParticles.Emit(emitParams, emitCount);
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

            float blendT = 1f - Mathf.Exp(-Mathf.Max(0.01f, cutRecoverySpeed) * deltaTime);
            _cutStrength = Mathf.Lerp(_cutStrength, 0f, blendT);
            ApplyCutState();

            _debugCutStrength = _cutStrength;
            _debugCutRadius = _cutRadius;
            _debugCutPosition = _cutPositionWS;
            _debugParticleCooldown = _particleCooldownRemaining;
        }

        private void Awake()
        {
            EnsurePropertyBlock();
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
            if (targetRenderers == null)
                return;

            EnsurePropertyBlock();
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];
                if (renderer == null)
                    continue;

                renderer.GetPropertyBlock(_mpb);
                _mpb.SetVector(InteractionPositionId, _cutPositionWS);
                _mpb.SetFloat(InteractionRadiusId, Mathf.Max(minCutRadius, _cutRadius));
                _mpb.SetFloat(InteractionCutStrengthId, _cutStrength);
                renderer.SetPropertyBlock(_mpb);
                _mpb.Clear();
            }
        }

        private void EnsurePropertyBlock()
        {
            if (_mpb != null)
                return;

            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — shared cut-mask payload for bound renderers — owner: SargassumCutResponder
        }

        private void UnregisterIfNeeded()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }
    }
}
