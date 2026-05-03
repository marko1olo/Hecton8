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
    public sealed class CameraRTManager : MonoBehaviour, ISlowTickable
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static CameraRTManager _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static CameraRTManager Instance => _instance;
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long CameraBudgetBytes = 256L * 1024L * 1024L; // 256 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        
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
            TryRegister();
        }
        
        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();
        }
        
        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();

            if (_instance == this)
                _instance = null;
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
            if (Hecton8.Core.GlobalRegistry.RenderTextureLifecycle == null)
            {
                CameraRTMemoryBytes = 0L;
                return;
            }
            
            // Query all Camera-owned RTs (zero-GC)
            _cameraRTs.Clear();
            Hecton8.Core.GlobalRegistry.RenderTextureLifecycle.GetAllocationsByCategory("Camera", _cameraRTs);
            
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registeredSlowTick = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying || _instance != this)
                return;

            GlobalRegistry.RegisterCameraRTRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.CameraRT, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterCameraRTRuntime(this);
            _serviceRegistered = false;
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
