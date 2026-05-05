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

            NativeArray<int> edgeOffsets = new NativeArray<int>(4, Allocator.Temp, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[4] - smoke CSR offsets - owner: AutomationSmokeTester
            NativeArray<int> edgeDestinations = new NativeArray<int>(2, Allocator.Temp, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[2] - smoke CSR destinations - owner: AutomationSmokeTester
            NativeArray<byte> storageCapacityByNode = new NativeArray<byte>(3, Allocator.Temp, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[3] - smoke storage flags - owner: AutomationSmokeTester
            NativeArray<byte> visited = new NativeArray<byte>(3, Allocator.Temp, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[3] - smoke BFS visited set - owner: AutomationSmokeTester
            NativeArray<int> queue = new NativeArray<int>(3, Allocator.Temp, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[3] - smoke BFS queue - owner: AutomationSmokeTester
            NativeArray<int> result = new NativeArray<int>(1, Allocator.Temp, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[1] - smoke BFS result cell - owner: AutomationSmokeTester
            RegisterTempArray(edgeOffsets, nameof(edgeOffsets));
            RegisterTempArray(edgeDestinations, nameof(edgeDestinations));
            RegisterTempArray(storageCapacityByNode, nameof(storageCapacityByNode));
            RegisterTempArray(visited, nameof(visited));
            RegisterTempArray(queue, nameof(queue));
            RegisterTempArray(result, nameof(result));

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

        private static void RegisterTempArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.Temp);
        }

        private static void DisposeTempArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
