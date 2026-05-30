using System;
#if UNITY_EDITOR
using System.IO;
#endif
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics.Vehicles
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Physics/Vehicle Component Damage Router")]
    public unsafe sealed class VehicleComponentDamageRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IColdTickable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int MinimumQualityHazardSignals = 8;
        private const int MaxGridWidth = 32;
        private const int MaxGridHeight = 16;
        private const int MaxGridDepth = 24;
        private const uint HazardLaneHash = 0x565A4844u; // VZHD
        private const uint VehicleFaultEventHash = 0x56444654u; // VDFT
        private const uint VehicleFaultDumpHash = 0x56534654u; // VSFT
        private static readonly ulong DamageMutationGuardMask =
            MutationGuardBit(VehicleDamageConstants.GridWriteBuffer) |
            MutationGuardBit(VehicleDamageConstants.GridReadBuffer) |
            MutationGuardBit(VehicleDamageConstants.SignalBuffer) |
            MutationGuardBit(VehicleDamageConstants.MockSignalBuffer) |
            MutationGuardBit(VehicleDamageConstants.StateWriteBuffer) |
            MutationGuardBit(VehicleDamageConstants.StateReadBuffer) |
            MutationGuardBit(VehicleDamageConstants.TuningBuffer) |
            MutationGuardBit(VehicleDamageConstants.TelemetryRingBuffer) |
            MutationGuardBit(VehicleDamageConstants.TelemetryCursorBuffer);

        [Header("Damage Grid")]
        [SerializeField, Range(2, MaxGridWidth)] private int gridWidth = VehicleDamageConstants.DefaultGridWidth;
        [SerializeField, Range(2, MaxGridHeight)] private int gridHeight = VehicleDamageConstants.DefaultGridHeight;
        [SerializeField, Range(2, MaxGridDepth)] private int gridDepth = VehicleDamageConstants.DefaultGridDepth;
        [SerializeField] private Vector3 gridCenterLocal = Vector3.zero;
        [SerializeField] private Vector3 gridSizeLocal = new Vector3(5.5f, 2.4f, 9.5f);
        [SerializeField, Min(0.01f)] private float baseArmor = 1.15f;
        [SerializeField, Min(0f)] private float directDamageScale = 1f;
        [SerializeField, Min(0.05f)] private float explosiveRadiusMeters = 1.8f;
        [SerializeField, Min(0.01f)] private float explosionFalloff = 0.85f;

        [Header("System Penalties")]
        [SerializeField, Min(0f)] private float ingressKgPerSecond = 8.5f;
        [SerializeField, Min(0f)] private float floodMassLimitKg = 7000f;
        [SerializeField, Range(0f, 1f)] private float fireChance01 = 0.12f;
        [SerializeField, Range(0f, 1f)] private float engineMinimumScalar = 0.08f;
        [SerializeField, Range(0f, 1f)] private float ballastMinimumScalar = 0.12f;
        [SerializeField, Range(0f, 1f)] private float sensorMinimumScalar = 0.1f;
        [SerializeField, Min(0f)] private float sensorPenaltyWeight = 1f;
        [SerializeField, Min(0f)] private float dragPenaltyWeight = 0.8f;

        [Header("Signal Intake")]
        [SerializeField] private uint acceptedTargetHash;
        [SerializeField] private bool enableEmergencyMockDamage;
        [SerializeField, Range(1, VehicleDamageConstants.MaxMockDamageSignals)] private int mockSignalCount = 4;
        [SerializeField, Min(0f)] private float mockMagnitude = 24000f;

        [Header("Debug")]
        [SerializeField] private bool drawDamageGizmos = true;
        [SerializeField, Range(1, 512)] private int maxGizmoCells = 192;

        private IDataVault _dataVault;
        private VaultGenerationHandle<VehicleGridCellDTO> _gridWriteHandle;
        private VaultGenerationHandle<VehicleGridCellDTO> _gridReadHandle;
        private VaultGenerationHandle<VehicleDamageSignalDTO> _signalHandle;
        private VaultGenerationHandle<VehicleDamageSignalDTO> _mockSignalHandle;
        private VaultGenerationHandle<VehicleDamageStateDTO> _stateWriteHandle;
        private VaultGenerationHandle<VehicleDamageStateDTO> _stateReadHandle;
        private VaultGenerationHandle<VehicleDamageTuningDTO> _tuningHandle;
        private VaultGenerationHandle<VehicleDamageTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<uint> _telemetryCursorHandle;
        private VaultGenerationHandle<SubmarineKinematicConfig> _kinematicConfigHandle;
        private JobHandle _damageHandle;
        private bool _damagePending;
        private bool _buffersLocked;
        private IDataVault _damageGuardVault;
        private bool _buffersReady;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredCold;
        private bool _registeredLate;
        private bool _registeredSlow;
        private bool _registeredHotSwapListener;
        private bool _dumpWritten;
        private bool _csvLoaded;
        private bool _coreBlackboxWarmed;
        private int _cellCount;
        private uint _frameCounter;
        private uint _resolvedVehicleHash;
        private long _csvStampUtcTicks;
        private double3 _cachedRootAup;
        private quaternion _cachedRootRotation;
        private bool _hasRootPoseSnapshot;
#if UNITY_EDITOR
        private string _projectRoot;
        private string _csvPath;
#endif

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 63);
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!VehicleDamageLayoutValidator.ValidateVehicleGridCellLayout(out string layoutError))
                Hecton8.Core.H8Debug.LogError(layoutError, this);
#endif

#if UNITY_EDITOR
            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "vehicle_component_layouts.csv");
