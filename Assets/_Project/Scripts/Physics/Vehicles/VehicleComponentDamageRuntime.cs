using System;
using System.IO;
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
    public unsafe sealed class VehicleComponentDamageRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private const int MinimumQualityHazardSignals = 8;
        private const int MaxGridWidth = 32;
        private const int MaxGridHeight = 16;
        private const int MaxGridDepth = 24;
        private const uint HazardLaneHash = 0x565A4844u; // VZHD
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_VEHICLE_SURGEON.bin";

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
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<SubmarineKinematicConfig> _kinematicConfigHandle;
        private JobHandle _damageHandle;
        private bool _damagePending;
        private bool _buffersLocked;
        private bool _buffersReady;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLate;
        private bool _registeredSlow;
        private bool _registeredHotSwapListener;
        private bool _dumpWritten;
        private bool _csvLoaded;
        private int _cellCount;
        private uint _frameCounter;
        private uint _resolvedVehicleHash;
        private long _csvStampUtcTicks;
        private double3 _cachedRootAup;
        private quaternion _cachedRootRotation;
        private bool _hasRootPoseSnapshot;
        private string _projectRoot;
        private string _csvPath;
        private string _dumpPath;

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (!VehicleDamageLayoutValidator.ValidateVehicleGridCellLayout(out string layoutError))
                Debug.LogError(layoutError, this);
