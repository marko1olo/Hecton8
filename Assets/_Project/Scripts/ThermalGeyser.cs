using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Items;
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
        private const float MinimumCylinderHeightMeters = 1f;
        private const float EruptionCylinderHeightMultiplier = 2.25f;
        private const float CavitationCylinderHeightMultiplier = 1.5f;
        private const float DefaultMineralEjectionIntervalSeconds = 600f;
        private const int MinimumEjectedMineralCount = 3;
        private const int MaximumEjectedMineralCount = 5;

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
        private LayerMask affectedLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("Mineral Ejection")]
        [SerializeField]
        [Tooltip("Low-tier mineral item emitted as physical loot during long-lived geyser activity.")]
        private ItemData ejectedMineralItem;

        [SerializeField, Range(60f, 1800f)]
        [Tooltip("Seconds between mineral ejection bursts while the geyser is erupting.")]
        private float mineralEjectionIntervalSeconds = DefaultMineralEjectionIntervalSeconds;

        [SerializeField, Range(0.1f, 30f)]
        [Tooltip("Impulse magnitude stored on ejected item records and applied when their Rigidbody hydrates.")]
        private float mineralEjectionImpulse = 8f;

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
        private float _mineralEjectionTimer = DefaultMineralEjectionIntervalSeconds;
        private uint _mineralEjectionSeed;
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
            _mineralEjectionTimer = Mathf.Max(60f, mineralEjectionIntervalSeconds);
        }

        /// <summary>
        /// Advances the authored eruption cadence.
        /// </summary>
        public void Tick(float dt)
        {
            float safeDt = Mathf.Max(0f, dt);
            TickMineralEjection(safeDt);

            _phaseTimer -= safeDt;
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
            bool playerBodyAffectedByOverlap = false;
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
                if (body != null && _playerRigidbody != null && ReferenceEquals(body, _playerRigidbody))
                    playerBodyAffectedByOverlap = true;

                Vector3 point = body != null ? body.worldCenterOfMass : hitCollider.bounds.center;
                Vector3 horizontal = point - origin;
                horizontal.y = 0f;
                float horizontalDistance = horizontal.magnitude;
                float verticalOffset = point.y - origin.y;
                float eruptionHeight = Mathf.Max(MinimumCylinderHeightMeters, _eruptionRadius * EruptionCylinderHeightMultiplier);
                float cavitationHeight = Mathf.Max(eruptionHeight, _cavitationRadius * CavitationCylinderHeightMultiplier);
                if (horizontalDistance > _cavitationRadius || verticalOffset < 0f || verticalOffset > cavitationHeight)
                    continue;

                float eruptionT = EvaluateCylinderAttenuation(horizontalDistance, _eruptionRadius, verticalOffset, eruptionHeight);
                float cavitationT = EvaluateCylinderAttenuation(horizontalDistance, _cavitationRadius, verticalOffset, cavitationHeight);

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
                float playerVerticalOffset = playerOffset.y;
                float playerEruptionHeight = Mathf.Max(MinimumCylinderHeightMeters, _eruptionRadius * EruptionCylinderHeightMultiplier);
                float playerCavitationHeight = Mathf.Max(playerEruptionHeight, _cavitationRadius * CavitationCylinderHeightMultiplier);
                if (playerDistance <= _cavitationRadius && playerVerticalOffset >= 0f && playerVerticalOffset <= playerCavitationHeight)
                {
                    float eruptionT = EvaluateCylinderAttenuation(playerDistance, _eruptionRadius, playerVerticalOffset, playerEruptionHeight);
                    float cavitationT = EvaluateCylinderAttenuation(playerDistance, _cavitationRadius, playerVerticalOffset, playerCavitationHeight);

                    if (!playerBodyAffectedByOverlap && eruptionT > 0f && _playerRigidbody != null)
                        PhysicsForceRouter.QueueForce(
                            _playerRigidbody,
                            Vector3.up * (_updraftStrength * eruptionT),
                            ForceMode.Acceleration);

                    if (cavitationT > 0f)
                    {
                        _playerMovement.ApplyEnvironmentalDrag(Mathf.Lerp(1f, _cavitationDragMultiplier, cavitationT));
                        if (!playerBodyAffectedByOverlap && _playerRigidbody != null)
                            PhysicsForceRouter.QueueForce(
                                _playerRigidbody,
                                Vector3.down * (_cavitationSinkAcceleration * cavitationT),
                                ForceMode.Acceleration);
                    }
                }
            }
        }

        private static float EvaluateCylinderAttenuation(float radialDistance, float radius, float verticalOffset, float height)
        {
            float safeRadius = Mathf.Max(0.01f, radius);
            float safeHeight = Mathf.Max(0.01f, height);
            float radialFactor = 1f - Mathf.Clamp01(radialDistance / safeRadius);
            float verticalFactor = 1f - Mathf.Clamp01(verticalOffset / safeHeight);
            return radialFactor * verticalFactor;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            return hash != 0u ? hash : 0xA341316Cu;
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private void Awake()
        {
            ResolveRuntimeWiring();
            _mineralEjectionSeed = unchecked((uint)EntityId.ToULong(GetEntityId())) ^ 0x9E3779B9u;
            _mineralEjectionTimer = Mathf.Max(60f, mineralEjectionIntervalSeconds);
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

        private void TickMineralEjection(float dt)
        {
            if (dt <= 0f || ejectedMineralItem == null)
                return;

            _mineralEjectionTimer -= dt;
            if (_mineralEjectionTimer > 0f || !_isErupting)
                return;

            _mineralEjectionTimer = Mathf.Max(60f, mineralEjectionIntervalSeconds);
            EjectMineralBurst();
        }

        private void EjectMineralBurst()
        {
            PersistentWorldRegistry registry = PersistentWorldRegistry.Instance;
            if (registry == null || ejectedMineralItem == null)
                return;

            uint state = _mineralEjectionSeed;
            _mineralEjectionSeed = Mix(_mineralEjectionSeed, 0xA511E9B3u);
            int count = MinimumEjectedMineralCount + (int)Mathf.Floor(Next01(ref state) * (MaximumEjectedMineralCount - MinimumEjectedMineralCount + 1));
            count = Mathf.Clamp(count, MinimumEjectedMineralCount, MaximumEjectedMineralCount);
            Vector3 origin = transform.position;
            for (int i = 0; i < count; i++)
            {
                Vector3 lateral = new Vector3((Next01(ref state) * 2f) - 1f, 0f, (Next01(ref state) * 2f) - 1f);
                if (lateral.sqrMagnitude <= 0.0001f)
                    lateral = Vector3.right;
                lateral.Normalize();

                Vector3 spawnPosition = origin + (Vector3.up * 0.25f) + (lateral * Mathf.Lerp(0.15f, 0.6f, Next01(ref state)));
                Vector3 impulse = (Vector3.up * Mathf.Lerp(0.85f, 1.25f, Next01(ref state)) * mineralEjectionImpulse) +
                                  (lateral * (mineralEjectionImpulse * 0.25f));
                registry.TryRegisterDroppedItem(ejectedMineralItem, 1, spawnPosition, impulse);
            }
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

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
