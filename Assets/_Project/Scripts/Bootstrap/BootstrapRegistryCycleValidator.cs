using System;
using Hecton8.Core;
using UnityEngine;

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
            GlobalRegistryServiceSlot.Input,
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
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PhysicsStateManager, GlobalRegistryServiceSlot.Dispatcher),
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
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Input, GlobalRegistryServiceSlot.Dispatcher),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.Player, GlobalRegistryServiceSlot.Input),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PlayerInventory, GlobalRegistryServiceSlot.Player),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PlayerInventory, GlobalRegistryServiceSlot.ObjectPool),
            new BootstrapRegistryDependencyEdge(GlobalRegistryServiceSlot.PlayerSensory, GlobalRegistryServiceSlot.Player),
        };

        // COLD ALLOC: startup graph validation scratch - owner: BootstrapRegistryCycleValidator
        private static readonly object _validationScratchLock = new object();
        private static readonly GlobalRegistryServiceSlot[] _startupExecutionOrderScratch =
            new GlobalRegistryServiceSlot[_startupNodes.Length];
        private static readonly int[] _inDegreeScratch = new int[_startupNodes.Length];
        private static readonly int[] _queueScratch = new int[_startupNodes.Length];

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

            int queueHead = 0;
            int queueTail = 0;

            for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
            {
                int sourceIndex = IndexOf(nodes, edges[edgeIndex].Source);
                int dependencyIndex = IndexOf(nodes, edges[edgeIndex].Dependency);
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
                    if (IndexOf(nodes, edges[edgeIndex].Dependency) != dependencyIndex)
                        continue;

                    int sourceIndex = IndexOf(nodes, edges[edgeIndex].Source);
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
                case GlobalRegistryServiceSlot.Input: return nameof(GlobalRegistryServiceSlot.Input);
                case GlobalRegistryServiceSlot.Player: return nameof(GlobalRegistryServiceSlot.Player);
                case GlobalRegistryServiceSlot.PlayerInventory: return nameof(GlobalRegistryServiceSlot.PlayerInventory);
                case GlobalRegistryServiceSlot.PlayerSensory: return nameof(GlobalRegistryServiceSlot.PlayerSensory);
                default: return "Unknown";
            }
        }
    }
}
