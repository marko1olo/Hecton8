// ============================================================================
// HECTON-8 — PowerGrid.cs
// Energy grid owner. Uses a CSR-backed LogisticsNetworkGraph for alloc-free
// topology, DSU island detection, and binary brownout state.
// ============================================================================

using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.World;

using SubmarineFluidDynamics = global::Hecton8.Physics.SubmarineFluidDynamics;

namespace Hecton8.Power
{
    /// <summary>
    /// One connected power grid.
    /// Owns node membership, native graph scratch, and consumer power state application.
    /// </summary>
    public sealed class PowerGrid
    {
        private static int s_x001DirectSignalPushDropCount_PowerGrid;

        private static int s_x001PowerGridSignalPushDropCount;
        private const float MinEdgeResistance = 0.0001f;
        private const float MinEdgeResistanceSquared = MinEdgeResistance * MinEdgeResistance;
        private const float RuptureDemandFactor = 0.015f;
        private const float ShortCircuitResistanceMultiplier = 100f;
        private const float BatteryDispatchDeltaTimeSeconds = 0.5f;
        private const float BatteryEmergencyReserveThreshold = 0.10f;
        private const float OverloadHeatWattsScale = 0.22f;
        private const float MinimumOverloadHeatWatts = 1800f;
        private const float OverloadThermalDamagePerSecond = 18f;
        private const float OverloadMeltdownTemperatureCelsius = 150f;
        private const float CableThermalConductivityWattsPerCelsius = 140f;
        private const int CableThermalSharePassCount = 8;
        private const float OceanThermalSinkTemperatureCelsius = 2f;
        private const float ThermalHeatInjectionScale = 0.001f;
        private const float ThermalDissipationApplyBlend = 0.25f;
        private const float MaximumAppliedThermalDeltaCelsius = 5f;
        private const float MinimumSubmergedFloodFillRatio = 0.05f;
        private const float SubmergedOverloadHeatJoulesMultiplier = 4.0f;
        private const float MinimumSubmergedOverloadHeatJoules = 3200f;
        private const float HydrogenPocketUnitsPerMegajoule = 0.04f;
        private const float OxygenPocketUnitsPerMegajoule = 0.02f;
        private const float BrownoutPotentialThreshold = LogisticsNetworkGraph.TwoPassPowerGridSolverJob.BrownoutPotentialThreshold;
        private const float FloodedShortCircuitPotentialThreshold = LogisticsNetworkGraph.TwoPassPowerGridSolverJob.FloodedShortCircuitPotentialThreshold;
        private const int PowerGridScratchBufferBase = 731700;
        private const int PowerGridScratchBufferStride = 16;
        private const int BatteryDispatchRecordsBufferOffset = 0;
        private const int BatteryDispatchResultsBufferOffset = 1;
        private const int ThermalTemperatureFrontBufferOffset = 2;
        private const int ThermalTemperatureBackBufferOffset = 3;
        private const int ThermalHeatInjectionBufferOffset = 4;
        private const int ThermalHullSinkConductanceBufferOffset = 5;
        private const int ThermalEdgeOffsetsBufferOffset = 6;
        private const int ThermalEdgeDestinationsBufferOffset = 7;
        private const int ThermalEdgeConductanceBufferOffset = 8;
        private static readonly uint ThermalDissipationDeferredWarningHash =
            unchecked((uint)LocHash.Compute("PowerGrid.ThermalDissipationDeferred"));
        private static readonly uint ThermalDissipationContextHash =
            unchecked((uint)LocHash.Compute("PowerGrid.ThermalDissipation"));

        private enum BatteryDispatchMode : byte
        {
            Idle = 0,
            Charge = 1,
            Discharge = 2
        }

        private enum SlowTickEvaluationPhase : byte
        {
            Idle = 0,
            InitialEvaluation = 1,
            FinalEvaluation = 2,
            ThermalDissipation = 3
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
            public ISubmarineAtmosphereRoomMutationSink Atmosphere;
            public SubmarineFluidDynamics FluidDynamics;
            public IDamageReceiver DamageReceiver;
            public int RoomIndex;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct ThermalSharePassJob : IJobParallelFor
        {
            private const float MinConductance = 0.0001f;

            public int NodeCount;
            public float OceanTemperatureCelsius;

            [ReadOnly, NoAlias] public NativeArray<int> EdgeOffsets;
            [ReadOnly, NoAlias] public NativeArray<int> EdgeDestinations;
            [ReadOnly, NoAlias] public NativeArray<float> EdgeConductance;
            [ReadOnly, NoAlias] public NativeArray<float> HeatInjection;
            [ReadOnly, NoAlias] public NativeArray<float> HullSinkConductance;
            [ReadOnly, NoAlias] public NativeArray<float> TemperatureInput;

            [NoAlias] public NativeArray<float> TemperatureOutput;

            public void Execute(int nodeIndex)
            {
                int safeNodeCount = math.min(NodeCount, math.min(TemperatureInput.Length, TemperatureOutput.Length));
                if (nodeIndex < 0 || nodeIndex >= safeNodeCount)
                    return;

                float weightedTemperature = HeatInjection[nodeIndex];
                float conductanceSum = 0f;

                int edgeStart = EdgeOffsets[nodeIndex];
                int edgeEnd = EdgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int neighborNodeIndex = EdgeDestinations[edgeIndex];
                    bool neighborInRange = (uint)neighborNodeIndex < (uint)safeNodeCount;
                    int safeNeighborIndex = math.clamp(neighborNodeIndex, 0, safeNodeCount - 1);
                    float conductance = math.max(MinConductance, EdgeConductance[edgeIndex]) *
                                        math.select(0f, 1f, neighborInRange);
                    conductanceSum += conductance;
                    weightedTemperature += conductance * TemperatureInput[safeNeighborIndex];
                }

                float hullSinkConductance = math.max(0f, HullSinkConductance[nodeIndex]);
                conductanceSum += hullSinkConductance;
                weightedTemperature += hullSinkConductance * OceanTemperatureCelsius;

                float solvedTemperature = weightedTemperature * math.rcp(math.max(conductanceSum, MinConductance));
                float nextTemperature = math.select(TemperatureInput[nodeIndex], solvedTemperature, conductanceSum > MinConductance);
                nextTemperature = math.select(TemperatureInput[nodeIndex], nextTemperature, math.isfinite(nextTemperature));

                TemperatureOutput[nodeIndex] = nextTemperature;
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
        private readonly List<uint> _consumerNodeIds;
        private readonly List<BatteryBankModule> _batteryRefs;
        private readonly List<PowerNode> _topologyNodes;
        private readonly List<OverloadThermalBinding> _overloadThermalBindings;
        private readonly Dictionary<PowerNode, float> _overloadThermalDamageByNode;
        private readonly LogisticsNetworkGraph _logisticsGraph;
        private readonly int _vaultBufferBase;
        private IDataVault _dataVault;
        private VaultGenerationHandle<BatteryDispatchRecord> _batteryDispatchRecordsHandle;
        private VaultGenerationHandle<BatteryDispatchResult> _batteryDispatchResultsHandle;
        private VaultGenerationHandle<float> _thermalTemperatureFrontHandle;
        private VaultGenerationHandle<float> _thermalTemperatureBackHandle;
        private VaultGenerationHandle<float> _thermalHeatInjectionHandle;
        private VaultGenerationHandle<float> _thermalHullSinkConductanceHandle;
        private VaultGenerationHandle<int> _thermalEdgeOffsetsHandle;
        private VaultGenerationHandle<int> _thermalEdgeDestinationsHandle;
        private VaultGenerationHandle<float> _thermalEdgeConductanceHandle;
        private JobHandle _thermalDissipationHandle;
        private bool _thermalDissipationPending;
        private bool _thermalDissipationResultInBackBuffer;
        private int _thermalDissipationNodeCount;
        private int _thermalDissipationScheduledIterations;
        private int _thermalDissipationDeferredFrames;

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
        private int _cableThermalIterationBudget = CableThermalSharePassCount;
        private bool _isDirty = true;
        private bool _hasEvaluatedAtLeastOnce;
        private bool _slowTickEvaluationPending;
        private bool _slowTickNodeStatePublishScheduled;
        private bool _splitCheckPending;
        private SlowTickEvaluationPhase _slowTickEvaluationPhase;

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

        internal float TryConsumeWirelessToolDemand(float requestedEnergyWattSeconds)
        {
            if (requestedEnergyWattSeconds <= 0f || !_hasBatteryBanks || _batteryEmergencyReserveActive)
                return 0f;

            float remaining = requestedEnergyWattSeconds;
            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                if (remaining <= 0.0001f)
                    break;

                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                remaining -= battery.TryConsumeDirectGridEnergy(remaining, BatteryEmergencyReserveThreshold);
            }

            _totalBatteryStoredEnergyWattSeconds = 0f;
            _totalBatteryCapacityWattSeconds = 0f;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                _totalBatteryStoredEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
            }

