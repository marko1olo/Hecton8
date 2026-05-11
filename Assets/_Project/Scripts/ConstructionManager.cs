// ============================================================================
// HECTON-8 - ConstructionManager.cs
// Runtime owner for placed base modules.
//
// GlobalRegistry service, ISaveable priority 90.
//
// Owns the registry of built modules. Save writes prefab ID, transform, and
// dynamic module state. Load removes old modules through the pool and respawns
// saved modules with restored state.
//
// Runtime zero-GC contract:
// - Register/Unregister: O(1) duplicate check, no LINQ.
// - List<GameObject> is preallocated with explicit capacity.
// - Swap-remove handles O(1) removal.
// - PopulateSaveData uses for-loops and TryGetComponent.
//
// Integration:
// - PlayerBuilder calls RegisterModule() after successful placement.
// - LoadFromSaveData calls ClearAllModules() before respawn.
// - ObjectPoolManager owns Spawn/Despawn for modules.
// - BaseModule integrity and flood state are persisted here.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class ConstructionManager : MonoBehaviour, IUpdatable, ILateFrameTickable, ISaveable, ISlowTickable, ILogisticsService, IGlobalRegistryHotSwapListener, IServiceHeartbeat, IServiceShutdown
    {
        private const float SlowTickDeltaTime = 0.5f;

        internal static ConstructionManager ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }
        // SERVICE STATE

        // INSPECTOR

        [Header("Catalog")]
        [Tooltip("Catalog of buildable base modules. Used to resolve prefabs by ID during load.")]
        [SerializeField] private ModuleCatalog catalog;

        [Header("Settings")]
        [Tooltip("Initial capacity for the placed-module registry. Increase for larger bases.")]
        [SerializeField] private int initialCapacity = 64;

        [Header("Ambient Accidents")]
        [Tooltip("Allows rare cold-path service accidents on already placed base modules.")]
        [SerializeField] private bool enableAmbientAccidents = true;
        [Tooltip("Interval between cold-path checks for ambient service accidents.")]
        [SerializeField] private float ambientAccidentCheckInterval = 90f;
        [Tooltip("Base accident chance per cold-path check. Final chance is multiplied by candidate risk score.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentBaseChance = 0.25f;
        [Tooltip("Minimum risk score required for a module to qualify as an accident candidate.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentMinRisk = 0.2f;
        [Tooltip("Integrity threshold below which a module is considered worn for the accident scheduler.")]
        [SerializeField, Range(0f, 1f)] private float ambientAccidentIntegrityThreshold = 0.8f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugModuleCount;
        [Tooltip("Runtime timer until the next ambient accident evaluation.")]
        [SerializeField] private float _debugAmbientAccidentTimer;

        // REGISTRY

        /// <summary>
        /// Registry of all placed modules. Preallocated and swap-removed for O(1) removal.
        /// </summary>
        private List<GameObject> _spawnedModules;
        private List<BaseModule> _spawnedBaseModules;
        private HabitatGraphManager _habitatGraphManager;
        private bool _tickRegistered;
        private bool _lateFrameTickRegistered;
        private bool _logisticsServiceRegistered;
        private bool _hotSwapListenerRegistered;
        private ISaveService _registeredSaveService;
        private bool _isInitialized;
        private bool _habitatGraphDirty;
        private float _slowTickAccumulator;
        private float _ambientAccidentTimer;
        private int _ambientAccidentCursor;

        // CONSTANTS - DEFAULT MODULE STATE

        /// <summary>
        /// Default integrity for modules without BaseModule and for old save migration.
        /// </summary>
        private const float DefaultIntegrity = 100f;

        /// <summary>Default flood state.</summary>
        private const bool  DefaultIsFlooded = false;

        // PUBLIC API - QUERIES

        /// <summary>Number of placed modules.</summary>
        public int ModuleCount => _spawnedModules != null ? _spawnedModules.Count : 0;

        /// <summary>Read-only access to placed modules for UI and minimap consumers.</summary>
        public IReadOnlyList<GameObject> SpawnedModules => _spawnedModules;

        /// <summary>Cached BaseModule count for hot-path gameplay systems that must not scan components.</summary>
        internal int SpawnedBaseModuleCount => _spawnedBaseModules != null ? _spawnedBaseModules.Count : 0;

        /// <summary>Indexed cached BaseModule access for hot-path gameplay systems that must not scan components.</summary>
        internal BaseModule GetSpawnedBaseModuleAt(int index)
        {
            return _spawnedBaseModules != null && index >= 0 && index < _spawnedBaseModules.Count
                ? _spawnedBaseModules[index]
                : null;
        }

        /// <summary>Read-only access to the module catalog for build tools and UI.</summary>
        public ModuleCatalog Catalog => catalog;

        /// <summary>
        /// True once the logistics owner is registered in the global registry.
        /// </summary>
        public bool IsInitialized => _isInitialized && ReferenceEquals(GlobalRegistry.Logistics, this);

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => IsInitialized;

        /// <summary>
        /// Registers the construction/logistics service with bootstrap-owned runtime systems.
        /// </summary>
        public void InitializeService()
        {
            EnsureRuntimeStorage();
            _isInitialized = true;
            TryRegisterLogisticsService();
            TryRegisterTick();
            TryRegisterLateFrameTick();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
        }

        // -----------------------------------------------------------------------------
        //  LIFECYCLE
        // -----------------------------------------------------------------------------

        private void Awake()
        {
            // Service storage.
            // Pre-allocate runtime lists.
            EnsureRuntimeStorage();
            _ambientAccidentTimer = 0f;
        }

        private void EnsureRuntimeStorage()
        {
            int capacity = Mathf.Max(1, initialCapacity);
            if (_spawnedModules == null)
                _spawnedModules = new List<GameObject>(capacity); // COLD ALLOC: List<GameObject>[initialCapacity] - construction module registry - owner: ConstructionManager

            if (_spawnedBaseModules == null)
                _spawnedBaseModules = new List<BaseModule>(capacity); // COLD ALLOC: List<BaseModule>[initialCapacity] - cached BaseModule registry for hot-path construction consumers - owner: ConstructionManager

            if (_habitatGraphManager == null)
                _habitatGraphManager = new HabitatGraphManager(capacity); // COLD ALLOC: HabitatGraphManager[1] - persistent placed-module CSR adjacency owner - owner: ConstructionManager
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            EnsureRuntimeStorage();
            _slowTickAccumulator = 0f;
            if (!_isInitialized)
                return;

            TryRegisterLogisticsService();
            TryRegisterTick();
            TryRegisterLateFrameTick();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
        }

        private void Start()
        {
            if (!_isInitialized)
                return;

            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
        }

        private void OnDisable()
        {
            UnregisterRuntimeHooks();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            UnregisterRuntimeHooks();
            _isInitialized = false;
            _spawnedModules?.Clear();
            _spawnedBaseModules?.Clear();
            if (_habitatGraphManager != null)
            {
                _habitatGraphManager.Dispose();
                _habitatGraphManager = null;
            }
        }

        private void UnregisterRuntimeHooks()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterTick();
            TryUnregisterLateFrameTick();
            TryUnregisterLogisticsService();
            _slowTickAccumulator = 0f;
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < SlowTickDeltaTime)
                return;

            _slowTickAccumulator -= SlowTickDeltaTime;
            if (_slowTickAccumulator > SlowTickDeltaTime)
                _slowTickAccumulator = SlowTickDeltaTime;

            SlowTick();
        }

        public void LateFrameTick()
        {
            if (!_habitatGraphDirty)
                return;

            RefreshHabitatGraph();
        }

        public void SlowTick()
        {
            if (_habitatGraphManager != null)
                _habitatGraphManager.ApplyHydrodynamicStress(SlowTickDeltaTime);

            if (!enableAmbientAccidents || ambientAccidentCheckInterval <= 0f)
                return;

            _ambientAccidentTimer += SlowTickDeltaTime;
            _debugAmbientAccidentTimer = _ambientAccidentTimer;

            if (_ambientAccidentTimer < ambientAccidentCheckInterval)
                return;

            _ambientAccidentTimer = 0f;
            _debugAmbientAccidentTimer = 0f;

            TryTriggerAmbientAccident();
        }

        // PUBLIC API: REGISTER / UNREGISTER

        /// <summary>
        /// Registers a placed module in the runtime construction registry.
        /// Adds module state to the cached registry and ignores duplicate references.
        /// </summary>
        /// <param name="module">Placed module GameObject.</param>
        public void RegisterModule(GameObject module)
        {
            if (module == null) return;

            // Guard: duplicate module reference.
            if (ContainsRef(module)) return;

            // Add to runtime registry.
            _spawnedModules.Add(module);
            if (module.TryGetComponent(out BaseModule baseModule) && !ContainsBaseModuleRef(baseModule))
                _spawnedBaseModules.Add(baseModule);

            RefreshHabitatGraph();
            if (module.TryGetComponent(out BaseModuleNavModifier navModifier))
                navModifier.RefreshVegetationExclusion();

            UpdateDiagnostics();
        }

        /// <summary>
        /// Registers a module and binds it to BuildableData.
        /// Automatically configures ModuleMarker.
        ///
        /// Preferred method: guarantees the marker exists.
        /// </summary>
        /// <param name="module">Final module GameObject.</param>
        /// <param name="data">BuildableData used for binding.</param>
        public void RegisterModule(GameObject module, BuildableData data)
        {
            if (module == null) return;

            // Ensure ModuleMarker exists.
            if (!module.TryGetComponent(out ModuleMarker marker))
            {
                marker = module.AddComponent<ModuleMarker>();
            }

            // Initialize marker when build data is present.
            if (data != null)
                marker.Initialize(data);

            if (data != null && module.TryGetComponent(out BaseModule baseModule))
                baseModule.ApplyBuildableTemplate(data);

            RegisterModule(module);
        }

        /// <summary>
        /// Removes the module from the registry. Does not despawn it.
        /// Use for deconstruction flows: Unregister + Pool.Despawn.
        ///
        /// Swap-remove: O(1).
        /// </summary>
        public void UnregisterModule(GameObject module)
        {
            if (module == null) return;

            SwapRemove(module);
            RemoveBaseModule(module);
            RefreshHabitatGraph();

            UpdateDiagnostics();
        }

        /// <summary>
        /// Removes the module from the registry and despawns it through the pool.
        /// Used by module deconstruction.
        /// </summary>
        public void DestroyModule(GameObject module)
        {
            if (module == null) return;

            UnregisterModule(module);

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            DespawnOrDestroyModuleInstance(module, pool);
        }

        /// <summary>
        /// Inserts a temporary external bypass cable between two placed habitat modules and rebuilds the runtime graph.
        /// </summary>
        public bool TryCreateTemporaryBypass(BaseModule sourceModule, BaseModule destinationModule)
        {
            return TryCreateTemporaryBypass(
                sourceModule,
                destinationModule,
                ResolveModuleHashId(sourceModule),
                ResolveModuleHashId(destinationModule));
        }

        /// <summary>
        /// Inserts a temporary external bypass cable between two placed habitat modules using captured module content hashes.
        /// </summary>
        public bool TryCreateTemporaryBypass(
            BaseModule sourceModule,
            BaseModule destinationModule,
            int sourceModuleHashId,
            int destinationModuleHashId)
        {
            if (_habitatGraphManager == null || sourceModule == null || destinationModule == null)
                return false;

            if (!_habitatGraphManager.TryAddTemporaryBypass(
                    sourceModule.gameObject,
                    destinationModule.gameObject,
                    sourceModuleHashId,
                    destinationModuleHashId,
                    out bool injectedDirectly))
            {
                return false;
            }

            if (!injectedDirectly)
                RefreshHabitatGraph();

            return true;
        }

        private static int ResolveModuleHashId(BaseModule module)
        {
            if (module != null &&
                module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                return marker.Data.ModuleHashId;
            }

            return 0;
        }

        // -----------------------------------------------------------------------------
        //  PUBLIC API - CLEAR ALL
        // -----------------------------------------------------------------------------

        /// <summary>
        /// Despawns all modules through the pool and clears the registry.
        ///
        /// Called by:
        ///   - LoadFromSaveData() before respawning save contents.
        ///   - New Game when the world must start empty.
        ///
        /// Iterates backwards so Despawn-triggered OnDisable callbacks cannot invalidate
        /// the active loop.
        /// </summary>
        public void ClearAllModules()
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;

            // Iterate backwards while the list can be modified by despawn callbacks.
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                GameObject module = _spawnedModules[i];

                if (module == null) continue; // already destroyed

                DespawnOrDestroyModuleInstance(module, pool);
            }

            _spawnedModules.Clear();
            _spawnedBaseModules.Clear();
            RefreshHabitatGraph();

            UpdateDiagnostics();
        }

        // -----------------------------------------------------------------------------
        //  ISaveable - SAVE / LOAD (Priority 90)
        // -----------------------------------------------------------------------------

        /// <summary>Construction loads last because it depends on the world state.</summary>
        public int SavePriority => 90;
        public int LoadPriority => 90;

        /// <summary>
        /// Writes all placed modules into ConstructionDTO.
        ///
        /// For each module:
        ///   1. Resolve ModuleMarker -> PrefabId.
        ///   2. Read transform position and rotation.
        ///   3. Read BaseModule dynamic state when present.
        ///   4. Write the result into dto.modules[].
        ///
        /// Modules without ModuleMarker are skipped with a warning.
        /// Modules without BaseModule are saved with default state values
        /// for passive supports and decoration.
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            ref ConstructionDTO dto = ref data.construction;
            dto.EnsureCapacity();
            dto.graphNodeCount = 0;
            dto.graphEdgeCount = 0;

            int moduleIndex = 0;
            int count = _spawnedModules.Count;

            for (int i = 0; i < count; i++)
            {
                GameObject module = _spawnedModules[i];

                // Guard: destroyed reference.
                if (module == null) continue;

                // Guard: missing marker.
                if (!module.TryGetComponent(out ModuleMarker marker))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has no ModuleMarker. " +
                        "Skipping save for this module.");
