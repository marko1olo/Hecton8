// ============================================================================
// HECTON-8 — PowerGrid.cs
// Energy grid owner. Uses a CSR-backed LogisticsNetworkGraph for alloc-free
// topology, DSU island detection, and priority brownout distribution.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Atmosphere;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
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
        private const float BatteryDispatchDeltaTimeSeconds = 0.5f;
        private const float BatteryEmergencyReserveThreshold = 0.10f;
        private const int BatteryParallelBatchSize = 16;
        private const float OverloadHeatWattsScale = 0.22f;
        private const float MinimumOverloadHeatWatts = 1800f;
        private const float OverloadThermalDamagePerSecond = 18f;
        private const float OverloadMeltdownTemperatureCelsius = 150f;

        private enum BatteryDispatchMode : byte
        {
            Idle = 0,
            Charge = 1,
            Discharge = 2
        }

        private struct BatteryDispatchRecord
        {
            public float StoredEnergyWattSeconds;
            public float CapacityWattSeconds;
            public float MaxChargePowerWatts;
            public float MaxDischargePowerWatts;
            public float ChargeEfficiency;
            public float DischargeEfficiency;
        }

        private struct BatteryDispatchResult
        {
            public float NextStoredEnergyWattSeconds;
            public float PlannedGridPowerWatts;
        }

        private struct OverloadThermalBinding
        {
            public BaseModule BaseModule;
            public SubmarineAtmosphereSystem Atmosphere;
            public IDamageReceiver DamageReceiver;
            public int RoomIndex;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct ResolveBatteryDispatchJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BatteryDispatchRecord> Records;
            public NativeArray<BatteryDispatchResult> Results;

            public BatteryDispatchMode Mode;
            public float RequestedPowerWatts;
            public float TotalAvailablePowerWatts;
            public float DeltaTimeSeconds;

            public void Execute(int index)
            {
                BatteryDispatchRecord record = Records[index];
                float safeCapacity = math.max(1f, record.CapacityWattSeconds);
                BatteryDispatchResult result = new BatteryDispatchResult
                {
                    NextStoredEnergyWattSeconds = math.clamp(record.StoredEnergyWattSeconds, 0f, safeCapacity),
                    PlannedGridPowerWatts = 0f
                };

                if (Mode == BatteryDispatchMode.Idle || RequestedPowerWatts <= 0.0001f || TotalAvailablePowerWatts <= 0.0001f)
                {
                    Results[index] = result;
                    return;
                }

                float safeDeltaTime = math.max(0.001f, DeltaTimeSeconds);
                float safeChargeEfficiency = math.max(0.1f, record.ChargeEfficiency);
                float safeDischargeEfficiency = math.max(0.1f, record.DischargeEfficiency);

                if (Mode == BatteryDispatchMode.Charge)
                {
                    float missingEnergy = math.max(0f, safeCapacity - record.StoredEnergyWattSeconds);
                    float availablePower = math.max(
                        0f,
                        math.min(
                            math.max(0f, record.MaxChargePowerWatts),
                            missingEnergy / (safeDeltaTime * safeChargeEfficiency)));
                    float allocatedPower = math.min(
                        availablePower,
                        RequestedPowerWatts * (availablePower / math.max(0.0001f, TotalAvailablePowerWatts)));
                    result.NextStoredEnergyWattSeconds = math.clamp(
                        record.StoredEnergyWattSeconds + (allocatedPower * safeDeltaTime * safeChargeEfficiency),
                        0f,
                        safeCapacity);
                    result.PlannedGridPowerWatts = -allocatedPower;
                    Results[index] = result;
                    return;
                }

                float availableOutputPower = math.max(
                    0f,
                    math.min(
                        math.max(0f, record.MaxDischargePowerWatts),
                        (record.StoredEnergyWattSeconds * safeDischargeEfficiency) / safeDeltaTime));
                float deliveredPower = math.min(
                    availableOutputPower,
                    RequestedPowerWatts * (availableOutputPower / math.max(0.0001f, TotalAvailablePowerWatts)));
                float drainedEnergy = (deliveredPower * safeDeltaTime) / safeDischargeEfficiency;
                result.NextStoredEnergyWattSeconds = math.clamp(record.StoredEnergyWattSeconds - drainedEnergy, 0f, safeCapacity);
                result.PlannedGridPowerWatts = deliveredPower;
                Results[index] = result;
            }
        }

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
        private readonly List<BatteryBankModule> _batteryRefs;
        private readonly List<PowerNode> _topologyNodes;
        private readonly List<OverloadThermalBinding> _overloadThermalBindings;
        private readonly Dictionary<PowerNode, float> _overloadThermalDamageByNode;
        private readonly LogisticsNetworkGraph _logisticsGraph;
        private NativeArray<BatteryDispatchRecord> _batteryDispatchRecords;
        private NativeArray<BatteryDispatchResult> _batteryDispatchResults;

        private float _totalGeneration;
        private float _totalConsumption;
        private float _balance;
        private float _supplyRatio = 1f;
        private float _totalBatteryStoredEnergyWattSeconds;
        private float _totalBatteryCapacityWattSeconds;
        private bool _hasPowerDeficit;
        private bool _hasBatteryBanks;
        private bool _batteryEmergencyReserveActive;
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

        /// <summary>Total committed stored energy across all storage banks in this grid.</summary>
        public float TotalBatteryStoredEnergyWattSeconds => _totalBatteryStoredEnergyWattSeconds;

        /// <summary>Total committed battery capacity across all storage banks in this grid.</summary>
        public float TotalBatteryCapacityWattSeconds => _totalBatteryCapacityWattSeconds;

        /// <summary>True while the remaining battery charge is reserved for emergency-only loads.</summary>
        public bool IsBatteryEmergencyReserveActive => _batteryEmergencyReserveActive;

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
            _batteryRefs = new List<BatteryBankModule>(safeCapacity);
            // COLD ALLOC: List<PowerNode>[initialCapacity] — topology node snapshot — owner: PowerGrid
            _topologyNodes = new List<PowerNode>(safeCapacity);
            // COLD ALLOC: List<OverloadThermalBinding>[initialCapacity] — per-node overload heat ownership cache — owner: PowerGrid
            _overloadThermalBindings = new List<OverloadThermalBinding>(safeCapacity);
            // COLD ALLOC: Dictionary<PowerNode,float>[initialCapacity] — persistent overload damage accumulation keyed by live nodes — owner: PowerGrid
            _overloadThermalDamageByNode = new Dictionary<PowerNode, float>(safeCapacity);
            _logisticsGraph = new LogisticsNetworkGraph(safeCapacity, safeCapacity * 4, safeCapacity * 2);
        }

        public void Dispose()
        {
            _logisticsGraph.CompleteEvaluation();
            _logisticsGraph.CompleteNodeStatePublish();
            _logisticsGraph.Dispose();
            DisposeBatteryDispatchBuffers();
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

            EvaluateBalanceViaScheduledJob();
            _isDirty = false;
            _hasEvaluatedAtLeastOnce = true;
        }

        /// <summary>Schedules a graph evaluation for the manager-owned SlowTick cadence.</summary>
        public void BeginSlowTickEvaluation()
        {
            if (_slowTickEvaluationPending || _logisticsGraph.HasPendingNodeStatePublish || _logisticsGraph.HasPendingEvaluation)
                return;

            if (!_isDirty && _hasEvaluatedAtLeastOnce && !_hasBatteryBanks)
                return;

            ResetBatteryDispatchPlans();
            BuildGraphSnapshot();

            if (_topologyNodes.Count <= 0)
            {
                _totalGeneration = 0f;
                _totalConsumption = 0f;
                _balance = 0f;
                _supplyRatio = 1f;
                _hasPowerDeficit = false;
                _brownoutTier = LogisticsBrownoutTier.None;
                _totalBatteryStoredEnergyWattSeconds = 0f;
                _totalBatteryCapacityWattSeconds = 0f;
                _hasBatteryBanks = false;
                _batteryEmergencyReserveActive = false;
                _islandCount = 0;
                _cycleCount = 0;
                _logisticsGraph.ClearPublishedNodeStates();
                _isDirty = false;
                _hasEvaluatedAtLeastOnce = true;
                return;
            }

            _logisticsGraph.ScheduleEvaluation();
            _slowTickEvaluationPending = true;
            _isDirty = false;
        }

        /// <summary>Completes the pending node-state publication pass and applies consumer power states.</summary>
        public void EndSlowTickEvaluation()
        {
            if (!_slowTickEvaluationPending)
                return;

            _logisticsGraph.CompleteEvaluation();
            LogisticsNetworkGraph.DistributionSummary rawDistribution = _logisticsGraph.GetScheduledDistributionSummary();
            ResolveBatteryDispatch(rawDistribution);

            BuildGraphSnapshot();
            JobHandle finalEvaluationHandle = _logisticsGraph.ScheduleEvaluation();
            _logisticsGraph.ScheduleNodeStatePublish(finalEvaluationHandle);
            _logisticsGraph.CompleteEvaluation();
            _logisticsGraph.CompleteNodeStatePublish();
            CommitBatteryDispatchPlans();
            ApplyDistributionSummary(
                _logisticsGraph.GetScheduledTopologySummary(),
                _logisticsGraph.GetScheduledDistributionSummary());
            ApplyConsumerStates();
            ApplyOverloadThermalDamage();
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

        private void EvaluateBalanceViaScheduledJob()
        {
            _slowTickEvaluationPending = false;
            ResetBatteryDispatchPlans();
            BuildGraphSnapshot();

            if (_topologyNodes.Count <= 0)
            {
                _totalGeneration = 0f;
                _totalConsumption = 0f;
                _balance = 0f;
                _supplyRatio = 1f;
                _hasPowerDeficit = false;
                _brownoutTier = LogisticsBrownoutTier.None;
                _totalBatteryStoredEnergyWattSeconds = 0f;
                _totalBatteryCapacityWattSeconds = 0f;
                _hasBatteryBanks = false;
                _batteryEmergencyReserveActive = false;
                _islandCount = 0;
                _cycleCount = 0;
                _logisticsGraph.ClearPublishedNodeStates();
                return;
            }

            _logisticsGraph.ScheduleEvaluation();
            _logisticsGraph.CompleteEvaluation();
            LogisticsNetworkGraph.DistributionSummary rawDistribution = _logisticsGraph.GetScheduledDistributionSummary();
            ResolveBatteryDispatch(rawDistribution);

            BuildGraphSnapshot();
            JobHandle finalEvaluationHandle = _logisticsGraph.ScheduleEvaluation();
            _logisticsGraph.ScheduleNodeStatePublish(finalEvaluationHandle);
            _logisticsGraph.CompleteEvaluation();
            _logisticsGraph.CompleteNodeStatePublish();
            CommitBatteryDispatchPlans();
            ApplyDistributionSummary(
                _logisticsGraph.GetScheduledTopologySummary(),
                _logisticsGraph.GetScheduledDistributionSummary());
            ApplyConsumerStates();
            ApplyOverloadThermalDamage();
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
            _batteryRefs.Clear();
            _topologyNodes.Clear();
            _overloadThermalBindings.Clear();

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
                    unchecked((uint)EntityId.ToULong(node.GetEntityId())),
                    ResolveNodeCapacity(node),
                    1f,
                    ResolveNodePriorityTier(node),
                    ResolveNodeFlags(node, statusBits),
                    statusBits);
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                OverloadThermalBinding overloadBinding = default;
                bool overloadBindingResolved = false;

                List<IPowerComponent> components = node.Components;
                if (components != null)
                {
                    int componentCount = components.Count;
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        IPowerComponent component = components[componentIndex];
                        if (component == null)
                            continue;

                        if (component is BatteryBankModule batteryBank)
                            _batteryRefs.Add(batteryBank);

                        if (!overloadBindingResolved && component is BaseModule baseModule)
                        {
                            SubmarineAtmosphereSystem atmosphere = baseModule.GetComponentInParent<SubmarineAtmosphereSystem>();
                            overloadBinding = new OverloadThermalBinding
                            {
                                BaseModule = baseModule,
                                Atmosphere = atmosphere,
                                DamageReceiver = baseModule.GetComponent<IDamageReceiver>(),
                                RoomIndex = atmosphere != null
                                    ? atmosphere.ResolveNearestRoomIndexForWorldPosition(baseModule.transform.position)
                                    : -1
                            };
                            overloadBindingResolved = true;
                        }

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

                _overloadThermalBindings.Add(overloadBinding);

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
            bool ambientLightsBrownedOut = _brownoutTier != LogisticsBrownoutTier.None || _batteryEmergencyReserveActive;
            int consumerCount = _consumerRefs.Count;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                IPowerComponent consumer = _consumerRefs[consumerIndex];
                if (consumer == null)
                    continue;

                if (consumer is BaseModule baseModule)
                    baseModule.SetAmbientLightsBrownout(ambientLightsBrownedOut);

                bool shouldHavePower = _logisticsGraph.IsConsumerPowered(consumerIndex);
                if (_batteryEmergencyReserveActive && !ShouldRemainPoweredDuringBatteryReserve(consumer))
                    shouldHavePower = false;
                if (consumer.HasPower != shouldHavePower)
                    consumer.OnPowerStatusChanged(shouldHavePower);
            }
        }

        private void ApplyOverloadThermalDamage()
        {
            int nodeCount = math.min(_topologyNodes.Count, _overloadThermalBindings.Count);
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                if (node == null)
                    continue;

                OverloadThermalBinding binding = _overloadThermalBindings[nodeIndex];
                if (binding.BaseModule == null || binding.Atmosphere == null || binding.RoomIndex < 0)
                    continue;

                uint nodeId = unchecked((uint)EntityId.ToULong(node.GetEntityId()));
                if (!_logisticsGraph.TryGetPublishedNodeState(nodeId, out ushort stateBits))
                    continue;

                bool overloaded = (((LogisticsNodeStateBits)stateBits) & LogisticsNodeStateBits.Overloaded) != 0;
                if (!overloaded)
                {
                    if (_overloadThermalDamageByNode.TryGetValue(node, out float cooledDamage) && cooledDamage > 0f)
                        _overloadThermalDamageByNode[node] = math.max(0f, cooledDamage - (OverloadThermalDamagePerSecond * BatteryDispatchDeltaTimeSeconds * 0.5f));
                    continue;
                }

                float overloadHeatWatts = math.max(MinimumOverloadHeatWatts, ResolveNodeCapacity(node) * OverloadHeatWattsScale);
                binding.Atmosphere.InjectRoomHeatEnergyJoules(binding.RoomIndex, overloadHeatWatts * BatteryDispatchDeltaTimeSeconds);
                node.SetShortCircuited(true);

                float accumulatedThermalDamage = 0f;
                _overloadThermalDamageByNode.TryGetValue(node, out accumulatedThermalDamage);
                accumulatedThermalDamage += OverloadThermalDamagePerSecond * BatteryDispatchDeltaTimeSeconds;
                _overloadThermalDamageByNode[node] = accumulatedThermalDamage;

                float roomTemperature = binding.Atmosphere.GetRoomTemperatureCelsius(binding.RoomIndex);
                if (roomTemperature < OverloadMeltdownTemperatureCelsius || node.IsRuptured)
                    continue;

                node.SetRuptured(true);
                FatalPressureImplosionEvent implosionEvent = new FatalPressureImplosionEvent(
                    nodeId,
                    binding.RoomIndex,
                    roomTemperature,
                    binding.BaseModule.transform.position);
                FatalPressureImplosionEvents.Notify(in implosionEvent);

                if (binding.DamageReceiver != null)
                {
                    float previousIntegrity = binding.BaseModule.MaxRecoverableIntegrity > 0.01f
                        ? math.saturate(binding.BaseModule.CurrentIntegrity / binding.BaseModule.MaxRecoverableIntegrity)
                        : 0f;
                    DamagePacket packet = new DamagePacket
                    {
                        Channel = DamageChannel.HullBreach,
                        PreviousValue = previousIntegrity,
                        NextValue = 0f,
                        Magnitude = math.max(accumulatedThermalDamage, roomTemperature),
                        LocalPoint = float3.zero,
                        DamageType = 0u,
                        IntegrityDelta = byte.MaxValue,
                        Depth = 0f,
                        SourceId = 0,
                        TraumaLevel = 0
                    };
                    binding.DamageReceiver.ReceiveDamage(in packet);
                }
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

            if (consumer is SubmarineElectrolysisModule)
            {
                flags |= LogisticsConsumerFlags.LifeSupport;
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

        private void ApplyDistributionSummary(
            LogisticsNetworkGraph.TopologySummary topology,
            LogisticsNetworkGraph.DistributionSummary distribution)
        {
            _totalGeneration = distribution.TotalGeneration;
            _totalConsumption = distribution.TotalConsumption;
            _balance = distribution.Balance;
            _supplyRatio = distribution.SupplyRatio;
            _hasPowerDeficit = distribution.HasDeficit;
            _brownoutTier = _batteryEmergencyReserveActive
                ? LogisticsBrownoutTier.EmergencyOnly
                : distribution.BrownoutTier;
            _islandCount = topology.IslandCount;
            _cycleCount = topology.CycleCount;
        }

        private void ResetBatteryDispatchPlans()
        {
            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
                _batteryRefs[batteryIndex]?.ResetDispatchPlan();

            _batteryEmergencyReserveActive = false;
        }

        private void ResolveBatteryDispatch(LogisticsNetworkGraph.DistributionSummary rawDistribution)
        {
            float rawBalance = rawDistribution.Balance;
            int batteryCount = _batteryRefs.Count;
            _hasBatteryBanks = batteryCount > 0;
            _totalBatteryStoredEnergyWattSeconds = 0f;
            _totalBatteryCapacityWattSeconds = 0f;
            _batteryEmergencyReserveActive = false;

            if (batteryCount <= 0)
                return;

            EnsureBatteryDispatchCapacity(batteryCount);

            float totalChargeAcceptanceWatts = 0f;
            float totalDischargeAvailabilityWatts = 0f;
            float totalReservePreservingDischargeWatts = 0f;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                float chargeAcceptanceWatts = battery.ResolveChargeAcceptanceWatts(BatteryDispatchDeltaTimeSeconds);
                float dischargeAvailabilityWatts = battery.ResolveDischargeAvailabilityWatts(BatteryDispatchDeltaTimeSeconds);
                float reservePreservingDischargeWatts = battery.ResolveDischargeAvailabilityWatts(
                    BatteryDispatchDeltaTimeSeconds,
                    BatteryEmergencyReserveThreshold);
                _batteryDispatchRecords[batteryIndex] = new BatteryDispatchRecord
                {
                    StoredEnergyWattSeconds = battery.StoredEnergyWattSeconds,
                    CapacityWattSeconds = battery.CapacityWattSeconds,
                    MaxChargePowerWatts = chargeAcceptanceWatts,
                    MaxDischargePowerWatts = dischargeAvailabilityWatts,
                    ChargeEfficiency = battery.ChargeEfficiency,
                    DischargeEfficiency = battery.DischargeEfficiency
                };

                _totalBatteryStoredEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
                totalChargeAcceptanceWatts += chargeAcceptanceWatts;
                totalDischargeAvailabilityWatts += dischargeAvailabilityWatts;
                totalReservePreservingDischargeWatts += reservePreservingDischargeWatts;
            }

            bool reserveAlreadyActive = ResolveBatteryEmergencyReserveActive(
                _totalBatteryStoredEnergyWattSeconds,
                _totalBatteryCapacityWattSeconds);

            BatteryDispatchMode dispatchMode = BatteryDispatchMode.Idle;
            float requestedPowerWatts = 0f;
            float totalAvailablePowerWatts = 0f;
            if (rawBalance > 0.0001f && totalChargeAcceptanceWatts > 0.0001f)
            {
                dispatchMode = BatteryDispatchMode.Charge;
                requestedPowerWatts = rawBalance;
                totalAvailablePowerWatts = totalChargeAcceptanceWatts;
            }
            else if (rawBalance < -0.0001f && totalDischargeAvailabilityWatts > 0.0001f)
            {
                dispatchMode = BatteryDispatchMode.Discharge;
                if (reserveAlreadyActive)
                {
                    requestedPowerWatts = math.max(0f, ResolveEmergencyReservedDemandWatts() - rawDistribution.TotalGeneration);
                    totalAvailablePowerWatts = totalDischargeAvailabilityWatts;
                }
                else
                {
                    requestedPowerWatts = -rawBalance;
                    totalAvailablePowerWatts = totalReservePreservingDischargeWatts;
                }
            }

            if (dispatchMode == BatteryDispatchMode.Idle ||
                requestedPowerWatts <= 0.0001f ||
                totalAvailablePowerWatts <= 0.0001f)
            {
                _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                    _totalBatteryStoredEnergyWattSeconds,
                    _totalBatteryCapacityWattSeconds);
                return;
            }

            ResolveBatteryDispatchJob dispatchJob = new ResolveBatteryDispatchJob
            {
                Records = _batteryDispatchRecords,
                Results = _batteryDispatchResults,
                Mode = dispatchMode,
                RequestedPowerWatts = requestedPowerWatts,
                TotalAvailablePowerWatts = totalAvailablePowerWatts,
                DeltaTimeSeconds = BatteryDispatchDeltaTimeSeconds
            };

            JobHandle dispatchHandle = dispatchJob.Schedule(batteryCount, BatteryParallelBatchSize);
            dispatchHandle.Complete();

            _totalBatteryStoredEnergyWattSeconds = 0f;
            _totalBatteryCapacityWattSeconds = 0f;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                BatteryDispatchResult result = _batteryDispatchResults[batteryIndex];
                battery.StageResolvedDispatch(result.NextStoredEnergyWattSeconds, result.PlannedGridPowerWatts);
                _totalBatteryStoredEnergyWattSeconds += result.NextStoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
            }

            _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                _totalBatteryStoredEnergyWattSeconds,
                _totalBatteryCapacityWattSeconds);
        }

        private void CommitBatteryDispatchPlans()
        {
            int batteryCount = _batteryRefs.Count;
            _totalBatteryStoredEnergyWattSeconds = 0f;
            _totalBatteryCapacityWattSeconds = 0f;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                battery.CommitResolvedDispatch();
                _totalBatteryStoredEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
            }

            _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                _totalBatteryStoredEnergyWattSeconds,
                _totalBatteryCapacityWattSeconds);
        }

        private void EnsureBatteryDispatchCapacity(int batteryCount)
        {
            if (batteryCount <= 0)
                return;

            if (!_batteryDispatchRecords.IsCreated || _batteryDispatchRecords.Length < batteryCount)
            {
                if (_batteryDispatchRecords.IsCreated)
                    _batteryDispatchRecords.Dispose();

                _batteryDispatchRecords = new NativeArray<BatteryDispatchRecord>(
                    batteryCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
            }

            if (!_batteryDispatchResults.IsCreated || _batteryDispatchResults.Length < batteryCount)
            {
                if (_batteryDispatchResults.IsCreated)
                    _batteryDispatchResults.Dispose();

                _batteryDispatchResults = new NativeArray<BatteryDispatchResult>(
                    batteryCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory);
            }
        }

        private void DisposeBatteryDispatchBuffers()
        {
            if (_batteryDispatchRecords.IsCreated)
                _batteryDispatchRecords.Dispose();

            if (_batteryDispatchResults.IsCreated)
                _batteryDispatchResults.Dispose();
        }

        private static bool ResolveBatteryEmergencyReserveActive(float totalStoredEnergyWattSeconds, float totalCapacityWattSeconds)
        {
            if (totalCapacityWattSeconds <= 0.0001f)
                return false;

            return (totalStoredEnergyWattSeconds / totalCapacityWattSeconds) <= BatteryEmergencyReserveThreshold + 0.0001f;
        }

        private static bool ShouldRemainPoweredDuringBatteryReserve(IPowerComponent consumer)
        {
            return consumer is BaseModule || consumer is SubmarineElectrolysisModule;
        }

        private float ResolveEmergencyReservedDemandWatts()
        {
            float demandWatts = 0f;
            int consumerCount = _consumerRefs.Count;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                IPowerComponent consumer = _consumerRefs[consumerIndex];
                if (consumer == null)
                    continue;

                if ((ResolveConsumerFlags(consumer) & LogisticsConsumerFlags.EmergencyReserved) == 0)
                    continue;

                demandWatts += math.max(0f, -consumer.PowerRating);
            }

            return demandWatts;
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