#endif

            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "vehicle_component_layouts.csv");
            _dumpPath = Path.Combine(_projectRoot, DumpRelativePath);
            _resolvedVehicleHash = ResolveVehicleHash();
            EnsureSignalLanes();
            EnsureDataVault();
            EnsureVaultBuffers(forceReinitialize: false);
            TryRefreshRootPoseSnapshot(transform, allowPresentationFallback: true);

            TryRegisterHotSwapListener();
            TryRegisterRuntimeLanes();
        }

        private void OnDisable()
        {
            if (_damagePending)
                DispatcherJobFence.TryComplete(ref _damageHandle, forceComplete: true);

            _damagePending = false;
            UnlockDamageBuffers();
            DumpBlackBoxIfFaulted();

            TryUnregisterHotSwapListener();
            TryUnregisterRuntimeLanes();
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

            if (_damagePending)
                DispatcherJobFence.TryComplete(ref _damageHandle, forceComplete: true);

            _damagePending = false;
            UnlockDamageBuffers();
            _dataVault = currentService as IDataVault;
            ClearVaultHandles();
            _buffersReady = false;
            if (isActiveAndEnabled && _dataVault != null)
                EnsureVaultBuffers(forceReinitialize: false);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (_damagePending || !_buffersReady)
                return;

            if (!LockDamageBuffers())
                return;

            if (!TryReadWritablePointers(
                    out VehicleGridCellDTO* gridWrite,
                    out VehicleGridCellDTO* gridRead,
                    out VehicleDamageSignalDTO* signals,
                    out VehicleDamageSignalDTO* mockSignals,
                    out VehicleDamageStateDTO* stateWrite,
                    out VehicleDamageStateDTO* stateRead,
                    out NativeArray<VehicleDamageTelemetryEntry> telemetry,
                    out NativeArray<uint> telemetryCursor,
                    out NativeArray<VehicleDamageTuningDTO> tuningArray,
                    out NativeArray<VehicleDamageSignalDTO> signalArray))
            {
                _buffersReady = false;
                UnlockDamageBuffers();
                return;
            }

            VehicleDamageTuningDTO tuning = UpdateTuningSnapshot(tuningArray);
            int realSignalCount = GatherCombatDamageSignals(signalArray, in tuning);
            int mockCount = ResolveMockSignalCount();
            int totalSignalCount = math.min(realSignalCount + mockCount, VehicleDamageConstants.MaxDamageSignals);

            Transform root = transform;
            if (!TryReadAuthoritativeRootPose(root, out double3 rootAup, out quaternion rootRotation))
            {
                UnlockDamageBuffers();
                return;
            }

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
                    GlobalQualityWeight = quality,
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
                    DestinationCapacity = VehicleDamageConstants.MaxDamageSignals
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
                    GlobalQualityWeight = quality,
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
                CellCount = _cellCount
            };

            _damageHandle = publishJob.Schedule(dependency);
            H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _damageHandle);
            _damagePending = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_damagePending)
                return;

            if (!DispatcherJobFence.TryComplete(ref _damageHandle, forceComplete: false))
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
            if (_dataVault != null)
                return true;

            _dataVault = GlobalRegistry.DataVault;

            return _dataVault != null;
        }

        private void TryRegisterRuntimeLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
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
            if (_registeredLate)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlow)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredFixed = false;
            _registeredPostFixed = false;
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
            _csvScratchHandle = default;
            _kinematicConfigHandle = default;
            _cellCount = 0;
            _csvLoaded = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveArray<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return _dataVault != null &&
                   handle.BufferID != 0u &&
                   _dataVault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetLocalPointer<T>(in VaultGenerationHandle<T> handle, out void* pointer) where T : struct
        {
            pointer = null;
            if (!TryResolveArray(in handle, out NativeArray<T> buffer))
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
            _csvScratchHandle = _dataVault.EnsureGenerationHandle<byte>(VehicleDamageConstants.CsvScratchBuffer, VehicleDamageConstants.CsvScratchBytes, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);

            if (!IsHandleValid(in _gridWriteHandle) || !IsHandleValid(in _gridReadHandle) || !IsHandleValid(in _signalHandle) ||
                !IsHandleValid(in _mockSignalHandle) || !IsHandleValid(in _stateWriteHandle) || !IsHandleValid(in _stateReadHandle) ||
                !IsHandleValid(in _tuningHandle) || !IsHandleValid(in _telemetryHandle) || !IsHandleValid(in _telemetryCursorHandle) ||
                !IsHandleValid(in _csvScratchHandle))
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

                if (!TryGetLocalPointer(in _gridWriteHandle, out void* writePtr) ||
                    !TryGetLocalPointer(in _gridReadHandle, out void* readPtr) ||
                    !TryResolveArray(in _tuningHandle, out NativeArray<VehicleDamageTuningDTO> tuningArray))
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
                JobHandle writeHandle = initWrite.Schedule(_cellCount, VehicleDamageConstants.JobBatchSize);
                JobHandle readHandle = initRead.Schedule(_cellCount, VehicleDamageConstants.JobBatchSize, writeHandle);
                DispatcherJobFence.TryComplete(ref readHandle, forceComplete: true);

                TryResolveArray(in _stateWriteHandle, out NativeArray<VehicleDamageStateDTO> stateWrite);
                TryResolveArray(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO> stateRead);
                if (stateWrite.IsCreated)
                    stateWrite[0] = BuildDefaultState();
                if (stateRead.IsCreated)
                    stateRead[0] = BuildDefaultState();

                TryResolveArray(in _telemetryCursorHandle, out NativeArray<uint> cursor);
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

            float quality = ResolveQualityWeight();
            int maxCount = math.clamp(mockSignalCount, 1, VehicleDamageConstants.MaxMockDamageSignals);
            return math.clamp((int)math.round(math.lerp(1f, maxCount, quality)), 1, math.min(maxCount, VehicleDamageConstants.MaxDamageSignals));
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
            telemetry = default;
            telemetryCursor = default;
            tuning = default;
            signalArray = default;

            if (!TryGetLocalPointer(in _gridWriteHandle, out void* gridWritePtr) ||
                !TryGetLocalPointer(in _gridReadHandle, out void* gridReadPtr) ||
                !TryResolveArray(in _signalHandle, out signalArray) ||
                !TryGetLocalPointer(in _mockSignalHandle, out void* mockSignalsPtr) ||
                !TryGetLocalPointer(in _stateWriteHandle, out void* stateWritePtr) ||
                !TryGetLocalPointer(in _stateReadHandle, out void* stateReadPtr) ||
                !TryResolveArray(in _telemetryHandle, out telemetry) ||
                !TryResolveArray(in _telemetryCursorHandle, out telemetryCursor) ||
                !TryResolveArray(in _tuningHandle, out tuning))
            {
                return false;
            }

            gridWrite = (VehicleGridCellDTO*)gridWritePtr;
            gridRead = (VehicleGridCellDTO*)gridReadPtr;
            signals = (VehicleDamageSignalDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(signalArray);
            mockSignals = (VehicleDamageSignalDTO*)mockSignalsPtr;
            stateWrite = (VehicleDamageStateDTO*)stateWritePtr;
            stateRead = (VehicleDamageStateDTO*)stateReadPtr;
            return gridWrite != null && gridRead != null && signals != null && mockSignals != null &&
                    stateWrite != null && stateRead != null && telemetry.IsCreated && telemetryCursor.IsCreated && tuning.IsCreated;
        }

        private bool LockDamageBuffers()
        {
            if (_buffersLocked || _dataVault == null)
                return _buffersLocked;

            if (!_dataVault.TryLockBuffer(VehicleDamageConstants.GridWriteBuffer, SystemID.VehiclesPhysics))
                return false;

            if (!_dataVault.TryLockBuffer(VehicleDamageConstants.GridReadBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.SignalBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.MockSignalBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.StateWriteBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics) ||
                !_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryCursorBuffer, SystemID.VehiclesPhysics))
            {
                _buffersLocked = true;
                UnlockDamageBuffers();
                return false;
            }

            _buffersLocked = true;
            return true;
        }

        private void UnlockDamageBuffers()
        {
            if (!_buffersLocked || _dataVault == null)
                return;

            _dataVault.TryUnlockBuffer(VehicleDamageConstants.GridWriteBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.GridReadBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.SignalBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.MockSignalBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.StateWriteBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics);
            _dataVault.TryUnlockBuffer(VehicleDamageConstants.TelemetryCursorBuffer, SystemID.VehiclesPhysics);
            _buffersLocked = false;
        }

