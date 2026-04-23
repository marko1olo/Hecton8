using System;
using Unity.Collections;
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

        private const int MinPriority = 0;
        private const int MaxPriority = 100;
        private const float MinResistance = 0.0001f;
        private const float Epsilon = 0.001f;
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
        private NativeArray<byte> _visited;
        private NativeArray<byte> _consumerStates;
        private NativeQueue<int> _bfsQueue;

        public LogisticsNetworkGraph(int nodeCapacity = 16, int edgeCapacity = 32, int consumerCapacity = 16)
        {
            int safeNodeCapacity = Mathf.Max(1, nodeCapacity);
            int safeEdgeCapacity = Mathf.Max(1, edgeCapacity);
            int safeConsumerCapacity = Mathf.Max(1, consumerCapacity);

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
            // COLD ALLOC: NativeQueue<int>[nodeCapacity] — iterative BFS frontier — owner: LogisticsNetworkGraph
            _bfsQueue = new NativeQueue<int>(Allocator.Persistent);
        }

        public int NodeCount => _nodeCount;
        public int ConsumerCount => _consumers.IsCreated ? _consumers.Length : 0;

        public void Dispose()
        {
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

            if (_visited.IsCreated)
                _visited.Dispose();

            if (_consumerStates.IsCreated)
                _consumerStates.Dispose();

            if (_bfsQueue.IsCreated)
                _bfsQueue.Dispose();
        }

        public void BeginBuild(LogisticsNetworkType networkType, int nodeCapacity, int edgeCapacity, int consumerCapacity)
        {
            int safeNodeCapacity = Mathf.Max(1, nodeCapacity);
            int safeEdgeCapacity = Mathf.Max(1, edgeCapacity);
            int safeConsumerCapacity = Mathf.Max(1, consumerCapacity);

            _networkType = networkType;
            _nodeCount = 0;
            _edgeCount = 0;

            EnsureNodeCapacity(safeNodeCapacity);
            EnsureEdgeCapacity(safeEdgeCapacity);
            EnsureTopologyCapacity(safeEdgeCapacity);
            EnsureProducerCapacity(safeNodeCapacity);
            EnsureConsumerCapacity(safeConsumerCapacity);
            EnsureWorkingCapacity(safeNodeCapacity, safeConsumerCapacity);

            _topologyEdgeList.Clear();
            _producers.Clear();
            _consumers.Clear();
            _producerMap.Clear();
            _consumerMap.Clear();
            _bfsQueue.Clear();
        }

        public int AddNode(uint nodeId, float capacity, float resistance, byte priorityTier, LogisticsNodeFlags flags)
        {
            if (_nodeCount >= _nodeBuffer.Length)
                EnsureNodeCapacity(_nodeCount + 1);

            _nodeBuffer[_nodeCount] = new LogisticsNode
            {
                Id = nodeId,
                Capacity = Mathf.Max(Epsilon, capacity),
                Resistance = SanitizeNodeResistance(resistance),
                CurrentLoad = 0f,
                Potential = 0f,
                Priority = priorityTier,
                Flags = flags | LogisticsNodeFlags.Active,
                NetworkId = 0,
                Reserved = 0
            };

            return _nodeCount++;
        }

        public void AddEdge(int sourceNodeIndex, int destinationNodeIndex, float resistance)
        {
            if (_topologyEdgeList.Length >= _topologyEdgeList.Capacity)
                _topologyEdgeList.Capacity = Mathf.Max(1, _topologyEdgeList.Capacity * 2);

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
                PowerPriority = Mathf.Clamp(powerPriority, MinPriority, MaxPriority),
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
                node.NetworkId = (byte)Mathf.Clamp(componentIndex, 0, byte.MaxValue);
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
            DistributionSummary summary = new DistributionSummary
            {
                TotalGeneration = ComputeTotalGeneration(),
                TotalConsumption = ComputeTotalConsumption()
            };

            summary.Balance = summary.TotalGeneration - summary.TotalConsumption;
            summary.SupplyRatio = summary.TotalConsumption > Epsilon
                ? Mathf.Clamp01(summary.TotalGeneration / summary.TotalConsumption)
                : 1f;
            summary.HasDeficit = summary.TotalGeneration + Epsilon < summary.TotalConsumption;
            summary.BrownoutTier = ResolveBrownoutTier(summary.SupplyRatio);

            if (_nodeCount <= 0)
                return summary;

            EnsureWorkingCapacity(_nodeCount, ConsumerCount);
            ResetConsumerStates(ConsumerCount);
            ResetDistributionState();
            SeedProducerPotentials();
            SeedConsumerPotentials(summary.SupplyRatio);
            TraverseProducerReachability(true);

            int consumerCount = ConsumerCount;
            if (consumerCount <= 0)
                return summary;

            float remainingSupply = summary.TotalGeneration;
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

                        if (ShouldMarkBrownout(in consumer, summary.BrownoutTier))
                            MarkBrownoutNode(consumer.NodeIndex);

                        if (!CanServeConsumer(in consumer, summary.BrownoutTier))
                        {
                            continue;
                        }

                        if (remainingSupply + Epsilon < consumer.Demand)
                        {
                            MarkBrownoutNode(consumer.NodeIndex);
                            continue;
                        }

                        remainingSupply -= consumer.Demand;
                        servedDemand += consumer.Demand;
                        _consumerStates[consumerIndex] = 1;
                        poweredCount++;
                    }
                }
            }

            summary.PoweredCount = poweredCount;
            summary.DisabledCount = consumerCount - poweredCount;
            summary.ServedDemand = servedDemand;
            summary.UnservedDemand = Mathf.Max(0f, summary.TotalConsumption - servedDemand);
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
                node.Potential = Mathf.Max(node.Potential, productionRate);
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
            while (_bfsQueue.Count > 0)
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
            while (_bfsQueue.Count > 0)
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

            return visitedCount;
        }

        private void AccumulateDirectedFlow(int sourceNodeIndex, int destinationNodeIndex, int edgeIndex)
        {
            LogisticsNode sourceNode = _nodeBuffer[sourceNodeIndex];
            LogisticsNode destinationNode = _nodeBuffer[destinationNodeIndex];
            float combinedResistance = Mathf.Max(
                MinResistance,
                _edgeResistance[edgeIndex] + sourceNode.Resistance + destinationNode.Resistance);

            if ((sourceNode.Flags & LogisticsNodeFlags.Ruptured) != 0 ||
                (destinationNode.Flags & LogisticsNodeFlags.Ruptured) != 0)
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
            while (parentIndex != _parents[parentIndex])
            {
                _parents[parentIndex] = _parents[_parents[parentIndex]];
                parentIndex = _parents[parentIndex];
            }

            int currentIndex = nodeIndex;
            while (currentIndex != parentIndex)
            {
                int nextIndex = _parents[currentIndex];
                _parents[currentIndex] = parentIndex;
                currentIndex = nextIndex;
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

        private float SanitizeNodeResistance(float resistance)
        {
            float sanitizedResistance = Mathf.Max(MinResistance, resistance);
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
            float sanitizedResistance = Mathf.Max(MinResistance, resistance);
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
            int safeLength = Mathf.Max(1, requiredLength);
            EnsureNodeArrayCapacity(ref _nodeBuffer, safeLength);
            EnsureIntArrayCapacity(ref _edgeOffsets, safeLength + 1);
            EnsureIntArrayCapacity(ref _edgeWriteCursor, safeLength);
        }

        private void EnsureEdgeCapacity(int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            EnsureIntArrayCapacity(ref _edgeDestinations, safeLength);
            EnsureFloatArrayCapacity(ref _edgeResistance, safeLength);
        }

        private void EnsureTopologyCapacity(int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (_topologyEdgeList.Capacity < safeLength)
                _topologyEdgeList.Capacity = safeLength;
        }

        private void EnsureProducerCapacity(int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (_producers.Capacity < safeLength)
                _producers.Capacity = safeLength;

            EnsureFloatMapCapacity(ref _producerMap, safeLength);
            EnsureFloatMapCapacity(ref _consumerMap, safeLength);
        }

        private void EnsureConsumerCapacity(int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
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
            EnsureByteArrayCapacity(ref _visited, nodeCount);
            EnsureByteArrayCapacity(ref _consumerStates, consumerCount);
        }

        private static void EnsureNodeArrayCapacity(ref NativeArray<LogisticsNode> array, int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<LogisticsNode>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureIntArrayCapacity(ref NativeArray<int> array, int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<int>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureFloatArrayCapacity(ref NativeArray<float> array, int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<float>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureByteArrayCapacity(ref NativeArray<byte> array, int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (array.IsCreated && array.Length >= safeLength)
                return;

            if (array.IsCreated)
                array.Dispose();

            array = new NativeArray<byte>(safeLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private static void EnsureFloatMapCapacity(ref NativeParallelHashMap<int, float> map, int requiredLength)
        {
            int safeLength = Mathf.Max(1, requiredLength);
            if (map.IsCreated && map.Capacity >= safeLength)
                return;

            if (map.IsCreated)
                map.Dispose();

            map = new NativeParallelHashMap<int, float>(safeLength, Allocator.Persistent);
        }
    }
}
