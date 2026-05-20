using System;
using System.IO;
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
    public unsafe sealed class VehicleComponentDamageRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, ISlowTickable
    {
        private const int LowTierHazardSignals = 8;
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
        private VaultBufferHandle<VehicleGridCellDTO> _gridWriteHandle;
        private VaultBufferHandle<VehicleGridCellDTO> _gridReadHandle;
        private VaultBufferHandle<VehicleDamageSignalDTO> _signalHandle;
        private VaultBufferHandle<VehicleDamageSignalDTO> _mockSignalHandle;
        private VaultBufferHandle<VehicleDamageStateDTO> _stateWriteHandle;
        private VaultBufferHandle<VehicleDamageStateDTO> _stateReadHandle;
        private VaultBufferHandle<VehicleDamageTuningDTO> _tuningHandle;
        private VaultBufferHandle<VehicleDamageTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<uint> _telemetryCursorHandle;
        private VaultBufferHandle<byte> _csvScratchHandle;
        private VaultBufferHandle<SubmarineKinematicConfig> _kinematicConfigHandle;
        private JobHandle _damageHandle;
        private bool _damagePending;
        private bool _buffersLocked;
        private bool _buffersReady;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLate;
        private bool _registeredSlow;
        private bool _dumpWritten;
        private bool _csvLoaded;
        private int _cellCount;
        private uint _frameCounter;
        private long _csvStampUtcTicks;
        private double3 _cachedRootAup;
        private quaternion _cachedRootRotation;
        private bool _hasRootPoseSnapshot;
        private string _projectRoot;
        private string _csvPath;
        private string _dumpPath;

        private void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!VehicleDamageLayoutValidator.ValidateVehicleGridCellLayout(out string layoutError))
                Debug.LogError(layoutError, this);
