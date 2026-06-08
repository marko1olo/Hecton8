using System;
using System.Diagnostics;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3420)]
    public sealed unsafe class HabitatFluidIncursionDirector : MonoBehaviour, IFixedTickable, IPostFixedTickable, IRenderable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_HabitatFluidIncursionDirector;

        private const SystemID OwnerSystem = SystemID.Fluid;
        private const uint HabitatFaultEventHash = 0x48464654u; // HFFT
        private const uint HabitatFaultDumpHash = 0x48464450u; // HFDP
        private const uint FloodMuffleLaneHash = 0x464C4D46u; // FLMF
        private const int FloodMuffleSignalCapacity = 32;
        private const int FloodMuffleMinimumQualityFrameSignals = 8;
        private const float DefaultExternalWaterlineRuntimeY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;
        private const ulong FluidSimulationMutationGuardMask = 0x00000000F00C4FFFUL;
        private const uint FluidPinFront = 1u << 0;
        private const uint FluidPinBack = 1u << 1;
        private const uint FluidPinIntegrity = 1u << 2;
        private const uint FluidPinEdgeOffsets = 1u << 3;
        private const uint FluidPinEdgeDestinations = 1u << 4;
        private const uint FluidPinEdgeFlags = 1u << 5;
        private const uint FluidPinEdgeConductivity = 1u << 6;
        private const uint FluidPinCentroids = 1u << 7;
        private const uint FluidPinWaterline = 1u << 8;
        private const uint FluidPinMassState = 1u << 9;
        private const uint FluidPinTuning = 1u << 10;
        private const uint FluidPinTelemetry = 1u << 11;
        private const uint FluidPinCompartmentTelemetry = 1u << 12;
        private const uint FluidPinTelemetryCursor = 1u << 13;
        private const uint FluidPinBfsQueue = 1u << 14;
        private const uint FluidPinBfsVisited = 1u << 15;
        private const uint FluidPinDeltaVolumes = 1u << 16;
        private const uint FluidPinTransferRemainders = 1u << 17;
        private const uint FluidPinSummary = 1u << 18;
        private static readonly int s_WaterlineBufferId = Shader.PropertyToID("_H8HabitatFluidWaterlines");
        private static readonly int s_WaterlineCountId = Shader.PropertyToID("_H8HabitatFluidWaterlineCount");
        private static readonly int s_GlobalFloodScalarId = Shader.PropertyToID("_H8HabitatFloodScalar");
        private static bool s_FloodMuffleLaneInitialized;

        [SerializeField, Range(1, HabitatFluidIncursionConstants.MaxCompartments)] private int compartmentCount = 16;
        [SerializeField, Min(1f)] private float defaultCompartmentVolumeM3 = HabitatFluidIncursionConstants.DefaultCompartmentVolumeM3;
        [SerializeField] private float defaultFloorHeightLocal = HabitatFluidIncursionConstants.DefaultFloorHeightLocal;
        [SerializeField, Min(0f)] private float externalWaterlineRuntimeY = DefaultExternalWaterlineRuntimeY;
        [SerializeField, Min(1f)] private float baseMassKg = 18000f;
        [SerializeField, Min(0.0001f)] private float mockBreachAreaM2 = 0.08f;
        [SerializeField, Min(0f)] private int mockBreachIndex = 0;
        [SerializeField] private bool seedMockBreachOnEnable = true;
        [SerializeField] private bool uploadShaderWaterlines = true;
        [SerializeField] private bool drawHeatmapGizmos = true;

        private IDataVault _vault;
        private VaultGenerationHandle<FluidCompartmentDTO> _frontHandle;
        private VaultGenerationHandle<FluidCompartmentDTO> _backHandle;
        private VaultGenerationHandle<IntegrityStateDTO> _integrityHandle;
        private VaultGenerationHandle<int> _edgeOffsetsHandle;
        private VaultGenerationHandle<int> _edgeDestinationsHandle;
        private VaultGenerationHandle<byte> _edgeFlagsHandle;
        private VaultGenerationHandle<float> _edgeConductivityHandle;
        private VaultGenerationHandle<float3> _centroidsHandle;
        private VaultGenerationHandle<FluidWaterlineShaderDTO> _waterlineHandle;
        private VaultGenerationHandle<FluidMassStateDTO> _massStateHandle;
        private VaultGenerationHandle<FluidIncursionTuningDTO> _tuningHandle;
        private VaultGenerationHandle<FluidIncursionTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<FluidCompartmentTelemetryDTO> _compartmentTelemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<int> _bfsQueueHandle;
        private VaultGenerationHandle<byte> _bfsVisitedHandle;
        private VaultGenerationHandle<float> _deltaVolumesHandle;
        private VaultGenerationHandle<float> _transferRemaindersHandle;
        private VaultGenerationHandle<FluidIncursionFrameSummaryDTO> _summaryHandle;

        private Transform _cachedTransform;
        private GraphicsBuffer _waterlineBufferA;
        private GraphicsBuffer _waterlineBufferB;
        private JobHandle _simulationHandle;
        private uint _sourceBodyId;
        private int _edgeCount;
        private int _waterlineBufferCapacity;
        private int _waterlineWriteBufferIndex;
        private ulong _activeMutationGuardMask;
        private uint _simulationBufferPinMask;
        private IDataVault _activeMutationGuardVault;
        private IDataVault _simulationBufferPinVault;
        private int _droppedSignalCount;
        private int _frame;
        private long _simulationScheduleTimestamp;
        private float _pendingFloodScalar;
        private float _massPublishAccumulator;
        private float _simulationAccumulator;
        private bool _hasScheduled;
        private bool _frontIsA = true;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredRenderable;
        private bool _registeredHotSwapListener;
        private bool _buffersReady;
        private bool _dumpWritten;
        private bool _coreBlackboxWarmed;
        private bool _waterlineUploadDirty;
        private bool _floodScalarDirty;

        public int DroppedSignalCount => _droppedSignalCount;

        private void OnEnable()
        {
            _cachedTransform = transform;
            _droppedSignalCount = 0;
            _sourceBodyId = Hecton8.Core.RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(GetEntityId()));
            TryRegisterHotSwapListener();
            CacheDataVaultCold(GlobalRegistry.DataVault);
            _buffersReady = EnsureBuffersInitialized();
            _waterlineUploadDirty = true;
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            SignalBus<SubmarineFloodStateSignal>.EnsureInitialized();
            EnsureFloodMuffleSignalLane();
            PhysicsEventBus.EnsureReady();
            WarmCoreBlackboxRoute();

            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void OnDisable()
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            ReleaseFluidSimulationBufferPins();
            ReleaseFluidSimulationMutationGuard();

            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredPostFixed)
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            if (_registeredRenderable)
                GlobalRegistry.Renderables.TryUnregister(this);

            TryUnregisterHotSwapListener();
            ReleaseFluidVaultHandles(_vault);
            ReleaseGraphicsBuffer(ref _waterlineBufferA);
            ReleaseGraphicsBuffer(ref _waterlineBufferB);
            _registeredFixed = false;
            _registeredPostFixed = false;
            _registeredRenderable = false;
            _buffersReady = false;
            _coreBlackboxWarmed = false;
            _cachedTransform = null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteScheduledSimulationForAuthoritativeWrite();
                ReleaseFluidSimulationBufferPins();
                ReleaseFluidSimulationMutationGuard();
                CacheDataVaultCold(currentService as IDataVault);
                if (isActiveAndEnabled)
                {
                    _buffersReady = EnsureBuffersInitialized();
                    WarmCoreBlackboxRoute();
                }
                _waterlineUploadDirty = true;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                if (_registeredFixed)
                {
                    GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                    _registeredFixed = false;
                }

                if (_registeredPostFixed)
                {
                    GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                    _registeredPostFixed = false;
                }

                if (currentService != null && isActiveAndEnabled)
                {
                    bool fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
                    bool postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
                    if (!fixedRegistered || !postFixedRegistered)
                    {
                        if (fixedRegistered)
                            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
                        if (postFixedRegistered)
                            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
                        return;
                    }

                    _registeredFixed = true;
                    _registeredPostFixed = true;
                }
            }
        }

        private void OnValidate()
        {
            compartmentCount = math.clamp(compartmentCount, 1, HabitatFluidIncursionConstants.MaxCompartments);
            defaultCompartmentVolumeM3 = math.max(1f, defaultCompartmentVolumeM3);
            mockBreachIndex = math.max(0, mockBreachIndex);
            mockBreachAreaM2 = math.max(0.0001f, mockBreachAreaM2);
            baseMassKg = math.max(1f, baseMassKg);
        }

        /// <summary>Schedules the deterministic scalar flood solver when the authority cadence reaches its window.</summary>
        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f || _hasScheduled)
                return;

            _simulationAccumulator = math.min(0.25f, _simulationAccumulator + fixedDeltaTime);
            float solverWindowSeconds = ResolveAuthoritySolverWindowSeconds();
            if (_simulationAccumulator + 0.00001f < solverWindowSeconds)
                return;
            float solverDeltaTime = _simulationAccumulator;

            if (!_buffersReady && !EnsureBuffersInitialized(false))
                return;

            if (!TryLockFluidSimulationBuffers())
                return;

            bool scheduled = false;
            try
            {
                NativeArray<FluidCompartmentDTO> read = ResolveActiveCompartments();
                NativeArray<FluidCompartmentDTO> write = ResolveInactiveCompartments();
                int capacity = ResolveCompartmentCapacity();
                NativeArray<IntegrityStateDTO> integrity = ResolveFluidVaultBuffer(ref _integrityHandle, BufferID.ShinobuFluidIntegrityState, capacity);
                NativeArray<int> edgeOffsets = ResolveFluidVaultBuffer(ref _edgeOffsetsHandle, BufferID.ShinobuFluidEdgeOffsets, capacity + 1);
                NativeArray<int> edgeDestinations = ResolveFluidVaultBuffer(ref _edgeDestinationsHandle, BufferID.ShinobuFluidEdgeDestinations, HabitatFluidIncursionConstants.MaxEdges);
                NativeArray<byte> edgeFlags = ResolveFluidVaultBuffer(ref _edgeFlagsHandle, BufferID.ShinobuFluidEdgeFlags, HabitatFluidIncursionConstants.MaxEdges);
                NativeArray<float> edgeConductivity = ResolveFluidVaultBuffer(ref _edgeConductivityHandle, BufferID.ShinobuFluidEdgeConductivity, HabitatFluidIncursionConstants.MaxEdges);
                NativeArray<float3> centroids = ResolveFluidVaultBuffer(ref _centroidsHandle, BufferID.ShinobuFluidCompartmentCentroids, capacity);
                NativeArray<FluidWaterlineShaderDTO> waterlines = ResolveFluidVaultBuffer(ref _waterlineHandle, BufferID.ShinobuFluidWaterlineShader, capacity);
                NativeArray<FluidMassStateDTO> massState = ResolveFluidVaultBuffer(ref _massStateHandle, BufferID.ShinobuFluidMassState, 1);
                NativeArray<FluidIncursionTuningDTO> tuningArray = ResolveFluidVaultBuffer(ref _tuningHandle, BufferID.ShinobuFluidTuning, 1);
                NativeArray<FluidIncursionTelemetryEntry> telemetry = ResolveFluidVaultBuffer(ref _telemetryHandle, BufferID.ShinobuFluidTelemetryRing, HabitatFluidIncursionConstants.TelemetryFrameCount);
                NativeArray<FluidCompartmentTelemetryDTO> compartmentTelemetry = ResolveFluidVaultBuffer(ref _compartmentTelemetryHandle, BufferID.ShinobuFluidCompartmentTelemetry, capacity);
                NativeArray<int> telemetryCursor = ResolveFluidVaultBuffer(ref _telemetryCursorHandle, BufferID.ShinobuFluidTelemetryCursor, 1);
                NativeArray<int> bfsQueue = ResolveFluidVaultBuffer(ref _bfsQueueHandle, BufferID.ShinobuFluidBfsQueue, capacity);
                NativeArray<byte> bfsVisited = ResolveFluidVaultBuffer(ref _bfsVisitedHandle, BufferID.ShinobuFluidBfsVisited, capacity);
                NativeArray<float> deltaVolumes = ResolveFluidVaultBuffer(ref _deltaVolumesHandle, BufferID.ShinobuFluidDeltaVolumes, capacity);
                NativeArray<float> transferRemainders = ResolveFluidVaultBuffer(ref _transferRemaindersHandle, BufferID.ShinobuFluidTransferRemainders, HabitatFluidIncursionConstants.MaxEdges);
                NativeArray<FluidIncursionFrameSummaryDTO> summary = ResolveFluidVaultBuffer(ref _summaryHandle, BufferID.ShinobuFluidFrameSummary, 1);

                if (!read.IsCreated || !write.IsCreated || !integrity.IsCreated || !edgeOffsets.IsCreated ||
                    !edgeDestinations.IsCreated || !edgeFlags.IsCreated || !edgeConductivity.IsCreated || !centroids.IsCreated ||
                    !waterlines.IsCreated || !massState.IsCreated || !tuningArray.IsCreated ||
                    !telemetry.IsCreated || !compartmentTelemetry.IsCreated || !telemetryCursor.IsCreated || !bfsQueue.IsCreated ||
                    !bfsVisited.IsCreated || !deltaVolumes.IsCreated || !transferRemainders.IsCreated || !summary.IsCreated)
                {
                    return;
                }

                _simulationAccumulator = 0f;

                int safeCompartmentCount = math.min(compartmentCount, read.Length);
                safeCompartmentCount = math.min(safeCompartmentCount, write.Length);
                safeCompartmentCount = math.min(safeCompartmentCount, integrity.Length);
                FluidIncursionTuningDTO tuning = RefreshTuning(tuningArray, solverDeltaTime, safeCompartmentCount);
                ApplyIncomingFluidIncursionSignals(read, write, integrity, safeCompartmentCount);
                summary[0] = default;

                FluidCompartmentDTO* readPtr = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(read);
                FluidCompartmentDTO* writePtr = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(write);
                IntegrityStateDTO* integrityPtr = (IntegrityStateDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(integrity);
                _simulationScheduleTimestamp = Stopwatch.GetTimestamp();

                FluidIngressJob ingressJob = new FluidIngressJob
                {
                    ReadCompartments = readPtr,
                    WriteCompartments = writePtr,
                    Integrity = integrityPtr,
                    IncursionWriter = SignalBus<FluidIncursionSignal>.ParallelWriter,
                    IncursionWriterBudget = SignalBus<FluidIncursionSignal>.ParallelWriterBudget,
                    CompartmentCount = safeCompartmentCount,
                    DeltaTime = solverDeltaTime,
                    DischargeCoefficient = tuning.DischargeCoefficient,
                    MaxIngressPerSecondNormalized = tuning.MaxIngressPerSecondNormalized,
                    ExternalWaterlineAup = ResolveExternalWaterlineAup()
                };
                JobHandle ingressHandle = ingressJob.Schedule(safeCompartmentCount, 32);

                FluidBfsPressureEqualizationJob bfsJob = new FluidBfsPressureEqualizationJob
                {
                    Compartments = writePtr,
                    Integrity = integrityPtr,
                    EdgeOffsets = edgeOffsets,
                    EdgeDestinations = edgeDestinations,
                    EdgeFlags = edgeFlags,
                    EdgeConductivity = edgeConductivity,
                    BfsQueue = bfsQueue,
                    BfsVisited = bfsVisited,
                    DeltaVolumes = deltaVolumes,
                    TransferRemainders = transferRemainders,
                    CompartmentCount = safeCompartmentCount,
                    EdgeCount = math.min(_edgeCount, edgeDestinations.Length),
                    SolverIterations = tuning.SolverIterations,
                    MaxVisitedNodes = ResolveAuthorityBfsNodeBudget(),
                    DeltaTime = solverDeltaTime,
                    TransferRate01PerSecond = tuning.TransferRate01PerSecond,
                    MaxTransferPerNodeM3 = tuning.MaxTransferPerNodeM3,
                    Summary = summary
                };
                JobHandle bfsHandle = bfsJob.Schedule(ingressHandle);

                FluidWaterlineMassSummaryJob massJob = new FluidWaterlineMassSummaryJob
                {
                    Compartments = writePtr,
                    LocalCentroids = centroids,
                    Waterlines = waterlines,
                    CompartmentTelemetry = compartmentTelemetry,
                    MassState = massState,
                    Summary = summary,
                    CompartmentCount = safeCompartmentCount,
                    EdgeCount = _edgeCount,
                    Frame = unchecked((uint)_frame),
                    SourceBodyId = _sourceBodyId,
                    BaseMassKg = tuning.BaseMassKg,
                    WaterDensityKgPerM3 = tuning.WaterDensityKgPerM3,
                    GlobalQualityWeight = tuning.GlobalQualityWeight,
                    VisualWobbleScalar = tuning.VisualWobbleScalar,
                    MathLod = (byte)tuning.SolverIterations
                };
                JobHandle massHandle = massJob.Schedule(bfsHandle);

                FluidTelemetryRecorderJob telemetryJob = new FluidTelemetryRecorderJob
                {
                    Summary = summary,
                    TelemetryRing = telemetry,
                    TelemetryCursor = telemetryCursor,
                    TelemetryCapacity = HabitatFluidIncursionConstants.TelemetryFrameCount,
                    CompartmentCount = safeCompartmentCount,
                    EdgeCount = _edgeCount
                };
                _simulationHandle = telemetryJob.Schedule(massHandle);
                H8Memory.RegisterActiveJob(OwnerSystem, _simulationHandle);
                _hasScheduled = true;
                _frame++;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseFluidSimulationBufferPins();
            }
        }

        /// <summary>Completes the scheduled flood job chain, swaps buffers, and publishes deferred scalar bridges.</summary>
        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_hasScheduled)
                return;

            if (!TryFinalizeScheduledSimulation())
                return;

            try
            {
                uint solverWallMicroseconds = ResolveElapsedMicroseconds(_simulationScheduleTimestamp);
                _frontIsA = !_frontIsA;
                _waterlineUploadDirty = true;

                NativeArray<FluidIncursionFrameSummaryDTO> summaryArray = ResolveFluidVaultBuffer(ref _summaryHandle, BufferID.ShinobuFluidFrameSummary, 1);
                if (!summaryArray.IsCreated || summaryArray.Length <= 0)
                    return;

                StampSolverWallTime(solverWallMicroseconds);
                FluidIncursionFrameSummaryDTO summary = summaryArray[0];
                if (summary.InvalidCount > 0 || (summary.Flags & 1u) != 0u)
                    DumpBlackBoxOnce();

                _massPublishAccumulator += fixedDeltaTime;
                NativeArray<FluidIncursionTuningDTO> tuningArray = ResolveFluidVaultBuffer(ref _tuningHandle, BufferID.ShinobuFluidTuning, 1);
                float publishInterval = tuningArray.IsCreated && tuningArray.Length > 0
                    ? math.max(0.02f, tuningArray[0].MassPublishIntervalSeconds)
                    : HabitatFluidIncursionConstants.DefaultMassPublishIntervalSeconds;
                if (_massPublishAccumulator >= publishInterval)
                {
                    _massPublishAccumulator = 0f;
                    if (!PublishMassAndAcousticSignals(in summary))
                    {
                        summary.Flags |= FluidCompartmentFlags.SignalOverflow;
                        summaryArray[0] = summary;
                    }
                }

                _pendingFloodScalar = summary.AcousticFloodIntensity01;
                _floodScalarDirty = true;
            }
            finally
            {
                _simulationScheduleTimestamp = 0L;
                ReleaseFluidSimulationBufferPins();
            }
        }

        /// <summary>Uploads dirty waterline scalar DTOs to the double-buffered global shader buffer.</summary>
        public void Render(float deltaTime)
        {
            if (_floodScalarDirty)
            {
                Shader.SetGlobalFloat(s_GlobalFloodScalarId, _pendingFloodScalar);
                _floodScalarDirty = false;
            }

            if (!uploadShaderWaterlines || !_buffersReady || _hasScheduled || !_waterlineUploadDirty)
                return;

            NativeArray<FluidWaterlineShaderDTO> waterlines = ResolveFluidVaultBuffer(ref _waterlineHandle, BufferID.ShinobuFluidWaterlineShader, ResolveCompartmentCapacity());
            if (!waterlines.IsCreated)
                return;

            int safeCount = math.min(compartmentCount, waterlines.Length);
            if (safeCount <= 0 || !EnsureWaterlineGraphicsBuffers(safeCount))
                return;

            GraphicsBuffer targetBuffer = AdvanceNextWaterlineWriteBuffer();
            if (targetBuffer == null ||
                !targetBuffer.IsValid() ||
                targetBuffer.stride != UnsafeUtility.SizeOf<FluidWaterlineShaderDTO>())
                return;

            GraphicsBufferUploadUtility.UploadNativeArray(targetBuffer, waterlines, safeCount);
            Shader.SetGlobalBuffer(s_WaterlineBufferId, targetBuffer);
            Shader.SetGlobalInt(s_WaterlineCountId, safeCount);
            _waterlineUploadDirty = false;
        }

        /// <summary>Returns the currently readable flood compartment buffer for editor/debug consumers.</summary>
        public bool TryGetActiveCompartmentSnapshot(out NativeArray<FluidCompartmentDTO>.ReadOnly compartments, out int count)
        {
            if (!_buffersReady || _vault == null || _hasScheduled)
            {
                compartments = default;
                count = 0;
                return false;
            }

            NativeArray<FluidCompartmentDTO> mutableCompartments = ResolveActiveCompartments();
            if (!mutableCompartments.IsCreated)
            {
                compartments = default;
                count = 0;
                return false;
            }

            compartments = mutableCompartments.AsReadOnly();
            count = math.min(compartmentCount, compartments.Length);
            return count > 0;
        }

        /// <summary>Installs an externally owned CSR habitat graph snapshot into the flood solver Vault buffers.</summary>
        public bool InstallCsrTopology(
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> edgeFlags,
            int nodeCount,
            int graphEdgeCount)
        {
            return InstallCsrTopology(edgeOffsets, edgeDestinations, edgeFlags, default(NativeArray<float>), nodeCount, graphEdgeCount);
        }

        /// <summary>Installs an externally owned CSR habitat graph snapshot with scalar door/bulkhead conductance.</summary>
        public bool InstallCsrTopology(
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> edgeFlags,
            NativeArray<float> edgeConductivity,
            int nodeCount,
            int graphEdgeCount)
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !EnsureBuffersInitialized())
                return false;

            if (!TryAcquireLocalFluidMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<int> targetOffsets = ResolveFluidVaultBuffer(ref _edgeOffsetsHandle, BufferID.ShinobuFluidEdgeOffsets, ResolveCompartmentCapacity() + 1);
                NativeArray<int> targetDestinations = ResolveFluidVaultBuffer(ref _edgeDestinationsHandle, BufferID.ShinobuFluidEdgeDestinations, HabitatFluidIncursionConstants.MaxEdges);
                NativeArray<byte> targetFlags = ResolveFluidVaultBuffer(ref _edgeFlagsHandle, BufferID.ShinobuFluidEdgeFlags, HabitatFluidIncursionConstants.MaxEdges);
                NativeArray<float> targetConductivity = ResolveFluidVaultBuffer(ref _edgeConductivityHandle, BufferID.ShinobuFluidEdgeConductivity, HabitatFluidIncursionConstants.MaxEdges);
                if (!edgeOffsets.IsCreated || !edgeDestinations.IsCreated || !edgeFlags.IsCreated ||
                    edgeOffsets.Length <= 0 ||
                    !targetOffsets.IsCreated || !targetDestinations.IsCreated || !targetFlags.IsCreated || !targetConductivity.IsCreated)
                {
                    return false;
                }

                int safeNodeCount = math.min(math.max(0, nodeCount), math.min(compartmentCount, math.min(edgeOffsets.Length - 1, targetOffsets.Length - 1)));
                int safeEdgeCount = math.min(math.max(0, graphEdgeCount), math.min(edgeDestinations.Length, targetDestinations.Length));
                if (edgeConductivity.IsCreated)
                    safeEdgeCount = math.min(safeEdgeCount, edgeConductivity.Length);
                NativeArray<float> transferRemainders = ResolveFluidVaultBuffer(ref _transferRemaindersHandle, BufferID.ShinobuFluidTransferRemainders, HabitatFluidIncursionConstants.MaxEdges);
                for (int i = 0; i <= safeNodeCount; i++)
                    targetOffsets[i] = edgeOffsets[i];
                for (int i = 0; i < safeEdgeCount; i++)
                {
                    targetDestinations[i] = edgeDestinations[i];
                    targetFlags[i] = edgeFlags[i];
                    targetConductivity[i] = (edgeFlags[i] & FluidEdgeFlags.Sealed) != 0
                        ? 0f
                        : (edgeConductivity.IsCreated ? math.saturate(edgeConductivity[i]) : 1f);
                    if (transferRemainders.IsCreated && i < transferRemainders.Length)
                        transferRemainders[i] = 0f;
                }
                for (int i = safeEdgeCount; i < targetDestinations.Length; i++)
                {
                    targetDestinations[i] = 0;
                    targetFlags[i] = FluidEdgeFlags.Sealed;
                    targetConductivity[i] = 0f;
                    if (transferRemainders.IsCreated && i < transferRemainders.Length)
                        transferRemainders[i] = 0f;
                }

                _edgeCount = safeEdgeCount;
                return true;
            }
            finally
            {
                ReleaseLocalFluidMutationGuard(guardVault, guardMask);
            }
        }

