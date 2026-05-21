// ============================================================================
// HECTON-8 - ShinobuLogisticsRouter.cs
// Flat WFC outpost logistics graph: power BFS, oxygen diffusion, pressure flags.
// ============================================================================

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Logistics.Grid.Contracts;
using Hecton8.World;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LogisticsNodeDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float Capacity;
        [FieldOffset(8)] public float CurrentLoad;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public int EdgeStartIndex;
        [FieldOffset(20)] public int EdgeCount;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LogisticsEdgeDTO
    {
        [FieldOffset(0)] public int2 Nodes;
        [FieldOffset(8)] public float Capacity;
        [FieldOffset(12)] public float Resistance;
        [FieldOffset(16)] public float Flow01;
        [FieldOffset(20)] public int LastMilliTransfer;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LogisticsTuningDTO
    {
        [FieldOffset(0)] public float ReactorOutputWatts;
        [FieldOffset(4)] public float LifeSupportDrainWatts;
        [FieldOffset(8)] public float OxygenDiffusionRate;
        [FieldOffset(12)] public float CrushDepthMultiplier;
        [FieldOffset(16)] public float BasePipeResistance;
        [FieldOffset(20)] public float JacobiSmoothingFactor;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LogisticsComponentSpecDTO
    {
        [FieldOffset(0)] public uint ModuleHash;
        [FieldOffset(4)] public float Capacity;
        [FieldOffset(8)] public float Resistance;
        [FieldOffset(12)] public float OxygenDemand;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public uint _pad0;
        [FieldOffset(24)] public uint _pad1;
        [FieldOffset(28)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LogisticsGraphTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public float TotalPowerGenerated;
        [FieldOffset(12)] public float TotalPowerConsumed;
        [FieldOffset(16)] public float TotalOxygen01;
        [FieldOffset(20)] public float SupplyRatio;
        [FieldOffset(24)] public int FrameIndex;
        [FieldOffset(28)] public int NodeCount;
        [FieldOffset(32)] public int ActiveNodeCount;
        [FieldOffset(36)] public int FaultFlags;
        [FieldOffset(40)] public int BreachedCount;
        [FieldOffset(44)] public int UnpoweredCount;
        [FieldOffset(48)] public int OxygenCadence;
        [FieldOffset(52)] public int ComponentCount;
        [FieldOffset(56)] public int JacobiIterations;
        [FieldOffset(60)] public int SolverMicros;
    }

    public static class LogisticsStateFlags
    {
        public const ulong Powered = 1UL << 0;
        public const ulong Flooded = 1UL << 1;
        public const ulong DoorLocked = 1UL << 2;
        public const ulong ReactorOverheating = 1UL << 3;
        public const ulong PowerGenerator = 1UL << 4;
        public const ulong Destroyed = 1UL << 5;
        public const ulong Breached = 1UL << 6;
        public const ulong DockingPort = 1UL << 7;
        public const ulong SubmarineAttached = 1UL << 8;
        public const ulong Unpowered = 1UL << 9;
        public const ulong LowOxygen = 1UL << 10;
        public const ulong LifeSupport = 1UL << 11;
        public const ulong Fabricator = 1UL << 12;
        public const ulong Divergent = 1UL << 13;
    }

    public static class LogisticsGraphFaultFlags
    {
        public const int None = 0;
        public const int LayoutFault = 1 << 0;
        public const int CapacityExceeded = 1 << 1;
        public const int InfiniteLoopGuard = 1 << 2;
        public const int OxygenNan = 1 << 3;
        public const int MissingGenerator = 1 << 4;
        public const int DumpedBlackBox = 1 << 5;
        public const int CsvParseFault = 1 << 6;
        public const int SignalOverflow = 1 << 7;
        public const int SolverDivergent = 1 << 8;
    }

    public sealed unsafe class ShinobuLogisticsRouter : IDisposable
    {
        public const int MaxNodes = 1000;
        public const int MaxDirectedEdges = WfcOutpostGridConstants.MaxDirectedEdges;
        public const int MaxAdjacencyEntries = MaxDirectedEdges * 2;
        public const int TelemetryFrames = WfcOutpostGridConstants.TelemetryFrames;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_114.bin";
        public const string DumpSurgeonRelativePath = "Docs/AgentLogs/Dump_LOGISTICS_SURGEON.bin";
        public const string DumpH8RelativePath = "Docs/AgentLogs/Dump_SHINOBU_114.h8dump";

        private const int PriorityLifeSupport = 0;
        private const int PriorityCorridor = 1;
        private const int PriorityIndustrial = 2;
        private const int PriorityOptional = 3;
        private const int CounterNodeCount = 0;
        private const int CounterEdgeCount = 1;
        private const int CounterFaultFlags = 2;
        private const int CounterActiveNodeCount = 3;
        private const int CounterBreachedCount = 4;
        private const int CounterUnpoweredCount = 5;
        private const int CounterAdjacencyEntryCount = 6;
        private const int CounterComponentCount = 7;
        private const int CounterGeneratorComponent = 8;
        private const int CounterJacobiIterations = 9;
        private const int CounterSpecCount = 10;
        private const int CounterBreachSignalCount = 11;
        private const int CounterCount = 16;
        private const int EdgeOffsetsBase = CounterCount;
        private const int EdgeWriteCursorBase = EdgeOffsetsBase + MaxNodes + 1;
        private const int EdgeDestinationsBase = EdgeWriteCursorBase + MaxNodes;
        private const int BfsQueueBase = EdgeDestinationsBase + MaxAdjacencyEntries;
        private const int ReachableOrderBase = BfsQueueBase + MaxNodes;
        private const int BreachNodeBase = ReachableOrderBase + MaxNodes;
        private const int IntLaneCount = BreachNodeBase + MaxNodes;
        private const int FluidIncursionSignalCapacity = MaxNodes;
        private const int CsvBufferBytes = 16 * 1024;
        private const int LowTierOxygenCadence = 5;
        private const int NormalOxygenCadence = 1;
        private const float OxygenTickSeconds = 0.1f;
        private const float MockDockingWatts = 400f;
        private const float DefaultBasePipeResistance = 0.35f;
        private const float DefaultJacobiSmoothing = 0.82f;
        private const uint SourceHash = 0x5348494Eu; // SHIN

        private static ShinobuLogisticsRouter _active;
        private static LogisticsTuningDTO _offlineTuning = EmergencyTuning();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _active = null;
            _offlineTuning = EmergencyTuning();
            ConfigurePublicSignalLanes();
        }

        private IDataVault _dataVault;
        private VaultGenerationHandle<LogisticsNodeDTO> _nodesHandle;
        private VaultGenerationHandle<LogisticsEdgeDTO> _edgesHandle;
        private VaultGenerationHandle<ulong> _stateFlagsHandle;
        private VaultGenerationHandle<float> _oxygenFrontHandle;
        private VaultGenerationHandle<float> _oxygenBackHandle;
        private VaultGenerationHandle<float> _internalPressureHandle;
        private VaultGenerationHandle<float> _externalPressureHandle;
        private VaultGenerationHandle<float> _yieldThresholdHandle;
        private VaultGenerationHandle<float> _reinforcementHandle;
        private VaultGenerationHandle<double3> _nodeAupHandle;
        private VaultGenerationHandle<float3> _localPositionsHandle;
        private VaultGenerationHandle<byte> _priorityTierHandle;
        private VaultGenerationHandle<byte> _visitedHandle;
        private VaultGenerationHandle<int> _cellToNodeHandle;
        private VaultGenerationHandle<int> _countersHandle;
        private VaultGenerationHandle<LogisticsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<LogisticsGraphTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<int> _componentIdsHandle;
        private VaultGenerationHandle<float> _pressureFrontHandle;
        private VaultGenerationHandle<float> _pressureBackHandle;
        private VaultGenerationHandle<float> _edgeRemainderMilliHandle;
        private VaultGenerationHandle<float> _csrEdgeCapacitiesHandle;
        private VaultGenerationHandle<float> _csrEdgeFlow01Handle;
        private VaultGenerationHandle<LogisticsComponentSpecDTO> _componentSpecsHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;

        internal NativeArray<LogisticsNodeDTO> _nodes;
        internal NativeArray<LogisticsEdgeDTO> _edges;
        internal NativeArray<ulong> _stateFlags;
        internal NativeArray<float> _oxygenFront;
        internal NativeArray<float> _oxygenBack;
        internal NativeArray<float> _internalPressureKpa;
        internal NativeArray<float> _externalPressureKpa;
        internal NativeArray<float> _yieldThresholdKpa;
        internal NativeArray<float> _reinforcement;
        internal NativeArray<double3> _nodeAup;
        internal NativeArray<float3> _localPositions;
        internal NativeArray<byte> _priorityTier;
        internal NativeArray<byte> _visited;
        internal NativeArray<int> _cellToNode;
        internal NativeArray<int> _counters;
        internal NativeArray<LogisticsTuningDTO> _tuning;
        internal NativeArray<LogisticsGraphTelemetryEntry> _blackBox;
        internal NativeArray<int> _componentIds;
        internal NativeArray<float> _pressureFront;
        internal NativeArray<float> _pressureBack;
        internal NativeArray<float> _edgeRemainderMilli;
        internal NativeArray<float> _csrEdgeCapacities;
        internal NativeArray<float> _csrEdgeFlow01;
        internal NativeArray<LogisticsComponentSpecDTO> _componentSpecs;
        internal NativeArray<byte> _csvScratch;

        private JobHandle _solveHandle;
        private JobHandle _csrRebuildHandle;
        private JobHandle _localShiftHandle;
        private bool _solvePending;
        private bool _csrRebuildPending;
        private bool _localShiftPending;
        private bool _initialized;
        private bool _hasGraph;
        private bool _hasFatalLayoutFault;
        private int _nodeCount;
        private int _edgeCount;
        private int _frameIndex;
        private int _oxygenCadenceCounter;
        private int _oxygenCadenceDivisor = NormalOxygenCadence;
        private int _jacobiIterations = 10;
        private float _globalQualityWeight = 1f;
        private long _solveStartTimestamp;
        private uint _activeGridHandle;
        private ulong _activeSectorHash;
        private uint _activeGridHash;
        private uint _activeGenerationSequence;
        private double3 _cameraAup;
        private LogisticsTuningDTO _queuedTuning;
        private bool _hasQueuedTuning;
        private bool _missingVaultWarned;
        private DateTime _csvLastWriteUtc;
        private string _csvPath;
        private IConnectionSplineBatchRendererService _pipeRenderer;
        private int _flowVisualPublishCursor;

        public static ShinobuLogisticsRouter Active => _active;

        public bool IsReady => _initialized && !_hasFatalLayoutFault;

        public int NodeCount => _nodeCount;

        public int EdgeCount => _edgeCount;

        public bool HasPendingSolve => _solvePending;

        public void InjectDataVault(IDataVault dataVault)
        {
            if (_initialized)
                return;

            _dataVault = dataVault;
            _missingVaultWarned = false;
        }

        public void EnsureInitialized()
        {
            if (_initialized)
                return;

            if (!ValidateLayouts(out int nodeBytes, out int edgeBytes, out int tuningBytes))
            {
                _hasFatalLayoutFault = true;
                GlobalTelemetryBus.PublishPerformanceWarning(0x53484C41u, SourceHash, nodeBytes + edgeBytes + tuningBytes);
                return;
            }

            if (!ResolveVaultBuffers())
            {
                if (!_missingVaultWarned)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(0x53485641u, SourceHash, 0);
                    _missingVaultWarned = true;
                }

                return;
            }

            if (!_nodes.IsCreated || !_edges.IsCreated || !_stateFlags.IsCreated || !_oxygenFront.IsCreated ||
                !_oxygenBack.IsCreated || !_internalPressureKpa.IsCreated || !_externalPressureKpa.IsCreated ||
                !_yieldThresholdKpa.IsCreated || !_reinforcement.IsCreated || !_nodeAup.IsCreated ||
                !_localPositions.IsCreated || !_priorityTier.IsCreated || !_visited.IsCreated ||
                !_cellToNode.IsCreated || !_counters.IsCreated || !_tuning.IsCreated || !_blackBox.IsCreated ||
                !_componentIds.IsCreated || !_pressureFront.IsCreated || !_pressureBack.IsCreated ||
                !_edgeRemainderMilli.IsCreated || !_csrEdgeCapacities.IsCreated || !_csrEdgeFlow01.IsCreated ||
                !_componentSpecs.IsCreated || !_csvScratch.IsCreated)
            {
                _hasFatalLayoutFault = true;
                return;
            }

            ConfigurePublicSignalLanes();
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            GlobalRegistry.TryGet(out _pipeRenderer);

            _tuning[0] = _offlineTuning;
            LogisticsGraphInitializeJob initializeJob = new LogisticsGraphInitializeJob
            {
                Nodes = _nodes,
                StateFlags = _stateFlags,
                OxygenFront = _oxygenFront,
                OxygenBack = _oxygenBack,
                InternalPressureKpa = _internalPressureKpa,
                ExternalPressureKpa = _externalPressureKpa,
                YieldThresholdKpa = _yieldThresholdKpa,
                Reinforcement = _reinforcement,
                NodeAup = _nodeAup,
                LocalPositions = _localPositions,
                PriorityTier = _priorityTier,
                Visited = _visited,
                CellToNode = _cellToNode,
                ComponentIds = _componentIds,
                PressureFront = _pressureFront,
                PressureBack = _pressureBack
            };
            for (int i = 0; i < MaxNodes; i++)
                initializeJob.Execute(i);

            for (int i = 0; i < _counters.Length; i++)
                _counters[i] = 0;
            for (int i = 0; i < _blackBox.Length; i++)
                _blackBox[i] = default;
            for (int i = 0; i < _csrEdgeCapacities.Length; i++)
            {
                _csrEdgeCapacities[i] = 0f;
                _csrEdgeFlow01[i] = 0f;
            }
            for (int i = 0; i < _edgeRemainderMilli.Length; i++)
                _edgeRemainderMilli[i] = 0f;

            _initialized = true;
            _missingVaultWarned = false;
            _active = this;
#if UNITY_EDITOR
            _csvPath = ResolveCsvPath();
#endif
        }

        public void SlowTick(float now)
        {
            EnsureInitialized();
            if (!_initialized || _hasFatalLayoutFault)
                return;
            if (!RefreshVaultAliases())
                return;
            if (_solvePending)
                return;

            TryConsumeGeneratedSignals();
            TryConsumeStateSignals();
            TryConsumeDockingSignals();
            RefreshHardwareCadence();
#if UNITY_EDITOR
            TryReloadCsvOverrides();
#endif

            if (!_hasGraph)
                BuildEmergencyMockGraph();

            if (_csrRebuildPending && _counters[CounterAdjacencyEntryCount] <= 0)
                return;

            ApplyQueuedTuning();
            ApplyDeterministicMockModuleToggle();

            _frameIndex++;
            bool runOxygen = ShouldRunOxygenSolver();
            _solveStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            int iterations = math.clamp(_jacobiIterations, 1, 10);
            int scheduledNodeCount = math.max(1, math.clamp(_nodeCount, 0, MaxNodes));
            JobHandle solveHandle = new LogisticsFlowPrepareJob
            {
                NodesPtr = (LogisticsNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                NodeCount = _nodeCount,
                StateFlags = _stateFlags,
                EdgeOffsetsBaseIndex = EdgeOffsetsBase,
                EdgeDestinationsBaseIndex = EdgeDestinationsBase,
                AdjacencyEntryCount = _counters[CounterAdjacencyEntryCount],
                ComponentIds = _componentIds,
                PressureFront = _pressureFront,
                PressureBack = _pressureBack,
                Visited = _visited,
                Counters = _counters,
                BfsQueueBaseIndex = BfsQueueBase,
                ReachableBaseIndex = ReachableOrderBase
            }.Schedule();

            // Residual guard lives inside LogisticsFlowSolverJob; stable nodes copy forward without re-solving.
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                bool frontToBack = (iteration & 1) == 0;
                solveHandle = new LogisticsFlowSolverJob
                {
                    NodesPtr = (LogisticsNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                    NodeCount = _nodeCount,
                    GlobalQualityWeight = _globalQualityWeight,
                    EdgeOffsetsBaseIndex = EdgeOffsetsBase,
                    EdgeDestinationsBaseIndex = EdgeDestinationsBase,
                    AdjacencyEntryCount = _counters[CounterAdjacencyEntryCount],
                    ComponentIds = _componentIds,
                    ReadPressure = frontToBack ? _pressureFront : _pressureBack,
                    WritePressure = frontToBack ? _pressureBack : _pressureFront,
                    CsrEdgeCapacities = _csrEdgeCapacities,
                    Visited = _visited,
                    Counters = _counters,
                    Tuning = _tuning
                }.Schedule(scheduledNodeCount, 64, solveHandle);
            }

            if ((iterations & 1) != 0)
            {
                solveHandle = new LogisticsPressureCopyJob
                {
                    NodeCount = _nodeCount,
                    SourcePressure = _pressureBack,
                    DestinationPressure = _pressureFront
                }.Schedule(scheduledNodeCount, 64, solveHandle);
            }

            _solveHandle = new LogisticsFlowFinalizeJob
            {
                NodesPtr = (LogisticsNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount,
                FrameIndex = _frameIndex,
                RunOxygen = runOxygen ? 1 : 0,
                JacobiIterations = iterations,
                OxygenDeltaSeconds = OxygenTickSeconds * _oxygenCadenceDivisor,
                DockingPowerWatts = MockDockingWatts,
                StateFlags = _stateFlags,
                Edges = _edges,
                EdgeOffsetsBaseIndex = EdgeOffsetsBase,
                EdgeDestinationsBaseIndex = EdgeDestinationsBase,
                AdjacencyEntryCount = _counters[CounterAdjacencyEntryCount],
                ComponentIds = _componentIds,
                PressureFront = _pressureFront,
                EdgeRemainderMilli = _edgeRemainderMilli,
                CsrEdgeCapacities = _csrEdgeCapacities,
                CsrEdgeFlow01 = _csrEdgeFlow01,
                Visited = _visited,
                OxygenFront = _oxygenFront,
                OxygenBack = _oxygenBack,
                InternalPressureKpa = _internalPressureKpa,
                ExternalPressureKpa = _externalPressureKpa,
                YieldThresholdKpa = _yieldThresholdKpa,
                Reinforcement = _reinforcement,
                Counters = _counters,
                BreachNodeBaseIndex = BreachNodeBase,
                Tuning = _tuning,
                BlackBox = _blackBox
            }.Schedule(solveHandle);
            _solvePending = true;
        }

        public void LateFrameTick(float now)
        {
            if (!_initialized)
                return;

            if (_csrRebuildPending && _csrRebuildHandle.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref _csrRebuildHandle);
                _csrRebuildPending = false;
            }

            if (_solvePending && _solveHandle.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref _solveHandle);
                _solvePending = false;
                PatchLatestTelemetryMicros();
                PublishSolveSideEffects();
                PublishFlowVisuals();
            }

            if (_hasGraph && !_localShiftPending)
            {
                if (!RefreshVaultAliases())
                    return;

                _localShiftHandle = new LocalShiftResolverJob
                {
                    NodeAup = _nodeAup,
                    LocalPositions = _localPositions,
                    CameraAup = _cameraAup
                }.Schedule(_nodeCount, 64);
                _localShiftPending = true;
            }

            if (_localShiftPending && _localShiftHandle.IsCompleted)
            {
                DispatcherJobFence.TryFinalizeCompleted(ref _localShiftHandle);
                _localShiftPending = false;
            }
        }

        public void SetCameraAup(double3 cameraAup)
        {
            if (math.all(math.isfinite(cameraAup)))
                _cameraAup = cameraAup;
        }

        public void ForceRebuildMockGraph()
        {
            EnsureInitialized();
            if (!_initialized || _hasFatalLayoutFault)
                return;

            BuildEmergencyMockGraph();
        }

        public void ForceDumpBlackBox()
        {
            if (_initialized)
                DumpBlackBox(_counters.IsCreated ? _counters[CounterFaultFlags] : 0);
        }

        public static bool ValidateLayouts(out int nodeBytes, out int edgeBytes, out int tuningBytes)
        {
            nodeBytes = UnsafeUtility.SizeOf<LogisticsNodeDTO>();
            edgeBytes = UnsafeUtility.SizeOf<LogisticsEdgeDTO>();
            tuningBytes = UnsafeUtility.SizeOf<LogisticsTuningDTO>();
            bool sizesValid = nodeBytes == 32 &&
                              edgeBytes == 32 &&
                              tuningBytes == 32 &&
                              UnsafeUtility.SizeOf<LogisticsComponentSpecDTO>() == 32 &&
                              UnsafeUtility.SizeOf<LogisticsGraphTelemetryEntry>() == 64;
#if UNITY_EDITOR
            return sizesValid &&
                   ValidateLogisticsNodeOffsets() &&
                   ValidateLogisticsEdgeOffsets() &&
                   ValidateLogisticsTuningOffsets() &&
                   ValidateLogisticsComponentSpecOffsets() &&
                   ValidateLogisticsTelemetryOffsets();
#else
            return sizesValid;
#endif
        }

#if UNITY_EDITOR
        public static bool ValidateLogisticsNodeOffsets()
        {
            return OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO.NodeHash)) == 0 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO.Capacity)) == 4 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO.CurrentLoad)) == 8 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO.Flags)) == 12 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO.EdgeStartIndex)) == 16 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO.EdgeCount)) == 20 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO._pad0)) == 24 &&
                   OffsetOf<LogisticsNodeDTO>(nameof(LogisticsNodeDTO._pad1)) == 28;
        }

        private static bool ValidateLogisticsEdgeOffsets()
        {
            return OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO.Nodes)) == 0 &&
                   OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO.Capacity)) == 8 &&
                   OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO.Resistance)) == 12 &&
                   OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO.Flow01)) == 16 &&
                   OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO.LastMilliTransfer)) == 20 &&
                   OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO.Flags)) == 24 &&
                   OffsetOf<LogisticsEdgeDTO>(nameof(LogisticsEdgeDTO._pad0)) == 28;
        }

        private static bool ValidateLogisticsTuningOffsets()
        {
            return OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.ReactorOutputWatts)) == 0 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.LifeSupportDrainWatts)) == 4 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.OxygenDiffusionRate)) == 8 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.CrushDepthMultiplier)) == 12 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.BasePipeResistance)) == 16 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.JacobiSmoothingFactor)) == 20 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.GlobalQualityWeight)) == 24 &&
                   OffsetOf<LogisticsTuningDTO>(nameof(LogisticsTuningDTO.Flags)) == 28;
        }

        private static bool ValidateLogisticsComponentSpecOffsets()
        {
            return OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO.ModuleHash)) == 0 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO.Capacity)) == 4 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO.Resistance)) == 8 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO.OxygenDemand)) == 12 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO.Flags)) == 16 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO._pad0)) == 20 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO._pad1)) == 24 &&
                   OffsetOf<LogisticsComponentSpecDTO>(nameof(LogisticsComponentSpecDTO._pad2)) == 28;
        }

        private static bool ValidateLogisticsTelemetryOffsets()
        {
            return OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.StateHash)) == 0 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.TotalPowerGenerated)) == 8 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.TotalPowerConsumed)) == 12 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.TotalOxygen01)) == 16 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.SupplyRatio)) == 20 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.FrameIndex)) == 24 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.NodeCount)) == 28 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.ActiveNodeCount)) == 32 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.FaultFlags)) == 36 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.BreachedCount)) == 40 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.UnpoweredCount)) == 44 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.OxygenCadence)) == 48 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.ComponentCount)) == 52 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.JacobiIterations)) == 56 &&
                   OffsetOf<LogisticsGraphTelemetryEntry>(nameof(LogisticsGraphTelemetryEntry.SolverMicros)) == 60;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            var field = typeof(T).GetField(fieldName);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
