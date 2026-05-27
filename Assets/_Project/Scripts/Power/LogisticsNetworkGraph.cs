using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [Flags]
    public enum LogisticsConsumerFlags : ushort
    {
        None = 0,
        LifeSupport = 1 << 0,
        AmbientLighting = 1 << 1,
        Essential = 1 << 2,
        EmergencyReserved = 1 << 3
    }

    [Flags]
    public enum LogisticsNodeFlags : byte
    {
        None = 0,
        Active = 1 << 0,
        Ruptured = 1 << 1,
        Brownout = 1 << 2,
        Isolated = 1 << 3,
        BridgeNode = 1 << 4,
        Overloaded = 1 << 5,
        EmergencyReserved = 1 << 6,
        Dirty = 1 << 7
    }

    public enum LogisticsBrownoutTier : byte
    {
        None = 0,
        AmbientLightsOnly = 1,
        EssentialOnly = 2,
        EmergencyOnly = 3
    }

    public enum LogisticsNetworkType : byte
    {
        PowerDc = 0,
        OxygenPressure = 1
    }

    [Flags]
    public enum LogisticsEdgeState : byte
    {
        None = 0,
        Overloaded = 1 << 0,
        Ruptured = 1 << 1
    }

    [Flags]
    public enum LogisticsNodeStateBits : ushort
    {
        None = 0,
        Active = 1 << 0,
        Reachable = 1 << 1,
        HasGeneration = 1 << 2,
        HasServedDemand = 1 << 3,
        Brownout = 1 << 4,
        Isolated = 1 << 5,
        Overloaded = 1 << 6,
        Ruptured = 1 << 7,
        EmergencyReserved = 1 << 8,
        HasPotential = 1 << 9,
        Powered = 1 << 10,
        Overheating = 1 << 11,
        Flooded = 1 << 12,
        Damaged = 1 << 13
    }

    [Flags]
    public enum LogisticsModuleStatusBits : byte
    {
        None = 0,
        Powered = 1 << 0,
        Overheating = 1 << 1,
        Flooded = 1 << 2,
        Damaged = 1 << 3,
        AnchorNode = 1 << 4,
        Anchored = 1 << 5,
        Unmoored = 1 << 6,
        EmergencyLockdown = 1 << 7
    }

    [Flags]
    public enum PowerGridNodeFlags : byte
    {
        None = 0,
        Powered = 1 << 0,
        Overloaded = 1 << 1,
        Damaged = 1 << 2,
        Offline = 1 << 3,
        Flooded = 1 << 4,
        Source = 1 << 5,
        Divergent = 1 << 6
    }

    /// <summary>
    /// Native-backed logistics graph kernel used by power and oxygen topologies.
    /// Runtime traversal reads CSR adjacency only: EdgeOffsets + EdgeDestinations + EdgeConductance.
    /// </summary>
    public sealed class LogisticsNetworkGraph : IDisposable
    {
        [StructLayout(LayoutKind.Explicit, Size = 32)]
        public struct LogisticsNode
        {
            [FieldOffset(0)]
            public uint Id;
            [FieldOffset(4)]
            public float Capacity;
            [FieldOffset(8)]
            public float Resistance;
            [FieldOffset(12)]
            public float CurrentLoad;
            [FieldOffset(16)]
            public float Potential;
            [FieldOffset(20)]
            public byte Priority;
            [FieldOffset(21)]
            public LogisticsNodeFlags Flags;
            [FieldOffset(22)]
            public byte NetworkId;
            [FieldOffset(23)]
            public byte Reserved;
            [FieldOffset(24)]
            private ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        public struct TopologySummary
        {
            [FieldOffset(0)]
            public int NodeCount;
            [FieldOffset(4)]
            public int EdgeCount;
            [FieldOffset(8)]
            public int IslandCount;
            [FieldOffset(12)]
            public int CycleCount;
            [FieldOffset(16)]
            public int BfsVisitedCount;
            [FieldOffset(20)]
            public int ProducerReachableCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = 40)]
        public struct DistributionSummary
        {
            [FieldOffset(0)]
            public float TotalGeneration;
            [FieldOffset(4)]
            public float TotalConsumption;
            [FieldOffset(8)]
            public float Balance;
            [FieldOffset(12)]
            public float SupplyRatio;
            [FieldOffset(16)]
            public float ServedDemand;
            [FieldOffset(20)]
            public float UnservedDemand;
            [FieldOffset(24)]
            public int PoweredCount;
            [FieldOffset(28)]
            public int DisabledCount;
            [FieldOffset(32)]
            public byte HasDeficit;
            [FieldOffset(33)]
            public LogisticsBrownoutTier BrownoutTier;
            [FieldOffset(34)]
            private ushort _pad0;
            [FieldOffset(36)]
            private uint _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct TopologyEdgeRecord
        {
            [FieldOffset(0)]
            public int SourceNodeIndex;
            [FieldOffset(4)]
            public int DestinationNodeIndex;
            [FieldOffset(8)]
            public float Resistance;
            [FieldOffset(12)]
            private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 8)]
        private struct ProducerRecord
        {
            [FieldOffset(0)]
            public int NodeIndex;
            [FieldOffset(4)]
            private uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ConsumerRecord
        {
            [FieldOffset(0)]
            public int NodeIndex;
            [FieldOffset(4)]
            public float Demand;
            [FieldOffset(8)]
            public int PowerPriority;
            [FieldOffset(12)]
            public LogisticsConsumerFlags Flags;
            [FieldOffset(14)]
            public byte PriorityTier;
            [FieldOffset(15)]
            private byte _pad0;
        }

        public static bool ValidateMemorySovereignLayouts(
            out int nodeBytes,
            out int topologySummaryBytes,
            out int distributionSummaryBytes)
        {
            nodeBytes = UnsafeUtility.SizeOf<LogisticsNode>();
            topologySummaryBytes = UnsafeUtility.SizeOf<TopologySummary>();
            distributionSummaryBytes = UnsafeUtility.SizeOf<DistributionSummary>();

            return nodeBytes == 32 &&
                   topologySummaryBytes == 24 &&
                   distributionSummaryBytes == 40 &&
                   UnsafeUtility.SizeOf<TopologyEdgeRecord>() == 16 &&
                   UnsafeUtility.SizeOf<ProducerRecord>() == 8 &&
                   UnsafeUtility.SizeOf<ConsumerRecord>() == 16 &&
                   ValidateLogisticsNodeLayout() &&
                   ValidateTopologySummaryLayout() &&
                   ValidateDistributionSummaryLayout() &&
                   OffsetOf<TopologyEdgeRecord>(nameof(TopologyEdgeRecord.SourceNodeIndex)) == 0 &&
                   OffsetOf<TopologyEdgeRecord>(nameof(TopologyEdgeRecord.DestinationNodeIndex)) == 4 &&
                   OffsetOf<TopologyEdgeRecord>(nameof(TopologyEdgeRecord.Resistance)) == 8 &&
                   OffsetOf<ProducerRecord>(nameof(ProducerRecord.NodeIndex)) == 0 &&
                   OffsetOf<ConsumerRecord>(nameof(ConsumerRecord.NodeIndex)) == 0 &&
                   OffsetOf<ConsumerRecord>(nameof(ConsumerRecord.Demand)) == 4 &&
                   OffsetOf<ConsumerRecord>(nameof(ConsumerRecord.PowerPriority)) == 8 &&
                   OffsetOf<ConsumerRecord>(nameof(ConsumerRecord.Flags)) == 12 &&
                   OffsetOf<ConsumerRecord>(nameof(ConsumerRecord.PriorityTier)) == 14;
        }

        private static bool ValidateLogisticsNodeLayout()
        {
            return OffsetOf<LogisticsNode>(nameof(LogisticsNode.Id)) == 0 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.Capacity)) == 4 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.Resistance)) == 8 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.CurrentLoad)) == 12 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.Potential)) == 16 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.Priority)) == 20 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.Flags)) == 21 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.NetworkId)) == 22 &&
                   OffsetOf<LogisticsNode>(nameof(LogisticsNode.Reserved)) == 23;
        }

        private static bool ValidateTopologySummaryLayout()
        {
            return OffsetOf<TopologySummary>(nameof(TopologySummary.NodeCount)) == 0 &&
                   OffsetOf<TopologySummary>(nameof(TopologySummary.EdgeCount)) == 4 &&
                   OffsetOf<TopologySummary>(nameof(TopologySummary.IslandCount)) == 8 &&
                   OffsetOf<TopologySummary>(nameof(TopologySummary.CycleCount)) == 12 &&
                   OffsetOf<TopologySummary>(nameof(TopologySummary.BfsVisitedCount)) == 16 &&
                   OffsetOf<TopologySummary>(nameof(TopologySummary.ProducerReachableCount)) == 20;
        }

        private static bool ValidateDistributionSummaryLayout()
        {
            return OffsetOf<DistributionSummary>(nameof(DistributionSummary.TotalGeneration)) == 0 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.TotalConsumption)) == 4 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.Balance)) == 8 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.SupplyRatio)) == 12 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.ServedDemand)) == 16 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.UnservedDemand)) == 20 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.PoweredCount)) == 24 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.DisabledCount)) == 28 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.HasDeficit)) == 32 &&
                   OffsetOf<DistributionSummary>(nameof(DistributionSummary.BrownoutTier)) == 33;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ValidateMemorySovereignLayoutsOnEditorLoad()
        {
            if (!ValidateMemorySovereignLayouts(out int nodeBytes, out int topologyBytes, out int distributionBytes))
            {
                Debug.LogError("LogisticsNetworkGraph layout fault.");
            }
        }
