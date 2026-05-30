using System;
using System.Diagnostics;
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
        private const int MinDrainageDeltaPassCount = 1;
        private const int MaxDrainageDeltaPassCount = 4;
        private const uint RuntimeHash = 0x53333430u;
        private const ulong DrainageVaultMutationGuardMask = 0x00000000FFFFF2FFUL;
        private const ulong SolverPinPumpNodes = 1UL << 0;
        private const ulong SolverPinPipeEdges = 1UL << 1;
        private const ulong SolverPinNodeAup = 1UL << 2;
        private const ulong SolverPinPumpRoomIndices = 1UL << 3;
        private const ulong SolverPinCsrOffsets = 1UL << 4;
        private const ulong SolverPinCsrDestinations = 1UL << 5;
        private const ulong SolverPinCsrConductance = 1UL << 6;
        private const ulong SolverPinCsrFlow = 1UL << 7;
        private const ulong SolverPinCsrFlatEdgeIndex = 1UL << 8;
        private const ulong SolverPinCsrWriteCursor = 1UL << 9;
        private const ulong SolverPinPressureFront = 1UL << 10;
        private const ulong SolverPinPressureBack = 1UL << 11;
        private const ulong SolverPinPowerPotential = 1UL << 12;
        private const ulong SolverPinPumpBaseMaxRate = 1UL << 13;
        private const ulong SolverPinPumpPowerNodeHashes = 1UL << 14;
        private const ulong SolverPinPumpRemainder = 1UL << 15;
        private const ulong SolverPinPumpMassError = 1UL << 16;
        private const ulong SolverPinRoomDrainLocks = 1UL << 17;
        private const ulong SolverPinTuning = 1UL << 18;
        private const ulong SolverPinTelemetry = 1UL << 19;
        private const ulong SolverPinTelemetryCursor = 1UL << 20;
        private const ulong SolverPinCounters = 1UL << 21;
        private const ulong SolverPinFrameSummary = 1UL << 22;
        private const ulong SolverPinFlowGpu = 1UL << 23;
        private const ulong SolverPinFluidFront = 1UL << 24;
        private const ulong SolverPinFluidBack = 1UL << 25;
        private const ulong SolverPinPowerNodes = 1UL << 26;
        private const ulong SolverPinPowerPotentialFront = 1UL << 27;

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
        private VaultGenerationHandle<DrainageTelemetryEntry> _frameSummaryHandle;
        private VaultGenerationHandle<DrainagePipeFlowGpuDTO> _flowGpuHandle;

        private JobHandle _solverHandle;
        private JobHandle _mockSeedHandle;
        private GraphicsBuffer _flowBufferA;
        private GraphicsBuffer _flowBufferB;
        private ulong _activeMutationGuardMask;
        private ulong _solverBufferPinMask;
        private IDataVault _activeMutationGuardVault;
        private IDataVault _solverBufferPinVault;
        private long _solverScheduleTimestamp;
        private uint _frameIndex;
        private uint _blackBoxStateHash;
        private uint _blackBoxFlags;
        private int _blackBoxEntryCount;
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

        public static bool TryGetLastBlackBoxSummary(out uint stateHash, out uint flags, out int entryCount)
        {
            SumpPumpPipeGridRuntime runtime = s_active;
            if (runtime != null && runtime._blackBoxDumped)
            {
                stateHash = runtime._blackBoxStateHash;
                flags = runtime._blackBoxFlags;
                entryCount = runtime._blackBoxEntryCount;
                return true;
            }

            stateHash = 0u;
            flags = 0u;
            entryCount = 0;
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
            BindDataVaultForLifecycle(GlobalRegistry.DataVault);
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
            ReleaseDrainageMutationGuard();
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

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                CompleteMockSeedForTeardown();
                CompleteScheduledSolverForTeardown();
                ReleaseDrainageMutationGuard();
                BindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);
                _buffersReady = _vault != null && TryInitializeBuffers();
                if (_buffersReady && generateMockOnEnable)
                    GenerateMockDrainageNetwork();
            }
        }

        private void BindDataVaultForLifecycle(IDataVault nextVault)
        {
            if (ReferenceEquals(_vault, nextVault))
                return;

            ReleaseOwnedBuffers();
            _vault = nextVault;
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

        /// <summary>Authority drainage cadence. No object pump or water-particle state is read here.</summary>
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
            if (ScheduleDrainageSolve(deltaTime, quality))
                _solveAccumulator = 0f;
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
                if (TryAcquireTelemetryMutationGuard())
                {
                    try
                    {
                        StampSolverWallTime(ResolveElapsedMicroseconds(_solverScheduleTimestamp));
                    }
                    finally
                    {
                        ReleaseDrainageMutationGuard();
                    }
                }

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

            Span<PipeProfileDTO> profileScratch = stackalloc PipeProfileDTO[SumpPumpPipeGridConstants.MaxPipeProfiles];
            if (!SumpPumpPipeGridValidation.TryParsePipeProfilesCsv(csvBytes, profileScratch, out profileCount))
                return false;

            if (!TryAcquireLocalDrainageMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return false;

            try
            {
                if (!TryBorrowMutable(in _profilesHandle, SumpPumpDrainageBufferIds.PipeProfiles, out NativeArray<PipeProfileDTO> profiles))
                    return false;

                CommitPipeProfileScratch(profileScratch, profileCount, profiles);
                return true;
            }
            finally
            {
                ReleaseLocalDrainageMutationGuard(guardVault, guardMask);
            }
        }
#endif

        private static void CommitPipeProfileScratch(
            ReadOnlySpan<PipeProfileDTO> source,
            int profileCount,
            NativeArray<PipeProfileDTO> profiles)
        {
            if (!profiles.IsCreated)
                return;

            int count = math.clamp(profileCount, 0, math.min(source.Length, profiles.Length));
            for (int i = 0; i < count; i++)
                profiles[i] = source[i];
            for (int i = count; i < profiles.Length; i++)
                profiles[i] = default;
        }

        /// <summary>Rebuilds the deterministic 2000-node / 6000-edge mock drainage topology in Vault buffers.</summary>
        public void GenerateMockDrainageNetwork()
        {
            if (_mockSeedScheduled || _solverScheduled)
                return;

            if (!_buffersReady)
                return;

            ReleaseDrainageMutationGuard();
            if (!TryAcquireDrainageMutationGuard())
                return;

            bool scheduled = false;
            try
            {
                if (!TryBorrowMutable(in _pumpNodesHandle, SumpPumpDrainageBufferIds.PumpNodes, out NativeArray<DrainageNodeDTO> pumps) ||
                    !TryBorrowMutable(in _pipeEdgesHandle, SumpPumpDrainageBufferIds.PipeEdges, out NativeArray<PipeEdgeDTO> edges) ||
                    !TryBorrowMutable(in _nodeAupHandle, SumpPumpDrainageBufferIds.NodeAup, out NativeArray<double3> nodeAup) ||
                    !TryBorrowMutable(in _pumpRoomIndicesHandle, SumpPumpDrainageBufferIds.PumpRoomIndices, out NativeArray<int> roomIndices) ||
                    !TryBorrowMutable(in _powerPotentialHandle, SumpPumpDrainageBufferIds.PowerPotential, out NativeArray<float> power) ||
                    !TryBorrowMutable(in _pumpBaseMaxRateHandle, SumpPumpDrainageBufferIds.PumpBaseMaxRate, out NativeArray<float> baseRates) ||
                    !TryBorrowMutable(in _pumpPowerNodeHashesHandle, SumpPumpDrainageBufferIds.PumpPowerNodeHashes, out NativeArray<uint> powerNodeHashes) ||
                    !TryBorrowMutable(in _countersHandle, SumpPumpDrainageBufferIds.Counters, out NativeArray<int> counters) ||
                    !TryBorrowMutable(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, out NativeArray<DrainageTuningDTO> tuning) ||
                    !pumps.IsCreated || !edges.IsCreated || !nodeAup.IsCreated || !roomIndices.IsCreated || !power.IsCreated || !baseRates.IsCreated || !powerNodeHashes.IsCreated || !counters.IsCreated || !tuning.IsCreated)
                {
                    return;
                }

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
                    ReleaseDrainageMutationGuard();
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
                !SumpPumpPipeGridValidation.ValidateDrainagePipeFlowGpuLayout())
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

        private bool TryBorrowMutable<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return _vault != null &&
                   IsDrainageVaultHandle(in handle, expectedBufferId) &&
                   _vault.TryResolveHandle(in handle, out buffer);
        }

        private bool TryRead<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return _vault != null &&
                   IsDrainageVaultHandle(in handle, expectedBufferId) &&
                   _vault.TryReadHandle(in handle, out buffer);
        }

        private bool ValidateOwnedBuffers()
        {
            int safeNodes = math.max(1, nodeCapacity);
            int safeEdges = math.max(1, edgeCapacity);
            int safeRoomLocks = math.max(safeNodes, HabitatFluidIncursionConstants.MaxCompartments);
            return HasResolvedBuffer(in _pumpNodesHandle, SumpPumpDrainageBufferIds.PumpNodes, safeNodes) &&
                   HasResolvedBuffer(in _pipeEdgesHandle, SumpPumpDrainageBufferIds.PipeEdges, safeEdges) &&
                   HasResolvedBuffer(in _nodeAupHandle, SumpPumpDrainageBufferIds.NodeAup, safeNodes) &&
                   HasResolvedBuffer(in _pumpRoomIndicesHandle, SumpPumpDrainageBufferIds.PumpRoomIndices, safeNodes) &&
                   HasResolvedBuffer(in _csrOffsetsHandle, SumpPumpDrainageBufferIds.CsrOffsets, safeNodes + 1) &&
                   HasResolvedBuffer(in _csrDestinationsHandle, SumpPumpDrainageBufferIds.CsrDestinations, safeEdges) &&
                   HasResolvedBuffer(in _csrConductanceHandle, SumpPumpDrainageBufferIds.CsrConductance, safeEdges) &&
                   HasResolvedBuffer(in _csrFlowHandle, SumpPumpDrainageBufferIds.CsrFlow, safeEdges) &&
                   HasResolvedBuffer(in _csrFlatEdgeIndexHandle, SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, safeEdges) &&
                   HasResolvedBuffer(in _csrWriteCursorHandle, SumpPumpDrainageBufferIds.CsrWriteCursor, safeNodes) &&
                   HasResolvedBuffer(in _pressureFrontHandle, SumpPumpDrainageBufferIds.PressureFront, safeNodes) &&
                   HasResolvedBuffer(in _pressureBackHandle, SumpPumpDrainageBufferIds.PressureBack, safeNodes) &&
                   HasResolvedBuffer(in _powerPotentialHandle, SumpPumpDrainageBufferIds.PowerPotential, safeNodes) &&
                   HasResolvedBuffer(in _pumpBaseMaxRateHandle, SumpPumpDrainageBufferIds.PumpBaseMaxRate, safeNodes) &&
                   HasResolvedBuffer(in _pumpPowerNodeHashesHandle, SumpPumpDrainageBufferIds.PumpPowerNodeHashes, safeNodes) &&
                   HasResolvedBuffer(in _pumpRemainderHandle, SumpPumpDrainageBufferIds.PumpRemainder, safeNodes) &&
                   HasResolvedBuffer(in _pumpMassErrorHandle, SumpPumpDrainageBufferIds.PumpMassError, safeNodes) &&
                   HasResolvedBuffer(in _roomDrainLocksHandle, SumpPumpDrainageBufferIds.RoomDrainLocks, safeRoomLocks) &&
                   HasResolvedBuffer(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, 1) &&
                   HasResolvedBuffer(in _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing, SumpPumpPipeGridConstants.TelemetryFrameCount) &&
                   HasResolvedBuffer(in _telemetryCursorHandle, SumpPumpDrainageBufferIds.TelemetryCursor, 1) &&
                   HasResolvedBuffer(in _countersHandle, SumpPumpDrainageBufferIds.Counters, SumpPumpPipeGridConstants.CounterCount) &&
                   HasResolvedBuffer(in _profilesHandle, SumpPumpDrainageBufferIds.PipeProfiles, SumpPumpPipeGridConstants.MaxPipeProfiles) &&
                   HasResolvedBuffer(in _frameSummaryHandle, SumpPumpDrainageBufferIds.FrameSummary, 1) &&
                   HasResolvedBuffer(in _flowGpuHandle, SumpPumpDrainageBufferIds.FlowGpu, safeEdges);
        }

        private bool HasResolvedBuffer<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId, int minLength) where T : struct
        {
            if (_vault == null ||
                minLength <= 0 ||
                !IsDrainageVaultHandle(in handle, expectedBufferId))
                return false;

            return _vault.TryReadHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= minLength;
        }

        private static bool IsDrainageVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystem &&
                   handle.Generation != 0u;
        }

        private bool ScheduleDrainageSolve(float deltaTime, float quality)
        {
            if (!TryLockDrainageSolverBuffers())
                return false;

            if (!TryBorrowMutable(in _pumpNodesHandle, SumpPumpDrainageBufferIds.PumpNodes, out NativeArray<DrainageNodeDTO> pumps) ||
                !TryBorrowMutable(in _pipeEdgesHandle, SumpPumpDrainageBufferIds.PipeEdges, out NativeArray<PipeEdgeDTO> edges) ||
                !TryBorrowMutable(in _nodeAupHandle, SumpPumpDrainageBufferIds.NodeAup, out NativeArray<double3> nodeAup) ||
                !TryBorrowMutable(in _pumpRoomIndicesHandle, SumpPumpDrainageBufferIds.PumpRoomIndices, out NativeArray<int> roomIndices) ||
                !TryBorrowMutable(in _csrOffsetsHandle, SumpPumpDrainageBufferIds.CsrOffsets, out NativeArray<int> csrOffsets) ||
                !TryBorrowMutable(in _csrDestinationsHandle, SumpPumpDrainageBufferIds.CsrDestinations, out NativeArray<int> csrDestinations) ||
                !TryBorrowMutable(in _csrConductanceHandle, SumpPumpDrainageBufferIds.CsrConductance, out NativeArray<float> csrConductance) ||
                !TryBorrowMutable(in _csrFlowHandle, SumpPumpDrainageBufferIds.CsrFlow, out NativeArray<float> csrFlow) ||
                !TryBorrowMutable(in _csrFlatEdgeIndexHandle, SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, out NativeArray<int> csrFlatEdgeIndex) ||
                !TryBorrowMutable(in _csrWriteCursorHandle, SumpPumpDrainageBufferIds.CsrWriteCursor, out NativeArray<int> csrWriteCursor) ||
                !TryBorrowMutable(in _pressureFrontHandle, SumpPumpDrainageBufferIds.PressureFront, out NativeArray<float> pressureFront) ||
                !TryBorrowMutable(in _pressureBackHandle, SumpPumpDrainageBufferIds.PressureBack, out NativeArray<float> pressureBack) ||
                !TryBorrowMutable(in _powerPotentialHandle, SumpPumpDrainageBufferIds.PowerPotential, out NativeArray<float> powerPotential) ||
                !TryBorrowMutable(in _pumpBaseMaxRateHandle, SumpPumpDrainageBufferIds.PumpBaseMaxRate, out NativeArray<float> pumpBaseMaxRate) ||
                !TryBorrowMutable(in _pumpPowerNodeHashesHandle, SumpPumpDrainageBufferIds.PumpPowerNodeHashes, out NativeArray<uint> pumpPowerNodeHashes) ||
                !TryBorrowMutable(in _pumpRemainderHandle, SumpPumpDrainageBufferIds.PumpRemainder, out NativeArray<float> pumpRemainder) ||
                !TryBorrowMutable(in _pumpMassErrorHandle, SumpPumpDrainageBufferIds.PumpMassError, out NativeArray<float> pumpMassError) ||
                !TryBorrowMutable(in _roomDrainLocksHandle, SumpPumpDrainageBufferIds.RoomDrainLocks, out NativeArray<DrainageRoomDrainLock64> roomDrainLocks) ||
                !TryBorrowMutable(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, out NativeArray<DrainageTuningDTO> tuning) ||
                !TryBorrowMutable(in _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing, out NativeArray<DrainageTelemetryEntry> telemetry) ||
                !TryBorrowMutable(in _telemetryCursorHandle, SumpPumpDrainageBufferIds.TelemetryCursor, out NativeArray<int> telemetryCursor) ||
                !TryBorrowMutable(in _countersHandle, SumpPumpDrainageBufferIds.Counters, out NativeArray<int> counters) ||
                !TryBorrowMutable(in _frameSummaryHandle, SumpPumpDrainageBufferIds.FrameSummary, out NativeArray<DrainageTelemetryEntry> frameSummary) ||
                !TryBorrowMutable(in _flowGpuHandle, SumpPumpDrainageBufferIds.FlowGpu, out NativeArray<DrainagePipeFlowGpuDTO> flowGpu) ||
                !pumps.IsCreated || !edges.IsCreated || !nodeAup.IsCreated || !roomIndices.IsCreated ||
                !csrOffsets.IsCreated || !csrDestinations.IsCreated || !csrConductance.IsCreated ||
                !csrFlow.IsCreated || !csrFlatEdgeIndex.IsCreated || !csrWriteCursor.IsCreated ||
                !pressureFront.IsCreated || !pressureBack.IsCreated || !powerPotential.IsCreated || !pumpBaseMaxRate.IsCreated || !pumpPowerNodeHashes.IsCreated ||
                !pumpRemainder.IsCreated || !pumpMassError.IsCreated || !roomDrainLocks.IsCreated || !tuning.IsCreated || !telemetry.IsCreated ||
                !telemetryCursor.IsCreated || !counters.IsCreated || !frameSummary.IsCreated || !flowGpu.IsCreated)
            {
                ReleaseDrainageSolverBufferPins();
                return false;
            }

            int nodeCount = ResolveNodeCount(counters);
            int edgeCount = ResolveEdgeCount(counters);
            bool scheduled = false;
            bool hasPendingJob = false;
            JobHandle pendingJob = default;
            try
            {
                uint telemetryFlags = SumpDrainageTelemetryFlags.None;
                bool hasFluidFront = TryLockAndReadExistingBuffer(BufferID.ShinobuFluidCompartmentFront, SolverPinFluidFront, out NativeArray<FluidCompartmentDTO> fluidFront);
                bool hasFluidBack = TryLockAndBorrowExistingBuffer(BufferID.ShinobuFluidCompartmentBack, SolverPinFluidBack, out NativeArray<FluidCompartmentDTO> fluidBack);
                bool hasPowerNodes = TryLockAndReadExistingBuffer(Hecton8.Power.PowerGridBufferIds.Nodes, SolverPinPowerNodes, out NativeArray<Hecton8.Power.PowerNodeDTO> powerNodes);
                bool hasPowerPotential = TryLockAndReadExistingBuffer(Hecton8.Power.PowerGridBufferIds.PotentialFront, SolverPinPowerPotentialFront, out NativeArray<float> powerPotentialFront);
                int compartmentCount = hasFluidFront && hasFluidBack ? math.min(fluidFront.Length, fluidBack.Length) : 0;
                if (compartmentCount <= 0)
                {
                    telemetryFlags |= SumpDrainageTelemetryFlags.MissingFluidVault;
                    ReleaseDrainageSolverBufferPin(BufferID.ShinobuFluidCompartmentBack, SolverPinFluidBack);
                    ReleaseDrainageSolverBufferPin(BufferID.ShinobuFluidCompartmentFront, SolverPinFluidFront);
                }
                if (!hasPowerNodes || !hasPowerPotential)
                {
                    telemetryFlags |= SumpDrainageTelemetryFlags.MissingPowerVault;
                    ReleaseDrainageSolverBufferPin(Hecton8.Power.PowerGridBufferIds.PotentialFront, SolverPinPowerPotentialFront);
                    ReleaseDrainageSolverBufferPin(Hecton8.Power.PowerGridBufferIds.Nodes, SolverPinPowerNodes);
                    powerNodes = default;
                    powerPotentialFront = default;
                }

                ResetFrameCounters(counters, nodeCount, edgeCount);
                DrainageTuningDTO activeTuning = RefreshTuning(tuning, deltaTime, quality, nodeCount, edgeCount);
                int deltaPassCount = ResolveDrainageDeltaPassCount(quality);
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
                    pendingJob = dependency;
                    hasPendingJob = true;
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
            pendingJob = dependency;
            hasPendingJob = true;

            for (int passIndex = 0; passIndex < deltaPassCount; passIndex++)
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
                pendingJob = dependency;
                hasPendingJob = true;
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
            pendingJob = dependency;
            hasPendingJob = true;

            if (compartmentCount > 0)
            {
                ClearDrainageRoomLocksJob clearLocksJob = new ClearDrainageRoomLocksJob
                {
                    RoomDrainLocks = roomDrainLocks,
                    Count = compartmentCount
                };
                dependency = clearLocksJob.Schedule(compartmentCount, 64, dependency);
                pendingJob = dependency;
                hasPendingJob = true;

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
                pendingJob = dependency;
                hasPendingJob = true;
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
                pendingJob = _solverHandle;
                hasPendingJob = true;
                H8Memory.RegisterActiveJob(OwnerSystem, _solverHandle);
                _solverScheduled = true;
                _pressureFrontIsA = nextFrontIsA;
                _solverScheduleTimestamp = Stopwatch.GetTimestamp();
                _flowUploadDirty = true;
                _frameIndex++;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                {
                    if (hasPendingJob)
                        DispatcherJobFence.TryComplete(ref pendingJob, forceComplete: true);

                    ReleaseDrainageSolverBufferPins();
                }
            }

            return scheduled;
        }

        private bool TryFinalizeScheduledSolverNoWait()
        {
            if (!_solverScheduled)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _solverHandle))
                return false;

            _solverScheduled = false;
            ReleaseDrainageSolverBufferPins();
            return true;
        }

        private bool TryFinalizeMockSeedNoWait()
        {
            if (!_mockSeedScheduled)
                return true;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _mockSeedHandle))
                return false;

            try
            {
                ClearRuntimeScalarBuffers();
                _topologyDirty = true;
                _pressureFrontIsA = true;
                _mockSeedScheduled = false;
            }
            finally
            {
                ReleaseDrainageMutationGuard();
            }

            return true;
        }

        private void CompleteMockSeedForTeardown()
        {
            if (!_mockSeedScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _mockSeedHandle, forceComplete: true))
                return;

            try
            {
                ClearRuntimeScalarBuffers();
                _topologyDirty = true;
                _pressureFrontIsA = true;
                _mockSeedScheduled = false;
            }
            finally
            {
                ReleaseDrainageMutationGuard();
            }
        }

        private void CompleteScheduledSolverForTeardown()
        {
            if (!_solverScheduled)
                return;

            if (!DispatcherJobFence.TryComplete(ref _solverHandle, forceComplete: true))
                return;

            try
            {
                _solverScheduled = false;
            }
            finally
            {
                ReleaseDrainageSolverBufferPins();
                ReleaseDrainageMutationGuard();
            }
        }

        private bool TryAcquireDrainageMutationGuard()
        {
            if (_activeMutationGuardMask != 0UL)
                return false;

            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireMutationGuard(DrainageVaultMutationGuardMask))
                return false;

            _activeMutationGuardMask = DrainageVaultMutationGuardMask;
            _activeMutationGuardVault = vault;
            return true;
        }

        private bool TryAcquireTelemetryMutationGuard()
        {
            return TryAcquireDrainageMutationGuard();
        }

        private bool TryAcquireLocalDrainageMutationGuard(out ulong guardMask, out IDataVault guardVault)
        {
            guardMask = 0UL;
            guardVault = null;
            IDataVault vault = _vault;
            if (vault == null || _activeMutationGuardMask != 0UL)
                return false;

            if (!vault.TryAcquireMutationGuard(DrainageVaultMutationGuardMask))
                return false;

            guardMask = DrainageVaultMutationGuardMask;
            guardVault = vault;
            return true;
        }

        private bool TryLockDrainageSolverBuffers()
        {
            if (_solverBufferPinMask != 0UL)
                return false;

            IDataVault vault = _vault;
            if (vault == null)
                return false;

            _solverBufferPinVault = vault;
            if (!TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PumpNodes, SolverPinPumpNodes) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PipeEdges, SolverPinPipeEdges) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.NodeAup, SolverPinNodeAup) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PumpRoomIndices, SolverPinPumpRoomIndices) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.CsrOffsets, SolverPinCsrOffsets) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.CsrDestinations, SolverPinCsrDestinations) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.CsrConductance, SolverPinCsrConductance) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.CsrFlow, SolverPinCsrFlow) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.CsrFlatEdgeIndex, SolverPinCsrFlatEdgeIndex) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.CsrWriteCursor, SolverPinCsrWriteCursor) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PressureFront, SolverPinPressureFront) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PressureBack, SolverPinPressureBack) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PowerPotential, SolverPinPowerPotential) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PumpBaseMaxRate, SolverPinPumpBaseMaxRate) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PumpPowerNodeHashes, SolverPinPumpPowerNodeHashes) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PumpRemainder, SolverPinPumpRemainder) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.PumpMassError, SolverPinPumpMassError) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.RoomDrainLocks, SolverPinRoomDrainLocks) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.Tuning, SolverPinTuning) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.TelemetryRing, SolverPinTelemetry) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.TelemetryCursor, SolverPinTelemetryCursor) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.Counters, SolverPinCounters) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.FrameSummary, SolverPinFrameSummary) ||
                !TryLockDrainageSolverBuffer(SumpPumpDrainageBufferIds.FlowGpu, SolverPinFlowGpu))
            {
                ReleaseDrainageSolverBufferPins();
                return false;
            }

            return true;
        }

        private bool TryLockDrainageSolverBuffer(BufferID bufferId, ulong pinBit)
        {
            IDataVault vault = _solverBufferPinVault;
            if (vault == null || bufferId == BufferID.Unknown)
                return false;

            if ((_solverBufferPinMask & pinBit) != 0UL)
                return true;

            if (!vault.TryLockBuffer(bufferId, OwnerSystem))
                return false;

            _solverBufferPinMask |= pinBit;
            return true;
        }

        private bool TryLockAndBorrowExistingBuffer<T>(BufferID bufferId, ulong pinBit, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _solverBufferPinVault;
            if (vault == null || !TryLockDrainageSolverBuffer(bufferId, pinBit))
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated)
            {
                return true;
            }

            ReleaseDrainageSolverBufferPin(bufferId, pinBit);
            buffer = default;
            return false;
        }

        private bool TryLockAndReadExistingBuffer<T>(BufferID bufferId, ulong pinBit, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _solverBufferPinVault;
            if (vault == null || !TryLockDrainageSolverBuffer(bufferId, pinBit))
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                vault.TryReadHandle(in handle, out buffer) &&
                buffer.IsCreated)
            {
                return true;
            }

            ReleaseDrainageSolverBufferPin(bufferId, pinBit);
            buffer = default;
            return false;
        }

        private void ReleaseDrainageSolverBufferPin(BufferID bufferId, ulong pinBit)
        {
            IDataVault vault = _solverBufferPinVault;
            if (vault == null || (_solverBufferPinMask & pinBit) == 0UL)
                return;

            _solverBufferPinMask &= ~pinBit;
            vault.TryUnlockBuffer(bufferId, OwnerSystem);
            if (_solverBufferPinMask == 0UL)
                _solverBufferPinVault = null;
        }

        private void ReleaseDrainageSolverBufferPins()
        {
            IDataVault vault = _solverBufferPinVault;
            ulong mask = _solverBufferPinMask;
            _solverBufferPinVault = null;
            _solverBufferPinMask = 0UL;
            if (vault == null || mask == 0UL)
                return;

            TryUnlockDrainageSolverPin(vault, mask, SolverPinPowerPotentialFront, Hecton8.Power.PowerGridBufferIds.PotentialFront);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPowerNodes, Hecton8.Power.PowerGridBufferIds.Nodes);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinFluidBack, BufferID.ShinobuFluidCompartmentBack);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinFluidFront, BufferID.ShinobuFluidCompartmentFront);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinFlowGpu, SumpPumpDrainageBufferIds.FlowGpu);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinFrameSummary, SumpPumpDrainageBufferIds.FrameSummary);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCounters, SumpPumpDrainageBufferIds.Counters);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinTelemetryCursor, SumpPumpDrainageBufferIds.TelemetryCursor);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinTelemetry, SumpPumpDrainageBufferIds.TelemetryRing);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinTuning, SumpPumpDrainageBufferIds.Tuning);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinRoomDrainLocks, SumpPumpDrainageBufferIds.RoomDrainLocks);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPumpMassError, SumpPumpDrainageBufferIds.PumpMassError);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPumpRemainder, SumpPumpDrainageBufferIds.PumpRemainder);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPumpPowerNodeHashes, SumpPumpDrainageBufferIds.PumpPowerNodeHashes);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPumpBaseMaxRate, SumpPumpDrainageBufferIds.PumpBaseMaxRate);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPowerPotential, SumpPumpDrainageBufferIds.PowerPotential);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPressureBack, SumpPumpDrainageBufferIds.PressureBack);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPressureFront, SumpPumpDrainageBufferIds.PressureFront);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCsrWriteCursor, SumpPumpDrainageBufferIds.CsrWriteCursor);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCsrFlatEdgeIndex, SumpPumpDrainageBufferIds.CsrFlatEdgeIndex);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCsrFlow, SumpPumpDrainageBufferIds.CsrFlow);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCsrConductance, SumpPumpDrainageBufferIds.CsrConductance);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCsrDestinations, SumpPumpDrainageBufferIds.CsrDestinations);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinCsrOffsets, SumpPumpDrainageBufferIds.CsrOffsets);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPumpRoomIndices, SumpPumpDrainageBufferIds.PumpRoomIndices);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinNodeAup, SumpPumpDrainageBufferIds.NodeAup);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPipeEdges, SumpPumpDrainageBufferIds.PipeEdges);
            TryUnlockDrainageSolverPin(vault, mask, SolverPinPumpNodes, SumpPumpDrainageBufferIds.PumpNodes);
        }

        private static void TryUnlockDrainageSolverPin(IDataVault vault, ulong mask, ulong pinBit, BufferID bufferId)
        {
            if ((mask & pinBit) != 0UL)
                vault.TryUnlockBuffer(bufferId, OwnerSystem);
        }

        private bool TryGuardAndBorrowMutableExistingBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null ||
                _activeMutationGuardMask == 0UL ||
                !_vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            if (_vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated)
                return true;

            buffer = default;
            return false;
        }

        private bool TryGuardAndReadExistingBuffer<T>(BufferID bufferId, out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (_vault == null ||
                _activeMutationGuardMask == 0UL ||
                !_vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle))
                return false;

            if (_vault.TryReadHandle(in handle, out buffer) && buffer.IsCreated)
                return true;

            buffer = default;
            return false;
        }

        private void ReleaseDrainageMutationGuard()
        {
            ulong guardMask = _activeMutationGuardMask;
            IDataVault vault = _activeMutationGuardVault;
            if (guardMask != 0UL)
                vault?.ReleaseMutationGuard(guardMask);
            _activeMutationGuardMask = 0UL;
            _activeMutationGuardVault = null;
        }

        private static void ReleaseLocalDrainageMutationGuard(IDataVault guardVault, ulong guardMask)
        {
            if (guardMask != 0UL)
                guardVault?.ReleaseMutationGuard(guardMask);
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
            tuning.DeltaPassCount = (ushort)ResolveDrainageDeltaPassCount(quality);
            tuningArray[0] = tuning;
            return tuning;
        }

        private void InitializeTuningIfNeeded()
        {
            if (!TryAcquireLocalDrainageMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return;

            try
            {
                if (!TryBorrowMutable(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, out NativeArray<DrainageTuningDTO> tuning) ||
                    !tuning.IsCreated ||
                    tuning.Length <= 0)
                    return;

                DrainageTuningDTO active = tuning[0];
                if (active.BasePipeConductance <= 0f || !math.isfinite(active.BasePipeConductance))
                    active = s_offlineTuning;
                tuning[0] = SanitizeTuning(active);
            }
            finally
            {
                ReleaseLocalDrainageMutationGuard(guardVault, guardMask);
            }
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

        private static int ResolveDrainageDeltaPassCount(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : SumpPumpPipeGridConstants.AuthoritativeQualityWeight);
            float curve = math.smoothstep(0f, 1f, q);
            int passCount = (int)math.round(math.lerp((float)MinDrainageDeltaPassCount, MaxDrainageDeltaPassCount, curve));
            return math.clamp(passCount, MinDrainageDeltaPassCount, MaxDrainageDeltaPassCount);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : SumpPumpPipeGridConstants.AuthoritativeQualityWeight);
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
            if (TryBorrowMutable(in _frameSummaryHandle, SumpPumpDrainageBufferIds.FrameSummary, out NativeArray<DrainageTelemetryEntry> summary) &&
                summary.IsCreated &&
                summary.Length > 0)
            {
                DrainageTelemetryEntry entry = summary[0];
                entry.SolverWallMicroseconds = solverWallMicroseconds;
                entry.Flags |= SumpDrainageTelemetryFlags.ScheduleWindowTiming;
                if (solverWallMicroseconds > 500u)
                    entry.Flags |= SumpDrainageTelemetryFlags.SolverOverBudget;
                summary[0] = entry;
            }

            if (!TryBorrowMutable(in _telemetryCursorHandle, SumpPumpDrainageBufferIds.TelemetryCursor, out NativeArray<int> cursor) ||
                !TryBorrowMutable(in _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing, out NativeArray<DrainageTelemetryEntry> telemetry) ||
                !cursor.IsCreated ||
                !telemetry.IsCreated ||
                cursor.Length <= 0 ||
                telemetry.Length <= 0)
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

            if (!TryAcquireTelemetryMutationGuard())
                return;

            try
            {
                if (!TryBorrowMutable(in _frameSummaryHandle, SumpPumpDrainageBufferIds.FrameSummary, out NativeArray<DrainageTelemetryEntry> summary) ||
                    !TryBorrowMutable(in _telemetryCursorHandle, SumpPumpDrainageBufferIds.TelemetryCursor, out NativeArray<int> cursor) ||
                    !TryBorrowMutable(in _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing, out NativeArray<DrainageTelemetryEntry> telemetry) ||
                    !summary.IsCreated ||
                    summary.Length <= 0 ||
                    !cursor.IsCreated ||
                    cursor.Length <= 0 ||
                    !telemetry.IsCreated ||
                    telemetry.Length <= 0)
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
            finally
            {
                ReleaseDrainageMutationGuard();
            }
        }

        private DrainageTelemetryEntry ReadFrameSummary()
        {
            return TryRead(in _frameSummaryHandle, SumpPumpDrainageBufferIds.FrameSummary, out NativeArray<DrainageTelemetryEntry> summary) &&
                   summary.IsCreated &&
                   summary.Length > 0
                ? summary[0]
                : default;
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

            if (!TryRead(in _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing, out NativeArray<DrainageTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
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

            if (!TryRead(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, out NativeArray<DrainageTuningDTO> tuningArray) ||
                !tuningArray.IsCreated ||
                tuningArray.Length <= 0)
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

            if (!TryAcquireLocalDrainageMutationGuard(out ulong guardMask, out IDataVault guardVault))
                return false;

            try
            {
                if (!TryBorrowMutable(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, out NativeArray<DrainageTuningDTO> tuningArray) ||
                    !tuningArray.IsCreated ||
                    tuningArray.Length <= 0)
                    return false;

                DrainageTuningDTO* tuningPtr = (DrainageTuningDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(tuningArray);
                UnsafeUtility.AsRef<DrainageTuningDTO>(tuningPtr) = SanitizeTuning(tuning);
                return true;
            }
            finally
            {
                ReleaseLocalDrainageMutationGuard(guardVault, guardMask);
            }
        }

        private void ClearRuntimeScalarBuffers()
        {
            TryBorrowMutable(in _pumpNodesHandle, SumpPumpDrainageBufferIds.PumpNodes, out NativeArray<DrainageNodeDTO> pumps);
            TryBorrowMutable(in _pressureFrontHandle, SumpPumpDrainageBufferIds.PressureFront, out NativeArray<float> pressureFront);
            TryBorrowMutable(in _pressureBackHandle, SumpPumpDrainageBufferIds.PressureBack, out NativeArray<float> pressureBack);
            TryBorrowMutable(in _pumpRemainderHandle, SumpPumpDrainageBufferIds.PumpRemainder, out NativeArray<float> remainder);
            TryBorrowMutable(in _pumpMassErrorHandle, SumpPumpDrainageBufferIds.PumpMassError, out NativeArray<float> massError);
            TryBorrowMutable(in _roomDrainLocksHandle, SumpPumpDrainageBufferIds.RoomDrainLocks, out NativeArray<DrainageRoomDrainLock64> roomLocks);
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
            TryRead(in _countersHandle, SumpPumpDrainageBufferIds.Counters, out NativeArray<int> counters);
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
            if (!TryRead(in _flowGpuHandle, SumpPumpDrainageBufferIds.FlowGpu, out NativeArray<DrainagePipeFlowGpuDTO> flowGpu) ||
                !flowGpu.IsCreated)
                return;

            int safeCount = math.min(math.max(0, validEdges), flowGpu.Length);
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
            if (!TryRead(in _csrOffsetsHandle, SumpPumpDrainageBufferIds.CsrOffsets, out NativeArray<int> offsets) ||
                !TryRead(in _csrFlowHandle, SumpPumpDrainageBufferIds.CsrFlow, out NativeArray<float> flows) ||
                !TryRead(in _countersHandle, SumpPumpDrainageBufferIds.Counters, out NativeArray<int> counters) ||
                !offsets.IsCreated ||
                !flows.IsCreated ||
                !counters.IsCreated)
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
            return TryRead(in _tuningHandle, SumpPumpDrainageBufferIds.Tuning, out NativeArray<DrainageTuningDTO> tuning) &&
                   tuning.IsCreated &&
                   tuning.Length > 0
                ? SanitizeTuning(tuning[0])
                : s_offlineTuning;
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
                ResetRuntimeStateForVaultRelease();
                return;
            }

            ReleaseOwnedHandle(ref _flowGpuHandle, SumpPumpDrainageBufferIds.FlowGpu);
            ReleaseOwnedHandle(ref _frameSummaryHandle, SumpPumpDrainageBufferIds.FrameSummary);
            ReleaseOwnedHandle(ref _profilesHandle, SumpPumpDrainageBufferIds.PipeProfiles);
            ReleaseOwnedHandle(ref _countersHandle, SumpPumpDrainageBufferIds.Counters);
            ReleaseOwnedHandle(ref _telemetryCursorHandle, SumpPumpDrainageBufferIds.TelemetryCursor);
            ReleaseOwnedHandle(ref _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing);
            ReleaseOwnedHandle(ref _tuningHandle, SumpPumpDrainageBufferIds.Tuning);
            ReleaseOwnedHandle(ref _roomDrainLocksHandle, SumpPumpDrainageBufferIds.RoomDrainLocks);
            ReleaseOwnedHandle(ref _pumpMassErrorHandle, SumpPumpDrainageBufferIds.PumpMassError);
            ReleaseOwnedHandle(ref _pumpRemainderHandle, SumpPumpDrainageBufferIds.PumpRemainder);
            ReleaseOwnedHandle(ref _pumpPowerNodeHashesHandle, SumpPumpDrainageBufferIds.PumpPowerNodeHashes);
            ReleaseOwnedHandle(ref _pumpBaseMaxRateHandle, SumpPumpDrainageBufferIds.PumpBaseMaxRate);
            ReleaseOwnedHandle(ref _powerPotentialHandle, SumpPumpDrainageBufferIds.PowerPotential);
            ReleaseOwnedHandle(ref _pressureBackHandle, SumpPumpDrainageBufferIds.PressureBack);
            ReleaseOwnedHandle(ref _pressureFrontHandle, SumpPumpDrainageBufferIds.PressureFront);
            ReleaseOwnedHandle(ref _csrWriteCursorHandle, SumpPumpDrainageBufferIds.CsrWriteCursor);
            ReleaseOwnedHandle(ref _csrFlatEdgeIndexHandle, SumpPumpDrainageBufferIds.CsrFlatEdgeIndex);
            ReleaseOwnedHandle(ref _csrFlowHandle, SumpPumpDrainageBufferIds.CsrFlow);
            ReleaseOwnedHandle(ref _csrConductanceHandle, SumpPumpDrainageBufferIds.CsrConductance);
            ReleaseOwnedHandle(ref _csrDestinationsHandle, SumpPumpDrainageBufferIds.CsrDestinations);
            ReleaseOwnedHandle(ref _csrOffsetsHandle, SumpPumpDrainageBufferIds.CsrOffsets);
            ReleaseOwnedHandle(ref _pumpRoomIndicesHandle, SumpPumpDrainageBufferIds.PumpRoomIndices);
            ReleaseOwnedHandle(ref _nodeAupHandle, SumpPumpDrainageBufferIds.NodeAup);
            ReleaseOwnedHandle(ref _pipeEdgesHandle, SumpPumpDrainageBufferIds.PipeEdges);
            ReleaseOwnedHandle(ref _pumpNodesHandle, SumpPumpDrainageBufferIds.PumpNodes);
            ResetHandles();
            ResetRuntimeStateForVaultRelease();
        }

        private void ReleaseOwnedHandle<T>(ref VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            if (IsDrainageVaultHandle(in handle, expectedBufferId))
            {
                _vault.ReleaseBuffer(in handle);
            }

            handle = default;
        }

        private void ResetRuntimeStateForVaultRelease()
        {
            _solverHandle = default;
            _mockSeedHandle = default;
            _activeMutationGuardMask = 0UL;
            _activeMutationGuardVault = null;
            _solverBufferPinMask = 0UL;
            _solverBufferPinVault = null;
            _solverScheduleTimestamp = 0L;
            _frameIndex = 0u;
            _flowBufferWriteIndex = 0;
            _solveAccumulator = 0f;
            _solverScheduled = false;
            _pressureFrontIsA = true;
            _topologyDirty = true;
            _flowUploadDirty = false;
            _mockSeedScheduled = false;
            _blackBoxDumped = false;
            _blackBoxStateHash = 0u;
            _blackBoxFlags = 0u;
            _blackBoxEntryCount = 0;
            _debugActivePumps = 0;
            _debugFrameEvacuatedM3 = 0f;
            _debugAveragePressure = 0f;
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
            _frameSummaryHandle = default;
            _flowGpuHandle = default;
        }

        private void RequestBlackBoxDumpOnce()
        {
            if (_blackBoxDumped)
                return;

            if (!TryRead(in _telemetryHandle, SumpPumpDrainageBufferIds.TelemetryRing, out NativeArray<DrainageTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length <= 0)
                return;

            TryRead(in _telemetryCursorHandle, SumpPumpDrainageBufferIds.TelemetryCursor, out NativeArray<int> telemetryCursor);
            int capacity = math.min(telemetry.Length, SumpPumpPipeGridConstants.TelemetryFrameCount);
            int writeCount = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? math.max(0, telemetryCursor[0]) : capacity;
            int validCount = math.min(capacity, writeCount);
            int oldestIndex = capacity > 0 && writeCount > capacity ? writeCount % capacity : 0;
            uint aggregateFlags = SumpDrainageTelemetryFlags.DumpedBlackBox;
            uint hash = RuntimeHash ^ (uint)validCount ^ ((uint)writeCount * 16777619u);

            for (int i = 0; i < validCount; i++)
            {
                DrainageTelemetryEntry entry = telemetry[(oldestIndex + i) % capacity];
                aggregateFlags |= entry.Flags;
                hash = SumpPumpPipeGridValidation.MixHash(hash, entry.StateHash);
                hash = SumpPumpPipeGridValidation.MixHash(hash, entry.FrameIndex);
                hash = SumpPumpPipeGridValidation.MixHash(hash, math.asuint(entry.AveragePressure));
                hash = SumpPumpPipeGridValidation.MixHash(hash, math.asuint(entry.FrameEvacuatedM3));
                hash = SumpPumpPipeGridValidation.MixHash(hash, entry.Flags);
            }

            _blackBoxStateHash = hash;
            _blackBoxFlags = aggregateFlags;
            _blackBoxEntryCount = validCount;
            _blackBoxDumped = true;
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

            NativeArray<float> pressure;
            bool hasPressure = _pressureFrontIsA
                ? TryRead(in _pressureFrontHandle, SumpPumpDrainageBufferIds.PressureFront, out pressure)
                : TryRead(in _pressureBackHandle, SumpPumpDrainageBufferIds.PressureBack, out pressure);
            if (!TryRead(in _pumpNodesHandle, SumpPumpDrainageBufferIds.PumpNodes, out NativeArray<DrainageNodeDTO> nodes) ||
                !TryRead(in _pipeEdgesHandle, SumpPumpDrainageBufferIds.PipeEdges, out NativeArray<PipeEdgeDTO> edges) ||
                !TryRead(in _nodeAupHandle, SumpPumpDrainageBufferIds.NodeAup, out NativeArray<double3> aup) ||
                !TryRead(in _countersHandle, SumpPumpDrainageBufferIds.Counters, out NativeArray<int> counters) ||
                !hasPressure ||
                !nodes.IsCreated ||
                !edges.IsCreated ||
                !aup.IsCreated ||
                !pressure.IsCreated ||
                !counters.IsCreated)
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

            NativeArray<float> pressure;
            bool hasPressure = _pressureFrontIsA
                ? TryRead(in _pressureFrontHandle, SumpPumpDrainageBufferIds.PressureFront, out pressure)
                : TryRead(in _pressureBackHandle, SumpPumpDrainageBufferIds.PressureBack, out pressure);
            if (!TryRead(in _pipeEdgesHandle, SumpPumpDrainageBufferIds.PipeEdges, out NativeArray<PipeEdgeDTO> edges) ||
                !TryRead(in _nodeAupHandle, SumpPumpDrainageBufferIds.NodeAup, out NativeArray<double3> aup) ||
                !TryRead(in _countersHandle, SumpPumpDrainageBufferIds.Counters, out NativeArray<int> counters) ||
                !hasPressure ||
                !edges.IsCreated ||
                !aup.IsCreated ||
                !pressure.IsCreated ||
                !counters.IsCreated ||
                aup.Length <= 0)
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
