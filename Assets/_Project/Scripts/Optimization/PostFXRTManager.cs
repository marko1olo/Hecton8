using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors PostFX subsystem RenderTexture memory consumption.
    /// Budget: 128 MB. Executes in ISlowTickable (~0.5s interval).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7995)]
    public sealed class PostFXRTManager : MonoBehaviour, ISlowTickable
    {
        // ── REGISTRY SLOT ──────────────────────────────────────────────────────────
        
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long PostFXBudgetBytes = 128L * 1024L * 1024L; // 128 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private bool _serviceRegistered;
        
        // COLD ALLOC: StringBuilder[1024] — zero-GC logging — owner: PostFXRTManager
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        
        // COLD ALLOC: List<RenderTextureAllocationRecord>[32] — RT query — owner: PostFXRTManager
        private readonly List<RenderTextureAllocationRecord> _postFXRTs = new List<RenderTextureAllocationRecord>(32);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Current PostFX RT memory consumption in bytes.
        /// </summary>
        public long PostFXRTMemoryBytes { get; private set; }
        
        /// <summary>
        /// Returns whether PostFX RT memory exceeds 128 MB budget.
        /// </summary>
        public bool IsOverBudget => PostFXRTMemoryBytes > PostFXBudgetBytes;
        
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
        /// ISlowTickable implementation. Monitors PostFX RT memory every ~0.5s.
        /// Zero GC: pre-allocated buffers, no LINQ, no string concat.
        /// </summary>
        public void SlowTick()
        {
            MeasurePostFXRTMemory();
            CheckBudget();
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void MeasurePostFXRTMemory()
        {
            if (Hecton8.Core.GlobalRegistry.RenderTextureLifecycle == null)
            {
                PostFXRTMemoryBytes = 0L;
                return;
            }
            
            // Query all PostFX-owned RTs (zero-GC)
            _postFXRTs.Clear();
            Hecton8.Core.GlobalRegistry.RenderTextureLifecycle.GetAllocationsByCategory(RenderTextureOwnerCategory.PostFX, _postFXRTs);
            
            // Calculate total PostFX RT memory (zero-GC loop)
            long totalBytes = 0L;
            for (int i = 0; i < _postFXRTs.Count; i++)
            {
                if (!_postFXRTs[i].IsDisposed)
                    totalBytes += _postFXRTs[i].MemoryBytes;
            }
            
            PostFXRTMemoryBytes = totalBytes;
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
            if (_registeredSlowTick || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;
            if (!ReferenceEquals(GlobalRegistry.PostFXRT, this))
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

            PostFXRTManager registered = GlobalRegistry.PostFXRT;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return false;
            }

            GlobalRegistry.RegisterPostFXRTRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PostFXRT, this);
            return _serviceRegistered;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPostFXRTRuntime(this);
            _serviceRegistered = false;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogBudgetViolation()
        {
            _reportBuilder.Clear();
            _reportBuilder.Append("[PostFXRTManager] BUDGET EXCEEDED: ");
            _reportBuilder.Append((PostFXRTMemoryBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB / ");
            _reportBuilder.Append((PostFXBudgetBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB");
            
            Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