#endif
                    continue;
                }

                // Guard: empty ID.
                string prefabId = marker.PrefabId;
                if (string.IsNullOrEmpty(prefabId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has empty PrefabId. " +
                        "Skipping.");
#endif
                    continue;
                }

                // Guard: save capacity.
                if (moduleIndex >= ConstructionDTO.MaxModules)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Max modules ({ConstructionDTO.MaxModules}) reached. " +
                        $"Truncating save: {count - moduleIndex} modules not saved.");
#endif
                    break;
                }

                // Serialize transform.
                Transform t = module.transform;
                ModuleDTO moduleDto = new ModuleDTO();
                moduleDto.prefabId = prefabId;
                moduleDto.SetPosition(t.position);
                moduleDto.SetRotation(t.rotation);
                moduleDto.slottedToolItemId = string.Empty;

                ModuleGraphNodeDTO graphNodeDto = new ModuleGraphNodeDTO();
                graphNodeDto.prefabId = prefabId;
                graphNodeDto.moduleHashId = marker.Data != null ? marker.Data.ModuleHashId : 0;
                graphNodeDto.SetAup(AbsoluteUniversePosition.FromRuntimePosition(t.position));
                graphNodeDto.SetRotation(t.rotation);

                // Serialize dynamic state.
                // Passive modules have no BaseModule.
                // Defaults are valid for load.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    moduleDto.integrity = baseModule.CurrentIntegrity;
                    moduleDto.repairIntegrityCap = baseModule.MaxRecoverableIntegrity;
                    moduleDto.airReserveNormalized = baseModule.AirReserveNormalized;
                    moduleDto.co2Normalized = baseModule.Co2Normalized;
                    moduleDto.isFlooded = baseModule.IsFlooded;
                    moduleDto.failureMode = (byte)baseModule.CurrentFailureMode;
                    moduleDto.floodedReefFloodSeconds = baseModule.FloodedReefFloodSeconds;
                    moduleDto.interiorReefInfestationActive = baseModule.InteriorReefInfestationActive;
                }
                else
                {
                    moduleDto.integrity = DefaultIntegrity;
                    moduleDto.repairIntegrityCap = DefaultIntegrity;
                    moduleDto.airReserveNormalized = 1f;
                    moduleDto.co2Normalized = 0f;
                    moduleDto.isFlooded = DefaultIsFlooded;
                    moduleDto.failureMode = (byte)BaseModuleFailureMode.None;
                    moduleDto.floodedReefFloodSeconds = 0f;
                    moduleDto.interiorReefInfestationActive = false;
                }

                if (module.TryGetComponent(out MaintenanceStationModule maintenanceStation) && maintenanceStation.HasSlottedTool)
                    moduleDto.slottedToolItemId = maintenanceStation.SlottedToolPersistentId;

                if (module.TryGetComponent(out LogisticsSorterModule logisticsSorter))
                    logisticsSorter.PopulateSaveData(ref moduleDto);

                if (module.TryGetComponent(out DeepDrillModule deepDrill))
                    deepDrill.PopulateSaveData(ref moduleDto);

                if (module.TryGetComponent(out CultivationManager cultivationManager))
                    cultivationManager.PopulateSaveData(ref moduleDto, ResolvePlayerItemCatalog());

                if (module.TryGetComponent(out LogisticsPipeNode logisticsPipe))
                    logisticsPipe.PopulateSaveData(ref moduleDto);

                dto.modules[moduleIndex] = moduleDto;
                dto.graphNodes[moduleIndex] = graphNodeDto;
                moduleIndex++;
            }

            dto.moduleCount = moduleIndex;
            dto.graphNodeCount = moduleIndex;
            PopulateGraphEdges(ref dto, moduleIndex);
        }

        /// <summary>
        /// Restores placed modules from ConstructionDTO.
        ///
        /// Order:
        ///   1. ClearAllModules() removes the current base.
        ///   2. For each ModuleDTO:
        ///      a. Resolve prefab through ModuleCatalog.
        ///      b. Spawn through ObjectPoolManager.
        ///      c. Restore dynamic state before the first SlowTick.
        ///         This happens synchronously in the same frame.
        ///      d. RegisterModule with BuildableData binding.
        ///
        /// Migration v1 -> v2: integrity == 0f means the field did not exist
        /// in the old save, so it is treated as 100%.
        ///
        /// On errors, the module is skipped and the game continues.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            // Validation.
            if (catalog == null)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog not assigned! " +
                    "Cannot load construction data.");
                return;
            }

            if (catalog.HasLookupAmbiguity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog has ambiguous ID aliases. " +
                    $"Construction load aborted: {catalog.LookupAmbiguitySummary}");
