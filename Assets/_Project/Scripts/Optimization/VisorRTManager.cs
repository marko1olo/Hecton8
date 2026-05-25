using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors Visor subsystem RenderTexture memory consumption.
    /// Budget: 64 MB. Executes in ISlowTickable (~0.5s interval).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7997)]
    public sealed class VisorRTManager : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        // ── REGISTRY SLOT ──────────────────────────────────────────────────────────
        
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long VisorBudgetBytes = 64L * 1024L * 1024L; // 64 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private IRenderTextureLifecycleService _cachedRenderTextureLifecycle;
        
        // COLD ALLOC: List<RenderTextureAllocationRecord>[32] — RT query — owner: VisorRTManager
        private readonly List<RenderTextureAllocationRecord> _visorRTs = new List<RenderTextureAllocationRecord>(32);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Current Visor RT memory consumption in bytes.
        /// </summary>
        public long VisorRTMemoryBytes { get; private set; }
        
        /// <summary>
        /// Returns whether Visor RT memory exceeds 64 MB budget.
        /// </summary>
        public bool IsOverBudget => VisorRTMemoryBytes > VisorBudgetBytes;
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void OnEnable()
        {
            if (TryRegisterService())
            {
                CacheRegistryServicesCold();
                TryRegisterHotSwapListener();
                TryRegister();
            }
        }
        
        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }
        
        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            TryUnregisterService();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.RenderTextureLifecycleRuntime)
                _cachedRenderTextureLifecycle = currentService as IRenderTextureLifecycleService;
        }
        
        // ── ISLOWTICABLE ───────────────────────────────────────────────────────────
        
        /// <summary>
        /// ISlowTickable implementation. Monitors Visor RT memory every ~0.5s.
        /// Zero GC: pre-allocated buffers, no LINQ, no string concat.
        /// </summary>
        public void SlowTick()
        {
            MeasureVisorRTMemory();
            CheckBudget();
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void MeasureVisorRTMemory()
        {
            IRenderTextureLifecycleService lifecycle = _cachedRenderTextureLifecycle;
            if (lifecycle == null)
            {
                VisorRTMemoryBytes = 0L;
                return;
            }
            
            // Query all Visor-owned RTs (zero-GC)
            _visorRTs.Clear();
            lifecycle.GetAllocationsByCategory(RenderTextureOwnerCategory.Visor, _visorRTs);
            
            // Calculate total Visor RT memory (zero-GC loop)
            long totalBytes = 0L;
            for (int i = 0; i < _visorRTs.Count; i++)
            {
                if (!_visorRTs[i].IsDisposed)
                    totalBytes += _visorRTs[i].MemoryBytes;
            }
            
            VisorRTMemoryBytes = totalBytes;
        }
        
        private void CheckBudget()
        {
            if (!IsOverBudget)
                return;
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now >= _nextLogTime)
            {
                _nextLogTime = now + LogThrottleInterval;
                LogBudgetViolation();
            }
#endif
        }

        private void TryRegister()
        {
            if (_registeredSlowTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.VisorRT, this))
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered)
                return true;
            if (!Application.isPlaying)
                return false;

            VisorRTManager registered = GlobalRegistry.VisorRT;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterVisorRTRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.VisorRT, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterVisorRTRuntime(this);
            _serviceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedRenderTextureLifecycle = GlobalRegistry.RenderTextureLifecycleService;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogBudgetViolation()
        {
            Hecton8.Core.H8Debug.LogWarning("[VisorRTManager] BUDGET EXCEEDED", this);
        }
#endif
    }
}
