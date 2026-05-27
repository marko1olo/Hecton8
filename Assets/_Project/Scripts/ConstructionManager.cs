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
using Hecton8.Core.Memory;
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
    public sealed class ConstructionManager : MonoBehaviour, IUpdatable, ILateFrameTickable, ISaveable, ISlowTickable, ILogisticsService, IHabitatGraphService, IConstructionParasiteGraphService, IHabitatDeconstructionSystem, IGlobalRegistryHotSwapListener, IServiceHeartbeat, IServiceShutdown, IOriginShiftListener, IRandomEventListener
    {
        private static int _signalPushDropCount;
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
        private const int DeconstructionBlackBoxCapacity = 300;
        private const int DeconstructionTransactionCapacity = HabitatDeconstructionTransactionKernel.MaxTeardownsUltra;
        private const int DeconstructionRefundCommandCapacity = DeconstructionTransactionCapacity * HabitatDeconstructionTransactionKernel.MaxCostPairs;
        private const int DeconstructionLootCacheCapacity = DeconstructionRefundCommandCapacity;
        private const string DeconstructionDumpRelativePath = "Docs/AgentLogs/Dump_1306_Construction_DeconstructionBlackBox.bin";
        private const string Shinobu336DumpRelativePath = "Docs/AgentLogs/Dump_1306_Construction_DeconstructionTelemetry.bin";
        private const int DeconstructionCounterLaneLength = 2;
        private const int DeconstructionRefundCommandCountIndex = 0;
        private const int DeconstructionLootCacheCountIndex = 1;
        private const BufferID DeconstructionDfsStackBufferId = (BufferID)72140;
        private const BufferID DeconstructionDfsVisitedBufferId = (BufferID)72141;
        private const BufferID DeconstructionDfsResultBufferId = (BufferID)72142;
        private const BufferID DeconstructionBlackBoxBufferId = (BufferID)72143;
        private const BufferID DeconstructionFallbackCostsBufferId = (BufferID)72144;

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
            BaseDegradationSystem.BindRuntimeServices(null, null);
        }
        // SERVICE STATE

        // INSPECTOR

        [Header("Catalog")]
        [Tooltip("Catalog of buildable base modules. Used to resolve prefabs by ID during load.")]
        [SerializeField] private ModuleCatalog catalog;

        [Header("Settings")]
        [Tooltip("Fixed placed-module registry capacity. Increase before runtime for larger bases.")]
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
        private ISaveService _cachedSaveService;
        private ISaveService _registeredSaveService;
        private IObjectPoolService _cachedObjectPool;
        private IPlayerInventoryService _cachedPlayerInventoryService;
        private IDataVault _cachedDataVault;
        private IGasDynamicsSolver _cachedGasDynamics;
        private IAtmosphereReadModel _cachedAtmosphereReadModel;
        private IAmbientCurrentReadModel _cachedAmbientCurrentReadModel;
        private IAudioService _cachedAudioService;
        private IFluidDecalPresentationSink _cachedFluidDecalPresentation;
        private IPhysicsService _cachedPhysicsService;
        private bool _isInitialized;
        private bool _habitatGraphDirty;
        private float _slowTickAccumulator;
        private float _ambientAccidentTimer;
        private int _ambientAccidentCursor;
        private Transform[] _jointRecoveryTransformStack;
        private Rigidbody[] _jointRecoveryBodies;
        private Vector3[] _jointRecoveryLinearVelocities;
        private Vector3[] _jointRecoveryAngularVelocities;
        private VaultGenerationHandle<int> _deconstructionDfsStackHandle;
        private VaultGenerationHandle<byte> _deconstructionDfsVisitedHandle;
        private VaultGenerationHandle<int> _deconstructionDfsResultHandle;
        private VaultGenerationHandle<HabitatDeconstructionTelemetryEntry> _deconstructionBlackBoxHandle;
        private VaultGenerationHandle<DeconstructionTransactionDTO> _deconstructionTransactionsHandle;
        private VaultGenerationHandle<ModuleCostDTO> _deconstructionFallbackCostsHandle;
        private VaultGenerationHandle<RefundCommandDTO> _deconstructionRefundCommandsHandle;
        private VaultGenerationHandle<LootCacheDTO> _deconstructionLootCachesHandle;
        private VaultGenerationHandle<int> _deconstructionCountersHandle;
        private VaultGenerationHandle<TeardownTelemetryEntry> _deconstructionTelemetryHandle;
        private VaultGenerationHandle<int> _deconstructionTelemetryCursorHandle;
        private VaultGenerationHandle<RefundProfileDTO> _deconstructionRefundProfilesHandle;
#if UNITY_EDITOR
        private VaultGenerationHandle<byte> _deconstructionCsvScratchHandle;
#endif
        private int _deconstructionBlackBoxCursor;
        private int _lastShinobu336RefundedResources;
        private int _lastShinobu336OverflowCaches;
        private int _lastShinobu336SeveredEdges;
        private int _lastShinobu336NodeIndex;
        private int _lastDeconstructionDfsVisitedCount;
        private int _lastDeconstructionDfsExpectedCount;
        private float _lastShinobu336BurstMicroseconds;
        private Vector3 _lastShinobu336TargetRuntimePosition;
        private uint _deconstructionSequence;
        private uint _lastShinobu336StateHash;
        private uint _lastShinobu336FaultFlags;

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

        public bool TryGetHabitatAcousticGraph(out HabitatGraphManager graph)
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

#if UNITY_EDITOR
        public static bool TryReadShinobu336EditorState(
            out int refundedResources,
            out int overflowCaches,
            out int severedEdges,
            out int targetNodeIndex,
            out Vector3 targetRuntimePosition,
            out float burstMicroseconds,
            out uint stateHash,
            out uint faultFlags)
        {
            refundedResources = 0;
            overflowCaches = 0;
            severedEdges = 0;
            targetNodeIndex = -1;
            targetRuntimePosition = default;
            burstMicroseconds = 0f;
            stateHash = 0u;
            faultFlags = 0u;

            ConstructionManager instance = ActiveRuntimeInstance;
            if (instance == null)
                return false;

            refundedResources = instance._lastShinobu336RefundedResources;
            overflowCaches = instance._lastShinobu336OverflowCaches;
            severedEdges = instance._lastShinobu336SeveredEdges;
            targetNodeIndex = instance._lastShinobu336NodeIndex;
            targetRuntimePosition = instance._lastShinobu336TargetRuntimePosition;
            burstMicroseconds = instance._lastShinobu336BurstMicroseconds;
            stateHash = instance._lastShinobu336StateHash;
            faultFlags = instance._lastShinobu336FaultFlags;
            return true;
        }
