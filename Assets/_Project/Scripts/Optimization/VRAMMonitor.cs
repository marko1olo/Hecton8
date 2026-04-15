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
        // ── SINGLETON ──────────────────────────────────────────────────────────────
        
        private static VRAMMonitor _instance;
        
        /// <summary>
        /// Singleton instance. Null-check required in OnDestroy.
        /// </summary>
        public static VRAMMonitor Instance => _instance;
        
        // ── INSPECTOR SETTINGS ─────────────────────────────────────────────────────
        
        [Header("── VRAM Budget Thresholds ──────────────────")]
        [SerializeField] private VRAMBudgetThresholds _budgetThresholds = VRAMBudgetThresholds.Default;
        
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
