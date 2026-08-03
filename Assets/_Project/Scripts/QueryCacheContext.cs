// ============================================================================
// HECTON-8 — QueryCacheContext.cs  v2.0
// Per-frame physics query result caching and deduplication.
//
// PURPOSE:
//   Avoid redundant scene-probe or batch queries from the same source
//   (Camera looking at interactions, tools, and UI) within a single frame.
//
// ZERO GC GUARANTEE:
//   NOTE: PlayerLook uses fixed slots; generic named contexts retain Dictionary storage.
//   • Quantized hashing for deterministic cache hits.
//   • Persistent Dictionary cache (Clear() instead of new).
//   • Struct-based result storage.
//
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Physics
{
    public struct CachedQueryResult
    {
        public QueryResult result;
        public int frameIndex;
    }

    public sealed class QueryCacheContext : IPlayerLookQueryCache
    {
        private const int FixedCacheCapacity = 64;
        private const int FixedCacheMask = FixedCacheCapacity - 1;
        private const byte FixedSlotEmpty = 0;
        private const byte FixedSlotOccupied = 1;
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const float QueryHashInvEpsilon = 1000f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            CacheHits = 0;
        }

        private readonly Dictionary<ulong, CachedQueryResult> _cache;
        private readonly ulong[] _fixedKeys;
        private readonly CachedQueryResult[] _fixedValues;
        private readonly byte[] _fixedStates;

        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public static int CacheHits;

        private int _lastUpdateFrame = -1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _fixedCacheSaturationLogged;
#endif

        public QueryCacheContext(string name, bool useFixedStorage = false)
        {
            _ = name;

            if (useFixedStorage)
            {
                // COLD ALLOC: ulong[64] - fixed hot PlayerLook query cache keys - owner: QueryCacheContext
                _fixedKeys = new ulong[FixedCacheCapacity];
                // COLD ALLOC: CachedQueryResult[64] - fixed hot PlayerLook query cache values - owner: QueryCacheContext
                _fixedValues = new CachedQueryResult[FixedCacheCapacity];
                // COLD ALLOC: byte[64] - fixed hot PlayerLook query cache states - owner: QueryCacheContext
                _fixedStates = new byte[FixedCacheCapacity];
                return;
            }

            // COLD ALLOC: Dictionary<ulong,CachedQueryResult>[32] - generic named query cache - owner: QueryCacheContext
            _cache = new Dictionary<ulong, CachedQueryResult>(32);
        }

        public void InvalidateIfStale()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastUpdateFrame == frame)
                return;

            if (_fixedStates != null)
                ClearFixedSlots();
            else
                _cache.Clear();

            Hits = 0;
            Misses = 0;
            _lastUpdateFrame = frame;
        }

        public bool TryGet(
            Ray ray,
            float distance,
            int mask,
            QueryTriggerInteraction triggerMode,
            out QueryResult result)
        {
            InvalidateIfStale();

            ulong key = ComputeHash(ray, distance, mask, triggerMode);
            if (_fixedStates != null)
                return TryGetFixed(key, out result);

            if (_cache.TryGetValue(key, out var cached))
            {
                CacheHits++;
                Hits++;
                result = cached.result;
                return true;
            }

            Misses++;
            result = default;
            return false;
        }

        public bool TryGetHit(
            Ray ray,
            float distance,
            int mask,
            QueryTriggerInteraction triggerMode,
            out InteractionSurfaceHit hit)
        {
            if (TryGet(ray, distance, mask, triggerMode, out QueryResult result) &&
                result.hasHit != 0)
            {
                hit = result.hit;
                return true;
            }

            hit = default;
            return false;
        }

        public void Set(
            Ray ray,
            float distance,
            int mask,
            QueryTriggerInteraction triggerMode,
            QueryResult result)
        {
            InvalidateIfStale();

            ulong key = ComputeHash(ray, distance, mask, triggerMode);
            if (_fixedStates != null)
            {
                SetFixed(key, result);
                return;
            }

            _cache[key] = new CachedQueryResult { result = result, frameIndex = SystemDispatcher.CurrentFrameIndex };
        }

        public void SetHit(
            Ray ray,
            float distance,
            int mask,
            QueryTriggerInteraction triggerMode,
            InteractionSurfaceHit hit)
        {
            Set(
                ray,
                distance,
                mask,
                triggerMode,
                new QueryResult { hasHit = 1, hit = hit });
        }

        private bool TryGetFixed(ulong key, out QueryResult result)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            int index = HashFixedSlot(key);
            for (int probe = 0; probe < FixedCacheCapacity; probe++)
            {
                byte state = _fixedStates[index];
                if (state == FixedSlotEmpty)
                {
                    Misses++;
                    result = default;
                    return false;
                }

                if (_fixedKeys[index] == key)
                {
                    CachedQueryResult cached = _fixedValues[index];
                    if (cached.frameIndex == frame)
                    {
                        CacheHits++;
                        Hits++;
                        result = cached.result;
                        return true;
                    }

                    Misses++;
                    result = default;
                    return false;
                }

                index = (index + 1) & FixedCacheMask;
            }

            Misses++;
            result = default;
            return false;
        }

        private void SetFixed(ulong key, QueryResult result)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            int initialIndex = HashFixedSlot(key);
            int index = initialIndex;
            for (int probe = 0; probe < FixedCacheCapacity; probe++)
            {
                byte state = _fixedStates[index];
                if (state == FixedSlotEmpty ||
                    _fixedKeys[index] == key)
                {
                    WriteFixedSlot(index, key, result, frame);
                    return;
                }

                index = (index + 1) & FixedCacheMask;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_fixedCacheSaturationLogged)
            {
                _fixedCacheSaturationLogged = true;
                Hecton8.Core.H8Debug.LogWarning("[QueryCacheContext] Fixed PlayerLook query cache saturated. Increase FixedCacheCapacity.");
            }
