using System;
using System.Collections.Generic;
using Hecton8.Core;
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

            Dictionary<long, TileRuntimeState>.Enumerator tileEnumerator = _tileStates.GetEnumerator();
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
            if (_corruptedChunkKeys.Contains(key))
                return false;

            TrimCorruptionStateToBudget();
            if (_corruptedChunkKeys.Count >= maxTrackedCorruptedChunks)
                return false;

            _corruptedChunkKeys.Add(key);
            _corruptedChunkOrder.Add(key);
            return true;
        }

        private bool IsChunkCorrupted(ChunkKey key)
        {
            return _corruptedChunkKeys.Contains(key);
        }

        private void ClearCorruptionStateForTile(int tileX, int tileZ)
        {
            for (int i = _corruptedChunkOrder.Count - 1; i >= 0; i--)
            {
                ChunkKey key = _corruptedChunkOrder[i];
                if (key.TileX != tileX || key.TileZ != tileZ)
                    continue;

                _corruptedChunkKeys.Remove(key);
                _corruptedChunkOrder.RemoveAt(i);
            }
        }

        private void TrimCorruptionStateToBudget()
        {
            if (_corruptedChunkKeys.Count < maxTrackedCorruptedChunks)
                return;

            for (int i = 0; i < _corruptedChunkOrder.Count && _corruptedChunkKeys.Count >= maxTrackedCorruptedChunks; i++)
            {
                ChunkKey key = _corruptedChunkOrder[i];
                if (_chunkPayloads.ContainsKey(key) ||
                    _chunkBuildJobs.ContainsKey(key) ||
                    ContainsDesiredChunk(key))
                {
                    continue;
                }

                _corruptedChunkKeys.Remove(key);
                _corruptedChunkOrder.RemoveAt(i);
                i--;
            }
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

            if (_chunkBuildJobs.ContainsKey(key))
            {
                CancelChunkBuildJob(key);
                changed = true;
            }

            return changed;
        }

        private bool InvalidateChunksForNewPermanentEchoes()
        {
            if (!_nativeMemory.EcosystemThreatEchoCurrentNative.IsCreated ||
                !_nativeMemory.EcosystemThreatEchoNextNative.IsCreated ||
                _ecosystemThreatGridResolution <= 0)
            {
                return false;
            }

            bool changed = false;
            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkPayload>.Enumerator payloadEnumerator = _chunkPayloads.GetEnumerator();
            while (payloadEnumerator.MoveNext())
            {
                ChunkPayload payload = payloadEnumerator.Current.Value;
                if (!HasNewPermanentEchoInBounds(payload.MinX, payload.MaxX, payload.MinZ, payload.MaxZ))
                    continue;

                _evictionKeys.Add(payloadEnumerator.Current.Key);
            }

            for (int i = 0; i < _evictionKeys.Count; i++)
            {
                ChunkKey key = _evictionKeys[i];
                changed |= InvalidateChunkForCorruption(key);
                if (TryGetDesiredChunkPriority(key, out float priority))
                    EnqueuePendingChunk(key, Mathf.Min(-0.5f, priority - 0.5f));
            }

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator jobEnumerator = _chunkBuildJobs.GetEnumerator();
            while (jobEnumerator.MoveNext())
            {
                ChunkBuildJobState jobState = jobEnumerator.Current.Value;
                if (jobState == null ||
                    !HasNewPermanentEchoInBounds(jobState.PayloadHeader.MinX, jobState.PayloadHeader.MaxX, jobState.PayloadHeader.MinZ, jobState.PayloadHeader.MaxZ))
                {
                    continue;
                }

                _jobScratchKeys.Add(jobEnumerator.Current.Key);
            }

            for (int i = 0; i < _jobScratchKeys.Count; i++)
            {
                ChunkKey key = _jobScratchKeys[i];
                CancelChunkBuildJob(key);
                changed = true;
                if (TryGetDesiredChunkPriority(key, out float priority))
                    EnqueuePendingChunk(key, Mathf.Min(-0.5f, priority - 0.5f));
            }

            if (changed)
                _activeSetDirty = true;

            return changed;
        }

        private bool HasNewPermanentEchoInBounds(float minX, float maxX, float minZ, float maxZ)
        {
            if (!_nativeMemory.EcosystemThreatEchoCurrentNative.IsCreated ||
                !_nativeMemory.EcosystemThreatEchoNextNative.IsCreated ||
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
                    if (_nativeMemory.EcosystemThreatEchoCurrentNative[index] == 0 || _nativeMemory.EcosystemThreatEchoNextNative[index] != 0)
                        continue;

                    return true;
                }
            }

            return false;
        }
    }
}
