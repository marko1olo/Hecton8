using System;
using System.Collections.Generic;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        private struct PoolBlock
        {
            public int Offset;
            public int Length;
        }

        private struct NativeChunkPool : IDisposable
        {
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<HectonVegetationInstanceData> Metadata;
            public NativeArray<int> Types;
            public NativeArray<int> SemanticTypes;
            public NativeArray<byte> BiomeLayers;
            public NativeArray<float> EdgeDistances;
            public NativeArray<Vector2> FlowDirections;
            public NativeArray<Vector3> FlowVectors;
            public int Capacity;
            public bool IsCreated => Matrices.IsCreated ||
                                     Metadata.IsCreated ||
                                     Types.IsCreated ||
                                     SemanticTypes.IsCreated ||
                                     BiomeLayers.IsCreated ||
                                     EdgeDistances.IsCreated ||
                                     FlowDirections.IsCreated ||
                                     FlowVectors.IsCreated;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DisposeNativeArray(ref Matrices, dependency);
                DisposeNativeArray(ref Metadata, dependency);
                DisposeNativeArray(ref Types, dependency);
                DisposeNativeArray(ref SemanticTypes, dependency);
                DisposeNativeArray(ref BiomeLayers, dependency);
                DisposeNativeArray(ref EdgeDistances, dependency);
                DisposeNativeArray(ref FlowDirections, dependency);
                DisposeNativeArray(ref FlowVectors, dependency);
                Capacity = 0;
            }
        }

        private struct ActiveAggregateNativeBufferSet : IDisposable
        {
            public NativeArray<Matrix4x4> Matrices;
            public NativeArray<HectonVegetationInstanceData> Metadata;
            public NativeArray<int> Types;
            public NativeArray<int> SemanticTypes;
            public NativeArray<byte> BiomeLayers;
            public NativeArray<Vector2> FlowDirections;
            public NativeArray<Vector3> FlowVectors;
            public bool IsCreated => Matrices.IsCreated ||
                                     Metadata.IsCreated ||
                                     Types.IsCreated ||
                                     SemanticTypes.IsCreated ||
                                     BiomeLayers.IsCreated ||
                                     FlowDirections.IsCreated ||
                                     FlowVectors.IsCreated;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DisposeNativeArray(ref Matrices, dependency);
                DisposeNativeArray(ref Metadata, dependency);
                DisposeNativeArray(ref Types, dependency);
                DisposeNativeArray(ref SemanticTypes, dependency);
                DisposeNativeArray(ref BiomeLayers, dependency);
                DisposeNativeArray(ref FlowDirections, dependency);
                DisposeNativeArray(ref FlowVectors, dependency);
            }
        }

        private struct VegetationNativeMemory : IDisposable
        {
            public NativeArray<VegetationDensityChunkRecord> DensityQueryChunksNative;
            public NativeArray<float3> DensityQueryGridNative;
            public NativeArray<float2> ThreatAttractorGridNative;
            public NativeArray<VegetationDensityChunkRecord> DensityQueryChunksScratchNative;
            public NativeArray<float3> DensityQueryGridScratchNative;
            public NativeArray<float2> ThreatAttractorGridScratchNative;
            public NativeArray<VegetationDensityChunkRecord> ThreatSamplingChunksNative;
            public NativeArray<float2> ThreatSamplingAttractorGridNative;
            public NativeArray<float3> FlowSamplingDensityGridNative;
            public NativeArray<float> FlowNavSupportGridNative;
            public NativeArray<float> EcosystemThreatGridCurrentNative;
            public NativeArray<float> EcosystemThreatGridNextNative;
            public NativeArray<byte> EcosystemThreatGridCompressedCurrentNative;
            public NativeArray<byte> EcosystemThreatGridCompressedNextNative;
            public NativeArray<byte> EcosystemThreatVoxelCurrentNative;
            public NativeArray<byte> EcosystemThreatVoxelNextNative;
            public NativeArray<byte> EcosystemThreatEchoCurrentNative;
            public NativeArray<byte> EcosystemThreatEchoNextNative;
            public NativeArray<float2> EcosystemFlowFieldCurrentNative;
            public NativeArray<float2> EcosystemFlowFieldNextNative;
            public NativeArray<SwarmWakeImpulse> SwarmWakeImpulseNative;
            public NativeArray<float> AbyssalThermalGridNative;
            public NativeArray<float> AbyssalThermalGridNextNative;
            public NativeArray<float3> AbyssalFlowVolumeCurrentNative;
            public NativeArray<float3> AbyssalFlowVolumeNextNative;
            public NativeArray<float> CanopyHeightGridNative;
            public NativeArray<TerrainHoleRecord> TerrainHoleRecordsNative;
            public NativeArray<TerrainHoleStreamingRecord> TerrainHoleStreamingRecordsNative;
            public NativeArray<ArtificialStructureRecord> ArtificialStructureRecordsNative;
            public NativeParallelMultiHashMap<int, int> ArtificialStructureHashFrontNative;
            public NativeParallelMultiHashMap<int, int> ArtificialStructureHashBackNative;
            public NativeParallelMultiHashMap<int, int> ThreatSamplingChunkHashFrontNative;
            public NativeParallelMultiHashMap<int, int> ThreatSamplingChunkHashBackNative;
            public NativeArray<Vector3> AbyssalAnchorPositionsNative;
            public NativeArray<Vector3> AbyssalNavNodeSnapshotNative;
            public NativeArray<Vector3> AbyssalNavConduitVectorsSnapshotNative;
            public NativeArray<float> AbyssalNavConduitStrengthSnapshotNative;
            public NativeArray<byte> AbyssalNavNodeTypesSnapshotNative;
            public NativeParallelMultiHashMap<int, int> AbyssalNavGraphHashNative;
            public NativeList<Vector3> AbyssalNavNodes;
            public NativeArray<Vector3> AbyssalPathSnapshotNative;
            public NativeList<Vector3> AbyssalPathRawResultNative;
            public NativeList<Vector3> AbyssalPathResultNative;
            public NativeArray<int> AbyssalPathParentsNative;
            public NativeArray<float> AbyssalPathGScoreNative;
            public NativeArray<float> AbyssalPathFScoreNative;
            public NativeArray<byte> AbyssalPathClosedFlagsNative;
            public NativeArray<int> AbyssalPathHeapNodesNative;
            public NativeArray<int> AbyssalPathHeapPositionsNative;
            public NativeArray<PredatorFearNodeSnapshot> PredatorFearNodesSnapshotNative;
            public NativeArray<HLODData> HlodRegistrySnapshotNative;
            public NativeArray<HLODData> VisibleHlodSnapshotNative;
            public NativeArray<byte> HlodVisibleFlagsNative;
            public NativeArray<float4> HlodFrustumPlanesNative;
            public NativeArray<ChunkSliceMoveRecord> SurfaceDefragMovesNative;
            public NativeArray<ChunkSliceMoveRecord> UnderwaterDefragMovesNative;
            public NativeArray<ActiveAggregateCopyRecord> SurfaceAggregateCopyRecordsNative;
            public NativeArray<ActiveAggregateCopyRecord> UnderwaterAggregateCopyRecordsNative;
            public NativeArray<MegaWreckStreamSection> MegaWreckStreamSnapshotNative;
            public bool IsCreated => DensityQueryChunksNative.IsCreated ||
                                     DensityQueryGridNative.IsCreated ||
                                     ThreatAttractorGridNative.IsCreated ||
                                     EcosystemThreatGridCurrentNative.IsCreated ||
                                     EcosystemThreatGridNextNative.IsCreated ||
                                     AbyssalFlowVolumeCurrentNative.IsCreated ||
                                     AbyssalFlowVolumeNextNative.IsCreated ||
                                     HlodRegistrySnapshotNative.IsCreated ||
                                     MegaWreckStreamSnapshotNative.IsCreated;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DisposeNativeArray(ref DensityQueryChunksNative, dependency);
                DisposeNativeArray(ref DensityQueryGridNative, dependency);
                DisposeNativeArray(ref ThreatAttractorGridNative, dependency);
                DisposeNativeArray(ref DensityQueryChunksScratchNative, dependency);
                DisposeNativeArray(ref DensityQueryGridScratchNative, dependency);
                DisposeNativeArray(ref ThreatAttractorGridScratchNative, dependency);
                DisposeNativeArray(ref ThreatSamplingChunksNative, dependency);
                DisposeNativeArray(ref ThreatSamplingAttractorGridNative, dependency);
                DisposeNativeArray(ref FlowSamplingDensityGridNative, dependency);
                DisposeNativeArray(ref FlowNavSupportGridNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridNextNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridCompressedCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatGridCompressedNextNative, dependency);
                DisposeNativeArray(ref EcosystemThreatVoxelCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatVoxelNextNative, dependency);
                DisposeNativeArray(ref EcosystemThreatEchoCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemThreatEchoNextNative, dependency);
                DisposeNativeArray(ref EcosystemFlowFieldCurrentNative, dependency);
                DisposeNativeArray(ref EcosystemFlowFieldNextNative, dependency);
                DisposeNativeArray(ref SwarmWakeImpulseNative, dependency);
                DisposeNativeArray(ref AbyssalThermalGridNative, dependency);
                DisposeNativeArray(ref AbyssalThermalGridNextNative, dependency);
                DisposeNativeArray(ref AbyssalFlowVolumeCurrentNative, dependency);
                DisposeNativeArray(ref AbyssalFlowVolumeNextNative, dependency);
                DisposeNativeArray(ref CanopyHeightGridNative, dependency);
                DisposeNativeArray(ref TerrainHoleRecordsNative, dependency);
                DisposeNativeArray(ref TerrainHoleStreamingRecordsNative, dependency);
                DisposeNativeArray(ref ArtificialStructureRecordsNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ArtificialStructureHashFrontNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ArtificialStructureHashBackNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ThreatSamplingChunkHashFrontNative, dependency);
                DisposeNativeParallelMultiHashMap(ref ThreatSamplingChunkHashBackNative, dependency);
                DisposeNativeArray(ref AbyssalAnchorPositionsNative, dependency);
                DisposeNativeArray(ref AbyssalNavNodeSnapshotNative, dependency);
                DisposeNativeArray(ref AbyssalNavConduitVectorsSnapshotNative, dependency);
                DisposeNativeArray(ref AbyssalNavConduitStrengthSnapshotNative, dependency);
                DisposeNativeArray(ref AbyssalNavNodeTypesSnapshotNative, dependency);
                DisposeNativeParallelMultiHashMap(ref AbyssalNavGraphHashNative, dependency);
                DisposeNativeList(ref AbyssalNavNodes, dependency);
                DisposeNativeArray(ref AbyssalPathSnapshotNative, dependency);
                DisposeNativeList(ref AbyssalPathRawResultNative, dependency);
                DisposeNativeList(ref AbyssalPathResultNative, dependency);
                DisposeNativeArray(ref AbyssalPathParentsNative, dependency);
                DisposeNativeArray(ref AbyssalPathGScoreNative, dependency);
                DisposeNativeArray(ref AbyssalPathFScoreNative, dependency);
                DisposeNativeArray(ref AbyssalPathClosedFlagsNative, dependency);
                DisposeNativeArray(ref AbyssalPathHeapNodesNative, dependency);
                DisposeNativeArray(ref AbyssalPathHeapPositionsNative, dependency);
                DisposeNativeArray(ref PredatorFearNodesSnapshotNative, dependency);
                DisposeNativeArray(ref HlodRegistrySnapshotNative, dependency);
                DisposeNativeArray(ref VisibleHlodSnapshotNative, dependency);
                DisposeNativeArray(ref HlodVisibleFlagsNative, dependency);
                DisposeNativeArray(ref HlodFrustumPlanesNative, dependency);
                DisposeNativeArray(ref SurfaceDefragMovesNative, dependency);
                DisposeNativeArray(ref UnderwaterDefragMovesNative, dependency);
                DisposeNativeArray(ref SurfaceAggregateCopyRecordsNative, dependency);
                DisposeNativeArray(ref UnderwaterAggregateCopyRecordsNative, dependency);
                DisposeNativeArray(ref MegaWreckStreamSnapshotNative, dependency);
            }
        }

        private float ComputeNativePoolFragmentationPercent()
        {
            float surfacePercent = ComputePoolFragmentationPercent(_surfacePoolFreeBlocks, _surfacePoolFreeBlockCount);
            float underwaterPercent = ComputePoolFragmentationPercent(_underwaterPoolFreeBlocks, _underwaterPoolFreeBlockCount);
            int surfaceCapacity = Mathf.Max(1, _surfaceChunkPool.Capacity);
            int underwaterCapacity = Mathf.Max(1, _underwaterChunkPool.Capacity);
            return ((surfacePercent * surfaceCapacity) + (underwaterPercent * underwaterCapacity)) / (surfaceCapacity + underwaterCapacity);
        }

        private long ComputeTileCacheUsedBytes()
        {
            long bytes = 0L;
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null)
                    continue;

                bytes += GetTileCacheBufferBytes(state.PrimaryCacheBuffer);
                bytes += GetTileCacheBufferBytes(state.SecondaryCacheBuffer);
            }

            return bytes;
        }

        private static long GetTileCacheBufferBytes(TileNativeCacheBuffer buffer)
        {
            long bytes = 0L;
            if (buffer.SandMaskNative.IsCreated)
                bytes += buffer.SandMaskNative.Length;
            if (buffer.RockMaskNative.IsCreated)
                bytes += buffer.RockMaskNative.Length;
            if (buffer.HeightSamplesNative.IsCreated)
                bytes += (long)buffer.HeightSamplesNative.Length * sizeof(ushort);

            return bytes;
        }

        private static float ComputePoolFragmentationPercent(PoolBlock[] freeBlocks, int freeBlockCount)
        {
            if (freeBlocks == null || freeBlockCount <= 1)
                return 0f;

            int totalFree = 0;
            int largestFree = 0;
            for (int i = 0; i < freeBlockCount; i++)
            {
                int length = Mathf.Max(0, freeBlocks[i].Length);
                totalFree += length;
                if (length > largestFree)
                    largestFree = length;
            }

            if (totalFree <= 0)
                return 0f;

            return (1f - ((float)largestFree / totalFree)) * 100f;
        }

        private void UpdateNativePoolDefragState(float dt)
        {
            if (dt <= 0f || playerTransform == null)
            {
                _idleNativePoolTimer = 0f;
                return;
            }

            Vector2 planarVelocity = new Vector2(_playerVelocity.x, _playerVelocity.z);
            if (planarVelocity.sqrMagnitude <= (nativePoolDefragIdleSpeedThreshold * nativePoolDefragIdleSpeedThreshold))
            {
                _idleNativePoolTimer += dt;
                return;
            }

            _idleNativePoolTimer = 0f;
        }

        private void TryScheduleNativePoolDefrag()
        {
            if (_poolDefragScheduled ||
                _idleNativePoolTimer < nativePoolDefragIdleSeconds ||
                _chunkBuildJobs.Count > 0 ||
                _activeSetDirty ||
                ComputeNativePoolFragmentationPercent() < nativePoolDefragThresholdPercent)
            {
                return;
            }

            _surfaceDefragMoveCount = BuildPoolDefragPlan(_surfaceChunkPool, isSurface: true, ref _surfaceDefragKeys, ref _surfaceDefragOffsets, ref _nativeMemory.SurfaceDefragMovesNative, out _surfaceDefragCompactUsedCount);
            _underwaterDefragMoveCount = BuildPoolDefragPlan(_underwaterChunkPool, isSurface: false, ref _underwaterDefragKeys, ref _underwaterDefragOffsets, ref _nativeMemory.UnderwaterDefragMovesNative, out _underwaterDefragCompactUsedCount);
            if (_surfaceDefragMoveCount <= 0 && _underwaterDefragMoveCount <= 0)
                return;

            EnsureDefragScratchPoolCapacity(ref _surfaceDefragScratchPool, _surfaceChunkPool.Capacity);
            EnsureDefragScratchPoolCapacity(ref _underwaterDefragScratchPool, _underwaterChunkPool.Capacity);
            InitializeDefragScratchFreeList(
                ref _surfaceDefragScratchFreeBlocks,
                ref _surfaceDefragScratchFreeBlockCount,
                _surfaceDefragScratchPool.Capacity,
                _surfaceDefragCompactUsedCount);
            InitializeDefragScratchFreeList(
                ref _underwaterDefragScratchFreeBlocks,
                ref _underwaterDefragScratchFreeBlockCount,
                _underwaterDefragScratchPool.Capacity,
                _underwaterDefragCompactUsedCount);

            _surfacePoolDefragHandle = SchedulePoolDefrag(_surfaceChunkPool, _surfaceDefragScratchPool, _nativeMemory.SurfaceDefragMovesNative, _surfaceDefragMoveCount);
            _underwaterPoolDefragHandle = SchedulePoolDefrag(_underwaterChunkPool, _underwaterDefragScratchPool, _nativeMemory.UnderwaterDefragMovesNative, _underwaterDefragMoveCount);
            _poolDefragScheduled = true;
            _idleNativePoolTimer = 0f;
        }

        private void CompleteNativePoolDefragIfReady(bool forceComplete)
        {
            if (!_poolDefragScheduled)
                return;

            bool surfaceReady = _surfaceDefragMoveCount <= 0 || _surfacePoolDefragHandle.IsCompleted;
            bool underwaterReady = _underwaterDefragMoveCount <= 0 || _underwaterPoolDefragHandle.IsCompleted;
            if (!forceComplete && (!surfaceReady || !underwaterReady))
                return;

            if (_surfaceDefragMoveCount > 0 &&
                !DispatcherJobSwap.TryComplete(ref _surfacePoolDefragHandle, forceComplete))
            {
                return;
            }

            if (_underwaterDefragMoveCount > 0 &&
                !DispatcherJobSwap.TryComplete(ref _underwaterPoolDefragHandle, forceComplete))
            {
                return;
            }

            if (_surfaceDefragMoveCount > 0)
            {
                SwapChunkPools(ref _surfaceChunkPool, ref _surfaceDefragScratchPool);
                ApplyPoolDefragOffsets(_surfaceDefragKeys, _surfaceDefragOffsets, _surfaceDefragMoveCount, isSurface: true);
                SwapPoolFreeLists(
                    ref _surfacePoolFreeBlocks,
                    ref _surfacePoolFreeBlockCount,
                    ref _surfaceDefragScratchFreeBlocks,
                    ref _surfaceDefragScratchFreeBlockCount);
                ResetPayloadPoolSetFlags(isSurface: true);
            }

            if (_underwaterDefragMoveCount > 0)
            {
                SwapChunkPools(ref _underwaterChunkPool, ref _underwaterDefragScratchPool);
                ApplyPoolDefragOffsets(_underwaterDefragKeys, _underwaterDefragOffsets, _underwaterDefragMoveCount, isSurface: false);
                SwapPoolFreeLists(
                    ref _underwaterPoolFreeBlocks,
                    ref _underwaterPoolFreeBlockCount,
                    ref _underwaterDefragScratchFreeBlocks,
                    ref _underwaterDefragScratchFreeBlockCount);
                ResetPayloadPoolSetFlags(isSurface: false);
            }

            _surfacePoolDefragHandle = default;
            _underwaterPoolDefragHandle = default;
            _poolDefragScheduled = false;
            _surfaceDefragMoveCount = 0;
            _underwaterDefragMoveCount = 0;
            _surfaceDefragCompactUsedCount = 0;
            _underwaterDefragCompactUsedCount = 0;
            if (isActiveAndEnabled)
                _activeSetDirty = !RebuildAndBindActiveBuffers();
        }

        private int BuildPoolDefragPlan(
            NativeChunkPool pool,
            bool isSurface,
            ref ChunkKey[] keys,
            ref int[] destinationOffsets,
            ref NativeArray<ChunkSliceMoveRecord> movesNative,
            out int compactUsedCount)
        {
            compactUsedCount = 0;
            if (!pool.Matrices.IsCreated || _chunkPayloads.Count <= 0)
                return 0;

            EnsureChunkKeyCapacity(ref keys, _chunkPayloads.Count);
            EnsureIntCapacity(ref destinationOffsets, _chunkPayloads.Count);
            EnsureNativeCapacity(ref movesNative, _chunkPayloads.Count);

            int moveCount = 0;
            int nextOffset = 0;
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkKey key = enumerator.Current.Key;
                ChunkPayload payload = enumerator.Current.Value;
                int sourceOffset = isSurface ? payload.SurfaceOffset : payload.UnderwaterOffset;
                int count = isSurface ? payload.SurfaceCount : payload.UnderwaterCount;
                if (count <= 0)
                    continue;

                keys[moveCount] = key;
                destinationOffsets[moveCount] = nextOffset;
                movesNative[moveCount] = new ChunkSliceMoveRecord
                {
                    SourceOffset = sourceOffset,
                    DestinationOffset = nextOffset,
                    Count = count
                };
                nextOffset += count;
                moveCount++;
            }

            compactUsedCount = nextOffset;
            return moveCount;
        }

        private static JobHandle SchedulePoolDefrag(
            NativeChunkPool sourcePool,
            NativeChunkPool destinationPool,
            NativeArray<ChunkSliceMoveRecord> moves,
            int moveCount)
        {
            if (moveCount <= 0)
                return default;

            var job = new DefragPoolJob
            {
                Moves = moves,
                MoveCount = moveCount,
                SourceMatrices = sourcePool.Matrices,
                SourceMetadata = sourcePool.Metadata,
                SourceTypes = sourcePool.Types,
                SourceSemanticTypes = sourcePool.SemanticTypes,
                SourceBiomeLayers = sourcePool.BiomeLayers,
                SourceEdgeDistances = sourcePool.EdgeDistances,
                SourceFlowDirections = sourcePool.FlowDirections,
                SourceFlowVectors = sourcePool.FlowVectors,
                DestinationMatrices = destinationPool.Matrices,
                DestinationMetadata = destinationPool.Metadata,
                DestinationTypes = destinationPool.Types,
                DestinationSemanticTypes = destinationPool.SemanticTypes,
                DestinationBiomeLayers = destinationPool.BiomeLayers,
                DestinationEdgeDistances = destinationPool.EdgeDistances,
                DestinationFlowDirections = destinationPool.FlowDirections,
                DestinationFlowVectors = destinationPool.FlowVectors
            };

            return job.Schedule();
        }

        private void ApplyPoolDefragOffsets(ChunkKey[] keys, int[] offsets, int moveCount, bool isSurface)
        {
            for (int i = 0; i < moveCount; i++)
            {
                ChunkKey key = keys[i];
                if (!_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    continue;

                if (isSurface)
                    payload.SurfaceOffset = offsets[i];
                else
                    payload.UnderwaterOffset = offsets[i];

                _chunkPayloads[key] = payload;
            }
        }

        private static int ComputeUsedCompactCount(Dictionary<ChunkKey, ChunkPayload> payloads, bool isSurface)
        {
            int maxUsed = 0;
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = payloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkPayload payload = enumerator.Current.Value;
                int offset = isSurface ? payload.SurfaceOffset : payload.UnderwaterOffset;
                int count = isSurface ? payload.SurfaceCount : payload.UnderwaterCount;
                maxUsed = Mathf.Max(maxUsed, offset + count);
            }

            return maxUsed;
        }

        private static void ResetPoolFreeList(ref PoolBlock[] freeBlocks, ref int freeBlockCount, int capacity, int usedCount)
        {
            EnsurePoolBlockCapacity(ref freeBlocks, 1);
            int clampedUsed = Mathf.Clamp(usedCount, 0, capacity);
            int freeLength = Mathf.Max(0, capacity - clampedUsed);
            if (freeLength <= 0)
            {
                freeBlockCount = 0;
                if (freeBlocks.Length > 0)
                    freeBlocks[0] = default;
                return;
            }

            freeBlocks[0] = new PoolBlock
            {
                Offset = clampedUsed,
                Length = freeLength
            };
            freeBlockCount = 1;
        }

        private static void InitializeDefragScratchFreeList(ref PoolBlock[] freeBlocks, ref int freeBlockCount, int capacity, int compactUsedCount)
        {
            ResetPoolFreeList(ref freeBlocks, ref freeBlockCount, capacity, compactUsedCount);
        }

        private static void EnsureDefragScratchPoolCapacity(ref NativeChunkPool scratchPool, int capacity)
        {
            if (capacity <= 0)
                return;

            if (scratchPool.Matrices.IsCreated && scratchPool.Capacity == capacity)
                return;

            PoolBlock[] scratchBlocks = null;
            int scratchBlockCount = 0;
            InitializeChunkPool(ref scratchPool, capacity, ref scratchBlocks, ref scratchBlockCount);
        }

        private static void SwapChunkPools(ref NativeChunkPool a, ref NativeChunkPool b)
        {
            NativeChunkPool temp = a;
            a = b;
            b = temp;
        }

        private static void SwapPoolFreeLists(ref PoolBlock[] a, ref int aCount, ref PoolBlock[] b, ref int bCount)
        {
            PoolBlock[] blocks = a;
            a = b;
            b = blocks;

            int count = aCount;
            aCount = bCount;
            bCount = count;
        }

        private void ResetPayloadPoolSetFlags(bool isSurface)
        {
            Dictionary<ChunkKey, ChunkPayload>.Enumerator enumerator = _chunkPayloads.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkKey key = enumerator.Current.Key;
                ChunkPayload payload = enumerator.Current.Value;
                if (isSurface)
                {
                    if (payload.SurfaceCount <= 0)
                        continue;

                    payload.SurfacePoolSet = 0;
                }
                else
                {
                    if (payload.UnderwaterCount <= 0)
                        continue;

                    payload.UnderwaterPoolSet = 0;
                }

                _chunkPayloads[key] = payload;
            }
        }
    }
}