#endif
            // Preserve open-address invariants; this cache is per-frame, so bounded clear beats corrupt eviction.
            ClearFixedSlots();
            WriteFixedSlot(initialIndex, key, result, frame);
        }

        private void WriteFixedSlot(int index, ulong key, QueryResult result, int frame)
        {
            _fixedKeys[index] = key;
            _fixedValues[index] = new CachedQueryResult { result = result, frameIndex = frame };
            _fixedStates[index] = FixedSlotOccupied;
        }

        private void ClearFixedSlots()
        {
            for (int i = 0; i < FixedCacheCapacity; i++)
            {
                _fixedKeys[i] = 0UL;
                _fixedValues[i] = default;
                _fixedStates[i] = FixedSlotEmpty;
            }
        }

        // ════════════════════════════════════════════════════════════════════════════════
        //  STABLE HASHING
        // ════════════════════════════════════════════════════════════════════════════════

        private static ulong ComputeHash(Ray ray, float distance, int mask, QueryTriggerInteraction triggerMode)
        {
            ulong h = FnvOffsetBasis;
            h = MixSigned(h, QuantizeSigned(ray.origin.x));
            h = MixSigned(h, QuantizeSigned(ray.origin.y));
            h = MixSigned(h, QuantizeSigned(ray.origin.z));
            h = MixSigned(h, QuantizeSigned(ray.direction.x));
            h = MixSigned(h, QuantizeSigned(ray.direction.y));
            h = MixSigned(h, QuantizeSigned(ray.direction.z));
            h = MixSigned(h, QuantizeSigned(distance));
            h = MixSigned(h, mask);
            h = MixSigned(h, (int)triggerMode);
            return h;
        }

        private static int HashFixedSlot(ulong key)
        {
            unchecked
            {
                key ^= key >> 33;
                key *= 0xff51afd7ed558ccdUL;
                key ^= key >> 33;
                key *= 0xc4ceb9fe1a85ec53UL;
                key ^= key >> 33;
                return (int)key & FixedCacheMask;
            }
        }

        private static int QuantizeSigned(float value)
        {
            float scaled = value * QueryHashInvEpsilon;
            if (scaled >= int.MaxValue)
                return int.MaxValue;
            if (scaled <= int.MinValue)
                return int.MinValue;

            return scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
        }

        private static ulong MixSigned(ulong hash, int value)
        {
            unchecked
            {
                return (hash ^ (uint)value) * FnvPrime;
            }
        }
    }

    public static class GlobalQueryCacheManager
    {
        private const string PlayerLookContextName = "PlayerLook";

        private static readonly Dictionary<string, QueryCacheContext> _contexts = new Dictionary<string, QueryCacheContext>(8);
        private static QueryCacheContext s_playerLookContext = new QueryCacheContext(PlayerLookContextName, useFixedStorage: true);

        /// <summary>
        /// Cached player look-query context for hot interaction, tool, and HUD probe paths.
        /// </summary>
        public static QueryCacheContext PlayerLook
        {
            get
            {
                if (s_playerLookContext == null)
                    s_playerLookContext = new QueryCacheContext(PlayerLookContextName, useFixedStorage: true);

                return s_playerLookContext;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _contexts.Clear();
            s_playerLookContext = new QueryCacheContext(PlayerLookContextName, useFixedStorage: true);
        }

        public static QueryCacheContext GetContext(string name)
        {
            if (string.IsNullOrEmpty(name) || name == PlayerLookContextName)
                return PlayerLook;

            if (!_contexts.TryGetValue(name, out var ctx))
            {
                ctx = new QueryCacheContext(name);
                _contexts[name] = ctx;
            }

            return ctx;
        }
    }
}
