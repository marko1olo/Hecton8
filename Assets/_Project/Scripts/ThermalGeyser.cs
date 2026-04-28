using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Caves
{
    /// <summary>
    /// Cave-owned eruptive geyser hazard. Alternates between idle and eruption windows, injects a local updraft,
    /// and applies cavitation stress to the scooter plus hanging collapse chunks.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CurrentVolume))]
    public sealed class ThermalGeyser : MonoBehaviour, ITickable, IUpdatable, IFixedTickable
    {
        [Header("── Runtime Wiring ──────────────────")]
        [SerializeField]
        [Tooltip("Local current volume driven by the geyser cycle.")]
        private CurrentVolume currentVolume;

        [SerializeField]
        [Tooltip("Optional eruption particle system toggled by the authored geyser cadence.")]
        private ParticleSystem eruptionParticles;

        [SerializeField]
        [Tooltip("Optional player override for isolated cave testing without bootstrap.")]
        private Transform playerTransformOverride;

        [Header("── Query Settings ──────────────────")]
        [SerializeField]
        [Tooltip("Layers sampled when applying eruption shock and cavitation to nearby rigidbodies.")]
        private LayerMask affectedLayers = ~0;

        private float _quietDuration = 10f;
        private float _eruptionDuration = 3f;
        private float _eruptionRadius = 4f;
        private float _cavitationRadius = 6f;
        private float _updraftStrength = 500f;
        private float _cavitationDragMultiplier = 2f;
        private float _cavitationSinkAcceleration = 12f;
        private float _chunkThermalDamagePerSecond = 2f;
        private float _phaseTimer;
        private bool _isErupting;
        private bool _registeredTick;
        private bool _registeredFixedTick;
        private Transform _playerTransform;
        private Rigidbody _playerRigidbody;
        private HectonPlayerMovement _playerMovement;
        private readonly Collider[] _affectedColliders = new Collider[24]; // COLD ALLOC: Collider[24] — bounded geyser influence query buffer — owner: ThermalGeyser

        /// <summary>
        /// Configures the geyser from cave dressing data.
        /// </summary>
        internal void Configure(ThermalGeyserConfig config, float globalIntensity)
        {
            if (config == null)
                return;

            _quietDuration = Mathf.Max(0.5f, config.quietDuration);
            _eruptionDuration = Mathf.Max(0.5f, config.eruptionDuration);
            _eruptionRadius = Mathf.Max(0.5f, config.eruptionRadius);
            _cavitationRadius = Mathf.Max(_eruptionRadius, config.cavitationRadius);
            _updraftStrength = Mathf.Max(0f, config.updraftStrength * Mathf.Max(0.1f, globalIntensity));
            _cavitationDragMultiplier = Mathf.Max(1f, config.cavitationDragMultiplier);
            _cavitationSinkAcceleration = Mathf.Max(0f, config.cavitationSinkAcceleration);
            _chunkThermalDamagePerSecond = Mathf.Max(0f, config.chunkThermalDamagePerSecond);

            ResolveRuntimeWiring();
            ConfigureCurrentVolume(isErupting: false);
            _phaseTimer = _quietDuration;
            _isErupting = false;
        }

        /// <summary>
        /// Advances the authored eruption cadence.
        /// </summary>
        public void Tick(float dt)
        {
            _phaseTimer -= Mathf.Max(0f, dt);
            if (_phaseTimer > 0f)
                return;

            _isErupting = !_isErupting;
            _phaseTimer = _isErupting ? _eruptionDuration : _quietDuration;
            ConfigureCurrentVolume(_isErupting);
            UpdateParticleState(_isErupting);
        }

        /// <summary>
        /// Applies physical eruption and cavitation effects during the fixed-step phase.
        /// </summary>
        public void FixedTick(float fdt)
        {
            if (!_isErupting || fdt <= 0f)
                return;

            ResolvePlayerContext();

            Vector3 origin = transform.position;
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                origin,
                _cavitationRadius,
                _affectedColliders,
                affectedLayers,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = _affectedColliders[i];
                if (hitCollider == null)
                    continue;

                Rigidbody body = hitCollider.attachedRigidbody;
                Vector3 point = body != null ? body.worldCenterOfMass : hitCollider.bounds.center;
                Vector3 horizontal = point - origin;
                horizontal.y = 0f;
                float horizontalDistance = horizontal.magnitude;
                if (horizontalDistance > _cavitationRadius)
                    continue;

                float eruptionT = 1f - Mathf.Clamp01(horizontalDistance / Mathf.Max(0.01f, _eruptionRadius));
                float cavitationT = 1f - Mathf.Clamp01(horizontalDistance / Mathf.Max(0.01f, _cavitationRadius));

                if (body != null)
                {
                    if (eruptionT > 0f)
                        PhysicsForceRouter.QueueForce(
                            body,
                            Vector3.up * (_updraftStrength * eruptionT),
                            ForceMode.Acceleration);

                    if (cavitationT > 0f)
                        PhysicsForceRouter.QueueForce(
                            body,
                            Vector3.down * (_cavitationSinkAcceleration * cavitationT),
                            ForceMode.Acceleration);
                }

                if (hitCollider.TryGetComponent(out SargassumCollapseChunk chunk))
                    chunk.ApplyThermalGeyserDamage(_chunkThermalDamagePerSecond * cavitationT * fdt);
            }

            if (_playerMovement != null && _playerTransform != null)
            {
                Vector3 playerOffset = _playerTransform.position - origin;
                Vector3 playerHorizontal = new Vector3(playerOffset.x, 0f, playerOffset.z);
                float playerDistance = playerHorizontal.magnitude;
                if (playerDistance <= _cavitationRadius)
                {
                    float eruptionT = 1f - Mathf.Clamp01(playerDistance / Mathf.Max(0.01f, _eruptionRadius));
                    float cavitationT = 1f - Mathf.Clamp01(playerDistance / Mathf.Max(0.01f, _cavitationRadius));

                    if (eruptionT > 0f)
                        _playerMovement.ApplyExternalThermalUpdraft(Vector3.up * (_updraftStrength * 0.01f * eruptionT * fdt));

                    if (cavitationT > 0f)
                    {
                        _playerMovement.ApplyEnvironmentalDrag(Mathf.Lerp(1f, _cavitationDragMultiplier, cavitationT));
                        if (_playerRigidbody != null)
                            PhysicsForceRouter.QueueForce(
                                _playerRigidbody,
                                Vector3.down * (_cavitationSinkAcceleration * cavitationT),
                                ForceMode.Acceleration);
                    }
                }
            }
        }

        private void Awake()
        {
            ResolveRuntimeWiring();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void ResolveRuntimeWiring()
        {
            if (currentVolume == null)
                TryGetComponent(out currentVolume);
        }

        private void ResolvePlayerContext()
        {
            Transform runtimePlayer = BootstrapState.CurrentPlayerTransform;
            _playerTransform = runtimePlayer != null ? runtimePlayer : playerTransformOverride;
            if (_playerTransform == null)
                return;

            if (_playerRigidbody == null || _playerRigidbody.transform != _playerTransform)
                _playerTransform.TryGetComponent(out _playerRigidbody);

            if (_playerMovement == null || _playerMovement.transform != _playerTransform)
                _playerTransform.TryGetComponent(out _playerMovement);
        }

        private void ConfigureCurrentVolume(bool isErupting)
        {
            if (currentVolume == null)
                return;

            currentVolume.ApplySemanticBoundsPreset(
                CurrentVolume.VolumeShape.Sphere,
                Vector3.one * (_cavitationRadius * 2f),
                _cavitationRadius);
            currentVolume.ApplySemanticFlowPreset(
                CurrentVolume.FlowPattern.Updraft,
                Vector3.up,
                isErupting ? _updraftStrength : 0f,
                1f,
                0f);
        }

        private void UpdateParticleState(bool erupting)
        {
            if (eruptionParticles == null)
                return;

            if (erupting)
            {
                if (!eruptionParticles.isPlaying)
                    eruptionParticles.Play(true);
            }
            else if (eruptionParticles.isPlaying)
            {
                eruptionParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void TryRegister()
        {
            if (!_registeredTick)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredTick = true;
            }

            if (_registeredFixedTick)
                return;

            GlobalRegistry.RegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredFixedTick = true;
        }

        private void TryUnregister()
        {
            if (_registeredTick)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            if (_registeredFixedTick)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);

            _registeredTick = false;
            _registeredFixedTick = false;
        }
    }
}
