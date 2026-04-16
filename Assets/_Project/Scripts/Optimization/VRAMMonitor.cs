using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Hecton8.Core;

namespace Hecton8.Optimization
{
    /// <summary>
    /// Monitors VRAM consumption and enforces budget thresholds.
    /// Executes in ISlowTickable (~0.5s interval) to avoid per-frame overhead.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8000)]
    public sealed class VRAMMonitor : MonoBehaviour, ISlowTickable
    {
        /// <summary>
        /// High-level VRAM pressure state derived from budget utilization.
        /// </summary>
        public enum VRAMPressureState : byte
        {
            Stable = 0,
            Warning = 1,
            Critical = 2
        }

        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static VRAMMonitor _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static VRAMMonitor Instance => _instance;
        
        // ── INSPECTOR SETTINGS ─────────────────────────────────────────────────────
        
        [Header("── VRAM Budget Thresholds ──────────────────")]
        [SerializeField] private VRAMBudgetThresholds _budgetThresholds = VRAMBudgetThresholds.Default;
        [Tooltip("Budget utilization at which VRAM pressure moves from stable to warning state.")]
        [SerializeField, Range(0.5f, 1f)] private float _warningBudgetFraction = 0.82f;
        [Tooltip("Budget utilization at which VRAM pressure becomes critical even before the hard budget break.")]
        [SerializeField, Range(0.7f, 1.5f)] private float _criticalBudgetFraction = 0.95f;
        
        // ── PRIVATE STATE ──────────────────────────────────────────────────────────
        
        private bool _registeredSlowTick;
        private ProfilerRecorder _textureMemoryRecorder;
        private ProfilerRecorder _renderTextureMemoryRecorder;
        
        // COLD ALLOC: StringBuilder[1024] — zero-GC logging — owner: VRAMMonitor
        private readonly StringBuilder _reportBuilder = new StringBuilder(1024);
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextLogTime;
#endif
        
        // ── PUBLIC PROPERTIES ──────────────────────────────────────────────────────
        
        /// <summary>
        /// Current texture memory consumption in bytes.
        /// </summary>
        public long TextureMemoryBytes { get; private set; }
        
        /// <summary>
        /// Current RenderTexture memory consumption in bytes.
        /// </summary>
        public long RenderTextureMemoryBytes { get; private set; }
        
        /// <summary>
        /// Total VRAM consumption in bytes (textures + RenderTextures + meshes + shaders).
        /// </summary>
        public long TotalVRAMBytes { get; private set; }
        
        /// <summary>
        /// Returns whether texture memory exceeds 900 MB threshold.
        /// </summary>
        public bool IsTextureMemoryOverBudget => TextureMemoryBytes > _budgetThresholds.TextureMemoryBudgetBytes;
        
        /// <summary>
        /// Returns whether RenderTexture memory exceeds 500 MB threshold.
        /// </summary>
        public bool IsRenderTextureMemoryOverBudget => RenderTextureMemoryBytes > _budgetThresholds.RenderTextureMemoryBudgetBytes;
        
        /// <summary>
        /// Returns whether total VRAM exceeds 1.2 GB threshold.
        /// </summary>
        public bool IsTotalVRAMOverBudget => TotalVRAMBytes > _budgetThresholds.TotalVRAMBudgetBytes;
        
        /// <summary>
        /// Normalized RenderTexture budget utilization.
        /// </summary>
        public float RenderTextureBudgetUtilization { get; private set; }
        
        /// <summary>
        /// Normalized total VRAM budget utilization.
        /// </summary>
        public float TotalVRAMBudgetUtilization { get; private set; }
        
        /// <summary>
        /// Current high-level VRAM pressure state.
        /// </summary>
        public VRAMPressureState PressureState { get; private set; }
        
        // ── LIFECYCLE ──────────────────────────────────────────────────────────────
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            _instance = this;
            
            StartRecorders();
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
            _textureMemoryRecorder.Dispose();
            _renderTextureMemoryRecorder.Dispose();
            
            if (_instance == this)
                _instance = null;
        }
        
        // ── ISLOWTICABLE ───────────────────────────────────────────────────────────
        
