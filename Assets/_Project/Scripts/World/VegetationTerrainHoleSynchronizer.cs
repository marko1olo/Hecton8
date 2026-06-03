using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        private const int EmptyTerrainHoleNativeCapacity = 1;

        /// <summary>
        /// Registers a persistent world-space terrain hole that suppresses vegetation generation inside the provided radius.
        /// </summary>
        public void RegisterTerrainHole(Vector3 position, float radius)
        {
            RegisterTerrainHoleHandle(position, radius);
        }

        /// <summary>
        /// Registers a persistent world-space terrain hole and returns a stable runtime handle for later removal.
        /// </summary>
        public int RegisterTerrainHoleHandle(Vector3 position, float radius)
        {
            if (radius <= 0f)
                return InvalidTerrainHoleId;

            float clampedRadius = Mathf.Max(0.5f, radius);
            float duplicateDistanceSq = Mathf.Max(0.25f, clampedRadius * 0.15f);
            duplicateDistanceSq *= duplicateDistanceSq;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                TerrainHoleRecord existing = _terrainHoleRecords[i];
                float dx = existing.X - position.x;
                float dz = existing.Z - position.z;
                if ((dx * dx) + (dz * dz) > duplicateDistanceSq)
                    continue;

                existing.X = position.x;
                existing.Y = position.y;
                existing.Z = position.z;
                existing.Radius = clampedRadius;
                existing.RadiusSq = clampedRadius * clampedRadius;
                existing.SourceType = TerrainHoleSourceType.CaveEntrance;
                _terrainHoleRecords[i] = existing;
                SyncTerrainHoleNativeCache();
                InvalidateChunksIntersectingHole(position, clampedRadius);
                RefreshResidency();
                return existing.HoleId;
            }

            if (!HasTerrainHoleCapacity(_terrainHoleCount + 1))
                return InvalidTerrainHoleId;

            int transientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (transientCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount,
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount + 1,
                    transientCount);
            }

            _terrainHoleRecords[_persistentTerrainHoleCount] = new TerrainHoleRecord
            {
                HoleId = _nextTerrainHoleId++,
                Y = position.y,
                X = position.x,
                Z = position.z,
                Radius = clampedRadius,
                RadiusSq = clampedRadius * clampedRadius,
                SourceType = TerrainHoleSourceType.CaveEntrance
            };
            _persistentTerrainHoleCount++;
            _terrainHoleCount++;
            SyncTerrainHoleNativeCache();
            InvalidateChunksIntersectingHole(position, clampedRadius);
            RefreshResidency();
            return _terrainHoleRecords[_persistentTerrainHoleCount - 1].HoleId;
        }

        /// <summary>
        /// Unregisters a persistent terrain hole by approximate world-space location and initiates vegetation rebuild so the area can regrow.
        /// </summary>
        public bool UnregisterTerrainHole(Vector3 position, float radius)
        {
            if (_persistentTerrainHoleCount <= 0)
                return false;

            float clampedRadius = Mathf.Max(0.5f, radius);
            float duplicateDistanceSq = Mathf.Max(0.25f, clampedRadius * 0.15f);
            duplicateDistanceSq *= duplicateDistanceSq;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                TerrainHoleRecord existing = _terrainHoleRecords[i];
                float dx = existing.X - position.x;
                float dz = existing.Z - position.z;
                if ((dx * dx) + (dz * dz) > duplicateDistanceSq)
                    continue;

                if (Mathf.Abs(existing.Radius - clampedRadius) > Mathf.Max(0.5f, clampedRadius * 0.35f))
                    continue;

                return UnregisterTerrainHole(existing.HoleId);
            }

            return false;
        }

        /// <summary>
        /// Unregisters a persistent terrain hole by stable runtime handle and initiates vegetation rebuild so the area can regrow.
        /// </summary>
        public bool UnregisterTerrainHole(int holeId)
        {
            if (holeId == InvalidTerrainHoleId || _persistentTerrainHoleCount <= 0)
                return false;

            int persistentIndex = -1;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                if (_terrainHoleRecords[i].HoleId != holeId)
                    continue;

                persistentIndex = i;
                break;
            }

            if (persistentIndex < 0)
                return false;

            TerrainHoleRecord removed = _terrainHoleRecords[persistentIndex];
            int persistentTailCount = _persistentTerrainHoleCount - persistentIndex - 1;
            if (persistentTailCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    persistentIndex + 1,
                    _terrainHoleRecords,
                    persistentIndex,
                    persistentTailCount);
            }

            int transientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (transientCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount,
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount - 1,
                    transientCount);
            }

            _persistentTerrainHoleCount--;
            _terrainHoleCount--;
            if (_terrainHoleCount >= 0 && _terrainHoleCount < _terrainHoleRecords.Length)
                _terrainHoleRecords[_terrainHoleCount] = default;

            SyncTerrainHoleNativeCache();
            InvalidateChunksIntersectingHole(new Vector3(removed.X, removed.Y, removed.Z), removed.Radius);
            RefreshResidency();
            return true;
        }

        /// <summary>
        /// Clears all registered terrain holes in one cold-path operation.
        /// </summary>
        public void ClearTerrainHoles()
        {
            if (_terrainHoleCount <= 0)
                return;

            _terrainHoleCount = 0;
            _persistentTerrainHoleCount = 0;
            _megaWreckInteriorMaskHash = 0;
            _nextTerrainHoleId = 1;
            ClearArtificialInteriorState();
            SyncTerrainHoleNativeCache();
            ClearAllResidency();
            RefreshResidency();
        }

        private void SyncMegaWreckInteriorTerrainHoles()
        {
            int currentTransientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (playerTransform == null ||
                _megaWreckStreamCount <= 0 ||
                _megaWreckStreamSnapshot == null)
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            Vector3 playerPosition = playerTransform.position;
            int wreckId = FindMegaWreckInteriorWreckId(playerPosition);
            if (wreckId == int.MinValue)
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            int matchingSectionCount = CountMegaWreckSections(wreckId);
            if (matchingSectionCount <= 0)
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            if (!HasTerrainHoleCapacity(_persistentTerrainHoleCount + matchingSectionCount))
            {
                ClearArtificialInteriorState();
                ClearTransientMegaWreckInteriorHoles(currentTransientCount);
                return;
            }

            int newHash = ComputeMegaWreckInteriorMaskHash(wreckId);
            if (currentTransientCount == matchingSectionCount && _megaWreckInteriorMaskHash == newHash)
                return;

            if (currentTransientCount > 0)
                InvalidateChunksIntersectingTerrainHoleRange(_persistentTerrainHoleCount, currentTransientCount);

            int writeIndex = _persistentTerrainHoleCount;
            bool hasInteriorBounds = false;
            Bounds interiorBounds = default;
            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                MegaWreckStreamSection section = _megaWreckStreamSnapshot[i];
                if (section.WreckId != wreckId)
                    continue;

                Bounds sectionBounds = GetMegaWreckSectionBounds(section);
                if (!hasInteriorBounds)
                {
                    interiorBounds = sectionBounds;
                    hasInteriorBounds = true;
                }
                else
                {
                    interiorBounds.Encapsulate(sectionBounds);
                }

                float horizontalHalfExtent = EstimateHorizontalHalfExtent(section.WorldSize.x, section.WorldSize.z);
                float radius = Mathf.Max(megaWreckInteriorMinimumHoleRadius, horizontalHalfExtent + megaWreckInteriorHolePadding);
                _terrainHoleRecords[writeIndex] = new TerrainHoleRecord
                {
                    HoleId = InvalidTerrainHoleId,
                    Y = section.WorldCenter.y,
                    X = section.WorldCenter.x,
                    Z = section.WorldCenter.z,
                    Radius = radius,
                    RadiusSq = radius * radius,
                    SourceType = TerrainHoleSourceType.MegaWreckInterior
                };
                writeIndex++;
            }

            _terrainHoleCount = writeIndex;
            _megaWreckInteriorMaskHash = newHash;
            if (hasInteriorBounds)
                SetArtificialInteriorState(StructureType.MegaWreck, wreckId, interiorBounds);
            else
                ClearArtificialInteriorState();
            SyncTerrainHoleNativeCache();
            InvalidateChunksIntersectingTerrainHoleRange(_persistentTerrainHoleCount, _terrainHoleCount - _persistentTerrainHoleCount);
            RefreshResidency();
        }

        private void ClearTransientMegaWreckInteriorHoles(int currentTransientCount)
        {
            if (currentTransientCount <= 0 && _megaWreckInteriorMaskHash == 0)
                return;

            if (currentTransientCount > 0)
                InvalidateChunksIntersectingTerrainHoleRange(_persistentTerrainHoleCount, currentTransientCount);

            _terrainHoleCount = _persistentTerrainHoleCount;
            _megaWreckInteriorMaskHash = 0;
            ClearArtificialInteriorState();
            SyncTerrainHoleNativeCache();
            RefreshResidency();
        }

        private void EvictDistantTerrainHoles()
        {
            if (playerTransform == null || _persistentTerrainHoleCount <= 0)
                return;

            float maxDistanceSq = DefaultTerrainHoleEvictionDistance * DefaultTerrainHoleEvictionDistance;
            Vector3 playerPosition = playerTransform.position;
            int removedCount = 0;

            int writeIndex = 0;
            for (int i = 0; i < _persistentTerrainHoleCount; i++)
            {
                TerrainHoleRecord hole = _terrainHoleRecords[i];
                float dx = hole.X - playerPosition.x;
                float dz = hole.Z - playerPosition.z;
                if ((dx * dx) + (dz * dz) > maxDistanceSq)
                {
                    removedCount++;
                    InvalidateChunksIntersectingHole(new Vector3(hole.X, hole.Y, hole.Z), hole.Radius);
                    continue;
                }

                if (writeIndex != i)
                    _terrainHoleRecords[writeIndex] = hole;

                writeIndex++;
            }

            if (removedCount <= 0)
                return;

            int transientCount = _terrainHoleCount - _persistentTerrainHoleCount;
            if (transientCount > 0)
            {
                Array.Copy(
                    _terrainHoleRecords,
                    _persistentTerrainHoleCount,
                    _terrainHoleRecords,
                    writeIndex,
                    transientCount);
            }

            int previousTerrainHoleCount = _terrainHoleCount;
            _persistentTerrainHoleCount = writeIndex;
            _terrainHoleCount = writeIndex + transientCount;
            for (int i = _terrainHoleCount; i < previousTerrainHoleCount; i++)
                _terrainHoleRecords[i] = default;

            SyncTerrainHoleNativeCache();
            RefreshResidency();
        }

        private void SetArtificialInteriorState(StructureType type, int structureId, Bounds bounds)
        {
            _activeArtificialInteriorState = new ArtificialInteriorState
            {
                IsActive = 1,
                Type = type,
                StructureId = structureId,
                Bounds = bounds
            };
            GlobalArtificialInteriorActive = true;
            GlobalArtificialInteriorType = type;
            GlobalArtificialInteriorId = structureId;
            GlobalArtificialInteriorBounds = bounds;
        }

        private void ClearArtificialInteriorState()
        {
            _activeArtificialInteriorState = default;
            GlobalArtificialInteriorActive = false;
            GlobalArtificialInteriorType = default;
            GlobalArtificialInteriorId = int.MinValue;
            GlobalArtificialInteriorBounds = default;
        }

        /// <summary>

        private bool HasTerrainHoleCapacity(int requiredCount)
        {
            if (_terrainHoleRecords != null && _terrainHoleRecords.Length >= requiredCount)
                return true;

            return false;
        }

        private static bool HasTerrainHoleStreamingCapacity(TerrainHoleStreamingRecord[] cache, int requiredCount)
        {
            if (cache != null && cache.Length >= requiredCount)
                return true;

            return false;
        }

        private static float EstimateHorizontalHalfExtent(float sizeX, float sizeZ)
        {
            float ax = math.abs(sizeX);
            float az = math.abs(sizeZ);
            float maxAxis = math.max(ax, az);
            float minAxis = math.min(ax, az);
            return (maxAxis + (minAxis * 0.375f)) * 0.5f;
        }

        private void SyncTerrainHoleNativeCache()
        {
            if (_terrainHoleCount <= 0)
            {
                ReleaseVegetationMemoryBuffer(ref _nativeMemory.TerrainHoleRecordsHandle);
                ReleaseVegetationMemoryBuffer(ref _nativeMemory.TerrainHoleStreamingRecordsHandle);

                MarkAllTileTerrainHolesDirty();
                return;
            }

            if (!HasTerrainHoleStreamingCapacity(_terrainHoleStreamingRecords, _terrainHoleCount))
            {
                MarkAllTileTerrainHolesDirty();
                return;
            }

            if (!WriteTerrainHoleRecordsNativeCache())
            {
                MarkAllTileTerrainHolesDirty();
                return;
            }

            if (!WriteTerrainHoleStreamingNativeCache())
            {
                MarkAllTileTerrainHolesDirty();
                return;
            }

            MarkAllTileTerrainHolesDirty();
        }

        private bool WriteTerrainHoleRecordsNativeCache()
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.TerrainHoleRecordsHandle,
                    BufferID.VegetationTerrainHoleRecords,
                    _terrainHoleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<TerrainHoleRecord> terrainHoleRecordsNative))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < _terrainHoleCount; i++)
                    terrainHoleRecordsNative[i] = _terrainHoleRecords[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.TerrainHoleRecordsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteTerrainHoleStreamingNativeCache()
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.TerrainHoleStreamingRecordsHandle,
                    BufferID.VegetationTerrainHoleStreamingRecords,
                    _terrainHoleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<TerrainHoleStreamingRecord> terrainHoleStreamingRecordsNative))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < _terrainHoleCount; i++)
                {
                    TerrainHoleRecord hole = _terrainHoleRecords[i];
                    TerrainHoleStreamingRecord streamingRecord = new TerrainHoleStreamingRecord
                    {
                        HoleId = hole.HoleId,
                        Position = new Vector3(hole.X, hole.Y, hole.Z),
                        Radius = hole.Radius,
                        SourceType = hole.SourceType
                    };
                    _terrainHoleStreamingRecords[i] = streamingRecord;
                    terrainHoleStreamingRecordsNative[i] = streamingRecord;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.TerrainHoleStreamingRecordsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void InvalidateChunksIntersectingHole(Vector3 position, float radius)
        {
            float radiusSq = radius * radius;
            ClearEvictionScratch();
            FixedChunkPayloadMap.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                if (DoesChunkIntersectHole(payloadEnumerator.Current.Value, position.x, position.z, radiusSq))
                {
                    if (!TryAddEvictionScratch(payloadEnumerator.Current.Key))
                        break;
                }
            }

            for (int i = 0; i < _evictionKeyCount; i++)
            {
                ChunkKey key = _evictionKeys[i];
                if (_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    ReleaseChunkPayloadStorage(payload);

                _chunkPayloads.Remove(key);
                RemoveChunkAbyssalNavPayload(key);
                RemoveChunkMegaWreckPayload(key);
            }

            _activeSetDirty = true;
        }

        private void InvalidateChunksIntersectingTerrainHoleRange(int startIndex, int count)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
            {
                int holeIndex = startIndex + i;
                if (holeIndex < 0 || holeIndex >= _terrainHoleCount)
                    break;

                TerrainHoleRecord hole = _terrainHoleRecords[holeIndex];
                InvalidateChunksIntersectingHole(new Vector3(hole.X, 0f, hole.Z), hole.Radius);
            }
        }

        private void InvalidateChunksIntersectingBounds(Bounds bounds)
        {
            if (bounds.size.sqrMagnitude <= 0.0001f)
                return;

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            ClearEvictionScratch();
            FixedChunkPayloadMap.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                if (DoesChunkIntersectBounds(payloadEnumerator.Current.Value, min.x, max.x, min.z, max.z))
                {
                    if (!TryAddEvictionScratch(payloadEnumerator.Current.Key))
                        break;
                }
            }

            for (int i = 0; i < _evictionKeyCount; i++)
            {
                ChunkKey key = _evictionKeys[i];
                if (_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
                    ReleaseChunkPayloadStorage(payload);

                _chunkPayloads.Remove(key);
                RemoveChunkAbyssalNavPayload(key);
                RemoveChunkMegaWreckPayload(key);
            }

            _activeSetDirty = true;
        }

        private static bool DoesChunkIntersectHole(ChunkPayload payload, float holeX, float holeZ, float radiusSq)
        {
            float clampedX = Mathf.Clamp(holeX, payload.MinX, payload.MaxX);
            float clampedZ = Mathf.Clamp(holeZ, payload.MinZ, payload.MaxZ);
            float dx = holeX - clampedX;
            float dz = holeZ - clampedZ;
            return (dx * dx) + (dz * dz) <= radiusSq;
        }

        private static bool DoesChunkIntersectBounds(ChunkPayload payload, float minX, float maxX, float minZ, float maxZ)
        {
            return maxX >= payload.MinX &&
                   minX <= payload.MaxX &&
                   maxZ >= payload.MinZ &&
                   minZ <= payload.MaxZ;
        }

        private int CountSemanticType(NativeChunkPool pool, int offset, int count, int semanticType)
        {
            int requiredPoolCount = offset + count;
            if (offset < 0 ||
                count <= 0 ||
                requiredPoolCount < offset)
            {
                return 0;
            }

            if (!TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
                return 0;

            int resolvedCount = 0;
            int end = math.min(poolView.SemanticTypes.Length, requiredPoolCount);
            for (int i = math.max(0, offset); i < end; i++)
            {
                if (poolView.SemanticTypes[i] == semanticType)
                    resolvedCount++;
            }

            return resolvedCount;
        }

        private void CopySemanticAnchorPositions(
            NativeChunkPool pool,
            int offset,
            int count,
            int semanticType,
            Vector3[] managedPositions,
            NativeArray<Vector3> nativePositions,
            double3 universeOffset,
            ref int writeIndex)
        {
            int requiredPoolCount = offset + count;
            if (offset < 0 ||
                count <= 0 ||
                requiredPoolCount < offset)
            {
                return;
            }

            if (!TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
                return;

            int end = math.min(poolView.SemanticTypes.Length, requiredPoolCount);
            for (int i = math.max(0, offset); i < end; i++)
            {
                if (poolView.SemanticTypes[i] != semanticType)
                    continue;

                double3 runtimePosition = new double3(poolView.Matrices[i].m03, poolView.Matrices[i].m13, poolView.Matrices[i].m23) + universeOffset;
                Vector3 position = ToVector3(runtimePosition);
                managedPositions[writeIndex] = position;
                nativePositions[writeIndex] = position;
                writeIndex++;
            }
        }

        private void CopySemanticAnchorAupPositions(
            NativeChunkPool pool,
            int offset,
            int count,
            int semanticType,
            NativeArray<AbsoluteUniversePosition> nativeAupPositions,
            double3 universeOffset,
            int writeCapacity,
            ref int writeIndex)
        {
            int requiredPoolCount = offset + count;
            if (offset < 0 ||
                count <= 0 ||
                requiredPoolCount < offset ||
                writeCapacity <= 0)
            {
                return;
            }

            if (!TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
                return;

            int end = math.min(poolView.SemanticTypes.Length, requiredPoolCount);
            for (int i = math.max(0, offset); i < end && writeIndex < writeCapacity; i++)
            {
                if (poolView.SemanticTypes[i] != semanticType)
                    continue;

                double3 runtimePosition = new double3(poolView.Matrices[i].m03, poolView.Matrices[i].m13, poolView.Matrices[i].m23) + universeOffset;
                nativeAupPositions[writeIndex] = AbsoluteUniversePosition.FromAbsolutePosition(runtimePosition + HectonFloatingOrigin.CurrentTotalOffsetDouble);
                writeIndex++;
            }
        }

        private bool TryAllocateChunkSliceForWrite(bool isSurface, int count, out int offset, out bool useScratchPool)
        {
            offset = -1;
            useScratchPool = false;

            if (_poolDefragScheduled)
            {
                if (isSurface && _surfaceDefragMoveCount > 0)
                {
                    useScratchPool = TryAllocateChunkSlice(ref _surfaceDefragScratchFreeBlocks, ref _surfaceDefragScratchFreeBlockCount, count, out offset);
                    if (useScratchPool)
                        return true;
                }

                if (!isSurface && _underwaterDefragMoveCount > 0)
                {
                    useScratchPool = TryAllocateChunkSlice(ref _underwaterDefragScratchFreeBlocks, ref _underwaterDefragScratchFreeBlockCount, count, out offset);
                    if (useScratchPool)
                        return true;
                }
            }

            return isSurface
                ? TryAllocateChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, count, out offset)
                : TryAllocateChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, count, out offset);
        }

        private NativeChunkPool ResolveChunkPool(bool isSurface, ChunkPayload payload)
        {
            if (isSurface)
                return payload.SurfacePoolSet == 0 ? _surfaceChunkPool : _surfaceDefragScratchPool;

            return payload.UnderwaterPoolSet == 0 ? _underwaterChunkPool : _underwaterDefragScratchPool;
        }

        private void FreeChunkSliceForPayload(bool isSurface, ChunkPayload payload)
        {
            if (isSurface)
            {
                if (payload.SurfaceCount <= 0)
                    return;

                if (payload.SurfacePoolSet == 0)
                    FreeChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, payload.SurfaceOffset, payload.SurfaceCount);
                else
                    FreeChunkSlice(ref _surfaceDefragScratchFreeBlocks, ref _surfaceDefragScratchFreeBlockCount, payload.SurfaceOffset, payload.SurfaceCount);

                return;
            }

            if (payload.UnderwaterCount <= 0)
                return;

            if (payload.UnderwaterPoolSet == 0)
                FreeChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, payload.UnderwaterOffset, payload.UnderwaterCount);
            else
                FreeChunkSlice(ref _underwaterDefragScratchFreeBlocks, ref _underwaterDefragScratchFreeBlockCount, payload.UnderwaterOffset, payload.UnderwaterCount);
        }

        private static bool TryAllocateChunkSlice(
            ref PoolBlock[] freeBlocks,
            ref int freeBlockCount,
            int count,
            out int offset)
        {
            offset = -1;
            if (count <= 0)
                return false;

            for (int i = 0; i < freeBlockCount; i++)
            {
                PoolBlock block = freeBlocks[i];
                if (block.Length < count)
                    continue;

                offset = block.Offset;
                block.Offset += count;
                block.Length -= count;
                if (block.Length > 0)
                {
                    freeBlocks[i] = block;
                }
                else
                {
                    for (int shift = i; shift < freeBlockCount - 1; shift++)
                        freeBlocks[shift] = freeBlocks[shift + 1];

                    freeBlockCount--;
                    if (freeBlockCount >= 0 && freeBlockCount < freeBlocks.Length)
                        freeBlocks[freeBlockCount] = default;
                }

                return true;
            }

            return false;
        }

        private static void FreeChunkSlice(ref PoolBlock[] freeBlocks, ref int freeBlockCount, int offset, int count)
        {
            if (count <= 0 || offset < 0)
                return;

            EnsurePoolBlockCapacity(ref freeBlocks, freeBlockCount + 1);
            int insertIndex = freeBlockCount;
            int insertWatchdog = freeBlockCount + 1;
            while (insertIndex > 0 &&
                   offset < freeBlocks[insertIndex - 1].Offset &&
                   insertWatchdog-- > 0)
            {
                freeBlocks[insertIndex] = freeBlocks[insertIndex - 1];
                insertIndex--;
            }

            freeBlocks[insertIndex] = new PoolBlock { Offset = offset, Length = count };
            freeBlockCount++;

            int mergeIndex = Mathf.Max(0, insertIndex - 1);
            int mergeWatchdog = freeBlockCount + 1;
            while (mergeIndex < freeBlockCount - 1 && mergeWatchdog-- > 0)
            {
                PoolBlock current = freeBlocks[mergeIndex];
                PoolBlock next = freeBlocks[mergeIndex + 1];
                if (current.Offset + current.Length < next.Offset)
                {
                    mergeIndex++;
                    continue;
                }

                current.Length = Mathf.Max(current.Length, (next.Offset + next.Length) - current.Offset);
                freeBlocks[mergeIndex] = current;
                for (int shift = mergeIndex + 1; shift < freeBlockCount - 1; shift++)
                    freeBlocks[shift] = freeBlocks[shift + 1];

                freeBlockCount--;
                freeBlocks[freeBlockCount] = default;
            }
        }

        private static void EnsurePoolBlockCapacity(ref PoolBlock[] blocks, int requiredCount)
        {
            if (blocks != null && blocks.Length >= requiredCount)
                return;

            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, requiredCount));
            // COLD ALLOC: PoolBlock[nextCapacity] - chunk-pool free-list growth - owner: HectonMapMagicVegetationBridge
            PoolBlock[] expanded = new PoolBlock[nextCapacity];
            if (blocks != null && blocks.Length > 0)
                Array.Copy(blocks, expanded, blocks.Length);

            blocks = expanded;
        }

        private void EnsureDensityQueryCapacity(int chunkCount)
        {
            if (chunkCount <= 0)
                return;

            int capacity = _densityQueryChunkKeys != null ? _densityQueryChunkKeys.Length : 0;
            if (capacity >= chunkCount)
                return;

            RecordChunkQueueCapacityExceeded(capacity, chunkCount);
            _selectedChunkCount = math.min(_selectedChunkCount, capacity);
        }

        private void DisposeAllTileNativeCaches()
        {
            FinalizePendingTileHeightReadbacks();
            TryDisposeDeferredTileCacheReadbacks();
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
                QueueDeferredTileCacheDisposal(enumerator.Current.Value);

            TryDisposeDeferredTileCacheReadbacks();
        }

        private void DisposeTerrainHoleCache()
        {
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.TerrainHoleRecordsHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.TerrainHoleStreamingRecordsHandle);
            _terrainHoleCount = 0;
            _persistentTerrainHoleCount = 0;
            _megaWreckInteriorMaskHash = 0;
            _nextTerrainHoleId = 1;
        }

        private void DisposeTileNativeCaches(TileRuntimeState state)
        {
            if (state == null)
                return;

            if (HasChunkBuildJobsForTile(state.TileX, state.TileZ))
            {
                CompleteAndReleaseChunkBuildJobsForTile(state.TileX, state.TileZ);
                state.TileCacheDisposalDeferred = true;
                return;
            }

            bool deferHeightReadbackDisposal = TryDeferTileHeightReadbackDisposal(state);
            if (state.HeightReadbackPending && !state.HeightReadbackRequest.done)
                return;

            DisposeTileNativeCacheBuffer(ref state.PrimaryCacheBuffer);
            DisposeTileNativeCacheBuffer(ref state.SecondaryCacheBuffer);
            ReleaseVegetationMemoryBuffer(ref state.TerrainHoleMaskHandle);
            ReleaseTileNativeCacheSlot(state);

            if (!deferHeightReadbackDisposal)
                DisposeTileHeightReadbackData(state);
            state.ActiveCacheBufferIndex = 0;
            state.PendingCacheBufferIndex = 0;
            state.HeightReadbackPending = false;
            state.HeightReadbackRequest = default;
            state.TileCacheDisposalDeferred = false;
            state.TileCacheEvictionDeferred = false;
            state.HolesResolution = 0;
            state.TerrainHoleMaskCount = 0;
            state.TerrainHolesDirty = false;
            state.TerrainHoleMaskManaged = null;
        }

        private void QueueDeferredTileCacheDisposal(TileRuntimeState state)
        {
            if (state == null)
                return;

            TryDisposeDeferredTileCacheReadbacks();
            DisposeTileNativeCaches(state);
        }

        private static bool TryDeferTileHeightReadbackDisposal(TileRuntimeState state)
        {
            if (state == null || !state.HeightReadbackPending)
                return false;

            if (state.HeightReadbackRequest.done)
                return false;

            if (state.HeightReadbackDisposalDeferred)
                return true;

            TryDisposeDeferredTileCacheReadbacks();
            if (s_DeferredTileCacheDisposalCount >= s_DeferredTileCacheDisposals.Length)
                return false;

            s_DeferredTileCacheDisposals[s_DeferredTileCacheDisposalCount++] = new DeferredTileCacheDisposal
            {
                Request = state.HeightReadbackRequest,
                State = state
            };
            state.HeightReadbackDisposalDeferred = true;
            state.HeightReadbackPending = false;
            state.HeightReadbackRepairRequested = false;
            state.HeightReadbackRepairSampleCount = 0;
            state.HeightReadbackRequest = default;
            return true;
        }

        private static void TryDisposeDeferredTileCacheReadbacks()
        {
            for (int i = s_DeferredTileCacheDisposalCount - 1; i >= 0; i--)
            {
                DeferredTileCacheDisposal disposal = s_DeferredTileCacheDisposals[i];
                if (!disposal.Request.done)
                    continue;

                TileRuntimeState state = disposal.State;
                if (state != null)
                {
                    state.HeightReadbackDisposalDeferred = false;
                    state.HeightReadbackPending = false;
                    state.HeightReadbackRequest = default;
                    ReleaseTileHeightReadbackData(state);
                }

                int lastIndex = --s_DeferredTileCacheDisposalCount;
                s_DeferredTileCacheDisposals[i] = s_DeferredTileCacheDisposals[lastIndex];
                s_DeferredTileCacheDisposals[lastIndex] = default;
            }
        }

        private void DisposeTileNativeCacheBuffer(ref TileNativeCacheBuffer buffer)
        {
            ReleaseVegetationMemoryBuffer(ref buffer.SandMaskHandle);
            ReleaseVegetationMemoryBuffer(ref buffer.RockMaskHandle);
            ReleaseVegetationMemoryBuffer(ref buffer.HeightSamplesHandle);
            buffer.SampleCount = 0;
            buffer.HeightSampleCount = 0;
        }

        private void MarkAllTileTerrainHolesDirty()
        {
            if (_tileStates.Count <= 0)
                return;

            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
                MarkTileTerrainHolesDirty(enumerator.Current.Value);

            enumerator.Dispose();
        }

        private static void MarkTileTerrainHolesDirty(TileRuntimeState state)
        {
            if (state == null)
                return;

            state.TerrainHolesDirty = true;
        }

        private bool TryPrepareTileTerrainHoleMaskHot(TileRuntimeState state)
        {
            if (!TryEnsureTileTerrainHoleNativeMaskCapacity(state, out int safeResolution))
                return false;

            return state.TerrainHoleMaskManaged != null &&
                   state.TerrainHoleMaskManaged.GetLength(0) == safeResolution &&
                   state.TerrainHoleMaskManaged.GetLength(1) == safeResolution;
        }

        private bool EnsureTileTerrainHoleMaskCapacityCold(TileRuntimeState state)
        {
            if (!TryEnsureTileTerrainHoleNativeMaskCapacity(state, out int safeResolution))
                return false;

            if (state.TerrainHoleMaskManaged == null ||
                state.TerrainHoleMaskManaged.GetLength(0) != safeResolution ||
                state.TerrainHoleMaskManaged.GetLength(1) != safeResolution)
            {
                // COLD ALLOC: bool[safeResolution,safeResolution] - reusable TerrainData.SetHolesDelayLOD staging buffer for one MapMagic tile - owner: HectonMapMagicVegetationBridge
                state.TerrainHoleMaskManaged = new bool[safeResolution, safeResolution];
            }

            return true;
        }

        private bool TryEnsureTileTerrainHoleNativeMaskCapacity(TileRuntimeState state, out int safeResolution)
        {
            safeResolution = 0;
            if (state == null)
                return false;

            safeResolution = Mathf.Max(0, state.HolesResolution);
            int safeLength = safeResolution > 0 ? safeResolution * safeResolution : 0;
            if (safeLength <= 0)
            {
                ReleaseVegetationMemoryBuffer(ref state.TerrainHoleMaskHandle);
                state.TerrainHoleMaskCount = 0;
                state.TerrainHoleMaskManaged = null;
                return false;
            }

            if (state.TileNativeCacheSlot < 0 ||
                !EnsureAggregateBuffer(
                    ref state.TerrainHoleMaskHandle,
                    ResolveTileTerrainHoleMaskBufferId(state.TileNativeCacheSlot),
                    safeLength))
                return false;

            state.TerrainHoleMaskCount = safeLength;
            return true;
        }

        private void TryScheduleTerrainHoleJobs()
        {
            if (_tileStates.Count <= 0)
                return;

            int scheduledThisTick = 0;
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (scheduledThisTick >= TerrainHoleTileScheduleBudgetPerSlowTick)
                    break;

                TileRuntimeState state = enumerator.Current.Value;
                if (state == null ||
                    state.Terrain == null ||
                    state.TerrainData == null ||
                    !state.TerrainHolesDirty)
                {
                    continue;
                }

                state.HolesResolution = state.TerrainData.holesResolution;
                if (!TryPrepareTileTerrainHoleMaskHot(state))
                    continue;

                if (state.TerrainHoleMaskHandle.BufferID == 0u ||
                    state.TerrainHoleMaskCount <= 0 ||
                    state.HolesResolution <= 0)
                {
                    state.TerrainHolesDirty = false;
                    continue;
                }

                if (BuildAndApplyTerrainHoleMaskSync(state))
                {
                    state.TerrainHolesDirty = false;
                    scheduledThisTick++;
                }
            }

            enumerator.Dispose();
        }

        private bool BuildAndApplyTerrainHoleMaskSync(TileRuntimeState state)
        {
            if (state == null ||
                state.TerrainData == null ||
                state.HolesResolution <= 0 ||
                state.TerrainHoleMaskHandle.BufferID == 0u ||
                state.TerrainHoleMaskCount <= 0 ||
                state.TerrainHoleMaskManaged == null)
            {
                return false;
            }

            int resolution = state.HolesResolution;
            int expectedLength = resolution * resolution;
            if (expectedLength <= 0 || state.TerrainHoleMaskCount < expectedLength)
                return false;

            BufferID terrainHoleMaskBufferId = unchecked((BufferID)(int)state.TerrainHoleMaskHandle.BufferID);
            if (!TryAcquireVegetationMemoryBuffer(
                    ref state.TerrainHoleMaskHandle,
                    terrainHoleMaskBufferId,
                    state.TerrainHoleMaskCount,
                    NativeArrayOptions.ClearMemory,
                    out IDataVault terrainHoleMaskVault,
                    out NativeArray<byte> terrainHoleMask))
            {
                return false;
            }

            bool builtMask = false;
            try
            {
                int length = math.min(expectedLength, terrainHoleMask.Length);
                int holeCount = math.min(_terrainHoleCount, _terrainHoleRecords.Length);
                for (int sampleIndex = 0; sampleIndex < length; sampleIndex++)
                {
                    int y = sampleIndex / resolution;
                    int x = sampleIndex - (y * resolution);
                    float normalizedX = resolution <= 1 ? 0f : x / (float)(resolution - 1);
                    float normalizedZ = resolution <= 1 ? 0f : y / (float)(resolution - 1);
                    float worldX = state.TerrainPosition.x + (normalizedX * state.TerrainSize.x);
                    float worldZ = state.TerrainPosition.z + (normalizedZ * state.TerrainSize.z);
                    byte surface = 1;
                    for (int holeIndex = 0; holeIndex < holeCount; holeIndex++)
                    {
                        TerrainHoleRecord hole = _terrainHoleRecords[holeIndex];
                        if (hole.SourceType != TerrainHoleSourceType.CaveEntrance)
                            continue;

                        float dx = worldX - hole.X;
                        float dz = worldZ - hole.Z;
                        if ((dx * dx) + (dz * dz) <= hole.RadiusSq)
                        {
                            surface = 0;
                            break;
                        }
                    }

                    terrainHoleMask[sampleIndex] = surface;
                    state.TerrainHoleMaskManaged[y, x] = surface != 0;
                }

                builtMask = true;
            }
            finally
            {
                terrainHoleMaskVault.ReleaseWriteLock(
                    in state.TerrainHoleMaskHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if (!builtMask)
                return false;

            state.TerrainData.SetHolesDelayLOD(0, 0, state.TerrainHoleMaskManaged);
            state.TerrainData.SyncTexture(TerrainData.HolesTextureName);
            return true;
        }

        private bool EnsureTileNativeCacheBufferCapacity(
            TileRuntimeState state,
            int bufferIndex,
            int sampleCount,
            int heightSampleCount)
        {
            if (state == null)
                return false;

            TileNativeCacheBuffer buffer = bufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            if (state.TileNativeCacheSlot < 0 ||
                sampleCount <= 0 ||
                heightSampleCount <= 0 ||
                !EnsureAggregateBuffer(
                    ref buffer.SandMaskHandle,
                    ResolveTileNativeCacheBufferId(state.TileNativeCacheSlot, bufferIndex, TileNativeCacheSandOffset),
                    sampleCount) ||
                !EnsureAggregateBuffer(
                    ref buffer.RockMaskHandle,
                    ResolveTileNativeCacheBufferId(state.TileNativeCacheSlot, bufferIndex, TileNativeCacheRockOffset),
                    sampleCount) ||
                !EnsureAggregateBuffer(
                    ref buffer.HeightSamplesHandle,
                    ResolveTileNativeCacheBufferId(state.TileNativeCacheSlot, bufferIndex, TileNativeCacheHeightOffset),
                    heightSampleCount))
                return false;

            buffer.SampleCount = sampleCount;
            buffer.HeightSampleCount = heightSampleCount;

            if (bufferIndex == 0)
                state.PrimaryCacheBuffer = buffer;
            else
                state.SecondaryCacheBuffer = buffer;
            return true;
        }
    }
}
