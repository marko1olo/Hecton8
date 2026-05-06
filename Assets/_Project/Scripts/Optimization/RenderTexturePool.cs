using System.Collections.Generic;
using Hecton8.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Optimization
{
    /// <summary>
    /// RenderTexture pooling system for temporary RT reuse.
    /// O(1) lookup via Dictionary keyed by hash(width, height, format).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7998)]
    public sealed class RenderTexturePool : MonoBehaviour
    {
        private const string PooledRenderTextureName = "Pooled_RT";

        // ── REGISTRY CACHE ─────────────────────────────────────────────────────────
        
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        // COLD ALLOC: Dictionary<ulong, Queue<RenderTexture>>[16] — R8 pool — owner: RenderTexturePool
        private readonly Dictionary<ulong, Queue<RenderTexture>> _poolR8 = new Dictionary<ulong, Queue<RenderTexture>>(16);
        
        // COLD ALLOC: Dictionary<ulong, Queue<RenderTexture>>[16] — RG16 pool — owner: RenderTexturePool
        private readonly Dictionary<ulong, Queue<RenderTexture>> _poolRG16 = new Dictionary<ulong, Queue<RenderTexture>>(16);
        
        // COLD ALLOC: Dictionary<ulong, Queue<RenderTexture>>[16] — ARGB64 pool — owner: RenderTexturePool
        private readonly Dictionary<ulong, Queue<RenderTexture>> _poolRGBA16 = new Dictionary<ulong, Queue<RenderTexture>>(16);
        
        // COLD ALLOC: Dictionary<ulong, Queue<RenderTexture>>[16] — RGBA32 pool — owner: RenderTexturePool
        private readonly Dictionary<ulong, Queue<RenderTexture>> _poolRGBA32 = new Dictionary<ulong, Queue<RenderTexture>>(16);
        
        private int _totalRentCalls;
        private int _totalReuseCount;
        private bool _registeredService;
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextStatsLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Returns pool hit rate (reuse count / total Rent calls).
        /// </summary>
        public float PoolHitRate => _totalRentCalls > 0 ? _totalReuseCount / (float)_totalRentCalls : 0f;
        
        /// <summary>
        /// Returns total number of pooled RenderTextures across all formats.
        /// </summary>
        public int TotalPooledCount
        {
            get
            {
                return CountPool(_poolR8) +
                       CountPool(_poolRG16) +
                       CountPool(_poolRGBA16) +
                       CountPool(_poolRGBA32);
            }
        }
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void OnEnable()
        {
            if (TryRegisterService())
                SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }
        
        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterService();
        }
        
        private void OnDestroy()
        {
            TryUnregisterService();
            ClearAllPools();
        }
        
        // ── PUBLIC API ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Rents a RenderTexture from the pool or allocates a new one.
        /// </summary>
        /// <param name="width">Width in pixels.</param>
        /// <param name="height">Height in pixels.</param>
        /// <param name="format">RenderTextureFormat (R8, RG16, ARGB64, RGBA32).</param>
        /// <param name="owner">Owner component for lifecycle tracking.</param>
        /// <returns>RenderTexture instance (pooled or new).</returns>
        public RenderTexture Rent(int width, int height, RenderTextureFormat format, Component owner)
        {
            ulong key = CalculateRTKey(width, height, format);
            Dictionary<ulong, Queue<RenderTexture>> pool = GetPoolForFormat(format);
            
            _totalRentCalls++;
            
            if (pool.TryGetValue(key, out Queue<RenderTexture> queue))
            {
                while (queue.Count > 0)
                {
                    RenderTexture rt = queue.Dequeue();
                    if (rt == null)
                        continue;

                    if (rt.width == width && rt.height == height && rt.format == format)
                    {
                        _totalReuseCount++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LogPoolStats();
#endif
                        return rt;
                    }

                    RenderTextureLifecycleTracker staleLifecycle = GlobalRegistry.RenderTextureLifecycle;
                    if (staleLifecycle != null)
                        staleLifecycle.RegisterDisposal(rt);

                    rt.Release();
                }
            }
            
            // Pool miss - allocate new RT
            RenderTexture newRT = new RenderTexture(width, height, 0, format);
            newRT.name = PooledRenderTextureName;
            
            RenderTextureLifecycleTracker lifecycle = GlobalRegistry.RenderTextureLifecycle;
            if (lifecycle != null)
            {
                lifecycle.RegisterAllocation(newRT, owner);
            }
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogPoolStats();
#endif
            
            return newRT;
        }
        
        /// <summary>
        /// Returns a RenderTexture to the pool for reuse.
        /// </summary>
        /// <param name="rt">RenderTexture to return.</param>
        public void Return(RenderTexture rt)
        {
            if (rt == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[RTPool] Return called with null RenderTexture");
#endif
                return;
            }
            
            ulong key = CalculateRTKey(rt.width, rt.height, rt.format);
            Dictionary<ulong, Queue<RenderTexture>> pool = GetPoolForFormat(rt.format);
            
            if (!pool.TryGetValue(key, out Queue<RenderTexture> queue))
            {
                queue = new Queue<RenderTexture>(16);
                pool[key] = queue;
            }
            
            if (queue.Count >= 16)
            {
                // Pool full - release immediately
                RenderTextureLifecycleTracker lifecycle = GlobalRegistry.RenderTextureLifecycle;
                if (lifecycle != null)
                    lifecycle.RegisterDisposal(rt);

                rt.Release();
                return;
            }
            
            // Add to pool
            queue.Enqueue(rt);
        }
        
        /// <summary>
        /// Clears all pools and releases RenderTextures.
        /// Called automatically on SceneManager.sceneUnloaded.
        /// </summary>
        public void ClearAllPools()
        {
            ClearPool(_poolR8);
            ClearPool(_poolRG16);
            ClearPool(_poolRGBA16);
            ClearPool(_poolRGBA32);
        }

        /// <summary>
        /// PDA close path: release pooled visor/UI RTs immediately instead of retaining them for reuse.
        /// </summary>
        public void ReclaimPdaRenderTextures()
        {
            ClearAllPools();
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private static ulong CalculateRTKey(int width, int height, RenderTextureFormat format)
        {
            uint safeWidth = width > 0 ? (uint)Mathf.Min(width, 0xFFFFFF) : 0u;
            uint safeHeight = height > 0 ? (uint)Mathf.Min(height, 0xFFFFFF) : 0u;
            uint safeFormat = (uint)((int)format & 0xFFFF);
            return ((ulong)safeWidth << 40) | ((ulong)safeHeight << 16) | safeFormat;
        }
        
        private Dictionary<ulong, Queue<RenderTexture>> GetPoolForFormat(RenderTextureFormat format)
        {
            return format switch
            {
                RenderTextureFormat.R8 => _poolR8,
                RenderTextureFormat.RG16 => _poolRG16,
                RenderTextureFormat.ARGB64 => _poolRGBA16,
                RenderTextureFormat.ARGB32 => _poolRGBA32,
                RenderTextureFormat.DefaultHDR => _poolRGBA16,
                _ => _poolRGBA32
            };
        }

        private bool TryRegisterService()
        {
            if (_registeredService)
                return true;
            if (!Application.isPlaying)
                return false;

            RenderTexturePool registered = GlobalRegistry.RenderTexturePool;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterRenderTexturePoolRuntime(this);
            _registeredService = ReferenceEquals(GlobalRegistry.RenderTexturePool, this);
            return _registeredService;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterRenderTexturePoolRuntime(this);
            _registeredService = false;
        }
        
        private void ClearPool(Dictionary<ulong, Queue<RenderTexture>> pool)
        {
            Dictionary<ulong, Queue<RenderTexture>>.Enumerator enumerator = pool.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Queue<RenderTexture> queue = enumerator.Current.Value;
                while (queue.Count > 0)
                {
                    RenderTexture rt = queue.Dequeue();
                    if (rt != null)
                    {
                        RenderTextureLifecycleTracker lifecycle = GlobalRegistry.RenderTextureLifecycle;
                        if (lifecycle != null)
                            lifecycle.RegisterDisposal(rt);

                        rt.Release();
                    }
                }
            }
            pool.Clear();
        }

        private static int CountPool(Dictionary<ulong, Queue<RenderTexture>> pool)
        {
            int total = 0;
            Dictionary<ulong, Queue<RenderTexture>>.Enumerator enumerator = pool.GetEnumerator();
            while (enumerator.MoveNext())
                total += enumerator.Current.Value.Count;

            return total;
        }
        
        private void HandleSceneUnloaded(Scene scene)
        {
            ClearAllPools();
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogPoolStats()
        {
            if (Time.time >= _nextStatsLogTime)
                _nextStatsLogTime = Time.time + 60f; // Log every 60s
        }
#endif
    }
}