#endif

        public static bool SelfAuditArchitecture(out uint auditHash)
        {
            bool layoutsValid = ValidateLayouts(out int nodeBytes, out int edgeBytes, out int tuningBytes);
            auditHash = 2166136261u;
            auditHash = (auditHash ^ (uint)nodeBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)edgeBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)tuningBytes) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsNodes) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsEdges) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsStateFlags) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsOxygenFront) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsOxygenBack) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsInternalPressure) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsExternalPressure) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsYieldThreshold) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsReinforcement) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsNodeAup) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsLocalPositions) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsPriorityTier) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsVisited) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsCellToNode) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsCounters) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsTuning) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsComponentIds) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsPressureFront) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsPressureBack) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsEdgeRemainderMilli) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsCsrEdgeCapacities) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsCsrEdgeFlow01) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsBlackBox) * 16777619u;
            auditHash = (auditHash ^ (uint)BufferID.ShinobuLogisticsCsvScratch) * 16777619u;
            auditHash = (auditHash ^ (uint)BfsQueueBase) * 16777619u;
            auditHash = (auditHash ^ (uint)ReachableOrderBase) * 16777619u;
            auditHash = (auditHash ^ (uint)BreachNodeBase) * 16777619u;
            auditHash = (auditHash ^ (uint)IntLaneCount) * 16777619u;
            return layoutsValid &&
                   MaxNodes == 1000 &&
                   MaxDirectedEdges >= 2500 &&
                   MaxAdjacencyEntries >= 5000 &&
                   CsvBufferBytes == 16 * 1024 &&
                   BfsQueueBase >= EdgeDestinationsBase + MaxAdjacencyEntries &&
                   ReachableOrderBase >= BfsQueueBase + MaxNodes &&
                   BreachNodeBase >= ReachableOrderBase + MaxNodes &&
                   IntLaneCount >= BreachNodeBase + MaxNodes &&
                   TelemetryFrames == 300;
        }

        private bool ResolveVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return ResolveVaultBuffer(vault, ref _nodesHandle, BufferID.ShinobuLogisticsNodes, MaxNodes, out _nodes) &&
                   ResolveVaultBuffer(vault, ref _edgesHandle, BufferID.ShinobuLogisticsEdges, MaxDirectedEdges, out _edges) &&
                   ResolveVaultBuffer(vault, ref _stateFlagsHandle, BufferID.ShinobuLogisticsStateFlags, MaxNodes, out _stateFlags) &&
                   ResolveVaultBuffer(vault, ref _oxygenFrontHandle, BufferID.ShinobuLogisticsOxygenFront, MaxNodes, out _oxygenFront) &&
                   ResolveVaultBuffer(vault, ref _oxygenBackHandle, BufferID.ShinobuLogisticsOxygenBack, MaxNodes, out _oxygenBack) &&
                   ResolveVaultBuffer(vault, ref _internalPressureHandle, BufferID.ShinobuLogisticsInternalPressure, MaxNodes, out _internalPressureKpa) &&
                   ResolveVaultBuffer(vault, ref _externalPressureHandle, BufferID.ShinobuLogisticsExternalPressure, MaxNodes, out _externalPressureKpa) &&
                   ResolveVaultBuffer(vault, ref _yieldThresholdHandle, BufferID.ShinobuLogisticsYieldThreshold, MaxNodes, out _yieldThresholdKpa) &&
                   ResolveVaultBuffer(vault, ref _reinforcementHandle, BufferID.ShinobuLogisticsReinforcement, MaxNodes, out _reinforcement) &&
                   ResolveVaultBuffer(vault, ref _nodeAupHandle, BufferID.ShinobuLogisticsNodeAup, MaxNodes, out _nodeAup) &&
                   ResolveVaultBuffer(vault, ref _localPositionsHandle, BufferID.ShinobuLogisticsLocalPositions, MaxNodes, out _localPositions) &&
                   ResolveVaultBuffer(vault, ref _priorityTierHandle, BufferID.ShinobuLogisticsPriorityTier, MaxNodes, out _priorityTier) &&
                   ResolveVaultBuffer(vault, ref _visitedHandle, BufferID.ShinobuLogisticsVisited, MaxNodes, out _visited) &&
                   ResolveVaultBuffer(vault, ref _cellToNodeHandle, BufferID.ShinobuLogisticsCellToNode, MaxNodes, out _cellToNode) &&
                   ResolveVaultBuffer(vault, ref _countersHandle, BufferID.ShinobuLogisticsCounters, IntLaneCount, out _counters) &&
                   ResolveVaultBuffer(vault, ref _tuningHandle, BufferID.ShinobuLogisticsTuning, 1, out _tuning) &&
                   ResolveVaultBuffer(vault, ref _blackBoxHandle, BufferID.ShinobuLogisticsBlackBox, TelemetryFrames, out _blackBox) &&
                   ResolveVaultBuffer(vault, ref _componentIdsHandle, BufferID.ShinobuLogisticsComponentIds, MaxNodes, out _componentIds) &&
                   ResolveVaultBuffer(vault, ref _pressureFrontHandle, BufferID.ShinobuLogisticsPressureFront, MaxNodes, out _pressureFront) &&
                   ResolveVaultBuffer(vault, ref _pressureBackHandle, BufferID.ShinobuLogisticsPressureBack, MaxNodes, out _pressureBack) &&
                   ResolveVaultBuffer(vault, ref _edgeRemainderMilliHandle, BufferID.ShinobuLogisticsEdgeRemainderMilli, MaxAdjacencyEntries, out _edgeRemainderMilli) &&
                   ResolveVaultBuffer(vault, ref _csrEdgeCapacitiesHandle, BufferID.ShinobuLogisticsCsrEdgeCapacities, MaxAdjacencyEntries, out _csrEdgeCapacities) &&
                   ResolveVaultBuffer(vault, ref _csrEdgeFlow01Handle, BufferID.ShinobuLogisticsCsrEdgeFlow01, MaxAdjacencyEntries, out _csrEdgeFlow01) &&
                   ResolveVaultBuffer(vault, ref _componentSpecsHandle, BufferID.ShinobuLogisticsComponentSpecs, MaxNodes, out _componentSpecs) &&
                   ResolveVaultBuffer(vault, ref _csvScratchHandle, BufferID.ShinobuLogisticsCsvScratch, CsvBufferBytes, out _csvScratch);
        }

        private static bool ResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsHandleValid(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle<T>(bufferId, out handle))
                    return false;
            }
            else
            {
                handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, SystemID.Power, NativeArrayOptions.UninitializedMemory);
            }

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool RefreshVaultAliases()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            return RefreshVaultBuffer(vault, ref _nodesHandle, MaxNodes, out _nodes) &&
                   RefreshVaultBuffer(vault, ref _edgesHandle, MaxDirectedEdges, out _edges) &&
                   RefreshVaultBuffer(vault, ref _stateFlagsHandle, MaxNodes, out _stateFlags) &&
                   RefreshVaultBuffer(vault, ref _oxygenFrontHandle, MaxNodes, out _oxygenFront) &&
                   RefreshVaultBuffer(vault, ref _oxygenBackHandle, MaxNodes, out _oxygenBack) &&
                   RefreshVaultBuffer(vault, ref _internalPressureHandle, MaxNodes, out _internalPressureKpa) &&
                   RefreshVaultBuffer(vault, ref _externalPressureHandle, MaxNodes, out _externalPressureKpa) &&
                   RefreshVaultBuffer(vault, ref _yieldThresholdHandle, MaxNodes, out _yieldThresholdKpa) &&
                   RefreshVaultBuffer(vault, ref _reinforcementHandle, MaxNodes, out _reinforcement) &&
                   RefreshVaultBuffer(vault, ref _nodeAupHandle, MaxNodes, out _nodeAup) &&
                   RefreshVaultBuffer(vault, ref _localPositionsHandle, MaxNodes, out _localPositions) &&
                   RefreshVaultBuffer(vault, ref _priorityTierHandle, MaxNodes, out _priorityTier) &&
                   RefreshVaultBuffer(vault, ref _visitedHandle, MaxNodes, out _visited) &&
                   RefreshVaultBuffer(vault, ref _cellToNodeHandle, MaxNodes, out _cellToNode) &&
                   RefreshVaultBuffer(vault, ref _countersHandle, IntLaneCount, out _counters) &&
                   RefreshVaultBuffer(vault, ref _tuningHandle, 1, out _tuning) &&
                   RefreshVaultBuffer(vault, ref _blackBoxHandle, TelemetryFrames, out _blackBox) &&
                   RefreshVaultBuffer(vault, ref _componentIdsHandle, MaxNodes, out _componentIds) &&
                   RefreshVaultBuffer(vault, ref _pressureFrontHandle, MaxNodes, out _pressureFront) &&
                   RefreshVaultBuffer(vault, ref _pressureBackHandle, MaxNodes, out _pressureBack) &&
                   RefreshVaultBuffer(vault, ref _edgeRemainderMilliHandle, MaxAdjacencyEntries, out _edgeRemainderMilli) &&
                   RefreshVaultBuffer(vault, ref _csrEdgeCapacitiesHandle, MaxAdjacencyEntries, out _csrEdgeCapacities) &&
                   RefreshVaultBuffer(vault, ref _csrEdgeFlow01Handle, MaxAdjacencyEntries, out _csrEdgeFlow01) &&
                   RefreshVaultBuffer(vault, ref _componentSpecsHandle, MaxNodes, out _componentSpecs) &&
                   RefreshVaultBuffer(vault, ref _csvScratchHandle, CsvBufferBytes, out _csvScratch);
        }

        private static bool RefreshVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || !IsHandleValid(in handle))
                return false;

            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandleValid<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u;
        }

        public static bool TryGetTuning(out LogisticsTuningDTO tuning)
        {
            ShinobuLogisticsRouter active = _active;
            if (active != null && active._initialized && active._tuning.IsCreated)
            {
                tuning = active._tuning[0];
                return true;
            }

            tuning = _offlineTuning;
            return false;
        }

        public static void SetTuning(in LogisticsTuningDTO tuning)
        {
            LogisticsTuningDTO sanitized = SanitizeTuning(tuning);
            _offlineTuning = sanitized;
            ShinobuLogisticsRouter active = _active;
            if (active == null || !active._initialized || !active._tuning.IsCreated)
                return;

            if (active._solvePending)
            {
                active._queuedTuning = sanitized;
                active._hasQueuedTuning = true;
                return;
            }

            IDataVault vault = active._dataVault;
            if (vault == null || !IsHandleValid(in active._tuningHandle))
                return;

            if (!vault.TryAcquireWriteLock(in active._tuningHandle, SystemID.Power, out NativeArray<LogisticsTuningDTO> tuningView))
                return;

            try
            {
                if (!tuningView.IsCreated || tuningView.Length == 0)
                    return;

                tuningView[0] = sanitized;
                active._tuning = tuningView;
            }
            finally
            {
                vault.ReleaseWriteLock(in active._tuningHandle, SystemID.Power);
            }
        }

        public static bool TryGetDebugEdge(
            int index,
            out float3 nodeA,
            out float3 nodeB,
            out ulong flagsA,
            out ulong flagsB)
        {
            return TryGetDebugEdge(index, out nodeA, out nodeB, out flagsA, out flagsB, out _, out _, out _);
        }

        public static bool TryGetDebugEdge(
            int index,
            out float3 nodeA,
            out float3 nodeB,
            out ulong flagsA,
            out ulong flagsB,
            out int componentA,
            out int componentB,
            out float flow01)
        {
            nodeA = default;
            nodeB = default;
            flagsA = 0UL;
            flagsB = 0UL;
            componentA = -1;
            componentB = -1;
            flow01 = 0f;

            ShinobuLogisticsRouter active = _active;
            if (active == null || !active._initialized || active._solvePending || active._localShiftPending)
                return false;
            if ((uint)index >= active._edgeCount)
                return false;

            int2 edge = active._edges[index].Nodes;
            if ((uint)edge.x >= active._nodeCount || (uint)edge.y >= active._nodeCount)
                return false;

            nodeA = active._localPositions[edge.x];
            nodeB = active._localPositions[edge.y];
            flagsA = active._stateFlags[edge.x];
            flagsB = active._stateFlags[edge.y];
            componentA = active._componentIds.IsCreated ? active._componentIds[edge.x] : -1;
            componentB = active._componentIds.IsCreated ? active._componentIds[edge.y] : -1;
            flow01 = math.saturate(active._edges[index].Flow01);
            return true;
        }

        public static bool TryGetLatestTelemetry(out LogisticsGraphTelemetryEntry entry)
        {
            entry = default;
            ShinobuLogisticsRouter active = _active;
            if (active == null || !active._initialized || !active._blackBox.IsCreated)
                return false;

            int frame = math.max(0, active._frameIndex);
            int index = frame % TelemetryFrames;
            if ((uint)index >= (uint)active._blackBox.Length)
                return false;

            entry = active._blackBox[index];
            return entry.FrameIndex != 0;
        }

        public static int DebugNodeCount()
        {
            ShinobuLogisticsRouter active = _active;
            return active != null && active._initialized ? active._nodeCount : 0;
        }

        public static int DebugEdgeCount()
        {
            ShinobuLogisticsRouter active = _active;
            return active != null && active._initialized ? active._edgeCount : 0;
        }

        public static bool HasActiveRuntime()
        {
            return _active != null && _active._initialized;
        }

        public void Dispose()
        {
            if (_solvePending)
            {
                DispatcherJobFence.TryComplete(ref _solveHandle, forceComplete: true);
                _solvePending = false;
            }

            if (_csrRebuildPending)
            {
                DispatcherJobFence.TryComplete(ref _csrRebuildHandle, forceComplete: true);
                _csrRebuildPending = false;
            }

            if (_localShiftPending)
            {
                DispatcherJobFence.TryComplete(ref _localShiftHandle, forceComplete: true);
                _localShiftPending = false;
            }

            if (ReferenceEquals(_active, this))
                _active = null;

            ClearVaultAliases();

            _initialized = false;
            _hasGraph = false;
            _nodeCount = 0;
            _edgeCount = 0;
        }

        private void ClearVaultAliases()
        {
            _csvScratch = default;
            _componentSpecs = default;
            _csrEdgeFlow01 = default;
            _csrEdgeCapacities = default;
            _edgeRemainderMilli = default;
            _pressureBack = default;
            _pressureFront = default;
            _componentIds = default;
            _blackBox = default;
            _tuning = default;
            _counters = default;
            _cellToNode = default;
            _visited = default;
            _priorityTier = default;
            _localPositions = default;
            _nodeAup = default;
            _reinforcement = default;
            _yieldThresholdKpa = default;
            _externalPressureKpa = default;
            _internalPressureKpa = default;
            _oxygenBack = default;
            _oxygenFront = default;
            _stateFlags = default;
            _edges = default;
            _nodes = default;

            _csvScratchHandle = default;
            _componentSpecsHandle = default;
            _csrEdgeFlow01Handle = default;
            _csrEdgeCapacitiesHandle = default;
            _edgeRemainderMilliHandle = default;
            _pressureBackHandle = default;
            _pressureFrontHandle = default;
            _componentIdsHandle = default;
            _blackBoxHandle = default;
            _tuningHandle = default;
            _countersHandle = default;
            _cellToNodeHandle = default;
            _visitedHandle = default;
            _priorityTierHandle = default;
            _localPositionsHandle = default;
            _nodeAupHandle = default;
            _reinforcementHandle = default;
            _yieldThresholdHandle = default;
            _externalPressureHandle = default;
            _internalPressureHandle = default;
            _oxygenBackHandle = default;
            _oxygenFrontHandle = default;
            _stateFlagsHandle = default;
            _edgesHandle = default;
            _nodesHandle = default;
            _dataVault = null;
        }

        private void TryConsumeGeneratedSignals()
        {
            ReadOnlySpan<WfcOutpostGeneratedSignal> signals = SignalBus<WfcOutpostGeneratedSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return;

            WfcOutpostGeneratedSignal latest = default;
            bool found = false;
            for (int i = 0; i < signals.Length; i++)
            {
                WfcOutpostGeneratedSignal signal = signals[i];
                if (signal.GridHandle == 0u || signal.CellCount == 0 || signal.SectorHash == 0UL || signal.GridHash == 0u)
                    continue;
                latest = signal;
                found = true;
            }

            if (!found)
                return;
            if (_hasGraph &&
                latest.GridHandle == _activeGridHandle &&
                latest.SectorHash == _activeSectorHash &&
                latest.GridHash == _activeGridHash &&
                latest.GenerationSequence == _activeGenerationSequence)
            {
                return;
            }

            if (WfcOutpostGridRegistry.TryGetGrid(latest.GridHandle, out WfcOutpostGridLease lease))
                BuildFromWfcGrid(in lease, in latest);
        }

        private void TryConsumeStateSignals()
        {
            ReadOnlySpan<WfcOutpostStateChangedSignal> stateSignals = SignalBus<WfcOutpostStateChangedSignal>.GetFrameSnapshot();
            bool rebuildAdjacency = false;
            for (int i = 0; i < stateSignals.Length; i++)
            {
                WfcOutpostStateChangedSignal signal = stateSignals[i];
                if (signal.SectorHash != _activeSectorHash || (uint)signal.CellIndex >= _cellToNode.Length)
                    continue;

                int nodeIndex = _cellToNode[signal.CellIndex];
                if ((uint)nodeIndex >= _nodeCount)
                    continue;

                ulong flags = _stateFlags[nodeIndex];
                if ((signal.CurrentFlags & 0x01) != 0)
                    flags |= LogisticsStateFlags.Destroyed;
                if ((signal.CurrentFlags & 0x02) != 0)
                    flags |= LogisticsStateFlags.DoorLocked;
                if ((signal.CurrentFlags & 0x04) != 0)
                    flags |= LogisticsStateFlags.Flooded;
                _stateFlags[nodeIndex] = flags;
                LogisticsNodeDTO node = _nodes[nodeIndex];
                node.Flags = (uint)flags;
                _nodes[nodeIndex] = node;
                rebuildAdjacency = true;
            }

            if (rebuildAdjacency)
                RebuildAdjacencyFromEdges();
        }

        private void TryConsumeDockingSignals()
        {
            ReadOnlySpan<DockingCompleteSignal> dockingSignals = SignalBus<DockingCompleteSignal>.GetFrameSnapshot();
            if (dockingSignals.Length == 0 || _nodeCount <= 0)
                return;

            int dockingNode = FindDockingNode();
            if (dockingNode < 0)
                return;

            ulong flags = _stateFlags[dockingNode] | LogisticsStateFlags.SubmarineAttached | LogisticsStateFlags.DockingPort;
            _stateFlags[dockingNode] = flags;
            LogisticsNodeDTO node = _nodes[dockingNode];
            node.Flags = (uint)flags;
            _nodes[dockingNode] = node;
        }

        private void RefreshHardwareCadence()
        {
            float quality = ResolveGlobalQualityWeight();
            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthSignal signal = healthSignals[i];
                float health = math.saturate(signal.SystemHealthIndex01);
                float pressurePenalty = math.saturate(signal.PressureLevel * 0.22f);
                quality = math.min(quality, math.saturate(health - pressurePenalty));
            }

            _globalQualityWeight = quality;
            float qualityCurve = quality * quality * (3f - (2f * quality));
            _jacobiIterations = PowerSolverConvergenceMath.ResolvePropagationIterations(quality);
            _oxygenCadenceDivisor = math.clamp((int)math.round(math.lerp(LowTierOxygenCadence, NormalOxygenCadence, qualityCurve)), NormalOxygenCadence, LowTierOxygenCadence);
            if (_tuning.IsCreated)
            {
                LogisticsTuningDTO tuning = _tuning[0];
                tuning.GlobalQualityWeight = quality;
                _tuning[0] = SanitizeTuning(tuning);
            }
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private bool ShouldRunOxygenSolver()
        {
            _oxygenCadenceCounter++;
            if (_oxygenCadenceCounter >= _oxygenCadenceDivisor)
            {
                _oxygenCadenceCounter = 0;
                return true;
            }

            return false;
        }

        private void ApplyQueuedTuning()
        {
            if (!_hasQueuedTuning)
                return;

            _tuning[0] = _queuedTuning;
            _hasQueuedTuning = false;
        }

        private void ApplyDeterministicMockModuleToggle()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_nodeCount <= 2 || (_frameIndex % 240) != 0)
                return;

            uint frame = (uint)_frameIndex;
            int nodeIndex = 1 + (int)((frame * 1103515245u + 12345u) % (uint)(_nodeCount - 1));
            ulong flags = _stateFlags[nodeIndex] ^ LogisticsStateFlags.DoorLocked;
            _stateFlags[nodeIndex] = flags;
            LogisticsNodeDTO node = _nodes[nodeIndex];
            node.Flags = (uint)flags;
            _nodes[nodeIndex] = node;
            RebuildAdjacencyFromEdges();
