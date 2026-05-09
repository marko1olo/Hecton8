using System;
using Hecton8.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Bootstrap
{
    /// <summary>
    /// Immutable dependency edge between registry service slots.
    /// </summary>
    public readonly struct BootstrapRegistryDependencyEdge
    {
        /// <summary>
        /// Service that cannot boot until <see cref="Dependency"/> is ready.
        /// </summary>
        public readonly GlobalRegistryServiceSlot Source;

        /// <summary>
        /// Required upstream service.
        /// </summary>
        public readonly GlobalRegistryServiceSlot Dependency;

        /// <summary>
        /// Creates one dependency edge for the startup graph.
        /// </summary>
        public BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot source, GlobalRegistryServiceSlot dependency)
        {
            Source = source;
            Dependency = dependency;
        }
    }

    /// <summary>
    /// Kahn topological validator for the registry service boot graph.
    /// </summary>
    public static class BootstrapRegistryCycleValidator
    {
        private static readonly GlobalRegistryServiceSlot[] _startupNodes =
        {
            GlobalRegistryServiceSlot.Dispatcher,
            GlobalRegistryServiceSlot.TickManager,
            GlobalRegistryServiceSlot.Save,
            GlobalRegistryServiceSlot.ObjectPool,
            GlobalRegistryServiceSlot.RenderDispatcher,
            GlobalRegistryServiceSlot.Scene,
            GlobalRegistryServiceSlot.InteractionSignals,
            GlobalRegistryServiceSlot.FloatingOriginRuntime,
            GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime,
            GlobalRegistryServiceSlot.PhysicsStateManager,
            GlobalRegistryServiceSlot.Physics,
            GlobalRegistryServiceSlot.Debris,
            GlobalRegistryServiceSlot.Environment,
            GlobalRegistryServiceSlot.OceanKinematics,
            GlobalRegistryServiceSlot.EcosystemDirector,
            GlobalRegistryServiceSlot.FaunaSimulation,
            GlobalRegistryServiceSlot.Audio,
            GlobalRegistryServiceSlot.PowerGrid,
            GlobalRegistryServiceSlot.Logistics,
            GlobalRegistryServiceSlot.NativeInputManagerRuntime,
            GlobalRegistryServiceSlot.Input,
            GlobalRegistryServiceSlot.BeaconNetworkRuntime,
            GlobalRegistryServiceSlot.ModWorldPersistenceRuntime,
            GlobalRegistryServiceSlot.Player,
            GlobalRegistryServiceSlot.PlayerInventory,
            GlobalRegistryServiceSlot.PlayerSensory,
        };

        private static readonly BootstrapRegistryDependencyEdge[] _startupEdges =
        {
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.TickManager, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Save, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ObjectPool, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.RenderDispatcher, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Scene, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.InteractionSignals, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.FloatingOriginRuntime, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime, GlobalRegistryServiceSlot.FloatingOriginRuntime),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PhysicsStateManager, GlobalRegistryServiceSlot.FloatingOriginRuntime),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Physics, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Physics, GlobalRegistryServiceSlot.PhysicsStateManager),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Debris, GlobalRegistryServiceSlot.ObjectPool),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Debris, GlobalRegistryServiceSlot.Physics),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Environment, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.OceanKinematics, GlobalRegistryServiceSlot.Environment),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.EcosystemDirector, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.FaunaSimulation, GlobalRegistryServiceSlot.EcosystemDirector),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.FaunaSimulation, GlobalRegistryServiceSlot.Physics),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Audio, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PowerGrid, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Logistics, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Logistics, GlobalRegistryServiceSlot.Save),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Logistics, GlobalRegistryServiceSlot.ObjectPool),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Logistics, GlobalRegistryServiceSlot.PowerGrid),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.NativeInputManagerRuntime, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Input, GlobalRegistryServiceSlot.NativeInputManagerRuntime),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Input, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.BeaconNetworkRuntime, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.BeaconNetworkRuntime, GlobalRegistryServiceSlot.Save),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ModWorldPersistenceRuntime, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ModWorldPersistenceRuntime, GlobalRegistryServiceSlot.Save),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ModWorldPersistenceRuntime, GlobalRegistryServiceSlot.ObjectPool),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.ModWorldPersistenceRuntime, GlobalRegistryServiceSlot.Scene),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Player, GlobalRegistryServiceSlot.Input),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PlayerInventory, GlobalRegistryServiceSlot.Player),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PlayerInventory, GlobalRegistryServiceSlot.ObjectPool),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PlayerSensory, GlobalRegistryServiceSlot.Player),
        };

        // COLD ALLOC: object[1] - startup graph validation scratch lock - owner: BootstrapRegistryCycleValidator
        private static readonly object _validationScratchLock = new object();
        // COLD ALLOC: GlobalRegistryServiceSlot[startup node count] - startup graph topological order scratch - owner: BootstrapRegistryCycleValidator
        private static readonly GlobalRegistryServiceSlot[] _startupExecutionOrderScratch =
            new GlobalRegistryServiceSlot[_startupNodes.Length];
        // COLD ALLOC: int[startup node count] - startup graph in-degree scratch - owner: BootstrapRegistryCycleValidator
        private static readonly int[] _inDegreeScratch = new int[_startupNodes.Length];
        // COLD ALLOC: int[startup node count] - startup graph Kahn queue scratch - owner: BootstrapRegistryCycleValidator
        private static readonly int[] _queueScratch = new int[_startupNodes.Length];
        // COLD ALLOC: int[256] - byte-sized service-slot to node-index lookup - owner: BootstrapRegistryCycleValidator
        private static readonly int[] _nodeIndexScratch = new int[256];

        /// <summary>
        /// Number of registry slots in the startup validation graph.
        /// </summary>
        public static int StartupNodeCount => _startupNodes.Length;

        /// <summary>
        /// Validates the canonical startup graph and writes the topological order.
        /// </summary>
        public static bool TryValidateStartupGraph(GlobalRegistryServiceSlot[] executionOrder, out int executionOrderCount)
        {
            return TryBuildExecutionOrder(_startupNodes, _startupEdges, executionOrder, out executionOrderCount);
        }

        /// <summary>
        /// Builds the canonical startup execution order or aborts play mode on a circular graph.
        /// </summary>
        public static bool TryBuildStartupExecutionOrderOrThrow(
            GlobalRegistryServiceSlot[] executionOrder,
            out int executionOrderCount)
        {
            if (TryValidateStartupGraph(executionOrder, out executionOrderCount))
                return true;

            HaltEditorPlayModeForCriticalCycle();
            throw new CriticalBootException("[BootstrapRegistryCycleValidator] Circular registry dependency graph.");
        }

        /// <summary>
        /// Validates the canonical startup graph without retaining the output order.
        /// </summary>
        public static bool TryValidateStartupGraph()
        {
            return TryValidateStartupGraph(_startupExecutionOrderScratch, out _);
        }

        /// <summary>
        /// Runs Kahn topological validation over an explicit registry graph.
        /// </summary>
        public static bool TryBuildExecutionOrder(
            GlobalRegistryServiceSlot[] nodes,
            BootstrapRegistryDependencyEdge[] edges,
            GlobalRegistryServiceSlot[] executionOrder,
            out int executionOrderCount)
        {
            executionOrderCount = 0;
            if (nodes == null || edges == null || executionOrder == null)
                return false;

            int nodeCount = nodes.Length;
            if (executionOrder.Length < nodeCount || nodeCount > _inDegreeScratch.Length)
                return false;

            lock (_validationScratchLock)
            {
                return TryBuildExecutionOrderLocked(nodes, edges, executionOrder, nodeCount, out executionOrderCount);
            }
        }

        private static bool TryBuildExecutionOrderLocked(
            GlobalRegistryServiceSlot[] nodes,
            BootstrapRegistryDependencyEdge[] edges,
            GlobalRegistryServiceSlot[] executionOrder,
            int nodeCount,
            out int executionOrderCount)
        {
            executionOrderCount = 0;
            Array.Clear(_inDegreeScratch, 0, nodeCount);
            Array.Clear(_queueScratch, 0, nodeCount);
            for (int i = 0; i < _nodeIndexScratch.Length; i++)
                _nodeIndexScratch[i] = -1;

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                int serviceSlotIndex = (int)(byte)nodes[nodeIndex];
                if (_nodeIndexScratch[serviceSlotIndex] >= 0)
                    return false;

                _nodeIndexScratch[serviceSlotIndex] = nodeIndex;
            }

            int queueHead = 0;
            int queueTail = 0;

            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                if (!IsValidEdge(edges, edgeIndex))
                    return false;

                int sourceIndex = ResolveNodeIndex(edges[edgeIndex].Source);
                int dependencyIndex = ResolveNodeIndex(edges[edgeIndex].Dependency);
                if (sourceIndex < 0 || dependencyIndex < 0)
                    return false;

                _inDegreeScratch[sourceIndex]++;
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                if (_inDegreeScratch[nodeIndex] == 0)
                    _queueScratch[queueTail++] = nodeIndex;
            }

            while (queueHead < queueTail)
            {
                int dependencyIndex = _queueScratch[queueHead++];
                executionOrder[executionOrderCount++] = nodes[dependencyIndex];

                for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
                {
                    if (ResolveNodeIndex(edges[edgeIndex].Dependency) != dependencyIndex)
                        continue;

                    int sourceIndex = ResolveNodeIndex(edges[edgeIndex].Source);
                    _inDegreeScratch[sourceIndex]--;
                    if (_inDegreeScratch[sourceIndex] == 0)
                        _queueScratch[queueTail++] = sourceIndex;
                }
            }

            if (executionOrderCount == nodeCount)
                return true;

            ReportCycle(nodes, edges, _inDegreeScratch);
            executionOrderCount = 0;
            return false;
        }

        private static bool IsValidEdge(BootstrapRegistryDependencyEdge[] edges, int edgeIndex)
        {
            BootstrapRegistryDependencyEdge edge = edges[edgeIndex];
            if (edge.Source == edge.Dependency)
                return false;

            for (int previousIndex = 0; previousIndex < edgeIndex; previousIndex++)
            {
                BootstrapRegistryDependencyEdge previous = edges[previousIndex];
                if (previous.Source == edge.Source && previous.Dependency == edge.Dependency)
                    return false;
            }

            return true;
        }

        private static int ResolveNodeIndex(GlobalRegistryServiceSlot serviceSlot)
        {
            return _nodeIndexScratch[(int)(byte)serviceSlot];
        }

        private static int IndexOf(GlobalRegistryServiceSlot[] nodes, GlobalRegistryServiceSlot serviceSlot)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == serviceSlot)
                    return i;
            }

            return -1;
        }

        private static void ReportCycle(
            GlobalRegistryServiceSlot[] nodes,
            BootstrapRegistryDependencyEdge[] edges,
            int[] inDegree)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                int sourceIndex = IndexOf(nodes, edges[edgeIndex].Source);
                int dependencyIndex = IndexOf(nodes, edges[edgeIndex].Dependency);
                if (sourceIndex < 0 || dependencyIndex < 0)
                    continue;
                if (inDegree[sourceIndex] <= 0 || inDegree[dependencyIndex] <= 0)
                    continue;

                string sourceName = GetServiceSlotName(edges[edgeIndex].Source);
                string dependencyName = GetServiceSlotName(edges[edgeIndex].Dependency);
                GlobalTelemetryBus.PublishBootstrapDependencyCycle(sourceName, dependencyName);
            }

            Debug.LogError("[BootstrapRegistryCycleValidator] Circular registry dependency detected. Edge details emitted to GlobalTelemetryBus.");