#endif

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
        public int SpawnedBaseModuleCount => _spawnedBaseModules != null ? _spawnedBaseModules.Count : 0;

        /// <summary>Indexed cached BaseModule access for hot-path gameplay systems that must not scan components.</summary>
        public BaseModule GetSpawnedBaseModuleAt(int index)
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
        public bool IsInitialized => _isInitialized && _logisticsServiceRegistered;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => IsInitialized;

        /// <summary>
        /// Registers the construction/logistics service with bootstrap-owned runtime systems.
        /// </summary>
        public void InitializeService()
        {
            CacheRegistryServicesCold();
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
            CacheRegistryServicesCold();
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
                _habitatGraphManager = new HabitatGraphManager(capacity, _cachedDataVault); // COLD ALLOC: HabitatGraphManager[1] - persistent placed-module CSR adjacency owner - owner: ConstructionManager
            else
                _habitatGraphManager.SetDataVault(_cachedDataVault);

            BindConstructionRuntimeServices();
            BaseLogisticsNetwork.BindDataVault(_cachedDataVault);
            LogisticsPipeTransportScheduler.BindDataVault(_cachedDataVault);
            TryEnsureDeconstructionVaultBuffers(capacity);

            int jointCapacity = Mathf.Max(InitialJointRecoveryCapacity, capacity);
            if (_jointRecoveryTransformStack == null || _jointRecoveryTransformStack.Length < jointCapacity)
                _jointRecoveryTransformStack = new Transform[jointCapacity]; // COLD ALLOC: Transform[capacity] - AUP shift joint traversal stack - owner: ConstructionManager

            int bodyCapacity = Mathf.Max(InitialJointBodyRecoveryCapacity, capacity * 2);
            if (_jointRecoveryBodies == null || _jointRecoveryBodies.Length < bodyCapacity)
            {
                _jointRecoveryBodies = new Rigidbody[bodyCapacity]; // COLD ALLOC: Rigidbody[capacity*2] - AUP joint velocity restore cache - owner: ConstructionManager
                _jointRecoveryLinearVelocities = new Vector3[bodyCapacity]; // COLD ALLOC: Vector3[capacity*2] - AUP joint linear velocity restore cache - owner: ConstructionManager
                _jointRecoveryAngularVelocities = new Vector3[bodyCapacity]; // COLD ALLOC: Vector3[capacity*2] - AUP joint angular velocity restore cache - owner: ConstructionManager
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedObjectPool = GlobalRegistry.ObjectPoolService;
            _cachedPlayerInventoryService = GlobalRegistry.PlayerInventory;
            _cachedDataVault = GlobalRegistry.DataVault;
            _cachedSaveService = GlobalRegistry.Save;
            _cachedGasDynamics = GlobalRegistry.GasDynamics;
            _cachedAtmosphereReadModel = GlobalRegistry.AtmosphereReadModel;
            _cachedAmbientCurrentReadModel = GlobalRegistry.AmbientCurrent;
            _cachedAudioService = GlobalRegistry.Audio;
            _cachedFluidDecalPresentation = GlobalRegistry.FluidDecalPresentation;
            _cachedPhysicsService = GlobalRegistry.Physics;
        }

        private void BindConstructionRuntimeServices()
        {
            _habitatGraphManager?.SetRuntimeServices(
                _cachedAtmosphereReadModel,
                _cachedAmbientCurrentReadModel,
                _cachedAudioService,
                _cachedFluidDecalPresentation);
            BaseDegradationSystem.BindRuntimeServices(this, _cachedFluidDecalPresentation);
        }

        private void ClearCachedRegistryServices()
        {
            _habitatGraphManager?.SetRuntimeServices(null, null, null, null);
            BaseDegradationSystem.BindRuntimeServices(null, null);
            BaseLogisticsNetwork.BindDataVault(null);
            LogisticsPipeTransportScheduler.BindDataVault(null);
            _cachedObjectPool = null;
            _cachedPlayerInventoryService = null;
            _cachedDataVault = null;
            _cachedSaveService = null;
            _cachedGasDynamics = null;
            _cachedAtmosphereReadModel = null;
            _cachedAmbientCurrentReadModel = null;
            _cachedAudioService = null;
            _cachedFluidDecalPresentation = null;
            _cachedPhysicsService = null;
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            CacheRegistryServicesCold();
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

            CacheRegistryServicesCold();
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
            ClearCachedRegistryServices();
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
            ClearCachedRegistryServices();
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

            _habitatGraphManager?.FlushVisualSync();
            DrainDeconstructionRequests();
        }

        public void SlowTick()
        {
            if (_habitatGraphManager != null)
            {
                _habitatGraphManager.ApplyHydrodynamicStress(SlowTickDeltaTime);
                _habitatGraphManager.PublishRoomSubmergedFractionsToGas(_cachedGasDynamics);
            }

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
            if (_spawnedModules == null || _spawnedBaseModules == null)
                EnsureRuntimeStorage();
            if (_spawnedModules == null || _spawnedBaseModules == null)
                return;

            // Guard: duplicate module reference.
            if (ContainsRef(module)) return;
            bool shouldAddBaseModule = module.TryGetComponent(out BaseModule baseModule) && !ContainsBaseModuleRef(baseModule);
            if (_spawnedModules.Count >= _spawnedModules.Capacity ||
                (shouldAddBaseModule && _spawnedBaseModules.Count >= _spawnedBaseModules.Capacity))
            {
                return;
            }

            // Add to runtime registry.
            _spawnedModules.Add(module);
            if (shouldAddBaseModule)
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

            IObjectPoolService pool = _cachedObjectPool;
            RetireModuleInstanceWithoutDestroy(module, pool);
        }

        /// <inheritdoc />
        public bool EnqueueDeconstruction(in DeconstructRequestSignal signal)
        {
            if (!_isInitialized || !Application.isPlaying)
                return false;

            SignalBus<DeconstructRequestSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
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
            int remainingThisFrame = HabitatDeconstructionTransactionKernel.ResolveMaxTeardownsPerFrame(HomeostasisBrain.GlobalQualityWeight);
            while (SignalBus<DeconstructRequestSignal>.TryConsumeFrame(out DeconstructRequestSignal request))
            {
                ProcessDeconstructionRequest(in request);
                remainingThisFrame--;
                if (remainingThisFrame <= 0)
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
                if (!ValidateDeconstructionProbe(in request, module))
                {
                    RejectDeconstruction(in request, DeconstructReasonRayMismatch, 0, 0, 0);
                    return;
                }
            }

            ProcessDeconstructionRequestAfterRayValidated(in request, module);
        }

        private void ProcessDeconstructionRequestAfterRayValidated(in DeconstructRequestSignal request, BaseModule module)
        {
            IObjectPoolService pool = _cachedObjectPool;
            if (pool == null || !pool.CanDespawnWithoutDestroy(module.gameObject))
            {
                RejectDeconstruction(in request, DeconstructReasonPoolUnavailable, 0, 0, 0);
                return;
            }

            if (_habitatGraphDirty)
                RefreshHabitatGraph();

            const bool skipDfs = false;
            _lastDeconstructionDfsVisitedCount = 0;
            _lastDeconstructionDfsExpectedCount = 0;
            if (_habitatGraphManager != null)
            {
                int deconstructionCapacity = Mathf.Max(initialCapacity, ModuleCount);
                if (!TryAcquireDeconstructionDfsBuffers(
                        deconstructionCapacity,
                        out NativeArray<int> dfsStack,
                        out NativeArray<byte> dfsVisited,
                        out NativeArray<int> dfsResult,
                        out IDataVault dfsVault))
                {
                    RejectDeconstruction(in request, DeconstructReasonGraphRejected, 3, 0, 0);
                    return;
                }

                try
                {
                    if (!_habitatGraphManager.TryValidateDeconstructionRollback(
                            module,
                            dfsStack,
                            dfsVisited,
                            dfsResult,
                            out byte graphRejectReason))
                    {
                        CacheDfsResult(dfsResult);
                        RejectDeconstruction(in request, DeconstructReasonGraphRejected, graphRejectReason, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                        return;
                    }

                    CacheDfsResult(dfsResult);
                }
                finally
                {
                    ReleaseDeconstructionDfsBuffers(dfsVault);
                }
            }

            PlayerInventory inventory = ResolvePlayerInventory();
            BuildableData buildData = FindBuildDataForModule(module);
            if (!module.TryBeginAuthoritativeDeconstruction())
            {
                RejectDeconstruction(in request, DeconstructReasonAlreadyActive, 0, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                return;
            }

            uint moduleHash = unchecked((uint)CaptureModuleHashId(module));
            if (moduleHash == 0u && buildData != null)
                moduleHash = unchecked((uint)buildData.ModuleHashId);
            ushort nodeId = (ushort)Mathf.Clamp(ResolveRegisteredModuleIndex(module.gameObject), 0, ushort.MaxValue);
            if (!ExecuteDeconstructionTransaction(
                    in request,
                    module,
                    buildData,
                    inventory,
                    moduleHash,
                    out ushort refundItemCount,
                    out int severedEdgeCount,
                    out int targetNodeIndex))
            {
                module.CancelAuthoritativeDeconstruction();
                RejectDeconstruction(in request, DeconstructReasonInventoryFull, 0, ReadDfsVisitedCount(), ReadDfsExpectedCount());
                PublishDeconstructionHudNotification(request.TargetEntityId, DeconstructReasonInventoryFull);
                return;
            }

            if (targetNodeIndex >= 0)
                severedEdgeCount = math.max(severedEdgeCount, _habitatGraphManager != null ? _habitatGraphManager.MarkDeconstructionEdgesSevered(targetNodeIndex) : 0);
            _lastShinobu336SeveredEdges = severedEdgeCount;

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
                CaptureModuleHashId(sourceModule),
                CaptureModuleHashId(destinationModule));
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

        private static int CaptureModuleHashId(BaseModule module)
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
                : RuntimeOriginRoute.CurrentRuntimeOriginAup();
            SignalBus<HabitatConstructionSignal>.TryPushTracked(new HabitatConstructionSignal
            {
                PositionAup = positionAup,
                ModuleHash = moduleHash,
                GraphId = (uint)Mathf.Max(0, _habitatGraphManager != null ? _habitatGraphManager.NodeCount : 0),
                NodeId = (ushort)Mathf.Clamp(moduleIndex, 0, ushort.MaxValue),
                Operation = operation,
                Flags = flags
            }, ref _signalPushDropCount);
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

            SignalBus<ModuleDeconstructSignal>.TryPushTracked(new ModuleDeconstructSignal
            {
                PositionAup = request.TargetAup,
                ModuleHash = moduleHash,
                TargetEntityId = request.TargetEntityId,
                NodeId = nodeId,
                Operation = ModuleDeconstructOperationDeleteMarker,
                Flags = flags,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            }, ref _signalPushDropCount);
        }

        private void PublishDeconstructionVfx(in DeconstructRequestSignal request)
        {
            SignalBus<DebrisSpawnSignal>.TryPushTracked(new DebrisSpawnSignal
            {
                PositionAup = request.TargetAup,
                SpeciesHash = 0u,
                SourceEntityId = request.TargetEntityId,
                Intensity01 = 1f,
                DebrisKind = DeconstructionDebrisKindDisintegrate,
                Flags = 0
            }, ref _signalPushDropCount);
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
            SignalBus<DeconstructResultSignal>.TryPushTracked(new DeconstructResultSignal
            {
                TargetAup = request.TargetAup,
                TargetEntityId = request.TargetEntityId,
                RequesterEntityId = request.RequesterEntityId,
                RefundItemCount = refundItemCount,
                Result = result,
                Reason = reason,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            }, ref _signalPushDropCount);
        }

        private static void PublishDeconstructionHudNotification(uint sourceId, byte reason)
        {
            SignalBus<HUDNotificationSignal>.TryPushTracked(new HUDNotificationSignal
            {
                MessageHash = 0xD3C04A11u,
                ContextHash = reason,
                SourceId = sourceId,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Severity = 2,
                Flags = 0
            }, ref _signalPushDropCount);
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

            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition targetAup = request.TargetAup;
            if (!TryResolveRuntimeFloat3AupDelta(in targetAup, in runtimeOriginAup, out float3 targetRuntime))
                return false;

            Vector3 modulePosition = module.transform.position;
            float3 moduleRuntime = new float3(modulePosition.x, modulePosition.y, modulePosition.z);
            if (!math.all(math.isfinite(moduleRuntime)))
                return false;

            float distanceSq = math.lengthsq(targetRuntime - moduleRuntime);
            return distanceSq <= 9f;
        }

        private static bool ValidateDeconstructionProbe(in DeconstructRequestSignal request, BaseModule module)
        {
            if (module == null || request.MaxDistance <= 0f)
                return false;

            float3 direction = request.RayDirection;
            float directionLengthSq = math.lengthsq(direction);
            AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition rayOriginAup = request.RayOriginAup;
            AbsoluteUniversePosition targetAup = request.TargetAup;
            if (!TryResolveRuntimeFloat3AupDelta(in rayOriginAup, in runtimeOriginAup, out float3 rayOriginRuntime) ||
                !TryResolveRuntimeFloat3AupDelta(in targetAup, in runtimeOriginAup, out float3 targetRuntime))
            {
                return false;
            }

            Vector3 modulePosition = module.transform.position;
            float3 moduleRuntime = new float3(modulePosition.x, modulePosition.y, modulePosition.z);
            if (!math.all(math.isfinite(direction)) ||
                !math.all(math.isfinite(moduleRuntime)) ||
                directionLengthSq <= 0.0001f)
            {
                return false;
            }

            float maxDistance = math.max(0.001f, request.MaxDistance);
            direction *= math.rsqrt(directionLengthSq);
            float3 toModule = moduleRuntime - rayOriginRuntime;
            float axialDistance = math.dot(toModule, direction);
            if (axialDistance < -0.01f || axialDistance > maxDistance + 0.25f)
                return false;

            float3 closest = rayOriginRuntime + direction * math.clamp(axialDistance, 0f, maxDistance);
            float lateralDistanceSq = math.lengthsq(moduleRuntime - closest);
            float targetDistanceSq = math.lengthsq(moduleRuntime - targetRuntime);
            return lateralDistanceSq <= 9f && targetDistanceSq <= 9f;
        }

        private static bool TryResolveRuntimeFloat3AupDelta(
            in AbsoluteUniversePosition position,
            in AbsoluteUniversePosition originAup,
            out float3 runtime)
        {
            runtime = default;
            if (!position.IsFinite() || !originAup.IsFinite())
                return false;

            double3 localDelta = position.ToAbsoluteDouble3() - originAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(localDelta)) ||
                math.any(math.abs(localDelta) > (double)float.MaxValue))
                return false;

            runtime = new float3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
            return math.all(math.isfinite(runtime));
        }

        private PlayerInventory ResolvePlayerInventory()
        {
            IPlayerInventoryService inventoryService = _cachedPlayerInventoryService;
            return inventoryService != null ? inventoryService.Inventory : null;
        }

        private BuildableData FindBuildDataForModule(BaseModule module)
        {
            if (module == null)
                return null;

            if (module.TryGetComponent(out ModuleMarker marker) &&
                marker != null &&
                marker.Data != null)
            {
                return marker.Data;
            }

            int moduleHashId = CaptureModuleHashId(module);
            if (moduleHashId == 0 && module.ModuleTemplate != null)
                moduleHashId = Hecton.Localization.LocHash.Compute(module.ModuleTemplate.PersistentId);

            return catalog != null ? catalog.FindDataByHashId(moduleHashId) : null;
        }

        private bool ExecuteDeconstructionTransaction(
            in DeconstructRequestSignal request,
            BaseModule module,
            BuildableData buildData,
            PlayerInventory inventory,
            uint moduleHash,
            out ushort refundItemCount)
        {
            return ExecuteDeconstructionTransaction(
                in request,
                module,
                buildData,
                inventory,
                moduleHash,
                out refundItemCount,
                out _,
                out _);
        }

        private bool ExecuteDeconstructionTransaction(
            in DeconstructRequestSignal request,
            BaseModule module,
            BuildableData buildData,
            PlayerInventory inventory,
            uint moduleHash,
            out ushort refundItemCount,
            out int severedEdgeCount,
            out int targetNodeIndex)
        {
            refundItemCount = 0;
            severedEdgeCount = 0;
            targetNodeIndex = -1;
            if (!HabitatDeconstructionTransactionKernel.RuntimeLayoutValid())
            {
                return false;
            }

            if (moduleHash == 0u || !TryBuildDeconstructionTransaction(in request, moduleHash, out DeconstructionTransactionDTO transaction))
                return false;

            if (!TryAcquireDeconstructionTransactionBuffers(
                    out NativeArray<DeconstructionTransactionDTO> transactions,
                    out NativeArray<RefundCommandDTO> refundCommands,
                    out NativeArray<LootCacheDTO> lootCaches,
                    out NativeArray<int> counters,
                    out NativeArray<ModuleCostDTO> fallbackCosts,
                    out IDataVault transactionVault))
            {
                return false;
            }

            bool telemetryLocked = false;

            NativeArray<int> edgeOffsets = default;
            NativeArray<int> edgeDestinations = default;
            NativeArray<float> edgeStrength = default;
            NativeArray<byte> edgeFlags = default;
            int nodeCount = 0;
            int edgeCount = 0;
            bool deconstructionCsrLocked = false;
            try
            {
                transactions[0] = transaction;
                counters[DeconstructionRefundCommandCountIndex] = 0;
                counters[DeconstructionLootCacheCountIndex] = 0;

                if (_habitatGraphManager != null)
                {
                    deconstructionCsrLocked = _habitatGraphManager.TryGetDeconstructionCsrLanes(
                        module,
                        out edgeOffsets,
                        out edgeDestinations,
                        out edgeStrength,
                        out edgeFlags,
                        out targetNodeIndex,
                        out nodeCount,
                        out edgeCount);
                }

                if (!TryResolveModuleCostSource(buildData, moduleHash, fallbackCosts, out NativeArray<ModuleCostDTO> moduleCosts, out int moduleCostCount))
                {
                    moduleCosts = fallbackCosts;
                    moduleCostCount = 0;
                }

                IDataVault vault = _cachedDataVault;
                telemetryLocked = TryAcquireDeconstructionTelemetry(
                    vault,
                    out NativeArray<TeardownTelemetryEntry> telemetryRing,
                    out NativeArray<int> telemetryCursor);

                long startTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                ExecuteModuleTeardownJob job = new ExecuteModuleTeardownJob
                {
                    Transactions = transactions,
                    ModuleCosts = moduleCosts,
                    EdgeOffsets = edgeOffsets,
                    EdgeDestinations = edgeDestinations,
                    EdgeStrength = edgeStrength,
                    EdgeFlags = edgeFlags,
                    RefundCommands = refundCommands,
                    RefundCommandCount = counters,
                    LootCaches = lootCaches,
                    LootCacheCount = counters,
                    TelemetryRing = telemetryRing,
                    TelemetryCursor = telemetryCursor,
                    TransactionCount = 1,
                    ModuleCostCount = moduleCostCount,
                    TargetNodeIndex = targetNodeIndex,
                    NodeCount = nodeCount,
                    EdgeCount = edgeCount,
                    RefundCommandCountIndex = DeconstructionRefundCommandCountIndex,
                    LootCacheCountIndex = DeconstructionLootCacheCountIndex,
                    MaxTeardownsPerFrame = HabitatDeconstructionTransactionKernel.ResolveMaxTeardownsPerFrame(HomeostasisBrain.GlobalQualityWeight),
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    SequenceBase = ++_deconstructionSequence,
                    LayoutValid = 1u,
                    GlobalQualityWeight = HomeostasisBrain.GlobalQualityWeight
                };
                try
                {
                    job.Execute(); // COLD SYNC JOB: player-triggered teardown transaction, bounded to <= 4 refund pairs.
                }
                finally
                {
                    if (deconstructionCsrLocked && _habitatGraphManager != null)
                    {
                        _habitatGraphManager.ReleaseDeconstructionCsrLanes();
                        deconstructionCsrLocked = false;
                    }
                }

                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTicks;
                float burstMicroseconds = (float)(elapsedTicks * 1000000.0 / System.Diagnostics.Stopwatch.Frequency);

                int refundCommandCount = counters[DeconstructionRefundCommandCountIndex];
                int returnedCount = ApplyRefundCommandsOrOverflow(in request, inventory, refundCommandCount, refundCommands, lootCaches, counters);
                PublishOverflowLootCaches(lootCaches, counters);
                refundItemCount = (ushort)Mathf.Clamp(returnedCount, 0, ushort.MaxValue);

                int overflowLootCacheCount = counters[DeconstructionLootCacheCountIndex];
                ReadLastDeconstructionTelemetry(
                    telemetryRing,
                    telemetryCursor,
                    burstMicroseconds,
                    returnedCount,
                    overflowLootCacheCount,
                    out severedEdgeCount,
                    out uint faultFlags,
                    out uint stateHash);
                _lastShinobu336BurstMicroseconds = burstMicroseconds;
                _lastShinobu336RefundedResources = returnedCount;
                _lastShinobu336OverflowCaches = overflowLootCacheCount;
                _lastShinobu336NodeIndex = targetNodeIndex;
                AbsoluteUniversePosition runtimeOriginAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
                AbsoluteUniversePosition targetAup = request.TargetAup;
                _lastShinobu336TargetRuntimePosition = TryResolveRuntimeFloat3AupDelta(in targetAup, in runtimeOriginAup, out float3 targetRuntime)
                    ? new Vector3(targetRuntime.x, targetRuntime.y, targetRuntime.z)
                    : (module != null ? module.transform.position : default);
                _lastShinobu336StateHash = stateHash;
                _lastShinobu336FaultFlags = faultFlags;

                if (telemetryLocked)
                {
                    ReleaseDeconstructionTelemetry(vault);
                    telemetryLocked = false;
                }

                if ((faultFlags & HabitatDeconstructionTransactionKernel.FaultNaN) != 0u || burstMicroseconds > 500f)
                    DumpShinobu336BlackBox();

                return true;
            }
            finally
            {
                if (deconstructionCsrLocked && _habitatGraphManager != null)
                    _habitatGraphManager.ReleaseDeconstructionCsrLanes();
                if (telemetryLocked)
                    ReleaseDeconstructionTelemetry(_cachedDataVault);
                ReleaseDeconstructionTransactionBuffers(transactionVault);
            }
        }

        private static bool TryBuildDeconstructionTransaction(
            in DeconstructRequestSignal request,
            uint moduleHash,
            out DeconstructionTransactionDTO transaction)
        {
            transaction = default;
            if (moduleHash == 0u || !request.TargetAup.IsFinite())
                return false;

            transaction.TargetModuleHash = moduleHash;
            transaction.InitiatorEntityHash = request.RequesterEntityId;
            transaction.OriginalAUP = request.TargetAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(transaction.OriginalAUP));
        }

        private bool TryResolveModuleCostSource(
            BuildableData buildData,
            uint moduleHash,
            NativeArray<ModuleCostDTO> fallbackCosts,
            out NativeArray<ModuleCostDTO> moduleCosts,
            out int moduleCostCount)
        {
            moduleCosts = default;
            moduleCostCount = 0;
            IDataVault vault = _cachedDataVault;
            if (vault != null &&
                BaseModuleCatalogRuntime.TryResolveViews(vault, out ModuleCatalogViews views) &&
                views.State.IsCreated &&
                views.State.Length > 0 &&
                views.Costs.IsCreated)
            {
                moduleCosts = views.Costs;
                moduleCostCount = (int)math.min(views.State[0].CostCount, (uint)views.Costs.Length);
                if (moduleCostCount > 0)
                    return true;
            }

            if (!fallbackCosts.IsCreated ||
                fallbackCosts.Length == 0 ||
                !TryBuildFallbackCostDto(buildData, moduleHash, out ModuleCostDTO fallbackCost))
            {
                return false;
            }

            fallbackCosts[0] = fallbackCost;
            moduleCosts = fallbackCosts;
            moduleCostCount = 1;
            return true;
        }

        private static bool TryBuildFallbackCostDto(BuildableData buildData, uint moduleHash, out ModuleCostDTO cost)
        {
            cost = default;
            if (buildData == null || buildData.buildCost == null || moduleHash == 0u)
                return false;

            cost.PrefabHashID = moduleHash;
            int written = 0;
            List<InventoryCost> buildCost = buildData.buildCost;
            int count = buildCost.Count;
            for (int i = 0; i < count && written < HabitatDeconstructionTransactionKernel.MaxCostPairs; i++)
            {
                InventoryCost entry = buildCost[i];
                int itemHashId = ResolveRefundCostItemHash(entry);
                int quantity = entry != null ? Mathf.Max(0, entry.amount) : 0;
                if (itemHashId == 0 || quantity <= 0)
                    continue;

                WriteFallbackCostPair(ref cost, written, unchecked((uint)itemHashId), quantity);
                written++;
            }

            cost.CostCount = (uint)written;
            return written > 0;
        }

        private static int ResolveRefundCostItemHash(InventoryCost cost)
        {
            if (cost == null || cost.item == null)
                return 0;

            return Hecton.Localization.LocHash.Compute(cost.item.PersistentId);
        }

        private static void WriteFallbackCostPair(ref ModuleCostDTO cost, int index, uint itemHash, int quantity)
        {
            switch (index)
            {
                case 0:
                    cost.ItemHash0 = itemHash;
                    cost.Quantity0 = quantity;
                    break;
                case 1:
                    cost.ItemHash1 = itemHash;
                    cost.Quantity1 = quantity;
                    break;
                case 2:
                    cost.ItemHash2 = itemHash;
                    cost.Quantity2 = quantity;
                    break;
                default:
                    cost.ItemHash3 = itemHash;
                    cost.Quantity3 = quantity;
                    break;
            }
        }

        private bool TryAcquireDeconstructionTelemetry(
            IDataVault vault,
            out NativeArray<TeardownTelemetryEntry> telemetryRing,
            out NativeArray<int> telemetryCursor)
        {
            telemetryRing = default;
            telemetryCursor = default;
            if (vault == null ||
                _deconstructionTelemetryHandle.Generation == 0u ||
                _deconstructionTelemetryCursorHandle.Generation == 0u)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionTelemetryHandle, SystemID.Construction, out telemetryRing))
                return false;

            if (!telemetryRing.IsCreated || telemetryRing.Length < HabitatDeconstructionTransactionKernel.TelemetryCapacity)
            {
                vault.ReleaseWriteLock(in _deconstructionTelemetryHandle, SystemID.Construction);
                telemetryRing = default;
                return false;
            }

            bool cursorLocked = vault.TryAcquireWriteLock(in _deconstructionTelemetryCursorHandle, SystemID.Construction, out telemetryCursor);
            if (cursorLocked &&
                telemetryCursor.IsCreated &&
                telemetryCursor.Length > 0)
                return true;

            if (cursorLocked)
                vault.ReleaseWriteLock(in _deconstructionTelemetryCursorHandle, SystemID.Construction);
            vault.ReleaseWriteLock(in _deconstructionTelemetryHandle, SystemID.Construction);
            telemetryRing = default;
            telemetryCursor = default;
            return false;
        }

        private void ReleaseDeconstructionTelemetry(IDataVault vault)
        {
            if (vault == null)
                return;

            if (_deconstructionTelemetryCursorHandle.Generation != 0u)
                vault.ReleaseWriteLock(in _deconstructionTelemetryCursorHandle, SystemID.Construction);
            if (_deconstructionTelemetryHandle.Generation != 0u)
                vault.ReleaseWriteLock(in _deconstructionTelemetryHandle, SystemID.Construction);
        }

        private int ApplyRefundCommandsOrOverflow(
            in DeconstructRequestSignal request,
            PlayerInventory inventory,
            int refundCommandCount,
            NativeArray<RefundCommandDTO> refundCommands,
            NativeArray<LootCacheDTO> lootCaches,
            NativeArray<int> counters)
        {
            int returnedQuantity = 0;
            int safeCount = math.min(math.max(0, refundCommandCount), refundCommands.IsCreated ? refundCommands.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                RefundCommandDTO command = refundCommands[i];
                if (command.ItemHash == 0u || command.Quantity <= 0)
                    continue;

                bool added = inventory != null && inventory.TryAddItem(unchecked((int)command.ItemHash), command.Quantity);
                if (added)
                {
                    returnedQuantity = Mathf.Min(ushort.MaxValue, returnedQuantity + command.Quantity);
                    PublishDeconstructionItemAcquired(in request, command.ItemHash, command.Quantity);
                    command.Status = HabitatDeconstructionTransactionKernel.RefundStatusPendingInventory;
                    refundCommands[i] = command;
                    continue;
                }

                command.Status = HabitatDeconstructionTransactionKernel.RefundStatusOverflowLootCache;
                refundCommands[i] = command;
                if (AppendOverflowLootCache(in request, in command, lootCaches, counters))
                    returnedQuantity = Mathf.Min(ushort.MaxValue, returnedQuantity + command.Quantity);
            }

            return returnedQuantity;
        }

        private static void PublishDeconstructionItemAcquired(
            in DeconstructRequestSignal request,
            uint itemHash,
            int quantity)
        {
            SignalBus<ItemAcquiredSignal>.TryPushTracked(new ItemAcquiredSignal
            {
                PositionAup = request.TargetAup,
                ItemHash = itemHash,
                OreHash = 0u,
                Quantity = (ushort)Mathf.Clamp(quantity, 0, ushort.MaxValue),
                SourceKind = 4,
                Flags = 0,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId
            }, ref _signalPushDropCount);
        }

        private bool AppendOverflowLootCache(
            in DeconstructRequestSignal request,
            in RefundCommandDTO command,
            NativeArray<LootCacheDTO> lootCaches,
            NativeArray<int> counters)
        {
            if (!lootCaches.IsCreated ||
                !counters.IsCreated ||
                counters.Length <= DeconstructionLootCacheCountIndex)
            {
                return false;
            }

            int index = counters[DeconstructionLootCacheCountIndex];
            if (index < 0 || index >= lootCaches.Length)
                return false;

            float q = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            float radius = math.lerp(0.35f, 0.95f, q);
            float3 offset = ResolveOverflowCacheOffset(command.Sequence, command.PairIndex, radius);
            double3 origin = request.TargetAup.ToAbsoluteDouble3();
            lootCaches[index] = new LootCacheDTO
            {
                PositionAup = origin + new double3(offset.x, offset.y, offset.z),
                LocalOffset = offset,
                ItemHash = command.ItemHash,
                Quantity = command.Quantity,
                SourceModuleHash = command.TargetModuleHash,
                Sequence = command.Sequence,
                Flags = 0u
            };
            counters[DeconstructionLootCacheCountIndex] = index + 1;
            return true;
        }

        private static float3 ResolveOverflowCacheOffset(uint sequence, byte pairIndex, float radius)
        {
            uint hash = sequence ^ ((uint)pairIndex * 0x9E3779B9u);
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            float angle = (hash & 1023u) * (6.28318530718f / 1024f);
            MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
            return new float3(cos * radius, 0.45f, sin * radius);
        }

        private void PublishOverflowLootCaches(
            NativeArray<LootCacheDTO> lootCaches,
            NativeArray<int> counters)
        {
            if (!lootCaches.IsCreated ||
                !counters.IsCreated ||
                counters.Length <= DeconstructionLootCacheCountIndex)
            {
                return;
            }

            int count = math.min(math.max(0, counters[DeconstructionLootCacheCountIndex]), lootCaches.Length);
            for (int i = 0; i < count; i++)
            {
                LootCacheDTO cache = lootCaches[i];
                if (cache.ItemHash == 0u || cache.Quantity <= 0)
                    continue;

                InventoryDeathLootCacheSignal signal = new InventoryDeathLootCacheSignal
                {
                    PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(cache.PositionAup),
                    GeneticsMask = 0UL,
                    InventoryHash = cache.SourceModuleHash,
                    ItemHash = cache.ItemHash,
                    Sequence = cache.Sequence,
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                    Quantity = (ushort)Mathf.Clamp(cache.Quantity, 0, ushort.MaxValue),
                    QualityMilli = 1000,
                    Flags = cache.Flags,
                    StateFlags = 0
                };
                SignalBus<InventoryDeathLootCacheSignal>.TryPushTracked(in signal, ref _signalPushDropCount);
            }
        }

        private void ReadLastDeconstructionTelemetry(
            NativeArray<TeardownTelemetryEntry> telemetryRing,
            NativeArray<int> telemetryCursor,
            float burstMicroseconds,
            int returnedQuantity,
            int overflowLootCaches,
            out int severedEdgeCount,
            out uint faultFlags,
            out uint stateHash)
        {
            severedEdgeCount = 0;
            faultFlags = 0u;
            stateHash = 0u;
            if (!telemetryRing.IsCreated ||
                !telemetryCursor.IsCreated ||
                telemetryRing.Length == 0 ||
                telemetryCursor.Length == 0)
            {
                return;
            }

            int cursor = telemetryCursor[0] - 1;
            if (cursor < 0)
                cursor += telemetryRing.Length;

            int index = cursor % telemetryRing.Length;
            TeardownTelemetryEntry entry = telemetryRing[index];
            entry.BurstMicroseconds = burstMicroseconds;
            entry.ResourcesRefunded = returnedQuantity;
            entry.OverflowLootCaches = overflowLootCaches;
            if (burstMicroseconds > 500f)
                entry.FaultFlags |= HabitatDeconstructionTransactionKernel.FaultBudgetExceeded;

            telemetryRing[index] = entry;
            severedEdgeCount = entry.EdgesSevered;
            faultFlags = entry.FaultFlags;
            stateHash = entry.StateHash;
        }

        private void DumpShinobu336BlackBox()
        {
            IDataVault vault = _cachedDataVault;
            if (vault == null ||
                _deconstructionTelemetryHandle.Generation == 0u ||
                !vault.TryAcquireWriteLock(in _deconstructionTelemetryHandle, SystemID.Construction, out NativeArray<TeardownTelemetryEntry> telemetryRing))
            {
                return;
            }

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Shinobu336DumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(HabitatDeconstructionTransactionKernel.SystemHash);
                    writer.Write(telemetryRing.Length);
                    for (int i = 0; i < telemetryRing.Length; i++)
                    {
                        TeardownTelemetryEntry entry = telemetryRing[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.TargetModuleHash);
                        writer.Write(entry.InitiatorEntityHash);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.ModulesProcessed);
                        writer.Write(entry.ResourcesRefunded);
                        writer.Write(entry.OverflowLootCaches);
                        writer.Write(entry.EdgesSevered);
                        writer.Write(entry.BurstMicroseconds);
                        writer.Write(entry.GlobalQualityWeight);
                        writer.Write(entry.FaultFlags);
                        writer.Write(entry.TargetNodeIndex);
                        writer.Write(entry.AupLocalMagnitude);
                    }
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _deconstructionTelemetryHandle, SystemID.Construction);
            }
        }

        private bool TryEnsureDeconstructionVaultBuffers(int requestedCapacity = 0)
        {
            IDataVault vault = _cachedDataVault;
            if (vault == null)
                return false;

            int capacity = Mathf.Max(1, requestedCapacity > 0 ? requestedCapacity : Mathf.Max(initialCapacity, ModuleCount));
            bool ready = true;
            ready &= EnsureDeconstructionVaultBuffer(vault, DeconstructionDfsStackBufferId, capacity, NativeArrayOptions.ClearMemory, ref _deconstructionDfsStackHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, DeconstructionDfsVisitedBufferId, capacity, NativeArrayOptions.ClearMemory, ref _deconstructionDfsVisitedHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, DeconstructionDfsResultBufferId, DeconstructionDfsResultLength, NativeArrayOptions.ClearMemory, ref _deconstructionDfsResultHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, DeconstructionBlackBoxBufferId, DeconstructionBlackBoxCapacity, NativeArrayOptions.ClearMemory, ref _deconstructionBlackBoxHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336TeardownTransactions, DeconstructionTransactionCapacity, NativeArrayOptions.UninitializedMemory, ref _deconstructionTransactionsHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, DeconstructionFallbackCostsBufferId, 1, NativeArrayOptions.UninitializedMemory, ref _deconstructionFallbackCostsHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336RefundCommands, DeconstructionRefundCommandCapacity, NativeArrayOptions.UninitializedMemory, ref _deconstructionRefundCommandsHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336LootCaches, DeconstructionLootCacheCapacity, NativeArrayOptions.UninitializedMemory, ref _deconstructionLootCachesHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336Counters, DeconstructionCounterLaneLength, NativeArrayOptions.ClearMemory, ref _deconstructionCountersHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336TelemetryRing, HabitatDeconstructionTransactionKernel.TelemetryCapacity, NativeArrayOptions.ClearMemory, ref _deconstructionTelemetryHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336TelemetryCursor, 1, NativeArrayOptions.ClearMemory, ref _deconstructionTelemetryCursorHandle);
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336RefundProfiles, HabitatDeconstructionTransactionKernel.RefundProfileCapacity, NativeArrayOptions.ClearMemory, ref _deconstructionRefundProfilesHandle);
#if UNITY_EDITOR
            ready &= EnsureDeconstructionVaultBuffer(vault, BufferID.Shinobu336CsvScratch, HabitatDeconstructionTransactionKernel.CsvScratchBytes, NativeArrayOptions.ClearMemory, ref _deconstructionCsvScratchHandle);
