using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors Visor subsystem RenderTexture memory consumption.
    /// Budget: 64 MB. Executes in ISlowTickable (~0.5s interval).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7997)]
    public sealed class VisorRTManager : MonoBehaviour, ISlowTickable
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static VisorRTManager _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static VisorRTManager Instance => _instance;
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long VisorBudgetBytes = 64L * 1024L * 1024L; // 64 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        
        // COLD ALLOC: StringBuilder[1024] — zero-GC logging — owner: VisorRTManager
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        
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
            TryRegister();
        }
        
        private void OnDisable()
        {
            TryUnregister();
        }
        
        private void OnDestroy()
        {
            TryUnregister();

            if (_instance == this)
                _instance = null;
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
            if (RenderTextureLifecycleTracker.Instance == null)
            {
                VisorRTMemoryBytes = 0L;
                return;
            }
            
            // Query all Visor-owned RTs (zero-GC)
            _visorRTs.Clear();
            RenderTextureLifecycleTracker.Instance.GetAllocationsByCategory("Visor", _visorRTs);
            
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = true;
        }

        private void TryUnregister()
        {
            if (!_registeredSlowTick)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registeredSlowTick = false;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogBudgetViolation()
        {
            _reportBuilder.Clear();
            _reportBuilder.Append("[VisorRTManager] BUDGET EXCEEDED: ");
            _reportBuilder.Append((VisorRTMemoryBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB / ");
            _reportBuilder.Append((VisorBudgetBytes / (1024f * 1024f)).ToString("0.00")).Append(" MB");
            
            Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
