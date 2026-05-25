using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Vault-backed CSR two-pass sump-pump drainage runtime for flooded rooms and pipe visuals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Construction/Sump Pump Pipe Grid Runtime")]
    public sealed unsafe class SumpPumpPipeGridRuntime : MonoBehaviour, ISlowTickable, ILateFrameTickable, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const SystemID OwnerSystem = SystemID.Construction;
        private const float SlowTickStepSeconds = 0.1f;
        private const int FixedDrainageDeltaPassCount = 2;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_340_Logistics.bin";
        private const uint RuntimeHash = 0x53333430u;
        private const ulong DumpMagic = 0x00384E4F54434548UL;
        private const uint DumpVersion = 1u;

        private static readonly int s_DrainagePipeEdgeFlowId = Shader.PropertyToID("_H8DrainagePipeEdgeFlow");
        private static readonly int s_DrainagePipeEdgeCountId = Shader.PropertyToID("_H8DrainagePipeEdgeCount");
        private static SumpPumpPipeGridRuntime s_active;
        private static DrainageTuningDTO s_offlineTuning = DefaultTuning();

        [Header("Graph")]
        [Tooltip("Maximum Vault pump/pipe node count. Mock topology defaults to 2000.")]
        [SerializeField, Range(16, SumpPumpPipeGridConstants.MaxPumpNodes)] private int nodeCapacity = SumpPumpPipeGridConstants.MaxPumpNodes;

        [Tooltip("Maximum flat directed pipe edge count. Mock topology defaults to 6000.")]
        [SerializeField, Range(16, SumpPumpPipeGridConstants.MaxPipeEdges)] private int edgeCapacity = SumpPumpPipeGridConstants.MaxPipeEdges;

        [Tooltip("Builds a deterministic 2000-node / 6000-edge drainage graph on enable when Vault buffers are empty.")]
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
        private VaultGenerationHandle<DrainageNodeDTO> _pumpNodesHandle;
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
        private VaultGenerationHandle<float> _pumpBaseMaxRateHandle;
        private VaultGenerationHandle<uint> _pumpPowerNodeHashesHandle;
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
        private JobHandle _mockSeedHandle;
        private GraphicsBuffer _flowBufferA;
        private GraphicsBuffer _flowBufferB;
        private Thread _dumpThread;
        private AutoResetEvent _dumpSignal;
        private byte[] _dumpBytes;
        private string _dumpPath;
        private ulong _lockedBufferMask;
        private long _solverScheduleTimestamp;
        private uint _frameIndex;
        private int _dumpByteCount;
        private int _dumpPending;
        private int _dumpThreadStop;
        private int _dumpWriteFault;
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
        private bool _mockSeedScheduled;
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

#if UNITY_EDITOR
        /// <summary>Copies the fixed telemetry ring into an editor-owned buffer without allocating per refresh.</summary>
        public static bool TryCopyTelemetry(DrainageTelemetryEntry[] target, out int count)
        {
            SumpPumpPipeGridRuntime runtime = s_active;
            if (runtime != null)
                return runtime.TryCopyTelemetryTo(target, out count);

            count = 0;
            return false;
        }

        /// <summary>Editor-only profile import bridge. Runtime must already own its cold Vault handles.</summary>
        public static bool TryLoadPipeProfilesFromCsvBytes(byte[] csvBytes, out int profileCount)
        {
            profileCount = 0;
            SumpPumpPipeGridRuntime runtime = s_active;
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   csvBytes != null &&
                   runtime.TryLoadPipeProfilesFromCsv(csvBytes, out profileCount);
        }
#endif

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

        /// <summary>Editor/cold facade for seeding the deterministic mock graph without scene search.</summary>
        public static bool TryGenerateMockDrainageNetwork()
        {
            SumpPumpPipeGridRuntime runtime = s_active;
            if (runtime == null || !runtime.isActiveAndEnabled || runtime._solverScheduled)
                return false;

            runtime.GenerateMockDrainageNetwork();
            return true;
        }

        private void OnEnable()
        {
            s_active = this;
            _vault = GlobalRegistry.DataVault;
            InitializeDumpWriterCold();
            _buffersReady = TryInitializeBuffers();
            if (_buffersReady && generateMockOnEnable)
                GenerateMockDrainageNetwork();

            TryRegisterHotSwapListener();
            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void OnDisable()
        {
            CompleteMockSeedForTeardown();
            CompleteScheduledSolverForTeardown();
            UnlockJobBuffers();
            if (_buffersReady && TryReadTuning(out DrainageTuningDTO tuning))
                s_offlineTuning = tuning;

            TryUnregisterHotSwapListener();
            if (_registeredLateFrameTick)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            if (_registeredSlowTick)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            ReleaseOwnedBuffers();
            ReleaseGraphicsBuffer(ref _flowBufferA);
            ReleaseGraphicsBuffer(ref _flowBufferB);
            ShutdownDumpWriterCold();
            if (ReferenceEquals(s_active, this))
                s_active = null;

            _registeredLateFrameTick = false;
            _registeredSlowTick = false;
            _buffersReady = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null)
            {
                if (_registeredLateFrameTick)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                    _registeredLateFrameTick = false;
                }

                if (_registeredSlowTick)
                {
                    GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                    _registeredSlowTick = false;
                }

                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault && currentService != null)
            {
                CompleteMockSeedForTeardown();
                CompleteScheduledSolverForTeardown();
                UnlockJobBuffers();
                ReleaseOwnedBuffers();
                _vault = currentService as IDataVault;
                _buffersReady = TryInitializeBuffers();
                if (_buffersReady && generateMockOnEnable)
                    GenerateMockDrainageNetwork();
            }
        }

        private bool _registeredHotSwap;

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        /// <summary>Dispatcher shutdown bridge.</summary>
        public void OnServiceShutdown()
        {
            OnDisable();
        }

        /// <summary>Quality-scaled slow simulation cadence. No object pump or water-particle state is read here.</summary>
        public void SlowTick()
        {
            if (_solverScheduled || _mockSeedScheduled)
                return;

            if (!_buffersReady)
                return;

            float quality = ResolveGlobalQualityWeight();
            _solveAccumulator = math.min(1f, _solveAccumulator + SlowTickStepSeconds);
            float cadence = ResolveSolveCadenceSeconds(quality);
            if (_solveAccumulator + 0.00001f < cadence)
                return;

            float deltaTime = _solveAccumulator;
            _solveAccumulator = 0f;
            ScheduleDrainageSolve(deltaTime, quality);
        }

        /// <summary>Completes the scheduled chain in the dispatcher visual-sync lane and uploads flow scalars.</summary>
        public void LateFrameTick()
        {
            if (!TryFinalizeMockSeedNoWait())
                return;

            bool solverWasScheduled = _solverScheduled;
            if (!TryFinalizeScheduledSolverNoWait())
                return;

            if (solverWasScheduled)
            {
                UnlockJobBuffers();
                StampSolverWallTime(ResolveElapsedMicroseconds(_solverScheduleTimestamp));
                _solverScheduleTimestamp = 0L;
            }
            else
            {
                RecordTelemetryHeartbeat();
            }

            DrainageTelemetryEntry entry = ReadFrameSummary();
            _debugActivePumps = (int)math.min(int.MaxValue, entry.ActivePumpCount);
            _debugFrameEvacuatedM3 = entry.FrameEvacuatedM3;
            _debugAveragePressure = entry.AveragePressure;

            if ((entry.Flags & (SumpDrainageTelemetryFlags.NonFinite | SumpDrainageTelemetryFlags.SolverOverBudget)) != 0u)
                RequestBlackBoxDumpOnce();

            if (_flowUploadDirty)
            {
                UploadFlowVisuals();
                _flowUploadDirty = false;
            }
        }

