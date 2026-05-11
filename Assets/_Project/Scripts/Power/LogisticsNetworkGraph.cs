using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.World;
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

    /// <summary>
    /// Native-backed logistics graph kernel used by power and oxygen topologies.
    /// Runtime traversal reads CSR adjacency only: EdgeOffsets + EdgeDestinations + EdgeConductance.
    /// </summary>
    public sealed class LogisticsNetworkGraph : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 32)]
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct PublishNodeStatesJob : IJobParallelFor
        {
            private const float PublishEpsilon = 0.0001f;

            [ReadOnly] public NativeArray<LogisticsNode> Nodes;
            [ReadOnly] public NativeArray<float> NodeNetInjection;
            [ReadOnly] public NativeArray<float> NodeServedDemand;
            [ReadOnly] public NativeArray<float> NodeVoltageSupplyRatio;

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
                PublishedNodeStateMap.TryAdd(node.Id, bitmask);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateGraphJob : IJob
        {
            public LogisticsNetworkType NetworkType;
            public int NodeCount;
            public int EdgeCount;
            public int ConsumerCount;
            public int SolveStartNode;
            public int SolveNodeCount;
            public byte RelaxationSliceOnly;

            public NativeArray<LogisticsNode> Nodes;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<float> EdgeConductance;
            [ReadOnly] public NativeArray<float> EdgeCapacity;
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
            public NativeArray<float> NodeServedDemand;
            public NativeArray<float> NodeVoltageSupplyRatio;
            public NativeArray<float> NodeSourcePotential;
            public NativeArray<float> NodeConductanceSum;
            public NativeArray<float> NodeConductanceInverseSum;
            public NativeArray<float> PotentialFront;
            public NativeArray<float> PotentialBack;
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
                if (RelaxationSliceOnly != 0)
                {
                    int topologyCycleCount = TopologySummaryBuffer.IsCreated && TopologySummaryBuffer.Length > 0
                        ? TopologySummaryBuffer[0].CycleCount
                        : 0;
                    ApplyJacobiPowerRelaxation(topologyCycleCount);
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
                summary.HasDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption;
                summary.BrownoutTier = ResolveBrownoutTier(summary.SupplyRatio);

                ResetConsumerStates();
                ResetDistributionState();
                BuildComponentDistributionState();
                AllocateServedDemand(ref summary);
                BuildNodeInjection();
                summary.UnservedDemand = math.max(0f, summary.TotalConsumption - summary.ServedDemand);
                summary.HasDeficit = summary.UnservedDemand > Epsilon;
                summary.BrownoutTier = summary.HasDeficit
                    ? LogisticsBrownoutTier.EmergencyOnly
                    : LogisticsBrownoutTier.None;
                ApplyBinaryNodeLoads();
                ApplyJacobiPowerRelaxation(topologyCycleCount);
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

                    if (ProducerMap.TryGetValue(nodeIndex, out float productionRate))
                    {
                        ComponentGeneration[componentIndex] += productionRate;
                        NodeSourcePotential[nodeIndex] = productionRate;
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
                }
            }

            private void ApplyJacobiPowerRelaxation(int topologyCycleCount)
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

                bool initializePotentialBuffers = RelaxationSliceOnly == 0;
                if (initializePotentialBuffers)
                {
                    for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                    {
                        float sourcePotential = NodeSourcePotential[nodeIndex];
                        float initialPotential = sourcePotential > Epsilon
                            ? math.max(0f, sourcePotential)
                            : math.max(0f, Nodes[nodeIndex].Potential);
                        PotentialFront[nodeIndex] = initialPotential;
                        PotentialBack[nodeIndex] = initialPotential;
                    }
                }

                ResolveSolveWindow(out int solveStartNode, out int solveEndNode);
                if (solveEndNode <= solveStartNode)
                    return;

                NativeArray<float> input = PotentialFront;
                NativeArray<float> output = PotentialBack;
                int iterationBudget = topologyCycleCount > 0
                    ? LoopedJacobiRelaxationIterations
                    : RadialJacobiRelaxationIterations;
                for (int iteration = 0; iteration < iterationBudget; iteration++)
                {
                    for (int nodeIndex = solveStartNode; nodeIndex < solveEndNode; nodeIndex++)
                    {
                        LogisticsNode node = Nodes[nodeIndex];
                        if ((node.Flags & (LogisticsNodeFlags.Isolated | LogisticsNodeFlags.Ruptured)) != 0)
                        {
                            output[nodeIndex] = 0f;
                            continue;
                        }

                        float sourcePotential = NodeSourcePotential[nodeIndex];
                        if (sourcePotential > Epsilon)
                        {
                            output[nodeIndex] = math.max(0f, sourcePotential);
                            continue;
                        }

                        float weightedNeighborPotential = 0f;
                        int edgeStart = EdgeOffsets[nodeIndex];
                        int edgeEnd = EdgeOffsets[nodeIndex + 1];
                        for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                        {
                            if ((EdgeStates[edgeIndex] & (byte)LogisticsEdgeState.Ruptured) != 0)
                                continue;

                            int destinationNodeIndex = EdgeDestinations[edgeIndex];
                            float conductance = EdgeConductance[edgeIndex];
                            weightedNeighborPotential += input[destinationNodeIndex] * conductance;
                        }

                        float injectionWatts = NodeNetInjection[nodeIndex];
                        float inverseConductanceSum = NodeConductanceInverseSum[nodeIndex];
                        float nextPotential = inverseConductanceSum > 0f
                            ? (weightedNeighborPotential + injectionWatts) * inverseConductanceSum
                            : math.max(0f, injectionWatts);
                        float currentPotential = math.max(0f, input[nodeIndex]);
                        float dampedPotential =
                            (currentPotential * JacobiDampingRetainedPotential) +
                            (math.max(0f, nextPotential) * JacobiDampingOmega);
                        output[nodeIndex] = dampedPotential;
                    }

                    NativeArray<float> swap = input;
                    input = output;
                    output = swap;
                }

                for (int nodeIndex = solveStartNode; nodeIndex < solveEndNode; nodeIndex++)
                {
                    LogisticsNode node = Nodes[nodeIndex];
                    node.Potential = input[nodeIndex];
                    node.CurrentLoad = math.max(node.CurrentLoad, math.abs(NodeNetInjection[nodeIndex]));
                    Nodes[nodeIndex] = node;
                    PotentialFront[nodeIndex] = input[nodeIndex];
                    PotentialBack[nodeIndex] = input[nodeIndex];
                }

                for (int sourceNodeIndex = solveStartNode; sourceNodeIndex < solveEndNode; sourceNodeIndex++)
                {
                    int edgeStart = EdgeOffsets[sourceNodeIndex];
                    int edgeEnd = EdgeOffsets[sourceNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int destinationNodeIndex = EdgeDestinations[edgeIndex];
                        byte edgeState = EdgeStates[edgeIndex];
                        if ((edgeState & (byte)LogisticsEdgeState.Ruptured) != 0)
                        {
                            EdgeFlow[edgeIndex] = 0f;
                            continue;
                        }

                        float flow = (input[sourceNodeIndex] - input[destinationNodeIndex]) * EdgeConductance[edgeIndex];
                        float absFlow = math.abs(flow);
                        float edgeCapacity = EdgeCapacity[edgeIndex];

                        if (absFlow > edgeCapacity * RuptureFlowMultiplier)
                        {
                            edgeState |= (byte)(LogisticsEdgeState.Overloaded | LogisticsEdgeState.Ruptured);
                            flow = 0f;
                            absFlow = 0f;
                            RemoveRupturedEdgeFromConductance(sourceNodeIndex, EdgeConductance[edgeIndex]);
                            DecrementRuntimeConductiveEdgeCount();
                            MarkNodeOverloaded(sourceNodeIndex);
                            MarkNodeOverloaded(destinationNodeIndex);
                        }
                        else if (absFlow > edgeCapacity)
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
                        AccumulateNodeLoad(sourceNodeIndex, absFlow);
                        AccumulateNodeLoad(destinationNodeIndex, absFlow);
                    }
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

                int producerCount = Producers.Length;
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

            private bool IsEdgeRuptured(int edgeIndex)
            {
                return (EdgeStates[edgeIndex] & (byte)LogisticsEdgeState.Ruptured) != 0;
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
        private const int MaxSearchDepth = 100;
        private const int ParallelNodeBatchSize = 64;
        private const int RadialJacobiRelaxationIterations = 1;
        private const int LoopedJacobiRelaxationIterations = 2;
        private const int AdaptiveSolveNodeThreshold = 500;
        private const int AdaptiveSolveNodesPerFrame = 250;
        private const float MinResistance = 0.0001f;
        private const float Epsilon = 0.001f;
        private const float JacobiDampingOmega = 0.6f;
        private const float JacobiDampingRetainedPotential = 1f - JacobiDampingOmega;
        private const float RuptureFlowMultiplier = 1.15f;
        private const string NativeMemoryOwner = nameof(LogisticsNetworkGraph);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;

        private LogisticsNetworkType _networkType;
        private int _nodeCount;
        private int _edgeCount;
        private int _conductiveEdgeCount;

        private NativeArray<LogisticsNode> _nodeBuffer;
        private NativeArray<int> _edgeOffsets;
        private NativeArray<int> _edgeDestinations;
        private NativeArray<float> _edgeConductance;
        private NativeArray<float> _edgeCapacity;
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
        private NativeArray<float> _nodeServedDemand;
        private NativeArray<float> _nodeVoltageSupplyRatio;
        private NativeArray<float> _nodeSourcePotential;
        private NativeArray<float> _nodeConductanceSum;
        private NativeArray<float> _nodeConductanceInverseSum;
        private NativeArray<float> _potentialFront;
        private NativeArray<float> _potentialBack;
        private NativeArray<float> _edgeFlow;
        private NativeArray<byte> _edgeStates;
        private NativeArray<int2> _edgeKeys;
        private NativeArray<int> _runtimeConductiveEdgeCount;
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
        private NativeArray<ushort> _publishedNodeStatesBack;
        private NativeParallelHashMap<uint, ushort> _publishedNodeStateMap;
        private NativeParallelHashMap<uint, ushort> _publishedNodeStateBackMap;
        private NativeQueue<int> _bfsQueue;
        private static int _nextSentinelInstanceId;
        private readonly string _bfsQueueSentinelLabel;
        private JobHandle _evaluateGraphJobHandle;
        private bool _evaluateGraphPending;
        private JobHandle _publishNodeStatesJobHandle;
        private bool _publishNodeStatesPending;
        private TopologySummary _committedTopologySummary;
        private DistributionSummary _committedDistributionSummary;
        private int _adaptiveSolveCursor;
        private int _adaptiveSolveRemainingNodes;
        private int _scheduledSolveNodeCount;
        private bool _scheduledAdaptiveSolveSlice;
        private bool _buildOpen;

        public LogisticsNetworkGraph(int nodeCapacity = 16, int edgeCapacity = 32, int consumerCapacity = 16)
        {
            int safeNodeCapacity = math.max(1, nodeCapacity);
            int safeEdgeCapacity = math.max(1, edgeCapacity);
            int safeConsumerCapacity = math.max(1, consumerCapacity);
            _bfsQueueSentinelLabel = string.Concat(nameof(_bfsQueue), "_", ++_nextSentinelInstanceId);

            // COLD ALLOC: LogisticsNode[nodeCapacity] — runtime node buffer — owner: LogisticsNetworkGraph
            _nodeBuffer = new NativeArray<LogisticsNode>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_nodeBuffer, nameof(_nodeBuffer));
            // COLD ALLOC: int[nodeCapacity + 1] — CSR edge offsets — owner: LogisticsNetworkGraph
            _edgeOffsets = new NativeArray<int>(safeNodeCapacity + 1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_edgeOffsets, nameof(_edgeOffsets));
            // COLD ALLOC: int[edgeCapacity] — CSR edge destinations — owner: LogisticsNetworkGraph
            _edgeDestinations = new NativeArray<int>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_edgeDestinations, nameof(_edgeDestinations));
            // COLD ALLOC: float[edgeCapacity] — CSR edge conductance weights — owner: LogisticsNetworkGraph
            _edgeConductance = new NativeArray<float>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[edgeCapacity] - precomputed reciprocal wire resistance - owner: LogisticsNetworkGraph
            RegisterNativeArray(_edgeConductance, nameof(_edgeConductance));
            _edgeCapacity = new NativeArray<float>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[edgeCapacity] - precomputed directed edge capacity - owner: LogisticsNetworkGraph
            RegisterNativeArray(_edgeCapacity, nameof(_edgeCapacity));
            _edgeFlow = new NativeArray<float>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[edgeCapacity] - Jacobi directed edge flow cache - owner: LogisticsNetworkGraph
            RegisterNativeArray(_edgeFlow, nameof(_edgeFlow));
            _edgeStates = new NativeArray<byte>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: byte[edgeCapacity] - wire overload/rupture state flags - owner: LogisticsNetworkGraph
            RegisterNativeArray(_edgeStates, nameof(_edgeStates));
            _edgeKeys = new NativeArray<int2>(safeEdgeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int2[edgeCapacity] - edge state identity guard - owner: LogisticsNetworkGraph
            RegisterNativeArray(_edgeKeys, nameof(_edgeKeys));
            // COLD ALLOC: int[nodeCapacity] — CSR write cursor scratch — owner: LogisticsNetworkGraph
            _edgeWriteCursor = new NativeArray<int>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_edgeWriteCursor, nameof(_edgeWriteCursor));
            _potentialFront = new NativeArray<float>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[nodeCapacity] - Jacobi potential front buffer - owner: LogisticsNetworkGraph
            RegisterNativeArray(_potentialFront, nameof(_potentialFront));
            _potentialBack = new NativeArray<float>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[nodeCapacity] - Jacobi potential back buffer - owner: LogisticsNetworkGraph
            RegisterNativeArray(_potentialBack, nameof(_potentialBack));
            _nodeConductanceSum = new NativeArray<float>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[nodeCapacity] - precomputed outgoing conductance sum - owner: LogisticsNetworkGraph
            RegisterNativeArray(_nodeConductanceSum, nameof(_nodeConductanceSum));
            _nodeConductanceInverseSum = new NativeArray<float>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: float[nodeCapacity] - precomputed inverse outgoing conductance sum - owner: LogisticsNetworkGraph
            RegisterNativeArray(_nodeConductanceInverseSum, nameof(_nodeConductanceInverseSum));
            _runtimeConductiveEdgeCount = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: int[1] - runtime conductive edge count after ruptures - owner: LogisticsNetworkGraph
            RegisterNativeArray(_runtimeConductiveEdgeCount, nameof(_runtimeConductiveEdgeCount));
            // COLD ALLOC: TopologyEdgeRecord[edgeCapacity] — topology mutation source — owner: LogisticsNetworkGraph
            _topologyEdgeList = new NativeList<TopologyEdgeRecord>(safeEdgeCapacity, Allocator.Persistent);
            RegisterNativeList(_topologyEdgeList, nameof(_topologyEdgeList));
            // COLD ALLOC: ProducerRecord[nodeCapacity] — producer seed list — owner: LogisticsNetworkGraph
            _producers = new NativeList<ProducerRecord>(safeNodeCapacity, Allocator.Persistent);
            RegisterNativeList(_producers, nameof(_producers));
            // COLD ALLOC: ConsumerRecord[consumerCapacity] — consumer demand list — owner: LogisticsNetworkGraph
            _consumers = new NativeList<ConsumerRecord>(safeConsumerCapacity, Allocator.Persistent);
            RegisterNativeList(_consumers, nameof(_consumers));
            // COLD ALLOC: NativeParallelHashMap<int,float>[nodeCapacity] — producer aggregation map — owner: LogisticsNetworkGraph
            _producerMap = new NativeParallelHashMap<int, float>(safeNodeCapacity, Allocator.Persistent);
            RegisterNativeParallelHashMap(_producerMap, nameof(_producerMap));
            // COLD ALLOC: NativeParallelHashMap<int,float>[nodeCapacity] — consumer aggregation map — owner: LogisticsNetworkGraph
            _consumerMap = new NativeParallelHashMap<int, float>(safeNodeCapacity, Allocator.Persistent);
            RegisterNativeParallelHashMap(_consumerMap, nameof(_consumerMap));
            // COLD ALLOC: NativeArray<ushort>[nodeCapacity] — published node-state bitmasks — owner: LogisticsNetworkGraph
            _publishedNodeStates = new NativeArray<ushort>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_publishedNodeStates, nameof(_publishedNodeStates));
            // COLD ALLOC: NativeArray<ushort>[nodeCapacity] — back-buffer node-state bitmasks for async publish — owner: LogisticsNetworkGraph
            _publishedNodeStatesBack = new NativeArray<ushort>(safeNodeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(_publishedNodeStatesBack, nameof(_publishedNodeStatesBack));
            // COLD ALLOC: NativeParallelHashMap<uint,ushort>[nodeCapacity] — published node-state lookup by stable node id — owner: LogisticsNetworkGraph
            _publishedNodeStateMap = new NativeParallelHashMap<uint, ushort>(safeNodeCapacity, Allocator.Persistent);
            RegisterNativeParallelHashMap(_publishedNodeStateMap, nameof(_publishedNodeStateMap));
            // COLD ALLOC: NativeParallelHashMap<uint,ushort>[nodeCapacity] — back-buffer node-state lookup for async publish — owner: LogisticsNetworkGraph
            _publishedNodeStateBackMap = new NativeParallelHashMap<uint, ushort>(safeNodeCapacity, Allocator.Persistent);
            RegisterNativeParallelHashMap(_publishedNodeStateBackMap, nameof(_publishedNodeStateBackMap));
            // COLD ALLOC: NativeQueue<int>[nodeCapacity] — iterative BFS frontier — owner: LogisticsNetworkGraph
            _bfsQueue = new NativeQueue<int>(Allocator.Persistent);
            NativeMemorySentinel.RegisterNativeQueue(
                _bfsQueue,
                safeNodeCapacity,
                NativeMemoryOwner,
                _bfsQueueSentinelLabel,
                NativeMemoryLifetime);
            PrewarmQueue(ref _bfsQueue, safeNodeCapacity);
            _committedDistributionSummary = new DistributionSummary
            {
                SupplyRatio = 1f,
                BrownoutTier = LogisticsBrownoutTier.None
            };
        }

        public int NodeCount => _nodeCount;
        public int EdgeCount => _edgeCount;
        public int ConsumerCount => _consumers.IsCreated ? _consumers.Length : 0;
        public bool HasPendingEvaluation => _evaluateGraphPending;
        public bool HasPendingNodeStatePublish => _publishNodeStatesPending;
        public bool UsesAdaptiveEvaluation => _nodeCount > AdaptiveSolveNodeThreshold;

        public NativeArray<byte>.ReadOnly GetEdgeStatesReadOnly()
        {
            TryCompleteEvaluation();
            return _edgeStates.IsCreated ? _edgeStates.AsReadOnly() : default;
        }

        public NativeArray<float>.ReadOnly GetEdgeFlowsReadOnly()
        {
            TryCompleteEvaluation();
            return _edgeFlow.IsCreated ? _edgeFlow.AsReadOnly() : default;
        }

        public bool TryGetDirectedEdgeState(int sourceNodeIndex, int destinationNodeIndex, out LogisticsEdgeState state, out float flow)
        {
            state = LogisticsEdgeState.None;
            flow = 0f;
            if (!TryCompleteEvaluation() ||
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

            DisposeNativeArray(ref _nodeBuffer, disposeDependency);
            DisposeNativeArray(ref _edgeOffsets, disposeDependency);
            DisposeNativeArray(ref _edgeDestinations, disposeDependency);
            DisposeNativeArray(ref _edgeConductance, disposeDependency);
            DisposeNativeArray(ref _edgeCapacity, disposeDependency);
            DisposeNativeArray(ref _edgeFlow, disposeDependency);
            DisposeNativeArray(ref _edgeStates, disposeDependency);
            DisposeNativeArray(ref _edgeKeys, disposeDependency);
            DisposeNativeArray(ref _edgeWriteCursor, disposeDependency);
            DisposeNativeArray(ref _potentialFront, disposeDependency);
            DisposeNativeArray(ref _potentialBack, disposeDependency);
            DisposeNativeArray(ref _nodeConductanceSum, disposeDependency);
            DisposeNativeArray(ref _nodeConductanceInverseSum, disposeDependency);
            DisposeNativeArray(ref _runtimeConductiveEdgeCount, disposeDependency);
            DisposeNativeList(ref _topologyEdgeList, disposeDependency, nameof(_topologyEdgeList));
            DisposeNativeList(ref _producers, disposeDependency, nameof(_producers));
            DisposeNativeList(ref _consumers, disposeDependency, nameof(_consumers));
            DisposeNativeParallelHashMap(ref _producerMap, disposeDependency, nameof(_producerMap));
            DisposeNativeParallelHashMap(ref _consumerMap, disposeDependency, nameof(_consumerMap));
            DisposeNativeArray(ref _parents, disposeDependency);
            DisposeNativeArray(ref _ranks, disposeDependency);
            DisposeNativeArray(ref _componentIds, disposeDependency);
            DisposeNativeArray(ref _componentSizes, disposeDependency);
            DisposeNativeArray(ref _rootToComponent, disposeDependency);
            DisposeNativeArray(ref _traversalQueue, disposeDependency);
            DisposeNativeArray(ref _visited, disposeDependency);
            DisposeNativeArray(ref _consumerStates, disposeDependency);
            DisposeNativeArray(ref _nodeNetInjection, disposeDependency);
            DisposeNativeArray(ref _nodeServedDemand, disposeDependency);
            DisposeNativeArray(ref _nodeVoltageSupplyRatio, disposeDependency);
            DisposeNativeArray(ref _nodeSourcePotential, disposeDependency);
            DisposeNativeArray(ref _componentGeneration, disposeDependency);
            DisposeNativeArray(ref _componentDemand, disposeDependency);
            DisposeNativeArray(ref _componentServedDemand, disposeDependency);
            DisposeNativeArray(ref _componentRemainingSupply, disposeDependency);
            DisposeNativeArray(ref _componentSupplyRatio, disposeDependency);
            DisposeNativeArray(ref _componentResidualInjection, disposeDependency);
            DisposeNativeArray(ref _componentAnchorNode, disposeDependency);
            DisposeNativeArray(ref _componentBrownoutTier, disposeDependency);
            DisposeNativeArray(ref _scheduledTopologySummary, disposeDependency);
            DisposeNativeArray(ref _scheduledDistributionSummary, disposeDependency);
            DisposeNativeArray(ref _publishedNodeStates, disposeDependency);
            DisposeNativeArray(ref _publishedNodeStatesBack, disposeDependency);
            DisposeNativeParallelHashMap(ref _publishedNodeStateMap, disposeDependency, nameof(_publishedNodeStateMap));
            DisposeNativeParallelHashMap(ref _publishedNodeStateBackMap, disposeDependency, nameof(_publishedNodeStateBackMap));

            if (_bfsQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(NativeMemoryOwner, _bfsQueueSentinelLabel);
                _bfsQueue.Dispose(disposeDependency);
                _bfsQueue = default;
            }

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
            _scheduledAdaptiveSolveSlice = false;
            _buildOpen = false;

            return JobHandle.CombineDependencies(evaluationHandle, publishHandle);
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle dependency, string label)
            where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            list.Dispose(dependency);
            list = default;
        }

        private static void DisposeNativeParallelHashMap<TKey, TValue>(
            ref NativeParallelHashMap<TKey, TValue> map,
            JobHandle dependency,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, label);
            map.Dispose(dependency);
            map = default;
        }

        private static void RegisterNativeArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void RegisterNativeList<T>(NativeList<T> list, string label)
            where T : unmanaged
        {
            NativeMemorySentinel.RegisterNativeList(list, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void RegisterNativeParallelHashMap<TKey, TValue>(NativeParallelHashMap<TKey, TValue> map, string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            NativeMemorySentinel.RegisterNativeParallelHashMap(map, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void EnsureNativeListCapacity<T>(ref NativeList<T> list, int requiredLength, string label)
            where T : unmanaged
        {
            int safeLength = math.max(1, requiredLength);
            if (!list.IsCreated)
            {
                list = new NativeList<T>(safeLength, Allocator.Persistent);
                RegisterNativeList(list, label);
                return;
            }

            if (list.Capacity >= safeLength)
                return;

            NativeMemorySentinel.UnregisterNativeList(NativeMemoryOwner, label);
            list.Capacity = safeLength;
            RegisterNativeList(list, label);
        }

        private static void DisposeNativeArrayImmediate<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void DisposeNativeParallelHashMapImmediate<TKey, TValue>(
            ref NativeParallelHashMap<TKey, TValue> map,
            string label)
            where TKey : unmanaged, IEquatable<TKey>
            where TValue : unmanaged
        {
            if (!map.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelHashMap(NativeMemoryOwner, label);
            map.Dispose();
            map = default;
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
            _networkType = networkType;
            _nodeCount = 0;
            _edgeCount = 0;
            _conductiveEdgeCount = 0;
            if (_runtimeConductiveEdgeCount.IsCreated)
                _runtimeConductiveEdgeCount[0] = 0;

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
            if (!_buildOpen)
                return -1;

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
            if (!_buildOpen)
                return;

            if (_topologyEdgeList.Length >= _topologyEdgeList.Capacity)
                EnsureNativeListCapacity(ref _topologyEdgeList, math.max(1, _topologyEdgeList.Capacity * 2), nameof(_topologyEdgeList));

            _topologyEdgeList.Add(new TopologyEdgeRecord
            {
                SourceNodeIndex = sourceNodeIndex,
                DestinationNodeIndex = destinationNodeIndex,
                Resistance = SanitizeEdgeResistance(resistance)
            });
        }

        public void AddProducer(int nodeIndex, float productionRate)
        {
            if (!_buildOpen)
                return;

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
            if (!_buildOpen)
                return;

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
            if (!_buildOpen)
                return;

            int nodeCount = _nodeCount;
            for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                _edgeOffsets[nodeIndex] = 0;

            int topologyEdgeCount = _topologyEdgeList.Length;
            _edgeCount = 0;
            _conductiveEdgeCount = 0;

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
            {
                _edgeWriteCursor[nodeIndex] = _edgeOffsets[nodeIndex];
                _nodeConductanceSum[nodeIndex] = 0f;
                _nodeConductanceInverseSum[nodeIndex] = 0f;
            }

            for (int edgeIndex = 0; edgeIndex < topologyEdgeCount; edgeIndex++)
            {
                TopologyEdgeRecord edge = _topologyEdgeList[edgeIndex];
                if (!IsValidNodeIndex(edge.SourceNodeIndex) || !IsValidNodeIndex(edge.DestinationNodeIndex))
                    continue;

                int writeIndex = _edgeWriteCursor[edge.SourceNodeIndex];
                _edgeWriteCursor[edge.SourceNodeIndex] = writeIndex + 1;
                _edgeDestinations[writeIndex] = edge.DestinationNodeIndex;
                float conductance = ResolveEdgeConductanceForBuild(
                    edge.SourceNodeIndex,
                    edge.DestinationNodeIndex,
                    edge.Resistance);
                _edgeConductance[writeIndex] = conductance;
                _edgeCapacity[writeIndex] = ResolveEdgeCapacityForBuild(edge.SourceNodeIndex, edge.DestinationNodeIndex);
                int2 edgeKey = new int2(edge.SourceNodeIndex, edge.DestinationNodeIndex);
                if (_edgeKeys.IsCreated &&
                    (writeIndex >= _edgeKeys.Length ||
                     _edgeKeys[writeIndex].x != edgeKey.x ||
                     _edgeKeys[writeIndex].y != edgeKey.y))
                {
                    if (_edgeStates.IsCreated && writeIndex < _edgeStates.Length)
                        _edgeStates[writeIndex] = 0;
                    if (_edgeFlow.IsCreated && writeIndex < _edgeFlow.Length)
                        _edgeFlow[writeIndex] = 0f;
                }

                if (_edgeKeys.IsCreated && writeIndex < _edgeKeys.Length)
                    _edgeKeys[writeIndex] = edgeKey;

                byte edgeState = _edgeStates.IsCreated && writeIndex < _edgeStates.Length
                    ? _edgeStates[writeIndex]
                    : (byte)0;
                if (conductance > Epsilon &&
                    (edgeState & (byte)LogisticsEdgeState.Ruptured) == 0)
                {
                    _conductiveEdgeCount++;
                    _nodeConductanceSum[edge.SourceNodeIndex] += conductance;
                }
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                float conductanceSum = _nodeConductanceSum[nodeIndex];
                _nodeConductanceInverseSum[nodeIndex] = conductanceSum > Epsilon
                    ? math.rcp(conductanceSum)
                    : 0f;
            }

            if (_runtimeConductiveEdgeCount.IsCreated)
                _runtimeConductiveEdgeCount[0] = _conductiveEdgeCount;

            _adaptiveSolveCursor = 0;
            _adaptiveSolveRemainingNodes = 0;
            _scheduledSolveNodeCount = 0;
            _scheduledAdaptiveSolveSlice = false;
            _buildOpen = false;
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
            _committedTopologySummary = summary;
            return summary;
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
            summary.UnservedDemand = math.max(0f, summary.TotalConsumption - summary.ServedDemand);
            summary.HasDeficit = summary.UnservedDemand > Epsilon;
            summary.BrownoutTier = summary.HasDeficit
                ? LogisticsBrownoutTier.EmergencyOnly
                : LogisticsBrownoutTier.None;
            ApplyBinaryNodeLoads();
            _committedDistributionSummary = summary;
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

        public bool TryGetConsumerVoltageSupplyRatio(int consumerIndex, out float voltageSupplyRatio)
        {
            voltageSupplyRatio = 1f;
            if (!_nodeVoltageSupplyRatio.IsCreated ||
                !_consumers.IsCreated ||
                consumerIndex < 0 ||
                consumerIndex >= _consumers.Length)
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
            _publishedNodeStateBackMap.Clear();

            JobHandle publishDependency = _evaluateGraphPending
                ? JobHandle.CombineDependencies(dependency, _evaluateGraphJobHandle)
                : dependency;

            PublishNodeStatesJob job = new PublishNodeStatesJob
            {
                Nodes = _nodeBuffer,
                NodeNetInjection = _nodeNetInjection,
                NodeServedDemand = _nodeServedDemand,
                NodeVoltageSupplyRatio = _nodeVoltageSupplyRatio,
                PublishedNodeStates = _publishedNodeStatesBack,
                PublishedNodeStateMap = _publishedNodeStateBackMap.AsParallelWriter()
            };

            _publishNodeStatesJobHandle = job.Schedule(_nodeCount, ParallelNodeBatchSize, publishDependency);
            _publishNodeStatesPending = true;
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
                CommitNoEdgeEvaluation();
                return default;
            }

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            EnsureSummaryBuffers();

            return ScheduleEvaluationSlice(relaxationSliceOnly: false);
        }

        private JobHandle ScheduleEvaluationSlice(bool relaxationSliceOnly)
        {
            ResolveAdaptiveSolveWindow(out int solveStartNode, out int solveNodeCount);

            EvaluateGraphJob job = new EvaluateGraphJob
            {
                NetworkType = _networkType,
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount,
                ConsumerCount = ConsumerCount,
                SolveStartNode = solveStartNode,
                SolveNodeCount = solveNodeCount,
                RelaxationSliceOnly = relaxationSliceOnly ? (byte)1 : (byte)0,
                Nodes = _nodeBuffer,
                EdgeOffsets = _edgeOffsets,
                EdgeDestinations = _edgeDestinations,
                EdgeConductance = _edgeConductance,
                EdgeCapacity = _edgeCapacity,
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
                NodeServedDemand = _nodeServedDemand,
                NodeVoltageSupplyRatio = _nodeVoltageSupplyRatio,
                NodeSourcePotential = _nodeSourcePotential,
                NodeConductanceSum = _nodeConductanceSum,
                NodeConductanceInverseSum = _nodeConductanceInverseSum,
                PotentialFront = _potentialFront,
                PotentialBack = _potentialBack,
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

        private void ResolveAdaptiveSolveWindow(out int solveStartNode, out int solveNodeCount)
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

            int contiguousNodeCount = _nodeCount - _adaptiveSolveCursor;
            solveStartNode = _adaptiveSolveCursor;
            solveNodeCount = math.min(
                AdaptiveSolveNodesPerFrame,
                math.min(contiguousNodeCount, _adaptiveSolveRemainingNodes));
            if (solveNodeCount <= 0)
            {
                solveStartNode = 0;
                solveNodeCount = math.min(AdaptiveSolveNodesPerFrame, _nodeCount);
            }

            _scheduledAdaptiveSolveSlice = true;
            _scheduledSolveNodeCount = solveNodeCount;
        }

        private void CommitNoEdgeEvaluation()
        {
            EnsureSummaryBuffers();
            if (_runtimeConductiveEdgeCount.IsCreated)
                _runtimeConductiveEdgeCount[0] = 0;
            _adaptiveSolveCursor = 0;
            _adaptiveSolveRemainingNodes = 0;
            _scheduledSolveNodeCount = 0;
            _scheduledAdaptiveSolveSlice = false;
            ResetNoEdgeRuntimeState();

            float totalGeneration = ComputeTotalGeneration();
            float totalConsumption = ComputeTotalConsumption();
            _scheduledTopologySummary[0] = new TopologySummary
            {
                NodeCount = _nodeCount,
                EdgeCount = _edgeCount,
                IslandCount = _nodeCount
            };
            _scheduledDistributionSummary[0] = new DistributionSummary
            {
                TotalGeneration = totalGeneration,
                TotalConsumption = totalConsumption,
                Balance = totalGeneration - totalConsumption,
                SupplyRatio = totalConsumption > Epsilon ? 0f : 1f,
                UnservedDemand = totalConsumption,
                DisabledCount = ConsumerCount,
                HasDeficit = totalConsumption > Epsilon,
                BrownoutTier = totalConsumption > Epsilon
                    ? LogisticsBrownoutTier.EmergencyOnly
                    : LogisticsBrownoutTier.None
            };
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
                if (_producerMap.TryGetValue(nodeIndex, out _))
                    node.Flags &= ~LogisticsNodeFlags.Isolated;
                else
                    node.Flags |= LogisticsNodeFlags.Isolated;
                _nodeBuffer[nodeIndex] = node;

                if (_nodeNetInjection.IsCreated && (uint)nodeIndex < (uint)_nodeNetInjection.Length)
                    _nodeNetInjection[nodeIndex] = 0f;
                if (_nodeServedDemand.IsCreated && (uint)nodeIndex < (uint)_nodeServedDemand.Length)
                    _nodeServedDemand[nodeIndex] = 0f;
                if (_nodeVoltageSupplyRatio.IsCreated && (uint)nodeIndex < (uint)_nodeVoltageSupplyRatio.Length)
                    _nodeVoltageSupplyRatio[nodeIndex] = 0f;
                if (_nodeSourcePotential.IsCreated && (uint)nodeIndex < (uint)_nodeSourcePotential.Length)
                    _nodeSourcePotential[nodeIndex] = 0f;
            }

            int consumerCount = _consumers.IsCreated ? _consumers.Length : 0;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                int nodeIndex = _consumers[consumerIndex].NodeIndex;
                if (!IsValidNodeIndex(nodeIndex))
                    continue;

                LogisticsNode node = _nodeBuffer[nodeIndex];
                node.Flags |= LogisticsNodeFlags.Brownout;
                _nodeBuffer[nodeIndex] = node;
            }

            int safeEdgeCount = math.min(_edgeCount, _edgeFlow.IsCreated ? _edgeFlow.Length : 0);
            for (int edgeIndex = 0; edgeIndex < safeEdgeCount; edgeIndex++)
                _edgeFlow[edgeIndex] = 0f;
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

            if (!DispatcherJobSwap.TryComplete(ref _evaluateGraphJobHandle, forceComplete: false))
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
        }

        public TopologySummary GetScheduledTopologySummary()
        {
            TryCompleteEvaluation();
            return _committedTopologySummary;
        }

        public DistributionSummary GetScheduledDistributionSummary()
        {
            TryCompleteEvaluation();
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

            if (!DispatcherJobSwap.TryComplete(ref _publishNodeStatesJobHandle, forceComplete: false))
                return false;

            _publishNodeStatesJobHandle = default;
            _publishNodeStatesPending = false;
            SwapPublishedNodeStateBuffers();
            return true;
        }

        private void SwapPublishedNodeStateBuffers()
        {
            NativeArray<ushort> statesSwap = _publishedNodeStates;
            _publishedNodeStates = _publishedNodeStatesBack;
            _publishedNodeStatesBack = statesSwap;

            NativeParallelHashMap<uint, ushort> mapSwap = _publishedNodeStateMap;
            _publishedNodeStateMap = _publishedNodeStateBackMap;
            _publishedNodeStateBackMap = mapSwap;
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

            if (_publishedNodeStateMap.IsCreated)
                _publishedNodeStateMap.Clear();
            if (_publishedNodeStateBackMap.IsCreated)
                _publishedNodeStateBackMap.Clear();
        }

        public bool TryGetPublishedNodeState(uint nodeId, out ushort stateBits)
        {
            TryCompleteNodeStatePublish();

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
                _nodeServedDemand[nodeIndex] = 0f;
                _nodeVoltageSupplyRatio[nodeIndex] = 1f;
                _nodeSourcePotential[nodeIndex] = 0f;
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
                {
                    _componentGeneration[componentIndex] += productionRate;
                    _nodeSourcePotential[nodeIndex] = productionRate;
                }
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
                    ? math.saturate(generation * math.rcp(demand))
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

                _componentRemainingSupply[componentIndex] = math.max(0f, _componentRemainingSupply[componentIndex] - consumer.Demand);
                _componentServedDemand[componentIndex] += consumer.Demand;
                _nodeServedDemand[nodeIndex] += consumer.Demand;
                _nodeVoltageSupplyRatio[nodeIndex] = 1f;
                _consumerStates[consumerIndex] = 1;
                poweredCount++;
                servedDemand += consumer.Demand;
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

                float dispatchedGeneration = productionRate * math.saturate(componentServedDemand * math.rcp(componentGeneration));
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
                _nodeVoltageSupplyRatio[nodeIndex] = componentPowered ? 1f : 0f;

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

            int searchBudget = math.min(_nodeCount, MaxSearchDepth);
            if (searchBudget <= 0)
                return 0;

            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _visited[nodeIndex] = 0;

            int head = 0;
            int tail = 0;
            _visited[startNodeIndex] = 1;
            _traversalQueue[tail++] = startNodeIndex;

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

                    _visited[destinationNodeIndex] = 1;
                    _traversalQueue[tail++] = destinationNodeIndex;
                }
            }

            return visitedCount;
        }

        private int TraverseProducerReachability()
        {
            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                _visited[nodeIndex] = 0;

            int searchBudget = math.min(_nodeCount, MaxSearchDepth);
            if (searchBudget <= 0)
                return 0;

            int head = 0;
            int tail = 0;

            int producerCount = _producers.Length;
            for (int producerIndex = 0; producerIndex < producerCount; producerIndex++)
            {
                int nodeIndex = _producers[producerIndex].NodeIndex;
                if (!IsValidNodeIndex(nodeIndex) || _visited[nodeIndex] != 0)
                    continue;

                if (tail >= searchBudget)
                    break;

                _visited[nodeIndex] = 1;
                _traversalQueue[tail++] = nodeIndex;
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

                    _visited[destinationNodeIndex] = 1;
                    _traversalQueue[tail++] = destinationNodeIndex;
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
            EnsureNodeArrayCapacity(ref _nodeBuffer, safeLength, nameof(_nodeBuffer));
            EnsureIntArrayCapacity(ref _edgeOffsets, safeLength + 1, nameof(_edgeOffsets));
            EnsureIntArrayCapacity(ref _edgeWriteCursor, safeLength, nameof(_edgeWriteCursor));
            EnsureFloatArrayCapacity(ref _potentialFront, safeLength, nameof(_potentialFront));
            EnsureFloatArrayCapacity(ref _potentialBack, safeLength, nameof(_potentialBack));
            EnsureFloatArrayCapacity(ref _nodeConductanceSum, safeLength, nameof(_nodeConductanceSum));
            EnsureFloatArrayCapacity(ref _nodeConductanceInverseSum, safeLength, nameof(_nodeConductanceInverseSum));
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureIntArrayCapacity(ref _edgeDestinations, safeLength, nameof(_edgeDestinations));
            EnsureFloatArrayCapacity(ref _edgeConductance, safeLength, nameof(_edgeConductance));
            EnsureFloatArrayCapacity(ref _edgeCapacity, safeLength, nameof(_edgeCapacity));
            EnsureFloatArrayCapacity(ref _edgeFlow, safeLength, nameof(_edgeFlow));
            EnsureByteArrayCapacity(ref _edgeStates, safeLength, nameof(_edgeStates));
            EnsureInt2ArrayCapacity(ref _edgeKeys, safeLength, nameof(_edgeKeys));
        }

        private void EnsureTopologyCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_topologyEdgeList.Capacity < safeLength)
                EnsureNativeListCapacity(ref _topologyEdgeList, safeLength, nameof(_topologyEdgeList));
        }

        private void EnsureProducerCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_producers.Capacity < safeLength)
                EnsureNativeListCapacity(ref _producers, safeLength, nameof(_producers));

            EnsureFloatMapCapacity(ref _producerMap, safeLength, nameof(_producerMap));
            EnsureFloatMapCapacity(ref _consumerMap, safeLength, nameof(_consumerMap));
        }

        private void EnsureConsumerCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            if (_consumers.Capacity < safeLength)
                EnsureNativeListCapacity(ref _consumers, safeLength, nameof(_consumers));
        }

        private void EnsureWorkingCapacity(int nodeCount, int consumerCount)
        {
            EnsureIntArrayCapacity(ref _parents, nodeCount, nameof(_parents));
            EnsureIntArrayCapacity(ref _ranks, nodeCount, nameof(_ranks));
            EnsureIntArrayCapacity(ref _componentIds, nodeCount, nameof(_componentIds));
            EnsureIntArrayCapacity(ref _componentSizes, nodeCount, nameof(_componentSizes));
            EnsureIntArrayCapacity(ref _rootToComponent, nodeCount, nameof(_rootToComponent));
            EnsureIntArrayCapacity(ref _traversalQueue, nodeCount, nameof(_traversalQueue));
            EnsureByteArrayCapacity(ref _visited, nodeCount, nameof(_visited));
            EnsureByteArrayCapacity(ref _consumerStates, consumerCount, nameof(_consumerStates));
            EnsureFloatArrayCapacity(ref _nodeNetInjection, nodeCount, nameof(_nodeNetInjection));
            EnsureFloatArrayCapacity(ref _nodeServedDemand, nodeCount, nameof(_nodeServedDemand));
            EnsureFloatArrayCapacity(ref _nodeVoltageSupplyRatio, nodeCount, nameof(_nodeVoltageSupplyRatio));
            EnsureFloatArrayCapacity(ref _nodeSourcePotential, nodeCount, nameof(_nodeSourcePotential));
            EnsureFloatArrayCapacity(ref _componentGeneration, nodeCount, nameof(_componentGeneration));
            EnsureFloatArrayCapacity(ref _componentDemand, nodeCount, nameof(_componentDemand));
            EnsureFloatArrayCapacity(ref _componentServedDemand, nodeCount, nameof(_componentServedDemand));
            EnsureFloatArrayCapacity(ref _componentRemainingSupply, nodeCount, nameof(_componentRemainingSupply));
            EnsureFloatArrayCapacity(ref _componentSupplyRatio, nodeCount, nameof(_componentSupplyRatio));
            EnsureFloatArrayCapacity(ref _componentResidualInjection, nodeCount, nameof(_componentResidualInjection));
            EnsureIntArrayCapacity(ref _componentAnchorNode, nodeCount, nameof(_componentAnchorNode));
            EnsureByteArrayCapacity(ref _componentBrownoutTier, nodeCount, nameof(_componentBrownoutTier));
            EnsurePublishedStateCapacity(nodeCount);
        }

        private void EnsureSummaryBuffers()
        {
            EnsureTopologySummaryCapacity(ref _scheduledTopologySummary, nameof(_scheduledTopologySummary));
            EnsureDistributionSummaryCapacity(ref _scheduledDistributionSummary, nameof(_scheduledDistributionSummary));
        }

        private static void EnsureNodeArrayCapacity(ref NativeArray<LogisticsNode> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<LogisticsNode>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureIntArrayCapacity(ref NativeArray<int> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureInt2ArrayCapacity(ref NativeArray<int2> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<int2>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureFloatArrayCapacity(ref NativeArray<float> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<float>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureByteArrayCapacity(ref NativeArray<byte> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<byte>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private void EnsurePublishedStateCapacity(int requiredLength)
        {
            int safeLength = math.max(1, requiredLength);
            EnsureUShortArrayCapacity(ref _publishedNodeStates, safeLength, nameof(_publishedNodeStates));
            EnsureUShortArrayCapacity(ref _publishedNodeStatesBack, safeLength, nameof(_publishedNodeStatesBack));
            EnsureUShortMapCapacity(ref _publishedNodeStateMap, safeLength, nameof(_publishedNodeStateMap));
            EnsureUShortMapCapacity(ref _publishedNodeStateBackMap, safeLength, nameof(_publishedNodeStateBackMap));
        }

        private static void EnsureUShortArrayCapacity(ref NativeArray<ushort> array, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<ushort>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureFloatMapCapacity(ref NativeParallelHashMap<int, float> map, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (map.IsCreated && map.Capacity >= safeLength)
                return;

            DisposeNativeParallelHashMapImmediate(ref map, label);

            map = new NativeParallelHashMap<int, float>(safeLength, Allocator.Persistent);
            RegisterNativeParallelHashMap(map, label);
        }

        private static void EnsureUShortMapCapacity(ref NativeParallelHashMap<uint, ushort> map, int requiredLength, string label)
        {
            int safeLength = math.max(1, requiredLength);
            if (map.IsCreated && map.Capacity >= safeLength)
                return;

            DisposeNativeParallelHashMapImmediate(ref map, label);

            map = new NativeParallelHashMap<uint, ushort>(safeLength, Allocator.Persistent);
            RegisterNativeParallelHashMap(map, label);
        }

        private static void EnsureTopologySummaryCapacity(ref NativeArray<TopologySummary> array, string label)
        {
            if (array.IsCreated && array.Length >= 1)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<TopologySummary>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }

        private static void EnsureDistributionSummaryCapacity(ref NativeArray<DistributionSummary> array, string label)
        {
            if (array.IsCreated && array.Length >= 1)
                return;

            DisposeNativeArrayImmediate(ref array);

            array = new NativeArray<DistributionSummary>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            RegisterNativeArray(array, label);
        }
    }
}