#endif
        }

        private static void HaltEditorPlayModeForCriticalCycle()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
                EditorApplication.isPlaying = false;
#endif
        }

        private static string GetServiceSlotName(GlobalRegistryServiceSlot serviceSlot)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher: return nameof(GlobalRegistryServiceSlot.Dispatcher);
                case GlobalRegistryServiceSlot.TickManager: return nameof(GlobalRegistryServiceSlot.TickManager);
                case GlobalRegistryServiceSlot.Save: return nameof(GlobalRegistryServiceSlot.Save);
                case GlobalRegistryServiceSlot.ObjectPool: return nameof(GlobalRegistryServiceSlot.ObjectPool);
                case GlobalRegistryServiceSlot.RenderDispatcher: return nameof(GlobalRegistryServiceSlot.RenderDispatcher);
                case GlobalRegistryServiceSlot.Scene: return nameof(GlobalRegistryServiceSlot.Scene);
                case GlobalRegistryServiceSlot.InteractionSignals: return nameof(GlobalRegistryServiceSlot.InteractionSignals);
                case GlobalRegistryServiceSlot.FloatingOriginRuntime: return nameof(GlobalRegistryServiceSlot.FloatingOriginRuntime);
                case GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime: return nameof(GlobalRegistryServiceSlot.ConnectionSplineBatchRendererRuntime);
                case GlobalRegistryServiceSlot.PhysicsStateManager: return nameof(GlobalRegistryServiceSlot.PhysicsStateManager);
                case GlobalRegistryServiceSlot.Physics: return nameof(GlobalRegistryServiceSlot.Physics);
                case GlobalRegistryServiceSlot.Debris: return nameof(GlobalRegistryServiceSlot.Debris);
                case GlobalRegistryServiceSlot.Environment: return nameof(GlobalRegistryServiceSlot.Environment);
                case GlobalRegistryServiceSlot.OceanKinematics: return nameof(GlobalRegistryServiceSlot.OceanKinematics);
                case GlobalRegistryServiceSlot.EcosystemDirector: return nameof(GlobalRegistryServiceSlot.EcosystemDirector);
                case GlobalRegistryServiceSlot.FaunaSimulation: return nameof(GlobalRegistryServiceSlot.FaunaSimulation);
                case GlobalRegistryServiceSlot.Audio: return nameof(GlobalRegistryServiceSlot.Audio);
                case GlobalRegistryServiceSlot.PowerGrid: return nameof(GlobalRegistryServiceSlot.PowerGrid);
                case GlobalRegistryServiceSlot.Logistics: return nameof(GlobalRegistryServiceSlot.Logistics);
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime: return nameof(GlobalRegistryServiceSlot.NativeInputManagerRuntime);
                case GlobalRegistryServiceSlot.Input: return nameof(GlobalRegistryServiceSlot.Input);
                case GlobalRegistryServiceSlot.BeaconNetworkRuntime: return nameof(GlobalRegistryServiceSlot.BeaconNetworkRuntime);
                case GlobalRegistryServiceSlot.ModWorldPersistenceRuntime: return nameof(GlobalRegistryServiceSlot.ModWorldPersistenceRuntime);
                case GlobalRegistryServiceSlot.Player: return nameof(GlobalRegistryServiceSlot.Player);
                case GlobalRegistryServiceSlot.PlayerInventory: return nameof(GlobalRegistryServiceSlot.PlayerInventory);
                case GlobalRegistryServiceSlot.PlayerSensory: return nameof(GlobalRegistryServiceSlot.PlayerSensory);
                default: return "Unknown";
            }
        }
    }
}