            _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                _totalBatteryStoredEnergyWattSeconds,
                _totalBatteryCapacityWattSeconds);
            return requestedEnergyWattSeconds - remaining;
        }

        public PowerGrid(int initialCapacity = 16, IDataVault dataVault = null)
        {
            int safeCapacity = math.max(1, initialCapacity);

            Id = _nextId++;
            _vaultBufferBase = PowerGridScratchBufferBase + (Id * PowerGridScratchBufferStride);
            _dataVault = dataVault;
            // COLD ALLOC: HashSet<PowerNode>[initialCapacity] — grid membership cache — owner: PowerGrid
            _nodes = new HashSet<PowerNode>(safeCapacity);
            // COLD ALLOC: List<IPowerComponent>[initialCapacity] — consumer reference cache — owner: PowerGrid
            _consumerRefs = new List<IPowerComponent>(safeCapacity);
            // COLD ALLOC: List<uint>[initialCapacity] - stable node ids parallel to consumer references - owner: PowerGrid
            _consumerNodeIds = new List<uint>(safeCapacity);
            _batteryRefs = new List<BatteryBankModule>(safeCapacity);
            // COLD ALLOC: List<PowerNode>[initialCapacity] — topology node snapshot — owner: PowerGrid
            _topologyNodes = new List<PowerNode>(safeCapacity);
            // COLD ALLOC: List<OverloadThermalBinding>[initialCapacity] — per-node overload heat ownership cache — owner: PowerGrid
            _overloadThermalBindings = new List<OverloadThermalBinding>(safeCapacity);
            // COLD ALLOC: Dictionary<PowerNode,float>[initialCapacity] — persistent overload damage accumulation keyed by live nodes — owner: PowerGrid
            _overloadThermalDamageByNode = new Dictionary<PowerNode, float>(safeCapacity);
            _logisticsGraph = new LogisticsNetworkGraph(safeCapacity, safeCapacity * 4, safeCapacity * 2);
        }