#endif
            _resolvedVehicleHash = ResolveVehicleHash();
            EnsureSignalLanes();
            TryRegisterHotSwapListener();
            CacheDataVaultCold();
            EnsureVaultBuffers(forceReinitialize: false);
            WarmCoreBlackboxRoute();
            TryRefreshRootPoseSnapshot(transform, allowPresentationFallback: true);

            TryRegisterRuntimeLanes();
        }

        private void OnDisable()
        {
            CompleteDamageForLifecycle();
            DumpBlackBoxIfFaulted();

            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLanes();
            _coreBlackboxWarmed = false;
        }

        private void OnDestroy()
        {
            CompleteDamageForLifecycle();
            ReleaseOwnedVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = null;
            _buffersReady = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (currentService == null || !isActiveAndEnabled)
                    return;

                TryUnregisterRuntimeLanes();
                TryRegisterRuntimeLanes();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            RebindDataVaultForLifecycle(currentService as IDataVault);
            if (isActiveAndEnabled && _dataVault != null)
            {
                EnsureVaultBuffers(forceReinitialize: false);
                WarmCoreBlackboxRoute();
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_damagePending || !_buffersReady)
                return;

            if (!LockDamageBuffers())
                return;

            bool keepDamageGuard = false;
            try
            {
                if (!TryReadWritablePointers(
                        out VehicleGridCellDTO* gridWrite,
                        out VehicleGridCellDTO* gridRead,
                        out VehicleDamageSignalDTO* signals,
                        out VehicleDamageSignalDTO* mockSignals,
                        out VehicleDamageStateDTO* stateWrite,
                        out VehicleDamageStateDTO* stateRead,
                        out int gridWriteCapacity,
                        out int gridReadCapacity,
                        out int signalCapacity,
                        out int mockSignalCapacity,
                        out int stateWriteCapacity,
                        out int stateReadCapacity,
                        out NativeArray<VehicleDamageTelemetryEntry> telemetry,
                        out NativeArray<uint> telemetryCursor,
                        out NativeArray<VehicleDamageTuningDTO> tuningArray,
                        out NativeArray<VehicleDamageSignalDTO> signalArray))
                {
                    _buffersReady = false;
                    return;
                }

                VehicleDamageTuningDTO tuning = UpdateTuningSnapshot(tuningArray);
                int realSignalCount = GatherCombatDamageSignals(signalArray, in tuning);
                int mockCount = math.min(
                    ResolveMockSignalCount(),
                    math.min(mockSignalCapacity, math.max(0, signalCapacity - realSignalCount)));
                int totalSignalCount = math.min(realSignalCount + mockCount, signalCapacity);

                Transform root = transform;
                if (!TryReadAuthoritativeRootPose(root, out double3 rootAup, out quaternion rootRotation))
                    return;

                quaternion inverseRotation = math.conjugate(rootRotation);
                uint frame = ++_frameCounter;
                float quality = ResolveQualityWeight();
                uint vehicleHash = _resolvedVehicleHash;
                float depthMeters = ResolveDepthMeters(rootAup);

                JobHandle dependency = default;
                if (mockCount > 0)
                {
                    GenerateMockVehicleDamageJob mockJob = new GenerateMockVehicleDamageJob
                    {
                        Signals = mockSignals,
                        SignalCount = mockCount,
                        RootAup = rootAup,
                        Frame = frame,
                        RadiusMeters = explosiveRadiusMeters,
                        Magnitude = mockMagnitude
                    };

                    JobHandle mockHandle = mockJob.Schedule(mockCount, VehicleDamageConstants.JobBatchSize, dependency);
                    CopyVehicleDamageSignalsJob copyJob = new CopyVehicleDamageSignalsJob
                    {
                        Source = mockSignals,
                        Destination = signals,
                        SourceCount = mockCount,
                        DestinationOffset = realSignalCount,
                        DestinationCapacity = signalCapacity
                    };
                    dependency = copyJob.Schedule(mockCount, VehicleDamageConstants.JobBatchSize, mockHandle);
                }

                if (totalSignalCount > 0)
                {
                    MapImpactToGridJob mapJob = new MapImpactToGridJob
                    {
                        Cells = gridWrite,
                        Signals = signals,
                        SignalCount = totalSignalCount,
                        GridWidth = tuning.GridWidth,
                        GridHeight = tuning.GridHeight,
                        GridDepth = tuning.GridDepth,
                        RootAup = rootAup,
                        InverseRootRotation = inverseRotation,
                        GridCenterLocal = tuning.GridCenterLocal,
                        GridSizeLocal = tuning.GridSizeLocal,
                        DirectDamageScale = tuning.DirectDamageScale
                    };

                    dependency = mapJob.Schedule(totalSignalCount, VehicleDamageConstants.JobBatchSize, dependency);

                    ApplyVehicleDamageReductionJob reductionJob = new ApplyVehicleDamageReductionJob
                    {
                        Cells = gridWrite,
                        Signals = signals,
                        SignalCount = totalSignalCount,
                        CellCount = _cellCount,
                        GridWidth = tuning.GridWidth,
                        GridHeight = tuning.GridHeight,
                        GridDepth = tuning.GridDepth,
                        GridSizeLocal = tuning.GridSizeLocal,
                        DirectDamageScale = tuning.DirectDamageScale,
                        ExplosionFalloff = tuning.ExplosionFalloff
                    };

                    dependency = reductionJob.Schedule(_cellCount, VehicleDamageConstants.JobBatchSize, dependency);
                }

                EvaluateVehicleSystemsJob evaluateJob = new EvaluateVehicleSystemsJob
                {
                    Cells = gridWrite,
                    Signals = signals,
                    StateWrite = stateWrite,
                    Telemetry = telemetry,
                    TelemetryCursor = telemetryCursor,
                    HazardWriter = SignalBus<VehicleHazardSignal>.ParallelWriter,
                    HazardWriterBudget = SignalBus<VehicleHazardSignal>.ParallelWriterBudget,
                    CellCount = _cellCount,
                    SignalCount = totalSignalCount,
                    Frame = frame,
                    VehicleHash = vehicleHash,
                    RootAup = rootAup,
                    FixedDeltaTime = fixedDeltaTime,
                    RootDepthMeters = depthMeters,
                    GlobalQualityWeight = quality,
                    Tuning = tuning
                };

                dependency = evaluateJob.Schedule(dependency);

                PublishVehicleDamageStateJob publishJob = new PublishVehicleDamageStateJob
                {
                    GridWrite = gridWrite,
                    GridRead = gridRead,
                    StateWrite = stateWrite,
                    StateRead = stateRead,
                    CellCount = _cellCount,
                    GridWriteCapacity = gridWriteCapacity,
                    GridReadCapacity = gridReadCapacity,
                    StateWriteCapacity = stateWriteCapacity,
                    StateReadCapacity = stateReadCapacity
                };

                _damageHandle = publishJob.Schedule(dependency);
                H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _damageHandle);
                _damagePending = true;
                keepDamageGuard = true;
            }
            finally
            {
                if (!keepDamageGuard)
                    UnlockDamageBuffers();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_damagePending)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _damageHandle))
                return;

            _damagePending = false;
            UnlockDamageBuffers();
            DumpBlackBoxIfFaulted();
        }

        public void LateFrameTick()
        {
            TryRefreshRootPoseSnapshot(transform, allowPresentationFallback: true);
        }

        public void SlowTick()
        {
            if (!_buffersReady)
                return;
        }

        public void ColdTick()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;

            EnsureDataVault();
            if (!_damagePending && !_buffersLocked)
                EnsureVaultBuffers(forceReinitialize: false);

#if UNITY_EDITOR
            if (!_damagePending && !_buffersLocked)
                TryLoadCsvLayout();