#endif

            _projectRoot = ResolveProjectRoot();
            _csvPath = Path.Combine(_projectRoot, "vehicle_component_layouts.csv");
            _dumpPath = Path.Combine(_projectRoot, DumpRelativePath);
            EnsureSignalLanes();
            ResolveDataVault();
            EnsureVaultBuffers(forceReinitialize: false);
            TryRefreshRootPoseSnapshot(transform, allowPresentationFallback: true);

            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            if (_damagePending)
                DispatcherJobFence.TryComplete(ref _damageHandle, forceComplete: true);

            _damagePending = false;
            UnlockDamageBuffers();
            DumpBlackBoxIfFaulted();

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

        public void FixedTick(float fixedDeltaTime)
        {
            if (_damagePending || !_buffersReady)
                return;

            if (!LockDamageBuffers())
                return;

            if (!TryResolveWritablePointers(
                    out VehicleGridCellDTO* gridWrite,
                    out VehicleGridCellDTO* gridRead,
                    out VehicleDamageSignalDTO* signals,
                    out VehicleDamageSignalDTO* mockSignals,
                    out VehicleDamageStateDTO* stateWrite,
                    out VehicleDamageStateDTO* stateRead,
                    out NativeArray<VehicleDamageTelemetryEntry> telemetry,
                    out NativeArray<uint> telemetryCursor,
                    out NativeArray<VehicleDamageTuningDTO> tuningArray))
            {
                _buffersReady = false;
                UnlockDamageBuffers();
                return;
            }

            VehicleDamageTuningDTO tuning = ResolveTuning(tuningArray);
            int realSignalCount = GatherCombatDamageSignals(_signalHandle.Resolve(_dataVault), in tuning);
            int mockCount = ResolveMockSignalCount();
            int totalSignalCount = math.min(realSignalCount + mockCount, VehicleDamageConstants.MaxDamageSignals);

            Transform root = transform;
            if (!TryResolveAuthoritativeRootPose(root, out double3 rootAup, out quaternion rootRotation))
            {
                UnlockDamageBuffers();
                return;
            }

            quaternion inverseRotation = math.inverse(rootRotation);
            uint frame = ++_frameCounter;
            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            uint vehicleHash = acceptedTargetHash != 0u ? acceptedTargetHash : unchecked((uint)gameObject.GetInstanceID());
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

                PropagateDamageJob propagateJob = new PropagateDamageJob
                {
                    Cells = gridWrite,
                    Signals = signals,
                    SignalCount = totalSignalCount,
                    GridWidth = tuning.GridWidth,
                    GridHeight = tuning.GridHeight,
                    GridDepth = tuning.GridDepth,
                    GridSizeLocal = tuning.GridSizeLocal,
                    GlobalQualityWeight = quality,
                    ExplosionFalloff = tuning.ExplosionFalloff
                };

                dependency = propagateJob.Schedule(totalSignalCount, 1, dependency);
            }

            EvaluateVehicleSystemsJob evaluateJob = new EvaluateVehicleSystemsJob
            {
                Cells = gridWrite,
                Signals = signals,
                StateWrite = stateWrite,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
                HazardWriter = SignalBus<VehicleHazardSignal>.ParallelWriter,
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
            ResolveDataVault();
            if (!_damagePending && !_buffersLocked)
                EnsureVaultBuffers(forceReinitialize: false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_damagePending && !_buffersLocked)
                TryLoadCsvLayout();
#endif
        }

        private bool ResolveDataVault()
        {
            if (_dataVault != null)
                return true;

            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                _dataVault = latest;

            return _dataVault != null;
        }

        private bool EnsureVaultBuffers(bool forceReinitialize)
        {
            if (!ResolveDataVault())
                return false;

            int width = math.clamp(gridWidth, 2, MaxGridWidth);
            int height = math.clamp(gridHeight, 2, MaxGridHeight);
            int depth = math.clamp(gridDepth, 2, MaxGridDepth);
            int cellCount = width * height * depth;
            bool reinitialize = forceReinitialize || cellCount != _cellCount;

            _gridWriteHandle = _dataVault.GetBufferHandle<VehicleGridCellDTO>(VehicleDamageConstants.GridWriteBuffer, cellCount, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _gridReadHandle = _dataVault.GetBufferHandle<VehicleGridCellDTO>(VehicleDamageConstants.GridReadBuffer, cellCount, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _signalHandle = _dataVault.GetBufferHandle<VehicleDamageSignalDTO>(VehicleDamageConstants.SignalBuffer, VehicleDamageConstants.MaxDamageSignals, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _mockSignalHandle = _dataVault.GetBufferHandle<VehicleDamageSignalDTO>(VehicleDamageConstants.MockSignalBuffer, VehicleDamageConstants.MaxMockDamageSignals, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);
            _stateWriteHandle = _dataVault.GetBufferHandle<VehicleDamageStateDTO>(VehicleDamageConstants.StateWriteBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _stateReadHandle = _dataVault.GetBufferHandle<VehicleDamageStateDTO>(VehicleDamageConstants.StateReadBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _tuningHandle = _dataVault.GetBufferHandle<VehicleDamageTuningDTO>(VehicleDamageConstants.TuningBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _dataVault.GetBufferHandle<VehicleDamageTelemetryEntry>(VehicleDamageConstants.TelemetryRingBuffer, VehicleDamageConstants.TelemetryCapacity, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = _dataVault.GetBufferHandle<uint>(VehicleDamageConstants.TelemetryCursorBuffer, 1, SystemID.VehiclesPhysics, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = _dataVault.GetBufferHandle<byte>(VehicleDamageConstants.CsvScratchBuffer, VehicleDamageConstants.CsvScratchBytes, SystemID.VehiclesPhysics, NativeArrayOptions.UninitializedMemory);

            if (!_gridWriteHandle.IsCreated || !_gridReadHandle.IsCreated || !_signalHandle.IsCreated ||
                !_mockSignalHandle.IsCreated || !_stateWriteHandle.IsCreated || !_stateReadHandle.IsCreated ||
                !_tuningHandle.IsCreated || !_telemetryHandle.IsCreated || !_telemetryCursorHandle.IsCreated ||
                !_csvScratchHandle.IsCreated)
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

                VehicleGridCellDTO* write = (VehicleGridCellDTO*)_gridWriteHandle.ResolvePointer(_dataVault);
                VehicleGridCellDTO* read = (VehicleGridCellDTO*)_gridReadHandle.ResolvePointer(_dataVault);
                VehicleDamageTuningDTO tuning = ResolveTuning(_tuningHandle.Resolve(_dataVault));

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

                NativeArray<VehicleDamageStateDTO> stateWrite = _stateWriteHandle.Resolve(_dataVault);
                NativeArray<VehicleDamageStateDTO> stateRead = _stateReadHandle.Resolve(_dataVault);
                if (stateWrite.IsCreated)
                    stateWrite[0] = BuildDefaultState();
                if (stateRead.IsCreated)
                    stateRead[0] = BuildDefaultState();

                NativeArray<uint> cursor = _telemetryCursorHandle.Resolve(_dataVault);
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

        private VehicleDamageTuningDTO ResolveTuning(NativeArray<VehicleDamageTuningDTO> tuningArray)
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
            current.FireChance01 = math.saturate(math.isfinite(current.FireChance01) ? current.FireChance01 : serialized.FireChance01);
            current.SensorPenaltyWeight = PositiveOrFallback(current.SensorPenaltyWeight, serialized.SensorPenaltyWeight, 0f);
            current.DragPenaltyWeight = PositiveOrFallback(current.DragPenaltyWeight, serialized.DragPenaltyWeight, 0f);
            current.FloodMassLimitKg = PositiveOrFallback(current.FloodMassLimitKg, serialized.FloodMassLimitKg, 0f);
            current.EngineMinimumScalar = math.saturate(math.isfinite(current.EngineMinimumScalar) ? current.EngineMinimumScalar : serialized.EngineMinimumScalar);
            current.BallastMinimumScalar = math.saturate(math.isfinite(current.BallastMinimumScalar) ? current.BallastMinimumScalar : serialized.BallastMinimumScalar);
            current.SensorMinimumScalar = math.saturate(math.isfinite(current.SensorMinimumScalar) ? current.SensorMinimumScalar : serialized.SensorMinimumScalar);
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

            float quality = math.saturate(math.isfinite(HomeostasisBrain.GlobalQualityWeight) ? HomeostasisBrain.GlobalQualityWeight : 1f);
            int maxCount = math.clamp(mockSignalCount, 1, VehicleDamageConstants.MaxMockDamageSignals);
            return math.clamp((int)math.round(math.lerp(1f, maxCount, quality)), 1, math.min(maxCount, VehicleDamageConstants.MaxDamageSignals));
        }

        private bool TryResolveWritablePointers(
            out VehicleGridCellDTO* gridWrite,
            out VehicleGridCellDTO* gridRead,
            out VehicleDamageSignalDTO* signals,
            out VehicleDamageSignalDTO* mockSignals,
            out VehicleDamageStateDTO* stateWrite,
            out VehicleDamageStateDTO* stateRead,
            out NativeArray<VehicleDamageTelemetryEntry> telemetry,
            out NativeArray<uint> telemetryCursor,
            out NativeArray<VehicleDamageTuningDTO> tuning)
        {
            gridWrite = (VehicleGridCellDTO*)_gridWriteHandle.ResolvePointer(_dataVault);
            gridRead = (VehicleGridCellDTO*)_gridReadHandle.ResolvePointer(_dataVault);
            signals = (VehicleDamageSignalDTO*)_signalHandle.ResolvePointer(_dataVault);
            mockSignals = (VehicleDamageSignalDTO*)_mockSignalHandle.ResolvePointer(_dataVault);
            stateWrite = (VehicleDamageStateDTO*)_stateWriteHandle.ResolvePointer(_dataVault);
            stateRead = (VehicleDamageStateDTO*)_stateReadHandle.ResolvePointer(_dataVault);
            telemetry = _telemetryHandle.Resolve(_dataVault);
            telemetryCursor = _telemetryCursorHandle.Resolve(_dataVault);
            tuning = _tuningHandle.Resolve(_dataVault);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

                NativeArray<byte> scratch = _csvScratchHandle.Resolve(_dataVault);
                NativeArray<VehicleGridCellDTO> gridWrite = _gridWriteHandle.Resolve(_dataVault);
                NativeArray<VehicleGridCellDTO> gridRead = _gridReadHandle.Resolve(_dataVault);
                NativeArray<VehicleDamageTuningDTO> tuning = _tuningHandle.Resolve(_dataVault);
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
            if (_dumpWritten || _dataVault == null || !_stateReadHandle.IsCreated || !_telemetryHandle.IsCreated)
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

                if (!_dataVault.ResolveBuffer(ref _stateReadHandle) || !_dataVault.ResolveBuffer(ref _telemetryHandle))
                    return false;

                VehicleDamageStateDTO state = _stateReadHandle.GetElementAsReadOnlyRef(_dataVault, 0);
                if ((state.Flags & VehicleDamageConstants.StateFlagFatalNan) == 0u)
                    return false;

                NativeArray<VehicleDamageTelemetryEntry> telemetry = _telemetryHandle.Resolve(_dataVault);
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
            SignalBus<VehicleHazardSignal>.Configure(VehicleDamageConstants.MaxDamageSignals, VehicleDamageConstants.MaxDamageSignals, LowTierHazardSignals, HazardLaneHash);
            SignalBus<VehicleHazardSignal>.EnsureInitialized();
        }

        private bool TryResolveAuthoritativeRootPose(Transform root, out double3 rootAup, out quaternion rootRotation)
        {
            if (_hasRootPoseSnapshot &&
                math.all(math.isfinite(_cachedRootAup)) &&
                math.all(math.isfinite(_cachedRootRotation.value)))
            {
                rootAup = _cachedRootAup;
                rootRotation = NormalizeOrIdentity(_cachedRootRotation);
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
                if (!_kinematicConfigHandle.IsCreated)
                    _dataVault.TryGetBufferHandle(BufferID.SubmarineKinematicConfig, out _kinematicConfigHandle);

                if (_kinematicConfigHandle.IsCreated &&
                    _dataVault.TryLockBuffer(BufferID.SubmarineKinematicConfig, SystemID.VehiclesPhysics))
                {
                    try
                    {
                        if (_dataVault.ResolveBuffer(ref _kinematicConfigHandle) &&
                            _kinematicConfigHandle.Length > 0)
                        {
                            SubmarineKinematicConfig config = _kinematicConfigHandle.GetElementAsReadOnlyRef(_dataVault, 0);
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

            return new quaternion(value.value * math.rsqrt(lengthSq));
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
        public bool TryReadEditorTuning(out VehicleDamageTuningDTO tuning)
        {
            tuning = default;
            if (_damagePending || _buffersLocked || _dataVault == null || !_tuningHandle.IsCreated)
                return false;

            bool locked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics))
                    return false;
                locked = true;

                if (!_dataVault.ResolveBuffer(ref _tuningHandle) || !_tuningHandle.IsCreated || _tuningHandle.Length <= 0)
                    return false;

                tuning = _tuningHandle.GetElementAsReadOnlyRef(_dataVault, 0);
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
            if (_damagePending || _buffersLocked || _dataVault == null || !_tuningHandle.IsCreated)
                return false;

            bool locked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TuningBuffer, SystemID.VehiclesPhysics))
                    return false;
                locked = true;

                if (!_dataVault.ResolveBuffer(ref _tuningHandle) || !_tuningHandle.IsCreated || _tuningHandle.Length <= 0)
                    return false;

                ref VehicleDamageTuningDTO tuning = ref _tuningHandle.GetElementAsRef(_dataVault, 0);
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
        public bool TryReadEditorDamageSnapshot(
            out VehicleDamageStateDTO state,
            out VehicleDamageTelemetryEntry telemetry,
            out bool hasTelemetry)
        {
            state = default;
            telemetry = default;
            hasTelemetry = false;

            if (_damagePending || _buffersLocked || _dataVault == null || !_stateReadHandle.IsCreated)
                return false;

            bool stateLocked = false;
            bool telemetryLocked = false;
            bool cursorLocked = false;
            try
            {
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.StateReadBuffer, SystemID.VehiclesPhysics))
                    return false;
                stateLocked = true;

                if (!_dataVault.ResolveBuffer(ref _stateReadHandle) || !_stateReadHandle.IsCreated || _stateReadHandle.Length <= 0)
                    return false;

                state = _stateReadHandle.GetElementAsReadOnlyRef(_dataVault, 0);

                if (!_telemetryHandle.IsCreated || !_telemetryCursorHandle.IsCreated)
                    return true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryRingBuffer, SystemID.VehiclesPhysics))
                    return true;
                telemetryLocked = true;
                if (!_dataVault.TryLockBuffer(VehicleDamageConstants.TelemetryCursorBuffer, SystemID.VehiclesPhysics))
                    return true;
                cursorLocked = true;

                if (!_dataVault.ResolveBuffer(ref _telemetryHandle) ||
                    !_dataVault.ResolveBuffer(ref _telemetryCursorHandle) ||
                    !_telemetryHandle.IsCreated ||
                    !_telemetryCursorHandle.IsCreated ||
                    _telemetryHandle.Length <= 0 ||
                    _telemetryCursorHandle.Length <= 0)
                {
                    return true;
                }

                uint cursor = _telemetryCursorHandle.GetElementAsReadOnlyRef(_dataVault, 0);
                if (cursor == 0u)
                    return true;

                int index = (int)((cursor - 1u) % (uint)math.min(_telemetryHandle.Length, VehicleDamageConstants.TelemetryCapacity));
                telemetry = _telemetryHandle.GetElementAsReadOnlyRef(_dataVault, index);
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
            if (!drawDamageGizmos || _damagePending || _dataVault == null || !_gridReadHandle.IsCreated)
                return;

            if (!_dataVault.ResolveBuffer(ref _gridReadHandle) || !_gridReadHandle.IsCreated)
                return;

            NativeArray<VehicleGridCellDTO> cells = _gridReadHandle.Resolve(_dataVault);
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
