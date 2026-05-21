using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Debugging
{
    public struct AutomationOmegaSmokeResult
    {
        public byte Passed;
        public int NodeCount;
        public int EdgeCount;
        public int RoutedNode;
        public int NoStorageRouteNode;
        public int InvalidStartRouteNode;
        public int ExpectedStorageNode;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Debug/Automation Omega Smoke Tester")]
    public sealed class AutomationOmegaSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(AutomationOmegaSmokeTester);
        private const int StressNodeCount = 64;

        [ContextMenu("Run Omega Logistics Route Stress Smoke")]
        public void RunSmokePass()
        {
            RunLogisticsRouteStressSmoke();
        }

        public static AutomationOmegaSmokeResult RunLogisticsRouteStressSmoke()
        {
            int nodeCount = StressNodeCount;
            int edgeCount = nodeCount - 1;
            int storageNodeIndex = nodeCount - 1;

            AutomationOmegaSmokeResult smokeResult = new AutomationOmegaSmokeResult
            {
                Passed = 0,
                NodeCount = nodeCount,
                EdgeCount = edgeCount,
                RoutedNode = -1,
                NoStorageRouteNode = -1,
                InvalidStartRouteNode = -1,
                ExpectedStorageNode = storageNodeIndex
            };

            NativeArray<int> edgeOffsets = new NativeArray<int>(nodeCount + 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[65] - omega smoke CSR offsets - owner: AutomationOmegaSmokeTester
            NativeArray<int> edgeDestinations = new NativeArray<int>(edgeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[63] - omega smoke CSR destinations - owner: AutomationOmegaSmokeTester
            NativeArray<byte> storageCapacityByNode = new NativeArray<byte>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[64] - omega smoke storage flags - owner: AutomationOmegaSmokeTester
            NativeArray<byte> visited = new NativeArray<byte>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[64] - omega smoke BFS visited set - owner: AutomationOmegaSmokeTester
            NativeArray<int> queue = new NativeArray<int>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[64] - omega smoke BFS queue - owner: AutomationOmegaSmokeTester
            NativeArray<int> routeResult = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - omega smoke BFS result cell - owner: AutomationOmegaSmokeTester
            RegisterTempJobArray(edgeOffsets, nameof(edgeOffsets));
            RegisterTempJobArray(edgeDestinations, nameof(edgeDestinations));
            RegisterTempJobArray(storageCapacityByNode, nameof(storageCapacityByNode));
            RegisterTempJobArray(visited, nameof(visited));
            RegisterTempJobArray(queue, nameof(queue));
            RegisterTempJobArray(routeResult, nameof(routeResult));

            try
            {
                JobHandle buildHandle = new LogisticsPipeRoutingKernel.BuildLinearStressGraphJob
                {
                    NodeCount = nodeCount,
                    EdgeCount = edgeCount,
                    StorageNodeIndex = storageNodeIndex,
                    EdgeOffsets = edgeOffsets,
                    EdgeDestinations = edgeDestinations,
                    StorageCapacityByNode = storageCapacityByNode
                }.Schedule(nodeCount + 1, 16);

                // COLD SYNC JOB: batch smoke must inspect the generated deterministic CSR before process exit.
                Hecton8.World.DispatcherJobSwap.TryComplete(ref buildHandle, forceComplete: true);

                LogisticsPipeRoutingKernel.ExecuteRouteBfs(
                    nodeCount,
                    0,
                    edgeOffsets,
                    edgeDestinations,
                    storageCapacityByNode,
                    visited,
                    queue,
                    routeResult);
                smokeResult.RoutedNode = routeResult[0];

                storageCapacityByNode[storageNodeIndex] = 0;
                LogisticsPipeRoutingKernel.ExecuteRouteBfs(
                    nodeCount,
                    0,
                    edgeOffsets,
                    edgeDestinations,
                    storageCapacityByNode,
                    visited,
                    queue,
                    routeResult);
                smokeResult.NoStorageRouteNode = routeResult[0];

                LogisticsPipeRoutingKernel.ExecuteRouteBfs(
                    nodeCount,
                    -1,
                    edgeOffsets,
                    edgeDestinations,
                    storageCapacityByNode,
                    visited,
                    queue,
                    routeResult);
                smokeResult.InvalidStartRouteNode = routeResult[0];

                smokeResult.Passed =
                    smokeResult.RoutedNode == storageNodeIndex &&
                    smokeResult.NoStorageRouteNode == -1 &&
                    smokeResult.InvalidStartRouteNode == -1
                        ? (byte)1
                        : (byte)0;

                return smokeResult;
            }
            finally
            {
                DisposeTempJobArray(ref edgeOffsets);
                DisposeTempJobArray(ref edgeDestinations);
                DisposeTempJobArray(ref storageCapacityByNode);
                DisposeTempJobArray(ref visited);
                DisposeTempJobArray(ref queue);
                DisposeTempJobArray(ref routeResult);
            }
        }

        private static void RegisterTempJobArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTempJobArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
