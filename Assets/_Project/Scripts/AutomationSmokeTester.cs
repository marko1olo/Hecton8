using Hecton8.Construction;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Hecton8.Debugging
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Debug/Automation Smoke Tester")]
    public sealed class AutomationSmokeTester : MonoBehaviour
    {
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

            NativeArray<int> edgeOffsets = new NativeArray<int>(4, Allocator.Temp, NativeArrayOptions.ClearMemory);
            NativeArray<int> edgeDestinations = new NativeArray<int>(2, Allocator.Temp, NativeArrayOptions.ClearMemory);
            NativeArray<byte> storageCapacityByNode = new NativeArray<byte>(3, Allocator.Temp, NativeArrayOptions.ClearMemory);
            NativeArray<byte> visited = new NativeArray<byte>(3, Allocator.Temp, NativeArrayOptions.ClearMemory);
            NativeArray<int> queue = new NativeArray<int>(3, Allocator.Temp, NativeArrayOptions.ClearMemory);
            NativeArray<int> result = new NativeArray<int>(1, Allocator.Temp, NativeArrayOptions.ClearMemory);

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

                new BaseLogisticsNetwork.LogisticsPipeRouteBfsJob
                {
                    NodeCount = 3,
                    StartNodeIndex = 0,
                    EdgeOffsets = edgeOffsets,
                    EdgeDestinations = edgeDestinations,
                    StorageCapacityByNode = storageCapacityByNode,
                    Visited = visited,
                    Queue = queue,
                    ResultNodeIndex = result
                }.Run();

                routedNode = result[0];
                depositedUnits = routedNode == 2 ? 1 : 0;
                return routedNode == 2 && depositedUnits == 1;
            }
            finally
            {
                edgeOffsets.Dispose();
                edgeDestinations.Dispose();
                storageCapacityByNode.Dispose();
                visited.Dispose();
                queue.Dispose();
                result.Dispose();
            }
        }
    }
}
