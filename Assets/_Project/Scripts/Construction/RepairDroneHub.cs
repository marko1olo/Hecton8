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
        private const string DefaultRepairSupplyItemId = "Data_TitaniumScrap";
        private const int MaxDiscoveredSupplyCrates = 12;
        private const int SupplyOverlapCapacity = 24;
        private const float SupplyRescanInterval = 5f;
        private static readonly HashSet<int> s_ClaimedModuleIds = new HashSet<int>(64);
        private static readonly List<RepairDroneHub> s_ActiveHubs = new List<RepairDroneHub>(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_ClaimedModuleIds.Clear();
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

        // COLD ALLOC: Collider[24] — nearby storage discovery buffer — owner: RepairDroneHub
        private readonly Collider[] _supplyOverlapBuffer = new Collider[SupplyOverlapCapacity];
        // COLD ALLOC: StorageCrate[12] — auto-discovered storage endpoints — owner: RepairDroneHub
        private readonly StorageCrate[] _discoveredSupplyCrates = new StorageCrate[MaxDiscoveredSupplyCrates];

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
        public float PowerRating => -(standbyPowerDraw + ResolveActiveDroneCount() * activeDronePowerDraw);

        /// <summary>Priority used during power shedding.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached grid availability propagated by PowerGrid.UpdateBalance.</summary>
        public bool HasPower => _hasPower;

        internal static List<RepairDroneHub> ActiveHubs => s_ActiveHubs;
        internal int ActiveDroneCount => ResolveActiveDroneCount();
        internal int TotalLaunchCount => _launchCountTotal;

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (!hasPower)
                RecallActiveDrones();
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
        }

        private void TryRegister()
        {
            if (_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register((ISlowTickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister((ISlowTickable)this);
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
                repairSupplyItem = catalog.FindById(DefaultRepairSupplyItemId);
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

            BaseModule target = ResolveNextRepairTarget();
            if (target == null)
                return;

            if (!HasRepairSupplyAvailable())
                return;

            PowerGrid grid = _powerNode.Grid;
            if (grid.HasPowerDeficit || !TryClaimTarget(target))
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                ReleaseClaim(GetModuleRuntimeId(target));
                return;
            }

            Vector3 launchPosition = ResolveLaunchPosition();
            GameObject droneObject = pool.Spawn(dronePrefab, launchPosition, _cachedTransform.rotation, true);
            if (droneObject == null || !droneObject.TryGetComponent(out RepairDroneEntity drone))
            {
                if (droneObject != null)
                    pool.Despawn(droneObject);

                ReleaseClaim(GetModuleRuntimeId(target));
                return;
            }

            if (!TryConsumeRepairSupply())
            {
                pool.Despawn(droneObject);
                ReleaseClaim(GetModuleRuntimeId(target));
                return;
            }

            if (launchBurstPowerCost > 0f)
                grid.ConsumePower(launchBurstPowerCost);

            _activeDrones[freeSlot] = drone;
            _activeTargetIds[freeSlot] = GetModuleRuntimeId(target);
            _launchCountTotal++;
            _debugCurrentTargetName = target.name;
            drone.AssignMission(this, target, launchPosition, droneRepairRate);
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

        private BaseModule ResolveNextRepairTarget()
        {
            ConstructionManager manager = ConstructionManager.Instance;
            IReadOnlyList<GameObject> modules = manager != null ? manager.SpawnedModules : null;
            if (modules == null || modules.Count == 0)
                return null;

            BaseModule bestTarget = null;
            float bestIntegrity = 2f;
            float bestDistanceSqr = float.MaxValue;
            Vector3 hubPosition = _cachedTransform.position;

            int moduleCount = modules.Count;
            for (int i = 0; i < moduleCount; i++)
            {
                GameObject moduleObject = modules[i];
                if (moduleObject == null || !moduleObject.activeInHierarchy || !moduleObject.TryGetComponent(out BaseModule module))
                    continue;

                if (!IsEligibleRepairTarget(module))
                    continue;

                float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
                float integrityNormalized = Mathf.Clamp01(module.CurrentIntegrity / recoverableIntegrity);
                float distanceSqr = (module.transform.position - hubPosition).sqrMagnitude;

                if (integrityNormalized < bestIntegrity ||
                    (Mathf.Abs(integrityNormalized - bestIntegrity) <= 0.001f && distanceSqr < bestDistanceSqr))
                {
                    bestIntegrity = integrityNormalized;
                    bestDistanceSqr = distanceSqr;
                    bestTarget = module;
                }
            }

            return bestTarget;
        }

        private bool IsEligibleRepairTarget(BaseModule module)
        {
            if (module == null)
                return false;

            int moduleId = GetModuleRuntimeId(module);
            if (s_ClaimedModuleIds.Contains(moduleId))
                return false;

            float recoverableIntegrity = Mathf.Max(1f, module.MaxRecoverableIntegrity);
            bool belowThreshold = module.CurrentIntegrity / recoverableIntegrity < dispatchIntegrityThreshold;
            if (!belowThreshold && !module.IsFlooded && !module.HasCascadeFailure)
                return false;

            if (module.CurrentIntegrity >= recoverableIntegrity && !module.IsFlooded)
                return false;

            if (_powerNode != null &&
                module.TryGetComponent(out PowerNode modulePowerNode) &&
                modulePowerNode.Grid != null &&
                _powerNode.Grid != null &&
                !ReferenceEquals(modulePowerNode.Grid, _powerNode.Grid))
            {
                return false;
            }

            return true;
        }

        private bool HasRepairSupplyAvailable()
        {
            if (repairSupplyItem == null)
                return false;

            return TryResolveRepairSupplySlot(consume: false);
        }

        private bool TryConsumeRepairSupply()
        {
            if (repairSupplyItem == null)
                return false;

            for (int i = 0; i < scrapPerMission; i++)
            {
                if (!TryResolveRepairSupplySlot(consume: true))
                    return false;
            }

            return true;
        }

        private bool TryResolveRepairSupplySlot(bool consume)
        {
            if (TryResolveRepairSupplySlot(supplyCrates, supplyCrates != null ? supplyCrates.Length : 0, consume))
                return true;

            return TryResolveRepairSupplySlot(_discoveredSupplyCrates, _discoveredSupplyCount, consume);
        }

        private bool TryResolveRepairSupplySlot(StorageCrate[] crates, int count, bool consume)
        {
            if (crates == null || repairSupplyItem == null)
                return false;

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

                    return true;
                }
            }

            return false;
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

            ReleaseClaim(_activeTargetIds[slot]);
            _activeTargetIds[slot] = 0;
            _activeDrones[slot] = null;
        }

        private bool TryClaimTarget(BaseModule module)
        {
            if (module == null)
                return false;

            int instanceId = GetModuleRuntimeId(module);
            return s_ClaimedModuleIds.Add(instanceId);
        }

        private void ReleaseClaim(int instanceId)
        {
            if (instanceId == 0)
                return;

            s_ClaimedModuleIds.Remove(instanceId);
        }

        private static int GetModuleRuntimeId(BaseModule module)
        {
            return module == null
                ? 0
                : unchecked((int)EntityId.ToULong(module.GetEntityId()));
        }

        private Vector3 ResolveLaunchPosition()
        {
            return launchPoint != null ? launchPoint.position : _cachedTransform.position;
        }

        private void RefreshDiagnostics()
        {
            _debugActiveDroneCount = ResolveActiveDroneCount();
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

        private int ResolveActiveDroneCount()
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
    }
}
