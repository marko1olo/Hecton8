using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Physics;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Scavenging
{
    /// <summary>
    /// Pooled harvestable node with AUP-derived persistence and spatial-hash registration.
    /// Legacy UniqueId support remains for scene-authored compatibility, but authoritative depletion lives in PersistentWorldRegistry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour, IPoolable, ICuttable, IInteractionSignalConsumer
    {
        private static readonly int _MeltCenterId = Shader.PropertyToID("_MeltCenter");
        private static readonly int _MeltRadiusId = Shader.PropertyToID("_MeltRadius");
        private const int DebrisPoolWarmupCount = 96;
        private const int MinimumImpactDebrisCount = 3;
        private const int MaximumImpactDebrisCount = 5;
        private const float ImpactDebrisLifetimeSeconds = 4f;
        private const float ImpactDebrisSinkDurationSeconds = 1.15f;
        private const float ImpactDebrisSinkDepthMultiplier = 1.6f;
        private const float DefaultFirstYieldSampleSeconds = 0.12f;
        private const float MinimumYieldSampleSeconds = 0.016f;
        private const float MaximumYieldSampleSeconds = 0.35f;
        private const int DepletionLockFree = 0;
        private const int DepletionLockOwned = 1;
        private static readonly int _SteamExplosionLayerMask = HectonLayerMasks.MountedSweepLayerMask;
        private static readonly Collider[] _steamExplosionOverlapBuffer = new Collider[16];
        private static readonly Rigidbody[] _steamExplosionBodyBuffer = new Rigidbody[16];
        private static GameObject s_runtimeDebrisPrefab;
        private static Mesh s_runtimeDebrisMesh;
        private static Material s_runtimeDebrisMaterial;
        private static PhysicsMaterial s_runtimeSedimentDebrisPhysicsMaterial;
        private static PhysicsMaterial s_runtimeBasaltDebrisPhysicsMaterial;
        private static bool s_runtimeDebrisPoolReady;
        // COLD ALLOC: RegistryBucket<ResourceNode>[4096] — authored/persistent resource node registry for legacy world-state compatibility — owner: ResourceNode
        private static readonly RegistryBucket<ResourceNode> _worldStateRegistry = new RegistryBucket<ResourceNode>(4096);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _worldStateRegistry.Clear();
            s_runtimeDebrisPrefab = null;
            s_runtimeDebrisMesh = null;
            s_runtimeDebrisMaterial = null;
            s_runtimeSedimentDebrisPhysicsMaterial = null;
            s_runtimeBasaltDebrisPhysicsMaterial = null;
            s_runtimeDebrisPoolReady = false;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Legacy scene-facing ID retained for WorldStateManager compatibility. Runtime procedural nodes derive this from the AUP tombstone hash.")]
        private string uniqueId;

        [SerializeField]
        [Tooltip("When enabled, the node derives its legacy uniqueId from the AUP tombstone hash.")]
        private bool autoGenerateId = true;

        [SerializeField]
        [Tooltip("Legacy chunk scalar kept for older authoring paths that still expect deterministic scene IDs.")]
        private float chunkSize = 1000f;

        [SerializeField]
        [Tooltip("Optional data-driven resource template applied by the distribution director.")]
        private ResourceNodeTemplate resourceTemplate;

        [Header("Health")]
        [SerializeField, Min(1f)]
        [Tooltip("Maximum integrity before the node resolves and despawns.")]
        private float maxHealth = 100f;

        [Header("Loot")]
        [SerializeField]
        [Tooltip("Legacy pooled loot prefab emitted when the node resolves.")]
        private GameObject lootPrefab;

        [SerializeField, Min(0)]
        [Tooltip("How many pooled pickup pieces this node emits on depletion.")]
        private int lootCount = 3;

        [SerializeField, Min(0f)]
        [Tooltip("Lifetime applied to emitted pooled loot.")]
        private float lootLifetime = 30f;

        [Header("Scatter")]
        [SerializeField, Min(0f)]
        [Tooltip("Randomized scatter radius applied to emitted pooled loot.")]
        private float scatterRadius = 0.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Impulse magnitude applied to emitted pooled loot.")]
        private float scatterForce = 2.5f;

        [SerializeField, Min(0f)]
        [Tooltip("Additional upward lift applied to emitted pooled loot.")]
        private float upwardBias = 1.5f;

        [Header("Presentation")]
        [SerializeField, Min(0f)]
        [Tooltip("Maximum melt radius driven into the authored shader.")]
        private float maxMeltRadius = 0.5f;

        [SerializeField]
        [Tooltip("Optional explicit renderer receiving the melt property block.")]
        private Renderer targetRenderer;

        private Transform _cachedTransform;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider _boxCollider;
        private SphereCollider _sphereCollider;
        private MaterialPropertyBlock _propertyBlock;
        private Vector4 _localHitPoint;
        private float _currentHealth;
        private bool _isDepleted;
        private bool _despawnRequested;
        private bool _lootSpawnBlockedLogged;
        private bool _registeredToWorldStateRegistry;
        private int _spatialHandle;
        private ulong _persistentTombstoneId;
        private AbsoluteUniversePosition _persistentAup;
        private bool _hasPersistentAup;
        private HectonVoxelEngine _cachedVoxelEngine;
        private float _lastYieldSampleTimeSeconds;
        private float _pressureMetamorphismProgressSeconds;
        private long _fractionalYieldRemainderGrams;
        private int _yieldDropCount;
        private NativeArray<int> _depletionLock;

        /// <summary>Legacy scene-facing ID retained for compatibility systems.</summary>
        public string UniqueId => uniqueId;

        /// <summary>Current node integrity.</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Normalized health in the range [0,1].</summary>
        public float HealthNormalized => maxHealth > 0f ? _currentHealth / maxHealth : 0f;

        /// <summary>True once the node has resolved and should not be interacted with again.</summary>
        public bool IsDepleted => _isDepleted;

        /// <summary>Applied authoring template when spawned by the distribution director.</summary>
        public ResourceNodeTemplate ResourceTemplate => resourceTemplate;

        /// <summary>AUP-derived persistence tombstone key used by PersistentWorldRegistry.</summary>
        public ulong PersistentTombstoneId => _persistentTombstoneId;

        /// <summary>Accumulated deep-pressure metamorphism progress in in-game seconds.</summary>
        public float PressureMetamorphismProgressSeconds => Mathf.Max(0f, _pressureMetamorphismProgressSeconds);

        /// <summary>Half extents registered into the AUP spatial hash.</summary>
        public Vector3 SpatialHalfExtents
        {
            get
            {
                if (resourceTemplate != null)
                {
                    Vector3 physicalSize = resourceTemplate.PhysicalSize;
                    return new Vector3(
                        Mathf.Max(0.05f, physicalSize.x * 0.5f),
                        Mathf.Max(0.05f, physicalSize.y * 0.5f),
                        Mathf.Max(0.05f, physicalSize.z * 0.5f));
                }

                if (targetRenderer != null)
                    return targetRenderer.bounds.extents;

                return Vector3.one * 0.5f;
            }
        }

        public static int WorldStateRegistryCount => _worldStateRegistry.Count;

        public static ResourceNode GetWorldStateRegistryAt(int index)
        {
            return _worldStateRegistry.GetAt(index);
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _meshFilter);
            TryGetComponent(out _meshRenderer);
            TryGetComponent(out _boxCollider);
            TryGetComponent(out _sphereCollider);
            targetRenderer = targetRenderer != null
                ? targetRenderer
                : (_meshRenderer != null ? _meshRenderer : ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(_cachedTransform));

            _propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-node melt shader overrides — owner: ResourceNode
            _depletionLock = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] — interlocked depletion gate for pooled resource tombstones — owner: ResourceNode
            NativeMemorySentinel.RegisterNativeArray(
                _depletionLock,
                nameof(ResourceNode),
                nameof(_depletionLock),
                NativeAllocationLifetime.Scene);
            ResetState();
        }

        private void OnEnable()
        {
            RegisterWorldStateRegistry();

            if (IsPooledInstance())
                return;

            ActivateRuntimeState();
        }

        private void OnDisable()
        {
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
        }

        public void OnSpawn()
        {
            ResetState();
            RegisterWorldStateRegistry();
            ActivateRuntimeState();
        }

        public void OnDespawn()
        {
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            ResetState();
            _persistentTombstoneId = 0UL;
            _persistentAup = default;
            _hasPersistentAup = false;
            if (autoGenerateId)
                uniqueId = null;
        }

        private void OnDestroy()
        {
            if (_depletionLock.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_depletionLock);
                _depletionLock.Dispose();
            }
        }

        /// <summary>
        /// Assigns a legacy compatibility ID explicitly.
        /// </summary>
        public void SetUniqueId(string id)
        {
            uniqueId = id;
            autoGenerateId = false;
        }

        /// <summary>
        /// Applies a runtime template and optional fallback ghost presentation after a pooled spawn.
        /// </summary>
        public void ApplyRuntimeTemplate(ResourceNodeTemplate template, Mesh fallbackMesh, Material fallbackMaterial)
        {
            resourceTemplate = template;
            if (template == null)
                return;

            maxHealth = template.MaxIntegrity;
            lootPrefab = template.LootPickupPrefab;
            lootCount = template.DefaultLootCount;
            ApplyPresentation(template, fallbackMesh, fallbackMaterial);
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);

            if (_spatialHandle != 0)
                WorldSpatialHashGrid.SetResourceHalfExtents(_spatialHandle, SpatialHalfExtents);
        }

        /// <summary>
        /// Updates the resident metamorphism progress lane without mutating the authored template.
        /// </summary>
        /// <param name="progressSeconds">Accumulated compression progress in in-game seconds.</param>
        public void SetPressureMetamorphismProgressSeconds(float progressSeconds)
        {
            _pressureMetamorphismProgressSeconds = Mathf.Max(0f, progressSeconds);
        }

        /// <summary>
        /// Returns the static AUP identity captured when this node entered the runtime resource graph.
        /// </summary>
        internal bool TryGetPersistentAup(out AbsoluteUniversePosition position)
        {
            position = _persistentAup;
            return _hasPersistentAup;
        }

        /// <summary>
        /// Re-evaluates the node's AUP identity and spatial registration after a runtime template update.
        /// </summary>
        public void RefreshRuntimeSpatialRegistration()
        {
            if (!isActiveAndEnabled)
                return;

            ResolvePersistentIdentity();
            if (ShouldSuppressSpawn())
            {
                DespawnSelf();
                return;
            }

            if (_spatialHandle == 0)
            {
                RegisterSpatialHandle();
                return;
            }

            WorldSpatialHashGrid.SetResourceHalfExtents(_spatialHandle, SpatialHalfExtents);
            WorldSpatialHashGrid.Refresh(_spatialHandle);
        }

        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            TakeDamage(
                damage,
                damage,
                ResolveYieldSampleDeltaSeconds(),
                hitPoint,
                ResolveFallbackNormal(hitPoint),
                allowIncrementalYield: true,
                allowImpactDebris: true);
            if (!_isDepleted && targetRenderer != null)
                UpdateMeltProperties(hitPoint);
        }

        public void ApplyInteractionSignal(in Hecton8.Interaction.InteractionSignal signal, Vector3 runtimeHitPoint)
        {
            if (_isDepleted || _despawnRequested || signal.PowerDelivered <= 0f)
                return;

            if (ShouldTriggerSteamExplosion(in signal))
            {
                TriggerSteamExplosion(runtimeHitPoint);
                return;
            }

            Vector3 hitNormal = new Vector3(signal.HitNormal.x, signal.HitNormal.y, signal.HitNormal.z);
            TakeDamage(
                signal.PowerDelivered,
                Mathf.Max(signal.Source.Power, signal.PowerDelivered),
                ResolveYieldSampleDeltaSeconds(),
                runtimeHitPoint,
                hitNormal.sqrMagnitude > 0.0001f ? ResolveDominantAxis(hitNormal) : ResolveFallbackNormal(runtimeHitPoint),
                allowIncrementalYield: true,
                allowImpactDebris: true);

            if (!_isDepleted && targetRenderer != null)
                UpdateMeltProperties(runtimeHitPoint);
        }

        public void TakeDamage(float amount)
        {
            TakeDamage(
                amount,
                amount,
                ResolveYieldSampleDeltaSeconds(),
                _cachedTransform != null ? _cachedTransform.position : transform.position,
                Vector3.up,
                allowIncrementalYield: true,
                allowImpactDebris: true);
        }

        private void ActivateRuntimeState()
        {
            ResolvePersistentIdentity();
            if (ShouldSuppressSpawn())
            {
                _isDepleted = true;
                DespawnSelf();
                return;
            }

            RegisterSpatialHandle();
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float3 absoluteValue = math.abs(value);
            float maxAxis = math.max(absoluteValue.x, math.max(absoluteValue.y, absoluteValue.z));
            float minAxis = math.min(absoluteValue.x, math.min(absoluteValue.y, absoluteValue.z));
            float midAxis = absoluteValue.x + absoluteValue.y + absoluteValue.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.125f;
        }

        private void ResolvePersistentIdentity()
        {
            _persistentAup = AbsoluteUniversePosition.FromRuntimePosition(_cachedTransform.position);
            _hasPersistentAup = IsFiniteAup(in _persistentAup);
            _persistentTombstoneId = _hasPersistentAup
                ? PersistentWorldRegistry.ComputeResourceNodeTombstoneId(in _persistentAup)
                : 0UL;

            if (autoGenerateId)
                uniqueId = PersistentWorldRegistry.FormatResourceNodeTombstoneId(_persistentTombstoneId);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private bool ShouldSuppressSpawn()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(_persistentTombstoneId))
                return true;

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
                if (worldStateManager != null && worldStateManager.IsNodeDepleted(uniqueId))
                    return true;
            }

            return false;
        }

        private void RegisterPersistentDepletion()
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry != null)
                registry.TryRegisterDestroyedResourceNode(_persistentTombstoneId, _cachedTransform.position);

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
                if (worldStateManager != null)
                    worldStateManager.RegisterDepletedNode(uniqueId);
            }

            TryApplyDepletionCrater();
        }

        private bool TrySpawnLoot()
        {
            if (resourceTemplate != null && resourceTemplate.ExtractorYieldItem != null)
                return true;

            if (lootPrefab == null || lootCount <= 0)
                return true;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
            {
                if (!_lootSpawnBlockedLogged)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[ResourceNode] ObjectPoolManager unavailable. Depletion aborted to prevent loot loss.", this);
#endif
                    _lootSpawnBlockedLogged = true;
                }

                return false;
            }

            Vector3 origin = _cachedTransform.position;
            uint state = BuildDeterministicScatterSeed(0xD1B54A32u);
            for (int i = 0; i < lootCount; i++)
            {
                Vector3 offset = NextScatterVector(ref state) * scatterRadius;
                Vector3 spawnPosition = origin + offset;
                Quaternion spawnRotation = NextCardinalRotation(ref state);
                GameObject loot = pool.Spawn(lootPrefab, spawnPosition, spawnRotation);
                if (loot == null)
                    continue;

                if (loot.TryGetComponent(out Rigidbody rigidbody))
                {
                    Vector3 force = NextScatterVector(ref state) * scatterForce;
                    force.y = Mathf.Abs(force.y) + upwardBias;
                    PhysicsForceRouter.QueueForce(rigidbody, force, ForceMode.Impulse);
                    PhysicsForceRouter.QueueTorque(rigidbody, NextScatterVector(ref state) * (scatterForce * 0.5f), ForceMode.Impulse);
                }

                if (lootLifetime > 0f)
                    pool.Despawn(loot, lootLifetime);
            }

            return true;
        }

        private bool ShouldTriggerSteamExplosion(in Hecton8.Interaction.InteractionSignal signal)
        {
            if (resourceTemplate == null ||
                !resourceTemplate.TriggersSteamExplosionWithoutThermalShield ||
                !IsMiningSignal(in signal))
            {
                return false;
            }

            IModularEquipmentService modularEquipment = GlobalRegistry.ModularEquipment;
            return modularEquipment == null ||
                   !modularEquipment.HasUpgrade(signal.Source.ToolID, ToolUpgradeBits.ThermalShield);
        }

        private static bool IsMiningSignal(in Hecton8.Interaction.InteractionSignal signal)
        {
            switch ((InteractionEffectType)signal.EffectType)
            {
                case InteractionEffectType.Drill:
                case InteractionEffectType.PlasmaCut:
                case InteractionEffectType.Torch:
                case InteractionEffectType.Boil:
                    return true;

                default:
                    return false;
            }
        }

        private void TriggerSteamExplosion(Vector3 runtimeOrigin)
        {
            if (resourceTemplate == null)
                return;

            float radius = resourceTemplate.SteamExplosionRadiusMeters;
            float impulseMagnitude = resourceTemplate.SteamExplosionImpulse;
            if (radius <= 0f || impulseMagnitude <= 0f)
                return;

            int overlapCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                runtimeOrigin,
                radius,
                _steamExplosionOverlapBuffer,
                _SteamExplosionLayerMask,
                QueryTriggerInteraction.Ignore);
            if (overlapCount <= 0)
                return;

            int uniqueBodyCount = 0;
            for (int i = 0; i < overlapCount; i++)
            {
                Collider collider = _steamExplosionOverlapBuffer[i];
                _steamExplosionOverlapBuffer[i] = null;
                if (collider == null)
                    continue;

                Rigidbody body = collider.attachedRigidbody;
                if (body == null)
                    continue;

                bool alreadyQueued = false;
                for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
                {
                    if (ReferenceEquals(_steamExplosionBodyBuffer[bodyIndex], body))
                    {
                        alreadyQueued = true;
                        break;
                    }
                }

                if (alreadyQueued || uniqueBodyCount >= _steamExplosionBodyBuffer.Length)
                    continue;

                _steamExplosionBodyBuffer[uniqueBodyCount++] = body;
            }

            for (int i = 0; i < uniqueBodyCount; i++)
            {
                Rigidbody body = _steamExplosionBodyBuffer[i];
                _steamExplosionBodyBuffer[i] = null;
                if (body == null)
                    continue;

                Vector3 direction = body.worldCenterOfMass - runtimeOrigin;
                float distance = ApproximateMagnitude(new float3(direction.x, direction.y, direction.z));
                if (distance > 0.0001f)
                    direction /= distance;
                else
                    direction = Vector3.up;

                float falloff = math.saturate(1f - distance / radius);
                Vector3 impulse = direction * (impulseMagnitude * falloff);
                if (impulse.sqrMagnitude <= 0.0001f)
                    continue;

                PhysicsForceRouter.QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private void TryApplyDepletionCrater()
        {
            if (resourceTemplate == null || !resourceTemplate.LeavesDepletionCrater)
                return;

            float craterRadiusMeters = resourceTemplate.DepletionCraterRadiusMeters;
            if (craterRadiusMeters <= 0f)
                return;

            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref _cachedVoxelEngine);
            if (_cachedVoxelEngine == null)
                return;

            if (!_cachedVoxelEngine.TryGetNearestActiveVolume(_cachedTransform.position, out HectonVoxelVolume volume) ||
                volume == null)
            {
                return;
            }

            if (ResolveDebrisPhysicalProfile() == ResourceNodeTemplate.DebrisPhysicalProfile.Sediment)
                volume.ApplyPersistentResourceSedimentRotCrater(_cachedTransform.position, craterRadiusMeters);
            else
                volume.ApplyPersistentResourceCrater(_cachedTransform.position, craterRadiusMeters);
        }

        private void DespawnSelf()
        {
            if (_despawnRequested)
                return;

            _despawnRequested = true;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
            {
                pool.Despawn(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void ResetState()
        {
            _currentHealth = Mathf.Max(1f, maxHealth);
            _isDepleted = false;
            _despawnRequested = false;
            _lootSpawnBlockedLogged = false;
            _lastYieldSampleTimeSeconds = -1f;
            _pressureMetamorphismProgressSeconds = 0f;
            _fractionalYieldRemainderGrams = 0L;
            _yieldDropCount = 0;
            ResetDepletionLock();
            ResetMeltProperties();
        }

        private unsafe bool TryAcquireDepletionLock()
        {
            if (!_depletionLock.IsCreated || _depletionLock.Length <= 0)
                return !_isDepleted && !_despawnRequested;

            int* lockPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_depletionLock);
            return Interlocked.CompareExchange(ref lockPtr[0], DepletionLockOwned, DepletionLockFree) == DepletionLockFree;
        }

        private unsafe void ReleaseDepletionLock()
        {
            if (!_depletionLock.IsCreated || _depletionLock.Length <= 0)
                return;

            int* lockPtr = (int*)NativeArrayUnsafeUtility.GetUnsafePtr(_depletionLock);
            Interlocked.Exchange(ref lockPtr[0], DepletionLockFree);
        }

        private void ResetDepletionLock()
        {
            if (_depletionLock.IsCreated && _depletionLock.Length > 0)
                _depletionLock[0] = DepletionLockFree;
        }

        private void TakeDamage(
            float amount,
            float toolPower,
            float elapsedSeconds,
            Vector3 hitPoint,
            Vector3 hitNormal,
            bool allowIncrementalYield,
            bool allowImpactDebris)
        {
            if (_isDepleted || _despawnRequested || amount <= 0f)
                return;

            ResourceNodeTemplate.RuntimeDescriptor descriptor = resourceTemplate != null
                ? resourceTemplate.BuildRuntimeDescriptor()
                : default;
            float hardness = resourceTemplate != null
                ? Mathf.Max(0.01f, descriptor.ToolResistance)
                : 1f;
            float appliedDamage = Mathf.Max(0.01f, amount / hardness);
            float previousHealth = _currentHealth;
            float nextHealth = previousHealth - appliedDamage;
            bool depletionHit = nextHealth <= 0f;
            bool depletionLockAcquired = false;
            if (depletionHit)
            {
                depletionLockAcquired = TryAcquireDepletionLock();
                if (!depletionLockAcquired)
                    return;
            }

            if (allowIncrementalYield)
                TryEmitIncrementalYield(descriptor, Mathf.Max(toolPower, amount), elapsedSeconds, hitPoint, hitNormal);

            if (allowImpactDebris)
                SpawnImpactDebris(hitPoint, hitNormal, Mathf.Max(toolPower, amount));

            _currentHealth = nextHealth;
            if (_currentHealth > 0f)
            {
                if (depletionLockAcquired)
                    ReleaseDepletionLock();

                return;
            }

            if (!TrySpawnLoot())
            {
                _currentHealth = previousHealth;
                if (depletionLockAcquired)
                    ReleaseDepletionLock();

                return;
            }

            _currentHealth = 0f;
            _isDepleted = true;
            RegisterPersistentDepletion();
            DespawnSelf();
        }

        private void TryEmitIncrementalYield(
            ResourceNodeTemplate.RuntimeDescriptor descriptor,
            float toolPower,
            float elapsedSeconds,
            Vector3 hitPoint,
            Vector3 hitNormal)
        {
            if (resourceTemplate == null)
                return;

            ItemData primaryYieldItem = resourceTemplate.ExtractorYieldItem;
            if (primaryYieldItem == null)
                return;

            float extractedMassKg = ResourceYieldMath.EvaluateExtractedMassKg(
                Mathf.Max(0.01f, toolPower),
                Mathf.Max(0.01f, descriptor.ToolResistance),
                Mathf.Max(MinimumYieldSampleSeconds, elapsedSeconds));
            if (extractedMassKg <= 0f)
                return;

            long extractedGrams = ResourceYieldMath.KilogramsToWholeGrams(extractedMassKg);
            long unitItemMassGrams = Mathf.Max(1, ResourceYieldMath.KilogramsToWholeGrams(Mathf.Max(0.01f, resourceTemplate.UnitItemMassKg)));
            long availableGrams = _fractionalYieldRemainderGrams + extractedGrams;
            long wholeUnitsLong = unitItemMassGrams > 0L ? availableGrams / unitItemMassGrams : 0L;
            int wholeUnits = wholeUnitsLong > int.MaxValue ? int.MaxValue : (int)wholeUnitsLong;
            if (wholeUnits <= 0)
            {
                _fractionalYieldRemainderGrams = availableGrams;
                return;
            }

            int emittedCount = 0;
            uint seed = unchecked((uint)_persistentTombstoneId) ^ ((uint)_yieldDropCount * 0x9E3779B9u);
            for (int i = 0; i < wholeUnits; i++)
            {
                if (!TryDropYieldItem(primaryYieldItem, hitPoint, hitNormal, seed ^ (uint)i))
                    break;

                emittedCount++;
                _yieldDropCount++;
            }

            if (emittedCount > 0)
            {
                long remainingGrams = availableGrams - (emittedCount * unitItemMassGrams);
                _fractionalYieldRemainderGrams = remainingGrams > 0L ? remainingGrams : 0L;
            }
            else
            {
                _fractionalYieldRemainderGrams = availableGrams;
            }
        }

        private bool TryDropYieldItem(ItemData itemData, Vector3 hitPoint, Vector3 hitNormal, uint seed)
        {
            PersistentWorldRegistry registry = GlobalRegistry.PersistentWorldRegistry;
            if (registry == null || itemData == null)
                return false;

            Vector3 outwardNormal = hitNormal.sqrMagnitude > 0.0001f ? ResolveDominantAxis(hitNormal) : ResolveFallbackNormal(hitPoint);
            Vector3 tangent = ResolveTangent(outwardNormal, seed);
            Vector3 spawnPosition = hitPoint + (outwardNormal * 0.12f) + (tangent * 0.05f);
            Vector3 impulse = (outwardNormal * 0.8f) + (tangent * 0.25f);
            bool registered = registry.TryRegisterDroppedItem(itemData, 1, spawnPosition, impulse);
            if (registered)
            {
                ItemAcquiredSignal signal = new ItemAcquiredSignal
                {
                    PositionAup = AbsoluteUniversePosition.FromRuntimePosition(spawnPosition),
                    ItemHash = unchecked((uint)itemData.PersistentHashId),
                    OreHash = unchecked((uint)_persistentTombstoneId ^ (uint)(_persistentTombstoneId >> 32)),
                    Quantity = 1,
                    SourceKind = 1,
                    Flags = 0,
                    Frame = unchecked((uint)Time.frameCount)
                };
                GlobalSignals.Push(in signal);
            }

            return registered;
        }

        private void SpawnImpactDebris(Vector3 hitPoint, Vector3 hitNormal, float toolPower)
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null || !EnsureRuntimeDebrisPool(pool))
                return;

            Material debrisMaterial = ResolveDebrisMaterial();
            Mesh debrisMesh = _meshFilter != null && _meshFilter.sharedMesh != null ? _meshFilter.sharedMesh : s_runtimeDebrisMesh;
            if (debrisMesh == null)
                return;

            ResourceNodeTemplate.DebrisPhysicalProfile debrisProfile = ResolveDebrisPhysicalProfile();
            PhysicsMaterial debrisPhysicsMaterial = ResolveDebrisPhysicsMaterial(debrisProfile);
            uint state = unchecked((uint)_persistentTombstoneId) ^ ((uint)(_yieldDropCount + 1) * 0x85EBCA6Bu);
            int debrisCount = MinimumImpactDebrisCount + (int)Mathf.Floor(Next01(ref state) * (MaximumImpactDebrisCount - MinimumImpactDebrisCount + 1));
            debrisCount = Mathf.Clamp(debrisCount, MinimumImpactDebrisCount, MaximumImpactDebrisCount);
            Vector3 outwardNormal = hitNormal.sqrMagnitude > 0.0001f ? ResolveDominantAxis(hitNormal) : ResolveFallbackNormal(hitPoint);
            float impulseScale = Mathf.Clamp(toolPower, 0.4f, 3.5f);

            for (int i = 0; i < debrisCount; i++)
            {
                Quaternion rotation = NextCardinalRotation(ref state);
                Vector3 tangent = ResolveTangent(outwardNormal, state ^ (uint)i);
                Vector3 spawnPosition = hitPoint + (outwardNormal * 0.08f) + (tangent * (0.04f + (0.03f * Next01(ref state))));
                GameObject debris = pool.Spawn(s_runtimeDebrisPrefab, spawnPosition, rotation);
                if (debris == null)
                    return;

                if (debris.TryGetComponent(out MeshFilter debrisFilter))
                    debrisFilter.sharedMesh = debrisMesh;

                if (debris.TryGetComponent(out MeshRenderer debrisRenderer) && debrisMaterial != null)
                    debrisRenderer.sharedMaterial = debrisMaterial;

                debris.transform.localScale = Vector3.one * (0.08f + 0.1f * Next01(ref state));
                if (debris.TryGetComponent(out RuntimeDebrisShard shard))
                {
                    float sinkDepth = Mathf.Max(0.08f, debris.transform.localScale.y * ImpactDebrisSinkDepthMultiplier);
                    shard.ConfigureRuntime(
                        pool,
                        debrisProfile,
                        debrisPhysicsMaterial,
                        ImpactDebrisLifetimeSeconds,
                        ImpactDebrisSinkDurationSeconds,
                        sinkDepth);
                }

                if (debris.TryGetComponent(out Rigidbody body))
                {
                    body.isKinematic = false;
                    body.useGravity = true;
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.WakeUp();

                    Vector3 velocityChange = ((outwardNormal * (0.65f + 0.55f * Next01(ref state))) +
                                              (tangent * (0.12f + 0.28f * Next01(ref state)))) * impulseScale;
                    PhysicsForceRouter.QueueForce(body, velocityChange, ForceMode.VelocityChange);
                    PhysicsForceRouter.QueueTorque(body, tangent * (0.12f + 0.16f * Next01(ref state)), ForceMode.VelocityChange);
                }
            }
        }

        private static bool EnsureRuntimeDebrisPool(ObjectPoolManager pool)
        {
            if (pool == null)
                return false;

            if (s_runtimeDebrisPrefab == null)
                s_runtimeDebrisPrefab = BuildRuntimeDebrisPrefab();

            if (s_runtimeDebrisPrefab == null)
                return false;

            if (!pool.HasPool(s_runtimeDebrisPrefab))
                pool.Warmup(s_runtimeDebrisPrefab, DebrisPoolWarmupCount);

            s_runtimeDebrisPoolReady = pool.HasPool(s_runtimeDebrisPrefab);
            return s_runtimeDebrisPoolReady;
        }

        private static GameObject BuildRuntimeDebrisPrefab()
        {
            if (s_runtimeDebrisMesh == null || s_runtimeDebrisMaterial == null)
            {
                // COLD ALLOC: GameObject[1] - temporary primitive source used to capture the built-in cube mesh/material for runtime mineral debris - owner: ResourceNode
                GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (primitive.TryGetComponent(out MeshFilter primitiveFilter))
                    s_runtimeDebrisMesh = primitiveFilter.sharedMesh;

                if (primitive.TryGetComponent(out MeshRenderer primitiveRenderer))
                    s_runtimeDebrisMaterial = primitiveRenderer.sharedMaterial;

                if (Application.isPlaying)
                    Destroy(primitive);
                else
                    DestroyImmediate(primitive);
            }

            EnsureRuntimeDebrisPhysicsMaterials();

            if (s_runtimeDebrisMesh == null)
                return null;

            // COLD ALLOC: GameObject[1] - pooled mineral debris runtime prefab - owner: ResourceNode
            GameObject prefab = new GameObject("[RuntimeMineralDebrisShard]");
            prefab.SetActive(false);
            prefab.hideFlags = HideFlags.HideAndDontSave;

            MeshFilter filter = prefab.AddComponent<MeshFilter>();
            filter.sharedMesh = s_runtimeDebrisMesh;

            MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
            if (s_runtimeDebrisMaterial != null)
                renderer.sharedMaterial = s_runtimeDebrisMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            BoxCollider collider = prefab.AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            Rigidbody body = prefab.AddComponent<Rigidbody>();
            body.mass = 0.08f;
            body.useGravity = true;
            body.linearDamping = 0.45f;
            body.angularDamping = 0.35f;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.isKinematic = false;

            prefab.AddComponent<RuntimeDebrisShard>();
            return prefab;
        }

        private Material ResolveDebrisMaterial()
        {
            if (_meshRenderer != null && _meshRenderer.sharedMaterial != null)
                return _meshRenderer.sharedMaterial;

            if (targetRenderer != null && targetRenderer.sharedMaterial != null)
                return targetRenderer.sharedMaterial;

            return s_runtimeDebrisMaterial;
        }

        private ResourceNodeTemplate.DebrisPhysicalProfile ResolveDebrisPhysicalProfile()
        {
            return resourceTemplate != null
                ? resourceTemplate.ResolveDebrisPhysicalProfile()
                : ResourceNodeTemplate.DebrisPhysicalProfile.Basalt;
        }

        private PhysicsMaterial ResolveDebrisPhysicsMaterial(ResourceNodeTemplate.DebrisPhysicalProfile profile)
        {
            if (resourceTemplate != null)
            {
                PhysicsMaterial authoredMaterial = resourceTemplate.ResolveDebrisPhysicsMaterial(profile);
                if (authoredMaterial != null)
                    return authoredMaterial;
            }

            EnsureRuntimeDebrisPhysicsMaterials();
            return profile == ResourceNodeTemplate.DebrisPhysicalProfile.Sediment
                ? s_runtimeSedimentDebrisPhysicsMaterial
                : s_runtimeBasaltDebrisPhysicsMaterial;
        }

        private static void EnsureRuntimeDebrisPhysicsMaterials()
        {
            if (s_runtimeSedimentDebrisPhysicsMaterial == null)
                s_runtimeSedimentDebrisPhysicsMaterial = BuildRuntimeDebrisPhysicsMaterial(
                    "RuntimeSedimentShardPhysics",
                    dynamicFriction: 0.92f,
                    staticFriction: 0.96f,
                    bounciness: 0.01f);

            if (s_runtimeBasaltDebrisPhysicsMaterial == null)
                s_runtimeBasaltDebrisPhysicsMaterial = BuildRuntimeDebrisPhysicsMaterial(
                    "RuntimeBasaltShardPhysics",
                    dynamicFriction: 0.58f,
                    staticFriction: 0.64f,
                    bounciness: 0.03f);
        }

        private static PhysicsMaterial BuildRuntimeDebrisPhysicsMaterial(
            string materialName,
            float dynamicFriction,
            float staticFriction,
            float bounciness)
        {
            // COLD ALLOC: PhysicsMaterial[1] - shared pooled shard collision response profile - owner: ResourceNode
            PhysicsMaterial material = new PhysicsMaterial(materialName)
            {
                dynamicFriction = dynamicFriction,
                staticFriction = staticFriction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            material.hideFlags = HideFlags.HideAndDontSave;
            return material;
        }

        private float ResolveYieldSampleDeltaSeconds()
        {
            float currentTimeSeconds = Time.time;
            if (_lastYieldSampleTimeSeconds < 0f)
            {
                _lastYieldSampleTimeSeconds = currentTimeSeconds;
                return DefaultFirstYieldSampleSeconds;
            }

            float deltaSeconds = Mathf.Clamp(currentTimeSeconds - _lastYieldSampleTimeSeconds, MinimumYieldSampleSeconds, MaximumYieldSampleSeconds);
            _lastYieldSampleTimeSeconds = currentTimeSeconds;
            return deltaSeconds;
        }

        private static Vector3 ResolveTangent(Vector3 normal, uint seed)
        {
            Vector3 tangent = ResolveDominantTangent(normal);
            Vector3 bitangent = Vector3.Cross(normal, tangent);
            switch ((seed >> 13) & 7u)
            {
                case 0u:
                    return tangent;
                case 1u:
                    return -tangent;
                case 2u:
                    return bitangent;
                case 3u:
                    return -bitangent;
                case 4u:
                    return (tangent + bitangent) * 0.70710678f;
                case 5u:
                    return (tangent - bitangent) * 0.70710678f;
                case 6u:
                    return (-tangent + bitangent) * 0.70710678f;
                default:
                    return (-tangent - bitangent) * 0.70710678f;
            }
        }

        private static float Next01(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private uint BuildDeterministicScatterSeed(uint salt)
        {
            uint hash = unchecked((uint)_persistentTombstoneId) ^ salt;
            hash ^= (uint)((ulong)_persistentTombstoneId >> 32);
            hash ^= (uint)(_yieldDropCount + 1) * 0x9E3779B9u;
            ulong ownerEntityId = _cachedTransform != null
                ? EntityId.ToULong(_cachedTransform.GetEntityId())
                : EntityId.ToULong(GetEntityId());
            hash ^= unchecked((uint)ownerEntityId);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash != 0u ? hash : 0xA341316Cu;
        }

        private static Vector3 NextScatterVector(ref uint state)
        {
            return new Vector3(
                (Next01(ref state) * 2f) - 1f,
                (Next01(ref state) * 2f) - 1f,
                (Next01(ref state) * 2f) - 1f);
        }

        private static Quaternion NextCardinalRotation(ref uint state)
        {
            uint lane = (uint)(Next01(ref state) * 8f) & 7u;
            switch (lane)
            {
                case 0u:
                    return Quaternion.identity;
                case 1u:
                    return new Quaternion(0f, 0.70710678f, 0f, 0.70710678f);
                case 2u:
                    return new Quaternion(0f, 1f, 0f, 0f);
                case 3u:
                    return new Quaternion(0f, -0.70710678f, 0f, 0.70710678f);
                case 4u:
                    return new Quaternion(0.38268343f, 0f, 0f, 0.9238795f);
                case 5u:
                    return new Quaternion(-0.38268343f, 0f, 0f, 0.9238795f);
                case 6u:
                    return new Quaternion(0f, 0f, 0.38268343f, 0.9238795f);
                default:
                    return new Quaternion(0f, 0f, -0.38268343f, 0.9238795f);
            }
        }

        private static Vector3 ResolveDominantTangent(Vector3 normal)
        {
            float ax = Mathf.Abs(normal.x);
            float ay = Mathf.Abs(normal.y);
            float az = Mathf.Abs(normal.z);
            if (ay >= ax && ay >= az)
                return normal.y >= 0f ? Vector3.right : Vector3.left;
            if (ax >= az)
                return normal.x >= 0f ? Vector3.forward : Vector3.back;

            return normal.z >= 0f ? Vector3.right : Vector3.left;
        }

        private void RegisterSpatialHandle()
        {
            if (_spatialHandle != 0 || _isDepleted || !isActiveAndEnabled)
                return;

            _spatialHandle = WorldSpatialHashGrid.RegisterResource(this, SpatialHalfExtents);
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void RegisterWorldStateRegistry()
        {
            if (_registeredToWorldStateRegistry)
                return;

            _worldStateRegistry.Register(this);
            _registeredToWorldStateRegistry = true;
        }

        private void UnregisterWorldStateRegistry()
        {
            if (!_registeredToWorldStateRegistry)
                return;

            _worldStateRegistry.Unregister(this);
            _registeredToWorldStateRegistry = false;
        }

        private Vector3 ResolveFallbackNormal(Vector3 worldHitPoint)
        {
            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            Vector3 normal = worldHitPoint - origin;
            if (normal.sqrMagnitude <= 0.000001f)
                return _cachedTransform != null ? _cachedTransform.up : Vector3.up;

            return ResolveDominantAxis(normal);
        }

        private static Vector3 ResolveDominantAxis(Vector3 value)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            float az = Mathf.Abs(value.z);
            if (ay >= ax && ay >= az)
                return value.y >= 0f ? Vector3.up : Vector3.down;
            if (ax >= az)
                return value.x >= 0f ? Vector3.right : Vector3.left;

            return value.z >= 0f ? Vector3.forward : Vector3.back;
        }

        private void UpdateMeltProperties(Vector3 worldHitPoint)
        {
            Vector3 localPosition = _cachedTransform.InverseTransformPoint(worldHitPoint);
            _localHitPoint.x = localPosition.x;
            _localHitPoint.y = localPosition.y;
            _localHitPoint.z = localPosition.z;
            _localHitPoint.w = 0f;

            float meltRadius = (1f - HealthNormalized) * maxMeltRadius;
            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(_MeltCenterId, _localHitPoint);
            _propertyBlock.SetFloat(_MeltRadiusId, meltRadius);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ResetMeltProperties()
        {
            if (targetRenderer == null || _propertyBlock == null)
                return;

            targetRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetVector(_MeltCenterId, Vector4.zero);
            _propertyBlock.SetFloat(_MeltRadiusId, 0f);
            targetRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ApplyPresentation(ResourceNodeTemplate template, Mesh fallbackMesh, Material fallbackMaterial)
        {
            if (template == null)
                return;

            Vector3 physicalSize = template.PhysicalSize;
            _cachedTransform.localScale = physicalSize;

            if (_meshFilter != null)
                _meshFilter.sharedMesh = template.NodeMesh != null ? template.NodeMesh : fallbackMesh;

            if (_meshRenderer != null)
            {
                Material sharedMaterial = template.NodeMaterial != null ? template.NodeMaterial : fallbackMaterial;
                if (sharedMaterial != null)
                    _meshRenderer.sharedMaterial = sharedMaterial;

                targetRenderer = _meshRenderer;
            }

            ConfigurePrimitiveColliders(template.RuntimeColliderShape, physicalSize);
            ResetMeltProperties();
        }

        private void ConfigurePrimitiveColliders(ResourceNodeTemplate.ColliderShape shape, Vector3 physicalSize)
        {
            Vector3 safeSize = new Vector3(
                Mathf.Max(0.1f, physicalSize.x),
                Mathf.Max(0.1f, physicalSize.y),
                Mathf.Max(0.1f, physicalSize.z));

            if (_boxCollider != null)
            {
                _boxCollider.enabled = shape == ResourceNodeTemplate.ColliderShape.Box;
                _boxCollider.center = Vector3.zero;
                _boxCollider.size = Vector3.one;
            }

            if (_sphereCollider != null)
            {
                _sphereCollider.enabled = shape == ResourceNodeTemplate.ColliderShape.Sphere;
                _sphereCollider.center = Vector3.zero;
                _sphereCollider.radius = 0.5f;
            }
        }

        private bool IsPooledInstance()
        {
            return TryGetComponent(out ObjectPoolManager.PoolItemMarker _);
        }

        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        private sealed class RuntimeDebrisShard : MonoBehaviour, IPoolable, IUpdatable
        {
            private Transform _cachedTransform;
            private Rigidbody _rigidbody;
            private Collider _collider;
            private ObjectPoolManager _owningPool;
            private bool _registeredToDispatcher;
            private bool _active;
            private bool _sinking;
            private float _lifetimeSeconds;
            private float _sinkDurationSeconds;
            private float _sinkStartTimeSeconds;
            private float _sinkDepthMeters;
            private float _ageSeconds;
            private Vector3 _sinkStartPosition;
            private Vector3 _sinkEndPosition;

            private void Awake()
            {
                _cachedTransform = transform;
                TryGetComponent(out _rigidbody);
                TryGetComponent(out _collider);
            }

            public void OnSpawn()
            {
                ResetRuntimeState();
                TryRegisterToDispatcher();
            }

            public void OnDespawn()
            {
                TryUnregisterFromDispatcher();
                ResetPhysicsState();
                ResetRuntimeState();
                _owningPool = null;
            }

            public void Tick(float deltaTime)
            {
                if (!_active)
                    return;

                _ageSeconds += math.max(0f, deltaTime);
                if (!_sinking && _ageSeconds >= _sinkStartTimeSeconds)
                    BeginSinkPhase();

                if (_sinking)
                {
                    float sinkElapsedSeconds = _ageSeconds - _sinkStartTimeSeconds;
                    float sinkT = _sinkDurationSeconds > 0.0001f
                        ? math.saturate(sinkElapsedSeconds / _sinkDurationSeconds)
                        : 1f;
                    _cachedTransform.position = _sinkStartPosition + (_sinkEndPosition - _sinkStartPosition) * sinkT;
                }

                if (_ageSeconds >= _lifetimeSeconds)
                    RequestDespawn();
            }

            public void ConfigureRuntime(
                ObjectPoolManager owningPool,
                ResourceNodeTemplate.DebrisPhysicalProfile profile,
                PhysicsMaterial physicsMaterial,
                float lifetimeSeconds,
                float sinkDurationSeconds,
                float sinkDepthMeters)
            {
                if (_cachedTransform == null)
                    _cachedTransform = transform;

                _owningPool = owningPool;
                _active = true;
                _sinking = false;
                _ageSeconds = 0f;
                _lifetimeSeconds = Mathf.Max(0.1f, lifetimeSeconds);
                _sinkDurationSeconds = Mathf.Clamp(sinkDurationSeconds, 0.05f, _lifetimeSeconds);
                _sinkStartTimeSeconds = Mathf.Max(0f, _lifetimeSeconds - _sinkDurationSeconds);
                _sinkDepthMeters = Mathf.Max(0.05f, sinkDepthMeters);

                if (_collider != null)
                    _collider.sharedMaterial = physicsMaterial;

                if (_rigidbody != null)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                    _rigidbody.isKinematic = false;
                    _rigidbody.useGravity = true;

                    if (profile == ResourceNodeTemplate.DebrisPhysicalProfile.Sediment)
                    {
                        _rigidbody.mass = 0.07f;
                        _rigidbody.linearDamping = 0.6f;
                        _rigidbody.angularDamping = 0.3f;
                    }
                    else
                    {
                        _rigidbody.mass = 0.14f;
                        _rigidbody.linearDamping = 0.22f;
                        _rigidbody.angularDamping = 0.1f;
                    }

                    _rigidbody.WakeUp();
                }

                TryRegisterToDispatcher();
            }

            private void BeginSinkPhase()
            {
                _sinking = true;
                _sinkStartPosition = _cachedTransform.position;
                _sinkEndPosition = _sinkStartPosition + (Vector3.down * _sinkDepthMeters);

                if (_rigidbody != null)
                {
                    _rigidbody.linearVelocity = Vector3.zero;
                    _rigidbody.angularVelocity = Vector3.zero;
                    _rigidbody.isKinematic = true;
                    _rigidbody.useGravity = false;
                }
            }

            private void RequestDespawn()
            {
                _active = false;

                ObjectPoolManager pool = _owningPool != null ? _owningPool : GlobalRegistry.ObjectPool;
                if (pool != null && TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
                {
                    pool.Despawn(gameObject);
                    return;
                }

                gameObject.SetActive(false);
            }

            private void TryRegisterToDispatcher()
            {
                if (_registeredToDispatcher || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                    return;

                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = GlobalRegistry.Updatables.Contains(this);
            }

            private void TryUnregisterFromDispatcher()
            {
                if (!_registeredToDispatcher)
                    return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }

            private void ResetPhysicsState()
            {
                if (_rigidbody == null)
                    return;

                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
                _rigidbody.isKinematic = false;
                _rigidbody.useGravity = true;
            }

            private void ResetRuntimeState()
            {
                _active = false;
                _sinking = false;
                _ageSeconds = 0f;
                _lifetimeSeconds = 0f;
                _sinkDurationSeconds = 0f;
                _sinkStartTimeSeconds = 0f;
                _sinkDepthMeters = 0f;
                _sinkStartPosition = Vector3.zero;
                _sinkEndPosition = Vector3.zero;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            lootCount = Mathf.Max(0, lootCount);
            lootLifetime = Mathf.Max(0f, lootLifetime);
            scatterRadius = Mathf.Max(0f, scatterRadius);
            scatterForce = Mathf.Max(0f, scatterForce);
            chunkSize = Mathf.Max(1f, chunkSize);
            maxMeltRadius = Mathf.Max(0f, maxMeltRadius);
        }

        [ContextMenu("Generate Deterministic ID")]
        private void EditorGenerateDeterministicId()
        {
            ulong tombstoneId = PersistentWorldRegistry.ComputeResourceNodeTombstoneId(transform.position);
            uniqueId = PersistentWorldRegistry.FormatResourceNodeTombstoneId(tombstoneId);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private void OnDrawGizmos()
        {
            Vector3 halfExtents = Application.isPlaying ? SpatialHalfExtents : Vector3.one * 0.5f;
            Gizmos.color = !string.IsNullOrEmpty(uniqueId)
                ? new Color(1f, 0.35f, 0.1f, 0.35f)
                : new Color(0.6f, 0.6f, 0.6f, 0.2f);
            Gizmos.DrawWireCube(transform.position, halfExtents * 2f);
        }
#endif
    }
}
