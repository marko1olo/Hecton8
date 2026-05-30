using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Optimization
{
    /// <summary>
    /// RenderTexture pooling system for temporary RT reuse.
    /// O(1) lookup via Dictionary keyed by hash(width, height, format, depth).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7998)]
    public sealed class RenderTexturePool : MonoBehaviour, IRenderTexturePoolService, ISlowTickable, IGlobalRegistryHotSwapListener
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

        // COLD ALLOC: Queue<RenderTexture>[16] - prewarmed screen-size R8 RT bucket - owner: RenderTexturePool
        private readonly Queue<RenderTexture> _screenR8Queue = new Queue<RenderTexture>(16);
        // COLD ALLOC: Queue<RenderTexture>[16] - prewarmed screen-size RG16 RT bucket - owner: RenderTexturePool
        private readonly Queue<RenderTexture> _screenRG16Queue = new Queue<RenderTexture>(16);
        // COLD ALLOC: Queue<RenderTexture>[16] - prewarmed screen-size ARGB64 RT bucket - owner: RenderTexturePool
        private readonly Queue<RenderTexture> _screenARGB64Queue = new Queue<RenderTexture>(16);
        // COLD ALLOC: Queue<RenderTexture>[16] - prewarmed screen-size DefaultHDR RT bucket - owner: RenderTexturePool
        private readonly Queue<RenderTexture> _screenDefaultHdrQueue = new Queue<RenderTexture>(16);
        // COLD ALLOC: Queue<RenderTexture>[16] - prewarmed screen-size ARGB32 RT bucket - owner: RenderTexturePool
        private readonly Queue<RenderTexture> _screenARGB32Queue = new Queue<RenderTexture>(16);
        // COLD ALLOC: Queue<RenderTexture>[16] - prewarmed screen-size Default RT bucket - owner: RenderTexturePool
        private readonly Queue<RenderTexture> _screenDefaultQueue = new Queue<RenderTexture>(16);
        
        private int _totalRentCalls;
        private int _totalReuseCount;
        private int _lastScreenWidth;
        private int _lastScreenHeight;
        private bool _registeredService;
        private bool _registeredSlowTick;
        private bool _hotSwapRegistered;
        private IRenderTextureLifecycleService _lifecycleTracker;
        
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
            CaptureScreenSetup();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            PrewarmCurrentScreenQueues();
            if (TryRegisterService())
            {
                TryRegisterSlowTickable();
                SceneManager.sceneUnloaded += HandleSceneUnloaded;
            }
        }
        
        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }
        
        private void OnDestroy()
        {
            TryUnregisterSlowTickable();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
            ClearAllPools(preserveScreenBuckets: false);
        }

        public void SlowTick()
        {
            DefragForCurrentScreenIfNeeded();
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
            return Rent(width, height, format, owner, 0);
        }

        /// <summary>
        /// Rents a RenderTexture from the pool or allocates a new one, preserving depth-buffer class.
        /// </summary>
        public RenderTexture Rent(int width, int height, RenderTextureFormat format, Component owner, int depthBits)
        {
            int safeWidth = Mathf.Max(1, width);
            int safeHeight = Mathf.Max(1, height);
            int safeDepthBits = Mathf.Clamp(depthBits, 0, 255);
            ulong key = CalculateRTKey(safeWidth, safeHeight, format, safeDepthBits);
            Dictionary<ulong, Queue<RenderTexture>> pool = GetPoolForFormat(format);
            
            _totalRentCalls++;
            
            if (pool.TryGetValue(key, out Queue<RenderTexture> queue))
            {
                while (queue.Count > 0)
                {
                    RenderTexture rt = queue.Dequeue();
                    if (rt == null)
                        continue;

                    if (rt.width == safeWidth && rt.height == safeHeight && rt.format == format && rt.depth == safeDepthBits)
                    {
                        _totalReuseCount++;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        LogPoolStats();
#endif
                        return rt;
                    }

                    IRenderTextureLifecycleService staleLifecycle = _lifecycleTracker;
                    if (staleLifecycle != null)
                        staleLifecycle.RegisterDisposal(rt);

                    rt.Release();
                    Destroy(rt);
                }
            }
            
            // Pool miss - allocate new RT
            RenderTexture newRT = new RenderTexture(safeWidth, safeHeight, safeDepthBits, format);
            newRT.name = PooledRenderTextureName;
            
            IRenderTextureLifecycleService lifecycle = _lifecycleTracker;
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
                Hecton8.Core.H8Debug.LogWarning("[RTPool] Return called with null RenderTexture");
#endif
                return;
            }

            ulong key = CalculateRTKey(rt.width, rt.height, rt.format, rt.depth);
            Dictionary<ulong, Queue<RenderTexture>> pool = GetPoolForFormat(rt.format);
            
            if (!pool.TryGetValue(key, out Queue<RenderTexture> queue))
            {
                IRenderTextureLifecycleService lifecycle = _lifecycleTracker;
                if (lifecycle != null)
                    lifecycle.RegisterDisposal(rt);

                rt.Release();
                Destroy(rt);
                return;
            }
            
            if (queue.Count >= 16)
            {
                // Pool full - release immediately
                IRenderTextureLifecycleService lifecycle = _lifecycleTracker;
                if (lifecycle != null)
                    lifecycle.RegisterDisposal(rt);

                rt.Release();
                Destroy(rt);
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
            ClearAllPools(preserveScreenBuckets: true);
        }

        private void ClearAllPools(bool preserveScreenBuckets)
        {
            ClearPool(_poolR8);
            ClearPool(_poolRG16);
            ClearPool(_poolRGBA16);
            ClearPool(_poolRGBA32);

            if (preserveScreenBuckets)
                PrewarmCurrentScreenQueues();
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
            return CalculateRTKey(width, height, format, 0);
        }

        private static ulong CalculateRTKey(int width, int height, RenderTextureFormat format, int depthBits)
        {
            uint safeWidth = width > 0 ? (uint)Mathf.Min(width, 0xFFFFF) : 0u;
            uint safeHeight = height > 0 ? (uint)Mathf.Min(height, 0xFFFFF) : 0u;
            uint safeFormat = (uint)((int)format & 0xFFFF);
            uint safeDepth = (uint)Mathf.Clamp(depthBits, 0, 0xFF);
            return ((ulong)safeWidth << 44) | ((ulong)safeHeight << 24) | ((ulong)safeFormat << 8) | safeDepth;
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

        private void CaptureScreenSetup()
        {
            _lastScreenWidth = Mathf.Max(1, Screen.width);
            _lastScreenHeight = Mathf.Max(1, Screen.height);
        }

        private void DefragForCurrentScreenIfNeeded()
        {
            int currentWidth = Mathf.Max(1, Screen.width);
            int currentHeight = Mathf.Max(1, Screen.height);
            if (currentWidth == _lastScreenWidth && currentHeight == _lastScreenHeight)
                return;

            _lastScreenWidth = currentWidth;
            _lastScreenHeight = currentHeight;
            ClearPool(_poolR8);
            ClearPool(_poolRG16);
            ClearPool(_poolRGBA16);
            ClearPool(_poolRGBA32);
            PrewarmCurrentScreenQueues();
        }

        private void PrewarmCurrentScreenQueues()
        {
            BindQueue(_poolR8, CalculateRTKey(_lastScreenWidth, _lastScreenHeight, RenderTextureFormat.R8), _screenR8Queue);
            BindQueue(_poolRG16, CalculateRTKey(_lastScreenWidth, _lastScreenHeight, RenderTextureFormat.RG16), _screenRG16Queue);
            BindQueue(_poolRGBA16, CalculateRTKey(_lastScreenWidth, _lastScreenHeight, RenderTextureFormat.ARGB64), _screenARGB64Queue);
            BindQueue(_poolRGBA16, CalculateRTKey(_lastScreenWidth, _lastScreenHeight, RenderTextureFormat.DefaultHDR), _screenDefaultHdrQueue);
            BindQueue(_poolRGBA32, CalculateRTKey(_lastScreenWidth, _lastScreenHeight, RenderTextureFormat.ARGB32), _screenARGB32Queue);
            BindQueue(_poolRGBA32, CalculateRTKey(_lastScreenWidth, _lastScreenHeight, RenderTextureFormat.Default), _screenDefaultQueue);
        }

        private static void BindQueue(Dictionary<ulong, Queue<RenderTexture>> pool, ulong key, Queue<RenderTexture> queue)
        {
            if (pool.ContainsKey(key))
                return;

            queue.Clear();
            pool.Add(key, queue);
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime)
                _lifecycleTracker = currentService as IRenderTextureLifecycleService;
        }

        private void CacheRegistryServicesCold()
        {
            _lifecycleTracker = GlobalRegistry.RenderTextureLifecycleService;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregisterSlowTickable()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
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
                        IRenderTextureLifecycleService lifecycle = _lifecycleTracker;
                        if (lifecycle != null)
                            lifecycle.RegisterDisposal(rt);

                        rt.Release();
                        Destroy(rt);
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
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now >= _nextStatsLogTime)
                _nextStatsLogTime = now + 60f; // Log every 60s
        }
#endif
    }
}
