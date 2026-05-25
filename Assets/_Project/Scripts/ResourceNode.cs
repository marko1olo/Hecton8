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
        private const uint ImpactDebrisRockSpeciesHash = 0x524E4442u; // RNDB
        private const uint ImpactDebrisSedimentSpeciesHash = 0x53454442u; // SEDB
        private const float DefaultFirstYieldSampleSeconds = 0.12f;
        private const float MinimumYieldSampleSeconds = 0.016f;
        private const int DepletionLockFree = 0;
        private const int DepletionLockOwned = 1;
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
        private static bool s_registryCacheRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _worldStateRegistry.Clear();
            s_persistentWorldRegistry = null;
            s_worldStateManager = null;
            s_playerInventoryService = null;
            s_modularEquipmentService = null;
            s_objectPool = null;
            s_registryCacheRegistered = false;
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
                        s_objectPool = currentService as IObjectPoolService;
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
        private uint _lastLootOracleToolMask = ScavengingLootOracleConstants.ToolMaskAny;
        private GameObject _cachedLootOraclePrefab;
        private uint _cachedLootOracleItemHash;
        private uint _cachedLootOracleUnitQuantity;
        private bool _registeredToWorldStateRegistry;
        private int _spatialHandle;
        private ulong _persistentTombstoneId;
        private AbsoluteUniversePosition _persistentAup;
        private bool _hasPersistentAup;
        private HectonVoxelEngine _cachedVoxelEngine;
        private float _pressureMetamorphismProgressSeconds;
        private long _fractionalYieldRemainderGrams;
        private int _yieldDropCount;
        private int _depletionLockState;

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

        private static void EnsureRegistryCache()
        {
            s_persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            s_worldStateManager = Hecton8.Core.GlobalRegistry.WorldState;
            s_playerInventoryService = GlobalRegistry.PlayerInventory;
            s_modularEquipmentService = GlobalRegistry.ModularEquipment;
            s_objectPool = GlobalRegistry.ObjectPoolService;

            if (s_registryCacheRegistered || !Application.isPlaying)
                return;

            s_registryCacheRegistered = GlobalRegistry.TryRegisterHotSwapListener(_registryCacheListener);
        }

        private void Awake()
        {
            EnsureRegistryCache();
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
            TryWarmLootOraclePayloadCache();
        }

        private void OnEnable()
        {
            EnsureRegistryCache();
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
            EnsureRegistryCache();
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
            _cachedLootOraclePrefab = null;
            _cachedLootOracleItemHash = 0u;
            _cachedLootOracleUnitQuantity = 0u;
            lootCount = template.DefaultLootCount;
            TryWarmLootOraclePayloadCache();
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
            return float.IsFinite(position.x) &&
                   float.IsFinite(position.y) &&
                   float.IsFinite(position.z);
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
                return true;

            if (lootPrefab == null || lootCount <= 0)
                return true;

            if (!TryResolveLootOraclePayload(out uint itemHash, out uint quantity, allowHierarchyScan: false))
            {
                if (!_lootSpawnBlockedLogged)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[ResourceNode] Loot prefab has no PickupItem/HectonItem payload. Depletion aborted to prevent loot loss.", this);
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
                Debug.LogError("[ResourceNode] Loot oracle unavailable. Depletion aborted to prevent loot loss.", this);
#endif
                _lootSpawnBlockedLogged = true;
            }

            return accepted;
        }

        private void TryWarmLootOraclePayloadCache()
        {
            if (lootPrefab == null || lootCount <= 0)
                return;

            if (_cachedLootOraclePrefab == lootPrefab && _cachedLootOracleItemHash != 0u && _cachedLootOracleUnitQuantity != 0u)
                return;

            TryResolveLootOraclePayload(out _, out _, allowHierarchyScan: true);
        }

        private bool TryResolveLootOraclePayload(out uint itemHash, out uint quantity, bool allowHierarchyScan)
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

            if (lootPrefab.TryGetComponent(out PickupItem pickupItem) && pickupItem.ItemData != null)
            {
                return CacheLootOraclePayload(lootPrefab, unchecked((uint)pickupItem.ItemData.PersistentHashId), pickupItem.Quantity, out itemHash, out quantity);
            }

            if (allowHierarchyScan)
            {
                if (!lootPrefab.TryGetComponent(out PickupItem childPickup))
                    childPickup = lootPrefab.GetComponentInChildren<PickupItem>(true);
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
                    childHectonItem = lootPrefab.GetComponentInChildren<HectonItem>(true);
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

                PhysicsForceRouter.QueueForce(body, impulse, ForceMode.Impulse);
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

            IObjectPoolService pool = s_objectPool;
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
            _lastLootOracleToolMask = ScavengingLootOracleConstants.ToolMaskAny;
            _pressureMetamorphismProgressSeconds = 0f;
            _fractionalYieldRemainderGrams = 0L;
            _yieldDropCount = 0;
            ResetDepletionLock();
            ResetMeltProperties();
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

            int unitsToQueue = math.min(wholeUnits, ushort.MaxValue);
            uint seed = unchecked((uint)_persistentTombstoneId) ^ ((uint)_yieldDropCount * 0x9E3779B9u);
            int emittedCount = TryQueueIncrementalYieldItems(primaryYieldItem, unitsToQueue, hitPoint, hitNormal, seed)
                ? unitsToQueue
                : 0;
            _yieldDropCount += emittedCount;

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
            SignalBus<DebrisSpawnSignal>.TryPush(in signal);
        }

        private ResourceNodeTemplate.DebrisPhysicalProfile ResolveDebrisPhysicalProfile()
        {
            return resourceTemplate != null
                ? resourceTemplate.ResolveDebrisPhysicalProfile()
                : ResourceNodeTemplate.DebrisPhysicalProfile.Basalt;
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