#endif
            return ready;
        }

        private static bool EnsureDeconstructionVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault == null || requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            if (TryOpenDeconstructionVaultBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                return true;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle))
            {
                handle = existingHandle;
                if (TryOpenDeconstructionVaultBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                    return true;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Construction,
                options);
            if (TryOpenDeconstructionVaultBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> _))
                return true;

            handle = default;
            return false;
        }

        private static bool TryOpenDeconstructionVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.Generation != 0u &&
                   handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.Construction &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryAcquireDeconstructionDfsBuffers(
            int requestedCapacity,
            out NativeArray<int> dfsStack,
            out NativeArray<byte> dfsVisited,
            out NativeArray<int> dfsResult,
            out IDataVault vault)
        {
            dfsStack = default;
            dfsVisited = default;
            dfsResult = default;
            vault = _cachedDataVault;
            int capacity = Mathf.Max(1, requestedCapacity);
            if (!TryEnsureDeconstructionVaultBuffers(capacity) || vault == null)
                return false;

            int acquiredCount = 0;
            if (!vault.TryAcquireWriteLock(in _deconstructionDfsStackHandle, SystemID.Construction, out dfsStack))
            {
                ReleaseDeconstructionDfsBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 1;
            if (!dfsStack.IsCreated || dfsStack.Length < capacity)
            {
                ReleaseDeconstructionDfsBuffers(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionDfsVisitedHandle, SystemID.Construction, out dfsVisited))
            {
                ReleaseDeconstructionDfsBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 2;
            if (!dfsVisited.IsCreated || dfsVisited.Length < capacity)
            {
                ReleaseDeconstructionDfsBuffers(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionDfsResultHandle, SystemID.Construction, out dfsResult))
            {
                ReleaseDeconstructionDfsBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 3;
            if (!dfsResult.IsCreated || dfsResult.Length < DeconstructionDfsResultLength)
            {
                ReleaseDeconstructionDfsBuffers(vault, acquiredCount);
                return false;
            }

            return true;
        }

        private void ReleaseDeconstructionDfsBuffers(IDataVault vault)
        {
            ReleaseDeconstructionDfsBuffers(vault, 3);
        }

        private void ReleaseDeconstructionDfsBuffers(IDataVault vault, int acquiredCount)
        {
            if (vault == null)
                return;

            if (acquiredCount >= 3)
                vault.ReleaseWriteLock(in _deconstructionDfsResultHandle, SystemID.Construction);
            if (acquiredCount >= 2)
                vault.ReleaseWriteLock(in _deconstructionDfsVisitedHandle, SystemID.Construction);
            if (acquiredCount >= 1)
                vault.ReleaseWriteLock(in _deconstructionDfsStackHandle, SystemID.Construction);
        }

        private bool TryAcquireDeconstructionTransactionBuffers(
            out NativeArray<DeconstructionTransactionDTO> transactions,
            out NativeArray<RefundCommandDTO> refundCommands,
            out NativeArray<LootCacheDTO> lootCaches,
            out NativeArray<int> counters,
            out NativeArray<ModuleCostDTO> fallbackCosts,
            out IDataVault vault)
        {
            transactions = default;
            refundCommands = default;
            lootCaches = default;
            counters = default;
            fallbackCosts = default;
            vault = _cachedDataVault;
            if (!TryEnsureDeconstructionVaultBuffers(Mathf.Max(initialCapacity, ModuleCount)) || vault == null)
                return false;

            int acquiredCount = 0;
            if (!vault.TryAcquireWriteLock(in _deconstructionTransactionsHandle, SystemID.Construction, out transactions))
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 1;
            if (!transactions.IsCreated || transactions.Length < 1)
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionRefundCommandsHandle, SystemID.Construction, out refundCommands))
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 2;
            if (!refundCommands.IsCreated || refundCommands.Length < DeconstructionRefundCommandCapacity)
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionLootCachesHandle, SystemID.Construction, out lootCaches))
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 3;
            if (!lootCaches.IsCreated || lootCaches.Length < DeconstructionLootCacheCapacity)
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionCountersHandle, SystemID.Construction, out counters))
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 4;
            if (!counters.IsCreated || counters.Length < DeconstructionCounterLaneLength)
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _deconstructionFallbackCostsHandle, SystemID.Construction, out fallbackCosts))
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            acquiredCount = 5;
            if (!fallbackCosts.IsCreated || fallbackCosts.Length < 1)
            {
                ReleaseDeconstructionTransactionBuffers(vault, acquiredCount);
                return false;
            }

            return true;
        }

        private void ReleaseDeconstructionTransactionBuffers(IDataVault vault)
        {
            ReleaseDeconstructionTransactionBuffers(vault, 5);
        }

        private void ReleaseDeconstructionTransactionBuffers(IDataVault vault, int acquiredCount)
        {
            if (vault == null)
                return;

            if (acquiredCount >= 5)
                vault.ReleaseWriteLock(in _deconstructionFallbackCostsHandle, SystemID.Construction);
            if (acquiredCount >= 4)
                vault.ReleaseWriteLock(in _deconstructionCountersHandle, SystemID.Construction);
            if (acquiredCount >= 3)
                vault.ReleaseWriteLock(in _deconstructionLootCachesHandle, SystemID.Construction);
            if (acquiredCount >= 2)
                vault.ReleaseWriteLock(in _deconstructionRefundCommandsHandle, SystemID.Construction);
            if (acquiredCount >= 1)
                vault.ReleaseWriteLock(in _deconstructionTransactionsHandle, SystemID.Construction);
        }

        private bool TryAcquireDeconstructionBlackBox(
            out NativeArray<HabitatDeconstructionTelemetryEntry> blackBox,
            out IDataVault vault)
        {
            blackBox = default;
            vault = _cachedDataVault;
            if (!TryEnsureDeconstructionVaultBuffers(Mathf.Max(initialCapacity, ModuleCount)) || vault == null)
                return false;

            if (!vault.TryAcquireWriteLock(in _deconstructionBlackBoxHandle, SystemID.Construction, out blackBox))
                return false;

            if (blackBox.IsCreated && blackBox.Length >= DeconstructionBlackBoxCapacity)
                return true;

            vault.ReleaseWriteLock(in _deconstructionBlackBoxHandle, SystemID.Construction);
            blackBox = default;
            return false;
        }

        private void ReleaseDeconstructionBlackBox(IDataVault vault)
        {
            if (vault != null)
                vault.ReleaseWriteLock(in _deconstructionBlackBoxHandle, SystemID.Construction);
        }

        private void DisposeDeconstructionNativeBuffers()
        {
            _deconstructionDfsStackHandle = default;
            _deconstructionDfsVisitedHandle = default;
            _deconstructionDfsResultHandle = default;
            _deconstructionBlackBoxHandle = default;
            _deconstructionTransactionsHandle = default;
            _deconstructionFallbackCostsHandle = default;
            _deconstructionRefundCommandsHandle = default;
            _deconstructionLootCachesHandle = default;
            _deconstructionCountersHandle = default;
            _deconstructionTelemetryHandle = default;
            _deconstructionTelemetryCursorHandle = default;
            _deconstructionRefundProfilesHandle = default;
#if UNITY_EDITOR
            _deconstructionCsvScratchHandle = default;
#endif
            _deconstructionBlackBoxCursor = 0;
            _lastDeconstructionDfsVisitedCount = 0;
            _lastDeconstructionDfsExpectedCount = 0;
            _deconstructionSequence = 0u;
            _lastShinobu336RefundedResources = 0;
            _lastShinobu336OverflowCaches = 0;
            _lastShinobu336SeveredEdges = 0;
            _lastShinobu336NodeIndex = -1;
            _lastShinobu336BurstMicroseconds = 0f;
            _lastShinobu336TargetRuntimePosition = default;
            _lastShinobu336StateHash = 0u;
            _lastShinobu336FaultFlags = 0u;
        }

        private int ReadDfsVisitedCount()
        {
            return _lastDeconstructionDfsVisitedCount;
        }

        private int ReadDfsExpectedCount()
        {
            return _lastDeconstructionDfsExpectedCount;
        }

        private void CacheDfsResult(NativeArray<int> dfsResult)
        {
            _lastDeconstructionDfsVisitedCount = dfsResult.IsCreated && dfsResult.Length > 1 ? dfsResult[1] : 0;
            _lastDeconstructionDfsExpectedCount = dfsResult.IsCreated && dfsResult.Length > 2 ? dfsResult[2] : 0;
        }

        private void WriteDeconstructionBlackBoxSample(
            in DeconstructRequestSignal request,
            byte result,
            byte reason,
            int visitedCount,
            int expectedCount)
        {
            if (!TryAcquireDeconstructionBlackBox(out NativeArray<HabitatDeconstructionTelemetryEntry> blackBox, out IDataVault vault))
                return;

            try
            {
                int index = _deconstructionBlackBoxCursor;
                if (index < 0 || index >= blackBox.Length)
                    index = 0;

                blackBox[index] = new HabitatDeconstructionTelemetryEntry
                {
                    Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
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
                if (_deconstructionBlackBoxCursor >= blackBox.Length)
                    _deconstructionBlackBoxCursor = 0;
            }
            finally
            {
                ReleaseDeconstructionBlackBox(vault);
            }
        }

        private void DumpDeconstructionBlackBox()
        {
            if (!TryAcquireDeconstructionBlackBox(out NativeArray<HabitatDeconstructionTelemetryEntry> blackBox, out IDataVault vault))
                return;

            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DeconstructionDumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(DeconstructionBlackBoxCapacity);
                    writer.Write(_deconstructionBlackBoxCursor);
                    for (int i = 0; i < blackBox.Length; i++)
                    {
                        HabitatDeconstructionTelemetryEntry entry = blackBox[i];
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
            finally
            {
                ReleaseDeconstructionBlackBox(vault);
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
            IObjectPoolService pool = _cachedObjectPool;

            // Iterate backwards while the list can be modified by despawn callbacks.
            for (int i = _spawnedModules.Count - 1; i >= 0; i--)
            {
                GameObject module = _spawnedModules[i];

                if (module == null) continue; // already destroyed

                RetireModuleInstanceWithoutDestroy(module, pool);
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
            if (!HasConstructionSaveDtoCapacity(in dto))
            {
                ClearConstructionSaveCounts(ref dto);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[ConstructionManager] Construction save DTO capacity missing. Save payload cleared.");
#endif
                return;
            }

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
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Module has no ModuleMarker. Skipping save.");
#endif
                    continue;
                }

                // Guard: empty ID.
                string prefabId = marker.PrefabId;
                if (string.IsNullOrEmpty(prefabId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Module has empty PrefabId. Skipping save.");
#endif
                    continue;
                }

                // Guard: save capacity.
                if (moduleIndex >= ConstructionDTO.MaxModules)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Max modules reached. Truncating construction save.");
#endif
                    break;
                }

                // Serialize transform.
                Transform t = module.transform;
                ModuleDTO moduleDto = dto.modules[moduleIndex];
                moduleDto.ResetForConstructionSave();
                moduleDto.prefabId = prefabId;
                moduleDto.SetPosition(t.position);
                moduleDto.SetRotation(t.rotation);
                moduleDto.slottedToolItemId = string.Empty;

                ModuleGraphNodeDTO graphNodeDto = default;
                graphNodeDto.prefabId = prefabId;
                graphNodeDto.moduleHashId = marker.Data != null ? marker.Data.ModuleHashId : 0;
                graphNodeDto.SetAup(TryResolveAupFromRuntimeOrigin(t.position, out AbsoluteUniversePosition moduleAup)
                    ? moduleAup
                    : RuntimeOriginRoute.CurrentRuntimeOriginAup());
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

        private static bool HasConstructionSaveDtoCapacity(in ConstructionDTO dto)
        {
            return dto.modules != null &&
                   dto.modules.Length >= ConstructionDTO.MaxModules &&
                   dto.graphNodes != null &&
                   dto.graphNodes.Length >= ConstructionDTO.MaxModules &&
                   dto.graphEdges != null &&
                   dto.graphEdges.Length >= ConstructionDTO.MaxGraphEdges &&
                   dto.moduleBlitRecords != null &&
                   dto.moduleBlitRecords.Length >= ConstructionDTO.MaxModules &&
                   dto.habitatFloodStates != null &&
                   dto.habitatFloodStates.Length >= ConstructionDTO.MaxModules &&
                   HasModuleSaveNestedArrayCapacity(dto.modules);
        }

        private static bool HasModuleSaveNestedArrayCapacity(ModuleDTO[] modules)
        {
            if (modules == null || modules.Length < ConstructionDTO.MaxModules)
                return false;

            for (int i = 0; i < ConstructionDTO.MaxModules; i++)
            {
                if (!modules[i].HasNestedArrayCapacity())
                    return false;
            }

            return true;
        }

        private static void ClearConstructionSaveCounts(ref ConstructionDTO dto)
        {
            dto.moduleCount = 0;
            dto.graphNodeCount = 0;
            dto.graphEdgeCount = 0;
            dto.moduleBlitCount = 0;
            dto.habitatFloodStateCount = 0;
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
                Hecton8.Core.H8Debug.LogError(
                    "[ConstructionManager] ModuleCatalog not assigned. Cannot load construction data.");
                return;
            }

            if (catalog.HasLookupAmbiguity)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    "[ConstructionManager] ModuleCatalog has ambiguous ID aliases. Construction load aborted.");
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
                Hecton8.Core.H8Debug.Log("[ConstructionManager] No construction data to load.");
                return;
            }

            // 2. Respawn modules from save.
            IObjectPoolService pool = _cachedObjectPool;
            ClearAllModules();
            int count = hasGraphTopology
                ? Mathf.Min(dto.graphNodeCount, dto.graphNodes.Length)
                : Mathf.Min(dto.moduleCount, dto.modules.Length);
            int loadedCount   = 0;
            int skippedCount  = 0;
            AbsoluteUniversePosition graphLoadRuntimeOriginAup = hasGraphTopology
                ? RuntimeOriginRoute.CurrentRuntimeOriginAup()
                : default;

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
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Module not found in catalog. Skipping.");
#endif
                    skippedCount++;
                    continue;
                }

                // Validate position.
                Vector3 pos;
                if (hasGraphTopology)
                {
                    AbsoluteUniversePosition graphAup = graphNodeDto.GetAup();
                    if (!TryResolveRuntimeFloat3AupDelta(in graphAup, in graphLoadRuntimeOriginAup, out float3 graphRuntimePosition))
                    {
                        skippedCount++;
                        continue;
                    }

                    pos = new Vector3(graphRuntimePosition.x, graphRuntimePosition.y, graphRuntimePosition.z);
                }
                else
                {
                    pos = moduleDto.GetPosition();
                }

                Quaternion rot = hasGraphTopology
                    ? graphNodeDto.GetRotation()
                    : moduleDto.GetRotation();

                if (float.IsNaN(pos.x) || float.IsInfinity(pos.x) ||
                    float.IsNaN(pos.y) || float.IsInfinity(pos.y) ||
                    float.IsNaN(pos.z) || float.IsInfinity(pos.z))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Module has invalid position. Skipping.");
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
                        Hecton8.Core.H8Debug.LogWarning(
                            "[ConstructionManager] ObjectPoolManager unavailable while loading pooled prefab.");
#endif
                        skippedCount++;
                        continue;
                    }

                    module = pool.Spawn(buildData.finalPrefab, pos, rot);
                }
                else if (!ConstructionRuntimeProxyFactory.TryCreatePlacedProxy(buildData, pos, rot, out module))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Module has no finalPrefab and proxy generation failed. Skipping.");
