using Hecton8.Construction;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Debug/Automation Smoke Tester")]
    public sealed class AutomationSmokeTester : MonoBehaviour
    {
        private const string NativeMemoryOwner = nameof(AutomationSmokeTester);

        [SerializeField] private bool runOnStart;

#pragma warning disable CS0414
        [SerializeField] private bool _debugLastPass;
        [SerializeField] private int _debugRoutedNode;
        [SerializeField] private int _debugDepositedUnits;
#pragma warning restore CS0414

        private void Start()
        {
            if (runOnStart)
                RunSmokePass();
        }

        [ContextMenu("Run Extractor Storage Route Smoke Pass")]
        public void RunSmokePass()
        {
            _debugLastPass = RunExtractorFillsStorageSmoke(out _debugRoutedNode, out _debugDepositedUnits);
        }

        public static bool RunExtractorFillsStorageSmoke(out int routedNode, out int depositedUnits)
        {
            routedNode = -1;
            depositedUnits = 0;

            NativeArray<int> edgeOffsets = AllocateTrackedTempArray<int>(4, nameof(edgeOffsets), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[4] - smoke CSR offsets - owner: AutomationSmokeTester
            NativeArray<int> edgeDestinations = AllocateTrackedTempArray<int>(2, nameof(edgeDestinations), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[2] - smoke CSR destinations - owner: AutomationSmokeTester
            NativeArray<byte> storageCapacityByNode = AllocateTrackedTempArray<byte>(3, nameof(storageCapacityByNode), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[3] - smoke storage flags - owner: AutomationSmokeTester
            NativeArray<byte> visited = AllocateTrackedTempArray<byte>(3, nameof(visited), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[3] - smoke BFS visited set - owner: AutomationSmokeTester
            NativeArray<int> queue = AllocateTrackedTempArray<int>(3, nameof(queue), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[3] - smoke BFS queue - owner: AutomationSmokeTester
            NativeArray<int> result = AllocateTrackedTempArray<int>(1, nameof(result), NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - smoke BFS result cell - owner: AutomationSmokeTester

            try
            {
                edgeOffsets[0] = 0;
                edgeOffsets[1] = 1;
                edgeOffsets[2] = 2;
                edgeOffsets[3] = 2;
                edgeDestinations[0] = 1;
                edgeDestinations[1] = 2;
                storageCapacityByNode[2] = 1;
                result[0] = -1;

                LogisticsPipeRoutingKernel.ExecuteRouteBfs(
                    3,
                    0,
                    edgeOffsets,
                    edgeDestinations,
                    storageCapacityByNode,
                    visited,
                    queue,
                    result);

                routedNode = result[0];
                depositedUnits = routedNode == 2 ? 1 : 0;
                return routedNode == 2 && depositedUnits == 1;
            }
            finally
            {
                DisposeTempArray(ref edgeOffsets);
                DisposeTempArray(ref edgeDestinations);
                DisposeTempArray(ref storageCapacityByNode);
                DisposeTempArray(ref visited);
                DisposeTempArray(ref queue);
                DisposeTempArray(ref result);
            }
        }

        private static NativeArray<T> AllocateTrackedTempArray<T>(int length, string label, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.Temp, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Temp);
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

        private static void DisposeTempArray<T>(ref NativeArray<T> array) where T : struct
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