#if UNITY_EDITOR
        /// <summary>Applies cold CSV compartment capacities to both solver buffers without managed string splitting.</summary>
        public int ApplyCompartmentVolumeCsv(NativeArray<byte> csvBytes, int byteCount)
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !EnsureBuffersInitialized())
                return 0;

            if (!TryAcquireLocalFluidMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return 0;

            try
            {
                NativeArray<FluidCompartmentDTO> active = ResolveActiveCompartments();
                NativeArray<FluidCompartmentDTO> inactive = ResolveInactiveCompartments();
                if (!active.IsCreated || !inactive.IsCreated)
                    return 0;

                int applied = HabitatFluidIncursionCsv.ParseCompartmentVolumesCsv(csvBytes, byteCount, active, compartmentCount);
                HabitatFluidIncursionCsv.ParseCompartmentVolumesCsv(csvBytes, byteCount, inactive, compartmentCount);
                return applied;
            }
            finally
            {
                ReleaseLocalFluidMutationGuard(guardVault, guardMask);
            }
        }
#endif

        /// <summary>Injects a cold/profiling mock breach into both solver buffers and shared integrity state.</summary>
        public bool GenerateMockHullBreach(int breachIndex, float breachAreaM2, float ingressRateM3PerSecond)
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !EnsureBuffersInitialized())
                return false;

            if (!TryAcquireLocalFluidMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<FluidCompartmentDTO> front = ResolveFluidVaultBuffer(ref _frontHandle, BufferID.ShinobuFluidCompartmentFront, ResolveCompartmentCapacity());
                NativeArray<FluidCompartmentDTO> back = ResolveFluidVaultBuffer(ref _backHandle, BufferID.ShinobuFluidCompartmentBack, ResolveCompartmentCapacity());
                NativeArray<IntegrityStateDTO> integrity = ResolveFluidVaultBuffer(ref _integrityHandle, BufferID.ShinobuFluidIntegrityState, ResolveCompartmentCapacity());
                if (!front.IsCreated || !back.IsCreated || !integrity.IsCreated)
                    return false;

                int safeCount = math.min(compartmentCount, math.min(front.Length, back.Length));
                if (safeCount <= 0)
                    return false;

                MockHullBreachJob breachFront = new MockHullBreachJob
                {
                    Compartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                    Integrity = (IntegrityStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(integrity),
                    CompartmentCount = safeCount,
                    BreachIndex = breachIndex,
                    BreachAreaM2 = math.max(0.0001f, breachAreaM2),
                    IngressRateM3PerSecond = math.max(0f, ingressRateM3PerSecond)
                };
                // COLD DIRECT SEED: explicit damage-control/profiling breach injection outside fixed solver cadence.
                breachFront.Execute();

                MockHullBreachJob breachBack = breachFront;
                breachBack.Compartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
                // COLD DIRECT SEED: mirror seed preserves deterministic double-buffer state.
                breachBack.Execute();
                _waterlineUploadDirty = true;
                return true;
            }
            finally
            {
                ReleaseLocalFluidMutationGuard(guardVault, guardMask);
            }
        }

        /// <summary>Injects deterministic scalar water into one node for headless flood distribution tests.</summary>
        public bool GenerateMockFloodDistribution(int targetNodeIndex, float addedWaterM3)
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !EnsureBuffersInitialized())
                return false;

            if (!TryAcquireLocalFluidMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return false;

            try
            {
                NativeArray<FluidCompartmentDTO> front = ResolveFluidVaultBuffer(ref _frontHandle, BufferID.ShinobuFluidCompartmentFront, ResolveCompartmentCapacity());
                NativeArray<FluidCompartmentDTO> back = ResolveFluidVaultBuffer(ref _backHandle, BufferID.ShinobuFluidCompartmentBack, ResolveCompartmentCapacity());
                if (!front.IsCreated || !back.IsCreated)
                    return false;

                int safeCount = math.min(compartmentCount, math.min(front.Length, back.Length));
                if (safeCount <= 0)
                    return false;

                GenerateMockFloodIncursionJob frontJob = new GenerateMockFloodIncursionJob
                {
                    Compartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front),
                    CompartmentCount = safeCount,
                    TargetNodeIndex = targetNodeIndex,
                    AddedWaterM3 = math.max(0f, addedWaterM3)
                };
                frontJob.Execute();

                GenerateMockFloodIncursionJob backJob = frontJob;
                backJob.Compartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
                backJob.Execute();
                _waterlineUploadDirty = true;
                return true;
            }
            finally
            {
                ReleaseLocalFluidMutationGuard(guardVault, guardMask);
            }
        }

        private void ApplyIncomingFluidIncursionSignals(
            NativeArray<FluidCompartmentDTO> read,
            NativeArray<FluidCompartmentDTO> write,
            NativeArray<IntegrityStateDTO> integrity,
            int safeCount)
        {
            ReadOnlySpan<FluidIncursionSignal> signals = SignalBus<FluidIncursionSignal>.GetFrameSnapshot();
            int signalCount = math.min(signals.Length, 64);
            for (int i = 0; i < signalCount; i++)
            {
                FluidIncursionSignal signal = signals[i];
                if (!AbsoluteUniversePosition.IsFinite(in signal.LeakAup))
                    continue;

                double3 leakAup = signal.LeakAup.ToAbsoluteDouble3();
                if (!math.all(math.isfinite(leakAup)))
                    continue;

                int compartmentIndex = ResolveSignalCompartmentIndex(read, safeCount, signal.CompartmentId, leakAup);
                if ((uint)compartmentIndex >= (uint)safeCount)
                    continue;

                uint nodeHash = signal.CompartmentId != 0u
                    ? signal.CompartmentId
                    : read[compartmentIndex].NodeHashID;
                float flow01 = math.saturate(math.isfinite(signal.FlowRate01) ? signal.FlowRate01 : 0f);
                float breachArea = math.lerp(0.002f, math.max(0.002f, mockBreachAreaM2), flow01);
                FluidAup48 leakBlit = ToFluidAup48(in signal.LeakAup);

                ApplyIncursionSignalToCompartment(read, compartmentIndex, nodeHash);
                ApplyIncursionSignalToCompartment(write, compartmentIndex, nodeHash);

                IntegrityStateDTO state = integrity[compartmentIndex];
                state.CenterAup = leakBlit;
                state.NodeHash = nodeHash;
                state.BreachAreaM2 = math.max(state.BreachAreaM2, breachArea);
                state.Integrity01 = math.min(math.isfinite(state.Integrity01) ? state.Integrity01 : 1f, 0.35f);
                state.Flags |= IntegrityStateDTO.FlagBreached;
                integrity[compartmentIndex] = state;
            }
        }

        private static int ResolveSignalCompartmentIndex(
            NativeArray<FluidCompartmentDTO> compartments,
            int safeCount,
            uint nodeHash,
            double3 leakAup)
        {
            if (nodeHash != 0u)
            {
                for (int i = 0; i < safeCount; i++)
                {
                    if (compartments[i].NodeHashID == nodeHash)
                        return i;
                }
            }

            int nearestIndex = -1;
            double nearestDistanceSq = double.MaxValue;
            for (int i = 0; i < safeCount; i++)
            {
                double3 delta = compartments[i].LocalCenterOfMass - leakAup;
                double distanceSq = math.lengthsq(delta);
                if (!math.isfinite(distanceSq) || distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                nearestIndex = i;
            }

            return nearestIndex;
        }

        private static void ApplyIncursionSignalToCompartment(
            NativeArray<FluidCompartmentDTO> compartments,
            int index,
            uint nodeHash)
        {
            if (!compartments.IsCreated || (uint)index >= (uint)compartments.Length)
                return;

            FluidCompartmentDTO dto = compartments[index];
            if (nodeHash != 0u)
                dto.NodeHashID = nodeHash;
            dto.Flags |= FluidCompartmentFlags.Breached;
            compartments[index] = dto;
        }

        private void CacheDataVaultCold(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            ReleaseFluidVaultHandles(_vault);
            _vault = vault;
            ClearVaultHandles();
        }

        private void ReleaseFluidVaultHandles(IDataVault vault)
        {
            if (vault == null)
            {
                ClearVaultHandles();
                return;
            }

            ReleaseFluidVaultHandle(vault, ref _frontHandle);
            ReleaseFluidVaultHandle(vault, ref _backHandle);
            ReleaseFluidVaultHandle(vault, ref _integrityHandle);
            ReleaseFluidVaultHandle(vault, ref _edgeOffsetsHandle);
            ReleaseFluidVaultHandle(vault, ref _edgeDestinationsHandle);
            ReleaseFluidVaultHandle(vault, ref _edgeFlagsHandle);
            ReleaseFluidVaultHandle(vault, ref _edgeConductivityHandle);
            ReleaseFluidVaultHandle(vault, ref _centroidsHandle);
            ReleaseFluidVaultHandle(vault, ref _waterlineHandle);
            ReleaseFluidVaultHandle(vault, ref _massStateHandle);
            ReleaseFluidVaultHandle(vault, ref _tuningHandle);
            ReleaseFluidVaultHandle(vault, ref _telemetryHandle);
            ReleaseFluidVaultHandle(vault, ref _compartmentTelemetryHandle);
            ReleaseFluidVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseFluidVaultHandle(vault, ref _bfsQueueHandle);
            ReleaseFluidVaultHandle(vault, ref _bfsVisitedHandle);
            ReleaseFluidVaultHandle(vault, ref _deltaVolumesHandle);
            ReleaseFluidVaultHandle(vault, ref _transferRemaindersHandle);
            ReleaseFluidVaultHandle(vault, ref _summaryHandle);
            ClearVaultHandles();
        }

        private static void ReleaseFluidVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u &&
                handle.Generation != 0u &&
                handle.SystemID == (uint)OwnerSystem)
            {
                vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void ClearVaultHandles()
        {
            _frontHandle = default;
            _backHandle = default;
            _integrityHandle = default;
            _edgeOffsetsHandle = default;
            _edgeDestinationsHandle = default;
            _edgeFlagsHandle = default;
            _edgeConductivityHandle = default;
            _centroidsHandle = default;
            _waterlineHandle = default;
            _massStateHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _compartmentTelemetryHandle = default;
            _telemetryCursorHandle = default;
            _bfsQueueHandle = default;
            _bfsVisitedHandle = default;
            _deltaVolumesHandle = default;
            _transferRemaindersHandle = default;
            _summaryHandle = default;
            _edgeCount = 0;
            _activeMutationGuardMask = 0UL;
            _activeMutationGuardVault = null;
            _simulationBufferPinMask = 0u;
            _simulationBufferPinVault = null;
            _buffersReady = false;
        }

        private int ResolveCompartmentCapacity()
        {
            return math.clamp(compartmentCount, 1, HabitatFluidIncursionConstants.MaxCompartments);
        }

        private bool OpenOrAcquireFluidVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool allowColdInitialization,
            out NativeArray<T> buffer)
            where T : struct
        {
            if (TryOpenFluidVaultBuffer(ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (_vault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (_vault.IsAllocationLocked || _vault.IsCompactionFenceActive)
            {
                if (!_vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existingHandle) ||
                    !IsFluidVaultHandle(in existingHandle, bufferId))
                {
                    buffer = default;
                    return false;
                }

                handle = existingHandle;
                return TryOpenFluidVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
            }

            if (!allowColdInitialization)
                return false;

            handle = _vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystem,
                options);
            return TryOpenFluidVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryOpenFluidVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (_vault == null ||
                requiredLength <= 0 ||
                !IsFluidVaultHandle(in handle, bufferId) ||
                !_vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private NativeArray<T> ResolveFluidVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : struct
        {
            return TryOpenFluidVaultBuffer(ref handle, bufferId, requiredLength, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static bool IsFluidVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private bool EnsureBuffersInitialized(bool allowColdInitialization = true)
        {
            if (!allowColdInitialization)
                return _buffersReady;

            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_vault == null)
                return false;

            int safeCount = math.clamp(compartmentCount, 1, HabitatFluidIncursionConstants.MaxCompartments);
            int edgeCapacity = HabitatFluidIncursionConstants.MaxEdges;
            bool buffersReady =
                OpenOrAcquireFluidVaultBuffer(ref _frontHandle, BufferID.ShinobuFluidCompartmentFront, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _backHandle, BufferID.ShinobuFluidCompartmentBack, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _integrityHandle, BufferID.ShinobuFluidIntegrityState, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _edgeOffsetsHandle, BufferID.ShinobuFluidEdgeOffsets, safeCount + 1, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _edgeDestinationsHandle, BufferID.ShinobuFluidEdgeDestinations, edgeCapacity, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _edgeFlagsHandle, BufferID.ShinobuFluidEdgeFlags, edgeCapacity, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _edgeConductivityHandle, BufferID.ShinobuFluidEdgeConductivity, edgeCapacity, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _centroidsHandle, BufferID.ShinobuFluidCompartmentCentroids, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _waterlineHandle, BufferID.ShinobuFluidWaterlineShader, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _massStateHandle, BufferID.ShinobuFluidMassState, 1, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _tuningHandle, BufferID.ShinobuFluidTuning, 1, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _telemetryHandle, BufferID.ShinobuFluidTelemetryRing, HabitatFluidIncursionConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _compartmentTelemetryHandle, BufferID.ShinobuFluidCompartmentTelemetry, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _telemetryCursorHandle, BufferID.ShinobuFluidTelemetryCursor, 1, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _bfsQueueHandle, BufferID.ShinobuFluidBfsQueue, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _bfsVisitedHandle, BufferID.ShinobuFluidBfsVisited, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _deltaVolumesHandle, BufferID.ShinobuFluidDeltaVolumes, safeCount, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _transferRemaindersHandle, BufferID.ShinobuFluidTransferRemainders, edgeCapacity, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _) &&
                OpenOrAcquireFluidVaultBuffer(ref _summaryHandle, BufferID.ShinobuFluidFrameSummary, 1, NativeArrayOptions.UninitializedMemory, allowColdInitialization, out _);
            if (!buffersReady)
                return false;

            if (!FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout())
                return false;

            InitializeColdBootBuffers(safeCount);
            compartmentCount = safeCount;
            _buffersReady = true;
            return true;
        }

        private void InitializeColdBootBuffers(int safeCount)
        {
            NativeArray<FluidCompartmentDTO> front = ResolveFluidVaultBuffer(ref _frontHandle, BufferID.ShinobuFluidCompartmentFront, safeCount);
            NativeArray<FluidCompartmentDTO> back = ResolveFluidVaultBuffer(ref _backHandle, BufferID.ShinobuFluidCompartmentBack, safeCount);
            NativeArray<IntegrityStateDTO> integrity = ResolveFluidVaultBuffer(ref _integrityHandle, BufferID.ShinobuFluidIntegrityState, safeCount);
            NativeArray<float3> centroids = ResolveFluidVaultBuffer(ref _centroidsHandle, BufferID.ShinobuFluidCompartmentCentroids, safeCount);
            NativeArray<FluidWaterlineShaderDTO> waterlines = ResolveFluidVaultBuffer(ref _waterlineHandle, BufferID.ShinobuFluidWaterlineShader, safeCount);
            if (!front.IsCreated || !back.IsCreated || !integrity.IsCreated || !centroids.IsCreated || !waterlines.IsCreated)
                return;

            FluidAup48 origin = ResolveColdBootOriginAup();
            FluidCompartmentDTO* frontPtr = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(front);
            FluidCompartmentDTO* backPtr = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
            IntegrityStateDTO* integrityPtr = (IntegrityStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(integrity);

            FluidCompartmentClearJob clearFront = new FluidCompartmentClearJob
            {
                Compartments = frontPtr,
                Integrity = integrityPtr,
                LocalCentroids = centroids,
                Waterlines = waterlines,
                OriginAup = origin,
                NodeHashSeed = 0xF1000001u,
                DefaultVolumeM3 = defaultCompartmentVolumeM3,
                DefaultFloorHeightLocal = defaultFloorHeightLocal,
                ActiveCount = safeCount
            };
            // COLD DIRECT CLEAR: boot-only explicit initialization of uninitialized Vault memory.
            for (int i = 0; i < safeCount; i++)
                clearFront.Execute(i);

            FluidCompartmentClearJob clearBack = clearFront;
            clearBack.Compartments = backPtr;
            // COLD DIRECT CLEAR: boot-only mirror clear for deterministic double-buffer start state.
            for (int i = 0; i < safeCount; i++)
                clearBack.Execute(i);

            BuildDefaultLineTopology(safeCount);
            InitializeTuningBuffer(safeCount);

            NativeArray<int> telemetryCursor = ResolveFluidVaultBuffer(ref _telemetryCursorHandle, BufferID.ShinobuFluidTelemetryCursor, 1);
            NativeArray<FluidCompartmentTelemetryDTO> compartmentTelemetry = ResolveFluidVaultBuffer(ref _compartmentTelemetryHandle, BufferID.ShinobuFluidCompartmentTelemetry, safeCount);
            NativeArray<FluidIncursionFrameSummaryDTO> summary = ResolveFluidVaultBuffer(ref _summaryHandle, BufferID.ShinobuFluidFrameSummary, 1);
            NativeArray<FluidMassStateDTO> massState = ResolveFluidVaultBuffer(ref _massStateHandle, BufferID.ShinobuFluidMassState, 1);
            NativeArray<float> transferRemainders = ResolveFluidVaultBuffer(ref _transferRemaindersHandle, BufferID.ShinobuFluidTransferRemainders, HabitatFluidIncursionConstants.MaxEdges);
            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                telemetryCursor[0] = 0;
            if (compartmentTelemetry.IsCreated)
            {
                for (int i = 0; i < safeCount && i < compartmentTelemetry.Length; i++)
                    compartmentTelemetry[i] = default;
            }
            if (summary.IsCreated && summary.Length > 0)
                summary[0] = default;
            if (massState.IsCreated && massState.Length > 0)
                massState[0] = default;
            if (transferRemainders.IsCreated)
            {
                for (int i = 0; i < transferRemainders.Length; i++)
                    transferRemainders[i] = 0f;
            }

            if (seedMockBreachOnEnable)
            {
                MockHullBreachJob breachFront = new MockHullBreachJob
                {
                    Compartments = frontPtr,
                    Integrity = integrityPtr,
                    CompartmentCount = safeCount,
                    BreachIndex = mockBreachIndex,
                    BreachAreaM2 = mockBreachAreaM2,
                    IngressRateM3PerSecond = HabitatFluidIncursionConstants.DefaultIngressRateM3PerSecond
                };
                // COLD DIRECT SEED: isolated profile breach seed before runtime ticks start.
                breachFront.Execute();
                MockHullBreachJob breachBack = breachFront;
                breachBack.Compartments = backPtr;
                // COLD DIRECT SEED: mirror seed keeps both buffers identical at frame zero.
                breachBack.Execute();
            }
        }

        private FluidAup48 ResolveColdBootOriginAup()
        {
            Vector3 runtimePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                runtimePosition = Vector3.zero;
            }

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            AbsoluteUniversePosition originAup = math.all(math.isfinite(origin))
                ? AbsoluteUniversePosition.FromAbsolutePosition(origin)
                : default;
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return ToFluidAup48(in resolvedAup);
        }

        private void BuildDefaultLineTopology(int safeCount)
        {
            NativeArray<int> edgeOffsets = ResolveFluidVaultBuffer(ref _edgeOffsetsHandle, BufferID.ShinobuFluidEdgeOffsets, safeCount + 1);
            NativeArray<int> edgeDestinations = ResolveFluidVaultBuffer(ref _edgeDestinationsHandle, BufferID.ShinobuFluidEdgeDestinations, HabitatFluidIncursionConstants.MaxEdges);
            NativeArray<byte> edgeFlags = ResolveFluidVaultBuffer(ref _edgeFlagsHandle, BufferID.ShinobuFluidEdgeFlags, HabitatFluidIncursionConstants.MaxEdges);
            NativeArray<float> edgeConductivity = ResolveFluidVaultBuffer(ref _edgeConductivityHandle, BufferID.ShinobuFluidEdgeConductivity, HabitatFluidIncursionConstants.MaxEdges);
            if (!edgeOffsets.IsCreated || !edgeDestinations.IsCreated || !edgeFlags.IsCreated || !edgeConductivity.IsCreated)
                return;

            int edgeCursor = 0;
            for (int node = 0; node < safeCount; node++)
            {
                edgeOffsets[node] = edgeCursor;
                if (node > 0 && edgeCursor < edgeDestinations.Length)
                {
                    edgeDestinations[edgeCursor] = node - 1;
                    edgeFlags[edgeCursor] = 0;
                    edgeConductivity[edgeCursor] = 1f;
                    edgeCursor++;
                }
                if (node + 1 < safeCount && edgeCursor < edgeDestinations.Length)
                {
                    edgeDestinations[edgeCursor] = node + 1;
                    edgeFlags[edgeCursor] = 0;
                    edgeConductivity[edgeCursor] = 1f;
                    edgeCursor++;
                }
            }

            edgeOffsets[safeCount] = edgeCursor;
            for (int i = edgeCursor; i < edgeDestinations.Length; i++)
            {
                edgeDestinations[i] = 0;
                edgeFlags[i] = FluidEdgeFlags.Sealed;
                edgeConductivity[i] = 0f;
            }

            _edgeCount = edgeCursor;
        }

        private void InitializeTuningBuffer(int safeCount)
        {
            NativeArray<FluidIncursionTuningDTO> tuningArray = ResolveFluidVaultBuffer(ref _tuningHandle, BufferID.ShinobuFluidTuning, 1);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                return;

            float quality = ResolveGlobalQualityWeight();
            tuningArray[0] = new FluidIncursionTuningDTO
            {
                GlobalQualityWeight = quality,
                TransferRate01PerSecond = HabitatFluidIncursionConstants.DefaultTransferRate01PerSecond,
                MaxTransferPerNodeM3 = HabitatFluidIncursionConstants.DefaultMaxTransferPerNodeM3,
                DischargeCoefficient = 0.72f,
                MaxIngressPerSecondNormalized = 0.18f,
                MassPublishIntervalSeconds = HabitatFluidIncursionConstants.DefaultMassPublishIntervalSeconds,
                BaseMassKg = baseMassKg,
                WaterDensityKgPerM3 = HabitatFluidIncursionConstants.SeawaterDensityKgPerM3,
                VisualWobbleScalar = 0.35f,
                AcousticMuffleGain = 1f,
                StateHash = 0u,
                Frame = unchecked((uint)_frame),
                SolverIterations = ResolveAuthoritySolverIterations(),
                CompartmentCount = (ushort)math.min(ushort.MaxValue, safeCount),
                EdgeCount = (ushort)math.min(ushort.MaxValue, _edgeCount)
            };
        }

        private FluidIncursionTuningDTO RefreshTuning(NativeArray<FluidIncursionTuningDTO> tuningArray, float deltaTime, int safeCount)
        {
            FluidIncursionTuningDTO tuning = tuningArray[0];
            float quality = ResolveGlobalQualityWeight();
            tuning.GlobalQualityWeight = quality;
            tuning.SolverIterations = ResolveAuthoritySolverIterations();
            tuning.Frame = unchecked((uint)_frame);
            tuning.CompartmentCount = (ushort)math.min(ushort.MaxValue, safeCount);
            tuning.EdgeCount = (ushort)math.min(ushort.MaxValue, _edgeCount);
            tuning.TransferRate01PerSecond = math.max(0f, tuning.TransferRate01PerSecond);
            tuning.MaxTransferPerNodeM3 = math.max(0.01f, tuning.MaxTransferPerNodeM3);
            tuning.DischargeCoefficient = math.clamp(tuning.DischargeCoefficient, 0.1f, 1f);
            tuning.MaxIngressPerSecondNormalized = math.max(0.01f, tuning.MaxIngressPerSecondNormalized);
            tuning.MassPublishIntervalSeconds = math.max(deltaTime, tuning.MassPublishIntervalSeconds);
            tuning.BaseMassKg = math.max(1f, baseMassKg);
            tuning.WaterDensityKgPerM3 = math.max(1f, tuning.WaterDensityKgPerM3);
            tuning.VisualWobbleScalar = math.max(0f, tuning.VisualWobbleScalar);
            tuning.AcousticMuffleGain = math.max(0f, tuning.AcousticMuffleGain);
            tuningArray[0] = tuning;
            return tuning;
        }

        private bool PublishMassAndAcousticSignals(in FluidIncursionFrameSummaryDTO summary)
        {
            NativeArray<FluidMassStateDTO> massStateArray = ResolveFluidVaultBuffer(ref _massStateHandle, BufferID.ShinobuFluidMassState, 1);
            if (!massStateArray.IsCreated || massStateArray.Length <= 0)
                return true;

            bool accepted = true;
            FluidMassStateDTO massState = massStateArray[0];
            SubmarineFloodStateSignal floodState = new SubmarineFloodStateSignal
            {
                DynamicCenterOfMassLocal = massState.DynamicCenterOfMassLocal,
                DynamicCenterOfMassOffsetLocal = massState.DynamicCenterOfMassOffsetLocal,
                TotalWaterMassKg = massState.TotalWaterMassKg,
                BaseMassKg = massState.BaseMassKg,
                FillRatio01 = massState.FillRatio01,
                AngularDragMultiplier = massState.AngularDragMultiplier,
                SourceBodyId = massState.SourceBodyId,
                Frame = massState.Frame,
                RoomCount = massState.CompartmentCount,
                MathLod = massState.MathLod,
                Flags = massState.Flags
            };
            if (massState.TotalWaterMassKg > HabitatFluidIncursionConstants.WaterEpsilonM3)
                floodState.Flags |= SubmarineFloodStateSignal.FlagHasFloodMass;
            if (summary.MaxFill01 > 0.82f)
                floodState.Flags |= SubmarineFloodStateSignal.FlagCriticalFlood;
            if (summary.InvalidCount > 0)
                floodState.Flags |= SubmarineFloodStateSignal.FlagInvalid;
            if (!SignalBus<SubmarineFloodStateSignal>.TryPushTracked(in floodState, ref s_x001DirectSignalPushDropCount_HabitatFluidIncursionDirector))
            {
                IncrementDroppedSignalCount();
                accepted = false;
            }

            Vector3 dynamicCenter = new Vector3(
                massState.DynamicCenterOfMassLocal.x,
                massState.DynamicCenterOfMassLocal.y,
                massState.DynamicCenterOfMassLocal.z);
            Vector3 centerOffset = new Vector3(
                massState.DynamicCenterOfMassOffsetLocal.x,
                massState.DynamicCenterOfMassOffsetLocal.y,
                massState.DynamicCenterOfMassOffsetLocal.z);
            FloodMassShiftEvent massEvent = new FloodMassShiftEvent(
                dynamicCenter,
                centerOffset,
                massState.TotalWaterMassKg,
                massState.FillRatio01,
                massState.AngularDragMultiplier,
                unchecked((int)massState.SourceBodyId),
                massState.Frame,
                massState.MathLod,
                massState.Flags);
            if (!PhysicsEventBus.TryNotifyFloodMassShift(in massEvent))
            {
                IncrementDroppedSignalCount();
                accepted = false;
            }

            if (!PublishAcousticMuffle(in summary))
                accepted = false;

            return accepted;
        }

        private bool PublishAcousticMuffle(in FluidIncursionFrameSummaryDTO summary)
        {
            float intensity = math.saturate(summary.AcousticFloodIntensity01);
            float cutoffHz = math.lerp(9000f, HabitatFluidIncursionConstants.DefaultLowPassCutoffHz, intensity);
            float transmission01 = math.saturate(math.lerp(1f, 0.22f, intensity));
            FluidAup48 sourceAup = ResolveSourceAupBlit();
            HabitatFloodAcousticMuffleSignal signal = new HabitatFloodAcousticMuffleSignal
            {
                SourceGridX = sourceAup.GridX,
                SourceGridY = sourceAup.GridY,
                SourceGridZ = sourceAup.GridZ,
                SourceLocal = sourceAup.Local,
                SourceHash = _sourceBodyId,
                FloodIntensity01 = intensity,
                LowPassCutoffHz = cutoffHz,
                TransmissionByte = (byte)math.clamp((int)math.round(transmission01 * 255f), 0, 255),
                Flags = (byte)(summary.MaxFill01 > 0.82f ? HabitatFloodAcousticMuffleSignal.FlagCriticalFlood : 0)
            };
            if (SignalBus<HabitatFloodAcousticMuffleSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_HabitatFluidIncursionDirector))
                return true;

            IncrementDroppedSignalCount();
            return false;
        }

        private void IncrementDroppedSignalCount()
        {
            if (_droppedSignalCount < 0x3FFFFFFF)
                _droppedSignalCount++;
        }

        private FluidAup48 ResolveSourceAupBlit()
        {
            NativeArray<IntegrityStateDTO> integrity = ResolveFluidVaultBuffer(ref _integrityHandle, BufferID.ShinobuFluidIntegrityState, ResolveCompartmentCapacity());
            if (integrity.IsCreated && integrity.Length > 0)
                return integrity[0].CenterAup;

            return default;
        }

        private NativeArray<FluidCompartmentDTO> ResolveActiveCompartments()
        {
            return _frontIsA
                ? ResolveFluidVaultBuffer(ref _frontHandle, BufferID.ShinobuFluidCompartmentFront, ResolveCompartmentCapacity())
                : ResolveFluidVaultBuffer(ref _backHandle, BufferID.ShinobuFluidCompartmentBack, ResolveCompartmentCapacity());
        }

        private NativeArray<FluidCompartmentDTO> ResolveInactiveCompartments()
        {
            return _frontIsA
                ? ResolveFluidVaultBuffer(ref _backHandle, BufferID.ShinobuFluidCompartmentBack, ResolveCompartmentCapacity())
                : ResolveFluidVaultBuffer(ref _frontHandle, BufferID.ShinobuFluidCompartmentFront, ResolveCompartmentCapacity());
        }

        private void CompleteScheduledSimulationForAuthoritativeWrite()
        {
            if (!_hasScheduled)
                return;

            // COLD SYNC JOB: topology/CSV/mock author writes must not race a pending flood worker.
            bool completed;
            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            if (!completed)
                return;

            try
            {
                _hasScheduled = false;
            }
            finally
            {
                ReleaseFluidSimulationBufferPins();
                ReleaseFluidSimulationMutationGuard();
            }
        }

        private bool TryFinalizeScheduledSimulation()
        {
            if (!_hasScheduled)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return false;

            _hasScheduled = false;
            return true;
        }

        private bool TryLockFluidSimulationBuffers()
        {
            if (_simulationBufferPinMask != 0u)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            _simulationBufferPinVault = vault;
            if (!TryLockFluidSimulationBuffer(BufferID.ShinobuFluidCompartmentFront, FluidPinFront) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidCompartmentBack, FluidPinBack) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidIntegrityState, FluidPinIntegrity) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidEdgeOffsets, FluidPinEdgeOffsets) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidEdgeDestinations, FluidPinEdgeDestinations) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidEdgeFlags, FluidPinEdgeFlags) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidEdgeConductivity, FluidPinEdgeConductivity) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidCompartmentCentroids, FluidPinCentroids) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidWaterlineShader, FluidPinWaterline) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidMassState, FluidPinMassState) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidTuning, FluidPinTuning) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidTelemetryRing, FluidPinTelemetry) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidCompartmentTelemetry, FluidPinCompartmentTelemetry) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidTelemetryCursor, FluidPinTelemetryCursor) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidBfsQueue, FluidPinBfsQueue) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidBfsVisited, FluidPinBfsVisited) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidDeltaVolumes, FluidPinDeltaVolumes) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidTransferRemainders, FluidPinTransferRemainders) ||
                !TryLockFluidSimulationBuffer(BufferID.ShinobuFluidFrameSummary, FluidPinSummary))
            {
                ReleaseFluidSimulationBufferPins();
                return false;
            }

            return true;
        }

        private bool TryLockFluidSimulationBuffer(BufferID bufferId, uint pinBit)
        {
            IDataVault vault = _simulationBufferPinVault;
            if (vault == null || bufferId == BufferID.Unknown)
                return false;

            if ((_simulationBufferPinMask & pinBit) != 0u)
                return true;

            if (!vault.TryLockBuffer(bufferId, OwnerSystem))
                return false;

            _simulationBufferPinMask |= pinBit;
            return true;
        }

        private void ReleaseFluidSimulationBufferPins()
        {
            IDataVault vault = _simulationBufferPinVault;
            uint mask = _simulationBufferPinMask;
            _simulationBufferPinVault = null;
            _simulationBufferPinMask = 0u;
            if (vault == null || mask == 0u)
                return;

            TryUnlockFluidSimulationPin(vault, mask, FluidPinSummary, BufferID.ShinobuFluidFrameSummary);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinTransferRemainders, BufferID.ShinobuFluidTransferRemainders);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinDeltaVolumes, BufferID.ShinobuFluidDeltaVolumes);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinBfsVisited, BufferID.ShinobuFluidBfsVisited);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinBfsQueue, BufferID.ShinobuFluidBfsQueue);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinTelemetryCursor, BufferID.ShinobuFluidTelemetryCursor);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinCompartmentTelemetry, BufferID.ShinobuFluidCompartmentTelemetry);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinTelemetry, BufferID.ShinobuFluidTelemetryRing);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinTuning, BufferID.ShinobuFluidTuning);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinMassState, BufferID.ShinobuFluidMassState);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinWaterline, BufferID.ShinobuFluidWaterlineShader);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinCentroids, BufferID.ShinobuFluidCompartmentCentroids);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinEdgeConductivity, BufferID.ShinobuFluidEdgeConductivity);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinEdgeFlags, BufferID.ShinobuFluidEdgeFlags);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinEdgeDestinations, BufferID.ShinobuFluidEdgeDestinations);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinEdgeOffsets, BufferID.ShinobuFluidEdgeOffsets);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinIntegrity, BufferID.ShinobuFluidIntegrityState);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinBack, BufferID.ShinobuFluidCompartmentBack);
            TryUnlockFluidSimulationPin(vault, mask, FluidPinFront, BufferID.ShinobuFluidCompartmentFront);
        }

        private static void TryUnlockFluidSimulationPin(IDataVault vault, uint mask, uint pinBit, BufferID bufferId)
        {
            if ((mask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystem);
        }

        private bool TryAcquireFluidSimulationMutationGuard()
        {
            if (_activeMutationGuardMask != 0UL)
                return true;

            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireMutationGuard(FluidSimulationMutationGuardMask))
                return false;

            _activeMutationGuardMask = FluidSimulationMutationGuardMask;
            _activeMutationGuardVault = vault;
            return true;
        }

        private bool TryAcquireLocalFluidMutationGuard(out ulong guardMask, out IDataVault guardVault)
        {
            guardMask = 0UL;
            guardVault = null;
            IDataVault vault = _vault;
            if (vault == null || _activeMutationGuardMask != 0UL)
                return false;

            if (!vault.TryAcquireMutationGuard(FluidSimulationMutationGuardMask))
                return false;

            guardMask = FluidSimulationMutationGuardMask;
            guardVault = vault;
            return true;
        }

        private void ReleaseFluidSimulationMutationGuard()
        {
            ulong guardMask = _activeMutationGuardMask;
            IDataVault vault = _activeMutationGuardVault;
            if (guardMask != 0UL)
                vault?.ReleaseMutationGuard(guardMask);
            _activeMutationGuardMask = 0UL;
            _activeMutationGuardVault = null;
        }

        private static void ReleaseLocalFluidMutationGuard(IDataVault guardVault, ulong guardMask)
        {
            if (guardMask != 0UL)
                guardVault?.ReleaseMutationGuard(guardMask);
        }

        private bool EnsureWaterlineGraphicsBuffers(int safeCount)
        {
            if (_waterlineBufferA != null &&
                _waterlineBufferB != null &&
                _waterlineBufferCapacity >= safeCount)
            {
                return _waterlineBufferA.IsValid() && _waterlineBufferB.IsValid();
            }

            ReleaseGraphicsBuffer(ref _waterlineBufferA);
            ReleaseGraphicsBuffer(ref _waterlineBufferB);
            _waterlineBufferCapacity = math.max(1, math.ceilpow2(safeCount));
            _waterlineBufferA = CreateWaterlineGraphicsBuffer(_waterlineBufferCapacity);
            _waterlineBufferB = CreateWaterlineGraphicsBuffer(_waterlineBufferCapacity);
            return _waterlineBufferA.IsValid() && _waterlineBufferB.IsValid();
        }

        private static GraphicsBuffer CreateWaterlineGraphicsBuffer(int capacity)
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                capacity,
                UnsafeUtility.SizeOf<FluidWaterlineShaderDTO>());
        }

        private GraphicsBuffer AdvanceNextWaterlineWriteBuffer()
        {
            _waterlineWriteBufferIndex ^= 1;
            return _waterlineWriteBufferIndex == 0 ? _waterlineBufferA : _waterlineBufferB;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private FluidAup48 ResolveExternalWaterlineAup()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            AbsoluteUniversePosition originAup = math.all(math.isfinite(origin))
                ? AbsoluteUniversePosition.FromAbsolutePosition(origin)
                : default;
            AbsoluteUniversePosition waterlineAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(0d, ResolveExternalWaterlineRuntimeY(externalWaterlineRuntimeY), 0d));
            return ToFluidAup48(in waterlineAup);
        }

        private static float ResolveExternalWaterlineRuntimeY(float candidateWaterlineY)
        {
            return math.isfinite(candidateWaterlineY) &&
                math.abs(candidateWaterlineY) > 0.0001f &&
                math.abs(candidateWaterlineY) <= 1000f
                ? candidateWaterlineY
                : DefaultExternalWaterlineRuntimeY;
        }

        private static FluidAup48 ToFluidAup48(in AbsoluteUniversePosition position)
        {
            return new FluidAup48
            {
                GridX = position.GridX,
                GridY = position.GridY,
                GridZ = position.GridZ,
                Local = new float3(position.LocalX, position.LocalY, position.LocalZ),
                Reserved0 = 0u,
                Reserved1 = 0UL
            };
        }

        private static ushort ResolveSolverIterations(float quality)
        {
            float q = math.smoothstep(0f, 1f, math.saturate(quality));
            return (ushort)math.clamp(
                (int)math.round(math.lerp(
                    HabitatFluidIncursionConstants.MinSolverIterations,
                    HabitatFluidIncursionConstants.MaxSolverIterations,
                    q)),
                HabitatFluidIncursionConstants.MinSolverIterations,
                HabitatFluidIncursionConstants.MaxSolverIterations);
        }

        private static ushort ResolveAuthoritySolverIterations()
        {
            return ResolveSolverIterations(HabitatFluidIncursionMath.AuthoritativeQualityWeight);
        }

        private static int ResolveBfsNodeBudget(float quality)
        {
            float q = math.smoothstep(0f, 1f, math.saturate(quality));
            return math.clamp(
                (int)math.round(math.lerp(
                    HabitatFluidIncursionConstants.MinBfsNodesPerTick,
                    HabitatFluidIncursionConstants.MaxBfsNodesPerTick,
                    q)),
                HabitatFluidIncursionConstants.MinBfsNodesPerTick,
                HabitatFluidIncursionConstants.MaxBfsNodesPerTick);
        }

        private static int ResolveAuthorityBfsNodeBudget()
        {
            return ResolveBfsNodeBudget(HabitatFluidIncursionMath.AuthoritativeQualityWeight);
        }

        private static float ResolveAuthoritySolverWindowSeconds()
        {
            float q = math.smoothstep(0f, 1f, HabitatFluidIncursionMath.AuthoritativeQualityWeight);
            return math.lerp(0.2f, 0.016f, q);
        }

        private static float ResolveGlobalQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, HabitatFluidIncursionMath.AuthoritativeQualityWeight);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : HabitatFluidIncursionMath.AuthoritativeQualityWeight);
        }

        private static uint ResolveElapsedMicroseconds(long startTimestamp)
        {
            if (startTimestamp <= 0L)
                return 0u;

            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            if (elapsedTicks < 0L)
                elapsedTicks = 0L;

            long frequency = Stopwatch.Frequency > 0L ? Stopwatch.Frequency : 1L;
            long microseconds = (elapsedTicks * 1000000L) / frequency;
            return microseconds >= uint.MaxValue ? uint.MaxValue : (uint)microseconds;
        }

        private void StampSolverWallTime(uint solverWallMicroseconds)
        {
            NativeArray<FluidIncursionFrameSummaryDTO> summaryArray = ResolveFluidVaultBuffer(ref _summaryHandle, BufferID.ShinobuFluidFrameSummary, 1);
            if (summaryArray.IsCreated && summaryArray.Length > 0)
            {
                FluidIncursionFrameSummaryDTO summary = summaryArray[0];
                summary.SolverWallMicroseconds = solverWallMicroseconds;
                summaryArray[0] = summary;
            }

            NativeArray<int> telemetryCursor = ResolveFluidVaultBuffer(ref _telemetryCursorHandle, BufferID.ShinobuFluidTelemetryCursor, 1);
            NativeArray<FluidIncursionTelemetryEntry> telemetry = ResolveFluidVaultBuffer(ref _telemetryHandle, BufferID.ShinobuFluidTelemetryRing, HabitatFluidIncursionConstants.TelemetryFrameCount);
            if (!telemetryCursor.IsCreated || telemetryCursor.Length <= 0 || !telemetry.IsCreated || telemetry.Length <= 0)
                return;

            int capacity = math.min(HabitatFluidIncursionConstants.TelemetryFrameCount, telemetry.Length);
            int cursor = telemetryCursor[0] - 1;
            if (cursor < 0 || capacity <= 0)
                return;

            int index = cursor % capacity;
            FluidIncursionTelemetryEntry entry = telemetry[index];
            entry.SolverWallMicroseconds = solverWallMicroseconds;
            telemetry[index] = entry;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticLanes()
        {
            s_FloodMuffleLaneInitialized = false;
        }

        private static void EnsureFloodMuffleSignalLane()
        {
            if (s_FloodMuffleLaneInitialized)
                return;

            SignalBus<HabitatFloodAcousticMuffleSignal>.Configure(
                HabitatFloodAcousticMuffleSignal.ExpectedCapacity,
                HabitatFloodAcousticMuffleSignal.MaxFrameSignals,
                HabitatFloodAcousticMuffleSignal.LowTierFrameSignals,
                HabitatFloodAcousticMuffleSignal.LaneHash);
            SignalBus<HabitatFloodAcousticMuffleSignal>.EnsureInitialized();
            s_FloodMuffleLaneInitialized = true;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
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

        private void DumpBlackBoxOnce()
        {
            if (_dumpWritten)
                return;

            NativeArray<FluidIncursionTelemetryEntry> telemetry = ResolveFluidVaultBuffer(ref _telemetryHandle, BufferID.ShinobuFluidTelemetryRing, HabitatFluidIncursionConstants.TelemetryFrameCount);
            NativeArray<int> cursor = ResolveFluidVaultBuffer(ref _telemetryCursorHandle, BufferID.ShinobuFluidTelemetryCursor, 1);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            int cursorValue = cursor.IsCreated && cursor.Length > 0 ? math.max(0, cursor[0]) : 0;
            int latestIndex = cursorValue > 0 ? (cursorValue - 1) % telemetry.Length : 0;
            FluidIncursionTelemetryEntry latest = telemetry[latestIndex];
            float scalar = math.max(latest.TotalWaterM3, latest.PeakIngressRate);
            GlobalTelemetryBus.PushEvent(HabitatFaultEventHash, scalar, latest.StateHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(HabitatFaultDumpHash);
            _dumpWritten = true;
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private void OnDrawGizmos()
        {
            if (!drawHeatmapGizmos || !TryGetActiveCompartmentSnapshot(out NativeArray<FluidCompartmentDTO>.ReadOnly compartments, out int count))
                return;

            int capacity = ResolveCompartmentCapacity();
            NativeArray<float3> centroids = ResolveFluidVaultBuffer(ref _centroidsHandle, BufferID.ShinobuFluidCompartmentCentroids, capacity);
            NativeArray<int> edgeOffsets = ResolveFluidVaultBuffer(ref _edgeOffsetsHandle, BufferID.ShinobuFluidEdgeOffsets, capacity + 1);
            NativeArray<int> edgeDestinations = ResolveFluidVaultBuffer(ref _edgeDestinationsHandle, BufferID.ShinobuFluidEdgeDestinations, HabitatFluidIncursionConstants.MaxEdges);
            if (!centroids.IsCreated)
                return;

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            for (int i = 0; i < count && i < centroids.Length; i++)
            {
                FluidCompartmentDTO dto = compartments[i];
                float fill = dto.MaxWaterVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                    ? math.saturate(dto.CurrentWaterVolume * math.rcp(dto.MaxWaterVolume))
                    : 0f;
                Vector3 position = localToWorld.MultiplyPoint3x4(centroids[i]);
                Color color = Color.Lerp(new Color(0f, 0.35f, 1f, 0.18f), new Color(0f, 0f, 0.22f, 1f), fill);
                Gizmos.color = color;
                Gizmos.DrawCube(position, Vector3.one * math.lerp(0.35f, 1.1f, fill));
            }

            if (!edgeOffsets.IsCreated || !edgeDestinations.IsCreated)
                return;

            Gizmos.color = Color.red;
            int safeEdgeCount = math.min(_edgeCount, edgeDestinations.Length);
            for (int node = 0; node < count && node + 1 < edgeOffsets.Length; node++)
            {
                int start = math.clamp(edgeOffsets[node], 0, safeEdgeCount);
                int end = math.clamp(edgeOffsets[node + 1], start, safeEdgeCount);
                for (int edge = start; edge < end; edge++)
                {
                    int dst = edgeDestinations[edge];
                    if ((uint)dst >= (uint)count || node > dst || dst >= centroids.Length)
                        continue;

                    FluidCompartmentDTO source = compartments[node];
                    FluidCompartmentDTO target = compartments[dst];
                    float sourceFill = source.MaxWaterVolume > 0f ? source.CurrentWaterVolume * math.rcp(source.MaxWaterVolume) : 0f;
                    float targetFill = target.MaxWaterVolume > 0f ? target.CurrentWaterVolume * math.rcp(target.MaxWaterVolume) : 0f;
                    if (math.abs(sourceFill - targetFill) <= 0.01f)
                        continue;

                    Vector3 a = localToWorld.MultiplyPoint3x4(centroids[node]);
                    Vector3 b = localToWorld.MultiplyPoint3x4(centroids[dst]);
                    Gizmos.DrawLine(sourceFill >= targetFill ? a : b, sourceFill >= targetFill ? b : a);
                }
            }
        }
    }
}
