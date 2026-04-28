// ============================================================================
// HECTON-8 — PowerGrid.cs
// Energy grid owner. Uses a CSR-backed LogisticsNetworkGraph for alloc-free
// topology, DSU island detection, and priority brownout distribution.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Gameplay;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    /// <summary>
    /// One connected power grid.
    /// Owns node membership, native graph scratch, and consumer power state application.
    /// </summary>
    public sealed class PowerGrid
    {
        private const float MinEdgeResistance = 0.0001f;
        private const float RuptureDemandFactor = 0.015f;
        private const float ShortCircuitResistanceMultiplier = 100f;

        /// <summary>Unique runtime ID for diagnostics.</summary>
        public readonly int Id;

        private static int _nextId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _nextId = 0;
        }

        private readonly HashSet<PowerNode> _nodes;
        private readonly List<IPowerComponent> _consumerRefs;
        private readonly List<PowerNode> _topologyNodes;
        private readonly LogisticsNetworkGraph _logisticsGraph;

        private float _totalGeneration;
        private float _totalConsumption;
        private float _balance;
        private float _supplyRatio = 1f;
        private bool _hasPowerDeficit;
        private LogisticsBrownoutTier _brownoutTier;
        private int _islandCount = 1;
        private int _cycleCount;
        private int _graphBuildVersion;
        private bool _isDirty = true;
        private bool _hasEvaluatedAtLeastOnce;
        private bool _slowTickEvaluationPending;

        /// <summary>Number of registered nodes in this grid.</summary>
        public int NodeCount => _nodes.Count;

        /// <summary>Total positive generation across the current graph snapshot.</summary>
        public float TotalGeneration => _totalGeneration;

        /// <summary>Total requested consumption across the current graph snapshot.</summary>
        public float TotalConsumption => _totalConsumption;

        /// <summary>Generation minus requested consumption.</summary>
        public float Balance => _balance;

        /// <summary>Generation / requested consumption in the last distribution pass.</summary>
        public float SupplyRatio => _supplyRatio;

        /// <summary>True when requested consumption exceeds generation.</summary>
        public bool HasPowerDeficit => _hasPowerDeficit;

        /// <summary>True when a topology or demand change is waiting for the next SlowTick evaluation.</summary>
        public bool IsDirty => _isDirty;

        /// <summary>Brownout tier selected by the graph kernel.</summary>
        public LogisticsBrownoutTier BrownoutTier => _brownoutTier;

        /// <summary>Connected components detected in the most recent topology pass.</summary>
        public int IslandCount => _islandCount;

        /// <summary>Cycle count detected by DSU during the most recent topology pass.</summary>
        public int CycleCount => _cycleCount;

        /// <summary>Read-only access for manager-level membership changes.</summary>
        public HashSet<PowerNode> Nodes => _nodes;

        public PowerGrid(int initialCapacity = 16)
        {
            int safeCapacity = math.max(1, initialCapacity);

            Id = _nextId++;
            // COLD ALLOC: HashSet<PowerNode>[initialCapacity] — grid membership cache — owner: PowerGrid
            _nodes = new HashSet<PowerNode>(safeCapacity);
            // COLD ALLOC: List<IPowerComponent>[initialCapacity] — consumer reference cache — owner: PowerGrid
            _consumerRefs = new List<IPowerComponent>(safeCapacity);
            // COLD ALLOC: List<PowerNode>[initialCapacity] — topology node snapshot — owner: PowerGrid
            _topologyNodes = new List<PowerNode>(safeCapacity);
            _logisticsGraph = new LogisticsNetworkGraph(safeCapacity, safeCapacity * 4, safeCapacity * 2);
        }

        public void Dispose()
        {
            _logisticsGraph.CompleteEvaluation();
            _logisticsGraph.CompleteNodeStatePublish();
            _logisticsGraph.Dispose();
        }

        /// <summary>
        /// Consumes buffered generation for one-shot systems. Next UpdateBalance rebuild overrides this cache.
        /// </summary>
        public void ConsumePower(float amount)
        {
            if (amount <= 0f)
                return;

            _totalGeneration = math.max(0f, _totalGeneration - amount);
            _balance = _totalGeneration - _totalConsumption;
            _supplyRatio = _totalConsumption > 0.0001f ? math.saturate(_totalGeneration / _totalConsumption) : 1f;
            _hasPowerDeficit = _totalGeneration + 0.0001f < _totalConsumption;
            _brownoutTier = ResolveBrownoutTier(_supplyRatio);
            _isDirty = true;
        }

        /// <summary>Marks this grid for the next SlowTick evaluation.</summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>Adds a node to this grid and updates its owner reference.</summary>
        public void AddNode(PowerNode node)
        {
            if (node == null)
                return;

            if (!_nodes.Add(node) && ReferenceEquals(node.Grid, this))
                return;

            node.SetGrid(this);
            _isDirty = true;
        }

        /// <summary>Removes a node from this grid and clears the owner reference.</summary>
        public void RemoveNode(PowerNode node)
        {
            if (node == null)
                return;

            _nodes.Remove(node);
            if (ReferenceEquals(node.Grid, this))
                node.SetGrid(null);

            _isDirty = true;
        }

        /// <summary>Absorbs all nodes from another grid.</summary>
        public void AbsorbAll(PowerGrid other)
        {
            if (other == null || ReferenceEquals(other, this))
                return;

            HashSet<PowerNode>.Enumerator enumerator = other._nodes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                PowerNode node = enumerator.Current;
                if (node == null)
                    continue;

                _nodes.Add(node);
                node.SetGrid(this);
            }

            other._nodes.Clear();
            _isDirty = true;
        }

        /// <summary>
        /// Rebuilds the logistics graph snapshot and applies greedy distribution results to all consumers.
        /// </summary>
        public void UpdateBalance()
        {
            if (_slowTickEvaluationPending)
                EndSlowTickEvaluation();

            EvaluateBalanceViaScheduledJob(scheduleNodeStatePublish: false);
            ApplyConsumerStates();
            _isDirty = false;
            _hasEvaluatedAtLeastOnce = true;
        }

        /// <summary>Schedules a graph evaluation for the manager-owned SlowTick cadence.</summary>
        public void BeginSlowTickEvaluation()
        {
            if (_slowTickEvaluationPending || _logisticsGraph.HasPendingNodeStatePublish || _logisticsGraph.HasPendingEvaluation)
                return;

            if (!_isDirty && _hasEvaluatedAtLeastOnce)
                return;

            BuildGraphSnapshot();

            if (_topologyNodes.Count <= 0)
            {
                _totalGeneration = 0f;
                _totalConsumption = 0f;
                _balance = 0f;
                _supplyRatio = 1f;
                _hasPowerDeficit = false;
                _brownoutTier = LogisticsBrownoutTier.None;
                _islandCount = 0;
                _cycleCount = 0;
                _logisticsGraph.ClearPublishedNodeStates();
                _isDirty = false;
                _hasEvaluatedAtLeastOnce = true;
                return;
            }

            JobHandle evaluationHandle = _logisticsGraph.ScheduleEvaluation();
            _logisticsGraph.ScheduleNodeStatePublish(evaluationHandle);
            _slowTickEvaluationPending = true;
            _isDirty = false;
        }

        /// <summary>Completes the pending node-state publication pass and applies consumer power states.</summary>
        public void EndSlowTickEvaluation()
        {
            if (!_slowTickEvaluationPending && !_logisticsGraph.HasPendingNodeStatePublish)
                return;

            _logisticsGraph.CompleteEvaluation();
            _logisticsGraph.CompleteNodeStatePublish();
            LogisticsNetworkGraph.TopologySummary topology = _logisticsGraph.GetScheduledTopologySummary();
            LogisticsNetworkGraph.DistributionSummary distribution = _logisticsGraph.GetScheduledDistributionSummary();

            _totalGeneration = distribution.TotalGeneration;
            _totalConsumption = distribution.TotalConsumption;
            _balance = distribution.Balance;
            _supplyRatio = distribution.SupplyRatio;
            _hasPowerDeficit = distribution.HasDeficit;
            _brownoutTier = distribution.BrownoutTier;
            _islandCount = topology.IslandCount;
            _cycleCount = topology.CycleCount;

            ApplyConsumerStates();
            _slowTickEvaluationPending = false;
            _hasEvaluatedAtLeastOnce = true;
        }

        /// <summary>Rebuilds the topology-only snapshot and returns current connectivity analysis.</summary>
        internal LogisticsNetworkGraph.TopologySummary AnalyzeTopology()
        {
            BuildGraphSnapshot();
            _logisticsGraph.ScheduleEvaluation();
            _logisticsGraph.CompleteEvaluation();
            LogisticsNetworkGraph.TopologySummary topology = _logisticsGraph.GetScheduledTopologySummary();
            _islandCount = topology.IslandCount;
            _cycleCount = topology.CycleCount;
            return topology;
        }

        private void EvaluateBalanceViaScheduledJob(bool scheduleNodeStatePublish)
        {
            _slowTickEvaluationPending = false;
            BuildGraphSnapshot();

            if (_topologyNodes.Count <= 0)
            {
                _totalGeneration = 0f;
                _totalConsumption = 0f;
                _balance = 0f;
                _supplyRatio = 1f;
                _hasPowerDeficit = false;
                _brownoutTier = LogisticsBrownoutTier.None;
                _islandCount = 0;
                _cycleCount = 0;
                _logisticsGraph.ClearPublishedNodeStates();
                return;
            }

            JobHandle evaluationHandle = _logisticsGraph.ScheduleEvaluation();
            if (scheduleNodeStatePublish)
                _logisticsGraph.ScheduleNodeStatePublish(evaluationHandle);

            _logisticsGraph.CompleteEvaluation();
            LogisticsNetworkGraph.TopologySummary topology = _logisticsGraph.GetScheduledTopologySummary();
            LogisticsNetworkGraph.DistributionSummary distribution = _logisticsGraph.GetScheduledDistributionSummary();

            _totalGeneration = distribution.TotalGeneration;
            _totalConsumption = distribution.TotalConsumption;
            _balance = distribution.Balance;
            _supplyRatio = distribution.SupplyRatio;
            _hasPowerDeficit = distribution.HasDeficit;
            _brownoutTier = distribution.BrownoutTier;
            _islandCount = topology.IslandCount;
            _cycleCount = topology.CycleCount;

            if (scheduleNodeStatePublish)
                _logisticsGraph.CompleteNodeStatePublish();
            else
                _logisticsGraph.PublishNodeStateSynchronously();
        }

        internal List<PowerNode> TopologyNodes => _topologyNodes;

        internal int GetNodeComponentId(int nodeIndex)
        {
            return _logisticsGraph.GetNodeComponentId(nodeIndex);
        }

        internal int GetComponentSize(int componentIndex)
        {
            return _logisticsGraph.GetComponentSize(componentIndex);
        }

        private void BuildGraphSnapshot()
        {
            _consumerRefs.Clear();
            _topologyNodes.Clear();

            int rawNodeCount = _nodes.Count;
            if (rawNodeCount <= 0)
            {
                _logisticsGraph.BeginBuild(LogisticsNetworkType.PowerDc, 1, 1, 1);
                _logisticsGraph.FinalizeBuild();
                return;
            }

            _graphBuildVersion++;
            if (_graphBuildVersion == int.MaxValue)
                _graphBuildVersion = 1;

            HashSet<PowerNode>.Enumerator nodeEnumerator = _nodes.GetEnumerator();
            while (nodeEnumerator.MoveNext())
            {
                PowerNode node = nodeEnumerator.Current;
                if (node == null)
                    continue;

                node.GraphScratchVersion = _graphBuildVersion;
                node.GraphScratchIndex = _topologyNodes.Count;
                _topologyNodes.Add(node);
            }

            int nodeCount = _topologyNodes.Count;
            if (nodeCount <= 0)
            {
                _logisticsGraph.BeginBuild(LogisticsNetworkType.PowerDc, 1, 1, 1);
                _logisticsGraph.FinalizeBuild();
                return;
            }

            int edgeCount = 0;
            int consumerCount = 0;

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];

                List<PowerNode> neighbors = node.Neighbors;
                if (neighbors != null)
                {
                    int neighborCount = neighbors.Count;
                    for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                    {
                        PowerNode neighbor = neighbors[neighborIndex];
                        if (neighbor == null ||
                            !ReferenceEquals(neighbor.Grid, this) ||
                            neighbor.GraphScratchVersion != _graphBuildVersion)
                        {
                            continue;
                        }

                        edgeCount++;
                    }
                }

                List<IPowerComponent> components = node.Components;
                if (components == null)
                {
                    if (node.IsRuptured)
                        consumerCount++;
                    continue;
                }

                int componentCount = components.Count;
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                {
                    IPowerComponent component = components[componentIndex];
                    if (component == null || component.PowerRating >= 0f)
                        continue;

                    consumerCount++;
                }

                if (node.IsRuptured)
                    consumerCount++;
            }

            _logisticsGraph.BeginBuild(LogisticsNetworkType.PowerDc, nodeCount, edgeCount, consumerCount);

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                byte statusBits = ResolveNodeStatusBits(node);
                _logisticsGraph.AddNode(
                    unchecked((uint)node.GetInstanceID()),
                    ResolveNodeCapacity(node),
                    1f,
                    ResolveNodePriorityTier(node),
                    ResolveNodeFlags(node, statusBits),
                    statusBits);
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];

                List<IPowerComponent> components = node.Components;
                if (components != null)
                {
                    int componentCount = components.Count;
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        IPowerComponent component = components[componentIndex];
                        if (component == null)
                            continue;

                        float rating = component.PowerRating;
                        if (rating > 0f)
                        {
                            _logisticsGraph.AddProducer(nodeIndex, rating);
                            continue;
                        }

                        if (rating >= 0f)
                            continue;

                        _consumerRefs.Add(component);
                        _logisticsGraph.AddConsumer(
                            nodeIndex,
                            -rating,
                            component.PowerPriority,
                            ResolveConsumerPriorityTier(component),
                            ResolveConsumerFlags(component));
                    }
                }

                if (node.IsRuptured)
                {
                    float ruptureDemand = ResolveRuptureDemand(node);
                    if (ruptureDemand > 0f)
                    {
                        _consumerRefs.Add(null);
                        _logisticsGraph.AddConsumer(
                            nodeIndex,
                            ruptureDemand,
                            0,
                            0,
                            LogisticsConsumerFlags.Essential);
                    }
                }

                List<PowerNode> neighbors = node.Neighbors;
                if (neighbors == null)
                    continue;

                int neighborCount = neighbors.Count;
                for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    PowerNode neighbor = neighbors[neighborIndex];
                    if (neighbor == null ||
                        !ReferenceEquals(neighbor.Grid, this) ||
                        neighbor.GraphScratchVersion != _graphBuildVersion)
                    {
                        continue;
                    }

                    _logisticsGraph.AddEdge(nodeIndex, neighbor.GraphScratchIndex, ResolveEdgeResistance(node, neighbor));
                }
            }

            _logisticsGraph.FinalizeBuild();
        }

        private void ApplyConsumerStates()
        {
            bool ambientLightsBrownedOut = _brownoutTier != LogisticsBrownoutTier.None;
            int consumerCount = _consumerRefs.Count;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                IPowerComponent consumer = _consumerRefs[consumerIndex];
                if (consumer == null)
                    continue;

                if (consumer is BaseModule baseModule)
                    baseModule.SetAmbientLightsBrownout(ambientLightsBrownedOut);

                bool shouldHavePower = _logisticsGraph.IsConsumerPowered(consumerIndex);
                if (consumer.HasPower != shouldHavePower)
                    consumer.OnPowerStatusChanged(shouldHavePower);
            }
        }

        private static float ResolveNodeCapacity(PowerNode node)
        {
            List<IPowerComponent> components = node.Components;
            if (components == null)
                return 1f;

            float totalCapacity = 0f;
            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                IPowerComponent component = components[componentIndex];
                if (component == null)
                    continue;

                totalCapacity += math.abs(component.PowerRating);
            }

            return math.max(1f, totalCapacity);
        }

        private static LogisticsNodeFlags ResolveNodeFlags(PowerNode node, byte statusBits)
        {
            LogisticsNodeFlags flags = LogisticsNodeFlags.Active | LogisticsNodeFlags.Dirty;
            if (node.IsRuptured)
                flags |= LogisticsNodeFlags.Ruptured;
            if ((statusBits & (byte)LogisticsModuleStatusBits.Overheating) != 0)
                flags |= LogisticsNodeFlags.Overloaded;

            List<IPowerComponent> components = node.Components;
            if (components == null)
                return flags;

            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                IPowerComponent component = components[componentIndex];
                if (component == null)
                    continue;

                LogisticsConsumerFlags consumerFlags = ResolveConsumerFlags(component);
                if ((consumerFlags & LogisticsConsumerFlags.EmergencyReserved) != 0)
                {
                    flags |= LogisticsNodeFlags.EmergencyReserved;
                    break;
                }
            }

            return flags;
        }

        private static byte ResolveNodeStatusBits(PowerNode node)
        {
            LogisticsModuleStatusBits statusBits = LogisticsModuleStatusBits.None;
            if (node == null)
                return (byte)statusBits;

            if (node.HasPower)
                statusBits |= LogisticsModuleStatusBits.Powered;
            if (node.IsShortCircuited)
                statusBits |= LogisticsModuleStatusBits.Overheating;
            if (node.IsRuptured)
                statusBits |= LogisticsModuleStatusBits.Damaged;

            List<IPowerComponent> components = node.Components;
            if (components == null)
                return (byte)statusBits;

            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                IPowerComponent component = components[componentIndex];
                if (component == null)
                    continue;

                if (component.HasPower)
                    statusBits |= LogisticsModuleStatusBits.Powered;

                if (component is not BaseModule baseModule)
                    continue;

                if (baseModule.IsFlooded)
                    statusBits |= LogisticsModuleStatusBits.Flooded;

                if (baseModule.CurrentIntegrity + 0.001f < baseModule.MaxRecoverableIntegrity ||
                    baseModule.HasCascadeFailure)
                {
                    statusBits |= LogisticsModuleStatusBits.Damaged;
                }
            }

            return (byte)statusBits;
        }

        private static byte ResolveNodePriorityTier(PowerNode node)
        {
            byte bestTier = 3;

            List<IPowerComponent> components = node.Components;
            if (components == null)
                return bestTier;

            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                IPowerComponent component = components[componentIndex];
                if (component == null)
                    continue;

                byte tier = ResolveConsumerPriorityTier(component);
                if (tier < bestTier)
                    bestTier = tier;
            }

            return bestTier;
        }

        private static byte ResolveConsumerPriorityTier(IPowerComponent consumer)
        {
            if (consumer is BaseModule)
                return 0;

            int priority = math.clamp(consumer.PowerPriority, 0, 100);
            if (priority <= 25)
                return 1;

            if (priority <= 60)
                return 2;

            return 3;
        }

        private static LogisticsConsumerFlags ResolveConsumerFlags(IPowerComponent consumer)
        {
            LogisticsConsumerFlags flags = LogisticsConsumerFlags.None;

            if (consumer is BaseModule)
            {
                flags |= LogisticsConsumerFlags.LifeSupport;
                flags |= LogisticsConsumerFlags.AmbientLighting;
                flags |= LogisticsConsumerFlags.Essential;
                flags |= LogisticsConsumerFlags.EmergencyReserved;
                return flags;
            }

            if (consumer.PowerPriority <= 25)
                flags |= LogisticsConsumerFlags.Essential;

            return flags;
        }

        private static float ResolveEdgeResistance(PowerNode sourceNode, PowerNode destinationNode)
        {
            Vector3 delta = destinationNode.transform.position - sourceNode.transform.position;
            float resistance = math.max(MinEdgeResistance, math.length((float3)delta));
            if (sourceNode.IsShortCircuited || destinationNode.IsShortCircuited)
                resistance *= ShortCircuitResistanceMultiplier;

            return resistance;
        }

        private static float ResolveRuptureDemand(PowerNode node)
        {
            return math.max(0.25f, ResolveNodeCapacity(node) * RuptureDemandFactor);
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
}
