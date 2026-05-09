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

        private bool TryValidateResidentTileCaches()
        {
            bool readbackChanged = FinalizePendingTileHeightReadbacks();
            if (_tileStates.Count == 0 ||
                !TryResolvePlayerRuntimePositionFromAup(out Vector3 playerRuntimePosition))
            {
                EnforceTileCacheLruBudget();
                return readbackChanged;
            }

            if (Time.unscaledTime < _nextCacheValidationTime)
            {
                EnforceTileCacheLruBudget();
                return readbackChanged;
            }

            _nextCacheValidationTime = Time.unscaledTime + CacheValidationInterval;
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
            _tileStateRemovalScratchKeys.Clear();
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null)
                    continue;

                if (state.PendingRemoval)
                {
                    if (!state.HeightReadbackPending || TryFinalizeTileHeightReadback(state))
                        _tileStateRemovalScratchKeys.Add(enumerator.Current.Key);

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

            for (int i = 0; i < _tileStateRemovalScratchKeys.Count; i++)
                FinalizeDeferredTileRemoval(_tileStateRemovalScratchKeys[i]);

            return changed;
        }

        private static bool TryFinalizeTileHeightReadback(TileRuntimeState state)
        {
            if (state == null || !state.HeightReadbackPending)
                return false;

            if (!state.HeightReadbackRequest.done)
                return false;

            bool completedSuccessfully = !state.HeightReadbackRequest.hasError;
            state.HeightReadbackPending = false;
            if (!completedSuccessfully)
            {
                state.HeightmapHash = 0;
                state.HeightmapUpdateCount = 0u;
                return false;
            }

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
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
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

        private static bool TryGetActiveTileCache(
            TileRuntimeState state,
            out NativeArray<byte> sandMask,
            out NativeArray<byte> rockMask,
            out NativeArray<ushort> heightSamples)
        {
            sandMask = default;
            rockMask = default;
            heightSamples = default;
            if (state == null)
                return false;

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            if (!buffer.SandMaskNative.IsCreated ||
                !buffer.RockMaskNative.IsCreated ||
                !buffer.HeightSamplesNative.IsCreated)
            {
                return false;
            }

            sandMask = buffer.SandMaskNative;
            rockMask = buffer.RockMaskNative;
            heightSamples = buffer.HeightSamplesNative;
            TouchTileCacheState(state);
            return true;
        }

        private static void TouchTileCacheState(TileRuntimeState state)
        {
            if (state == null)
                return;

            state.LastAccessFrame = unchecked((uint)math.max(0, Time.frameCount));
        }

        private static bool HasActiveTileCache(TileRuntimeState state)
        {
            if (state == null)
                return false;

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            return buffer.SandMaskNative.IsCreated &&
                   buffer.RockMaskNative.IsCreated &&
                   buffer.HeightSamplesNative.IsCreated;
        }

        private static void EvictTileCache(TileRuntimeState state)
        {
            if (state == null)
                return;

            DisposeTileNativeCaches(state);
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

                Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    long tileKey = enumerator.Current.Key;
                    TileRuntimeState state = enumerator.Current.Value;
                    if (!HasActiveTileCache(state))
                        continue;

                    residentCacheCount++;
                    if (tileKey == protectedTileKey || state == null || state.HeightReadbackPending)
                        continue;

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

            RefreshTerrainTextureCaches(state, terrainData);
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
