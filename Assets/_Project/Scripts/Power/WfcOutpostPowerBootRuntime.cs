using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Logistics.Grid;
using Hecton8.Logistics.Grid.Contracts;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// Signal-driven WFC outpost logistics boot. Owns native SOA state; no MonoBehaviour power nodes are created.
    /// </summary>
    internal sealed class WfcOutpostPowerBootRuntime : IDisposable
    {
        private static int s_x001WfcOutpostPowerBootRuntimeSignalPushDropCount;
        private const string OwnerName = "OUTPOST_LOGISTICS_INITIALIZER";
        private const int MaxCells = WfcOutpostGridConstants.MaxCellCount;
        private const int MaxDirectedEdges = WfcOutpostGridConstants.MaxDirectedEdges;
        private const int TelemetryCapacity = WfcOutpostGridConstants.TelemetryFrames;
        private const int TelemetryEntrySizeBytes = 64;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_1319_WfcOutpostPowerBoot.bin";
        private const uint DumpMagic = 0x4F504257u; // OPBW
        private const int DumpVersion = 1;
        private const int DumpHeaderBytes = 20;
        private const int DumpTelemetryRowBytes = 56;
        private const SystemID LogisticsGridSystemId = (SystemID)512;
        private const float InitialReactorOutput01 = 0.05f;
        private const float ReactorDecayPerSecond = 0.01f / 60f;
        private const float ReactorBrownoutThreshold01 = 0.02f;
        private const float DoorUnlockVoltage = 0.1f;

        // Lower rail of the sealed-door Schmitt latch. A door that is already open
        // stays open until potential falls to this level, so solver ripple around
        // DoorUnlockVoltage cannot strobe the door. AGENTS.md:239 hysteresis band.
        private const float DoorLockVoltage = 0.07f;

        // Continuous seconds the opposing side must hold before the latch flips.
        // AGENTS.md:239 minimum time band is 2-3 s.
        private const float DoorSwitchDwellSeconds = 2.5f;

        // Round-robin keepalive window, in node indices, re-published per pass so a
        // door controller that spawned after the last transition still converges.
        private const int DoorResyncStrideNodes = 32;

        // Hard per-pass push budget. SignalBus<WfcOutpostDoorPowerSignal> is a
        // 64-entry lane; anything above the budget is deferred to the next pass
        // instead of being silently dropped by the bus.
        private const int DoorSignalPushBudgetPerPass = 24;
        private const float GeneratorWatts = 5000f;
        private const float RoomDemandWatts = 12f;
        private const float CorridorDemandWatts = 4f;
        private const float DoorDemandWatts = 8f;
        private const float DatapadDemandWatts = 2f;
        private const float NodeCapacityWatts = 1000f;
        private const float EdgeResistance = 0.025f;
        private const float NodeResistance = 0.02f;
        private const float StandardOxygenKPa = HectonSurvivalContract.StandardOxygenKPa;
        private const float StandardCarbonDioxideKPa = HectonSurvivalContract.StandardCarbonDioxideKPa;
        private const float StandardNitrogenKPa = HectonSurvivalContract.StandardNitrogenKPa;
        private const float StandardAmbientKPa = HectonSurvivalContract.KPaPerAtmosphere;
        private const float ShiftEpsilonMeters = 0.0001f;
        private const float MaxAupShiftMeters = 10000f;
        private const double MacroAupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersDouble;
        private const uint OutpostNodeCountHash = 0x4F4E4354u; // ONCT
        private const uint WfcPowerBootContextHash = 0x57465042u; // WFPB
        private const uint FaultTelemetryHash = 0x57464654u; // WFFT
        private const int FatalGraphFaultMask = WfcOutpostGraphFaultFlags.CapacityExceeded |
                                                WfcOutpostGraphFaultFlags.InvalidDimensions |
                                                WfcOutpostGraphFaultFlags.InvalidBuffers |
                                                WfcOutpostGraphFaultFlags.NoPowerNodes;
        private const uint FaultFlag = 1u << 31;
        private const uint AupShiftFlag = 1u << 2;
        private const uint ReactorClockFaultFlag = 1u << 3;
        private static readonly ulong TranslationMutationGuardMask =
            MutationGuardBit(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731640) |
            MutationGuardBit(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731641) |
            MutationGuardBit(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731642) |
            MutationGuardBit(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731643) |
            MutationGuardBit(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731645);

        private readonly LogisticsNetworkGraph _graph; // COLD ALLOC: LogisticsNetworkGraph[1] - WFC outpost power evaluator - owner: WfcOutpostPowerBootRuntime
        // COLD ALLOC: byte[500] - per-node sealed-door power latch state - owner: WfcOutpostPowerBootRuntime
        private readonly byte[] _doorLatchState = new byte[MaxCells];
        // COLD ALLOC: float[500] - per-node dwell anchor seconds for the door power latch - owner: WfcOutpostPowerBootRuntime
        private readonly float[] _doorLatchCandidateSince = new float[MaxCells];
        // COLD ALLOC: byte[500] - last door state actually accepted by the signal lane - owner: WfcOutpostPowerBootRuntime
        private readonly byte[] _doorPublishedState = new byte[MaxCells];
        private VaultGenerationHandle<WfcOutpostPowerNode> _nodesHandle;
        private VaultGenerationHandle<int> _cellToNodeHandle;
        private VaultGenerationHandle<int> _countsHandle;
        private VaultGenerationHandle<int> _generatorNodeIndexHandle;
        private VaultGenerationHandle<WfcOutpostPowerBootTelemetryEntry> _blackBoxHandle;
        private VaultGenerationHandle<int2> _powerEdgesHandle;
        private IDataVault _dataVault;
        private JobHandle _translationHandle;
        private BufferID _translationGridLeaseBufferId;
        private SystemID _translationGridLeaseSystemId;
        private ulong _translationBufferLockMask;
        private IGasDynamicsSolver _gasDynamics;
        private WfcOutpostGridDescriptor _activeDescriptor;
        private WfcOutpostGridDescriptor _pendingDescriptor;
        private uint _activeGridHandle;
        private uint _pendingGridHandle;
        private int _activeNodeCount;
        private int _activeDirectedEdgeCount;
        private int _activeDoorCount;
        private int _activeRoomCount;
        private int _activeGeneratorNodeIndex;
        private int _blackBoxCursor;
        private int _doorResyncCursor;
        private int _lastGraphFaultFlags;
        private float _reactorOutput01;
        private float _lastReactorUpdateTime;
        private bool _initialized;
        private bool _translationPending;
        private bool _graphEvaluationPending;
        private bool _hasActiveGraph;
        private bool _gasSeedPending;
        private bool _faultDumped;

        private NativeArray<WfcOutpostPowerNode> _nodes => ResolveBuffer(in _nodesHandle);
        private NativeArray<int> _cellToNode => ResolveBuffer(in _cellToNodeHandle);
        private NativeArray<int> _counts => ResolveBuffer(in _countsHandle);
        private NativeArray<int> _generatorNodeIndex => ResolveBuffer(in _generatorNodeIndexHandle);
        private NativeArray<WfcOutpostPowerBootTelemetryEntry> _blackBox => ResolveBuffer(in _blackBoxHandle);
        private NativeArray<int2> _powerEdges => ResolveBuffer(in _powerEdgesHandle);

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        private struct WfcOutpostPowerBootTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public uint GridHandle;
            [FieldOffset(8)]
            public ulong SectorHash;
            [FieldOffset(16)]
            public int NodeCount;
            [FieldOffset(20)]
            public int DirectedEdgeCount;
            [FieldOffset(24)]
            public int DoorCount;
            [FieldOffset(28)]
            public int RoomCount;
            [FieldOffset(32)]
            public int GeneratorNodeIndex;
            [FieldOffset(36)]
            public float ReactorOutput01;
            [FieldOffset(40)]
            public float SupplyRatio;
            [FieldOffset(44)]
            public float BrownoutSeverity01;
            [FieldOffset(48)]
            public uint Flags;
            [FieldOffset(52)]
            public uint GraphHash;
            [FieldOffset(56)]
            public uint Reserved0;
            [FieldOffset(60)]
            public uint Reserved1;
        }

        public WfcOutpostPowerBootRuntime()
        {
            _graph = new LogisticsNetworkGraph(MaxCells, MaxDirectedEdges, MaxCells);
            _reactorOutput01 = InitialReactorOutput01;
            _activeGeneratorNodeIndex = -1;
        }

        public bool HasPendingWork => _translationPending || _graphEvaluationPending;

        public void Initialize()
        {
            if (_initialized)
                return;

            _dataVault = GlobalRegistry.DataVault;
            EnsureBuffers();
            SignalCorridorRuntime.EnsureInitialized();

            _initialized = _nodes.IsCreated &&
                           _cellToNode.IsCreated &&
                           _counts.IsCreated &&
                           _generatorNodeIndex.IsCreated &&
                           _blackBox.IsCreated &&
                           _powerEdges.IsCreated;
        }

        public void RebindDataVault(IDataVault currentVault)
        {
            if (ReferenceEquals(_dataVault, currentVault))
                return;

            JobHandle translationDependency = _translationPending ? _translationHandle : default;
            if (_translationPending)
            {
                _translationHandle = default;
                _translationPending = false;
            }

            _graphEvaluationPending = false;
            _graph.InjectDataVault(currentVault);
            ForceCompleteTranslationInPostSimulationWindow(ref translationDependency);
            ReleasePendingTranslationLocks();
            ReleaseBuffers();
            ResetRuntimeStateForDataVaultRebind();

            _dataVault = currentVault;
            if (currentVault != null)
            {
                EnsureBuffers();
                SignalCorridorRuntime.EnsureInitialized();
                _initialized = _nodes.IsCreated &&
                               _cellToNode.IsCreated &&
                               _counts.IsCreated &&
                               _generatorNodeIndex.IsCreated &&
                               _blackBox.IsCreated &&
                               _powerEdges.IsCreated;
                TryBindGraphBaseAwakeState();
            }

            JobHandle.ScheduleBatchedJobs();
        }

        private void ResetRuntimeStateForDataVaultRebind()
        {
            _activeDescriptor = default;
            _pendingDescriptor = default;
            _activeGridHandle = 0u;
            _pendingGridHandle = 0u;
            _activeNodeCount = 0;
            _activeDirectedEdgeCount = 0;
            _activeDoorCount = 0;
            _activeRoomCount = 0;
            _activeGeneratorNodeIndex = -1;
            _blackBoxCursor = 0;
            _lastGraphFaultFlags = 0;
            _reactorOutput01 = InitialReactorOutput01;
            _lastReactorUpdateTime = 0f;
            _initialized = false;
            _translationHandle = default;
            _translationGridLeaseBufferId = BufferID.Unknown;
            _translationGridLeaseSystemId = SystemID.Unknown;
            _translationBufferLockMask = 0UL;
            _translationPending = false;
            _graphEvaluationPending = false;
            _hasActiveGraph = false;
            _gasSeedPending = false;
            _faultDumped = false;
            ResetDoorPowerLatches();
        }

        /// <summary>
        /// Drops every sealed-door latch back to unknown. Node indices are only
        /// stable inside one translated graph, so a rebind or a new outpost must
        /// not inherit another graph's latch or dwell anchor.
        /// </summary>
        private void ResetDoorPowerLatches()
        {
            for (int i = 0; i < MaxCells; i++)
            {
                _doorLatchState[i] = PowerHysteresisLatch.StateUnknown;
                _doorLatchCandidateSince[i] = 0f;
                _doorPublishedState[i] = PowerHysteresisLatch.StateUnknown;
            }

            _doorResyncCursor = 0;
        }

        private void EnsureBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            _nodesHandle = vault.EnsureGenerationHandle<WfcOutpostPowerNode>(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731640, MaxCells, LogisticsGridSystemId, NativeArrayOptions.ClearMemory);
            _cellToNodeHandle = vault.EnsureGenerationHandle<int>(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731641, MaxCells, LogisticsGridSystemId, NativeArrayOptions.ClearMemory);
            _countsHandle = vault.EnsureGenerationHandle<int>(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731642, WfcOutpostGraphCountSlots.Count, LogisticsGridSystemId, NativeArrayOptions.ClearMemory);
            _generatorNodeIndexHandle = vault.EnsureGenerationHandle<int>(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731643, 1, LogisticsGridSystemId, NativeArrayOptions.ClearMemory);
            _blackBoxHandle = vault.EnsureGenerationHandle<WfcOutpostPowerBootTelemetryEntry>(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731644, TelemetryCapacity, LogisticsGridSystemId, NativeArrayOptions.ClearMemory);
            _powerEdgesHandle = vault.EnsureGenerationHandle<int2>(BufferID.WfcOutpostPowerBootRuntime_MigratedID_731645, MaxDirectedEdges, LogisticsGridSystemId, NativeArrayOptions.ClearMemory);
        }

        private NativeArray<T> ResolveBuffer<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
                return default;

            return vault.TryResolveHandle(in handle, out NativeArray<T> buffer) && buffer.IsCreated
                ? buffer
                : default;
        }

        private void ReleaseBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseBuffer(vault, ref _nodesHandle);
                ReleaseBuffer(vault, ref _cellToNodeHandle);
                ReleaseBuffer(vault, ref _countsHandle);
                ReleaseBuffer(vault, ref _generatorNodeIndexHandle);
                ReleaseBuffer(vault, ref _blackBoxHandle);
                ReleaseBuffer(vault, ref _powerEdgesHandle);
            }

            _dataVault = null;
        }

        private static void ReleaseBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryLockTranslationBuffers(out ulong lockMask)
        {
            lockMask = TranslationMutationGuardMask;
            IDataVault vault = _dataVault;
            if (vault == null ||
                lockMask == 0UL ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(lockMask))
            {
                lockMask = 0UL;
                return false;
            }

            return true;
        }

        private void UnlockTranslationBuffers(ulong lockMask)
        {
            IDataVault vault = _dataVault;
            if (vault == null || lockMask == 0UL)
                return;

            vault.ReleaseMutationGuard(lockMask);
        }

        private void ClearPowerEdges()
        {
            NativeArray<int2> edges = _powerEdges;
            if (!edges.IsCreated)
                return;

            for (int i = 0; i < edges.Length; i++)
                edges[i] = default;
        }

        private static void WriteNative<T>(NativeArray<T> buffer, int index, T value)
            where T : struct
        {
            buffer[index] = value;
        }

        public void BindGasDynamics(IGasDynamicsSolver gasDynamics)
        {
            _gasDynamics = gasDynamics;
            TryBindGraphBaseAwakeState();
            if (_gasSeedPending && _gasDynamics != null)
                TrySeedGasRooms();
        }

        public void SlowTick(float now)
        {
            if (!_initialized)
                return;

            ApplyAupShiftSignals();
            UpdateReactorDecay(now);

            if (_gasSeedPending && _gasDynamics != null)
                TrySeedGasRooms();

            if (_translationPending || _graphEvaluationPending)
                return;

            if (TryScheduleLatestGeneratedSignal())
                return;

            if (_hasActiveGraph)
                ScheduleGraphEvaluation(now);
        }

        public bool LateFrameTick(float now)
        {
            if (!_initialized)
                return false;

            if (_translationPending)
            {
                if (!DispatcherJobSwap.TryFinalizeCompleted(ref _translationHandle))
                    return true;

                _translationPending = false;
                try
                {
                    CommitTranslation(now);
                }
                finally
                {
                    ReleasePendingTranslationLocks();
                }
            }

            if (!_graphEvaluationPending)
                return false;

            if (!_graph.TryCompleteEvaluation())
                return true;

            _graphEvaluationPending = false;
            PublishDoorPowerSignals(now);
            PublishReactorBrownoutIfNeeded();
            WriteBlackBox(0u, ResolveLastSupplyRatio(), ResolveLastBrownoutSeverity());
            return false;
        }

        public void Dispose()
        {
            JobHandle dependency = _translationPending ? _translationHandle : default;
            if (_translationPending)
            {
                _translationHandle = default;
                _translationPending = false;
            }

            _graphEvaluationPending = false;

            _graph.Dispose();
            ForceCompleteTranslationInPostSimulationWindow(ref dependency);
            ReleasePendingTranslationLocks();
            ReleaseBuffers();
            JobHandle.ScheduleBatchedJobs();

            _initialized = false;
            _hasActiveGraph = false;
            _gasSeedPending = false;
            ResetDoorPowerLatches();
        }

        private static bool ForceCompleteTranslationInPostSimulationWindow(ref JobHandle dependency)
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return DispatcherJobFence.TryComplete(ref dependency, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private bool TryScheduleLatestGeneratedSignal()
        {
            ReadOnlySpan<WfcOutpostGeneratedSignal> signals = SignalBus<WfcOutpostGeneratedSignal>.GetFrameSnapshot();
            if (signals.Length == 0)
                return false;

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
                return false;

            return TryScheduleTranslation(in latest);
        }

        private bool TryScheduleTranslation(in WfcOutpostGeneratedSignal signal)
        {
            if (IsActiveGraphCurrent(in signal) || IsKnownFatalGraphCurrent(in signal))
                return false;

            if (_translationBufferLockMask != 0UL || _translationGridLeaseBufferId != BufferID.Unknown)
                return false;

            if (!WfcOutpostGridRegistry.TryAcquireGridLease(signal.GridHandle, out WfcOutpostGridLease lease))
                return false;

            if (!TryLockTranslationBuffers(out ulong lockMask))
            {
                WfcOutpostGridRegistry.ReleaseGridLease(in lease);
                return false;
            }

            bool scheduled = false;
            try
            {
                ClearPowerEdges();
                _pendingDescriptor = lease.Descriptor;
                _pendingGridHandle = signal.GridHandle;
                _lastGraphFaultFlags = 0;
                _faultDumped = false;

                WfcOutpostGraphTranslationJob job = new WfcOutpostGraphTranslationJob
                {
                    Cells = lease.Cells,
                    Descriptor = lease.Descriptor,
                    Nodes = _nodes,
                    CellToNode = _cellToNode,
                    PowerEdges = _powerEdges,
                    Counts = _counts,
                    GeneratorNodeIndex = _generatorNodeIndex
                };

                _translationHandle = job.Schedule();
                _translationPending = true;
                _translationBufferLockMask = lockMask;
                _translationGridLeaseBufferId = lease.BufferId;
                _translationGridLeaseSystemId = lease.SystemId;
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                {
                    UnlockTranslationBuffers(lockMask);
                    WfcOutpostGridRegistry.ReleaseGridLease(in lease);
                }
            }
        }

        private void ReleasePendingTranslationLocks()
        {
            if (_translationBufferLockMask != 0UL)
            {
                UnlockTranslationBuffers(_translationBufferLockMask);
                _translationBufferLockMask = 0UL;
            }

            if (_translationGridLeaseBufferId != BufferID.Unknown)
            {
                WfcOutpostGridRegistry.ReleaseGridLease(_translationGridLeaseBufferId, _translationGridLeaseSystemId);
                _translationGridLeaseBufferId = BufferID.Unknown;
                _translationGridLeaseSystemId = SystemID.Unknown;
            }
        }

        private bool IsActiveGraphCurrent(in WfcOutpostGeneratedSignal signal)
        {
            return _hasActiveGraph && IsSignalForActiveDescriptor(in signal);
        }

        private bool IsKnownFatalGraphCurrent(in WfcOutpostGeneratedSignal signal)
        {
            return HasFatalGraphFault(_lastGraphFaultFlags) && IsSignalForActiveDescriptor(in signal);
        }

        private bool IsSignalForActiveDescriptor(in WfcOutpostGeneratedSignal signal)
        {
            return signal.GridHandle == _activeGridHandle &&
                   signal.SectorHash == _activeDescriptor.SectorHash &&
                   signal.GenerationSequence == _activeDescriptor.GenerationSequence &&
                   signal.GridHash == _activeDescriptor.GridHash &&
                   signal.CellCount == _activeDescriptor.CellCount;
        }

        private void CommitTranslation(float now)
        {
            _activeDescriptor = _pendingDescriptor;
            _activeGridHandle = _pendingGridHandle;
            _activeNodeCount = ReadBoundedCount(WfcOutpostGraphCountSlots.NodeCount, MaxCells);
            _activeDirectedEdgeCount = ReadBoundedCount(WfcOutpostGraphCountSlots.DirectedEdgeCount, MaxDirectedEdges);
            _activeDoorCount = ReadBoundedCount(WfcOutpostGraphCountSlots.DoorCount, MaxCells);
            _activeRoomCount = ReadBoundedCount(WfcOutpostGraphCountSlots.RoomCount, MaxCells);
            _activeGeneratorNodeIndex = _generatorNodeIndex.IsCreated && _generatorNodeIndex.Length > 0 ? _generatorNodeIndex[0] : -1;
            _lastGraphFaultFlags = ReadCount(WfcOutpostGraphCountSlots.FaultFlags);
            _reactorOutput01 = InitialReactorOutput01;
            _lastReactorUpdateTime = SanitizeClockSeconds(now);
            ResetDoorPowerLatches();
            bool hasFatalGraphFault = HasFatalGraphFault(_lastGraphFaultFlags);
            _gasSeedPending = !hasFatalGraphFault;
            _hasActiveGraph = _activeNodeCount > 0 && !hasFatalGraphFault;

            GlobalTelemetryBus.PublishPerformanceWarning(OutpostNodeCountHash, WfcPowerBootContextHash, _activeNodeCount);
            if (_lastGraphFaultFlags != 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, _lastGraphFaultFlags);
                WriteBlackBox((uint)_lastGraphFaultFlags, 0f, 1f);
                DumpBlackBox((uint)_lastGraphFaultFlags);
            }

            if (!hasFatalGraphFault)
                TrySeedGasRooms();
            if (_hasActiveGraph)
                ScheduleGraphEvaluation(now);
        }

        private void ScheduleGraphEvaluation(float now)
        {
            if (_activeNodeCount <= 0 || HasFatalGraphFault(_lastGraphFaultFlags))
                return;

            TryBindGraphBaseAwakeState();
            BuildLogisticsGraph();
            _graph.ScheduleEvaluation();
            _graphEvaluationPending = _graph.HasPendingEvaluation;
            if (!_graphEvaluationPending)
            {
                PublishDoorPowerSignals(now);
                PublishReactorBrownoutIfNeeded();
                WriteBlackBox(0u, ResolveLastSupplyRatio(), ResolveLastBrownoutSeverity());
            }
        }

        private void BuildLogisticsGraph()
        {
            int nodeCount = math.clamp(_activeNodeCount, 0, MaxCells);
            int edgeCount = math.clamp(_activeDirectedEdgeCount, 0, MaxDirectedEdges);
            _graph.BeginBuild(LogisticsNetworkType.PowerDc, nodeCount, math.max(1, edgeCount), nodeCount);

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                WfcOutpostPowerNode node = _nodes[nodeIndex];
                LogisticsNodeFlags flags = LogisticsNodeFlags.Active;
                if (nodeIndex == _activeGeneratorNodeIndex)
                    flags |= LogisticsNodeFlags.EmergencyReserved;

                _graph.AddNode(node.NodeId, NodeCapacityWatts, NodeResistance, node.PriorityTier, flags, node.Kind);
            }

            for (int edgeIndex = 0; edgeIndex < edgeCount; edgeIndex++)
            {
                int2 edge = _powerEdges[edgeIndex];
                if ((uint)edge.x < (uint)nodeCount && (uint)edge.y < (uint)nodeCount)
                    _graph.AddEdge(edge.x, edge.y, EdgeResistance);
            }

            if ((uint)_activeGeneratorNodeIndex < (uint)nodeCount)
                _graph.AddProducer(_activeGeneratorNodeIndex, GeneratorWatts * math.saturate(_reactorOutput01));

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                WfcOutpostPowerNode node = _nodes[nodeIndex];
                float demand = ResolveDemandWatts(node.Kind);
                if (demand <= 0f)
                    continue;

                LogisticsConsumerFlags consumerFlags = ResolveConsumerFlags(node.Kind);
                _graph.AddConsumer(nodeIndex, demand, ResolveConsumerPriority(node.Kind), node.PriorityTier, consumerFlags);
            }

            _graph.FinalizeBuild();
        }

        private void TryBindGraphBaseAwakeState()
        {
            IGasDynamicsSolver gas = _gasDynamics;
            if (gas == null || !gas.IsInitialized)
            {
                _graph.TryBindBaseAwakeStateValue(1);
                return;
            }

            if (!gas.TryGetBaseHibernationSnapshot(0, out GasBaseHibernationSnapshot snapshot))
            {
                _graph.TryBindBaseAwakeStateValue(1);
                return;
            }

            _graph.TryBindBaseAwakeStateValue(snapshot.Awake ? (byte)1 : (byte)0);
        }

        private void UpdateReactorDecay(float now)
        {
            float safeNow = SanitizeClockSeconds(now);
            if (!_hasActiveGraph)
            {
                _lastReactorUpdateTime = safeNow;
                return;
            }

            if (!math.isfinite(now))
            {
                _lastReactorUpdateTime = safeNow;
                WriteBlackBox(FaultFlag | ReactorClockFaultFlag, 0f, 1f);
                return;
            }

            if (!math.isfinite(_lastReactorUpdateTime))
            {
                _lastReactorUpdateTime = safeNow;
                WriteBlackBox(FaultFlag | ReactorClockFaultFlag, 0f, 1f);
                return;
            }

            if (_lastReactorUpdateTime <= 0f)
                _lastReactorUpdateTime = safeNow;

            float dt = math.max(0f, safeNow - _lastReactorUpdateTime);
            _lastReactorUpdateTime = safeNow;
            _reactorOutput01 = math.max(0f, _reactorOutput01 - dt * ReactorDecayPerSecond);
        }

        private void PublishDoorPowerSignals(float now)
        {
            if (!_hasActiveGraph)
                return;

            int nodeCount = math.clamp(_activeNodeCount, 0, MaxCells);
            if (nodeCount <= 0)
            {
                _doorResyncCursor = 0;
                return;
            }

            float safeNow = SanitizeClockSeconds(now);
            uint frame = CurrentFrameU32();
            int resyncWindowStart = _doorResyncCursor % nodeCount;
            int pushBudget = DoorSignalPushBudgetPerPass;

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                WfcOutpostPowerNode node = _nodes[nodeIndex];
                if (node.Kind != WfcOutpostGridConstants.SealedDoor)
                    continue;

                float voltage = 0f;
                _graph.TryGetNodePotential(nodeIndex, out voltage);
                voltage = math.saturate(voltage);

                byte latched = PowerHysteresisLatch.Evaluate(
                    _doorLatchState[nodeIndex],
                    voltage,
                    DoorUnlockVoltage,
                    DoorLockVoltage,
                    safeNow,
                    _doorLatchCandidateSince[nodeIndex],
                    DoorSwitchDwellSeconds,
                    out float nextCandidateSince);
                _doorLatchState[nodeIndex] = latched;
                _doorLatchCandidateSince[nodeIndex] = nextCandidateSince;

                bool changed = _doorPublishedState[nodeIndex] != latched;
                if (!changed && !IsInDoorResyncWindow(nodeIndex, resyncWindowStart, nodeCount))
                    continue;

                if (pushBudget <= 0)
                {
                    // Lane budget spent. Leave _doorPublishedState untouched so this
                    // door is retried next pass instead of being dropped by the bus.
                    continue;
                }

                byte unlocked = (byte)(PowerHysteresisLatch.IsEngaged(latched) ? 1 : 0);
                WfcOutpostDoorPowerSignal signal = new WfcOutpostDoorPowerSignal
                {
                    DoorAup = ResolveNodeAup(in node),
                    SectorHash = _activeDescriptor.SectorHash,
                    GridHandle = _activeGridHandle,
                    NodeId = node.NodeId,
                    CellIndex = node.CellIndex,
                    DoorId = node.DoorId,
                    Voltage = voltage,
                    Frame = frame,
                    Unlocked = unlocked,
                    Flags = unlocked
                };

                if (!SignalBus<WfcOutpostDoorPowerSignal>.TryPushTracked(in signal, ref s_x001WfcOutpostPowerBootRuntimeSignalPushDropCount))
                    break;

                _doorPublishedState[nodeIndex] = latched;
                pushBudget--;
            }

            int nextCursor = resyncWindowStart + DoorResyncStrideNodes;
            _doorResyncCursor = nextCursor >= nodeCount ? nextCursor % nodeCount : nextCursor;
        }

        /// <summary>
        /// Round-robin keepalive window over node indices. Unchanged doors are
        /// re-announced a slice at a time so a door controller that spawned after
        /// the last real transition still converges, without flooding the lane.
        /// </summary>
        private static bool IsInDoorResyncWindow(int nodeIndex, int windowStart, int nodeCount)
        {
            if (nodeCount <= 0)
                return false;
            if (DoorResyncStrideNodes >= nodeCount)
                return true;

            int offset = nodeIndex - windowStart;
            if (offset < 0)
                offset += nodeCount;

            return offset < DoorResyncStrideNodes;
        }

        private void PublishReactorBrownoutIfNeeded()
        {
            if (!_hasActiveGraph || _reactorOutput01 >= ReactorBrownoutThreshold01)
                return;

            float supplyRatio = ResolveLastSupplyRatio();
            float severity = ResolveLastBrownoutSeverity();
            BrownoutSignal signal = new BrownoutSignal
            {
                NetworkId = _activeGridHandle,
                NodeId = (uint)math.max(0, _activeGeneratorNodeIndex),
                SupplyRatio = supplyRatio,
                Severity01 = severity,
                Frame = CurrentFrameU32(),
                Priority = (byte)LogisticsBrownoutTier.EmergencyOnly,
                Flags = 1 << 2
            };
            SignalBus<BrownoutSignal>.TryPushTracked(in signal, ref s_x001WfcOutpostPowerBootRuntimeSignalPushDropCount);
        }

        private void TrySeedGasRooms()
        {
            if (!_hasActiveGraph || HasFatalGraphFault(_lastGraphFaultFlags))
            {
                _gasSeedPending = false;
                return;
            }

            IGasDynamicsSolver gas = _gasDynamics;
            if (gas == null || !gas.IsInitialized || _activeRoomCount <= 0)
            {
                _gasSeedPending = true;
                return;
            }

            int roomLimit = math.min(_activeRoomCount, gas.RoomCount);
            if (roomLimit <= 0)
            {
                _gasSeedPending = true;
                return;
            }

            float o2 = StandardOxygenKPa * 0.05f;
            int nodeLimit = math.clamp(_activeNodeCount, 0, MaxCells);
            bool allSeeded = true;
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                WfcOutpostPowerNode node = _nodes[nodeIndex];
                if (node.RoomId == ushort.MaxValue || node.RoomId >= roomLimit)
                    continue;

                bool configured = gas.TryConfigureRoom(node.RoomId, o2, StandardCarbonDioxideKPa, StandardNitrogenKPa, StandardAmbientKPa, 0);
                bool scrubberSet = gas.TrySetScrubberPowered(node.RoomId, false);
                allSeeded &= configured && scrubberSet;
            }

            _gasSeedPending = !allSeeded;
        }

        private void ApplyAupShiftSignals()
        {
            if (!_hasActiveGraph && !_translationPending)
                return;

            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 shift = shifts[i].ShiftMeters;
                if (!math.all(math.isfinite(shift)))
                {
                    WriteBlackBox(FaultFlag | AupShiftFlag, 0f, 1f);
                    continue;
                }

                if (math.all(math.abs(shift) < new float3(ShiftEpsilonMeters)))
                    continue;

                if (!IsWithinAupShiftLimit(shift))
                {
                    WriteBlackBox(FaultFlag | AupShiftFlag, 0f, 1f);
                    continue;
                }

                if (_hasActiveGraph)
                    ShiftDescriptor(ref _activeDescriptor, shift);
                if (_translationPending)
                    ShiftDescriptor(ref _pendingDescriptor, shift);
            }
        }

        private AbsoluteUniversePosition ResolveNodeAup(in WfcOutpostPowerNode node)
        {
            double3 origin = ResolveMacroAupAbsolute(in _activeDescriptor.OriginAup);
            double3 delta = new double3(
                node.LocalOffsetMeters.x,
                node.LocalOffsetMeters.y,
                node.LocalOffsetMeters.z);
            double3 absolute = math.all(math.isfinite(delta)) ? origin + delta : origin;
            if (!math.all(math.isfinite(absolute)))
                absolute = origin;
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
        }

        private static void ShiftDescriptor(ref WfcOutpostGridDescriptor descriptor, float3 shift)
        {
            double3 origin = ResolveMacroAupAbsolute(in descriptor.OriginAup);
            double3 shifted = origin + new double3(-shift.x, -shift.y, -shift.z);
            if (math.all(math.isfinite(shifted)))
                descriptor.OriginAup = ToMacroAup(shifted);
        }

        private static bool IsWithinAupShiftLimit(float3 shiftMeters)
        {
            return math.all(math.abs(shiftMeters) <= new float3(MaxAupShiftMeters));
        }

        private static double3 ResolveMacroAupAbsolute(in Hecton8.Core.Contracts.MacroDatabaseAup aup)
        {
            double3 local = new double3(aup.LocalX, aup.LocalY, aup.LocalZ);
            if (!math.all(math.isfinite(local)))
                local = double3.zero;

            return new double3(
                (aup.GridX * MacroAupCellSizeMeters) + local.x,
                (aup.GridY * MacroAupCellSizeMeters) + local.y,
                (aup.GridZ * MacroAupCellSizeMeters) + local.z);
        }

        private static Hecton8.Core.Contracts.MacroDatabaseAup ToMacroAup(double3 absolute)
        {
            if (!math.all(math.isfinite(absolute)))
                return default;

            long gridX = (long)math.floor(absolute.x / MacroAupCellSizeMeters);
            long gridY = (long)math.floor(absolute.y / MacroAupCellSizeMeters);
            long gridZ = (long)math.floor(absolute.z / MacroAupCellSizeMeters);
            return new Hecton8.Core.Contracts.MacroDatabaseAup
            {
                GridX = gridX,
                GridY = gridY,
                GridZ = gridZ,
                LocalX = (float)(absolute.x - (gridX * MacroAupCellSizeMeters)),
                LocalY = (float)(absolute.y - (gridY * MacroAupCellSizeMeters)),
                LocalZ = (float)(absolute.z - (gridZ * MacroAupCellSizeMeters))
            };
        }

        private static Hecton8.Core.Contracts.MacroDatabaseAup ToMacroAup(in AbsoluteUniversePosition aup)
        {
            return new Hecton8.Core.Contracts.MacroDatabaseAup
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ
            };
        }

        private float ResolveLastSupplyRatio()
        {
            LogisticsNetworkGraph.DistributionSummary summary = _graph.GetScheduledDistributionSummary();
            return math.saturate(summary.TotalConsumption > 0.0001f ? summary.SupplyRatio : 1f);
        }

        private float ResolveLastBrownoutSeverity()
        {
            float supplySeverity = 1f - ResolveLastSupplyRatio();
            if (_reactorOutput01 >= ReactorBrownoutThreshold01)
                return math.saturate(supplySeverity);

            float reactorSeverity = 1f - math.saturate(_reactorOutput01 / ReactorBrownoutThreshold01);
            return math.saturate(math.max(supplySeverity, reactorSeverity));
        }

        private void WriteBlackBox(uint flags, float supplyRatio, float severity01)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _blackBoxHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _blackBoxHandle, LogisticsGridSystemId, out NativeArray<WfcOutpostPowerBootTelemetryEntry> blackBox))
            {
                return;
            }

            bool dumpRequired = false;
            try
            {
                if (!blackBox.IsCreated || blackBox.Length == 0)
                    return;

                if (!math.isfinite(supplyRatio) || !math.isfinite(severity01) || !math.isfinite(_reactorOutput01))
                {
                    flags |= FaultFlag;
                    supplyRatio = 0f;
                    severity01 = 1f;
                    _reactorOutput01 = 0f;
                }

                int length = blackBox.Length;
                int index = _blackBoxCursor;
                if ((uint)index >= (uint)length)
                    index = 0;

                WriteNative(blackBox, index, new WfcOutpostPowerBootTelemetryEntry
                {
                    Frame = CurrentFrameU32(),
                    GridHandle = _activeGridHandle,
                    SectorHash = _activeDescriptor.SectorHash,
                    NodeCount = _activeNodeCount,
                    DirectedEdgeCount = _activeDirectedEdgeCount,
                    DoorCount = _activeDoorCount,
                    RoomCount = _activeRoomCount,
                    GeneratorNodeIndex = _activeGeneratorNodeIndex,
                    ReactorOutput01 = math.saturate(_reactorOutput01),
                    SupplyRatio = math.saturate(supplyRatio),
                    BrownoutSeverity01 = math.saturate(severity01),
                    Flags = flags,
                    GraphHash = _activeDescriptor.GridHash
                });
                index++;
                _blackBoxCursor = index >= length ? 0 : index;
                dumpRequired = (flags & FaultFlag) != 0;
            }
            finally
            {
                vault.ReleaseWriteLock(in _blackBoxHandle, LogisticsGridSystemId);
            }

            if (dumpRequired)
                DumpBlackBox(flags);
        }

        private void DumpBlackBox(uint reasonFlags)
        {
            if (_faultDumped || !_blackBox.IsCreated)
                return;

            try
            {
                int length = _blackBox.Length;
                int startIndex = _blackBoxCursor;
                if ((uint)startIndex >= (uint)length)
                    startIndex = 0;

                long totalBytes = DumpHeaderBytes + ((long)length * DumpTelemetryRowBytes);
                if (totalBytes < DumpHeaderBytes || totalBytes > int.MaxValue)
                    return;

                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    (int)totalBytes,
                    nameof(WfcOutpostPowerBootRuntime),
                    "wfcOutpostPowerBootBlackBoxPayload");
                try
                {
                    WriteUInt32LittleEndian(payload, 0, DumpMagic);
                    WriteInt32LittleEndian(payload, 4, DumpVersion);
                    WriteUInt32LittleEndian(payload, 8, reasonFlags);
                    WriteInt32LittleEndian(payload, 12, length);
                    WriteInt32LittleEndian(payload, 16, startIndex);

                    int cursor = DumpHeaderBytes;
                    for (int offset = 0; offset < length; offset++)
                    {
                        int sourceIndex = startIndex + offset;
                        if (sourceIndex >= length)
                            sourceIndex -= length;

                        WriteBlackBoxEntry(payload, cursor, _blackBox[sourceIndex]);
                        cursor += DumpTelemetryRowBytes;
                    }

                    _faultDumped = NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, (int)totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(WfcOutpostPowerBootRuntime),
                        "wfcOutpostPowerBootBlackBoxPayload");
                }
            }
            catch (IOException exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, exception.HResult);
            }
            catch (UnauthorizedAccessException exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, exception.HResult);
            }
            catch (ArgumentException exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, exception.HResult);
            }
            catch (NotSupportedException exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, exception.HResult);
            }
        }

        private static void WriteBlackBoxEntry(
            NativeArray<byte> destination,
            int offset,
            WfcOutpostPowerBootTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, offset, entry.Frame);
            WriteUInt32LittleEndian(destination, offset + 4, entry.GridHandle);
            WriteUInt64LittleEndian(destination, offset + 8, entry.SectorHash);
            WriteInt32LittleEndian(destination, offset + 16, entry.NodeCount);
            WriteInt32LittleEndian(destination, offset + 20, entry.DirectedEdgeCount);
            WriteInt32LittleEndian(destination, offset + 24, entry.DoorCount);
            WriteInt32LittleEndian(destination, offset + 28, entry.RoomCount);
            WriteInt32LittleEndian(destination, offset + 32, entry.GeneratorNodeIndex);
            WriteFloat32LittleEndian(destination, offset + 36, entry.ReactorOutput01);
            WriteFloat32LittleEndian(destination, offset + 40, entry.SupplyRatio);
            WriteFloat32LittleEndian(destination, offset + 44, entry.BrownoutSeverity01);
            WriteUInt32LittleEndian(destination, offset + 48, entry.Flags);
            WriteUInt32LittleEndian(destination, offset + 52, entry.GraphHash);
        }

        private static void WriteFloat32LittleEndian(NativeArray<byte> destination, int offset, float value)
        {
            WriteUInt32LittleEndian(destination, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> destination, int offset, int value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> destination, int offset, uint value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(NativeArray<byte> destination, int offset, ulong value)
        {
            WriteUInt32LittleEndian(destination, offset, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, offset + 4, unchecked((uint)(value >> 32)));
        }

        private int ReadCount(int slot)
        {
            return _counts.IsCreated && (uint)slot < (uint)_counts.Length ? math.max(0, _counts[slot]) : 0;
        }

        private int ReadBoundedCount(int slot, int maxValue)
        {
            return math.min(ReadCount(slot), maxValue);
        }

        private static uint CurrentFrameU32()
        {
            return Hecton8.Core.SystemDispatcher.CurrentFrameId;
        }

        private static bool HasFatalGraphFault(int faultFlags)
        {
            return (faultFlags & FatalGraphFaultMask) != 0;
        }

        private static float SanitizeClockSeconds(float now)
        {
            return math.isfinite(now) ? math.max(0f, now) : 0f;
        }

        private static float ResolveDemandWatts(byte kind)
        {
            switch (kind)
            {
                case WfcOutpostGridConstants.Generator:
                    return 0f;
                case WfcOutpostGridConstants.SealedDoor:
                case WfcOutpostGridConstants.Hatch:
                    return DoorDemandWatts;
                case WfcOutpostGridConstants.Corridor:
                    return CorridorDemandWatts;
                case WfcOutpostGridConstants.Datapad:
                    return DatapadDemandWatts;
                default:
                    return RoomDemandWatts;
            }
        }

        private static int ResolveConsumerPriority(byte kind)
        {
            if (kind == WfcOutpostGridConstants.SealedDoor || kind == WfcOutpostGridConstants.Hatch)
                return 90;
            if (kind == WfcOutpostGridConstants.Datapad)
                return 60;
            return 40;
        }

        private static LogisticsConsumerFlags ResolveConsumerFlags(byte kind)
        {
            if (kind == WfcOutpostGridConstants.SealedDoor || kind == WfcOutpostGridConstants.Hatch)
                return LogisticsConsumerFlags.Essential | LogisticsConsumerFlags.EmergencyReserved;
            if (kind == WfcOutpostGridConstants.Room)
                return LogisticsConsumerFlags.LifeSupport | LogisticsConsumerFlags.AmbientLighting;
            return LogisticsConsumerFlags.AmbientLighting;
        }
    }
}