#endif
                return;
            }

            ConstructionDTO dto = data.construction;
            ItemCatalog itemCatalog = ResolvePlayerItemCatalog();
            bool hasGraphTopology = data.version >= 47 &&
                                    dto.graphNodes != null &&
                                    dto.graphNodeCount > 0;

            // 1. Remove current base.

            // Guard: empty data.
            if ((!hasGraphTopology && (dto.modules == null || dto.moduleCount <= 0)) ||
                (hasGraphTopology && dto.graphNodeCount <= 0))
            {
                ClearAllModules();
                Debug.Log("[ConstructionManager] No construction data to load.");
                return;
            }

            // 2. Respawn modules from save.
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            ClearAllModules();
            int count = hasGraphTopology
                ? Mathf.Min(dto.graphNodeCount, dto.graphNodes.Length)
                : Mathf.Min(dto.moduleCount, dto.modules.Length);
            int loadedCount   = 0;
            int skippedCount  = 0;

            for (int i = 0; i < count; i++)
            {
                ModuleGraphNodeDTO graphNodeDto = hasGraphTopology ? dto.graphNodes[i] : default;
                bool hasLegacyModuleState = dto.modules != null && i >= 0 && i < dto.moduleCount && i < dto.modules.Length;
                ModuleDTO moduleDto = hasLegacyModuleState ? dto.modules[i] : default;

                // Resolve prefab.
                string prefabId = hasGraphTopology && !string.IsNullOrEmpty(graphNodeDto.prefabId)
                    ? graphNodeDto.prefabId
                    : moduleDto.prefabId;

                if (string.IsNullOrEmpty(prefabId) && (!hasGraphTopology || graphNodeDto.moduleHashId == 0))
                {
                    skippedCount++;
                    continue;
                }

                BuildableData buildData = !string.IsNullOrEmpty(prefabId)
                    ? catalog.FindDataById(prefabId)
                    : null;

                if (buildData == null && hasGraphTopology && graphNodeDto.moduleHashId != 0)
                    buildData = catalog.FindDataByHashId(graphNodeDto.moduleHashId);

                if (buildData == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{prefabId}' " +
                        "not found in catalog. Skipping.");
#endif
                    skippedCount++;
                    continue;
                }

                // Validate position.
                Vector3 pos = hasGraphTopology
                    ? graphNodeDto.GetAup().ToRuntimeFloat3()
                    : moduleDto.GetPosition();
                Quaternion rot = hasGraphTopology
                    ? graphNodeDto.GetRotation()
                    : moduleDto.GetRotation();

                if (float.IsNaN(pos.x) || float.IsInfinity(pos.x) ||
                    float.IsNaN(pos.y) || float.IsInfinity(pos.y) ||
                    float.IsNaN(pos.z) || float.IsInfinity(pos.z))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{moduleDto.prefabId}' " +
                        "has invalid position. Skipping.");
