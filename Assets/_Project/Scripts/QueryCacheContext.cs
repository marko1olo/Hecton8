// ============================================================================
// HECTON-8 — QueryCacheContext.cs  v2.0
// Per-frame physics query result caching and deduplication.
//
// PURPOSE:
//   Avoid redundant Physics.Raycast or Batch queries from the same source 
//   (Camera looking at interactions, tools, and UI) within a single frame.
//
// ZERO GC GUARANTEE:
//   • Quantized hashing for deterministic cache hits.
//   • Persistent Dictionary cache (Clear() instead of new).
//   • Struct-based result storage.
//
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Physics
{
    public struct CachedQueryResult
    {
        public QueryResult result;
        public int frameIndex;
    }

    public sealed class QueryCacheContext
    {
        private readonly string _name;
        private readonly Dictionary<ulong, CachedQueryResult> _cache = new Dictionary<ulong, CachedQueryResult>(32);
        
        public int Hits { get; private set; }
        public int Misses { get; private set; }
        public static int CacheHits;

        private int _lastUpdateFrame = -1;

        public QueryCacheContext(string name) => _name = name;

        public void InvalidateIfStale()
        {
            if (_lastUpdateFrame != Time.frameCount)
            {
                _cache.Clear();
                Hits = 0;
                Misses = 0;
                _lastUpdateFrame = Time.frameCount;
            }
        }

        public bool TryGet(Ray ray, float distance, int mask, out QueryResult result)
        {
            InvalidateIfStale();

            ulong key = ComputeHash(ray, distance, mask);
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

        public void Set(Ray ray, float distance, int mask, QueryResult result)
        {
            ulong key = ComputeHash(ray, distance, mask);
            _cache[key] = new CachedQueryResult { result = result, frameIndex = Time.frameCount };
        }

        // ════════════════════════════════════════════════════════════════════════════════
        //  STABLE HASHING
        // ════════════════════════════════════════════════════════════════════════════════

        private static ulong ComputeHash(Ray ray, float distance, int mask)
        {
            const float epsilon = 0.001f;
            ulong h = 14695981039346656037UL; // FNV offset basis
            h = (h ^ (ulong)(ray.origin.x / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)(ray.origin.y / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)(ray.origin.z / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)(ray.direction.x / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)(ray.direction.y / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)(ray.direction.z / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)(distance / epsilon)) * 1099511628211UL;
            h = (h ^ (ulong)mask) * 1099511628211UL;
            return h;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)] // Run AFTER all gameplay systems
    public sealed class GlobalQueryCacheManager : MonoBehaviour
    {
        private static GlobalQueryCacheManager _instance;
        private static readonly Dictionary<string, QueryCacheContext> _contexts = new Dictionary<string, QueryCacheContext>(8);

        public static QueryCacheContext GetContext(string name)
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("[GlobalQueryCache]");
                _instance = go.AddComponent<GlobalQueryCacheManager>();
                DontDestroyOnLoad(go);
            }

            if (!_contexts.TryGetValue(name, out var ctx))
            {
                ctx = new QueryCacheContext(name);
                _contexts[name] = ctx;
            }

            return ctx;
        }

        private void LateUpdate()
        {
            // Per-frame invalidation via clearing internal state
            foreach (var ctx in _contexts.Values) ctx.InvalidateIfStale();
            
            // Clean up old queries in RaycastBatchHelper if shared
            if (RaycastBatchHelper.Instance != null) RaycastBatchHelper.Instance.ClearQueries();
        }
    }
}
