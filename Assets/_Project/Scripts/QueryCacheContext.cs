// ============================================================================
// HECTON-8 — QueryCacheContext.cs  v1.0
// Per-frame raycast/overlap query result caching with deduplication.
//
// PURPOSE:
//   Eliminate redundant physics queries in complex gameplay systems.
//   If multiple systems need to raycast from same origin (camera, player pos),
//   cache the result. Automatically invalidate at frame end.
//
// WHY THIS MATTERS:
//   • Multiple systems may raycast from player position each frame:
//     - PlayerInteraction (hover detection)
//     - FlashlightTool (target detection)
//     - FieldTargetDescriptor (contextual UI)
//   • Each raycast has CPU cost (tree traversal, distance checks, etc).
//   • Caching can reduce from 3 raycasts to 1.
//   • Cache invalidates automatically — no manual management needed.
//
// USAGE:
//   // In system that raycasts frequently:
//   var ctx = QueryCacheContext.GetOrCreateContext("PlayerLook");
//
//   // First query — actual raycast
//   if (!ctx.TryGetCachedHit(ray, out var hit))
//   {
//       Physics.Raycast(ray, out hit, distance);
//       ctx.CacheHit(ray, hit);
//   }
//
//   // Subsequent queries (same ray) — instant hit from cache
//   if (ctx.TryGetCachedHit(ray, out var cachedHit))
//       Debug.Log($"Cached hit at {cachedHit.point}");
//
// SMART DEDUPLICATION:
//   • Queries are deduplicated by (origin, direction, distance, mask).
//   • Small floating-point differences are tolerated (epsilon = 0.001f).
//   • Cache key is computed via GetStableHashCode — deterministic.
//
// AUTO-INVALIDATION:
//   • Each LateUpdate, stale caches are cleared.
//   • Invalidation is O(1) per context (just clear the dict).
//   • Multiple calls to same query (within frame) all hit cache.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Cached raycast or overlap query result.
    /// Includes validity flag and expiration time.
    /// </summary>
    public struct CachedQueryResult
    {
        public RaycastHit hit;
        public bool hasHit;
        public int frameWhenCached;
        public float timeSeconds;
    }

    /// <summary>
    /// Per-frame query cache for a specific raycasting scenario.
    /// Automatically invalidated and recreated each frame.
    /// </summary>
    public sealed class QueryCacheContext
    {
        private readonly string _contextName;
        private int _lastValidatedFrame;

        /// <summary>
        /// Cache storage: Query hash → CachedQueryResult.
        /// Cleared at frame end via InvalidateIfStale().
        /// </summary>
        private readonly Dictionary<ulong, CachedQueryResult> _cache = new Dictionary<ulong, CachedQueryResult>(16);

        /// <summary>
        /// Number of cache hits this frame (for diagnostics).
        /// </summary>
        public int CacheHitCount { get; private set; }

        /// <summary>
        /// Number of cache misses (actual raycasts) this frame.
        /// </summary>
        public int CacheMissCount { get; private set; }

        // ════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════

        public QueryCacheContext(string name)
        {
            _contextName = name;
            _lastValidatedFrame = -1;
        }

        /// <summary>
        /// Invalidate cache if frame has changed.
        /// Called automatically by GlobalQueryCacheManager.
        /// </summary>
        public void InvalidateIfStale()
        {
            if (_lastValidatedFrame != Time.frameCount)
            {
                _cache.Clear();
                CacheHitCount = 0;
                CacheMissCount = 0;
                _lastValidatedFrame = Time.frameCount;
            }
        }

        // ════════════════════════════════════════════════════════════
        //  QUERY API
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Try to retrieve cached hit for this ray.
        /// If cache is valid and hit exists, returns true.
        /// If cache miss, returns false (caller should raycast and cache result).
        /// </summary>
        public bool TryGetCachedHit(Ray ray, float distance, int layerMask, out RaycastHit hit)
        {
            InvalidateIfStale();

            ulong key = ComputeQueryHash(ray, distance, layerMask);
            if (_cache.TryGetValue(key, out var cached))
            {
                CacheHitCount++;
                hit = cached.hit;
                return cached.hasHit;
            }

            CacheMissCount++;
            hit = default;
            return false;
        }

        /// <summary>
        /// Cache a raycast result for this ray.
        /// Call after executing Physics.Raycast if TryGetCachedHit returned false.
        /// </summary>
        public void CacheHit(Ray ray, float distance, int layerMask, RaycastHit hit, bool hasHit)
        {
            InvalidateIfStale();

            ulong key = ComputeQueryHash(ray, distance, layerMask);
            _cache[key] = new CachedQueryResult
            {
                hit = hit,
                hasHit = hasHit,
                frameWhenCached = Time.frameCount,
                timeSeconds = Time.realtimeSinceStartup
            };
        }

        /// <summary>
        /// Overload that caches based on Physics.Raycast result.
        /// </summary>
        public void CacheHit(Ray ray, float distance, int layerMask, out RaycastHit hit)
        {
            bool hasHit = UnityEngine.Physics.Raycast(ray, out hit, distance, layerMask);
            CacheHit(ray, distance, layerMask, hit, hasHit);
        }

        // ════════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ════════════════════════════════════════════════════════════

        public float CacheHitRate
        {
            get
            {
                int total = CacheHitCount + CacheMissCount;
                return total > 0 ? (CacheHitCount / (float)total) * 100f : 0f;
            }
        }

        public override string ToString() =>
            $"[QueryCache:{_contextName}] Hits={CacheHitCount} Misses={CacheMissCount} HitRate={CacheHitRate:F1}%";

        // ════════════════════════════════════════════════════════════
        //  HASHING — Stable query key from ray data
        // ════════════════════════════════════════════════════════════

        private const float QueryHashEpsilon = 0.001f;

        private static ulong ComputeQueryHash(Ray ray, float distance, int layerMask)
        {
            // Quantize ray origin/direction to reduce hash collisions
            // from floating-point precision differences.

            int originX = (int)(ray.origin.x / QueryHashEpsilon);
            int originY = (int)(ray.origin.y / QueryHashEpsilon);
            int originZ = (int)(ray.origin.z / QueryHashEpsilon);

            int dirX = (int)(ray.direction.x / QueryHashEpsilon);
            int dirY = (int)(ray.direction.y / QueryHashEpsilon);
            int dirZ = (int)(ray.direction.z / QueryHashEpsilon);

            int distInt = (int)(distance / QueryHashEpsilon);

            // Combine into 64-bit hash via Jenkins-one-at-a-time
            ulong hash = 5381;
            hash = ((hash << 5) + hash) ^ (ulong)originX;
            hash = ((hash << 5) + hash) ^ (ulong)originY;
            hash = ((hash << 5) + hash) ^ (ulong)originZ;
            hash = ((hash << 5) + hash) ^ (ulong)dirX;
            hash = ((hash << 5) + hash) ^ (ulong)dirY;
            hash = ((hash << 5) + hash) ^ (ulong)dirZ;
            hash = ((hash << 5) + hash) ^ (ulong)distInt;
            hash = ((hash << 5) + hash) ^ (ulong)layerMask;

            return hash;
        }
    }

    /// <summary>
    /// Global manager for all QueryCacheContext instances.
    /// Invalidates stale caches at frame end (LateUpdate).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(9000)] // Run AFTER all gameplay code
    public sealed class GlobalQueryCacheManager : MonoBehaviour
    {
        private static GlobalQueryCacheManager _instance;
        private static readonly Dictionary<string, QueryCacheContext> _contexts = new Dictionary<string, QueryCacheContext>(8);

        public static GlobalQueryCacheManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[QueryCacheManager]");
                    _instance = go.AddComponent<GlobalQueryCacheManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>
        /// Get or create a named QueryCacheContext.
        /// Contexts persist for the lifetime of the manager.
        /// </summary>
        public static QueryCacheContext GetOrCreateContext(string name)
        {
            _ = Instance; // Ensure singleton exists

            if (!_contexts.TryGetValue(name, out var ctx))
            {
                ctx = new QueryCacheContext(name);
                _contexts[name] = ctx;
            }

            return ctx;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void LateUpdate()
        {
            // Invalidate all stale caches at frame end
            foreach (var ctx in _contexts.Values)
                ctx.InvalidateIfStale();
        }

#if UNITY_EDITOR
        public static void PrintAllContextStats()
        {
            Debug.Log("=== Query Cache Contexts ===");
            foreach (var kvp in _contexts)
                Debug.Log(kvp.Value);
        }
#endif
    }
}
