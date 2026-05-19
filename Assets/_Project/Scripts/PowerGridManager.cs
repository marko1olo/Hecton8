// ============================================================================
// HECTON-8 â€” PowerGridManager.cs
// Global owner for all power grids. Uses LogisticsNetworkGraph-backed topology
// snapshots for connectivity checks and brownout-aware distribution.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5500)]
    public sealed class PowerGridManager : MonoBehaviour, ISlowTickable, ILateFrameTickable, IPowerGridService, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
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
        private bool _hotSwapRegistered;
        private bool _slowTickFinalizationPending;
        private float _pendingWirelessToolDemandWattSeconds;
        private float _nextPowerColdTickTime;
        private float _nextSubmarineThermalGridTickTime;
        private uint _submarineThermalGridSimulationFrame;
        private WfcOutpostPowerBootRuntime _wfcOutpostPowerBoot; // COLD ALLOC: WfcOutpostPowerBootRuntime[1] - WFC outpost signal boot owner - owner: PowerGridManager
        private ShinobuLogisticsRouter _shinobuLogisticsRouter; // COLD ALLOC: ShinobuLogisticsRouter[1] - zero-GC WFC logistics BFS owner - owner: PowerGridManager
        private SubmarineOsThermalGridRuntime _submarineThermalGridRuntime; // COLD ALLOC: SubmarineOsThermalGridRuntime[1] - submarine OS thermal grid owner - owner: PowerGridManager
        private const float PowerGridColdTickSeconds = 1f;
        private const float SubmarineThermalGridLowCadenceSeconds = 0.2f;
        private const float SubmarineThermalGridHighCadenceSeconds = 1f / 60f;
        private const float MaxPendingWirelessToolDemandWattSeconds = 4096f;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

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
            EnsureWfcOutpostPowerBoot();
            EnsureShinobuLogisticsRouter();
            EnsureSubmarineThermalGridRuntime();
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

            EnsureWfcOutpostPowerBoot();
            EnsureShinobuLogisticsRouter();
            EnsureSubmarineThermalGridRuntime();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            EnsureWfcOutpostPowerBoot();
            EnsureShinobuLogisticsRouter();
            EnsureSubmarineThermalGridRuntime();
            TryRegister();
            TryRegisterService();
        }

        private void OnDisable()
        {
            UnregisterRuntimeHooks();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            UnregisterRuntimeHooks();
            DisposeAllGrids();
            _wfcOutpostPowerBoot?.Dispose();
            _wfcOutpostPowerBoot = null;
            _shinobuLogisticsRouter?.Dispose();
            _shinobuLogisticsRouter = null;
            _submarineThermalGridRuntime?.Dispose();
            _submarineThermalGridRuntime = null;
            _pendingWirelessToolDemandWattSeconds = 0f;
            _debugPendingWirelessToolDemandWattSeconds = 0f;
            _slowTickFinalizationPending = false;
            _nextPowerColdTickTime = 0f;
            _nextSubmarineThermalGridTickTime = 0f;
            _submarineThermalGridSimulationFrame = 0u;
        }

        private void UnregisterRuntimeHooks()
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
            float now = Time.unscaledTime;
            _wfcOutpostPowerBoot?.SlowTick(now);
            _shinobuLogisticsRouter?.SlowTick(now);
            _submarineThermalGridRuntime?.TryCompleteExternalThermalInjectionPostSimulation();
            _submarineThermalGridRuntime?.TryCommitTopologyRebuildPostSimulation();
            _submarineThermalGridRuntime?.TryCompleteSolvePostSimulation();
            ScheduleSubmarineThermalGridIfDue(now);

            if (_slowTickFinalizationPending)
                return;

            if (_allGrids == null)
                return;

            if (now + 0.0001f < _nextPowerColdTickTime)
                return;

            _nextPowerColdTickTime = now + PowerGridColdTickSeconds;

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
            _wfcOutpostPowerBoot?.LateFrameTick(Time.unscaledTime);
            _shinobuLogisticsRouter?.LateFrameTick(Time.unscaledTime);
            _submarineThermalGridRuntime?.TryCompleteExternalThermalInjectionPostSimulation();
            _submarineThermalGridRuntime?.TryCommitTopologyRebuildPostSimulation();
            _submarineThermalGridRuntime?.TryCompleteSolvePostSimulation();
            ScheduleSubmarineThermalGridIfDue(Time.unscaledTime);
            _submarineThermalGridRuntime?.TryPublishVisualShaderScalars();
            if (!_slowTickFinalizationPending)
                return;

            if (!TryFinalizeSlowTickEvaluations())
                return;

            ProcessPendingSplitChecks();
            PublishTelemetrySnapshot();
            _slowTickFinalizationPending = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.GasDynamicsRuntime)
            {
                _wfcOutpostPowerBoot?.BindGasDynamics(currentService as IGasDynamicsSolver);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                _shinobuLogisticsRouter?.Dispose();
                _shinobuLogisticsRouter = null;
                _submarineThermalGridRuntime?.Dispose();
                _submarineThermalGridRuntime = null;
                _nextSubmarineThermalGridTickTime = 0f;
                _submarineThermalGridSimulationFrame = 0u;
                if (currentService is IDataVault)
                {
                    EnsureShinobuLogisticsRouter();
                    EnsureSubmarineThermalGridRuntime();
                }
            }
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

            grid.RequestSplitCheck();
        }

        private static void SplitGridIfDisconnected(PowerGrid grid, LogisticsNetworkGraph.TopologySummary topology)
        {
            if (grid == null || grid.NodeCount <= 1)
                return;

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

        public bool TryGetGridPowerPotentialsReadOnly(int gridIndex, out NativeArray<float>.ReadOnly potentials)
        {
            potentials = default;
            if (_allGrids == null || gridIndex < 0 || gridIndex >= _allGrids.Count)
                return false;

            PowerGrid grid = _allGrids[gridIndex];
            if (grid == null)
                return false;

            potentials = grid.GetPowerPotentialsReadOnly();
            return potentials.Length > 0;
        }

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

        private static void ProcessPendingSplitChecks()
        {
            if (_allGrids == null)
                return;

            int gridCount = _allGrids.Count;
            for (int gridIndex = gridCount - 1; gridIndex >= 0; gridIndex--)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null)
                    continue;

                if (!grid.TryConsumePendingSplitCheck(out LogisticsNetworkGraph.TopologySummary topology))
                    continue;

                SplitGridIfDisconnected(grid, topology);
            }
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
            PublishBrownoutSignal(in telemetrySnapshot);
        }

        private static void PublishBrownoutSignal(in PowerGridTelemetrySnapshot snapshot)
        {
            byte flags = 0;
            if (snapshot.HasPowerDeficit)
                flags |= 1;
            if (snapshot.EmergencyReserveActive)
                flags |= 1 << 1;

            float severity01 = snapshot.HighestBrownoutTier != LogisticsBrownoutTier.None ||
                               snapshot.HasPowerDeficit ||
                               snapshot.EmergencyReserveActive
                ? math.saturate(1f - snapshot.SupplyRatio)
                : 0f;
            if (snapshot.EmergencyReserveActive)
                severity01 = math.max(severity01, 0.75f);

            BrownoutSignal signal = new BrownoutSignal
            {
                NetworkId = unchecked((uint)math.max(0, snapshot.GridCount)),
                NodeId = unchecked((uint)math.max(0, snapshot.DeficitGridCount)),
                SupplyRatio = math.saturate(snapshot.SupplyRatio),
                Severity01 = severity01,
                Frame = unchecked((uint)math.max(0, Time.frameCount)),
                Priority = (byte)snapshot.HighestBrownoutTier,
                Flags = flags
            };
            GlobalSignals.Publish(in signal);
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
            TryRegisterHotSwapListener();
            _wfcOutpostPowerBoot?.BindGasDynamics(GlobalRegistry.GasDynamics);
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
            if (_hotSwapRegistered)
            {
                GlobalRegistry.UnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPowerGridService(this);
            _serviceRegistered = false;
        }

        private void EnsureWfcOutpostPowerBoot()
        {
            if (_wfcOutpostPowerBoot != null)
                return;

            _wfcOutpostPowerBoot = new WfcOutpostPowerBootRuntime();
            _wfcOutpostPowerBoot.Initialize();
        }

        private void EnsureShinobuLogisticsRouter()
        {
            if (_shinobuLogisticsRouter == null)
                _shinobuLogisticsRouter = new ShinobuLogisticsRouter();

            _shinobuLogisticsRouter.InjectDataVault(GlobalRegistry.DataVault);
            _shinobuLogisticsRouter.EnsureInitialized();
        }

        private void EnsureSubmarineThermalGridRuntime()
        {
            if (_submarineThermalGridRuntime == null)
                _submarineThermalGridRuntime = new SubmarineOsThermalGridRuntime();

            _submarineThermalGridRuntime.InjectDataVault(GlobalRegistry.DataVault);
            _submarineThermalGridRuntime.EnsureInitialized();
        }

        private void ScheduleSubmarineThermalGridIfDue(float now)
        {
            SubmarineOsThermalGridRuntime runtime = _submarineThermalGridRuntime;
            if (runtime == null)
                return;

            float quality = HomeostasisBrain.GlobalQualityWeight;
            float cadenceSeconds = ResolveSubmarineThermalGridCadenceSeconds(quality);
            if (now + 0.0001f < _nextSubmarineThermalGridTickTime)
                return;

            uint candidateSimulationFrame = unchecked(_submarineThermalGridSimulationFrame + 1u);
            if (candidateSimulationFrame == 0u)
                candidateSimulationFrame = 1u;

            if (runtime.ScheduleSolve(cadenceSeconds, quality, candidateSimulationFrame, default, out _))
            {
                _submarineThermalGridSimulationFrame = candidateSimulationFrame;
                _nextSubmarineThermalGridTickTime = now + cadenceSeconds;
                return;
            }

            _nextSubmarineThermalGridTickTime = now + math.min(cadenceSeconds, 0.05f);
        }

        internal static float ResolveSubmarineThermalGridCadenceSeconds(float globalQualityWeight)
        {
            float weight = math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 0f);
            float smooth = weight * weight * (3f - 2f * weight);
            return math.lerp(SubmarineThermalGridLowCadenceSeconds, SubmarineThermalGridHighCadenceSeconds, smooth);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
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