#if UNITY_EDITOR
        private bool TryLoadCsvLayout()
        {
            if (!_buffersReady || _dataVault == null || !File.Exists(_csvPath))
                return false;

            bool scratchLocked = false;
            bool writeLocked = false;
            bool readLocked = false;
            bool tuningLocked = false;
            try
            {
                FileInfo info = new FileInfo(_csvPath);
                long stamp = info.LastWriteTimeUtc.Ticks;
                if (_csvLoaded && stamp == _csvStampUtcTicks)
                    return false;

                if (info.Length <= 0L || info.Length > VehicleDamageConstants.CsvScratchBytes)
                    return false;

                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.CsvScratchBuffer, SystemID.VehiclesPhysics))
                    return false;
                scratchLocked = true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.GridWriteBuffer, SystemID.VehiclesPhysics))
                    return false;
                writeLocked = true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.GridReadBuffer, SystemID.VehiclesPhysics))
                    return false;
                readLocked = true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics))
                    return false;
                tuningLocked = true;

                TryResolveArray(in _csvScratchHandle, out NativeArray<byte> scratch);
                TryResolveArray(in _gridWriteHandle, out NativeArray<VehicleGridCellDTO> gridWrite);
                TryResolveArray(in _gridReadHandle, out NativeArray<VehicleGridCellDTO> gridRead);
                TryResolveArray(in _tuningHandle, out NativeArray<VehicleDamageTuningDTO> tuning);
                if (!scratch.IsCreated || !gridWrite.IsCreated || !gridRead.IsCreated || !tuning.IsCreated)
                    return false;

                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                Span<byte> bytes = new Span<byte>(scratchPtr, (int)info.Length);
                using (FileStream stream = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    int read = stream.Read(bytes);
                    ReadOnlySpan<byte> csv = bytes.Slice(0, read);
                    int applied = VehicleComponentLayoutCsvParser.Apply(csv, gridWrite, gridWidth, gridHeight, gridDepth);
                    if (applied <= 0)
                        return false;
                }

                for (int i = 0; i < math.min(gridWrite.Length, gridRead.Length); i++)
                    gridRead[i] = gridWrite[i];

                VehicleDamageTuningDTO current = tuning[0];
                current.SourceHash = VehicleDamageConstants.SourceHashCsv;
                current.Flags &= ~VehicleDamageConstants.TuningFlagRuntimeSerialized;
                current.Flags |= VehicleDamageConstants.TuningFlagCsvLayout;
                tuning[0] = current;
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
                if (tuningLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics);
                if (readLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.GridReadBuffer, SystemID.VehiclesPhysics);
                if (writeLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.GridWriteBuffer, SystemID.VehiclesPhysics);
                if (scratchLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.CsvScratchBuffer, SystemID.VehiclesPhysics);
            }
        }
