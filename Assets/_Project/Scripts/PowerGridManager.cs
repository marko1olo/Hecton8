// ============================================================================
// HECTON-8 — PowerGridManager.cs
// Global owner for all power grids. Uses LogisticsNetworkGraph-backed topology
// snapshots for connectivity checks and brownout-aware distribution.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Power
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5500)]
    public sealed class PowerGridManager : MonoBehaviour, IUpdatable, ISlowTickable
    {
        private const float SlowTickIntervalSeconds = 0.1f;

        private static PowerGridManager _instance;
        private static List<PowerGrid> _allGrids;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
            DisposeAllGrids();
        }

        public static PowerGridManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        internal static List<PowerGrid> RuntimeGrids => _allGrids;

        [Header("â”€â”€ Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐÐ°Ñ‡Ð°Ð»ÑŒÐ½Ð°Ñ Ñ‘Ð¼ÐºÐ¾ÑÑ‚ÑŒ ÑÐ¿Ð¸ÑÐºÐ° ÑÐµÑ‚ÐµÐ¹.")]
        [SerializeField] private int initialGridCapacity = 16;

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private int _debugGridCount;
        [SerializeField] private int _debugTotalNodes;
        [SerializeField] private float _debugTotalGeneration;
        [SerializeField] private float _debugTotalConsumption;
        [SerializeField] private int _debugDeficitGrids;

        private bool _dispatcherRegistered;
        private float _slowTickAccumulator;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_allGrids == null)
                _allGrids = new List<PowerGrid>(Mathf.Max(1, initialGridCapacity));
        }

        private void OnEnable()
        {
            TryRegister();
            _slowTickAccumulator = 0f;
        }

        private void OnDisable()
        {
            TryUnregister();
            _slowTickAccumulator = 0f;
        }

        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
            {
                _instance = null;
                DisposeAllGrids();
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < SlowTickIntervalSeconds)
                return;

            _slowTickAccumulator -= SlowTickIntervalSeconds;
            if (_slowTickAccumulator > SlowTickIntervalSeconds)
                _slowTickAccumulator = SlowTickIntervalSeconds;

            SlowTick();
        }

        public void SlowTick()
        {
            if (_allGrids == null)
                return;

            int totalNodes = 0;
            int deficitCount = 0;
            float totalGeneration = 0f;
            float totalConsumption = 0f;

            for (int gridIndex = _allGrids.Count - 1; gridIndex >= 0; gridIndex--)
            {
                PowerGrid grid = _allGrids[gridIndex];
                if (grid == null || grid.NodeCount == 0)
                {
                    SwapRemoveAt(gridIndex);
                    continue;
                }

                grid.UpdateBalance();

                totalNodes += grid.NodeCount;
                totalGeneration += grid.TotalGeneration;
                totalConsumption += grid.TotalConsumption;

                if (grid.HasPowerDeficit)
                    deficitCount++;
            }

            UpdateDiagnostics(totalGeneration, totalConsumption, totalNodes, deficitCount);
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

        private void TryRegister()
        {
            if (_dispatcherRegistered || !Application.isPlaying)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _dispatcherRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_dispatcherRegistered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _dispatcherRegistered = false;
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
