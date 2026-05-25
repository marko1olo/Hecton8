using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Construction
{
    internal static class HabitatStressJobLayout
    {
        public const int DirtyRegionResultStrideBytes = 32;
        public const int FloodPropagationSummaryStrideBytes = 32;
    }

    [StructLayout(LayoutKind.Explicit, Size = HabitatStressJobLayout.DirtyRegionResultStrideBytes)]
    internal struct HabitatDirtyRegionResult
    {
        [FieldOffset(0)]
        public int NodeCount;
        [FieldOffset(4)]
        public int DirtySeedCount;
        [FieldOffset(8)]
        public int RupturedSeedCount;
        [FieldOffset(12)]
        public int IslandCount;
        [FieldOffset(16)]
        public int VisitedNodeCount;
        [FieldOffset(20)]
        public int ShaderUpdateCount;
        [FieldOffset(24)]
        public int QueueOverflow;
        [FieldOffset(28)]
        private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = HabitatStressJobLayout.FloodPropagationSummaryStrideBytes)]
    internal struct HabitatFloodPropagationSummary
    {
        [FieldOffset(0)]
        public int ProcessedNodeCount;
        [FieldOffset(4)]
        public int FlowedEdgeCount;
        [FieldOffset(8)]
        public int SealedEdgeCount;
        [FieldOffset(12)]
        public int NonFiniteCount;
        [FieldOffset(16)]
        public int InvalidConnectionCount;
        [FieldOffset(20)]
        public float TransferredVolumeM3;
        [FieldOffset(24)]
        public float MaxDeltaLevel01;
        [FieldOffset(28)]
        private uint _pad0;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HabitatFloodPropagationJob : IJob
    {
        public int NodeCount;
        public int EdgeCount;
        public int StartNodeIndex;
        public int ProcessNodeCount;
        public float DeltaTime;
        public float FlowRate01PerSecond;
        public float MaxTransferPerEdgeM3;
        public float WaterEpsilon01;

        [NoAlias, ReadOnly] public NativeArray<float> RoomWaterLevels;
        [NoAlias, ReadOnly] public NativeArray<float> RoomVolumes;
        [NoAlias, ReadOnly] public NativeArray<byte> RoomFlags;
        [NoAlias, ReadOnly] public NativeArray<byte> EdgeFlags;
        [ReadOnly] public NativeParallelMultiHashMap<int, HabitatFloodConnection> Connections;

        [NoAlias] public NativeArray<float> RoomDeltaLevels;
        [NoAlias] public NativeArray<HabitatFloodPropagationSummary> Result;

        public void Execute()
        {
            HabitatFloodPropagationSummary summary = default;
            if (!RoomWaterLevels.IsCreated ||
                !RoomVolumes.IsCreated ||
                !RoomFlags.IsCreated ||
                !RoomDeltaLevels.IsCreated ||
                !Connections.IsCreated ||
                DeltaTime <= 0f ||
                !math.isfinite(DeltaTime))
            {
                WriteResult(summary);
                return;
            }

            int safeNodeCount = math.min(
                math.max(0, NodeCount),
                math.min(RoomWaterLevels.Length, math.min(RoomVolumes.Length, math.min(RoomFlags.Length, RoomDeltaLevels.Length))));
            int safeEdgeCount = EdgeFlags.IsCreated
                ? math.min(math.max(0, EdgeCount), EdgeFlags.Length)
                : 0;
            if (safeNodeCount <= 0)
            {
                WriteResult(summary);
                return;
            }

            for (int nodeIndex = 0; nodeIndex < safeNodeCount; nodeIndex++)
                RoomDeltaLevels[nodeIndex] = 0f;

            int safeProcessCount = math.clamp(ProcessNodeCount, 0, safeNodeCount);
            if (safeProcessCount <= 0)
            {
                WriteResult(summary);
                return;
            }

            int safeStart = safeNodeCount > 0
                ? math.clamp(StartNodeIndex, 0, safeNodeCount - 1)
                : 0;
            float safeDeltaTime = math.max(0f, DeltaTime);
            float safeFlowRate = math.max(0f, FlowRate01PerSecond);
            float epsilon = math.max(0.000001f, WaterEpsilon01);

            for (int offset = 0; offset < safeProcessCount; offset++)
            {
                int sourceIndex = safeStart + offset;
                if (sourceIndex >= safeNodeCount)
                    sourceIndex -= safeNodeCount;

                summary.ProcessedNodeCount++;
                float sourceLevel01 = ResolveSourceAvailableLevel01(sourceIndex);
                if (sourceLevel01 <= epsilon)
                    continue;

                float sourceVolumeM3 = math.max(epsilon, RoomVolumes[sourceIndex]);
                if (!math.isfinite(sourceVolumeM3))
                {
                    summary.NonFiniteCount++;
                    continue;
                }

                NativeParallelMultiHashMapIterator<int> iterator;
                HabitatFloodConnection connection;
                if (!Connections.TryGetFirstValue(sourceIndex, out connection, out iterator))
                    continue;

                do
                {
                    sourceLevel01 = ResolveSourceAvailableLevel01(sourceIndex);
                    if (sourceLevel01 <= epsilon)
                        break;

                    int destinationIndex = connection.DestinationIndex;
                    int edgeIndex = connection.CsrEdgeIndex;
                    if (destinationIndex < 0 ||
                        destinationIndex >= safeNodeCount ||
                        edgeIndex < 0 ||
                        edgeIndex >= safeEdgeCount)
                    {
                        summary.InvalidConnectionCount++;
                        continue;
                    }

                    if (IsConnectionSealed(edgeIndex))
                    {
                        summary.SealedEdgeCount++;
                        continue;
                    }

                    float destinationLevel01 = ResolveDestinationCommittedLevel01(destinationIndex);
                    float levelDelta01 = sourceLevel01 - destinationLevel01;
                    if (levelDelta01 <= epsilon || !math.isfinite(levelDelta01))
                        continue;

                    float destinationVolumeM3 = math.max(epsilon, RoomVolumes[destinationIndex]);
                    if (!math.isfinite(destinationVolumeM3))
                    {
                        summary.NonFiniteCount++;
                        continue;
                    }

                    float resistance = math.max(0.1f, connection.FlowResistance);
                    float transferLevel01 = math.min(
                        sourceLevel01,
                        levelDelta01 * safeFlowRate * safeDeltaTime * math.rcp(resistance));
                    if (transferLevel01 <= epsilon || !math.isfinite(transferLevel01))
                        continue;

                    float sourceBudgetM3 = math.min(sourceLevel01 * sourceVolumeM3, transferLevel01 * sourceVolumeM3);
                    float destinationCapacityM3 = math.max(0f, (1f - destinationLevel01) * destinationVolumeM3);
                    float maxTransferM3 = math.max(0f, MaxTransferPerEdgeM3);
                    float transferM3 = math.min(math.min(sourceBudgetM3, destinationCapacityM3), maxTransferM3);
                    if (transferM3 <= epsilon || !math.isfinite(transferM3))
                        continue;

                    float sourceDelta01 = transferM3 * math.rcp(sourceVolumeM3);
                    float destinationDelta01 = transferM3 * math.rcp(destinationVolumeM3);
                    RoomDeltaLevels[sourceIndex] -= sourceDelta01;
                    RoomDeltaLevels[destinationIndex] += destinationDelta01;
                    summary.FlowedEdgeCount++;
                    summary.TransferredVolumeM3 += transferM3;
                    summary.MaxDeltaLevel01 = math.max(
                        summary.MaxDeltaLevel01,
                        math.max(math.abs(sourceDelta01), math.abs(destinationDelta01)));
                }
                while (Connections.TryGetNextValue(out connection, ref iterator));
            }

            WriteResult(summary);
        }

        private float ResolveSourceAvailableLevel01(int roomIndex)
        {
            if (roomIndex < 0 ||
                roomIndex >= RoomWaterLevels.Length ||
                roomIndex >= RoomDeltaLevels.Length)
            {
                return 0f;
            }

            float pendingOutgoingOnly01 = math.min(0f, RoomDeltaLevels[roomIndex]);
            float level01 = RoomWaterLevels[roomIndex] + pendingOutgoingOnly01;
            return math.isfinite(level01) ? math.saturate(level01) : 0f;
        }

        private float ResolveDestinationCommittedLevel01(int roomIndex)
        {
            if (roomIndex < 0 ||
                roomIndex >= RoomWaterLevels.Length ||
                roomIndex >= RoomDeltaLevels.Length)
            {
                return 1f;
            }

            float level01 = RoomWaterLevels[roomIndex] + RoomDeltaLevels[roomIndex];
            return math.isfinite(level01) ? math.saturate(level01) : 0f;
        }

        private bool IsConnectionSealed(int edgeIndex)
        {
            return edgeIndex >= 0 &&
                   edgeIndex < EdgeFlags.Length &&
                   (EdgeFlags[edgeIndex] & (byte)HabitatEdgeFloodFlags.Sealed) != 0;
        }

        private void WriteResult(HabitatFloodPropagationSummary summary)
        {
            if (Result.IsCreated && Result.Length > 0)
                Result[0] = summary;
        }
    }

    /// <summary>
    /// Burst BFS over only the neighborhoods touched by dirty rupture seeds.
    /// The caller keeps the previous full CSR snapshot alive; this job only rewrites IslandIds for affected islands.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HabitatDirtyRegionRebuildJob : IJob
    {
        public int NodeCount;
        public int DirtyNodeCount;
        public int CurrentVisitStamp;
        public int IslandIdBase;
        public int ShaderUpdateCapacity;

        [NoAlias, ReadOnly] public NativeArray<int> EdgeOffsets;
        [NoAlias, ReadOnly] public NativeArray<int> EdgeDestinations;
        [NoAlias, ReadOnly] public NativeArray<byte> SeveredEdgeMask;
        [NoAlias, ReadOnly] public NativeArray<byte> RupturedNodeMask;
        [NoAlias, ReadOnly] public NativeArray<int> DirtyNodeIndices;

        [NoAlias] public NativeArray<int> TraversalQueue;
        [NoAlias] public NativeArray<int> VisitStamp;
        [NoAlias] public NativeArray<int> IslandIds;
        [NoAlias] public NativeArray<HabitatDirtyRegionResult> Result;

        public void Execute()
        {
            HabitatDirtyRegionResult result = new HabitatDirtyRegionResult
            {
                NodeCount = math.max(0, NodeCount),
                DirtySeedCount = math.min(DirtyNodeCount, DirtyNodeIndices.Length)
            };

            if (!EdgeOffsets.IsCreated ||
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

            int edgeOffsetNodeCapacity = math.max(0, EdgeOffsets.Length - 1);
            int safeNodeCount = math.max(0, math.min(
                math.max(0, NodeCount),
                math.min(VisitStamp.Length, math.min(IslandIds.Length, math.min(RupturedNodeMask.Length, edgeOffsetNodeCapacity)))));
            int safeEdgeCount = math.max(0, EdgeDestinations.Length);
            result.NodeCount = safeNodeCount;
            if (safeNodeCount <= 0)
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
                if (!IsValidNode(seedNodeIndex, safeNodeCount))
                    continue;

                if (IsNodeRuptured(seedNodeIndex))
                {
                    result.RupturedSeedCount++;
                    int edgeStart = ResolveEdgeStart(seedNodeIndex, safeEdgeCount);
                    int edgeEnd = ResolveEdgeEnd(seedNodeIndex, edgeStart, safeEdgeCount);
                    for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                    {
                        int neighborNodeIndex = EdgeDestinations[edgeIndex];
                        FloodFillIsland(
                            neighborNodeIndex,
                            safeNodeCount,
                            safeEdgeCount,
                            ref islandOrdinal,
                            ref visitedCount,
                            ref queueOverflow);
                    }

                    continue;
                }

                FloodFillIsland(
                    seedNodeIndex,
                    safeNodeCount,
                    safeEdgeCount,
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
            int safeNodeCount,
            int safeEdgeCount,
            ref int islandOrdinal,
            ref int visitedCount,
            ref int queueOverflow)
        {
            if (!IsValidNode(startNodeIndex, safeNodeCount) ||
                IsNodeRuptured(startNodeIndex) ||
                VisitStamp[startNodeIndex] == CurrentVisitStamp)
            {
                return;
            }

            int queueCapacity = math.min(TraversalQueue.Length, safeNodeCount);
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

                int edgeStart = ResolveEdgeStart(nodeIndex, safeEdgeCount);
                int edgeEnd = ResolveEdgeEnd(nodeIndex, edgeStart, safeEdgeCount);
                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    if (IsEdgeSevered(edgeIndex))
                        continue;

                    int neighborNodeIndex = EdgeDestinations[edgeIndex];
                    if (!IsValidNode(neighborNodeIndex, safeNodeCount) ||
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

        private bool IsValidNode(int nodeIndex, int safeNodeCount)
        {
            return nodeIndex >= 0 && nodeIndex < safeNodeCount;
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

        private int ResolveEdgeStart(int nodeIndex, int safeEdgeCount)
        {
            if (nodeIndex < 0 || nodeIndex >= EdgeOffsets.Length - 1)
                return 0;

            return math.clamp(EdgeOffsets[nodeIndex], 0, safeEdgeCount);
        }

        private int ResolveEdgeEnd(int nodeIndex, int edgeStart, int safeEdgeCount)
        {
            if (nodeIndex < 0 || nodeIndex >= EdgeOffsets.Length - 1)
                return edgeStart;

            return math.clamp(EdgeOffsets[nodeIndex + 1], edgeStart, safeEdgeCount);
        }

        private void WriteResult(HabitatDirtyRegionResult result)
        {
            if (Result.IsCreated && Result.Length > 0)
                Result[0] = result;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct HabitatWaterlineShaderUpdateJob : IJobParallelFor
    {
        public int NodeCount;

        [NoAlias, ReadOnly] public NativeArray<int> IslandIds;
        [NoAlias, ReadOnly] public NativeArray<float> FloodLevel01;
        [NoAlias, ReadOnly] public NativeArray<float> WaterSurfaceY;
        [NoAlias, ReadOnly] public NativeArray<float> BrownoutFlicker01;
        [NoAlias, ReadOnly] public NativeArray<float> CondensationDepth01;

        [NoAlias, WriteOnly] public NativeArray<float4> ModuleWaterLevels;

        public void Execute(int index)
        {
            if (index < 0 ||
                index >= NodeCount ||
                index >= ModuleWaterLevels.Length ||
                index >= IslandIds.Length ||
                index >= FloodLevel01.Length ||
                index >= WaterSurfaceY.Length ||
                index >= BrownoutFlicker01.Length ||
                index >= CondensationDepth01.Length)
            {
                return;
            }

            float active01 = IslandIds[index] >= 0 ? 1f : 0f;
            ModuleWaterLevels[index] = new float4(
                WaterSurfaceY[index],
                math.saturate(FloodLevel01[index]) * active01,
                math.saturate(BrownoutFlicker01[index]) * active01,
                math.saturate(CondensationDepth01[index]) * active01);
        }
    }
}