#endif
                    skippedCount++;
                    continue;
                }

                // Normalize quaternion to protect against save drift.
                if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
                    rot = Quaternion.identity;
                else
                    rot.Normalize();

                // Spawn.
                GameObject module;
                if (buildData.finalPrefab != null)
                {
                    if (pool == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning(
                            $"[ConstructionManager] ObjectPoolManager unavailable while loading '{prefabId}'. Skipping pooled prefab.");
#endif
                        skippedCount++;
                        continue;
                    }

                    module = pool.Spawn(buildData.finalPrefab, pos, rot);
                }
                else if (!ConstructionRuntimeProxyFactory.TryCreatePlacedProxy(buildData, pos, rot, out module))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{prefabId}' has no finalPrefab and proxy generation failed. Skipping.");
#endif
                    skippedCount++;
                    continue;
                }

                if (module == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Failed to spawn '{prefabId}'.");
#endif
                    skippedCount++;
                    continue;
                }

                // Restore dynamic state.
                // Restore synchronously before the first SlowTick.
                // BaseModule.OnEnable() registers SlowTick, but the first tick
                // runs on the next timer interval.
                // State is already restored by then.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    baseModule.ApplyBuildableTemplate(buildData);

                    // Migration v1 -> v2: integrity == 0f means
                    // the field did not exist in the old save.
                    // Treat it as full health.
                    float loadedIntegrity = moduleDto.integrity;
                    if (loadedIntegrity <= 0f)
                        loadedIntegrity = DefaultIntegrity;

                    float loadedRepairCap = moduleDto.repairIntegrityCap;
                    if (loadedRepairCap <= 0f)
                        loadedRepairCap = baseModule.MaxIntegrity;

                    float loadedAirReserveNormalized = data.version >= 28
                        ? Mathf.Clamp01(moduleDto.airReserveNormalized)
                        : 1f;
                    float loadedCo2Normalized = data.version >= 34
                        ? Mathf.Clamp01(moduleDto.co2Normalized)
                        : 0f;
                    float loadedFloodedReefFloodSeconds = data.version >= 49
                        ? Mathf.Max(0f, moduleDto.floodedReefFloodSeconds)
                        : 0f;
                    bool loadedInteriorReefInfestationActive = data.version >= 49 && moduleDto.interiorReefInfestationActive;

                    baseModule.SetState(
                        loadedIntegrity,
                        moduleDto.isFlooded,
                        (BaseModuleFailureMode)moduleDto.failureMode,
                        loadedRepairCap,
                        loadedAirReserveNormalized,
                        loadedCo2Normalized,
                        loadedFloodedReefFloodSeconds,
                        loadedInteriorReefInfestationActive);
                }

                if (hasLegacyModuleState &&
                    data.version >= 35 &&
                    itemCatalog != null &&
                    !string.IsNullOrWhiteSpace(moduleDto.slottedToolItemId) &&
                    module.TryGetComponent(out MaintenanceStationModule maintenanceStation))
                {
                    ItemData slottedToolItem = itemCatalog.FindById(moduleDto.slottedToolItemId);
                    if (slottedToolItem != null)
                        maintenanceStation.TryRestoreSlottedTool(slottedToolItem);
                }

                // Register with BuildableData binding.
                if (hasLegacyModuleState && data.version >= 36 && itemCatalog != null)
                {
                    if (module.TryGetComponent(out LogisticsSorterModule logisticsSorter))
                        logisticsSorter.RestoreFromSaveData(moduleDto, itemCatalog);

                    if (module.TryGetComponent(out DeepDrillModule deepDrill))
                        deepDrill.RestoreFromSaveData(moduleDto, itemCatalog);

                    if (module.TryGetComponent(out CultivationManager cultivationManager))
                        cultivationManager.RestoreFromSaveData(moduleDto, itemCatalog);

                    if (module.TryGetComponent(out LogisticsPipeNode logisticsPipe))
                        logisticsPipe.RestoreFromSaveData(moduleDto, itemCatalog);
                }

                RegisterModule(module, buildData);
                loadedCount++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[ConstructionManager] Loaded {loadedCount} modules" +
                (skippedCount > 0 ? $", skipped {skippedCount}." : "."));