#if UNITY_EDITOR
        /// <summary>Cold CSV ingestion entry point for pipe_and_pump_specs.csv bytes.</summary>
        public bool TryLoadPipeProfilesFromCsv(ReadOnlySpan<byte> csvBytes, out int profileCount)
        {
            profileCount = 0;
            if (!_buffersReady)
                return false;

            if (!_vault.TryLockBuffer(SumpPumpDrainageBufferIds.PipeProfiles, OwnerSystem))
                return false;

            try
            {
                NativeArray<PipeProfileDTO> profiles = BorrowMutable(in _profilesHandle);
                return SumpPumpPipeGridValidation.TryParsePipeProfilesCsv(csvBytes, profiles, out profileCount);
            }
            finally
            {
                _vault.TryUnlockBuffer(SumpPumpDrainageBufferIds.PipeProfiles, OwnerSystem);
            }
        }
#endif

        /// <summary>Rebuilds the deterministic 2000-node / 6000-edge mock drainage topology in Vault buffers.</summary>
        public void GenerateMockDrainageNetwork()
        {
            if (_mockSeedScheduled || _solverScheduled)
                return;

            if (!_buffersReady)
                return;

            UnlockJobBuffers();
            if (!TryLockJobBuffers())
                return;

            bool scheduled = false;
            try
            {
                NativeArray<DrainageNodeDTO> pumps = BorrowMutable(in _pumpNodesHandle);
                NativeArray<PipeEdgeDTO> edges = BorrowMutable(in _pipeEdgesHandle);
                NativeArray<double3> nodeAup = BorrowMutable(in _nodeAupHandle);
                NativeArray<int> roomIndices = BorrowMutable(in _pumpRoomIndicesHandle);
                NativeArray<float> power = BorrowMutable(in _powerPotentialHandle);
                NativeArray<float> baseRates = BorrowMutable(in _pumpBaseMaxRateHandle);
                NativeArray<uint> powerNodeHashes = BorrowMutable(in _pumpPowerNodeHashesHandle);
                NativeArray<int> counters = BorrowMutable(in _countersHandle);
                NativeArray<DrainageTuningDTO> tuning = BorrowMutable(in _tuningHandle);
                if (!pumps.IsCreated || !edges.IsCreated || !nodeAup.IsCreated || !roomIndices.IsCreated || !power.IsCreated || !baseRates.IsCreated || !powerNodeHashes.IsCreated || !counters.IsCreated || !tuning.IsCreated)
                    return;

                DrainageTuningDTO current = tuning.Length > 0 ? SanitizeTuning(tuning[0]) : DefaultTuning();
                GenerateMockPipeNetworkJob job = new GenerateMockPipeNetworkJob
                {
                    PumpNodes = pumps,
                    PipeEdges = edges,
                    NodeAup = nodeAup,
                    PumpRoomIndices = roomIndices,
                    PowerPotential = power,
                    PumpBaseMaxRate = baseRates,
                    PumpPowerNodeHashes = powerNodeHashes,
                    Counters = counters,
                    Tuning = tuning,
                    RequestedNodeCount = math.min(nodeCapacity, SumpPumpPipeGridConstants.MaxPumpNodes),
                    RequestedEdgeCount = math.min(edgeCapacity, SumpPumpPipeGridConstants.MaxPipeEdges),
                    BaseConductance = current.BasePipeConductance,
                    MaxPumpRate = current.MaxPumpThroughputM3PerSecond,
                    PumpPowerDraw = current.PumpPowerDraw
                };

                _mockSeedHandle = job.Schedule();
                H8Memory.RegisterActiveJob(OwnerSystem, _mockSeedHandle);
                _mockSeedScheduled = true;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    UnlockJobBuffers();
            }
        }

        private bool TryInitializeBuffers()
        {
            if (_vault == null ||
                !SumpPumpPipeGridValidation.ValidatePumpNodeLayout() ||
                !SumpPumpPipeGridValidation.ValidatePipeEdgeLayout() ||
                !SumpPumpPipeGridValidation.ValidateRoomDrainLockLayout() ||
                !SumpPumpPipeGridValidation.ValidateDrainageTuningLayout() ||
                !SumpPumpPipeGridValidation.ValidatePipeProfileLayout() ||
                !SumpPumpPipeGridValidation.ValidateDrainageTelemetryLayout() ||
                !SumpPumpPipeGridValidation.ValidateDrainagePipeFlowGpuLayout() ||
                !SumpPumpPipeGridValidation.ValidateDrainageDumpHeaderLayout())
                return false;

            nodeCapacity = math.clamp(nodeCapacity, 16, SumpPumpPipeGridConstants.MaxPumpNodes);
            edgeCapacity = math.clamp(edgeCapacity, 16, SumpPumpPipeGridConstants.MaxPipeEdges);
            int roomLockCapacity = math.max(nodeCapacity, HabitatFluidIncursionConstants.MaxCompartments);
            _pumpNodesHandle = _vault.EnsureGenerationHandle<DrainageNodeDTO>(SumpPumpDrainageBufferIds.PumpNodes, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pipeEdgesHandle = _vault.EnsureGenerationHandle<PipeEdgeDTO>(SumpPumpDrainageBufferIds.PipeEdges, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _nodeAupHandle = _vault.EnsureGenerationHandle<double3>(SumpPumpDrainageBufferIds.NodeAup, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpRoomIndicesHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.PumpRoomIndices, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrOffsetsHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrOffsets, nodeCapacity + 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrDestinationsHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrDestinations, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrConductanceHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.CsrConductance, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrFlowHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.CsrFlow, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrFlatEdgeIndexHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csrWriteCursorHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.CsrWriteCursor, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pressureFrontHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.PressureFront, nodeCapacity, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _pressureBackHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.PressureBack, nodeCapacity, OwnerSystem, NativeArrayOptions.UninitializedMemory);
            _powerPotentialHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.PowerPotential, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpBaseMaxRateHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.PumpBaseMaxRate, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpPowerNodeHashesHandle = _vault.EnsureGenerationHandle<uint>(SumpPumpDrainageBufferIds.PumpPowerNodeHashes, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpRemainderHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.PumpRemainder, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _pumpMassErrorHandle = _vault.EnsureGenerationHandle<float>(SumpPumpDrainageBufferIds.PumpMassError, nodeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _roomDrainLocksHandle = _vault.EnsureGenerationHandle<DrainageRoomDrainLock64>(SumpPumpDrainageBufferIds.RoomDrainLocks, roomLockCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            _tuningHandle = _vault.EnsureGenerationHandle<DrainageTuningDTO>(SumpPumpDrainageBufferIds.Tuning, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryHandle = _vault.EnsureGenerationHandle<DrainageTelemetryEntry>(SumpPumpDrainageBufferIds.TelemetryRing, SumpPumpPipeGridConstants.TelemetryFrameCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _telemetryCursorHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.TelemetryCursor, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _countersHandle = _vault.EnsureGenerationHandle<int>(SumpPumpDrainageBufferIds.Counters, SumpPumpPipeGridConstants.CounterCount, OwnerSystem, NativeArrayOptions.ClearMemory);
            _profilesHandle = _vault.EnsureGenerationHandle<PipeProfileDTO>(SumpPumpDrainageBufferIds.PipeProfiles, SumpPumpPipeGridConstants.MaxPipeProfiles, OwnerSystem, NativeArrayOptions.ClearMemory);
            _csvScratchHandle = _vault.EnsureGenerationHandle<byte>(SumpPumpDrainageBufferIds.CsvScratch, SumpPumpPipeGridConstants.CsvScratchBytes, OwnerSystem, NativeArrayOptions.ClearMemory);
            _frameSummaryHandle = _vault.EnsureGenerationHandle<DrainageTelemetryEntry>(SumpPumpDrainageBufferIds.FrameSummary, 1, OwnerSystem, NativeArrayOptions.ClearMemory);
            _flowGpuHandle = _vault.EnsureGenerationHandle<DrainagePipeFlowGpuDTO>(SumpPumpDrainageBufferIds.FlowGpu, edgeCapacity, OwnerSystem, NativeArrayOptions.ClearMemory);
            if (!ValidateOwnedBuffers())
            {
                ReleaseOwnedBuffers();
                return false;
            }

            if (uploadVisualFlowBuffer && !EnsureFlowGraphicsBuffers(edgeCapacity))
                uploadVisualFlowBuffer = false;

            InitializeTuningIfNeeded();
            return true;
        }

        private NativeArray<T> BorrowMutable<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return _vault != null && _vault.TryResolveHandle(in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        private NativeArray<T> Read<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return _vault != null && _vault.TryReadHandle(in handle, out NativeArray<T> buffer) ? buffer : default;
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
                   HasResolvedBuffer(in _pumpBaseMaxRateHandle, safeNodes) &&
                   HasResolvedBuffer(in _pumpPowerNodeHashesHandle, safeNodes) &&
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

            NativeArray<DrainageNodeDTO> pumps = BorrowMutable(in _pumpNodesHandle);
            NativeArray<PipeEdgeDTO> edges = BorrowMutable(in _pipeEdgesHandle);
            NativeArray<double3> nodeAup = BorrowMutable(in _nodeAupHandle);
            NativeArray<int> roomIndices = BorrowMutable(in _pumpRoomIndicesHandle);
            NativeArray<int> csrOffsets = BorrowMutable(in _csrOffsetsHandle);
            NativeArray<int> csrDestinations = BorrowMutable(in _csrDestinationsHandle);
            NativeArray<float> csrConductance = BorrowMutable(in _csrConductanceHandle);
            NativeArray<float> csrFlow = BorrowMutable(in _csrFlowHandle);
            NativeArray<int> csrFlatEdgeIndex = BorrowMutable(in _csrFlatEdgeIndexHandle);
            NativeArray<int> csrWriteCursor = BorrowMutable(in _csrWriteCursorHandle);
            NativeArray<float> pressureFront = BorrowMutable(in _pressureFrontHandle);
            NativeArray<float> pressureBack = BorrowMutable(in _pressureBackHandle);
            NativeArray<float> powerPotential = BorrowMutable(in _powerPotentialHandle);
            NativeArray<float> pumpBaseMaxRate = BorrowMutable(in _pumpBaseMaxRateHandle);
            NativeArray<uint> pumpPowerNodeHashes = BorrowMutable(in _pumpPowerNodeHashesHandle);
            NativeArray<float> pumpRemainder = BorrowMutable(in _pumpRemainderHandle);
            NativeArray<float> pumpMassError = BorrowMutable(in _pumpMassErrorHandle);
            NativeArray<DrainageRoomDrainLock64> roomDrainLocks = BorrowMutable(in _roomDrainLocksHandle);
            NativeArray<DrainageTuningDTO> tuning = BorrowMutable(in _tuningHandle);
            NativeArray<DrainageTelemetryEntry> telemetry = BorrowMutable(in _telemetryHandle);
            NativeArray<int> telemetryCursor = BorrowMutable(in _telemetryCursorHandle);
            NativeArray<int> counters = BorrowMutable(in _countersHandle);
            NativeArray<DrainageTelemetryEntry> frameSummary = BorrowMutable(in _frameSummaryHandle);
            NativeArray<DrainagePipeFlowGpuDTO> flowGpu = BorrowMutable(in _flowGpuHandle);
            if (!pumps.IsCreated || !edges.IsCreated || !nodeAup.IsCreated || !roomIndices.IsCreated ||
                !csrOffsets.IsCreated || !csrDestinations.IsCreated || !csrConductance.IsCreated ||
                !csrFlow.IsCreated || !csrFlatEdgeIndex.IsCreated || !csrWriteCursor.IsCreated ||
                !pressureFront.IsCreated || !pressureBack.IsCreated || !powerPotential.IsCreated || !pumpBaseMaxRate.IsCreated || !pumpPowerNodeHashes.IsCreated ||
                !pumpRemainder.IsCreated || !pumpMassError.IsCreated || !roomDrainLocks.IsCreated || !tuning.IsCreated || !telemetry.IsCreated ||
                !telemetryCursor.IsCreated || !counters.IsCreated || !frameSummary.IsCreated || !flowGpu.IsCreated)
            {
                UnlockJobBuffers();
                return;
            }

            int nodeCount = ResolveNodeCount(counters);
            int edgeCount = ResolveEdgeCount(counters);
            uint telemetryFlags = SumpDrainageTelemetryFlags.None;
            bool hasFluidFront = TryLockAndReadExistingBuffer(BufferID.ShinobuFluidCompartmentFront, 20, out NativeArray<FluidCompartmentDTO> fluidFront);
            bool hasFluidBack = TryLockAndBorrowMutableExistingBuffer(BufferID.ShinobuFluidCompartmentBack, 21, out NativeArray<FluidCompartmentDTO> fluidBack);
            bool hasPowerNodes = TryLockAndReadExistingBuffer(Hecton8.Power.PowerGridBufferIds.Nodes, 24, out NativeArray<Hecton8.Power.PowerNodeDTO> powerNodes);
            bool hasPowerPotential = TryLockAndReadExistingBuffer(Hecton8.Power.PowerGridBufferIds.PotentialFront, 25, out NativeArray<float> powerPotentialFront);
            int compartmentCount = hasFluidFront && hasFluidBack ? math.min(fluidFront.Length, fluidBack.Length) : 0;
            if (compartmentCount <= 0)
            {
                telemetryFlags |= SumpDrainageTelemetryFlags.MissingFluidVault;
                UnlockTrackedBuffer(BufferID.ShinobuFluidCompartmentBack, 21);
                UnlockTrackedBuffer(BufferID.ShinobuFluidCompartmentFront, 20);
            }
            if (!hasPowerNodes || !hasPowerPotential)
            {
                telemetryFlags |= SumpDrainageTelemetryFlags.MissingPowerVault;
                UnlockTrackedBuffer(Hecton8.Power.PowerGridBufferIds.PotentialFront, 25);
                UnlockTrackedBuffer(Hecton8.Power.PowerGridBufferIds.Nodes, 24);
                powerNodes = default;
                powerPotentialFront = default;
            }

            ResetFrameCounters(counters, nodeCount, edgeCount);
            DrainageTuningDTO activeTuning = RefreshTuning(tuning, deltaTime, quality, nodeCount, edgeCount);
            int deltaPassCount = FixedDrainageDeltaPassCount;
            counters[SumpPumpPipeGridConstants.CounterDeltaPassCount] = deltaPassCount;
            DrainageNodeDTO* pumpPtr = (DrainageNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(pumps);
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
            ApplyPumpPowerConstraintJob powerJob = new ApplyPumpPowerConstraintJob
            {
                PumpNodes = pumpPtr,
                PumpBaseMaxRate = pumpBaseMaxRate,
                PumpPowerNodeHashes = pumpPowerNodeHashes,
                PowerNodes = powerNodes,
                PowerPotentialFront = powerPotentialFront,
                PowerPotential = powerPotential,
                NodeCount = nodeCount,
                MaxPumpThroughputM3PerSecond = activeTuning.MaxPumpThroughputM3PerSecond
            };
            dependency = powerJob.Schedule(nodeCount, 64, dependency);

            for (int passIndex = 0; passIndex < FixedDrainageDeltaPassCount; passIndex++)
            {
                EvaluatePipePressureDeltaPassJob pressureJob = new EvaluatePipePressureDeltaPassJob
                {
                    PumpNodes = pumpPtr,
                    NodeEdgeOffsets = csrOffsets,
                    EdgeDestinations = csrDestinations,
                    EdgeConductance = csrConductance,
                    NodeAup = nodeAup,
                    PressureFront = pressureRead,
                    PressureBack = pressureWrite,
                    PowerPotential = powerPotential,
                    NodeCount = nodeCount,
                    DeltaSmoothingFactor = activeTuning.DeltaSmoothingFactor,
                    GravityAssistScalar = activeTuning.GravityAssistScalar,
                    GravityResistanceScalar = activeTuning.GravityResistanceScalar
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

                ExecuteWaterEvacuationJob evacuateJob = new ExecuteWaterEvacuationJob
                {
                    PumpNodes = pumpPtr,
                    FrontCompartments = (FluidCompartmentDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(fluidFront),
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
                DeltaPassCount = deltaPassCount,
                FrameIndex = _frameIndex,
                GlobalQualityWeight = quality,
                PumpPowerDrawWatts = activeTuning.PumpPowerDraw,
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

        private bool TryFinalizeMockSeedNoWait()
        {
            if (!_mockSeedScheduled)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _mockSeedHandle))
                return false;

            ClearRuntimeScalarBuffers();
            _topologyDirty = true;
            _pressureFrontIsA = true;
            _mockSeedScheduled = false;
            UnlockJobBuffers();
            return true;
        }

        private void CompleteMockSeedForTeardown()
        {
            if (!_mockSeedScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _mockSeedHandle, forceComplete: true))
                return;

            ClearRuntimeScalarBuffers();
            _topologyDirty = true;
            _pressureFrontIsA = true;
            _mockSeedScheduled = false;
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
                   TryLock(SumpPumpDrainageBufferIds.PumpBaseMaxRate, 26) &&
                   TryLock(SumpPumpDrainageBufferIds.PumpPowerNodeHashes, 27) &&
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

        private bool TryLockAndBorrowMutableExistingBuffer<T>(BufferID bufferId, int bit, out NativeArray<T> buffer) where T : struct
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

        private bool TryLockAndReadExistingBuffer<T>(BufferID bufferId, int bit, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null || !_vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            if (!_vault.TryLockBuffer(bufferId, OwnerSystem))
                return false;

            _lockedBufferMask |= 1UL << bit;
            if (_vault.TryReadHandle(in handle, out buffer) && buffer.IsCreated)
                return true;

            UnlockTrackedBuffer(bufferId, bit);
            buffer = default;
            return false;
        }

        private void UnlockJobBuffers()
        {
            UnlockIf(Hecton8.Power.PowerGridBufferIds.PotentialFront, 25);
            UnlockIf(Hecton8.Power.PowerGridBufferIds.Nodes, 24);
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
            UnlockIf(SumpPumpDrainageBufferIds.PumpPowerNodeHashes, 27);
            UnlockIf(SumpPumpDrainageBufferIds.PumpBaseMaxRate, 26);
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
            tuning.DeltaPassCount = FixedDrainageDeltaPassCount;
            tuningArray[0] = tuning;
            return tuning;
        }

        private void InitializeTuningIfNeeded()
        {
            NativeArray<DrainageTuningDTO> tuning = BorrowMutable(in _tuningHandle);
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
            sanitized.DeltaSmoothingFactor = math.saturate(math.isfinite(sanitized.DeltaSmoothingFactor) ? sanitized.DeltaSmoothingFactor : SumpPumpPipeGridConstants.DefaultDeltaSmoothingFactor);
            sanitized.MaxPumpRateScale = math.max(0.001f, math.isfinite(sanitized.MaxPumpRateScale) ? sanitized.MaxPumpRateScale : 1f);
            sanitized.MaxPumpThroughputM3PerSecond = math.max(0.000001f, math.isfinite(sanitized.MaxPumpThroughputM3PerSecond) ? sanitized.MaxPumpThroughputM3PerSecond : SumpPumpPipeGridConstants.DefaultMaxPumpRateM3PerSecond);
            sanitized.GravityAssistScalar = math.max(0f, math.isfinite(sanitized.GravityAssistScalar) ? sanitized.GravityAssistScalar : SumpPumpPipeGridConstants.DefaultGravityAssistScalar);
            sanitized.GravityResistanceScalar = math.max(0f, math.isfinite(sanitized.GravityResistanceScalar) ? sanitized.GravityResistanceScalar : SumpPumpPipeGridConstants.DefaultGravityResistanceScalar);
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
                DeltaSmoothingFactor = SumpPumpPipeGridConstants.DefaultDeltaSmoothingFactor,
                MaxPumpRateScale = 1f,
                MaxPumpThroughputM3PerSecond = SumpPumpPipeGridConstants.DefaultMaxPumpRateM3PerSecond,
                GravityAssistScalar = SumpPumpPipeGridConstants.DefaultGravityAssistScalar,
                GravityResistanceScalar = SumpPumpPipeGridConstants.DefaultGravityResistanceScalar,
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

        private static float ResolveSolveCadenceSeconds(float quality)
        {
            float q = math.saturate(quality);
            float thermalCurve = math.smoothstep(0f, 1f, q);
            float lowPowerT = math.saturate((0.30f - q) * 3.3333333f);
            lowPowerT = lowPowerT * lowPowerT * (3f - 2f * lowPowerT);
            float lowPowerHold = lowPowerT * math.max(0f, 0.30f - q) * 0.12f;
            return math.lerp(0.5f, 0.1f, thermalCurve) + lowPowerHold;
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
            NativeArray<DrainageTelemetryEntry> summary = BorrowMutable(in _frameSummaryHandle);
            if (summary.IsCreated && summary.Length > 0)
            {
                DrainageTelemetryEntry entry = summary[0];
                entry.SolverWallMicroseconds = solverWallMicroseconds;
                entry.Flags |= SumpDrainageTelemetryFlags.ScheduleWindowTiming;
                if (solverWallMicroseconds > 500u)
                    entry.Flags |= SumpDrainageTelemetryFlags.SolverOverBudget;
                summary[0] = entry;
            }

            NativeArray<int> cursor = BorrowMutable(in _telemetryCursorHandle);
            NativeArray<DrainageTelemetryEntry> telemetry = BorrowMutable(in _telemetryHandle);
            if (!cursor.IsCreated || !telemetry.IsCreated || cursor.Length <= 0 || telemetry.Length <= 0)
                return;

            int capacity = math.min(telemetry.Length, SumpPumpPipeGridConstants.TelemetryFrameCount);
            int index = (cursor[0] - 1) % capacity;
            if (index < 0)
                index += capacity;
            DrainageTelemetryEntry ringEntry = telemetry[index];
            ringEntry.SolverWallMicroseconds = solverWallMicroseconds;
            ringEntry.Flags |= SumpDrainageTelemetryFlags.ScheduleWindowTiming;
            if (solverWallMicroseconds > 500u)
                ringEntry.Flags |= SumpDrainageTelemetryFlags.SolverOverBudget;
            telemetry[index] = ringEntry;
        }

        private void RecordTelemetryHeartbeat()
        {
            if (!_buffersReady || _solverScheduled || _mockSeedScheduled)
                return;

            NativeArray<DrainageTelemetryEntry> summary = BorrowMutable(in _frameSummaryHandle);
            NativeArray<int> cursor = BorrowMutable(in _telemetryCursorHandle);
            NativeArray<DrainageTelemetryEntry> telemetry = BorrowMutable(in _telemetryHandle);
            if (!summary.IsCreated || summary.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0 || !telemetry.IsCreated || telemetry.Length <= 0)
                return;

            DrainageTelemetryEntry entry = summary[0];
            uint baseHash = entry.StateHash != 0u ? entry.StateHash : SumpPumpPipeGridConstants.FnvOffset;
            float averagePressure = math.isfinite(entry.AveragePressure) ? entry.AveragePressure : 0f;
            float maxPressure = math.isfinite(entry.MaxPressure) ? entry.MaxPressure : 0f;
            float totalEvacuated = math.isfinite(entry.TotalEvacuatedM3) ? math.max(0f, entry.TotalEvacuatedM3) : 0f;
            float quality = ResolveGlobalQualityWeight();

            entry.FrameIndex = _frameIndex++;
            entry.StateHash = SumpPumpPipeGridValidation.MixHash(baseHash, entry.FrameIndex);
            entry.StateHash = SumpPumpPipeGridValidation.MixHash(entry.StateHash, math.asuint(averagePressure));
            entry.StateHash = SumpPumpPipeGridValidation.MixHash(entry.StateHash, math.asuint(totalEvacuated));
            entry.FrameEvacuatedM3 = 0f;
            entry.TotalEvacuatedM3 = totalEvacuated;
            entry.AveragePressure = averagePressure;
            entry.MaxPressure = math.max(0f, maxPressure);
            entry.GlobalQualityWeight = quality;
            entry.TotalPowerDrawWatts = math.isfinite(entry.TotalPowerDrawWatts) ? math.max(0f, entry.TotalPowerDrawWatts) : 0f;
            entry.SolverWallMicroseconds = 0u;
            entry.Flags &= ~(SumpDrainageTelemetryFlags.ScheduleWindowTiming | SumpDrainageTelemetryFlags.SolverOverBudget);
            entry.Flags |= SumpDrainageTelemetryFlags.HeartbeatFrame;
            summary[0] = entry;

            int capacity = math.min(telemetry.Length, SumpPumpPipeGridConstants.TelemetryFrameCount);
            int writeCursor = cursor[0];
            int index = writeCursor % capacity;
            if (index < 0)
                index += capacity;
            telemetry[index] = entry;
            cursor[0] = writeCursor + 1;
        }

        private DrainageTelemetryEntry ReadFrameSummary()
        {
            NativeArray<DrainageTelemetryEntry> summary = Read(in _frameSummaryHandle);
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

#if UNITY_EDITOR
        private bool TryCopyTelemetryTo(DrainageTelemetryEntry[] target, out int count)
        {
            count = 0;
            if (target == null || target.Length <= 0 || !_buffersReady || _solverScheduled)
                return false;

            NativeArray<DrainageTelemetryEntry> telemetry = Read(in _telemetryHandle);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            count = math.min(target.Length, telemetry.Length);
            for (int i = 0; i < count; i++)
                target[i] = telemetry[i];
            return count > 0;
        }
#endif

        private bool TryReadTuning(out DrainageTuningDTO tuning)
        {
            if (!_buffersReady)
            {
                tuning = s_offlineTuning;
                return false;
            }

            NativeArray<DrainageTuningDTO> tuningArray = Read(in _tuningHandle);
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
            if (!_buffersReady)
                return false;
            if (_solverScheduled)
                return false;

            if (!_vault.TryLockBuffer(SumpPumpDrainageBufferIds.Tuning, OwnerSystem))
                return false;

            try
            {
                NativeArray<DrainageTuningDTO> tuningArray = BorrowMutable(in _tuningHandle);
                if (!tuningArray.IsCreated || tuningArray.Length <= 0)
                    return false;

                DrainageTuningDTO* tuningPtr = (DrainageTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
                UnsafeUtility.AsRef<DrainageTuningDTO>(tuningPtr) = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                _vault.TryUnlockBuffer(SumpPumpDrainageBufferIds.Tuning, OwnerSystem);
            }
        }

        private void ClearRuntimeScalarBuffers()
        {
            NativeArray<DrainageNodeDTO> pumps = BorrowMutable(in _pumpNodesHandle);
            NativeArray<float> pressureFront = BorrowMutable(in _pressureFrontHandle);
            NativeArray<float> pressureBack = BorrowMutable(in _pressureBackHandle);
            NativeArray<float> remainder = BorrowMutable(in _pumpRemainderHandle);
            NativeArray<float> massError = BorrowMutable(in _pumpMassErrorHandle);
            NativeArray<DrainageRoomDrainLock64> roomLocks = BorrowMutable(in _roomDrainLocksHandle);
            for (int i = 0; i < nodeCapacity; i++)
            {
                float seededPressure = pumps.IsCreated && i < pumps.Length
                    ? math.max(0f, pumps[i].HydraulicPressure)
                    : 0f;
                if (pressureFront.IsCreated && i < pressureFront.Length)
                    pressureFront[i] = seededPressure;
                if (pressureBack.IsCreated && i < pressureBack.Length)
                    pressureBack[i] = seededPressure;
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
            NativeArray<int> counters = BorrowMutable(in _countersHandle);
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
            NativeArray<DrainagePipeFlowGpuDTO> flowGpu = BorrowMutable(in _flowGpuHandle);
            int safeCount = math.min(math.max(0, validEdges), flowGpu.IsCreated ? flowGpu.Length : 0);
            if (safeCount <= 0 || !HasFlowGraphicsBuffers(safeCount))
                return;

            GraphicsBuffer target = AdvanceFlowWriteBuffer();
            if (target == null || !target.IsValid())
                return;

            NativeArray<DrainagePipeFlowGpuDTO> mapped = target.LockBufferForWrite<DrainagePipeFlowGpuDTO>(0, safeCount);
            try
            {
                void* dst = NativeArrayUnsafeUtility.GetUnsafePtr(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(flowGpu);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<DrainagePipeFlowGpuDTO>());
            }
            finally
            {
                target.UnlockBufferAfterWrite<DrainagePipeFlowGpuDTO>(safeCount);
            }
            Shader.SetGlobalBuffer(s_DrainagePipeEdgeFlowId, target);
            Shader.SetGlobalInt(s_DrainagePipeEdgeCountId, safeCount);
        }

        private void PublishConnectionSplineNodeFlow()
        {
            NativeArray<int> offsets = BorrowMutable(in _csrOffsetsHandle);
            NativeArray<float> flows = BorrowMutable(in _csrFlowHandle);
            NativeArray<int> counters = BorrowMutable(in _countersHandle);
            if (!offsets.IsCreated || !flows.IsCreated || !counters.IsCreated)
                return;

            int nodeCount = ResolveNodeCount(counters);
            DrainageTuningDTO tuning = ReadTuningOrDefault();
            float visualGain = math.max(0.001f, tuning.VisualFlowGain);
            float qualityCurve = math.smoothstep(0f, 1f, ResolveGlobalQualityWeight());
            int publishBudget = math.clamp((int)math.lerp(16f, math.max(16f, nodeCount), qualityCurve), 1, math.max(1, nodeCount));
            int stride = math.max(1, nodeCount / publishBudget);
            int startNode = stride > 1 ? (int)(_frameIndex % (uint)stride) : 0;
            for (int nodeIndex = startNode; nodeIndex < nodeCount; nodeIndex += stride)
            {
                int start = math.clamp(offsets[nodeIndex], 0, flows.Length);
                int end = math.clamp(offsets[nodeIndex + 1], start, flows.Length);
                float flow01 = 0f;
                for (int edgeIndex = start; edgeIndex < end; edgeIndex++)
                    flow01 = math.max(flow01, math.saturate(math.abs(flows[edgeIndex]) * visualGain));
                if (flow01 > 0.01f)
                    ConnectionSplineBatchRenderer.SetPipeNodeFlow((uint)nodeIndex, flow01);
            }
        }

        private DrainageTuningDTO ReadTuningOrDefault()
        {
            NativeArray<DrainageTuningDTO> tuning = Read(in _tuningHandle);
            return tuning.IsCreated && tuning.Length > 0 ? SanitizeTuning(tuning[0]) : s_offlineTuning;
        }

        private bool EnsureFlowGraphicsBuffers(int safeCount)
        {
            try
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
            catch
            {
                ReleaseGraphicsBuffer(ref _flowBufferA);
                ReleaseGraphicsBuffer(ref _flowBufferB);
                _flowBufferCapacity = 0;
                return false;
            }
        }

        private bool HasFlowGraphicsBuffers(int safeCount)
        {
            return _flowBufferA != null &&
                   _flowBufferB != null &&
                   _flowBufferCapacity >= safeCount &&
                   _flowBufferA.IsValid() &&
                   _flowBufferB.IsValid();
        }

        private static GraphicsBuffer CreateFlowGraphicsBuffer(int capacity)
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                capacity,
                UnsafeUtility.SizeOf<DrainagePipeFlowGpuDTO>());
        }

        private GraphicsBuffer AdvanceFlowWriteBuffer()
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
            _vault.ReleaseBuffer(in _pumpPowerNodeHashesHandle);
            _vault.ReleaseBuffer(in _pumpBaseMaxRateHandle);
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
            _pumpBaseMaxRateHandle = default;
            _pumpPowerNodeHashesHandle = default;
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

        private void RequestBlackBoxDumpOnce()
        {
            if (_blackBoxDumped)
                return;

            NativeArray<DrainageTelemetryEntry> telemetry = Read(in _telemetryHandle);
            if (!telemetry.IsCreated || telemetry.Length <= 0 || _dumpBytes == null || _dumpSignal == null)
                return;

            try
            {
                NativeArray<int> telemetryCursor = Read(in _telemetryCursorHandle);
                int capacity = math.min(telemetry.Length, SumpPumpPipeGridConstants.TelemetryFrameCount);
                int writeCount = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? math.max(0, telemetryCursor[0]) : capacity;
                int validCount = math.min(capacity, writeCount);
                int oldestIndex = capacity > 0 && writeCount > capacity ? writeCount % capacity : 0;
                uint aggregateFlags = 0u;
                for (int i = 0; i < validCount; i++)
                    aggregateFlags |= telemetry[(oldestIndex + i) % capacity].Flags;

                DrainageDumpHeader header = new DrainageDumpHeader
                {
                    Magic = DumpMagic,
                    EntryCount = (uint)validCount,
                    StructSizeBytes = (uint)UnsafeUtility.SizeOf<DrainageTelemetryEntry>(),
                    Version = DumpVersion,
                    Capacity = (uint)capacity,
                    WriteCount = (uint)writeCount,
                    OldestIndex = (uint)oldestIndex,
                    RuntimeHash = RuntimeHash,
                    Flags = aggregateFlags
                };

                int headerBytes = UnsafeUtility.SizeOf<DrainageDumpHeader>();
                int rowBytes = UnsafeUtility.SizeOf<DrainageTelemetryEntry>();
                int requiredBytes = headerBytes + (validCount * rowBytes);
                if (requiredBytes <= headerBytes || requiredBytes > _dumpBytes.Length)
                    return;

                fixed (byte* dumpPtr = _dumpBytes)
                {
                    byte* cursor = dumpPtr;
                    UnsafeUtility.MemCpy(cursor, UnsafeUtility.AddressOf(ref header), headerBytes);
                    cursor += headerBytes;
                    for (int i = 0; i < validCount; i++)
                    {
                        int telemetryIndex = (oldestIndex + i) % capacity;
                        DrainageTelemetryEntry entry = telemetry[telemetryIndex];
                        UnsafeUtility.MemCpy(cursor, UnsafeUtility.AddressOf(ref entry), rowBytes);
                        cursor += rowBytes;
                    }
                }

                _blackBoxDumped = true;
                Volatile.Write(ref _dumpByteCount, requiredBytes);
                Interlocked.Exchange(ref _dumpPending, 1);
                _dumpSignal.Set();
            }
            catch
            {
                Interlocked.Exchange(ref _dumpWriteFault, 1);
            }
        }

        private void InitializeDumpWriterCold()
        {
            if (_dumpSignal != null)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                _dumpPath = Path.Combine(projectRoot, DumpRelativePath);
                string directory = Path.GetDirectoryName(_dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                int byteCapacity = UnsafeUtility.SizeOf<DrainageDumpHeader>() +
                                   (SumpPumpPipeGridConstants.TelemetryFrameCount * UnsafeUtility.SizeOf<DrainageTelemetryEntry>());
                _dumpBytes = new byte[byteCapacity];
                _dumpThreadStop = 0;
                _dumpPending = 0;
                _dumpByteCount = 0;
                _dumpWriteFault = 0;
                _dumpSignal = new AutoResetEvent(false);
                _dumpThread = new Thread(DumpWriterLoop)
                {
                    IsBackground = true,
                    Name = "SHINOBU_340_BlackBox"
                };
                _dumpThread.Start();
            }
            catch
            {
                _dumpPath = null;
                _dumpBytes = null;
                _dumpSignal = null;
                _dumpThread = null;
                _dumpWriteFault = 1;
            }
        }

        private void ShutdownDumpWriterCold()
        {
            AutoResetEvent signal = _dumpSignal;
            Thread thread = _dumpThread;
            if (signal == null)
                return;

            Interlocked.Exchange(ref _dumpThreadStop, 1);
            signal.Set();
            if (thread == null)
            {
                signal.Dispose();
                _dumpSignal = null;
                return;
            }

            if (thread.Join(50))
            {
                signal.Dispose();
                _dumpSignal = null;
                _dumpThread = null;
            }
        }

        private void DumpWriterLoop()
        {
            while (true)
            {
                AutoResetEvent signal = _dumpSignal;
                if (signal == null)
                    return;

                signal.WaitOne();
                if (Volatile.Read(ref _dumpThreadStop) != 0 && Volatile.Read(ref _dumpPending) == 0)
                    return;

                if (Interlocked.Exchange(ref _dumpPending, 0) == 0)
                    continue;

                int byteCount = Volatile.Read(ref _dumpByteCount);
                if (byteCount <= 0 || _dumpBytes == null || string.IsNullOrEmpty(_dumpPath))
                {
                    Interlocked.Exchange(ref _dumpWriteFault, 1);
                    continue;
                }

                try
                {
                    using (FileStream stream = new FileStream(_dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                        stream.Write(_dumpBytes, 0, math.min(byteCount, _dumpBytes.Length));
                }
                catch
                {
                    Interlocked.Exchange(ref _dumpWriteFault, 1);
                }

                if (Volatile.Read(ref _dumpThreadStop) != 0)
                    return;
            }
        }

#if UNITY_EDITOR
        public struct PressureDebugNode
        {
            public double3 Aup;
            public float Pressure;
            public uint Flags;
        }

        public struct PressureDebugEdge
        {
            public int SourceIndex;
            public int DestinationIndex;
            public float Flow01;
            public uint Flags;
        }

        public static bool TryCopyPressureDebugSnapshot(
            PressureDebugNode[] nodeSink,
            PressureDebugEdge[] edgeSink,
            out int nodeCount,
            out int edgeCount)
        {
            nodeCount = 0;
            edgeCount = 0;
            SumpPumpPipeGridRuntime runtime = s_active;
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   runtime.CopyPressureDebugSnapshot(nodeSink, edgeSink, out nodeCount, out edgeCount);
        }

        private bool CopyPressureDebugSnapshot(
            PressureDebugNode[] nodeSink,
            PressureDebugEdge[] edgeSink,
            out int nodeCount,
            out int edgeCount)
        {
            nodeCount = 0;
            edgeCount = 0;
            if (!_buffersReady || _solverScheduled || _mockSeedScheduled || nodeSink == null || edgeSink == null)
                return false;

            NativeArray<DrainageNodeDTO> nodes = Read(in _pumpNodesHandle);
            NativeArray<PipeEdgeDTO> edges = Read(in _pipeEdgesHandle);
            NativeArray<double3> aup = Read(in _nodeAupHandle);
            NativeArray<float> pressure = _pressureFrontIsA ? Read(in _pressureFrontHandle) : Read(in _pressureBackHandle);
            NativeArray<int> counters = Read(in _countersHandle);
            if (!nodes.IsCreated || !edges.IsCreated || !aup.IsCreated || !pressure.IsCreated || !counters.IsCreated)
                return false;

            int resolvedNodeCount = math.min(ResolveNodeCount(counters), math.min(nodes.Length, math.min(aup.Length, pressure.Length)));
            resolvedNodeCount = math.min(math.max(0, resolvedNodeCount), nodeSink.Length);
            if (resolvedNodeCount <= 0)
                return false;

            for (int i = 0; i < resolvedNodeCount; i++)
            {
                DrainageNodeDTO node = nodes[i];
                nodeSink[i] = new PressureDebugNode
                {
                    Aup = aup[i],
                    Pressure = math.max(0f, math.isfinite(pressure[i]) ? pressure[i] : 0f),
                    Flags = node.Flags
                };
            }

            DrainageTuningDTO tuning = ReadTuningOrDefault();
            float visualFlowGain = math.max(0.001f, tuning.VisualFlowGain);
            int resolvedEdgeCount = math.min(ResolveEdgeCount(counters), math.min(edges.Length, edgeSink.Length));
            for (int i = 0; i < resolvedEdgeCount; i++)
            {
                PipeEdgeDTO edge = edges[i];
                edgeSink[i] = new PressureDebugEdge
                {
                    SourceIndex = edge.SourceNodeIndex,
                    DestinationIndex = edge.DestinationNodeIndex,
                    Flow01 = math.saturate(math.abs(edge.CurrentFlow) * visualFlowGain),
                    Flags = edge.Flags
                };
            }

            nodeCount = resolvedNodeCount;
            edgeCount = resolvedEdgeCount;
            return true;
        }

        private void OnDrawGizmos()
        {
            if (!_buffersReady || _solverScheduled)
                return;

            NativeArray<PipeEdgeDTO> edges = Read(in _pipeEdgesHandle);
            NativeArray<double3> aup = Read(in _nodeAupHandle);
            NativeArray<float> pressure = _pressureFrontIsA ? Read(in _pressureFrontHandle) : Read(in _pressureBackHandle);
            NativeArray<int> counters = Read(in _countersHandle);
            if (!edges.IsCreated || !aup.IsCreated || !pressure.IsCreated || !counters.IsCreated || aup.Length <= 0)
                return;

            int edgeCount = ResolveEdgeCount(counters);
            double3 origin = aup[0];
            DrainageTuningDTO tuning = ReadTuningOrDefault();
            float visualFlowGain = math.max(0.001f, tuning.VisualFlowGain);
            for (int edgeIndex = 0; edgeIndex < edgeCount && edgeIndex < edges.Length; edgeIndex++)
            {
                PipeEdgeDTO edge = edges[edgeIndex];
                if ((edge.Flags & SumpPipeEdgeFlags.Active) == 0u ||
                    (uint)edge.SourceNodeIndex >= (uint)aup.Length ||
                    (uint)edge.DestinationNodeIndex >= (uint)aup.Length)
                {
                    continue;
                }

                float flow01 = math.saturate(math.abs(edge.CurrentFlow) * visualFlowGain);
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
