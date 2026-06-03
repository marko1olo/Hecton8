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

        /// <summary>
        /// Marks resident vegetation chunks as corrupted and signals a rebuild through the bridge-owned dirty flag.
        /// </summary>
        public void CorruptZone(Vector3 worldPos, float radius)
        {
            if (_tileStates.Count <= 0)
                return;

            float clampedRadius = Mathf.Max(1f, radius);
            float radiusSq = clampedRadius * clampedRadius;
            bool changed = false;

            FixedTileStateMap.Enumerator tileEnumerator = _tileStates.GetEnumerator();
            while (tileEnumerator.MoveNext())
            {
                TileRuntimeState state = tileEnumerator.Current.Value;
                if (state == null || state.ChunkCountX <= 0 || state.ChunkCountZ <= 0)
                    continue;

                for (int chunkZ = 0; chunkZ < state.ChunkCountZ; chunkZ++)
                {
                    for (int chunkX = 0; chunkX < state.ChunkCountX; chunkX++)
                    {
                        GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
                        if (!DoesChunkBoundsIntersectCircle(minX, maxX, minZ, maxZ, worldPos.x, worldPos.z, radiusSq))
                            continue;

                        ChunkKey key = new ChunkKey(state.TileX, state.TileZ, chunkX, chunkZ);
                        bool isTrackedCorruption = IsChunkCorrupted(key) || MarkChunkCorrupted(key);
                        if (!isTrackedCorruption)
                            continue;

                        changed = true;
                        changed |= InvalidateChunkForCorruption(key);
                        if (TryGetDesiredChunkPriority(key, out float priority))
                            EnqueuePendingChunk(key, Mathf.Min(-1f, priority - 1f));
                    }
                }
            }

            if (changed)
                _activeSetDirty = true;
        }
        /// <summary>
        /// Registers a chunk-level persistent corruption marker for deterministic vegetation rebuilds.
        /// </summary>
        private bool MarkChunkCorrupted(ChunkKey key)
        {
            if (IsChunkCorrupted(key))
                return false;

            TrimCorruptionStateToBudget();
            int capacity = ResolveCorruptedChunkCapacity();
            if (_corruptedChunkCount >= capacity)
            {
                RecordChunkQueueCapacityExceeded(capacity, _corruptedChunkCount);
                return false;
            }

            _corruptedChunkOrder[_corruptedChunkCount++] = key;
            return true;
        }

        private bool IsChunkCorrupted(ChunkKey key)
        {
            for (int i = 0; i < _corruptedChunkCount; i++)
            {
                if (_corruptedChunkOrder[i].Equals(key))
                    return true;
            }

            return false;
        }

        private void ClearCorruptionStateForTile(int tileX, int tileZ)
        {
            for (int i = _corruptedChunkCount - 1; i >= 0; i--)
            {
                ChunkKey key = _corruptedChunkOrder[i];
                if (key.TileX != tileX || key.TileZ != tileZ)
                    continue;

                RemoveCorruptedChunkAt(i);
            }
        }

        private void TrimCorruptionStateToBudget()
        {
            int capacity = ResolveCorruptedChunkCapacity();
            if (_corruptedChunkCount < capacity)
                return;

            for (int i = 0; i < _corruptedChunkCount && _corruptedChunkCount >= capacity; i++)
            {
                ChunkKey key = _corruptedChunkOrder[i];
                if (_chunkPayloads.ContainsKey(key) ||
                    ContainsDesiredChunk(key))
                {
                    continue;
                }

                RemoveCorruptedChunkAt(i);
                i--;
            }
        }

        private int ResolveCorruptedChunkCapacity()
        {
            int requested = math.max(0, maxTrackedCorruptedChunks);
            return math.min(requested, _corruptedChunkOrder.Length);
        }

        private void RemoveCorruptedChunkAt(int index)
        {
            if ((uint)index >= (uint)_corruptedChunkCount)
                return;

            int moveCount = _corruptedChunkCount - index - 1;
            if (moveCount > 0)
                Array.Copy(_corruptedChunkOrder, index + 1, _corruptedChunkOrder, index, moveCount);

            _corruptedChunkCount--;
            _corruptedChunkOrder[_corruptedChunkCount] = default;
        }

        private bool InvalidateChunkForCorruption(ChunkKey key)
        {
            bool changed = false;
            if (_chunkPayloads.TryGetValue(key, out ChunkPayload payload))
            {
                ReleaseChunkPayloadStorage(payload);
                _chunkPayloads.Remove(key);
                RemoveChunkAbyssalNavPayload(key);
                RemoveChunkMegaWreckPayload(key);
                changed = true;
            }

            return changed;
        }

        private bool InvalidateChunksForNewPermanentEchoes(
            NativeArray<byte> previousEchoFlags,
            NativeArray<byte> currentEchoFlags)
        {
            if (!previousEchoFlags.IsCreated ||
                !currentEchoFlags.IsCreated ||
                _ecosystemThreatGridResolution <= 0)
            {
                return false;
            }

            bool changed = false;
            ClearEvictionScratch();
            FixedChunkPayloadMap.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                ChunkPayload payload = payloadEnumerator.Current.Value;
                if (!HasNewPermanentEchoInBounds(
                        payload.MinX,
                        payload.MaxX,
                        payload.MinZ,
                        payload.MaxZ,
                        previousEchoFlags,
                        currentEchoFlags))
                    continue;

                if (!TryAddEvictionScratch(payloadEnumerator.Current.Key))
                    break;
            }

            for (int i = 0; i < _evictionKeyCount; i++)
            {
                ChunkKey key = _evictionKeys[i];
                changed |= InvalidateChunkForCorruption(key);
                if (TryGetDesiredChunkPriority(key, out float priority))
                    EnqueuePendingChunk(key, Mathf.Min(-0.5f, priority - 0.5f));
            }

            if (changed)
                _activeSetDirty = true;

            return changed;
        }

        private bool InvalidateChunksForNewPermanentEchoes(NativeArray<ThreatPropagationStagingPoint> staging)
        {
            if (!staging.IsCreated ||
                _ecosystemThreatGridResolution <= 0)
            {
                return false;
            }

            bool changed = false;
            ClearEvictionScratch();
            FixedChunkPayloadMap.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                ChunkPayload payload = payloadEnumerator.Current.Value;
                if (!HasNewPermanentEchoInBounds(
                        payload.MinX,
                        payload.MaxX,
                        payload.MinZ,
                        payload.MaxZ,
                        staging))
                    continue;

                if (!TryAddEvictionScratch(payloadEnumerator.Current.Key))
                    break;
            }

            for (int i = 0; i < _evictionKeyCount; i++)
            {
                ChunkKey key = _evictionKeys[i];
                changed |= InvalidateChunkForCorruption(key);
                if (TryGetDesiredChunkPriority(key, out float priority))
                    EnqueuePendingChunk(key, Mathf.Min(-0.5f, priority - 0.5f));
            }

            if (changed)
                _activeSetDirty = true;

            return changed;
        }

        private bool HasNewPermanentEchoInBounds(
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            NativeArray<byte> previousEchoFlags,
            NativeArray<byte> currentEchoFlags)
        {
            if (!previousEchoFlags.IsCreated ||
                !currentEchoFlags.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f)
            {
                return false;
            }

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - _ecosystemThreatGridCenter.x) / threatGridCellSize) + halfExtent - 1, 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - _ecosystemThreatGridCenter.x) / threatGridCellSize) + halfExtent + 1, 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - _ecosystemThreatGridCenter.z) / threatGridCellSize) + halfExtent - 1, 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - _ecosystemThreatGridCenter.z) / threatGridCellSize) + halfExtent + 1, 0, _ecosystemThreatGridResolution - 1);

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int index = (cellZ * _ecosystemThreatGridResolution) + cellX;
                    if ((uint)index >= (uint)previousEchoFlags.Length ||
                        (uint)index >= (uint)currentEchoFlags.Length ||
                        currentEchoFlags[index] == 0 ||
                        previousEchoFlags[index] != 0)
                    {
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private bool HasNewPermanentEchoInBounds(
            float minX,
            float maxX,
            float minZ,
            float maxZ,
            NativeArray<ThreatPropagationStagingPoint> staging)
        {
            if (!staging.IsCreated ||
                _ecosystemThreatGridResolution <= 0 ||
                threatGridCellSize <= 0f)
            {
                return false;
            }

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int minCellX = Mathf.Clamp(Mathf.FloorToInt((minX - _ecosystemThreatGridCenter.x) / threatGridCellSize) + halfExtent - 1, 0, _ecosystemThreatGridResolution - 1);
            int maxCellX = Mathf.Clamp(Mathf.CeilToInt((maxX - _ecosystemThreatGridCenter.x) / threatGridCellSize) + halfExtent + 1, 0, _ecosystemThreatGridResolution - 1);
            int minCellZ = Mathf.Clamp(Mathf.FloorToInt((minZ - _ecosystemThreatGridCenter.z) / threatGridCellSize) + halfExtent - 1, 0, _ecosystemThreatGridResolution - 1);
            int maxCellZ = Mathf.Clamp(Mathf.CeilToInt((maxZ - _ecosystemThreatGridCenter.z) / threatGridCellSize) + halfExtent + 1, 0, _ecosystemThreatGridResolution - 1);

            for (int cellZ = minCellZ; cellZ <= maxCellZ; cellZ++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    int index = (cellZ * _ecosystemThreatGridResolution) + cellX;
                    if ((uint)index >= (uint)staging.Length)
                        continue;

                    ThreatPropagationStagingPoint point = staging[index];
                    if (point.NextEcho == 0 || point.PreviousEcho != 0)
                        continue;

                    return true;
                }
            }

            return false;
        }
    }
}
