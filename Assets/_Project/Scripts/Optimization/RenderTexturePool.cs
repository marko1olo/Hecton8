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
        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static RenderTexturePool _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static RenderTexturePool Instance => _instance;
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        // COLD ALLOC: Dictionary<int, Queue<RenderTexture>>[16] — R8 pool — owner: RTPool
        private readonly Dictionary<int, Queue<RenderTexture>> _poolR8 = new Dictionary<int, Queue<RenderTexture>>(16);
        
        // COLD ALLOC: Dictionary<int, Queue<RenderTexture>>[16] — RG16 pool — owner: RTPool
        private readonly Dictionary<int, Queue<RenderTexture>> _poolRG16 = new Dictionary<int, Queue<RenderTexture>>(16);
        
        // COLD ALLOC: Dictionary<int, Queue<RenderTexture>>[16] — ARGB64 pool — owner: RTPool
        private readonly Dictionary<int, Queue<RenderTexture>> _poolRGBA16 = new Dictionary<int, Queue<RenderTexture>>(16);
        
        // COLD ALLOC: Dictionary<int, Queue<RenderTexture>>[16] — RGBA32 pool — owner: RTPool
        private readonly Dictionary<int, Queue<RenderTexture>> _poolRGBA32 = new Dictionary<int, Queue<RenderTexture>>(16);
        
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
                int total = 0;
                foreach (var kvp in _poolR8)
                    total += kvp.Value.Count;
                foreach (var kvp in _poolRG16)
                    total += kvp.Value.Count;
                foreach (var kvp in _poolRGBA16)
                    total += kvp.Value.Count;
                foreach (var kvp in _poolRGBA32)
                    total += kvp.Value.Count;
                return total;
            }
        }
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
        }
        
        private void OnEnable()
        {
            TryRegisterService();
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
            
            if (_instance == this)
                _instance = null;
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
            int hash = CalculateRTHash(width, height, format);
            Dictionary<int, Queue<RenderTexture>> pool = GetPoolForFormat(format);
            
            _totalRentCalls++;
            
            if (pool.TryGetValue(hash, out Queue<RenderTexture> queue) && queue.Count > 0)
            {
                // Pool hit
                RenderTexture rt = queue.Dequeue();
                _totalReuseCount++;
                
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogPoolStats();
#endif
                
                return rt;
            }
            
            // Pool miss - allocate new RT
            RenderTexture newRT = new RenderTexture(width, height, 0, format);
            newRT.name = $"Pooled_RT_{width}x{height}_{format}";
            
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
            
            int hash = CalculateRTHash(rt.width, rt.height, rt.format);
            Dictionary<int, Queue<RenderTexture>> pool = GetPoolForFormat(rt.format);
            
            if (!pool.TryGetValue(hash, out Queue<RenderTexture> queue))
            {
                queue = new Queue<RenderTexture>(16);
                pool[hash] = queue;
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
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private static int CalculateRTHash(int width, int height, RenderTextureFormat format)
        {
            // Collision-free for typical resolutions (width < 65536, height < 65536, format < 256)
            return width ^ (height << 16) ^ ((int)format << 24);
        }
        
        private Dictionary<int, Queue<RenderTexture>> GetPoolForFormat(RenderTextureFormat format)
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

        private void TryRegisterService()
        {
            if (_registeredService || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterRenderTexturePoolRuntime(this);
            _registeredService = true;
        }

        private void TryUnregisterService()
        {
            if (!_registeredService)
                return;

            GlobalRegistry.UnregisterRenderTexturePoolRuntime(this);
            _registeredService = false;
        }
        
        private void ClearPool(Dictionary<int, Queue<RenderTexture>> pool)
        {
            foreach (var kvp in pool)
            {
                while (kvp.Value.Count > 0)
                {
                    RenderTexture rt = kvp.Value.Dequeue();
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
        
        private void HandleSceneUnloaded(Scene scene)
        {
            ClearAllPools();
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogPoolStats()
        {
            if (Time.time >= _nextStatsLogTime)
            {
                _nextStatsLogTime = Time.time + 60f; // Log every 60s
                Debug.Log($"[RTPool] Hit Rate: {(PoolHitRate * 100f):0.0}% | Total Pooled: {TotalPooledCount} | Rent Calls: {_totalRentCalls} | Reuses: {_totalReuseCount}");
            }
        }
#endif
    }
}
