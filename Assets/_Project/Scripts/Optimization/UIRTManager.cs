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
        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static UIRTManager _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static UIRTManager Instance => _instance;
        
        // ── CONSTANTS ──────────────────────────────────────────────────────────────
        
        private const long UIBudgetBytes = 64L * 1024L * 1024L; // 64 MB
        private const float LogThrottleInterval = 5f; // Log once per 5s
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        
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
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }
        
        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredSlowTick)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }
        
        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registeredSlowTick)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredSlowTick = false;
            }
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
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
            if (RenderTextureLifecycleTracker.Instance == null)
            {
                UIRTMemoryBytes = 0L;
                return;
            }
            
            // Query all UI-owned RTs (zero-GC)
            _uiRTs.Clear();
            RenderTextureLifecycleTracker.Instance.GetAllocationsByCategory("UI", _uiRTs);
            
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
