// ============================================================================
// HECTON-8 â€” PowerGridManager.cs
// Global owner for all power grids. Uses LogisticsNetworkGraph-backed topology
// snapshots for connectivity checks and brownout-aware distribution.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5500)]
    public sealed class PowerGridManager : MonoBehaviour, ISlowTickable, ILateFrameTickable, IPowerGridService
    {
        private static List<PowerGrid> _allGrids;

        internal static PowerGridManager ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeAllGrids();
            ActiveRuntimeInstance = null;
        }

        internal static int RuntimeGridCount => _allGrids != null ? _allGrids.Count : 0;

        internal static PowerGrid GetRuntimeGridAt(int index)
        {
            return _allGrids != null && index >= 0 && index < _allGrids.Count ? _allGrids[index] : null;
        }

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Settings Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [Tooltip("ÃÂÃÂ°Ã‘â€¡ÃÂ°ÃÂ»Ã‘Å’ÃÂ½ÃÂ°Ã‘Â Ã‘â€˜ÃÂ¼ÃÂºÃÂ¾Ã‘ÂÃ‘â€šÃ‘Å’ Ã‘ÂÃÂ¿ÃÂ¸Ã‘ÂÃÂºÃÂ° Ã‘ÂÃÂµÃ‘â€šÃÂµÃÂ¹.")]
        [SerializeField] private int initialGridCapacity = 16;

        [Header("Ã¢â€â‚¬Ã¢â€â‚¬ Diagnostics Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬")]
        [SerializeField] private int _debugGridCount;
        [SerializeField] private int _debugTotalNodes;
        [SerializeField] private float _debugTotalGeneration;
        [SerializeField] private float _debugTotalConsumption;
        [SerializeField] private int _debugDeficitGrids;
        [SerializeField] private float _debugBatteryStoredEnergyWattSeconds;
        [SerializeField] private float _debugBatteryCapacityWattSeconds;
        [SerializeField] private float _debugBatteryChargeNormalized;
        [SerializeField] private int _debugEmergencyReserveGridCount;
        [SerializeField] private float _debugPendingWirelessToolDemandWattSeconds;

        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _serviceRegistered;
        private bool _slowTickFinalizationPending;
        private float _pendingWirelessToolDemandWattSeconds;
        private const float MaxPendingWirelessToolDemandWattSeconds = 4096f;

        public BatteryRuntimeSnapshot BatterySnapshot => new BatteryRuntimeSnapshot
        {
            TotalStoredEnergyWattSeconds = _debugBatteryStoredEnergyWattSeconds,
            TotalCapacityWattSeconds = _debugBatteryCapacityWattSeconds,
            ChargeNormalized = _debugBatteryChargeNormalized,
            EmergencyReserveActive = _debugEmergencyReserveGridCount > 0
        };

        /// <summary>
        /// Explicit bootstrap entry point. Service lifetime is owned by <see cref="Hecton8.Bootstrap.GameBootstrapper"/>.
        /// </summary>
        public void InitializeService()
        {
            EnsureStorage();
            TryRegister();
            TryRegisterService();
        }

        internal static LogisticsBrownoutTier ResolveProjectedBrownoutTier(float supplyWatts, float demandWatts)
        {
            if (demandWatts <= 0.0001f)
                return LogisticsBrownoutTier.None;

            float supplyRatio = math.saturate(supplyWatts / demandWatts);
            if (supplyRatio < 0.10f)
                return LogisticsBrownoutTier.EmergencyOnly;

            if (supplyRatio < 0.40f)
                return LogisticsBrownoutTier.EssentialOnly;

            if (supplyRatio < 0.85f)
                return LogisticsBrownoutTier.AmbientLightsOnly;

            return LogisticsBrownoutTier.None;
        }

        public bool TryQueueWirelessToolDrain(float energyWattSeconds, out float grantedEnergyWattSeconds)
        {
            grantedEnergyWattSeconds = 0f;
            if (energyWattSeconds <= 0f || _debugBatteryStoredEnergyWattSeconds <= 0.0001f || _debugEmergencyReserveGridCount > 0)
                return false;

            float remainingQueueCapacity = math.max(0f, MaxPendingWirelessToolDemandWattSeconds - _pendingWirelessToolDemandWattSeconds);
            if (remainingQueueCapacity <= 0.0001f)
                return false;

            grantedEnergyWattSeconds = math.min(energyWattSeconds, remainingQueueCapacity);
            _pendingWirelessToolDemandWattSeconds += grantedEnergyWattSeconds;
            _debugPendingWirelessToolDemandWattSeconds = _pendingWirelessToolDemandWattSeconds;
            return grantedEnergyWattSeconds > 0.0001f;
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            if (_allGrids == null)
                _allGrids = new List<PowerGrid>(math.max(1, initialGridCapacity));
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            TryRegister();
            TryRegisterService();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            CompletePendingSlowTickEvaluationsForTeardown();
            TryUnregister();
            TryUnregisterService();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (_allGrids == null)
                return;

            if (_slowTickFinalizationPending)
                return;

            for (int gridIndex = _allGrids.Count - 1; gridIndex >= 0; gridIndex--)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null || grid.NodeCount == 0)
                {
                    SwapRemoveAt(gridIndex);
                    continue;
                }

                grid.BeginSlowTickEvaluation();
            }

            _slowTickFinalizationPending = true;
        }

        /// <inheritdoc />
        public void LateFrameTick()
        {
            if (!_slowTickFinalizationPending)
                return;

            if (!TryFinalizeSlowTickEvaluations())
                return;

            PublishTelemetrySnapshot();
            _slowTickFinalizationPending = false;
        }

        private void OnDestroy()
        {
            CompletePendingSlowTickEvaluationsForTeardown();
            TryUnregister();
            TryUnregisterService();
            DisposeAllGrids();
        }

        public static PowerGrid CreateGrid(PowerNode initialNode)
        {
            if (initialNode == null)
                return null;

            EnsureStorage();

            PowerGrid grid = new PowerGrid();
            grid.AddNode(initialNode);
            _allGrids.Add(grid);
            return grid;
        }

        public static void DestroyGrid(PowerGrid grid)
        {
            if (grid == null || _allGrids == null)
                return;

            int gridCount = _allGrids.Count;
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
            {
                if (!ReferenceEquals(_allGrids[gridIndex], grid))
                    continue;

                SwapRemoveAt(gridIndex);
                return;
            }
        }

        public static PowerGrid MergeGrids(PowerGrid a, PowerGrid b)
        {
            if (a == null)
                return b;

            if (b == null)
                return a;

            if (ReferenceEquals(a, b))
                return a;

            PowerGrid larger;
            PowerGrid smaller;
            if (a.NodeCount >= b.NodeCount)
            {
                larger = a;
                smaller = b;
            }
            else
            {
                larger = b;
                smaller = a;
            }

            larger.AbsorbAll(smaller);
            DestroyGrid(smaller);
            return larger;
        }

        public static void CheckAndSplitGrid(PowerGrid grid)
        {
            if (grid == null || grid.NodeCount <= 1)
                return;

            LogisticsNetworkGraph.TopologySummary topology = grid.AnalyzeTopology();
            if (topology.NodeCount <= 1)
                return;

            if (topology.BfsVisitedCount == topology.NodeCount && topology.IslandCount <= 1)
                return;

            EnsureStorage();

            List<PowerNode> topologyNodes = grid.TopologyNodes;
            if (topologyNodes == null || topologyNodes.Count <= 0)
                return;

            int primaryComponentId = grid.GetNodeComponentId(0);
            if (primaryComponentId < 0)
                primaryComponentId = 0;

            for (int componentId = 0; componentId < topology.IslandCount; componentId++)
            {
                if (componentId == primaryComponentId)
                    continue;

                int componentSize = grid.GetComponentSize(componentId);
                if (componentSize <= 0)
                    continue;

                PowerGrid newGrid = new PowerGrid(componentSize);
                _allGrids.Add(newGrid);

                for (int nodeIndex = topologyNodes.Count - 1; nodeIndex >= 0; nodeIndex--)
                {
                    if (grid.GetNodeComponentId(nodeIndex) != componentId)
                        continue;

                    PowerNode node = topologyNodes[nodeIndex];
                    if (node == null)
                        continue;

                    grid.RemoveNode(node);
                    newGrid.AddNode(node);
                }

                newGrid.UpdateBalance();
            }

            grid.UpdateBalance();
        }

        public int GridCount => _allGrids != null ? _allGrids.Count : 0;
        public float TotalGeneration => _debugTotalGeneration;
        public float TotalConsumption => _debugTotalConsumption;

        private static void EnsureStorage()
        {
            if (_allGrids == null)
                _allGrids = new List<PowerGrid>(16);
        }

        private static void DisposeAllGrids()
        {
            if (_allGrids == null)
                return;

            int gridCount = _allGrids.Count;
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
                _allGrids[gridIndex]?.Dispose();

            _allGrids.Clear();
        }

        private static void SwapRemoveAt(int index)
        {
            PowerGrid removedGrid = _allGrids[index];
            int lastIndex = _allGrids.Count - 1;
            if (index < lastIndex)
                _allGrids[index] = _allGrids[lastIndex];

            _allGrids.RemoveAt(lastIndex);
            removedGrid?.Dispose();
        }

        private static bool TryFinalizeSlowTickEvaluations()
        {
            if (_allGrids == null)
                return true;

            for (int gridIndex = _allGrids.Count - 1; gridIndex >= 0; gridIndex--)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null || grid.NodeCount == 0)
                    SwapRemoveAt(gridIndex);
            }

            bool allReady = true;
            int gridCount = _allGrids.Count;
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null)
                    continue;

                if (!grid.TryEndSlowTickEvaluation())
                    allReady = false;
            }

            return allReady;
        }

        private static void CompleteAllPendingGridEvaluations()
        {
            if (_allGrids == null)
                return;

            int gridCount = _allGrids.Count;
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
                _allGrids[gridIndex]?.EndSlowTickEvaluation();
        }

        private void CompletePendingSlowTickEvaluationsForTeardown()
        {
            if (!_slowTickFinalizationPending)
                return;

            CompleteAllPendingGridEvaluations();
            _slowTickFinalizationPending = false;
        }

        private void PublishTelemetrySnapshot()
        {
            int totalNodes = 0;
            int deficitCount = 0;
            float totalGeneration = 0f;
            float totalConsumption = 0f;
            float totalBatteryStoredEnergy = 0f;
            float totalBatteryCapacity = 0f;
            int emergencyReserveGridCount = 0;
            float lowestSupplyRatio = 1f;
            LogisticsBrownoutTier highestBrownoutTier = LogisticsBrownoutTier.None;
            int finalizedGridCount = _allGrids != null ? _allGrids.Count : 0;

            for (int gridIndex = 0; gridIndex < finalizedGridCount; gridIndex++)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null)
                    continue;

                totalNodes += grid.NodeCount;
                totalGeneration += grid.TotalGeneration;
                totalConsumption += grid.TotalConsumption;
                totalBatteryStoredEnergy += grid.TotalBatteryStoredEnergyWattSeconds;
                totalBatteryCapacity += grid.TotalBatteryCapacityWattSeconds;
                lowestSupplyRatio = math.min(lowestSupplyRatio, grid.SupplyRatio);

                if (grid.HasPowerDeficit)
                    deficitCount++;
                if (grid.IsBatteryEmergencyReserveActive)
                    emergencyReserveGridCount++;
                if (grid.BrownoutTier > highestBrownoutTier)
                    highestBrownoutTier = grid.BrownoutTier;
            }

            ConsumePendingWirelessToolDemand();
            totalBatteryStoredEnergy = 0f;
            totalBatteryCapacity = 0f;
            emergencyReserveGridCount = 0;
            for (int gridIndex = 0; gridIndex < finalizedGridCount; gridIndex++)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null)
                    continue;

                totalBatteryStoredEnergy += grid.TotalBatteryStoredEnergyWattSeconds;
                totalBatteryCapacity += grid.TotalBatteryCapacityWattSeconds;
                if (grid.IsBatteryEmergencyReserveActive)
                    emergencyReserveGridCount++;
            }

            UpdateDiagnostics(totalGeneration, totalConsumption, totalNodes, deficitCount);
            _debugBatteryStoredEnergyWattSeconds = totalBatteryStoredEnergy;
            _debugBatteryCapacityWattSeconds = totalBatteryCapacity;
            _debugBatteryChargeNormalized = totalBatteryCapacity > 0.0001f
                ? math.saturate(totalBatteryStoredEnergy / totalBatteryCapacity)
                : 0f;
            _debugEmergencyReserveGridCount = emergencyReserveGridCount;
            _debugPendingWirelessToolDemandWattSeconds = _pendingWirelessToolDemandWattSeconds;

            float supplyRatio = totalConsumption > 0.0001f
                ? math.saturate(totalGeneration / totalConsumption)
                : 1f;
            if (finalizedGridCount > 0)
                supplyRatio = math.min(supplyRatio, lowestSupplyRatio);

            float availablePowerNormalized = totalBatteryCapacity > 0.0001f
                ? _debugBatteryChargeNormalized
                : supplyRatio;

            PowerGridTelemetrySnapshot telemetrySnapshot = new PowerGridTelemetrySnapshot(
                finalizedGridCount,
                deficitCount,
                totalGeneration,
                totalConsumption,
                supplyRatio,
                _debugBatteryChargeNormalized,
                availablePowerNormalized,
                highestBrownoutTier,
                deficitCount > 0,
                emergencyReserveGridCount > 0);
            PowerGridTelemetryEvents.Raise(in telemetrySnapshot);
        }

        private void TryRegister()
        {
            if (_dispatcherRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _dispatcherRegistered = SystemDispatcher.GetSlowLane(PriorityLayer.Environment).Contains(this);
            GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Environment).Contains(this);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterPowerGridService(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PowerGrid, this);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPowerGridService(this);
            _serviceRegistered = false;
        }

        private void ConsumePendingWirelessToolDemand()
        {
            if (_pendingWirelessToolDemandWattSeconds <= 0.0001f || _allGrids == null)
                return;

            float remaining = _pendingWirelessToolDemandWattSeconds;
            int gridCount = _allGrids.Count;
            for (int gridIndex = 0; gridIndex < gridCount; gridIndex++)
            {
                if (remaining <= 0.0001f)
                    break;

                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null)
                    continue;

                remaining -= grid.TryConsumeWirelessToolDemand(remaining);
            }

            _pendingWirelessToolDemandWattSeconds = math.max(0f, remaining);
            _debugPendingWirelessToolDemandWattSeconds = _pendingWirelessToolDemandWattSeconds;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(float generation, float consumption, int totalNodes, int deficitGrids)
        {
            _debugGridCount = _allGrids != null ? _allGrids.Count : 0;
            _debugTotalNodes = totalNodes;
            _debugTotalGeneration = generation;
            _debugTotalConsumption = consumption;
            _debugDeficitGrids = deficitGrids;
        }
    }
}
