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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class ConstructionManager : MonoBehaviour, IUpdatable, ILateFrameTickable, ISaveable, ISlowTickable, ILogisticsService, IHabitatGraphService, IHabitatDeconstructionSystem, IGlobalRegistryHotSwapListener, IServiceHeartbeat, IServiceShutdown, IOriginShiftListener, IRandomEventListener
    {
        private const float SlowTickDeltaTime = 0.1f;
        private const int InitialJointRecoveryCapacity = 64;
        private const int InitialJointBodyRecoveryCapacity = 128;
        private const byte HabitatConstructionOperationPlaced = 1;
        private const byte HabitatConstructionOperationRemoved = 2;
        private const byte HabitatConstructionFlagSmokeVfx = 1 << 0;
        private const byte HabitatConstructionFlagGraphDirty = 1 << 1;
        private const byte DeconstructResultRejected = 0;
        private const byte DeconstructResultAccepted = 1;
        private const byte DeconstructReasonNone = 0;
        private const byte DeconstructReasonNoTarget = 1;
        private const byte DeconstructReasonRayMismatch = 2;
        private const byte DeconstructReasonGraphRejected = 3;
        private const byte DeconstructReasonInventoryFull = 4;
        private const byte DeconstructReasonPoolUnavailable = 5;
        private const byte DeconstructReasonAlreadyActive = 6;
        private const byte ModuleDeconstructOperationDeleteMarker = 1;
        private const byte ModuleDeconstructFlagForcePowerColdTick = 1 << 0;
        private const byte ModuleDeconstructFlagDfsSkippedLowTier = 1 << 1;
        private const byte DeconstructionDebrisKindDisintegrate = 10;
        private const int DeconstructionDfsResultLength = 4;
        private const int DeconstructionRaycastCapacity = 1;
        private const int DeconstructionBlackBoxCapacity = 300;
        private const string DeconstructionDumpRelativePath = "Docs/AgentLogs/Dump_BASE_DECONSTRUCTION_SYS.bin";
        private const string NativeMemoryOwner = nameof(ConstructionManager);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct HabitatDeconstructionTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint TargetEntityId;
            [FieldOffset(8)]
            public uint RequesterEntityId;
            [FieldOffset(12)]
            public float DistanceMeters;
            [FieldOffset(16)]
            public ushort DfsVisitedCount;
            [FieldOffset(18)]
            public ushort DfsExpectedCount;
            [FieldOffset(20)]
            public byte Result;
            [FieldOffset(21)]
            public byte Reason;
            [FieldOffset(22)]
            public byte Flags;
            [FieldOffset(23)]
            public byte Reserved;
            [FieldOffset(24)]
            private ulong _pad0;
        }

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
        private bool _habitatGraphServiceRegistered;
        private bool _habitatDeconstructionServiceRegistered;
        private bool _hotSwapListenerRegistered;
        private bool _originShiftListenerRegistered;
        private bool _randomEventListenerRegistered;
        private ISaveService _registeredSaveService;
        private bool _isInitialized;
        private bool _habitatGraphDirty;
        private float _slowTickAccumulator;
        private float _ambientAccidentTimer;
        private int _ambientAccidentCursor;
        private List<Joint> _jointRecoveryBuffer;
        private Rigidbody[] _jointRecoveryBodies;
        private Vector3[] _jointRecoveryLinearVelocities;
        private Vector3[] _jointRecoveryAngularVelocities;
        private NativeList<long> _deconstructionDfsStack;
        private NativeParallelHashSet<long> _deconstructionDfsVisited;
        private NativeArray<int> _deconstructionDfsResult;
        private NativeArray<HabitatDeconstructionTelemetryEntry> _deconstructionBlackBox;
        private NativeArray<RaycastCommand> _deconstructionRaycastCommands;
        private NativeArray<RaycastHit> _deconstructionRaycastHits;
        private DeconstructRequestSignal _pendingDeconstructionRequest;
        private BaseModule _pendingDeconstructionModule;
        private JobHandle _deconstructionRaycastHandle;
        private int _deconstructionBlackBoxCursor;
        private bool _deconstructionRaycastScheduled;

        // CONSTANTS - DEFAULT MODULE STATE

        /// <summary>
        /// Default integrity for modules without BaseModule and for old save migration.
        /// </summary>
        private const float DefaultIntegrity = 100f;

        /// <summary>Default flood state.</summary>
        private const bool  DefaultIsFlooded = false;
        private const byte ModuleBlitFlagFlooded = 1 << 0;
        private const byte ModuleBlitFlagInteriorReef = 1 << 1;

        // PUBLIC API - QUERIES

        internal bool TryGetHabitatAcousticGraph(out HabitatGraphManager graph)
        {
            graph = _habitatGraphManager;
            return graph != null && graph.NodeCount > 0;
        }

        /// <inheritdoc />
        int IHabitatGraphService.RoomCount => _habitatGraphManager != null ? _habitatGraphManager.NodeCount : 0;

        /// <inheritdoc />
        NativeArray<float>.ReadOnly IHabitatGraphService.RoomWaterLevels =>
            _habitatGraphManager != null
                ? _habitatGraphManager.RoomWaterLevels
                : default;

        /// <inheritdoc />
        public uint FloodStateSequence => _habitatGraphManager != null ? _habitatGraphManager.FloodStateSequence : 0u;

        /// <inheritdoc />
        public bool TryResolveRoomWaterline(
            Vector3 runtimePosition,
            int cachedRoomId,
            out HabitatRoomWaterlineSnapshot snapshot)
        {
            snapshot = default;
            return _habitatGraphManager != null &&
                   _habitatGraphManager.TryResolveRoomWaterline(runtimePosition, cachedRoomId, out snapshot);
        }

        /// <inheritdoc />
        public bool TryGetRoomWaterline(int roomId, out HabitatRoomWaterlineSnapshot snapshot)
        {
            snapshot = default;
            return _habitatGraphManager != null &&
                   _habitatGraphManager.TryGetRoomWaterline(roomId, out snapshot);
        }

        /// <summary>Number of placed modules.</summary>
        public int ModuleCount => _spawnedModules != null ? _spawnedModules.Count : 0;

        /// <summary>Read-only access to placed modules for UI and minimap consumers.</summary>
        public IReadOnlyList<GameObject> SpawnedModules => _spawnedModules;

        /// <summary>Indexed placed-module access for hot-path construction consumers that must avoid interface-list dispatch.</summary>
        internal GameObject GetSpawnedModuleAt(int index)
        {
            return _spawnedModules != null && (uint)index < (uint)_spawnedModules.Count
                ? _spawnedModules[index]
                : null;
        }

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
            TryRegisterHabitatGraphService();
            TryRegisterHabitatDeconstructionService();
            TryRegisterTick();
            TryRegisterLateFrameTick();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            TryRegisterOriginShiftListener();
            TryRegisterRandomEventListener();
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

            EnsureDeconstructionNativeBuffers(capacity);

            int jointCapacity = Mathf.Max(InitialJointRecoveryCapacity, capacity);
            if (_jointRecoveryBuffer == null)
                _jointRecoveryBuffer = new List<Joint>(jointCapacity); // COLD ALLOC: List<Joint>[capacity] - AUP shift joint re-anchor staging - owner: ConstructionManager

            int bodyCapacity = Mathf.Max(InitialJointBodyRecoveryCapacity, capacity * 2);
            if (_jointRecoveryBodies == null || _jointRecoveryBodies.Length < bodyCapacity)
            {
                _jointRecoveryBodies = new Rigidbody[bodyCapacity]; // COLD ALLOC: Rigidbody[capacity*2] - AUP joint velocity restore cache - owner: ConstructionManager
                _jointRecoveryLinearVelocities = new Vector3[bodyCapacity]; // COLD ALLOC: Vector3[capacity*2] - AUP joint linear velocity restore cache - owner: ConstructionManager
                _jointRecoveryAngularVelocities = new Vector3[bodyCapacity]; // COLD ALLOC: Vector3[capacity*2] - AUP joint angular velocity restore cache - owner: ConstructionManager
            }
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            EnsureRuntimeStorage();
            _slowTickAccumulator = 0f;
            if (!_isInitialized)
                return;

            TryRegisterLogisticsService();
            TryRegisterHabitatGraphService();
            TryRegisterHabitatDeconstructionService();
            TryRegisterTick();
            TryRegisterLateFrameTick();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            TryRegisterOriginShiftListener();
            TryRegisterRandomEventListener();
        }

        private void Start()
        {
            if (!_isInitialized)
                return;

            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            TryRegisterOriginShiftListener();
            TryRegisterRandomEventListener();
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
            DisposeDeconstructionNativeBuffers();
        }

        private void UnregisterRuntimeHooks()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterTick();
            TryUnregisterLateFrameTick();
            TryUnregisterHabitatDeconstructionService();
            TryUnregisterHabitatGraphService();
            TryUnregisterLogisticsService();
            _slowTickAccumulator = 0f;
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            TryUnregisterOriginShiftListener();
            TryUnregisterRandomEventListener();
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
            if (_habitatGraphDirty)
                RefreshHabitatGraph();

            FinalizeDeferredDeconstructionRaycast();
            DrainDeconstructionRequests();
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
            PublishHabitatConstructionSignal(
                module,
                HabitatConstructionOperationPlaced,
                HabitatConstructionFlagGraphDirty | HabitatConstructionFlagSmokeVfx);
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

            PublishHabitatConstructionSignal(
                module,
                HabitatConstructionOperationRemoved,
                HabitatConstructionFlagGraphDirty);
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

        /// <inheritdoc />
        public bool EnqueueDeconstruction(in DeconstructRequestSignal signal)
        {
            if (!_isInitialized || !Application.isPlaying)
                return false;

            GlobalSignals.Publish(in signal);
            return true;
        }

        /// <inheritdoc />
        public bool TrySetDeconstructionPreview(uint targetEntityId, bool enabled)
        {
            if (!_isInitialized || !Application.isPlaying)
                return false;

            BaseModule module = ResolveBaseModuleByEntityId(targetEntityId);
            if (module == null)
                return false;

            module.SetDeconstructionPreview(enabled);
            return true;
        }

        private void DrainDeconstructionRequests()
        {
            if (_deconstructionRaycastScheduled)
                return;

            while (GlobalSignals.TryDequeueDeconstructRequest(out DeconstructRequestSignal request))
            {
                ProcessDeconstructionRequest(in request);
                if (_deconstructionRaycastScheduled)
                    break;
            }
        }

        private void ProcessDeconstructionRequest(in DeconstructRequestSignal request)
        {
            BaseModule module = ResolveBaseModuleByEntityId(request.TargetEntityId);
            if (module == null)
            {
                RejectDeconstruction(in request, DeconstructReasonNoTarget, 0, 0, 0);
                return;
            }

            if (!ValidateAupTarget(in request, module))
            {
                DumpDeconstructionBlackBox();
                RejectDeconstruction(in request, DeconstructReasonNoTarget, 0, 0, 0);
                return;
            }

            if (request.MaxDistance > 0f)
            {
                if (!TryScheduleDeferredDeconstructionRaycast(in request, module))
                    RejectDeconstruction(in request, DeconstructReasonRayMismatch, 0, 0, 0);
                return;
            }

            ProcessDeconstructionRequestAfterRayValidated(in request, module);
        }

        private void ProcessDeconstructionRequestAfterRayValidated(in DeconstructRequestSignal request, BaseModule module)
        {
            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null || !pool.CanDespawnWithoutDestroy(module.gameObject))
            {
                RejectDeconstruction(in request, DeconstructReasonPoolUnavailable, 0, 0, 0);
                return;
            }

            if (_habitatGraphDirty)
                RefreshHabitatGraph();

            const bool skipDfs = false;
            EnsureDeconstructionNativeBuffers(Mathf.Max(initialCapacity, ModuleCount));
            if (_habitatGraphManager != null &&
                !_habitatGraphManager.TryValidateDeconstructionRollback(
                    module,
                    _deconstructionDfsStack,
                    _deconstructionDfsVisited,
                    _deconstructionDfsResult,
                    out byte graphRejectReason))
            {
                RejectDeconstruction(in request, DeconstructReasonGraphRejected, graphRejectReason, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                return;
            }

            PlayerInventory inventory = ResolvePlayerInventory();
            BuildableData buildData = ResolveBuildData(module);
            if (!CanRefundToInventory(buildData, inventory, out ushort refundItemCount))
            {
                RejectDeconstruction(in request, DeconstructReasonInventoryFull, 0, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                PublishDeconstructionHudNotification(request.TargetEntityId, DeconstructReasonInventoryFull);
                return;
            }

            if (!module.TryBeginAuthoritativeDeconstruction())
            {
                RejectDeconstruction(in request, DeconstructReasonAlreadyActive, 0, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                return;
            }

            if (!ApplyInventoryRefund(in request, buildData, inventory, out refundItemCount))
            {
                module.CancelAuthoritativeDeconstruction();
                RejectDeconstruction(in request, DeconstructReasonInventoryFull, 0, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                PublishDeconstructionHudNotification(request.TargetEntityId, DeconstructReasonInventoryFull);
                return;
            }

            uint moduleHash = unchecked((uint)ResolveModuleHashId(module));
            ushort nodeId = (ushort)Mathf.Clamp(ResolveRegisteredModuleIndex(module.gameObject), 0, ushort.MaxValue);

            PublishDeconstructionVfx(in request);
            module.EjectHostedContentsForDeconstruction(inventory, pool);
            module.PrepareForDeconstructionPoolReturn();
            UnregisterModule(module.gameObject);
            PublishModuleDeconstructSignal(moduleHash, nodeId, in request, skipDfs);
            pool.Despawn(module.gameObject);
            AcceptDeconstruction(in request, refundItemCount, skipDfs);
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

        private void PublishHabitatConstructionSignal(GameObject module, byte operation, byte flags)
        {
            if (module == null || !Application.isPlaying)
                return;

            uint moduleHash = 0u;
            if (module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                moduleHash = unchecked((uint)marker.Data.ModuleHashId);
            }

            int moduleIndex = ResolveRegisteredModuleIndex(module);
            AbsoluteUniversePosition positionAup = TryResolveAupFromRuntimeOrigin(module.transform.position, out AbsoluteUniversePosition resolvedPositionAup)
                ? resolvedPositionAup
                : GlobalSignals.CurrentRuntimeOriginAup();
            GlobalSignals.Publish(new HabitatConstructionSignal
            {
                PositionAup = positionAup,
                ModuleHash = moduleHash,
                GraphId = (uint)Mathf.Max(0, _habitatGraphManager != null ? _habitatGraphManager.NodeCount : 0),
                NodeId = (ushort)Mathf.Clamp(moduleIndex, 0, ushort.MaxValue),
                Operation = operation,
                Flags = flags
            });
        }

        private static void PublishModuleDeconstructSignal(
            uint moduleHash,
            ushort nodeId,
            in DeconstructRequestSignal request,
            bool dfsSkipped)
        {
            if (!Application.isPlaying)
                return;

            byte flags = ModuleDeconstructFlagForcePowerColdTick;
            if (dfsSkipped)
                flags |= ModuleDeconstructFlagDfsSkippedLowTier;

            GlobalSignals.Publish(new ModuleDeconstructSignal
            {
                PositionAup = request.TargetAup,
                ModuleHash = moduleHash,
                TargetEntityId = request.TargetEntityId,
                NodeId = nodeId,
                Operation = ModuleDeconstructOperationDeleteMarker,
                Flags = flags,
                Frame = (uint)Mathf.Max(0, Time.frameCount)
            });
        }

        private void PublishDeconstructionVfx(in DeconstructRequestSignal request)
        {
            GlobalSignals.Publish(new DebrisSpawnSignal
            {
                PositionAup = request.TargetAup,
                SpeciesHash = 0u,
                SourceEntityId = request.TargetEntityId,
                Intensity01 = 1f,
                DebrisKind = DeconstructionDebrisKindDisintegrate,
                Flags = 0
            });
        }

        private void AcceptDeconstruction(in DeconstructRequestSignal request, ushort refundItemCount, bool dfsSkipped)
        {
            byte reason = dfsSkipped ? ModuleDeconstructFlagDfsSkippedLowTier : DeconstructReasonNone;
            PublishDeconstructionResult(in request, DeconstructResultAccepted, reason, refundItemCount);
            WriteDeconstructionBlackBoxSample(in request, DeconstructResultAccepted, reason, ReadDfsVisitedCount(), ReadDfsExpectedCount());
        }

        private void RejectDeconstruction(
            in DeconstructRequestSignal request,
            byte reason,
            int graphDetail,
            int visitedCount,
            int expectedCount)
        {
            PublishDeconstructionResult(in request, DeconstructResultRejected, reason, 0);
            WriteDeconstructionBlackBoxSample(in request, DeconstructResultRejected, reason, visitedCount, expectedCount + graphDetail);
        }

        private static void PublishDeconstructionResult(
            in DeconstructRequestSignal request,
            byte result,
            byte reason,
            ushort refundItemCount)
        {
            GlobalSignals.Publish(new DeconstructResultSignal
            {
                TargetAup = request.TargetAup,
                TargetEntityId = request.TargetEntityId,
                RequesterEntityId = request.RequesterEntityId,
                RefundItemCount = refundItemCount,
                Result = result,
                Reason = reason,
                Frame = (uint)Mathf.Max(0, Time.frameCount)
            });
        }

        private static void PublishDeconstructionHudNotification(uint sourceId, byte reason)
        {
            GlobalSignals.Publish(new HUDNotificationSignal
            {
                MessageHash = 0xD3C04A11u,
                ContextHash = reason,
                SourceId = sourceId,
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                Severity = 2,
                Flags = 0
            });
        }

        private BaseModule ResolveBaseModuleByEntityId(uint targetEntityId)
        {
            if (targetEntityId == 0u || _spawnedBaseModules == null)
                return null;

            int count = _spawnedBaseModules.Count;
            for (int i = 0; i < count; i++)
            {
                BaseModule module = _spawnedBaseModules[i];
                if (module == null)
                    continue;

                uint entityId = unchecked((uint)EntityId.ToULong(module.GetEntityId()));
                if (entityId == targetEntityId)
                    return module;
            }

            return null;
        }

        private static bool ValidateAupTarget(in DeconstructRequestSignal request, BaseModule module)
        {
            if (module == null)
                return false;

            float3 targetRuntime = request.TargetAup.ToRuntimeFloat3();
            Vector3 modulePosition = module.transform.position;
            float3 moduleRuntime = new float3(modulePosition.x, modulePosition.y, modulePosition.z);
            if (!math.all(math.isfinite(targetRuntime)) || !math.all(math.isfinite(moduleRuntime)))
                return false;

            float distanceSq = math.lengthsq(targetRuntime - moduleRuntime);
            return distanceSq <= 9f;
        }

        private bool TryScheduleDeferredDeconstructionRaycast(in DeconstructRequestSignal request, BaseModule module)
        {
            if (module == null || _deconstructionRaycastScheduled)
                return false;

            if (!_deconstructionRaycastCommands.IsCreated || !_deconstructionRaycastHits.IsCreated)
                EnsureDeconstructionNativeBuffers(Mathf.Max(initialCapacity, ModuleCount));

            if (!_deconstructionRaycastCommands.IsCreated ||
                !_deconstructionRaycastHits.IsCreated ||
                !TryCreateDeferredDeconstructionRaycastCommand(in request, out RaycastCommand command))
            {
                return false;
            }

            _deconstructionRaycastCommands[0] = command;
            _deconstructionRaycastHits[0] = default;
            _pendingDeconstructionRequest = request;
            _pendingDeconstructionModule = module;
            _deconstructionRaycastHandle = RaycastCommand.ScheduleBatch(
                _deconstructionRaycastCommands,
                _deconstructionRaycastHits,
                DeconstructionRaycastCapacity,
                default);
            _deconstructionRaycastScheduled = true;
            return true;
        }

        private static bool TryCreateDeferredDeconstructionRaycastCommand(
            in DeconstructRequestSignal request,
            out RaycastCommand command)
        {
            command = default;
            float3 direction = request.RayDirection;
            float directionLengthSq = math.lengthsq(direction);
            if (!math.all(math.isfinite(direction)) || directionLengthSq <= 0.0001f)
                return false;

            direction *= math.rsqrt(math.max(directionLengthSq, 0.0001f));
            float3 origin3 = request.RayOriginAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(origin3)))
                return false;

            command = new RaycastCommand
            {
                from = new Vector3(origin3.x, origin3.y, origin3.z),
                direction = new Vector3(direction.x, direction.y, direction.z),
                distance = math.max(0.001f, request.MaxDistance),
                queryParameters = new QueryParameters
                {
                    layerMask = HectonLayerMasks.ConstructionSurfaceLayerMask,
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    hitBackfaces = false,
                    hitMultipleFaces = false
                }
            };
            return true;
        }

        private void FinalizeDeferredDeconstructionRaycast()
        {
            if (!_deconstructionRaycastScheduled ||
                !DispatcherJobSwap.TryFinalizeCompleted(ref _deconstructionRaycastHandle))
            {
                return;
            }

            _deconstructionRaycastScheduled = false;
            DeconstructRequestSignal request = _pendingDeconstructionRequest;
            BaseModule module = _pendingDeconstructionModule;
            _pendingDeconstructionRequest = default;
            _pendingDeconstructionModule = null;

            if (module == null || !ReferenceEquals(module, ResolveBaseModuleByEntityId(request.TargetEntityId)))
            {
                RejectDeconstruction(in request, DeconstructReasonNoTarget, 0, 0, 0);
                return;
            }

            if (!IsDeferredDeconstructionRaycastOwnedBy(module))
            {
                RejectDeconstruction(in request, DeconstructReasonRayMismatch, 0, 0, 0);
                return;
            }

            ProcessDeconstructionRequestAfterRayValidated(in request, module);
        }

        private bool IsDeferredDeconstructionRaycastOwnedBy(BaseModule module)
        {
            if (!_deconstructionRaycastHits.IsCreated || _deconstructionRaycastHits.Length == 0 || module == null)
                return false;

            Collider hitCollider = _deconstructionRaycastHits[0].collider;
            if (hitCollider == null)
                return false;

            BaseModule hitModule = hitCollider.GetComponentInParent<BaseModule>();
            return ReferenceEquals(hitModule, module);
        }

        private static PlayerInventory ResolvePlayerInventory()
        {
            IPlayerInventoryService inventoryService = GlobalRegistry.PlayerInventory;
            return inventoryService != null ? inventoryService.Inventory : null;
        }

        private BuildableData ResolveBuildData(BaseModule module)
        {
            if (module == null)
                return null;

            if (module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                return marker.Data;
            }

            int moduleHashId = ResolveModuleHashId(module);
            if (moduleHashId == 0 && module.ModuleTemplate != null)
                moduleHashId = Hecton.Localization.LocHash.Compute(module.ModuleTemplate.PersistentId);

            return catalog != null ? catalog.FindDataByHashId(moduleHashId) : null;
        }

        private static bool CanRefundToInventory(
            BuildableData buildData,
            PlayerInventory inventory,
            out ushort refundItemCount)
        {
            refundItemCount = 0;
            List<InventoryCost> buildCost = buildData != null ? buildData.buildCost : null;
            if (buildCost == null || buildCost.Count == 0)
                return true;

            if (inventory == null || inventory.Grid == null)
                return false;

            int costCount = buildCost.Count;
            Span<int> refundItemHashIds = stackalloc int[costCount];
            Span<int> refundQuantities = stackalloc int[costCount];
            int groupCount = BuildRefundGroups(buildCost, refundItemHashIds, refundQuantities, out int totalRefund);
            if (groupCount < 0)
                return false;

            if (groupCount > 0 && !inventory.CanAcceptItemQuantityBatch(refundItemHashIds, refundQuantities, groupCount))
                return false;

            refundItemCount = (ushort)totalRefund;
            return true;
        }

        private static bool ApplyInventoryRefund(
            in DeconstructRequestSignal request,
            BuildableData buildData,
            PlayerInventory inventory,
            out ushort refundItemCount)
        {
            refundItemCount = 0;
            List<InventoryCost> buildCost = buildData != null ? buildData.buildCost : null;
            if (buildCost == null || buildCost.Count == 0)
                return true;

            if (inventory == null || inventory.Grid == null)
                return false;

            int costCount = buildCost.Count;
            Span<int> refundItemHashIds = stackalloc int[costCount];
            Span<int> refundQuantities = stackalloc int[costCount];
            int groupCount = BuildRefundGroups(buildCost, refundItemHashIds, refundQuantities, out int totalRefund);
            if (groupCount < 0)
                return false;

            for (int i = 0; i < groupCount; i++)
            {
                int itemHashId = refundItemHashIds[i];
                int refundAmount = refundQuantities[i];
                if (!inventory.TryAddItem(itemHashId, refundAmount))
                    return false;

                ushort publishedQuantity = (ushort)Mathf.Clamp(refundAmount, 0, ushort.MaxValue);
                GlobalSignals.Publish(new ItemAcquiredSignal
                {
                    PositionAup = request.TargetAup,
                    ItemHash = unchecked((uint)itemHashId),
                    OreHash = 0u,
                    Quantity = publishedQuantity,
                    SourceKind = 4,
                    Flags = 0,
                    Frame = (uint)Mathf.Max(0, Time.frameCount)
                });

            }

            refundItemCount = (ushort)totalRefund;
            return true;
        }

        private static int BuildRefundGroups(
            List<InventoryCost> buildCost,
            Span<int> itemHashIds,
            Span<int> quantities,
            out int totalRefund)
        {
            totalRefund = 0;
            int groupCount = 0;
            int costCount = buildCost != null ? buildCost.Count : 0;
            if (itemHashIds.Length < costCount || quantities.Length < costCount)
                return -1;

            itemHashIds.Slice(0, costCount).Clear();
            quantities.Slice(0, costCount).Clear();

            for (int i = 0; i < costCount; i++)
            {
                InventoryCost cost = buildCost[i];
                int itemHashId = ResolveRefundCostItemHash(cost);
                if (itemHashId == 0)
                    continue;

                int refundAmount = Mathf.Max(0, cost.amount) >> 1;
                if (refundAmount <= 0)
                    continue;

                int groupIndex = FindRefundGroupIndex(itemHashIds, groupCount, itemHashId);
                if (groupIndex < 0)
                {
                    groupIndex = groupCount++;
                    itemHashIds[groupIndex] = itemHashId;
                }

                quantities[groupIndex] = Mathf.Min(ushort.MaxValue, quantities[groupIndex] + refundAmount);
                totalRefund = Mathf.Min(ushort.MaxValue, totalRefund + refundAmount);
            }

            return groupCount;
        }

        private static int ResolveRefundCostItemHash(InventoryCost cost)
        {
            if (cost == null || cost.item == null)
                return 0;

            return Hecton.Localization.LocHash.Compute(cost.item.PersistentId);
        }

        private static int FindRefundGroupIndex(Span<int> itemHashIds, int groupCount, int itemHashId)
        {
            for (int i = 0; i < groupCount; i++)
            {
                if (itemHashIds[i] == itemHashId)
                    return i;
            }

            return -1;
        }

        private void EnsureDeconstructionNativeBuffers(int requestedCapacity)
        {
            int capacity = Mathf.Max(1, requestedCapacity);
            if (!_deconstructionDfsStack.IsCreated)
            {
                _deconstructionDfsStack = new NativeList<long>(capacity, DataVaultExemptSceneScratchAllocator); // COLD ALLOC: NativeList<long>[module capacity] - rollback DFS stack - owner: ConstructionManager
                NativeMemorySentinel.RegisterNativeList(_deconstructionDfsStack, NativeMemoryOwner, nameof(_deconstructionDfsStack), NativeMemoryLifetime);
            }
            else if (_deconstructionDfsStack.Capacity < capacity)
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_deconstructionDfsStack));
                _deconstructionDfsStack.Capacity = capacity;
                NativeMemorySentinel.RegisterNativeList(_deconstructionDfsStack, NativeMemoryOwner, nameof(_deconstructionDfsStack), NativeMemoryLifetime);
            }

            if (!_deconstructionDfsVisited.IsCreated)
            {
                _deconstructionDfsVisited = new NativeParallelHashSet<long>(capacity, Allocator.Persistent); // COLD ALLOC: NativeParallelHashSet<long>[module capacity] - rollback DFS visited set - owner: ConstructionManager
                NativeMemorySentinel.RegisterNativeParallelHashSet(
                    _deconstructionDfsVisited,
                    NativeMemoryOwner,
                    nameof(_deconstructionDfsVisited),
                    NativeMemoryLifetime);
            }
            else if (_deconstructionDfsVisited.Capacity < capacity)
            {
                _deconstructionDfsVisited.Capacity = capacity;
                NativeMemorySentinel.RefreshNativeParallelHashSet(_deconstructionDfsVisited, NativeMemoryOwner, nameof(_deconstructionDfsVisited));
            }

            if (!_deconstructionDfsResult.IsCreated)
            {
                _deconstructionDfsResult = new NativeArray<int>(DeconstructionDfsResultLength, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[4] - rollback DFS result lane - owner: ConstructionManager
                NativeMemorySentinel.RegisterNativeArray(_deconstructionDfsResult, NativeMemoryOwner, nameof(_deconstructionDfsResult), NativeMemoryLifetime);
            }

            if (!_deconstructionBlackBox.IsCreated)
            {
                _deconstructionBlackBox = new NativeArray<HabitatDeconstructionTelemetryEntry>(
                    DeconstructionBlackBoxCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<HabitatDeconstructionTelemetryEntry>[300] - deconstruction black box - owner: ConstructionManager
                NativeMemorySentinel.RegisterNativeArray(_deconstructionBlackBox, NativeMemoryOwner, nameof(_deconstructionBlackBox), NativeMemoryLifetime);
            }

            if (!_deconstructionRaycastCommands.IsCreated)
            {
                _deconstructionRaycastCommands = new NativeArray<RaycastCommand>(
                    DeconstructionRaycastCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[1] - deferred deconstruction ownership query - owner: ConstructionManager
                NativeMemorySentinel.RegisterNativeArray(_deconstructionRaycastCommands, NativeMemoryOwner, nameof(_deconstructionRaycastCommands), NativeMemoryLifetime);
            }

            if (!_deconstructionRaycastHits.IsCreated)
            {
                _deconstructionRaycastHits = new NativeArray<RaycastHit>(
                    DeconstructionRaycastCapacity,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[1] - deferred deconstruction ownership result - owner: ConstructionManager
                NativeMemorySentinel.RegisterNativeArray(_deconstructionRaycastHits, NativeMemoryOwner, nameof(_deconstructionRaycastHits), NativeMemoryLifetime);
            }
        }

        private void DisposeDeconstructionNativeBuffers()
        {
            CompleteDeferredDeconstructionRaycastForTeardown();

            if (_deconstructionDfsStack.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, nameof(_deconstructionDfsStack));
                _deconstructionDfsStack.Dispose();
                _deconstructionDfsStack = default;
            }

            if (_deconstructionDfsVisited.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(NativeMemoryOwner, nameof(_deconstructionDfsVisited));
                _deconstructionDfsVisited.Dispose();
                _deconstructionDfsVisited = default;
            }

            if (_deconstructionDfsResult.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_deconstructionDfsResult);
                _deconstructionDfsResult.Dispose();
                _deconstructionDfsResult = default;
            }

            if (_deconstructionBlackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_deconstructionBlackBox);
                _deconstructionBlackBox.Dispose();
                _deconstructionBlackBox = default;
            }

            if (_deconstructionRaycastCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_deconstructionRaycastCommands);
                _deconstructionRaycastCommands.Dispose();
                _deconstructionRaycastCommands = default;
            }

            if (_deconstructionRaycastHits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_deconstructionRaycastHits);
                _deconstructionRaycastHits.Dispose();
                _deconstructionRaycastHits = default;
            }

            _deconstructionBlackBoxCursor = 0;
            _pendingDeconstructionRequest = default;
            _pendingDeconstructionModule = null;
        }

        private void CompleteDeferredDeconstructionRaycastForTeardown()
        {
            if (!_deconstructionRaycastScheduled)
                return;

            DispatcherJobFence.TryComplete(ref _deconstructionRaycastHandle, forceComplete: true);
            _deconstructionRaycastScheduled = false;
            _pendingDeconstructionRequest = default;
            _pendingDeconstructionModule = null;
        }

        private int ReadDfsVisitedCount()
        {
            return _deconstructionDfsResult.IsCreated && _deconstructionDfsResult.Length > 1
                ? _deconstructionDfsResult[1]
                : 0;
        }

        private int ReadDfsExpectedCount()
        {
            return _deconstructionDfsResult.IsCreated && _deconstructionDfsResult.Length > 2
                ? _deconstructionDfsResult[2]
                : 0;
        }

        private void WriteDeconstructionBlackBoxSample(
            in DeconstructRequestSignal request,
            byte result,
            byte reason,
            int visitedCount,
            int expectedCount)
        {
            if (!_deconstructionBlackBox.IsCreated || _deconstructionBlackBox.Length == 0)
                return;

            int index = _deconstructionBlackBoxCursor;
            if (index < 0 || index >= _deconstructionBlackBox.Length)
                index = 0;

            _deconstructionBlackBox[index] = new HabitatDeconstructionTelemetryEntry
            {
                Frame = (uint)Mathf.Max(0, Time.frameCount),
                TargetEntityId = request.TargetEntityId,
                RequesterEntityId = request.RequesterEntityId,
                DistanceMeters = Mathf.Max(0f, request.MaxDistance),
                DfsVisitedCount = (ushort)Mathf.Clamp(visitedCount, 0, ushort.MaxValue),
                DfsExpectedCount = (ushort)Mathf.Clamp(expectedCount, 0, ushort.MaxValue),
                Result = result,
                Reason = reason,
                Flags = request.Flags,
                Reserved = 0
            };

            _deconstructionBlackBoxCursor = index + 1;
            if (_deconstructionBlackBoxCursor >= _deconstructionBlackBox.Length)
                _deconstructionBlackBoxCursor = 0;
        }

        private void DumpDeconstructionBlackBox()
        {
            if (!_deconstructionBlackBox.IsCreated)
                return;

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DeconstructionDumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(DeconstructionBlackBoxCapacity);
                writer.Write(_deconstructionBlackBoxCursor);
                for (int i = 0; i < _deconstructionBlackBox.Length; i++)
                {
                    HabitatDeconstructionTelemetryEntry entry = _deconstructionBlackBox[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.TargetEntityId);
                    writer.Write(entry.RequesterEntityId);
                    writer.Write(entry.DistanceMeters);
                    writer.Write(entry.DfsVisitedCount);
                    writer.Write(entry.DfsExpectedCount);
                    writer.Write(entry.Result);
                    writer.Write(entry.Reason);
                    writer.Write(entry.Flags);
                    writer.Write(entry.Reserved);
                }
            }
        }

        private int ResolveRegisteredModuleIndex(GameObject module)
        {
            if (module == null || _spawnedModules == null)
                return 0;

            int count = _spawnedModules.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(_spawnedModules[i], module))
                    return i;
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
            dto.moduleBlitCount = 0;

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
                graphNodeDto.SetAup(TryResolveAupFromRuntimeOrigin(t.position, out AbsoluteUniversePosition moduleAup)
                    ? moduleAup
                    : GlobalSignals.CurrentRuntimeOriginAup());
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
                    moduleDto.health = PackHealthByte(baseModule.CurrentIntegrity, baseModule.MaxIntegrity);
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
                    moduleDto.health = byte.MaxValue;
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
                dto.moduleBlitRecords[moduleIndex] = BuildModuleBlitRecord(moduleDto, graphNodeDto);
                moduleIndex++;
            }

            dto.moduleCount = moduleIndex;
            dto.graphNodeCount = moduleIndex;
            dto.moduleBlitCount = moduleIndex;
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
                float3 graphRuntimePosition = hasGraphTopology ? graphNodeDto.GetAup().ToRuntimeFloat3() : float3.zero;
                Vector3 pos = hasGraphTopology
                    ? new Vector3(graphRuntimePosition.x, graphRuntimePosition.y, graphRuntimePosition.z)
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

        private static ModuleBlitDTO BuildModuleBlitRecord(in ModuleDTO moduleDto, in ModuleGraphNodeDTO graphNodeDto)
        {
            byte flags = 0;
            if (moduleDto.isFlooded)
                flags |= ModuleBlitFlagFlooded;
            if (moduleDto.interiorReefInfestationActive)
                flags |= ModuleBlitFlagInteriorReef;

            return new ModuleBlitDTO
            {
                prefabHashId = HashStableString(moduleDto.prefabId),
                moduleHashId = graphNodeDto.moduleHashId,
                aupGridX = graphNodeDto.aupGridX,
                aupGridY = graphNodeDto.aupGridY,
                aupGridZ = graphNodeDto.aupGridZ,
                aupLocalX = graphNodeDto.aupLocalX,
                aupLocalY = graphNodeDto.aupLocalY,
                aupLocalZ = graphNodeDto.aupLocalZ,
                rotX = graphNodeDto.rotX,
                rotY = graphNodeDto.rotY,
                rotZ = graphNodeDto.rotZ,
                rotW = graphNodeDto.rotW,
                health = moduleDto.health,
                flags = flags,
                failureMode = moduleDto.failureMode,
                reserved = 0
            };
        }

        private static byte PackHealthByte(float currentIntegrity, float maxIntegrity)
        {
            if (!float.IsFinite(currentIntegrity) || currentIntegrity <= 0f)
                return 0;

            float safeMaxIntegrity = float.IsFinite(maxIntegrity) && maxIntegrity > 0.001f
                ? maxIntegrity
                : DefaultIntegrity;
            int packed = Mathf.RoundToInt(Mathf.Clamp01(currentIntegrity / safeMaxIntegrity) * 255f);
            return (byte)Mathf.Clamp(packed, 0, 255);
        }

        private static int HashStableString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0;

            unchecked
            {
                int hash = (int)2166136261u;
                for (int i = 0; i < value.Length; i++)
                    hash = (hash ^ value[i]) * 16777619;

                return hash;
            }
        }

        /// <summary>
        /// Checks whether the module reference is already registered. O(n), but only
        /// called by Register. Zero GC.
        /// </summary>
        private void PopulateGraphEdges(ref ConstructionDTO dto, int savedNodeCount)
        {
            dto.graphEdgeCount = 0;
            if (_habitatGraphManager == null || savedNodeCount <= 0 || _habitatGraphManager.NodeCount != savedNodeCount)
                return;

            NativeArray<int>.ReadOnly edgeOffsets = _habitatGraphManager.EdgeOffsets;
            NativeArray<int>.ReadOnly edgeDestinations = _habitatGraphManager.EdgeDestinations;
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

            if (module.GetComponent("ConstructionRuntimeProxyTag") != null)
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

        private void TryRegisterHabitatGraphService()
        {
            if (_habitatGraphServiceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterHabitatGraphService(this);
            _habitatGraphServiceRegistered = ReferenceEquals(GlobalRegistry.HabitatGraph, this);
        }

        private void TryRegisterHabitatDeconstructionService()
        {
            if (_habitatDeconstructionServiceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterHabitatDeconstructionSystem(this);
            _habitatDeconstructionServiceRegistered = ReferenceEquals(GlobalRegistry.HabitatDeconstruction, this);
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

        private void TryUnregisterHabitatGraphService()
        {
            if (!_habitatGraphServiceRegistered)
                return;

            GlobalRegistry.UnregisterHabitatGraphService(this);
            _habitatGraphServiceRegistered = false;
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
            _hotSwapListenerRegistered = GlobalRegistry.IsHotSwapListenerRegistered(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            if (GlobalRegistry.IsHotSwapListenerRegistered(this))
                GlobalRegistry.UnregisterHotSwapListener(this);

            _hotSwapListenerRegistered = false;
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_originShiftListenerRegistered || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _originShiftListenerRegistered = HectonFloatingOrigin.IsListenerRegistered(this);
        }

        private void TryUnregisterOriginShiftListener()
        {
            if (!_originShiftListenerRegistered)
                return;

            HectonFloatingOrigin.UnregisterListener(this);
            _originShiftListenerRegistered = false;
        }

        private void TryRegisterRandomEventListener()
        {
            if (_randomEventListenerRegistered || !Application.isPlaying)
                return;

            RandomEventEvents.Register(this);
            _randomEventListenerRegistered = true;
        }

        private void TryUnregisterRandomEventListener()
        {
            if (!_randomEventListenerRegistered)
                return;

            RandomEventEvents.Unregister(this);
            _randomEventListenerRegistered = false;
        }

        private void TryUnregisterLogisticsService()
        {
            if (!_logisticsServiceRegistered)
                return;

            GlobalRegistry.UnregisterLogisticsService(this);
            _logisticsServiceRegistered = false;
        }

        private void TryUnregisterHabitatDeconstructionService()
        {
            if (!_habitatDeconstructionServiceRegistered)
                return;

            GlobalRegistry.UnregisterHabitatDeconstructionSystem(this);
            _habitatDeconstructionServiceRegistered = false;
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            BaseDegradationSystem.ApplyOriginShift(in shiftData);
            DroneFleetManager.ApplyOriginShift(shiftData.ShiftOffset);
            RecoverHabitatJointsAfterOriginShift(in shiftData);
        }

        public void OnRandomEventStarted(RandomEventType type, float intensity)
        {
        }

        public void OnRandomEventEnded(RandomEventType type)
        {
        }

        public void OnSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            _habitatGraphManager?.RegisterSeismicVibration(
                payload.EpicenterWS,
                payload.ImpulseRadiusMeters,
                payload.ImpulseMagnitude);
        }

        private void RecoverHabitatJointsAfterOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            if (!IsFiniteVector(shiftOffset) || shiftOffset.sqrMagnitude <= 0.000001f)
                return;

            EnsureRuntimeStorage();
            PurgeNullEntries();

            int capturedBodyCount = 0;
            bool capacityOverflow = false;
            int moduleCount = _spawnedModules.Count;
            for (int moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
            {
                GameObject module = _spawnedModules[moduleIndex];
                if (module == null)
                    continue;

                _jointRecoveryBuffer.Clear();
                module.GetComponentsInChildren(true, _jointRecoveryBuffer);
                int jointCount = _jointRecoveryBuffer.Count;
                for (int jointIndex = 0; jointIndex < jointCount; jointIndex++)
                {
                    Joint joint = _jointRecoveryBuffer[jointIndex];
                    if (joint == null)
                        continue;

                    if (joint.TryGetComponent(out Rigidbody ownerBody))
                        capacityOverflow |= !TryCaptureJointBodyVelocity(ownerBody, ref capturedBodyCount);
                    capacityOverflow |= !TryCaptureJointBodyVelocity(joint.connectedBody, ref capturedBodyCount);

                    if (joint.connectedBody == null && !joint.autoConfigureConnectedAnchor)
                        joint.connectedAnchor -= shiftOffset;
                }
            }

            RestoreCapturedJointBodyVelocities(capturedBodyCount);
            UnityEngine.Physics.SyncTransforms();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (capacityOverflow)
                Debug.LogWarning("[ConstructionManager] AUP joint recovery body cache exhausted; increase initialCapacity.", this);
#endif
        }

        private bool TryCaptureJointBodyVelocity(Rigidbody body, ref int capturedBodyCount)
        {
            if (body == null)
                return true;

            for (int i = 0; i < capturedBodyCount; i++)
            {
                if (ReferenceEquals(_jointRecoveryBodies[i], body))
                    return true;
            }

            if (_jointRecoveryBodies == null || capturedBodyCount >= _jointRecoveryBodies.Length)
                return false;

            _jointRecoveryBodies[capturedBodyCount] = body;
            _jointRecoveryLinearVelocities[capturedBodyCount] = body.linearVelocity;
            _jointRecoveryAngularVelocities[capturedBodyCount] = body.angularVelocity;
            capturedBodyCount++;
            return true;
        }

        private void RestoreCapturedJointBodyVelocities(int capturedBodyCount)
        {
            for (int i = 0; i < capturedBodyCount; i++)
            {
                Rigidbody body = _jointRecoveryBodies[i];
                if (body != null)
                {
                    body.linearVelocity = _jointRecoveryLinearVelocities[i];
                    body.angularVelocity = _jointRecoveryAngularVelocities[i];
                }

                _jointRecoveryBodies[i] = null;
                _jointRecoveryLinearVelocities[i] = Vector3.zero;
                _jointRecoveryAngularVelocities[i] = Vector3.zero;
            }
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFiniteVector(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            double3 localDelta = new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            positionAup = AbsoluteUniversePosition.OffsetMeters(in originAup, localDelta);
            return positionAup.IsFinite();
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
