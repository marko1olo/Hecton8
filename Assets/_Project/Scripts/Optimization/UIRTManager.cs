using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors UI subsystem RenderTexture memory consumption.
    /// Budget: 64 MB. Executes in ISlowTickable (~0.5s interval).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7994)]
    public sealed class UIRTManager : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        // ── REGISTRY SLOT ──────────────────────────────────────────────────────────
        
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long UIBudgetBytes = 64L * 1024L * 1024L; // 64 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private IRenderTextureLifecycleService _cachedRenderTextureLifecycle;
        
        // COLD ALLOC: List<RenderTextureAllocationRecord>[32] — RT query — owner: UIRTManager
        private readonly List<RenderTextureAllocationRecord> _uiRTs = new List<RenderTextureAllocationRecord>(32);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Current UI RT memory consumption in bytes.
        /// </summary>
        public long UIRTMemoryBytes { get; private set; }
        
        /// <summary>
        /// Returns whether UI RT memory exceeds 64 MB budget.
        /// </summary>
        public bool IsOverBudget => UIRTMemoryBytes > UIBudgetBytes;
        
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
        /// ISlowTickable implementation. Monitors UI RT memory every ~0.5s.
        /// Zero GC: pre-allocated buffers, no LINQ, no string concat.
        /// </summary>
        public void SlowTick()
        {
            MeasureUIRTMemory();
            CheckBudget();
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void MeasureUIRTMemory()
        {
            IRenderTextureLifecycleService lifecycle = _cachedRenderTextureLifecycle;
            if (lifecycle == null)
            {
                UIRTMemoryBytes = 0L;
                return;
            }
            
            // Query all UI-owned RTs (zero-GC)
            _uiRTs.Clear();
            lifecycle.GetAllocationsByCategory(RenderTextureOwnerCategory.UI, _uiRTs);
            
            // Calculate total UI RT memory (zero-GC loop)
            long totalBytes = 0L;
            for (int i = 0; i < _uiRTs.Count; i++)
            {
                if (!_uiRTs[i].IsDisposed)
                    totalBytes += _uiRTs[i].MemoryBytes;
            }
            
            UIRTMemoryBytes = totalBytes;
        }
        
        private void CheckBudget()
        {
            if (!IsOverBudget)
                return;
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.time >= _nextLogTime)
            {
                _nextLogTime = Time.time + LogThrottleInterval;
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
            if (!ReferenceEquals(GlobalRegistry.UIRT, this))
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

            UIRTManager registered = GlobalRegistry.UIRT;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterUIRTRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.UIRT, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterUIRTRuntime(this);
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
            Debug.LogWarning("[UIRTManager] BUDGET EXCEEDED", this);
        }
#endif
    }
}
