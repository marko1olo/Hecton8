using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Signals;
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
        private const string OwnerName = "OUTPOST_LOGISTICS_INITIALIZER";
        private const int MaxCells = WfcOutpostGridConstants.MaxCellCount;
        private const int MaxDirectedEdges = WfcOutpostGridConstants.MaxDirectedEdges;
        private const int TelemetryCapacity = WfcOutpostGridConstants.TelemetryFrames;
        private const int TelemetryEntrySizeBytes = 64;
        private const uint DumpMagic = 0x4F504257u; // OPBW
        private const int DumpVersion = 1;
        private const SystemID LogisticsGridSystemId = (SystemID)512;
        private const float InitialReactorOutput01 = 0.05f;
        private const float ReactorDecayPerSecond = 0.01f / 60f;
        private const float ReactorBrownoutThreshold01 = 0.02f;
        private const float DoorUnlockVoltage = 0.1f;
        private const float GeneratorWatts = 5000f;
        private const float RoomDemandWatts = 12f;
        private const float CorridorDemandWatts = 4f;
        private const float DoorDemandWatts = 8f;
        private const float DatapadDemandWatts = 2f;
        private const float NodeCapacityWatts = 1000f;
        private const float EdgeResistance = 0.025f;
        private const float NodeResistance = 0.02f;
        private const float StandardOxygenKPa = 21.22f;
        private const float StandardCarbonDioxideKPa = 0.04f;
        private const float StandardNitrogenKPa = 80.065f;
        private const float StandardAmbientKPa = 101.325f;
        private const uint OutpostNodeCountHash = 0x4F4E4354u; // ONCT
        private const uint WfcPowerBootContextHash = 0x57465042u; // WFPB
        private const uint FaultTelemetryHash = 0x57464654u; // WFFT

        private readonly LogisticsNetworkGraph _graph; // COLD ALLOC: LogisticsNetworkGraph[1] - WFC outpost power evaluator - owner: WfcOutpostPowerBootRuntime
        private NativeArray<WfcOutpostPowerNode> _nodes;
        private NativeArray<int> _cellToNode;
        private NativeArray<int> _counts;
        private NativeArray<int> _generatorNodeIndex;
        private NativeArray<WfcOutpostPowerBootTelemetryEntry> _blackBox;
        private NativeParallelMultiHashMap<int, int> _powerEdges;
        private JobHandle _translationHandle;
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
        private int _lastGraphFaultFlags;
        private float _reactorOutput01;
        private float _lastReactorUpdateTime;
        private bool _initialized;
        private bool _translationPending;
        private bool _graphEvaluationPending;
        private bool _hasActiveGraph;
        private bool _gasSeedPending;
        private bool _faultDumped;

        [StructLayout(LayoutKind.Sequential, Size = TelemetryEntrySizeBytes)]
        private struct WfcOutpostPowerBootTelemetryEntry
        {
            public uint Frame;
            public uint GridHandle;
            public ulong SectorHash;
            public int NodeCount;
            public int DirectedEdgeCount;
            public int DoorCount;
            public int RoomCount;
            public int GeneratorNodeIndex;
            public float ReactorOutput01;
            public float SupplyRatio;
            public float BrownoutSeverity01;
            public uint Flags;
            public uint GraphHash;
            public uint Reserved0;
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

            _nodes = H8Memory.Allocate<WfcOutpostPowerNode>(MaxCells, LogisticsGridSystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WfcOutpostPowerNode>[500] - WFC outpost node SOA - owner: WfcOutpostPowerBootRuntime
            _cellToNode = H8Memory.Allocate<int>(MaxCells, LogisticsGridSystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[500] - WFC cell-to-node map - owner: WfcOutpostPowerBootRuntime
            _counts = H8Memory.Allocate<int>(WfcOutpostGraphCountSlots.Count, LogisticsGridSystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[5] - WFC graph counters - owner: WfcOutpostPowerBootRuntime
            _generatorNodeIndex = H8Memory.Allocate<int>(1, LogisticsGridSystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - WFC generator node index - owner: WfcOutpostPowerBootRuntime
            _blackBox = H8Memory.Allocate<WfcOutpostPowerBootTelemetryEntry>(TelemetryCapacity, LogisticsGridSystemId, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WfcOutpostPowerBootTelemetryEntry>[300] - WFC power blackbox - owner: WfcOutpostPowerBootRuntime
            _powerEdges = new NativeParallelMultiHashMap<int, int>(MaxDirectedEdges, Allocator.Persistent); // COLD ALLOC: NativeParallelMultiHashMap<int,int>[3000] - logical WFC power adjacency - owner: WfcOutpostPowerBootRuntime
            NativeMemorySentinel.RegisterNativeParallelMultiHashMap(
                _powerEdges,
                OwnerName,
                nameof(_powerEdges),
                NativeAllocationLifetime.Session);

            _initialized = _nodes.IsCreated &&
                           _cellToNode.IsCreated &&
                           _counts.IsCreated &&
                           _generatorNodeIndex.IsCreated &&
                           _blackBox.IsCreated &&
                           _powerEdges.IsCreated;
        }

        public void BindGasDynamics(IGasDynamicsSolver gasDynamics)
        {
            _gasDynamics = gasDynamics;
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
                ScheduleGraphEvaluation();
        }

        public bool LateFrameTick(float now)
        {
            if (!_initialized)
                return false;

            if (_translationPending)
            {
                if (!_translationHandle.IsCompleted)
                    return true;

                _translationHandle.Complete();
                _translationHandle = default;
                _translationPending = false;
                CommitTranslation(now);
            }

            if (!_graphEvaluationPending)
                return false;

            if (!_graph.TryCompleteEvaluation())
                return true;

            _graphEvaluationPending = false;
            PublishDoorPowerSignals();
            PublishReactorBrownoutIfNeeded();
            WriteBlackBox(0u, ResolveLastSupplyRatio(), ResolveLastBrownoutSeverity());
            return false;
        }

        public void Dispose()
        {
            if (_translationPending)
            {
                _translationHandle.Complete();
                _translationPending = false;
            }

            if (_graphEvaluationPending)
            {
                _graph.CompleteEvaluation();
                _graphEvaluationPending = false;
            }

            _graph.Dispose();
            NativeMemorySentinel.UnregisterNativeParallelMultiHashMap(OwnerName, nameof(_powerEdges));

            JobHandle dependency = default;
            if (_powerEdges.IsCreated)
            {
                dependency = _powerEdges.Dispose(dependency);
                _powerEdges = default;
            }

            dependency = H8Memory.Release(ref _nodes, dependency, LogisticsGridSystemId);
            dependency = H8Memory.Release(ref _cellToNode, dependency, LogisticsGridSystemId);
            dependency = H8Memory.Release(ref _counts, dependency, LogisticsGridSystemId);
            dependency = H8Memory.Release(ref _generatorNodeIndex, dependency, LogisticsGridSystemId);
            H8Memory.Release(ref _blackBox, dependency, LogisticsGridSystemId);

            _initialized = false;
            _hasActiveGraph = false;
            _gasSeedPending = false;
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
                if (signal.GridHandle == 0u || signal.CellCount == 0)
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
            if (!WfcOutpostGridRegistry.TryGetGrid(signal.GridHandle, out WfcOutpostGridLease lease))
                return false;

            _powerEdges.Clear();
            _pendingDescriptor = lease.Descriptor;
            _pendingGridHandle = signal.GridHandle;
            _lastGraphFaultFlags = 0;

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
            return true;
        }

        private void CommitTranslation(float now)
        {
            _activeDescriptor = _pendingDescriptor;
            _activeGridHandle = _pendingGridHandle;
            _activeNodeCount = ReadCount(WfcOutpostGraphCountSlots.NodeCount);
            _activeDirectedEdgeCount = ReadCount(WfcOutpostGraphCountSlots.DirectedEdgeCount);
            _activeDoorCount = ReadCount(WfcOutpostGraphCountSlots.DoorCount);
            _activeRoomCount = ReadCount(WfcOutpostGraphCountSlots.RoomCount);
            _activeGeneratorNodeIndex = _generatorNodeIndex.IsCreated && _generatorNodeIndex.Length > 0 ? _generatorNodeIndex[0] : -1;
            _lastGraphFaultFlags = ReadCount(WfcOutpostGraphCountSlots.FaultFlags);
            _reactorOutput01 = InitialReactorOutput01;
            _lastReactorUpdateTime = now;
            _gasSeedPending = true;
            _hasActiveGraph = _activeNodeCount > 0;

            GlobalTelemetryBus.PublishPerformanceWarning(OutpostNodeCountHash, WfcPowerBootContextHash, _activeNodeCount);
            if (_lastGraphFaultFlags != 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, _lastGraphFaultFlags);
                WriteBlackBox((uint)_lastGraphFaultFlags, 0f, 1f);
                DumpBlackBox((uint)_lastGraphFaultFlags);
            }

            TrySeedGasRooms();
            if (_hasActiveGraph)
                ScheduleGraphEvaluation();
        }

        private void ScheduleGraphEvaluation()
        {
            if (_activeNodeCount <= 0)
                return;

            BuildLogisticsGraph();
            _graph.ScheduleEvaluation();
            _graphEvaluationPending = _graph.HasPendingEvaluation;
            if (!_graphEvaluationPending)
            {
                PublishDoorPowerSignals();
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

            for (int sourceNode = 0; sourceNode < nodeCount; sourceNode++)
            {
                if (!_powerEdges.TryGetFirstValue(sourceNode, out int destinationNode, out NativeParallelMultiHashMapIterator<int> iterator))
                    continue;

                do
                {
                    if ((uint)destinationNode < (uint)nodeCount)
                        _graph.AddEdge(sourceNode, destinationNode, EdgeResistance);
                }
                while (_powerEdges.TryGetNextValue(out destinationNode, ref iterator));
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

        private void UpdateReactorDecay(float now)
        {
            if (!_hasActiveGraph)
            {
                _lastReactorUpdateTime = now;
                return;
            }

            if (!math.isfinite(now))
                return;

            if (_lastReactorUpdateTime <= 0f)
                _lastReactorUpdateTime = now;

            float dt = math.max(0f, now - _lastReactorUpdateTime);
            _lastReactorUpdateTime = now;
            _reactorOutput01 = math.max(0f, _reactorOutput01 - dt * ReactorDecayPerSecond);
        }

        private void PublishDoorPowerSignals()
        {
            int nodeCount = math.clamp(_activeNodeCount, 0, MaxCells);
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                WfcOutpostPowerNode node = _nodes[nodeIndex];
                if (node.Kind != WfcOutpostGridConstants.SealedDoor)
                    continue;

                float voltage = 0f;
                _graph.TryGetNodePotential(nodeIndex, out voltage);
                voltage = math.saturate(voltage);
                WfcOutpostDoorPowerSignal signal = new WfcOutpostDoorPowerSignal
                {
                    DoorAup = ResolveNodeAup(in node),
                    SectorHash = _activeDescriptor.SectorHash,
                    GridHandle = _activeGridHandle,
                    NodeId = node.NodeId,
                    CellIndex = node.CellIndex,
                    DoorId = node.DoorId,
                    Voltage = voltage,
                    Frame = unchecked((uint)math.max(0, Time.frameCount)),
                    Unlocked = (byte)(voltage > DoorUnlockVoltage ? 1 : 0),
                    Flags = (byte)(voltage > DoorUnlockVoltage ? 1 : 0)
                };
                GlobalSignals.Publish(in signal);
            }
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
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                Priority = (byte)LogisticsBrownoutTier.EmergencyOnly,
                Flags = 1 << 2
            };
            GlobalSignals.Publish(in signal);
        }

        private void TrySeedGasRooms()
        {
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
            for (int nodeIndex = 0; nodeIndex < _activeNodeCount; nodeIndex++)
            {
                WfcOutpostPowerNode node = _nodes[nodeIndex];
                if (node.RoomId == ushort.MaxValue || node.RoomId >= roomLimit)
                    continue;

                gas.TryConfigureRoom(node.RoomId, o2, StandardCarbonDioxideKPa, StandardNitrogenKPa, StandardAmbientKPa, 0);
                gas.TrySetScrubberPowered(node.RoomId, false);
            }

            _gasSeedPending = false;
        }

        private void ApplyAupShiftSignals()
        {
            if (!_hasActiveGraph && !_translationPending)
                return;

            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                float3 shift = shifts[i].ShiftMeters;
                if (!math.all(math.isfinite(shift)) || math.all(math.abs(shift) < new float3(0.0001f)))
                    continue;

                if (_hasActiveGraph)
                    ShiftDescriptor(ref _activeDescriptor, shift);
                if (_translationPending)
                    ShiftDescriptor(ref _pendingDescriptor, shift);
            }
        }

        private AbsoluteUniversePosition ResolveNodeAup(in WfcOutpostPowerNode node)
        {
            double3 absolute = _activeDescriptor.OriginAup.ToAbsoluteDouble3() + new double3(
                node.LocalOffsetMeters.x,
                node.LocalOffsetMeters.y,
                node.LocalOffsetMeters.z);
            return AbsoluteUniversePosition.FromAbsolutePosition(absolute);
        }

        private static void ShiftDescriptor(ref WfcOutpostGridDescriptor descriptor, float3 shift)
        {
            double3 shifted = descriptor.OriginAup.ToAbsoluteDouble3() - new double3(shift.x, shift.y, shift.z);
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromAbsolutePosition(shifted);
            descriptor.OriginAup = ToMacroAup(in aup);
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
            if (!_blackBox.IsCreated || _blackBox.Length == 0)
                return;

            if (!math.isfinite(supplyRatio) || !math.isfinite(severity01) || !math.isfinite(_reactorOutput01))
            {
                flags |= 1u << 31;
                supplyRatio = 0f;
                severity01 = 1f;
                _reactorOutput01 = 0f;
            }

            int index = _blackBoxCursor % _blackBox.Length;
            _blackBox[index] = new WfcOutpostPowerBootTelemetryEntry
            {
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
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
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % _blackBox.Length;

            if ((flags & (1u << 31)) != 0)
                DumpBlackBox(flags);
        }

        private void DumpBlackBox(uint reasonFlags)
        {
            if (_faultDumped || !_blackBox.IsCreated)
                return;

            _faultDumped = true;
            try
            {
                string path = Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_OUTPOST_LOGISTICS_INITIALIZER.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                using BinaryWriter writer = new BinaryWriter(stream);
                writer.Write(DumpMagic);
                writer.Write(DumpVersion);
                writer.Write(reasonFlags);
                writer.Write(_blackBox.Length);
                writer.Write(_blackBoxCursor);
                for (int i = 0; i < _blackBox.Length; i++)
                {
                    WfcOutpostPowerBootTelemetryEntry entry = _blackBox[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.GridHandle);
                    writer.Write(entry.SectorHash);
                    writer.Write(entry.NodeCount);
                    writer.Write(entry.DirectedEdgeCount);
                    writer.Write(entry.DoorCount);
                    writer.Write(entry.RoomCount);
                    writer.Write(entry.GeneratorNodeIndex);
                    writer.Write(entry.ReactorOutput01);
                    writer.Write(entry.SupplyRatio);
                    writer.Write(entry.BrownoutSeverity01);
                    writer.Write(entry.Flags);
                    writer.Write(entry.GraphHash);
                }
            }
            catch (Exception exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(FaultTelemetryHash, WfcPowerBootContextHash, exception.HResult);
            }
        }

        private int ReadCount(int slot)
        {
            return _counts.IsCreated && (uint)slot < (uint)_counts.Length ? math.max(0, _counts[slot]) : 0;
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