        /// <summary>
        /// ISlowTickable implementation. Measures VRAM every ~0.5s.
        /// Zero GC: pre-allocated buffers, no LINQ, no string concat.
        /// </summary>
        public void SlowTick()
        {
            MeasureVRAM();
            CheckThresholds();
        }
        
        // ── PUBLIC API ─────────────────────────────────────────────────────────────
        
        /// <summary>
        /// Queries current VRAM consumption breakdown.
        /// </summary>
        /// <param name="textureMemoryBytes">Texture memory in bytes.</param>
        /// <param name="renderTextureMemoryBytes">RenderTexture memory in bytes.</param>
        /// <param name="totalVRAMBytes">Total VRAM in bytes.</param>
        public void GetVRAMBreakdown(out long textureMemoryBytes, out long renderTextureMemoryBytes, out long totalVRAMBytes)
        {
            textureMemoryBytes = TextureMemoryBytes;
            renderTextureMemoryBytes = RenderTextureMemoryBytes;
            totalVRAMBytes = TotalVRAMBytes;
        }
        
        // ── PRIVATE METHODS ────────────────────────────────────────────────────────
        
        private void StartRecorders()
        {
            // Texture memory recorder
            _textureMemoryRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "Texture Memory",
                1,
                ProfilerRecorderOptions.Default);
            
            // RenderTexture memory recorder
            _renderTextureMemoryRecorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "RenderTexture Memory",
                1,
                ProfilerRecorderOptions.Default);
        }
        
        private void MeasureVRAM()
        {
            if (_textureMemoryRecorder.Valid)
            {
                TextureMemoryBytes = _textureMemoryRecorder.LastValue;
            }
            
            if (_renderTextureMemoryRecorder.Valid)
            {
                RenderTextureMemoryBytes = _renderTextureMemoryRecorder.LastValue;
            }
            
            TotalVRAMBytes = Profiler.GetTotalAllocatedMemoryLong();
            RenderTextureBudgetUtilization = CalculateBudgetUtilization(
                RenderTextureMemoryBytes,
                _budgetThresholds.RenderTextureMemoryBudgetBytes);
            TotalVRAMBudgetUtilization = CalculateBudgetUtilization(
                TotalVRAMBytes,
                _budgetThresholds.TotalVRAMBudgetBytes);
            UpdatePressureState();
        }
        
        private void CheckThresholds()
        {
            if (IsTextureMemoryOverBudget || IsRenderTextureMemoryOverBudget || IsTotalVRAMOverBudget)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (Time.time >= _nextLogTime)
                {
                    _nextLogTime = Time.time + 5f; // Throttle to once per 5s
                    LogVRAMWarning();
                }
#endif
            }
        }

        private float CalculateBudgetUtilization(long usedBytes, long budgetBytes)
        {
            if (budgetBytes <= 0L)
                return 0f;

            return usedBytes / (float)budgetBytes;
        }

        private void UpdatePressureState()
        {
            float maxUtilization = RenderTextureBudgetUtilization > TotalVRAMBudgetUtilization
                ? RenderTextureBudgetUtilization
                : TotalVRAMBudgetUtilization;

            if (IsRenderTextureMemoryOverBudget || IsTotalVRAMOverBudget || maxUtilization >= _criticalBudgetFraction)
            {
                PressureState = VRAMPressureState.Critical;
                return;
            }

            PressureState = maxUtilization >= _warningBudgetFraction
                ? VRAMPressureState.Warning
                : VRAMPressureState.Stable;
        }
        
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogVRAMWarning()
        {
            _reportBuilder.Clear();
            _reportBuilder.Append("[VRAMMonitor] BUDGET EXCEEDED: ");
            _reportBuilder.Append("Texture=").Append((TextureMemoryBytes / (1024f * 1024f)).ToString("0.0")).Append("MB ");
            _reportBuilder.Append("RT=").Append((RenderTextureMemoryBytes / (1024f * 1024f)).ToString("0.0")).Append("MB ");
            _reportBuilder.Append("Total=").Append((TotalVRAMBytes / (1024f * 1024f)).ToString("0.0")).Append("MB");
            
            Debug.LogWarning(_reportBuilder.ToString(), this);
        }
#endif
    }
}
