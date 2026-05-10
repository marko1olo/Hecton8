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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            // â”€â”€ Service â”€â”€
            // â”€â”€ Pre-allocate â”€â”€
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
        /// Ð£Ð´Ð°Ð»ÑÐµÑ‚ Ð¼Ð¾Ð´ÑƒÐ»ÑŒ Ð¸Ð· Ñ€ÐµÐµÑÑ‚Ñ€Ð°. ÐÐ• Ð´ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ ÐµÐ³Ð¾.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐ¹ Ð´Ð»Ñ Ð´ÐµÐºÐ¾Ð½ÑÑ‚Ñ€ÑƒÐºÑ†Ð¸Ð¸: Unregister + Pool.Despawn.
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
        /// Ð£Ð´Ð°Ð»ÑÐµÑ‚ Ð¸Ð· Ñ€ÐµÐµÑÑ‚Ñ€Ð° Ð˜ Ð´ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ Ñ‡ÐµÑ€ÐµÐ· Ð¿ÑƒÐ».
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ Ð¿Ñ€Ð¸ Ð´ÐµÐºÐ¾Ð½ÑÑ‚Ñ€ÑƒÐºÑ†Ð¸Ð¸ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹.
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” CLEAR ALL
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð”ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ Ð’Ð¡Ð• Ð¼Ð¾Ð´ÑƒÐ»Ð¸ Ñ‡ÐµÑ€ÐµÐ· Ð¿ÑƒÐ» Ð¸ Ð¾Ñ‡Ð¸Ñ‰Ð°ÐµÑ‚ Ñ€ÐµÐµÑÑ‚Ñ€.
        ///
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ:
        ///   â€¢ LoadFromSaveData() Ð¿ÐµÑ€ÐµÐ´ Ñ€ÐµÑÐ¿Ð°Ð²Ð½Ð¾Ð¼ Ð¸Ð· ÑÐµÐ¹Ð²Ð°
        ///   â€¢ New Game (ÐµÑÐ»Ð¸ Ð½ÑƒÐ¶Ð½Ð¾ Ð½Ð°Ñ‡Ð°Ñ‚ÑŒ Ñ Ñ‡Ð¸ÑÑ‚Ð¾Ð³Ð¾ Ð¼Ð¸Ñ€Ð°)
        ///
        /// Ð˜Ñ‚ÐµÑ€Ð°Ñ†Ð¸Ñ Ð¾Ð±Ñ€Ð°Ñ‚Ð½Ñ‹Ð¼ Ñ†Ð¸ÐºÐ»Ð¾Ð¼: Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ Ð¿Ñ€Ð¸ Despawn,
        /// ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ð¹ Ð¼Ð¾Ð¶ÐµÑ‚ Ð²Ñ‹Ð·Ð²Ð°Ñ‚ÑŒ OnDisable Ð½Ð° Ð¼Ð¾Ð´ÑƒÐ»ÑÑ….
        /// </summary>
        public void ClearAllModules()
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;

            // â”€â”€ ÐžÐ±Ñ€Ð°Ñ‚Ð½Ñ‹Ð¹ Ñ†Ð¸ÐºÐ»: Ð±ÐµÐ·Ð¾Ð¿Ð°ÑÐ½Ð¾ Ð¿Ñ€Ð¸ Ð¼Ð¾Ð´Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸Ð¸ ÑÐ¿Ð¸ÑÐºÐ° â”€â”€
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                GameObject module = _spawnedModules[i];

                if (module == null) continue; // ÑƒÐ¶Ðµ ÑƒÐ½Ð¸Ñ‡Ñ‚Ð¾Ð¶ÐµÐ½

                DespawnOrDestroyModuleInstance(module, pool);
            }

            _spawnedModules.Clear();
            _spawnedBaseModules.Clear();
            RefreshHabitatGraph();

            UpdateDiagnostics();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ISaveable â€” SAVE / LOAD (Priority 90)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Construction Ð·Ð°Ð³Ñ€ÑƒÐ¶Ð°ÐµÑ‚ÑÑ ÐŸÐžÐ¡Ð›Ð•Ð”ÐÐ•Ð™ (Ð·Ð°Ð²Ð¸ÑÐ¸Ñ‚ Ð¾Ñ‚ Ð¼Ð¸Ñ€Ð°).</summary>
        public int SavePriority => 90;
        public int LoadPriority => 90;

        /// <summary>
        /// Ð—Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÑ‚ Ð²ÑÐµ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸ Ð² ConstructionDTO.
        ///
        /// Ð”Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð¼Ð¾Ð´ÑƒÐ»Ñ:
        ///   1. ÐŸÐ¾Ð»ÑƒÑ‡Ð°ÐµÑ‚ ModuleMarker â†’ PrefabId
        ///   2. Ð§Ð¸Ñ‚Ð°ÐµÑ‚ transform.position Ð¸ rotation
        ///   3. Ð§Ð¸Ñ‚Ð°ÐµÑ‚ BaseModule.CurrentIntegrity Ð¸ IsFlooded (ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ)
        ///   4. Ð—Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÐµÑ‚ Ð² dto.modules[]
        ///
        /// ÐœÐ¾Ð´ÑƒÐ»Ð¸ Ð±ÐµÐ· ModuleMarker â€” Ð¿Ñ€Ð¾Ð¿ÑƒÑÐºÐ°ÑŽÑ‚ÑÑ Ñ Warning.
        /// ÐœÐ¾Ð´ÑƒÐ»Ð¸ Ð±ÐµÐ· BaseModule â€” Ð·Ð°Ð¿Ð¸ÑÑ‹Ð²Ð°ÑŽÑ‚ÑÑ Ñ Ð´ÐµÑ„Ð¾Ð»Ñ‚Ð½Ñ‹Ð¼Ð¸ Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸ÑÐ¼Ð¸
        /// (100% HP, Ð½Ðµ Ð·Ð°Ñ‚Ð¾Ð¿Ð»ÐµÐ½). Ð­Ñ‚Ð¾ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ð¾ Ð´Ð»Ñ Ð¿Ð°ÑÑÐ¸Ð²Ð½Ñ‹Ñ… Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ (Ð¾Ð¿Ð¾Ñ€Ñ‹).
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

                // â”€â”€ Guard: destroyed reference â”€â”€
                if (module == null) continue;

                // â”€â”€ Guard: missing marker â”€â”€
                if (!module.TryGetComponent(out ModuleMarker marker))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has no ModuleMarker. " +
                        "Skipping save for this module.");
                    continue;
                }

                // â”€â”€ Guard: empty ID â”€â”€
                string prefabId = marker.PrefabId;
                if (string.IsNullOrEmpty(prefabId))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{module.name}' has empty PrefabId. " +
                        "Skipping.");
                    continue;
                }

                // â”€â”€ Guard: capacity â”€â”€
                if (moduleIndex >= ConstructionDTO.MaxModules)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Max modules ({ConstructionDTO.MaxModules}) reached. " +
                        $"Truncating save: {count - moduleIndex} modules not saved.");
                    break;
                }

                // â”€â”€ Serialize transform â”€â”€
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

                // â”€â”€ Serialize dynamic state â”€â”€
                // ÐŸÐ°ÑÑÐ¸Ð²Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸ (Ð¾Ð¿Ð¾Ñ€Ñ‹, Ð´ÐµÐºÐ¾Ñ€) Ð½Ðµ Ð¸Ð¼ÐµÑŽÑ‚ BaseModule.
                // Ð”Ð»Ñ Ð½Ð¸Ñ… Ð¿Ð¸ÑˆÐµÐ¼ Ð´ÐµÑ„Ð¾Ð»Ñ‚Ð½Ñ‹Ðµ Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ñ â€” Ð¿Ñ€Ð¸ Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ Ð¾Ð½Ð¸ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ñ‹.
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
        /// Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ Ð¿Ð¾ÑÑ‚Ñ€Ð¾ÐµÐ½Ð½Ñ‹Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ð¸ Ð¸Ð· ConstructionDTO.
        ///
        /// ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº:
        ///   1. ClearAllModules() â€” ÑƒÐ´Ð°Ð»Ð¸Ñ‚ÑŒ Ñ‚ÐµÐºÑƒÑ‰ÑƒÑŽ Ð±Ð°Ð·Ñƒ
        ///   2. Ð”Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ ModuleDTO:
        ///      a. ÐÐ°Ð¹Ñ‚Ð¸ Ð¿Ñ€ÐµÑ„Ð°Ð± Ñ‡ÐµÑ€ÐµÐ· ModuleCatalog
        ///      b. Spawn Ñ‡ÐµÑ€ÐµÐ· ObjectPoolManager
        ///      c. Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð¾Ð²Ð¸Ñ‚ÑŒ Ð´Ð¸Ð½Ð°Ð¼Ð¸Ñ‡ÐµÑÐºÐ¾Ðµ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ (integrity, isFlooded)
        ///         Ð”Ðž Ð¿ÐµÑ€Ð²Ð¾Ð³Ð¾ SlowTick (ÑÐ¸Ð½Ñ…Ñ€Ð¾Ð½Ð½Ð¾, Ð² Ñ‚Ð¾Ð¼ Ð¶Ðµ ÐºÐ°Ð´Ñ€Ðµ)
        ///      d. RegisterModule (Ñ Ð¿Ñ€Ð¸Ð²ÑÐ·ÐºÐ¾Ð¹ BuildableData)
        ///
        /// ÐœÐ¸Ð³Ñ€Ð°Ñ†Ð¸Ñ v1 â†’ v2: ÐµÑÐ»Ð¸ integrity == 0f (Ð´ÐµÑ„Ð¾Ð»Ñ‚ Ð´Ð»Ñ float Ð¿Ñ€Ð¸
        /// Ð´ÐµÑÐµÑ€Ð¸Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ð¸ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾ ÑÐµÐ¹Ð²Ð° Ð±ÐµÐ· ÑÑ‚Ð¾Ð³Ð¾ Ð¿Ð¾Ð»Ñ), Ñ‚Ñ€Ð°ÐºÑ‚ÑƒÐµÐ¼ ÐºÐ°Ðº 100%.
        ///
        /// ÐŸÑ€Ð¸ Ð¾ÑˆÐ¸Ð±ÐºÐ°Ñ…: Ð¼Ð¾Ð´ÑƒÐ»ÑŒ Ð¿Ñ€Ð¾Ð¿ÑƒÑÐºÐ°ÐµÑ‚ÑÑ, Ð¸Ð³Ñ€Ð° Ð½Ðµ ÐºÑ€Ð°ÑˆÐ¸Ñ‚ÑÑ.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            // â”€â”€ Ð’Ð°Ð»Ð¸Ð´Ð°Ñ†Ð¸Ñ â”€â”€
            if (catalog == null)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog not assigned! " +
                    "Cannot load construction data.");
                return;
            }

            if (catalog.HasLookupAmbiguity)
            {
                Debug.LogError(
                    "[ConstructionManager] ModuleCatalog has ambiguous ID aliases. " +
                    $"Construction load aborted: {catalog.LookupAmbiguitySummary}");
                return;
            }

            ConstructionDTO dto = data.construction;
            ItemCatalog itemCatalog = ResolvePlayerItemCatalog();
            bool hasGraphTopology = data.version >= 47 &&
                                    dto.graphNodes != null &&
                                    dto.graphNodeCount > 0;

            // â”€â”€ 1. Ð£Ð´Ð°Ð»ÑÐµÐ¼ Ñ‚ÐµÐºÑƒÑ‰ÑƒÑŽ Ð±Ð°Ð·Ñƒ â”€â”€

            // â”€â”€ Guard: Ð¿ÑƒÑÑ‚Ñ‹Ðµ Ð´Ð°Ð½Ð½Ñ‹Ðµ â”€â”€
            if ((!hasGraphTopology && (dto.modules == null || dto.moduleCount <= 0)) ||
                (hasGraphTopology && dto.graphNodeCount <= 0))
            {
                ClearAllModules();
                Debug.Log("[ConstructionManager] No construction data to load.");
                return;
            }

            // â”€â”€ 2. Ð ÐµÑÐ¿Ð°Ð²Ð½ Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð¸Ð· ÑÐµÐ¹Ð²Ð° â”€â”€
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

                // â”€â”€ ÐŸÐ¾Ð¸ÑÐº Ð¿Ñ€ÐµÑ„Ð°Ð±Ð° â”€â”€
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
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{prefabId}' " +
                        "not found in catalog. Skipping.");
                    skippedCount++;
                    continue;
                }

                // â”€â”€ Ð’Ð°Ð»Ð¸Ð´Ð°Ñ†Ð¸Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ â”€â”€
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
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{moduleDto.prefabId}' " +
                        "has invalid position. Skipping.");
                    skippedCount++;
                    continue;
                }

                // ÐÐ¾Ñ€Ð¼Ð°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ quaternion (Ð·Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ float-Ð´Ñ€Ð¸Ñ„Ñ‚Ð° Ð² ÑÐµÐ¹Ð²Ðµ)
                if (rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f)
                    rot = Quaternion.identity;
                else
                    rot.Normalize();

                // â”€â”€ Spawn â”€â”€
                GameObject module;
                if (buildData.finalPrefab != null)
                {
                    if (pool == null)
                    {
                        Debug.LogWarning(
                            $"[ConstructionManager] ObjectPoolManager unavailable while loading '{prefabId}'. Skipping pooled prefab.");
                        skippedCount++;
                        continue;
                    }

                    module = pool.Spawn(buildData.finalPrefab, pos, rot);
                }
                else if (!ConstructionRuntimeProxyFactory.TryCreatePlacedProxy(buildData, pos, rot, out module))
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Module '{prefabId}' has no finalPrefab and proxy generation failed. Skipping.");
                    skippedCount++;
                    continue;
                }

                if (module == null)
                {
                    Debug.LogWarning(
                        $"[ConstructionManager] Failed to spawn '{prefabId}'.");
                    skippedCount++;
                    continue;
                }

                // â”€â”€ Restore dynamic state â”€â”€
                // Ð’ÐÐ–ÐÐž: Ð²Ñ‹Ð¿Ð¾Ð»Ð½ÑÐµÑ‚ÑÑ ÑÐ¸Ð½Ñ…Ñ€Ð¾Ð½Ð½Ð¾, Ð”Ðž Ð¿ÐµÑ€Ð²Ð¾Ð³Ð¾ SlowTick.
                // BaseModule.OnEnable() Ñ€ÐµÐ³Ð¸ÑÑ‚Ñ€Ð¸Ñ€ÑƒÐµÑ‚ SlowTick, Ð½Ð¾ Ð¿ÐµÑ€Ð²Ñ‹Ð¹
                // Ð²Ñ‹Ð·Ð¾Ð² Ð¿Ñ€Ð¾Ð¸Ð·Ð¾Ð¹Ð´Ñ‘Ñ‚ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð² ÑÐ»ÐµÐ´ÑƒÑŽÑ‰ÐµÐ¼ Ð¸Ð½Ñ‚ÐµÑ€Ð²Ð°Ð»Ðµ Ñ‚Ð°Ð¹Ð¼ÐµÑ€Ð°.
                // Ðš ÑÑ‚Ð¾Ð¼Ñƒ Ð¼Ð¾Ð¼ÐµÐ½Ñ‚Ñƒ ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ ÑƒÐ¶Ðµ Ð±ÑƒÐ´ÐµÑ‚ ÑƒÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð¾.
                if (module.TryGetComponent(out BaseModule baseModule))
                {
                    baseModule.ApplyBuildableTemplate(buildData);

                    // ÐœÐ¸Ð³Ñ€Ð°Ñ†Ð¸Ñ v1 â†’ v2: integrity == 0f Ð¾Ð·Ð½Ð°Ñ‡Ð°ÐµÑ‚,
                    // Ñ‡Ñ‚Ð¾ Ð¿Ð¾Ð»Ðµ Ð½Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð¾Ð²Ð°Ð»Ð¾ Ð² ÑÑ‚Ð°Ñ€Ð¾Ð¼ ÑÐµÐ¹Ð²Ðµ.
                    // Ð¢Ñ€Ð°ÐºÑ‚ÑƒÐµÐ¼ ÐºÐ°Ðº Â«Ð¿Ð¾Ð»Ð½Ð¾Ðµ Ð·Ð´Ð¾Ñ€Ð¾Ð²ÑŒÐµÂ».
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

                // â”€â”€ Register Ñ Ð¿Ñ€Ð¸Ð²ÑÐ·ÐºÐ¾Ð¹ Ðº BuildableData â”€â”€
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

            Debug.Log(
                $"[ConstructionManager] Loaded {loadedCount} modules" +
                (skippedCount > 0 ? $", skipped {skippedCount}." : "."));

            UpdateDiagnostics();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” COLLECTION HELPERS (Zero GC)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ€Ð¾Ð²ÐµÑ€ÑÐµÑ‚ Ð½Ð°Ð»Ð¸Ñ‡Ð¸Ðµ Ð¼Ð¾Ð´ÑƒÐ»Ñ Ð¿Ð¾ ÑÑÑ‹Ð»ÐºÐµ. O(n), Ð½Ð¾ Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ
        /// Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿Ñ€Ð¸ Register (Ñ€ÐµÐ´ÐºÐ¾). Zero GC.
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
                        Debug.LogWarning(
                            $"[ConstructionManager] Habitat graph edge budget ({ConstructionDTO.MaxGraphEdges}) exceeded during save. Truncating persisted topology.");
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
        /// Swap-remove: O(1) ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ðµ Ð±ÐµÐ· ÑÐ´Ð²Ð¸Ð³Ð° Ð¼Ð°ÑÑÐ¸Ð²Ð°.
        /// ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº Ð¼Ð¾Ð´ÑƒÐ»ÐµÐ¹ Ð½Ðµ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ð½ (Ð´Ð¾Ð¿ÑƒÑÑ‚Ð¸Ð¼Ð¾ Ð´Ð»Ñ ÑÑ‚Ð¾Ð¹ ÑÐ¸ÑÑ‚ÐµÐ¼Ñ‹).
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
        /// ÐžÑ‡Ð¸Ñ‰Ð°ÐµÑ‚ null-ÑÑÑ‹Ð»ÐºÐ¸ Ð¸Ð· ÑÐ¿Ð¸ÑÐºÐ° (Ð·Ð°Ñ‰Ð¸Ñ‚Ð° Ð¾Ñ‚ Destroy Ð¸Ð·Ð²Ð½Ðµ).
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¿ÐµÑ€ÐµÐ´ Save Ð´Ð»Ñ Ð³Ð°Ñ€Ð°Ð½Ñ‚Ð¸Ð¸ Ñ†ÐµÐ»Ð¾ÑÑ‚Ð½Ð¾ÑÑ‚Ð¸.
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
            string summary = BuildAmbientAccidentSummary(module, risk);
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

        private static string BuildAmbientAccidentSummary(BaseModule module, float risk)
        {
            if (module == null)
                return "Neglected service hardware destabilized and rolled into a cascade failure.";

            float integrity01 = module.MaxIntegrity > 0f
                ? module.CurrentIntegrity / module.MaxIntegrity
                : 0f;

            string condition;
            if (module.IsFlooded)
                condition = "Residual flooding was left unresolved.";
            else if (!module.HasPower)
                condition = "Power loss left pumps and service recovery offline.";
            else
                condition = "Hull fatigue crossed the unattended maintenance margin.";

            return $"Integrity {integrity01:0%}. {condition} Risk {Mathf.Clamp01(risk):0%} converted into a live compartment incident.";
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
