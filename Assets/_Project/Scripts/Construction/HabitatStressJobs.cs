using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal struct HabitatDirtyRegionResult
    {
        public int NodeCount;
        public int DirtySeedCount;
        public int RupturedSeedCount;
        public int IslandCount;
        public int VisitedNodeCount;
        public int ShaderUpdateCount;
        public int QueueOverflow;
    }

    /// <summary>
    /// Burst BFS over only the neighborhoods touched by dirty rupture seeds.
    /// The caller keeps the previous full CSR snapshot alive; this job only rewrites IslandIds for affected islands.
    /// </summary>
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal struct HabitatDirtyRegionRebuildJob : IJob
    {
        public int NodeCount;
        public int DirtyNodeCount;
        public int CurrentVisitStamp;
        public int IslandIdBase;
        public int ShaderUpdateCapacity;

        [ReadOnly] public NativeArray<int> EdgeOffsets;
        [ReadOnly] public NativeArray<int> EdgeDestinations;
        [ReadOnly] public NativeArray<byte> SeveredEdgeMask;
        [ReadOnly] public NativeArray<byte> RupturedNodeMask;
        [ReadOnly] public NativeArray<int> DirtyNodeIndices;

        public NativeArray<int> TraversalQueue;
        public NativeArray<int> VisitStamp;
        public NativeArray<int> IslandIds;
        public NativeArray<HabitatDirtyRegionResult> Result;

        public void Execute()
        {
            HabitatDirtyRegionResult result = new HabitatDirtyRegionResult
            {
                NodeCount = NodeCount,
                DirtySeedCount = math.min(DirtyNodeCount, DirtyNodeIndices.Length)
            };

            if (NodeCount <= 0 ||
                !EdgeOffsets.IsCreated ||
                !EdgeDestinations.IsCreated ||
                !RupturedNodeMask.IsCreated ||
                !DirtyNodeIndices.IsCreated ||
                !TraversalQueue.IsCreated ||
                !VisitStamp.IsCreated ||
                !IslandIds.IsCreated ||
                !Result.IsCreated)
            {
                WriteResult(result);
                return;
            }

            int dirtyCount = math.min(DirtyNodeCount, DirtyNodeIndices.Length);
            int islandOrdinal = 0;
            int visitedCount = 0;
            int queueOverflow = 0;

            for (int dirtyIndex = 0; dirtyIndex < dirtyCount; dirtyIndex++)
            {
                int seedNodeIndex = DirtyNodeIndices[dirtyIndex];
                if (!IsValidNode(seedNodeIndex))
                    continue;

                if (IsNodeRuptured(seedNodeIndex))
                {
                    result.RupturedSeedCount++;
                    int edgeStart = EdgeOffsets[seedNodeIndex];
                    int edgeEnd = EdgeOffsets[seedNodeIndex + 1];
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = EdgeDestinations[edgeIndex];
                        FloodFillIsland(
                            neighborNodeIndex,
                            ref islandOrdinal,
                            ref visitedCount,
                            ref queueOverflow);
                    }

                    continue;
                }

                FloodFillIsland(
                    seedNodeIndex,
                    ref islandOrdinal,
                    ref visitedCount,
                    ref queueOverflow);
            }

            result.IslandCount = islandOrdinal;
            result.VisitedNodeCount = visitedCount;
            result.ShaderUpdateCount = math.min(math.max(0, ShaderUpdateCapacity), visitedCount);
            result.QueueOverflow = queueOverflow;
            WriteResult(result);
        }

        private void FloodFillIsland(
            int startNodeIndex,
            ref int islandOrdinal,
            ref int visitedCount,
            ref int queueOverflow)
        {
            if (!IsValidNode(startNodeIndex) ||
                IsNodeRuptured(startNodeIndex) ||
                VisitStamp[startNodeIndex] == CurrentVisitStamp)
            {
                return;
            }

            int queueCapacity = math.min(TraversalQueue.Length, NodeCount);
            if (queueCapacity <= 0)
            {
                queueOverflow = 1;
                return;
            }

            int islandId = IslandIdBase + islandOrdinal;
            islandOrdinal++;

            int head = 0;
            int tail = 0;
            TraversalQueue[tail++] = startNodeIndex;
            VisitStamp[startNodeIndex] = CurrentVisitStamp;
            IslandIds[startNodeIndex] = islandId;

            while (head < tail)
            {
                int nodeIndex = TraversalQueue[head++];
                visitedCount++;

                int edgeStart = EdgeOffsets[nodeIndex];
                int edgeEnd = EdgeOffsets[nodeIndex + 1];
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    if (IsEdgeSevered(edgeIndex))
                        continue;

                    int neighborNodeIndex = EdgeDestinations[edgeIndex];
                    if (!IsValidNode(neighborNodeIndex) ||
                        IsNodeRuptured(neighborNodeIndex) ||
                        VisitStamp[neighborNodeIndex] == CurrentVisitStamp)
                    {
                        continue;
                    }

                    if (tail >= queueCapacity)
                    {
                        queueOverflow = 1;
                        continue;
                    }

                    VisitStamp[neighborNodeIndex] = CurrentVisitStamp;
                    IslandIds[neighborNodeIndex] = islandId;
                    TraversalQueue[tail++] = neighborNodeIndex;
                }
            }
        }

        private bool IsValidNode(int nodeIndex)
        {
            return nodeIndex >= 0 && nodeIndex < NodeCount;
        }

        private bool IsNodeRuptured(int nodeIndex)
        {
            return nodeIndex >= 0 &&
                   nodeIndex < RupturedNodeMask.Length &&
                   RupturedNodeMask[nodeIndex] != 0;
        }

        private bool IsEdgeSevered(int edgeIndex)
        {
            return SeveredEdgeMask.IsCreated &&
                   edgeIndex >= 0 &&
                   edgeIndex < SeveredEdgeMask.Length &&
                   SeveredEdgeMask[edgeIndex] != 0;
        }

        private void WriteResult(HabitatDirtyRegionResult result)
        {
            if (Result.IsCreated && Result.Length > 0)
                Result[0] = result;
        }
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
    internal struct HabitatWaterlineShaderUpdateJob : IJobParallelFor
    {
        public int NodeCount;

        [ReadOnly] public NativeArray<int> IslandIds;
        [ReadOnly] public NativeArray<float> FloodLevel01;
        [ReadOnly] public NativeArray<float> WaterSurfaceY;
        [ReadOnly] public NativeArray<float> BrownoutFlicker01;
        [ReadOnly] public NativeArray<float> CondensationDepth01;

        [WriteOnly] public NativeArray<float4> ModuleWaterLevels;

        public void Execute(int index)
        {
            if (index < 0 || index >= ModuleWaterLevels.Length || index >= NodeCount)
                return;

            float active01 = IslandIds[index] >= 0 ? 1f : 0f;
            ModuleWaterLevels[index] = new float4(
                WaterSurfaceY[index],
                math.saturate(FloodLevel01[index]) * active01,
                math.saturate(BrownoutFlicker01[index]) * active01,
                math.saturate(CondensationDepth01[index]) * active01);
        }
    }
}