#endif

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        public struct TwoPassPowerGridSolverJob : IJob
        {
            public const int FixedPropagationPassCount = 2;
            public const float BrownoutPotentialThreshold = 0.2f;
            public const float FloodedShortCircuitPotentialThreshold = 0.5f;

            public int NodeCount;
            public int BaseAwakeIndex;
            public byte BaseAwakeStateValue;
            public float GlobalQualityWeight;

            [ReadOnly, NoAlias] public NativeArray<int> EdgeOffsets;
            [ReadOnly, NoAlias] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<float> PowerCapacities;

            public NativeArray<float> PowerPotentials;
            public NativeArray<float> NextPowerPotentials;
            public NativeArray<byte> NodeFlags;

            public void Execute()
            {
                if (IsBoundBaseHibernating())
                    return;

                if (NodeCount <= 0 ||
                    !EdgeOffsets.IsCreated ||
                    !EdgeDestinations.IsCreated ||
                    !PowerCapacities.IsCreated ||
                    !PowerPotentials.IsCreated ||
                    !NextPowerPotentials.IsCreated ||
                    !NodeFlags.IsCreated)
                {
                    return;
                }

                int safeNodeCount = math.min(NodeCount, math.min(PowerPotentials.Length, math.min(NextPowerPotentials.Length, math.min(PowerCapacities.Length, NodeFlags.Length))));
                if (safeNodeCount <= 0)
                    return;

                if (!HasAnySource(safeNodeCount))
                {
                    ClearPowerState(safeNodeCount);
                    return;
                }

                SeedSourcePotentials(safeNodeCount);
                float q = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
                PropagateNeighborDelta(safeNodeCount, PowerPotentials, NextPowerPotentials, math.lerp(0.70f, 0.94f, q));
                PropagateNeighborDelta(safeNodeCount, NextPowerPotentials, PowerPotentials, math.lerp(0.35f, 0.68f, q));

                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                {
                    float resolvedPotential = ClampPotential(PowerPotentials[nodeIndex], PowerCapacities[nodeIndex]);
                    byte flags = NodeFlags[nodeIndex];
                    if (resolvedPotential < BrownoutPotentialThreshold)
                        flags = (byte)((flags & ~(byte)PowerGridNodeFlags.Powered) | (byte)PowerGridNodeFlags.Offline);
                    else
                        flags = (byte)((flags | (byte)PowerGridNodeFlags.Powered) & ~(byte)PowerGridNodeFlags.Offline);

                    if ((flags & (byte)PowerGridNodeFlags.Flooded) != 0 &&
                        resolvedPotential > FloodedShortCircuitPotentialThreshold)
                    {
                        flags |= (byte)PowerGridNodeFlags.Damaged;
                    }

                    PowerPotentials[nodeIndex] = resolvedPotential;
                    NextPowerPotentials[nodeIndex] = resolvedPotential;
                    NodeFlags[nodeIndex] = flags;
                }
            }

            private static float ClampPotential(float potential, float capacity)
            {
                float safePotential = math.isfinite(potential) ? potential : 0f;
                if (!math.isfinite(capacity) || capacity <= 0f)
                    return 0f;

                return math.saturate(safePotential);
            }

            private bool HasAnySource(int safeNodeCount)
            {
                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                {
                    byte flags = NodeFlags[nodeIndex];
                    if ((flags & (byte)PowerGridNodeFlags.Source) != 0 &&
                        (flags & (byte)(PowerGridNodeFlags.Offline | PowerGridNodeFlags.Damaged)) == 0 &&
                        ClampPotential(1f, PowerCapacities[nodeIndex]) > 0f)
                    {
                        return true;
                    }
                }

                return false;
            }

            private void ClearPowerState(int safeNodeCount)
            {
                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                {
                    byte flags = NodeFlags[nodeIndex];
                    flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                    PowerPotentials[nodeIndex] = 0f;
                    NextPowerPotentials[nodeIndex] = 0f;
                    NodeFlags[nodeIndex] = flags;
                }
            }

            private void SeedSourcePotentials(int safeNodeCount)
            {
                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                {
                    byte flags = NodeFlags[nodeIndex];
                    if ((flags & (byte)(PowerGridNodeFlags.Offline | PowerGridNodeFlags.Damaged)) != 0)
                    {
                        PowerPotentials[nodeIndex] = 0f;
                        NextPowerPotentials[nodeIndex] = 0f;
                        continue;
                    }

                    float potential = (flags & (byte)PowerGridNodeFlags.Source) != 0
                        ? ClampPotential(1f, PowerCapacities[nodeIndex])
                        : ClampPotential(PowerPotentials[nodeIndex], PowerCapacities[nodeIndex]);
                    PowerPotentials[nodeIndex] = potential;
                    NextPowerPotentials[nodeIndex] = potential;
                }
            }

            private void PropagateNeighborDelta(
                int safeNodeCount,
                NativeArray<float> input,
                NativeArray<float> output,
                float propagationGain)
            {
                float gain = math.saturate(math.isfinite(propagationGain) ? propagationGain : 1f);
                for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                {
                    byte flags = NodeFlags[nodeIndex];
                    if ((flags & (byte)(PowerGridNodeFlags.Offline | PowerGridNodeFlags.Damaged)) != 0)
                    {
                        output[nodeIndex] = 0f;
                        continue;
                    }

                    if ((flags & (byte)PowerGridNodeFlags.Source) != 0)
                    {
                        output[nodeIndex] = ClampPotential(1f, PowerCapacities[nodeIndex]);
                        continue;
                    }

                    float weightedPotential = 0f;
                    float conductanceSum = 0f;
                    int edgeStart = nodeIndex + 1 < EdgeOffsets.Length ? EdgeOffsets[nodeIndex] : 0;
                    int edgeEnd = nodeIndex + 1 < EdgeOffsets.Length ? EdgeOffsets[nodeIndex + 1] : edgeStart;
                    edgeEnd = math.min(edgeEnd, EdgeDestinations.Length);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborIndex = EdgeDestinations[edgeIndex];
                        if ((uint)neighborIndex >= (uint)safeNodeCount)
                            continue;

                        byte neighborFlags = NodeFlags[neighborIndex];
                        if ((neighborFlags & (byte)(PowerGridNodeFlags.Offline | PowerGridNodeFlags.Damaged)) != 0)
                            continue;

                        float neighborCapacity = math.max(0f, PowerCapacities[neighborIndex]);
                        float conductance = neighborCapacity > 0f ? math.rcp(1f + neighborCapacity) + 1f : 1f;
                        weightedPotential += conductance * ClampPotential(input[neighborIndex], neighborCapacity);
                        conductanceSum += conductance;
                    }

                    float currentPotential = ClampPotential(input[nodeIndex], PowerCapacities[nodeIndex]);
                    float targetPotential = conductanceSum > 0f
                        ? weightedPotential * math.rcp(conductanceSum)
                        : currentPotential;
                    float solvedPotential = currentPotential + (targetPotential - currentPotential) * gain;
                    if (!math.isfinite(solvedPotential))
                    {
                        solvedPotential = currentPotential;
                        flags |= (byte)PowerGridNodeFlags.Divergent;
                        NodeFlags[nodeIndex] = flags;
                    }

                    output[nodeIndex] = ClampPotential(solvedPotential, PowerCapacities[nodeIndex]);
                }
            }

            private bool IsBoundBaseHibernating()
            {
                return BaseAwakeStateValue == 0;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct PublishNodeStatesJob : IJobParallelFor
        {
            private const float PublishEpsilon = 0.0001f;

            [ReadOnly] public NativeArray<LogisticsNode> Nodes;
            [ReadOnly] public NativeArray<float> NodeNetInjection;
            [ReadOnly] public NativeArray<float> NodeServedDemand;
            [ReadOnly] public NativeArray<float> NodeVoltageSupplyRatio;

            public NativeArray<ushort> PublishedNodeStates;

            public void Execute(int index)
            {
                LogisticsNode node = Nodes[index];
                LogisticsNodeStateBits stateBits = LogisticsNodeStateBits.None;

                if ((node.Flags & LogisticsNodeFlags.Active) != 0)
                    stateBits |= LogisticsNodeStateBits.Active;

                if ((node.Flags & LogisticsNodeFlags.Isolated) == 0)
                    stateBits |= LogisticsNodeStateBits.Reachable;

                if ((node.Flags & LogisticsNodeFlags.Brownout) != 0)
                    stateBits |= LogisticsNodeStateBits.Brownout;

                if (NodeServedDemand[index] > PublishEpsilon &&
                    NodeVoltageSupplyRatio[index] < 0.85f)
                {
                    stateBits |= LogisticsNodeStateBits.Brownout;
                }

                if ((node.Flags & LogisticsNodeFlags.Isolated) != 0)
                    stateBits |= LogisticsNodeStateBits.Isolated;

                if ((node.Flags & LogisticsNodeFlags.Overloaded) != 0)
                    stateBits |= LogisticsNodeStateBits.Overloaded;

                if ((node.Flags & LogisticsNodeFlags.Ruptured) != 0)
                    stateBits |= LogisticsNodeStateBits.Ruptured;

                if ((node.Flags & LogisticsNodeFlags.EmergencyReserved) != 0)
                    stateBits |= LogisticsNodeStateBits.EmergencyReserved;

                if (NodeNetInjection[index] > PublishEpsilon)
                    stateBits |= LogisticsNodeStateBits.HasGeneration;

                if (NodeServedDemand[index] > PublishEpsilon)
                    stateBits |= LogisticsNodeStateBits.HasServedDemand;

                if (node.Potential > PublishEpsilon)
                    stateBits |= LogisticsNodeStateBits.HasPotential;

                LogisticsModuleStatusBits moduleStatus = (LogisticsModuleStatusBits)node.Reserved;
                if ((moduleStatus & LogisticsModuleStatusBits.Powered) != 0)
                    stateBits |= LogisticsNodeStateBits.Powered;
                if ((moduleStatus & LogisticsModuleStatusBits.Overheating) != 0)
                    stateBits |= LogisticsNodeStateBits.Overheating;
                if ((moduleStatus & LogisticsModuleStatusBits.Flooded) != 0)
                    stateBits |= LogisticsNodeStateBits.Flooded;
                if ((moduleStatus & LogisticsModuleStatusBits.Damaged) != 0)
                    stateBits |= LogisticsNodeStateBits.Damaged;

                ushort bitmask = (ushort)stateBits;
                PublishedNodeStates[index] = bitmask;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateGraphJob : IJob
        {
            public LogisticsNetworkType NetworkType;
            public int NodeCount;
            public int EdgeCount;
            public int ConsumerCount;
            public int ProducerCount;
            public int SolveStartNode;
            public int SolveNodeCount;
            public int BaseAwakeIndex;
            public byte BaseAwakeStateValue;
            public byte RelaxationSliceOnly;
            public float GlobalQualityWeight;

            public NativeArray<LogisticsNode> Nodes;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<float> EdgeConductance;
            [ReadOnly] public NativeArray<float> EdgeCapacity;
            [ReadOnly] public NativeArray<ProducerRecord> Producers;
            [ReadOnly] public NativeArray<ConsumerRecord> Consumers;
            [ReadOnly] public NativeArray<float> ProducerRates;

            public NativeArray<int> Parents;
            public NativeArray<int> Ranks;
            public NativeArray<int> ComponentIds;
            public NativeArray<int> ComponentSizes;
            public NativeArray<int> RootToComponent;
            public NativeArray<int> TraversalQueue;
            public NativeArray<byte> Visited;
            public NativeArray<byte> ConsumerStates;
            public NativeArray<float> NodeNetInjection;
            public NativeArray<float> NodeServedDemand;
            public NativeArray<float> NodeVoltageSupplyRatio;
            public NativeArray<float> NodeSourcePotential;
            public NativeArray<float> NodeConductanceSum;
            public NativeArray<float> NodeConductanceInverseSum;
            public NativeArray<float> PotentialFront;
            public NativeArray<float> PotentialBack;
            public NativeArray<float> PowerCapacities;
            public NativeArray<byte> PowerNodeFlags;
            public NativeArray<float> EdgeFlow;
            public NativeArray<byte> EdgeStates;
            public NativeArray<int> RuntimeConductiveEdgeCount;
            public NativeArray<float> ComponentGeneration;
            public NativeArray<float> ComponentDemand;
            public NativeArray<float> ComponentServedDemand;
            public NativeArray<float> ComponentRemainingSupply;
            public NativeArray<float> ComponentSupplyRatio;
            public NativeArray<float> ComponentResidualInjection;
            public NativeArray<int> ComponentAnchorNode;
            public NativeArray<byte> ComponentBrownoutTier;
            public NativeArray<TopologySummary> TopologySummaryBuffer;
            public NativeArray<DistributionSummary> DistributionSummaryBuffer;

            public void Execute()
            {
                if (IsBoundBaseHibernating())
                {
                    CommitHibernatingEvaluation();
                    return;
                }

                if (RelaxationSliceOnly != 0)
                {
                    int topologyCycleCount = TopologySummaryBuffer.IsCreated && TopologySummaryBuffer.Length > 0
                        ? TopologySummaryBuffer[0].CycleCount
                        : 0;
                    ApplyTwoPassPowerDeltaPropagation(topologyCycleCount);
                    return;
                }

                TopologySummary topology = new TopologySummary
                {
                    NodeCount = NodeCount,
                    EdgeCount = EdgeCount
                };

                DistributionSummary distribution = new DistributionSummary
                {
                    SupplyRatio = 1f,
                    BrownoutTier = LogisticsBrownoutTier.None
                };

                if (NodeCount > 0)
                {
                    ResetTopologyScratch();
                    ClearNodeFlags(LogisticsNodeFlags.Isolated | LogisticsNodeFlags.BridgeNode);

                    int cycleCount = 0;
                    for (int sourceNodeIndex = 0; sourceNodeIndex < NodeCount; sourceNodeIndex++)
                    {
                        int edgeStart = EdgeOffsets[sourceNodeIndex];
                        int edgeEnd = EdgeOffsets[sourceNodeIndex + 1];
                        for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                        {
                            if (IsEdgeRuptured(edgeIndex))
                                continue;

                            int destinationNodeIndex = EdgeDestinations[edgeIndex];
                            if (!IsValidNodeIndex(destinationNodeIndex) || destinationNodeIndex < sourceNodeIndex)
                                continue;

                            int sourceRoot = FindRoot(sourceNodeIndex);
                            int destinationRoot = FindRoot(destinationNodeIndex);
                            if (sourceRoot == destinationRoot)
                            {
                                cycleCount++;
                                continue;
                            }

                            UnionRoots(sourceRoot, destinationRoot);
                        }
                    }

                    int islandCount = 0;
                    for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                    {
                        int rootIndex = FindRoot(nodeIndex);
                        Parents[nodeIndex] = rootIndex;

                        int componentIndex = RootToComponent[rootIndex];
                        if (componentIndex < 0)
                        {
                            componentIndex = islandCount;
                            RootToComponent[rootIndex] = componentIndex;
                            ComponentSizes[componentIndex] = 0;
                            islandCount++;
                        }

                        ComponentIds[nodeIndex] = componentIndex;
                        ComponentSizes[componentIndex] = ComponentSizes[componentIndex] + 1;

                        LogisticsNode node = Nodes[nodeIndex];
                        node.NetworkId = (byte)math.clamp(componentIndex, 0, byte.MaxValue);
                        Nodes[nodeIndex] = node;
                    }

                    topology.IslandCount = islandCount;
                    topology.CycleCount = cycleCount;
                    topology.BfsVisitedCount = TraverseReachableFrom(0);
                    topology.ProducerReachableCount = TraverseProducerReachability();
                    MarkIsolatedNodesFromVisited();

                    distribution = EvaluateDistribution(topology.CycleCount);
                }

                TopologySummaryBuffer[0] = topology;
                DistributionSummaryBuffer[0] = distribution;
            }

            private void CommitHibernatingEvaluation()
            {
                ClearEdgeFlows();
                if (TopologySummaryBuffer.IsCreated && TopologySummaryBuffer.Length > 0)
                {
                    TopologySummaryBuffer[0] = new TopologySummary
                    {
                        NodeCount = NodeCount,
                        EdgeCount = EdgeCount
                    };
                }

                if (DistributionSummaryBuffer.IsCreated && DistributionSummaryBuffer.Length > 0)
                {
                    DistributionSummaryBuffer[0] = new DistributionSummary
                    {
                        SupplyRatio = 1f,
                        BrownoutTier = LogisticsBrownoutTier.None
                    };
                }
            }

            private DistributionSummary EvaluateDistribution(int topologyCycleCount)
            {
                DistributionSummary summary = new DistributionSummary
                {
                    TotalGeneration = ComputeTotalGeneration(),
                    TotalConsumption = ComputeTotalConsumption()
                };

                summary.Balance = summary.TotalGeneration - summary.TotalConsumption;
                summary.SupplyRatio = summary.TotalConsumption > Epsilon
                    ? math.saturate(summary.TotalGeneration * math.rcp(summary.TotalConsumption))
                    : 1f;
                summary.HasDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption ? (byte)1 : (byte)0;
                summary.BrownoutTier = ResolveBrownoutTier(summary.SupplyRatio);

                ResetConsumerStates();
                ResetDistributionState();
                BuildComponentDistributionState();
                AllocateServedDemand(ref summary);
                BuildNodeInjection();
                summary.UnservedDemand = math.max(0f, summary.TotalConsumption - summary.ServedDemand);
                summary.HasDeficit = summary.UnservedDemand > Epsilon ? (byte)1 : (byte)0;
                summary.BrownoutTier = summary.HasDeficit != 0
                    ? LogisticsBrownoutTier.EmergencyOnly
                    : LogisticsBrownoutTier.None;
                ApplyBinaryNodeLoads();
                ApplyTwoPassPowerDeltaPropagation(topologyCycleCount);
                return summary;
            }

            private float ComputeTotalGeneration()
            {
                float totalGeneration = 0f;
                int producerCount = math.min(ProducerCount, Producers.IsCreated ? Producers.Length : 0);
                for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
                {
                    int nodeIndex = Producers[producerIndex].NodeIndex;
                    if (TryReadProducerRate(nodeIndex, out float productionRate))
                        totalGeneration += productionRate;
                }

                return totalGeneration;
            }

            private float ComputeTotalConsumption()
            {
                float totalConsumption = 0f;
                for (int consumerIndex = 0; consumerIndex < ConsumerCount; consumerIndex++)
                    totalConsumption += Consumers[consumerIndex].Demand;

                return totalConsumption;
            }

            private void ResetTopologyScratch()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    Parents[nodeIndex] = nodeIndex;
                    Ranks[nodeIndex] = 0;
                    ComponentIds[nodeIndex] = -1;
                    ComponentSizes[nodeIndex] = 0;
                    RootToComponent[nodeIndex] = -1;
                    Visited[nodeIndex] = 0;
                }
            }

            private void ResetConsumerStates()
            {
                for (int consumerIndex = 0; consumerIndex < ConsumerCount; consumerIndex++)
                    ConsumerStates[consumerIndex] = 0;
            }

            private void ResetDistributionState()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    node.CurrentLoad = 0f;
                    node.Potential = 0f;
                    node.Flags &= ~(LogisticsNodeFlags.Brownout | LogisticsNodeFlags.Overloaded | LogisticsNodeFlags.Dirty);
                    Nodes[nodeIndex] = node;

                    if (PowerCapacities.IsCreated && nodeIndex < PowerCapacities.Length)
                        PowerCapacities[nodeIndex] = math.max(Epsilon, node.Capacity);
                    if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                        PowerNodeFlags[nodeIndex] = ResolvePowerGridNodeFlags(node.Flags, node.Reserved);

                    NodeNetInjection[nodeIndex] = 0f;
                    NodeServedDemand[nodeIndex] = 0f;
                    NodeVoltageSupplyRatio[nodeIndex] = 1f;
                    NodeSourcePotential[nodeIndex] = 0f;
                    ComponentGeneration[nodeIndex] = 0f;
                    ComponentDemand[nodeIndex] = 0f;
                    ComponentServedDemand[nodeIndex] = 0f;
                    ComponentRemainingSupply[nodeIndex] = 0f;
                    ComponentSupplyRatio[nodeIndex] = 1f;
                    ComponentResidualInjection[nodeIndex] = 0f;
                    ComponentAnchorNode[nodeIndex] = -1;
                    ComponentBrownoutTier[nodeIndex] = (byte)LogisticsBrownoutTier.None;
                }
            }

            private void BuildComponentDistributionState()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    int componentIndex = ComponentIds[nodeIndex];
                    if (componentIndex < 0 || componentIndex >= NodeCount)
                        continue;

                    if (ComponentAnchorNode[componentIndex] < 0)
                        ComponentAnchorNode[componentIndex] = nodeIndex;

                    if (TryReadProducerRate(nodeIndex, out float productionRate))
                    {
                        ComponentGeneration[componentIndex] += productionRate;
                        NodeSourcePotential[nodeIndex] = 1f;
                        if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                            PowerNodeFlags[nodeIndex] = (byte)(PowerNodeFlags[nodeIndex] | (byte)(PowerGridNodeFlags.Source | PowerGridNodeFlags.Powered));
                    }
                }

                for (int consumerIndex = 0; consumerIndex < ConsumerCount; consumerIndex++)
                {
                    ConsumerRecord consumer = Consumers[consumerIndex];
                    int componentIndex = ComponentIds[consumer.NodeIndex];
                    if (componentIndex < 0 || componentIndex >= NodeCount)
                        continue;

                    ComponentDemand[componentIndex] += consumer.Demand;
                }

                for (int componentIndex = 0; componentIndex < NodeCount; componentIndex++)
                {
                    if (ComponentAnchorNode[componentIndex] < 0)
                        continue;

                    float demand = ComponentDemand[componentIndex];
                    float generation = ComponentGeneration[componentIndex];
                    float supplyRatio = demand > Epsilon ? math.saturate(generation * math.rcp(demand)) : 1f;
                    ComponentRemainingSupply[componentIndex] = generation;
                    ComponentSupplyRatio[componentIndex] = supplyRatio;
                    ComponentBrownoutTier[componentIndex] = (byte)ResolveBrownoutTier(supplyRatio);
                }
            }

            private void AllocateServedDemand(ref DistributionSummary summary)
            {
                if (ConsumerCount <= 0)
                    return;

                int poweredCount = 0;
                float servedDemand = 0f;
                bool globalDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption;

                for (int consumerIndex = 0; consumerIndex < ConsumerCount; consumerIndex++)
                {
                    ConsumerRecord consumer = Consumers[consumerIndex];
                    int nodeIndex = consumer.NodeIndex;
                    if (!IsValidNodeIndex(nodeIndex))
                    {
                        continue;
                    }

                    int componentIndex = ComponentIds[nodeIndex];
                    bool componentCanServe =
                        !globalDeficit &&
                        componentIndex >= 0 &&
                        componentIndex < NodeCount &&
                        ComponentGeneration[componentIndex] > Epsilon &&
                        ComponentGeneration[componentIndex] + Epsilon >= ComponentDemand[componentIndex] &&
                        CanServeConsumer(in consumer);

                    if (!componentCanServe)
                    {
                        MarkBrownoutNode(nodeIndex);
                        continue;
                    }

                    ComponentRemainingSupply[componentIndex] = math.max(0f, ComponentRemainingSupply[componentIndex] - consumer.Demand);
                    ComponentServedDemand[componentIndex] += consumer.Demand;
                    NodeServedDemand[nodeIndex] += consumer.Demand;
                    NodeVoltageSupplyRatio[nodeIndex] = 1f;
                    ConsumerStates[consumerIndex] = 1;
                    poweredCount++;
                    servedDemand += consumer.Demand;
                }

                summary.PoweredCount = poweredCount;
                summary.DisabledCount = ConsumerCount - poweredCount;
                summary.ServedDemand = servedDemand;
            }

            private void BuildNodeInjection()
            {
                int producerCount = math.min(ProducerCount, Producers.IsCreated ? Producers.Length : 0);
                for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
                {
                    int nodeIndex = Producers[producerIndex].NodeIndex;
                    int componentIndex = ComponentIds[nodeIndex];
                    if (componentIndex < 0 || componentIndex >= NodeCount)
                        continue;

                    if (!TryReadProducerRate(nodeIndex, out float productionRate))
                        continue;

                    float componentGeneration = ComponentGeneration[componentIndex];
                    float componentServedDemand = ComponentServedDemand[componentIndex];
                    if (componentGeneration <= Epsilon || componentServedDemand <= Epsilon)
                        continue;

                    float dispatchedGeneration = productionRate * math.saturate(componentServedDemand * math.rcp(componentGeneration));
                    NodeNetInjection[nodeIndex] += dispatchedGeneration;
                }

                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    NodeNetInjection[nodeIndex] -= NodeServedDemand[nodeIndex];
                    int componentIndex = ComponentIds[nodeIndex];
                    if (componentIndex < 0 || componentIndex >= NodeCount)
                        continue;

                    ComponentResidualInjection[componentIndex] += NodeNetInjection[nodeIndex];
                }

                for (int componentIndex = 0; componentIndex < NodeCount; componentIndex++)
                {
                    int anchorNodeIndex = ComponentAnchorNode[componentIndex];
                    if (!IsValidNodeIndex(anchorNodeIndex))
                        continue;

                    float residual = ComponentResidualInjection[componentIndex];
                    if (math.abs(residual) > Epsilon)
                        NodeNetInjection[anchorNodeIndex] -= residual;
                }
            }

            private void ApplyBinaryNodeLoads()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    int componentIndex = ComponentIds[nodeIndex];
                    bool componentPowered =
                        componentIndex >= 0 &&
                        componentIndex < NodeCount &&
                        ComponentGeneration[componentIndex] > Epsilon &&
                        ComponentGeneration[componentIndex] + Epsilon >= ComponentDemand[componentIndex];

                    if (!componentPowered && NodeServedDemand[nodeIndex] > Epsilon)
                        node.Flags |= LogisticsNodeFlags.Brownout;

                    float generatedWatts = math.max(0f, NodeNetInjection[nodeIndex]);
                    float consumedWatts = NodeServedDemand[nodeIndex];
                    node.Potential = componentPowered ? 1f : 0f;
                    node.CurrentLoad = math.max(generatedWatts, consumedWatts);
                    NodeVoltageSupplyRatio[nodeIndex] = componentPowered ? 1f : 0f;

                    if (node.CurrentLoad > node.Capacity * 1.15f)
                        node.Flags |= LogisticsNodeFlags.Overloaded;

                    Nodes[nodeIndex] = node;
                    if (PowerCapacities.IsCreated && nodeIndex < PowerCapacities.Length)
                        PowerCapacities[nodeIndex] = math.max(Epsilon, node.Capacity);
                    if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                    {
                        byte flags = ResolvePowerGridNodeFlags(node.Flags, node.Reserved);
                        if (componentPowered)
                            flags = (byte)((flags | (byte)PowerGridNodeFlags.Powered) & ~(byte)PowerGridNodeFlags.Offline);
                        else
                            flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                        if (NodeSourcePotential[nodeIndex] > Epsilon)
                            flags |= (byte)PowerGridNodeFlags.Source;
                        PowerNodeFlags[nodeIndex] = flags;
                    }
                }
            }

            private void ApplyTwoPassPowerDeltaPropagation(int topologyCycleCount)
            {
                if (NetworkType != LogisticsNetworkType.PowerDc ||
                    !PotentialFront.IsCreated ||
                    !PotentialBack.IsCreated ||
                    !EdgeFlow.IsCreated ||
                    !EdgeStates.IsCreated ||
                    !EdgeConductance.IsCreated ||
                    !EdgeCapacity.IsCreated ||
                    !NodeSourcePotential.IsCreated ||
                    !NodeConductanceSum.IsCreated ||
                    !NodeConductanceInverseSum.IsCreated ||
                    !RuntimeConductiveEdgeCount.IsCreated ||
                    EdgeCount <= 0 ||
                    RuntimeConductiveEdgeCount[0] <= 0)
                {
                    ClearEdgeFlows();
                    return;
                }

                if (!HasAnyPoweredComponent())
                {
                    CommitUnpoweredPowerState();
                    return;
                }

                bool initializePotentialBuffers = RelaxationSliceOnly == 0;
                if (initializePotentialBuffers)
                {
                    for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                    {
                        float sourcePotential = NodeSourcePotential[nodeIndex];
                        float initialPotential = sourcePotential > Epsilon
                            ? 1f
                            : math.saturate(Nodes[nodeIndex].Potential);
                        PotentialFront[nodeIndex] = initialPotential;
                        PotentialBack[nodeIndex] = initialPotential;
                    }
                }

                ResolveSolveWindow(out int solveStartNode, out int solveEndNode);
                if (solveEndNode <= solveStartNode)
                    return;

                float quality = math.saturate(math.isfinite(GlobalQualityWeight) ? GlobalQualityWeight : 1f);
                float distributionGain = math.lerp(0.72f, 0.96f, quality);
                float equalizationGain = topologyCycleCount > 0
                    ? math.lerp(0.42f, 0.74f, quality)
                    : math.lerp(0.28f, 0.55f, quality);
                ApplyPowerDeltaPass(solveStartNode, solveEndNode, PotentialFront, PotentialBack, distributionGain);
                ApplyPowerDeltaPass(solveStartNode, solveEndNode, PotentialBack, PotentialFront, equalizationGain);

                for (int nodeIndex = solveStartNode; nodeIndex < solveEndNode; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    float resolvedPotential = math.isfinite(PotentialFront[nodeIndex]) ? math.saturate(PotentialFront[nodeIndex]) : 0f;
                    if (NodeSourcePotential[nodeIndex] > Epsilon &&
                        (node.Flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Ruptured)) == 0)
                    {
                        resolvedPotential = 1f;
                    }

                    node.Potential = resolvedPotential;
                    node.CurrentLoad = math.max(node.CurrentLoad, math.abs(NodeNetInjection[nodeIndex]));
                    if (resolvedPotential < TwoPassPowerGridSolverJob.BrownoutPotentialThreshold)
                        node.Flags |= LogisticsNodeFlags.Brownout;
                    Nodes[nodeIndex] = node;
                    PotentialFront[nodeIndex] = resolvedPotential;
                    PotentialBack[nodeIndex] = resolvedPotential;
                    NodeVoltageSupplyRatio[nodeIndex] = resolvedPotential;
                    if (PowerCapacities.IsCreated && nodeIndex < PowerCapacities.Length)
                        PowerCapacities[nodeIndex] = math.max(Epsilon, node.Capacity);
                    if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                    {
                        byte flags = PowerNodeFlags[nodeIndex];
                        if (resolvedPotential < TwoPassPowerGridSolverJob.BrownoutPotentialThreshold)
                            flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                        else
                            flags = (byte)((flags | (byte)PowerGridNodeFlags.Powered) & ~(byte)PowerGridNodeFlags.Offline);

                        if ((flags & (byte)PowerGridNodeFlags.Flooded) != 0 &&
                            resolvedPotential > TwoPassPowerGridSolverJob.FloodedShortCircuitPotentialThreshold)
                        {
                            flags |= (byte)PowerGridNodeFlags.Damaged;
                        }

                        PowerNodeFlags[nodeIndex] = flags;
                    }
                }

                for (int sourceNodeIndex = solveStartNode; sourceNodeIndex < solveEndNode; sourceNodeIndex++)
                {
                    int edgeStart = EdgeOffsets[sourceNodeIndex];
                    int edgeEnd = EdgeOffsets[sourceNodeIndex + 1];
                    float sourceLoadWatts = math.abs(NodeNetInjection[sourceNodeIndex]);
                    float sourceConductanceSum = math.max(0f, NodeConductanceSum[sourceNodeIndex]);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(destinationNodeIndex))
                        {
                            EdgeFlow[edgeIndex] = 0f;
                            continue;
                        }

                        byte edgeState = EdgeStates[edgeIndex];
                        if ((edgeState & (byte)LogisticsEdgeState.Ruptured) != 0)
                        {
                            EdgeFlow[edgeIndex] = 0f;
                            continue;
                        }

                        float edgeConductance = math.max(0f, EdgeConductance[edgeIndex]);
                        float flow = (PotentialFront[sourceNodeIndex] - PotentialFront[destinationNodeIndex]) * edgeConductance;
                        float sourceEdgeLoadWatts = sourceConductanceSum > Epsilon
                            ? sourceLoadWatts * math.saturate(edgeConductance * math.rcp(sourceConductanceSum))
                            : sourceLoadWatts;
                        float destinationConductanceSum = math.max(0f, NodeConductanceSum[destinationNodeIndex]);
                        float destinationLoadWatts = math.abs(NodeNetInjection[destinationNodeIndex]);
                        float destinationEdgeLoadWatts = destinationConductanceSum > Epsilon
                            ? destinationLoadWatts * math.saturate(edgeConductance * math.rcp(destinationConductanceSum))
                            : destinationLoadWatts;
                        float edgeLoadWatts = math.max(sourceEdgeLoadWatts, destinationEdgeLoadWatts);
                        float edgeCapacity = math.max(Epsilon, EdgeCapacity[edgeIndex]);

                        if (edgeLoadWatts > edgeCapacity * RuptureFlowMultiplier)
                        {
                            edgeState |= (byte)(LogisticsEdgeState.Overloaded | LogisticsEdgeState.Ruptured);
                            flow = 0f;
                            edgeLoadWatts = 0f;
                            RemoveRupturedEdgeFromConductance(sourceNodeIndex, edgeConductance);
                            sourceConductanceSum = math.max(0f, sourceConductanceSum - edgeConductance);
                            DecrementRuntimeConductiveEdgeCount();
                            MarkNodeOverloaded(sourceNodeIndex);
                            MarkNodeOverloaded(destinationNodeIndex);
                        }
                        else if (edgeLoadWatts > edgeCapacity)
                        {
                            edgeState |= (byte)LogisticsEdgeState.Overloaded;
                            MarkNodeOverloaded(sourceNodeIndex);
                            MarkNodeOverloaded(destinationNodeIndex);
                        }
                        else
                        {
                            edgeState = (byte)(edgeState & ~(byte)LogisticsEdgeState.Overloaded);
                        }

                        EdgeStates[edgeIndex] = edgeState;
                        EdgeFlow[edgeIndex] = flow;
                        AccumulateNodeLoad(sourceNodeIndex, edgeLoadWatts);
                        AccumulateNodeLoad(destinationNodeIndex, edgeLoadWatts);
                    }
                }
            }

            private bool HasAnyPoweredComponent()
            {
                for (int componentIndex = 0; componentIndex < NodeCount; componentIndex++)
                {
                    if (ComponentAnchorNode[componentIndex] >= 0 &&
                        ComponentGeneration[componentIndex] > Epsilon)
                    {
                        return true;
                    }
                }

                return false;
            }

            private void CommitUnpoweredPowerState()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    node.Potential = 0f;
                    node.CurrentLoad = math.max(0f, NodeServedDemand[nodeIndex]);
                    if (NodeServedDemand[nodeIndex] > Epsilon)
                        node.Flags |= LogisticsNodeFlags.Brownout;

                    Nodes[nodeIndex] = node;
                    PotentialFront[nodeIndex] = 0f;
                    PotentialBack[nodeIndex] = 0f;
                    NodeVoltageSupplyRatio[nodeIndex] = 0f;
                    if (PowerCapacities.IsCreated && nodeIndex < PowerCapacities.Length)
                        PowerCapacities[nodeIndex] = math.max(Epsilon, node.Capacity);
                    if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                    {
                        byte flags = ResolvePowerGridNodeFlags(node.Flags, node.Reserved);
                        flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                        PowerNodeFlags[nodeIndex] = flags;
                    }
                }

                ClearEdgeFlows();
            }

            private void ApplyPowerDeltaPass(
                int solveStartNode,
                int solveEndNode,
                NativeArray<float> input,
                NativeArray<float> output,
                float propagationGain)
            {
                float gain = math.saturate(math.isfinite(propagationGain) ? propagationGain : 1f);
                for (int nodeIndex = solveStartNode; nodeIndex < solveEndNode; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    if ((node.Flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Ruptured)) != 0)
                    {
                        output[nodeIndex] = 0f;
                        continue;
                    }

                    if (NodeSourcePotential[nodeIndex] > Epsilon)
                    {
                        output[nodeIndex] = 1f;
                        continue;
                    }

                    float weightedPotential = 0f;
                    float conductanceSum = 0f;
                    int edgeStart = EdgeOffsets[nodeIndex];
                    int edgeEnd = EdgeOffsets[nodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        if ((EdgeStates[edgeIndex] & (byte)LogisticsEdgeState.Ruptured) != 0)
                            continue;

                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if ((uint)destinationNodeIndex >= (uint)NodeCount)
                            continue;

                        LogisticsNode destinationNode = Nodes[destinationNodeIndex];
                        if ((destinationNode.Flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Ruptured)) != 0)
                            continue;

                        float conductance = math.max(0f, EdgeConductance[edgeIndex]);
                        if (conductance <= Epsilon)
                            continue;

                        weightedPotential += conductance * math.saturate(input[destinationNodeIndex]);
                        conductanceSum += conductance;
                    }

                    float currentPotential = math.saturate(input[nodeIndex]);
                    float nodeCapacity = math.max(Epsilon, math.isfinite(node.Capacity) ? node.Capacity : 0f);
                    float injectionBias = math.clamp(
                        (math.isfinite(NodeNetInjection[nodeIndex]) ? NodeNetInjection[nodeIndex] : 0f) * math.rcp(nodeCapacity),
                        -1f,
                        1f);
                    float targetPotential = conductanceSum > Epsilon
                        ? (weightedPotential + injectionBias) * math.rcp(conductanceSum + 1f)
                        : currentPotential;
                    float nextPotential = currentPotential + (targetPotential - currentPotential) * gain;
                    if (!math.isfinite(nextPotential) || math.abs(nextPotential) > 16f)
                    {
                        nextPotential = currentPotential;
                        if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                            PowerNodeFlags[nodeIndex] = (byte)(PowerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Divergent);
                    }

                    output[nodeIndex] = math.saturate(nextPotential);
                }
            }

            private void ResolveSolveWindow(out int solveStartNode, out int solveEndNode)
            {
                solveStartNode = math.clamp(SolveStartNode, 0, math.max(0, NodeCount));
                int requestedNodeCount = SolveNodeCount > 0
                    ? SolveNodeCount
                    : NodeCount;
                requestedNodeCount = math.min(requestedNodeCount, NodeCount);
                solveEndNode = math.min(NodeCount, solveStartNode + requestedNodeCount);
                if (solveEndNode <= solveStartNode && NodeCount > 0)
                {
                    solveStartNode = 0;
                    solveEndNode = math.min(NodeCount, requestedNodeCount);
                }
            }

            private void RemoveRupturedEdgeFromConductance(int sourceNodeIndex, float conductance)
            {
                if (!IsValidNodeIndex(sourceNodeIndex) ||
                    !NodeConductanceSum.IsCreated ||
                    sourceNodeIndex >= NodeConductanceSum.Length ||
                    conductance <= Epsilon)
                    return;

                float conductanceSum = math.max(0f, NodeConductanceSum[sourceNodeIndex] - conductance);
                NodeConductanceSum[sourceNodeIndex] = conductanceSum;
                NodeConductanceInverseSum[sourceNodeIndex] = conductanceSum > Epsilon
                    ? math.rcp(conductanceSum)
                    : 0f;
            }

            private void DecrementRuntimeConductiveEdgeCount()
            {
                if (!RuntimeConductiveEdgeCount.IsCreated || RuntimeConductiveEdgeCount.Length <= 0)
                    return;

                RuntimeConductiveEdgeCount[0] = math.max(0, RuntimeConductiveEdgeCount[0] - 1);
            }

            private void ClearEdgeFlows()
            {
                int safeEdgeCount = math.min(EdgeCount, EdgeFlow.IsCreated ? EdgeFlow.Length : 0);
                for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
                    EdgeFlow[edgeIndex] = 0f;
            }

            private void AccumulateNodeLoad(int nodeIndex, float loadWatts)
            {
                if (!IsValidNodeIndex(nodeIndex) || loadWatts <= Epsilon)
                    return;

                LogisticsNode node = Nodes[nodeIndex];
                node.CurrentLoad = math.max(node.CurrentLoad, loadWatts);
                Nodes[nodeIndex] = node;
            }

            private void MarkNodeOverloaded(int nodeIndex)
            {
                if (!IsValidNodeIndex(nodeIndex))
                    return;

                LogisticsNode node = Nodes[nodeIndex];
                node.Flags |= LogisticsNodeFlags.Overloaded;
                Nodes[nodeIndex] = node;
                if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                    PowerNodeFlags[nodeIndex] = (byte)(PowerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Overloaded);
            }

            private int TraverseReachableFrom(int startNodeIndex)
            {
                if (!IsValidNodeIndex(startNodeIndex))
                    return 0;

                int searchBudget = math.min(NodeCount, MaxSearchDepth);
                if (searchBudget <= 0)
                    return 0;

                ClearVisited();
                int head = 0;
                int tail = 0;
                Visited[startNodeIndex] = 1;
                TraversalQueue[tail++] = startNodeIndex;

                int visitedCount = 0;
                while (head < tail && visitedCount < searchBudget)
                {
                    int nodeIndex = TraversalQueue[head++];
                    visitedCount++;

                    int edgeStart = EdgeOffsets[nodeIndex];
                    int edgeEnd = EdgeOffsets[nodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        if (IsEdgeRuptured(edgeIndex))
                            continue;

                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(destinationNodeIndex) || Visited[destinationNodeIndex] != 0)
                            continue;

                        if (tail >= searchBudget)
                            continue;

                        Visited[destinationNodeIndex] = 1;
                        TraversalQueue[tail++] = destinationNodeIndex;
                    }
                }

                return visitedCount;
            }

            private int TraverseProducerReachability()
            {
                ClearVisited();
                int searchBudget = math.min(NodeCount, MaxSearchDepth);
                if (searchBudget <= 0)
                    return 0;

                int head = 0;
                int tail = 0;

                int producerCount = math.min(ProducerCount, Producers.IsCreated ? Producers.Length : 0);
                for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
                {
                    int nodeIndex = Producers[producerIndex].NodeIndex;
                    if (!IsValidNodeIndex(nodeIndex) || Visited[nodeIndex] != 0)
                        continue;

                    if (tail >= searchBudget)
                        break;

                    Visited[nodeIndex] = 1;
                    TraversalQueue[tail++] = nodeIndex;
                }

                int visitedCount = 0;
                while (head < tail && visitedCount < searchBudget)
                {
                    int sourceNodeIndex = TraversalQueue[head++];
                    visitedCount++;

                    int edgeStart = EdgeOffsets[sourceNodeIndex];
                    int edgeEnd = EdgeOffsets[sourceNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        if (IsEdgeRuptured(edgeIndex))
                            continue;

                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(destinationNodeIndex) || Visited[destinationNodeIndex] != 0)
                            continue;

                        if (tail >= searchBudget)
                            continue;

                        Visited[destinationNodeIndex] = 1;
                        TraversalQueue[tail++] = destinationNodeIndex;
                    }
                }

                return visitedCount;
            }

            private void ClearVisited()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                    Visited[nodeIndex] = 0;
            }

            private void MarkIsolatedNodesFromVisited()
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    if (Visited[nodeIndex] == 0)
                        node.Flags |= LogisticsNodeFlags.Isolated;
                    else
                        node.Flags &= ~LogisticsNodeFlags.Isolated;

                    Nodes[nodeIndex] = node;
                }
            }

            private void ClearNodeFlags(LogisticsNodeFlags flags)
            {
                for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    node.Flags &= ~flags;
                    Nodes[nodeIndex] = node;
                }
            }

            private void MarkBrownoutNode(int nodeIndex)
            {
                if (!IsValidNodeIndex(nodeIndex))
                    return;

                LogisticsNode node = Nodes[nodeIndex];
                node.Flags |= LogisticsNodeFlags.Brownout;
                Nodes[nodeIndex] = node;
                if (PowerNodeFlags.IsCreated && nodeIndex < PowerNodeFlags.Length)
                    PowerNodeFlags[nodeIndex] = (byte)((PowerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
            }

            private bool CanServeConsumer(in ConsumerRecord consumer)
            {
                if (!IsValidNodeIndex(consumer.NodeIndex))
                    return false;

                LogisticsNode node = Nodes[consumer.NodeIndex];
                if ((node.Flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Ruptured)) != 0)
                    return false;

                return true;
            }

            private int FindRoot(int nodeIndex)
            {
                int parentIndex = Parents[nodeIndex];
                int rootWatchdog = math.max(1, NodeCount + 1);
                while (parentIndex != Parents[parentIndex] && rootWatchdog-- > 0)
                {
                    Parents[parentIndex] = Parents[Parents[parentIndex]];
                    parentIndex = Parents[parentIndex];
                }

                if (rootWatchdog <= 0)
                {
                    Parents[nodeIndex] = nodeIndex;
                    return nodeIndex;
                }

                int currentIndex = nodeIndex;
                int compressionWatchdog = math.max(1, NodeCount + 1);
                while (currentIndex != parentIndex && compressionWatchdog-- > 0)
                {
                    int nextIndex = Parents[currentIndex];
                    Parents[currentIndex] = parentIndex;
                    currentIndex = nextIndex;
                }

                if (compressionWatchdog <= 0)
                {
                    Parents[nodeIndex] = nodeIndex;
                    return nodeIndex;
                }

                return parentIndex;
            }

            private void UnionRoots(int leftRoot, int rightRoot)
            {
                if (Ranks[leftRoot] < Ranks[rightRoot])
                {
                    Parents[leftRoot] = rightRoot;
                    return;
                }

                if (Ranks[leftRoot] > Ranks[rightRoot])
                {
                    Parents[rightRoot] = leftRoot;
                    return;
                }

                Parents[rightRoot] = leftRoot;
                Ranks[leftRoot] = Ranks[leftRoot] + 1;
            }

            private bool IsValidNodeIndex(int nodeIndex)
            {
                return nodeIndex >= 0 && nodeIndex < NodeCount;
            }

            private bool IsBoundBaseHibernating()
            {
                return BaseAwakeStateValue == 0;
            }

            private bool IsEdgeRuptured(int edgeIndex)
            {
                return (EdgeStates[edgeIndex] & (byte)LogisticsEdgeState.Ruptured) != 0;
            }

            private bool TryReadProducerRate(int nodeIndex, out float productionRate)
            {
                productionRate = 0f;
                if (!ProducerRates.IsCreated || (uint)nodeIndex >= (uint)ProducerRates.Length)
                    return false;

                productionRate = ProducerRates[nodeIndex];
                return productionRate > Epsilon && math.isfinite(productionRate);
            }

            private static LogisticsBrownoutTier ResolveBrownoutTier(float supplyRatio)
            {
                if (supplyRatio < 0.10f)
                    return LogisticsBrownoutTier.EmergencyOnly;
                if (supplyRatio < 0.40f)
                    return LogisticsBrownoutTier.EssentialOnly;
                if (supplyRatio < 0.85f)
                    return LogisticsBrownoutTier.AmbientLightsOnly;
                return LogisticsBrownoutTier.None;
            }
        }

        private const int MinPriority = 0;
        private const int MaxPriority = 100;
        private const int MaxActiveCompartmentSearchNodes = 4096;
        private const int MaxSearchDepth = MaxActiveCompartmentSearchNodes;
        private const int ParallelNodeBatchSize = 64;
        private const int FixedPowerDeltaPropagationPassCount = TwoPassPowerGridSolverJob.FixedPropagationPassCount;
        private const int AdaptiveSolveNodeThreshold = 500;
        private const int LowAdaptiveSolveNodesPerFrame = 128;
        private const int Mx350AdaptiveSolveNodesPerFrame = 160;
        private const int MidAdaptiveSolveNodesPerFrame = 250;
        private const int HighAdaptiveSolveNodesPerFrame = 500;
        private const int UltraAdaptiveSolveNodesPerFrame = 1000;
        private const float MinResistance = 0.0001f;
        private const float Epsilon = 0.001f;
        private const float RuptureFlowMultiplier = 1.15f;
        private const int PowerBlackBoxCapacity = 300;
        private const uint PowerBlackBoxMagic = 0x50475244u; // "PGRD"
        private const uint PowerBlackBoxVersion = 1u;
        private const uint PowerBlackBoxNonFiniteFlag = 1u << 0;
        private const uint PowerBlackBoxNoConductiveEdgesFlag = 1u << 1;
        private const uint PowerBlackBoxBrownoutFlag = 1u << 2;
        private const uint PowerBlackBoxOverloadFlag = 1u << 3;
        private const uint PowerBlackBoxHibernatingFlag = 1u << 4;
        private const uint PowerBlackBoxVaultLockFailureFlag = 1u << 5;
        private const string PowerBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_1319_Logistics.bin";
        private const int LogisticsGraphBufferBase = 731300;
        private const int LogisticsGraphBufferStride = 64;
        private const string NativeMemoryOwner = nameof(LogisticsNetworkGraph);

        private LogisticsNetworkType _networkType;
        private int _nodeCount;
        private int _edgeCount;
        private int _conductiveEdgeCount;

        private VaultGenerationHandle<LogisticsNode> _nodeBufferHandle;
        private VaultGenerationHandle<int> _edgeOffsetsHandle;
        private VaultGenerationHandle<int> _edgeDestinationsHandle;
        private VaultGenerationHandle<float> _edgeConductanceHandle;
        private VaultGenerationHandle<float> _edgeCapacityHandle;
        private VaultGenerationHandle<int> _edgeWriteCursorHandle;
        private VaultGenerationHandle<TopologyEdgeRecord> _topologyEdgeListHandle;
        private VaultGenerationHandle<ProducerRecord> _producersHandle;
        private VaultGenerationHandle<ConsumerRecord> _consumersHandle;
        private VaultGenerationHandle<float> _producerRatesHandle;
        private VaultGenerationHandle<float> _consumerDemandHandle;
        private VaultGenerationHandle<int> _parentsHandle;
        private VaultGenerationHandle<int> _ranksHandle;
        private VaultGenerationHandle<int> _componentIdsHandle;
        private VaultGenerationHandle<int> _componentSizesHandle;
        private VaultGenerationHandle<int> _rootToComponentHandle;
        private VaultGenerationHandle<int> _traversalQueueHandle;
        private VaultGenerationHandle<byte> _visitedHandle;
        private VaultGenerationHandle<byte> _consumerStatesHandle;
        private VaultGenerationHandle<float> _nodeNetInjectionHandle;
        private VaultGenerationHandle<float> _nodeServedDemandHandle;
        private VaultGenerationHandle<float> _nodeVoltageSupplyRatioHandle;
        private VaultGenerationHandle<float> _nodeSourcePotentialHandle;
        private VaultGenerationHandle<float> _nodeConductanceSumHandle;
        private VaultGenerationHandle<float> _nodeConductanceInverseSumHandle;
        private VaultGenerationHandle<float> _potentialFrontHandle;
        private VaultGenerationHandle<float> _potentialBackHandle;
        private VaultGenerationHandle<float> _powerCapacitiesHandle;
        private VaultGenerationHandle<byte> _powerNodeFlagsHandle;
        private VaultGenerationHandle<float> _edgeFlowHandle;
        private VaultGenerationHandle<byte> _edgeStatesHandle;
        private VaultGenerationHandle<int2> _edgeKeysHandle;
        private VaultGenerationHandle<int> _runtimeConductiveEdgeCountHandle;
        private VaultGenerationHandle<float> _componentGenerationHandle;
        private VaultGenerationHandle<float> _componentDemandHandle;
        private VaultGenerationHandle<float> _componentServedDemandHandle;
        private VaultGenerationHandle<float> _componentRemainingSupplyHandle;
        private VaultGenerationHandle<float> _componentSupplyRatioHandle;
        private VaultGenerationHandle<float> _componentResidualInjectionHandle;
        private VaultGenerationHandle<int> _componentAnchorNodeHandle;
        private VaultGenerationHandle<byte> _componentBrownoutTierHandle;
        private VaultGenerationHandle<TopologySummary> _scheduledTopologySummaryHandle;
        private VaultGenerationHandle<DistributionSummary> _scheduledDistributionSummaryHandle;
        private VaultGenerationHandle<ushort> _publishedNodeStatesHandle;
        private VaultGenerationHandle<ushort> _publishedNodeStatesBackHandle;
        private VaultGenerationHandle<PowerTelemetryEntry> _powerBlackBoxHandle;
        private VaultGenerationHandle<PowerGridCounter64> _powerBlackBoxCursorHandle;
        private IDataVault _dataVault;
        private IDataVault _powerBlackBoxVault;
        private readonly int _vaultBufferBase;
        private static int _nextSentinelInstanceId;
        private JobHandle _evaluateGraphJobHandle;
        private bool _evaluateGraphPending;
        private JobHandle _publishNodeStatesJobHandle;
        private bool _publishNodeStatesPending;
        private TopologySummary _committedTopologySummary;
        private DistributionSummary _committedDistributionSummary;
        private int _adaptiveSolveCursor;
        private int _adaptiveSolveRemainingNodes;
        private int _scheduledSolveNodeCount;
        private int _lastPowerBlackBoxSolveStartNode;
        private int _lastPowerBlackBoxSolveNodeCount;
        private int _baseAwakeIndex;
        private byte _baseAwakeStateValue;
        private int _topologyEdgeCount;
        private int _producerCount;
        private int _consumerCount;
        private bool _scheduledAdaptiveSolveSlice;
        private bool _powerBlackBoxDumped;
        private bool _buildOpen;
        private bool _unpoweredZeroStateLatched;
        private bool _noEdgeZeroStateLatched;

        private NativeArray<LogisticsNode> _nodeBuffer => ResolveVaultBuffer(in _nodeBufferHandle);
        private NativeArray<int> _edgeOffsets => ResolveVaultBuffer(in _edgeOffsetsHandle);
        private NativeArray<int> _edgeDestinations => ResolveVaultBuffer(in _edgeDestinationsHandle);
        private NativeArray<float> _edgeConductance => ResolveVaultBuffer(in _edgeConductanceHandle);
        private NativeArray<float> _edgeCapacity => ResolveVaultBuffer(in _edgeCapacityHandle);
        private NativeArray<int> _edgeWriteCursor => ResolveVaultBuffer(in _edgeWriteCursorHandle);
        private NativeArray<TopologyEdgeRecord> _topologyEdgeList => ResolveVaultBuffer(in _topologyEdgeListHandle);
        private NativeArray<ProducerRecord> _producers => ResolveVaultBuffer(in _producersHandle);
        private NativeArray<ConsumerRecord> _consumers => ResolveVaultBuffer(in _consumersHandle);
        private NativeArray<float> _producerRates => ResolveVaultBuffer(in _producerRatesHandle);
        private NativeArray<float> _consumerDemand => ResolveVaultBuffer(in _consumerDemandHandle);
        private NativeArray<int> _parents => ResolveVaultBuffer(in _parentsHandle);
        private NativeArray<int> _ranks => ResolveVaultBuffer(in _ranksHandle);
        private NativeArray<int> _componentIds => ResolveVaultBuffer(in _componentIdsHandle);
        private NativeArray<int> _componentSizes => ResolveVaultBuffer(in _componentSizesHandle);
        private NativeArray<int> _rootToComponent => ResolveVaultBuffer(in _rootToComponentHandle);
        private NativeArray<int> _traversalQueue => ResolveVaultBuffer(in _traversalQueueHandle);
        private NativeArray<byte> _visited => ResolveVaultBuffer(in _visitedHandle);
        private NativeArray<byte> _consumerStates => ResolveVaultBuffer(in _consumerStatesHandle);
        private NativeArray<float> _nodeNetInjection => ResolveVaultBuffer(in _nodeNetInjectionHandle);
        private NativeArray<float> _nodeServedDemand => ResolveVaultBuffer(in _nodeServedDemandHandle);
        private NativeArray<float> _nodeVoltageSupplyRatio => ResolveVaultBuffer(in _nodeVoltageSupplyRatioHandle);
        private NativeArray<float> _nodeSourcePotential => ResolveVaultBuffer(in _nodeSourcePotentialHandle);
        private NativeArray<float> _nodeConductanceSum => ResolveVaultBuffer(in _nodeConductanceSumHandle);
        private NativeArray<float> _nodeConductanceInverseSum => ResolveVaultBuffer(in _nodeConductanceInverseSumHandle);
        private NativeArray<float> _potentialFront => ResolveVaultBuffer(in _potentialFrontHandle);
        private NativeArray<float> _potentialBack => ResolveVaultBuffer(in _potentialBackHandle);
        private NativeArray<float> _powerCapacities => ResolveVaultBuffer(in _powerCapacitiesHandle);
        private NativeArray<byte> _powerNodeFlags => ResolveVaultBuffer(in _powerNodeFlagsHandle);
        private NativeArray<float> _edgeFlow => ResolveVaultBuffer(in _edgeFlowHandle);
        private NativeArray<byte> _edgeStates => ResolveVaultBuffer(in _edgeStatesHandle);
        private NativeArray<int2> _edgeKeys => ResolveVaultBuffer(in _edgeKeysHandle);
        private NativeArray<int> _runtimeConductiveEdgeCount => ResolveVaultBuffer(in _runtimeConductiveEdgeCountHandle);
        private NativeArray<float> _componentGeneration => ResolveVaultBuffer(in _componentGenerationHandle);
        private NativeArray<float> _componentDemand => ResolveVaultBuffer(in _componentDemandHandle);
        private NativeArray<float> _componentServedDemand => ResolveVaultBuffer(in _componentServedDemandHandle);
        private NativeArray<float> _componentRemainingSupply => ResolveVaultBuffer(in _componentRemainingSupplyHandle);
        private NativeArray<float> _componentSupplyRatio => ResolveVaultBuffer(in _componentSupplyRatioHandle);
        private NativeArray<float> _componentResidualInjection => ResolveVaultBuffer(in _componentResidualInjectionHandle);
        private NativeArray<int> _componentAnchorNode => ResolveVaultBuffer(in _componentAnchorNodeHandle);
        private NativeArray<byte> _componentBrownoutTier => ResolveVaultBuffer(in _componentBrownoutTierHandle);
        private NativeArray<TopologySummary> _scheduledTopologySummary => ResolveVaultBuffer(in _scheduledTopologySummaryHandle);
        private NativeArray<DistributionSummary> _scheduledDistributionSummary => ResolveVaultBuffer(in _scheduledDistributionSummaryHandle);
        private NativeArray<ushort> _publishedNodeStates => ResolveVaultBuffer(in _publishedNodeStatesHandle);
        private NativeArray<ushort> _publishedNodeStatesBack => ResolveVaultBuffer(in _publishedNodeStatesBackHandle);

        public LogisticsNetworkGraph(int nodeCapacity = 16, int edgeCapacity = 32, int consumerCapacity = 16)
        {
            int safeNodeCapacity = math.max(1, nodeCapacity);
            int safeEdgeCapacity = math.max(1, edgeCapacity);
            int safeConsumerCapacity = math.max(1, consumerCapacity);
            _vaultBufferBase = LogisticsGraphBufferBase + (++_nextSentinelInstanceId * LogisticsGraphBufferStride);
            _dataVault = GlobalRegistry.DataVault;
            _powerBlackBoxVault = _dataVault;
            _baseAwakeStateValue = 1;

            EnsureNodeCapacity(safeNodeCapacity);
            EnsureEdgeCapacity(safeEdgeCapacity);
            EnsureTopologyCapacity(safeEdgeCapacity);
            EnsureProducerCapacity(safeNodeCapacity);
            EnsureConsumerCapacity(safeConsumerCapacity);
            EnsureWorkingCapacity(safeNodeCapacity, safeConsumerCapacity);
            EnsureSummaryBuffers();
            EnsurePowerBlackBoxBuffers();
            _committedDistributionSummary = new DistributionSummary
            {
                SupplyRatio = 1f,
                BrownoutTier = LogisticsBrownoutTier.None
            };
        }

        public int NodeCount => _nodeCount;
        public int EdgeCount => _edgeCount;
        public int ConsumerCount => _consumerCount;
        public bool HasPendingEvaluation => _evaluateGraphPending;
        public bool HasPendingNodeStatePublish => _publishNodeStatesPending;
        public bool UsesAdaptiveEvaluation => _nodeCount > AdaptiveSolveNodeThreshold;

        public NativeArray<byte>.ReadOnly GetEdgeStatesReadOnly()
        {
            if (_evaluateGraphPending)
                return default;

            return _edgeStates.IsCreated ? _edgeStates.AsReadOnly() : default;
        }

        public NativeArray<float>.ReadOnly GetEdgeFlowsReadOnly()
        {
            if (_evaluateGraphPending)
                return default;

            return _edgeFlow.IsCreated ? _edgeFlow.AsReadOnly() : default;
        }

        public NativeArray<float>.ReadOnly GetPowerPotentialsReadOnly()
        {
            if (_evaluateGraphPending || !_potentialFront.IsCreated)
                return default;

            return _potentialFront.AsReadOnly();
        }

        public NativeArray<float>.ReadOnly GetPowerCapacitiesReadOnly()
        {
            if (_evaluateGraphPending || !_powerCapacities.IsCreated)
                return default;

            return _powerCapacities.AsReadOnly();
        }

        public NativeArray<byte>.ReadOnly GetNodeFlagsReadOnly()
        {
            if (_evaluateGraphPending || !_powerNodeFlags.IsCreated)
                return default;

            return _powerNodeFlags.AsReadOnly();
        }

        /// <summary>
        /// Binds this power graph to a scalar habitat base awake state copied from the atmosphere authority.
        /// </summary>
        /// <returns>True when the binding was accepted.</returns>
        public bool TryBindBaseAwakeStateValue(byte baseAwakeStateValue)
        {
            if (_evaluateGraphPending || _publishNodeStatesPending)
                return false;

            _baseAwakeIndex = 0;
            _baseAwakeStateValue = baseAwakeStateValue == 0 ? (byte)0 : (byte)1;
            return true;
        }

        public bool TryGetNodePotential(int nodeIndex, out float potential)
        {
            potential = 0f;
            if (_evaluateGraphPending ||
                !_potentialFront.IsCreated ||
                nodeIndex < 0 ||
                nodeIndex >= _nodeCount)
            {
                return false;
            }

            float rawPotential = _potentialFront[nodeIndex];
            if (!math.isfinite(rawPotential))
            {
                WritePowerBlackBoxSample(PowerBlackBoxNonFiniteFlag);
                return false;
            }

            potential = math.saturate(rawPotential);
            return true;
        }

        public bool TryConsumeNodePotential(int nodeIndex, float consumption)
        {
            if (_evaluateGraphPending ||
                !_potentialFront.IsCreated ||
                !_potentialBack.IsCreated ||
                nodeIndex < 0 ||
                nodeIndex >= _nodeCount ||
                !math.isfinite(consumption) ||
                consumption <= 0f)
            {
                return false;
            }

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return false;

            try
            {
                float currentPotential = math.isfinite(_potentialFront[nodeIndex])
                    ? math.saturate(_potentialFront[nodeIndex])
                    : 0f;
                if (!math.isfinite(_potentialFront[nodeIndex]))
                    WritePowerBlackBoxSample(PowerBlackBoxNonFiniteFlag);
                float nextPotential = math.saturate(currentPotential - consumption);
                WriteNative(_potentialFront, nodeIndex, nextPotential);
                WriteNative(_potentialBack, nodeIndex, nextPotential);
                if (_nodeVoltageSupplyRatio.IsCreated && nodeIndex < _nodeVoltageSupplyRatio.Length)
                    WriteNative(_nodeVoltageSupplyRatio, nodeIndex, nextPotential);

                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Potential = nextPotential;
                if (nextPotential < TwoPassPowerGridSolverJob.BrownoutPotentialThreshold)
                {
                    node.Flags |= LogisticsNodeFlags.Brownout;
                    if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                        WriteNative(_powerNodeFlags, nodeIndex, (byte)((_powerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered));
                }

                WriteNative(_nodeBuffer, nodeIndex, node);
                return true;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public bool TryRemovePowerConnectionBucket(int sourceNodeIndex)
        {
            if (_evaluateGraphPending ||
                !_edgeOffsets.IsCreated ||
                !_edgeStates.IsCreated ||
                !_edgeFlow.IsCreated ||
                sourceNodeIndex < 0 ||
                sourceNodeIndex >= _nodeCount)
            {
                return false;
            }

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return false;

            try
            {
                int edgeStart = _edgeOffsets[sourceNodeIndex];
                int edgeEnd = _edgeOffsets[sourceNodeIndex + 1];
                int removedConductiveEdges = 0;
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    byte state = _edgeStates[edgeIndex];
                    if ((state & (byte)LogisticsEdgeState.Ruptured) == 0)
                        removedConductiveEdges++;

                    WriteNative(_edgeStates, edgeIndex, (byte)(state | (byte)LogisticsEdgeState.Ruptured));
                    WriteNative(_edgeFlow, edgeIndex, 0f);
                }

                if (_runtimeConductiveEdgeCount.IsCreated && _runtimeConductiveEdgeCount.Length > 0)
                    WriteNative(_runtimeConductiveEdgeCount, 0, math.max(0, _runtimeConductiveEdgeCount[0] - removedConductiveEdges));
                return true;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public bool TryGetDirectedEdgeState(int sourceNodeIndex, int destinationNodeIndex, out LogisticsEdgeState state, out float flow)
        {
            state = LogisticsEdgeState.None;
            flow = 0f;
            if (_evaluateGraphPending ||
                !_edgeOffsets.IsCreated ||
                !_edgeDestinations.IsCreated ||
                !_edgeStates.IsCreated ||
                !_edgeFlow.IsCreated ||
                sourceNodeIndex < 0 ||
                sourceNodeIndex >= _nodeCount)
            {
                return false;
            }

            int edgeStart = _edgeOffsets[sourceNodeIndex];
            int edgeEnd = _edgeOffsets[sourceNodeIndex + 1];
            for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
            {
                if (_edgeDestinations[edgeIndex] != destinationNodeIndex)
                    continue;

                state = (LogisticsEdgeState)_edgeStates[edgeIndex];
                flow = _edgeFlow[edgeIndex];
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            JobHandle disposeDependency = CancelPendingJobsForDispose();
            DispatcherJobFence.TryComplete(ref disposeDependency, forceComplete: true);

            _baseAwakeIndex = 0;
            _baseAwakeStateValue = 1;
            _topologyEdgeCount = 0;
            _producerCount = 0;
            _consumerCount = 0;

            ReleaseVaultBuffer(ref _nodeBufferHandle);
            ReleaseVaultBuffer(ref _edgeOffsetsHandle);
            ReleaseVaultBuffer(ref _edgeDestinationsHandle);
            ReleaseVaultBuffer(ref _edgeConductanceHandle);
            ReleaseVaultBuffer(ref _edgeCapacityHandle);
            ReleaseVaultBuffer(ref _edgeWriteCursorHandle);
            ReleaseVaultBuffer(ref _topologyEdgeListHandle);
            ReleaseVaultBuffer(ref _producersHandle);
            ReleaseVaultBuffer(ref _consumersHandle);
            ReleaseVaultBuffer(ref _producerRatesHandle);
            ReleaseVaultBuffer(ref _consumerDemandHandle);
            ReleaseVaultBuffer(ref _parentsHandle);
            ReleaseVaultBuffer(ref _ranksHandle);
            ReleaseVaultBuffer(ref _componentIdsHandle);
            ReleaseVaultBuffer(ref _componentSizesHandle);
            ReleaseVaultBuffer(ref _rootToComponentHandle);
            ReleaseVaultBuffer(ref _traversalQueueHandle);
            ReleaseVaultBuffer(ref _visitedHandle);
            ReleaseVaultBuffer(ref _consumerStatesHandle);
            ReleaseVaultBuffer(ref _nodeNetInjectionHandle);
            ReleaseVaultBuffer(ref _nodeServedDemandHandle);
            ReleaseVaultBuffer(ref _nodeVoltageSupplyRatioHandle);
            ReleaseVaultBuffer(ref _nodeSourcePotentialHandle);
            ReleaseVaultBuffer(ref _nodeConductanceSumHandle);
            ReleaseVaultBuffer(ref _nodeConductanceInverseSumHandle);
            ReleaseVaultBuffer(ref _potentialFrontHandle);
            ReleaseVaultBuffer(ref _potentialBackHandle);
            ReleaseVaultBuffer(ref _powerCapacitiesHandle);
            ReleaseVaultBuffer(ref _powerNodeFlagsHandle);
            ReleaseVaultBuffer(ref _edgeFlowHandle);
            ReleaseVaultBuffer(ref _edgeStatesHandle);
            ReleaseVaultBuffer(ref _edgeKeysHandle);
            ReleaseVaultBuffer(ref _runtimeConductiveEdgeCountHandle);
            ReleaseVaultBuffer(ref _componentGenerationHandle);
            ReleaseVaultBuffer(ref _componentDemandHandle);
            ReleaseVaultBuffer(ref _componentServedDemandHandle);
            ReleaseVaultBuffer(ref _componentRemainingSupplyHandle);
            ReleaseVaultBuffer(ref _componentSupplyRatioHandle);
            ReleaseVaultBuffer(ref _componentResidualInjectionHandle);
            ReleaseVaultBuffer(ref _componentAnchorNodeHandle);
            ReleaseVaultBuffer(ref _componentBrownoutTierHandle);
            ReleaseVaultBuffer(ref _scheduledTopologySummaryHandle);
            ReleaseVaultBuffer(ref _scheduledDistributionSummaryHandle);
            ReleaseVaultBuffer(ref _publishedNodeStatesHandle);
            ReleaseVaultBuffer(ref _publishedNodeStatesBackHandle);
            _powerBlackBoxHandle = default;
            _powerBlackBoxCursorHandle = default;
            _dataVault = null;
            _powerBlackBoxVault = null;

            JobHandle.ScheduleBatchedJobs();
        }

        private JobHandle CancelPendingJobsForDispose()
        {
            JobHandle evaluationHandle = _evaluateGraphPending ? _evaluateGraphJobHandle : default;
            JobHandle publishHandle = _publishNodeStatesPending ? _publishNodeStatesJobHandle : default;

            _evaluateGraphJobHandle = default;
            _publishNodeStatesJobHandle = default;
            _evaluateGraphPending = false;
            _publishNodeStatesPending = false;
            _adaptiveSolveCursor = 0;
            _adaptiveSolveRemainingNodes = 0;
            _scheduledSolveNodeCount = 0;
            _lastPowerBlackBoxSolveStartNode = 0;
            _lastPowerBlackBoxSolveNodeCount = 0;
            _scheduledAdaptiveSolveSlice = false;
            _buildOpen = false;

            return JobHandle.CombineDependencies(evaluationHandle, publishHandle);
        }

        private BufferID ResolveGraphBufferId(int localOffset)
        {
            return (BufferID)(_vaultBufferBase + localOffset);
        }

        private NativeArray<T> ResolveVaultBuffer<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u)
                return default;

            return vault.TryResolveHandle(in handle, out NativeArray<T> buffer) && buffer.IsCreated
                ? buffer
                : default;
        }

        private void EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            int localOffset,
            int requiredLength,
            NativeArrayOptions options = NativeArrayOptions.ClearMemory)
            where T : struct
        {
            IDataVault vault = _dataVault;
            int safeLength = math.max(1, requiredLength);
            if (vault == null)
            {
                handle = default;
                return;
            }

            if (handle.BufferID != 0u &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= safeLength)
            {
                return;
            }

            handle = vault.EnsureGenerationHandle<T>(
                ResolveGraphBufferId(localOffset),
                safeLength,
                SystemID.Power,
                options);
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryLockGraphMutationBuffers(out ulong lockedMask)
        {
            lockedMask = 0UL;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool locked =
                TryLockGraphBuffer(vault, in _nodeBufferHandle, 0, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeOffsetsHandle, 1, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeDestinationsHandle, 2, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeConductanceHandle, 3, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeCapacityHandle, 4, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeWriteCursorHandle, 5, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _topologyEdgeListHandle, 6, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _producersHandle, 7, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _consumersHandle, 8, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _producerRatesHandle, 9, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _consumerDemandHandle, 10, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _parentsHandle, 11, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _ranksHandle, 12, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentIdsHandle, 13, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentSizesHandle, 14, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _rootToComponentHandle, 15, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _traversalQueueHandle, 16, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _visitedHandle, 17, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _consumerStatesHandle, 18, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _nodeNetInjectionHandle, 19, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _nodeServedDemandHandle, 20, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _nodeVoltageSupplyRatioHandle, 21, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _nodeSourcePotentialHandle, 22, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _nodeConductanceSumHandle, 23, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _nodeConductanceInverseSumHandle, 24, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _potentialFrontHandle, 25, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _potentialBackHandle, 26, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _powerCapacitiesHandle, 27, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _powerNodeFlagsHandle, 28, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeFlowHandle, 29, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeStatesHandle, 30, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _edgeKeysHandle, 31, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _runtimeConductiveEdgeCountHandle, 32, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentGenerationHandle, 33, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentDemandHandle, 34, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentServedDemandHandle, 35, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentRemainingSupplyHandle, 36, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentSupplyRatioHandle, 37, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentResidualInjectionHandle, 38, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentAnchorNodeHandle, 39, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _componentBrownoutTierHandle, 40, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _scheduledTopologySummaryHandle, 41, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _scheduledDistributionSummaryHandle, 42, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _publishedNodeStatesHandle, 43, ref lockedMask) &&
                TryLockGraphBuffer(vault, in _publishedNodeStatesBackHandle, 44, ref lockedMask);

            if (locked)
                return true;

            UnlockGraphMutationBuffers(lockedMask);
            lockedMask = 0UL;
            return false;
        }

        private static bool TryLockGraphBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int bit,
            ref ulong lockedMask) where T : struct
        {
            if (handle.BufferID == 0u)
                return true;

            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryLockBuffer((BufferID)handle.BufferID, SystemID.Power))
                return false;

            lockedMask |= 1UL << bit;
            return true;
        }

        private void UnlockGraphMutationBuffers(ulong lockedMask)
        {
            IDataVault vault = _dataVault;
            if (vault == null || lockedMask == 0UL)
                return;

            UnlockGraphBuffer(vault, in _publishedNodeStatesBackHandle, 44, lockedMask);
            UnlockGraphBuffer(vault, in _publishedNodeStatesHandle, 43, lockedMask);
            UnlockGraphBuffer(vault, in _scheduledDistributionSummaryHandle, 42, lockedMask);
            UnlockGraphBuffer(vault, in _scheduledTopologySummaryHandle, 41, lockedMask);
            UnlockGraphBuffer(vault, in _componentBrownoutTierHandle, 40, lockedMask);
            UnlockGraphBuffer(vault, in _componentAnchorNodeHandle, 39, lockedMask);
            UnlockGraphBuffer(vault, in _componentResidualInjectionHandle, 38, lockedMask);
            UnlockGraphBuffer(vault, in _componentSupplyRatioHandle, 37, lockedMask);
            UnlockGraphBuffer(vault, in _componentRemainingSupplyHandle, 36, lockedMask);
            UnlockGraphBuffer(vault, in _componentServedDemandHandle, 35, lockedMask);
            UnlockGraphBuffer(vault, in _componentDemandHandle, 34, lockedMask);
            UnlockGraphBuffer(vault, in _componentGenerationHandle, 33, lockedMask);
            UnlockGraphBuffer(vault, in _runtimeConductiveEdgeCountHandle, 32, lockedMask);
            UnlockGraphBuffer(vault, in _edgeKeysHandle, 31, lockedMask);
            UnlockGraphBuffer(vault, in _edgeStatesHandle, 30, lockedMask);
            UnlockGraphBuffer(vault, in _edgeFlowHandle, 29, lockedMask);
            UnlockGraphBuffer(vault, in _powerNodeFlagsHandle, 28, lockedMask);
            UnlockGraphBuffer(vault, in _powerCapacitiesHandle, 27, lockedMask);
            UnlockGraphBuffer(vault, in _potentialBackHandle, 26, lockedMask);
            UnlockGraphBuffer(vault, in _potentialFrontHandle, 25, lockedMask);
            UnlockGraphBuffer(vault, in _nodeConductanceInverseSumHandle, 24, lockedMask);
            UnlockGraphBuffer(vault, in _nodeConductanceSumHandle, 23, lockedMask);
            UnlockGraphBuffer(vault, in _nodeSourcePotentialHandle, 22, lockedMask);
            UnlockGraphBuffer(vault, in _nodeVoltageSupplyRatioHandle, 21, lockedMask);
            UnlockGraphBuffer(vault, in _nodeServedDemandHandle, 20, lockedMask);
            UnlockGraphBuffer(vault, in _nodeNetInjectionHandle, 19, lockedMask);
            UnlockGraphBuffer(vault, in _consumerStatesHandle, 18, lockedMask);
            UnlockGraphBuffer(vault, in _visitedHandle, 17, lockedMask);
            UnlockGraphBuffer(vault, in _traversalQueueHandle, 16, lockedMask);
            UnlockGraphBuffer(vault, in _rootToComponentHandle, 15, lockedMask);
            UnlockGraphBuffer(vault, in _componentSizesHandle, 14, lockedMask);
            UnlockGraphBuffer(vault, in _componentIdsHandle, 13, lockedMask);
            UnlockGraphBuffer(vault, in _ranksHandle, 12, lockedMask);
            UnlockGraphBuffer(vault, in _parentsHandle, 11, lockedMask);
            UnlockGraphBuffer(vault, in _consumerDemandHandle, 10, lockedMask);
            UnlockGraphBuffer(vault, in _producerRatesHandle, 9, lockedMask);
            UnlockGraphBuffer(vault, in _consumersHandle, 8, lockedMask);
            UnlockGraphBuffer(vault, in _producersHandle, 7, lockedMask);
            UnlockGraphBuffer(vault, in _topologyEdgeListHandle, 6, lockedMask);
            UnlockGraphBuffer(vault, in _edgeWriteCursorHandle, 5, lockedMask);
            UnlockGraphBuffer(vault, in _edgeCapacityHandle, 4, lockedMask);
            UnlockGraphBuffer(vault, in _edgeConductanceHandle, 3, lockedMask);
            UnlockGraphBuffer(vault, in _edgeDestinationsHandle, 2, lockedMask);
            UnlockGraphBuffer(vault, in _edgeOffsetsHandle, 1, lockedMask);
            UnlockGraphBuffer(vault, in _nodeBufferHandle, 0, lockedMask);
        }

        private static void UnlockGraphBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int bit,
            ulong lockedMask) where T : struct
        {
            if ((lockedMask & (1UL << bit)) != 0UL && handle.BufferID != 0u)
                vault.TryUnlockBuffer((BufferID)handle.BufferID, SystemID.Power);
        }

        private static void ClearNativeArray<T>(NativeArray<T> buffer)
            where T : struct
        {
            if (!buffer.IsCreated)
                return;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = default;
        }

        private static void WriteNative<T>(NativeArray<T> buffer, int index, T value)
            where T : struct
        {
            buffer[index] = value;
        }

        private static void AddNative(NativeArray<int> buffer, int index, int delta)
        {
            buffer[index] = buffer[index] + delta;
        }

        private static void AddNative(NativeArray<float> buffer, int index, float delta)
        {
            buffer[index] = buffer[index] + delta;
        }

        private void EnsurePowerBlackBoxBuffers()
        {
            IDataVault vault = _powerBlackBoxVault;
            if (vault == null)
                return;

            _powerBlackBoxHandle = vault.EnsureGenerationHandle<PowerTelemetryEntry>(
                PowerGridBufferIds.TelemetryRing,
                PowerBlackBoxCapacity,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
            _powerBlackBoxCursorHandle = vault.EnsureGenerationHandle<PowerGridCounter64>(
                PowerGridBufferIds.TelemetryCursor,
                1,
                SystemID.Power,
                NativeArrayOptions.ClearMemory);
        }

        public void BeginBuild(LogisticsNetworkType networkType, int nodeCapacity, int edgeCapacity, int consumerCapacity)
        {
            if (!TryCompleteEvaluation() || !TryCompleteNodeStatePublish())
            {
                _buildOpen = false;
                return;
            }

            int safeNodeCapacity = math.max(1, nodeCapacity);
            int safeEdgeCapacity = math.max(1, edgeCapacity);
            int safeConsumerCapacity = math.max(1, consumerCapacity);

            _buildOpen = true;
            _unpoweredZeroStateLatched = false;
            _noEdgeZeroStateLatched = false;
            _networkType = networkType;
            _nodeCount = 0;
            _edgeCount = 0;
            _conductiveEdgeCount = 0;

            EnsureNodeCapacity(safeNodeCapacity);
            EnsureEdgeCapacity(safeEdgeCapacity);
            EnsureTopologyCapacity(safeEdgeCapacity);
            EnsureProducerCapacity(safeNodeCapacity);
            EnsureConsumerCapacity(safeConsumerCapacity);
            EnsureWorkingCapacity(safeNodeCapacity, safeConsumerCapacity);
            EnsureSummaryBuffers();

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
            {
                _buildOpen = false;
                return;
            }

            try
            {
                if (_runtimeConductiveEdgeCount.IsCreated)
                    WriteNative(_runtimeConductiveEdgeCount, 0, 0);

                _topologyEdgeCount = 0;
                _producerCount = 0;
                _consumerCount = 0;
                ClearNativeArray(_producerRates);
                ClearNativeArray(_consumerDemand);
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public int AddNode(uint nodeId, float capacity, float resistance, byte priorityTier, LogisticsNodeFlags flags, byte reservedState)
        {
            if (!_buildOpen)
                return -1;

            if (_nodeCount >= _nodeBuffer.Length)
                EnsureNodeCapacity(_nodeCount + 1);

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return -1;

            try
            {
                WriteNative(_nodeBuffer, _nodeCount, new LogisticsNode
                {
                    Id = nodeId,
                    Capacity = math.max(Epsilon, capacity),
                    Resistance = SanitizeNodeResistance(resistance),
                    CurrentLoad = 0f,
                    Potential = 0f,
                    Priority = priorityTier,
                    Flags = flags | LogisticsNodeFlags.Active,
                    NetworkId = 0,
                    Reserved = reservedState
                });

                WriteNative(_powerCapacities, _nodeCount, math.max(Epsilon, capacity));
                WriteNative(_powerNodeFlags, _nodeCount, ResolvePowerGridNodeFlags(flags, reservedState));
                return _nodeCount++;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public void AddEdge(int sourceNodeIndex, int destinationNodeIndex, float resistance)
        {
            if (!_buildOpen)
                return;

            if (_topologyEdgeCount >= _topologyEdgeList.Length)
                EnsureTopologyCapacity(math.max(1, _topologyEdgeList.Length * 2));

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                WriteNative(_topologyEdgeList, _topologyEdgeCount++, new TopologyEdgeRecord
                {
                    SourceNodeIndex = sourceNodeIndex,
                    DestinationNodeIndex = destinationNodeIndex,
                    Resistance = SanitizeEdgeResistance(resistance)
                });
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public void AddProducer(int nodeIndex, float productionRate)
        {
            if (!_buildOpen)
                return;

            if (nodeIndex < 0 || nodeIndex >= _nodeCount || productionRate <= 0f)
                return;

            NativeArray<float> producerRates = _producerRates;
            if (!producerRates.IsCreated || nodeIndex >= producerRates.Length)
                return;

            if (_producerCount >= _producers.Length)
                EnsureProducerCapacity(math.max(1, _producers.Length * 2));

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                float currentProduction = _producerRates[nodeIndex];
                if (currentProduction > Epsilon)
                {
                    WriteNative(_producerRates, nodeIndex, currentProduction + productionRate);
                    if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                        WriteNative(_powerNodeFlags, nodeIndex, (byte)(_powerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Source | (byte)PowerGridNodeFlags.Powered));
                    return;
                }

                WriteNative(_producerRates, nodeIndex, productionRate);
                WriteNative(_producers, _producerCount++, new ProducerRecord { NodeIndex = nodeIndex });
                if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                    WriteNative(_powerNodeFlags, nodeIndex, (byte)(_powerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Source | (byte)PowerGridNodeFlags.Powered));
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public void AddConsumer(int nodeIndex, float demand, int powerPriority, byte priorityTier, LogisticsConsumerFlags flags)
        {
            if (!_buildOpen)
                return;

            if (nodeIndex < 0 || nodeIndex >= _nodeCount || demand <= 0f)
                return;

            if (_consumerCount >= _consumers.Length)
                EnsureConsumerCapacity(math.max(1, _consumers.Length * 2));

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                if (_consumerDemand.IsCreated && nodeIndex < _consumerDemand.Length)
                    AddNative(_consumerDemand, nodeIndex, demand);

                WriteNative(_consumers, _consumerCount++, new ConsumerRecord
                {
                    NodeIndex = nodeIndex,
                    Demand = demand,
                    PowerPriority = math.clamp(powerPriority, MinPriority, MaxPriority),
                    PriorityTier = priorityTier,
                    Flags = flags
                });
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public void FinalizeBuild()
        {
            if (!_buildOpen)
                return;

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
            {
                _buildOpen = false;
                return;
            }

            try
            {
                int nodeCount = _nodeCount;
                for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                    WriteNative(_edgeOffsets, nodeIndex, 0);

                int topologyEdgeCount = _topologyEdgeCount;
                _edgeCount = 0;
                _conductiveEdgeCount = 0;

                for (int edgeIndex = 0; edgeIndex < topologyEdgeCount; edgeIndex++)
                {
                    TopologyEdgeRecord edge = _topologyEdgeList[edgeIndex];
                    if (!IsValidNodeIndex(edge.SourceNodeIndex) || !IsValidNodeIndex(edge.DestinationNodeIndex))
                        continue;

                    AddNative(_edgeOffsets, edge.SourceNodeIndex + 1, 1);
                    _edgeCount++;
                }

                for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
                    AddNative(_edgeOffsets, nodeIndex, _edgeOffsets[nodeIndex - 1]);

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    WriteNative(_edgeWriteCursor, nodeIndex, _edgeOffsets[nodeIndex]);
                    WriteNative(_nodeConductanceSum, nodeIndex, 0f);
                    WriteNative(_nodeConductanceInverseSum, nodeIndex, 0f);
                }

                for (int edgeIndex = 0; edgeIndex < topologyEdgeCount; edgeIndex++)
                {
                    TopologyEdgeRecord edge = _topologyEdgeList[edgeIndex];
                    if (!IsValidNodeIndex(edge.SourceNodeIndex) || !IsValidNodeIndex(edge.DestinationNodeIndex))
                        continue;

                    int writeIndex = _edgeWriteCursor[edge.SourceNodeIndex];
                    WriteNative(_edgeWriteCursor, edge.SourceNodeIndex, writeIndex + 1);
                    WriteNative(_edgeDestinations, writeIndex, edge.DestinationNodeIndex);
                    float conductance = ResolveEdgeConductanceForBuild(
                        edge.SourceNodeIndex,
                        edge.DestinationNodeIndex,
                        edge.Resistance);
                    WriteNative(_edgeConductance, writeIndex, conductance);
                    WriteNative(_edgeCapacity, writeIndex, ResolveEdgeCapacityForBuild(edge.SourceNodeIndex, edge.DestinationNodeIndex));
                    int2 edgeKey = new int2(edge.SourceNodeIndex, edge.DestinationNodeIndex);
                    if (_edgeKeys.IsCreated &&
                        (writeIndex >= _edgeKeys.Length ||
                         _edgeKeys[writeIndex].x != edgeKey.x ||
                         _edgeKeys[writeIndex].y != edgeKey.y))
                    {
                        if (_edgeStates.IsCreated && writeIndex < _edgeStates.Length)
                            WriteNative(_edgeStates, writeIndex, (byte)0);
                        if (_edgeFlow.IsCreated && writeIndex < _edgeFlow.Length)
                            WriteNative(_edgeFlow, writeIndex, 0f);
                    }

                    if (_edgeKeys.IsCreated && writeIndex < _edgeKeys.Length)
                        WriteNative(_edgeKeys, writeIndex, edgeKey);

                    byte edgeState = _edgeStates.IsCreated && writeIndex < _edgeStates.Length
                        ? _edgeStates[writeIndex]
                        : (byte)0;
                    if (conductance > Epsilon &&
                        (edgeState & (byte)LogisticsEdgeState.Ruptured) == 0)
                    {
                        _conductiveEdgeCount++;
                        AddNative(_nodeConductanceSum, edge.SourceNodeIndex, conductance);
                    }
                }

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    float conductanceSum = _nodeConductanceSum[nodeIndex];
                    WriteNative(_nodeConductanceInverseSum, nodeIndex, conductanceSum > Epsilon
                        ? math.rcp(conductanceSum)
                        : 0f);
                }

                if (_runtimeConductiveEdgeCount.IsCreated)
                    WriteNative(_runtimeConductiveEdgeCount, 0, _conductiveEdgeCount);

                _adaptiveSolveCursor = 0;
                _adaptiveSolveRemainingNodes = 0;
                _scheduledSolveNodeCount = 0;
                _lastPowerBlackBoxSolveStartNode = 0;
                _lastPowerBlackBoxSolveNodeCount = 0;
                _scheduledAdaptiveSolveSlice = false;
                _unpoweredZeroStateLatched = false;
                _noEdgeZeroStateLatched = false;
                _buildOpen = false;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public TopologySummary AnalyzeTopology()
        {
            if (!TryCompleteEvaluation())
                return _committedTopologySummary;

            TopologySummary summary = new TopologySummary
            {
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount
            };

            if (_nodeCount <= 0)
                return summary;

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return _committedTopologySummary;

            try
            {
                ResetTopologyScratch(_nodeCount);
                ClearNodeFlags(LogisticsNodeFlags.Isolated | LogisticsNodeFlags.BridgeNode);

                int cycleCount = 0;
                for (int sourceNodeIndex = 0; sourceNodeIndex < _nodeCount; sourceNodeIndex++)
                {
                    int edgeStart = _edgeOffsets[sourceNodeIndex];
                    int edgeEnd = _edgeOffsets[sourceNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        if (IsEdgeRuptured(edgeIndex))
                            continue;

                        int destinationNodeIndex = _edgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(destinationNodeIndex))
                            continue;

                        if (destinationNodeIndex < sourceNodeIndex)
                            continue;

                        int sourceRoot = FindRoot(sourceNodeIndex);
                        int destinationRoot = FindRoot(destinationNodeIndex);
                        if (sourceRoot == destinationRoot)
                        {
                            cycleCount++;
                            continue;
                        }

                        UnionRoots(sourceRoot, destinationRoot);
                    }
                }

                int islandCount = 0;
                for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                {
                    int rootIndex = FindRoot(nodeIndex);
                    WriteNative(_parents, nodeIndex, rootIndex);

                    int componentIndex = _rootToComponent[rootIndex];
                    if (componentIndex < 0)
                    {
                        componentIndex = islandCount;
                        WriteNative(_rootToComponent, rootIndex, componentIndex);
                        WriteNative(_componentSizes, componentIndex, 0);
                        islandCount++;
                    }

                    WriteNative(_componentIds, nodeIndex, componentIndex);
                    AddNative(_componentSizes, componentIndex, 1);

                    LogisticsNode node = _nodeBuffer[nodeIndex];
                    node.NetworkId = (byte)math.clamp(componentIndex, 0, (int)byte.MaxValue);
                    WriteNative(_nodeBuffer, nodeIndex, node);
                }

                summary.IslandCount = islandCount;
                summary.CycleCount = cycleCount;
                summary.BfsVisitedCount = TraverseReachableFromInternal(0);
                summary.ProducerReachableCount = TraverseProducerReachability();
                MarkIsolatedNodesFromVisited();
                _committedTopologySummary = summary;
                return summary;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public DistributionSummary Distribute()
        {
            if (!TryCompleteEvaluation())
                return _committedDistributionSummary;

            DistributionSummary summary = new DistributionSummary
            {
                TotalGeneration = ComputeTotalGeneration(),
                TotalConsumption = ComputeTotalConsumption()
            };

            summary.Balance = summary.TotalGeneration - summary.TotalConsumption;
            summary.SupplyRatio = summary.TotalConsumption > Epsilon
                ? math.saturate(summary.TotalGeneration * math.rcp(summary.TotalConsumption))
                : 1f;
            summary.HasDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption ? (byte)1 : (byte)0;
            summary.BrownoutTier = ResolveBrownoutTier(summary.SupplyRatio);

            if (_nodeCount <= 0)
                return summary;

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return _committedDistributionSummary;

            try
            {
                ResetConsumerStates(ConsumerCount);
                ResetDistributionState();
                BuildComponentDistributionState();
                AllocateServedDemand(ref summary);
                BuildNodeInjection();
                summary.UnservedDemand = math.max(0f, summary.TotalConsumption - summary.ServedDemand);
                summary.HasDeficit = summary.UnservedDemand > Epsilon ? (byte)1 : (byte)0;
                summary.BrownoutTier = summary.HasDeficit != 0
                    ? LogisticsBrownoutTier.EmergencyOnly
                    : LogisticsBrownoutTier.None;
                ApplyBinaryNodeLoads();
                _committedDistributionSummary = summary;
                return summary;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public int GetNodeComponentId(int nodeIndex)
        {
            if (_evaluateGraphPending || !_componentIds.IsCreated || nodeIndex < 0 || nodeIndex >= _componentIds.Length)
                return -1;

            return _componentIds[nodeIndex];
        }

        public int GetComponentSize(int componentIndex)
        {
            if (_evaluateGraphPending || !_componentSizes.IsCreated || componentIndex < 0 || componentIndex >= _componentSizes.Length)
                return 0;

            return _componentSizes[componentIndex];
        }

        public bool IsNodeReachable(int nodeIndex)
        {
            if (_evaluateGraphPending || !_visited.IsCreated || nodeIndex < 0 || nodeIndex >= _nodeCount)
                return false;

            return _visited[nodeIndex] != 0;
        }

        public bool IsConsumerPowered(int consumerIndex)
        {
            if (_evaluateGraphPending || !_consumerStates.IsCreated || consumerIndex < 0 || consumerIndex >= _consumerStates.Length)
                return false;

            return _consumerStates[consumerIndex] != 0;
        }

        public bool TryGetConsumerVoltageSupplyRatio(int consumerIndex, out float voltageSupplyRatio)
        {
            voltageSupplyRatio = 1f;
            if (_evaluateGraphPending ||
                !_nodeVoltageSupplyRatio.IsCreated ||
                consumerIndex < 0 ||
                consumerIndex >= _consumerCount)
            {
                return false;
            }

            int nodeIndex = _consumers[consumerIndex].NodeIndex;
            if (nodeIndex < 0 || nodeIndex >= _nodeVoltageSupplyRatio.Length)
                return false;

            voltageSupplyRatio = math.saturate(_nodeVoltageSupplyRatio[nodeIndex]);
            return math.isfinite(voltageSupplyRatio);
        }

        public void ScheduleNodeStatePublish()
        {
            ScheduleNodeStatePublish(default);
        }

        public void ScheduleNodeStatePublish(JobHandle dependency)
        {
            if (_publishNodeStatesPending)
                return;

            if (_nodeCount <= 0)
            {
                ClearPublishedNodeStates();
                return;
            }

            EnsurePublishedStateCapacity(_nodeCount);
            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                ClearNativeArray(_publishedNodeStatesBack);

                JobHandle publishDependency = _evaluateGraphPending
                    ? JobHandle.CombineDependencies(dependency, _evaluateGraphJobHandle)
                    : dependency;

                PublishNodeStatesJob job = new PublishNodeStatesJob
                {
                    Nodes = _nodeBuffer,
                    NodeNetInjection = _nodeNetInjection,
                    NodeServedDemand = _nodeServedDemand,
                    NodeVoltageSupplyRatio = _nodeVoltageSupplyRatio,
                    PublishedNodeStates = _publishedNodeStatesBack
                };

                _publishNodeStatesJobHandle = job.Schedule(_nodeCount, ParallelNodeBatchSize, publishDependency);
                _publishNodeStatesPending = true;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public JobHandle ScheduleEvaluation()
        {
            if (_evaluateGraphPending)
                return _evaluateGraphJobHandle;

            _adaptiveSolveRemainingNodes = 0;
            _scheduledSolveNodeCount = 0;
            _scheduledAdaptiveSolveSlice = false;

            int runtimeConductiveEdgeCount = _runtimeConductiveEdgeCount.IsCreated
                ? _runtimeConductiveEdgeCount[0]
                : _conductiveEdgeCount;
            if (_nodeCount <= 0 || _edgeCount <= 0 || runtimeConductiveEdgeCount <= 0)
            {
                if (_noEdgeZeroStateLatched)
                    return default;

                CommitNoEdgeEvaluation();
                _noEdgeZeroStateLatched = true;
                _unpoweredZeroStateLatched = false;
                return default;
            }

            if (_networkType == LogisticsNetworkType.PowerDc && ComputeTotalGeneration() <= Epsilon)
            {
                if (_unpoweredZeroStateLatched)
                    return default;

                CommitUnpoweredEvaluation();
                _unpoweredZeroStateLatched = true;
                _noEdgeZeroStateLatched = false;
                return default;
            }

            _unpoweredZeroStateLatched = false;
            _noEdgeZeroStateLatched = false;
            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            EnsureSummaryBuffers();

            return ScheduleEvaluationSlice(relaxationSliceOnly: false);
        }

        private JobHandle ScheduleEvaluationSlice(bool relaxationSliceOnly)
        {
            float qualityWeight = ResolveEvaluationQualityWeight();
            ResolveAdaptiveSolveWindow(qualityWeight, out int solveStartNode, out int solveNodeCount);
            _lastPowerBlackBoxSolveStartNode = solveStartNode;
            _lastPowerBlackBoxSolveNodeCount = solveNodeCount;

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
            {
                _scheduledAdaptiveSolveSlice = false;
                _scheduledSolveNodeCount = 0;
                return default;
            }

            try
            {
                EvaluateGraphJob job = new EvaluateGraphJob
                {
                    NetworkType = _networkType,
                    NodeCount = _nodeCount,
                    EdgeCount = _edgeCount,
                    ConsumerCount = ConsumerCount,
                    ProducerCount = _producerCount,
                    SolveStartNode = solveStartNode,
                    SolveNodeCount = solveNodeCount,
                    BaseAwakeIndex = _baseAwakeIndex,
                    BaseAwakeStateValue = _baseAwakeStateValue,
                    RelaxationSliceOnly = relaxationSliceOnly ? (byte)1 : (byte)0,
                    GlobalQualityWeight = qualityWeight,
                    Nodes = _nodeBuffer,
                    EdgeOffsets = _edgeOffsets,
                    EdgeDestinations = _edgeDestinations,
                    EdgeConductance = _edgeConductance,
                    EdgeCapacity = _edgeCapacity,
                    Producers = _producers,
                    Consumers = _consumers,
                    ProducerRates = _producerRates,
                    Parents = _parents,
                    Ranks = _ranks,
                    ComponentIds = _componentIds,
                    ComponentSizes = _componentSizes,
                    RootToComponent = _rootToComponent,
                    TraversalQueue = _traversalQueue,
                    Visited = _visited,
                    ConsumerStates = _consumerStates,
                    NodeNetInjection = _nodeNetInjection,
                    NodeServedDemand = _nodeServedDemand,
                    NodeVoltageSupplyRatio = _nodeVoltageSupplyRatio,
                    NodeSourcePotential = _nodeSourcePotential,
                    NodeConductanceSum = _nodeConductanceSum,
                    NodeConductanceInverseSum = _nodeConductanceInverseSum,
                    PotentialFront = _potentialFront,
                    PotentialBack = _potentialBack,
                    PowerCapacities = _powerCapacities,
                    PowerNodeFlags = _powerNodeFlags,
                    EdgeFlow = _edgeFlow,
                    EdgeStates = _edgeStates,
                    RuntimeConductiveEdgeCount = _runtimeConductiveEdgeCount,
                    ComponentGeneration = _componentGeneration,
                    ComponentDemand = _componentDemand,
                    ComponentServedDemand = _componentServedDemand,
                    ComponentRemainingSupply = _componentRemainingSupply,
                    ComponentSupplyRatio = _componentSupplyRatio,
                    ComponentResidualInjection = _componentResidualInjection,
                    ComponentAnchorNode = _componentAnchorNode,
                    ComponentBrownoutTier = _componentBrownoutTier,
                    TopologySummaryBuffer = _scheduledTopologySummary,
                    DistributionSummaryBuffer = _scheduledDistributionSummary
                };

                _evaluateGraphJobHandle = job.Schedule();
                _evaluateGraphPending = true;
                return _evaluateGraphJobHandle;
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        private void ResolveAdaptiveSolveWindow(float globalQualityWeight, out int solveStartNode, out int solveNodeCount)
        {
            solveStartNode = 0;
            solveNodeCount = _nodeCount;
            _scheduledAdaptiveSolveSlice = false;
            _scheduledSolveNodeCount = solveNodeCount;

            if (_nodeCount <= AdaptiveSolveNodeThreshold)
            {
                _adaptiveSolveCursor = 0;
                _adaptiveSolveRemainingNodes = 0;
                return;
            }

            if (_adaptiveSolveCursor < 0 || _adaptiveSolveCursor >= _nodeCount)
                _adaptiveSolveCursor = 0;
            if (_adaptiveSolveRemainingNodes <= 0 || _adaptiveSolveRemainingNodes > _nodeCount)
                _adaptiveSolveRemainingNodes = _nodeCount;

            int solveBudget = ResolveAdaptiveSolveNodesPerFrame(globalQualityWeight);
            int contiguousNodeCount = _nodeCount - _adaptiveSolveCursor;
            solveStartNode = _adaptiveSolveCursor;
            solveNodeCount = math.min(
                solveBudget,
                math.min(contiguousNodeCount, _adaptiveSolveRemainingNodes));
            if (solveNodeCount <= 0)
            {
                solveStartNode = 0;
                solveNodeCount = math.min(solveBudget, _nodeCount);
            }

            _scheduledAdaptiveSolveSlice = true;
            _scheduledSolveNodeCount = solveNodeCount;
        }

        private static float ResolveEvaluationQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, PowerSolverConvergenceMath.AuthoritativeQualityWeight);

            return PowerSolverConvergenceMath.AuthoritativeQualityWeight;
        }

        private static int ResolveAdaptiveSolveNodesPerFrame(float globalQualityWeight)
        {
            float q = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
            float lowToMiddle = math.lerp(LowAdaptiveSolveNodesPerFrame, Mx350AdaptiveSolveNodesPerFrame, math.saturate(q * 3f));
            float middleToHigh = math.lerp(Mx350AdaptiveSolveNodesPerFrame, MidAdaptiveSolveNodesPerFrame, math.saturate((q - 0.33f) * 3f));
            float highToUltra = math.lerp(HighAdaptiveSolveNodesPerFrame, UltraAdaptiveSolveNodesPerFrame, math.saturate((q - 0.66f) * 3f));
            float lowerBand = math.lerp(lowToMiddle, middleToHigh, math.saturate((q - 0.20f) * 2.5f));
            return math.clamp((int)math.round(math.lerp(lowerBand, highToUltra, math.saturate((q - 0.55f) * 2.22f))), LowAdaptiveSolveNodesPerFrame, UltraAdaptiveSolveNodesPerFrame);
        }

        private void CommitUnpoweredEvaluation()
        {
            EnsureSummaryBuffers();
            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                _adaptiveSolveCursor = 0;
                _adaptiveSolveRemainingNodes = 0;
                _scheduledSolveNodeCount = 0;
                _scheduledAdaptiveSolveSlice = false;
                ResetUnpoweredRuntimeState();

                float totalConsumption = ComputeTotalConsumption();
                WriteNative(_scheduledTopologySummary, 0, new TopologySummary
                {
                    NodeCount = _nodeCount,
                    EdgeCount = _edgeCount,
                    IslandCount = _nodeCount > 0 ? 1 : 0,
                    BfsVisitedCount = 0,
                    ProducerReachableCount = 0
                });
                WriteNative(_scheduledDistributionSummary, 0, new DistributionSummary
                {
                    TotalGeneration = 0f,
                    TotalConsumption = totalConsumption,
                    Balance = -totalConsumption,
                    SupplyRatio = totalConsumption > Epsilon ? 0f : 1f,
                    ServedDemand = 0f,
                    UnservedDemand = totalConsumption,
                    PoweredCount = 0,
                    DisabledCount = ConsumerCount,
                    HasDeficit = totalConsumption > Epsilon ? (byte)1 : (byte)0,
                    BrownoutTier = totalConsumption > Epsilon
                        ? LogisticsBrownoutTier.EmergencyOnly
                        : LogisticsBrownoutTier.None
                });
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }

            CommitScheduledEvaluation();
        }

        private void CommitNoEdgeEvaluation()
        {
            EnsureSummaryBuffers();
            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                if (_runtimeConductiveEdgeCount.IsCreated)
                    WriteNative(_runtimeConductiveEdgeCount, 0, 0);
                _adaptiveSolveCursor = 0;
                _adaptiveSolveRemainingNodes = 0;
                _scheduledSolveNodeCount = 0;
                _scheduledAdaptiveSolveSlice = false;
                ResetNoEdgeRuntimeState();

                float totalGeneration = ComputeTotalGeneration();
                float totalConsumption = ComputeTotalConsumption();
                WriteNative(_scheduledTopologySummary, 0, new TopologySummary
                {
                    NodeCount = _nodeCount,
                    EdgeCount = _edgeCount,
                    IslandCount = _nodeCount
                });
                WriteNative(_scheduledDistributionSummary, 0, new DistributionSummary
                {
                    TotalGeneration = totalGeneration,
                    TotalConsumption = totalConsumption,
                    Balance = totalGeneration - totalConsumption,
                    SupplyRatio = totalConsumption > Epsilon ? 0f : 1f,
                    UnservedDemand = totalConsumption,
                    DisabledCount = ConsumerCount,
                    HasDeficit = totalConsumption > Epsilon ? (byte)1 : (byte)0,
                    BrownoutTier = totalConsumption > Epsilon
                        ? LogisticsBrownoutTier.EmergencyOnly
                        : LogisticsBrownoutTier.None
                });
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }

            CommitScheduledEvaluation();
        }

        private void ResetNoEdgeRuntimeState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.CurrentLoad = 0f;
                node.Potential = 0f;
                node.Flags &= ~(LogisticsNodeFlags.Brownout | LogisticsNodeFlags.Overloaded | LogisticsNodeFlags.Dirty);
                node.Flags |= LogisticsNodeFlags.Isolated;
                WriteNative(_nodeBuffer, nodeIndex, node);
                if (_powerCapacities.IsCreated && nodeIndex < _powerCapacities.Length)
                    WriteNative(_powerCapacities, nodeIndex, math.max(Epsilon, node.Capacity));
                if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                {
                    byte flags = ResolvePowerGridNodeFlags(node.Flags, node.Reserved);
                    if (HasProducer(nodeIndex))
                        flags |= (byte)PowerGridNodeFlags.Source;
                    flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                    WriteNative(_powerNodeFlags, nodeIndex, flags);
                }

                if (_nodeNetInjection.IsCreated && (uint)nodeIndex < (uint)_nodeNetInjection.Length)
                    WriteNative(_nodeNetInjection, nodeIndex, 0f);
                if (_nodeServedDemand.IsCreated && (uint)nodeIndex < (uint)_nodeServedDemand.Length)
                    WriteNative(_nodeServedDemand, nodeIndex, 0f);
                if (_nodeVoltageSupplyRatio.IsCreated && (uint)nodeIndex < (uint)_nodeVoltageSupplyRatio.Length)
                    WriteNative(_nodeVoltageSupplyRatio, nodeIndex, 0f);
                if (_nodeSourcePotential.IsCreated && (uint)nodeIndex < (uint)_nodeSourcePotential.Length)
                    WriteNative(_nodeSourcePotential, nodeIndex, 0f);
            }

            int consumerCount = _consumerCount;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                int nodeIndex = _consumers[consumerIndex].NodeIndex;
                if (!IsValidNodeIndex(nodeIndex))
                    continue;

                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Flags |= LogisticsNodeFlags.Brownout;
                WriteNative(_nodeBuffer, nodeIndex, node);
                if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                    WriteNative(_powerNodeFlags, nodeIndex, (byte)((_powerNodeFlags[nodeIndex] | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered));
            }

            int safeEdgeCount = math.min(_edgeCount, _edgeFlow.IsCreated ? _edgeFlow.Length : 0);
            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
                WriteNative(_edgeFlow, edgeIndex, 0f);
        }

        private void ResetUnpoweredRuntimeState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.CurrentLoad = 0f;
                node.Potential = 0f;
                node.Flags &= ~(LogisticsNodeFlags.Overloaded | LogisticsNodeFlags.Dirty);
                if (ConsumerCount > 0)
                    node.Flags |= LogisticsNodeFlags.Brownout;
                WriteNative(_nodeBuffer, nodeIndex, node);

                if (_powerCapacities.IsCreated && nodeIndex < _powerCapacities.Length)
                    WriteNative(_powerCapacities, nodeIndex, math.max(Epsilon, node.Capacity));
                if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                {
                    byte flags = ResolvePowerGridNodeFlags(node.Flags, node.Reserved);
                    flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                    WriteNative(_powerNodeFlags, nodeIndex, flags);
                }

                if (_nodeNetInjection.IsCreated && (uint)nodeIndex < (uint)_nodeNetInjection.Length)
                    WriteNative(_nodeNetInjection, nodeIndex, 0f);
                if (_nodeServedDemand.IsCreated && (uint)nodeIndex < (uint)_nodeServedDemand.Length)
                    WriteNative(_nodeServedDemand, nodeIndex, 0f);
                if (_nodeVoltageSupplyRatio.IsCreated && (uint)nodeIndex < (uint)_nodeVoltageSupplyRatio.Length)
                    WriteNative(_nodeVoltageSupplyRatio, nodeIndex, 0f);
                if (_nodeSourcePotential.IsCreated && (uint)nodeIndex < (uint)_nodeSourcePotential.Length)
                    WriteNative(_nodeSourcePotential, nodeIndex, 0f);
                if (_potentialFront.IsCreated && (uint)nodeIndex < (uint)_potentialFront.Length)
                    WriteNative(_potentialFront, nodeIndex, 0f);
                if (_potentialBack.IsCreated && (uint)nodeIndex < (uint)_potentialBack.Length)
                    WriteNative(_potentialBack, nodeIndex, 0f);
            }

            int safeEdgeCount = math.min(_edgeCount, _edgeFlow.IsCreated ? _edgeFlow.Length : 0);
            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
                WriteNative(_edgeFlow, edgeIndex, 0f);
        }

        public void CompleteEvaluation()
        {
            TryCompleteEvaluation();
        }

        /// <summary>
        /// Completes the scheduled graph evaluation only when the job is already finished.
        /// </summary>
        /// <returns>True when no evaluation is pending or the pending evaluation was completed.</returns>
        public bool TryCompleteEvaluation()
        {
            if (!_evaluateGraphPending)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _evaluateGraphJobHandle, forceComplete: false))
                return false;

            _evaluateGraphJobHandle = default;
            _evaluateGraphPending = false;
            if (_scheduledAdaptiveSolveSlice)
            {
                int completedNodeCount = math.max(0, _scheduledSolveNodeCount);
                _adaptiveSolveRemainingNodes = math.max(0, _adaptiveSolveRemainingNodes - completedNodeCount);
                _adaptiveSolveCursor += completedNodeCount;
                if (_adaptiveSolveCursor >= _nodeCount)
                    _adaptiveSolveCursor = 0;

                int runtimeConductiveEdgeCount = _runtimeConductiveEdgeCount.IsCreated
                    ? _runtimeConductiveEdgeCount[0]
                    : _conductiveEdgeCount;
                if (runtimeConductiveEdgeCount <= 0)
                    _adaptiveSolveRemainingNodes = 0;

                if (_adaptiveSolveRemainingNodes > 0 && completedNodeCount > 0)
                {
                    ScheduleEvaluationSlice(relaxationSliceOnly: true);
                    return false;
                }

                _adaptiveSolveRemainingNodes = 0;
                _scheduledSolveNodeCount = 0;
                _scheduledAdaptiveSolveSlice = false;
            }

            CommitScheduledEvaluation();
            return true;
        }

        private void CommitScheduledEvaluation()
        {
            _committedTopologySummary = _scheduledTopologySummary.IsCreated && _scheduledTopologySummary.Length > 0
                ? _scheduledTopologySummary[0]
                : default;

            _committedDistributionSummary = _scheduledDistributionSummary.IsCreated && _scheduledDistributionSummary.Length > 0
                ? _scheduledDistributionSummary[0]
                : new DistributionSummary
                {
                    SupplyRatio = 1f,
                    BrownoutTier = LogisticsBrownoutTier.None
                };

            WritePowerBlackBoxSample(0u);
        }

        private bool TryResolvePowerBlackBox(out NativeArray<PowerTelemetryEntry> ring, out NativeArray<PowerGridCounter64> cursor)
        {
            ring = default;
            cursor = default;
            IDataVault vault = _powerBlackBoxVault;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            if (_powerBlackBoxHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _powerBlackBoxHandle, out ring) ||
                !ring.IsCreated ||
                ring.Length < PowerBlackBoxCapacity)
            {
                if (!vault.TryGetGenerationHandle<PowerTelemetryEntry>(
                        PowerGridBufferIds.TelemetryRing,
                        out _powerBlackBoxHandle) ||
                    _powerBlackBoxHandle.BufferID == 0u ||
                    !vault.TryResolveHandle(in _powerBlackBoxHandle, out ring) ||
                    !ring.IsCreated ||
                    ring.Length < PowerBlackBoxCapacity)
                {
                    return false;
                }
            }

            if (_powerBlackBoxCursorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in _powerBlackBoxCursorHandle, out cursor) ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
            {
                if (!vault.TryGetGenerationHandle<PowerGridCounter64>(
                        PowerGridBufferIds.TelemetryCursor,
                        out _powerBlackBoxCursorHandle) ||
                    _powerBlackBoxCursorHandle.BufferID == 0u ||
                    !vault.TryResolveHandle(in _powerBlackBoxCursorHandle, out cursor) ||
                    !cursor.IsCreated ||
                    cursor.Length <= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryAcquirePowerBlackBoxWriteLock(
            out NativeArray<PowerTelemetryEntry> ring,
            out NativeArray<PowerGridCounter64> cursor,
            out byte lockMask)
        {
            ring = default;
            cursor = default;
            lockMask = 0;
            IDataVault vault = _powerBlackBoxVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (_powerBlackBoxHandle.BufferID == 0u &&
                (!vault.TryGetGenerationHandle<PowerTelemetryEntry>(PowerGridBufferIds.TelemetryRing, out _powerBlackBoxHandle) ||
                 _powerBlackBoxHandle.BufferID == 0u))
            {
                return false;
            }

            if (_powerBlackBoxCursorHandle.BufferID == 0u &&
                (!vault.TryGetGenerationHandle<PowerGridCounter64>(PowerGridBufferIds.TelemetryCursor, out _powerBlackBoxCursorHandle) ||
                 _powerBlackBoxCursorHandle.BufferID == 0u))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _powerBlackBoxHandle, SystemID.Power, out ring) ||
                !ring.IsCreated ||
                ring.Length < PowerBlackBoxCapacity)
            {
                ring = default;
                return false;
            }

            lockMask |= 1;
            if (vault.IsCompactionFenceActive)
            {
                ReleasePowerBlackBoxWriteLock(lockMask);
                ring = default;
                return false;
            }

            if (!vault.TryAcquireWriteLock(in _powerBlackBoxCursorHandle, SystemID.Power, out cursor) ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
            {
                ReleasePowerBlackBoxWriteLock(lockMask);
                ring = default;
                cursor = default;
                lockMask = 0;
                return false;
            }

            lockMask |= 2;
            return true;
        }

        private void ReleasePowerBlackBoxWriteLock(byte lockMask)
        {
            IDataVault vault = _powerBlackBoxVault;
            if (vault == null)
                return;

            if ((lockMask & 2) != 0)
                vault.ReleaseWriteLock(in _powerBlackBoxCursorHandle, SystemID.Power);
            if ((lockMask & 1) != 0)
                vault.ReleaseWriteLock(in _powerBlackBoxHandle, SystemID.Power);
        }

        private void WritePowerBlackBoxSample(uint reasonFlags)
        {
            if (!TryAcquirePowerBlackBoxWriteLock(
                    out NativeArray<PowerTelemetryEntry> powerBlackBox,
                    out NativeArray<PowerGridCounter64> powerBlackBoxCursor,
                    out byte blackBoxLockMask))
                return;

            uint flags = reasonFlags;
            bool dumpRequired = false;
            try
            {
                int runtimeEdgeCount = _runtimeConductiveEdgeCount.IsCreated && _runtimeConductiveEdgeCount.Length > 0
                    ? _runtimeConductiveEdgeCount[0]
                    : _conductiveEdgeCount;
                if (runtimeEdgeCount <= 0)
                    flags |= PowerBlackBoxNoConductiveEdgesFlag;
                if (_baseAwakeStateValue == 0)
                {
                    flags |= PowerBlackBoxHibernatingFlag;
                }

                float minPotential = 0f;
                float maxPotential = 0f;
                bool hasPotential = false;
                int brownoutCount = 0;
                int overloadedCount = 0;
                uint stateHash = 2166136261u;
                stateHash = HashPowerBlackBox(stateHash, (uint)_nodeCount);
                stateHash = HashPowerBlackBox(stateHash, (uint)_edgeCount);
                stateHash = HashPowerBlackBox(stateHash, (uint)math.max(0, runtimeEdgeCount));
                stateHash = HashPowerBlackBox(stateHash, QuantizePowerBlackBoxFloat(_committedDistributionSummary.TotalGeneration));
                stateHash = HashPowerBlackBox(stateHash, QuantizePowerBlackBoxFloat(_committedDistributionSummary.TotalConsumption));
                stateHash = HashPowerBlackBox(stateHash, QuantizePowerBlackBoxFloat(_committedDistributionSummary.SupplyRatio));

            int safeNodeCount = _nodeBuffer.IsCreated
                ? math.min(_nodeCount, _nodeBuffer.Length)
                : 0;
            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                float potential = node.Potential;
                float netInjection = _nodeNetInjection.IsCreated && nodeIndex < _nodeNetInjection.Length
                    ? _nodeNetInjection[nodeIndex]
                    : 0f;
                float supplyRatio = _nodeVoltageSupplyRatio.IsCreated && nodeIndex < _nodeVoltageSupplyRatio.Length
                    ? _nodeVoltageSupplyRatio[nodeIndex]
                    : 0f;
                byte powerFlags = _powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length
                    ? _powerNodeFlags[nodeIndex]
                    : (byte)0;

                if (!math.isfinite(potential) ||
                    !math.isfinite(netInjection) ||
                    !math.isfinite(supplyRatio))
                {
                    flags |= PowerBlackBoxNonFiniteFlag;
                    potential = math.isfinite(potential) ? potential : 0f;
                    netInjection = math.isfinite(netInjection) ? netInjection : 0f;
                    supplyRatio = math.isfinite(supplyRatio) ? supplyRatio : 0f;
                }

                if (!hasPotential)
                {
                    minPotential = potential;
                    maxPotential = potential;
                    hasPotential = true;
                }
                else
                {
                    minPotential = math.min(minPotential, potential);
                    maxPotential = math.max(maxPotential, potential);
                }

                if ((node.Flags & LogisticsNodeFlags.Brownout) != 0 ||
                    (powerFlags & (byte)PowerGridNodeFlags.Offline) != 0)
                {
                    brownoutCount++;
                }

                if ((node.Flags & LogisticsNodeFlags.Overloaded) != 0 ||
                    (powerFlags & (byte)PowerGridNodeFlags.Overloaded) != 0)
                {
                    overloadedCount++;
                }

                stateHash = HashPowerBlackBox(stateHash, node.Id);
                stateHash = HashPowerBlackBox(stateHash, (uint)node.Flags);
                stateHash = HashPowerBlackBox(stateHash, powerFlags);
                stateHash = HashPowerBlackBox(stateHash, QuantizePowerBlackBoxFloat(potential));
                stateHash = HashPowerBlackBox(stateHash, QuantizePowerBlackBoxFloat(netInjection));
                stateHash = HashPowerBlackBox(stateHash, QuantizePowerBlackBoxFloat(supplyRatio));
            }

            if (brownoutCount > 0)
                flags |= PowerBlackBoxBrownoutFlag;
            if (overloadedCount > 0)
                flags |= PowerBlackBoxOverloadFlag;

            PowerGridCounter64 cursorState = powerBlackBoxCursor[0];
            int cursor = cursorState.Value;
            if ((uint)cursor >= (uint)powerBlackBox.Length)
                cursor = 0;
            uint frameIndex = unchecked(cursorState.Flags + 1u);
            if (frameIndex == 0u)
                frameIndex = 1u;

            PowerTelemetryEntry entry = default;
            entry.FrameIndex = frameIndex;
            entry.StateHash = stateHash;
            entry.ReasonFlags = flags;
            entry.NodeCount = _committedTopologySummary.NodeCount;
            entry.EdgeCount = _committedTopologySummary.EdgeCount;
            entry.RuntimeEdgeCount = runtimeEdgeCount;
            entry.SolveStartNode = _lastPowerBlackBoxSolveStartNode;
            entry.SolveNodeCount = _lastPowerBlackBoxSolveNodeCount;
            entry.TotalGeneration = _committedDistributionSummary.TotalGeneration;
            entry.TotalConsumption = _committedDistributionSummary.TotalConsumption;
            entry.SupplyRatio = _committedDistributionSummary.SupplyRatio;
            entry.Balance = _committedDistributionSummary.Balance;
            entry.MinPotential = hasPotential ? minPotential : 0f;
            entry.MaxPotential = hasPotential ? maxPotential : 0f;
            entry.BrownoutCount = brownoutCount;
            entry.OverloadedCount = overloadedCount;
            powerBlackBox[cursor] = entry;

            int nextCursor = (cursor + 1) % powerBlackBox.Length;
            cursorState.Value = nextCursor;
            cursorState.Flags = frameIndex;
            powerBlackBoxCursor[0] = cursorState;
                if ((flags & PowerBlackBoxNonFiniteFlag) != 0)
                    dumpRequired = true;
            }
            finally
            {
                ReleasePowerBlackBoxWriteLock(blackBoxLockMask);
            }

            if (dumpRequired)
                DumpPowerBlackBoxOnce(flags);
        }

        private void DumpPowerBlackBoxOnce(uint reasonFlags)
        {
            if (_powerBlackBoxDumped ||
                !TryResolvePowerBlackBox(out NativeArray<PowerTelemetryEntry> powerBlackBox, out NativeArray<PowerGridCounter64> powerBlackBoxCursor))
            {
                return;
            }

            _powerBlackBoxDumped = true;
            int cursor = powerBlackBoxCursor[0].Value;
            if ((uint)cursor >= (uint)powerBlackBox.Length)
                cursor = 0;
            DumpPowerBlackBox(reasonFlags, powerBlackBox, cursor);
        }

        private void DumpPowerBlackBox(uint reasonFlags, NativeArray<PowerTelemetryEntry> powerBlackBox, int cursor)
        {
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", PowerBlackBoxDumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read)))
                {
                    writer.Write(PowerBlackBoxMagic);
                    writer.Write(PowerBlackBoxVersion);
                    writer.Write((uint)PowerBlackBoxCapacity);
                    writer.Write((uint)Marshal.SizeOf<PowerTelemetryEntry>());
                    writer.Write((uint)cursor);
                    writer.Write(reasonFlags);
                    for (int entryOffset = 0; entryOffset < powerBlackBox.Length; entryOffset++)
                    {
                        int entryIndex = (cursor + entryOffset) % powerBlackBox.Length;
                        WritePowerBlackBoxEntry(writer, powerBlackBox[entryIndex]);
                    }
                }
            }
            catch (IOException)
            {
                Hecton8.Core.H8Debug.LogError("Power grid black-box dump failed.");
            }
            catch (UnauthorizedAccessException)
            {
                Hecton8.Core.H8Debug.LogError("Power grid black-box dump failed.");
            }
            catch (ArgumentException)
            {
                Hecton8.Core.H8Debug.LogError("Power grid black-box dump failed.");
            }
            catch (NotSupportedException)
            {
                Hecton8.Core.H8Debug.LogError("Power grid black-box dump failed.");
            }
        }

        private static void WritePowerBlackBoxEntry(BinaryWriter writer, PowerTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.StateHash);
            writer.Write(entry.ReasonFlags);
            writer.Write(entry.NodeCount);
            writer.Write(entry.EdgeCount);
            writer.Write(entry.RuntimeEdgeCount);
            writer.Write(entry.SolveStartNode);
            writer.Write(entry.SolveNodeCount);
            writer.Write(entry.TotalGeneration);
            writer.Write(entry.TotalConsumption);
            writer.Write(entry.SupplyRatio);
            writer.Write(entry.Balance);
            writer.Write(entry.MinPotential);
            writer.Write(entry.MaxPotential);
            writer.Write(entry.BrownoutCount);
            writer.Write(entry.OverloadedCount);
        }

        private static uint QuantizePowerBlackBoxFloat(float value)
        {
            if (!math.isfinite(value))
                return 0x7FC00000u;

            float clamped = math.clamp(value, -1000000f, 1000000f);
            return (uint)(int)math.round(clamped * 1000f);
        }

        private static uint HashPowerBlackBox(uint hash, uint value)
        {
            unchecked
            {
                return (hash ^ value) * 16777619u;
            }
        }

        public TopologySummary GetScheduledTopologySummary()
        {
            return _committedTopologySummary;
        }

        public DistributionSummary GetScheduledDistributionSummary()
        {
            return _committedDistributionSummary;
        }

        public void CompleteNodeStatePublish()
        {
            TryCompleteNodeStatePublish();
        }

        /// <summary>
        /// Completes the node-state publish job only when the job is already finished.
        /// </summary>
        /// <returns>True when no publish job is pending or the pending publish job was completed.</returns>
        public bool TryCompleteNodeStatePublish()
        {
            if (!_publishNodeStatesPending)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _publishNodeStatesJobHandle, forceComplete: false))
                return false;

            _publishNodeStatesJobHandle = default;
            _publishNodeStatesPending = false;
            SwapPublishedNodeStateBuffers();
            return true;
        }

        private void SwapPublishedNodeStateBuffers()
        {
            VaultGenerationHandle<ushort> statesSwap = _publishedNodeStatesHandle;
            _publishedNodeStatesHandle = _publishedNodeStatesBackHandle;
            _publishedNodeStatesBackHandle = statesSwap;
        }

        public void PublishNodeStateSynchronously()
        {
            if (_publishNodeStatesPending && !TryCompleteNodeStatePublish())
                return;

            PublishNodeStatesMainThread();
        }

        public void ClearPublishedNodeStates()
        {
            if (_publishNodeStatesPending && !TryCompleteNodeStatePublish())
                return;

            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                ClearNativeArray(_publishedNodeStates);
                ClearNativeArray(_publishedNodeStatesBack);
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        public bool TryGetPublishedNodeState(uint nodeId, out ushort stateBits)
        {
            if (_publishNodeStatesPending)
            {
                stateBits = 0;
                return false;
            }

            NativeArray<ushort> states = _publishedNodeStates;
            NativeArray<LogisticsNode> nodes = _nodeBuffer;
            if (!states.IsCreated || !nodes.IsCreated)
            {
                stateBits = 0;
                return false;
            }

            int safeNodeCount = math.min(_nodeCount, math.min(nodes.Length, states.Length));
            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
            {
                if (nodes[nodeIndex].Id != nodeId)
                    continue;

                stateBits = states[nodeIndex];
                return stateBits != 0;
            }

            stateBits = 0;
            return false;
        }

        public int TraverseReachableFrom(int startNodeIndex)
        {
            return TraverseReachableFromInternal(startNodeIndex);
        }

        private float ComputeTotalGeneration()
        {
            float totalGeneration = 0f;
            int producerCount = math.min(_producerCount, _producers.IsCreated ? _producers.Length : 0);
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                if (TryReadProducerRate(nodeIndex, out float productionRate))
                    totalGeneration += productionRate;
            }

            return totalGeneration;
        }

        private float ComputeTotalConsumption()
        {
            float totalConsumption = 0f;
            int consumerCount = math.min(_consumerCount, _consumers.IsCreated ? _consumers.Length : 0);
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
                totalConsumption += _consumers[consumerIndex].Demand;

            return totalConsumption;
        }

        private bool TryReadProducerRate(int nodeIndex, out float productionRate)
        {
            productionRate = 0f;
            NativeArray<float> producerRates = _producerRates;
            if (!producerRates.IsCreated || (uint)nodeIndex >= (uint)producerRates.Length)
                return false;

            productionRate = producerRates[nodeIndex];
            return productionRate > Epsilon && math.isfinite(productionRate);
        }

        private bool HasProducer(int nodeIndex)
        {
            return TryReadProducerRate(nodeIndex, out _);
        }

        private void ResetDistributionState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.CurrentLoad = 0f;
                node.Potential = 0f;
                node.Flags &= ~(LogisticsNodeFlags.Brownout | LogisticsNodeFlags.Overloaded | LogisticsNodeFlags.Dirty);
                WriteNative(_nodeBuffer, nodeIndex, node);
                if (_powerCapacities.IsCreated && nodeIndex < _powerCapacities.Length)
                    WriteNative(_powerCapacities, nodeIndex, math.max(Epsilon, node.Capacity));
                if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                    WriteNative(_powerNodeFlags, nodeIndex, ResolvePowerGridNodeFlags(node.Flags, node.Reserved));
                WriteNative(_nodeNetInjection, nodeIndex, 0f);
                WriteNative(_nodeServedDemand, nodeIndex, 0f);
                WriteNative(_nodeVoltageSupplyRatio, nodeIndex, 1f);
                WriteNative(_nodeSourcePotential, nodeIndex, 0f);
                WriteNative(_componentGeneration, nodeIndex, 0f);
                WriteNative(_componentDemand, nodeIndex, 0f);
                WriteNative(_componentServedDemand, nodeIndex, 0f);
                WriteNative(_componentRemainingSupply, nodeIndex, 0f);
                WriteNative(_componentSupplyRatio, nodeIndex, 1f);
                WriteNative(_componentResidualInjection, nodeIndex, 0f);
                WriteNative(_componentAnchorNode, nodeIndex, -1);
                WriteNative(_componentBrownoutTier, nodeIndex, (byte)LogisticsBrownoutTier.None);
            }
        }

        private void BuildComponentDistributionState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                int componentIndex = _componentIds[nodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                if (_componentAnchorNode[componentIndex] < 0)
                    WriteNative(_componentAnchorNode, componentIndex, nodeIndex);

                if (TryReadProducerRate(nodeIndex, out float productionRate))
                {
                    AddNative(_componentGeneration, componentIndex, productionRate);
                    WriteNative(_nodeSourcePotential, nodeIndex, 1f);
                    if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                        WriteNative(_powerNodeFlags, nodeIndex, (byte)(_powerNodeFlags[nodeIndex] | (byte)(PowerGridNodeFlags.Source | PowerGridNodeFlags.Powered)));
                }
            }

            int consumerCount = math.min(_consumerCount, _consumers.IsCreated ? _consumers.Length : 0);
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                ConsumerRecord consumer = _consumers[consumerIndex];
                int componentIndex = _componentIds[consumer.NodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                AddNative(_componentDemand, componentIndex, consumer.Demand);
            }

            for (int componentIndex = 0; componentIndex < _nodeCount; componentIndex++)
            {
                if (_componentAnchorNode[componentIndex] < 0)
                    continue;

                float demand = _componentDemand[componentIndex];
                float generation = _componentGeneration[componentIndex];
                float supplyRatio = demand > Epsilon
                    ? math.saturate(generation * math.rcp(demand))
                    : 1f;

                WriteNative(_componentRemainingSupply, componentIndex, generation);
                WriteNative(_componentSupplyRatio, componentIndex, supplyRatio);
                WriteNative(_componentBrownoutTier, componentIndex, (byte)ResolveBrownoutTier(supplyRatio));
            }
        }

        private void AllocateServedDemand(ref DistributionSummary summary)
        {
            int consumerCount = ConsumerCount;
            if (consumerCount <= 0)
                return;

            int poweredCount = 0;
            float servedDemand = 0f;
            bool globalDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption;

            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                ConsumerRecord consumer = _consumers[consumerIndex];
                int nodeIndex = consumer.NodeIndex;
                if (!IsValidNodeIndex(nodeIndex))
                {
                    continue;
                }

                int componentIndex = _componentIds[nodeIndex];
                bool componentCanServe =
                    !globalDeficit &&
                    componentIndex >= 0 &&
                    componentIndex < _nodeCount &&
                    _componentGeneration[componentIndex] > Epsilon &&
                    _componentGeneration[componentIndex] + Epsilon >= _componentDemand[componentIndex] &&
                    CanServeConsumer(in consumer);

                if (!componentCanServe)
                {
                    MarkBrownoutNode(nodeIndex);
                    continue;
                }

                WriteNative(_componentRemainingSupply, componentIndex, math.max(0f, _componentRemainingSupply[componentIndex] - consumer.Demand));
                AddNative(_componentServedDemand, componentIndex, consumer.Demand);
                AddNative(_nodeServedDemand, nodeIndex, consumer.Demand);
                WriteNative(_nodeVoltageSupplyRatio, nodeIndex, 1f);
                WriteNative(_consumerStates, consumerIndex, (byte)1);
                poweredCount++;
                servedDemand += consumer.Demand;
            }

            summary.PoweredCount = poweredCount;
            summary.DisabledCount = consumerCount - poweredCount;
            summary.ServedDemand = servedDemand;
        }

        private void BuildNodeInjection()
        {
            int producerCount = math.min(_producerCount, _producers.IsCreated ? _producers.Length : 0);
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                int componentIndex = _componentIds[nodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                if (!TryReadProducerRate(nodeIndex, out float productionRate))
                    continue;

                float componentGeneration = _componentGeneration[componentIndex];
                float componentServedDemand = _componentServedDemand[componentIndex];
                if (componentGeneration <= Epsilon || componentServedDemand <= Epsilon)
                    continue;

                float dispatchedGeneration = productionRate * math.saturate(componentServedDemand * math.rcp(componentGeneration));
                AddNative(_nodeNetInjection, nodeIndex, dispatchedGeneration);
            }

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                AddNative(_nodeNetInjection, nodeIndex, -_nodeServedDemand[nodeIndex]);
                int componentIndex = _componentIds[nodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                AddNative(_componentResidualInjection, componentIndex, _nodeNetInjection[nodeIndex]);
            }

            for (int componentIndex = 0; componentIndex < _nodeCount; componentIndex++)
            {
                int anchorNodeIndex = _componentAnchorNode[componentIndex];
                if (!IsValidNodeIndex(anchorNodeIndex))
                    continue;

                float residual = _componentResidualInjection[componentIndex];
                if (math.abs(residual) > Epsilon)
                    AddNative(_nodeNetInjection, anchorNodeIndex, -residual);
            }
        }

        private void ApplyBinaryNodeLoads()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                int componentIndex = _componentIds[nodeIndex];
                bool componentPowered =
                    componentIndex >= 0 &&
                    componentIndex < _nodeCount &&
                    _componentGeneration[componentIndex] > Epsilon &&
                    _componentGeneration[componentIndex] + Epsilon >= _componentDemand[componentIndex];

                if (!componentPowered && _nodeServedDemand[nodeIndex] > Epsilon)
                    node.Flags |= LogisticsNodeFlags.Brownout;

                float generatedWatts = math.max(0f, _nodeNetInjection[nodeIndex]);
                float consumedWatts = _nodeServedDemand[nodeIndex];
                node.Potential = componentPowered ? 1f : 0f;
                node.CurrentLoad = math.max(generatedWatts, consumedWatts);
                WriteNative(_nodeVoltageSupplyRatio, nodeIndex, componentPowered ? 1f : 0f);

                if (node.CurrentLoad > node.Capacity * 1.15f)
                    node.Flags |= LogisticsNodeFlags.Overloaded;

                WriteNative(_nodeBuffer, nodeIndex, node);
                if (_powerCapacities.IsCreated && nodeIndex < _powerCapacities.Length)
                    WriteNative(_powerCapacities, nodeIndex, math.max(Epsilon, node.Capacity));
                if (_powerNodeFlags.IsCreated && nodeIndex < _powerNodeFlags.Length)
                {
                    byte flags = ResolvePowerGridNodeFlags(node.Flags, node.Reserved);
                    if (componentPowered)
                        flags = (byte)((flags | (byte)PowerGridNodeFlags.Powered) & ~(byte)PowerGridNodeFlags.Offline);
                    else
                        flags = (byte)((flags | (byte)PowerGridNodeFlags.Offline) & ~(byte)PowerGridNodeFlags.Powered);
                    if (_nodeSourcePotential[nodeIndex] > Epsilon)
                        flags |= (byte)PowerGridNodeFlags.Source;
                    WriteNative(_powerNodeFlags, nodeIndex, flags);
                }
            }
        }

        private void ResetTopologyScratch(int nodeCount)
        {
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                WriteNative(_parents, nodeIndex, nodeIndex);
                WriteNative(_ranks, nodeIndex, 0);
                WriteNative(_componentIds, nodeIndex, -1);
                WriteNative(_componentSizes, nodeIndex, 0);
                WriteNative(_rootToComponent, nodeIndex, -1);
                WriteNative(_visited, nodeIndex, (byte)0);
            }
        }

        private void ResetConsumerStates(int consumerCount)
        {
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
                WriteNative(_consumerStates, consumerIndex, (byte)0);
        }

        private void ClearNodeFlags(LogisticsNodeFlags flags)
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Flags &= ~flags;
                WriteNative(_nodeBuffer, nodeIndex, node);
            }
        }

        private void MarkIsolatedNodesFromVisited()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                if (_visited[nodeIndex] == 0)
                    node.Flags |= LogisticsNodeFlags.Isolated;
                else
                    node.Flags &= ~LogisticsNodeFlags.Isolated;

                WriteNative(_nodeBuffer, nodeIndex, node);
            }
        }

        private void MarkBrownoutNode(int nodeIndex)
        {
            if (!IsValidNodeIndex(nodeIndex))
                return;

            LogisticsNode node = _nodeBuffer[nodeIndex];
            node.Flags |= LogisticsNodeFlags.Brownout;
            WriteNative(_nodeBuffer, nodeIndex, node);
        }

        private int TraverseReachableFromInternal(int startNodeIndex)
        {
            if (!IsValidNodeIndex(startNodeIndex))
                return 0;

            int searchBudget = math.min(_nodeCount, MaxSearchDepth);
            if (searchBudget <= 0)
                return 0;

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                WriteNative(_visited, nodeIndex, (byte)0);

            int head = 0;
            int tail = 0;
            WriteNative(_visited, startNodeIndex, (byte)1);
            WriteNative(_traversalQueue, tail++, startNodeIndex);

            int visitedCount = 0;
            while (head < tail && visitedCount < searchBudget)
            {
                int nodeIndex = _traversalQueue[head++];
                visitedCount++;

                int edgeStart = _edgeOffsets[nodeIndex];
                int edgeEnd = _edgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    if (IsEdgeRuptured(edgeIndex))
                        continue;

                    int destinationNodeIndex = _edgeDestinations[edgeIndex];
                    if (!IsValidNodeIndex(destinationNodeIndex) || _visited[destinationNodeIndex] != 0)
                        continue;

                    if (tail >= searchBudget)
                        continue;

                    WriteNative(_visited, destinationNodeIndex, (byte)1);
                    WriteNative(_traversalQueue, tail++, destinationNodeIndex);
                }
            }

            return visitedCount;
        }

        private int TraverseProducerReachability()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                WriteNative(_visited, nodeIndex, (byte)0);

            int searchBudget = math.min(_nodeCount, MaxSearchDepth);
            if (searchBudget <= 0)
                return 0;

            int head = 0;
            int tail = 0;

            int producerCount = math.min(_producerCount, _producers.IsCreated ? _producers.Length : 0);
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                if (!IsValidNodeIndex(nodeIndex) || _visited[nodeIndex] != 0)
                    continue;

                if (tail >= searchBudget)
                    break;

                WriteNative(_visited, nodeIndex, (byte)1);
                WriteNative(_traversalQueue, tail++, nodeIndex);
            }

            int visitedCount = 0;
            while (head < tail && visitedCount < searchBudget)
            {
                int sourceNodeIndex = _traversalQueue[head++];
                visitedCount++;

                int edgeStart = _edgeOffsets[sourceNodeIndex];
                int edgeEnd = _edgeOffsets[sourceNodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    if (IsEdgeRuptured(edgeIndex))
                        continue;

                    int destinationNodeIndex = _edgeDestinations[edgeIndex];
                    if (!IsValidNodeIndex(destinationNodeIndex))
                        continue;

                    if (_visited[destinationNodeIndex] != 0)
                        continue;

                    if (tail >= searchBudget)
                        continue;

                    WriteNative(_visited, destinationNodeIndex, (byte)1);
                    WriteNative(_traversalQueue, tail++, destinationNodeIndex);
                }
            }

            return visitedCount;
        }

        private float ResolveEdgeConductanceForBuild(int sourceNodeIndex, int destinationNodeIndex, float edgeResistance)
        {
            LogisticsNode sourceNode = _nodeBuffer[sourceNodeIndex];
            LogisticsNode destinationNode = _nodeBuffer[destinationNodeIndex];
            float combinedResistance = math.max(
                MinResistance,
                edgeResistance + sourceNode.Resistance + destinationNode.Resistance);
            return math.rcp(combinedResistance);
        }

        private float ResolveEdgeCapacityForBuild(int sourceNodeIndex, int destinationNodeIndex)
        {
            LogisticsNode sourceNode = _nodeBuffer[sourceNodeIndex];
            LogisticsNode destinationNode = _nodeBuffer[destinationNodeIndex];
            return math.max(Epsilon, math.min(sourceNode.Capacity, destinationNode.Capacity));
        }

        private static byte ResolvePowerGridNodeFlags(LogisticsNodeFlags flags, byte reservedState)
        {
            PowerGridNodeFlags nodeFlags = PowerGridNodeFlags.None;
            LogisticsModuleStatusBits moduleStatus = (LogisticsModuleStatusBits)reservedState;

            if ((moduleStatus & LogisticsModuleStatusBits.Powered) != 0)
                nodeFlags |= PowerGridNodeFlags.Powered;
            if ((flags & LogisticsNodeFlags.Overloaded) != 0 ||
                (moduleStatus & LogisticsModuleStatusBits.Overheating) != 0)
                nodeFlags |= PowerGridNodeFlags.Overloaded;
            if ((flags & LogisticsNodeFlags.Ruptured) != 0 ||
                (moduleStatus & LogisticsModuleStatusBits.Damaged) != 0)
                nodeFlags |= PowerGridNodeFlags.Damaged;
            if ((flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Brownout)) != 0)
                nodeFlags |= PowerGridNodeFlags.Offline;
            if ((moduleStatus & LogisticsModuleStatusBits.Flooded) != 0)
                nodeFlags |= PowerGridNodeFlags.Flooded;

            return (byte)nodeFlags;
        }

        private bool CanServeConsumer(in ConsumerRecord consumer)
        {
            if (!IsValidNodeIndex(consumer.NodeIndex))
                return false;

            LogisticsNode node = _nodeBuffer[consumer.NodeIndex];
            if ((node.Flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Ruptured)) != 0)
                return false;

            return true;
        }

        private int FindRoot(int nodeIndex)
        {
            int parentIndex = _parents[nodeIndex];
            int rootWatchdog = math.max(1, _nodeCount + 1);
            while (parentIndex != _parents[parentIndex] && rootWatchdog-- > 0)
            {
                WriteNative(_parents, parentIndex, _parents[_parents[parentIndex]]);
                parentIndex = _parents[parentIndex];
            }

            if (rootWatchdog <= 0)
            {
                WriteNative(_parents, nodeIndex, nodeIndex);
                return nodeIndex;
            }

            int currentIndex = nodeIndex;
            int compressionWatchdog = math.max(1, _nodeCount + 1);
            while (currentIndex != parentIndex && compressionWatchdog-- > 0)
            {
                int nextIndex = _parents[currentIndex];
                WriteNative(_parents, currentIndex, parentIndex);
                currentIndex = nextIndex;
            }

            if (compressionWatchdog <= 0)
            {
                WriteNative(_parents, nodeIndex, nodeIndex);
                return nodeIndex;
            }

            return parentIndex;
        }

        private void UnionRoots(int leftRoot, int rightRoot)
        {
            if (_ranks[leftRoot] < _ranks[rightRoot])
            {
                WriteNative(_parents, leftRoot, rightRoot);
                return;
            }

            if (_ranks[leftRoot] > _ranks[rightRoot])
            {
                WriteNative(_parents, rightRoot, leftRoot);
                return;
            }

            WriteNative(_parents, rightRoot, leftRoot);
            AddNative(_ranks, leftRoot, 1);
        }

        private bool IsValidNodeIndex(int nodeIndex)
        {
            return nodeIndex >= 0 && nodeIndex < _nodeCount;
        }

        private bool IsEdgeRuptured(int edgeIndex)
        {
            return _edgeStates.IsCreated &&
                   (uint)edgeIndex < (uint)_edgeStates.Length &&
                   (_edgeStates[edgeIndex] & (byte)LogisticsEdgeState.Ruptured) != 0;
        }

        private void PublishNodeStatesMainThread()
        {
            if (_nodeCount <= 0)
            {
                ClearPublishedNodeStates();
                return;
            }

            EnsurePublishedStateCapacity(_nodeCount);
            if (!TryLockGraphMutationBuffers(out ulong lockedMask))
                return;

            try
            {
                ClearNativeArray(_publishedNodeStates);

                for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                {
                    LogisticsNode node = _nodeBuffer[nodeIndex];
                    LogisticsNodeStateBits stateBits = LogisticsNodeStateBits.None;

                    if ((node.Flags & LogisticsNodeFlags.Active) != 0)
                        stateBits |= LogisticsNodeStateBits.Active;

                    if ((node.Flags & LogisticsNodeFlags.Isolated) == 0)
                        stateBits |= LogisticsNodeStateBits.Reachable;

                    if ((node.Flags & LogisticsNodeFlags.Brownout) != 0)
                        stateBits |= LogisticsNodeStateBits.Brownout;

                    if (_nodeServedDemand[nodeIndex] > Epsilon &&
                        _nodeVoltageSupplyRatio[nodeIndex] <= Epsilon)
                    {
                        stateBits |= LogisticsNodeStateBits.Brownout;
                    }

                    if ((node.Flags & LogisticsNodeFlags.Isolated) != 0)
                        stateBits |= LogisticsNodeStateBits.Isolated;

                    if ((node.Flags & LogisticsNodeFlags.Overloaded) != 0)
                        stateBits |= LogisticsNodeStateBits.Overloaded;

                    if ((node.Flags & LogisticsNodeFlags.Ruptured) != 0)
                        stateBits |= LogisticsNodeStateBits.Ruptured;

                    if ((node.Flags & LogisticsNodeFlags.EmergencyReserved) != 0)
                        stateBits |= LogisticsNodeStateBits.EmergencyReserved;

                    if (_nodeNetInjection[nodeIndex] > Epsilon)
                        stateBits |= LogisticsNodeStateBits.HasGeneration;

                    if (_nodeServedDemand[nodeIndex] > Epsilon)
                        stateBits |= LogisticsNodeStateBits.HasServedDemand;

                    if (node.Potential > Epsilon)
                        stateBits |= LogisticsNodeStateBits.HasPotential;

                    ushort bitmask = (ushort)stateBits;
                    WriteNative(_publishedNodeStates, nodeIndex, bitmask);
                }
            }
            finally
            {
                UnlockGraphMutationBuffers(lockedMask);
            }
        }

        private float SanitizeNodeResistance(float resistance)
        {
            float sanitizedResistance = math.max(MinResistance, resistance);
            switch (_networkType)
            {
                case LogisticsNetworkType.OxygenPressure:
                    return sanitizedResistance * 1.25f;

                default:
                    return sanitizedResistance;
            }
        }

        private float SanitizeEdgeResistance(float resistance)
        {
            float sanitizedResistance = math.max(MinResistance, resistance);
            switch (_networkType)
            {
                case LogisticsNetworkType.OxygenPressure:
                    return sanitizedResistance * 1.15f;

                default:
                    return sanitizedResistance;
            }
        }

        private static LogisticsBrownoutTier ResolveBrownoutTier(float supplyRatio)
        {
            if (supplyRatio < 0.10f)
                return LogisticsBrownoutTier.EmergencyOnly;

            if (supplyRatio < 0.40f)
                return LogisticsBrownoutTier.EssentialOnly;

            if (supplyRatio < 0.85f)
                return LogisticsBrownoutTier.AmbientLightsOnly;

            return LogisticsBrownoutTier.None;
        }

        private void EnsureNodeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureVaultBuffer(ref _nodeBufferHandle, 0, safeLength);
            EnsureVaultBuffer(ref _edgeOffsetsHandle, 1, safeLength + 1);
            EnsureVaultBuffer(ref _edgeWriteCursorHandle, 5, safeLength);
            EnsureVaultBuffer(ref _potentialFrontHandle, 25, safeLength);
            EnsureVaultBuffer(ref _potentialBackHandle, 26, safeLength);
            EnsureVaultBuffer(ref _powerCapacitiesHandle, 27, safeLength);
            EnsureVaultBuffer(ref _powerNodeFlagsHandle, 28, safeLength);
            EnsureVaultBuffer(ref _nodeConductanceSumHandle, 23, safeLength);
            EnsureVaultBuffer(ref _nodeConductanceInverseSumHandle, 24, safeLength);
            EnsureVaultBuffer(ref _producerRatesHandle, 9, safeLength);
            EnsureVaultBuffer(ref _consumerDemandHandle, 10, safeLength);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureVaultBuffer(ref _edgeDestinationsHandle, 2, safeLength);
            EnsureVaultBuffer(ref _edgeConductanceHandle, 3, safeLength);
            EnsureVaultBuffer(ref _edgeCapacityHandle, 4, safeLength);
            EnsureVaultBuffer(ref _edgeFlowHandle, 29, safeLength);
            EnsureVaultBuffer(ref _edgeStatesHandle, 30, safeLength);
            EnsureVaultBuffer(ref _edgeKeysHandle, 31, safeLength);
            EnsureVaultBuffer(ref _runtimeConductiveEdgeCountHandle, 32, 1);
        }

        private void EnsureTopologyCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureVaultBuffer(ref _topologyEdgeListHandle, 6, safeLength);
        }

        private void EnsureProducerCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureVaultBuffer(ref _producersHandle, 7, safeLength);
            EnsureVaultBuffer(ref _producerRatesHandle, 9, safeLength);
            EnsureVaultBuffer(ref _consumerDemandHandle, 10, safeLength);
        }

        private void EnsureConsumerCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureVaultBuffer(ref _consumersHandle, 8, safeLength);
        }

        private void EnsureWorkingCapacity(int nodeCount, int consumerCount)
        {
            EnsureVaultBuffer(ref _parentsHandle, 11, nodeCount);
            EnsureVaultBuffer(ref _ranksHandle, 12, nodeCount);
            EnsureVaultBuffer(ref _componentIdsHandle, 13, nodeCount);
            EnsureVaultBuffer(ref _componentSizesHandle, 14, nodeCount);
            EnsureVaultBuffer(ref _rootToComponentHandle, 15, nodeCount);
            EnsureVaultBuffer(ref _traversalQueueHandle, 16, nodeCount);
            EnsureVaultBuffer(ref _visitedHandle, 17, nodeCount);
            EnsureVaultBuffer(ref _consumerStatesHandle, 18, consumerCount);
            EnsureVaultBuffer(ref _nodeNetInjectionHandle, 19, nodeCount);
            EnsureVaultBuffer(ref _nodeServedDemandHandle, 20, nodeCount);
            EnsureVaultBuffer(ref _nodeVoltageSupplyRatioHandle, 21, nodeCount);
            EnsureVaultBuffer(ref _nodeSourcePotentialHandle, 22, nodeCount);
            EnsureVaultBuffer(ref _componentGenerationHandle, 33, nodeCount);
            EnsureVaultBuffer(ref _componentDemandHandle, 34, nodeCount);
            EnsureVaultBuffer(ref _componentServedDemandHandle, 35, nodeCount);
            EnsureVaultBuffer(ref _componentRemainingSupplyHandle, 36, nodeCount);
            EnsureVaultBuffer(ref _componentSupplyRatioHandle, 37, nodeCount);
            EnsureVaultBuffer(ref _componentResidualInjectionHandle, 38, nodeCount);
            EnsureVaultBuffer(ref _componentAnchorNodeHandle, 39, nodeCount);
            EnsureVaultBuffer(ref _componentBrownoutTierHandle, 40, nodeCount);
            EnsurePublishedStateCapacity(nodeCount);
        }

        private void EnsureSummaryBuffers()
        {
            EnsureVaultBuffer(ref _scheduledTopologySummaryHandle, 41, 1);
            EnsureVaultBuffer(ref _scheduledDistributionSummaryHandle, 42, 1);
        }

        private void EnsurePublishedStateCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureVaultBuffer(ref _publishedNodeStatesHandle, 43, safeLength);
            EnsureVaultBuffer(ref _publishedNodeStatesBackHandle, 44, safeLength);
        }
    }
}