#endif

        private bool DumpBlackBoxIfFaulted()
        {
            if (_dumpWritten || _dataVault == null || !IsHandleValid(in _stateReadHandle) || !IsHandleValid(in _telemetryHandle))
                return false;

            bool stateLocked = false;
            bool telemetryLocked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics))
                    return false;
                stateLocked = true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics))
                    return false;
                telemetryLocked = true;

                if (!TryResolveArray(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO> stateRead) ||
                    stateRead.Length <= 0 ||
                    !TryResolveArray(in _telemetryHandle, out NativeArray<VehicleDamageTelemetryEntry> telemetry))
                {
                    return false;
                }

                VehicleDamageStateDTO state = ElementReadOnlyRef(stateRead, 0);
                if ((state.Flags & VehicleDamageConstants.StateFlagFatalNan) == 0u)
                    return false;

                if (!telemetry.IsCreated)
                    return false;

                bool written = TryWriteBlackBoxDump(_dumpPath, telemetry);
                _dumpWritten |= written;
                return written;
            }
            finally
            {
                if (telemetryLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics);
                if (stateLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics);
            }
        }

        private static bool TryWriteBlackBoxDump(string path, NativeArray<VehicleDamageTelemetryEntry> telemetry)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    int bytes = telemetry.Length * UnsafeUtility.SizeOf<VehicleDamageTelemetryEntry>();
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                    ReadOnlySpan<byte> payload = new ReadOnlySpan<byte>(ptr, bytes);
                    stream.Write(payload);
                }

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
                    _dataVault.TryLockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics))
                {
                    try
                    {
                        if (TryResolveArray(in _kinematicConfigHandle, out NativeArray<SubmarineKinematicConfig> kinematicConfig) &&
                            kinematicConfig.Length > 0)
                        {
                            SubmarineKinematicConfig config = ElementReadOnlyRef(kinematicConfig, 0);
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
                    finally
                    {
                        _dataVault.TryUnlockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics);
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
            if (math.all(math.isfinite(rootAup)) && rootAup.y > -1000000d && rootAup.y < 1000000d)
                return math.max(0f, (float)-rootAup.y);

            return 0f;
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : dataPath;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Copies the current tuning DTO for the editor facade without exposing Vault handles to editor code.
        /// </summary>
        public bool TryLockCopyEditorTuning(out VehicleDamageTuningDTO tuning)
        {
            tuning = default;
            if (_damagePending || _buffersLocked || _dataVault == null || !IsHandleValid(in _tuningHandle))
                return false;

            bool locked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics))
                    return false;
                locked = true;

                if (!TryResolveArray(in _tuningHandle, out NativeArray<VehicleDamageTuningDTO> tuningArray) ||
                    tuningArray.Length <= 0)
                    return false;

                tuning = ElementReadOnlyRef(tuningArray, 0);
                return tuning.SourceHash != 0u;
            }
            finally
            {
                if (locked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics);
            }
        }

        /// <summary>
        /// Applies a single editor tuning scalar directly to the Vault-backed DTO after checking job/lock state.
        /// </summary>
        public bool TryWriteEditorTuning(string propertyName, float value)
        {
            if (_damagePending || _buffersLocked || _dataVault == null || !IsHandleValid(in _tuningHandle))
                return false;

            bool locked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics))
                    return false;
                locked = true;

                if (!TryResolveArray(in _tuningHandle, out NativeArray<VehicleDamageTuningDTO> tuningArray) ||
                    tuningArray.Length <= 0)
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
                if (locked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics);
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

            bool stateLocked = false;
            bool telemetryLocked = false;
            bool cursorLocked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics))
                    return false;
                stateLocked = true;

                if (!TryResolveArray(in _stateReadHandle, out NativeArray<VehicleDamageStateDTO> stateArray) ||
                    stateArray.Length <= 0)
                    return false;

                state = ElementReadOnlyRef(stateArray, 0);

                if (!IsHandleValid(in _telemetryHandle) || !IsHandleValid(in _telemetryCursorHandle))
                    return true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics))
                    return true;
                telemetryLocked = true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryCursorBuffer, SystemID.VehiclesPhysics))
                    return true;
                cursorLocked = true;

                if (!TryResolveArray(in _telemetryHandle, out NativeArray<VehicleDamageTelemetryEntry> telemetryArray) ||
                    !TryResolveArray(in _telemetryCursorHandle, out NativeArray<uint> telemetryCursorArray) ||
                    telemetryArray.Length <= 0 ||
                    telemetryCursorArray.Length <= 0)
                {
                    return true;
                }

                uint cursor = ElementReadOnlyRef(telemetryCursorArray, 0);
                if (cursor == 0u)
                    return true;

                int index = (int)((cursor - 1u) % (uint)math.min(telemetryArray.Length, VehicleDamageConstants.TelemetryCapacity));
                telemetry = ElementReadOnlyRef(telemetryArray, index);
                hasTelemetry = true;
                return true;
            }
            finally
            {
                if (cursorLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.TelemetryCursorBuffer, SystemID.VehiclesPhysics);
                if (telemetryLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics);
                if (stateLocked)
                    _dataVault.TryUnlockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics);
            }
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

            TryResolveArray(in _gridReadHandle, out NativeArray<VehicleGridCellDTO> cells);
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
