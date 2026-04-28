using System;
using Unity.Burst;
using Unity.Collections;
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
        Damaged = 1 << 3
    }

    /// <summary>
    /// Native-backed logistics graph kernel used by power and oxygen topologies.
    /// Runtime traversal reads CSR adjacency only: EdgeOffsets + EdgeDestinations + EdgeResistance.
    /// </summary>
    public sealed class LogisticsNetworkGraph : IDisposable
    {
        public struct LogisticsNode
        {
            public uint Id;
            public float Capacity;
            public float Resistance;
            public float CurrentLoad;
            public float Potential;
            public byte Priority;
            public LogisticsNodeFlags Flags;
            public byte NetworkId;
            public byte Reserved;
        }

        public struct TopologySummary
        {
            public int NodeCount;
            public int EdgeCount;
            public int IslandCount;
            public int CycleCount;
            public int BfsVisitedCount;
            public int ProducerReachableCount;
        }

        public struct DistributionSummary
        {
            public float TotalGeneration;
            public float TotalConsumption;
            public float Balance;
            public float SupplyRatio;
            public float ServedDemand;
            public float UnservedDemand;
            public int PoweredCount;
            public int DisabledCount;
            public bool HasDeficit;
            public LogisticsBrownoutTier BrownoutTier;
        }

        private struct TopologyEdgeRecord
        {
            public int SourceNodeIndex;
            public int DestinationNodeIndex;
            public float Resistance;
        }

        private struct ProducerRecord
        {
            public int NodeIndex;
        }

        private struct ConsumerRecord
        {
            public int NodeIndex;
            public float Demand;
            public int PowerPriority;
            public byte PriorityTier;
            public LogisticsConsumerFlags Flags;
        }

        private struct PublishNodeStatesJob : IJobParallelFor
        {
            private const float PublishEpsilon = 0.0001f;

            [ReadOnly] public NativeArray<LogisticsNode> Nodes;
            [ReadOnly] public NativeArray<float> NodeNetInjection;
            [ReadOnly] public NativeArray<float> NodeServedDemand;

            public NativeArray<ushort> PublishedNodeStates;
            public NativeParallelHashMap<uint, ushort>.ParallelWriter PublishedNodeStateMap;

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
                PublishedNodeStateMap.TryAdd(node.Id, bitmask);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct EvaluateGraphJob : IJob
        {
            public LogisticsNetworkType NetworkType;
            public int NodeCount;
            public int EdgeCount;
            public int ConsumerCount;

            public NativeArray<LogisticsNode> Nodes;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<float> EdgeResistance;
            [ReadOnly] public NativeList<ProducerRecord> Producers;
            [ReadOnly] public NativeList<ConsumerRecord> Consumers;
            [ReadOnly] public NativeParallelHashMap<int, float> ProducerMap;

            public NativeArray<int> Parents;
            public NativeArray<int> Ranks;
            public NativeArray<int> ComponentIds;
            public NativeArray<int> ComponentSizes;
            public NativeArray<int> RootToComponent;
            public NativeArray<int> TraversalQueue;
            public NativeArray<byte> Visited;
            public NativeArray<byte> ConsumerStates;
            public NativeArray<float> NodeNetInjection;
            public NativeArray<float> NodePotentialFront;
            public NativeArray<float> NodePotentialBack;
            public NativeArray<float> NodeServedDemand;
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

                    distribution = EvaluateDistribution();
                }

                TopologySummaryBuffer[0] = topology;
                DistributionSummaryBuffer[0] = distribution;
            }

            private DistributionSummary EvaluateDistribution()
            {
                DistributionSummary summary = new DistributionSummary
                {
                    TotalGeneration = ComputeTotalGeneration(),
                    TotalConsumption = ComputeTotalConsumption()
                };

                summary.Balance = summary.TotalGeneration - summary.TotalConsumption;
                summary.SupplyRatio = summary.TotalConsumption > Epsilon
                    ? math.saturate(summary.TotalGeneration / summary.TotalConsumption)
                    : 1f;
                summary.HasDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption;
                summary.BrownoutTier = ResolveBrownoutTier(summary.SupplyRatio);

                ResetConsumerStates();
                ResetDistributionState();
                BuildComponentDistributionState();
                AllocateServedDemand(ref summary);
                BuildNodeInjection();
                summary.UnservedDemand = math.max(0f, summary.TotalConsumption - summary.ServedDemand);
                summary.HasDeficit = summary.UnservedDemand > Epsilon;
                return summary;
            }

            private float ComputeTotalGeneration()
            {
                float totalGeneration = 0f;
                int producerCount = Producers.Length;
                for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
                {
                    int nodeIndex = Producers[producerIndex].NodeIndex;
                    if (ProducerMap.TryGetValue(nodeIndex, out float productionRate))
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

                    NodeNetInjection[nodeIndex] = 0f;
                    NodePotentialFront[nodeIndex] = 0f;
                    NodePotentialBack[nodeIndex] = 0f;
                    NodeServedDemand[nodeIndex] = 0f;
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

                    if (ProducerMap.TryGetValue(nodeIndex, out float productionRate))
                        ComponentGeneration[componentIndex] += productionRate;
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
                    float supplyRatio = demand > Epsilon ? math.saturate(generation / demand) : 1f;
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

                for (int priorityTier = 0; priorityTier <= 3; priorityTier++)
                {
                    for (int priority = MinPriority; priority <= MaxPriority; priority++)
                    {
                        for (int consumerIndex = 0; consumerIndex < ConsumerCount; consumerIndex++)
                        {
                            ConsumerRecord consumer = Consumers[consumerIndex];
                            if (consumer.PriorityTier != priorityTier || consumer.PowerPriority != priority)
                                continue;

                            int componentIndex = ComponentIds[consumer.NodeIndex];
                            LogisticsBrownoutTier brownoutTier = componentIndex >= 0 && componentIndex < NodeCount
                                ? (LogisticsBrownoutTier)ComponentBrownoutTier[componentIndex]
                                : LogisticsBrownoutTier.EmergencyOnly;

                            if (ShouldMarkBrownout(in consumer, brownoutTier))
                                MarkBrownoutNode(consumer.NodeIndex);

                            if (!CanServeConsumer(in consumer, brownoutTier) ||
                                componentIndex < 0 ||
                                componentIndex >= NodeCount)
                            {
                                MarkBrownoutNode(consumer.NodeIndex);
                                continue;
                            }

                            float remainingSupply = ComponentRemainingSupply[componentIndex];
                            if (remainingSupply + Epsilon < consumer.Demand)
                            {
                                MarkBrownoutNode(consumer.NodeIndex);
                                continue;
                            }

                            ComponentRemainingSupply[componentIndex] = remainingSupply - consumer.Demand;
                            ComponentServedDemand[componentIndex] += consumer.Demand;
                            NodeServedDemand[consumer.NodeIndex] += consumer.Demand;
                            ConsumerStates[consumerIndex] = 1;
                            poweredCount++;
                            servedDemand += consumer.Demand;
                        }
                    }
                }

                summary.PoweredCount = poweredCount;
                summary.DisabledCount = ConsumerCount - poweredCount;
                summary.ServedDemand = servedDemand;
            }

            private void BuildNodeInjection()
            {
                int producerCount = Producers.Length;
                for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
                {
                    int nodeIndex = Producers[producerIndex].NodeIndex;
                    int componentIndex = ComponentIds[nodeIndex];
                    if (componentIndex < 0 || componentIndex >= NodeCount)
                        continue;

                    if (!ProducerMap.TryGetValue(nodeIndex, out float productionRate))
                        continue;

                    float componentGeneration = ComponentGeneration[componentIndex];
                    float componentServedDemand = ComponentServedDemand[componentIndex];
                    if (componentGeneration <= Epsilon || componentServedDemand <= Epsilon)
                        continue;

                    float dispatchedGeneration = productionRate * math.saturate(componentServedDemand / componentGeneration);
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

            private int TraverseReachableFrom(int startNodeIndex)
            {
                if (!IsValidNodeIndex(startNodeIndex))
                    return 0;

                ClearVisited();
                int head = 0;
                int tail = 0;
                Visited[startNodeIndex] = 1;
                TraversalQueue[tail++] = startNodeIndex;

                int visitedCount = 0;
                while (head < tail)
                {
                    int nodeIndex = TraversalQueue[head++];
                    visitedCount++;

                    int edgeStart = EdgeOffsets[nodeIndex];
                    int edgeEnd = EdgeOffsets[nodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(destinationNodeIndex) || Visited[destinationNodeIndex] != 0)
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
                int head = 0;
                int tail = 0;

                int producerCount = Producers.Length;
                for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
                {
                    int nodeIndex = Producers[producerIndex].NodeIndex;
                    if (!IsValidNodeIndex(nodeIndex) || Visited[nodeIndex] != 0)
                        continue;

                    Visited[nodeIndex] = 1;
                    TraversalQueue[tail++] = nodeIndex;
                }

                int visitedCount = 0;
                while (head < tail)
                {
                    int sourceNodeIndex = TraversalQueue[head++];
                    visitedCount++;

                    int edgeStart = EdgeOffsets[sourceNodeIndex];
                    int edgeEnd = EdgeOffsets[sourceNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(destinationNodeIndex) || Visited[destinationNodeIndex] != 0)
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
            }

            private bool CanServeConsumer(in ConsumerRecord consumer, LogisticsBrownoutTier brownoutTier)
            {
                if (!IsValidNodeIndex(consumer.NodeIndex))
                    return false;

                LogisticsNode node = Nodes[consumer.NodeIndex];
                if ((node.Flags & LogisticsNodeFlags.Isolated) != 0)
                    return false;

                switch (brownoutTier)
                {
                    case LogisticsBrownoutTier.EmergencyOnly:
                        return (consumer.Flags & LogisticsConsumerFlags.EmergencyReserved) != 0 || consumer.PriorityTier == 0;
                    case LogisticsBrownoutTier.EssentialOnly:
                        return (consumer.Flags & (LogisticsConsumerFlags.Essential | LogisticsConsumerFlags.EmergencyReserved)) != 0 ||
                               consumer.PriorityTier <= 1;
                    default:
                        return true;
                }
            }

            private static bool ShouldMarkBrownout(in ConsumerRecord consumer, LogisticsBrownoutTier brownoutTier)
            {
                switch (brownoutTier)
                {
                    case LogisticsBrownoutTier.EmergencyOnly:
                        return (consumer.Flags & LogisticsConsumerFlags.EmergencyReserved) == 0;
                    case LogisticsBrownoutTier.EssentialOnly:
                        return (consumer.Flags & (LogisticsConsumerFlags.Essential | LogisticsConsumerFlags.EmergencyReserved)) == 0;
                    case LogisticsBrownoutTier.AmbientLightsOnly:
                        return consumer.PriorityTier >= 2 || (consumer.Flags & LogisticsConsumerFlags.AmbientLighting) != 0;
                    default:
                        return false;
                }
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

            private float ResolveCombinedResistance(int sourceNodeIndex, int destinationNodeIndex, int edgeIndex)
            {
                LogisticsNode sourceNode = Nodes[sourceNodeIndex];
                LogisticsNode destinationNode = Nodes[destinationNodeIndex];
                float combinedResistance = math.max(
                    MinResistance,
                    EdgeResistance[edgeIndex] + sourceNode.Resistance + destinationNode.Resistance);

                if (NetworkType != LogisticsNetworkType.PowerDc &&
                    (((sourceNode.Flags | destinationNode.Flags) & LogisticsNodeFlags.Ruptured) != 0))
                {
                    combinedResistance *= RuptureResistanceMultiplier;
                }

                return combinedResistance;
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

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct InitializePotentialBuffersJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> NodeNetInjection;
            public NativeArray<float> NodePotentialFront;
            public NativeArray<float> NodePotentialBack;

            public void Execute(int index)
            {
                float seed = NodeNetInjection[index];
                NodePotentialFront[index] = seed;
                NodePotentialBack[index] = seed;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct RelaxNodePotentialsJob : IJobParallelFor
        {
            private const float MinResistanceValue = 0.0001f;
            private const float EpsilonValue = 0.001f;
            private const float RuptureResistanceMultiplierValue = 2f;

            public LogisticsNetworkType NetworkType;
            public int NodeCount;

            [ReadOnly] public NativeArray<LogisticsNode> Nodes;
            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<float> EdgeResistance;
            [ReadOnly] public NativeArray<int> ComponentIds;
            [ReadOnly] public NativeArray<int> ComponentAnchorNode;
            [ReadOnly] public NativeArray<float> NodeNetInjection;
            [ReadOnly] public NativeArray<float> NodePotentialFront;

            public NativeArray<float> NodePotentialBack;

            public void Execute(int nodeIndex)
            {
                int componentIndex = ComponentIds[nodeIndex];
                if (componentIndex < 0 || componentIndex >= NodeCount || ComponentAnchorNode[componentIndex] == nodeIndex)
                {
                    NodePotentialBack[nodeIndex] = 0f;
                    return;
                }

                float conductanceSum = 0f;
                float weightedPotential = 0f;

                int edgeStart = EdgeOffsets[nodeIndex];
                int edgeEnd = EdgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = EdgeDestinations[edgeIndex];
                    if (neighborNodeIndex < 0 ||
                        neighborNodeIndex >= NodeCount ||
                        ComponentIds[neighborNodeIndex] != componentIndex)
                    {
                        continue;
                    }

                    float conductance = 1f / ResolveCombinedResistance(nodeIndex, neighborNodeIndex, edgeIndex);
                    conductanceSum += conductance;
                    weightedPotential += conductance * NodePotentialFront[neighborNodeIndex];
                }

                if (conductanceSum <= EpsilonValue)
                {
                    NodePotentialBack[nodeIndex] = 0f;
                    return;
                }

                float relaxedPotential = (weightedPotential + NodeNetInjection[nodeIndex]) / conductanceSum;
                NodePotentialBack[nodeIndex] = math.isfinite(relaxedPotential) ? relaxedPotential : 0f;
            }

            private float ResolveCombinedResistance(int sourceNodeIndex, int destinationNodeIndex, int edgeIndex)
            {
                LogisticsNode sourceNode = Nodes[sourceNodeIndex];
                LogisticsNode destinationNode = Nodes[destinationNodeIndex];
                float combinedResistance = math.max(
                    MinResistanceValue,
                    EdgeResistance[edgeIndex] + sourceNode.Resistance + destinationNode.Resistance);

                if (NetworkType != LogisticsNetworkType.PowerDc &&
                    (((sourceNode.Flags | destinationNode.Flags) & LogisticsNodeFlags.Ruptured) != 0))
                {
                    combinedResistance *= RuptureResistanceMultiplierValue;
                }

                return combinedResistance;
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        private struct ApplyPotentialsAndLoadsJob : IJobParallelFor
        {
            private const float MinResistanceValue = 0.0001f;
            private const float EpsilonValue = 0.001f;
            private const float RuptureResistanceMultiplierValue = 2f;

            public LogisticsNetworkType NetworkType;
            public int NodeCount;

            public NativeArray<LogisticsNode> Nodes;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<float> EdgeResistance;
            [ReadOnly] public NativeArray<float> NodeServedDemand;
            [ReadOnly] public NativeArray<float> NodeNetInjection;
            [ReadOnly] public NativeArray<float> FinalPotentials;

            public void Execute(int nodeIndex)
            {
                LogisticsNode node = Nodes[nodeIndex];
                float branchLoad = 0f;
                float nodePotential = FinalPotentials[nodeIndex];

                int edgeStart = EdgeOffsets[nodeIndex];
                int edgeEnd = EdgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationNodeIndex = EdgeDestinations[edgeIndex];
                    if (destinationNodeIndex < 0 || destinationNodeIndex >= NodeCount)
                        continue;

                    float resistance = ResolveCombinedResistance(nodeIndex, destinationNodeIndex, edgeIndex);
                    float flowRate = math.abs((nodePotential - FinalPotentials[destinationNodeIndex]) / resistance);
                    if (flowRate > EpsilonValue)
                        branchLoad += flowRate;
                }

                node.Potential = nodePotential;
                node.CurrentLoad = math.max(branchLoad, NodeServedDemand[nodeIndex] + math.max(0f, NodeNetInjection[nodeIndex]));
                if (node.CurrentLoad > node.Capacity * 1.15f)
                    node.Flags |= LogisticsNodeFlags.Overloaded;

                Nodes[nodeIndex] = node;
            }

            private float ResolveCombinedResistance(int sourceNodeIndex, int destinationNodeIndex, int edgeIndex)
            {
                LogisticsNode sourceNode = Nodes[sourceNodeIndex];
                LogisticsNode destinationNode = Nodes[destinationNodeIndex];
                float combinedResistance = math.max(
                    MinResistanceValue,
                    EdgeResistance[edgeIndex] + sourceNode.Resistance + destinationNode.Resistance);

                if (NetworkType != LogisticsNetworkType.PowerDc &&
                    (((sourceNode.Flags | destinationNode.Flags) & LogisticsNodeFlags.Ruptured) != 0))
                {
                    combinedResistance *= RuptureResistanceMultiplierValue;
                }

                return combinedResistance;
            }
        }

        private const int MinPriority = 0;
        private const int MaxPriority = 100;
        private const int MaxJacobiIterations = 8;
        private const int ParallelNodeBatchSize = 64;
        private const float MinResistance = 0.0001f;
        private const float Epsilon = 0.001f;
        private const float JacobiConvergenceDelta = 0.01f;
        private const float RuptureResistanceMultiplier = 2f;

        private LogisticsNetworkType _networkType;
        private int _nodeCount;
        private int _edgeCount;

        private NativeArray<LogisticsNode> _nodeBuffer;
        private NativeArray<int> _edgeOffsets;
        private NativeArray<int> _edgeDestinations;
        private NativeArray<float> _edgeResistance;
        private NativeArray<int> _edgeWriteCursor;
        private NativeList<TopologyEdgeRecord> _topologyEdgeList;
        private NativeList<ProducerRecord> _producers;
        private NativeList<ConsumerRecord> _consumers;
        private NativeParallelHashMap<int, float> _producerMap;
        private NativeParallelHashMap<int, float> _consumerMap;
        private NativeArray<int> _parents;
        private NativeArray<int> _ranks;
        private NativeArray<int> _componentIds;
        private NativeArray<int> _componentSizes;
        private NativeArray<int> _rootToComponent;
        private NativeArray<int> _traversalQueue;
        private NativeArray<byte> _visited;
        private NativeArray<byte> _consumerStates;
        private NativeArray<float> _nodeNetInjection;
        private NativeArray<float> _nodePotentialFront;
        private NativeArray<float> _nodePotentialBack;
        private NativeArray<float> _nodeServedDemand;
        private NativeArray<float> _componentGeneration;
        private NativeArray<float> _componentDemand;
        private NativeArray<float> _componentServedDemand;
        private NativeArray<float> _componentRemainingSupply;
        private NativeArray<float> _componentSupplyRatio;
        private NativeArray<float> _componentResidualInjection;
        private NativeArray<int> _componentAnchorNode;
        private NativeArray<byte> _componentBrownoutTier;
        private NativeArray<TopologySummary> _scheduledTopologySummary;
        private NativeArray<DistributionSummary> _scheduledDistributionSummary;
        private NativeArray<ushort> _publishedNodeStates;
        private NativeParallelHashMap<uint, ushort> _publishedNodeStateMap;
        private NativeQueue<int> _bfsQueue;
        private JobHandle _evaluateGraphJobHandle;
        private bool _evaluateGraphPending;
        private JobHandle _publishNodeStatesJobHandle;
        private bool _publishNodeStatesPending;

        public LogisticsNetworkGraph(int nodeCapacity = 16, int edgeCapacity = 32, int consumerCapacity = 16)
        {
            int safeNodeCapacity = math.max(1, nodeCapacity);
            int safeEdgeCapacity = math.max(1, edgeCapacity);
            int safeConsumerCapacity = math.max(1, consumerCapacity);

            // COLD ALLOC: LogisticsNode[nodeCapacity] — runtime node buffer — owner: LogisticsNetworkGraph
            _nodeBuffer = new NativeArray<LogisticsNode>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: int[nodeCapacity + 1] — CSR edge offsets — owner: LogisticsNetworkGraph
            _edgeOffsets = new NativeArray<int>(safeNodeCapacity + 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: int[edgeCapacity] — CSR edge destinations — owner: LogisticsNetworkGraph
            _edgeDestinations = new NativeArray<int>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: float[edgeCapacity] — CSR edge resistance weights — owner: LogisticsNetworkGraph
            _edgeResistance = new NativeArray<float>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: int[nodeCapacity] — CSR write cursor scratch — owner: LogisticsNetworkGraph
            _edgeWriteCursor = new NativeArray<int>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: TopologyEdgeRecord[edgeCapacity] — topology mutation source — owner: LogisticsNetworkGraph
            _topologyEdgeList = new NativeList<TopologyEdgeRecord>(safeEdgeCapacity, Allocator.Persistent);
            // COLD ALLOC: ProducerRecord[nodeCapacity] — producer seed list — owner: LogisticsNetworkGraph
            _producers = new NativeList<ProducerRecord>(safeNodeCapacity, Allocator.Persistent);
            // COLD ALLOC: ConsumerRecord[consumerCapacity] — consumer demand list — owner: LogisticsNetworkGraph
            _consumers = new NativeList<ConsumerRecord>(safeConsumerCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<int,float>[nodeCapacity] — producer aggregation map — owner: LogisticsNetworkGraph
            _producerMap = new NativeParallelHashMap<int, float>(safeNodeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeParallelHashMap<int,float>[nodeCapacity] — consumer aggregation map — owner: LogisticsNetworkGraph
            _consumerMap = new NativeParallelHashMap<int, float>(safeNodeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeArray<ushort>[nodeCapacity] — published node-state bitmasks — owner: LogisticsNetworkGraph
            _publishedNodeStates = new NativeArray<ushort>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeParallelHashMap<uint,ushort>[nodeCapacity] — published node-state lookup by stable node id — owner: LogisticsNetworkGraph
            _publishedNodeStateMap = new NativeParallelHashMap<uint, ushort>(safeNodeCapacity, Allocator.Persistent);
            // COLD ALLOC: NativeQueue<int>[nodeCapacity] — iterative BFS frontier — owner: LogisticsNetworkGraph
            _bfsQueue = new NativeQueue<int>(Allocator.Persistent);
        }

        public int NodeCount => _nodeCount;
        public int ConsumerCount => _consumers.IsCreated ? _consumers.Length : 0;
        public bool HasPendingEvaluation => _evaluateGraphPending;
        public bool HasPendingNodeStatePublish => _publishNodeStatesPending;

        public void Dispose()
        {
            CompleteEvaluation();
            CompleteNodeStatePublish();

            if (_nodeBuffer.IsCreated)
                _nodeBuffer.Dispose();

            if (_edgeOffsets.IsCreated)
                _edgeOffsets.Dispose();

            if (_edgeDestinations.IsCreated)
                _edgeDestinations.Dispose();

            if (_edgeResistance.IsCreated)
                _edgeResistance.Dispose();

            if (_edgeWriteCursor.IsCreated)
                _edgeWriteCursor.Dispose();

            if (_topologyEdgeList.IsCreated)
                _topologyEdgeList.Dispose();

            if (_producers.IsCreated)
                _producers.Dispose();

            if (_consumers.IsCreated)
                _consumers.Dispose();

            if (_producerMap.IsCreated)
                _producerMap.Dispose();

            if (_consumerMap.IsCreated)
                _consumerMap.Dispose();

            if (_parents.IsCreated)
                _parents.Dispose();

            if (_ranks.IsCreated)
                _ranks.Dispose();

            if (_componentIds.IsCreated)
                _componentIds.Dispose();

            if (_componentSizes.IsCreated)
                _componentSizes.Dispose();

            if (_rootToComponent.IsCreated)
                _rootToComponent.Dispose();

            if (_traversalQueue.IsCreated)
                _traversalQueue.Dispose();

            if (_visited.IsCreated)
                _visited.Dispose();

            if (_consumerStates.IsCreated)
                _consumerStates.Dispose();

            if (_nodeNetInjection.IsCreated)
                _nodeNetInjection.Dispose();

            if (_nodePotentialFront.IsCreated)
                _nodePotentialFront.Dispose();

            if (_nodePotentialBack.IsCreated)
                _nodePotentialBack.Dispose();

            if (_nodeServedDemand.IsCreated)
                _nodeServedDemand.Dispose();

            if (_componentGeneration.IsCreated)
                _componentGeneration.Dispose();

            if (_componentDemand.IsCreated)
                _componentDemand.Dispose();

            if (_componentServedDemand.IsCreated)
                _componentServedDemand.Dispose();

            if (_componentRemainingSupply.IsCreated)
                _componentRemainingSupply.Dispose();

            if (_componentSupplyRatio.IsCreated)
                _componentSupplyRatio.Dispose();

            if (_componentResidualInjection.IsCreated)
                _componentResidualInjection.Dispose();

            if (_componentAnchorNode.IsCreated)
                _componentAnchorNode.Dispose();

            if (_componentBrownoutTier.IsCreated)
                _componentBrownoutTier.Dispose();

            if (_scheduledTopologySummary.IsCreated)
                _scheduledTopologySummary.Dispose();

            if (_scheduledDistributionSummary.IsCreated)
                _scheduledDistributionSummary.Dispose();

            if (_publishedNodeStates.IsCreated)
                _publishedNodeStates.Dispose();

            if (_publishedNodeStateMap.IsCreated)
                _publishedNodeStateMap.Dispose();

            if (_bfsQueue.IsCreated)
                _bfsQueue.Dispose();
        }

        public void BeginBuild(LogisticsNetworkType networkType, int nodeCapacity, int edgeCapacity, int consumerCapacity)
        {
            CompleteEvaluation();
            CompleteNodeStatePublish();

            int safeNodeCapacity = math.max(1, nodeCapacity);
            int safeEdgeCapacity = math.max(1, edgeCapacity);
            int safeConsumerCapacity = math.max(1, consumerCapacity);

            _networkType = networkType;
            _nodeCount = 0;
            _edgeCount = 0;

            EnsureNodeCapacity(safeNodeCapacity);
            EnsureEdgeCapacity(safeEdgeCapacity);
            EnsureTopologyCapacity(safeEdgeCapacity);
            EnsureProducerCapacity(safeNodeCapacity);
            EnsureConsumerCapacity(safeConsumerCapacity);
            EnsureWorkingCapacity(safeNodeCapacity, safeConsumerCapacity);
            EnsureSummaryBuffers();

            _topologyEdgeList.Clear();
            _producers.Clear();
            _consumers.Clear();
            _producerMap.Clear();
            _consumerMap.Clear();
            _bfsQueue.Clear();
        }

        public int AddNode(uint nodeId, float capacity, float resistance, byte priorityTier, LogisticsNodeFlags flags, byte reservedState)
        {
            if (_nodeCount >= _nodeBuffer.Length)
                EnsureNodeCapacity(_nodeCount + 1);

            _nodeBuffer[_nodeCount] = new LogisticsNode
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
            };

            return _nodeCount++;
        }

        public void AddEdge(int sourceNodeIndex, int destinationNodeIndex, float resistance)
        {
            if (_topologyEdgeList.Length >= _topologyEdgeList.Capacity)
                _topologyEdgeList.Capacity = math.max(1, _topologyEdgeList.Capacity * 2);

            _topologyEdgeList.Add(new TopologyEdgeRecord
            {
                SourceNodeIndex = sourceNodeIndex,
                DestinationNodeIndex = destinationNodeIndex,
                Resistance = SanitizeEdgeResistance(resistance)
            });
        }

        public void AddProducer(int nodeIndex, float productionRate)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodeCount || productionRate <= 0f)
                return;

            if (_producerMap.TryGetValue(nodeIndex, out float currentProduction))
            {
                _producerMap[nodeIndex] = currentProduction + productionRate;
                return;
            }

            _producerMap.Add(nodeIndex, productionRate);
            _producers.Add(new ProducerRecord { NodeIndex = nodeIndex });
        }

        public void AddConsumer(int nodeIndex, float demand, int powerPriority, byte priorityTier, LogisticsConsumerFlags flags)
        {
            if (nodeIndex < 0 || nodeIndex >= _nodeCount || demand <= 0f)
                return;

            if (_consumerMap.TryGetValue(nodeIndex, out float currentDemand))
                _consumerMap[nodeIndex] = currentDemand + demand;
            else
                _consumerMap.Add(nodeIndex, demand);

            _consumers.Add(new ConsumerRecord
            {
                NodeIndex = nodeIndex,
                Demand = demand,
                PowerPriority = math.clamp(powerPriority, MinPriority, MaxPriority),
                PriorityTier = priorityTier,
                Flags = flags
            });
        }

        public void FinalizeBuild()
        {
            int nodeCount = _nodeCount;
            for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = 0;

            int topologyEdgeCount = _topologyEdgeList.Length;
            _edgeCount = 0;

            for (int edgeIndex = 0; edgeIndex < topologyEdgeCount; edgeIndex++)
            {
                TopologyEdgeRecord edge = _topologyEdgeList[edgeIndex];
                if (!IsValidNodeIndex(edge.SourceNodeIndex) || !IsValidNodeIndex(edge.DestinationNodeIndex))
                    continue;

                _edgeOffsets[edge.SourceNodeIndex + 1] = _edgeOffsets[edge.SourceNodeIndex + 1] + 1;
                _edgeCount++;
            }

            for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = _edgeOffsets[nodeIndex] + _edgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                _edgeWriteCursor[nodeIndex] = _edgeOffsets[nodeIndex];

            for (int edgeIndex = 0; edgeIndex < topologyEdgeCount; edgeIndex++)
            {
                TopologyEdgeRecord edge = _topologyEdgeList[edgeIndex];
                if (!IsValidNodeIndex(edge.SourceNodeIndex) || !IsValidNodeIndex(edge.DestinationNodeIndex))
                    continue;

                int writeIndex = _edgeWriteCursor[edge.SourceNodeIndex];
                _edgeWriteCursor[edge.SourceNodeIndex] = writeIndex + 1;
                _edgeDestinations[writeIndex] = edge.DestinationNodeIndex;
                _edgeResistance[writeIndex] = edge.Resistance;
            }
        }

        public TopologySummary AnalyzeTopology()
        {
            CompleteEvaluation();

            TopologySummary summary = new TopologySummary
            {
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount
            };

            if (_nodeCount <= 0)
                return summary;

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            ResetTopologyScratch(_nodeCount);
            ClearNodeFlags(LogisticsNodeFlags.Isolated | LogisticsNodeFlags.BridgeNode);

            int cycleCount = 0;
            for (int sourceNodeIndex = 0; sourceNodeIndex < _nodeCount; sourceNodeIndex++)
            {
                int edgeStart = _edgeOffsets[sourceNodeIndex];
                int edgeEnd = _edgeOffsets[sourceNodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
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
                _parents[nodeIndex] = rootIndex;

                int componentIndex = _rootToComponent[rootIndex];
                if (componentIndex < 0)
                {
                    componentIndex = islandCount;
                    _rootToComponent[rootIndex] = componentIndex;
                    _componentSizes[componentIndex] = 0;
                    islandCount++;
                }

                _componentIds[nodeIndex] = componentIndex;
                _componentSizes[componentIndex] = _componentSizes[componentIndex] + 1;

                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.NetworkId = (byte)math.clamp(componentIndex, 0, (int)byte.MaxValue);
                _nodeBuffer[nodeIndex] = node;
            }

            summary.IslandCount = islandCount;
            summary.CycleCount = cycleCount;
            summary.BfsVisitedCount = TraverseReachableFromInternal(0);
            summary.ProducerReachableCount = TraverseProducerReachability();
            MarkIsolatedNodesFromVisited();
            return summary;
        }

        public DistributionSummary Distribute()
        {
            CompleteEvaluation();

            DistributionSummary summary = new DistributionSummary
            {
                TotalGeneration = ComputeTotalGeneration(),
                TotalConsumption = ComputeTotalConsumption()
            };

            summary.Balance = summary.TotalGeneration - summary.TotalConsumption;
            summary.SupplyRatio = summary.TotalConsumption > Epsilon
                ? math.saturate(summary.TotalGeneration / summary.TotalConsumption)
                : 1f;
            summary.HasDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption;
            summary.BrownoutTier = ResolveBrownoutTier(summary.SupplyRatio);

            if (_nodeCount <= 0)
                return summary;

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            ResetConsumerStates(ConsumerCount);
            ResetDistributionState();
            BuildComponentDistributionState();
            AllocateServedDemand(ref summary);
            BuildNodeInjection();
            RelaxNodePotentials();
            AccumulateNodeLoadsFromPotentials();
            summary.UnservedDemand = math.max(0f, summary.TotalConsumption - summary.ServedDemand);
            summary.HasDeficit = summary.UnservedDemand > Epsilon;
            return summary;
        }

        public int GetNodeComponentId(int nodeIndex)
        {
            if (!_componentIds.IsCreated || nodeIndex < 0 || nodeIndex >= _componentIds.Length)
                return -1;

            return _componentIds[nodeIndex];
        }

        public int GetComponentSize(int componentIndex)
        {
            if (!_componentSizes.IsCreated || componentIndex < 0 || componentIndex >= _componentSizes.Length)
                return 0;

            return _componentSizes[componentIndex];
        }

        public bool IsNodeReachable(int nodeIndex)
        {
            if (!_visited.IsCreated || nodeIndex < 0 || nodeIndex >= _nodeCount)
                return false;

            return _visited[nodeIndex] != 0;
        }

        public bool IsConsumerPowered(int consumerIndex)
        {
            if (!_consumerStates.IsCreated || consumerIndex < 0 || consumerIndex >= _consumerStates.Length)
                return false;

            return _consumerStates[consumerIndex] != 0;
        }

        public void ScheduleNodeStatePublish()
        {
            ScheduleNodeStatePublish(default);
        }

        public void ScheduleNodeStatePublish(JobHandle dependency)
        {
            CompleteNodeStatePublish();

            if (_nodeCount <= 0)
            {
                ClearPublishedNodeStates();
                return;
            }

            EnsurePublishedStateCapacity(_nodeCount);
            _publishedNodeStateMap.Clear();

            PublishNodeStatesJob job = new PublishNodeStatesJob
            {
                Nodes = _nodeBuffer,
                NodeNetInjection = _nodeNetInjection,
                NodeServedDemand = _nodeServedDemand,
                PublishedNodeStates = _publishedNodeStates,
                PublishedNodeStateMap = _publishedNodeStateMap.AsParallelWriter()
            };

            _publishNodeStatesJobHandle = job.Schedule(_nodeCount, ParallelNodeBatchSize, dependency);
            _publishNodeStatesPending = true;
        }

        public JobHandle ScheduleEvaluation()
        {
            CompleteEvaluation();

            if (_nodeCount <= 0)
            {
                EnsureSummaryBuffers();
                _scheduledTopologySummary[0] = new TopologySummary();
                _scheduledDistributionSummary[0] = new DistributionSummary
                {
                    SupplyRatio = 1f,
                    BrownoutTier = LogisticsBrownoutTier.None
                };
                return default;
            }

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            EnsureSummaryBuffers();

            EvaluateGraphJob job = new EvaluateGraphJob
            {
                NetworkType = _networkType,
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount,
                ConsumerCount = ConsumerCount,
                Nodes = _nodeBuffer,
                EdgeOffsets = _edgeOffsets,
                EdgeDestinations = _edgeDestinations,
                EdgeResistance = _edgeResistance,
                Producers = _producers,
                Consumers = _consumers,
                ProducerMap = _producerMap,
                Parents = _parents,
                Ranks = _ranks,
                ComponentIds = _componentIds,
                ComponentSizes = _componentSizes,
                RootToComponent = _rootToComponent,
                TraversalQueue = _traversalQueue,
                Visited = _visited,
                ConsumerStates = _consumerStates,
                NodeNetInjection = _nodeNetInjection,
                NodePotentialFront = _nodePotentialFront,
                NodePotentialBack = _nodePotentialBack,
                NodeServedDemand = _nodeServedDemand,
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

            JobHandle evaluationHandle = job.Schedule();

            InitializePotentialBuffersJob initializePotentialsJob = new InitializePotentialBuffersJob
            {
                NodeNetInjection = _nodeNetInjection,
                NodePotentialFront = _nodePotentialFront,
                NodePotentialBack = _nodePotentialBack
            };

            evaluationHandle = initializePotentialsJob.Schedule(_nodeCount, ParallelNodeBatchSize, evaluationHandle);

            NativeArray<float> relaxFront = _nodePotentialFront;
            NativeArray<float> relaxBack = _nodePotentialBack;
            for (int iteration = 0; iteration < MaxJacobiIterations; iteration++)
            {
                RelaxNodePotentialsJob relaxPotentialsJob = new RelaxNodePotentialsJob
                {
                    NetworkType = _networkType,
                    NodeCount = _nodeCount,
                    Nodes = _nodeBuffer,
                    EdgeOffsets = _edgeOffsets,
                    EdgeDestinations = _edgeDestinations,
                    EdgeResistance = _edgeResistance,
                    ComponentIds = _componentIds,
                    ComponentAnchorNode = _componentAnchorNode,
                    NodeNetInjection = _nodeNetInjection,
                    NodePotentialFront = relaxFront,
                    NodePotentialBack = relaxBack
                };

                evaluationHandle = relaxPotentialsJob.Schedule(_nodeCount, ParallelNodeBatchSize, evaluationHandle);
                NativeArray<float> swap = relaxFront;
                relaxFront = relaxBack;
                relaxBack = swap;
            }

            ApplyPotentialsAndLoadsJob applyPotentialsAndLoadsJob = new ApplyPotentialsAndLoadsJob
            {
                NetworkType = _networkType,
                NodeCount = _nodeCount,
                Nodes = _nodeBuffer,
                EdgeOffsets = _edgeOffsets,
                EdgeDestinations = _edgeDestinations,
                EdgeResistance = _edgeResistance,
                NodeServedDemand = _nodeServedDemand,
                NodeNetInjection = _nodeNetInjection,
                FinalPotentials = relaxFront
            };

            _evaluateGraphJobHandle = applyPotentialsAndLoadsJob.Schedule(_nodeCount, ParallelNodeBatchSize, evaluationHandle);
            _evaluateGraphPending = true;
            return _evaluateGraphJobHandle;
        }

        public void CompleteEvaluation()
        {
            if (!_evaluateGraphPending)
                return;

            _evaluateGraphJobHandle.Complete();
            _evaluateGraphJobHandle = default;
            _evaluateGraphPending = false;
        }

        public TopologySummary GetScheduledTopologySummary()
        {
            CompleteEvaluation();
            return _scheduledTopologySummary.IsCreated && _scheduledTopologySummary.Length > 0
                ? _scheduledTopologySummary[0]
                : default;
        }

        public DistributionSummary GetScheduledDistributionSummary()
        {
            CompleteEvaluation();
            if (_scheduledDistributionSummary.IsCreated && _scheduledDistributionSummary.Length > 0)
                return _scheduledDistributionSummary[0];

            return new DistributionSummary
            {
                SupplyRatio = 1f,
                BrownoutTier = LogisticsBrownoutTier.None
            };
        }

        public void CompleteNodeStatePublish()
        {
            if (!_publishNodeStatesPending)
                return;

            _publishNodeStatesJobHandle.Complete();
            _publishNodeStatesJobHandle = default;
            _publishNodeStatesPending = false;
        }

        public void PublishNodeStateSynchronously()
        {
            CompleteNodeStatePublish();
            PublishNodeStatesMainThread();
        }

        public void ClearPublishedNodeStates()
        {
            CompleteNodeStatePublish();

            if (_publishedNodeStateMap.IsCreated)
                _publishedNodeStateMap.Clear();
        }

        public bool TryGetPublishedNodeState(uint nodeId, out ushort stateBits)
        {
            CompleteNodeStatePublish();

            if (!_publishedNodeStateMap.IsCreated)
            {
                stateBits = 0;
                return false;
            }

            return _publishedNodeStateMap.TryGetValue(nodeId, out stateBits);
        }

        public int TraverseReachableFrom(int startNodeIndex)
        {
            return TraverseReachableFromInternal(startNodeIndex);
        }

        private float ComputeTotalGeneration()
        {
            float totalGeneration = 0f;
            int producerCount = _producers.Length;
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                if (_producerMap.TryGetValue(nodeIndex, out float productionRate))
                    totalGeneration += productionRate;
            }

            return totalGeneration;
        }

        private float ComputeTotalConsumption()
        {
            float totalConsumption = 0f;
            int consumerCount = _consumers.Length;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
                totalConsumption += _consumers[consumerIndex].Demand;

            return totalConsumption;
        }

        private void SeedProducerPotentials()
        {
            int producerCount = _producers.Length;
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                if (!IsValidNodeIndex(nodeIndex))
                    continue;

                if (!_producerMap.TryGetValue(nodeIndex, out float productionRate))
                    continue;

                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Potential = math.max(node.Potential, productionRate);
                _nodeBuffer[nodeIndex] = node;
            }
        }

        private void SeedConsumerPotentials(float supplyRatio)
        {
            int consumerCount = _consumers.Length;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                ConsumerRecord consumer = _consumers[consumerIndex];
                if (!IsValidNodeIndex(consumer.NodeIndex))
                    continue;

                LogisticsNode node = _nodeBuffer[consumer.NodeIndex];
                float scaledDemand = consumer.Demand * supplyRatio;
                if (scaledDemand > node.Potential)
                    node.Potential = scaledDemand;

                _nodeBuffer[consumer.NodeIndex] = node;
            }
        }

        private void ResetDistributionState()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.CurrentLoad = 0f;
                node.Potential = 0f;
                node.Flags &= ~(LogisticsNodeFlags.Brownout | LogisticsNodeFlags.Overloaded | LogisticsNodeFlags.Dirty);
                _nodeBuffer[nodeIndex] = node;
                _nodeNetInjection[nodeIndex] = 0f;
                _nodePotentialFront[nodeIndex] = 0f;
                _nodePotentialBack[nodeIndex] = 0f;
                _nodeServedDemand[nodeIndex] = 0f;
                _componentGeneration[nodeIndex] = 0f;
                _componentDemand[nodeIndex] = 0f;
                _componentServedDemand[nodeIndex] = 0f;
                _componentRemainingSupply[nodeIndex] = 0f;
                _componentSupplyRatio[nodeIndex] = 1f;
                _componentResidualInjection[nodeIndex] = 0f;
                _componentAnchorNode[nodeIndex] = -1;
                _componentBrownoutTier[nodeIndex] = (byte)LogisticsBrownoutTier.None;
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
                    _componentAnchorNode[componentIndex] = nodeIndex;

                if (_producerMap.TryGetValue(nodeIndex, out float productionRate))
                    _componentGeneration[componentIndex] += productionRate;
            }

            int consumerCount = _consumers.Length;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                ConsumerRecord consumer = _consumers[consumerIndex];
                int componentIndex = _componentIds[consumer.NodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                _componentDemand[componentIndex] += consumer.Demand;
            }

            for (int componentIndex = 0; componentIndex < _nodeCount; componentIndex++)
            {
                if (_componentAnchorNode[componentIndex] < 0)
                    continue;

                float demand = _componentDemand[componentIndex];
                float generation = _componentGeneration[componentIndex];
                float supplyRatio = demand > Epsilon
                    ? math.saturate(generation / demand)
                    : 1f;

                _componentRemainingSupply[componentIndex] = generation;
                _componentSupplyRatio[componentIndex] = supplyRatio;
                _componentBrownoutTier[componentIndex] = (byte)ResolveBrownoutTier(supplyRatio);
            }
        }

        private void AllocateServedDemand(ref DistributionSummary summary)
        {
            int consumerCount = ConsumerCount;
            if (consumerCount <= 0)
                return;

            int poweredCount = 0;
            float servedDemand = 0f;

            for (byte priorityTier = 0; priorityTier <= 3; priorityTier++)
            {
                for (int priority = MinPriority; priority <= MaxPriority; priority++)
                {
                    for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
                    {
                        ConsumerRecord consumer = _consumers[consumerIndex];
                        if (consumer.PriorityTier != priorityTier || consumer.PowerPriority != priority)
                            continue;

                        int componentIndex = _componentIds[consumer.NodeIndex];
                        LogisticsBrownoutTier brownoutTier = componentIndex >= 0 && componentIndex < _nodeCount
                            ? (LogisticsBrownoutTier)_componentBrownoutTier[componentIndex]
                            : LogisticsBrownoutTier.EmergencyOnly;

                        if (ShouldMarkBrownout(in consumer, brownoutTier))
                            MarkBrownoutNode(consumer.NodeIndex);

                        if (!CanServeConsumer(in consumer, brownoutTier))
                        {
                            MarkBrownoutNode(consumer.NodeIndex);
                            continue;
                        }

                        if (componentIndex < 0 || componentIndex >= _nodeCount)
                        {
                            MarkBrownoutNode(consumer.NodeIndex);
                            continue;
                        }

                        float remainingSupply = _componentRemainingSupply[componentIndex];
                        if (remainingSupply + Epsilon < consumer.Demand)
                        {
                            MarkBrownoutNode(consumer.NodeIndex);
                            continue;
                        }

                        _componentRemainingSupply[componentIndex] = remainingSupply - consumer.Demand;
                        _componentServedDemand[componentIndex] += consumer.Demand;
                        _nodeServedDemand[consumer.NodeIndex] += consumer.Demand;
                        _consumerStates[consumerIndex] = 1;
                        poweredCount++;
                        servedDemand += consumer.Demand;
                    }
                }
            }

            summary.PoweredCount = poweredCount;
            summary.DisabledCount = consumerCount - poweredCount;
            summary.ServedDemand = servedDemand;
        }

        private void BuildNodeInjection()
        {
            int producerCount = _producers.Length;
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                int componentIndex = _componentIds[nodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                if (!_producerMap.TryGetValue(nodeIndex, out float productionRate))
                    continue;

                float componentGeneration = _componentGeneration[componentIndex];
                float componentServedDemand = _componentServedDemand[componentIndex];
                if (componentGeneration <= Epsilon || componentServedDemand <= Epsilon)
                    continue;

                float dispatchedGeneration = productionRate * math.saturate(componentServedDemand / componentGeneration);
                _nodeNetInjection[nodeIndex] += dispatchedGeneration;
            }

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                _nodeNetInjection[nodeIndex] -= _nodeServedDemand[nodeIndex];
                int componentIndex = _componentIds[nodeIndex];
                if (componentIndex < 0 || componentIndex >= _nodeCount)
                    continue;

                _componentResidualInjection[componentIndex] += _nodeNetInjection[nodeIndex];
            }

            for (int componentIndex = 0; componentIndex < _nodeCount; componentIndex++)
            {
                int anchorNodeIndex = _componentAnchorNode[componentIndex];
                if (!IsValidNodeIndex(anchorNodeIndex))
                    continue;

                float residual = _componentResidualInjection[componentIndex];
                if (math.abs(residual) > Epsilon)
                    _nodeNetInjection[anchorNodeIndex] -= residual;
            }
        }

        private void RelaxNodePotentials()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                _nodePotentialFront[nodeIndex] = _nodeNetInjection[nodeIndex];
                _nodePotentialBack[nodeIndex] = _nodeNetInjection[nodeIndex];
            }

            for (int iteration = 0; iteration < MaxJacobiIterations; iteration++)
            {
                float maxDelta = 0f;

                for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                {
                    int componentIndex = _componentIds[nodeIndex];
                    if (componentIndex < 0 || _componentAnchorNode[componentIndex] == nodeIndex)
                    {
                        _nodePotentialBack[nodeIndex] = 0f;
                        continue;
                    }

                    float conductanceSum = 0f;
                    float weightedPotential = 0f;

                    int edgeStart = _edgeOffsets[nodeIndex];
                    int edgeEnd = _edgeOffsets[nodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = _edgeDestinations[edgeIndex];
                        if (!IsValidNodeIndex(neighborNodeIndex) || _componentIds[neighborNodeIndex] != componentIndex)
                            continue;

                        float conductance = 1f / ResolveCombinedResistance(nodeIndex, neighborNodeIndex, edgeIndex);
                        conductanceSum += conductance;
                        weightedPotential += conductance * _nodePotentialFront[neighborNodeIndex];
                    }

                    if (conductanceSum <= Epsilon)
                    {
                        _nodePotentialBack[nodeIndex] = 0f;
                        continue;
                    }

                    float relaxedPotential = (weightedPotential + _nodeNetInjection[nodeIndex]) / conductanceSum;
                    if (!math.isfinite(relaxedPotential))
                        relaxedPotential = 0f;

                    _nodePotentialBack[nodeIndex] = relaxedPotential;
                    float delta = math.abs(relaxedPotential - _nodePotentialFront[nodeIndex]);
                    if (delta > maxDelta)
                        maxDelta = delta;
                }

                NativeArray<float> swap = _nodePotentialFront;
                _nodePotentialFront = _nodePotentialBack;
                _nodePotentialBack = swap;

                if (maxDelta < JacobiConvergenceDelta)
                    break;
            }

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Potential = _nodePotentialFront[nodeIndex];
                _nodeBuffer[nodeIndex] = node;
            }
        }

        private void AccumulateNodeLoadsFromPotentials()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                float branchLoad = 0f;

                int edgeStart = _edgeOffsets[nodeIndex];
                int edgeEnd = _edgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationNodeIndex = _edgeDestinations[edgeIndex];
                    if (!IsValidNodeIndex(destinationNodeIndex))
                        continue;

                    float resistance = ResolveCombinedResistance(nodeIndex, destinationNodeIndex, edgeIndex);
                    float flowRate = math.abs((_nodePotentialFront[nodeIndex] - _nodePotentialFront[destinationNodeIndex]) / resistance);
                    if (flowRate > Epsilon)
                        branchLoad += flowRate;
                }

                node.CurrentLoad = math.max(branchLoad, _nodeServedDemand[nodeIndex] + math.max(0f, _nodeNetInjection[nodeIndex]));
                if (node.CurrentLoad > node.Capacity * 1.15f)
                    node.Flags |= LogisticsNodeFlags.Overloaded;

                _nodeBuffer[nodeIndex] = node;
            }
        }

        private void ResetTopologyScratch(int nodeCount)
        {
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                _parents[nodeIndex] = nodeIndex;
                _ranks[nodeIndex] = 0;
                _componentIds[nodeIndex] = -1;
                _componentSizes[nodeIndex] = 0;
                _rootToComponent[nodeIndex] = -1;
                _visited[nodeIndex] = 0;
            }

            _bfsQueue.Clear();
        }

        private void ResetConsumerStates(int consumerCount)
        {
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
                _consumerStates[consumerIndex] = 0;
        }

        private void ClearNodeFlags(LogisticsNodeFlags flags)
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
            {
                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Flags &= ~flags;
                _nodeBuffer[nodeIndex] = node;
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

                _nodeBuffer[nodeIndex] = node;
            }
        }

        private void MarkBrownoutNode(int nodeIndex)
        {
            if (!IsValidNodeIndex(nodeIndex))
                return;

            LogisticsNode node = _nodeBuffer[nodeIndex];
            node.Flags |= LogisticsNodeFlags.Brownout;
            _nodeBuffer[nodeIndex] = node;
        }

        private int TraverseReachableFromInternal(int startNodeIndex)
        {
            if (!IsValidNodeIndex(startNodeIndex))
                return 0;

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _visited[nodeIndex] = 0;

            _bfsQueue.Clear();
            _visited[startNodeIndex] = 1;
            _bfsQueue.Enqueue(startNodeIndex);

            int visitedCount = 0;
            int traversalWatchdog = math.max(1, _nodeCount + 1);
            while (_bfsQueue.Count > 0 && traversalWatchdog-- > 0)
            {
                int nodeIndex = _bfsQueue.Dequeue();
                visitedCount++;

                int edgeStart = _edgeOffsets[nodeIndex];
                int edgeEnd = _edgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationNodeIndex = _edgeDestinations[edgeIndex];
                    if (!IsValidNodeIndex(destinationNodeIndex) || _visited[destinationNodeIndex] != 0)
                        continue;

                    _visited[destinationNodeIndex] = 1;
                    _bfsQueue.Enqueue(destinationNodeIndex);
                }
            }

            if (_bfsQueue.Count > 0)
                _bfsQueue.Clear();

            return visitedCount;
        }

        private int TraverseProducerReachability()
        {
            return TraverseProducerReachability(false);
        }

        private int TraverseProducerReachability(bool accumulateFlow)
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _visited[nodeIndex] = 0;

            _bfsQueue.Clear();

            int producerCount = _producers.Length;
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                if (!IsValidNodeIndex(nodeIndex) || _visited[nodeIndex] != 0)
                    continue;

                _visited[nodeIndex] = 1;
                _bfsQueue.Enqueue(nodeIndex);
            }

            int visitedCount = 0;
            int traversalWatchdog = math.max(1, _nodeCount + 1);
            while (_bfsQueue.Count > 0 && traversalWatchdog-- > 0)
            {
                int sourceNodeIndex = _bfsQueue.Dequeue();
                visitedCount++;

                int edgeStart = _edgeOffsets[sourceNodeIndex];
                int edgeEnd = _edgeOffsets[sourceNodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationNodeIndex = _edgeDestinations[edgeIndex];
                    if (!IsValidNodeIndex(destinationNodeIndex))
                        continue;

                    if (accumulateFlow)
                        AccumulateDirectedFlow(sourceNodeIndex, destinationNodeIndex, edgeIndex);

                    if (_visited[destinationNodeIndex] != 0)
                        continue;

                    _visited[destinationNodeIndex] = 1;
                    _bfsQueue.Enqueue(destinationNodeIndex);
                }
            }

            if (_bfsQueue.Count > 0)
                _bfsQueue.Clear();

            return visitedCount;
        }

        private void AccumulateDirectedFlow(int sourceNodeIndex, int destinationNodeIndex, int edgeIndex)
        {
            LogisticsNode sourceNode = _nodeBuffer[sourceNodeIndex];
            LogisticsNode destinationNode = _nodeBuffer[destinationNodeIndex];
            float combinedResistance = math.max(
                MinResistance,
                _edgeResistance[edgeIndex] + sourceNode.Resistance + destinationNode.Resistance);

            if (ShouldApplyGraphRuptureResistance(sourceNode.Flags, destinationNode.Flags))
            {
                combinedResistance *= RuptureResistanceMultiplier;
            }

            float propagatedPotential = sourceNode.Potential * (1f / (1f + combinedResistance));
            if (propagatedPotential <= destinationNode.Potential + Epsilon)
                return;

            float flowRate = (sourceNode.Potential - destinationNode.Potential) / combinedResistance;
            if (flowRate <= 0f)
                return;

            destinationNode.Potential = propagatedPotential;
            sourceNode.CurrentLoad += flowRate;
            destinationNode.CurrentLoad += flowRate;

            if (sourceNode.CurrentLoad > sourceNode.Capacity * 1.15f)
                sourceNode.Flags |= LogisticsNodeFlags.Overloaded;

            if (destinationNode.CurrentLoad > destinationNode.Capacity * 1.15f)
                destinationNode.Flags |= LogisticsNodeFlags.Overloaded;

            _nodeBuffer[sourceNodeIndex] = sourceNode;
            _nodeBuffer[destinationNodeIndex] = destinationNode;
        }

        private float ResolveCombinedResistance(int sourceNodeIndex, int destinationNodeIndex, int edgeIndex)
        {
            LogisticsNode sourceNode = _nodeBuffer[sourceNodeIndex];
            LogisticsNode destinationNode = _nodeBuffer[destinationNodeIndex];
            float combinedResistance = math.max(
                MinResistance,
                _edgeResistance[edgeIndex] + sourceNode.Resistance + destinationNode.Resistance);

            if (ShouldApplyGraphRuptureResistance(sourceNode.Flags, destinationNode.Flags))
            {
                combinedResistance *= RuptureResistanceMultiplier;
            }

            return combinedResistance;
        }

        private bool ShouldApplyGraphRuptureResistance(LogisticsNodeFlags sourceFlags, LogisticsNodeFlags destinationFlags)
        {
            if (_networkType == LogisticsNetworkType.PowerDc)
                return false;

            return (sourceFlags & LogisticsNodeFlags.Ruptured) != 0 ||
                   (destinationFlags & LogisticsNodeFlags.Ruptured) != 0;
        }

        private bool CanServeConsumer(in ConsumerRecord consumer, LogisticsBrownoutTier brownoutTier)
        {
            if (!IsValidNodeIndex(consumer.NodeIndex))
                return false;

            LogisticsNode node = _nodeBuffer[consumer.NodeIndex];
            if ((node.Flags & LogisticsNodeFlags.Isolated) != 0)
                return false;

            switch (brownoutTier)
            {
                case LogisticsBrownoutTier.EmergencyOnly:
                    return (consumer.Flags & LogisticsConsumerFlags.EmergencyReserved) != 0 || consumer.PriorityTier == 0;

                case LogisticsBrownoutTier.EssentialOnly:
                    return (consumer.Flags & (LogisticsConsumerFlags.Essential | LogisticsConsumerFlags.EmergencyReserved)) != 0 ||
                           consumer.PriorityTier <= 1;

                default:
                    return true;
            }
        }

        private bool ShouldMarkBrownout(in ConsumerRecord consumer, LogisticsBrownoutTier brownoutTier)
        {
            switch (brownoutTier)
            {
                case LogisticsBrownoutTier.EmergencyOnly:
                    return (consumer.Flags & LogisticsConsumerFlags.EmergencyReserved) == 0;

                case LogisticsBrownoutTier.EssentialOnly:
                    return (consumer.Flags & (LogisticsConsumerFlags.Essential | LogisticsConsumerFlags.EmergencyReserved)) == 0;

                case LogisticsBrownoutTier.AmbientLightsOnly:
                    return consumer.PriorityTier >= 2 || (consumer.Flags & LogisticsConsumerFlags.AmbientLighting) != 0;

                default:
                    return false;
            }
        }

        private int FindRoot(int nodeIndex)
        {
            int parentIndex = _parents[nodeIndex];
            int rootWatchdog = math.max(1, _nodeCount + 1);
            while (parentIndex != _parents[parentIndex] && rootWatchdog-- > 0)
            {
                _parents[parentIndex] = _parents[_parents[parentIndex]];
                parentIndex = _parents[parentIndex];
            }

            if (rootWatchdog <= 0)
            {
                _parents[nodeIndex] = nodeIndex;
                return nodeIndex;
            }

            int currentIndex = nodeIndex;
            int compressionWatchdog = math.max(1, _nodeCount + 1);
            while (currentIndex != parentIndex && compressionWatchdog-- > 0)
            {
                int nextIndex = _parents[currentIndex];
                _parents[currentIndex] = parentIndex;
                currentIndex = nextIndex;
            }

            if (compressionWatchdog <= 0)
            {
                _parents[nodeIndex] = nodeIndex;
                return nodeIndex;
            }

            return parentIndex;
        }

        private void UnionRoots(int leftRoot, int rightRoot)
        {
            if (_ranks[leftRoot] < _ranks[rightRoot])
            {
                _parents[leftRoot] = rightRoot;
                return;
            }

            if (_ranks[leftRoot] > _ranks[rightRoot])
            {
                _parents[rightRoot] = leftRoot;
                return;
            }

            _parents[rightRoot] = leftRoot;
            _ranks[leftRoot] = _ranks[leftRoot] + 1;
        }

        private bool IsValidNodeIndex(int nodeIndex)
        {
            return nodeIndex >= 0 && nodeIndex < _nodeCount;
        }

        private void PublishNodeStatesMainThread()
        {
            if (_nodeCount <= 0)
            {
                ClearPublishedNodeStates();
                return;
            }

            EnsurePublishedStateCapacity(_nodeCount);
            _publishedNodeStateMap.Clear();

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
                _publishedNodeStates[nodeIndex] = bitmask;
                _publishedNodeStateMap.TryAdd(node.Id, bitmask);
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
            EnsureNodeArrayCapacity(ref _nodeBuffer, safeLength);
            EnsureIntArrayCapacity(ref _edgeOffsets, safeLength + 1);
            EnsureIntArrayCapacity(ref _edgeWriteCursor, safeLength);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureIntArrayCapacity(ref _edgeDestinations, safeLength);
            EnsureFloatArrayCapacity(ref _edgeResistance, safeLength);
        }

        private void EnsureTopologyCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_topologyEdgeList.Capacity < safeLength)
                _topologyEdgeList.Capacity = safeLength;
        }

        private void EnsureProducerCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_producers.Capacity < safeLength)
                _producers.Capacity = safeLength;

            EnsureFloatMapCapacity(ref _producerMap, safeLength);
            EnsureFloatMapCapacity(ref _consumerMap, safeLength);
        }

        private void EnsureConsumerCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_consumers.Capacity < safeLength)
                _consumers.Capacity = safeLength;
        }

        private void EnsureWorkingCapacity(int nodeCount, int consumerCount)
        {
            EnsureIntArrayCapacity(ref _parents, nodeCount);
            EnsureIntArrayCapacity(ref _ranks, nodeCount);
            EnsureIntArrayCapacity(ref _componentIds, nodeCount);
            EnsureIntArrayCapacity(ref _componentSizes, nodeCount);
            EnsureIntArrayCapacity(ref _rootToComponent, nodeCount);
            EnsureIntArrayCapacity(ref _traversalQueue, nodeCount);
            EnsureByteArrayCapacity(ref _visited, nodeCount);
            EnsureByteArrayCapacity(ref _consumerStates, consumerCount);
            EnsureFloatArrayCapacity(ref _nodeNetInjection, nodeCount);
            EnsureFloatArrayCapacity(ref _nodePotentialFront, nodeCount);
            EnsureFloatArrayCapacity(ref _nodePotentialBack, nodeCount);
            EnsureFloatArrayCapacity(ref _nodeServedDemand, nodeCount);
            EnsureFloatArrayCapacity(ref _componentGeneration, nodeCount);
            EnsureFloatArrayCapacity(ref _componentDemand, nodeCount);
            EnsureFloatArrayCapacity(ref _componentServedDemand, nodeCount);
            EnsureFloatArrayCapacity(ref _componentRemainingSupply, nodeCount);
            EnsureFloatArrayCapacity(ref _componentSupplyRatio, nodeCount);
            EnsureFloatArrayCapacity(ref _componentResidualInjection, nodeCount);
            EnsureIntArrayCapacity(ref _componentAnchorNode, nodeCount);
            EnsureByteArrayCapacity(ref _componentBrownoutTier, nodeCount);
            EnsurePublishedStateCapacity(nodeCount);
        }

        private void EnsureSummaryBuffers()
        {
            EnsureTopologySummaryCapacity(ref _scheduledTopologySummary);
            EnsureDistributionSummaryCapacity(ref _scheduledDistributionSummary);
        }

        private static void EnsureNodeArrayCapacity(ref NativeArray<LogisticsNode> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<LogisticsNode>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureIntArrayCapacity(ref NativeArray<int> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureFloatArrayCapacity(ref NativeArray<float> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<float>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureByteArrayCapacity(ref NativeArray<byte> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<byte>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void EnsurePublishedStateCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureUShortArrayCapacity(ref _publishedNodeStates, safeLength);
            EnsureUShortMapCapacity(ref _publishedNodeStateMap, safeLength);
        }

        private static void EnsureUShortArrayCapacity(ref NativeArray<ushort> array, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<ushort>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureFloatMapCapacity(ref NativeParallelHashMap<int, float> map, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (map.IsCreated && map.Capacity >= safeLength)
                return;

            if (map.IsCreated)
                map.Dispose();

            map = new NativeParallelHashMap<int, float>(safeLength, Allocator.Persistent);
        }

        private static void EnsureUShortMapCapacity(ref NativeParallelHashMap<uint, ushort> map, int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (map.IsCreated && map.Capacity >= safeLength)
                return;

            if (map.IsCreated)
                map.Dispose();

            map = new NativeParallelHashMap<uint, ushort>(safeLength, Allocator.Persistent);
        }

        private static void EnsureTopologySummaryCapacity(ref NativeArray<TopologySummary> array)
        {
            if (array.IsCreated && array.Length >= 1)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<TopologySummary>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureDistributionSummaryCapacity(ref NativeArray<DistributionSummary> array)
        {
            if (array.IsCreated && array.Length >= 1)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<DistributionSummary>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }
    }
}
