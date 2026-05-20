using System;
using System.Diagnostics;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Construction
{
    /// <summary>
    /// Vault-backed CSR/Jacobi sump-pump drainage runtime for flooded rooms and pipe visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Sump Pump Pipe Grid Runtime")]
    public sealed unsafe class SumpPumpPipeGridRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IServiceShutdown
    {
        private const SystemID OwnerSystem = SystemID.Construction;
        private const float SlowTickStepSeconds = 0.1f;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_222.bin";
        private const uint RuntimeHash = 0x50323232u;

        private static readonly int s_DrainagePipeEdgeFlowId = Shader.PropertyToID("_H8DrainagePipeEdgeFlow");
        private static readonly int s_DrainagePipeEdgeCountId = Shader.PropertyToID("_H8DrainagePipeEdgeCount");
        private static SumpPumpPipeGridRuntime s_active;
        private static DrainageTuningDTO s_offlineTuning = DefaultTuning();

        [Header("Graph")]
        [Tooltip("Maximum Vault pump/pipe node count. Mock topology defaults to 1000.")]
        [SerializeField, Range(16, SumpPumpPipeGridConstants.MaxPumpNodes)] private int nodeCapacity = SumpPumpPipeGridConstants.MaxPumpNodes;

        [Tooltip("Maximum flat directed pipe edge count. Mock topology defaults to 2500.")]
        [SerializeField, Range(16, SumpPumpPipeGridConstants.MaxPipeEdges)] private int edgeCapacity = SumpPumpPipeGridConstants.MaxPipeEdges;

        [Tooltip("Builds a deterministic 1000-node / 2500-edge drainage graph on enable when Vault buffers are empty.")]
        [SerializeField] private bool generateMockOnEnable = true;

        [Header("Visual Sync")]
        [Tooltip("Uploads edge flow scalars into a global structured buffer for shader panning.")]
        [SerializeField] private bool uploadVisualFlowBuffer = true;

        [Tooltip("Publishes aggregate node flow to the existing connection spline renderer.")]
        [SerializeField] private bool publishConnectionSplineFlow = true;

        [Header("Diagnostics")]
        [Tooltip("Latest solved active pump count.")]
        [SerializeField] private int _debugActivePumps;

        [Tooltip("Latest frame evacuation in cubic meters.")]
        [SerializeField] private float _debugFrameEvacuatedM3;

        [Tooltip("Latest average pipe pressure scalar.")]
        [SerializeField] private float _debugAveragePressure;

        private IDataVault _vault;
        private VaultGenerationHandle<PumpNodeDTO> _pumpNodesHandle;
        private VaultGenerationHandle<PipeEdgeDTO> _pipeEdgesHandle;
        private VaultGenerationHandle<double3> _nodeAupHandle;
        private VaultGenerationHandle<int> _pumpRoomIndicesHandle;
        private VaultGenerationHandle<int> _csrOffsetsHandle;
        private VaultGenerationHandle<int> _csrDestinationsHandle;
        private VaultGenerationHandle<float> _csrConductanceHandle;
        private VaultGenerationHandle<float> _csrFlowHandle;
        private VaultGenerationHandle<int> _csrFlatEdgeIndexHandle;
        private VaultGenerationHandle<int> _csrWriteCursorHandle;
        private VaultGenerationHandle<float> _pressureFrontHandle;
        private VaultGenerationHandle<float> _pressureBackHandle;
        private VaultGenerationHandle<float> _powerPotentialHandle;
        private VaultGenerationHandle<float> _pumpRemainderHandle;
        private VaultGenerationHandle<float> _pumpMassErrorHandle;
        private VaultGenerationHandle<DrainageRoomDrainLock64> _roomDrainLocksHandle;
        private VaultGenerationHandle<DrainageTuningDTO> _tuningHandle;
        private VaultGenerationHandle<DrainageTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<PipeProfileDTO> _profilesHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private VaultGenerationHandle<DrainageTelemetryEntry> _frameSummaryHandle;
        private VaultGenerationHandle<DrainagePipeFlowGpuDTO> _flowGpuHandle;

        private JobHandle _solverHandle;
        private GraphicsBuffer _flowBufferA;
        private GraphicsBuffer _flowBufferB;
        private ulong _lockedBufferMask;
        private long _solverScheduleTimestamp;
        private uint _frameIndex;
        private int _flowBufferCapacity;
        private int _flowBufferWriteIndex;
        private float _solveAccumulator;
        private bool _solverScheduled;
        private bool _buffersReady;
        private bool _registeredSlowTick;
        private bool _registeredLateFrameTick;
        private bool _pressureFrontIsA = true;
        private bool _topologyDirty = true;
        private bool _flowUploadDirty;
        private bool _blackBoxDumped;

        /// <summary>True when the Vault-backed sump pump runtime is the active drainage authority.</summary>
        public static bool HasActiveRuntime => s_active != null && s_active.isActiveAndEnabled;

        /// <summary>Reads the latest frame summary written by the Burst telemetry job.</summary>
        public static bool TryGetLatestTelemetry(out DrainageTelemetryEntry entry)
        {
            SumpPumpPipeGridRuntime runtime = s_active;
            if (runtime != null && runtime.TryReadLatestTelemetry(out entry))
                return true;

            entry = default;
            return false;
        }

        /// <summary>Reads the active Vault-backed drainage tuning.</summary>
        public static bool TryGetTuning(out DrainageTuningDTO tuning)
        {
            SumpPumpPipeGridRuntime runtime = s_active;
            if (runtime != null && runtime.TryReadTuning(out tuning))
                return true;

            tuning = s_offlineTuning;
            return false;
        }

        /// <summary>Writes the active Vault-backed drainage tuning, or stores an offline fallback before runtime boot.</summary>
        public static void SetTuning(in DrainageTuningDTO tuning)
        {
            SumpPumpPipeGridRuntime runtime = s_active;
            if (runtime != null && runtime.TryWriteTuning(in tuning))
                return;

            s_offlineTuning = SanitizeTuning(tuning);
        }

        private void OnEnable()
        {
            s_active = this;
            _buffersReady = TryResolveAndInitializeBuffers();
            if (_buffersReady && generateMockOnEnable)
                GenerateMockDrainageNetwork();

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            CompleteScheduledSolverForTeardown();
            UnlockJobBuffers();
            if (_buffersReady && TryReadTuning(out DrainageTuningDTO tuning))
                s_offlineTuning = tuning;

            if (_registeredLateFrameTick)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlowTick)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            ReleaseOwnedBuffers();
            ReleaseGraphicsBuffer(ref _flowBufferA);
            ReleaseGraphicsBuffer(ref _flowBufferB);
            if (ReferenceEquals(s_active, this))
                s_active = null;

            _registeredLateFrameTick = false;
            _registeredSlowTick = false;
            _buffersReady = false;
        }

        /// <summary>Dispatcher shutdown bridge.</summary>
        public void OnServiceShutdown()
        {
            OnDisable();
        }

        /// <summary>Quality-scaled slow simulation cadence. No object pump or water-particle state is read here.</summary>
        public void SlowTick()
        {
            if (_solverScheduled)
                return;

            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return;

            float quality = ResolveGlobalQualityWeight();
            _solveAccumulator = math.min(1f, _solveAccumulator + SlowTickStepSeconds);
            float cadence = math.lerp(0.5f, 0.1f, quality * quality);
            if (_solveAccumulator + 0.00001f < cadence)
                return;

            float deltaTime = _solveAccumulator;
            _solveAccumulator = 0f;
            ScheduleDrainageSolve(deltaTime, quality);
        }

        /// <summary>Completes the scheduled chain in the dispatcher visual-sync lane and uploads flow scalars.</summary>
        public void LateFrameTick()
        {
            if (!TryFinalizeScheduledSolverNoWait())
                return;

            UnlockJobBuffers();
            StampSolverWallTime(ResolveElapsedMicroseconds(_solverScheduleTimestamp));
            DrainageTelemetryEntry entry = ReadFrameSummary();
            _debugActivePumps = (int)math.min(int.MaxValue, entry.ActivePumpCount);
            _debugFrameEvacuatedM3 = entry.FrameEvacuatedM3;
            _debugAveragePressure = entry.AveragePressure;

            if ((entry.Flags & SumpDrainageTelemetryFlags.NonFinite) != 0u)
                DumpBlackBoxOnce();

            if (_flowUploadDirty)
            {
                UploadFlowVisuals();
                _flowUploadDirty = false;
            }
        }

        /// <summary>Cold CSV ingestion entry point for pipe_and_pump_specs.csv bytes.</summary>
        public bool TryLoadPipeProfilesFromCsv(ReadOnlySpan<byte> csvBytes, out int profileCount)
        {
            profileCount = 0;
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return false;

            if (!_vault.TryLockBuffer(SumpPumpDrainageBufferIds.PipeProfiles, OwnerSystem))
                return false;

            try
            {
                NativeArray<PipeProfileDTO> profiles = Resolve(in _profilesHandle);
                return SumpPumpPipeGridValidation.TryParsePipeProfilesCsv(csvBytes, profiles, out profileCount);
            }
            finally
            {
                _vault.TryUnlockBuffer(SumpPumpDrainageBufferIds.PipeProfiles, OwnerSystem);
            }
        }

        /// <summary>Rebuilds the deterministic 1000-node / 2500-edge mock drainage topology in Vault buffers.</summary>
        public void GenerateMockDrainageNetwork()
        {
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return;

            CompleteScheduledSolverForTeardown();
            UnlockJobBuffers();
            if (!TryLockJobBuffers())
                return;

            try
            {
                NativeArray<PumpNodeDTO> pumps = Resolve(in _pumpNodesHandle);
                NativeArray<PipeEdgeDTO> edges = Resolve(in _pipeEdgesHandle);
                NativeArray<double3> nodeAup = Resolve(in _nodeAupHandle);
                NativeArray<int> roomIndices = Resolve(in _pumpRoomIndicesHandle);
                NativeArray<float> power = Resolve(in _powerPotentialHandle);
                NativeArray<int> counters = Resolve(in _countersHandle);
                NativeArray<DrainageTuningDTO> tuning = Resolve(in _tuningHandle);
                if (!pumps.IsCreated || !edges.IsCreated || !nodeAup.IsCreated || !roomIndices.IsCreated || !power.IsCreated || !counters.IsCreated || !tuning.IsCreated)
                    return;

                DrainageTuningDTO current = tuning.Length > 0 ? SanitizeTuning(tuning[0]) : DefaultTuning();
                DrainageMockNetworkJob job = new DrainageMockNetworkJob
                {
                    PumpNodes = pumps,
                    PipeEdges = edges,
                    NodeAup = nodeAup,
                    PumpRoomIndices = roomIndices,
                    PowerPotential = power,
                    Counters = counters,
                    Tuning = tuning,
                    RequestedNodeCount = math.min(nodeCapacity, SumpPumpPipeGridConstants.MaxPumpNodes),
                    RequestedEdgeCount = math.min(edgeCapacity, SumpPumpPipeGridConstants.MaxPipeEdges),
                    BaseConductance = current.BasePipeConductance,
                    MaxPumpRate = SumpPumpPipeGridConstants.DefaultMaxPumpRateM3PerSecond,
                    PumpPowerDraw = current.PumpPowerDraw
                };

                job.Run();
                ClearRuntimeScalarBuffers();
                _topologyDirty = true;
                _pressureFrontIsA = true;
            }
            finally
            {
                UnlockJobBuffers();
            }
        }

        private bool TryResolveAndInitializeBuffers()
        {
            _vault = GlobalRegistry.DataVault;
            if (_vault == null ||
                !SumpPumpPipeGridValidation.ValidatePumpNodeLayout() ||
                !SumpPumpPipeGridValidation.ValidatePipeEdgeLayout() ||
                !SumpPumpPipeGridValidation.ValidateRoomDrainLockLayout())
                return false;

            nodeCapacity = math.clamp(nodeCapacity, 16, SumpPumpPipeGridConstants.MaxPumpNodes);
            edgeCapacity = math.clamp(edgeCapacity, 16, SumpPumpPipeGridConstants.MaxPipeEdges);
            int roomLockCapacity = math.max(nodeCapacity, HabitatFluidIncursionConstants.MaxCompartments);
            _pumpNodesHandle = _vault.GetGenerationHandle<PumpNodeDTO>(SumpPumpDrainageBufferIds.PumpNodes, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pipeEdgesHandle = _vault.GetGenerationHandle<PipeEdgeDTO>(SumpPumpDrainageBufferIds.PipeEdges, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _nodeAupHandle = _vault.GetGenerationHandle<double3>(SumpPumpDrainageBufferIds.NodeAup, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpRoomIndicesHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.PumpRoomIndices, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrOffsetsHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrOffsets, nodeCapacity + 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrDestinationsHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrDestinations, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrConductanceHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.CsrConductance, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrFlowHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.CsrFlow, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrFlatEdgeIndexHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrWriteCursorHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrWriteCursor, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pressureFrontHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.PressureFront, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pressureBackHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.PressureBack, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _powerPotentialHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.PowerPotential, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpRemainderHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.PumpRemainder, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpMassErrorHandle = _vault.GetGenerationHandle<float>(SumpPumpDrainageBufferIds.PumpMassError, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _roomDrainLocksHandle = _vault.GetGenerationHandle<DrainageRoomDrainLock64>(SumpPumpDrainageBufferIds.RoomDrainLocks, roomLockCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _tuningHandle = _vault.GetGenerationHandle<DrainageTuningDTO>(SumpPumpDrainageBufferIds.Tuning, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _vault.GetGenerationHandle<DrainageTelemetryEntry>(SumpPumpDrainageBufferIds.TelemetryRing, SumpPumpPipeGridConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.TelemetryCursor, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _countersHandle = _vault.GetGenerationHandle<int>(SumpPumpDrainageBufferIds.Counters, SumpPumpPipeGridConstants.CounterCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _profilesHandle = _vault.GetGenerationHandle<PipeProfileDTO>(SumpPumpDrainageBufferIds.PipeProfiles, SumpPumpPipeGridConstants.MaxPipeProfiles, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = _vault.GetGenerationHandle<byte>(SumpPumpDrainageBufferIds.CsvScratch, SumpPumpPipeGridConstants.CsvScratchBytes, OwnerSystem, NativeArrayOptions.ClearMemory);
            _frameSummaryHandle = _vault.GetGenerationHandle<DrainageTelemetryEntry>(SumpPumpDrainageBufferIds.FrameSummary, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _flowGpuHandle = _vault.GetGenerationHandle<DrainagePipeFlowGpuDTO>(SumpPumpDrainageBufferIds.FlowGpu, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!ValidateOwnedBuffers())
            {
                ReleaseOwnedBuffers();
                return false;
            }

            InitializeTuningIfNeeded();
            return true;
        }

        private NativeArray<T> Resolve<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return _vault != null && _vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        private bool ValidateOwnedBuffers()
        {
            int safeNodes = math.max(1, nodeCapacity);
            int safeEdges = math.max(1, edgeCapacity);
            int safeRoomLocks = math.max(safeNodes, HabitatFluidIncursionConstants.MaxCompartments);
            return HasResolvedBuffer(in _pumpNodesHandle, safeNodes) &&
                   HasResolvedBuffer(in _pipeEdgesHandle, safeEdges) &&
                   HasResolvedBuffer(in _nodeAupHandle, safeNodes) &&
                   HasResolvedBuffer(in _pumpRoomIndicesHandle, safeNodes) &&
                   HasResolvedBuffer(in _csrOffsetsHandle, safeNodes + 1) &&
                   HasResolvedBuffer(in _csrDestinationsHandle, safeEdges) &&
                   HasResolvedBuffer(in _csrConductanceHandle, safeEdges) &&
                   HasResolvedBuffer(in _csrFlowHandle, safeEdges) &&
                   HasResolvedBuffer(in _csrFlatEdgeIndexHandle, safeEdges) &&
                   HasResolvedBuffer(in _csrWriteCursorHandle, safeNodes) &&
                   HasResolvedBuffer(in _pressureFrontHandle, safeNodes) &&
                   HasResolvedBuffer(in _pressureBackHandle, safeNodes) &&
                   HasResolvedBuffer(in _powerPotentialHandle, safeNodes) &&
                   HasResolvedBuffer(in _pumpRemainderHandle, safeNodes) &&
                   HasResolvedBuffer(in _pumpMassErrorHandle, safeNodes) &&
                   HasResolvedBuffer(in _roomDrainLocksHandle, safeRoomLocks) &&
                   HasResolvedBuffer(in _tuningHandle, 1) &&
                   HasResolvedBuffer(in _telemetryHandle, SumpPumpPipeGridConstants.TelemetryFrameCount) &&
                   HasResolvedBuffer(in _telemetryCursorHandle, 1) &&
                   HasResolvedBuffer(in _countersHandle, SumpPumpPipeGridConstants.CounterCount) &&
                   HasResolvedBuffer(in _profilesHandle, SumpPumpPipeGridConstants.MaxPipeProfiles) &&
                   HasResolvedBuffer(in _csvScratchHandle, SumpPumpPipeGridConstants.CsvScratchBytes) &&
                   HasResolvedBuffer(in _frameSummaryHandle, 1) &&
                   HasResolvedBuffer(in _flowGpuHandle, safeEdges);
        }

        private bool HasResolvedBuffer<T>(in VaultGenerationHandle<T> handle, int minLength) where T : struct
        {
            if (_vault == null || handle.BufferID == 0u || minLength <= 0)
                return false;

            return _vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= minLength;
        }

        private void ScheduleDrainageSolve(float deltaTime, float quality)
        {
            if (!TryLockJobBuffers())
                return;

            NativeArray<PumpNodeDTO> pumps = Resolve(in _pumpNodesHandle);
            NativeArray<PipeEdgeDTO> edges = Resolve(in _pipeEdgesHandle);
            NativeArray<double3> nodeAup = Resolve(in _nodeAupHandle);
            NativeArray<int> roomIndices = Resolve(in _pumpRoomIndicesHandle);
            NativeArray<int> csrOffsets = Resolve(in _csrOffsetsHandle);
            NativeArray<int> csrDestinations = Resolve(in _csrDestinationsHandle);
            NativeArray<float> csrConductance = Resolve(in _csrConductanceHandle);
            NativeArray<float> csrFlow = Resolve(in _csrFlowHandle);
            NativeArray<int> csrFlatEdgeIndex = Resolve(in _csrFlatEdgeIndexHandle);
            NativeArray<int> csrWriteCursor = Resolve(in _csrWriteCursorHandle);
            NativeArray<float> pressureFront = Resolve(in _pressureFrontHandle);
            NativeArray<float> pressureBack = Resolve(in _pressureBackHandle);
            NativeArray<float> powerPotential = Resolve(in _powerPotentialHandle);
            NativeArray<float> pumpRemainder = Resolve(in _pumpRemainderHandle);
            NativeArray<float> pumpMassError = Resolve(in _pumpMassErrorHandle);
            NativeArray<DrainageRoomDrainLock64> roomDrainLocks = Resolve(in _roomDrainLocksHandle);
            NativeArray<DrainageTuningDTO> tuning = Resolve(in _tuningHandle);
            NativeArray<DrainageTelemetryEntry> telemetry = Resolve(in _telemetryHandle);
            NativeArray<int> telemetryCursor = Resolve(in _telemetryCursorHandle);
            NativeArray<int> counters = Resolve(in _countersHandle);
            NativeArray<DrainageTelemetryEntry> frameSummary = Resolve(in _frameSummaryHandle);
            NativeArray<DrainagePipeFlowGpuDTO> flowGpu = Resolve(in _flowGpuHandle);
            if (!pumps.IsCreated || !edges.IsCreated || !nodeAup.IsCreated || !roomIndices.IsCreated ||
                !csrOffsets.IsCreated || !csrDestinations.IsCreated || !csrConductance.IsCreated ||
                !csrFlow.IsCreated || !csrFlatEdgeIndex.IsCreated || !csrWriteCursor.IsCreated ||
                !pressureFront.IsCreated || !pressureBack.IsCreated || !powerPotential.IsCreated ||
                !pumpRemainder.IsCreated || !pumpMassError.IsCreated || !roomDrainLocks.IsCreated || !tuning.IsCreated || !telemetry.IsCreated ||
                !telemetryCursor.IsCreated || !counters.IsCreated || !frameSummary.IsCreated || !flowGpu.IsCreated)
            {
                UnlockJobBuffers();
                return;
            }

            int nodeCount = ResolveNodeCount(counters);
            int edgeCount = ResolveEdgeCount(counters);
            uint telemetryFlags = SumpDrainageTelemetryFlags.None;
            bool hasFluidFront = TryResolveLockedExistingBuffer(BufferID.ShinobuFluidCompartmentFront, 20, out NativeArray<FluidCompartmentDTO> fluidFront);
            bool hasFluidBack = TryResolveLockedExistingBuffer(BufferID.ShinobuFluidCompartmentBack, 21, out NativeArray<FluidCompartmentDTO> fluidBack);
            int compartmentCount = hasFluidFront && hasFluidBack ? math.min(fluidFront.Length, fluidBack.Length) : 0;
            if (compartmentCount <= 0)
            {
                telemetryFlags |= SumpDrainageTelemetryFlags.MissingFluidVault;
                UnlockTrackedBuffer(BufferID.ShinobuFluidCompartmentBack, 21);
                UnlockTrackedBuffer(BufferID.ShinobuFluidCompartmentFront, 20);
            }

            telemetryFlags |= HydratePowerPotentialFromVault(powerPotential, nodeCount);
            ResetFrameCounters(counters, nodeCount, edgeCount);
            DrainageTuningDTO activeTuning = RefreshTuning(tuning, deltaTime, quality, nodeCount, edgeCount);
            int iterations = ResolveSolverIterations(quality);
            counters[SumpPumpPipeGridConstants.CounterSolverIterations] = iterations;
            PumpNodeDTO* pumpPtr = (PumpNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(pumps);
            JobHandle dependency = default;

            if (_topologyDirty)
            {
                BuildCsrPipeGraphJob buildJob = new BuildCsrPipeGraphJob
                {
                    PipeEdges = edges,
                    NodeAup = nodeAup,
                    NodeEdgeOffsets = csrOffsets,
                    EdgeDestinations = csrDestinations,
                    EdgeConductance = csrConductance,
                    EdgeCurrentFlow = csrFlow,
                    CsrFlatEdgeIndex = csrFlatEdgeIndex,
                    EdgeWriteCursor = csrWriteCursor,
                    Counters = counters,
                    NodeCount = nodeCount,
                    EdgeCount = edgeCount,
                    BasePipeConductance = activeTuning.BasePipeConductance
                };
                dependency = buildJob.Schedule(dependency);
                _topologyDirty = false;
            }

            NativeArray<float> pressureRead = _pressureFrontIsA ? pressureFront : pressureBack;
            NativeArray<float> pressureWrite = _pressureFrontIsA ? pressureBack : pressureFront;
            bool nextFrontIsA = _pressureFrontIsA;
            for (int i = 0; i < iterations; i++)
            {
                PipePressureSolverJob pressureJob = new PipePressureSolverJob
                {
                    PumpNodes = pumpPtr,
                    NodeEdgeOffsets = csrOffsets,
                    EdgeDestinations = csrDestinations,
                    EdgeConductance = csrConductance,
                    PressureFront = pressureRead,
                    PressureBack = pressureWrite,
                    PowerPotential = powerPotential,
                    NodeCount = nodeCount,
                    JacobiSmoothingFactor = activeTuning.JacobiSmoothingFactor
                };
                dependency = pressureJob.Schedule(nodeCount, 64, dependency);
                NativeArray<float> swap = pressureRead;
                pressureRead = pressureWrite;
                pressureWrite = swap;
                nextFrontIsA = !nextFrontIsA;
            }

            PipeEdgeFlowJob flowJob = new PipeEdgeFlowJob
            {
                PipeEdges = edges,
                NodeEdgeOffsets = csrOffsets,
                EdgeDestinations = csrDestinations,
                CsrFlatEdgeIndex = csrFlatEdgeIndex,
                EdgeConductance = csrConductance,
                Pressure = pressureRead,
                EdgeCurrentFlow = csrFlow,
                FlowGpu = flowGpu,
                NodeCount = nodeCount,
                VisualFlowGain = activeTuning.VisualFlowGain
            };
            dependency = flowJob.Schedule(nodeCount, 64, dependency);

            if (compartmentCount > 0)
            {
                ClearDrainageRoomLocksJob clearLocksJob = new ClearDrainageRoomLocksJob
                {
                    RoomDrainLocks = roomDrainLocks,
                    Count = compartmentCount
                };
                dependency = clearLocksJob.Schedule(compartmentCount, 64, dependency);

                EvacuateWaterVolumeJob evacuateJob = new EvacuateWaterVolumeJob
                {
                    PumpNodes = pumpPtr,
                    FrontCompartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(fluidFront),
                    BackCompartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(fluidBack),
                    PumpRoomIndices = roomIndices,
                    PumpRemainderM3 = pumpRemainder,
                    PumpMassErrorM3 = pumpMassError,
                    RoomDrainLocks = (DrainageRoomDrainLock64*)NativeArrayUnsafeUtility.GetUnsafePtr(roomDrainLocks),
                    NodeCount = nodeCount,
                    CompartmentCount = compartmentCount,
                    RoomDrainLockCount = roomDrainLocks.Length,
                    DeltaTime = deltaTime,
                    MassQuantumM3 = activeTuning.MassQuantumM3
                };
                dependency = evacuateJob.Schedule(nodeCount, 64, dependency);
            }

            DrainageTelemetryRecorderJob telemetryJob = new DrainageTelemetryRecorderJob
            {
                PumpNodes = pumpPtr,
                Pressure = pressureRead,
                PumpMassErrorM3 = pumpMassError,
                Counters = counters,
                Tuning = tuning,
                TelemetryRing = telemetry,
                FrameSummary = frameSummary,
                TelemetryCursor = telemetryCursor,
                NodeCount = nodeCount,
                EdgeCount = edgeCount,
                SolverIterations = iterations,
                FrameIndex = _frameIndex,
                GlobalQualityWeight = quality,
                InputFlags = telemetryFlags
            };
            _solverHandle = telemetryJob.Schedule(dependency);
            H8Memory.RegisterActiveJob(OwnerSystem, _solverHandle);
            _solverScheduled = true;
            _pressureFrontIsA = nextFrontIsA;
            _solverScheduleTimestamp = Stopwatch.GetTimestamp();
            _flowUploadDirty = true;
            _frameIndex++;
        }

        private bool TryFinalizeScheduledSolverNoWait()
        {
            if (!_solverScheduled)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _solverHandle))
                return false;

            _solverScheduled = false;
            return true;
        }

        private void CompleteScheduledSolverForTeardown()
        {
            if (!_solverScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _solverHandle, forceComplete: true))
                return;

            _solverScheduled = false;
        }

        private bool TryLockJobBuffers()
        {
            _lockedBufferMask = 0UL;
            return TryLock(SumpPumpDrainageBufferIds.PumpNodes, 0) &&
                   TryLock(SumpPumpDrainageBufferIds.PipeEdges, 1) &&
                   TryLock(SumpPumpDrainageBufferIds.NodeAup, 2) &&
                   TryLock(SumpPumpDrainageBufferIds.PumpRoomIndices, 3) &&
                   TryLock(SumpPumpDrainageBufferIds.CsrOffsets, 4) &&
                   TryLock(SumpPumpDrainageBufferIds.CsrDestinations, 5) &&
                   TryLock(SumpPumpDrainageBufferIds.CsrConductance, 6) &&
                   TryLock(SumpPumpDrainageBufferIds.CsrFlow, 7) &&
                   TryLock(SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, 8) &&
                   TryLock(SumpPumpDrainageBufferIds.CsrWriteCursor, 9) &&
                   TryLock(SumpPumpDrainageBufferIds.PressureFront, 10) &&
                   TryLock(SumpPumpDrainageBufferIds.PressureBack, 11) &&
                   TryLock(SumpPumpDrainageBufferIds.PowerPotential, 12) &&
                   TryLock(SumpPumpDrainageBufferIds.PumpRemainder, 13) &&
                   TryLock(SumpPumpDrainageBufferIds.Tuning, 14) &&
                   TryLock(SumpPumpDrainageBufferIds.TelemetryRing, 15) &&
                   TryLock(SumpPumpDrainageBufferIds.TelemetryCursor, 16) &&
                   TryLock(SumpPumpDrainageBufferIds.Counters, 17) &&
                   TryLock(SumpPumpDrainageBufferIds.FrameSummary, 18) &&
                   TryLock(SumpPumpDrainageBufferIds.FlowGpu, 19) &&
                   TryLock(SumpPumpDrainageBufferIds.PumpMassError, 22) &&
                   TryLock(SumpPumpDrainageBufferIds.RoomDrainLocks, 23);
        }

        private bool TryLock(BufferID bufferId, int bit)
        {
            if (_vault != null && _vault.TryLockBuffer(bufferId, OwnerSystem))
            {
                _lockedBufferMask |= 1UL << bit;
                return true;
            }

            UnlockJobBuffers();
            return false;
        }

        private bool TryResolveLockedExistingBuffer<T>(BufferID bufferId, int bit, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null || !_vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            if (!_vault.TryLockBuffer(bufferId, OwnerSystem))
                return false;

            _lockedBufferMask |= 1UL << bit;
            if (_vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated)
                return true;

            UnlockTrackedBuffer(bufferId, bit);
            buffer = default;
            return false;
        }

        private void UnlockJobBuffers()
        {
            UnlockIf(BufferID.ShinobuFluidCompartmentBack, 21);
            UnlockIf(BufferID.ShinobuFluidCompartmentFront, 20);
            UnlockIf(SumpPumpDrainageBufferIds.RoomDrainLocks, 23);
            UnlockIf(SumpPumpDrainageBufferIds.PumpMassError, 22);
            UnlockIf(SumpPumpDrainageBufferIds.FlowGpu, 19);
            UnlockIf(SumpPumpDrainageBufferIds.FrameSummary, 18);
            UnlockIf(SumpPumpDrainageBufferIds.Counters, 17);
            UnlockIf(SumpPumpDrainageBufferIds.TelemetryCursor, 16);
            UnlockIf(SumpPumpDrainageBufferIds.TelemetryRing, 15);
            UnlockIf(SumpPumpDrainageBufferIds.Tuning, 14);
            UnlockIf(SumpPumpDrainageBufferIds.PumpRemainder, 13);
            UnlockIf(SumpPumpDrainageBufferIds.PowerPotential, 12);
            UnlockIf(SumpPumpDrainageBufferIds.PressureBack, 11);
            UnlockIf(SumpPumpDrainageBufferIds.PressureFront, 10);
            UnlockIf(SumpPumpDrainageBufferIds.CsrWriteCursor, 9);
            UnlockIf(SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, 8);
            UnlockIf(SumpPumpDrainageBufferIds.CsrFlow, 7);
            UnlockIf(SumpPumpDrainageBufferIds.CsrConductance, 6);
            UnlockIf(SumpPumpDrainageBufferIds.CsrDestinations, 5);
            UnlockIf(SumpPumpDrainageBufferIds.CsrOffsets, 4);
            UnlockIf(SumpPumpDrainageBufferIds.PumpRoomIndices, 3);
            UnlockIf(SumpPumpDrainageBufferIds.NodeAup, 2);
            UnlockIf(SumpPumpDrainageBufferIds.PipeEdges, 1);
            UnlockIf(SumpPumpDrainageBufferIds.PumpNodes, 0);
            _lockedBufferMask = 0UL;
        }

        private void UnlockTrackedBuffer(BufferID bufferId, int bit)
        {
            UnlockIf(bufferId, bit);
            _lockedBufferMask &= ~(1UL << bit);
        }

        private void UnlockIf(BufferID bufferId, int bit)
        {
            if ((_lockedBufferMask & (1UL << bit)) != 0UL)
                _vault?.TryUnlockBuffer(bufferId, OwnerSystem);
        }

        private uint HydratePowerPotentialFromVault(NativeArray<float> powerPotential, int nodeCount)
        {
            if (_vault == null ||
                !_vault.TryGetGenerationHandle<float>(BufferID.ShinobuLogisticsPressureFront, out VaultGenerationHandle<float> logisticsPressureHandle) ||
                !_vault.TryLockBuffer(BufferID.ShinobuLogisticsPressureFront, OwnerSystem))
            {
                for (int i = 0; i < nodeCount && i < powerPotential.Length; i++)
                    powerPotential[i] = 0f;
                return SumpDrainageTelemetryFlags.MissingPowerVault;
            }

            try
            {
                if (!_vault.TryResolveHandle(in logisticsPressureHandle, out NativeArray<float> logisticsPressure) ||
                    !logisticsPressure.IsCreated ||
                    logisticsPressure.Length <= 0)
                {
                    for (int i = 0; i < nodeCount && i < powerPotential.Length; i++)
                        powerPotential[i] = 0f;
                    return SumpDrainageTelemetryFlags.MissingPowerVault;
                }

                int copyCount = math.min(nodeCount, powerPotential.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    float potential = i < logisticsPressure.Length ? logisticsPressure[i] : 0f;
                    powerPotential[i] = math.saturate(math.isfinite(potential) ? potential : 0f);
                }

                return logisticsPressure.Length < copyCount
                    ? SumpDrainageTelemetryFlags.MissingPowerVault
                    : SumpDrainageTelemetryFlags.None;
            }
            finally
            {
                _vault.TryUnlockBuffer(BufferID.ShinobuLogisticsPressureFront, OwnerSystem);
            }
        }

        private void ResetFrameCounters(NativeArray<int> counters, int nodeCount, int edgeCount)
        {
            counters[SumpPumpPipeGridConstants.CounterFrameDrainedMilliM3] = 0;
            counters[SumpPumpPipeGridConstants.CounterActivePumps] = 0;
            counters[SumpPumpPipeGridConstants.CounterNanCount] = 0;
            counters[SumpPumpPipeGridConstants.CounterPowerMilliWatts] = 0;
            counters[SumpPumpPipeGridConstants.CounterMassErrorMilliM3] = 0;
            counters[SumpPumpPipeGridConstants.CounterNodeCount] = nodeCount;
            counters[SumpPumpPipeGridConstants.CounterEdgeCount] = edgeCount;
        }

        private DrainageTuningDTO RefreshTuning(NativeArray<DrainageTuningDTO> tuningArray, float deltaTime, float quality, int nodeCount, int edgeCount)
        {
            DrainageTuningDTO tuning = tuningArray.Length > 0 ? SanitizeTuning(tuningArray[0]) : DefaultTuning();
            tuning.GlobalQualityWeight = quality;
            tuning.DeltaTimeSeconds = math.max(0f, deltaTime);
            tuning.NodeCount = (ushort)math.min(ushort.MaxValue, math.max(0, nodeCount));
            tuning.EdgeCount = (ushort)math.min(ushort.MaxValue, math.max(0, edgeCount));
            tuning.SolverIterations = (ushort)ResolveSolverIterations(quality);
            tuningArray[0] = tuning;
            return tuning;
        }

        private void InitializeTuningIfNeeded()
        {
            NativeArray<DrainageTuningDTO> tuning = Resolve(in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

            DrainageTuningDTO active = tuning[0];
            if (active.BasePipeConductance <= 0f || !math.isfinite(active.BasePipeConductance))
                active = s_offlineTuning;
            tuning[0] = SanitizeTuning(active);
        }

        private static DrainageTuningDTO SanitizeTuning(in DrainageTuningDTO tuning)
        {
            DrainageTuningDTO sanitized = tuning;
            sanitized.GlobalQualityWeight = math.saturate(math.isfinite(sanitized.GlobalQualityWeight) ? sanitized.GlobalQualityWeight : 1f);
            sanitized.BasePipeConductance = math.max(0.000001f, math.isfinite(sanitized.BasePipeConductance) ? sanitized.BasePipeConductance : SumpPumpPipeGridConstants.DefaultBasePipeConductance);
            sanitized.PumpPowerDraw = math.max(0f, math.isfinite(sanitized.PumpPowerDraw) ? sanitized.PumpPowerDraw : SumpPumpPipeGridConstants.DefaultPumpPowerDrawWatts);
            sanitized.JacobiSmoothingFactor = math.saturate(math.isfinite(sanitized.JacobiSmoothingFactor) ? sanitized.JacobiSmoothingFactor : SumpPumpPipeGridConstants.DefaultJacobiSmoothingFactor);
            sanitized.MaxPumpRateScale = math.max(0.001f, math.isfinite(sanitized.MaxPumpRateScale) ? sanitized.MaxPumpRateScale : 1f);
            sanitized.VisualFlowGain = math.max(0.001f, math.isfinite(sanitized.VisualFlowGain) ? sanitized.VisualFlowGain : SumpPumpPipeGridConstants.DefaultVisualFlowGain);
            sanitized.MassQuantumM3 = math.max(0.000001f, math.isfinite(sanitized.MassQuantumM3) ? sanitized.MassQuantumM3 : SumpPumpPipeGridConstants.DefaultMassQuantumM3);
            return sanitized;
        }

        private static DrainageTuningDTO DefaultTuning()
        {
            return new DrainageTuningDTO
            {
                GlobalQualityWeight = 1f,
                BasePipeConductance = SumpPumpPipeGridConstants.DefaultBasePipeConductance,
                PumpPowerDraw = SumpPumpPipeGridConstants.DefaultPumpPowerDrawWatts,
                JacobiSmoothingFactor = SumpPumpPipeGridConstants.DefaultJacobiSmoothingFactor,
                MaxPumpRateScale = 1f,
                VisualFlowGain = SumpPumpPipeGridConstants.DefaultVisualFlowGain,
                MassQuantumM3 = SumpPumpPipeGridConstants.DefaultMassQuantumM3
            };
        }

        private int ResolveNodeCount(NativeArray<int> counters)
        {
            int count = counters.IsCreated && counters.Length > SumpPumpPipeGridConstants.CounterNodeCount
                ? counters[SumpPumpPipeGridConstants.CounterNodeCount]
                : nodeCapacity;
            return math.clamp(count <= 0 ? nodeCapacity : count, 1, math.min(nodeCapacity, SumpPumpPipeGridConstants.MaxPumpNodes));
        }

        private int ResolveEdgeCount(NativeArray<int> counters)
        {
            int count = counters.IsCreated && counters.Length > SumpPumpPipeGridConstants.CounterEdgeCount
                ? counters[SumpPumpPipeGridConstants.CounterEdgeCount]
                : edgeCapacity;
            return math.clamp(count <= 0 ? edgeCapacity : count, 0, math.min(edgeCapacity, SumpPumpPipeGridConstants.MaxPipeEdges));
        }

        private static int ResolveSolverIterations(float quality)
        {
            return math.clamp((int)math.lerp(1f, 8f, math.saturate(quality)), 1, 8);
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
            NativeArray<DrainageTelemetryEntry> summary = Resolve(in _frameSummaryHandle);
            if (summary.IsCreated && summary.Length > 0)
            {
                DrainageTelemetryEntry entry = summary[0];
                entry.SolverWallMicroseconds = solverWallMicroseconds;
                summary[0] = entry;
            }

            NativeArray<int> cursor = Resolve(in _telemetryCursorHandle);
            NativeArray<DrainageTelemetryEntry> telemetry = Resolve(in _telemetryHandle);
            if (!cursor.IsCreated || !telemetry.IsCreated || cursor.Length <= 0 || telemetry.Length <= 0)
                return;

            int capacity = math.min(telemetry.Length, SumpPumpPipeGridConstants.TelemetryFrameCount);
            int index = (cursor[0] - 1) % capacity;
            if (index < 0)
                index += capacity;
            DrainageTelemetryEntry ringEntry = telemetry[index];
            ringEntry.SolverWallMicroseconds = solverWallMicroseconds;
            telemetry[index] = ringEntry;
        }

        private DrainageTelemetryEntry ReadFrameSummary()
        {
            NativeArray<DrainageTelemetryEntry> summary = Resolve(in _frameSummaryHandle);
            return summary.IsCreated && summary.Length > 0 ? summary[0] : default;
        }

        private bool TryReadLatestTelemetry(out DrainageTelemetryEntry entry)
        {
            if (!_buffersReady || _solverScheduled)
            {
                entry = default;
                return false;
            }

            entry = ReadFrameSummary();
            return entry.FrameIndex != 0u || entry.StateHash != 0u;
        }

        private bool TryReadTuning(out DrainageTuningDTO tuning)
        {
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
            {
                tuning = s_offlineTuning;
                return false;
            }

            NativeArray<DrainageTuningDTO> tuningArray = Resolve(in _tuningHandle);
            if (!tuningArray.IsCreated || tuningArray.Length <= 0)
            {
                tuning = s_offlineTuning;
                return false;
            }

            tuning = SanitizeTuning(tuningArray[0]);
            return true;
        }

        private bool TryWriteTuning(in DrainageTuningDTO tuning)
        {
            if (!_buffersReady && !TryResolveAndInitializeBuffers())
                return false;
            if (_solverScheduled)
                return false;

            if (!_vault.TryLockBuffer(SumpPumpDrainageBufferIds.Tuning, OwnerSystem))
                return false;

            try
            {
                NativeArray<DrainageTuningDTO> tuningArray = Resolve(in _tuningHandle);
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                tuningArray[0] = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                _vault.TryUnlockBuffer(SumpPumpDrainageBufferIds.Tuning, OwnerSystem);
            }
        }

        private void ClearRuntimeScalarBuffers()
        {
            NativeArray<float> pressureFront = Resolve(in _pressureFrontHandle);
            NativeArray<float> pressureBack = Resolve(in _pressureBackHandle);
            NativeArray<float> remainder = Resolve(in _pumpRemainderHandle);
            NativeArray<float> massError = Resolve(in _pumpMassErrorHandle);
            NativeArray<DrainageRoomDrainLock64> roomLocks = Resolve(in _roomDrainLocksHandle);
            for (int i = 0; i < nodeCapacity; i++)
            {
                if (pressureFront.IsCreated && i < pressureFront.Length)
                    pressureFront[i] = 0f;
                if (pressureBack.IsCreated && i < pressureBack.Length)
                    pressureBack[i] = 0f;
                if (remainder.IsCreated && i < remainder.Length)
                    remainder[i] = 0f;
                if (massError.IsCreated && i < massError.Length)
                    massError[i] = 0f;
            }

            if (!roomLocks.IsCreated)
                return;

            for (int i = 0; i < roomLocks.Length; i++)
                roomLocks[i] = default;
        }

        private void UploadFlowVisuals()
        {
            NativeArray<int> counters = Resolve(in _countersHandle);
            int validEdges = counters.IsCreated && counters.Length > SumpPumpPipeGridConstants.CounterValidCsrEdges
                ? counters[SumpPumpPipeGridConstants.CounterValidCsrEdges]
                : edgeCapacity;
            if (uploadVisualFlowBuffer)
                UploadStructuredFlowBuffer(validEdges);
            if (publishConnectionSplineFlow)
                PublishConnectionSplineNodeFlow();
        }

        private void UploadStructuredFlowBuffer(int validEdges)
        {
            NativeArray<DrainagePipeFlowGpuDTO> flowGpu = Resolve(in _flowGpuHandle);
            int safeCount = math.min(math.max(0, validEdges), flowGpu.IsCreated ? flowGpu.Length : 0);
            if (safeCount <= 0 || !EnsureFlowGraphicsBuffers(safeCount))
                return;

            GraphicsBuffer target = ResolveNextFlowWriteBuffer();
            if (target == null || !target.IsValid())
                return;

            NativeArray<DrainagePipeFlowGpuDTO> mapped = target.LockBufferForWrite<DrainagePipeFlowGpuDTO>(0, safeCount);
            void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
            void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(flowGpu);
            UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<DrainagePipeFlowGpuDTO>());
            target.UnlockBufferAfterWrite<DrainagePipeFlowGpuDTO>(safeCount);
            Shader.SetGlobalBuffer(s_DrainagePipeEdgeFlowId, target);
            Shader.SetGlobalInt(s_DrainagePipeEdgeCountId, safeCount);
        }

        private void PublishConnectionSplineNodeFlow()
        {
            NativeArray<int> offsets = Resolve(in _csrOffsetsHandle);
            NativeArray<float> flows = Resolve(in _csrFlowHandle);
            NativeArray<int> counters = Resolve(in _countersHandle);
            if (!offsets.IsCreated || !flows.IsCreated || !counters.IsCreated)
                return;

            int nodeCount = ResolveNodeCount(counters);
            DrainageTuningDTO tuning = ReadTuningOrDefault();
            float visualGain = math.max(0.001f, tuning.VisualFlowGain);
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                int start = math.clamp(offsets[nodeIndex], 0, flows.Length);
                int end = math.clamp(offsets[nodeIndex + 1], start, flows.Length);
                float flow01 = 0f;
                for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
                    flow01 = math.max(flow01, math.saturate(math.abs(flows[edgeIndex]) * visualGain));
                if (flow01 > 0.001f)
                    ConnectionSplineBatchRenderer.SetPipeNodeFlow((uint)nodeIndex, flow01);
            }
        }

        private DrainageTuningDTO ReadTuningOrDefault()
        {
            NativeArray<DrainageTuningDTO> tuning = Resolve(in _tuningHandle);
            return tuning.IsCreated && tuning.Length > 0 ? SanitizeTuning(tuning[0]) : s_offlineTuning;
        }

        private bool EnsureFlowGraphicsBuffers(int safeCount)
        {
            if (_flowBufferA != null && _flowBufferB != null && _flowBufferCapacity >= safeCount)
                return _flowBufferA.IsValid() && _flowBufferB.IsValid();

            ReleaseGraphicsBuffer(ref _flowBufferA);
            ReleaseGraphicsBuffer(ref _flowBufferB);
            _flowBufferCapacity = math.max(1, math.ceilpow2(safeCount));
            _flowBufferA = CreateFlowGraphicsBuffer(_flowBufferCapacity);
            _flowBufferB = CreateFlowGraphicsBuffer(_flowBufferCapacity);
            return _flowBufferA.IsValid() && _flowBufferB.IsValid();
        }

        private static GraphicsBuffer CreateFlowGraphicsBuffer(int capacity)
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                capacity,
                UnsafeUtility.SizeOf<DrainagePipeFlowGpuDTO>());
        }

        private GraphicsBuffer ResolveNextFlowWriteBuffer()
        {
            _flowBufferWriteIndex ^= 1;
            return _flowBufferWriteIndex == 0 ? _flowBufferA : _flowBufferB;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private void ReleaseOwnedBuffers()
        {
            if (_vault == null)
            {
                ResetHandles();
                return;
            }

            _vault.ReleaseBuffer(in _flowGpuHandle);
            _vault.ReleaseBuffer(in _frameSummaryHandle);
            _vault.ReleaseBuffer(in _csvScratchHandle);
            _vault.ReleaseBuffer(in _profilesHandle);
            _vault.ReleaseBuffer(in _countersHandle);
            _vault.ReleaseBuffer(in _telemetryCursorHandle);
            _vault.ReleaseBuffer(in _telemetryHandle);
            _vault.ReleaseBuffer(in _tuningHandle);
            _vault.ReleaseBuffer(in _roomDrainLocksHandle);
            _vault.ReleaseBuffer(in _pumpMassErrorHandle);
            _vault.ReleaseBuffer(in _pumpRemainderHandle);
            _vault.ReleaseBuffer(in _powerPotentialHandle);
            _vault.ReleaseBuffer(in _pressureBackHandle);
            _vault.ReleaseBuffer(in _pressureFrontHandle);
            _vault.ReleaseBuffer(in _csrWriteCursorHandle);
            _vault.ReleaseBuffer(in _csrFlatEdgeIndexHandle);
            _vault.ReleaseBuffer(in _csrFlowHandle);
            _vault.ReleaseBuffer(in _csrConductanceHandle);
            _vault.ReleaseBuffer(in _csrDestinationsHandle);
            _vault.ReleaseBuffer(in _csrOffsetsHandle);
            _vault.ReleaseBuffer(in _pumpRoomIndicesHandle);
            _vault.ReleaseBuffer(in _nodeAupHandle);
            _vault.ReleaseBuffer(in _pipeEdgesHandle);
            _vault.ReleaseBuffer(in _pumpNodesHandle);
            ResetHandles();
        }

        private void ResetHandles()
        {
            _pumpNodesHandle = default;
            _pipeEdgesHandle = default;
            _nodeAupHandle = default;
            _pumpRoomIndicesHandle = default;
            _csrOffsetsHandle = default;
            _csrDestinationsHandle = default;
            _csrConductanceHandle = default;
            _csrFlowHandle = default;
            _csrFlatEdgeIndexHandle = default;
            _csrWriteCursorHandle = default;
            _pressureFrontHandle = default;
            _pressureBackHandle = default;
            _powerPotentialHandle = default;
            _pumpRemainderHandle = default;
            _pumpMassErrorHandle = default;
            _roomDrainLocksHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _countersHandle = default;
            _profilesHandle = default;
            _csvScratchHandle = default;
            _frameSummaryHandle = default;
            _flowGpuHandle = default;
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped)
                return;

            NativeArray<DrainageTelemetryEntry> telemetry = Resolve(in _telemetryHandle);
            if (!telemetry.IsCreated)
                return;

            _blackBoxDumped = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(dumpPath));
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(RuntimeHash);
                    writer.Write(telemetry.Length);
                    for (int i = 0; i < telemetry.Length; i++)
                    {
                        DrainageTelemetryEntry entry = telemetry[i];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.FrameEvacuatedM3);
                        writer.Write(entry.TotalEvacuatedM3);
                        writer.Write(entry.AveragePressure);
                        writer.Write(entry.MaxPressure);
                        writer.Write(entry.GlobalQualityWeight);
                        writer.Write(entry.TotalPowerDrawWatts);
                        writer.Write(entry.ActivePumpCount);
                        writer.Write(entry.NanCount);
                        writer.Write(entry.SolverWallMicroseconds);
                        writer.Write(entry.NodeCount);
                        writer.Write(entry.EdgeCount);
                        writer.Write(entry.Flags);
                        writer.Write(entry.ConservativeMassErrorMilli);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception);
#endif
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_buffersReady || _solverScheduled)
                return;

            NativeArray<PipeEdgeDTO> edges = Resolve(in _pipeEdgesHandle);
            NativeArray<double3> aup = Resolve(in _nodeAupHandle);
            NativeArray<float> pressure = _pressureFrontIsA ? Resolve(in _pressureFrontHandle) : Resolve(in _pressureBackHandle);
            NativeArray<int> counters = Resolve(in _countersHandle);
            if (!edges.IsCreated || !aup.IsCreated || !pressure.IsCreated || !counters.IsCreated || aup.Length <= 0)
                return;

            int edgeCount = ResolveEdgeCount(counters);
            double3 origin = aup[0];
            for (int edgeIndex = 0; edgeIndex < edgeCount && edgeIndex < edges.Length; edgeIndex++)
            {
                PipeEdgeDTO edge = edges[edgeIndex];
                if ((edge.Flags & SumpPipeEdgeFlags.Active) == 0u ||
                    (uint)edge.SourceNodeIndex >= (uint)aup.Length ||
                    (uint)edge.DestinationNodeIndex >= (uint)aup.Length)
                {
                    continue;
                }

                float flow01 = math.saturate(math.abs(edge.CurrentFlow) * ReadTuningOrDefault().VisualFlowGain);
                Gizmos.color = Color.Lerp(Color.blue, Color.white, flow01);
                Gizmos.DrawLine(ToGizmoPosition(aup[edge.SourceNodeIndex], origin), ToGizmoPosition(aup[edge.DestinationNodeIndex], origin));
            }

            int nodeCount = ResolveNodeCount(counters);
            for (int nodeIndex = 0; nodeIndex < nodeCount && nodeIndex < aup.Length && nodeIndex < pressure.Length; nodeIndex++)
            {
                float pressure01 = math.saturate(pressure[nodeIndex]);
                Gizmos.color = Color.Lerp(Color.cyan, Color.red, pressure01);
                Gizmos.DrawSphere(ToGizmoPosition(aup[nodeIndex], origin), math.lerp(0.06f, 0.18f, pressure01));
            }
        }

        private Vector3 ToGizmoPosition(double3 nodeAup, double3 originAup)
        {
            double3 delta = nodeAup - originAup;
            return transform.position + new Vector3(
                (float)math.clamp(delta.x, -100000d, 100000d),
                (float)math.clamp(delta.y, -100000d, 100000d),
                (float)math.clamp(delta.z, -100000d, 100000d));
        }
#endif
    }
}