#endif
                    skippedCount++;
                    continue;
                }

                if (module == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Failed to spawn module.");
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
            Hecton8.Core.H8Debug.Log(
                "[ConstructionManager] Construction load completed.");
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
                    Hecton8.Core.H8Debug.LogWarning(
                        "[ConstructionManager] Habitat graph edge budget exceeded during save. Truncating persisted topology.");
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

        private static void RetireModuleInstanceWithoutDestroy(GameObject module, IObjectPoolService pool)
        {
            if (module == null)
                return;

            if (pool != null && pool.CanDespawnWithoutDestroy(module))
            {
                pool.Despawn(module);
                return;
            }

            module.transform.SetParent(null, false);
            module.SetActive(false);
        }

        private ItemCatalog ResolvePlayerItemCatalog()
        {
            IPlayerInventoryService inventoryService = _cachedPlayerInventoryService;
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

            _tickRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameTickRegistered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameTickRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
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
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    if (_registeredSaveService != null)
                        TryUnregisterSaveParticipant();

                    _cachedSaveService = currentService as ISaveService;
                    if (_isInitialized && isActiveAndEnabled)
                        TryRegisterSaveParticipant();
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _cachedObjectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _cachedPlayerInventoryService = currentService as IPlayerInventoryService;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    _cachedDataVault = currentService as IDataVault;
                    _habitatGraphManager?.SetDataVault(_cachedDataVault);
                    BaseLogisticsNetwork.BindDataVault(_cachedDataVault);
                    LogisticsPipeTransportScheduler.BindDataVault(_cachedDataVault);
                    if (_isInitialized && isActiveAndEnabled)
                        TryEnsureDeconstructionVaultBuffers();
                    break;
                case GlobalRegistryServiceSlot.GasDynamicsRuntime:
                    _cachedGasDynamics = currentService as IGasDynamicsSolver;
                    break;
                case GlobalRegistryServiceSlot.AtmosphereRuntime:
                    _cachedAtmosphereReadModel = currentService as IAtmosphereReadModel;
                    _habitatGraphManager?.SetAtmosphereReadModel(_cachedAtmosphereReadModel);
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _cachedAmbientCurrentReadModel = currentService as IAmbientCurrentReadModel;
                    _habitatGraphManager?.SetAmbientCurrentReadModel(_cachedAmbientCurrentReadModel);
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _cachedAudioService = currentService as IAudioService;
                    _habitatGraphManager?.SetAudioService(_cachedAudioService);
                    break;
                case GlobalRegistryServiceSlot.AbyssalFluidDecalRuntime:
                    _cachedFluidDecalPresentation = currentService as IFluidDecalPresentationSink;
                    _habitatGraphManager?.SetFluidDecalPresentation(_cachedFluidDecalPresentation);
                    BaseDegradationSystem.BindRuntimeServices(this, _cachedFluidDecalPresentation);
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _cachedPhysicsService = currentService as IPhysicsService;
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            TryRegisterSaveParticipant(_cachedSaveService);
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

                capacityOverflow |= !RecoverModuleJointsAfterOriginShift(module.transform, shiftOffset, ref capturedBodyCount);
            }

            RestoreCapturedJointBodyVelocities(capturedBodyCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (capacityOverflow)
                Hecton8.Core.H8Debug.LogWarning("[ConstructionManager] AUP joint recovery body cache exhausted; increase initialCapacity.", this);
#endif
        }

        private bool RecoverModuleJointsAfterOriginShift(Transform root, Vector3 shiftOffset, ref int capturedBodyCount)
        {
            Transform[] stack = _jointRecoveryTransformStack;
            if (root == null || stack == null || stack.Length <= 0)
                return true;

            bool completed = true;
            int stackCount = 1;
            stack[0] = root;
            while (stackCount > 0)
            {
                Transform current = stack[--stackCount];
                stack[stackCount] = null;
                if (current == null)
                    continue;

                completed &= RecoverJointComponent<FixedJoint>(current, shiftOffset, ref capturedBodyCount);
                completed &= RecoverJointComponent<ConfigurableJoint>(current, shiftOffset, ref capturedBodyCount);
                completed &= RecoverJointComponent<HingeJoint>(current, shiftOffset, ref capturedBodyCount);
                completed &= RecoverJointComponent<SpringJoint>(current, shiftOffset, ref capturedBodyCount);
                completed &= RecoverJointComponent<CharacterJoint>(current, shiftOffset, ref capturedBodyCount);

                int childCount = current.childCount;
                for (int childIndex = 0; childIndex < childCount; childIndex++)
                {
                    if (stackCount >= stack.Length)
                    {
                        completed = false;
                        break;
                    }

                    stack[stackCount++] = current.GetChild(childIndex);
                }
            }

            return completed;
        }

        private bool RecoverJointComponent<TJoint>(Transform owner, Vector3 shiftOffset, ref int capturedBodyCount)
            where TJoint : Joint
        {
            if (owner == null || !owner.TryGetComponent(out TJoint joint) || joint == null)
                return true;

            bool completed = true;
            if (joint.TryGetComponent(out Rigidbody ownerBody))
                completed &= TryCaptureJointBodyVelocity(ownerBody, ref capturedBodyCount);
            completed &= TryCaptureJointBodyVelocity(joint.connectedBody, ref capturedBodyCount);

            if (joint.connectedBody == null && !joint.autoConfigureConnectedAnchor)
                joint.connectedAnchor -= shiftOffset;

            return completed;
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
            IPhysicsService physicsService = _cachedPhysicsService;
            for (int i = 0; i < capturedBodyCount; i++)
            {
                Rigidbody body = _jointRecoveryBodies[i];
                if (body != null && physicsService != null)
                {
                    physicsService.QueueLinearVelocitySet(body, _jointRecoveryLinearVelocities[i]);
                    physicsService.QueueAngularVelocitySet(body, _jointRecoveryAngularVelocities[i]);
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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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
            hash = FoldAmbientAccidentHash(hash, (uint)CaptureModuleHashId(candidate));
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

            string source = CaptureModuleSource(module);
            string summary = BuildAmbientAccidentSummary(module);
            FieldOperationLogSystem.RecordOperation(source, "SERVICE ACCIDENT", summary, "WARN");

            module.ApplyDamage(module.CurrentIntegrity + 1f);
        }

        private static string CaptureModuleSource(BaseModule module)
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

        public void NotifyModuleParasiteRootStateChanged(BaseModule module)
        {
            if (_habitatGraphManager == null || module == null)
                return;

            MarkHabitatGraphDirty();
        }

        public bool TryResolveFungalMindTarget(BaseModule sourceModule, out BaseModule targetModule, out float targetPotential)
        {
            targetModule = null;
            targetPotential = 0f;
            return _habitatGraphManager != null &&
                   _habitatGraphManager.TryResolveFungalMindTarget(sourceModule, out targetModule, out targetPotential);
        }
    }
}
