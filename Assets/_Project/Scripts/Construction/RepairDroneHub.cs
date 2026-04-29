using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Autonomous repair dispatch hub that consumes power and scrap to launch pooled drones toward damaged base modules.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton8/Construction/Repair Drone Hub")]
    public sealed class RepairDroneHub : MonoBehaviour, ISlowTickable, IPoolable, IPowerComponent
    {
        private const string DefaultRepairSupplyItemId = "Nanite_Solder";
        private const string LegacyRepairSupplyItemId = "Data_TitaniumScrap";
        private const int MaxDiscoveredSupplyCrates = 12;
        private const int SupplyOverlapCapacity = 24;
        private const float SupplyRescanInterval = 5f;
        private static readonly List<RepairDroneHub> s_ActiveHubs = new List<RepairDroneHub>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ActiveHubs.Clear();
        }

        [Header("── Drone Bay ──────────────────────────")]
        [Tooltip("Pooled drone prefab launched by this hub.")]
        [SerializeField] private GameObject dronePrefab;

        [Tooltip("Optional launch socket. Falls back to this transform when omitted.")]
        [SerializeField] private Transform launchPoint;

        [Tooltip("Maximum number of simultaneous drone sorties this hub can sustain.")]
        [SerializeField, Range(1, 4)] private int maxConcurrentDrones = 1;

        [Tooltip("Integrity threshold below which the hub dispatches repairs. Uses current recoverable integrity as the reference ceiling.")]
        [SerializeField, Range(0.1f, 1f)] private float dispatchIntegrityThreshold = 0.8f;

        [Tooltip("Repair throughput passed into each drone mission.")]
        [SerializeField, Range(1f, 100f)] private float droneRepairRate = 18f;

        [Header("── Supply Chain ──────────────────────")]
        [Tooltip("Optional explicit storage crates that can feed this hub with repair scrap.")]
        [SerializeField] private StorageCrate[] supplyCrates;

        [Tooltip("Auto-discovers nearby StorageCrate endpoints when explicit references are not authored.")]
        [SerializeField] private bool autoDiscoverNearbyStorage = true;

        [Tooltip("World radius used when auto-discovering nearby supply crates.")]
        [SerializeField, Range(1f, 40f)] private float supplySearchRadius = 18f;

        [Tooltip("Layer mask used while probing for nearby storage crates.")]
        [SerializeField] private LayerMask supplySearchMask = ~0;

        [Tooltip("Override repair supply item. Falls back to Data_TitaniumScrap through the active ItemCatalog when left empty.")]
        [SerializeField] private ItemData repairSupplyItem;

        [Tooltip("How many scrap units are removed from storage for each sortie.")]
        [SerializeField, Range(1, 8)] private int scrapPerMission = 1;

        [Header("── Power Budget ──────────────────────")]
        [Tooltip("Baseline power draw of the powered drone bay.")]
        [SerializeField, Range(0f, 50f)] private float standbyPowerDraw = 2f;

        [Tooltip("Additional continuous power draw per drone while it is servicing a module.")]
        [SerializeField, Range(0f, 50f)] private float activeDronePowerDraw = 6f;

        [Tooltip("Immediate one-shot grid cost consumed when a drone launches from the bay.")]
        [SerializeField, Range(0f, 50f)] private float launchBurstPowerCost = 4f;

        [Tooltip("Priority used when the grid starts shedding non-critical loads. Lower is more critical.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 25;

        [Header("── Diagnostics ───────────────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private int _debugActiveDroneCount;
        [SerializeField] private int _debugSupplyCrateCount;
        [SerializeField] private string _debugCurrentTargetName;
        [SerializeField] private float _debugLastAssignmentScore;
        [SerializeField] private int _debugLastAssignedSupplyUnits;

        // COLD ALLOC: Collider[24] — nearby storage discovery buffer — owner: RepairDroneHub
        private readonly Collider[] _supplyOverlapBuffer = new Collider[SupplyOverlapCapacity];
        // COLD ALLOC: StorageCrate[12] — auto-discovered storage endpoints — owner: RepairDroneHub
        private readonly StorageCrate[] _discoveredSupplyCrates = new StorageCrate[MaxDiscoveredSupplyCrates];
        // COLD ALLOC: int[1] — repair-supply hash bridge for logistics reservations — owner: RepairDroneHub
        private readonly int[] _repairSupplyHashIds = new int[1];
        // COLD ALLOC: int[1] — repair-supply quantity bridge for logistics reservations — owner: RepairDroneHub
        private readonly int[] _repairSupplyAmounts = new int[1];

        private Transform _cachedTransform;
        private PowerNode _powerNode;
        private RepairDroneEntity[] _activeDrones;
        private int[] _activeTargetIds;
        private bool _registered;
        private bool _hasPower = true;
        private float _supplyRescanTimer;
        private int _discoveredSupplyCount;
        private int _launchCountTotal;

        /// <summary>Hub power draw scales with the number of active sorties.</summary>
        public float PowerRating => -(standbyPowerDraw + CountActiveDronesInternal() * activeDronePowerDraw);

        /// <summary>Priority used during power shedding.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached grid availability propagated by PowerGrid.UpdateBalance.</summary>
        public bool HasPower => _hasPower;

        internal static List<RepairDroneHub> ActiveHubs => s_ActiveHubs;
        internal int ActiveDroneCount => CountActiveDronesInternal();
        internal int TotalLaunchCount => _launchCountTotal;
        internal Vector3 DockPosition => ResolveDockSocketPositionInternal();
        internal Quaternion DockRotation => ResolveDockSocketRotationInternal();
        internal PowerGrid CurrentGrid => _powerNode != null ? _powerNode.Grid : null;
        internal int ActiveSlotCapacity => _activeDrones != null ? _activeDrones.Length : Mathf.Max(1, maxConcurrentDrones);
        internal bool HasOperationalPower => _hasPower && _powerNode != null && _powerNode.Grid != null;

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower)
                RecallActiveDrones();

            DroneFleetManager.NotifyFleetStateChanged();
        }

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _powerNode);

            int capacity = Mathf.Max(1, maxConcurrentDrones);
            _activeDrones = new RepairDroneEntity[capacity]; // COLD ALLOC: RepairDroneEntity[capacity] — active drone slots — owner: RepairDroneHub
            _activeTargetIds = new int[capacity]; // COLD ALLOC: int[capacity] — claimed target ids by slot — owner: RepairDroneHub
            ResolveRepairSupplyItem();
        }

        private void OnEnable()
        {
            RegisterHubInstance();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            UnregisterHubInstance();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            WarmupDrones();
            ResolveRepairSupplyItem();
            RefreshSupplyCrates(true);
            RegisterHubInstance();
            TryRegister();
            DroneFleetManager.NotifyFleetStateChanged();
        }

        public void OnDespawn()
        {
            ReturnAllDronesToPool();
            TryUnregister();
            _hasPower = true;
            _debugHasPower = true;
            _debugCurrentTargetName = string.Empty;
            _debugActiveDroneCount = 0;
            UnregisterHubInstance();
            DroneFleetManager.NotifyFleetStateChanged();
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
            DroneFleetManager.NotifyFleetStateChanged();
        }

        /// <summary>Called by pooled drones once they have returned to the hub and are ready to despawn.</summary>
        public void NotifyDroneReturned(RepairDroneEntity drone)
        {
            if (drone == null || _activeDrones == null)
                return;

            int slot = FindDroneSlot(drone);
            if (slot < 0)
                return;

            ClearDroneSlot(slot);
            RefreshDiagnostics();
            DroneFleetManager.NotifyFleetStateChanged();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void WarmupDrones()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null || dronePrefab == null || _activeDrones == null)
                return;

            pool.Warmup(dronePrefab, _activeDrones.Length);
        }

        private void ResolveRepairSupplyItem()
        {
            if (repairSupplyItem != null)
                return;

            PlayerInventory inventory = PlayerInventory.Instance;
            ItemCatalog catalog = inventory != null ? inventory.ItemCatalog : null;
            if (catalog != null)
            {
                repairSupplyItem = catalog.FindById(DefaultRepairSupplyItemId);
                if (repairSupplyItem == null)
                    repairSupplyItem = catalog.FindById(LegacyRepairSupplyItemId);
            }
        }

        private void RefreshSupplyCrates(bool forceImmediate)
        {
            _discoveredSupplyCount = 0;

            if (!autoDiscoverNearbyStorage && !forceImmediate)
                return;

            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                _cachedTransform.position,
                supplySearchRadius,
                _supplyOverlapBuffer,
                supplySearchMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount && _discoveredSupplyCount < _discoveredSupplyCrates.Length; i++)
            {
                Collider candidate = _supplyOverlapBuffer[i];
                if (candidate == null)
                    continue;

                StorageCrate crate = candidate.GetComponent<StorageCrate>() ?? candidate.GetComponentInParent<StorageCrate>();
                if (crate == null || ContainsSupplyCrate(crate))
                    continue;

                _discoveredSupplyCrates[_discoveredSupplyCount++] = crate;
            }

            for (int i = hitCount; i < _supplyOverlapBuffer.Length; i++)
                _supplyOverlapBuffer[i] = null;
        }

        private bool ContainsSupplyCrate(StorageCrate crate)
        {
            if (crate == null)
                return false;

            if (supplyCrates != null)
            {
                for (int i = 0; i < supplyCrates.Length; i++)
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
            if (dronePrefab == null || _activeDrones == null || _powerNode == null || _powerNode.Grid == null)
                return;

            int freeSlot = FindFreeDroneSlot();
            if (freeSlot < 0)
                return;

            if (!DroneFleetManager.TryAssignRepairTask(this, dispatchIntegrityThreshold, out BaseModule target, out float assignmentScore, out _))
                return;

            PowerGrid grid = _powerNode.Grid;
            if (target == null || grid.HasPowerDeficit)
                return;

            int requiredSupplyUnits = ResolveMissionSupplyUnits(target);
            if (!HasRepairSupplyAvailable(requiredSupplyUnits))
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            Vector3 launchPosition = ResolveDockSocketPositionInternal();
            GameObject droneObject = pool.Spawn(dronePrefab, launchPosition, _cachedTransform.rotation, true);
            if (droneObject == null || !droneObject.TryGetComponent(out RepairDroneEntity drone))
            {
                if (droneObject != null)
                    pool.Despawn(droneObject);
                return;
            }

            if (!TryConsumeRepairSupply(requiredSupplyUnits))
            {
                pool.Despawn(droneObject);
                return;
            }

            if (launchBurstPowerCost > 0f)
                grid.ConsumePower(launchBurstPowerCost);

            _activeDrones[freeSlot] = drone;
            _activeTargetIds[freeSlot] = GetModuleRuntimeId(target);
            _launchCountTotal++;
            _debugCurrentTargetName = target.name;
            _debugLastAssignmentScore = assignmentScore;
            _debugLastAssignedSupplyUnits = requiredSupplyUnits;
            drone.AssignMission(this, target, launchPosition, droneRepairRate, requiredSupplyUnits);
        }

        private void RegisterHubInstance()
        {
            for (int i = 0; i < s_ActiveHubs.Count; i++)
            {
                if (ReferenceEquals(s_ActiveHubs[i], this))
                    return;
            }

            s_ActiveHubs.Add(this);
        }

        private void UnregisterHubInstance()
        {
            for (int i = s_ActiveHubs.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_ActiveHubs[i], this))
                    s_ActiveHubs.RemoveAt(i);
            }
        }

        private bool HasRepairSupplyAvailable(int requiredUnits)
        {
            if (repairSupplyItem == null || requiredUnits <= 0)
                return false;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
            {
                int hashId = ResolveRepairSupplyHashId();
                return hashId != 0 && BaseLogisticsNetwork.CountAccessibleItem(grid, hashId) >= requiredUnits;
            }

            return TryResolveRepairSupplySlot(requiredUnits, consume: false);
        }

        private bool TryConsumeRepairSupply(int requiredUnits)
        {
            if (repairSupplyItem == null || requiredUnits <= 0)
                return false;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            int hashId = ResolveRepairSupplyHashId();
            if (grid != null && hashId != 0)
            {
                _repairSupplyHashIds[0] = hashId;
                _repairSupplyAmounts[0] = requiredUnits;
                if (!BaseLogisticsNetwork.TryReserveResources(grid, _repairSupplyHashIds, _repairSupplyAmounts, 1, out BaseLogisticsNetwork.LogisticsReservation reservation))
                    return false;

                BaseLogisticsNetwork.CommitReserved(reservation);
                return true;
            }

            return TryResolveRepairSupplySlot(requiredUnits, consume: true);
        }

        private bool TryResolveRepairSupplySlot(int requiredUnits, bool consume)
        {
            if (TryResolveRepairSupplySlot(supplyCrates, supplyCrates != null ? supplyCrates.Length : 0, requiredUnits, consume))
                return true;

            return TryResolveRepairSupplySlot(_discoveredSupplyCrates, _discoveredSupplyCount, requiredUnits, consume);
        }

        private bool TryResolveRepairSupplySlot(StorageCrate[] crates, int count, int requiredUnits, bool consume)
        {
            if (crates == null || repairSupplyItem == null)
                return false;

            if (consume && CountRepairSupplyUnits(crates, count) < requiredUnits)
                return false;

            int remaining = requiredUnits;
            for (int crateIndex = 0; crateIndex < count; crateIndex++)
            {
                StorageCrate crate = crates[crateIndex];
                if (crate == null)
                    continue;

                ItemData[] entries = crate.ContainedItems;
                if (entries == null)
                    continue;

                int entryCount = entries.Length;
                for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
                {
                    if (!ReferenceEquals(entries[entryIndex], repairSupplyItem))
                        continue;

                    if (consume)
                        entries[entryIndex] = null;

                    remaining--;
                    if (remaining <= 0)
                        return true;
                }
            }

            return remaining <= 0;
        }

        private int FindFreeDroneSlot()
        {
            if (_activeDrones == null)
                return -1;

            int slotCount = _activeDrones.Length;
            for (int i = 0; i < slotCount; i++)
            {
                RepairDroneEntity drone = _activeDrones[i];
                if (drone == null || !drone.gameObject.activeInHierarchy)
                    return i;
            }

            return -1;
        }

        private int FindDroneSlot(RepairDroneEntity drone)
        {
            if (_activeDrones == null || drone == null)
                return -1;

            int slotCount = _activeDrones.Length;
            for (int i = 0; i < slotCount; i++)
            {
                if (ReferenceEquals(_activeDrones[i], drone))
                    return i;
            }

            return -1;
        }

        private void CompactActiveDrones()
        {
            if (_activeDrones == null)
                return;

            int slotCount = _activeDrones.Length;
            for (int i = 0; i < slotCount; i++)
            {
                RepairDroneEntity drone = _activeDrones[i];
                if (drone == null || drone.gameObject.activeInHierarchy)
                    continue;

                ClearDroneSlot(i);
            }
        }

        private void RecallActiveDrones()
        {
            if (_activeDrones == null)
                return;

            int slotCount = _activeDrones.Length;
            for (int i = 0; i < slotCount; i++)
            {
                RepairDroneEntity drone = _activeDrones[i];
                if (drone == null)
                    continue;

                drone.AbortMission();
            }
        }

        private void ReturnAllDronesToPool()
        {
            if (_activeDrones == null)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            int slotCount = _activeDrones.Length;
            for (int i = 0; i < slotCount; i++)
            {
                RepairDroneEntity drone = _activeDrones[i];
                if (drone != null && drone.gameObject.activeInHierarchy && pool != null)
                    pool.Despawn(drone.gameObject);

                ClearDroneSlot(i);
            }
        }

        private void ClearDroneSlot(int slot)
        {
            if (_activeDrones == null || slot < 0 || slot >= _activeDrones.Length)
                return;

            _activeTargetIds[slot] = 0;
            _activeDrones[slot] = null;
        }

        private static int GetModuleRuntimeId(BaseModule module)
        {
            return module == null
                ? 0
                : unchecked((int)EntityId.ToULong(module.GetEntityId()));
        }

        private Vector3 ResolveDockSocketPositionInternal()
        {
            return launchPoint != null ? launchPoint.position : _cachedTransform.position;
        }

        private Quaternion ResolveDockSocketRotationInternal()
        {
            return launchPoint != null ? launchPoint.rotation : _cachedTransform.rotation;
        }

        private void RefreshDiagnostics()
        {
            _debugActiveDroneCount = CountActiveDronesInternal();
            _debugCurrentTargetName = string.Empty;

            if (_activeDrones != null)
            {
                int slotCount = _activeDrones.Length;
                for (int i = 0; i < slotCount; i++)
                {
                    RepairDroneEntity drone = _activeDrones[i];
                    if (drone == null || !drone.gameObject.activeInHierarchy)
                        continue;

                    if (string.IsNullOrEmpty(_debugCurrentTargetName) && drone.CurrentTarget != null)
                        _debugCurrentTargetName = drone.CurrentTarget.name;
                }
            }

            _debugSupplyCrateCount = (supplyCrates != null ? supplyCrates.Length : 0) + _discoveredSupplyCount;
        }

        internal int ResolveDockedStasisSlotCount()
        {
            int availableDockSlots = Mathf.Max(0, ActiveSlotCapacity - CountActiveDronesInternal());
            if (availableDockSlots <= 0)
                return 0;

            return (!_hasPower || !HasRepairSupplyAvailable(1))
                ? availableDockSlots
                : 0;
        }

        private int CountActiveDronesInternal()
        {
            if (_activeDrones == null)
                return 0;

            int activeCount = 0;
            int slotCount = _activeDrones.Length;
            for (int i = 0; i < slotCount; i++)
            {
                RepairDroneEntity drone = _activeDrones[i];
                if (drone != null && drone.gameObject.activeInHierarchy)
                    activeCount++;
            }

            return activeCount;
        }

        private int ResolveMissionSupplyUnits(BaseModule target)
        {
            if (target == null)
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
            return repairSupplyItem != null && !string.IsNullOrWhiteSpace(repairSupplyItem.PersistentId)
                ? Hecton.Localization.LocHash.Compute(repairSupplyItem.PersistentId)
                : 0;
        }

        private int CountRepairSupplyUnits(StorageCrate[] crates, int count)
        {
            if (crates == null || repairSupplyItem == null)
                return 0;

            int availableUnits = 0;
            for (int crateIndex = 0; crateIndex < count; crateIndex++)
            {
                StorageCrate crate = crates[crateIndex];
                ItemData[] entries = crate != null ? crate.ContainedItems : null;
                if (entries == null)
                    continue;

                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    if (ReferenceEquals(entries[entryIndex], repairSupplyItem))
                        availableUnits++;
                }
            }

            return availableUnits;
        }
    }
}