        public void InjectDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            CompleteThermalDissipationForTeardown();
            DisposeBatteryDispatchBuffers();
            DisposeThermalDissipationBuffers();
            _dataVault = dataVault;
        }

        public void Dispose()
        {
            CompleteThermalDissipationForTeardown();
            _logisticsGraph.CompleteEvaluation();
            _logisticsGraph.CompleteNodeStatePublish();
            _logisticsGraph.Dispose();
            DisposeBatteryDispatchBuffers();
            DisposeThermalDissipationBuffers();
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

        internal void RequestSplitCheck()
        {
            _splitCheckPending = true;
            _isDirty = true;
        }

        internal bool TryConsumePendingSplitCheck(out LogisticsNetworkGraph.TopologySummary topology)
        {
            topology = default;
            if (!_splitCheckPending)
                return false;

            if (_slowTickEvaluationPending || _logisticsGraph.HasPendingEvaluation || _logisticsGraph.HasPendingNodeStatePublish)
                return false;

            if (!_hasEvaluatedAtLeastOnce)
            {
                BeginSlowTickEvaluation();
                return false;
            }

            topology = _logisticsGraph.GetScheduledTopologySummary();
            _splitCheckPending = false;
            return true;
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
            _isDirty = true;
            BeginSlowTickEvaluation();
        }

        /// <summary>Schedules a graph evaluation for the manager-owned SlowTick cadence.</summary>
        public void BeginSlowTickEvaluation()
        {
            if (_slowTickEvaluationPending || _logisticsGraph.HasPendingNodeStatePublish || _logisticsGraph.HasPendingEvaluation)
                return;

            if (!_isDirty && _hasEvaluatedAtLeastOnce && !_hasBatteryBanks)
            {
                _logisticsGraph.ScheduleEvaluation();
                _slowTickNodeStatePublishScheduled = false;
                _slowTickEvaluationPending = true;
                _slowTickEvaluationPhase = SlowTickEvaluationPhase.FinalEvaluation;
                return;
            }

            ResetBatteryDispatchPlans();
            if (!BuildGraphSnapshot())
                return;

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
                _slowTickNodeStatePublishScheduled = false;
                _slowTickEvaluationPhase = SlowTickEvaluationPhase.Idle;
                return;
            }

            _logisticsGraph.ScheduleEvaluation();
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPending = true;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.InitialEvaluation;
            _isDirty = false;
        }

        /// <summary>Completes the pending node-state publication pass and applies consumer power states.</summary>
        public void EndSlowTickEvaluation()
        {
            TryEndSlowTickEvaluation();
        }

        /// <summary>
        /// Progresses the pending slow-tick evaluation without forcing job completion.
        /// </summary>
        /// <returns>True when the evaluation is fully committed or no evaluation is pending.</returns>
        public bool TryEndSlowTickEvaluation()
        {
            if (!_slowTickEvaluationPending)
                return true;

            if (_slowTickEvaluationPhase == SlowTickEvaluationPhase.InitialEvaluation)
            {
                if (!_logisticsGraph.TryCompleteEvaluation())
                    return false;

                ScheduleFinalSlowTickEvaluationPass();
            }

            if (_slowTickEvaluationPhase == SlowTickEvaluationPhase.FinalEvaluation)
            {
                if (!_logisticsGraph.TryCompleteEvaluation())
                    return false;

                if (!_slowTickNodeStatePublishScheduled)
                {
                    _logisticsGraph.ScheduleNodeStatePublish();
                    _slowTickNodeStatePublishScheduled = true;
                }

                if (!_logisticsGraph.TryCompleteNodeStatePublish())
                    return false;

                CommitSlowTickEvaluation();
            }

            if (_slowTickEvaluationPhase == SlowTickEvaluationPhase.ThermalDissipation)
            {
                if (!TryCommitCableThermalSharing())
                    return false;

                CompleteSlowTickCommit();
            }

            return !_slowTickEvaluationPending;
        }

        /// <summary>Rebuilds the topology-only snapshot and returns current connectivity analysis.</summary>
        internal LogisticsNetworkGraph.TopologySummary AnalyzeTopology()
        {
            if (!BuildGraphSnapshot())
                return _logisticsGraph.GetScheduledTopologySummary();

            LogisticsNetworkGraph.TopologySummary topology = _logisticsGraph.AnalyzeTopology();
            _islandCount = topology.IslandCount;
            _cycleCount = topology.CycleCount;
            return topology;
        }

        private void ScheduleFinalSlowTickEvaluationPass()
        {
            LogisticsNetworkGraph.DistributionSummary rawDistribution = _logisticsGraph.GetScheduledDistributionSummary();
            ResolveBatteryDispatch(rawDistribution);

            if (!BuildGraphSnapshot())
                return;
            _logisticsGraph.ScheduleEvaluation();
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.FinalEvaluation;
        }

        private void CommitSlowTickEvaluation()
        {
            CommitBatteryDispatchPlans();
            ApplyDistributionSummary(
                _logisticsGraph.GetScheduledTopologySummary(),
                _logisticsGraph.GetScheduledDistributionSummary());
            ApplyConsumerStates();
            ApplyOverloadThermalDamage();
            ApplyFloodedShortCircuitDamage();
            if (ScheduleCableThermalSharing())
            {
                _slowTickEvaluationPhase = SlowTickEvaluationPhase.ThermalDissipation;
                return;
            }

            CompleteSlowTickCommit();
        }

        private void CompleteSlowTickCommit()
        {
            _slowTickEvaluationPending = false;
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.Idle;
            _hasEvaluatedAtLeastOnce = true;
        }

        private void EvaluateBalanceViaScheduledJob()
        {
            _slowTickEvaluationPending = false;
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.Idle;
            ResetBatteryDispatchPlans();
            if (!BuildGraphSnapshot())
                return;

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
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPending = true;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.InitialEvaluation;
            EndSlowTickEvaluation();
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

        internal NativeArray<float>.ReadOnly GetPowerPotentialsReadOnly()
        {
            return _logisticsGraph.GetPowerPotentialsReadOnly();
        }

        internal NativeArray<byte>.ReadOnly GetNodeFlagsReadOnly()
        {
            return _logisticsGraph.GetNodeFlagsReadOnly();
        }

        internal bool TryGetNodePotential(int nodeIndex, out float potential)
        {
            return _logisticsGraph.TryGetNodePotential(nodeIndex, out potential);
        }

        internal bool TryConsumeNodePotential(int nodeIndex, float potentialCost)
        {
            return _logisticsGraph.TryConsumeNodePotential(nodeIndex, potentialCost);
        }

        internal bool TryRemovePowerConnectionBucket(int sourceNodeIndex)
        {
            bool removed = _logisticsGraph.TryRemovePowerConnectionBucket(sourceNodeIndex);
            if (removed)
                _isDirty = true;
            return removed;
        }

        private bool BuildGraphSnapshot()
        {
            if (_logisticsGraph.HasPendingEvaluation || _logisticsGraph.HasPendingNodeStatePublish)
                return false;

            _consumerRefs.Clear();
            _consumerNodeIds.Clear();
            _batteryRefs.Clear();
            _topologyNodes.Clear();
            _overloadThermalBindings.Clear();

            int rawNodeCount = _nodes.Count;
            if (rawNodeCount <= 0)
            {
                _logisticsGraph.BeginBuild(LogisticsNetworkType.PowerDc, 1, 1, 1);
                _logisticsGraph.FinalizeBuild();
                return true;
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
                return true;
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
                            neighbor.GraphScratchVersion != _graphBuildVersion ||
                            !ShouldPublishPowerEdge(node, neighbor))
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
                uint nodeId = unchecked((uint)EntityId.ToULong(node.GetEntityId()));
                byte statusBits = ResolveNodeStatusBits(node);
                _logisticsGraph.AddNode(
                    nodeId,
                    ResolveNodeCapacity(node),
                    1f,
                    ResolveNodePriorityTier(node),
                    ResolveNodeFlags(node, statusBits),
                    statusBits);
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                uint nodeId = unchecked((uint)EntityId.ToULong(node.GetEntityId()));
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
                            ISubmarineAtmosphereRoomMutationSink atmosphere = ComponentReferenceUtility.ResolveParentService<ISubmarineAtmosphereRoomMutationSink>(baseModule);
                            if (atmosphere != null && !atmosphere.IsAtmosphereRuntimeActive)
                                atmosphere = null;
                            overloadBinding = new OverloadThermalBinding
                            {
                                BaseModule = baseModule,
                                Atmosphere = atmosphere,
                                FluidDynamics = baseModule.GetComponentInParent<SubmarineFluidDynamics>(),
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
                        _consumerNodeIds.Add(nodeId);
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
                        _consumerNodeIds.Add(nodeId);
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
                        neighbor.GraphScratchVersion != _graphBuildVersion ||
                        !ShouldPublishPowerEdge(node, neighbor))
                    {
                        continue;
                    }

                    _logisticsGraph.AddEdge(nodeIndex, neighbor.GraphScratchIndex, ResolveEdgeResistance(node, neighbor));
                }
            }

            _logisticsGraph.FinalizeBuild();
            return true;
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

                bool consumerVoltageResolved = _logisticsGraph.TryGetConsumerVoltageSupplyRatio(
                    consumerIndex,
                    out float consumerVoltageSupplyRatio);
                if (!consumerVoltageResolved)
                    consumerVoltageSupplyRatio = _supplyRatio;

                consumerVoltageSupplyRatio = math.saturate(math.select(0f, consumerVoltageSupplyRatio, math.isfinite(consumerVoltageSupplyRatio)));
                BaseModule baseModule = consumer as BaseModule;
                IContinuousPowerComponent continuousPower = baseModule == null ? consumer as IContinuousPowerComponent : null;
                if (baseModule == null &&
                    continuousPower != null &&
                    math.abs(continuousPower.Voltage01 - consumerVoltageSupplyRatio) > 0.0005f)
                {
                    continuousPower.OnVoltageChanged(consumerVoltageSupplyRatio);
                }

                bool voltageBrownout = consumerVoltageSupplyRatio < BrownoutPotentialThreshold;
                if (baseModule != null)
                    baseModule.SetAmbientPowerVisualState(
                        ambientLightsBrownedOut || consumerVoltageSupplyRatio < 0.80f,
                        consumerVoltageSupplyRatio);

                bool shouldHavePower = _logisticsGraph.IsConsumerPowered(consumerIndex);
                if (voltageBrownout && baseModule == null && continuousPower == null)
                    shouldHavePower = false;
                if (_batteryEmergencyReserveActive && !ShouldRemainPoweredDuringBatteryReserve(consumer))
                    shouldHavePower = false;
                if (consumer.HasPower != shouldHavePower)
                {
                    bool wasPowered = consumer.HasPower;
                    consumer.OnPowerStatusChanged(shouldHavePower);
                    if (wasPowered && !shouldHavePower)
                    {
                        uint nodeId = consumerIndex < _consumerNodeIds.Count ? _consumerNodeIds[consumerIndex] : 0u;
                        PublishNodeBrownoutSignal(nodeId, consumerVoltageSupplyRatio, consumer.PowerPriority);
                    }
                }
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
                ApplySubmergedOverloadFluidHeating(binding, overloadHeatWatts);

                float accumulatedThermalDamage = 0f;
                _overloadThermalDamageByNode.TryGetValue(node, out accumulatedThermalDamage);
                accumulatedThermalDamage += OverloadThermalDamagePerSecond * BatteryDispatchDeltaTimeSeconds;
                _overloadThermalDamageByNode[node] = accumulatedThermalDamage;

                float roomTemperature = binding.Atmosphere.GetRoomTemperatureCelsius(binding.RoomIndex);
                TryTriggerThermalMeltdown(
                    node,
                    binding,
                    roomTemperature,
                    math.max(accumulatedThermalDamage, roomTemperature));
            }
        }

        private void ApplyFloodedShortCircuitDamage()
        {
            int nodeCount = math.min(_topologyNodes.Count, _overloadThermalBindings.Count);
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                if (node == null)
                    continue;

                OverloadThermalBinding binding = _overloadThermalBindings[nodeIndex];
                if (binding.BaseModule == null || !binding.BaseModule.IsFlooded)
                    continue;

                if (!_logisticsGraph.TryGetNodePotential(nodeIndex, out float potential) ||
                    potential <= FloodedShortCircuitPotentialThreshold)
                {
                    continue;
                }

                node.SetShortCircuited(true);
                _logisticsGraph.TryConsumeNodePotential(nodeIndex, potential);
                PublishElectricShortCircuitDamageSignal(node, potential);
            }
        }

        private void PublishElectricShortCircuitDamageSignal(PowerNode node, float potential)
        {
            if (node == null)
                return;

            uint nodeId = unchecked((uint)EntityId.ToULong(node.GetEntityId()));
            Hecton8.Core.Contracts.Signals.CombatDamageSignal signal = new Hecton8.Core.Contracts.Signals.CombatDamageSignal
            {
                ImpactAup = double3.zero,
                Direction = float3.zero,
                Magnitude = math.max(0.01f, potential),
                DamageType = (uint)DamageTypeMask.Emp,
                TargetHash = nodeId,
                SourceHash = 0,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SourceId = 0,
                IntegrityDelta = 0,
                Channel = (byte)DamageChannel.Power,
                TargetId = nodeId > ushort.MaxValue ? ushort.MaxValue : (ushort)nodeId,
                Flags = Hecton8.Core.Contracts.Signals.CombatDamageSignal.DirectRuntimeFlag |
                        Hecton8.Core.Contracts.Signals.CombatDamageSignal.VisualOnlyFlag
            };
            SignalBus<CombatDamageSignal>.TryPushTracked(in signal, ref s_x001PowerGridSignalPushDropCount);
        }

        private void PublishNodeBrownoutSignal(uint nodeId, float supplyRatio, int priority)
        {
            BrownoutSignal signal = new BrownoutSignal
            {
                NetworkId = unchecked((uint)math.max(0, Id)),
                NodeId = nodeId,
                SupplyRatio = math.saturate(supplyRatio),
                Severity01 = math.saturate(1f - supplyRatio),
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Priority = (byte)math.clamp(priority, 0, byte.MaxValue),
                Flags = 1
            };
            SignalBus<BrownoutSignal>.TryPushTracked(in signal, ref s_x001PowerGridSignalPushDropCount);
        }

        private static void ApplySubmergedOverloadFluidHeating(in OverloadThermalBinding binding, float overloadHeatWatts)
        {
            if (binding.BaseModule == null ||
                binding.Atmosphere == null ||
                binding.FluidDynamics == null ||
                binding.RoomIndex < 0)
            {
                return;
            }

            float floodFillRatio = binding.Atmosphere.GetRoomFloodFillRatio(binding.RoomIndex);
            if (!binding.BaseModule.IsFlooded && floodFillRatio < MinimumSubmergedFloodFillRatio)
                return;

            float submergedHeatJoules = math.max(
                MinimumSubmergedOverloadHeatJoules,
                overloadHeatWatts * BatteryDispatchDeltaTimeSeconds * SubmergedOverloadHeatJoulesMultiplier);
            Vector3 runtimePoint = binding.BaseModule.transform.position;
            binding.FluidDynamics.InjectLocalizedWaterHeat(runtimePoint, submergedHeatJoules);

            float heatMegajoules = submergedHeatJoules / 1_000_000f;
            float hydrogenUnits = heatMegajoules * HydrogenPocketUnitsPerMegajoule;
            float oxygenUnits = heatMegajoules * OxygenPocketUnitsPerMegajoule;
            if (hydrogenUnits <= 0f && oxygenUnits <= 0f)
                return;

            binding.Atmosphere.InjectElectrolysisGasPocket(
                binding.RoomIndex,
                hydrogenUnits,
                oxygenUnits,
                0f);
        }

        private bool ScheduleCableThermalSharing()
        {
            if (_thermalDissipationPending)
                return true;

            int nodeCount = math.min(_topologyNodes.Count, _overloadThermalBindings.Count);
            if (nodeCount <= 1)
                return false;

            int directedEdgeCount = BuildThermalDissipationSnapshot(nodeCount);
            if (directedEdgeCount <= 0)
                return false;

            float qualityWeight = PowerGridManager.ResolveMathLodQualityWeight();
            int qualityIterationBudget = SubmarineOsThermalGridRuntime.ResolvePropagationIterations(qualityWeight);
            int iterationBudget = math.clamp(_cableThermalIterationBudget, 1, qualityIterationBudget);

            if (!TryLockThermalDissipationBuffers(out int thermalLockedMask))
                return false;

            try
            {
                NativeArray<float> inputTemperatures = ResolveVaultBuffer(in _thermalTemperatureFrontHandle);
                NativeArray<float> outputTemperatures = ResolveVaultBuffer(in _thermalTemperatureBackHandle);
                NativeArray<int> edgeOffsets = ResolveVaultBuffer(in _thermalEdgeOffsetsHandle);
                NativeArray<int> edgeDestinations = ResolveVaultBuffer(in _thermalEdgeDestinationsHandle);
                NativeArray<float> edgeConductance = ResolveVaultBuffer(in _thermalEdgeConductanceHandle);
                NativeArray<float> heatInjection = ResolveVaultBuffer(in _thermalHeatInjectionHandle);
                NativeArray<float> hullSinkConductance = ResolveVaultBuffer(in _thermalHullSinkConductanceHandle);
                if (!inputTemperatures.IsCreated ||
                    !outputTemperatures.IsCreated ||
                    !edgeOffsets.IsCreated ||
                    !edgeDestinations.IsCreated ||
                    !edgeConductance.IsCreated ||
                    !heatInjection.IsCreated ||
                    !hullSinkConductance.IsCreated ||
                    inputTemperatures.Length < nodeCount ||
                    outputTemperatures.Length < nodeCount ||
                    edgeOffsets.Length <= nodeCount ||
                    edgeDestinations.Length < directedEdgeCount ||
                    edgeConductance.Length < directedEdgeCount ||
                    heatInjection.Length < nodeCount ||
                    hullSinkConductance.Length < nodeCount)
                {
                    return false;
                }

                JobHandle thermalHandle = default;
                for (int iteration = 0; iteration < iterationBudget; iteration++)
                {
                    ThermalSharePassJob job = new ThermalSharePassJob
                    {
                        NodeCount = nodeCount,
                        OceanTemperatureCelsius = OceanThermalSinkTemperatureCelsius,
                        EdgeOffsets = edgeOffsets,
                        EdgeDestinations = edgeDestinations,
                        EdgeConductance = edgeConductance,
                        HeatInjection = heatInjection,
                        HullSinkConductance = hullSinkConductance,
                        TemperatureInput = inputTemperatures,
                        TemperatureOutput = outputTemperatures
                    };
                    thermalHandle = job.Schedule(nodeCount, 32, thermalHandle);
                    NativeArray<float> swap = inputTemperatures;
                    inputTemperatures = outputTemperatures;
                    outputTemperatures = swap;
                }

                _thermalDissipationHandle = thermalHandle;
                _thermalDissipationPending = true;
                _thermalDissipationNodeCount = nodeCount;
                _thermalDissipationScheduledIterations = iterationBudget;
                _thermalDissipationDeferredFrames = 0;
                _thermalDissipationResultInBackBuffer = (iterationBudget & 1) != 0;
                return true;
            }
            finally
            {
                UnlockThermalDissipationBuffers(thermalLockedMask);
            }
        }

        private bool TryCommitCableThermalSharing()
        {
            if (!_thermalDissipationPending)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _thermalDissipationHandle, forceComplete: false))
            {
                _thermalDissipationDeferredFrames++;
                if (_thermalDissipationDeferredFrames == 2)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        ThermalDissipationDeferredWarningHash,
                        ThermalDissipationContextHash,
                        _thermalDissipationDeferredFrames);
                }
                return false;
            }

            VaultGenerationHandle<float> resultHandle = _thermalDissipationResultInBackBuffer
                ? _thermalTemperatureBackHandle
                : _thermalTemperatureFrontHandle;
            if (!TryReadOnlyVaultBuffer(in resultHandle, out NativeArray<float>.ReadOnly resultTemperatures))
            {
                _thermalDissipationPending = false;
                _thermalDissipationResultInBackBuffer = false;
                _thermalDissipationNodeCount = 0;
                _thermalDissipationScheduledIterations = 0;
                _thermalDissipationDeferredFrames = 0;
                return true;
            }

            int nodeCount = math.min(_thermalDissipationNodeCount, math.min(_topologyNodes.Count, _overloadThermalBindings.Count));
            ApplyThermalDissipationResult(nodeCount, resultTemperatures);
            AdaptCableThermalIterationBudget(false, _thermalDissipationScheduledIterations, _thermalDissipationScheduledIterations);

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                if (node == null)
                    continue;

                OverloadThermalBinding binding = _overloadThermalBindings[nodeIndex];
                if (binding.BaseModule == null || binding.Atmosphere == null || binding.RoomIndex < 0)
                    continue;

                float roomTemperature = binding.Atmosphere.GetRoomTemperatureCelsius(binding.RoomIndex);
                TryTriggerThermalMeltdown(node, binding, roomTemperature, roomTemperature);
            }

            _thermalDissipationPending = false;
            _thermalDissipationResultInBackBuffer = false;
            _thermalDissipationNodeCount = 0;
            _thermalDissipationScheduledIterations = 0;
            _thermalDissipationDeferredFrames = 0;
            return true;
        }

        private void AdaptCableThermalIterationBudget(bool budgetExceeded, int completedIterations, int requestedIterations)
        {
            if (budgetExceeded)
            {
                _cableThermalIterationBudget = math.max(1, requestedIterations - 1);
                return;
            }

            if (completedIterations >= requestedIterations && requestedIterations < CableThermalSharePassCount)
                _cableThermalIterationBudget = requestedIterations + 1;
        }

        private int BuildThermalDissipationSnapshot(int nodeCount)
        {
            int directedEdgeCount = CountThermalDirectedEdges(nodeCount);
            if (!EnsureThermalDissipationCapacity(nodeCount, directedEdgeCount))
                return 0;

            if (!TryLockThermalDissipationBuffers(out int thermalLockedMask))
                return 0;

            try
            {
                NativeArray<float> thermalTemperatureFront = ResolveVaultBuffer(in _thermalTemperatureFrontHandle);
                NativeArray<float> thermalTemperatureBack = ResolveVaultBuffer(in _thermalTemperatureBackHandle);
                NativeArray<float> thermalHeatInjection = ResolveVaultBuffer(in _thermalHeatInjectionHandle);
                NativeArray<float> thermalHullSinkConductance = ResolveVaultBuffer(in _thermalHullSinkConductanceHandle);
                NativeArray<int> thermalEdgeOffsets = ResolveVaultBuffer(in _thermalEdgeOffsetsHandle);
                NativeArray<int> thermalEdgeDestinations = ResolveVaultBuffer(in _thermalEdgeDestinationsHandle);
                NativeArray<float> thermalEdgeConductance = ResolveVaultBuffer(in _thermalEdgeConductanceHandle);
                if (!thermalTemperatureFront.IsCreated ||
                    !thermalTemperatureBack.IsCreated ||
                    !thermalHeatInjection.IsCreated ||
                    !thermalHullSinkConductance.IsCreated ||
                    !thermalEdgeOffsets.IsCreated ||
                    !thermalEdgeDestinations.IsCreated ||
                    !thermalEdgeConductance.IsCreated ||
                    thermalTemperatureFront.Length < nodeCount ||
                    thermalTemperatureBack.Length < nodeCount ||
                    thermalHeatInjection.Length < nodeCount ||
                    thermalHullSinkConductance.Length < nodeCount ||
                    thermalEdgeOffsets.Length <= nodeCount ||
                    thermalEdgeDestinations.Length < directedEdgeCount ||
                    thermalEdgeConductance.Length < directedEdgeCount)
                {
                    return 0;
                }

                for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                    thermalEdgeOffsets[nodeIndex] = 0;

                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    PowerNode node = _topologyNodes[nodeIndex];
                    OverloadThermalBinding binding = _overloadThermalBindings[nodeIndex];
                    float roomTemperature = binding.Atmosphere != null && binding.RoomIndex >= 0
                        ? binding.Atmosphere.GetRoomTemperatureCelsius(binding.RoomIndex)
                        : OceanThermalSinkTemperatureCelsius;
                    thermalTemperatureFront[nodeIndex] = roomTemperature;
                    thermalTemperatureBack[nodeIndex] = roomTemperature;
                    thermalHeatInjection[nodeIndex] = ResolveNodeHeatInjection(node);
                    thermalHullSinkConductance[nodeIndex] = ResolveHullSinkConductance(in binding);

                    List<PowerNode> neighbors = node != null ? node.Neighbors : null;
                    int neighborCount = neighbors != null ? neighbors.Count : 0;
                    for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                    {
                        PowerNode neighbor = neighbors[neighborIndex];
                        if (!IsThermalNeighborValid(node, neighbor, nodeCount))
                            continue;

                        thermalEdgeOffsets[nodeIndex + 1] = thermalEdgeOffsets[nodeIndex + 1] + 1;
                    }
                }

                for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
                    thermalEdgeOffsets[nodeIndex] = thermalEdgeOffsets[nodeIndex] + thermalEdgeOffsets[nodeIndex - 1];

                int writeIndex = 0;
                for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
                {
                    PowerNode node = _topologyNodes[nodeIndex];
                    List<PowerNode> neighbors = node != null ? node.Neighbors : null;
                    int neighborCount = neighbors != null ? neighbors.Count : 0;
                    for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                    {
                        PowerNode neighbor = neighbors[neighborIndex];
                        if (!IsThermalNeighborValid(node, neighbor, nodeCount))
                            continue;

                        thermalEdgeDestinations[writeIndex] = neighbor.GraphScratchIndex;
                        thermalEdgeConductance[writeIndex] = CableThermalConductivityWattsPerCelsius /
                                                              math.max(MinEdgeResistance, ResolveEdgeResistance(node, neighbor));
                        writeIndex++;
                    }
                }

                return writeIndex;
            }
            finally
            {
                UnlockThermalDissipationBuffers(thermalLockedMask);
            }
        }

        private int CountThermalDirectedEdges(int nodeCount)
        {
            int directedEdgeCount = 0;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                List<PowerNode> neighbors = node != null ? node.Neighbors : null;
                int neighborCount = neighbors != null ? neighbors.Count : 0;
                for (int neighborIndex = 0; neighborIndex < neighborCount; neighborIndex++)
                {
                    if (IsThermalNeighborValid(node, neighbors[neighborIndex], nodeCount))
                        directedEdgeCount++;
                }
            }

            return directedEdgeCount;
        }

        private bool IsThermalNeighborValid(PowerNode source, PowerNode neighbor, int nodeCount)
        {
            return source != null &&
                   neighbor != null &&
                   ReferenceEquals(neighbor.Grid, this) &&
                   neighbor.GraphScratchVersion == _graphBuildVersion &&
                   neighbor.GraphScratchIndex >= 0 &&
                   neighbor.GraphScratchIndex < nodeCount &&
                   ShouldPublishPowerEdge(source, neighbor);
        }

        private float ResolveNodeHeatInjection(PowerNode node)
        {
            if (node == null || node.Components == null)
                return 0f;

            float heatWatts = 0f;
            List<IPowerComponent> components = node.Components;
            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                IPowerComponent component = components[componentIndex];
                if (component == null)
                    continue;

                float rating = component.PowerRating;
                if (rating < 0f && component.HasPower)
                    heatWatts += -rating;
                else if (rating > 0f && component is BioReactor)
                    heatWatts += rating * 0.5f;
            }

            return math.max(0f, heatWatts * ThermalHeatInjectionScale);
        }

        private float ResolveHullSinkConductance(in OverloadThermalBinding binding)
        {
            BaseModule baseModule = binding.BaseModule;
            if (baseModule == null)
                return 0f;

            float surfaceArea = math.max(1f, baseModule.ResolveThermalSurfaceAreaSquareMeters());
            float sink = surfaceArea * 0.05f;
            if (baseModule.IsFlooded || baseModule.IntegrityState == BaseModuleIntegrityState.Ruptured)
                sink *= 8f;
            return sink;
        }

        private void ApplyThermalDissipationResult(int nodeCount, NativeArray<float>.ReadOnly resultTemperatures)
        {
            if (resultTemperatures.Length <= 0)
                return;

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                OverloadThermalBinding binding = _overloadThermalBindings[nodeIndex];
                if (binding.Atmosphere == null || binding.RoomIndex < 0)
                    continue;

                float currentTemperature = binding.Atmosphere.GetRoomTemperatureCelsius(binding.RoomIndex);
                float targetTemperature = resultTemperatures[nodeIndex];
                float deltaCelsius = math.clamp(
                    (targetTemperature - currentTemperature) * ThermalDissipationApplyBlend,
                    -MaximumAppliedThermalDeltaCelsius,
                    MaximumAppliedThermalDeltaCelsius);
                if (math.abs(deltaCelsius) > 0.001f && math.isfinite(deltaCelsius))
                    binding.Atmosphere.InjectRoomTemperatureDeltaCelsius(binding.RoomIndex, deltaCelsius);
            }
        }

        private bool EnsureThermalDissipationCapacity(int nodeCount, int directedEdgeCount)
        {
            int safeNodeCount = math.max(1, nodeCount);
            int safeEdgeCount = math.max(1, directedEdgeCount);
            return
                EnsureVaultBuffer(
                    ref _thermalTemperatureFrontHandle,
                    ThermalTemperatureFrontBufferOffset,
                    safeNodeCount,
                    NativeArrayOptions.UninitializedMemory) &&
                EnsureVaultBuffer(
                    ref _thermalTemperatureBackHandle,
                    ThermalTemperatureBackBufferOffset,
                    safeNodeCount,
                    NativeArrayOptions.UninitializedMemory) &&
                EnsureVaultBuffer(
                    ref _thermalHeatInjectionHandle,
                    ThermalHeatInjectionBufferOffset,
                    safeNodeCount,
                    NativeArrayOptions.UninitializedMemory) &&
                EnsureVaultBuffer(
                    ref _thermalHullSinkConductanceHandle,
                    ThermalHullSinkConductanceBufferOffset,
                    safeNodeCount,
                    NativeArrayOptions.UninitializedMemory) &&
                EnsureVaultBuffer(
                    ref _thermalEdgeOffsetsHandle,
                    ThermalEdgeOffsetsBufferOffset,
                    safeNodeCount + 1,
                    NativeArrayOptions.UninitializedMemory) &&
                EnsureVaultBuffer(
                    ref _thermalEdgeDestinationsHandle,
                    ThermalEdgeDestinationsBufferOffset,
                    safeEdgeCount,
                    NativeArrayOptions.UninitializedMemory) &&
                EnsureVaultBuffer(
                    ref _thermalEdgeConductanceHandle,
                    ThermalEdgeConductanceBufferOffset,
                    safeEdgeCount,
                    NativeArrayOptions.UninitializedMemory);
        }

        private void CompleteThermalDissipationForTeardown()
        {
            if (!_thermalDissipationPending)
                return;

            DispatcherJobSwap.TryComplete(ref _thermalDissipationHandle, forceComplete: true);
            _thermalDissipationPending = false;
            _thermalDissipationResultInBackBuffer = false;
            _thermalDissipationNodeCount = 0;
            _thermalDissipationScheduledIterations = 0;
            _thermalDissipationDeferredFrames = 0;
        }

        private void DisposeThermalDissipationBuffers()
        {
            CompleteThermalDissipationForTeardown();
            ReleaseVaultBuffer(ref _thermalTemperatureFrontHandle);
            ReleaseVaultBuffer(ref _thermalTemperatureBackHandle);
            ReleaseVaultBuffer(ref _thermalHeatInjectionHandle);
            ReleaseVaultBuffer(ref _thermalHullSinkConductanceHandle);
            ReleaseVaultBuffer(ref _thermalEdgeOffsetsHandle);
            ReleaseVaultBuffer(ref _thermalEdgeDestinationsHandle);
            ReleaseVaultBuffer(ref _thermalEdgeConductanceHandle);
        }

        private void TryTriggerThermalMeltdown(
            PowerNode node,
            OverloadThermalBinding binding,
            float roomTemperature,
            float damageMagnitude)
        {
            if (node == null ||
                binding.BaseModule == null ||
                binding.Atmosphere == null ||
                binding.RoomIndex < 0 ||
                roomTemperature < OverloadMeltdownTemperatureCelsius ||
                node.IsRuptured ||
                node.IsShortCircuited)
            {
                return;
            }

            uint nodeId = unchecked((uint)EntityId.ToULong(node.GetEntityId()));
            float stress01 = math.saturate((math.max(damageMagnitude, roomTemperature) - OverloadMeltdownTemperatureCelsius) /
                                           math.max(1f, OverloadMeltdownTemperatureCelsius));
            node.SetShortCircuited(true);
            binding.BaseModule.SetAmbientPowerVisualState(true, math.saturate(1f - stress01));
            PublishNodeBrownoutSignal(nodeId, math.saturate(1f - stress01), 100);
            global::Hecton8.Core.Contracts.Signals.AudioEvent audioEvent =
                global::Hecton8.Core.Contracts.Signals.AudioEvent.FromStructuralStress(
                    binding.BaseModule.transform.position,
                    math.max(0.25f, stress01),
                    math.lerp(0.95f, 0.55f, stress01));
            global::Hecton8.Core.Contracts.Signals.SignalBus<global::Hecton8.Core.Contracts.Signals.AudioEvent>.TryPushTracked(in audioEvent, ref s_x001DirectSignalPushDropCount_PowerGrid);
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

        private static bool ShouldPublishPowerEdge(PowerNode sourceNode, PowerNode destinationNode)
        {
            return sourceNode != null &&
                   destinationNode != null &&
                   !sourceNode.IsRuptured &&
                   !destinationNode.IsRuptured &&
                   !sourceNode.IsShortCircuited &&
                   !destinationNode.IsShortCircuited;
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
            float distanceSq = delta.sqrMagnitude;
            float resistance = distanceSq > MinEdgeResistanceSquared && math.isfinite(distanceSq)
                ? distanceSq * math.rsqrt(distanceSq)
                : MinEdgeResistance;
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
            _hasPowerDeficit = distribution.HasDeficit != 0;
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

            if (!EnsureBatteryDispatchCapacity(batteryCount))
                return;

            if (!TryLockBatteryDispatchBuffers(out int batteryLockedMask))
                return;

            try
            {
                NativeArray<BatteryDispatchRecord> batteryDispatchRecords = ResolveVaultBuffer(in _batteryDispatchRecordsHandle);
                NativeArray<BatteryDispatchResult> batteryDispatchResults = ResolveVaultBuffer(in _batteryDispatchResultsHandle);
                if (!batteryDispatchRecords.IsCreated ||
                    !batteryDispatchResults.IsCreated ||
                    batteryDispatchRecords.Length < batteryCount ||
                    batteryDispatchResults.Length < batteryCount)
                {
                    return;
                }

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
                    batteryDispatchRecords[batteryIndex] = new BatteryDispatchRecord
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

                for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
                {
                    BatteryDispatchRecord dispatchRecord = batteryDispatchRecords[batteryIndex];
                    batteryDispatchResults[batteryIndex] = ResolveBatteryDispatchRecord(
                        in dispatchRecord,
                        dispatchMode,
                        requestedPowerWatts,
                        totalAvailablePowerWatts,
                        BatteryDispatchDeltaTimeSeconds);
                }

                _totalBatteryStoredEnergyWattSeconds = 0f;
                _totalBatteryCapacityWattSeconds = 0f;
                for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
                {
                    BatteryBankModule battery = _batteryRefs[batteryIndex];
                    if (battery == null)
                        continue;

                    BatteryDispatchResult result = batteryDispatchResults[batteryIndex];
                    battery.StageResolvedDispatch(result.NextStoredEnergyWattSeconds, result.PlannedGridPowerWatts);
                    _totalBatteryStoredEnergyWattSeconds += result.NextStoredEnergyWattSeconds;
                    _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
                }

                _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                    _totalBatteryStoredEnergyWattSeconds,
                    _totalBatteryCapacityWattSeconds);
            }
            finally
            {
                UnlockBatteryDispatchBuffers(batteryLockedMask);
            }
        }

        private static BatteryDispatchResult ResolveBatteryDispatchRecord(
            in BatteryDispatchRecord record,
            BatteryDispatchMode mode,
            float requestedPowerWatts,
            float totalAvailablePowerWatts,
            float deltaTimeSeconds)
        {
            float safeCapacity = math.max(1f, record.CapacityWattSeconds);
            BatteryDispatchResult result = new BatteryDispatchResult
            {
                NextStoredEnergyWattSeconds = math.clamp(record.StoredEnergyWattSeconds, 0f, safeCapacity),
                PlannedGridPowerWatts = 0f
            };

            if (mode == BatteryDispatchMode.Idle ||
                requestedPowerWatts <= 0.0001f ||
                totalAvailablePowerWatts <= 0.0001f)
            {
                return result;
            }

            float safeDeltaTime = math.max(0.001f, deltaTimeSeconds);
            float safeChargeEfficiency = math.max(0.1f, record.ChargeEfficiency);
            float safeDischargeEfficiency = math.max(0.1f, record.DischargeEfficiency);

            if (mode == BatteryDispatchMode.Charge)
            {
                float missingEnergy = math.max(0f, safeCapacity - record.StoredEnergyWattSeconds);
                float availablePower = math.max(
                    0f,
                    math.min(
                        math.max(0f, record.MaxChargePowerWatts),
                        missingEnergy / (safeDeltaTime * safeChargeEfficiency)));
                float allocatedPower = math.min(
                    availablePower,
                    requestedPowerWatts * (availablePower / math.max(0.0001f, totalAvailablePowerWatts)));
                result.NextStoredEnergyWattSeconds = math.clamp(
                    record.StoredEnergyWattSeconds + (allocatedPower * safeDeltaTime * safeChargeEfficiency),
                    0f,
                    safeCapacity);
                result.PlannedGridPowerWatts = -allocatedPower;
                return result;
            }

            float availableOutputPower = math.max(
                0f,
                math.min(
                    math.max(0f, record.MaxDischargePowerWatts),
                    (record.StoredEnergyWattSeconds * safeDischargeEfficiency) / safeDeltaTime));
            float deliveredPower = math.min(
                availableOutputPower,
                requestedPowerWatts * (availableOutputPower / math.max(0.0001f, totalAvailablePowerWatts)));
            float drainedEnergy = (deliveredPower * safeDeltaTime) / safeDischargeEfficiency;
            result.NextStoredEnergyWattSeconds = math.clamp(record.StoredEnergyWattSeconds - drainedEnergy, 0f, safeCapacity);
            result.PlannedGridPowerWatts = deliveredPower;
            return result;
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

        private bool EnsureBatteryDispatchCapacity(int batteryCount)
        {
            if (batteryCount <= 0)
                return false;

            return
                EnsureVaultBuffer(
                    ref _batteryDispatchRecordsHandle,
                    BatteryDispatchRecordsBufferOffset,
                    batteryCount,
                    NativeArrayOptions.ClearMemory) &&
                EnsureVaultBuffer(
                    ref _batteryDispatchResultsHandle,
                    BatteryDispatchResultsBufferOffset,
                    batteryCount,
                    NativeArrayOptions.ClearMemory);
        }

        private void DisposeBatteryDispatchBuffers()
        {
            ReleaseVaultBuffer(ref _batteryDispatchRecordsHandle);
            ReleaseVaultBuffer(ref _batteryDispatchResultsHandle);
        }

        private BufferID ResolveScratchBufferId(int localOffset)
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

        private bool TryReadOnlyVaultBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   handle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.Length > 0;
        }

        private bool EnsureVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            int localOffset,
            int requiredLength,
            NativeArrayOptions options)
            where T : struct
        {
            IDataVault vault = _dataVault;
            int safeLength = math.max(1, requiredLength);
            if (vault == null || vault.IsAllocationLocked)
            {
                handle = default;
                return false;
            }

            if (handle.BufferID != 0u &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= safeLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(
                ResolveScratchBufferId(localOffset),
                safeLength,
                SystemID.Power,
                options);

            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= safeLength;
        }

        private void ReleaseVaultBuffer<T>(ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool TryLockVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int bit,
            ref int lockedMask)
            where T : struct
        {
            if (handle.BufferID == 0u)
                return true;

            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            if (!vault.TryAcquireWriteLock(in handle, SystemID.Power, out NativeArray<T> _))
                return false;

            lockedMask |= 1 << bit;
            return true;
        }

        private static void UnlockVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int bit,
            int lockedMask)
            where T : struct
        {
            if ((lockedMask & (1 << bit)) != 0 && handle.BufferID != 0u)
                vault.ReleaseWriteLock(in handle, SystemID.Power);
        }

        private bool TryLockThermalDissipationBuffers(out int lockedMask)
        {
            lockedMask = 0;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool locked =
                TryLockVaultBuffer(vault, in _thermalTemperatureFrontHandle, ThermalTemperatureFrontBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _thermalTemperatureBackHandle, ThermalTemperatureBackBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _thermalHeatInjectionHandle, ThermalHeatInjectionBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _thermalHullSinkConductanceHandle, ThermalHullSinkConductanceBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _thermalEdgeOffsetsHandle, ThermalEdgeOffsetsBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _thermalEdgeDestinationsHandle, ThermalEdgeDestinationsBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _thermalEdgeConductanceHandle, ThermalEdgeConductanceBufferOffset, ref lockedMask);

            if (locked)
                return true;

            UnlockThermalDissipationBuffers(lockedMask);
            lockedMask = 0;
            return false;
        }

        private void UnlockThermalDissipationBuffers(int lockedMask)
        {
            IDataVault vault = _dataVault;
            if (vault == null || lockedMask == 0)
                return;

            UnlockVaultBuffer(vault, in _thermalEdgeConductanceHandle, ThermalEdgeConductanceBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _thermalEdgeDestinationsHandle, ThermalEdgeDestinationsBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _thermalEdgeOffsetsHandle, ThermalEdgeOffsetsBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _thermalHullSinkConductanceHandle, ThermalHullSinkConductanceBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _thermalHeatInjectionHandle, ThermalHeatInjectionBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _thermalTemperatureBackHandle, ThermalTemperatureBackBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _thermalTemperatureFrontHandle, ThermalTemperatureFrontBufferOffset, lockedMask);
        }

        private bool TryLockBatteryDispatchBuffers(out int lockedMask)
        {
            lockedMask = 0;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            bool locked =
                TryLockVaultBuffer(vault, in _batteryDispatchRecordsHandle, BatteryDispatchRecordsBufferOffset, ref lockedMask) &&
                TryLockVaultBuffer(vault, in _batteryDispatchResultsHandle, BatteryDispatchResultsBufferOffset, ref lockedMask);

            if (locked)
                return true;

            UnlockBatteryDispatchBuffers(lockedMask);
            lockedMask = 0;
            return false;
        }

        private void UnlockBatteryDispatchBuffers(int lockedMask)
        {
            IDataVault vault = _dataVault;
            if (vault == null || lockedMask == 0)
                return;

            UnlockVaultBuffer(vault, in _batteryDispatchResultsHandle, BatteryDispatchResultsBufferOffset, lockedMask);
            UnlockVaultBuffer(vault, in _batteryDispatchRecordsHandle, BatteryDispatchRecordsBufferOffset, lockedMask);
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
