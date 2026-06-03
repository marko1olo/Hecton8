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

        private bool TryValidateResidentTileCaches()
        {
            bool readbackChanged = FinalizePendingTileHeightReadbacks();
            if (_tileStates.Count == 0 ||
                !TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition))
            {
                EnforceTileCacheLruBudget();
                return readbackChanged;
            }

            float now = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextCacheValidationTime)
            {
                EnforceTileCacheLruBudget();
                return readbackChanged;
            }

            _nextCacheValidationTime = now + CacheValidationInterval;
            bool changed = false;
            int remainingBudget = CacheValidationTileBudget;
            long validatedTileA = long.MinValue;
            long validatedTileB = long.MinValue;

            if (TryFindPlayerTileState(playerRuntimePosition, out TileRuntimeState playerTileState) &&
                playerTileState != null)
            {
                long playerTileKey = PackTileCoord(playerTileState.TileX, playerTileState.TileZ);
                if (TryValidateTileState(playerTileState))
                    changed = true;

                validatedTileA = playerTileKey;
                remainingBudget--;
            }

            if (_selectedChunkCount <= 0 || remainingBudget <= 0)
            {
                EnforceTileCacheLruBudget();
                return changed || readbackChanged;
            }

            int startIndex = _cacheValidationChunkCursor;
            for (int scanned = 0; scanned < _selectedChunkCount && remainingBudget > 0; scanned++)
            {
                int selectedIndex = (startIndex + scanned) % _selectedChunkCount;
                ChunkKey key = _selectedChunkKeys[selectedIndex];
                long tileKey = PackTileCoord(key.TileX, key.TileZ);
                if (tileKey == validatedTileA || tileKey == validatedTileB)
                    continue;

                if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) || state == null)
                    continue;

                if (TryValidateTileState(state))
                    changed = true;

                if (validatedTileA == long.MinValue)
                    validatedTileA = tileKey;
                else
                    validatedTileB = tileKey;

                remainingBudget--;
                _cacheValidationChunkCursor = (selectedIndex + 1) % _selectedChunkCount;
            }

            EnforceTileCacheLruBudget();
            return changed || readbackChanged;
        }

        private bool FinalizePendingTileHeightReadbacks()
        {
            if (_tileStates.Count <= 0)
                return false;

            bool changed = false;
            _tileStateRemovalScratchKeyCount = 0;
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null)
                    continue;

                if (state.PendingRemoval)
                {
                    if (!state.HeightReadbackPending || TryFinalizeTileHeightReadback(state))
                    {
                        if (_tileStateRemovalScratchKeyCount < _tileStateRemovalScratchKeys.Length)
                            _tileStateRemovalScratchKeys[_tileStateRemovalScratchKeyCount++] = enumerator.Current.Key;
                        else
                            RecordChunkQueueCapacityExceeded(_tileStateRemovalScratchKeys.Length, _tileStateRemovalScratchKeyCount);
                    }

                    continue;
                }

                if (!state.HeightReadbackPending)
                    continue;

                if (TryFinalizeTileHeightReadback(state))
                {
                    InvalidateTileChunks(state.TileX, state.TileZ, state.ChunkCountX, state.ChunkCountZ);
                    changed = true;
                }
            }

            enumerator.Dispose();

            for (int i = 0; i < _tileStateRemovalScratchKeyCount; i++)
                FinalizeDeferredTileRemoval(_tileStateRemovalScratchKeys[i]);

            _tileStateRemovalScratchKeyCount = 0;

            return changed;
        }

        private bool TryFinalizeDeferredTileCacheDisposals()
        {
            if (_tileStates.Count <= 0)
                return false;

            bool changed = false;
            _tileStateRemovalScratchKeyCount = 0;
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null ||
                    !state.TileCacheDisposalDeferred ||
                    HasChunkBuildJobsForTile(state.TileX, state.TileZ))
                {
                    continue;
                }

                if (_tileStateRemovalScratchKeyCount < _tileStateRemovalScratchKeys.Length)
                    _tileStateRemovalScratchKeys[_tileStateRemovalScratchKeyCount++] = enumerator.Current.Key;
                else
                    RecordChunkQueueCapacityExceeded(_tileStateRemovalScratchKeys.Length, _tileStateRemovalScratchKeyCount);
            }

            enumerator.Dispose();

            for (int i = 0; i < _tileStateRemovalScratchKeyCount; i++)
                changed |= TryFinalizeDeferredTileCacheDisposal(_tileStateRemovalScratchKeys[i]);

            _tileStateRemovalScratchKeyCount = 0;
            return changed;
        }

        private bool TryFinalizeDeferredTileCacheDisposal(long tileKey)
        {
            if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) ||
                state == null ||
                !state.TileCacheDisposalDeferred ||
                HasChunkBuildJobsForTile(state.TileX, state.TileZ))
            {
                return false;
            }

            bool pendingRemoval = state.PendingRemoval;
            bool pendingEviction = state.TileCacheEvictionDeferred;
            state.TileCacheDisposalDeferred = false;
            state.TileCacheEvictionDeferred = false;
            DisposeTileNativeCaches(state);
            if (state.TileCacheDisposalDeferred)
                return false;

            if (pendingRemoval)
            {
                _tileStates.Remove(tileKey);
                _activeSetDirty = true;
                return true;
            }

            if (pendingEviction)
            {
                ClearEvictedTileCacheMetadata(state);
                return true;
            }

            return true;
        }

        private bool TryFinalizeTileHeightReadback(TileRuntimeState state)
        {
            if (state == null || !state.HeightReadbackPending || state.HeightReadbackDisposalDeferred)
                return false;

            if (!state.HeightReadbackRequest.done)
                return false;

            bool completedSuccessfully = !state.HeightReadbackRequest.hasError;
            if (!completedSuccessfully)
            {
                state.HeightReadbackPending = false;
                state.HeightmapHash = 0;
                state.HeightmapUpdateCount = 0u;
                return false;
            }

            TileNativeCacheBuffer pendingBuffer = state.PendingCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;
            if (state.TileNativeCacheSlot < 0 || pendingBuffer.HeightSampleCount <= 0)
            {
                state.HeightReadbackPending = false;
                return false;
            }

            NativeArray<ushort> readbackData = state.HeightReadbackData;
            if (!readbackData.IsCreated || readbackData.Length < pendingBuffer.HeightSampleCount)
            {
                state.HeightReadbackPending = false;
                return false;
            }

            BufferID heightBufferId = ResolveTileNativeCacheBufferId(
                state.TileNativeCacheSlot,
                state.PendingCacheBufferIndex,
                TileNativeCacheHeightOffset);
            if (!TryAcquireVegetationMemoryBuffer(
                    ref pendingBuffer.HeightSamplesHandle,
                    heightBufferId,
                    pendingBuffer.HeightSampleCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault heightVault,
                    out NativeArray<ushort> heightSamples))
            {
                if (state.PendingCacheBufferIndex == 0)
                    state.PrimaryCacheBuffer = pendingBuffer;
                else
                    state.SecondaryCacheBuffer = pendingBuffer;
                return false;
            }

            try
            {
                NativeArray<ushort>.Copy(readbackData, 0, heightSamples, 0, pendingBuffer.HeightSampleCount);
            }
            finally
            {
                heightVault.ReleaseWriteLock(
                    in pendingBuffer.HeightSamplesHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            if (state.PendingCacheBufferIndex == 0)
                state.PrimaryCacheBuffer = pendingBuffer;
            else
                state.SecondaryCacheBuffer = pendingBuffer;

            state.HeightReadbackPending = false;
            state.ActiveCacheBufferIndex = state.PendingCacheBufferIndex;
            TouchTileCacheState(state);
            unchecked
            {
                state.CacheRevision++;
            }

            return true;
        }

        private bool CanRefreshThreatSpatialSnapshots()
        {
            return !_threatPropagationScheduled &&
                   !_flowFieldScheduled &&
                   !_abyssalThermalGridScheduled &&
                   !_abyssalPathScheduled;
        }

        private void FinalizeDeferredTileRemoval(long tileKey)
        {
            if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) || state == null)
                return;

            if (state.HeightReadbackPending)
                return;

            DisposeTileNativeCaches(state);
            if (state.TileCacheDisposalDeferred)
                return;

            _tileStates.Remove(tileKey);
            _activeSetDirty = true;
        }

        private bool TryFindPlayerTileState(Vector3 playerPosition, out TileRuntimeState state)
        {
            return TryFindTileStateAtPosition(playerPosition, out state);
        }

        private bool TryFindTileStateAtPosition(Vector3 worldPosition, out TileRuntimeState state)
        {
            state = null;
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState candidate = enumerator.Current.Value;
                if (candidate == null || candidate.PendingRemoval)
                    continue;

                Vector3 terrainMin = candidate.TerrainPosition;
                Vector3 terrainMax = candidate.TerrainPosition + candidate.TerrainSize;
                if (worldPosition.x < terrainMin.x || worldPosition.x > terrainMax.x)
                    continue;
                if (worldPosition.z < terrainMin.z || worldPosition.z > terrainMax.z)
                    continue;

                state = candidate;
                return true;
            }

            return false;
        }

        private bool TryGetActiveTileCache(
            TileRuntimeState state,
            out NativeArray<byte> sandMask,
            out NativeArray<byte> rockMask,
            out NativeArray<ushort> heightSamples)
        {
            sandMask = default;
            rockMask = default;
            heightSamples = default;
            if (state == null || state.TileCacheDisposalDeferred)
                return false;

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            if (state.TileNativeCacheSlot < 0 ||
                buffer.SampleCount <= 0 ||
                buffer.HeightSampleCount <= 0 ||
                buffer.SandMaskHandle.BufferID == 0u ||
                buffer.RockMaskHandle.BufferID == 0u ||
                buffer.HeightSamplesHandle.BufferID == 0u)
            {
                return false;
            }

            BufferID sandBufferId = unchecked((BufferID)(int)buffer.SandMaskHandle.BufferID);
            BufferID rockBufferId = unchecked((BufferID)(int)buffer.RockMaskHandle.BufferID);
            BufferID heightBufferId = unchecked((BufferID)(int)buffer.HeightSamplesHandle.BufferID);
            if (!TryReadVegetationMemoryBuffer(in buffer.SandMaskHandle, sandBufferId, buffer.SampleCount, out sandMask) ||
                !TryReadVegetationMemoryBuffer(in buffer.RockMaskHandle, rockBufferId, buffer.SampleCount, out rockMask) ||
                !TryReadVegetationMemoryBuffer(in buffer.HeightSamplesHandle, heightBufferId, buffer.HeightSampleCount, out heightSamples))
            {
                sandMask = default;
                rockMask = default;
                heightSamples = default;
                return false;
            }

            return true;
        }

        private static void TouchTileCacheState(TileRuntimeState state)
        {
            if (state == null)
                return;

            state.LastAccessFrame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
        }

        private static bool HasActiveTileCache(TileRuntimeState state)
        {
            if (state == null || state.TileCacheDisposalDeferred)
                return false;

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            return state.TileNativeCacheSlot >= 0 &&
                   buffer.SampleCount > 0 &&
                   buffer.HeightSampleCount > 0 &&
                   buffer.SandMaskHandle.BufferID != 0u &&
                   buffer.RockMaskHandle.BufferID != 0u &&
                   buffer.HeightSamplesHandle.BufferID != 0u;
        }

        private void EvictTileCache(TileRuntimeState state)
        {
            if (state == null)
                return;

            DisposeTileNativeCaches(state);
            if (state.TileCacheDisposalDeferred)
            {
                state.TileCacheEvictionDeferred = true;
                return;
            }

            ClearEvictedTileCacheMetadata(state);
        }

        private static void ClearEvictedTileCacheMetadata(TileRuntimeState state)
        {
            if (state == null)
                return;

            state.AlphamapTextureCache = null;
            state.HeightTextureCache = null;
            state.AlphamapTextureCount = 0;
            state.CombinedAlphamapHash = 0;
            state.CombinedAlphamapUpdateCount = 0u;
            state.HeightmapHash = 0;
            state.HeightmapUpdateCount = 0u;
            state.CacheRevision = 0;
            state.LastAccessFrame = 0u;
        }

        private long ResolveProtectedTileKey()
        {
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition) ||
                !TryFindPlayerTileState(playerRuntimePosition, out TileRuntimeState playerTileState) ||
                playerTileState == null)
            {
                return long.MinValue;
            }

            return PackTileCoord(playerTileState.TileX, playerTileState.TileZ);
        }

        private void EnforceTileCacheLruBudget()
        {
            if (_tileStates.Count <= TileCacheLruCapacity)
                return;

            long protectedTileKey = ResolveProtectedTileKey();
            int lruIterations = 0;
            while (lruIterations < MaxTileCacheLruIterations)
            {
                lruIterations++;
                int residentCacheCount = 0;
                long evictionKey = long.MinValue;
                uint oldestAccessFrame = uint.MaxValue;

                FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    long tileKey = enumerator.Current.Key;
                    TileRuntimeState state = enumerator.Current.Value;
                    if (!HasActiveTileCache(state))
                        continue;

                    residentCacheCount++;
                    if (tileKey == protectedTileKey ||
                        state == null ||
                        state.HeightReadbackPending ||
                        state.TileCacheDisposalDeferred)
                    {
                        continue;
                    }

                    if (state.LastAccessFrame <= oldestAccessFrame)
                    {
                        oldestAccessFrame = state.LastAccessFrame;
                        evictionKey = tileKey;
                    }
                }

                if (residentCacheCount <= TileCacheLruCapacity || evictionKey == long.MinValue)
                    return;

                if (_tileStates.TryGetValue(evictionKey, out TileRuntimeState evictionState))
                    EvictTileCache(evictionState);
            }

            LogLoopGuardHit(nameof(EnforceTileCacheLruBudget), MaxTileCacheLruIterations);
        }

        private static bool HasTileCacheSignatureChanged(TileRuntimeState state, TerrainData terrainData)
        {
            if (state == null || terrainData == null)
                return false;

            if (!TryRefreshTerrainTextureCachesHot(state, terrainData))
                return true;

            CaptureTileCacheSignature(
                state.AlphamapTextureCache,
                state.HeightTextureCache,
                out int alphamapTextureCount,
                out int combinedAlphamapHash,
                out uint combinedAlphamapUpdateCount,
                out int heightmapHash,
                out uint heightmapUpdateCount);

            return state.AlphamapTextureCount != alphamapTextureCount ||
                   state.CombinedAlphamapHash != combinedAlphamapHash ||
                   state.CombinedAlphamapUpdateCount != combinedAlphamapUpdateCount ||
                   state.HeightmapHash != heightmapHash ||
                   state.HeightmapUpdateCount != heightmapUpdateCount;
        }

        private bool TryValidateTileState(TileRuntimeState state)
        {
            if (state == null || state.TerrainData == null)
                return false;

            if (!HasActiveTileCache(state))
                return CacheTileMasks(state, state.TerrainData);

            if (!HasTileCacheSignatureChanged(state, state.TerrainData))
                return false;

            return CacheTileMasks(state, state.TerrainData);
        }
    }
}
