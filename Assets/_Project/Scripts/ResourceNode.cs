using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Gameplay.Atlas6Liability;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Tools;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Scavenging
{
    /// <summary>
    /// Pooled harvestable node with AUP-derived persistence and spatial-hash registration.
    /// Legacy UniqueId support remains for scene-authored compatibility, but authoritative depletion lives in PersistentWorldRegistry.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceNode : MonoBehaviour, IPoolable, ICuttable, IInteractionSignalConsumer, IInteractionVulnerabilitySource
    {
        private static int s_x001ResourceNodeSignalPushDropCount;
        private static readonly int _MeltCenterId = Shader.PropertyToID("_MeltCenter");
        private static readonly int _MeltRadiusId = Shader.PropertyToID("_MeltRadius");
        private const uint ImpactDebrisRockSpeciesHash = 0x524E4442u; // RNDB
        private const uint ImpactDebrisSedimentSpeciesHash = 0x53454442u; // SEDB
        private const float DefaultFirstYieldSampleSeconds = 0.12f;
        private const float MinimumYieldSampleSeconds = 0.016f;
        private const int DepletionLockFree = 0;
        private const int DepletionLockOwned = 1;
        private const float QualityParticleEnableThreshold = 0.22f;
        private const float QualityParticleInvRange = 1f / (1f - QualityParticleEnableThreshold);
        private static readonly int _SteamExplosionLayerMask = HectonLayerMasks.MountedSweepLayerMask;
        private static readonly SpatialQueryHit[] _steamExplosionContacts = new SpatialQueryHit[16];
        private static readonly Rigidbody[] _steamExplosionBodyBuffer = new Rigidbody[16];
        // COLD ALLOC: RegistryBucket<ResourceNode>[4096] — authored/persistent resource node registry for legacy world-state compatibility — owner: ResourceNode
        private static readonly RegistryBucket<ResourceNode> _worldStateRegistry = new RegistryBucket<ResourceNode>(4096);
        private static readonly RegistryCacheListener _registryCacheListener = new RegistryCacheListener();
        private static PersistentWorldRegistry s_persistentWorldRegistry;
        private static WorldStateManager s_worldStateManager;
        private static IPlayerInventoryService s_playerInventoryService;
        private static IModularEquipmentService s_modularEquipmentService;
        private static IObjectPoolService s_objectPool;
        private static IPhysicsService s_physicsService;
        private static bool s_registryCacheBootstrapped;
        private static bool s_registryCacheRegistered;
        private static int s_registryCacheRefreshFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _worldStateRegistry.Clear();
            s_persistentWorldRegistry = null;
            s_worldStateManager = null;
            s_playerInventoryService = null;
            s_modularEquipmentService = null;
            s_objectPool = null;
            s_physicsService = null;
            s_registryCacheBootstrapped = false;
            s_registryCacheRegistered = false;
            s_registryCacheRefreshFrame = -1;
        }

        private sealed class RegistryCacheListener : IGlobalRegistryHotSwapListener
        {
            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                switch (serviceSlot)
                {
                    case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                        s_persistentWorldRegistry = currentService as PersistentWorldRegistry;
                        break;
                    case GlobalRegistryServiceSlot.WorldStateRuntime:
                        s_worldStateManager = currentService as WorldStateManager;
                        break;
                    case GlobalRegistryServiceSlot.PlayerInventory:
                        s_playerInventoryService = currentService as IPlayerInventoryService;
                        break;
                    case GlobalRegistryServiceSlot.ModularEquipment:
                        s_modularEquipmentService = currentService as IModularEquipmentService;
                        break;
                    case GlobalRegistryServiceSlot.ObjectPool:
                        CacheObjectPoolService(currentService as ObjectPoolManager);
                        break;
                    case GlobalRegistryServiceSlot.Physics:
                        s_physicsService = currentService as IPhysicsService;
                        break;
                }
            }
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

        [SerializeField]
        [Tooltip("Optional cheap mesh used when GlobalQualityWeight is near minimum survival. Collider and economy truth stay unchanged.")]
        private Mesh lowQualityNodeMesh;

        [SerializeField]
        [Tooltip("Optional authored ambient particle systems. Emission is continuously scaled by GlobalQualityWeight and disabled on weak devices.")]
        private ParticleSystem[] qualityScaledParticleSystems;

        [SerializeField, Range(0f, 128f)]
        [Tooltip("Maximum ambient particle emission rate at GlobalQualityWeight 1.0.")]
        private float maxQualityParticleRate = 18f;

        [SerializeField, Range(0, 512)]
        [Tooltip("Maximum ambient particle budget at GlobalQualityWeight 1.0.")]
        private int maxQualityParticles = 96;

        private Transform _cachedTransform;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private BoxCollider _boxCollider;
        private SphereCollider _sphereCollider;
        private GameObject _cachedGameObject;
        private MaterialPropertyBlock _propertyBlock;
        private Vector4 _localHitPoint;
        private float _currentHealth;
        private bool _isDepleted;
        private bool _despawnRequested;
        private bool _lootSpawnBlockedLogged;
        private bool _isKnownPooledInstance;
        private uint _lastLootOracleToolMask = ScavengingLootOracleConstants.ToolMaskAny;
        private GameObject _cachedLootOraclePrefab;
        private uint _cachedLootOracleItemHash;
        private uint _cachedLootOracleUnitQuantity;
        private bool _registeredToWorldStateRegistry;
        private bool _worldStateSuppressedByPersistence;
        private int _spatialHandle;
        private ulong _persistentTombstoneId;
        private AbsoluteUniversePosition _persistentAup;
        private bool _hasPersistentAup;
        private bool _pendingFreshRuntimeTemplateHealthReset;
        private HectonVoxelEngine _cachedVoxelEngine;
        private float _pressureMetamorphismProgressSeconds;
        private long _fractionalYieldRemainderGrams;
        private int _yieldDropCount;
        private int _depletionLockState;
        private int _resourceTemplateStableHashId;

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

        /// <summary>Cached stable hash of the applied resource template.</summary>
        public int ResourceTemplateStableHashId => _resourceTemplateStableHashId;

        /// <summary>Tool capability mask required by the authored node template.</summary>
        public uint VulnerabilityMask => ResolveRequiredToolCapabilityMask(resourceTemplate);

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

        internal static int ApplyPersistentWorldRegistryStateToRegisteredNodes()
        {
            return ApplyPersistentWorldRegistryStateToRegisteredNodes(null);
        }

        internal static int ApplyPersistentWorldRegistryStateToRegisteredNodes(PersistentWorldRegistry registry)
        {
            int suppressedCount = 0;
            EnsureRegistryCache();
            if (registry != null)
                s_persistentWorldRegistry = registry;

            for (int i = _worldStateRegistry.Count - 1; i >= 0; i--)
            {
                ResourceNode node = _worldStateRegistry.GetAt(i);
                if (node == null ||
                    node.IsPooledInstance() ||
                    !node.gameObject.activeSelf ||
                    !node.ShouldSuppressSpawn())
                {
                    continue;
                }

                node.ApplyWorldStateSuppression();
                suppressedCount++;
            }

            return suppressedCount;
        }

        private static void EnsureRegistryCache()
        {
            if (!s_registryCacheRegistered && Application.isPlaying)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(_registryCacheListener);
                s_registryCacheRegistered = GlobalRegistry.TryRegisterHotSwapListener(_registryCacheListener);
            }

            if (s_registryCacheBootstrapped && !ShouldRefreshRegistryCacheCold())
                return;

            s_registryCacheBootstrapped = true;
            s_registryCacheRefreshFrame = Application.isPlaying ? Time.frameCount : -1;
            s_persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            s_worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
            s_playerInventoryService = GlobalRegistry.PlayerInventory;
            s_modularEquipmentService = GlobalRegistry.ModularEquipment;
            CacheObjectPoolService(null);
            s_physicsService = GlobalRegistry.Physics;
        }

        private static void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                s_objectPool = pool;
                return;
            }

            s_objectPool = null;
        }

        private static bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = s_objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                s_objectPool = resolved;
                pool = resolved;
                return true;
            }

            s_objectPool = null;
            pool = null;
            return false;
        }

        private static bool ShouldRefreshRegistryCacheCold()
        {
            if (!Application.isPlaying)
                return false;

            if (s_registryCacheRefreshFrame == Time.frameCount)
                return false;

            return s_persistentWorldRegistry == null ||
                   s_worldStateManager == null ||
                   s_playerInventoryService == null ||
                   s_modularEquipmentService == null ||
                   s_objectPool == null ||
                   s_physicsService == null;
        }

        private void Awake()
        {
            EnsureRegistryCache();
            _cachedGameObject = gameObject;
            _cachedTransform = transform;
            TryGetComponent(out _meshFilter);
            TryGetComponent(out _meshRenderer);
            TryGetComponent(out _boxCollider);
            TryGetComponent(out _sphereCollider);
            targetRenderer = targetRenderer != null
                ? targetRenderer
                : (_meshRenderer != null ? _meshRenderer : ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(_cachedTransform));

            _propertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-node melt shader overrides — owner: ResourceNode
            ResetState();
            RefreshResourceTemplateStableHash();
            TryWarmLootOraclePayloadCache();
        }

        private void OnEnable()
        {
            EnsureRegistryCache();

            if (IsPooledInstance())
                return;

            RegisterWorldStateRegistry();
            ActivateRuntimeState();
            InteractableRegistry.RegisterTree(this);
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterSpatialHandle();
            if (IsPooledInstance())
                UnregisterWorldStateRegistry();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterWorldStateRegistry();
        }

        public void OnSpawn()
        {
            _isKnownPooledInstance = true;
            EnsureRegistryCache();
            ResetState();
            _pendingFreshRuntimeTemplateHealthReset = true;
            ActivateRuntimeState();
            InteractableRegistry.RegisterTree(this);
        }

        public void OnDespawn()
        {
            InteractableRegistry.InvalidateTree(this);
            UnregisterSpatialHandle();
            UnregisterWorldStateRegistry();
            ResetState();
            _pendingFreshRuntimeTemplateHealthReset = false;
            _persistentTombstoneId = 0UL;
            _persistentAup = default;
            _hasPersistentAup = false;
            if (autoGenerateId)
                uniqueId = null;
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
            RefreshResourceTemplateStableHash();
            if (template == null)
            {
                _pendingFreshRuntimeTemplateHealthReset = false;
                return;
            }

            maxHealth = template.MaxIntegrity;
            lootPrefab = template.LootPickupPrefab;
            _cachedLootOraclePrefab = null;
            _cachedLootOracleItemHash = 0u;
            _cachedLootOracleUnitQuantity = 0u;
            lootCount = template.DefaultLootCount;
            TryCacheLootOraclePayloadFromTemplate(template);
            ApplyPresentation(template, fallbackMesh, fallbackMaterial);
            if (_pendingFreshRuntimeTemplateHealthReset)
            {
                _currentHealth = Mathf.Max(1f, maxHealth);
                _pendingFreshRuntimeTemplateHealthReset = false;
            }
            else
            {
                _currentHealth = Mathf.Clamp(_currentHealth, 0f, maxHealth);
            }

            if (isActiveAndEnabled)
                InteractableRegistry.RegisterTree(this);

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

            RefreshPersistentIdentity();
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
            if (!CanApplyToolCapability(ToolCapabilityMasks.Cut))
                return;

            _lastLootOracleToolMask = ScavengingLootOracleConstants.ToolMaskCutter;
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

            uint capabilityMask = ToolCapabilityMasks.ResolveCapabilityMask((InteractionEffectType)signal.EffectType);
            if (capabilityMask != 0u && !CanApplyToolCapability(capabilityMask))
                return;

            _lastLootOracleToolMask = ResolveLootOracleToolMask((InteractionEffectType)signal.EffectType);
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
            _lastLootOracleToolMask = ScavengingLootOracleConstants.ToolMaskAny;
            TakeDamage(
                amount,
                amount,
                ResolveYieldSampleDeltaSeconds(),
                _cachedTransform != null ? _cachedTransform.position : transform.position,
                Vector3.up,
                allowIncrementalYield: true,
                allowImpactDebris: true);
        }

        private bool CanApplyToolCapability(uint capabilityMask)
        {
            if (capabilityMask == 0u)
                return true;

            return (VulnerabilityMask & capabilityMask) != 0u;
        }

        private static uint ResolveRequiredToolCapabilityMask(ResourceNodeTemplate template)
        {
            return template != null
                ? ResolveRequiredToolCapabilityMask(template.RequiredToolClass)
                : uint.MaxValue;
        }

        private static uint ResolveRequiredToolCapabilityMask(ResourceNodeTemplate.HarvestToolClass toolClass)
        {
            switch (toolClass)
            {
                case ResourceNodeTemplate.HarvestToolClass.Knife:
                    return ToolCapabilityMasks.Cut;

                case ResourceNodeTemplate.HarvestToolClass.Drill:
                    return ToolCapabilityMasks.Drill;

                case ResourceNodeTemplate.HarvestToolClass.Laser:
                    return ToolCapabilityMasks.Laser;

                case ResourceNodeTemplate.HarvestToolClass.Salvage:
                    return ToolCapabilityMasks.Salvage;

                case ResourceNodeTemplate.HarvestToolClass.Any:
                default:
                    return uint.MaxValue;
            }
        }

        private void ActivateRuntimeState()
        {
            RefreshPersistentIdentity();
            if (ShouldSuppressSpawn())
            {
                _isDepleted = true;
                _worldStateSuppressedByPersistence = true;
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

        private void RefreshPersistentIdentity()
        {
            Vector3 runtimePosition = _cachedTransform != null ? _cachedTransform.position : transform.position;
            _hasPersistentAup = TryResolveAupFromRuntimeOrigin(runtimePosition, out _persistentAup);
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

        private static bool IsFiniteRuntimePosition(Vector3 position)
        {
            return math.isfinite(position.x) &&
                   math.isfinite(position.y) &&
                   math.isfinite(position.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteRuntimePosition(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            double3 localDelta = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            positionAup = AbsoluteUniversePosition.OffsetMeters(in originAup, localDelta);
            return IsFiniteAup(in positionAup);
        }

        private bool TryResolveAupFromPersistentRuntimeDelta(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!_hasPersistentAup || !IsFiniteAup(in _persistentAup) || !IsFiniteRuntimePosition(runtimePosition))
                return false;

            Vector3 anchorRuntime = _cachedTransform != null ? _cachedTransform.position : transform.position;
            if (!IsFiniteRuntimePosition(anchorRuntime))
                return false;

            double3 localDelta = new double3(
                (double)runtimePosition.x - anchorRuntime.x,
                (double)runtimePosition.y - anchorRuntime.y,
                (double)runtimePosition.z - anchorRuntime.z);
            positionAup = AbsoluteUniversePosition.OffsetMeters(in _persistentAup, localDelta);
            return IsFiniteAup(in positionAup);
        }

        private bool ShouldSuppressSpawn()
        {
            PersistentWorldRegistry registry = s_persistentWorldRegistry;
            if (registry != null && registry.IsResourceNodeTombstoned(_persistentTombstoneId))
                return true;

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager worldStateManager = s_worldStateManager;
                if (worldStateManager != null && worldStateManager.IsNodeDepleted(uniqueId))
                    return true;
            }

            return false;
        }

        private void RegisterPersistentDepletion()
        {
            PersistentWorldRegistry registry = s_persistentWorldRegistry;
            if (registry != null)
                registry.TryRegisterDestroyedResourceNode(_persistentTombstoneId, in _persistentAup);

            if (!string.IsNullOrEmpty(uniqueId))
            {
                WorldStateManager worldStateManager = s_worldStateManager;
                if (worldStateManager != null)
                    worldStateManager.RegisterDepletedNode(uniqueId);
            }

            TryApplyDepletionCrater();
        }

        private bool TrySpawnLoot()
        {
            if (resourceTemplate != null && resourceTemplate.ExtractorYieldItem != null)
                return TrySpawnTemplateDepletionYield();

            if (lootPrefab == null || lootCount <= 0)
                return true;

            if (!TryReadCachedLootOraclePayload(out uint itemHash, out uint quantity))
            {
                if (!_lootSpawnBlockedLogged)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[ResourceNode] Loot prefab has no PickupItem/HectonItem payload. Depletion aborted to prevent loot loss.", this);
#endif
                    _lootSpawnBlockedLogged = true;
                }

                return false;
            }

            if (!_hasPersistentAup || !IsFiniteAup(in _persistentAup))
                return false;

            IPlayerInventoryService inventoryService = s_playerInventoryService;
            var inventory = inventoryService != null ? inventoryService.Inventory : null;
            uint signalQuantity = ScavengingLootOracleRuntime.ClampItemSignalQuantity(quantity);
            int quantityForCapacity = (int)signalQuantity;
            bool capacityAvailable = inventory == null ||
                                     inventory.CanAcceptItemQuantity(unchecked((int)itemHash), quantityForCapacity);
            bool accepted = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                in _persistentAup,
                itemHash,
                0u,
                signalQuantity,
                _lastLootOracleToolMask,
                capacityAvailable);
            if (!accepted && capacityAvailable && !_lootSpawnBlockedLogged)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ResourceNode] Loot oracle unavailable. Depletion aborted to prevent loot loss.", this);
#endif
                _lootSpawnBlockedLogged = true;
            }

            return accepted;
        }

        private bool TrySpawnTemplateDepletionYield()
        {
            if (resourceTemplate == null)
                return true;

            int targetYieldCount = Mathf.Clamp(
                resourceTemplate.DefaultLootCount,
                0,
                (int)ScavengingLootOracleConstants.ItemSignalMaxQuantity);
            int missingYieldCount = targetYieldCount - _yieldDropCount;
            if (missingYieldCount <= 0)
                return true;

            ItemData yieldItem = resourceTemplate.ExtractorYieldItem;
            if (yieldItem == null || yieldItem.PersistentHashId == 0)
                return false;

            if (!_hasPersistentAup || !IsFiniteAup(in _persistentAup))
                return false;

            uint itemHash = unchecked((uint)yieldItem.PersistentHashId);
            uint signalQuantity = ScavengingLootOracleRuntime.ClampItemSignalQuantity((uint)missingYieldCount);
            int quantityForCapacity = (int)signalQuantity;
            IPlayerInventoryService inventoryService = s_playerInventoryService;
            var inventory = inventoryService != null ? inventoryService.Inventory : null;
            bool capacityAvailable = inventory == null ||
                                     inventory.CanAcceptItemQuantity(unchecked((int)itemHash), quantityForCapacity);
            bool accepted = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                in _persistentAup,
                itemHash,
                itemHash,
                signalQuantity,
                _lastLootOracleToolMask,
                capacityAvailable);
            if (!accepted)
            {
                if (capacityAvailable && !_lootSpawnBlockedLogged)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[ResourceNode] Template depletion yield could not be queued. Depletion aborted to prevent loot loss.", this);
#endif
                    _lootSpawnBlockedLogged = true;
                }

                return false;
            }

            _yieldDropCount += quantityForCapacity;
            Atlas6CorporateLiabilityManager.TryReportXenonOmegaExtracted(_resourceTemplateStableHashId, quantityForCapacity);
            return true;
        }

        private void TryWarmLootOraclePayloadCache()
        {
            if (lootPrefab == null || lootCount <= 0)
                return;

            if (_cachedLootOraclePrefab == lootPrefab && _cachedLootOracleItemHash != 0u && _cachedLootOracleUnitQuantity != 0u)
                return;

            TryCaptureLootOraclePayloadFromPrefabCold(out _, out _, allowHierarchyScan: true);
        }

        private bool TryCacheLootOraclePayloadFromTemplate(ResourceNodeTemplate template)
        {
            if (template == null || lootPrefab == null || lootCount <= 0)
                return false;

            int yieldHash = template.ExtractorYieldItemHashId;
            if (yieldHash == 0)
                return false;

            return CacheLootOraclePayload(
                lootPrefab,
                unchecked((uint)yieldHash),
                1,
                out _,
                out _);
        }

        private bool TryReadCachedLootOraclePayload(out uint itemHash, out uint quantity)
        {
            itemHash = 0u;
            quantity = 0u;
            if (lootPrefab == null || lootCount <= 0)
                return false;

            if (_cachedLootOraclePrefab == lootPrefab && _cachedLootOracleItemHash != 0u && _cachedLootOracleUnitQuantity != 0u)
            {
                itemHash = _cachedLootOracleItemHash;
                quantity = MultiplyLootQuantitySaturated(lootCount, _cachedLootOracleUnitQuantity);
                return true;
            }

            return false;
        }

        private bool TryCaptureLootOraclePayloadFromPrefabCold(out uint itemHash, out uint quantity, bool allowHierarchyScan)
        {
            itemHash = 0u;
            quantity = 0u;
            if (lootPrefab == null || lootCount <= 0)
                return false;

            if (TryReadCachedLootOraclePayload(out itemHash, out quantity))
                return true;

            if (lootPrefab.TryGetComponent(out PickupItem pickupItem) && pickupItem.ItemData != null)
            {
                return CacheLootOraclePayload(lootPrefab, unchecked((uint)pickupItem.ItemData.PersistentHashId), pickupItem.Quantity, out itemHash, out quantity);
            }

            if (allowHierarchyScan)
            {
                if (!lootPrefab.TryGetComponent(out PickupItem childPickup))
                    childPickup = ComponentReferenceUtility.ResolveOwnedComponent<PickupItem>(lootPrefab.transform);
                if (childPickup != null && childPickup.ItemData != null)
                {
                    return CacheLootOraclePayload(lootPrefab, unchecked((uint)childPickup.ItemData.PersistentHashId), childPickup.Quantity, out itemHash, out quantity);
                }
            }

            if (lootPrefab.TryGetComponent(out HectonItem hectonItem) && hectonItem.Data != null)
            {
                return CacheLootOraclePayload(lootPrefab, unchecked((uint)hectonItem.Data.PersistentHashId), hectonItem.Quantity, out itemHash, out quantity);
            }

            if (allowHierarchyScan)
            {
                if (!lootPrefab.TryGetComponent(out HectonItem childHectonItem))
                    childHectonItem = ComponentReferenceUtility.ResolveOwnedComponent<HectonItem>(lootPrefab.transform);
                if (childHectonItem != null && childHectonItem.Data != null)
                {
                    return CacheLootOraclePayload(lootPrefab, unchecked((uint)childHectonItem.Data.PersistentHashId), childHectonItem.Quantity, out itemHash, out quantity);
                }
            }

            return false;
        }

        private bool CacheLootOraclePayload(GameObject prefab, uint resolvedItemHash, int unitQuantity, out uint itemHash, out uint quantity)
        {
            itemHash = resolvedItemHash;
            uint safeUnitQuantity = (uint)Mathf.Max(1, unitQuantity);
            quantity = MultiplyLootQuantitySaturated(lootCount, safeUnitQuantity);
            if (itemHash == 0u)
                return false;

            _cachedLootOraclePrefab = prefab;
            _cachedLootOracleItemHash = itemHash;
            _cachedLootOracleUnitQuantity = safeUnitQuantity;
            return true;
        }

        private static uint MultiplyLootQuantitySaturated(int authoredLootCount, uint unitQuantity)
        {
            uint safeLootCount = (uint)Mathf.Max(1, authoredLootCount);
            uint safeUnitQuantity = math.max(1u, unitQuantity);
            ulong total = (ulong)safeLootCount * safeUnitQuantity;
            return (uint)math.min(total, (ulong)uint.MaxValue);
        }

        private static uint ResolveLootOracleToolMask(InteractionEffectType effectType)
        {
            switch (effectType)
            {
                case InteractionEffectType.Drill:
                    return ScavengingLootOracleConstants.ToolMaskDrill;
                case InteractionEffectType.PlasmaCut:
                case InteractionEffectType.Torch:
                case InteractionEffectType.Boil:
                    return ScavengingLootOracleConstants.ToolMaskCutter;
                default:
                    return ScavengingLootOracleConstants.ToolMaskKnife;
            }
        }

        private bool ShouldTriggerSteamExplosion(in Hecton8.Interaction.InteractionSignal signal)
        {
            if (resourceTemplate == null ||
                !resourceTemplate.TriggersSteamExplosionWithoutThermalShield ||
                !IsMiningSignal(in signal))
            {
                return false;
            }

            IModularEquipmentService modularEquipment = s_modularEquipmentService;
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

            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            int overlapCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                runtimeOrigin,
                radius,
                kindMask,
                _steamExplosionContacts);
            if (overlapCount <= 0)
                return;

            int uniqueBodyCount = 0;
            for (int i = 0; i < overlapCount; i++)
            {
                SpatialQueryHit hit = _steamExplosionContacts[i];
                _steamExplosionContacts[i] = default;
                if (!LayerMatchesMask(hit.Layer, _SteamExplosionLayerMask))
                    continue;

                Rigidbody body = hit.Rigidbody;
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

                s_physicsService?.QueueForce(body, impulse, ForceMode.Impulse);
            }
        }

        private static bool LayerMatchesMask(int layer, int mask)
        {
            return layer >= 0 && layer < 32 && (mask & (1 << layer)) != 0;
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

            if (IsPooledInstance() && TryResolveCachedObjectPool(out IObjectPoolService pool))
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
            _worldStateSuppressedByPersistence = false;
            _lootSpawnBlockedLogged = false;
            _lastLootOracleToolMask = ScavengingLootOracleConstants.ToolMaskAny;
            _pressureMetamorphismProgressSeconds = 0f;
            _fractionalYieldRemainderGrams = 0L;
            _yieldDropCount = 0;
            ResetDepletionLock();
            ResetMeltProperties();
            ApplyQualityScaledParticles(0f);
        }

        private bool TryAcquireDepletionLock()
        {
            return Interlocked.CompareExchange(ref _depletionLockState, DepletionLockOwned, DepletionLockFree) == DepletionLockFree;
        }

        private void ReleaseDepletionLock()
        {
            Interlocked.Exchange(ref _depletionLockState, DepletionLockFree);
        }

        private void ResetDepletionLock()
        {
            _depletionLockState = DepletionLockFree;
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

            try
            {
                if (allowIncrementalYield)
                    TryEmitIncrementalYield(descriptor, Mathf.Max(toolPower, amount), elapsedSeconds, hitPoint, hitNormal);

                if (allowImpactDebris)
                    SpawnImpactDebris(hitPoint, hitNormal, Mathf.Max(toolPower, amount));

                _currentHealth = nextHealth;
                if (_currentHealth > 0f)
                    return;

                if (!TrySpawnLoot())
                {
                    _currentHealth = previousHealth;
                    return;
                }

                _currentHealth = 0f;
                _isDepleted = true;
                _worldStateSuppressedByPersistence = true;
                RegisterPersistentDepletion();
                DespawnSelf();
            }
            finally
            {
                if (depletionLockAcquired)
                    ReleaseDepletionLock();
            }
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

            int unitsToQueue = math.min(wholeUnits, ushort.MaxValue);
            uint seed = unchecked((uint)_persistentTombstoneId) ^ ((uint)_yieldDropCount * 0x9E3779B9u);
            int emittedCount = TryQueueIncrementalYieldItems(primaryYieldItem, unitsToQueue, hitPoint, hitNormal, seed)
                ? unitsToQueue
                : 0;
            _yieldDropCount += emittedCount;

            if (emittedCount > 0)
            {
                Atlas6CorporateLiabilityManager.TryReportXenonOmegaExtracted(_resourceTemplateStableHashId, emittedCount);
                long remainingGrams = availableGrams - (emittedCount * unitItemMassGrams);
                _fractionalYieldRemainderGrams = remainingGrams > 0L ? remainingGrams : 0L;
            }
            else
            {
                _fractionalYieldRemainderGrams = availableGrams;
            }
        }

        private bool TryQueueIncrementalYieldItems(ItemData itemData, int quantity, Vector3 hitPoint, Vector3 hitNormal, uint seed)
        {
            if (itemData == null || quantity <= 0 || itemData.PersistentHashId == 0)
                return false;

            Vector3 outwardNormal = hitNormal.sqrMagnitude > 0.0001f ? ResolveDominantAxis(hitNormal) : ResolveFallbackNormal(hitPoint);
            Vector3 tangent = ResolveTangent(outwardNormal, seed);
            Vector3 signalPosition = hitPoint + (outwardNormal * 0.12f) + (tangent * 0.05f);
            if (!TryResolveAupFromPersistentRuntimeDelta(signalPosition, out AbsoluteUniversePosition aup))
                return false;

            uint itemHash = unchecked((uint)itemData.PersistentHashId);
            uint signalQuantity = ScavengingLootOracleRuntime.ClampItemSignalQuantity((uint)quantity);
            IPlayerInventoryService inventoryService = s_playerInventoryService;
            var inventory = inventoryService != null ? inventoryService.Inventory : null;
            bool capacityAvailable = inventory == null || inventory.CanAcceptItemQuantity(unchecked((int)itemHash), (int)signalQuantity);
            return ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                in aup,
                itemHash,
                itemHash,
                signalQuantity,
                _lastLootOracleToolMask,
                capacityAvailable,
                emitDepletionDelta: false);
        }

        private void SpawnImpactDebris(Vector3 hitPoint, Vector3 hitNormal, float toolPower)
        {
            Vector3 outwardNormal = hitNormal.sqrMagnitude > 0.0001f ? ResolveDominantAxis(hitNormal) : ResolveFallbackNormal(hitPoint);
            Vector3 signalPosition = hitPoint + outwardNormal * 0.08f;
            if (!TryResolveAupFromPersistentRuntimeDelta(signalPosition, out AbsoluteUniversePosition aup))
                return;

            float safeToolPower = math.select(0.4f, toolPower, math.isfinite(toolPower));
            float power01 = math.saturate(safeToolPower * 0.2857143f);
            float quality = ScavengingLootOracleMath.SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);
            float qualityCurve = quality * quality * (3f - (2f * quality));
            int requestedParticles = (int)math.round(math.lerp(4f, 64f, qualityCurve) * math.lerp(0.75f, 1.25f, power01));
            ResourceNodeTemplate.DebrisPhysicalProfile debrisProfile = ResolveDebrisPhysicalProfile();
            uint seed = unchecked((uint)_persistentTombstoneId) ^
                        unchecked((uint)((ulong)_persistentTombstoneId >> 32)) ^
                        ((uint)(_yieldDropCount + 1) * 0x85EBCA6Bu);
            seed ^= debrisProfile == ResourceNodeTemplate.DebrisPhysicalProfile.Sediment
                ? ImpactDebrisSedimentSpeciesHash
                : ImpactDebrisRockSpeciesHash;

            DebrisSpawnSignal signal = default;
            signal.PositionAup = aup;
            signal.SpeciesHash = debrisProfile == ResourceNodeTemplate.DebrisPhysicalProfile.Sediment
                ? ImpactDebrisSedimentSpeciesHash
                : ImpactDebrisRockSpeciesHash;
            signal.SourceEntityId = seed != 0u ? seed : ImpactDebrisRockSpeciesHash;
            signal.Intensity01 = power01;
            signal.DebrisKind = DebrisSpawnSignal.DebrisKindRockShard;
            signal.Flags = DebrisSpawnSignal.FlagComputeShard;
            signal.Quantity = (ushort)math.clamp(requestedParticles, 1, 96);
            SignalBus<DebrisSpawnSignal>.TryPushTracked(in signal, ref s_x001ResourceNodeSignalPushDropCount);
        }

        private ResourceNodeTemplate.DebrisPhysicalProfile ResolveDebrisPhysicalProfile()
        {
            return resourceTemplate != null
                ? resourceTemplate.ResolveDebrisPhysicalProfile()
                : ResourceNodeTemplate.DebrisPhysicalProfile.Basalt;
        }

        private void RefreshResourceTemplateStableHash()
        {
            _resourceTemplateStableHashId = resourceTemplate != null
                ? resourceTemplate.ResolveStableHashIdCold()
                : 0;
        }

        private static float ResolveYieldSampleDeltaSeconds()
        {
            return DefaultFirstYieldSampleSeconds;
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

            if (IsPooledInstance())
                return;

            _worldStateRegistry.Register(this);
            _registeredToWorldStateRegistry = true;
        }

        internal void ApplyWorldStateSuppression()
        {
            RefreshPersistentIdentity();
            _isDepleted = true;
            _worldStateSuppressedByPersistence = true;
            DespawnSelf();
        }

        internal bool TryRestoreWorldStateSuppression()
        {
            if (!_worldStateSuppressedByPersistence)
                return false;

            EnsureRegistryCache();
            RefreshPersistentIdentity();
            if (ShouldSuppressSpawn())
                return false;

            ResetState();
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            return true;
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
            float qualityWeight = ScavengingLootOracleMath.SanitizeQualityWeight(HomeostasisBrain.GlobalQualityWeight);

            if (_meshFilter != null)
            {
                Mesh authoredMesh = template.NodeMesh != null
                    ? template.NodeMesh
                    : (fallbackMesh != null ? fallbackMesh : _meshFilter.sharedMesh);
                _meshFilter.sharedMesh = qualityWeight <= QualityParticleEnableThreshold && lowQualityNodeMesh != null
                    ? lowQualityNodeMesh
                    : authoredMesh;
            }

            if (_meshRenderer != null)
            {
                Material sharedMaterial = template.NodeMaterial != null
                    ? template.NodeMaterial
                    : (fallbackMaterial != null ? fallbackMaterial : _meshRenderer.sharedMaterial);
                if (sharedMaterial != null)
                    _meshRenderer.sharedMaterial = sharedMaterial;

                targetRenderer = _meshRenderer;
            }

            ConfigurePrimitiveColliders(template.RuntimeColliderShape, physicalSize);
            ApplyQualityScaledParticles(qualityWeight);
            ResetMeltProperties();
        }

        private void ApplyQualityScaledParticles(float qualityWeight)
        {
            ParticleSystem[] systems = qualityScaledParticleSystems;
            if (systems == null || systems.Length == 0)
                return;

            float emissionWeight = math.saturate((qualityWeight - QualityParticleEnableThreshold) * QualityParticleInvRange);
            bool enableEmission = emissionWeight > 0.001f;
            int particleBudget = enableEmission
                ? math.clamp((int)math.round(maxQualityParticles * emissionWeight), 1, math.max(1, maxQualityParticles))
                : 0;
            float emissionRate = math.max(0f, maxQualityParticleRate) * emissionWeight;

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem particleSystem = systems[i];
                if (particleSystem == null)
                    continue;

                ParticleSystem.MainModule main = particleSystem.main;
                main.maxParticles = particleBudget;

                ParticleSystem.EmissionModule emission = particleSystem.emission;
                emission.enabled = enableEmission;
                emission.rateOverTimeMultiplier = emissionRate;

                if (enableEmission)
                {
                    if (!particleSystem.isPlaying)
                        particleSystem.Play(true);
                }
                else if (particleSystem.isPlaying)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
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

        internal bool IsPooledInstance()
        {
            if (_isKnownPooledInstance)
                return true;

            if (TryResolveCachedObjectPool(out IObjectPoolService pool) &&
                pool.CanDespawnWithoutDestroy(_cachedGameObject != null ? _cachedGameObject : gameObject))
            {
                _isKnownPooledInstance = true;
                return true;
            }

            if (!TryGetComponent(out ObjectPoolManager.PoolItemMarker _))
                return false;

            _isKnownPooledInstance = true;
            return true;
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
            ScavengingLootOracleRuntime.DrawHighestProbabilityGizmo(this, transform.position);
        }
#endif
    }
}
