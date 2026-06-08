using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Autonomous repair dispatch hub that consumes power and scrap to launch pooled drones toward damaged base modules.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Repair Drone Hub")]
    public sealed class RepairDroneHub : MonoBehaviour, ISlowTickable, IPoolable, IPowerComponent, IGlobalRegistryHotSwapListener
    {
        private const string DefaultRepairSupplyItemId = "Nanite_Solder";
        private const string LegacyRepairSupplyItemId = "Data_TitaniumScrap";
        private const int MaxDiscoveredSupplyCrates = 12;
        private const int SupplyOverlapCapacity = 24;
        private const int SupplyCrateLookupCacheCapacity = SupplyOverlapCapacity;
        private const int MaxMainThreadSupplyScanCount = 64;
        private const int ActiveHubCapacity = 32;
        private const float SupplyRescanInterval = 5f;
        private static readonly int DefaultRepairSupplyHashId = Hecton.Localization.LocHash.Compute(DefaultRepairSupplyItemId);
        private static readonly int LegacyRepairSupplyHashId = Hecton.Localization.LocHash.Compute(LegacyRepairSupplyItemId);
        // COLD ALLOC: RepairDroneHub[32] - fixed active hub registry, no managed List growth - owner: RepairDroneHub
        private static readonly RepairDroneHub[] s_ActiveHubs = new RepairDroneHub[ActiveHubCapacity];
        private static int s_ActiveHubCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_ActiveHubCount; i++)
                s_ActiveHubs[i] = null;

            s_ActiveHubCount = 0;
            DroneFleetManager.ClearRepairTorchAcousticBinding();
        }

        private static void RefreshRepairTorchAcousticBindingFromActiveHubs()
        {
            for (int i = s_ActiveHubCount - 1; i >= 0; i--)
            {
                RepairDroneHub hub = s_ActiveHubs[i];
                if (hub == null || hub.repairTorchAcousticClip == null)
                    continue;

                DroneFleetManager.ConfigureRepairTorchAcoustic(
                    hub.repairTorchAcousticClip,
                    hub.repairTorchAcousticVolume,
                    hub.repairTorchAcousticPitch);
                return;
            }

            DroneFleetManager.ClearRepairTorchAcousticBinding();
        }

        [Header("Drone Bay")]
        [Tooltip("Optional drone visual source used for headless indirect rendering mesh and material extraction.")]
        [SerializeField] private GameObject dronePrefab;

        [Tooltip("Optional compute shader for GPU-only phantom drone swarm visuals.")]
        [SerializeField] private ComputeShader phantomDroneCompute;

        [Tooltip("Optional material for GPU-only phantom drone swarm visuals.")]
        [SerializeField] private Material phantomDroneMaterial;

        [Tooltip("Optional one-shot repair torch clip forwarded into the headless drone acoustic event lane.")]
        [SerializeField] private AudioClip repairTorchAcousticClip;

        [Tooltip("Base volume for headless repair torch acoustic pulses.")]
        [SerializeField, Range(0f, 1f)] private float repairTorchAcousticVolume = 0.32f;

        [Tooltip("Base pitch for headless repair torch acoustic pulses.")]
        [SerializeField, Range(0.25f, 2f)] private float repairTorchAcousticPitch = 1f;

        [Tooltip("Optional launch socket. Falls back to this transform when omitted.")]
        [SerializeField] private Transform launchPoint;

        [Tooltip("Optional airlock event target opened when a headless drone reaches final docking approach.")]
        [SerializeField] private BaseAirlock dockingAirlock;

        [Tooltip("Maximum number of simultaneous drone sorties this hub can sustain.")]
        [SerializeField, Range(1, 4)] private int maxConcurrentDrones = 1;

        [Tooltip("Integrity threshold below which the hub dispatches repairs. Uses current recoverable integrity as the reference ceiling.")]
        [SerializeField, Range(0.1f, 1f)] private float dispatchIntegrityThreshold = 0.8f;

        [Tooltip("Repair throughput passed into each drone mission.")]
        [SerializeField, Range(1f, 100f)] private float droneRepairRate = 18f;

        [Header("Supply Chain")]
        [Tooltip("Optional explicit storage crates that can feed this hub with repair scrap.")]
        [SerializeField] private StorageCrate[] supplyCrates;

        [Tooltip("Auto-discovers nearby StorageCrate endpoints when explicit references are not authored.")]
        [SerializeField] private bool autoDiscoverNearbyStorage = true;

        [Tooltip("World radius used when auto-discovering nearby supply crates.")]
        [SerializeField, Range(1f, 40f)] private float supplySearchRadius = 18f;

        [Tooltip("Layer mask used while probing for nearby storage crates.")]
        [SerializeField] private LayerMask supplySearchMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Override repair supply item. Falls back to Data_TitaniumScrap through the active ItemCatalog when left empty.")]
        [SerializeField] private ItemData repairSupplyItem;

        [Tooltip("How many scrap units are removed from storage for each sortie.")]
        [SerializeField, Range(1, 8)] private int scrapPerMission = 1;

        [Header("Power Budget")]
        [Tooltip("Baseline power draw of the powered drone bay.")]
        [SerializeField, Range(0f, 50f)] private float standbyPowerDraw = 2f;

        [Tooltip("Additional continuous power draw per drone while it is servicing a module.")]
        [SerializeField, Range(0f, 50f)] private float activeDronePowerDraw = 6f;

        [Tooltip("Immediate one-shot grid cost consumed when a drone launches from the bay.")]
        [SerializeField, Range(0f, 50f)] private float launchBurstPowerCost = 4f;

        [Tooltip("Priority used when the grid starts shedding non-critical loads. Lower is more critical.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 25;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private int _debugActiveDroneCount;
        [SerializeField] private int _debugSupplyCrateCount;
        [SerializeField] private string _debugCurrentTargetName;
        [SerializeField] private int _debugCurrentTargetId;
        [SerializeField] private float _debugLastAssignmentScore;
        [SerializeField] private int _debugLastAssignedSupplyUnits;

        // COLD ALLOC: Collider[24] - nearby storage discovery buffer - owner: RepairDroneHub
        private readonly StorageCrate[] _supplyDiscoveryBuffer = new StorageCrate[SupplyOverlapCapacity];
        // COLD ALLOC: StorageCrate[12] - auto-discovered storage endpoints - owner: RepairDroneHub
        private readonly StorageCrate[] _discoveredSupplyCrates = new StorageCrate[MaxDiscoveredSupplyCrates];
        // COLD ALLOC: int[1] - repair-supply hash bridge for logistics reservations - owner: RepairDroneHub
        private readonly int[] _repairSupplyHashIds = new int[1];
        // COLD ALLOC: int[1] - repair-supply quantity bridge for logistics reservations - owner: RepairDroneHub
        private readonly int[] _repairSupplyAmounts = new int[1];
        // COLD ALLOC: ulong[24] - overlap collider id cache for storage discovery - owner: RepairDroneHub
        private readonly ulong[] _supplyCrateLookupColliderIds = new ulong[SupplyCrateLookupCacheCapacity];
        // COLD ALLOC: StorageCrate[24] - overlap collider resolved storage cache - owner: RepairDroneHub
        private readonly StorageCrate[] _supplyCrateLookupCrates = new StorageCrate[SupplyCrateLookupCacheCapacity];

        private Transform _cachedTransform;
        private BaseAirlock _cachedDockingAirlock;
        private PowerNode _powerNode;
        private int[] _activeDroneIds;
        private int[] _activeTargetIds;
        private bool _registered;
        private bool _hasPower = true;
        private float _supplyRescanTimer;
        private int _discoveredSupplyCount;
        private int _supplyCrateLookupCount;
        private int _supplyCrateLookupWriteCursor;
        private int _launchCountTotal;
        private IPlayerInventoryService _cachedPlayerInventoryService;
        private ItemData _cachedRepairSupplyHashItem;
        private int _cachedRepairSupplyHashId;
        private bool _hotSwapRegistered;

        /// <summary>Hub power draw scales with the number of active sorties.</summary>
        public float PowerRating => -(standbyPowerDraw + ActiveDroneCountInternal * activeDronePowerDraw);

        /// <summary>Priority used during power shedding.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached grid availability propagated by PowerGrid.UpdateBalance.</summary>
        public bool HasPower => _hasPower;

        internal static int ActiveHubCount => s_ActiveHubCount;
        internal int ActiveDroneCount => ActiveDroneCountInternal;
        internal int TotalLaunchCount => _launchCountTotal;
        internal Vector3 DockPosition => ResolvedDockSocketPosition;
        internal Quaternion DockRotation => ResolvedDockSocketRotation;
        internal AbsoluteUniversePosition DockAup => ResolveDockAup();
        internal Vector3 DockForward => ResolvedDockSocketRotation * Vector3.forward;
        internal BaseAirlock DockingAirlock => ResolveDockingAirlock();
        internal PowerGrid CurrentGrid => _powerNode != null ? _powerNode.Grid : null;
        internal int ActiveSlotCapacity => _activeDroneIds != null ? _activeDroneIds.Length : Mathf.Max(1, maxConcurrentDrones);
        internal bool HasOperationalPower => _hasPower && _powerNode != null && _powerNode.Grid != null;

        private AbsoluteUniversePosition ResolveDockAup()
        {
            Vector3 dockPosition = ResolvedDockSocketPosition;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!float.IsFinite(dockPosition.x) ||
                !float.IsFinite(dockPosition.y) ||
                !float.IsFinite(dockPosition.z))
            {
                return originAup;
            }

            AbsoluteUniversePosition dockAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(dockPosition.x, dockPosition.y, dockPosition.z));
            return dockAup.IsFinite() ? dockAup : originAup;
        }

        internal static RepairDroneHub GetActiveHubAt(int index)
        {
            return index >= 0 && index < s_ActiveHubCount ? s_ActiveHubs[index] : null;
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower)
                RecallActiveDrones();

            DroneFleetManager.TryNotifyFleetStateChanged();
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _powerNode);
            CacheDockingAirlockCold();

            int capacity = Mathf.Max(1, maxConcurrentDrones);
            _activeDroneIds = new int[capacity]; // COLD ALLOC: int[capacity] - active headless drone ids by hub slot - owner: RepairDroneHub
            _activeTargetIds = new int[capacity]; // COLD ALLOC: int[capacity] - claimed target ids by slot - owner: RepairDroneHub
            ConfigureDroneFleetRuntimeBindings();
            RefreshCachedRegistryServices();
            ResolveRepairSupplyItem();
        }

        private void OnEnable()
        {
            ConfigureDroneFleetRuntimeBindings();
            CacheDockingAirlockCold();
            CacheRegistryServices();
            RegisterHubInstance();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearSupplyLookupCache();
            _cachedPlayerInventoryService = null;
            UnregisterHubInstance();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            ConfigureDroneFleetRuntimeBindings();
            CacheDockingAirlockCold();
            CacheRegistryServices();
            ResolveRepairSupplyItem();
            ClearSupplyLookupCache();
            RefreshSupplyCrates(true);
            RegisterHubInstance();
            TryRegister();
            DroneFleetManager.TryNotifyFleetStateChanged();
        }

        public void OnDespawn()
        {
            ReturnAllDronesToPool();
            TryUnregister();
            TryUnregisterHotSwapListener();
            _hasPower = true;
            _debugHasPower = true;
            _debugCurrentTargetId = 0;
#if UNITY_EDITOR
            _debugCurrentTargetName = string.Empty;
#endif
            _debugActiveDroneCount = 0;
            ClearSupplyLookupCache();
            _cachedPlayerInventoryService = null;
            UnregisterHubInstance();
            DroneFleetManager.TryNotifyFleetStateChanged();
        }

        public void SlowTick()
        {
            ResolveRepairSupplyItem();
            CompactActiveDrones();

            if (autoDiscoverNearbyStorage)
            {
                _supplyRescanTimer -= 0.5f;
                if (_supplyRescanTimer <= 0f)
                {
                    RefreshSupplyCrates(false);
                    _supplyRescanTimer = SupplyRescanInterval;
                }
            }

            if (!_hasPower)
            {
                RecallActiveDrones();
                RefreshDiagnostics();
                return;
            }

            TryDispatchDrone();
            RefreshDiagnostics();
            DroneFleetManager.TryNotifyFleetStateChanged();
        }

        internal void NotifyHeadlessDroneReturned(int droneId)
        {
            if (droneId <= 0 || _activeDroneIds == null)
                return;

            int slot = FindDroneSlot(droneId);
            if (slot < 0)
                return;

            ClearDroneSlot(slot);
            RefreshDiagnostics();
            DroneFleetManager.TryNotifyFleetStateChanged();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            CacheRegistryServices();
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void ConfigureDroneFleetRuntimeBindings()
        {
            DroneFleetManager.ConfigureHeadlessRenderSource(dronePrefab);
            DroneFleetManager.ConfigurePhantomSwarm(phantomDroneCompute, phantomDroneMaterial);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    bool shouldRestoreTick = currentService != null && isActiveAndEnabled;
                    TryUnregister();
                    if (shouldRestoreTick)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _cachedPlayerInventoryService = currentService as IPlayerInventoryService;
                    break;
            }
        }

        private void ResolveRepairSupplyItem()
        {
            if (repairSupplyItem != null)
            {
                if (!ReferenceEquals(_cachedRepairSupplyHashItem, repairSupplyItem))
                    RefreshRepairSupplyHash();

                return;
            }

            IPlayerInventoryService inventoryService = _cachedPlayerInventoryService;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            ItemCatalog catalog = inventory != null ? inventory.ItemCatalog : null;
            if (catalog != null)
            {
                repairSupplyItem = catalog.FindById(DefaultRepairSupplyItemId);
                if (repairSupplyItem == null)
                    repairSupplyItem = catalog.FindById(LegacyRepairSupplyItemId);
            }

            if (!ReferenceEquals(_cachedRepairSupplyHashItem, repairSupplyItem))
                RefreshRepairSupplyHash();
        }

        private void CacheRegistryServices()
        {
            RefreshCachedRegistryServices();
            TryRegisterHotSwapListener();
        }

        private void RefreshCachedRegistryServices()
        {
            _cachedPlayerInventoryService = GlobalRegistry.PlayerInventory;
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

        private void RefreshSupplyCrates(bool forceImmediate)
        {
            _discoveredSupplyCount = 0;

            if (!autoDiscoverNearbyStorage && !forceImmediate)
                return;

            int hitCount = BaseLogisticsNetwork.CollectStorageCratesNonAlloc(
                _cachedTransform.position,
                supplySearchRadius,
                _supplyDiscoveryBuffer);

            for (int i = 0; i < hitCount && _discoveredSupplyCount < _discoveredSupplyCrates.Length; i++)
            {
                StorageCrate crate = _supplyDiscoveryBuffer[i];
                _supplyDiscoveryBuffer[i] = null;
                if (crate == null)
                    continue;

                if (ContainsSupplyCrate(crate))
                    continue;

                _discoveredSupplyCrates[_discoveredSupplyCount++] = crate;
            }

            for (int i = hitCount; i < _supplyDiscoveryBuffer.Length; i++)
                _supplyDiscoveryBuffer[i] = null;
        }

        private bool TryResolveSupplyCrate(Collider candidate, out StorageCrate crate)
        {
            crate = null;
            if (candidate == null)
                return false;

            ulong colliderId = ResolveColliderRuntimeId(candidate);
            if (colliderId != 0UL)
            {
                for (int i = 0; i < _supplyCrateLookupCount; i++)
                {
                    if (_supplyCrateLookupColliderIds[i] != colliderId)
                        continue;

                    crate = _supplyCrateLookupCrates[i];
                    if (crate != null)
                        return crate.gameObject.activeInHierarchy;

                    _supplyCrateLookupColliderIds[i] = 0UL;
                    break;
                }
            }

            if (!candidate.TryGetComponent(out crate))
                ConstructionParentLookup.TryCaptureSelfOrParent(candidate, out crate);

            if (colliderId != 0UL && crate != null)
                CacheSupplyCrateLookup(colliderId, crate);

            return crate != null;
        }

        private void CacheSupplyCrateLookup(ulong colliderId, StorageCrate crate)
        {
            if (colliderId == 0UL || crate == null)
                return;

            int slot;
            if (_supplyCrateLookupCount < _supplyCrateLookupColliderIds.Length)
            {
                slot = _supplyCrateLookupCount;
                _supplyCrateLookupCount++;
            }
            else
            {
                slot = _supplyCrateLookupWriteCursor;
            }

            _supplyCrateLookupColliderIds[slot] = colliderId;
            _supplyCrateLookupCrates[slot] = crate;
            _supplyCrateLookupWriteCursor = (_supplyCrateLookupWriteCursor + 1) % _supplyCrateLookupColliderIds.Length;
        }

        private void ClearSupplyLookupCache()
        {
            for (int i = 0; i < _supplyCrateLookupCount; i++)
            {
                _supplyCrateLookupColliderIds[i] = 0UL;
                _supplyCrateLookupCrates[i] = null;
            }

            _supplyCrateLookupCount = 0;
            _supplyCrateLookupWriteCursor = 0;
        }

        private static ulong ResolveColliderRuntimeId(Collider collider)
        {
            return collider != null
                ? EntityId.ToULong(collider.GetEntityId())
                : 0UL;
        }

        private bool ContainsSupplyCrate(StorageCrate crate)
        {
            if (crate == null)
                return false;

            if (supplyCrates != null)
            {
                int explicitCrateCount = Mathf.Min(supplyCrates.Length, MaxMainThreadSupplyScanCount);
                for (int i = 0; i < explicitCrateCount; i++)
                {
                    if (ReferenceEquals(supplyCrates[i], crate))
                        return true;
                }
            }

            for (int i = 0; i < _discoveredSupplyCount; i++)
            {
                if (ReferenceEquals(_discoveredSupplyCrates[i], crate))
                    return true;
            }

            return false;
        }

        private void TryDispatchDrone()
        {
            if (_activeDroneIds == null || _powerNode == null || _powerNode.Grid == null)
                return;

            int freeSlot = FindFreeDroneSlot();
            if (freeSlot < 0)
                return;

            if (!DroneFleetManager.TryAssignFleetTask(this, dispatchIntegrityThreshold, out DroneFleetTask task, out float assignmentScore, out _))
                return;

            PowerGrid grid = _powerNode.Grid;
            if (!task.IsValid() || grid.HasPowerDeficit)
                return;

            int requiredSupplyUnits = ResolveMissionSupplyUnits(in task);
            if (!HasRepairSupplyAvailable(requiredSupplyUnits))
                return;

            Vector3 launchPosition = ResolvedDockSocketPosition;
            if (!DroneFleetManager.TryLaunchHeadlessDrone(
                this,
                in task,
                launchPosition,
                droneRepairRate,
                requiredSupplyUnits,
                out int droneId))
                return;

            if (!TryConsumeRepairSupply(requiredSupplyUnits))
            {
                DroneFleetManager.ReleaseHeadlessDrone(droneId);
                return;
            }

            if (launchBurstPowerCost > 0f)
                grid.ConsumePower(launchBurstPowerCost);

            _activeDroneIds[freeSlot] = droneId;
            _activeTargetIds[freeSlot] = GetModuleRuntimeId(task.Module);
            _debugCurrentTargetId = _activeTargetIds[freeSlot];
            _launchCountTotal++;
#if UNITY_EDITOR
            _debugCurrentTargetName = task.Module != null ? task.Module.name : string.Empty;
#endif
            _debugLastAssignmentScore = assignmentScore;
            _debugLastAssignedSupplyUnits = requiredSupplyUnits;
        }

        private void RegisterHubInstance()
        {
            for (int i = 0; i < s_ActiveHubCount; i++)
            {
                if (ReferenceEquals(s_ActiveHubs[i], this))
                {
                    RefreshRepairTorchAcousticBindingFromActiveHubs();
                    return;
                }
            }

            if (s_ActiveHubCount >= s_ActiveHubs.Length)
                return;

            s_ActiveHubs[s_ActiveHubCount++] = this;
            RefreshRepairTorchAcousticBindingFromActiveHubs();
        }

        private void UnregisterHubInstance()
        {
            for (int i = s_ActiveHubCount - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(s_ActiveHubs[i], this))
                    continue;

                int lastIndex = s_ActiveHubCount - 1;
                for (int shiftIndex = i; shiftIndex < lastIndex; shiftIndex++)
                    s_ActiveHubs[shiftIndex] = s_ActiveHubs[shiftIndex + 1];

                s_ActiveHubs[lastIndex] = null;
                s_ActiveHubCount = lastIndex;
                RefreshRepairTorchAcousticBindingFromActiveHubs();
                return;
            }
        }

        private bool HasRepairSupplyAvailable(int requiredUnits)
        {
            if (requiredUnits <= 0)
                return false;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
            {
                return ResolveAvailableRepairSupplyHashId(grid, requiredUnits) != 0;
            }

            return TryResolveRepairSupplySlot(requiredUnits, consume: false);
        }

        private bool TryConsumeRepairSupply(int requiredUnits)
        {
            return TryConsumeRepairSupplyInternal(requiredUnits, commitViaCommandQueue: false);
        }

        internal bool TryAcquireDroneResupply(int requestedUnits, out int grantedUnits)
        {
            grantedUnits = 0;
            int safeRequestedUnits = Mathf.Max(1, requestedUnits);
            if (!TryConsumeRepairSupplyInternal(safeRequestedUnits, commitViaCommandQueue: false))
                return false;

            grantedUnits = safeRequestedUnits;
            return true;
        }

        internal bool TryQueueDroneResupplyCommit(int requestedUnits, int droneId, out bool committedImmediately, out int queuedReservationId)
        {
            committedImmediately = false;
            queuedReservationId = 0;
            if (droneId <= 0)
                return false;

            int safeRequestedUnits = 1;
            return TryConsumeRepairSupplyInternal(
                safeRequestedUnits,
                commitViaCommandQueue: true,
                requesterId: droneId,
                out committedImmediately,
                out queuedReservationId);
        }

        internal bool TryAttachOrphanedDrone(int droneId)
        {
            if (_activeDroneIds == null || droneId <= 0)
                return false;

            if (FindDroneSlot(droneId) >= 0)
                return true;

            int slot = FindFreeDroneSlot();
            if (slot < 0)
                return false;

            _activeDroneIds[slot] = droneId;
            _activeTargetIds[slot] = 0;
            return true;
        }

        internal bool TryResolveNearestSupplyEndpoint(Vector3 requesterPosition, out Vector3 endpointPosition)
        {
            endpointPosition = DockPosition;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
            {
                int hashId = ResolveAvailableRepairSupplyHashId(grid, 1);
                if (hashId != 0 && BaseLogisticsNetwork.TryResolveNearestSupplyEndpoint(grid, hashId, requesterPosition, out endpointPosition))
                    return true;
            }

            return TryResolveNearestSupplyEndpoint(supplyCrates, supplyCrates != null ? Mathf.Min(supplyCrates.Length, MaxMainThreadSupplyScanCount) : 0, requesterPosition, ref endpointPosition) ||
                   TryResolveNearestSupplyEndpoint(_discoveredSupplyCrates, _discoveredSupplyCount, requesterPosition, ref endpointPosition);
        }

        private bool TryConsumeRepairSupplyInternal(int requiredUnits, bool commitViaCommandQueue)
        {
            return TryConsumeRepairSupplyInternal(requiredUnits, commitViaCommandQueue, 0, out _, out _);
        }

        private bool TryConsumeRepairSupplyInternal(
            int requiredUnits,
            bool commitViaCommandQueue,
            int requesterId,
            out bool committedImmediately,
            out int queuedReservationId)
        {
            committedImmediately = false;
            queuedReservationId = 0;
            if (requiredUnits <= 0)
                return false;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            int hashId = ResolveAvailableRepairSupplyHashId(grid, requiredUnits);
            if (grid != null && hashId != 0)
            {
                _repairSupplyHashIds[0] = hashId;
                _repairSupplyAmounts[0] = requiredUnits;
                if (!BaseLogisticsNetwork.TryReserveResources(grid, _repairSupplyHashIds, _repairSupplyAmounts, 1, out BaseLogisticsNetwork.LogisticsReservation reservation))
                    return false;

                queuedReservationId = reservation.ReservationId;
                if (commitViaCommandQueue)
                {
                    if (!BaseLogisticsNetwork.TryCommitReservedViaCommandQueue(reservation, requesterId, out committedImmediately))
                    {
                        queuedReservationId = 0;
                        return false;
                    }

                    if (committedImmediately)
                        queuedReservationId = 0;
                }
                else
                {
                    BaseLogisticsNetwork.CommitReserved(reservation);
                    committedImmediately = true;
                    queuedReservationId = 0;
                }
                return true;
            }

            if (!TryResolveRepairSupplySlot(requiredUnits, consume: true))
                return false;

            committedImmediately = true;
            return true;
        }

        private bool TryResolveNearestSupplyEndpoint(StorageCrate[] crates, int count, Vector3 requesterPosition, ref Vector3 endpointPosition)
        {
            if (crates == null)
                return false;

            int primaryHashId = ResolvePrimaryRepairSupplyHashId();
            bool found = false;
            float bestDistanceSq = float.MaxValue;
            int crateCount = Mathf.Min(count, MaxMainThreadSupplyScanCount);
            for (int i = 0; i < crateCount; i++)
            {
                StorageCrate crate = crates[i];
                if (!CrateContainsRepairSupply(crate, primaryHashId))
                    continue;

                Vector3 candidatePosition = crate.transform.position;
                float distanceSq = (candidatePosition - requesterPosition).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                endpointPosition = candidatePosition;
                found = true;
            }

            return found;
        }

        private bool TryResolveRepairSupplySlot(int requiredUnits, bool consume)
        {
            if (TryResolveRepairSupplySlot(supplyCrates, supplyCrates != null ? Mathf.Min(supplyCrates.Length, MaxMainThreadSupplyScanCount) : 0, requiredUnits, consume))
                return true;

            return TryResolveRepairSupplySlot(_discoveredSupplyCrates, _discoveredSupplyCount, requiredUnits, consume);
        }

        private bool TryResolveRepairSupplySlot(StorageCrate[] crates, int count, int requiredUnits, bool consume)
        {
            if (crates == null || requiredUnits <= 0)
                return false;

            int primaryHashId = ResolvePrimaryRepairSupplyHashId();
            if (!consume)
                return CountRepairSupplyUnits(crates, count) >= requiredUnits;

            if (CountRepairSupplyUnits(crates, count) < requiredUnits)
                return false;

            int remaining = requiredUnits;
            int crateCount = Mathf.Min(count, MaxMainThreadSupplyScanCount);
            for (int crateIndex = 0; crateIndex < crateCount; crateIndex++)
            {
                StorageCrate crate = crates[crateIndex];
                if (crate == null)
                    continue;

                while (remaining > 0 && TryConsumeRepairSupplyUnit(crate, primaryHashId))
                    remaining--;

                if (remaining <= 0)
                    return true;
            }

            return remaining <= 0;
        }

        private int FindFreeDroneSlot()
        {
            if (_activeDroneIds == null)
                return -1;

            int slotCount = _activeDroneIds.Length;
            for (int i = 0; i < slotCount; i++)
            {
                int droneId = _activeDroneIds[i];
                if (droneId <= 0 || !DroneFleetManager.IsHeadlessDroneActive(droneId))
                    return i;
            }

            return -1;
        }

        private int FindDroneSlot(int droneId)
        {
            if (_activeDroneIds == null || droneId <= 0)
                return -1;

            int slotCount = _activeDroneIds.Length;
            for (int i = 0; i < slotCount; i++)
            {
                if (_activeDroneIds[i] == droneId)
                    return i;
            }

            return -1;
        }

        private void CompactActiveDrones()
        {
            if (_activeDroneIds == null)
                return;

            int slotCount = _activeDroneIds.Length;
            for (int i = 0; i < slotCount; i++)
            {
                int droneId = _activeDroneIds[i];
                if (droneId <= 0 || DroneFleetManager.IsHeadlessDroneActive(droneId))
                    continue;

                ClearDroneSlot(i);
            }
        }

        private void RecallActiveDrones()
        {
            if (_activeDroneIds == null)
                return;

            int slotCount = _activeDroneIds.Length;
            for (int i = 0; i < slotCount; i++)
            {
                int droneId = _activeDroneIds[i];
                if (droneId <= 0)
                    continue;

                DroneFleetManager.AbortHeadlessDrone(droneId);
            }
        }

        private void ReturnAllDronesToPool()
        {
            if (_activeDroneIds == null)
                return;

            int slotCount = _activeDroneIds.Length;
            for (int i = 0; i < slotCount; i++)
            {
                int droneId = _activeDroneIds[i];
                if (droneId > 0)
                    DroneFleetManager.ReleaseHeadlessDrone(droneId);

                ClearDroneSlot(i);
            }
        }

        private void ClearDroneSlot(int slot)
        {
            if (_activeDroneIds == null || slot < 0 || slot >= _activeDroneIds.Length)
                return;

            _activeTargetIds[slot] = 0;
            _activeDroneIds[slot] = 0;
        }

        private static int GetModuleRuntimeId(BaseModule module)
        {
            return module == null
                ? 0
                : unchecked((int)EntityId.ToULong(module.GetEntityId()));
        }

        private Vector3 ResolvedDockSocketPosition => launchPoint != null ? launchPoint.position : _cachedTransform.position;

        private Quaternion ResolvedDockSocketRotation => launchPoint != null ? launchPoint.rotation : _cachedTransform.rotation;

        private BaseAirlock ResolveDockingAirlock()
        {
            if (dockingAirlock != null)
                return dockingAirlock;

            return _cachedDockingAirlock;
        }

        private void CacheDockingAirlockCold()
        {
            if (dockingAirlock != null)
            {
                _cachedDockingAirlock = dockingAirlock;
                return;
            }

            if (_cachedDockingAirlock != null)
                return;

            if (!TryGetComponent(out _cachedDockingAirlock))
                ConstructionParentLookup.TryCaptureSelfOrParent(this, out _cachedDockingAirlock);
        }

        private void RefreshDiagnostics()
        {
            _debugActiveDroneCount = ActiveDroneCountInternal;
            if (_debugActiveDroneCount <= 0)
            {
                _debugCurrentTargetId = 0;
#if UNITY_EDITOR
                _debugCurrentTargetName = string.Empty;
#endif
            }

            _debugSupplyCrateCount = (supplyCrates != null ? supplyCrates.Length : 0) + _discoveredSupplyCount;
        }

        internal int ResolveDockedStasisSlotCount()
        {
            int availableDockSlots = Mathf.Max(0, ActiveSlotCapacity - ActiveDroneCountInternal);
            if (availableDockSlots <= 0)
                return 0;

            return (!_hasPower || !HasRepairSupplyAvailable(1))
                ? availableDockSlots
                : 0;
        }

        private int ActiveDroneCountInternal
        {
            get
            {
                if (_activeDroneIds == null)
                    return 0;

                int activeCount = 0;
                int slotCount = _activeDroneIds.Length;
                for (int i = 0; i < slotCount; i++)
                {
                    int droneId = _activeDroneIds[i];
                    if (droneId > 0 && DroneFleetManager.IsHeadlessDroneActive(droneId))
                        activeCount++;
                }

                return activeCount;
            }
        }

        private int ResolveMissionSupplyUnits(in DroneFleetTask task)
        {
            BaseModule target = task.Module;
            if (target == null)
                return Mathf.Max(1, scrapPerMission);

            if (task.Kind == DroneFleetTaskKind.CutParasite)
                return Mathf.Max(1, scrapPerMission);

            float recoverableIntegrity = Mathf.Max(1f, target.MaxRecoverableIntegrity);
            float missingIntegrity = Mathf.Max(0f, recoverableIntegrity - target.CurrentIntegrity);
            float missingIntegrityPercent = (missingIntegrity / recoverableIntegrity) * 100f;
            int missionUnits = Mathf.Max(1, Mathf.CeilToInt(missingIntegrityPercent * 0.1f));
            if (target.IsFlooded || BaseDegradationSystem.IsModuleRuptured(target))
                missionUnits++;

            return Mathf.Max(scrapPerMission, missionUnits);
        }

        private int ResolveRepairSupplyHashId()
        {
            if (!ReferenceEquals(_cachedRepairSupplyHashItem, repairSupplyItem))
                RefreshRepairSupplyHash();

            return _cachedRepairSupplyHashId;
        }

        private int ResolvePrimaryRepairSupplyHashId()
        {
            int primaryHashId = ResolveRepairSupplyHashId();
            return primaryHashId != 0 ? primaryHashId : DefaultRepairSupplyHashId;
        }

        private void RefreshRepairSupplyHash()
        {
            _cachedRepairSupplyHashItem = repairSupplyItem;
            _cachedRepairSupplyHashId = ItemData.ResolvePersistentHashId(repairSupplyItem);
        }

        private int ResolveAvailableRepairSupplyHashId(PowerGrid grid, int requiredUnits)
        {
            if (grid == null || requiredUnits <= 0)
                return 0;

            int primaryHashId = ResolvePrimaryRepairSupplyHashId();

            if (primaryHashId != 0 && BaseLogisticsNetwork.CountAccessibleItem(grid, primaryHashId) >= requiredUnits)
                return primaryHashId;

            int legacyHashId = LegacyRepairSupplyHashId;
            if (legacyHashId != 0 && legacyHashId != primaryHashId && BaseLogisticsNetwork.CountAccessibleItem(grid, legacyHashId) >= requiredUnits)
                return legacyHashId;

            return 0;
        }

        private int CountRepairSupplyUnits(StorageCrate[] crates, int count)
        {
            if (crates == null)
                return 0;

            int primaryHashId = ResolvePrimaryRepairSupplyHashId();
            int availableUnits = 0;
            int crateCount = Mathf.Min(count, MaxMainThreadSupplyScanCount);
            for (int crateIndex = 0; crateIndex < crateCount; crateIndex++)
            {
                StorageCrate crate = crates[crateIndex];
                availableUnits += CountRepairSupplyUnits(crate, primaryHashId);
            }

            return availableUnits;
        }

        private bool CrateContainsRepairSupply(StorageCrate crate, int primaryHashId)
        {
            return CountRepairSupplyUnits(crate, primaryHashId) > 0;
        }

        private static int CountRepairSupplyUnits(StorageCrate crate, int primaryHashId)
        {
            if (crate == null)
                return 0;

            int count = primaryHashId != 0 ? crate.CountItemByHash(primaryHashId) : 0;
            int legacyHashId = LegacyRepairSupplyHashId;
            if (legacyHashId != 0 && legacyHashId != primaryHashId)
                count += crate.CountItemByHash(legacyHashId);

            return count;
        }

        private static bool TryConsumeRepairSupplyUnit(StorageCrate crate, int primaryHashId)
        {
            if (crate == null)
                return false;

            if (primaryHashId != 0 && crate.TryConsumeItemByHash(primaryHashId))
                return true;

            int legacyHashId = LegacyRepairSupplyHashId;
            return legacyHashId != 0 &&
                   legacyHashId != primaryHashId &&
                   crate.TryConsumeItemByHash(legacyHashId);
        }
    }
}
