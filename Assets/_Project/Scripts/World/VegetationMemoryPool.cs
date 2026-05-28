using System;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        private static bool RuntimeNativePoolDefragEnabled => false;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PoolBlock
        {
            public int Offset;
            public int Length;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct NativeChunkPool : IDisposable
        {
            public VaultGenerationHandle<Matrix4x4> MatricesHandle;
            public VaultGenerationHandle<HectonVegetationInstanceData> MetadataHandle;
            public VaultGenerationHandle<int> TypesHandle;
            public VaultGenerationHandle<int> SemanticTypesHandle;
            public VaultGenerationHandle<byte> BiomeLayersHandle;
            public VaultGenerationHandle<float> EdgeDistancesHandle;
            public VaultGenerationHandle<Vector2> FlowDirectionsHandle;
            public VaultGenerationHandle<Vector3> FlowVectorsHandle;
            public int Capacity;
            public bool IsCreated => MatricesHandle.BufferID != 0u ||
                                     MetadataHandle.BufferID != 0u ||
                                     TypesHandle.BufferID != 0u ||
                                     SemanticTypesHandle.BufferID != 0u ||
                                     BiomeLayersHandle.BufferID != 0u ||
                                     EdgeDistancesHandle.BufferID != 0u ||
                                     FlowDirectionsHandle.BufferID != 0u ||
                                     FlowVectorsHandle.BufferID != 0u;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                MatricesHandle = default;
                MetadataHandle = default;
                TypesHandle = default;
                SemanticTypesHandle = default;
                BiomeLayersHandle = default;
                EdgeDistancesHandle = default;
                FlowDirectionsHandle = default;
                FlowVectorsHandle = default;
                Capacity = 0;
            }
        }

        private ref struct NativeChunkPoolView
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

            public NativeChunkPoolView(
                NativeArray<Matrix4x4> matrices,
                NativeArray<HectonVegetationInstanceData> metadata,
                NativeArray<int> types,
                NativeArray<int> semanticTypes,
                NativeArray<byte> biomeLayers,
                NativeArray<float> edgeDistances,
                NativeArray<Vector2> flowDirections,
                NativeArray<Vector3> flowVectors,
                int capacity)
            {
                Matrices = matrices;
                Metadata = metadata;
                Types = types;
                SemanticTypes = semanticTypes;
                BiomeLayers = biomeLayers;
                EdgeDistances = edgeDistances;
                FlowDirections = flowDirections;
                FlowVectors = flowVectors;
                Capacity = capacity;
            }

            public bool IsCreated => Matrices.IsCreated &&
                                     Metadata.IsCreated &&
                                     Types.IsCreated &&
                                     SemanticTypes.IsCreated &&
                                     BiomeLayers.IsCreated &&
                                     EdgeDistances.IsCreated &&
                                      FlowDirections.IsCreated &&
                                      FlowVectors.IsCreated;
        }

        private struct NativeChunkPoolWriteLocks
        {
            public IDataVault MatricesVault;
            public IDataVault MetadataVault;
            public IDataVault TypesVault;
            public IDataVault SemanticTypesVault;
            public IDataVault BiomeLayersVault;
            public IDataVault EdgeDistancesVault;
            public IDataVault FlowDirectionsVault;
            public IDataVault FlowVectorsVault;
            public bool MatricesLocked;
            public bool MetadataLocked;
            public bool TypesLocked;
            public bool SemanticTypesLocked;
            public bool BiomeLayersLocked;
            public bool EdgeDistancesLocked;
            public bool FlowDirectionsLocked;
            public bool FlowVectorsLocked;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct ActiveAggregateNativeBufferSet : IDisposable
        {
            public VaultGenerationHandle<Matrix4x4> MatricesHandle;
            public VaultGenerationHandle<HectonVegetationInstanceData> MetadataHandle;
            public VaultGenerationHandle<int> TypesHandle;
            public VaultGenerationHandle<int> SemanticTypesHandle;
            public VaultGenerationHandle<byte> BiomeLayersHandle;
            public VaultGenerationHandle<Vector2> FlowDirectionsHandle;
            public VaultGenerationHandle<Vector3> FlowVectorsHandle;
            public VaultGenerationHandle<byte> MatrixDirtyPagesHandle;
            public VaultGenerationHandle<byte> MetadataDirtyPagesHandle;
            public int Capacity;
            public int DirtyPageCapacity;
            public bool IsCreated => MatricesHandle.BufferID != 0u ||
                                     MetadataHandle.BufferID != 0u ||
                                     TypesHandle.BufferID != 0u ||
                                     SemanticTypesHandle.BufferID != 0u ||
                                     BiomeLayersHandle.BufferID != 0u ||
                                     FlowDirectionsHandle.BufferID != 0u ||
                                     FlowVectorsHandle.BufferID != 0u ||
                                     MatrixDirtyPagesHandle.BufferID != 0u ||
                                     MetadataDirtyPagesHandle.BufferID != 0u;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                MatricesHandle = default;
                MetadataHandle = default;
                TypesHandle = default;
                SemanticTypesHandle = default;
                BiomeLayersHandle = default;
                FlowDirectionsHandle = default;
                FlowVectorsHandle = default;
                MatrixDirtyPagesHandle = default;
                MetadataDirtyPagesHandle = default;
                Capacity = 0;
                DirtyPageCapacity = 0;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct VegetationNativeMemory : IDisposable
        {
            public VaultGenerationHandle<VegetationDensityChunkRecord> DensityQueryChunksHandle;
            public VaultGenerationHandle<float3> DensityQueryGridHandle;
            public VaultGenerationHandle<float2> ThreatAttractorGridHandle;
            public VaultGenerationHandle<float> EcosystemThreatGridHandle;
            public VaultGenerationHandle<byte> EcosystemThreatGridCompressedHandle;
            public VaultGenerationHandle<byte> EcosystemThreatVoxelHandle;
            public VaultGenerationHandle<byte> EcosystemThreatEchoHandle;
            public VaultGenerationHandle<float2> EcosystemFlowFieldHandle;
            public VaultGenerationHandle<float> AbyssalThermalGridHandle;
            public VaultGenerationHandle<float3> AbyssalFlowVolumeHandle;
            public VaultGenerationHandle<float> CanopyHeightGridHandle;
            public VaultGenerationHandle<TerrainHoleRecord> TerrainHoleRecordsHandle;
            public VaultGenerationHandle<TerrainHoleStreamingRecord> TerrainHoleStreamingRecordsHandle;
            public VaultGenerationHandle<ArtificialStructureRecord> ArtificialStructureRecordsHandle;
            public VaultGenerationHandle<Vector3> AbyssalAnchorPositionsHandle;
            public VaultGenerationHandle<AbsoluteUniversePosition> AbyssalAnchorAupPositionsHandle;
            public VaultGenerationHandle<Vector3> AbyssalNavNodeSnapshotHandle;
            public VaultGenerationHandle<Vector3> AbyssalNavConduitVectorsHandle;
            public VaultGenerationHandle<float> AbyssalNavConduitStrengthsHandle;
            public VaultGenerationHandle<byte> AbyssalNavNodeTypesHandle;
            public VaultGenerationHandle<Vector3> AbyssalPathSnapshotHandle;
            public VaultGenerationHandle<PredatorFearNodeSnapshot> PredatorFearNodesSnapshotHandle;
            public VaultGenerationHandle<HLODData> HlodRegistrySnapshotHandle;
            public VaultGenerationHandle<HLODData> VisibleHlodSnapshotHandle;
            public VaultGenerationHandle<ChunkSliceMoveRecord> SurfaceDefragMovesHandle;
            public VaultGenerationHandle<ChunkSliceMoveRecord> UnderwaterDefragMovesHandle;
            public VaultGenerationHandle<ActiveAggregateCopyRecord> SurfaceAggregateCopyRecordsHandle;
            public VaultGenerationHandle<ActiveAggregateCopyRecord> UnderwaterAggregateCopyRecordsHandle;
            public VaultGenerationHandle<MegaWreckStreamSection> MegaWreckStreamSnapshotHandle;
            public bool IsCreated => DensityQueryChunksHandle.BufferID != 0u ||
                                     DensityQueryGridHandle.BufferID != 0u ||
                                     ThreatAttractorGridHandle.BufferID != 0u ||
                                     EcosystemThreatGridHandle.BufferID != 0u ||
                                     EcosystemThreatGridCompressedHandle.BufferID != 0u ||
                                     EcosystemThreatVoxelHandle.BufferID != 0u ||
                                     EcosystemThreatEchoHandle.BufferID != 0u ||
                                      EcosystemFlowFieldHandle.BufferID != 0u ||
                                      AbyssalThermalGridHandle.BufferID != 0u ||
                                      AbyssalFlowVolumeHandle.BufferID != 0u ||
                                      CanopyHeightGridHandle.BufferID != 0u ||
                                      TerrainHoleRecordsHandle.BufferID != 0u ||
                                      ArtificialStructureRecordsHandle.BufferID != 0u ||
                                      AbyssalAnchorPositionsHandle.BufferID != 0u ||
                                      AbyssalAnchorAupPositionsHandle.BufferID != 0u ||
                                      AbyssalNavNodeSnapshotHandle.BufferID != 0u ||
                                      AbyssalNavConduitVectorsHandle.BufferID != 0u ||
                                      AbyssalNavConduitStrengthsHandle.BufferID != 0u ||
                                      AbyssalNavNodeTypesHandle.BufferID != 0u ||
                                      AbyssalPathSnapshotHandle.BufferID != 0u ||
                                     PredatorFearNodesSnapshotHandle.BufferID != 0u ||
                                     HlodRegistrySnapshotHandle.BufferID != 0u ||
                                     VisibleHlodSnapshotHandle.BufferID != 0u ||
                                     SurfaceDefragMovesHandle.BufferID != 0u ||
                                     UnderwaterDefragMovesHandle.BufferID != 0u ||
                                     SurfaceAggregateCopyRecordsHandle.BufferID != 0u ||
                                     UnderwaterAggregateCopyRecordsHandle.BufferID != 0u ||
                                     MegaWreckStreamSnapshotHandle.BufferID != 0u;

            public void Dispose()
            {
                Dispose(default);
            }

            public void Dispose(JobHandle dependency)
            {
                DensityQueryChunksHandle = default;
                DensityQueryGridHandle = default;
                ThreatAttractorGridHandle = default;
                EcosystemThreatGridHandle = default;
                EcosystemThreatGridCompressedHandle = default;
                EcosystemThreatVoxelHandle = default;
                EcosystemThreatEchoHandle = default;
                EcosystemFlowFieldHandle = default;
                AbyssalThermalGridHandle = default;
                AbyssalFlowVolumeHandle = default;
                CanopyHeightGridHandle = default;
                TerrainHoleRecordsHandle = default;
                TerrainHoleStreamingRecordsHandle = default;
                ArtificialStructureRecordsHandle = default;
                AbyssalAnchorPositionsHandle = default;
                AbyssalAnchorAupPositionsHandle = default;
                AbyssalNavNodeSnapshotHandle = default;
                AbyssalNavConduitVectorsHandle = default;
                AbyssalNavConduitStrengthsHandle = default;
                AbyssalNavNodeTypesHandle = default;
                AbyssalPathSnapshotHandle = default;
                PredatorFearNodesSnapshotHandle = default;
                HlodRegistrySnapshotHandle = default;
                VisibleHlodSnapshotHandle = default;
                SurfaceDefragMovesHandle = default;
                UnderwaterDefragMovesHandle = default;
                SurfaceAggregateCopyRecordsHandle = default;
                UnderwaterAggregateCopyRecordsHandle = default;
                MegaWreckStreamSnapshotHandle = default;
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
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
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
            if (buffer.SandMaskHandle.BufferID != 0u)
                bytes += math.max(0, buffer.SampleCount);
            if (buffer.RockMaskHandle.BufferID != 0u)
                bytes += math.max(0, buffer.SampleCount);
            if (buffer.HeightSamplesHandle.BufferID != 0u)
                bytes += (long)math.max(0, buffer.HeightSampleCount) * sizeof(ushort);

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
            if (!RuntimeNativePoolDefragEnabled)
            {
                _idleNativePoolTimer = 0f;
                return;
            }

            if (_poolDefragScheduled ||
                _idleNativePoolTimer < nativePoolDefragIdleSeconds ||
                _activeSetDirty ||
                ComputeNativePoolFragmentationPercent() < nativePoolDefragThresholdPercent)
            {
                return;
            }

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
            RecordVegetationMemoryTelemetry(
                VegetationMemorySovereigntyConstants.TelemetryRingBufferId,
                _vegetationMemoryTelemetryHandle.Generation,
                _surfaceChunkPool.Capacity + _underwaterChunkPool.Capacity,
                _surfaceActiveCount + _underwaterActiveCount,
                _surfaceActiveCount + _underwaterActiveCount,
                0f,
                VegetationMemoryTelemetryCode.DefragCompleted,
                VegetationMemoryTelemetryPhase.Defrag,
                VegetationMemorySovereigntyConstants.FlagDefrag,
                default);
            if (isActiveAndEnabled)
                _activeSetDirty = !RebuildAndBindActiveBuffers();
        }

        private int BuildPoolDefragPlan(
            NativeChunkPool pool,
            bool isSurface,
            ref ChunkKey[] keys,
            ref int[] destinationOffsets,
            ref VaultGenerationHandle<ChunkSliceMoveRecord> movesHandle,
            BufferID movesBufferId,
            out int compactUsedCount,
            out NativeArray<ChunkSliceMoveRecord> scheduledMoves)
        {
            compactUsedCount = 0;
            scheduledMoves = default;
            if (!pool.IsCreated || _chunkPayloads.Count <= 0)
                return 0;

            int requiredMoveCapacity = _chunkPayloads.Count;
            if (!HasPoolDefragStagingCapacity(keys, destinationOffsets, requiredMoveCapacity))
            {
                RecordVegetationMemoryTelemetry(
                    movesBufferId,
                    movesHandle.Generation,
                    requiredMoveCapacity,
                    ResolvePoolDefragStagingCapacity(keys, destinationOffsets),
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.StagingCapacityExceeded,
                    VegetationMemoryTelemetryPhase.Defrag,
                    VegetationMemorySovereigntyConstants.FlagDefrag | VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return 0;
            }

            if (!TryAcquireVegetationMemoryBuffer(
                    ref movesHandle,
                    movesBufferId,
                    requiredMoveCapacity,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<ChunkSliceMoveRecord> movesNative))
            {
                return 0;
            }

            int moveCount = 0;
            int nextOffset = 0;
            int actualScheduledLength = 0;
            bool allocationFailed = false;
            try
            {
                scheduledMoves = H8Memory.Allocate<ChunkSliceMoveRecord>(
                    requiredMoveCapacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                actualScheduledLength = scheduledMoves.IsCreated ? scheduledMoves.Length : 0;
                if (!scheduledMoves.IsCreated ||
                    scheduledMoves.Length < requiredMoveCapacity)
                {
                    allocationFailed = true;
                    H8Memory.Release(ref scheduledMoves, VegetationMemorySovereigntyConstants.OwnerSystemId);
                }
                else
                {
                    FixedChunkPayloadMap.Enumerator enumerator = _chunkPayloads.GetEnumerator();
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
                        ChunkSliceMoveRecord move = new ChunkSliceMoveRecord
                        {
                            SourceOffset = sourceOffset,
                            DestinationOffset = nextOffset,
                            Count = count
                        };
                        movesNative[moveCount] = move;
                        scheduledMoves[moveCount] = move;
                        nextOffset += count;
                        moveCount++;
                    }
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in movesHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if (allocationFailed)
            {
                RecordVegetationMemoryTelemetry(
                    movesBufferId,
                    movesHandle.Generation,
                    requiredMoveCapacity,
                    actualScheduledLength,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.VaultResolveFailed,
                    VegetationMemoryTelemetryPhase.Defrag,
                    VegetationMemorySovereigntyConstants.FlagStaleHandle,
                    default);
                return 0;
            }

            compactUsedCount = nextOffset;
            if (moveCount <= 0)
                H8Memory.Release(ref scheduledMoves, VegetationMemorySovereigntyConstants.OwnerSystemId);
            return moveCount;
        }

        private void EnsurePoolDefragStagingCapacity(int requiredCount)
        {
            EnsureChunkKeyCapacity(ref _surfaceDefragKeys, requiredCount);
            EnsureIntCapacity(ref _surfaceDefragOffsets, requiredCount);
            EnsureChunkKeyCapacity(ref _underwaterDefragKeys, requiredCount);
            EnsureIntCapacity(ref _underwaterDefragOffsets, requiredCount);
        }

        private static bool HasPoolDefragStagingCapacity(ChunkKey[] keys, int[] destinationOffsets, int requiredCount)
        {
            return requiredCount > 0 &&
                   keys != null &&
                   destinationOffsets != null &&
                   keys.Length >= requiredCount &&
                   destinationOffsets.Length >= requiredCount;
        }

        private static int ResolvePoolDefragStagingCapacity(ChunkKey[] keys, int[] destinationOffsets)
        {
            int keyCapacity = keys != null ? keys.Length : 0;
            int offsetCapacity = destinationOffsets != null ? destinationOffsets.Length : 0;
            return math.min(keyCapacity, offsetCapacity);
        }

        private JobHandle SchedulePoolDefrag(
            in NativeChunkPool sourcePool,
            ref NativeChunkPool destinationPool,
            NativeArray<ChunkSliceMoveRecord> moves,
            int moveCount)
        {
            H8Memory.Release(ref moves, VegetationMemorySovereigntyConstants.OwnerSystemId);
            return default;
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

                SetChunkPayload(key, payload);
            }
        }

        private static int ComputeUsedCompactCount(FixedChunkPayloadMap payloads, bool isSurface)
        {
            int maxUsed = 0;
            FixedChunkPayloadMap.Enumerator enumerator = payloads.GetEnumerator();
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

        private void EnsureDefragScratchPoolCapacity(
            ref NativeChunkPool scratchPool,
            BufferID matrixBufferId,
            int capacity)
        {
            if (capacity <= 0)
                return;

            if (scratchPool.IsCreated && scratchPool.Capacity == capacity)
                return;

            PoolBlock[] scratchBlocks = null;
            int scratchBlockCount = 0;
            InitializeChunkPool(ref scratchPool, matrixBufferId, capacity, ref scratchBlocks, ref scratchBlockCount);
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
            FixedChunkPayloadMap.Enumerator enumerator = _chunkPayloads.GetEnumerator();
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

                SetChunkPayload(key, payload);
            }
        }
    }
}
