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

            NativeArray<int> edgeOffsets = AllocateTrackedTempJobArray<int>(nodeCount + 1, nameof(edgeOffsets), NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[65] - omega smoke CSR offsets - owner: AutomationOmegaSmokeTester
            NativeArray<int> edgeDestinations = AllocateTrackedTempJobArray<int>(edgeCount, nameof(edgeDestinations), NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[63] - omega smoke CSR destinations - owner: AutomationOmegaSmokeTester
            NativeArray<byte> storageCapacityByNode = AllocateTrackedTempJobArray<byte>(nodeCount, nameof(storageCapacityByNode), NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[64] - omega smoke storage flags - owner: AutomationOmegaSmokeTester
            NativeArray<byte> visited = AllocateTrackedTempJobArray<byte>(nodeCount, nameof(visited), NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<byte>[64] - omega smoke BFS visited set - owner: AutomationOmegaSmokeTester
            NativeArray<int> queue = AllocateTrackedTempJobArray<int>(nodeCount, nameof(queue), NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[64] - omega smoke BFS queue - owner: AutomationOmegaSmokeTester
            NativeArray<int> routeResult = AllocateTrackedTempJobArray<int>(1, nameof(routeResult), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - omega smoke BFS result cell - owner: AutomationOmegaSmokeTester

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

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, string label, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
                if (sentinelId > 0)
                    return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();

                throw;
            }

            array.Dispose();
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void DisposeTempJobArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }
    }
}
