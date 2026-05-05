using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    /// <summary>
    /// Stateless CSR routing kernels for logistics pipe traversal.
    /// </summary>
    internal static class LogisticsPipeRoutingKernel
    {
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct RouteBfsJob : IJob
        {
            public int NodeCount;
            public int StartNodeIndex;

            [ReadOnly] public NativeArray<int> EdgeOffsets;
            [ReadOnly] public NativeArray<int> EdgeDestinations;
            [ReadOnly] public NativeArray<byte> StorageCapacityByNode;

            public NativeArray<byte> Visited;
            public NativeArray<int> Queue;
            public NativeArray<int> ResultNodeIndex;

            public void Execute()
            {
                ExecuteRouteBfs(
                    NodeCount,
                    StartNodeIndex,
                    EdgeOffsets,
                    EdgeDestinations,
                    StorageCapacityByNode,
                    Visited,
                    Queue,
                    ResultNodeIndex);
            }
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        internal struct BuildLinearStressGraphJob : IJobParallelFor
        {
            public int NodeCount;
            public int EdgeCount;
            public int StorageNodeIndex;

            public NativeArray<int> EdgeOffsets;
            public NativeArray<int> EdgeDestinations;
            public NativeArray<byte> StorageCapacityByNode;

            public void Execute(int index)
            {
                if (index <= NodeCount)
                    EdgeOffsets[index] = math.min(index, EdgeCount);

                if (index < EdgeCount)
                    EdgeDestinations[index] = index + 1;

                if (index < NodeCount)
                    StorageCapacityByNode[index] = (byte)(index == StorageNodeIndex ? 1 : 0);
            }
        }

        internal static void ExecuteRouteBfs(
            int nodeCount,
            int startNodeIndex,
            NativeArray<int> edgeOffsets,
            NativeArray<int> edgeDestinations,
            NativeArray<byte> storageCapacityByNode,
            NativeArray<byte> visited,
            NativeArray<int> queue,
            NativeArray<int> resultNodeIndex)
        {
            if (!resultNodeIndex.IsCreated || resultNodeIndex.Length <= 0)
                return;

            resultNodeIndex[0] = -1;

            int safeNodeCount = math.min(nodeCount, math.min(storageCapacityByNode.Length, math.min(visited.Length, queue.Length)));
            if (safeNodeCount <= 0 ||
                startNodeIndex < 0 ||
                startNodeIndex >= safeNodeCount ||
                !edgeOffsets.IsCreated ||
                edgeOffsets.Length <= safeNodeCount ||
                !edgeDestinations.IsCreated)
            {
                return;
            }

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                visited[nodeIndex] = 0;

            int head = 0;
            int tail = 0;
            queue[tail++] = startNodeIndex;
            visited[startNodeIndex] = 1;

            while (head < tail)
            {
                int nodeIndex = queue[head++];
                if (storageCapacityByNode[nodeIndex] != 0)
                {
                    resultNodeIndex[0] = nodeIndex;
                    return;
                }

                int edgeStart = edgeOffsets[nodeIndex];
                int edgeEnd = edgeOffsets[nodeIndex + 1];
                if (edgeStart < 0 || edgeEnd < edgeStart || edgeEnd > edgeDestinations.Length)
                    continue;

                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    int destinationNodeIndex = edgeDestinations[edgeIndex];
                    if (destinationNodeIndex < 0 ||
                        destinationNodeIndex >= safeNodeCount ||
                        visited[destinationNodeIndex] != 0)
                    {
                        continue;
                    }

                    visited[destinationNodeIndex] = 1;
                    if (tail >= safeNodeCount)
                        return;

                    queue[tail++] = destinationNodeIndex;
                }
            }
        }
    }
}