#endif

            UpdateDiagnostics();
        }

        // -----------------------------------------------------------------------------
        //  PRIVATE - COLLECTION HELPERS (Zero GC)
        // -----------------------------------------------------------------------------

        /// <summary>
        /// Checks whether the module reference is already registered. O(n), but only
        /// called by Register. Zero GC.
        /// </summary>
        private void PopulateGraphEdges(ref ConstructionDTO dto, int savedNodeCount)
        {
            dto.graphEdgeCount = 0;
            if (_habitatGraphManager == null || savedNodeCount <= 0 || _habitatGraphManager.NodeCount != savedNodeCount)
                return;

            NativeArray<int> edgeOffsets = _habitatGraphManager.EdgeOffsets;
            NativeArray<int> edgeDestinations = _habitatGraphManager.EdgeDestinations;
            int edgeWriteIndex = 0;

            for (int sourceIndex = 0; sourceIndex < savedNodeCount; sourceIndex++)
            {
                int edgeStart = edgeOffsets[sourceIndex];
                int edgeEnd = edgeOffsets[sourceIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationIndex = edgeDestinations[edgeIndex];
                    if (destinationIndex <= sourceIndex || destinationIndex >= savedNodeCount)
                        continue;

                if (edgeWriteIndex >= ConstructionDTO.MaxGraphEdges)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ConstructionManager] Habitat graph edge budget ({ConstructionDTO.MaxGraphEdges}) exceeded during save. Truncating persisted topology.");
#endif
                    dto.graphEdgeCount = edgeWriteIndex;
                    return;
                }

                    dto.graphEdges[edgeWriteIndex] = new ModuleGraphEdgeDTO
                    {
                        sourceNodeIndex = sourceIndex,
                        destinationNodeIndex = destinationIndex
                    };
                    edgeWriteIndex++;
                }
            }

            dto.graphEdgeCount = edgeWriteIndex;
        }

        private static void DespawnOrDestroyModuleInstance(GameObject module, ObjectPoolManager pool)
        {
            if (module == null)
                return;

            if (module.TryGetComponent(out ConstructionRuntimeProxyTag _))
            {
                Destroy(module);
                return;
            }

            if (pool != null)
                pool.Despawn(module);
            else
                Destroy(module);
        }

        private static ItemCatalog ResolvePlayerItemCatalog()
        {
            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            return inventory != null ? inventory.ItemCatalog : null;
        }

        private bool ContainsRef(GameObject module)
        {
            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                    return true;
            }
            return false;
        }

        private bool ContainsBaseModuleRef(BaseModule module)
        {
            if (module == null || _spawnedBaseModules == null)
                return false;

            int count = _spawnedBaseModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedBaseModules[i], module))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Swap-remove: O(1) deletion without shifting the full array.
        /// Module order is intentionally not stable in this registry.
        /// </summary>
        private void SwapRemove(GameObject module)
        {
            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                {
                    int last = count - 1;
                    _spawnedModules[i] = _spawnedModules[last];
                    _spawnedModules.RemoveAt(last);
                    return;
                }
            }
        }

        private void RemoveBaseModule(GameObject module)
        {
            if (module == null || _spawnedBaseModules == null)
                return;

            if (!module.TryGetComponent(out BaseModule baseModule))
                return;

            int count = _spawnedBaseModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(_spawnedBaseModules[i], baseModule))
                    continue;

                int last = count - 1;
                _spawnedBaseModules[i] = _spawnedBaseModules[last];
                _spawnedBaseModules.RemoveAt(last);
                return;
            }
        }

        /// <summary>
        /// Removes null references from the list after external Destroy calls.
        /// Called before Save to keep the registry coherent.
        /// </summary>
        private void PurgeNullEntries()
        {
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                if (_spawnedModules[i] == null)
                {
                    int last = _spawnedModules.Count - 1;
                    _spawnedModules[i] = _spawnedModules[last];
                    _spawnedModules.RemoveAt(last);
                }
            }

            if (_spawnedBaseModules == null)
                return;

            for (int i = _spawnedBaseModules.Count - 1; i >= 0; i--)
            {
                if (_spawnedBaseModules[i] != null)
                    continue;

                int last = _spawnedBaseModules.Count - 1;
                _spawnedBaseModules[i] = _spawnedBaseModules[last];
                _spawnedBaseModules.RemoveAt(last);
            }
        }

        // -----------------------------------------------------------------------------
        //  DIAGNOSTICS
        // -----------------------------------------------------------------------------

        private void TryRegisterLogisticsService()
        {
            if (_logisticsServiceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterLogisticsService(this);
            _logisticsServiceRegistered = ReferenceEquals(GlobalRegistry.Logistics, this);
        }

        private void TryRegisterTick()
        {
            if (_tickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameTickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameTickRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryUnregisterTick()
        {
            if (!_tickRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _tickRegistered = false;
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameTickRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameTickRegistered = false;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            if (_registeredSaveService != null)
                TryUnregisterSaveParticipant();

            if (!_isInitialized || !isActiveAndEnabled)
                return;

            TryRegisterSaveParticipant(currentService as ISaveService);
        }

        private void TryRegisterSaveParticipant()
        {
            TryRegisterSaveParticipant(GlobalRegistry.Save);
        }

        private void TryRegisterSaveParticipant(ISaveService saveService)
        {
            if (!_isInitialized || !Application.isPlaying || _registeredSaveService != null || saveService == null)
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (_registeredSaveService == null)
                return;

            _registeredSaveService.Unregister(this);
            _registeredSaveService = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterHotSwapListener(this);
            _hotSwapListenerRegistered = GlobalRegistry.HotSwapListeners.Contains(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            if (GlobalRegistry.HotSwapListeners.Contains(this))
                GlobalRegistry.UnregisterHotSwapListener(this);

            _hotSwapListenerRegistered = false;
        }

        private void TryUnregisterLogisticsService()
        {
            if (!_logisticsServiceRegistered)
                return;

            GlobalRegistry.UnregisterLogisticsService(this);
            _logisticsServiceRegistered = false;
        }

        private void TryTriggerAmbientAccident()
        {
            PurgeNullEntries();

            int count = _spawnedModules.Count;
            if (count <= 0)
                return;

            BaseModule candidate = null;
            float bestRisk = ambientAccidentMinRisk;
            int startIndex = _ambientAccidentCursor % count;

            for (int offset = 0; offset < count; offset++)
            {
                int index = (startIndex + offset) % count;
                GameObject moduleObject = _spawnedModules[index];
                if (moduleObject == null || !moduleObject.TryGetComponent(out BaseModule module))
                    continue;

                if (!TryEvaluateAmbientAccidentRisk(module, out float risk))
                    continue;

                if (risk <= bestRisk)
                    continue;

                bestRisk = risk;
                candidate = module;
                _ambientAccidentCursor = index + 1;
            }

            if (candidate == null)
                return;

            float accidentChance = Mathf.Clamp01(ambientAccidentBaseChance * bestRisk);
            if (!PassDeterministicAmbientAccidentChance(candidate, accidentChance))
                return;

            TriggerAmbientAccident(candidate, bestRisk);
        }

        private bool PassDeterministicAmbientAccidentChance(BaseModule candidate, float chance01)
        {
            if (chance01 <= 0f)
                return false;
            if (chance01 >= 1f)
                return true;

            uint roll = BuildAmbientAccidentRoll(candidate);
            uint threshold24 = (uint)(chance01 * 0x00FFFFFFu);
            return (roll & 0x00FFFFFFu) <= threshold24;
        }

        private uint BuildAmbientAccidentRoll(BaseModule candidate)
        {
            uint hash = 2166136261u;
            hash = FoldAmbientAccidentHash(hash, (uint)ResolveModuleHashId(candidate));
            hash = FoldAmbientAccidentHash(hash, (uint)_ambientAccidentCursor);

            if (candidate != null)
            {
                AbsoluteUniversePosition position = AbsoluteUniversePosition.FromRuntimePosition(candidate.transform.position);
                hash = FoldAmbientAccidentHash(hash, (uint)position.GridX);
                hash = FoldAmbientAccidentHash(hash, (uint)((ulong)position.GridX >> 32));
                hash = FoldAmbientAccidentHash(hash, (uint)position.GridY);
                hash = FoldAmbientAccidentHash(hash, (uint)((ulong)position.GridY >> 32));
                hash = FoldAmbientAccidentHash(hash, (uint)position.GridZ);
                hash = FoldAmbientAccidentHash(hash, (uint)((ulong)position.GridZ >> 32));
            }

            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        private static uint FoldAmbientAccidentHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private bool TryEvaluateAmbientAccidentRisk(BaseModule module, out float risk)
        {
            risk = 0f;

            if (module == null)
                return false;

            if (module.HasCascadeFailure || module.CurrentIntegrity <= 0f || module.MaxIntegrity <= 0f)
                return false;

            float integrity01 = module.CurrentIntegrity / module.MaxIntegrity;
            if (integrity01 >= 0.999f && module.HasPower && !module.IsFlooded)
                return false;

            risk = 1f - integrity01;

            if (integrity01 <= ambientAccidentIntegrityThreshold)
                risk += 0.25f;

            if (!module.HasPower)
                risk += 0.2f;

            if (module.IsFlooded)
                risk += 0.35f;

            return risk >= ambientAccidentMinRisk;
        }

        private static void TriggerAmbientAccident(BaseModule module, float risk)
        {
            if (module == null)
                return;

            string source = ResolveModuleSource(module);
            string summary = BuildAmbientAccidentSummary(module);
            FieldOperationLogSystem.RecordOperation(source, "SERVICE ACCIDENT", summary, "WARN");

            module.ApplyDamage(module.CurrentIntegrity + 1f);
        }

        private static string ResolveModuleSource(BaseModule module)
        {
            if (module != null && module.TryGetComponent(out ModuleMarker marker) && marker.Data != null)
            {
                string moduleName = marker.Data.moduleName;
                if (!string.IsNullOrWhiteSpace(moduleName))
                    return moduleName;
            }

            return "BASE";
        }

        private static string BuildAmbientAccidentSummary(BaseModule module)
        {
            if (module == null)
                return "Neglected service hardware destabilized and rolled into a cascade failure.";

            if (module.IsFlooded)
                return "Residual flooding was left unresolved and rolled into a live compartment incident.";
            else if (!module.HasPower)
                return "Power loss left pumps offline and rolled into a live compartment incident.";
            else
                return "Hull fatigue crossed the maintenance margin and rolled into a live compartment incident.";
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugModuleCount = _spawnedModules.Count;
        }

        private void RefreshHabitatGraph()
        {
            if (_habitatGraphManager == null || _spawnedModules == null)
            {
                _habitatGraphDirty = false;
                return;
            }

            _habitatGraphManager.Rebuild(_spawnedModules);
            _habitatGraphDirty = false;
        }

        private void MarkHabitatGraphDirty()
        {
            _habitatGraphDirty = true;
            TryRegisterLateFrameTick();
        }

        internal void NotifyModuleEmergencyStateChanged(BaseModule module)
        {
            if (_habitatGraphManager == null)
                return;

            _habitatGraphManager.NotifyModuleEmergencyStateChanged(module);
        }

        internal void NotifyModuleImploded(BaseModule module)
        {
            if (_habitatGraphManager == null || module == null)
                return;

            FloraInteractionManager floraInteractionManager = FloraInteractionManager.ActiveRuntimeInstance;
            if (floraInteractionManager != null)
                floraInteractionManager.KillAttachedParasites(module);

            MarkHabitatGraphDirty();
        }

        internal void NotifyModuleDetachedAsDebris(BaseModule module)
        {
            if (_habitatGraphManager == null || module == null)
                return;

            GameObject moduleObject = module.gameObject;
            SwapRemove(moduleObject);
            RemoveBaseModule(moduleObject);
            MarkHabitatGraphDirty();
            UpdateDiagnostics();
        }

        internal void NotifyModuleParasiteRootStateChanged(BaseModule module)
        {
            if (_habitatGraphManager == null || module == null)
                return;

            MarkHabitatGraphDirty();
        }

        internal bool TryResolveFungalMindTarget(BaseModule sourceModule, out BaseModule targetModule, out float targetPotential)
        {
            targetModule = null;
            targetPotential = 0f;
            return _habitatGraphManager != null &&
                   _habitatGraphManager.TryResolveFungalMindTarget(sourceModule, out targetModule, out targetPotential);
        }
    }
}
