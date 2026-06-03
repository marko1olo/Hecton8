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
        internal const float LogisticsTickDeltaTimeSeconds = 0.2f;
        private const float BatteryDispatchDeltaTimeSeconds = LogisticsTickDeltaTimeSeconds;
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
        private const int PowerGridScratchBufferBase = 2731700;
        private const int PowerGridScratchBufferLockLaneStride = 32;
        private const int ThermalTemperatureFrontBufferOffset = 2;
        private const int ThermalTemperatureBackBufferOffset = 3;
        private const int ThermalHeatInjectionBufferOffset = 4;
        private const int ThermalHullSinkConductanceBufferOffset = 5;
        private const int ThermalEdgeOffsetsBufferOffset = 6;
        private const int ThermalEdgeDestinationsBufferOffset = 7;
        private const int ThermalEdgeConductanceBufferOffset = 8;
        private const int PowerGridScratchBufferStride =
            (ThermalEdgeConductanceBufferOffset + 1) * PowerGridScratchBufferLockLaneStride + PowerGridScratchBufferLockLaneStride;
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

        private struct CachedOverloadServices
        {
            public ISubmarineAtmosphereRoomMutationSink Atmosphere;
            public SubmarineFluidDynamics FluidDynamics;
            public IDamageReceiver DamageReceiver;
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
        private readonly List<int> _consumerNodeIndices;
        private readonly List<BatteryBankModule> _batteryRefs;
        private readonly List<int> _batteryNodeIndices;
        private readonly List<int> _batteryChargeConsumerIndices;
        private readonly List<int> _batteryReserveComponentIds;
        private readonly List<float> _batteryReserveComponentStoredEnergyWattSeconds;
        private readonly List<float> _batteryReserveComponentCapacityWattSeconds;
        private readonly List<float> _batteryReserveComponentMinDischargeEfficiency;
        private readonly List<float> _batteryReserveComponentPowerLimitedGridEnergyWattSeconds;
        private readonly List<float> _batteryReserveComponentWirelessAvailableEnergyWattSeconds;
        private readonly List<float> _batteryReserveComponentReservedWirelessEnergyWattSeconds;
        private readonly List<byte> _batteryReserveComponentStates;
        private readonly List<PowerNode> _topologyNodes;
        private readonly List<OverloadThermalBinding> _overloadThermalBindings;
        private readonly Dictionary<PowerNode, float> _overloadThermalDamageByNode;
        private readonly Dictionary<BaseModule, CachedOverloadServices> _overloadServiceCache;
        private readonly LogisticsNetworkGraph _logisticsGraph;
        private readonly int _vaultBufferBase;
        private IDataVault _dataVault;
        private VaultGenerationHandle<float> _thermalTemperatureFrontHandle;
        private VaultGenerationHandle<float> _thermalTemperatureBackHandle;
        private VaultGenerationHandle<float> _thermalHeatInjectionHandle;
        private VaultGenerationHandle<float> _thermalHullSinkConductanceHandle;
        private VaultGenerationHandle<int> _thermalEdgeOffsetsHandle;
        private VaultGenerationHandle<int> _thermalEdgeDestinationsHandle;
        private VaultGenerationHandle<float> _thermalEdgeConductanceHandle;
        private readonly List<int> _thermalEdgeOffsetsScratch;
        private readonly List<int> _thermalEdgeDestinationsScratch;
        private readonly List<float> _thermalEdgeConductanceScratch;
        private readonly List<float> _thermalTemperatureFrontScratch;
        private readonly List<float> _thermalTemperatureBackScratch;
        private readonly List<float> _thermalHeatInjectionScratch;
        private readonly List<float> _thermalHullSinkConductanceScratch;
        private JobHandle _thermalDissipationHandle;
        private bool _thermalDissipationPending;
        private bool _thermalDissipationResultInBackBuffer;
        private int _thermalDissipationNodeCount;
        private int _thermalDissipationScheduledIterations;
        private int _thermalDissipationDeferredFrames;
        private ulong _thermalDissipationPinnedMask;
        private IDataVault _thermalDissipationPinnedVault;

        private float _totalGeneration;
        private float _totalConsumption;
        private float _balance;
        private float _supplyRatio = 1f;
        private float _totalBatteryStoredEnergyWattSeconds;
        private float _totalBatteryCapacityWattSeconds;
        private float _cachedWirelessToolAvailableEnergyWattSeconds;
        private float _reservedWirelessToolDrainWattSeconds;
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

        internal float WirelessToolAvailableEnergyWattSeconds => _cachedWirelessToolAvailableEnergyWattSeconds;

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

        internal float TryReserveWirelessToolDemand(float requestedEnergyWattSeconds)
        {
            if (requestedEnergyWattSeconds <= 0f || !_hasBatteryBanks)
                return 0f;

            if (_cachedWirelessToolAvailableEnergyWattSeconds <= 0.0001f ||
                _batteryReserveComponentIds.Count <= 0 ||
                _batteryReserveComponentReservedWirelessEnergyWattSeconds.Count < _batteryReserveComponentIds.Count)
            {
                return 0f;
            }

            float remaining = requestedEnergyWattSeconds;
            int componentCount = _batteryReserveComponentIds.Count;
            for (int cacheIndex = 0; cacheIndex < componentCount; cacheIndex++)
            {
                if (remaining <= 0.0001f)
                    break;

                if (cacheIndex >= _batteryReserveComponentWirelessAvailableEnergyWattSeconds.Count ||
                    cacheIndex >= _batteryReserveComponentStates.Count ||
                    _batteryReserveComponentStates[cacheIndex] != 0)
                {
                    continue;
                }

                float componentAvailableEnergy = _batteryReserveComponentWirelessAvailableEnergyWattSeconds[cacheIndex];
                if (componentAvailableEnergy <= 0.0001f)
                    continue;

                float reservedEnergy = math.min(remaining, componentAvailableEnergy);
                if (reservedEnergy <= 0.0001f)
                    continue;

                remaining -= reservedEnergy;
                _batteryReserveComponentWirelessAvailableEnergyWattSeconds[cacheIndex] =
                    math.max(0f, componentAvailableEnergy - reservedEnergy);
                _batteryReserveComponentReservedWirelessEnergyWattSeconds[cacheIndex] += reservedEnergy;
            }

            float reservedTotal = math.max(0f, requestedEnergyWattSeconds - remaining);
            _cachedWirelessToolAvailableEnergyWattSeconds = math.max(0f, _cachedWirelessToolAvailableEnergyWattSeconds - reservedTotal);
            _reservedWirelessToolDrainWattSeconds += reservedTotal;
            return reservedTotal;
        }

        internal float ConsumeReservedWirelessToolDemand()
        {
            if (_reservedWirelessToolDrainWattSeconds <= 0.0001f)
                return 0f;

            if (!_hasBatteryBanks)
            {
                _reservedWirelessToolDrainWattSeconds = 0f;
                int reservedComponentCount = _batteryReserveComponentReservedWirelessEnergyWattSeconds.Count;
                for (int cacheIndex = 0; cacheIndex < reservedComponentCount; cacheIndex++)
                    _batteryReserveComponentReservedWirelessEnergyWattSeconds[cacheIndex] = 0f;
                return 0f;
            }

            float remaining = _reservedWirelessToolDrainWattSeconds;
            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                if (remaining <= 0.0001f)
                    break;

                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                int componentIndex = ResolveBatteryComponentIndex(batteryIndex);
                int cacheIndex = FindBatteryReserveComponentCacheIndex(componentIndex);
                if (cacheIndex < 0 ||
                    cacheIndex >= _batteryReserveComponentReservedWirelessEnergyWattSeconds.Count)
                {
                    continue;
                }

                float componentReservedEnergy = _batteryReserveComponentReservedWirelessEnergyWattSeconds[cacheIndex];
                if (componentReservedEnergy <= 0.0001f)
                    continue;

                float requestedFromBattery = math.min(remaining, componentReservedEnergy);
                float batteryConsumedEnergy = battery.TryConsumeDirectGridEnergy(
                    requestedFromBattery,
                    BatteryEmergencyReserveThreshold);
                if (batteryConsumedEnergy <= 0.0001f)
                    continue;

                remaining -= batteryConsumedEnergy;
                _batteryReserveComponentReservedWirelessEnergyWattSeconds[cacheIndex] =
                    math.max(0f, componentReservedEnergy - batteryConsumedEnergy);
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
            float consumedEnergy = math.max(0f, _reservedWirelessToolDrainWattSeconds - remaining);
            _reservedWirelessToolDrainWattSeconds = 0f;
            int componentCount = _batteryReserveComponentReservedWirelessEnergyWattSeconds.Count;
            for (int cacheIndex = 0; cacheIndex < componentCount; cacheIndex++)
                _batteryReserveComponentReservedWirelessEnergyWattSeconds[cacheIndex] = 0f;

            return consumedEnergy;
        }

        public PowerGrid(int initialCapacity = 16, IDataVault dataVault = null)
        {
            int safeCapacity = math.max(1, initialCapacity);

            Id = _nextId++;
            _vaultBufferBase =
                PowerGridScratchBufferBase +
                Id * PowerGridScratchBufferStride +
                (Id & 31);
            _dataVault = dataVault;
            // COLD ALLOC: HashSet<PowerNode>[initialCapacity] — grid membership cache — owner: PowerGrid
            _nodes = new HashSet<PowerNode>(safeCapacity);
            // COLD ALLOC: List<IPowerComponent>[initialCapacity] — consumer reference cache — owner: PowerGrid
            _consumerRefs = new List<IPowerComponent>(safeCapacity);
            // COLD ALLOC: List<uint>[initialCapacity] - stable node ids parallel to consumer references - owner: PowerGrid
            _consumerNodeIds = new List<uint>(safeCapacity);
            // COLD ALLOC: List<int>[initialCapacity] - graph node indices parallel to consumer references - owner: PowerGrid
            _consumerNodeIndices = new List<int>(safeCapacity);
            // COLD ALLOC: List<BatteryBankModule>[initialCapacity] - storage banks in the current graph snapshot - owner: PowerGrid
            _batteryRefs = new List<BatteryBankModule>(safeCapacity);
            // COLD ALLOC: List<int>[initialCapacity] - graph node indices parallel to battery references - owner: PowerGrid
            _batteryNodeIndices = new List<int>(safeCapacity);
            _batteryChargeConsumerIndices = new List<int>(safeCapacity);
            _batteryReserveComponentIds = new List<int>(safeCapacity);
            _batteryReserveComponentStoredEnergyWattSeconds = new List<float>(safeCapacity);
            _batteryReserveComponentCapacityWattSeconds = new List<float>(safeCapacity);
            _batteryReserveComponentMinDischargeEfficiency = new List<float>(safeCapacity);
            _batteryReserveComponentPowerLimitedGridEnergyWattSeconds = new List<float>(safeCapacity);
            _batteryReserveComponentWirelessAvailableEnergyWattSeconds = new List<float>(safeCapacity);
            _batteryReserveComponentReservedWirelessEnergyWattSeconds = new List<float>(safeCapacity);
            _batteryReserveComponentStates = new List<byte>(safeCapacity);
            // COLD ALLOC: List<PowerNode>[initialCapacity] — topology node snapshot — owner: PowerGrid
            _topologyNodes = new List<PowerNode>(safeCapacity);
            // COLD ALLOC: List<OverloadThermalBinding>[initialCapacity] — per-node overload heat ownership cache — owner: PowerGrid
            _overloadThermalBindings = new List<OverloadThermalBinding>(safeCapacity);
            // COLD ALLOC: Dictionary<PowerNode,float>[initialCapacity] — persistent overload damage accumulation keyed by live nodes — owner: PowerGrid
            _overloadThermalDamageByNode = new Dictionary<PowerNode, float>(safeCapacity);
            _overloadServiceCache = new Dictionary<BaseModule, CachedOverloadServices>(safeCapacity);
            _logisticsGraph = new LogisticsNetworkGraph(safeCapacity, safeCapacity * 4, safeCapacity * 2, dataVault);
            int edgeCapacity = math.max(1, safeCapacity * 4);
            _thermalEdgeOffsetsScratch = new List<int>(safeCapacity + 1);
            _thermalEdgeDestinationsScratch = new List<int>(edgeCapacity);
            _thermalEdgeConductanceScratch = new List<float>(edgeCapacity);
            _thermalTemperatureFrontScratch = new List<float>(safeCapacity);
            _thermalTemperatureBackScratch = new List<float>(safeCapacity);
            _thermalHeatInjectionScratch = new List<float>(safeCapacity);
            _thermalHullSinkConductanceScratch = new List<float>(safeCapacity);
        }

        public void InjectDataVault(IDataVault dataVault)
        {
            ConsumeReservedWirelessToolDemand();

            bool samePowerVault = ReferenceEquals(_dataVault, dataVault);
            bool graphRebound = _logisticsGraph.InjectDataVault(dataVault);
            if (samePowerVault && !graphRebound)
                return;

            CompleteThermalDissipationForTeardown();
            DisposeThermalDissipationBuffers();
            _dataVault = dataVault;
            _isDirty = true;
            _hasEvaluatedAtLeastOnce = false;
            _slowTickEvaluationPending = false;
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.Idle;
        }

        public void Dispose()
        {
            CancelPendingSlowTickWorkForTeardown();
            _logisticsGraph.Dispose();
            DisposeThermalDissipationBuffers();
        }

        internal void CancelPendingSlowTickWorkForTeardown()
        {
            ConsumeReservedWirelessToolDemand();
            CompleteThermalDissipationForTeardown();
            _logisticsGraph.CancelPendingWorkForTeardown();
            ResetBatteryDispatchPlans();
            _slowTickEvaluationPending = false;
            _slowTickNodeStatePublishScheduled = false;
            _slowTickEvaluationPhase = SlowTickEvaluationPhase.Idle;
            _isDirty = true;
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
            EnsureGraphSnapshotCapacityForCurrentTopologyCold();
            WarmOverloadServiceCacheForNode(node);
            _isDirty = true;
        }

        /// <summary>Removes a node from this grid and clears the owner reference.</summary>
        public void RemoveNode(PowerNode node)
        {
            if (node == null)
                return;

            ConsumeReservedWirelessToolDemand();
            _nodes.Remove(node);
            RemoveOverloadServiceCacheForNode(node);
            if (ReferenceEquals(node.Grid, this))
                node.SetGrid(null);

            _isDirty = true;
        }

        /// <summary>Absorbs all nodes from another grid.</summary>
        public void AbsorbAll(PowerGrid other)
        {
            if (other == null || ReferenceEquals(other, this))
                return;

            ConsumeReservedWirelessToolDemand();
            other.ConsumeReservedWirelessToolDemand();

            HashSet<PowerNode>.Enumerator enumerator = other._nodes.GetEnumerator();
            while (enumerator.MoveNext())
            {
                PowerNode node = enumerator.Current;
                if (node == null)
                    continue;

                _nodes.Add(node);
                node.SetGrid(this);
                TransferOrWarmOverloadServiceCacheForNode(node, other);
            }

            other._nodes.Clear();
            other._overloadServiceCache.Clear();
            EnsureGraphSnapshotCapacityForCurrentTopologyCold();
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

            ConsumeReservedWirelessToolDemand();

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
                _cachedWirelessToolAvailableEnergyWattSeconds = 0f;
                _reservedWirelessToolDrainWattSeconds = 0f;
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
                    if (!_logisticsGraph.HasPendingNodeStatePublish)
                        return false;

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
            ConsumeReservedWirelessToolDemand();
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
                _cachedWirelessToolAvailableEnergyWattSeconds = 0f;
                _reservedWirelessToolDrainWattSeconds = 0f;
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

        internal int LogisticsNodeCount => _logisticsGraph.NodeCount;

        internal int LogisticsEdgeCount => _logisticsGraph.EdgeCount;

        internal NativeArray<int>.ReadOnly GetLogisticsEdgeOffsetsReadOnly()
        {
            return _logisticsGraph.GetEdgeOffsetsReadOnly();
        }

        internal NativeArray<int>.ReadOnly GetLogisticsEdgeDestinationsReadOnly()
        {
            return _logisticsGraph.GetEdgeDestinationsReadOnly();
        }

        internal bool TryResolveLogisticsNodeIndex(PowerNode node, out int nodeIndex)
        {
            nodeIndex = -1;
            if (node == null || !ReferenceEquals(node.Grid, this))
                return false;

            int scratchIndex = node.GraphScratchIndex;
            if (node.GraphScratchVersion != _graphBuildVersion ||
                scratchIndex < 0 ||
                scratchIndex >= _logisticsGraph.NodeCount)
            {
                return false;
            }

            nodeIndex = scratchIndex;
            return true;
        }

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

            int rawNodeCount = _nodes.Count;
            EnsureGraphSnapshotNodeCapacity(rawNodeCount);
            _consumerRefs.Clear();
            _consumerNodeIds.Clear();
            _consumerNodeIndices.Clear();
            _batteryRefs.Clear();
            _batteryNodeIndices.Clear();
            _batteryChargeConsumerIndices.Clear();
            _batteryReserveComponentIds.Clear();
            _batteryReserveComponentStoredEnergyWattSeconds.Clear();
            _batteryReserveComponentCapacityWattSeconds.Clear();
            _batteryReserveComponentMinDischargeEfficiency.Clear();
            _batteryReserveComponentPowerLimitedGridEnergyWattSeconds.Clear();
            _batteryReserveComponentWirelessAvailableEnergyWattSeconds.Clear();
            _batteryReserveComponentReservedWirelessEnergyWattSeconds.Clear();
            _batteryReserveComponentStates.Clear();
            _topologyNodes.Clear();
            _overloadThermalBindings.Clear();

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
            int batteryCount = 0;

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
                    if (component == null)
                        continue;

                    if (component is BatteryBankModule)
                        batteryCount++;

                    if (component.PowerRating >= 0f)
                        continue;

                    consumerCount++;
                }

                if (node.IsRuptured)
                    consumerCount++;
            }

            EnsureGraphSnapshotCapacity(nodeCount, consumerCount, batteryCount);
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

                        int batteryIndex = -1;
                        if (component is BatteryBankModule batteryBank)
                        {
                            batteryIndex = _batteryRefs.Count;
                            _batteryRefs.Add(batteryBank);
                            _batteryNodeIndices.Add(nodeIndex);
                            _batteryChargeConsumerIndices.Add(-1);
                        }

                        if (!overloadBindingResolved && component is BaseModule baseModule)
                        {
                            CachedOverloadServices services = ReadCachedOverloadServices(baseModule);
                            ISubmarineAtmosphereRoomMutationSink atmosphere = services.Atmosphere;
                            if (atmosphere != null && !atmosphere.IsAtmosphereRuntimeActive)
                                atmosphere = null;
                            overloadBinding = new OverloadThermalBinding
                            {
                                BaseModule = baseModule,
                                Atmosphere = atmosphere,
                                FluidDynamics = services.FluidDynamics,
                                DamageReceiver = services.DamageReceiver,
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

                        int consumerIndex = _consumerRefs.Count;
                        if (batteryIndex >= 0 && batteryIndex < _batteryChargeConsumerIndices.Count)
                            _batteryChargeConsumerIndices[batteryIndex] = consumerIndex;

                        _consumerRefs.Add(component);
                        _consumerNodeIds.Add(nodeId);
                        _consumerNodeIndices.Add(nodeIndex);
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
                        _consumerNodeIndices.Add(nodeIndex);
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

            EnsureBatteryReserveComponentCacheCapacity(_batteryRefs.Count);
            _logisticsGraph.FinalizeBuild();
            return true;
        }

        private void EnsureGraphSnapshotCapacity(int nodeCapacity, int consumerCapacity, int batteryCapacity)
        {
            EnsureGraphSnapshotNodeCapacity(nodeCapacity);
            EnsureListCapacity(_consumerRefs, consumerCapacity);
            EnsureListCapacity(_consumerNodeIds, consumerCapacity);
            EnsureListCapacity(_consumerNodeIndices, consumerCapacity);
            EnsureListCapacity(_batteryRefs, batteryCapacity);
            EnsureListCapacity(_batteryNodeIndices, batteryCapacity);
            EnsureListCapacity(_batteryChargeConsumerIndices, batteryCapacity);
            EnsureBatteryReserveComponentCacheCapacity(batteryCapacity);
        }

        private void EnsureGraphSnapshotCapacityForCurrentTopologyCold()
        {
            int nodeCapacity = _nodes.Count;
            int consumerCapacity = 0;
            int batteryCapacity = 0;

            HashSet<PowerNode>.Enumerator nodeEnumerator = _nodes.GetEnumerator();
            while (nodeEnumerator.MoveNext())
            {
                PowerNode node = nodeEnumerator.Current;
                if (node == null)
                    continue;

                List<IPowerComponent> components = node.Components;
                if (components != null)
                {
                    int componentCount = components.Count;
                    for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    {
                        IPowerComponent component = components[componentIndex];
                        if (component == null)
                            continue;

                        if (component is BatteryBankModule)
                            batteryCapacity++;
                        if (component.PowerRating < 0f)
                            consumerCapacity++;
                    }
                }

                consumerCapacity++;
            }

            EnsureGraphSnapshotCapacity(nodeCapacity, consumerCapacity, batteryCapacity);
        }

        private void EnsureGraphSnapshotNodeCapacity(int nodeCapacity)
        {
            int safeNodeCapacity = math.max(0, nodeCapacity);
            EnsureListCapacity(_topologyNodes, safeNodeCapacity);
            EnsureListCapacity(_overloadThermalBindings, safeNodeCapacity);
        }

        private static void EnsureListCapacity<T>(List<T> list, int requiredCapacity)
        {
            if (list != null && list.Capacity < requiredCapacity)
                list.Capacity = requiredCapacity;
        }

        private CachedOverloadServices ReadCachedOverloadServices(BaseModule baseModule)
        {
            if (baseModule == null)
                return default;

            if (_overloadServiceCache.TryGetValue(baseModule, out CachedOverloadServices services))
                return services;

            return default;
        }

        private void WarmOverloadServiceCacheForNode(PowerNode node)
        {
            if (node == null || node.Components == null)
                return;

            List<IPowerComponent> components = node.Components;
            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                if (components[componentIndex] is BaseModule baseModule)
                    WarmOverloadServiceCache(baseModule);
            }
        }

        private void TransferOrWarmOverloadServiceCacheForNode(PowerNode node, PowerGrid sourceGrid)
        {
            if (node == null || node.Components == null)
                return;

            List<IPowerComponent> components = node.Components;
            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                if (components[componentIndex] is not BaseModule baseModule)
                    continue;

                if (sourceGrid != null &&
                    sourceGrid._overloadServiceCache.TryGetValue(baseModule, out CachedOverloadServices services))
                {
                    _overloadServiceCache[baseModule] = services;
                    continue;
                }

                WarmOverloadServiceCache(baseModule);
            }
        }

        private void WarmOverloadServiceCache(BaseModule baseModule)
        {
            if (baseModule == null || _overloadServiceCache.ContainsKey(baseModule))
                return;

            baseModule.TryGetComponent(out IDamageReceiver damageReceiver);
            CachedOverloadServices services = new CachedOverloadServices
            {
                Atmosphere = ComponentReferenceUtility.ResolveParentService<ISubmarineAtmosphereRoomMutationSink>(baseModule),
                FluidDynamics = ComponentReferenceUtility.ResolveParentService<SubmarineFluidDynamics>(baseModule),
                DamageReceiver = damageReceiver
            };
            _overloadServiceCache[baseModule] = services;
        }

        private void RemoveOverloadServiceCacheForNode(PowerNode node)
        {
            if (node == null || node.Components == null)
                return;

            List<IPowerComponent> components = node.Components;
            int componentCount = components.Count;
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                if (components[componentIndex] is BaseModule baseModule)
                    _overloadServiceCache.Remove(baseModule);
            }
        }

        private void ApplyConsumerStates()
        {
            RebuildBatteryReserveComponentCache();

            int consumerCount = _consumerRefs.Count;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                IPowerComponent consumer = _consumerRefs[consumerIndex];
                if (consumer == null)
                    continue;

                int componentIndex = ResolveConsumerComponentIndex(consumerIndex);
                bool componentReserveActive = TryGetCachedBatteryEmergencyReserveActive(
                    componentIndex,
                    out bool cachedReserveActive) && cachedReserveActive;
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
                        componentReserveActive || consumerVoltageSupplyRatio < 0.80f,
                        consumerVoltageSupplyRatio);

                bool shouldHavePower = _logisticsGraph.IsConsumerPowered(consumerIndex);
                if (voltageBrownout && baseModule == null && continuousPower == null)
                    shouldHavePower = false;
                if (componentReserveActive && !ShouldRemainPoweredDuringBatteryReserve(consumer))
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

            if (!TryPinThermalDissipationJobBuffers(out IDataVault pinnedVault, out ulong pinnedMask))
                return false;

            bool scheduled = false;
            try
            {
                float qualityWeight = PowerGridManager.ResolveMathLodQualityWeight();
                int qualityIterationBudget = SubmarineOsThermalGridRuntime.ResolvePropagationIterations(qualityWeight);
                int iterationBudget = math.clamp(_cableThermalIterationBudget, 1, qualityIterationBudget);

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
                _thermalDissipationPinnedVault = pinnedVault;
                _thermalDissipationPinnedMask = pinnedMask;
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseThermalDissipationJobPins(pinnedVault, pinnedMask);
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

            try
            {
                VaultGenerationHandle<float> resultHandle = _thermalDissipationResultInBackBuffer
                    ? _thermalTemperatureBackHandle
                    : _thermalTemperatureFrontHandle;
                if (!TryReadOnlyVaultBuffer(in resultHandle, out NativeArray<float>.ReadOnly resultTemperatures))
                    return true;

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
            }
            finally
            {
                _thermalDissipationPending = false;
                _thermalDissipationResultInBackBuffer = false;
                _thermalDissipationNodeCount = 0;
                _thermalDissipationScheduledIterations = 0;
                _thermalDissipationDeferredFrames = 0;
                ReleaseThermalDissipationJobPins();
            }
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

            List<int> edgeOffsets = _thermalEdgeOffsetsScratch;
            List<int> edgeDestinations = _thermalEdgeDestinationsScratch;
            List<float> edgeConductance = _thermalEdgeConductanceScratch;
            List<float> temperatureFront = _thermalTemperatureFrontScratch;
            List<float> temperatureBack = _thermalTemperatureBackScratch;
            List<float> heatInjection = _thermalHeatInjectionScratch;
            List<float> hullSinkConductance = _thermalHullSinkConductanceScratch;

            ResetScratchList(edgeOffsets, nodeCount + 1, 0);
            ResetScratchList(edgeDestinations, directedEdgeCount, 0);
            ResetScratchList(edgeConductance, directedEdgeCount, 0f);
            ResetScratchList(temperatureFront, nodeCount, 0f);
            ResetScratchList(temperatureBack, nodeCount, 0f);
            ResetScratchList(heatInjection, nodeCount, 0f);
            ResetScratchList(hullSinkConductance, nodeCount, 0f);

            for (int nodeIndex = 0; nodeIndex <= nodeCount; nodeIndex++)
                edgeOffsets[nodeIndex] = 0;

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

                    edgeOffsets[nodeIndex + 1] = edgeOffsets[nodeIndex + 1] + 1;
                }
            }

            for (int nodeIndex = 1; nodeIndex <= nodeCount; nodeIndex++)
                edgeOffsets[nodeIndex] = edgeOffsets[nodeIndex] + edgeOffsets[nodeIndex - 1];

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                PowerNode node = _topologyNodes[nodeIndex];
                OverloadThermalBinding binding = _overloadThermalBindings[nodeIndex];
                float sourceTemperature = binding.Atmosphere != null && binding.RoomIndex >= 0
                    ? binding.Atmosphere.GetRoomTemperatureCelsius(binding.RoomIndex)
                    : OceanThermalSinkTemperatureCelsius;
                temperatureFront[nodeIndex] = sourceTemperature;
                temperatureBack[nodeIndex] = sourceTemperature;
                heatInjection[nodeIndex] = ResolveNodeHeatInjection(node);
                hullSinkConductance[nodeIndex] = ResolveHullSinkConductance(in binding);
            }

            int conductanceWriteIndex = 0;
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

                    edgeDestinations[conductanceWriteIndex] = neighbor.GraphScratchIndex;
                    edgeConductance[conductanceWriteIndex] =
                        CableThermalConductivityWattsPerCelsius /
                        math.max(MinEdgeResistance, ResolveEdgeResistance(node, neighbor));
                    conductanceWriteIndex++;
                }
            }

            if (conductanceWriteIndex != directedEdgeCount)
                return 0;

            return CopyScratchToWriteBuffer(in _thermalEdgeOffsetsHandle, edgeOffsets, nodeCount + 1) &&
                   CopyScratchToWriteBuffer(in _thermalTemperatureFrontHandle, temperatureFront, nodeCount) &&
                   CopyScratchToWriteBuffer(in _thermalTemperatureBackHandle, temperatureBack, nodeCount) &&
                   CopyScratchToWriteBuffer(in _thermalHeatInjectionHandle, heatInjection, nodeCount) &&
                   CopyScratchToWriteBuffer(in _thermalHullSinkConductanceHandle, hullSinkConductance, nodeCount) &&
                   CopyScratchToWriteBuffer(in _thermalEdgeDestinationsHandle, edgeDestinations, directedEdgeCount) &&
                   CopyScratchToWriteBuffer(in _thermalEdgeConductanceHandle, edgeConductance, directedEdgeCount)
                ? directedEdgeCount
                : 0;
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
            bool buffersReady =
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
            if (!buffersReady)
                return false;

            EnsureThermalDissipationScratchCapacity(safeNodeCount, safeEdgeCount);
            return true;
        }

        private void EnsureThermalDissipationScratchCapacity(int nodeCount, int directedEdgeCount)
        {
            int nodeArrayLength = math.max(1, nodeCount);
            int offsetArrayLength = nodeArrayLength + 1;
            int edgeArrayLength = math.max(1, directedEdgeCount);

            // COLD/GROWTH MANAGED CAPACITY: PowerGrid already owns List caches; vault keeps all persistent native job buffers.
            EnsureListCapacity(_thermalEdgeOffsetsScratch, offsetArrayLength);
            EnsureListCapacity(_thermalTemperatureFrontScratch, nodeArrayLength);
            EnsureListCapacity(_thermalTemperatureBackScratch, nodeArrayLength);
            EnsureListCapacity(_thermalHeatInjectionScratch, nodeArrayLength);
            EnsureListCapacity(_thermalHullSinkConductanceScratch, nodeArrayLength);
            EnsureListCapacity(_thermalEdgeDestinationsScratch, edgeArrayLength);
            EnsureListCapacity(_thermalEdgeConductanceScratch, edgeArrayLength);
        }

        private static void ResetScratchList<T>(List<T> list, int count, T value)
        {
            list.Clear();
            for (int index = 0; index < count; index++)
                list.Add(value);
        }

        private static void ClearScratchList<T>(List<T> list)
        {
            list.Clear();
        }

        private bool CopyScratchToWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            List<T> source,
            int count)
            where T : struct
        {
            if (source == null || source.Count < count)
                return false;

            if (!TryAcquireWriteBuffer(in handle, out NativeArray<T> destination))
                return false;

            try
            {
                if (!destination.IsCreated || destination.Length < count)
                    return false;

                for (int index = 0; index < count; index++)
                    destination[index] = source[index];

                return true;
            }
            finally
            {
                ReleaseWriteBuffer(in handle);
            }
        }

        private void CompleteThermalDissipationForTeardown()
        {
            if (!_thermalDissipationPending)
            {
                ReleaseThermalDissipationJobPins();
                return;
            }

            try
            {
                DispatcherJobSwap.TryComplete(ref _thermalDissipationHandle, forceComplete: true);
            }
            finally
            {
                _thermalDissipationPending = false;
                _thermalDissipationResultInBackBuffer = false;
                _thermalDissipationNodeCount = 0;
                _thermalDissipationScheduledIterations = 0;
                _thermalDissipationDeferredFrames = 0;
                ReleaseThermalDissipationJobPins();
            }
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
            ClearScratchList(_thermalEdgeOffsetsScratch);
            ClearScratchList(_thermalTemperatureFrontScratch);
            ClearScratchList(_thermalTemperatureBackScratch);
            ClearScratchList(_thermalHeatInjectionScratch);
            ClearScratchList(_thermalHullSinkConductanceScratch);
            ClearScratchList(_thermalEdgeDestinationsScratch);
            ClearScratchList(_thermalEdgeConductanceScratch);
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
                   sourceNode.IsRuntimePowerConductive &&
                   destinationNode.IsRuntimePowerConductive &&
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
            _brownoutTier = distribution.BrownoutTier;
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
            int batteryCount = _batteryRefs.Count;
            _hasBatteryBanks = batteryCount > 0;
            _totalBatteryStoredEnergyWattSeconds = 0f;
            _totalBatteryCapacityWattSeconds = 0f;
            _batteryEmergencyReserveActive = false;

            if (batteryCount <= 0)
                return;

            float totalChargeAcceptanceWatts = 0f;
            float totalDischargeAvailabilityWatts = 0f;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                float chargeAcceptanceWatts = battery.ResolveChargeAcceptanceWatts(BatteryDispatchDeltaTimeSeconds);
                float dischargeAvailabilityWatts = battery.ResolveDischargeAvailabilityWatts(BatteryDispatchDeltaTimeSeconds);

                _totalBatteryStoredEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
                totalChargeAcceptanceWatts += chargeAcceptanceWatts;
                totalDischargeAvailabilityWatts += dischargeAvailabilityWatts;
            }

            if (!math.isfinite(rawDistribution.Balance) ||
                (totalChargeAcceptanceWatts <= 0.0001f && totalDischargeAvailabilityWatts <= 0.0001f))
            {
                _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                    _totalBatteryStoredEnergyWattSeconds,
                    _totalBatteryCapacityWattSeconds);
                return;
            }

            _totalBatteryStoredEnergyWattSeconds = 0f;
            _totalBatteryCapacityWattSeconds = 0f;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                int componentIndex = ResolveBatteryComponentIndex(batteryIndex);
                if (!TryResolveComponentBatteryDispatch(
                        componentIndex,
                        out BatteryDispatchMode dispatchMode,
                        out float requestedPowerWatts,
                        out float totalAvailablePowerWatts,
                        out bool reserveAlreadyActive))
                {
                    _totalBatteryStoredEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                    _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
                    continue;
                }

                float chargeAcceptanceWatts = battery.ResolveChargeAcceptanceWatts(BatteryDispatchDeltaTimeSeconds);
                float dischargeAvailabilityWatts = battery.ResolveDischargeAvailabilityWatts(BatteryDispatchDeltaTimeSeconds);
                float reservePreservingDischargeWatts = battery.ResolveDischargeAvailabilityWatts(
                    BatteryDispatchDeltaTimeSeconds,
                    BatteryEmergencyReserveThreshold);
                float dispatchDischargeWatts = dispatchMode == BatteryDispatchMode.Discharge && !reserveAlreadyActive
                    ? reservePreservingDischargeWatts
                    : dischargeAvailabilityWatts;

                BatteryDispatchRecord dispatchRecord;
                dispatchRecord.StoredEnergyWattSeconds = battery.StoredEnergyWattSeconds;
                dispatchRecord.CapacityWattSeconds = battery.CapacityWattSeconds;
                dispatchRecord.MaxChargePowerWatts = chargeAcceptanceWatts;
                dispatchRecord.MaxDischargePowerWatts = dispatchDischargeWatts;
                dispatchRecord.ChargeEfficiency = battery.ChargeEfficiency;
                dispatchRecord.DischargeEfficiency = battery.DischargeEfficiency;

                BatteryDispatchResult result = ResolveBatteryDispatchRecord(
                    in dispatchRecord,
                    dispatchMode,
                    requestedPowerWatts,
                    totalAvailablePowerWatts,
                    BatteryDispatchDeltaTimeSeconds);
                battery.StageResolvedDispatch(result.NextStoredEnergyWattSeconds, result.PlannedGridPowerWatts);
                _totalBatteryStoredEnergyWattSeconds += result.NextStoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
            }

            _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                _totalBatteryStoredEnergyWattSeconds,
                _totalBatteryCapacityWattSeconds);
        }

        private int ResolveBatteryComponentIndex(int batteryIndex)
        {
            if (batteryIndex < 0 || batteryIndex >= _batteryNodeIndices.Count)
                return -1;

            return _logisticsGraph.GetNodeComponentId(_batteryNodeIndices[batteryIndex]);
        }

        private int ResolveConsumerComponentIndex(int consumerIndex)
        {
            if (consumerIndex < 0 || consumerIndex >= _consumerNodeIndices.Count)
                return -1;

            return _logisticsGraph.GetNodeComponentId(_consumerNodeIndices[consumerIndex]);
        }

        private bool ResolveBatteryEmergencyReserveActiveForComponent(int componentIndex)
        {
            ResolveComponentBatteryStorage(componentIndex, out float storedEnergyWattSeconds, out float capacityWattSeconds);
            return ResolveBatteryEmergencyReserveActive(storedEnergyWattSeconds, capacityWattSeconds);
        }

        private void ResolveComponentBatteryStorage(
            int componentIndex,
            out float storedEnergyWattSeconds,
            out float capacityWattSeconds)
        {
            storedEnergyWattSeconds = 0f;
            capacityWattSeconds = 0f;
            if (componentIndex < 0)
                return;

            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                if (ResolveBatteryComponentIndex(batteryIndex) != componentIndex)
                    continue;

                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                storedEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                capacityWattSeconds += battery.CapacityWattSeconds;
            }
        }

        private bool TryResolveComponentBatteryDispatch(
            int componentIndex,
            out BatteryDispatchMode dispatchMode,
            out float requestedPowerWatts,
            out float totalAvailablePowerWatts,
            out bool reserveAlreadyActive)
        {
            dispatchMode = BatteryDispatchMode.Idle;
            requestedPowerWatts = 0f;
            totalAvailablePowerWatts = 0f;
            reserveAlreadyActive = false;

            if (!_logisticsGraph.TryGetComponentDistribution(componentIndex, out float generationWatts, out float demandWatts))
                return false;

            float componentBalanceWatts = generationWatts - demandWatts;
            if (componentBalanceWatts > 0.0001f)
            {
                totalAvailablePowerWatts = ResolveComponentBatteryChargeAcceptanceWatts(componentIndex);
                if (totalAvailablePowerWatts <= 0.0001f)
                    return false;

                dispatchMode = BatteryDispatchMode.Charge;
                requestedPowerWatts = componentBalanceWatts;
                return true;
            }

            if (componentBalanceWatts >= -0.0001f)
                return false;

            reserveAlreadyActive = ResolveBatteryEmergencyReserveActiveForComponent(componentIndex);
            if (reserveAlreadyActive)
            {
                totalAvailablePowerWatts = ResolveComponentBatteryDischargeAvailabilityWatts(componentIndex, 0f);
                if (totalAvailablePowerWatts <= 0.0001f)
                    return false;

                dispatchMode = BatteryDispatchMode.Discharge;
                requestedPowerWatts = math.max(0f, ResolveEmergencyReservedDemandWatts(componentIndex) - generationWatts);
                return requestedPowerWatts > 0.0001f;
            }

            float reserveSafeAvailablePowerWatts = ResolveComponentBatteryAggregateReserveDischargeAvailabilityWatts(componentIndex);
            if (reserveSafeAvailablePowerWatts <= 0.0001f)
                return false;

            totalAvailablePowerWatts = ResolveComponentBatteryDischargeAvailabilityWatts(
                componentIndex,
                BatteryEmergencyReserveThreshold);
            if (totalAvailablePowerWatts <= 0.0001f)
                return false;

            dispatchMode = BatteryDispatchMode.Discharge;
            requestedPowerWatts = math.min(-componentBalanceWatts, reserveSafeAvailablePowerWatts);
            return requestedPowerWatts > 0.0001f;
        }

        private float ResolveComponentBatteryChargeAcceptanceWatts(int componentIndex)
        {
            if (componentIndex < 0)
                return 0f;

            float totalWatts = 0f;
            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                if (ResolveBatteryComponentIndex(batteryIndex) != componentIndex)
                    continue;

                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                totalWatts += battery.ResolveChargeAcceptanceWatts(BatteryDispatchDeltaTimeSeconds);
            }

            return totalWatts;
        }

        private float ResolveComponentBatteryDischargeAvailabilityWatts(int componentIndex, float reserveFloorNormalized)
        {
            if (componentIndex < 0)
                return 0f;

            float totalWatts = 0f;
            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                if (ResolveBatteryComponentIndex(batteryIndex) != componentIndex)
                    continue;

                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                totalWatts += battery.ResolveDischargeAvailabilityWatts(
                    BatteryDispatchDeltaTimeSeconds,
                    reserveFloorNormalized);
            }

            return totalWatts;
        }

        private float ResolveComponentBatteryAggregateReserveDischargeAvailabilityWatts(int componentIndex)
        {
            if (componentIndex < 0)
                return 0f;

            float storedEnergyWattSeconds = 0f;
            float capacityWattSeconds = 0f;
            float minDischargeEfficiency = 1f;
            float powerLimitedGridEnergyWattSeconds = 0f;
            int batteryCount = _batteryRefs.Count;
            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                if (ResolveBatteryComponentIndex(batteryIndex) != componentIndex)
                    continue;

                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                storedEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                capacityWattSeconds += battery.CapacityWattSeconds;
                minDischargeEfficiency = math.min(minDischargeEfficiency, battery.DischargeEfficiency);
                powerLimitedGridEnergyWattSeconds += battery.ResolveDischargeAvailabilityWatts(
                    BatteryDispatchDeltaTimeSeconds,
                    0f) * BatteryDispatchDeltaTimeSeconds;
            }

            if (capacityWattSeconds <= 0.0001f)
                return 0f;

            float reserveSafeGridEnergyWattSeconds = math.max(
                0f,
                (storedEnergyWattSeconds - (capacityWattSeconds * BatteryEmergencyReserveThreshold)) *
                math.max(0.1f, minDischargeEfficiency));
            float reserveSafeGridPowerWatts = math.min(
                powerLimitedGridEnergyWattSeconds,
                reserveSafeGridEnergyWattSeconds) / BatteryDispatchDeltaTimeSeconds;
            return math.max(0f, reserveSafeGridPowerWatts);
        }

        private void RebuildBatteryReserveComponentCache()
        {
            int batteryCount = _batteryRefs.Count;
            EnsureBatteryReserveComponentCacheCapacity(batteryCount);
            _batteryReserveComponentIds.Clear();
            _batteryReserveComponentStoredEnergyWattSeconds.Clear();
            _batteryReserveComponentCapacityWattSeconds.Clear();
            _batteryReserveComponentMinDischargeEfficiency.Clear();
            _batteryReserveComponentPowerLimitedGridEnergyWattSeconds.Clear();
            _batteryReserveComponentWirelessAvailableEnergyWattSeconds.Clear();
            _batteryReserveComponentReservedWirelessEnergyWattSeconds.Clear();
            _batteryReserveComponentStates.Clear();
            _cachedWirelessToolAvailableEnergyWattSeconds = 0f;

            for (int batteryIndex = 0; batteryIndex < batteryCount; batteryIndex++)
            {
                BatteryBankModule battery = _batteryRefs[batteryIndex];
                if (battery == null)
                    continue;

                int componentIndex = ResolveBatteryComponentIndex(batteryIndex);
                if (componentIndex < 0)
                    continue;

                float dischargeEfficiency = battery.DischargeEfficiency;
                float powerLimitedGridEnergyWattSeconds = battery.ResolveDischargeAvailabilityWatts(
                    BatteryDispatchDeltaTimeSeconds,
                    BatteryEmergencyReserveThreshold) * BatteryDispatchDeltaTimeSeconds;
                int cacheIndex = FindBatteryReserveComponentCacheIndex(componentIndex);
                if (cacheIndex < 0)
                {
                    _batteryReserveComponentIds.Add(componentIndex);
                    _batteryReserveComponentStoredEnergyWattSeconds.Add(battery.StoredEnergyWattSeconds);
                    _batteryReserveComponentCapacityWattSeconds.Add(battery.CapacityWattSeconds);
                    _batteryReserveComponentMinDischargeEfficiency.Add(dischargeEfficiency);
                    _batteryReserveComponentPowerLimitedGridEnergyWattSeconds.Add(powerLimitedGridEnergyWattSeconds);
                    _batteryReserveComponentWirelessAvailableEnergyWattSeconds.Add(0f);
                    _batteryReserveComponentReservedWirelessEnergyWattSeconds.Add(0f);
                    _batteryReserveComponentStates.Add(0);
                    continue;
                }

                _batteryReserveComponentStoredEnergyWattSeconds[cacheIndex] += battery.StoredEnergyWattSeconds;
                _batteryReserveComponentCapacityWattSeconds[cacheIndex] += battery.CapacityWattSeconds;
                _batteryReserveComponentMinDischargeEfficiency[cacheIndex] = math.min(
                    _batteryReserveComponentMinDischargeEfficiency[cacheIndex],
                    dischargeEfficiency);
                _batteryReserveComponentPowerLimitedGridEnergyWattSeconds[cacheIndex] += powerLimitedGridEnergyWattSeconds;
            }

            int componentCount = _batteryReserveComponentIds.Count;
            for (int cacheIndex = 0; cacheIndex < componentCount; cacheIndex++)
            {
                float storedEnergyWattSeconds = _batteryReserveComponentStoredEnergyWattSeconds[cacheIndex];
                float capacityWattSeconds = _batteryReserveComponentCapacityWattSeconds[cacheIndex];
                bool reserveActive = ResolveBatteryEmergencyReserveActive(
                    storedEnergyWattSeconds,
                    capacityWattSeconds);
                float reserveSafeGridEnergyWattSeconds = reserveActive
                    ? 0f
                    : math.max(
                        0f,
                        (storedEnergyWattSeconds - (capacityWattSeconds * BatteryEmergencyReserveThreshold)) *
                        math.max(0.1f, _batteryReserveComponentMinDischargeEfficiency[cacheIndex]));
                float wirelessAvailableEnergyWattSeconds = math.min(
                    math.max(0f, _batteryReserveComponentPowerLimitedGridEnergyWattSeconds[cacheIndex]),
                    reserveSafeGridEnergyWattSeconds);
                _batteryReserveComponentWirelessAvailableEnergyWattSeconds[cacheIndex] = wirelessAvailableEnergyWattSeconds;
                _batteryReserveComponentStates[cacheIndex] = reserveActive ? (byte)1 : (byte)0;
                _cachedWirelessToolAvailableEnergyWattSeconds += wirelessAvailableEnergyWattSeconds;
            }
        }

        private void EnsureBatteryReserveComponentCacheCapacity(int componentCapacity)
        {
            int safeCapacity = math.max(0, componentCapacity);
            if (_batteryReserveComponentIds.Capacity < safeCapacity)
                _batteryReserveComponentIds.Capacity = safeCapacity;
            if (_batteryReserveComponentStoredEnergyWattSeconds.Capacity < safeCapacity)
                _batteryReserveComponentStoredEnergyWattSeconds.Capacity = safeCapacity;
            if (_batteryReserveComponentCapacityWattSeconds.Capacity < safeCapacity)
                _batteryReserveComponentCapacityWattSeconds.Capacity = safeCapacity;
            if (_batteryReserveComponentMinDischargeEfficiency.Capacity < safeCapacity)
                _batteryReserveComponentMinDischargeEfficiency.Capacity = safeCapacity;
            if (_batteryReserveComponentPowerLimitedGridEnergyWattSeconds.Capacity < safeCapacity)
                _batteryReserveComponentPowerLimitedGridEnergyWattSeconds.Capacity = safeCapacity;
            if (_batteryReserveComponentWirelessAvailableEnergyWattSeconds.Capacity < safeCapacity)
                _batteryReserveComponentWirelessAvailableEnergyWattSeconds.Capacity = safeCapacity;
            if (_batteryReserveComponentReservedWirelessEnergyWattSeconds.Capacity < safeCapacity)
                _batteryReserveComponentReservedWirelessEnergyWattSeconds.Capacity = safeCapacity;
            if (_batteryReserveComponentStates.Capacity < safeCapacity)
                _batteryReserveComponentStates.Capacity = safeCapacity;
        }

        private int FindBatteryReserveComponentCacheIndex(int componentIndex)
        {
            int componentCount = _batteryReserveComponentIds.Count;
            for (int cacheIndex = 0; cacheIndex < componentCount; cacheIndex++)
            {
                if (_batteryReserveComponentIds[cacheIndex] == componentIndex)
                    return cacheIndex;
            }

            return -1;
        }

        private bool TryGetCachedBatteryEmergencyReserveActive(int componentIndex, out bool reserveActive)
        {
            reserveActive = false;
            int cacheIndex = FindBatteryReserveComponentCacheIndex(componentIndex);
            if (cacheIndex < 0 || cacheIndex >= _batteryReserveComponentStates.Count)
                return false;

            reserveActive = _batteryReserveComponentStates[cacheIndex] != 0;
            return true;
        }

        private static BatteryDispatchResult ResolveBatteryDispatchRecord(
            in BatteryDispatchRecord record,
            BatteryDispatchMode mode,
            float requestedPowerWatts,
            float totalAvailablePowerWatts,
            float deltaTimeSeconds)
        {
            float safeCapacity = math.max(1f, record.CapacityWattSeconds);
            BatteryDispatchResult result;
            result.NextStoredEnergyWattSeconds = math.clamp(record.StoredEnergyWattSeconds, 0f, safeCapacity);
            result.PlannedGridPowerWatts = 0f;

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

                int chargeConsumerIndex = batteryIndex < _batteryChargeConsumerIndices.Count
                    ? _batteryChargeConsumerIndices[batteryIndex]
                    : -1;
                if (chargeConsumerIndex >= 0 && !_logisticsGraph.IsConsumerPowered(chargeConsumerIndex))
                {
                    battery.ResetDispatchPlan();
                }
                else if (chargeConsumerIndex >= 0 && battery.PlannedGridPowerWatts < -0.0001f)
                {
                    float chargeServiceRatio = 1f;
                    if (_logisticsGraph.TryGetConsumerVoltageSupplyRatio(chargeConsumerIndex, out float resolvedServiceRatio))
                        chargeServiceRatio = resolvedServiceRatio;

                    battery.CommitResolvedDispatch(chargeServiceRatio);
                }
                else
                {
                    battery.CommitResolvedDispatch();
                }

                _totalBatteryStoredEnergyWattSeconds += battery.StoredEnergyWattSeconds;
                _totalBatteryCapacityWattSeconds += battery.CapacityWattSeconds;
            }

            _batteryEmergencyReserveActive = ResolveBatteryEmergencyReserveActive(
                _totalBatteryStoredEnergyWattSeconds,
                _totalBatteryCapacityWattSeconds);
        }

        private BufferID ResolveScratchBufferId(int localOffset)
        {
            return (BufferID)(_vaultBufferBase + localOffset * PowerGridScratchBufferLockLaneStride);
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

        private bool TryPinThermalDissipationJobBuffers(out IDataVault vault, out ulong pinnedMask)
        {
            vault = _dataVault;
            pinnedMask = 0UL;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            pinnedMask = ResolveThermalDissipationMutationGuardMask();
            if (pinnedMask == 0UL || !vault.TryAcquireMutationGuard(pinnedMask))
            {
                vault = null;
                pinnedMask = 0UL;
                return false;
            }

            return true;
        }

        private ulong ResolveThermalDissipationMutationGuardMask()
        {
            return ResolveScratchMutationGuardBit(ThermalTemperatureFrontBufferOffset) |
                   ResolveScratchMutationGuardBit(ThermalTemperatureBackBufferOffset) |
                   ResolveScratchMutationGuardBit(ThermalHeatInjectionBufferOffset) |
                   ResolveScratchMutationGuardBit(ThermalHullSinkConductanceBufferOffset) |
                   ResolveScratchMutationGuardBit(ThermalEdgeOffsetsBufferOffset) |
                   ResolveScratchMutationGuardBit(ThermalEdgeDestinationsBufferOffset) |
                   ResolveScratchMutationGuardBit(ThermalEdgeConductanceBufferOffset);
        }

        private ulong ResolveScratchMutationGuardBit(int localOffset)
        {
            int activeLockBit = ((int)ResolveScratchBufferId(localOffset)) & 31;
            return 1UL << activeLockBit;
        }

        private void ReleaseThermalDissipationJobPins()
        {
            IDataVault vault = _thermalDissipationPinnedVault;
            ulong pinnedMask = _thermalDissipationPinnedMask;
            _thermalDissipationPinnedVault = null;
            _thermalDissipationPinnedMask = 0UL;
            ReleaseThermalDissipationJobPins(vault, pinnedMask);
        }

        private void ReleaseThermalDissipationJobPins(IDataVault vault, ulong pinnedMask)
        {
            if (vault == null || pinnedMask == 0UL)
                return;

            try
            {
            }
            finally
            {
                vault.ReleaseMutationGuard(pinnedMask);
            }
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

        private bool TryAcquireWriteBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || handle.BufferID == 0u || vault.IsCompactionFenceActive)
                return false;

            bool acquired = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in handle, SystemID.Power, out buffer))
                    return false;

                acquired = true;
                if (!buffer.IsCreated)
                    return false;

                acquired = false;
                return true;
            }
            finally
            {
                if (acquired)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.Power);
                    buffer = default;
                }
            }
        }

        private void ReleaseWriteBuffer<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseWriteLock(in handle, SystemID.Power);
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

        private float ResolveEmergencyReservedDemandWatts(int componentIndex)
        {
            if (componentIndex < 0)
                return 0f;

            float demandWatts = 0f;
            int consumerCount = _consumerRefs.Count;
            for (int consumerIndex = 0; consumerIndex < consumerCount; consumerIndex++)
            {
                IPowerComponent consumer = _consumerRefs[consumerIndex];
                if (consumer == null)
                    continue;

                if (consumerIndex >= _consumerNodeIndices.Count ||
                    _logisticsGraph.GetNodeComponentId(_consumerNodeIndices[consumerIndex]) != componentIndex)
                {
                    continue;
                }

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