#endif
        }

        private bool EnsureDataVault()
        {
            return _dataVault != null;
        }

        private void CacheDataVaultCold()
        {
            IDataVault currentVault = GlobalRegistry.DataVault;
            if (!ReferenceEquals(_dataVault, currentVault))
                RebindDataVaultForLifecycle(currentVault);
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredCold)
                _registeredCold = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterRuntimeLanes()
        {
            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredPostFixed)
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            if (_registeredCold)
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredFixed = false;
            _registeredPostFixed = false;
            _registeredCold = false;
            _registeredLate = false;
            _registeredSlow = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void ClearVaultHandles()
        {
            _gridWriteHandle = default;
            _gridReadHandle = default;
            _signalHandle = default;
            _mockSignalHandle = default;
            _stateWriteHandle = default;
            _stateReadHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _kinematicConfigHandle = default;
            _cellCount = 0;
            _csvLoaded = false;
        }

        private void CompleteDamageForLifecycle()
        {
            if (_damagePending)
                ForceCompleteDamageInPostFixedWindow();

            _damagePending = false;
            UnlockDamageBuffers();
        }

        private void ForceCompleteDamageInPostFixedWindow()
        {
            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _damageHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
                return;

            CompleteDamageForLifecycle();
            ReleaseOwnedVaultHandles(_dataVault);
            ClearVaultHandles();
            _dataVault = nextVault;
            _buffersReady = false;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _gridWriteHandle);
            ReleaseOwnedVaultHandle(vault, ref _gridReadHandle);
            ReleaseOwnedVaultHandle(vault, ref _signalHandle);
            ReleaseOwnedVaultHandle(vault, ref _mockSignalHandle);
            ReleaseOwnedVaultHandle(vault, ref _stateWriteHandle);
            ReleaseOwnedVaultHandle(vault, ref _stateReadHandle);
            ReleaseOwnedVaultHandle(vault, ref _tuningHandle);
            ReleaseOwnedVaultHandle(vault, ref _telemetryHandle);
            ReleaseOwnedVaultHandle(vault, ref _telemetryCursorHandle);
        }

        private static void ReleaseOwnedVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (vault != null &&
                handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)SystemID.VehiclesPhysics)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryOpenArrayForOwner<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return _dataVault != null &&
                   handle.BufferID != 0u &&
                   _dataVault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryOpenLocalPointerForOwner<T>(in VaultGenerationHandle<T> handle, out void* pointer) where T : struct
        {
            pointer = null;
            if (!TryOpenArrayForOwner(in handle, out NativeArray<T> buffer))
                return false;

            pointer = NativeArrayUnsafeUtility.GetUnsafePtr(buffer);
            return pointer != null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref T ElementRef<T>(NativeArray<T> buffer, int index) where T : struct
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(NativeArrayUnsafeUtility.GetUnsafePtr(buffer), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref readonly T ElementReadOnlyRef<T>(NativeArray<T> buffer, int index) where T : struct
        {
            return ref UnsafeUtility.ArrayElementAsRef<T>(
                (void*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(buffer),
                index);
        }

        private bool EnsureVaultBuffers(bool forceReinitialize)
        {
            if (!EnsureDataVault())
                return false;

            if (_dataVault.IsAllocationLocked || _dataVault.IsCompactionFenceActive)
                return false;

            int width = math.clamp(gridWidth, 2, MaxGridWidth);
            int height = math.clamp(gridHeight, 2, MaxGridHeight);
            int depth = math.clamp(gridDepth, 2, MaxGridDepth);
            int cellCount = width * height * depth;
            bool reinitialize = forceReinitialize || cellCount != _cellCount;

            _gridWriteHandle = _dataVault.EnsureGenerationHandle<VehicleGridCellDTO>(VehicleDamageConstants.GridWriteBuffer, cellCount, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _gridReadHandle = _dataVault.EnsureGenerationHandle<VehicleGridCellDTO>(VehicleDamageConstants.GridReadBuffer, cellCount, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _signalHandle = _dataVault.EnsureGenerationHandle<VehicleDamageSignalDTO>(VehicleDamageConstants.SignalBuffer, VehicleDamageConstants.MaxDamageSignals, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _mockSignalHandle = _dataVault.EnsureGenerationHandle<VehicleDamageSignalDTO>(VehicleDamageConstants.MockSignalBuffer, VehicleDamageConstants.MaxMockDamageSignals, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _stateWriteHandle = _dataVault.EnsureGenerationHandle<VehicleDamageStateDTO>(VehicleDamageConstants.StateWriteBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _stateReadHandle = _dataVault.EnsureGenerationHandle<VehicleDamageStateDTO>(VehicleDamageConstants.StateReadBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _tuningHandle = _dataVault.EnsureGenerationHandle<VehicleDamageTuningDTO>(VehicleDamageConstants.TuningBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _dataVault.EnsureGenerationHandle<VehicleDamageTelemetryEntry>(VehicleDamageConstants.TelemetryRingBuffer, VehicleDamageConstants.TelemetryCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = _dataVault.EnsureGenerationHandle<uint>(VehicleDamageConstants.TelemetryCursorBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            if (!IsHandleValid(in _gridWriteHandle) || !IsHandleValid(in _gridReadHandle) || !IsHandleValid(in _signalHandle) ||
                !IsHandleValid(in _mockSignalHandle) || !IsHandleValid(in _stateWriteHandle) || !IsHandleValid(in _stateReadHandle) ||
                !IsHandleValid(in _tuningHandle) || !IsHandleValid(in _telemetryHandle) || !IsHandleValid(in _telemetryCursorHandle))
            {
                _buffersReady = false;
                return false;
            }

            _cellCount = cellCount;
            if (reinitialize)
                InitializeGridBuffers();

            _buffersReady = true;
            return true;
        }

        private void InitializeGridBuffers()
        {
            bool locked = false;
            try
            {
                locked = LockDamageBuffers();
                if (!locked)
                    return;

                if (!TryOpenLocalPointerForOwner(in _gridWriteHandle, out void* writePtr) ||
                    !TryOpenLocalPointerForOwner(in _gridReadHandle, out void* readPtr) ||
                    !TryOpenArrayForOwner(in _tuningHandle, out NativeArray<VehicleDamageTuningDTO> tuningArray))
                {
                    return;
                }

                VehicleGridCellDTO* write = (VehicleGridCellDTO*)writePtr;
                VehicleGridCellDTO* read = (VehicleGridCellDTO*)readPtr;
                VehicleDamageTuningDTO tuning = UpdateTuningSnapshot(tuningArray);

                InitializeVehicleGridJob initWrite = new InitializeVehicleGridJob
                {
                    Cells = write,
                    GridWidth = tuning.GridWidth,
                    GridHeight = tuning.GridHeight,
                    GridDepth = tuning.GridDepth,
                    BaseArmor = tuning.BaseArmor
                };

                InitializeVehicleGridJob initRead = initWrite;
                initRead.Cells = read;
                for (int i = 0; i < _cellCount; i++)
                {
                    initWrite.Execute(i);
                    initRead.Execute(i);
                }

                TryOpenArrayForOwner(in _stateWriteHandle, out NativeArray<VehicleDamageStateDTO> stateWrite);
                TryOpenArrayForOwner(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO> stateRead);
                if (stateWrite.IsCreated)
                    stateWrite[0] = BuildDefaultState();
                if (stateRead.IsCreated)
                    stateRead[0] = BuildDefaultState();

                TryOpenArrayForOwner(in _telemetryCursorHandle, out NativeArray<uint> cursor);
                if (cursor.IsCreated)
                    cursor[0] = 0u;
            }
            finally
            {
                if (locked)
                    UnlockDamageBuffers();
            }
        }

        private VehicleDamageTuningDTO BuildTuning()
        {
            Vector3 safeSize = new Vector3(
                math.max(0.001f, math.abs(gridSizeLocal.x)),
                math.max(0.001f, math.abs(gridSizeLocal.y)),
                math.max(0.001f, math.abs(gridSizeLocal.z)));

            VehicleDamageTuningDTO tuning = default;
            tuning.GridWidth = math.clamp(gridWidth, 2, MaxGridWidth);
            tuning.GridHeight = math.clamp(gridHeight, 2, MaxGridHeight);
            tuning.GridDepth = math.clamp(gridDepth, 2, MaxGridDepth);
            tuning.CellSizeMeters = math.cmin(new float3(safeSize.x / tuning.GridWidth, safeSize.y / tuning.GridHeight, safeSize.z / tuning.GridDepth));
            tuning.GridCenterLocal = new float3(gridCenterLocal.x, gridCenterLocal.y, gridCenterLocal.z);
            tuning.GridSizeLocal = new float3(safeSize.x, safeSize.y, safeSize.z);
            tuning.BaseArmor = math.max(0.01f, baseArmor);
            tuning.DirectDamageScale = math.max(0f, directDamageScale);
            tuning.ExplosiveRadiusMeters = math.max(0.05f, explosiveRadiusMeters);
            tuning.ExplosionFalloff = math.max(0.01f, explosionFalloff);
            tuning.IngressKgPerSecond = math.max(0f, ingressKgPerSecond);
            tuning.FireChance01 = math.saturate(fireChance01);
            tuning.SensorPenaltyWeight = math.max(0f, sensorPenaltyWeight);
            tuning.DragPenaltyWeight = math.max(0f, dragPenaltyWeight);
            tuning.FloodMassLimitKg = math.max(0f, floodMassLimitKg);
            tuning.SourceHash = VehicleDamageConstants.SourceHashRuntime;
            tuning.EngineMinimumScalar = math.saturate(engineMinimumScalar);
            tuning.BallastMinimumScalar = math.saturate(ballastMinimumScalar);
            tuning.SensorMinimumScalar = math.saturate(sensorMinimumScalar);
            tuning.Flags = VehicleDamageConstants.TuningFlagRuntimeSerialized;
            return tuning;
        }

        private VehicleDamageTuningDTO UpdateTuningSnapshot(NativeArray<VehicleDamageTuningDTO> tuningArray)
        {
            VehicleDamageTuningDTO serialized = BuildTuning();
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return serialized;

            VehicleDamageTuningDTO current = tuningArray[0];
            bool externalOverride =
                current.SourceHash == VehicleDamageConstants.SourceHashCsv ||
                current.SourceHash == VehicleDamageConstants.SourceHashEditor ||
                (current.Flags & (VehicleDamageConstants.TuningFlagCsvLayout | VehicleDamageConstants.TuningFlagEditorOverride)) != 0u;

            if (!externalOverride)
            {
                tuningArray[0] = serialized;
                return serialized;
            }

            current.GridWidth = serialized.GridWidth;
            current.GridHeight = serialized.GridHeight;
            current.GridDepth = serialized.GridDepth;
            current.CellSizeMeters = serialized.CellSizeMeters;
            current.GridCenterLocal = serialized.GridCenterLocal;
            current.GridSizeLocal = serialized.GridSizeLocal;
            current.BaseArmor = PositiveOrFallback(current.BaseArmor, serialized.BaseArmor, 0.01f);
            current.DirectDamageScale = PositiveOrFallback(current.DirectDamageScale, serialized.DirectDamageScale, 0f);
            current.ExplosiveRadiusMeters = PositiveOrFallback(current.ExplosiveRadiusMeters, serialized.ExplosiveRadiusMeters, 0.05f);
            current.ExplosionFalloff = PositiveOrFallback(current.ExplosionFalloff, serialized.ExplosionFalloff, 0.01f);
            current.IngressKgPerSecond = PositiveOrFallback(current.IngressKgPerSecond, serialized.IngressKgPerSecond, 0f);
            current.FireChance01 = math.saturate(math.select(serialized.FireChance01, current.FireChance01, math.isfinite(current.FireChance01)));
            current.SensorPenaltyWeight = PositiveOrFallback(current.SensorPenaltyWeight, serialized.SensorPenaltyWeight, 0f);
            current.DragPenaltyWeight = PositiveOrFallback(current.DragPenaltyWeight, serialized.DragPenaltyWeight, 0f);
            current.FloodMassLimitKg = PositiveOrFallback(current.FloodMassLimitKg, serialized.FloodMassLimitKg, 0f);
            current.EngineMinimumScalar = math.saturate(math.select(serialized.EngineMinimumScalar, current.EngineMinimumScalar, math.isfinite(current.EngineMinimumScalar)));
            current.BallastMinimumScalar = math.saturate(math.select(serialized.BallastMinimumScalar, current.BallastMinimumScalar, math.isfinite(current.BallastMinimumScalar)));
            current.SensorMinimumScalar = math.saturate(math.select(serialized.SensorMinimumScalar, current.SensorMinimumScalar, math.isfinite(current.SensorMinimumScalar)));
            current.Flags &= ~VehicleDamageConstants.TuningFlagRuntimeSerialized;
            tuningArray[0] = current;
            return current;
        }

        private static float PositiveOrFallback(float value, float fallback, float minimum)
        {
            if (!math.isfinite(value) || value < minimum)
                return fallback;

            return value;
        }

        private static VehicleDamageStateDTO BuildDefaultState()
        {
            VehicleDamageStateDTO state = default;
            state.MaxThrustScalar = 1f;
            state.BuoyancyScalar = 1f;
            state.SensorScalar = 1f;
            state.DragScalar = 1f;
            state.StructuralIntegrity01 = 1f;
            state.Flags = VehicleDamageConstants.StateFlagInitialized;
            return state;
        }

        private int GatherCombatDamageSignals(NativeArray<VehicleDamageSignalDTO> target, in VehicleDamageTuningDTO tuning)
        {
            if (!target.IsCreated)
                return 0;

            ReadOnlySpan<CombatDamageSignal> signals = SignalBus<CombatDamageSignal>.GetFrameSnapshot();
            int count = 0;
            int capacity = math.min(target.Length, VehicleDamageConstants.MaxDamageSignals);
            for (int i = 0; i < signals.Length && count < capacity; i++)
            {
                CombatDamageSignal signal = signals[i];
                if ((signal.Flags & CombatDamageSignal.VisualOnlyFlag) != 0)
                    continue;

                if (acceptedTargetHash != 0u && signal.TargetHash != 0u && signal.TargetHash != acceptedTargetHash)
                    continue;

                if (!CombatDamageSignalCodec.IsFiniteAup(signal.ImpactAup))
                    continue;

                VehicleDamageSignalDTO dto = default;
                dto.ImpactAup = signal.ImpactAup;
                dto.Direction = signal.Direction;
                dto.Magnitude = math.max(0f, signal.Magnitude);
                dto.DamageType = signal.DamageType;
                dto.TargetHash = signal.TargetHash;
                dto.SourceHash = signal.SourceHash;
                dto.Frame = signal.Frame;
                dto.SourceId = signal.SourceId;
                dto.TargetId = signal.TargetId;
                dto.Channel = signal.Channel;
                dto.Flags = signal.Flags;
                dto.IntegrityDelta = signal.IntegrityDelta;
                dto.RadiusMeters = math.max(0.05f, tuning.ExplosiveRadiusMeters + (dto.Magnitude * 0.000006f));
                dto.Falloff = tuning.ExplosionFalloff;
                dto.ArmorPierce = math.saturate(signal.IntegrityDelta * (1f / 64f));
                dto.GridIndex = -1;
                dto.MappedFlags = VehicleDamageConstants.DamageFlagFiniteAup;
                if ((signal.DamageType & VehicleDamageConstants.DamageTypeExplosiveMask) != 0u)
                    dto.MappedFlags |= VehicleDamageConstants.DamageFlagExplosive;

                target[count++] = dto;
            }

            return count;
        }

        private int ResolveMockSignalCount()
        {
            if (!enableEmergencyMockDamage)
                return 0;

            int maxCount = math.clamp(mockSignalCount, 1, VehicleDamageConstants.MaxMockDamageSignals);
            return math.min(maxCount, VehicleDamageConstants.MaxDamageSignals);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private uint ResolveVehicleHash()
        {
            uint fallback = Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
            return math.select(fallback, acceptedTargetHash, acceptedTargetHash != 0u);
        }

        private bool TryReadWritablePointers(
            out VehicleGridCellDTO* gridWrite,
            out VehicleGridCellDTO* gridRead,
            out VehicleDamageSignalDTO* signals,
            out VehicleDamageSignalDTO* mockSignals,
            out VehicleDamageStateDTO* stateWrite,
            out VehicleDamageStateDTO* stateRead,
            out int gridWriteCapacity,
            out int gridReadCapacity,
            out int signalCapacity,
            out int mockSignalCapacity,
            out int stateWriteCapacity,
            out int stateReadCapacity,
            out NativeArray<VehicleDamageTelemetryEntry> telemetry,
            out NativeArray<uint> telemetryCursor,
            out NativeArray<VehicleDamageTuningDTO> tuning,
            out NativeArray<VehicleDamageSignalDTO> signalArray)
        {
            gridWrite = null;
            gridRead = null;
            signals = null;
            mockSignals = null;
            stateWrite = null;
            stateRead = null;
            gridWriteCapacity = 0;
            gridReadCapacity = 0;
            signalCapacity = 0;
            mockSignalCapacity = 0;
            stateWriteCapacity = 0;
            stateReadCapacity = 0;
            telemetry = default;
            telemetryCursor = default;
            tuning = default;
            signalArray = default;

            if (!TryOpenArrayForOwner(in _gridWriteHandle, out NativeArray<VehicleGridCellDTO> gridWriteArray) ||
                !TryOpenArrayForOwner(in _gridReadHandle, out NativeArray<VehicleGridCellDTO> gridReadArray) ||
                !TryOpenArrayForOwner(in _signalHandle, out signalArray) ||
                !TryOpenArrayForOwner(in _mockSignalHandle, out NativeArray<VehicleDamageSignalDTO> mockSignalArray) ||
                !TryOpenArrayForOwner(in _stateWriteHandle, out NativeArray<VehicleDamageStateDTO> stateWriteArray) ||
                !TryOpenArrayForOwner(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO> stateReadArray) ||
                !TryOpenArrayForOwner(in _telemetryHandle, out telemetry) ||
                !TryOpenArrayForOwner(in _telemetryCursorHandle, out telemetryCursor) ||
                !TryOpenArrayForOwner(in _tuningHandle, out tuning))
            {
                return false;
            }

            gridWriteCapacity = gridWriteArray.Length;
            gridReadCapacity = gridReadArray.Length;
            signalCapacity = math.min(signalArray.Length, VehicleDamageConstants.MaxDamageSignals);
            mockSignalCapacity = math.min(mockSignalArray.Length, VehicleDamageConstants.MaxMockDamageSignals);
            stateWriteCapacity = stateWriteArray.Length;
            stateReadCapacity = stateReadArray.Length;

            if (gridWriteCapacity < _cellCount ||
                gridReadCapacity < _cellCount ||
                signalCapacity <= 0 ||
                mockSignalCapacity < 0 ||
                stateWriteCapacity <= 0 ||
                stateReadCapacity <= 0 ||
                telemetry.Length <= 0 ||
                telemetryCursor.Length <= 0 ||
                tuning.Length <= 0)
            {
                return false;
            }

            gridWrite = (VehicleGridCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(gridWriteArray);
            gridRead = (VehicleGridCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(gridReadArray);
            signals = (VehicleDamageSignalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(signalArray);
            mockSignals = (VehicleDamageSignalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(mockSignalArray);
            stateWrite = (VehicleDamageStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateWriteArray);
            stateRead = (VehicleDamageStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(stateReadArray);
            return gridWrite != null && gridRead != null && signals != null && mockSignals != null &&
                    stateWrite != null && stateRead != null && telemetry.IsCreated && telemetryCursor.IsCreated && tuning.IsCreated;
        }

        private bool LockDamageBuffers()
        {
            if (_buffersLocked)
                return _buffersLocked;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(DamageMutationGuardMask))
                return false;

            _damageGuardVault = vault;
            _buffersLocked = true;
            return true;
        }

        private void UnlockDamageBuffers()
        {
            if (!_buffersLocked)
            {
                _damageGuardVault = null;
                return;
            }

            IDataVault vault = _damageGuardVault;
            _damageGuardVault = null;
            _buffersLocked = false;
            vault?.ReleaseMutationGuard(DamageMutationGuardMask);
        }

#if UNITY_EDITOR
        private bool TryLoadCsvLayout()
        {
            IDataVault vault = _dataVault;
            if (!_buffersReady || vault == null || !File.Exists(_csvPath))
                return false;

            NativeArray<VehicleGridCellDTO> stagedGrid = default;
            try
            {
                FileInfo info = new FileInfo(_csvPath);
                long stamp = info.LastWriteTimeUtc.Ticks;
                if (_csvLoaded && stamp == _csvStampUtcTicks)
                    return false;

                if (info.Length <= 0L || info.Length > VehicleDamageConstants.CsvImportByteCapacity)
                    return false;

                int cellCount = math.min(_cellCount, MaxGridWidth * MaxGridHeight * MaxGridDepth);
                if (cellCount <= 0)
                    return false;

                stagedGrid = new NativeArray<VehicleGridCellDTO>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                if (!TryStageCsvGrid(stagedGrid, cellCount))
                    return false;

                Span<byte> bytes = stackalloc byte[VehicleDamageConstants.CsvImportByteCapacity];
                int expectedBytes = (int)info.Length;
                int read = 0;
                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    while (read < expectedBytes)
                    {
                        int chunk = stream.Read(bytes.Slice(read, expectedBytes - read));
                        if (chunk <= 0)
                            return false;
                        read += chunk;
                    }
                }

                if (read != expectedBytes)
                    return false;

                int applied = VehicleComponentLayoutCsvParser.Apply(
                    bytes.Slice(0, read),
                    stagedGrid,
                    gridWidth,
                    gridHeight,
                    gridDepth);
                if (applied <= 0)
                    return false;

                if (!TryCommitCsvGrid(in _gridWriteHandle, stagedGrid, cellCount))
                    return false;
                if (!TryCommitCsvGrid(in _gridReadHandle, stagedGrid, cellCount))
                    return false;
                if (!TryCommitCsvTuning())
                    return false;

                _csvLoaded = true;
                _csvStampUtcTicks = stamp;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                if (stagedGrid.IsCreated)
                    stagedGrid.Dispose();
            }
        }

        private bool TryStageCsvGrid(NativeArray<VehicleGridCellDTO> scratch, int cellCount)
        {
            if (!scratch.IsCreated || cellCount <= 0 || cellCount > scratch.Length)
                return false;

            IDataVault vault = _dataVault;
            if (vault != null &&
                IsHandleValid(in _gridReadHandle) &&
                vault.TryReadOnlyHandle(in _gridReadHandle, out NativeArray<VehicleGridCellDTO>.ReadOnly gridRead) &&
                gridRead.Length >= cellCount)
            {
                for (int i = 0; i < cellCount; i++)
                    scratch[i] = gridRead[i];
                return true;
            }

            VehicleDamageTuningDTO tuning = BuildTuning();
            for (int i = 0; i < cellCount; i++)
                scratch[i] = BuildDefaultGridCell(i, in tuning);
            return true;
        }

        private bool TryCommitCsvGrid(
            in VaultGenerationHandle<VehicleGridCellDTO> handle,
            NativeArray<VehicleGridCellDTO> source,
            int cellCount)
        {
            IDataVault vault = _dataVault;
            if (vault == null || !source.IsCreated || cellCount <= 0 || cellCount > source.Length)
                return false;

            bool lockAcquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.VehiclesPhysics, out NativeArray<VehicleGridCellDTO> target))
                {
                    return false;
                }
                lockAcquired = true;
                if (target.Length < cellCount)
                    return false;

                long byteCount = (long)cellCount * UnsafeUtility.SizeOf<VehicleGridCellDTO>();
                if (!UnsafeMemoryCopyGuard.SafeCopy(
                        NativeArrayUnsafeUtility.GetUnsafePtr(target),
                        (long)target.Length * UnsafeUtility.SizeOf<VehicleGridCellDTO>(),
                        NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source),
                        byteCount))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in handle, SystemID.VehiclesPhysics);
            }
        }

        private bool TryCommitCsvTuning()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            bool lockAcquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.VehiclesPhysics, out NativeArray<VehicleDamageTuningDTO> tuning))
                {
                    return false;
                }
                lockAcquired = true;
                if (tuning.Length <= 0)
                    return false;

                VehicleDamageTuningDTO current = tuning[0];
                if (current.SourceHash == 0u)
                    current = BuildTuning();

                current.SourceHash = VehicleDamageConstants.SourceHashCsv;
                current.Flags &= ~VehicleDamageConstants.TuningFlagRuntimeSerialized;
                current.Flags |= VehicleDamageConstants.TuningFlagCsvLayout;
                tuning[0] = current;
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _tuningHandle, SystemID.VehiclesPhysics);
            }
        }

        private static VehicleGridCellDTO BuildDefaultGridCell(int index, in VehicleDamageTuningDTO tuning)
        {
            int width = math.max(1, tuning.GridWidth);
            int height = math.max(1, tuning.GridHeight);
            int depth = math.max(1, tuning.GridDepth);
            int x;
            int y;
            int z;
            DecodeGridIndex(index, width, height, out x, out y, out z);

            bool outer = x == 0 || y == 0 || z == 0 || x == width - 1 || y == height - 1 || z == depth - 1;
            uint component = ResolveDefaultGridComponentHash(x, y, z, width, height, depth);
            uint flags = math.select(0u, VehicleDamageConstants.CellFlagOuterHull, outer);
            if (component == VehicleDamageConstants.ComponentEngine)
                flags |= VehicleDamageConstants.CellFlagEngineCritical | VehicleDamageConstants.CellFlagFlammable;
            else if (component == VehicleDamageConstants.ComponentBallast)
                flags |= VehicleDamageConstants.CellFlagBallastCritical;
            else if (component == VehicleDamageConstants.ComponentSensors)
                flags |= VehicleDamageConstants.CellFlagSensorCritical;
            else if (component == VehicleDamageConstants.ComponentPower)
                flags |= VehicleDamageConstants.CellFlagFlammable;

            VehicleGridCellDTO cell = default;
            cell.Integrity01 = 1f;
            cell.ComponentHash = component;
            cell.StatusFlags = flags;
            cell.ArmorValue = math.max(0.01f, tuning.BaseArmor * math.select(1f, 1.3f, outer));
            return cell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DecodeGridIndex(int index, int width, int height, out int x, out int y, out int z)
        {
            int layer = width * height;
            z = index / layer;
            int rem = index - (z * layer);
            y = rem / width;
            x = rem - (y * width);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ResolveDefaultGridComponentHash(int x, int y, int z, int width, int height, int depth)
        {
            int aftStart = (depth * 5) / 8;
            int bowSensors = depth / 4;
            if (z >= aftStart && y <= (height * 2) / 3)
                return VehicleDamageConstants.ComponentEngine;
            if (y <= height / 3 && z > bowSensors && z < aftStart)
                return VehicleDamageConstants.ComponentBallast;
            if (z <= bowSensors || y >= (height * 2) / 3)
                return VehicleDamageConstants.ComponentSensors;
            if (x == width / 2 || x == (width / 2) - 1)
                return VehicleDamageConstants.ComponentPower;
            return VehicleDamageConstants.ComponentHull;
        }
#endif

        private bool DumpBlackBoxIfFaulted()
        {
            IDataVault vault = _dataVault;
            if (_dumpWritten || vault == null || !IsHandleValid(in _stateReadHandle))
                return false;

            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return false;

            float structuralIntegrity01 = 0f;
            uint stateHash = 0u;

            if (!vault.TryReadOnlyHandle(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO>.ReadOnly stateRead) ||
                stateRead.Length <= 0)
            {
                return false;
            }

            VehicleDamageStateDTO state = stateRead[0];
            if ((state.Flags & VehicleDamageConstants.StateFlagFatalNan) == 0u)
                return false;

            structuralIntegrity01 = state.StructuralIntegrity01;
            stateHash = state.StateHash;

            GlobalTelemetryBus.PushEvent(VehicleFaultEventHash, structuralIntegrity01, stateHash);
            bool written = GlobalTelemetryBus.TryDumpBlackboxNow(VehicleFaultDumpHash);
            _dumpWritten |= written;
            return written;
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed || !Application.isPlaying)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private static void EnsureSignalLanes()
        {
            SignalBus<CombatDamageSignal>.EnsureInitialized();
            SignalBus<VehicleHazardSignal>.Configure(VehicleDamageConstants.MaxDamageSignals, VehicleDamageConstants.MaxDamageSignals, MinimumQualityHazardSignals, HazardLaneHash);
            SignalBus<VehicleHazardSignal>.EnsureInitialized();
        }

        private bool TryReadAuthoritativeRootPose(Transform root, out double3 rootAup, out quaternion rootRotation)
        {
            if (_hasRootPoseSnapshot &&
                math.all(math.isfinite(_cachedRootAup)) &&
                math.all(math.isfinite(_cachedRootRotation.value)))
            {
                rootAup = _cachedRootAup;
                rootRotation = NormalizeOrIdentity(_cachedRootRotation);
                return true;
            }

#if UNITY_EDITOR
            Vector3 position = root.position;
            Quaternion rotation = root.rotation;
            rootAup = new double3(position.x, position.y, position.z);
            rootRotation = NormalizeOrIdentity(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            return math.all(math.isfinite(rootAup));
#else
            rootAup = default;
            rootRotation = quaternion.identity;
            return false;
#endif
        }

        private bool TryRefreshRootPoseSnapshot(Transform root, bool allowPresentationFallback)
        {
            if (_dataVault != null)
            {
                if (!IsHandleValid(in _kinematicConfigHandle))
                    _dataVault.TryGetGenerationHandle(BufferID.SubmarineKinematicConfig, out _kinematicConfigHandle);

                if (IsHandleValid(in _kinematicConfigHandle) &&
                    _dataVault.TryReadOnlyHandle(in _kinematicConfigHandle, out NativeArray<SubmarineKinematicConfig>.ReadOnly kinematicConfig) &&
                    kinematicConfig.Length > 0)
                {
                    SubmarineKinematicConfig config = kinematicConfig[0];
                    Vector3 localPosition = root.position;
                    Quaternion localRotation = root.rotation;
                    double3 origin = config.LocalOriginAup;
                    double3 resolvedAup = origin + new double3(localPosition.x, localPosition.y, localPosition.z);
                    quaternion resolvedRotation = NormalizeOrIdentity(new quaternion(localRotation.x, localRotation.y, localRotation.z, localRotation.w));
                    if (math.all(math.isfinite(resolvedAup)) && math.all(math.isfinite(resolvedRotation.value)))
                    {
                        _cachedRootAup = resolvedAup;
                        _cachedRootRotation = resolvedRotation;
                        _hasRootPoseSnapshot = true;
                        return true;
                    }
                }
            }

#if UNITY_EDITOR
            if (!allowPresentationFallback)
                return false;

            Vector3 position = root.position;
            Quaternion rotation = root.rotation;
            double3 fallbackAup = new double3(position.x, position.y, position.z);
            if (!math.all(math.isfinite(fallbackAup)))
                return false;

            _cachedRootAup = fallbackAup;
            _cachedRootRotation = NormalizeOrIdentity(new quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            _hasRootPoseSnapshot = true;
            return true;
#else
            return false;
#endif
        }

        private static quaternion NormalizeOrIdentity(quaternion value)
        {
            float lengthSq = math.lengthsq(value.value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return quaternion.identity;

            return new quaternion(value.value * math.rsqrt(math.max(lengthSq, 0.0001f)));
        }

        private static float ResolveDepthMeters(double3 rootAup)
        {
            if (!math.all(math.isfinite(rootAup)))
                return 0f;

            const double seaLevelAupY = 0d;
            double depthMeters = seaLevelAupY - rootAup.y;
            if (!math.isfinite(depthMeters))
                return 0f;

            return (float)math.min(1000000d, math.max(0d, depthMeters));
        }

#if UNITY_EDITOR
        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : dataPath;
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Copies the current tuning DTO for the editor facade without exposing Vault handles to editor code.
        /// </summary>
        public bool TryLockCopyEditorTuning(out VehicleDamageTuningDTO tuning)
        {
            tuning = default;
            if (_damagePending || _buffersLocked || _dataVault == null || !IsHandleValid(in _tuningHandle))
                return false;

            if (!_dataVault.TryReadOnlyHandle(in _tuningHandle, out NativeArray<VehicleDamageTuningDTO>.ReadOnly tuningArray) ||
                tuningArray.Length <= 0)
            {
                return false;
            }

            tuning = tuningArray[0];
            return tuning.SourceHash != 0u;
        }

        /// <summary>
        /// Applies a single editor tuning scalar directly to the Vault-backed DTO after checking job/lock state.
        /// </summary>
        public bool TryWriteEditorTuning(string propertyName, float value)
        {
            IDataVault vault = _dataVault;
            if (_damagePending || _buffersLocked || vault == null || !IsHandleValid(in _tuningHandle))
                return false;

            bool lockAcquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.VehiclesPhysics, out NativeArray<VehicleDamageTuningDTO> tuningArray))
                    return false;
                lockAcquired = true;
                if (tuningArray.Length <= 0)
                    return false;

                ref VehicleDamageTuningDTO tuning = ref ElementRef(tuningArray, 0);
                if (tuning.SourceHash == 0u)
                {
                    tuning.BaseArmor = 1f;
                    tuning.DirectDamageScale = 1f;
                    tuning.ExplosiveRadiusMeters = 1f;
                    tuning.ExplosionFalloff = 1f;
                    tuning.IngressKgPerSecond = 0f;
                    tuning.FireChance01 = 0f;
                    tuning.SensorPenaltyWeight = 1f;
                    tuning.DragPenaltyWeight = 1f;
                    tuning.FloodMassLimitKg = 0f;
                    tuning.EngineMinimumScalar = 0.08f;
                    tuning.BallastMinimumScalar = 0.12f;
                    tuning.SensorMinimumScalar = 0.1f;
                }

                if (propertyName == "baseArmor")
                    tuning.BaseArmor = math.max(0.01f, value);
                else if (propertyName == "explosiveRadiusMeters")
                    tuning.ExplosiveRadiusMeters = math.max(0.05f, value);
                else if (propertyName == "explosionFalloff")
                    tuning.ExplosionFalloff = math.max(0.01f, value);
                else if (propertyName == "fireChance01")
                    tuning.FireChance01 = math.saturate(value);
                else
                    return false;

                tuning.SourceHash = VehicleDamageConstants.SourceHashEditor;
                tuning.Flags &= ~VehicleDamageConstants.TuningFlagRuntimeSerialized;
                tuning.Flags |= VehicleDamageConstants.TuningFlagEditorOverride;
                return true;
            }
            finally
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _tuningHandle, SystemID.VehiclesPhysics);
            }
        }

        /// <summary>
        /// Copies the published state and latest telemetry entry for editor readout without touching write buffers.
        /// </summary>
        public bool TryLockCopyEditorDamageSnapshot(
            out VehicleDamageStateDTO state,
            out VehicleDamageTelemetryEntry telemetry,
            out bool hasTelemetry)
        {
            state = default;
            telemetry = default;
            hasTelemetry = false;

            if (_damagePending || _buffersLocked || _dataVault == null || !IsHandleValid(in _stateReadHandle))
                return false;

            if (!_dataVault.TryReadOnlyHandle(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO>.ReadOnly stateArray) ||
                stateArray.Length <= 0)
                return false;

            state = stateArray[0];

            if (!IsHandleValid(in _telemetryHandle) || !IsHandleValid(in _telemetryCursorHandle))
                return true;
            if (!_dataVault.TryReadOnlyHandle(in _telemetryHandle, out NativeArray<VehicleDamageTelemetryEntry>.ReadOnly telemetryArray) ||
                !_dataVault.TryReadOnlyHandle(in _telemetryCursorHandle, out NativeArray<uint>.ReadOnly telemetryCursorArray) ||
                telemetryArray.Length <= 0 ||
                telemetryCursorArray.Length <= 0)
            {
                return true;
            }

            uint cursor = telemetryCursorArray[0];
            if (cursor == 0u)
                return true;

            int index = (int)((cursor - 1u) % (uint)math.min(telemetryArray.Length, VehicleDamageConstants.TelemetryCapacity));
            telemetry = telemetryArray[index];
            hasTelemetry = true;
            return true;
        }

        private void OnValidate()
        {
            gridWidth = math.clamp(gridWidth, 2, MaxGridWidth);
            gridHeight = math.clamp(gridHeight, 2, MaxGridHeight);
            gridDepth = math.clamp(gridDepth, 2, MaxGridDepth);
            baseArmor = math.max(0.01f, baseArmor);
            directDamageScale = math.max(0f, directDamageScale);
            explosiveRadiusMeters = math.max(0.05f, explosiveRadiusMeters);
            explosionFalloff = math.max(0.01f, explosionFalloff);
            floodMassLimitKg = math.max(0f, floodMassLimitKg);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDamageGizmos || _damagePending || _dataVault == null || !IsHandleValid(in _gridReadHandle))
                return;

            _dataVault.TryReadOnlyHandle(in _gridReadHandle, out NativeArray<VehicleGridCellDTO>.ReadOnly cells);
            if (!cells.IsCreated || cells.Length <= 0)
                return;

            int width = math.clamp(gridWidth, 2, MaxGridWidth);
            int height = math.clamp(gridHeight, 2, MaxGridHeight);
            int depth = math.clamp(gridDepth, 2, MaxGridDepth);
            Vector3 size = new Vector3(
                math.max(0.001f, math.abs(gridSizeLocal.x)) / width,
                math.max(0.001f, math.abs(gridSizeLocal.y)) / height,
                math.max(0.001f, math.abs(gridSizeLocal.z)) / depth);
            Vector3 min = gridCenterLocal - (gridSizeLocal * 0.5f);
            int limit = math.min(cells.Length, maxGizmoCells);
            int stride = math.max(1, cells.Length / math.max(1, limit));
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < cells.Length; i += stride)
            {
                VehicleGridCellDTO cell = cells[i];
                if (cell.Integrity01 >= 0.985f && (cell.StatusFlags & (VehicleDamageConstants.CellFlagFlooded | VehicleDamageConstants.CellFlagBurning)) == 0u)
                    continue;

                int layer = width * height;
                int z = i / layer;
                int rem = i - (z * layer);
                int y = rem / width;
                int x = rem - (y * width);
                float integrity = math.saturate(cell.Integrity01);
                Gizmos.color = (cell.StatusFlags & VehicleDamageConstants.CellFlagBurning) != 0u
                    ? new Color(1f, 0.25f, 0.05f, 0.55f)
                    : new Color(1f - integrity, integrity, 0.05f, 0.45f);
                Vector3 center = min + Vector3.Scale(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), size);
                Gizmos.DrawCube(center, size * 0.82f);
            }

            Gizmos.matrix = oldMatrix;
        }
#endif
    }
}
