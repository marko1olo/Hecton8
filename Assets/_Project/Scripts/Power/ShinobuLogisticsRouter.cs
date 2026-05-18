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
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct LogisticsNodeDTO
    {
        public int NodeIndex;
        public int ParentIndex;
        public ulong ConnectionMask;
        public float PowerDemand;
        public float OxygenDemand;
        public ulong StateFlags;
    }

    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public struct ConnectionEdgeDTO
    {
        public int2 Nodes;
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct LogisticsTuningDTO
    {
        public float ReactorOutputWatts;
        public float LifeSupportDrainWatts;
        public float OxygenDiffusionRate;
        public float CrushDepthMultiplier;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct LogisticsGraphTelemetryEntry
    {
        public ulong StateHash;
        public float TotalPowerGenerated;
        public float TotalPowerConsumed;
        public float TotalOxygen01;
        public float SupplyRatio;
        public int FrameIndex;
        public int NodeCount;
        public int ActiveNodeCount;
        public int FaultFlags;
        public int BreachedCount;
        public int UnpoweredCount;
        public int OxygenCadence;
        public int Reserved0;
        public int Reserved1;
        public int Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public partial struct MockModuleStateSignal : ISignal
    {
        public ulong SectorHash;
        public ulong Reserved1;
        public uint Frame;
        public uint SourceHash;
        public int NodeIndex;
        public ushort Reserved0;
        public byte Flags;
        public byte State;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    internal partial struct HullBreachSignal
    {
        public ulong SectorHash;
        public ulong Reserved0;
        public float3 Position;
        public float PressureDeltaKpa;
        public float Oxygen01;
        public uint Frame;
        public uint SourceHash;
        public uint Flags;
        public int NodeIndex;
        public int Reserved1;
        public int Reserved2;
        public int Reserved3;
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
    }

    public sealed unsafe class ShinobuLogisticsRouter : IDisposable
    {
        public const int MaxNodes = WfcOutpostGridConstants.MaxCellCount;
        public const int MaxDirectedEdges = WfcOutpostGridConstants.MaxDirectedEdges;
        public const int MaxAdjacencyEntries = MaxDirectedEdges * 2;
        public const int TelemetryFrames = WfcOutpostGridConstants.TelemetryFrames;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_LOGISTICS_GRAPH.bin";
        public const string DumpH8RelativePath = "Docs/AgentLogs/Dump_SHINOBU_13.h8dump";

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
        private const int CounterCount = 8;
        private const int EdgeOffsetsBase = CounterCount;
        private const int EdgeWriteCursorBase = EdgeOffsetsBase + MaxNodes + 1;
        private const int EdgeDestinationsBase = EdgeWriteCursorBase + MaxNodes;
        private const int IntLaneCount = EdgeDestinationsBase + MaxAdjacencyEntries;
        private const int FluidIncursionSignalCapacity = MaxNodes;
        private const int CsvBufferBytes = 16 * 1024;
        private const int LowTierOxygenCadence = 5;
        private const int NormalOxygenCadence = 1;
        private const float OxygenTickSeconds = 0.1f;
        private const float MockDockingWatts = 400f;
        private const uint SourceHash = 0x5348494Eu; // SHIN
        private const string SentinelOwner = "SHINOBU_13";
        private const string NeighborLabel = "ShinobuNeighbors";
        private const string BfsQueueLabel = "ShinobuBfsQueue";
        private const string MockSignalQueueLabel = "ShinobuMockModuleSignals";
        private const string BreachSignalQueueLabel = "ShinobuHullBreachSignals";
        private const string ReachableListLabel = "ShinobuReachableOrder";

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
        private VaultBufferHandle<LogisticsNodeDTO> _nodesHandle;
        private VaultBufferHandle<ConnectionEdgeDTO> _edgesHandle;
        private VaultBufferHandle<ulong> _stateFlagsHandle;
        private VaultBufferHandle<float> _oxygenFrontHandle;
        private VaultBufferHandle<float> _oxygenBackHandle;
        private VaultBufferHandle<float> _internalPressureHandle;
        private VaultBufferHandle<float> _externalPressureHandle;
        private VaultBufferHandle<float> _yieldThresholdHandle;
        private VaultBufferHandle<float> _reinforcementHandle;
        private VaultBufferHandle<double3> _nodeAupHandle;
        private VaultBufferHandle<float3> _localPositionsHandle;
        private VaultBufferHandle<byte> _priorityTierHandle;
        private VaultBufferHandle<byte> _visitedHandle;
        private VaultBufferHandle<int> _cellToNodeHandle;
        private VaultBufferHandle<int> _countersHandle;
        private VaultBufferHandle<LogisticsTuningDTO> _tuningHandle;
        private VaultBufferHandle<LogisticsGraphTelemetryEntry> _blackBoxHandle;

        internal NativeArray<LogisticsNodeDTO> _nodes;
        internal NativeArray<ConnectionEdgeDTO> _edges;
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

        private NativeParallelMultiHashMap<int, int> _neighbors;
        private NativeQueue<int> _bfsQueue;
        private NativeQueue<MockModuleStateSignal> _mockStateSignals;
        private NativeQueue<HullBreachSignal> _breachSignals;
        private NativeList<int> _reachableOrder;

        private JobHandle _solveHandle;
        private JobHandle _localShiftHandle;
        private bool _solvePending;
        private bool _localShiftPending;
        private bool _initialized;
        private bool _hasGraph;
        private bool _hasFatalLayoutFault;
        private int _nodeCount;
        private int _edgeCount;
        private int _frameIndex;
        private int _oxygenCadenceCounter;
        private int _oxygenCadenceDivisor = NormalOxygenCadence;
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
        private readonly byte[] _csvBuffer = new byte[CsvBufferBytes]; // COLD ALLOC: byte[16KB] - CSV tuning scratch - owner: SHINOBU_13

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
                !_cellToNode.IsCreated || !_counters.IsCreated || !_tuning.IsCreated || !_blackBox.IsCreated)
            {
                _hasFatalLayoutFault = true;
                return;
            }

            _neighbors = new NativeParallelMultiHashMap<int, int>(MaxAdjacencyEntries, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[MaxDirectedEdges*2] - cold adjacency splice mirror - owner: SHINOBU_13
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(_neighbors, SentinelOwner, NeighborLabel, NativeAllocationLifetime.Session);
            _bfsQueue = new NativeQueue<int>(Allocator.Persistent); // COLD ALLOC: NativeQueue<int>[500] - BFS traversal queue - owner: SHINOBU_13
            NativeMemorySentinel.RegisterNativeQueue(_bfsQueue, MaxNodes, SentinelOwner, BfsQueueLabel, NativeAllocationLifetime.Session);
            _mockStateSignals = new NativeQueue<MockModuleStateSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<MockModuleStateSignal>[32] - local dependency mock lane - owner: SHINOBU_13
            NativeMemorySentinel.RegisterNativeQueue(_mockStateSignals, 32, SentinelOwner, MockSignalQueueLabel, NativeAllocationLifetime.Session);
            _breachSignals = new NativeQueue<HullBreachSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<HullBreachSignal>[MaxNodes] - local breach payload queue - owner: SHINOBU_13
            NativeMemorySentinel.RegisterNativeQueue(_breachSignals, MaxNodes, SentinelOwner, BreachSignalQueueLabel, NativeAllocationLifetime.Session);
            _reachableOrder = new NativeList<int>(MaxNodes, Allocator.Persistent); // COLD ALLOC: NativeList<int>[500] - BFS reachable order scratch - owner: SHINOBU_13
            NativeMemorySentinel.RegisterNativeList(_reachableOrder, SentinelOwner, ReachableListLabel, NativeAllocationLifetime.Session);

            ConfigurePublicSignalLanes();
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            PrewarmQueue(ref _bfsQueue, MaxNodes);
            PrewarmQueue(ref _mockStateSignals, 32);
            PrewarmQueue(ref _breachSignals, MaxNodes);

            _tuning[0] = _offlineTuning;
            new LogisticsGraphInitializeJob
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
                CellToNode = _cellToNode
            }.Schedule(MaxNodes, 64).Complete();

            for (int i = 0; i < _counters.Length; i++)
                _counters[i] = 0;
            for (int i = 0; i < _blackBox.Length; i++)
                _blackBox[i] = default;

            _initialized = true;
            _missingVaultWarned = false;
            _active = this;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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

            TryConsumeGeneratedSignals();
            TryConsumeStateSignals();
            TryConsumeDockingSignals();
            RefreshHardwareCadence();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            TryReloadCsvOverrides();
#endif

            if (!_hasGraph)
                BuildEmergencyMockGraph();

            if (_solvePending)
                return;

            ApplyQueuedTuning();
            ScheduleMockModuleSignal();

            _frameIndex++;
            bool runOxygen = ShouldRunOxygenSolver();
            _solveHandle = new LogisticsSolveJob
            {
                NodesPtr = (LogisticsNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount,
                FrameIndex = _frameIndex,
                RunOxygen = runOxygen ? 1 : 0,
                OxygenDeltaSeconds = OxygenTickSeconds * _oxygenCadenceDivisor,
                DockingPowerWatts = MockDockingWatts,
                SectorHash = _activeSectorHash,
                StateFlags = _stateFlags,
                Edges = _edges,
                EdgeOffsetsBaseIndex = EdgeOffsetsBase,
                EdgeDestinationsBaseIndex = EdgeDestinationsBase,
                AdjacencyEntryCount = _counters[CounterAdjacencyEntryCount],
                TraversalQueue = _bfsQueue,
                ReachableOrder = _reachableOrder,
                Visited = _visited,
                PriorityTier = _priorityTier,
                OxygenFront = _oxygenFront,
                OxygenBack = _oxygenBack,
                InternalPressureKpa = _internalPressureKpa,
                ExternalPressureKpa = _externalPressureKpa,
                YieldThresholdKpa = _yieldThresholdKpa,
                Reinforcement = _reinforcement,
                LocalPositions = _localPositions,
                Counters = _counters,
                Tuning = _tuning,
                BlackBox = _blackBox,
                BreachSignals = _breachSignals
            }.Schedule();
            _solvePending = true;
        }

        public void LateFrameTick(float now)
        {
            if (!_initialized)
                return;

            if (_solvePending && _solveHandle.IsCompleted)
            {
                _solveHandle.Complete();
                _solvePending = false;
                PublishSolveSideEffects();
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
                _localShiftHandle.Complete();
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
            edgeBytes = UnsafeUtility.SizeOf<ConnectionEdgeDTO>();
            tuningBytes = UnsafeUtility.SizeOf<LogisticsTuningDTO>();
            return nodeBytes == 32 &&
                   edgeBytes == 8 &&
                   tuningBytes == 16 &&
                   UnsafeUtility.SizeOf<LogisticsGraphTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<MockModuleStateSignal>() == 32 &&
                   UnsafeUtility.SizeOf<HullBreachSignal>() == 64;
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
                   ResolveVaultBuffer(vault, ref _blackBoxHandle, BufferID.ShinobuLogisticsBlackBox, TelemetryFrames, out _blackBox);
        }

        private static bool ResolveVaultBuffer<T>(
            IDataVault vault,
            ref VaultBufferHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (vault.IsAllocationLocked)
            {
                if (!handle.IsCreated && !vault.TryGetBufferHandle<T>(bufferId, out handle))
                    return false;

                buffer = handle.Resolve(vault);
                return buffer.IsCreated && buffer.Length >= requiredLength;
            }

            handle = vault.GetBufferHandle<T>(bufferId, requiredLength, SystemID.Power, NativeArrayOptions.UninitializedMemory);
            buffer = handle.Resolve(vault);
            return buffer.IsCreated && buffer.Length >= requiredLength;
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
                   RefreshVaultBuffer(vault, ref _blackBoxHandle, TelemetryFrames, out _blackBox);
        }

        private static bool RefreshVaultBuffer<T>(
            IDataVault vault,
            ref VaultBufferHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || !handle.IsCreated)
                return false;

            buffer = handle.Resolve(vault);
            return buffer.IsCreated && buffer.Length >= requiredLength;
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

            active._tuning[0] = sanitized;
        }

        public static bool TryGetDebugEdge(
            int index,
            out float3 nodeA,
            out float3 nodeB,
            out ulong flagsA,
            out ulong flagsB)
        {
            nodeA = default;
            nodeB = default;
            flagsA = 0UL;
            flagsB = 0UL;

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
            return true;
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
                _solveHandle.Complete();
                _solvePending = false;
            }

            if (_localShiftPending)
            {
                _localShiftHandle.Complete();
                _localShiftPending = false;
            }

            if (ReferenceEquals(_active, this))
                _active = null;

            if (_reachableOrder.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(SentinelOwner, ReachableListLabel);
                _reachableOrder.Dispose();
            }

            if (_breachSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(SentinelOwner, BreachSignalQueueLabel);
                _breachSignals.Dispose();
            }

            if (_mockStateSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(SentinelOwner, MockSignalQueueLabel);
                _mockStateSignals.Dispose();
            }

            if (_bfsQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(SentinelOwner, BfsQueueLabel);
                _bfsQueue.Dispose();
            }

            if (_neighbors.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(SentinelOwner, NeighborLabel);
                _neighbors.Dispose();
            }

            ClearVaultAliases();

            _initialized = false;
            _hasGraph = false;
            _nodeCount = 0;
            _edgeCount = 0;
        }

        private void ClearVaultAliases()
        {
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
                node.StateFlags = flags;
                _nodes[nodeIndex] = node;
                _neighbors.Remove(nodeIndex);
                rebuildAdjacency = true;
            }

            while (_mockStateSignals.TryDequeue(out MockModuleStateSignal signal))
            {
                if ((uint)signal.NodeIndex >= _nodeCount)
                    continue;

                ulong flags = _stateFlags[signal.NodeIndex];
                if ((signal.Flags & 0x01) != 0)
                    flags |= LogisticsStateFlags.Destroyed;
                if ((signal.Flags & 0x02) != 0)
                    flags ^= LogisticsStateFlags.DoorLocked;
                if ((signal.Flags & 0x04) != 0)
                    flags |= LogisticsStateFlags.Flooded;
                _stateFlags[signal.NodeIndex] = flags;
                LogisticsNodeDTO node = _nodes[signal.NodeIndex];
                node.StateFlags = flags;
                _nodes[signal.NodeIndex] = node;
                _neighbors.Remove(signal.NodeIndex);
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
            node.StateFlags = flags;
            _nodes[dockingNode] = node;
        }

        private void RefreshHardwareCadence()
        {
            bool lowTier = SignalBusRegistry.LowTierMode || SignalBusRegistry.SystemStress01 > 0.72f;
            ReadOnlySpan<SystemHealthSignal> healthSignals = SignalBus<SystemHealthSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
            {
                SystemHealthSignal signal = healthSignals[i];
                if (signal.SystemHealthIndex01 < 0.42f || signal.PressureLevel >= 2)
                {
                    lowTier = true;
                    break;
                }
            }

            _oxygenCadenceDivisor = lowTier ? LowTierOxygenCadence : NormalOxygenCadence;
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

        private void ScheduleMockModuleSignal()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_nodeCount <= 2 || (_frameIndex % 240) != 0)
                return;

            new MockModuleStateSignalJob
            {
                Frame = (uint)_frameIndex,
                NodeCount = _nodeCount,
                SectorHash = _activeSectorHash,
                Output = _mockStateSignals.AsParallelWriter()
            }.Schedule().Complete();
#endif
        }

        private void BuildFromWfcGrid(in WfcOutpostGridLease lease, in WfcOutpostGeneratedSignal signal)
        {
            ResetGraphBuffers();

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
                    NodeIndex = nodeIndex,
                    ParentIndex = -1,
                    ConnectionMask = 0UL,
                    PowerDemand = ResolvePowerDemand(kind),
                    OxygenDemand = ResolveOxygenDemand(kind),
                    StateFlags = flags
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
            ResetGraphBuffers();
            GenerateEmergencyMockProfiles();

            const int mockCount = MockWFCGraphGenerator.RoomCount;
            _nodeCount = mockCount;
            _activeGridHandle = 0x53484D47u;
            _activeSectorHash = 0x5348494E4F425531UL;
            _activeGridHash = 0x4D4F434Bu;
            _activeGenerationSequence = 1u;

            for (int i = 0; i < mockCount; i++)
            {
                byte kind = MockWFCGraphGenerator.ResolveKind(i);
                ulong flags = ResolveInitialStateFlags(kind, new int3(i, 0, 0), new int3(mockCount, 1, 1));
                _nodes[i] = new LogisticsNodeDTO
                {
                    NodeIndex = i,
                    ParentIndex = -1,
                    ConnectionMask = 0UL,
                    PowerDemand = ResolvePowerDemand(kind),
                    OxygenDemand = ResolveOxygenDemand(kind),
                    StateFlags = flags
                };
                _stateFlags[i] = flags;
                _oxygenFront[i] = i == mockCount - 1 ? 55f : 100f;
                _oxygenBack[i] = _oxygenFront[i];
                _internalPressureKpa[i] = 101.3f;
                _externalPressureKpa[i] = 101.3f + (i * 17f);
                _yieldThresholdKpa[i] = 650f + ResolveReinforcement(kind) * 220f;
                _reinforcement[i] = ResolveReinforcement(kind);
                _nodeAup[i] = new double3(i * 6.0, 0.0, (i & 1) * 4.0);
                _localPositions[i] = new float3(i * 6f, 0f, (i & 1) * 4f);
                _priorityTier[i] = ResolvePriority(kind);
                _cellToNode[i] = i;
            }

            uint lcg = 0x13B5u;
            for (int i = 0; i < mockCount - 1; i++)
                AddUndirectedEdge(i, i + 1);
            for (int i = 0; i < mockCount; i++)
            {
                int target = MockWFCGraphGenerator.ResolveExtraTarget(ref lcg, mockCount);
                if (target != i && math.abs(target - i) > 1)
                    AddUndirectedEdge(i, target);
            }

            _hasGraph = true;
            RebuildCsrFromEdges();
            _counters[CounterNodeCount] = _nodeCount;
            _counters[CounterEdgeCount] = _edgeCount;
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
                CrushDepthMultiplier = 1f
            };
        }

        private void ResetGraphBuffers()
        {
            if (_solvePending)
            {
                _solveHandle.Complete();
                _solvePending = false;
            }

            _neighbors.Clear();
            _bfsQueue.Clear();
            _reachableOrder.Clear();
            _nodeCount = 0;
            _edgeCount = 0;
            _hasGraph = false;
            for (int i = 0; i < _cellToNode.Length; i++)
                _cellToNode[i] = -1;
            for (int i = 0; i < _counters.Length; i++)
                _counters[i] = 0;
        }

        private void RebuildAdjacencyFromEdges()
        {
            _neighbors.Clear();
            for (int i = 0; i < _edgeCount; i++)
            {
                int2 edge = _edges[i].Nodes;
                if ((uint)edge.x >= _nodeCount || (uint)edge.y >= _nodeCount)
                    continue;

                if ((_stateFlags[edge.x] & LogisticsStateFlags.Destroyed) != 0 ||
                    (_stateFlags[edge.y] & LogisticsStateFlags.Destroyed) != 0)
                {
                    continue;
                }

                _neighbors.Add(edge.x, edge.y);
                _neighbors.Add(edge.y, edge.x);
            }

            RebuildCsrFromEdges();
        }

        private void RebuildCsrFromEdges()
        {
            if (!_counters.IsCreated)
                return;

            int nodeCount = math.clamp(_nodeCount, 0, MaxNodes);
            int maxEntries = math.min(MaxAdjacencyEntries, _counters.Length - EdgeDestinationsBase);
            if (nodeCount <= 0 || maxEntries <= 0)
            {
                _counters[CounterAdjacencyEntryCount] = 0;
                return;
            }

            for (int i = 0; i <= nodeCount; i++)
                _counters[EdgeOffsetsBase + i] = 0;
            for (int i = 0; i < nodeCount; i++)
                _counters[EdgeWriteCursorBase + i] = 0;

            int adjacencyCount = 0;
            for (int i = 0; i < _edgeCount; i++)
            {
                int2 edge = _edges[i].Nodes;
                if (!IsTraversableEdge(edge, nodeCount))
                    continue;

                if (adjacencyCount + 2 > maxEntries)
                {
                    _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CapacityExceeded;
                    break;
                }

                _counters[EdgeOffsetsBase + edge.x + 1]++;
                _counters[EdgeOffsetsBase + edge.y + 1]++;
                adjacencyCount += 2;
            }

            int prefix = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                int count = _counters[EdgeOffsetsBase + i + 1];
                _counters[EdgeOffsetsBase + i] = prefix;
                _counters[EdgeWriteCursorBase + i] = prefix;
                prefix += count;
            }

            _counters[EdgeOffsetsBase + nodeCount] = prefix;
            int writtenEntries = 0;
            for (int i = 0; i < _edgeCount; i++)
            {
                int2 edge = _edges[i].Nodes;
                if (!IsTraversableEdge(edge, nodeCount))
                    continue;
                if (writtenEntries + 2 > adjacencyCount)
                    break;

                int writeA = _counters[EdgeWriteCursorBase + edge.x]++;
                int writeB = _counters[EdgeWriteCursorBase + edge.y]++;
                if ((uint)writeA < (uint)maxEntries)
                    _counters[EdgeDestinationsBase + writeA] = edge.y;
                if ((uint)writeB < (uint)maxEntries)
                    _counters[EdgeDestinationsBase + writeB] = edge.x;
                writtenEntries += 2;
            }

            _counters[CounterAdjacencyEntryCount] = prefix;
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

            _edges[_edgeCount++] = new ConnectionEdgeDTO { Nodes = new int2(low, high) };
            _neighbors.Add(low, high);
            _neighbors.Add(high, low);
            SetConnectionMaskBit(low, high);
            SetConnectionMaskBit(high, low);
        }

        private void SetConnectionMaskBit(int source, int destination)
        {
            if ((uint)destination >= 64u)
                return;

            LogisticsNodeDTO node = _nodes[source];
            node.ConnectionMask |= 1UL << destination;
            _nodes[source] = node;
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
            while (_breachSignals.TryDequeue(out HullBreachSignal breach))
            {
                if (!PublishFluidIncursion(in breach))
                    _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.SignalOverflow;
            }

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

        private void DumpBlackBox(int faultFlags)
        {
            WriteBlackBoxDump(faultFlags, DumpRelativePath);
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
                    }
                }
            }
            catch (Exception exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(0x5348444Du, SourceHash, exception.HResult);
            }
        }

        private bool PublishFluidIncursion(in HullBreachSignal breach)
        {
            if ((uint)breach.NodeIndex >= (uint)_nodeCount)
                return false;

            AbsoluteUniversePosition leakAup = AbsoluteUniversePosition.FromAbsolutePosition(_nodeAup[breach.NodeIndex]);
            FluidIncursionSignal incursion = new FluidIncursionSignal
            {
                LeakAup = leakAup,
                CompartmentId = (uint)breach.NodeIndex,
                FloodLevel01 = 1f,
                FlowRate01 = math.saturate(breach.PressureDeltaKpa * 0.001f),
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

            _csvLastWriteUtc = writeUtc;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long streamLength = stream.Length;
                    int length = streamLength > _csvBuffer.Length ? _csvBuffer.Length : (int)streamLength;
                    int read = 0;
                    while (read < length)
                    {
                        int chunk = stream.Read(_csvBuffer, read, length - read);
                        if (chunk <= 0)
                            break;
                        read += chunk;
                    }

                    ParseCsv(read);
                }
            }
            catch (Exception exception)
            {
                _counters[CounterFaultFlags] |= LogisticsGraphFaultFlags.CsvParseFault;
                GlobalTelemetryBus.PublishPerformanceWarning(0x53484353u, SourceHash, exception.HResult);
            }
        }

        private string ResolveCsvPath()
        {
            string streamingPath = Path.Combine(Application.streamingAssetsPath, "base_module_stats.csv");
            if (File.Exists(streamingPath))
                return streamingPath;

            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string rootStreamingPath = Path.Combine(root, "StreamingAssets", "base_module_stats.csv");
            return File.Exists(rootStreamingPath) ? rootStreamingPath : streamingPath;
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
                while (index < length && _csvBuffer[index] != (byte)',' && _csvBuffer[index] != (byte)'=' && _csvBuffer[index] != (byte)'\n' && _csvBuffer[index] != (byte)'\r')
                    index++;
                int keyLength = index - keyStart;
                if (index >= length)
                    break;

                byte separator = _csvBuffer[index++];
                if (separator == (byte)'\n' || separator == (byte)'\r')
                    continue;

                int valueStart = index;
                while (index < length && _csvBuffer[index] != (byte)'\n' && _csvBuffer[index] != (byte)'\r' && _csvBuffer[index] != (byte)',')
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

                while (index < length && (_csvBuffer[index] == (byte)'\n' || _csvBuffer[index] == (byte)'\r' || _csvBuffer[index] == (byte)','))
                    index++;
            }

            SetTuning(tuning);
        }

        private bool KeyEquals(int start, int length, string literal, uint keyHash)
        {
            if (HashLiteral(literal) != keyHash)
                return false;

            int literalIndex = 0;
            for (int i = 0; i < length; i++)
            {
                byte b = _csvBuffer[start + i];
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
                byte b = _csvBuffer[start + i];
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
            if (index < end && _csvBuffer[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            float value = 0f;
            while (index < end)
            {
                byte b = _csvBuffer[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                value = (value * 10f) + (b - (byte)'0');
                index++;
            }

            if (index < end && _csvBuffer[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < end)
                {
                    byte b = _csvBuffer[index];
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
                CrushDepthMultiplier = math.clamp(FiniteOr(tuning.CrushDepthMultiplier, 1f), 0.1f, 10f)
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

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);
            while (queue.TryDequeue(out _))
            {
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = false)]
        private struct LogisticsGraphInitializeJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<LogisticsNodeDTO> Nodes;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<ulong> StateFlags;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> OxygenFront;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> OxygenBack;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> InternalPressureKpa;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> ExternalPressureKpa;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> YieldThresholdKpa;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float> Reinforcement;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<double3> NodeAup;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<float3> LocalPositions;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<byte> PriorityTier;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<byte> Visited;
            [NativeDisableContainerSafetyRestriction]
            public NativeArray<int> CellToNode;

            public void Execute(int index)
            {
                Nodes[index] = new LogisticsNodeDTO
                {
                    NodeIndex = index,
                    ParentIndex = -1,
                    ConnectionMask = 0UL,
                    PowerDemand = 0f,
                    OxygenDemand = 0f,
                    StateFlags = LogisticsStateFlags.Unpowered
                };
                StateFlags[index] = LogisticsStateFlags.Unpowered;
                OxygenFront[index] = 100f;
                OxygenBack[index] = 100f;
                InternalPressureKpa[index] = 101.3f;
                ExternalPressureKpa[index] = 101.3f;
                YieldThresholdKpa[index] = 650f;
                Reinforcement[index] = 1f;
                NodeAup[index] = default;
                LocalPositions[index] = default;
                PriorityTier[index] = PriorityOptional;
                Visited[index] = 0;
                CellToNode[index] = -1;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = false)]
        private struct MockModuleStateSignalJob : IJob
        {
            public uint Frame;
            public int NodeCount;
            public ulong SectorHash;
            public NativeQueue<MockModuleStateSignal>.ParallelWriter Output;

            public void Execute()
            {
                if (NodeCount <= 2)
                    return;

                int nodeIndex = 1 + (int)((Frame * 1103515245u + 12345u) % (uint)(NodeCount - 1));
                Output.Enqueue(new MockModuleStateSignal
                {
                    Frame = Frame,
                    NodeIndex = nodeIndex,
                    Flags = (Frame & 1u) == 0u ? (byte)0x02 : (byte)0x00,
                    State = 1,
                    SourceHash = SourceHash,
                    SectorHash = SectorHash
                });
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = false)]
        private unsafe struct LogisticsSolveJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public LogisticsNodeDTO* NodesPtr;
            public int NodeCount;
            public int EdgeCount;
            public int FrameIndex;
            public int RunOxygen;
            public float OxygenDeltaSeconds;
            public float DockingPowerWatts;
            public ulong SectorHash;
            public NativeArray<ulong> StateFlags;
            [ReadOnly] public NativeArray<ConnectionEdgeDTO> Edges;
            public int EdgeOffsetsBaseIndex;
            public int EdgeDestinationsBaseIndex;
            public int AdjacencyEntryCount;
            public NativeQueue<int> TraversalQueue;
            public NativeList<int> ReachableOrder;
            public NativeArray<byte> Visited;
            [ReadOnly] public NativeArray<byte> PriorityTier;
            public NativeArray<float> OxygenFront;
            public NativeArray<float> OxygenBack;
            [ReadOnly] public NativeArray<float> InternalPressureKpa;
            [ReadOnly] public NativeArray<float> ExternalPressureKpa;
            [ReadOnly] public NativeArray<float> YieldThresholdKpa;
            [ReadOnly] public NativeArray<float> Reinforcement;
            [ReadOnly] public NativeArray<float3> LocalPositions;
            public NativeArray<int> Counters;
            [ReadOnly] public NativeArray<LogisticsTuningDTO> Tuning;
            public NativeArray<LogisticsGraphTelemetryEntry> BlackBox;
            public NativeQueue<HullBreachSignal> BreachSignals;

            public void Execute()
            {
                int nodeCount = math.clamp(NodeCount, 0, MaxNodes);
                TraversalQueue.Clear();
                ReachableOrder.Clear();

                float totalGenerated = 0f;
                float totalConsumed = 0f;
                int faultFlags = Counters[CounterFaultFlags];
                int activeCount = 0;
                int unpoweredCount = 0;
                int breachedCount = 0;
                ulong stateHash = 1469598103934665603UL;

                for (int i = 0; i < nodeCount; i++)
                {
                    Visited[i] = 0;
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    node.ParentIndex = -1;
                    node.StateFlags &= ~(LogisticsStateFlags.Powered | LogisticsStateFlags.Unpowered);
                    if ((node.StateFlags & LogisticsStateFlags.Destroyed) == 0)
                        node.StateFlags |= LogisticsStateFlags.Unpowered;
                    StateFlags[i] = node.StateFlags;
                }

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    if ((node.StateFlags & LogisticsStateFlags.Destroyed) != 0)
                        continue;

                    bool generator = (node.StateFlags & LogisticsStateFlags.PowerGenerator) != 0;
                    bool dockedSub = (node.StateFlags & (LogisticsStateFlags.DockingPort | LogisticsStateFlags.SubmarineAttached)) ==
                                      (LogisticsStateFlags.DockingPort | LogisticsStateFlags.SubmarineAttached);
                    if (!generator && !dockedSub)
                        continue;

                    Visited[i] = 1;
                    TraversalQueue.Enqueue(i);
                    ReachableOrder.Add(i);
                    totalGenerated += generator ? Tuning[0].ReactorOutputWatts : DockingPowerWatts;
                }

                int adjacencyCount = math.clamp(AdjacencyEntryCount, 0, MaxAdjacencyEntries);
                int guard = 0;
                while (TraversalQueue.TryDequeue(out int current))
                {
                    guard++;
                    if (guard > nodeCount * 8 + 8)
                    {
                        faultFlags |= LogisticsGraphFaultFlags.InfiniteLoopGuard;
                        break;
                    }

                    if ((uint)current >= nodeCount)
                        continue;

                    ref LogisticsNodeDTO currentNode = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + current);
                    if ((currentNode.StateFlags & (LogisticsStateFlags.Destroyed | LogisticsStateFlags.DoorLocked)) != 0)
                        continue;

                    int start = math.clamp(Counters[EdgeOffsetsBaseIndex + current], 0, adjacencyCount);
                    int end = math.clamp(Counters[EdgeOffsetsBaseIndex + current + 1], start, adjacencyCount);
                    for (int edgeCursor = start; edgeCursor < end; edgeCursor++)
                    {
                        int neighbor = Counters[EdgeDestinationsBaseIndex + edgeCursor];
                        if ((uint)neighbor >= nodeCount || Visited[neighbor] != 0)
                            continue;

                        ref LogisticsNodeDTO neighborNode = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + neighbor);
                        if ((neighborNode.StateFlags & (LogisticsStateFlags.Destroyed | LogisticsStateFlags.DoorLocked)) != 0)
                            continue;

                        Visited[neighbor] = 1;
                        neighborNode.ParentIndex = current;
                        TraversalQueue.Enqueue(neighbor);
                        ReachableOrder.Add(neighbor);
                    }
                }

                float remainingWatts = totalGenerated;
                for (int priority = PriorityLifeSupport; priority <= PriorityOptional; priority++)
                {
                    int reachableCount = ReachableOrder.Length;
                    for (int i = 0; i < reachableCount; i++)
                    {
                        int nodeIndex = ReachableOrder[i];
                        if ((uint)nodeIndex >= nodeCount || PriorityTier[nodeIndex] != priority)
                            continue;

                        ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + nodeIndex);
                        if ((node.StateFlags & LogisticsStateFlags.Destroyed) != 0)
                            continue;

                        float demand = math.max(0f, node.PowerDemand);
                        bool isGenerator = (node.StateFlags & LogisticsStateFlags.PowerGenerator) != 0;
                        if (isGenerator || demand <= 0f || remainingWatts >= demand)
                        {
                            remainingWatts -= isGenerator ? 0f : demand;
                            totalConsumed += isGenerator ? 0f : demand;
                            activeCount++;
                            node.StateFlags |= LogisticsStateFlags.Powered;
                            node.StateFlags &= ~LogisticsStateFlags.Unpowered;
                        }
                        else
                        {
                            unpoweredCount++;
                            node.StateFlags &= ~LogisticsStateFlags.Powered;
                            node.StateFlags |= LogisticsStateFlags.Unpowered;
                        }

                        StateFlags[nodeIndex] = node.StateFlags;
                    }
                }

                if (RunOxygen != 0)
                    SolveOxygenAndPressure(nodeCount, ref faultFlags);

                float totalOxygen01 = 0f;
                breachedCount = 0;
                unpoweredCount = 0;
                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    if ((node.StateFlags & LogisticsStateFlags.Destroyed) == 0 &&
                        (node.StateFlags & LogisticsStateFlags.Unpowered) != 0)
                    {
                        unpoweredCount++;
                    }

                    if ((node.StateFlags & LogisticsStateFlags.Breached) != 0)
                        breachedCount++;
                    StateFlags[i] = node.StateFlags;
                    stateHash = (stateHash ^ node.StateFlags) * 1099511628211UL;
                    totalOxygen01 += OxygenFront[i] * 0.01f;
                }

                Counters[CounterNodeCount] = nodeCount;
                Counters[CounterEdgeCount] = EdgeCount;
                Counters[CounterFaultFlags] = faultFlags;
                Counters[CounterActiveNodeCount] = activeCount;
                Counters[CounterBreachedCount] = breachedCount;
                Counters[CounterUnpoweredCount] = unpoweredCount;

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
                    OxygenCadence = RunOxygen
                };
            }

            private void SolveOxygenAndPressure(int nodeCount, ref int faultFlags)
            {
                float diffusionRate = Tuning[0].OxygenDiffusionRate;
                float crushMultiplier = Tuning[0].CrushDepthMultiplier;

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    float oxygen = OxygenFront[i];
                    if ((node.StateFlags & LogisticsStateFlags.Powered) != 0)
                        oxygen -= node.OxygenDemand * OxygenDeltaSeconds;
                    else
                        oxygen -= node.OxygenDemand * OxygenDeltaSeconds * 2f;

                    if ((node.StateFlags & LogisticsStateFlags.Breached) != 0)
                        oxygen -= 18f * OxygenDeltaSeconds;

                    OxygenBack[i] = math.clamp(oxygen, 0f, 100f);
                }

                for (int i = 0; i < EdgeCount; i++)
                {
                    int2 edge = Edges[i].Nodes;
                    if ((uint)edge.x >= nodeCount || (uint)edge.y >= nodeCount)
                        continue;

                    ref LogisticsNodeDTO a = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + edge.x);
                    ref LogisticsNodeDTO b = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + edge.y);
                    ulong closedMask = LogisticsStateFlags.Destroyed | LogisticsStateFlags.DoorLocked;
                    if ((a.StateFlags & closedMask) != 0 || (b.StateFlags & closedMask) != 0)
                        continue;

                    float delta = (OxygenBack[edge.x] - OxygenBack[edge.y]) * diffusionRate * OxygenDeltaSeconds;
                    OxygenBack[edge.x] -= delta;
                    OxygenBack[edge.y] += delta;
                }

                for (int i = 0; i < nodeCount; i++)
                {
                    ref LogisticsNodeDTO node = ref UnsafeUtility.AsRef<LogisticsNodeDTO>(NodesPtr + i);
                    float oxygen = math.clamp(OxygenBack[i], 0f, 100f);
                    if (!math.isfinite(oxygen))
                    {
                        oxygen = 0f;
                        faultFlags |= LogisticsGraphFaultFlags.OxygenNan;
                    }

                    OxygenFront[i] = oxygen;
                    if (oxygen < 21f)
                        node.StateFlags |= LogisticsStateFlags.LowOxygen;
                    else
                        node.StateFlags &= ~LogisticsStateFlags.LowOxygen;

                    float pressureDelta = (ExternalPressureKpa[i] * crushMultiplier) - InternalPressureKpa[i];
                    float yield = YieldThresholdKpa[i] * math.max(0.25f, Reinforcement[i]);
                    bool breachedBefore = (node.StateFlags & LogisticsStateFlags.Breached) != 0;
                    if (pressureDelta > yield)
                    {
                        node.StateFlags |= LogisticsStateFlags.Breached | LogisticsStateFlags.Flooded;
                        if (!breachedBefore)
                        {
                            BreachSignals.Enqueue(new HullBreachSignal
                            {
                                Frame = (uint)FrameIndex,
                                NodeIndex = i,
                                Position = LocalPositions[i],
                                PressureDeltaKpa = pressureDelta,
                                Oxygen01 = oxygen * 0.01f,
                                SectorHash = SectorHash,
                                SourceHash = SourceHash,
                                Flags = 1u
                            });
                        }
                    }
                }
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low, CompileSynchronously = false)]
        private struct LocalShiftResolverJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<double3> NodeAup;
            public NativeArray<float3> LocalPositions;
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
        public const int RoomCount = 10;

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
