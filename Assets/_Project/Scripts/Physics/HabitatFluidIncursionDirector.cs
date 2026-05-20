using System;
using System.IO;
using System.Diagnostics;
using Hecton8.Core;
using Hecton8.Core.Contracts;
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
    public sealed unsafe class HabitatFluidIncursionDirector : MonoBehaviour, IFixedTickable, IPostFixedTickable, IRenderable
    {
        private const SystemID OwnerSystem = SystemID.Fluid;
        private const string DumpPath = "Docs/AgentLogs/Dump_FLUID_INCURSION.bin";
        private const uint FloodMuffleLaneHash = 0x464C4D46u; // FLMF
        private const int FloodMuffleSignalCapacity = 32;
        private static readonly int s_WaterlineBufferId = Shader.PropertyToID("_H8HabitatFluidWaterlines");
        private static readonly int s_WaterlineCountId = Shader.PropertyToID("_H8HabitatFluidWaterlineCount");
        private static readonly int s_GlobalFloodScalarId = Shader.PropertyToID("_H8HabitatFloodScalar");
        private static bool s_FloodMuffleLaneInitialized;

        [SerializeField, Range(1, HabitatFluidIncursionConstants.MaxCompartments)] private int compartmentCount = 16;
        [SerializeField, Min(1f)] private float defaultCompartmentVolumeM3 = HabitatFluidIncursionConstants.DefaultCompartmentVolumeM3;
        [SerializeField] private float defaultFloorHeightLocal = HabitatFluidIncursionConstants.DefaultFloorHeightLocal;
        [SerializeField, Min(0f)] private float externalWaterlineRuntimeY = 0f;
        [SerializeField, Min(1f)] private float baseMassKg = 18000f;
        [SerializeField, Min(0.0001f)] private float mockBreachAreaM2 = 0.08f;
        [SerializeField, Min(0f)] private int mockBreachIndex = 0;
        [SerializeField] private bool seedMockBreachOnEnable = true;
        [SerializeField] private bool uploadShaderWaterlines = true;
        [SerializeField] private bool drawHeatmapGizmos = true;

        private IDataVault _vault;
        private VaultBufferHandle<FluidCompartmentDTO> _frontHandle;
        private VaultBufferHandle<FluidCompartmentDTO> _backHandle;
        private VaultBufferHandle<IntegrityStateDTO> _integrityHandle;
        private VaultBufferHandle<int> _edgeOffsetsHandle;
        private VaultBufferHandle<int> _edgeDestinationsHandle;
        private VaultBufferHandle<byte> _edgeFlagsHandle;
        private VaultBufferHandle<float3> _centroidsHandle;
        private VaultBufferHandle<FluidWaterlineShaderDTO> _waterlineHandle;
        private VaultBufferHandle<FluidMassStateDTO> _massStateHandle;
        private VaultBufferHandle<FluidIncursionTuningDTO> _tuningHandle;
        private VaultBufferHandle<FluidIncursionTelemetryEntry> _telemetryHandle;
        private VaultBufferHandle<FluidCompartmentTelemetryDTO> _compartmentTelemetryHandle;
        private VaultBufferHandle<int> _telemetryCursorHandle;
        private VaultBufferHandle<int> _bfsQueueHandle;
        private VaultBufferHandle<byte> _bfsVisitedHandle;
        private VaultBufferHandle<float> _deltaVolumesHandle;
        private VaultBufferHandle<FluidIncursionFrameSummaryDTO> _summaryHandle;

        private Transform _cachedTransform;
        private GraphicsBuffer _waterlineBufferA;
        private GraphicsBuffer _waterlineBufferB;
        private JobHandle _simulationHandle;
        private uint _sourceBodyId;
        private int _edgeCount;
        private int _waterlineBufferCapacity;
        private int _waterlineWriteBufferIndex;
        private int _lockedBufferMask;
        private int _frame;
        private long _simulationScheduleTimestamp;
        private float _massPublishAccumulator;
        private float _simulationAccumulator;
        private bool _hasScheduled;
        private bool _frontIsA = true;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredRenderable;
        private bool _buffersReady;
        private bool _dumpWritten;
        private bool _waterlineUploadDirty;

        private void OnEnable()
        {
            _cachedTransform = transform;
            _sourceBodyId = unchecked((uint)math.abs(GetInstanceID()));
            _buffersReady = TryResolveAndInitializeBuffers();
            _waterlineUploadDirty = true;
            GlobalSignals.InitializeAllQueues();
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            EnsureFloodMuffleSignalLane();
            PhysicsEventBus.EnsureReady();

            _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Environment);
            _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Environment);
            _registeredRenderable = GlobalRegistry.Renderables.TryRegister(this);
        }

        private void OnDisable()
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            UnlockJobBuffers();

            if (_registeredFixed)
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Environment);
            if (_registeredPostFixed)
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Environment);
            if (_registeredRenderable)
                GlobalRegistry.Renderables.TryUnregister(this);

            ReleaseGraphicsBuffer(ref _waterlineBufferA);
            ReleaseGraphicsBuffer(ref _waterlineBufferB);
            _registeredFixed = false;
            _registeredPostFixed = false;
            _registeredRenderable = false;
            _buffersReady = false;
            _cachedTransform = null;
        }

        private void OnValidate()
        {
            compartmentCount = math.clamp(compartmentCount, 1, HabitatFluidIncursionConstants.MaxCompartments);
            defaultCompartmentVolumeM3 = math.max(1f, defaultCompartmentVolumeM3);
            mockBreachIndex = math.max(0, mockBreachIndex);
            mockBreachAreaM2 = math.max(0.0001f, mockBreachAreaM2);
            baseMassKg = math.max(1f, baseMassKg);
        }

        /// <summary>Schedules the deterministic scalar flood solver when quality-scaled cadence reaches its window.</summary>
        public void FixedTick(float fixedDeltaTime)
        {
            if (fixedDeltaTime <= 0f || _hasScheduled)
                return;

            _simulationAccumulator = math.min(0.25f, _simulationAccumulator + fixedDeltaTime);
            float solverQuality = ResolveGlobalQualityWeight();
            float cadenceCurve = solverQuality * solverQuality;
            float targetSolverHz = math.lerp(5f, 50f, cadenceCurve);
            float solverWindowSeconds = math.rcp(math.max(5f, targetSolverHz));
            if (_simulationAccumulator + 0.00001f < solverWindowSeconds)
                return;
            float solverDeltaTime = _simulationAccumulator;

            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return;

            NativeArray<FluidCompartmentDTO> read = ResolveActiveCompartments();
            NativeArray<FluidCompartmentDTO> write = ResolveInactiveCompartments();
            NativeArray<IntegrityStateDTO> integrity = _integrityHandle.Resolve(_vault);
            NativeArray<int> edgeOffsets = _edgeOffsetsHandle.Resolve(_vault);
            NativeArray<int> edgeDestinations = _edgeDestinationsHandle.Resolve(_vault);
            NativeArray<byte> edgeFlags = _edgeFlagsHandle.Resolve(_vault);
            NativeArray<float3> centroids = _centroidsHandle.Resolve(_vault);
            NativeArray<FluidWaterlineShaderDTO> waterlines = _waterlineHandle.Resolve(_vault);
            NativeArray<FluidMassStateDTO> massState = _massStateHandle.Resolve(_vault);
            NativeArray<FluidIncursionTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            NativeArray<FluidIncursionTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
            NativeArray<FluidCompartmentTelemetryDTO> compartmentTelemetry = _compartmentTelemetryHandle.Resolve(_vault);
            NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(_vault);
            NativeArray<int> bfsQueue = _bfsQueueHandle.Resolve(_vault);
            NativeArray<byte> bfsVisited = _bfsVisitedHandle.Resolve(_vault);
            NativeArray<float> deltaVolumes = _deltaVolumesHandle.Resolve(_vault);
            NativeArray<FluidIncursionFrameSummaryDTO> summary = _summaryHandle.Resolve(_vault);

            if (!read.IsCreated || !write.IsCreated || !integrity.IsCreated || !edgeOffsets.IsCreated ||
                !edgeDestinations.IsCreated || !edgeFlags.IsCreated || !centroids.IsCreated ||
                !waterlines.IsCreated || !massState.IsCreated || !tuningArray.IsCreated ||
                !telemetry.IsCreated || !compartmentTelemetry.IsCreated || !telemetryCursor.IsCreated || !bfsQueue.IsCreated ||
                !bfsVisited.IsCreated || !deltaVolumes.IsCreated || !summary.IsCreated)
            {
                return;
            }

            if (!TryLockJobBuffers())
                return;
            _simulationAccumulator = 0f;

            int safeCompartmentCount = math.min(compartmentCount, read.Length);
            safeCompartmentCount = math.min(safeCompartmentCount, write.Length);
            FluidIncursionTuningDTO tuning = RefreshTuning(tuningArray, solverDeltaTime, safeCompartmentCount);
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
                CompartmentCount = safeCompartmentCount,
                DeltaTime = solverDeltaTime,
                GlobalQualityWeight = tuning.GlobalQualityWeight,
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
                BfsQueue = bfsQueue,
                BfsVisited = bfsVisited,
                DeltaVolumes = deltaVolumes,
                CompartmentCount = safeCompartmentCount,
                EdgeCount = math.min(_edgeCount, edgeDestinations.Length),
                SolverIterations = tuning.SolverIterations,
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
            _hasScheduled = true;
            _frame++;
        }

        /// <summary>Completes the scheduled flood job chain, swaps buffers, and publishes deferred scalar bridges.</summary>
        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_hasScheduled)
                return;

            if (!TryFinalizeScheduledSimulation())
                return;

            uint solverWallMicroseconds = ResolveElapsedMicroseconds(_simulationScheduleTimestamp);
            UnlockJobBuffers();
            _frontIsA = !_frontIsA;
            _waterlineUploadDirty = true;

            NativeArray<FluidIncursionFrameSummaryDTO> summaryArray = _summaryHandle.Resolve(_vault);
            if (!summaryArray.IsCreated || summaryArray.Length <= 0)
                return;

            StampSolverWallTime(solverWallMicroseconds);
            FluidIncursionFrameSummaryDTO summary = summaryArray[0];
            if (summary.InvalidCount > 0 || (summary.Flags & 1u) != 0u)
                DumpBlackBoxOnce();

            _massPublishAccumulator += fixedDeltaTime;
            NativeArray<FluidIncursionTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
            float publishInterval = tuningArray.IsCreated && tuningArray.Length > 0
                ? math.max(0.02f, tuningArray[0].MassPublishIntervalSeconds)
                : HabitatFluidIncursionConstants.DefaultMassPublishIntervalSeconds;
            if (_massPublishAccumulator >= publishInterval)
            {
                _massPublishAccumulator = 0f;
                PublishMassAndAcousticSignals(in summary);
            }

            Shader.SetGlobalFloat(s_GlobalFloodScalarId, summary.AcousticFloodIntensity01);
        }

        /// <summary>Uploads dirty waterline scalar DTOs to the double-buffered global shader buffer.</summary>
        public void Render(float deltaTime)
        {
            if (!uploadShaderWaterlines || !_buffersReady || _hasScheduled || !_waterlineUploadDirty)
                return;

            NativeArray<FluidWaterlineShaderDTO> waterlines = _waterlineHandle.Resolve(_vault);
            if (!waterlines.IsCreated)
                return;

            int safeCount = math.min(compartmentCount, waterlines.Length);
            if (safeCount <= 0 || !EnsureWaterlineGraphicsBuffers(safeCount))
                return;

            GraphicsBuffer targetBuffer = ResolveNextWaterlineWriteBuffer();
            if (targetBuffer == null || !targetBuffer.IsValid())
                return;

            NativeArray<FluidWaterlineShaderDTO> mapped = targetBuffer.LockBufferForWrite<FluidWaterlineShaderDTO>(0, safeCount);
            unsafe
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(waterlines);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<FluidWaterlineShaderDTO>());
            }
            targetBuffer.UnlockBufferAfterWrite<FluidWaterlineShaderDTO>(safeCount);
            Shader.SetGlobalBuffer(s_WaterlineBufferId, targetBuffer);
            Shader.SetGlobalInt(s_WaterlineCountId, safeCount);
            _waterlineUploadDirty = false;
        }

        /// <summary>Returns the currently readable flood compartment buffer for editor/debug consumers.</summary>
        public bool TryGetActiveCompartmentSnapshot(out NativeArray<FluidCompartmentDTO> compartments, out int count)
        {
            if (!_buffersReady || _vault == null || _hasScheduled)
            {
                compartments = default;
                count = 0;
                return false;
            }

            compartments = ResolveActiveCompartments();
            count = compartments.IsCreated ? math.min(compartmentCount, compartments.Length) : 0;
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
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return false;

            NativeArray<int> targetOffsets = _edgeOffsetsHandle.Resolve(_vault);
            NativeArray<int> targetDestinations = _edgeDestinationsHandle.Resolve(_vault);
            NativeArray<byte> targetFlags = _edgeFlagsHandle.Resolve(_vault);
            if (!edgeOffsets.IsCreated || !edgeDestinations.IsCreated || !edgeFlags.IsCreated ||
                !targetOffsets.IsCreated || !targetDestinations.IsCreated || !targetFlags.IsCreated)
            {
                return false;
            }

            int safeNodeCount = math.min(math.max(0, nodeCount), math.min(compartmentCount, targetOffsets.Length - 1));
            int safeEdgeCount = math.min(math.max(0, graphEdgeCount), math.min(edgeDestinations.Length, targetDestinations.Length));
            for (int i = 0; i <= safeNodeCount; i++)
                targetOffsets[i] = edgeOffsets[i];
            for (int i = 0; i < safeEdgeCount; i++)
            {
                targetDestinations[i] = edgeDestinations[i];
                targetFlags[i] = edgeFlags[i];
            }

            _edgeCount = safeEdgeCount;
            return true;
        }

        /// <summary>Applies cold CSV compartment capacities to both solver buffers without managed string splitting.</summary>
        public int ApplyCompartmentVolumeCsv(NativeArray<byte> csvBytes, int byteCount)
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return 0;

            NativeArray<FluidCompartmentDTO> active = ResolveActiveCompartments();
            NativeArray<FluidCompartmentDTO> inactive = ResolveInactiveCompartments();
            if (!active.IsCreated || !inactive.IsCreated)
                return 0;

            int applied = HabitatFluidIncursionCsv.ParseCompartmentVolumesCsv(csvBytes, byteCount, active, compartmentCount);
            HabitatFluidIncursionCsv.ParseCompartmentVolumesCsv(csvBytes, byteCount, inactive, compartmentCount);
            return applied;
        }

        /// <summary>Injects a cold/profiling mock breach into both solver buffers and shared integrity state.</summary>
        public bool GenerateMockHullBreach(int breachIndex, float breachAreaM2, float ingressRateM3PerSecond)
        {
            CompleteScheduledSimulationForAuthoritativeWrite();
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return false;

            NativeArray<FluidCompartmentDTO> front = _frontHandle.Resolve(_vault);
            NativeArray<FluidCompartmentDTO> back = _backHandle.Resolve(_vault);
            NativeArray<IntegrityStateDTO> integrity = _integrityHandle.Resolve(_vault);
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
            // COLD SYNC JOB: explicit damage-control/profiling breach injection outside fixed solver cadence.
            JobHandle breachFrontHandle = breachFront.Schedule();
            DispatcherJobFence.TryComplete(ref breachFrontHandle, forceComplete: true);

            MockHullBreachJob breachBack = breachFront;
            breachBack.Compartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(back);
            // COLD SYNC JOB: mirror seed preserves deterministic double-buffer state.
            JobHandle breachBackHandle = breachBack.Schedule();
            DispatcherJobFence.TryComplete(ref breachBackHandle, forceComplete: true);
            _waterlineUploadDirty = true;
            return true;
        }

        private bool TryResolveAndInitializeBuffers()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (_vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                _vault = latestVault;

            if (_vault == null)
                return false;

            int safeCount = math.clamp(compartmentCount, 1, HabitatFluidIncursionConstants.MaxCompartments);
            int edgeCapacity = HabitatFluidIncursionConstants.MaxEdges;
            _frontHandle = _vault.GetBufferHandle<FluidCompartmentDTO>(BufferID.ShinobuFluidCompartmentFront, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _backHandle = _vault.GetBufferHandle<FluidCompartmentDTO>(BufferID.ShinobuFluidCompartmentBack, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _integrityHandle = _vault.GetBufferHandle<IntegrityStateDTO>(BufferID.ShinobuFluidIntegrityState, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _edgeOffsetsHandle = _vault.GetBufferHandle<int>(BufferID.ShinobuFluidEdgeOffsets, safeCount + 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _edgeDestinationsHandle = _vault.GetBufferHandle<int>(BufferID.ShinobuFluidEdgeDestinations, edgeCapacity, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _edgeFlagsHandle = _vault.GetBufferHandle<byte>(BufferID.ShinobuFluidEdgeFlags, edgeCapacity, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _centroidsHandle = _vault.GetBufferHandle<float3>(BufferID.ShinobuFluidCompartmentCentroids, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _waterlineHandle = _vault.GetBufferHandle<FluidWaterlineShaderDTO>(BufferID.ShinobuFluidWaterlineShader, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _massStateHandle = _vault.GetBufferHandle<FluidMassStateDTO>(BufferID.ShinobuFluidMassState, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _tuningHandle = _vault.GetBufferHandle<FluidIncursionTuningDTO>(BufferID.ShinobuFluidTuning, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _vault.GetBufferHandle<FluidIncursionTelemetryEntry>(BufferID.ShinobuFluidTelemetryRing, HabitatFluidIncursionConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _compartmentTelemetryHandle = _vault.GetBufferHandle<FluidCompartmentTelemetryDTO>(BufferID.ShinobuFluidCompartmentTelemetry, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = _vault.GetBufferHandle<int>(BufferID.ShinobuFluidTelemetryCursor, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _bfsQueueHandle = _vault.GetBufferHandle<int>(BufferID.ShinobuFluidBfsQueue, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _bfsVisitedHandle = _vault.GetBufferHandle<byte>(BufferID.ShinobuFluidBfsVisited, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _deltaVolumesHandle = _vault.GetBufferHandle<float>(BufferID.ShinobuFluidDeltaVolumes, safeCount, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _summaryHandle = _vault.GetBufferHandle<FluidIncursionFrameSummaryDTO>(BufferID.ShinobuFluidFrameSummary, 1, OwnerSystem, NativeArrayOptions.UninitializedMemory);

            if (!FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout())
                return false;

            InitializeColdBootBuffers(safeCount);
            compartmentCount = safeCount;
            _buffersReady = true;
            return true;
        }

        private void InitializeColdBootBuffers(int safeCount)
        {
            NativeArray<FluidCompartmentDTO> front = _frontHandle.Resolve(_vault);
            NativeArray<FluidCompartmentDTO> back = _backHandle.Resolve(_vault);
            NativeArray<IntegrityStateDTO> integrity = _integrityHandle.Resolve(_vault);
            NativeArray<float3> centroids = _centroidsHandle.Resolve(_vault);
            NativeArray<FluidWaterlineShaderDTO> waterlines = _waterlineHandle.Resolve(_vault);
            if (!front.IsCreated || !back.IsCreated || !integrity.IsCreated || !centroids.IsCreated || !waterlines.IsCreated)
                return;

            AbsoluteUniversePositionBlit origin = ResolveColdBootOriginAup();
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
            // COLD SYNC JOB: boot-only explicit initialization of uninitialized Vault memory.
            JobHandle clearFrontHandle = clearFront.Schedule(safeCount, 32);
            DispatcherJobFence.TryComplete(ref clearFrontHandle, forceComplete: true);

            FluidCompartmentClearJob clearBack = clearFront;
            clearBack.Compartments = backPtr;
            // COLD SYNC JOB: boot-only mirror clear for deterministic double-buffer start state.
            JobHandle clearBackHandle = clearBack.Schedule(safeCount, 32);
            DispatcherJobFence.TryComplete(ref clearBackHandle, forceComplete: true);

            BuildDefaultLineTopology(safeCount);
            InitializeTuningBuffer(safeCount);

            NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(_vault);
            NativeArray<FluidCompartmentTelemetryDTO> compartmentTelemetry = _compartmentTelemetryHandle.Resolve(_vault);
            NativeArray<FluidIncursionFrameSummaryDTO> summary = _summaryHandle.Resolve(_vault);
            NativeArray<FluidMassStateDTO> massState = _massStateHandle.Resolve(_vault);
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
                // COLD SYNC JOB: isolated profile breach seed before runtime ticks start.
                JobHandle breachFrontHandle = breachFront.Schedule();
                DispatcherJobFence.TryComplete(ref breachFrontHandle, forceComplete: true);
                MockHullBreachJob breachBack = breachFront;
                breachBack.Compartments = backPtr;
                // COLD SYNC JOB: mirror seed keeps both buffers identical at frame zero.
                JobHandle breachBackHandle = breachBack.Schedule();
                DispatcherJobFence.TryComplete(ref breachBackHandle, forceComplete: true);
            }
        }

        private AbsoluteUniversePositionBlit ResolveColdBootOriginAup()
        {
            Vector3 runtimePosition = _cachedTransform != null ? _cachedTransform.position : Vector3.zero;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                runtimePosition = Vector3.zero;
            }

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition resolvedAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePositionBlit.FromAup(in resolvedAup);
        }

        private void BuildDefaultLineTopology(int safeCount)
        {
            NativeArray<int> edgeOffsets = _edgeOffsetsHandle.Resolve(_vault);
            NativeArray<int> edgeDestinations = _edgeDestinationsHandle.Resolve(_vault);
            NativeArray<byte> edgeFlags = _edgeFlagsHandle.Resolve(_vault);
            if (!edgeOffsets.IsCreated || !edgeDestinations.IsCreated || !edgeFlags.IsCreated)
                return;

            int edgeCursor = 0;
            for (int node = 0; node < safeCount; node++)
            {
                edgeOffsets[node] = edgeCursor;
                if (node > 0 && edgeCursor < edgeDestinations.Length)
                {
                    edgeDestinations[edgeCursor] = node - 1;
                    edgeFlags[edgeCursor] = 0;
                    edgeCursor++;
                }
                if (node + 1 < safeCount && edgeCursor < edgeDestinations.Length)
                {
                    edgeDestinations[edgeCursor] = node + 1;
                    edgeFlags[edgeCursor] = 0;
                    edgeCursor++;
                }
            }

            edgeOffsets[safeCount] = edgeCursor;
            for (int i = edgeCursor; i < edgeDestinations.Length; i++)
            {
                edgeDestinations[i] = 0;
                edgeFlags[i] = FluidEdgeFlags.Sealed;
            }

            _edgeCount = edgeCursor;
        }

        private void InitializeTuningBuffer(int safeCount)
        {
            NativeArray<FluidIncursionTuningDTO> tuningArray = _tuningHandle.Resolve(_vault);
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
                SolverIterations = ResolveSolverIterations(quality),
                CompartmentCount = (ushort)math.min(ushort.MaxValue, safeCount),
                EdgeCount = (ushort)math.min(ushort.MaxValue, _edgeCount)
            };
        }

        private FluidIncursionTuningDTO RefreshTuning(NativeArray<FluidIncursionTuningDTO> tuningArray, float deltaTime, int safeCount)
        {
            FluidIncursionTuningDTO tuning = tuningArray[0];
            float quality = ResolveGlobalQualityWeight();
            tuning.GlobalQualityWeight = quality;
            tuning.SolverIterations = ResolveSolverIterations(quality);
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

        private void PublishMassAndAcousticSignals(in FluidIncursionFrameSummaryDTO summary)
        {
            NativeArray<FluidMassStateDTO> massStateArray = _massStateHandle.Resolve(_vault);
            if (!massStateArray.IsCreated || massStateArray.Length <= 0)
                return;

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
            GlobalSignals.Publish(in floodState);

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
            PhysicsEventBus.NotifyFloodMassShift(in massEvent);

            PublishAcousticMuffle(in summary);
        }

        private void PublishAcousticMuffle(in FluidIncursionFrameSummaryDTO summary)
        {
            float intensity = math.saturate(summary.AcousticFloodIntensity01);
            float cutoffHz = math.lerp(9000f, HabitatFluidIncursionConstants.DefaultLowPassCutoffHz, intensity);
            float transmission01 = math.saturate(math.lerp(1f, 0.22f, intensity));
            AbsoluteUniversePositionBlit sourceAup = ResolveSourceAupBlit();
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
            SignalBus<HabitatFloodAcousticMuffleSignal>.Push(in signal);
        }

        private AbsoluteUniversePositionBlit ResolveSourceAupBlit()
        {
            NativeArray<IntegrityStateDTO> integrity = _integrityHandle.Resolve(_vault);
            if (integrity.IsCreated && integrity.Length > 0)
                return integrity[0].CenterAup;

            return default;
        }

        private NativeArray<FluidCompartmentDTO> ResolveActiveCompartments()
        {
            return _frontIsA ? _frontHandle.Resolve(_vault) : _backHandle.Resolve(_vault);
        }

        private NativeArray<FluidCompartmentDTO> ResolveInactiveCompartments()
        {
            return _frontIsA ? _backHandle.Resolve(_vault) : _frontHandle.Resolve(_vault);
        }

        private void CompleteScheduledSimulationForAuthoritativeWrite()
        {
            if (!_hasScheduled)
                return;

            // COLD SYNC JOB: topology/CSV/mock author writes must not race a pending flood worker.
            if (!DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true))
                return;

            _hasScheduled = false;
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

        private bool TryLockJobBuffers()
        {
            _lockedBufferMask = 0;
            return TryLock(BufferID.ShinobuFluidCompartmentFront, 0) &&
                   TryLock(BufferID.ShinobuFluidCompartmentBack, 1) &&
                   TryLock(BufferID.ShinobuFluidIntegrityState, 2) &&
                   TryLock(BufferID.ShinobuFluidEdgeOffsets, 3) &&
                   TryLock(BufferID.ShinobuFluidEdgeDestinations, 4) &&
                   TryLock(BufferID.ShinobuFluidEdgeFlags, 5) &&
                   TryLock(BufferID.ShinobuFluidCompartmentCentroids, 6) &&
                   TryLock(BufferID.ShinobuFluidWaterlineShader, 7) &&
                   TryLock(BufferID.ShinobuFluidMassState, 8) &&
                   TryLock(BufferID.ShinobuFluidTuning, 9) &&
                   TryLock(BufferID.ShinobuFluidTelemetryRing, 10) &&
                   TryLock(BufferID.ShinobuFluidCompartmentTelemetry, 11) &&
                   TryLock(BufferID.ShinobuFluidTelemetryCursor, 12) &&
                   TryLock(BufferID.ShinobuFluidBfsQueue, 13) &&
                   TryLock(BufferID.ShinobuFluidBfsVisited, 14) &&
                   TryLock(BufferID.ShinobuFluidDeltaVolumes, 15) &&
                   TryLock(BufferID.ShinobuFluidFrameSummary, 16);
        }

        private bool TryLock(BufferID bufferId, int bit)
        {
            if (_vault.TryLockBuffer(bufferId, OwnerSystem))
            {
                _lockedBufferMask |= 1 << bit;
                return true;
            }

            UnlockJobBuffers();
            return false;
        }

        private void UnlockJobBuffers()
        {
            UnlockIf(BufferID.ShinobuFluidFrameSummary, 16);
            UnlockIf(BufferID.ShinobuFluidDeltaVolumes, 15);
            UnlockIf(BufferID.ShinobuFluidBfsVisited, 14);
            UnlockIf(BufferID.ShinobuFluidBfsQueue, 13);
            UnlockIf(BufferID.ShinobuFluidTelemetryCursor, 12);
            UnlockIf(BufferID.ShinobuFluidCompartmentTelemetry, 11);
            UnlockIf(BufferID.ShinobuFluidTelemetryRing, 10);
            UnlockIf(BufferID.ShinobuFluidTuning, 9);
            UnlockIf(BufferID.ShinobuFluidMassState, 8);
            UnlockIf(BufferID.ShinobuFluidWaterlineShader, 7);
            UnlockIf(BufferID.ShinobuFluidCompartmentCentroids, 6);
            UnlockIf(BufferID.ShinobuFluidEdgeFlags, 5);
            UnlockIf(BufferID.ShinobuFluidEdgeDestinations, 4);
            UnlockIf(BufferID.ShinobuFluidEdgeOffsets, 3);
            UnlockIf(BufferID.ShinobuFluidIntegrityState, 2);
            UnlockIf(BufferID.ShinobuFluidCompartmentBack, 1);
            UnlockIf(BufferID.ShinobuFluidCompartmentFront, 0);
            _lockedBufferMask = 0;
        }

        private void UnlockIf(BufferID bufferId, int bit)
        {
            if ((_lockedBufferMask & (1 << bit)) != 0)
                _vault?.TryUnlockBuffer(bufferId, OwnerSystem);
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

        private GraphicsBuffer ResolveNextWaterlineWriteBuffer()
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

        private AbsoluteUniversePositionBlit ResolveExternalWaterlineAup()
        {
            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            AbsoluteUniversePosition waterlineAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(0d, externalWaterlineRuntimeY, 0d));
            return AbsoluteUniversePositionBlit.FromAup(in waterlineAup);
        }

        private static ushort ResolveSolverIterations(float quality)
        {
            return (ushort)math.clamp((int)math.round(math.lerp(
                HabitatFluidIncursionConstants.MinSolverIterations,
                HabitatFluidIncursionConstants.MaxSolverIterations,
                math.saturate(quality))), HabitatFluidIncursionConstants.MinSolverIterations, HabitatFluidIncursionConstants.MaxSolverIterations);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
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
            NativeArray<FluidIncursionFrameSummaryDTO> summaryArray = _summaryHandle.Resolve(_vault);
            if (summaryArray.IsCreated && summaryArray.Length > 0)
            {
                FluidIncursionFrameSummaryDTO summary = summaryArray[0];
                summary.SolverWallMicroseconds = solverWallMicroseconds;
                summaryArray[0] = summary;
            }

            NativeArray<int> telemetryCursor = _telemetryCursorHandle.Resolve(_vault);
            NativeArray<FluidIncursionTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
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
                FloodMuffleSignalCapacity,
                maxFrameSignals: FloodMuffleSignalCapacity,
                lowTierFrameSignals: 8,
                laneHash: FloodMuffleLaneHash);
            SignalBus<HabitatFloodAcousticMuffleSignal>.EnsureInitialized();
            s_FloodMuffleLaneInitialized = true;
        }

        private void DumpBlackBoxOnce()
        {
            if (_dumpWritten)
                return;

            NativeArray<FluidIncursionTelemetryEntry> telemetry = _telemetryHandle.Resolve(_vault);
            if (!telemetry.IsCreated)
                return;

            _dumpWritten = true;
            Directory.CreateDirectory("Docs/AgentLogs");
            int bytes = math.min(telemetry.Length, HabitatFluidIncursionConstants.TelemetryFrameCount) *
                        UnsafeUtility.SizeOf<FluidIncursionTelemetryEntry>();
            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            using (FileStream stream = new FileStream(DumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough))
            {
                stream.Write(new ReadOnlySpan<byte>(source, bytes));
            }
        }

        private void OnDrawGizmos()
        {
            if (!drawHeatmapGizmos || !TryGetActiveCompartmentSnapshot(out NativeArray<FluidCompartmentDTO> compartments, out int count))
                return;

            NativeArray<float3> centroids = _centroidsHandle.Resolve(_vault);
            NativeArray<int> edgeOffsets = _edgeOffsetsHandle.Resolve(_vault);
            NativeArray<int> edgeDestinations = _edgeDestinationsHandle.Resolve(_vault);
            if (!centroids.IsCreated)
                return;

            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            for (int i = 0; i < count && i < centroids.Length; i++)
            {
                FluidCompartmentDTO dto = compartments[i];
                float fill = dto.MaxVolume > HabitatFluidIncursionConstants.WaterEpsilonM3
                    ? math.saturate(dto.CurrentWaterVolume * math.rcp(dto.MaxVolume))
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
                    float sourceFill = source.MaxVolume > 0f ? source.CurrentWaterVolume * math.rcp(source.MaxVolume) : 0f;
                    float targetFill = target.MaxVolume > 0f ? target.CurrentWaterVolume * math.rcp(target.MaxVolume) : 0f;
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