#endif
        }

        private void BuildFromWfcGrid(in WfcOutpostGridLease lease, in WfcOutpostGeneratedSignal signal)
        {
            if (!ResetGraphBuffers())
                return;

            WfcOutpostGridDescriptor descriptor = lease.Descriptor;
            int3 dimensions = descriptor.Dimensions;
            int cellCount = math.min(math.min(lease.Cells.Length, (int)descriptor.CellCount), MaxNodes);
            double3 origin = descriptor.OriginAup.ToAbsoluteDouble3();
            float cellSize = math.max(1f, descriptor.CellSizeMeters);
            float floorHeight = math.max(1f, descriptor.FloorHeightMeters);

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                byte packed = lease.Cells[cellIndex];
                if (!WfcOutpostGridConstants.IsPowerModuleKind(packed))
                    continue;

                if (_nodeCount >= MaxNodes)
                {
                    _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CapacityExceeded;
                    break;
                }

                int nodeIndex = _nodeCount++;
                _cellToNode[cellIndex] = nodeIndex;
                int3 cell = Unflatten(cellIndex, dimensions);
                byte kind = (byte)(packed & WfcOutpostGridConstants.CellMask);
                float3 local = new float3(cell.x * cellSize, cell.y * floorHeight, cell.z * cellSize);
                ulong flags = ResolveInitialStateFlags(kind, cell, dimensions);
                _nodes[nodeIndex] = new LogisticsNodeDTO
                {
                    NodeHash = ComputeNodeHash(signal.SectorHash, nodeIndex, kind),
                    Capacity = ResolvePowerDemand(kind),
                    CurrentLoad = 0f,
                    Flags = (uint)flags,
                    EdgeStartIndex = -1,
                    EdgeCount = 0
                };
                _stateFlags[nodeIndex] = flags;
                _oxygenFront[nodeIndex] = 100f;
                _oxygenBack[nodeIndex] = 100f;
                _internalPressureKpa[nodeIndex] = 101.3f;
                _externalPressureKpa[nodeIndex] = 101.3f + math.max(0f, -local.y + 80f) * 10.1f;
                _yieldThresholdKpa[nodeIndex] = 850f + ResolveReinforcement(kind) * 300f;
                _reinforcement[nodeIndex] = ResolveReinforcement(kind);
                _nodeAup[nodeIndex] = origin + new double3(local.x, local.y, local.z);
                _localPositions[nodeIndex] = local;
                _priorityTier[nodeIndex] = ResolvePriority(kind);
            }

            for (int cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                int nodeIndex = _cellToNode[cellIndex];
                if ((uint)nodeIndex >= _nodeCount)
                    continue;

                byte packed = lease.Cells[cellIndex];
                int3 cell = Unflatten(cellIndex, dimensions);
                TryConnectPlanar(lease.Cells, nodeIndex, packed, cell, dimensions, 1, 0, WfcOutpostGridConstants.East, WfcOutpostGridConstants.West);
                TryConnectPlanar(lease.Cells, nodeIndex, packed, cell, dimensions, -1, 0, WfcOutpostGridConstants.West, WfcOutpostGridConstants.East);
                TryConnectPlanar(lease.Cells, nodeIndex, packed, cell, dimensions, 0, 1, WfcOutpostGridConstants.North, WfcOutpostGridConstants.South);
                TryConnectPlanar(lease.Cells, nodeIndex, packed, cell, dimensions, 0, -1, WfcOutpostGridConstants.South, WfcOutpostGridConstants.North);

                if ((packed & WfcOutpostGridConstants.CellMask) == WfcOutpostGridConstants.Hatch)
                {
                    TryConnectVertical(cell, dimensions, 1, nodeIndex);
                    TryConnectVertical(cell, dimensions, -1, nodeIndex);
                }
            }

            _activeGridHandle = signal.GridHandle;
            _activeSectorHash = signal.SectorHash;
            _activeGridHash = signal.GridHash;
            _activeGenerationSequence = signal.GenerationSequence;
            _hasGraph = _nodeCount > 0;
            RebuildCsrFromEdges();
            _counters[CounterNodeCount] = _nodeCount;
            _counters[CounterEdgeCount] = _edgeCount;

            if (!HasGenerator())
                _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.MissingGenerator;
        }

        private void BuildEmergencyMockGraph()
        {
            if (!ResetGraphBuffers())
                return;
            GenerateEmergencyMockProfiles();

            _activeGridHandle = 0x53484D47u;
            _activeSectorHash = 0x5348494E4F425531UL;
            _activeGridHash = 0x4D4F434Bu;
            _activeGenerationSequence = 1u;

            GenerateMockLogisticsGraphJob mockTopologyJob = new GenerateMockLogisticsGraphJob
            {
                SectorHash = _activeSectorHash,
                Nodes = _nodes,
                Edges = _edges,
                StateFlags = _stateFlags,
                OxygenFront = _oxygenFront,
                OxygenBack = _oxygenBack,
                InternalPressureKpa = _internalPressureKpa,
                ExternalPressureKpa = _externalPressureKpa,
                YieldThresholdKpa = _yieldThresholdKpa,
                Reinforcement = _reinforcement,
                NodeAup = _nodeAup,
                LocalPositions = _localPositions,
                PriorityTier = _priorityTier,
                CellToNode = _cellToNode,
                Counters = _counters
            };
            mockTopologyJob.Execute(); // COLD SYNC JOB: emergency mock topology is generated once before any solver snapshot can exist.

            _nodeCount = _counters[CounterNodeCount];
            _edgeCount = _counters[CounterEdgeCount];

            _hasGraph = true;
            ScheduleCsrRebuild();
        }

        private void GenerateEmergencyMockProfiles()
        {
            LogisticsTuningDTO tuning = EmergencyTuning();
            _tuning[0] = tuning;
            _offlineTuning = tuning;
        }

        private static LogisticsTuningDTO EmergencyTuning()
        {
            return new LogisticsTuningDTO
            {
                ReactorOutputWatts = 1000f,
                LifeSupportDrainWatts = 12f,
                OxygenDiffusionRate = 0.18f,
                CrushDepthMultiplier = 1f,
                BasePipeResistance = DefaultBasePipeResistance,
                JacobiSmoothingFactor = DefaultJacobiSmoothing,
                GlobalQualityWeight = 1f,
                Flags = 0u
            };
        }

        private bool ResetGraphBuffers()
        {
            if (_solvePending || _csrRebuildPending || _localShiftPending)
            {
                if (_counters.IsCreated)
                    _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.LayoutFault;
                return false;
            }

            _nodeCount = 0;
            _edgeCount = 0;
            _hasGraph = false;
            for (int i = 0; i < _cellToNode.Length; i++)
                _cellToNode[i] = -1;
            for (int i = 0; i < _counters.Length; i++)
                _counters[i] = 0;
            return true;
        }

        private void RebuildAdjacencyFromEdges()
        {
            ScheduleCsrRebuild();
        }

        private void RebuildCsrFromEdges()
        {
            ScheduleCsrRebuild();
        }

        private void ScheduleCsrRebuild()
        {
            if (!_counters.IsCreated || !_csrEdgeCapacities.IsCreated || !_csrEdgeFlow01.IsCreated || !_nodes.IsCreated)
                return;

            if (_csrRebuildPending)
                return;

            _csrRebuildHandle = new BuildCsrGraphJob
            {
                NodesPtr = (LogisticsNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                NodeCount = math.clamp(_nodeCount, 0, MaxNodes),
                EdgeCount = math.clamp(_edgeCount, 0, MaxDirectedEdges),
                Edges = _edges,
                StateFlags = _stateFlags,
                Counters = _counters,
                CsrEdgeCapacities = _csrEdgeCapacities,
                CsrEdgeFlow01 = _csrEdgeFlow01,
                EdgeOffsetsBaseIndex = EdgeOffsetsBase,
                EdgeWriteCursorBaseIndex = EdgeWriteCursorBase,
                EdgeDestinationsBaseIndex = EdgeDestinationsBase
            }.Schedule();
            _csrRebuildPending = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsTraversableEdge(int2 edge, int nodeCount)
        {
            if ((uint)edge.x >= (uint)nodeCount || (uint)edge.y >= (uint)nodeCount)
                return false;

            return (_stateFlags[edge.x] & LogisticsStateFlags.Destroyed) == 0 &&
                   (_stateFlags[edge.y] & LogisticsStateFlags.Destroyed) == 0;
        }

        private void AddUndirectedEdge(int nodeA, int nodeB)
        {
            if ((uint)nodeA >= _nodeCount || (uint)nodeB >= _nodeCount || nodeA == nodeB || _edgeCount >= MaxDirectedEdges)
                return;

            int low = math.min(nodeA, nodeB);
            int high = math.max(nodeA, nodeB);
            for (int i = 0; i < _edgeCount; i++)
            {
                int2 existing = _edges[i].Nodes;
                if (existing.x == low && existing.y == high)
                    return;
            }

            _edges[_edgeCount++] = new LogisticsEdgeDTO
            {
                Nodes = new int2(low, high),
                Capacity = ResolveEdgeCapacity(low, high),
                Resistance = ResolveEdgeResistance(low, high),
                Flow01 = 0f,
                LastMilliTransfer = 0,
                Flags = 0u
            };
        }

        private void TryConnectPlanar(
            NativeArray<byte> cells,
            int nodeIndex,
            byte packed,
            int3 cell,
            int3 dimensions,
            int dx,
            int dz,
            byte exitFlag,
            byte reciprocalFlag)
        {
            int nx = cell.x + dx;
            int nz = cell.z + dz;
            if (nx < 0 || nz < 0 || nx >= dimensions.x || nz >= dimensions.z)
                return;

            int neighborCell = WfcOutpostGridConstants.Flatten(nx, cell.y, nz, dimensions);
            if ((uint)neighborCell >= _cellToNode.Length)
                return;

            int neighborNode = _cellToNode[neighborCell];
            if ((uint)neighborNode >= _nodeCount)
                return;

            byte neighborPacked = cells[neighborCell];
            if ((packed & exitFlag) != 0 || (neighborPacked & reciprocalFlag) != 0)
                AddUndirectedEdge(nodeIndex, neighborNode);
            else if (WfcOutpostGridConstants.IsRoomLikeKind(packed))
                AddUndirectedEdge(nodeIndex, neighborNode);
        }

        private void TryConnectVertical(int3 cell, int3 dimensions, int dy, int nodeIndex)
        {
            int ny = cell.y + dy;
            if (ny < 0 || ny >= dimensions.y)
                return;

            int neighborCell = WfcOutpostGridConstants.Flatten(cell.x, ny, cell.z, dimensions);
            if ((uint)neighborCell >= _cellToNode.Length)
                return;

            int neighborNode = _cellToNode[neighborCell];
            if ((uint)neighborNode < _nodeCount)
                AddUndirectedEdge(nodeIndex, neighborNode);
        }

        private int FindDockingNode()
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                if ((_stateFlags[i] & LogisticsStateFlags.DockingPort) != 0)
                    return i;
            }

            return _nodeCount > 0 ? _nodeCount - 1 : -1;
        }

        private bool HasGenerator()
        {
            for (int i = 0; i < _nodeCount; i++)
            {
                if ((_stateFlags[i] & LogisticsStateFlags.PowerGenerator) != 0)
                    return true;
            }

            return false;
        }

        private void PublishSolveSideEffects()
        {
            int breachSignalCount = _counters.IsCreated
                ? math.clamp(_counters[CounterBreachSignalCount], 0, MaxNodes)
                : 0;
            for (int i = 0; i < breachSignalCount; i++)
            {
                int nodeIndex = _counters[BreachNodeBase + i];
                if (!PublishFluidIncursion(nodeIndex))
                    _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.SignalOverflow;
            }
            if (_counters.IsCreated)
                _counters[CounterBreachSignalCount] = 0;

            int faultFlags = _counters[CounterFaultFlags];
            if ((faultFlags & (LogisticsGraphFaultFlags.InfiniteLoopGuard | LogisticsGraphFaultFlags.OxygenNan)) != 0)
                DumpBlackBox(faultFlags);

            int nodeCount = math.max(1, _counters[CounterNodeCount]);
            int unpowered = _counters[CounterUnpoweredCount];
            if (unpowered > 0)
            {
                BrownoutSignal signal = new BrownoutSignal
                {
                    NetworkId = _activeGridHandle,
                    NodeId = 0u,
                    SupplyRatio = 1f - math.saturate(unpowered / (float)nodeCount),
                    Severity01 = math.saturate(unpowered / (float)nodeCount),
                    Frame = (uint)_frameIndex,
                    Priority = 90,
                    Flags = 1 << 2
                };
                SignalBus<BrownoutSignal>.Push(in signal);
            }
        }

        private void PatchLatestTelemetryMicros()
        {
            if (!_blackBox.IsCreated)
                return;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _solveStartTimestamp;
            long frequency = System.Diagnostics.Stopwatch.Frequency;
            long micros64 = frequency > 0 ? (elapsedTicks * 1000000L) / frequency : 0L;
            int micros = micros64 > int.MaxValue ? int.MaxValue : (int)micros64;
            int index = _frameIndex % TelemetryFrames;
            if ((uint)index >= (uint)_blackBox.Length)
                return;

            LogisticsGraphTelemetryEntry entry = _blackBox[index];
            entry.SolverMicros = micros;
            _blackBox[index] = entry;
        }

        private void PublishFlowVisuals()
        {
            if (!_edges.IsCreated || !_nodes.IsCreated)
                return;

            IConnectionSplineBatchRendererService renderer = _pipeRenderer;
            if (renderer == null)
                return;

            int edgeCount = math.min(_edgeCount, _edges.Length);
            if (edgeCount <= 0)
            {
                _flowVisualPublishCursor = 0;
                return;
            }

            float quality = math.saturate(math.isfinite(_globalQualityWeight) ? _globalQualityWeight : 0f);
            float qualityCurve = quality * quality * (3f - (2f * quality));
            int publishBudget = math.clamp(
                (int)math.round(math.lerp(32f, edgeCount, qualityCurve)),
                1,
                edgeCount);
            int cursor = math.clamp(_flowVisualPublishCursor, 0, edgeCount - 1);
            for (int published = 0; published < publishBudget; published++)
            {
                int edgeIndex = cursor + published;
                if (edgeIndex >= edgeCount)
                    edgeIndex -= edgeCount;

                LogisticsEdgeDTO edge = _edges[edgeIndex];
                int2 nodes = edge.Nodes;
                float flow = math.saturate(edge.Flow01);
                if ((uint)nodes.x < (uint)_nodeCount)
                    renderer.SetPipeNodeFlow((uint)nodes.x, flow);
                if ((uint)nodes.y < (uint)_nodeCount)
                    renderer.SetPipeNodeFlow((uint)nodes.y, flow);
            }

            _flowVisualPublishCursor = cursor + publishBudget;
            if (_flowVisualPublishCursor >= edgeCount)
                _flowVisualPublishCursor -= edgeCount;
        }

        private void DumpBlackBox(int faultFlags)
        {
            WriteBlackBoxDump(faultFlags, DumpRelativePath);
            WriteBlackBoxDump(faultFlags, DumpSurgeonRelativePath);
            WriteBlackBoxDump(faultFlags, DumpH8RelativePath);
        }

        private void WriteBlackBoxDump(int faultFlags, string relativePath)
        {
            try
            {
                string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(SourceHash);
                    writer.Write(faultFlags | LogisticsGraphFaultFlags.DumpedBlackBox);
                    writer.Write(_frameIndex);
                    writer.Write(_nodeCount);
                    writer.Write(_edgeCount);
                    for (int i = 0; i < _blackBox.Length; i++)
                    {
                        LogisticsGraphTelemetryEntry entry = _blackBox[i];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.NodeCount);
                        writer.Write(entry.ActiveNodeCount);
                        writer.Write(entry.FaultFlags);
                        writer.Write(entry.TotalPowerGenerated);
                        writer.Write(entry.TotalPowerConsumed);
                        writer.Write(entry.TotalOxygen01);
                        writer.Write(entry.SupplyRatio);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.BreachedCount);
                        writer.Write(entry.UnpoweredCount);
                        writer.Write(entry.OxygenCadence);
                        writer.Write(entry.ComponentCount);
                        writer.Write(entry.JacobiIterations);
                        writer.Write(entry.SolverMicros);
                    }
                }
            }
            catch (Exception exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x5348444Du, SourceHash, exception.HResult);
            }
        }

        private bool PublishFluidIncursion(int nodeIndex)
        {
            if ((uint)nodeIndex >= (uint)_nodeCount)
                return false;

            float pressureDeltaKpa = 0f;
            if (_externalPressureKpa.IsCreated && _internalPressureKpa.IsCreated &&
                (uint)nodeIndex < (uint)_externalPressureKpa.Length &&
                (uint)nodeIndex < (uint)_internalPressureKpa.Length)
            {
                float externalPressure = FiniteOr(_externalPressureKpa[nodeIndex], 0f);
                float internalPressure = FiniteOr(_internalPressureKpa[nodeIndex], externalPressure);
                pressureDeltaKpa = math.max(0f, externalPressure - internalPressure);
            }

            AbsoluteUniversePosition leakAup = AbsoluteUniversePosition.FromAbsolutePosition(_nodeAup[nodeIndex]);
            FluidIncursionSignal incursion = new FluidIncursionSignal
            {
                LeakAup = leakAup,
                CompartmentId = (uint)nodeIndex,
                FloodLevel01 = 1f,
                FlowRate01 = math.saturate(pressureDeltaKpa * 0.001f),
                Flags = 1
            };
            return SignalBus<FluidIncursionSignal>.TryPush(in incursion);
        }

        private void TryReloadCsvOverrides()
        {
            string path = _csvPath;
            if (string.IsNullOrEmpty(path))
            {
                path = ResolveCsvPath();
                _csvPath = path;
            }

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc == _csvLastWriteUtc)
                return;

            if (!_csvScratch.IsCreated)
                return;

            try
            {
                int read = ReadFileIntoNativeScratch(path, _csvScratch);
                if (read <= 0)
                {
                    _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CsvParseFault;
                    GlobalTelemetryBus.PublishPerformanceWarning(0x5348435Au, SourceHash, 0);
                    return;
                }

                ParseCsv(read);
                _csvLastWriteUtc = writeUtc;
            }
            catch (Exception exception)
            {
                _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CsvParseFault;
                GlobalTelemetryBus.PublishPerformanceWarning(0x53484353u, SourceHash, exception.HResult);
            }
        }

        private static int ReadFileIntoNativeScratch(string path, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || string.IsNullOrEmpty(path))
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long streamLength = stream.Length;
                int length = streamLength > scratch.Length ? scratch.Length : (int)streamLength;
                void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                Span<byte> span = new Span<byte>(ptr, length);
                int read = 0;
                while (read < length)
                {
                    int chunk = stream.Read(span.Slice(read, length - read));
                    if (chunk <= 0)
                        break;

                    read += chunk;
                }

                return read;
            }
        }

        private string ResolveCsvPath()
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(root, "Assets", "_SourceData", "Power", "logistics_components.csv");
        }

        private void ParseCsv(int length)
        {
            if (length <= 0)
                return;

            LogisticsTuningDTO tuning = _tuning[0];
            int index = 0;
            while (index < length)
            {
                int keyStart = index;
                while (index < length && _csvScratch[index] != (byte)',' && _csvScratch[index] != (byte)'=' && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r')
                    index++;
                int keyLength = index - keyStart;
                if (index >= length)
                    break;

                byte separator = _csvScratch[index++];
                if (separator == (byte)'\n' || separator == (byte)'\r')
                    continue;

                int valueStart = index;
                while (index < length && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r' && _csvScratch[index] != (byte)',')
                    index++;
                int valueLength = index - valueStart;
                float value = ParseFloatAscii(valueStart, valueLength);
                uint keyHash = HashCsvKey(keyStart, keyLength);

                if (KeyEquals(keyStart, keyLength, "reactoroutput", keyHash))
                    tuning.ReactorOutputWatts = value;
                else if (KeyEquals(keyStart, keyLength, "lifesupportdrain", keyHash))
                    tuning.LifeSupportDrainWatts = value;
                else if (KeyEquals(keyStart, keyLength, "oxygendiffusionrate", keyHash))
                    tuning.OxygenDiffusionRate = value;
                else if (KeyEquals(keyStart, keyLength, "crushdepthmultiplier", keyHash))
                    tuning.CrushDepthMultiplier = value;
                else if (KeyEquals(keyStart, keyLength, "basepiperesistance", keyHash))
                    tuning.BasePipeResistance = value;
                else if (KeyEquals(keyStart, keyLength, "jacobismoothingfactor", keyHash))
                    tuning.JacobiSmoothingFactor = value;
                else if (KeyEquals(keyStart, keyLength, "globalqualityweight", keyHash))
                    tuning.GlobalQualityWeight = value;

                while (index < length && (_csvScratch[index] == (byte)'\n' || _csvScratch[index] == (byte)'\r' || _csvScratch[index] == (byte)','))
                    index++;
            }

            SetTuning(tuning);
            ParseComponentSpecs(length);
        }

        private void ParseComponentSpecs(int length)
        {
            if (!_componentSpecs.IsCreated || length <= 0)
                return;

            for (int i = 0; i < _componentSpecs.Length; i++)
                _componentSpecs[i] = default;

            int write = 0;
            int index = 0;
            while (index < length && write < _componentSpecs.Length)
            {
                int nameStart = index;
                while (index < length && _csvScratch[index] != (byte)',' && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r')
                    index++;
                int nameLength = index - nameStart;
                if (index >= length || _csvScratch[index] != (byte)',')
                {
                    while (index < length && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r')
                        index++;
                    while (index < length && (_csvScratch[index] == (byte)'\n' || _csvScratch[index] == (byte)'\r'))
                        index++;
                    continue;
                }

                index++;
                int capacityStart = index;
                while (index < length && _csvScratch[index] != (byte)',' && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r')
                    index++;
                int capacityLength = index - capacityStart;
                if (index >= length || _csvScratch[index] != (byte)',')
                    continue;

                index++;
                int resistanceStart = index;
                while (index < length && _csvScratch[index] != (byte)',' && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r')
                    index++;
                int resistanceLength = index - resistanceStart;

                float oxygenDemand = 0.08f;
                if (index < length && _csvScratch[index] == (byte)',')
                {
                    index++;
                    int oxygenStart = index;
                    while (index < length && _csvScratch[index] != (byte)'\n' && _csvScratch[index] != (byte)'\r')
                        index++;
                    oxygenDemand = ParseFloatAscii(oxygenStart, index - oxygenStart);
                }

                uint hash = HashCsvKey(nameStart, nameLength);
                if (hash != 0u && capacityLength > 0 && resistanceLength > 0)
                {
                    LogisticsComponentSpecDTO spec = new LogisticsComponentSpecDTO
                    {
                        ModuleHash = hash,
                        Capacity = math.max(0f, ParseFloatAscii(capacityStart, capacityLength)),
                        Resistance = math.max(0.001f, ParseFloatAscii(resistanceStart, resistanceLength)),
                        OxygenDemand = math.max(0f, oxygenDemand),
                        Flags = 0u
                    };
                    if (TryInsertComponentSpec(in spec))
                        write++;
                }

                while (index < length && (_csvScratch[index] == (byte)'\n' || _csvScratch[index] == (byte)'\r'))
                    index++;
            }

            _counters[CounterSpecCount] = write;
        }

        private bool TryInsertComponentSpec(in LogisticsComponentSpecDTO spec)
        {
            if (!_componentSpecs.IsCreated || spec.ModuleHash == 0u)
                return false;

            int length = _componentSpecs.Length;
            int slot = (int)(spec.ModuleHash % (uint)length);
            for (int probe = 0; probe < length; probe++)
            {
                int index = (slot + probe) % length;
                uint existing = _componentSpecs[index].ModuleHash;
                if (existing == 0u || existing == spec.ModuleHash)
                {
                    _componentSpecs[index] = spec;
                    return existing == 0u;
                }
            }

            _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CsvParseFault;
            return false;
        }

        private bool KeyEquals(int start, int length, string literal, uint keyHash)
        {
            if (HashLiteral(literal) != keyHash)
                return false;

            int literalIndex = 0;
            for (int i = 0; i < length; i++)
            {
                byte b = _csvScratch[start + i];
                if (b == (byte)'_' || b == (byte)' ' || b == (byte)'-')
                    continue;
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                if (literalIndex >= literal.Length || b != literal[literalIndex])
                    return false;
                literalIndex++;
            }

            return literalIndex == literal.Length;
        }

        private uint HashCsvKey(int start, int length)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < length; i++)
            {
                byte b = _csvScratch[start + i];
                if (b == (byte)'_' || b == (byte)' ' || b == (byte)'-')
                    continue;
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash = (hash ^ b) * 16777619u;
            }

            return hash;
        }

        private static uint HashLiteral(string literal)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < literal.Length; i++)
                hash = (hash ^ (byte)literal[i]) * 16777619u;
            return hash;
        }

        private float ParseFloatAscii(int start, int length)
        {
            if (length <= 0)
                return 0f;

            int end = start + length;
            int index = start;
            bool negative = false;
            if (index < end && _csvScratch[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            float value = 0f;
            while (index < end)
            {
                byte b = _csvScratch[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                value = (value * 10f) + (b - (byte)'0');
                index++;
            }

            if (index < end && _csvScratch[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < end)
                {
                    byte b = _csvScratch[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;
                    value += (b - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                }
            }

            return negative ? -value : value;
        }

        private static LogisticsTuningDTO SanitizeTuning(in LogisticsTuningDTO tuning)
        {
            return new LogisticsTuningDTO
            {
                ReactorOutputWatts = math.clamp(FiniteOr(tuning.ReactorOutputWatts, 1000f), 0f, 100000f),
                LifeSupportDrainWatts = math.clamp(FiniteOr(tuning.LifeSupportDrainWatts, 12f), 0f, 1000f),
                OxygenDiffusionRate = math.clamp(FiniteOr(tuning.OxygenDiffusionRate, 0.18f), 0.01f, 2f),
                CrushDepthMultiplier = math.clamp(FiniteOr(tuning.CrushDepthMultiplier, 1f), 0.1f, 10f),
                BasePipeResistance = math.clamp(FiniteOr(tuning.BasePipeResistance, DefaultBasePipeResistance), 0.001f, 8f),
                JacobiSmoothingFactor = math.clamp(FiniteOr(tuning.JacobiSmoothingFactor, DefaultJacobiSmoothing), 0.05f, 1f),
                GlobalQualityWeight = math.saturate(FiniteOr(tuning.GlobalQualityWeight, 1f)),
                Flags = tuning.Flags
            };
        }

        private static void ConfigurePublicSignalLanes()
        {
            SignalBus<FluidIncursionSignal>.Configure(
                FluidIncursionSignalCapacity,
                FluidIncursionSignalCapacity,
                FluidIncursionSignalCapacity,
                SourceHash);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static int3 Unflatten(int index, int3 dimensions)
        {
            int x = index % dimensions.x;
            int yz = index / dimensions.x;
            int z = yz % dimensions.z;
            int y = yz / dimensions.z;
            return new int3(x, y, z);
        }

        private static ulong ResolveInitialStateFlags(byte kind, int3 cell, int3 dimensions)
        {
            ulong flags = LogisticsStateFlags.Unpowered;
            if (kind == WfcOutpostGridConstants.Generator)
                flags |= LogisticsStateFlags.PowerGenerator | LogisticsStateFlags.LifeSupport;
            if (kind == WfcOutpostGridConstants.Room || kind == WfcOutpostGridConstants.Hatch || kind == WfcOutpostGridConstants.SealedDoor)
                flags |= LogisticsStateFlags.LifeSupport;
            if (kind == WfcOutpostGridConstants.Datapad)
                flags |= LogisticsStateFlags.Fabricator;
            if (kind == WfcOutpostGridConstants.SealedDoor)
                flags |= LogisticsStateFlags.DoorLocked;
            if (kind == WfcOutpostGridConstants.Hatch && (cell.x == 0 || cell.z == 0 || cell.x == dimensions.x - 1 || cell.z == dimensions.z - 1))
                flags |= LogisticsStateFlags.DockingPort;
            return flags;
        }

        private static float ResolvePowerDemand(byte kind)
        {
            if (kind == WfcOutpostGridConstants.Generator)
                return 0f;
            if (kind == WfcOutpostGridConstants.Corridor)
                return 5f;
            if (kind == WfcOutpostGridConstants.Datapad)
                return 45f;
            if (kind == WfcOutpostGridConstants.Hatch || kind == WfcOutpostGridConstants.SealedDoor)
                return 10f;
            return 18f;
        }

        private static uint ComputeNodeHash(ulong sectorHash, int nodeIndex, byte kind)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)sectorHash) * 16777619u;
            hash = (hash ^ (uint)(sectorHash >> 32)) * 16777619u;
            hash = (hash ^ (uint)nodeIndex) * 16777619u;
            hash = (hash ^ kind) * 16777619u;
            return hash != 0u ? hash : 1u;
        }

        private float ResolveEdgeCapacity(int nodeA, int nodeB)
        {
            float demandA = (uint)nodeA < (uint)_nodes.Length ? math.max(1f, _nodes[nodeA].Capacity) : 1f;
            float demandB = (uint)nodeB < (uint)_nodes.Length ? math.max(1f, _nodes[nodeB].Capacity) : 1f;
            return math.max(1f, 120f - math.min(80f, (demandA + demandB) * 0.25f));
        }

        private float ResolveEdgeResistance(int nodeA, int nodeB)
        {
            if ((uint)nodeA >= (uint)_nodeAup.Length || (uint)nodeB >= (uint)_nodeAup.Length)
                return DefaultBasePipeResistance;

            float length = CalculateAupEdgeLength(_nodeAup[nodeA], _nodeAup[nodeB]);
            return math.max(0.001f, DefaultBasePipeResistance + length * 0.0025f);
        }

        public static float CalculateAupEdgeLength(double3 nodeAup, double3 nodeBup)
        {
            double3 delta = nodeBup - nodeAup;
            if (!math.all(math.isfinite(delta)))
                return 0f;

            float distanceSq = math.lengthsq(new float3((float)delta.x, (float)delta.y, (float)delta.z));
            return distanceSq <= 0.000001f ? 0f : distanceSq * math.rsqrt(math.max(distanceSq, 0.000001f));
        }

        private static float ResolveOxygenDemand(byte kind)
        {
            if (kind == WfcOutpostGridConstants.Room)
                return 1.2f;
            if (kind == WfcOutpostGridConstants.Hatch || kind == WfcOutpostGridConstants.SealedDoor)
                return 0.35f;
            return 0.08f;
        }

        private static float ResolveReinforcement(byte kind)
        {
            if (kind == WfcOutpostGridConstants.Pillar)
                return 2f;
            if (kind == WfcOutpostGridConstants.Window)
                return 0.55f;
            if (kind == WfcOutpostGridConstants.Generator)
                return 1.4f;
            return 1f;
        }

        private static byte ResolvePriority(byte kind)
        {
            if (kind == WfcOutpostGridConstants.Room || kind == WfcOutpostGridConstants.Hatch || kind == WfcOutpostGridConstants.SealedDoor || kind == WfcOutpostGridConstants.Generator)
                return PriorityLifeSupport;
            if (kind == WfcOutpostGridConstants.Corridor)
                return PriorityCorridor;
            if (kind == WfcOutpostGridConstants.Datapad)
                return PriorityIndustrial;
            return PriorityOptional;
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct LogisticsGraphInitializeJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<LogisticsNodeDTO> Nodes;
            [NoAlias] public NativeArray<ulong> StateFlags;
            [NoAlias] public NativeArray<float> OxygenFront;
            [NoAlias] public NativeArray<float> OxygenBack;
            [NoAlias] public NativeArray<float> InternalPressureKpa;
            [NoAlias] public NativeArray<float> ExternalPressureKpa;
            [NoAlias] public NativeArray<float> YieldThresholdKpa;
            [NoAlias] public NativeArray<float> Reinforcement;
            [NoAlias] public NativeArray<double3> NodeAup;
            [NoAlias] public NativeArray<float3> LocalPositions;
            [NoAlias] public NativeArray<byte> PriorityTier;
            [NoAlias] public NativeArray<byte> Visited;
            [NoAlias] public NativeArray<int> CellToNode;
            [NoAlias] public NativeArray<int> ComponentIds;
            [NoAlias] public NativeArray<float> PressureFront;
            [NoAlias] public NativeArray<float> PressureBack;

            public void Execute(int index)
            {
                Nodes[index] = new LogisticsNodeDTO
                {
                    NodeHash = (uint)(index + 1),
                    Capacity = 0f,
                    CurrentLoad = 0f,
                    Flags = (uint)LogisticsStateFlags.Unpowered,
                    EdgeStartIndex = -1,
                    EdgeCount = 0,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                StateFlags[index] = LogisticsStateFlags.Unpowered;
                OxygenFront[index] = 100f;
                OxygenBack[index] = 100f;
                PressureFront[index] = 0f;
                PressureBack[index] = 0f;
                InternalPressureKpa[index] = 101.3f;
                ExternalPressureKpa[index] = 101.3f;
                YieldThresholdKpa[index] = 650f;
                Reinforcement[index] = 1f;
                NodeAup[index] = default;
                LocalPositions[index] = default;
                PriorityTier[index] = PriorityOptional;
                Visited[index] = 0;
                ComponentIds[index] = -1;
                CellToNode[index] = -1;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct GenerateMockLogisticsGraphJob : IJob
        {
            public ulong SectorHash;
            [NoAlias] public NativeArray<LogisticsNodeDTO> Nodes;
            [NoAlias] public NativeArray<LogisticsEdgeDTO> Edges;
            [NoAlias] public NativeArray<ulong> StateFlags;
            [NoAlias] public NativeArray<float> OxygenFront;
            [NoAlias] public NativeArray<float> OxygenBack;
            [NoAlias] public NativeArray<float> InternalPressureKpa;
            [NoAlias] public NativeArray<float> ExternalPressureKpa;
            [NoAlias] public NativeArray<float> YieldThresholdKpa;
            [NoAlias] public NativeArray<float> Reinforcement;
            [NoAlias] public NativeArray<double3> NodeAup;
            [NoAlias] public NativeArray<float3> LocalPositions;
            [NoAlias] public NativeArray<byte> PriorityTier;
            [NoAlias] public NativeArray<int> CellToNode;
            [NoAlias] public NativeArray<int> Counters;

            public void Execute()
            {
                int nodeCount = math.min(1000, Nodes.Length);
                int targetEdges = math.min(2500, Edges.Length);
                double3 origin = new double3(25000.0, -480.0, 25000.0);

                for (int i = 0; i < nodeCount; i++)
                {
                    byte kind = MockWFCGraphGenerator.ResolveKind(i);
                    ulong flags = ResolveInitialStateFlags(kind, new int3(i % 40, i / 400, (i / 10) % 40), new int3(40, 3, 40));
                    uint nodeHash = ComputeNodeHash(SectorHash, i, kind);
                    float demand = ResolvePowerDemand(kind);
                    Nodes[i] = new LogisticsNodeDTO
                    {
                        NodeHash = nodeHash,
                        Capacity = demand,
                        CurrentLoad = 0f,
                        Flags = (uint)flags,
                        EdgeStartIndex = -1,
                        EdgeCount = 0,
                        _pad0 = 0u,
                        _pad1 = 0u
                    };
                    StateFlags[i] = flags;
                    OxygenFront[i] = i == nodeCount - 1 ? 55f : 100f;
                    OxygenBack[i] = OxygenFront[i];
                    InternalPressureKpa[i] = 101.3f;
                    ExternalPressureKpa[i] = 101.3f + ((i % 71) * 11f);
                    float reinforcement = ResolveReinforcement(kind);
                    YieldThresholdKpa[i] = 650f + reinforcement * 220f;
                    Reinforcement[i] = reinforcement;
                    double3 local = new double3((i % 40) * 6.0, (i / 400) * 4.0, ((i / 40) % 25) * 6.0);
                    NodeAup[i] = origin + local;
                    LocalPositions[i] = new float3((float)local.x, (float)local.y, (float)local.z);
                    PriorityTier[i] = ResolvePriority(kind);
                    CellToNode[i] = i;
                }

                int edgeCount = 0;
                for (int i = 0; i < nodeCount - 1 && edgeCount < targetEdges; i++)
                    AddEdge(ref edgeCount, i, i + 1);

                for (int i = 0; i < nodeCount && edgeCount < targetEdges; i++)
                    AddEdge(ref edgeCount, i, (i + 37) % nodeCount);

                uint lcg = 0xA1145EEDu;
                int guard = 0;
                while (edgeCount < targetEdges && guard < targetEdges * 8)
                {
                    lcg = (lcg * 1664525u) + 1013904223u;
                    int a = (int)(lcg % (uint)nodeCount);
                    lcg = (lcg * 1664525u) + 1013904223u;
                    int b = (int)(lcg % (uint)nodeCount);
                    AddEdge(ref edgeCount, a, b);
                    guard++;
                }

                Counters[CounterNodeCount] = nodeCount;
                Counters[CounterEdgeCount] = edgeCount;
                if (edgeCount < targetEdges)
                    Counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CapacityExceeded;
            }

            private void AddEdge(ref int edgeCount, int nodeA, int nodeB)
            {
                if ((uint)nodeA >= (uint)Nodes.Length || (uint)nodeB >= (uint)Nodes.Length || nodeA == nodeB || edgeCount >= Edges.Length)
                    return;

                int low = math.min(nodeA, nodeB);
                int high = math.max(nodeA, nodeB);
                for (int i = 0; i < edgeCount; i++)
                {
                    int2 existing = Edges[i].Nodes;
                    if (existing.x == low && existing.y == high)
                        return;
                }

                double3 delta = NodeAup[high] - NodeAup[low];
                float lengthSq = math.lengthsq(new float3((float)delta.x, (float)delta.y, (float)delta.z));
                float length = lengthSq <= 0.000001f ? 0f : lengthSq * math.rsqrt(math.max(lengthSq, 0.000001f));
                float resistance = math.max(0.001f, DefaultBasePipeResistance + length * 0.0025f);
                float capacity = math.max(1f, 120f - math.min(80f, (Nodes[low].Capacity + Nodes[high].Capacity) * 0.25f));
                Edges[edgeCount++] = new LogisticsEdgeDTO
                {
                    Nodes = new int2(low, high),
                    Capacity = capacity,
                    Resistance = resistance,
                    Flow01 = 0f,
                    LastMilliTransfer = 0,
                    Flags = 0u,
                    _pad0 = 0u
                };
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private unsafe struct BuildCsrGraphJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public LogisticsNodeDTO* NodesPtr;
            public int NodeCount;
            public int EdgeCount;
            [ReadOnly] [NoAlias] public NativeArray<LogisticsEdgeDTO> Edges;
            [ReadOnly] [NoAlias] public NativeArray<ulong> StateFlags;
            [NoAlias] public NativeArray<int> Counters;
            [NoAlias] public NativeArray<float> CsrEdgeCapacities;
            [NoAlias] public NativeArray<float> CsrEdgeFlow01;
            public int EdgeOffsetsBaseIndex;
            public int EdgeWriteCursorBaseIndex;
            public int EdgeDestinationsBaseIndex;

            public void Execute()
            {
                int nodeCount = math.clamp(NodeCount, 0, MaxNodes);
                int edgeCount = math.clamp(EdgeCount, 0, math.min(MaxDirectedEdges, Edges.Length));
                int maxEntries = math.min(MaxAdjacencyEntries, math.min(CsrEdgeCapacities.Length, CsrEdgeFlow01.Length));
                int faultFlags = Counters[CounterFaultFlags];

                for (int i = 0; i <= nodeCount; i++)
                    Counters[EdgeOffsetsBaseIndex + i] = 0;
                for (int i = 0; i < nodeCount; i++)
                {
                    Counters[EdgeWriteCursorBaseIndex + i] = 0;
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    node.EdgeStartIndex = -1;
                    node.EdgeCount = 0;
                }

                int adjacencyCount = 0;
                for (int i = 0; i < edgeCount; i++)
                {
                    int2 edge = Edges[i].Nodes;
                    if (!IsTraversable(edge, nodeCount))
                        continue;

                    if (adjacencyCount + 2 > maxEntries)
                    {
                        faultFlags |= LogisticsGraphFaultFlags.CapacityExceeded;
                        break;
                    }

                    Counters[EdgeOffsetsBaseIndex + edge.x + 1]++;
                    Counters[EdgeOffsetsBaseIndex + edge.y + 1]++;
                    adjacencyCount += 2;
                }

                int prefix = 0;
                for (int i = 0; i < nodeCount; i++)
                {
                    int degree = Counters[EdgeOffsetsBaseIndex + i + 1];
                    Counters[EdgeOffsetsBaseIndex + i] = prefix;
                    Counters[EdgeWriteCursorBaseIndex + i] = prefix;
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    node.EdgeStartIndex = prefix;
                    node.EdgeCount = degree;
                    prefix += degree;
                }

                Counters[EdgeOffsetsBaseIndex + nodeCount] = prefix;
                for (int i = 0; i < edgeCount; i++)
                {
                    LogisticsEdgeDTO edgeDto = Edges[i];
                    int2 edge = edgeDto.Nodes;
                    if (!IsTraversable(edge, nodeCount))
                        continue;

                    int writeA = Counters[EdgeWriteCursorBaseIndex + edge.x]++;
                    int writeB = Counters[EdgeWriteCursorBaseIndex + edge.y]++;
                    float conductance = ResolveConductance(edgeDto, edge);
                    if ((uint)writeA < (uint)maxEntries)
                    {
                        Counters[EdgeDestinationsBaseIndex + writeA] = edge.y;
                        CsrEdgeCapacities[writeA] = conductance;
                        CsrEdgeFlow01[writeA] = 0f;
                    }

                    if ((uint)writeB < (uint)maxEntries)
                    {
                        Counters[EdgeDestinationsBaseIndex + writeB] = edge.x;
                        CsrEdgeCapacities[writeB] = conductance;
                        CsrEdgeFlow01[writeB] = 0f;
                    }
                }

                Counters[CounterNodeCount] = nodeCount;
                Counters[CounterEdgeCount] = edgeCount;
                Counters[CounterAdjacencyEntryCount] = prefix;
                Counters[CounterFaultFlags] = faultFlags;
            }

            private bool IsTraversable(int2 edge, int nodeCount)
            {
                if ((uint)edge.x >= (uint)nodeCount || (uint)edge.y >= (uint)nodeCount)
                    return false;

                return true;
            }

            private float ResolveConductance(in LogisticsEdgeDTO edgeDto, int2 edge)
            {
                ulong isolated = LogisticsStateFlags.Destroyed | LogisticsStateFlags.Breached | LogisticsStateFlags.DoorLocked;
                if (((StateFlags[edge.x] | StateFlags[edge.y]) & isolated) != 0UL)
                    return 0f;

                float capacity = math.max(0f, math.isfinite(edgeDto.Capacity) ? edgeDto.Capacity : 0f);
                float resistance = math.max(0.001f, math.isfinite(edgeDto.Resistance) ? edgeDto.Resistance : 0.001f);
                return capacity / resistance;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private unsafe struct LogisticsFlowPrepareJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public LogisticsNodeDTO* NodesPtr;
            public int NodeCount;
            public int EdgeOffsetsBaseIndex;
            public int EdgeDestinationsBaseIndex;
            public int AdjacencyEntryCount;
            [NoAlias] public NativeArray<ulong> StateFlags;
            [NoAlias] public NativeArray<int> ComponentIds;
            [NoAlias] public NativeArray<float> PressureFront;
            [NoAlias] public NativeArray<float> PressureBack;
            [NoAlias] public NativeArray<byte> Visited;
            [NoAlias] public NativeArray<int> Counters;
            public int BfsQueueBaseIndex;
            public int ReachableBaseIndex;

            public void Execute()
            {
                int nodeCount = math.clamp(NodeCount, 0, MaxNodes);
                int adjacencyCount = math.clamp(AdjacencyEntryCount, 0, MaxAdjacencyEntries);
                int faultFlags = Counters[CounterFaultFlags];
                Counters[CounterBreachSignalCount] = 0;

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    uint flags = node.Flags;
                    flags &= ~((uint)LogisticsStateFlags.Powered | (uint)LogisticsStateFlags.Unpowered);
                    if (!HasFlag(flags, LogisticsStateFlags.Destroyed))
                        flags |= (uint)LogisticsStateFlags.Unpowered;

                    node.Flags = flags;
                    node.CurrentLoad = Sanitize01(node.CurrentLoad);
                    StateFlags[i] = flags;
                    PressureFront[i] = IsSource(flags) ? 1f : node.CurrentLoad;
                    PressureBack[i] = 0f;
                    ComponentIds[i] = -1;
                    Visited[i] = 0;
                }

                int componentCount = IdentifyComponents(nodeCount, adjacencyCount, ref faultFlags);

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    int componentId = ComponentIds[i];
                    if ((uint)componentId >= (uint)componentCount || Visited[componentId] == 0)
                    {
                        node.CurrentLoad = 0f;
                        PressureFront[i] = 0f;
                        node.Flags = (node.Flags | (uint)LogisticsStateFlags.Unpowered) & ~(uint)LogisticsStateFlags.Powered;
                        StateFlags[i] = node.Flags;
                    }
                }

                Counters[CounterComponentCount] = componentCount;
                Counters[CounterFaultFlags] = faultFlags;
            }

            private int IdentifyComponents(int nodeCount, int adjacencyCount, ref int faultFlags)
            {
                int componentCount = 0;
                for (int startNode = 0; startNode < nodeCount; startNode++)
                {
                    if (ComponentIds[startNode] >= 0)
                        continue;

                    ref LogisticsNodeDTO start = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + startNode);
                    if (IsClosed(start.Flags))
                    {
                        ComponentIds[startNode] = -1;
                        continue;
                    }

                    int queueHead = 0;
                    int queueTail = 0;
                    int reachableCount = 0;
                    Counters[BfsQueueBaseIndex + queueTail] = startNode;
                    queueTail++;
                    ComponentIds[startNode] = componentCount;
                    bool hasSource = false;
                    int guard = 0;

                    while (queueHead < queueTail)
                    {
                        int current = Counters[BfsQueueBaseIndex + queueHead];
                        queueHead++;
                        guard++;
                        if (guard > nodeCount * 8 + 8)
                        {
                            faultFlags |= LogisticsGraphFaultFlags.InfiniteLoopGuard;
                            break;
                        }

                        if ((uint)current >= (uint)nodeCount)
                            continue;

                        ref LogisticsNodeDTO currentNode = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + current);
                        if (IsClosed(currentNode.Flags))
                            continue;

                        if (reachableCount < MaxNodes)
                        {
                            Counters[ReachableBaseIndex + reachableCount] = current;
                            reachableCount++;
                        }
                        else
                        {
                            faultFlags |= LogisticsGraphFaultFlags.CapacityExceeded;
                            break;
                        }

                        hasSource |= IsSource(currentNode.Flags);
                        int edgeStart = math.clamp(Counters[EdgeOffsetsBaseIndex + current], 0, adjacencyCount);
                        int edgeEnd = math.clamp(Counters[EdgeOffsetsBaseIndex + current + 1], edgeStart, adjacencyCount);
                        for (int edgeCursor = edgeStart; edgeCursor < edgeEnd; edgeCursor++)
                        {
                            int neighbor = Counters[EdgeDestinationsBaseIndex + edgeCursor];
                            if ((uint)neighbor >= (uint)nodeCount || ComponentIds[neighbor] >= 0)
                                continue;

                            ref LogisticsNodeDTO neighborNode = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + neighbor);
                            if (IsClosed(neighborNode.Flags))
                                continue;

                            ComponentIds[neighbor] = componentCount;
                            if (queueTail < MaxNodes)
                            {
                                Counters[BfsQueueBaseIndex + queueTail] = neighbor;
                                queueTail++;
                            }
                            else
                            {
                                faultFlags |= LogisticsGraphFaultFlags.CapacityExceeded;
                                break;
                            }
                        }
                    }

                    Visited[componentCount] = hasSource ? (byte)1 : (byte)0;
                    if (!hasSource)
                    {
                        for (int i = 0; i < reachableCount; i++)
                        {
                            int nodeIndex = Counters[ReachableBaseIndex + i];
                            ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + nodeIndex);
                            node.CurrentLoad = 0f;
                            node.Flags = (node.Flags | (uint)LogisticsStateFlags.Unpowered) & ~(uint)LogisticsStateFlags.Powered;
                            StateFlags[nodeIndex] = node.Flags;
                            PressureFront[nodeIndex] = 0f;
                        }
                    }

                    componentCount++;
                    if (componentCount >= nodeCount)
                        break;
                }

                return componentCount;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasFlag(uint flags, ulong mask)
            {
                return (flags & (uint)mask) != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsSource(uint flags)
            {
                bool generator = HasFlag(flags, LogisticsStateFlags.PowerGenerator);
                bool docked = HasFlag(flags, LogisticsStateFlags.DockingPort) && HasFlag(flags, LogisticsStateFlags.SubmarineAttached);
                return generator || docked;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsClosed(uint flags)
            {
                return HasFlag(flags, LogisticsStateFlags.Destroyed) || HasFlag(flags, LogisticsStateFlags.DoorLocked);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Sanitize01(float value)
            {
                return math.saturate(math.isfinite(value) ? value : 0f);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private unsafe struct LogisticsFlowSolverJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public LogisticsNodeDTO* NodesPtr;
            public int NodeCount;
            public float GlobalQualityWeight;
            public int EdgeOffsetsBaseIndex;
            public int EdgeDestinationsBaseIndex;
            public int AdjacencyEntryCount;
            [ReadOnly] [NoAlias] public NativeArray<int> ComponentIds;
            [ReadOnly] [NoAlias] public NativeArray<float> ReadPressure;
            [NoAlias] public NativeArray<float> WritePressure;
            [ReadOnly] [NoAlias] public NativeArray<float> CsrEdgeCapacities;
            [ReadOnly] [NoAlias] public NativeArray<byte> Visited;
            [ReadOnly] [NoAlias] public NativeArray<int> Counters;
            [ReadOnly] [NoAlias] public NativeArray<LogisticsTuningDTO> Tuning;

            public void Execute(int index)
            {
                int nodeCount = math.clamp(NodeCount, 0, MaxNodes);
                if ((uint)index >= (uint)nodeCount)
                    return;

                int adjacencyCount = math.clamp(AdjacencyEntryCount, 0, math.min(MaxAdjacencyEntries, CsrEdgeCapacities.Length));
                ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + index);
                uint flags = node.Flags & ~(uint)LogisticsStateFlags.Divergent;
                int componentId = ComponentIds[index];
                if (IsClosed(flags) || (uint)componentId >= (uint)nodeCount || Visited[componentId] == 0)
                {
                    WritePressure[index] = 0f;
                    return;
                }

                LogisticsTuningDTO tuning = Tuning[0];
                float qualityWeight = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 0f);
                float qualityCurve = qualityWeight * qualityWeight * (3f - (2f * qualityWeight));
                float smoothing = math.clamp(FiniteOr(tuning.JacobiSmoothingFactor, DefaultJacobiSmoothing) * math.lerp(0.72f, 1f, qualityCurve), 0.05f, 1f);
                float omega = PowerSolverConvergenceMath.ResolveSolverOmega(GlobalQualityWeight);
                float targetTolerance = PowerSolverConvergenceMath.ResolveSolverTargetTolerance(0.001f, GlobalQualityWeight);
                bool source = IsSource(flags);
                bool pressureFault = !math.isfinite(ReadPressure[index]);
                float previousPressure = Sanitize01(ReadPressure[index]);
                float weightedPotential = 0f;
                float conductanceSum = 0f;

                int start = math.clamp(Counters[EdgeOffsetsBaseIndex + index], 0, adjacencyCount);
                int end = math.clamp(Counters[EdgeOffsetsBaseIndex + index + 1], start, adjacencyCount);
                for (int edgeCursor = start; edgeCursor < end; edgeCursor++)
                {
                    int neighbor = Counters[EdgeDestinationsBaseIndex + edgeCursor];
                    if ((uint)neighbor >= (uint)nodeCount || ComponentIds[neighbor] != componentId)
                        continue;

                    float conductanceRaw = CsrEdgeCapacities[edgeCursor];
                    float neighborPressureRaw = ReadPressure[neighbor];
                    pressureFault |= !math.isfinite(conductanceRaw) || !math.isfinite(neighborPressureRaw);
                    float conductance = SanitizeNonNegative(conductanceRaw);
                    weightedPotential += conductance * Sanitize01(neighborPressureRaw);
                    conductanceSum += conductance;
                }

                float reactorOutput = math.max(1f, FiniteOr(tuning.ReactorOutputWatts, 1000f));
                float generatorRate = source ? 1f : 0f;
                float demandRate = source ? 0f : math.saturate(FiniteOr(node.Capacity, 0f) / reactorOutput);
                float relaxed = (weightedPotential + generatorRate - demandRate) * math.rcp(math.max(conductanceSum + 1f, 1f));

                float jacobiPressure = math.saturate(relaxed);
                float pressure = previousPressure + (jacobiPressure - previousPressure) * math.clamp(smoothing * omega, 0.05f, 1.85f);
                pressureFault |= !math.isfinite(pressure);
                if (pressureFault)
                {
                    node.Flags = flags | (uint)LogisticsStateFlags.Divergent;
                    WritePressure[index] = previousPressure;
                    return;
                }

                float resolvedPressure = math.saturate(pressure);
                float residual = math.abs(resolvedPressure - previousPressure);
                WritePressure[index] = residual <= targetTolerance ? previousPressure : resolvedPressure;
                node.Flags = flags;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasFlag(uint flags, ulong mask)
            {
                return (flags & (uint)mask) != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsSource(uint flags)
            {
                bool generator = HasFlag(flags, LogisticsStateFlags.PowerGenerator);
                bool docked = HasFlag(flags, LogisticsStateFlags.DockingPort) && HasFlag(flags, LogisticsStateFlags.SubmarineAttached);
                return generator || docked;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsClosed(uint flags)
            {
                return HasFlag(flags, LogisticsStateFlags.Destroyed) || HasFlag(flags, LogisticsStateFlags.DoorLocked);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Sanitize01(float value)
            {
                return math.saturate(math.isfinite(value) ? value : 0f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeNonNegative(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct LogisticsPressureCopyJob : IJobParallelFor
        {
            public int NodeCount;
            [ReadOnly] [NoAlias] public NativeArray<float> SourcePressure;
            [NoAlias] public NativeArray<float> DestinationPressure;

            public void Execute(int index)
            {
                int nodeCount = math.clamp(NodeCount, 0, MaxNodes);
                if ((uint)index >= (uint)nodeCount)
                    return;

                DestinationPressure[index] = SourcePressure[index];
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private unsafe struct LogisticsFlowFinalizeJob : IJob
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] public LogisticsNodeDTO* NodesPtr;
            public int NodeCount;
            public int EdgeCount;
            public int FrameIndex;
            public int RunOxygen;
            public int JacobiIterations;
            public float OxygenDeltaSeconds;
            public float DockingPowerWatts;
            [NoAlias] public NativeArray<ulong> StateFlags;
            [NoAlias] public NativeArray<LogisticsEdgeDTO> Edges;
            public int EdgeOffsetsBaseIndex;
            public int EdgeDestinationsBaseIndex;
            public int AdjacencyEntryCount;
            [ReadOnly] [NoAlias] public NativeArray<int> ComponentIds;
            [NoAlias] public NativeArray<float> PressureFront;
            [NoAlias] public NativeArray<float> EdgeRemainderMilli;
            [NoAlias] public NativeArray<float> CsrEdgeCapacities;
            [NoAlias] public NativeArray<float> CsrEdgeFlow01;
            [ReadOnly] [NoAlias] public NativeArray<byte> Visited;
            [NoAlias] public NativeArray<float> OxygenFront;
            [NoAlias] public NativeArray<float> OxygenBack;
            [ReadOnly] [NoAlias] public NativeArray<float> InternalPressureKpa;
            [ReadOnly] [NoAlias] public NativeArray<float> ExternalPressureKpa;
            [ReadOnly] [NoAlias] public NativeArray<float> YieldThresholdKpa;
            [ReadOnly] [NoAlias] public NativeArray<float> Reinforcement;
            [NoAlias] public NativeArray<int> Counters;
            public int BreachNodeBaseIndex;
            [ReadOnly] [NoAlias] public NativeArray<LogisticsTuningDTO> Tuning;
            [NoAlias] public NativeArray<LogisticsGraphTelemetryEntry> BlackBox;

            public void Execute()
            {
                int nodeCount = math.clamp(NodeCount, 0, MaxNodes);
                int edgeCount = math.clamp(EdgeCount, 0, math.min(MaxDirectedEdges, Edges.Length));
                int adjacencyCount = math.clamp(AdjacencyEntryCount, 0, math.min(MaxAdjacencyEntries, CsrEdgeCapacities.Length));
                int componentCount = math.clamp(Counters[CounterComponentCount], 0, nodeCount);
                int iterations = math.clamp(JacobiIterations, 1, 8);
                LogisticsTuningDTO tuning = Tuning[0];

                float totalGenerated = 0f;
                float totalConsumed = 0f;
                int faultFlags = Counters[CounterFaultFlags];
                int activeCount = 0;
                ulong stateHash = 1469598103934665603UL;

                for (int i = 0; i < nodeCount; i++)
                {
                    float pressure = PressureFront[i];
                    if (!math.isfinite(pressure))
                    {
                        pressure = 0f;
                        faultFlags |= LogisticsGraphFaultFlags.OxygenNan;
                    }

                    PressureFront[i] = math.saturate(pressure);
                }

                QuantizeLoadsAndFlows(nodeCount, edgeCount, adjacencyCount, componentCount, tuning, ref totalGenerated, ref totalConsumed, ref activeCount, ref faultFlags, ref stateHash);

                if (RunOxygen != 0)
                    SolveOxygenAndPressure(nodeCount, edgeCount, tuning, ref faultFlags);

                float totalOxygen01 = 0f;
                int breachedCount = 0;
                int unpoweredCount = 0;
                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    if (!HasFlag(node.Flags, LogisticsStateFlags.Destroyed) &&
                        HasFlag(node.Flags, LogisticsStateFlags.Unpowered))
                    {
                        unpoweredCount++;
                    }

                    if (HasFlag(node.Flags, LogisticsStateFlags.Breached))
                        breachedCount++;
                    if (HasFlag(node.Flags, LogisticsStateFlags.Divergent))
                        faultFlags |= LogisticsGraphFaultFlags.SolverDivergent;

                    StateFlags[i] = node.Flags;
                    stateHash = (stateHash ^ node.Flags) * 1099511628211UL;
                    stateHash = (stateHash ^ (uint)math.round(node.CurrentLoad * 1000f)) * 1099511628211UL;
                    totalOxygen01 += math.saturate(FiniteOr(OxygenFront[i], 0f) * 0.01f);
                }

                Counters[CounterNodeCount] = nodeCount;
                Counters[CounterEdgeCount] = edgeCount;
                Counters[CounterFaultFlags] = faultFlags;
                Counters[CounterActiveNodeCount] = activeCount;
                Counters[CounterBreachedCount] = breachedCount;
                Counters[CounterUnpoweredCount] = unpoweredCount;
                Counters[CounterComponentCount] = componentCount;
                Counters[CounterJacobiIterations] = iterations;

                int telemetryIndex = FrameIndex % TelemetryFrames;
                BlackBox[telemetryIndex] = new LogisticsGraphTelemetryEntry
                {
                    FrameIndex = FrameIndex,
                    NodeCount = nodeCount,
                    ActiveNodeCount = activeCount,
                    FaultFlags = faultFlags,
                    TotalPowerGenerated = totalGenerated,
                    TotalPowerConsumed = totalConsumed,
                    TotalOxygen01 = nodeCount > 0 ? totalOxygen01 / nodeCount : 0f,
                    SupplyRatio = totalConsumed > 0.0001f ? math.saturate(totalGenerated / totalConsumed) : 1f,
                    StateHash = stateHash,
                    BreachedCount = breachedCount,
                    UnpoweredCount = unpoweredCount,
                    OxygenCadence = RunOxygen,
                    ComponentCount = componentCount,
                    JacobiIterations = iterations,
                    SolverMicros = 0
                };
            }

            private void QuantizeLoadsAndFlows(
                int nodeCount,
                int edgeCount,
                int adjacencyCount,
                int componentCount,
                in LogisticsTuningDTO tuning,
                ref float totalGenerated,
                ref float totalConsumed,
                ref int activeCount,
                ref int faultFlags,
                ref ulong stateHash)
            {
                totalGenerated = 0f;
                totalConsumed = 0f;
                activeCount = 0;
                stateHash = 1469598103934665603UL;

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    uint flags = node.Flags;
                    bool source = IsSource(flags);
                    int componentId = ComponentIds[i];
                    float load = ((uint)componentId < (uint)componentCount && Visited[componentId] != 0) ? Sanitize01(PressureFront[i]) : 0f;
                    if (source)
                    {
                        load = 1f;
                        totalGenerated += HasFlag(flags, LogisticsStateFlags.PowerGenerator)
                            ? math.max(0f, FiniteOr(tuning.ReactorOutputWatts, 1000f))
                            : math.max(0f, DockingPowerWatts);
                    }

                    int loadMilli = (int)math.round(math.saturate(load) * 1000f);
                    load = loadMilli * 0.001f;
                    node.CurrentLoad = load;

                    if (load > 0.20f || source)
                    {
                        flags |= (uint)LogisticsStateFlags.Powered;
                        flags &= ~(uint)LogisticsStateFlags.Unpowered;
                        activeCount++;
                        if (!source)
                            totalConsumed += math.max(0f, FiniteOr(node.Capacity, 0f)) * load;
                    }
                    else
                    {
                        flags &= ~(uint)LogisticsStateFlags.Powered;
                        flags |= (uint)LogisticsStateFlags.Unpowered;
                    }

                    node.Flags = flags;
                    StateFlags[i] = flags;
                }

                for (int source = 0; source < nodeCount; source++)
                {
                    int start = math.clamp(Counters[EdgeOffsetsBaseIndex + source], 0, adjacencyCount);
                    int end = math.clamp(Counters[EdgeOffsetsBaseIndex + source + 1], start, adjacencyCount);
                    for (int edgeCursor = start; edgeCursor < end; edgeCursor++)
                    {
                        int destination = Counters[EdgeDestinationsBaseIndex + edgeCursor];
                        if ((uint)destination >= (uint)nodeCount || ComponentIds[source] != ComponentIds[destination])
                        {
                            CsrEdgeFlow01[edgeCursor] = 0f;
                            EdgeRemainderMilli[edgeCursor] = 0f;
                            continue;
                        }

                        float conductance = SanitizeNonNegative(CsrEdgeCapacities[edgeCursor]);
                        float delta = (Sanitize01(PressureFront[source]) - Sanitize01(PressureFront[destination])) * conductance;
                        float rawMilli = delta * 1000f + FiniteOr(EdgeRemainderMilli[edgeCursor], 0f);
                        int milli = (int)math.trunc(rawMilli);
                        EdgeRemainderMilli[edgeCursor] = rawMilli - milli;
                        CsrEdgeFlow01[edgeCursor] = math.saturate(math.abs(milli) * 0.001f / math.max(1f, conductance));
                    }
                }

                for (int i = 0; i < edgeCount; i++)
                {
                    LogisticsEdgeDTO edge = Edges[i];
                    int2 nodes = edge.Nodes;
                    if ((uint)nodes.x >= (uint)nodeCount || (uint)nodes.y >= (uint)nodeCount || ComponentIds[nodes.x] != ComponentIds[nodes.y])
                    {
                        edge.Flow01 = 0f;
                        edge.LastMilliTransfer = 0;
                        Edges[i] = edge;
                        continue;
                    }

                    float capacity = math.max(1f, FiniteOr(edge.Capacity, 1f));
                    float resistance = math.max(0.001f, FiniteOr(edge.Resistance, 0.001f));
                    float flow = (Sanitize01(PressureFront[nodes.x]) - Sanitize01(PressureFront[nodes.y])) * capacity / resistance;
                    int milli = (int)math.trunc(flow * 1000f);
                    edge.LastMilliTransfer = milli;
                    edge.Flow01 = math.saturate(math.abs(milli) * 0.001f / capacity);
                    Edges[i] = edge;
                }
            }

            private void SolveOxygenAndPressure(int nodeCount, int edgeCount, in LogisticsTuningDTO tuning, ref int faultFlags)
            {
                float diffusionRate = math.clamp(FiniteOr(tuning.OxygenDiffusionRate, 0.18f), 0.01f, 2f);
                float crushMultiplier = math.clamp(FiniteOr(tuning.CrushDepthMultiplier, 1f), 0.1f, 10f);

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    float oxygen = math.clamp(FiniteOr(OxygenFront[i], 0f), 0f, 100f);
                    float demand = math.max(0.08f, math.max(0f, FiniteOr(node.Capacity, 0f)) * 0.01f);
                    if (HasFlag(node.Flags, LogisticsStateFlags.Powered))
                        oxygen -= demand * OxygenDeltaSeconds;
                    else
                        oxygen -= demand * OxygenDeltaSeconds * 2f;

                    if (HasFlag(node.Flags, LogisticsStateFlags.Breached))
                        oxygen -= 18f * OxygenDeltaSeconds;

                    OxygenBack[i] = math.clamp(oxygen, 0f, 100f);
                }

                for (int i = 0; i < edgeCount; i++)
                {
                    int2 edge = Edges[i].Nodes;
                    if ((uint)edge.x >= (uint)nodeCount || (uint)edge.y >= (uint)nodeCount)
                        continue;

                    ref LogisticsNodeDTO a = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + edge.x);
                    ref LogisticsNodeDTO b = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + edge.y);
                    if (IsClosed(a.Flags) || IsClosed(b.Flags))
                        continue;

                    float delta = (FiniteOr(OxygenBack[edge.x], 0f) - FiniteOr(OxygenBack[edge.y], 0f)) * diffusionRate * OxygenDeltaSeconds;
                    if (!math.isfinite(delta))
                    {
                        faultFlags |= LogisticsGraphFaultFlags.OxygenNan;
                        continue;
                    }

                    OxygenBack[edge.x] -= delta;
                    OxygenBack[edge.y] += delta;
                }

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    float oxygen = math.clamp(FiniteOr(OxygenBack[i], 0f), 0f, 100f);
                    OxygenFront[i] = oxygen;
                    if (oxygen < 21f)
                        node.Flags |= (uint)LogisticsStateFlags.LowOxygen;
                    else
                        node.Flags &= ~(uint)LogisticsStateFlags.LowOxygen;

                    float pressureDelta = (FiniteOr(ExternalPressureKpa[i], 101.3f) * crushMultiplier) - FiniteOr(InternalPressureKpa[i], 101.3f);
                    float yield = math.max(0.001f, FiniteOr(YieldThresholdKpa[i], 650f) * math.max(0.25f, FiniteOr(Reinforcement[i], 1f)));
                    bool breachedBefore = HasFlag(node.Flags, LogisticsStateFlags.Breached);
                    if (pressureDelta > yield)
                    {
                        node.Flags |= (uint)(LogisticsStateFlags.Breached | LogisticsStateFlags.Flooded);
                        if (!breachedBefore)
                        {
                            int breachSlot = Counters[CounterBreachSignalCount];
                            if (breachSlot < MaxNodes)
                            {
                                Counters[BreachNodeBaseIndex + breachSlot] = i;
                                Counters[CounterBreachSignalCount] = breachSlot + 1;
                            }
                            else
                            {
                                faultFlags |= LogisticsGraphFaultFlags.SignalOverflow;
                            }
                        }
                    }

                    StateFlags[i] = node.Flags;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool HasFlag(uint flags, ulong mask)
            {
                return (flags & (uint)mask) != 0u;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsSource(uint flags)
            {
                bool generator = HasFlag(flags, LogisticsStateFlags.PowerGenerator);
                bool docked = HasFlag(flags, LogisticsStateFlags.DockingPort) && HasFlag(flags, LogisticsStateFlags.SubmarineAttached);
                return generator || docked;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsClosed(uint flags)
            {
                return HasFlag(flags, LogisticsStateFlags.Destroyed) || HasFlag(flags, LogisticsStateFlags.DoorLocked);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float Sanitize01(float value)
            {
                return math.saturate(math.isfinite(value) ? value : 0f);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static float SanitizeNonNegative(float value)
            {
                return math.isfinite(value) ? math.max(0f, value) : 0f;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        private struct LocalShiftResolverJob : IJobParallelFor
        {
            [ReadOnly] [NoAlias] public NativeArray<double3> NodeAup;
            [NoAlias] public NativeArray<float3> LocalPositions;
            public double3 CameraAup;

            public void Execute(int index)
            {
                double3 shifted = NodeAup[index] - CameraAup;
                if (!math.all(math.isfinite(shifted)))
                {
                    LocalPositions[index] = default;
                    return;
                }

                LocalPositions[index] = new float3((float)shifted.x, (float)shifted.y, (float)shifted.z);
            }
        }
    }

    public static class MockWFCGraphGenerator
    {
        public const int RoomCount = ShinobuLogisticsRouter.MaxNodes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ResolveKind(int index)
        {
            if (index == 0)
                return WfcOutpostGridConstants.Generator;
            if (index == RoomCount - 1)
                return WfcOutpostGridConstants.Hatch;
            if ((index % 3) == 0)
                return WfcOutpostGridConstants.Datapad;
            return (index & 1) == 0 ? WfcOutpostGridConstants.Room : WfcOutpostGridConstants.Corridor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ResolveExtraTarget(ref uint lcg, int nodeCount)
        {
            lcg = (lcg * 1664525u) + 1013904223u;
            return nodeCount > 0 ? (int)(lcg % (uint)nodeCount) : 0;
        }
    }
}
