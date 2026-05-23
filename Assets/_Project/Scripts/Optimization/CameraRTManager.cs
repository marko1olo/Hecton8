using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors Camera subsystem RenderTexture memory consumption.
    /// Budget: 256 MB. Executes in ISlowTickable (~0.5s interval).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7996)]
    public sealed class CameraRTManager : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        // ── REGISTRY SLOT ──────────────────────────────────────────────────────────
        
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long CameraBudgetBytes = 256L * 1024L * 1024L; // 256 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        private bool _registeredHotSwapListener;
        private RenderTextureLifecycleTracker _cachedRenderTextureLifecycle;
        
        // COLD ALLOC: StringBuilder[1024] — zero-GC logging — owner: CameraRTManager
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        
        // COLD ALLOC: List<RenderTextureAllocationRecord>[32] — RT query — owner: CameraRTManager
        private readonly List<RenderTextureAllocationRecord> _cameraRTs = new List<RenderTextureAllocationRecord>(32);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Current Camera RT memory consumption in bytes.
        /// </summary>
        public long CameraRTMemoryBytes { get; private set; }
        
        /// <summary>
        /// Returns whether Camera RT memory exceeds 256 MB budget.
        /// </summary>
        public bool IsOverBudget => CameraRTMemoryBytes > CameraBudgetBytes;
        
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
                _cachedRenderTextureLifecycle = currentService as RenderTextureLifecycleTracker;
        }
        
        // ── ISLOWTICABLE ───────────────────────────────────────────────────────────
        
        /// <summary>
        /// ISlowTickable implementation. Monitors Camera RT memory every ~0.5s.
        /// Zero GC: pre-allocated buffers, no LINQ, no string concat.
        /// </summary>
        public void SlowTick()
        {
            MeasureCameraRTMemory();
            CheckBudget();
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void MeasureCameraRTMemory()
        {
            RenderTextureLifecycleTracker lifecycle = _cachedRenderTextureLifecycle;
            if (lifecycle == null)
            {
                CameraRTMemoryBytes = 0L;
                return;
            }
            
            // Query all Camera-owned RTs (zero-GC)
            _cameraRTs.Clear();
            lifecycle.GetAllocationsByCategory(RenderTextureOwnerCategory.Camera, _cameraRTs);
            
            // Calculate total Camera RT memory (zero-GC loop)
            long totalBytes = 0L;
            for (int i = 0; i < _cameraRTs.Count; i++)
            {
                if (!_cameraRTs[i].IsDisposed)
                    totalBytes += _cameraRTs[i].MemoryBytes;
            }
            
            CameraRTMemoryBytes = totalBytes;
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
            if (_registeredSlowTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.CameraRT, this))
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered)
                return true;
            if (!Application.isPlaying)
                return false;

            CameraRTManager registered = GlobalRegistry.CameraRT;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterCameraRTRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.CameraRT, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterCameraRTRuntime(this);
            _serviceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedRenderTextureLifecycle = GlobalRegistry.RenderTextureLifecycle;
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
            _reportBuilder.Clear();
            _reportBuilder.Append("[CameraRTManager] BUDGET EXCEEDED: ");
            _reportBuilder.Append((CameraRTMemoryBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB / ");
            _reportBuilder.Append((CameraBudgetBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB");
            
            Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
