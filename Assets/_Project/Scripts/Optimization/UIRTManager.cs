using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors UI subsystem RenderTexture memory consumption.
    /// Budget: 64 MB. Executes in ISlowTickable (~0.5s interval).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7994)]
    public sealed class UIRTManager : MonoBehaviour, ISlowTickable
    {
        // ── REGISTRY SLOT ──────────────────────────────────────────────────────────
        
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long UIBudgetBytes = 64L * 1024L * 1024L; // 64 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        
        // COLD ALLOC: StringBuilder[1024] — zero-GC logging — owner: UIRTManager
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        
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
            if (Hecton8.Core.GlobalRegistry.RenderTextureLifecycle == null)
            {
                UIRTMemoryBytes = 0L;
                return;
            }
            
            // Query all UI-owned RTs (zero-GC)
            _uiRTs.Clear();
            Hecton8.Core.GlobalRegistry.RenderTextureLifecycle.GetAllocationsByCategory(RenderTextureOwnerCategory.UI, _uiRTs);
            
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = GlobalRegistry.SlowTickables.Contains(this);
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
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogBudgetViolation()
        {
            _reportBuilder.Clear();
            _reportBuilder.Append("[UIRTManager] BUDGET EXCEEDED: ");
            _reportBuilder.Append((UIRTMemoryBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB / ");
            _reportBuilder.Append((UIBudgetBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB");
            
            Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
