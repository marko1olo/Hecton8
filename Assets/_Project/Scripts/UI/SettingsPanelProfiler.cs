using System;
using System.Diagnostics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Performance profiler for SettingsPanel.
    /// Measures GC allocations, frame time, and apply time.
    /// Zero-GC: uses Stopwatch, no string allocations in hot paths.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Settings Panel Profiler")]
    public sealed class SettingsPanelProfiler : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        // INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("=== PROFILING ===")]
        [SerializeField, Tooltip("Enable profiling (Editor/Development builds only)")]
        private bool enableProfiling = true;

        [SerializeField, Tooltip("Log profiling results to console")]
        private bool logResults = true;

        [SerializeField, Tooltip("Target apply time (ms)")]
        private float targetApplyTimeMs = 50f;

        // ══════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════

        private Stopwatch _stopwatch;
        private long _gcAllocBefore;
        private long _gcAllocAfter;
        private long _totalMemoryBefore;
        private long _totalMemoryAfter;

        // Cached results (avoid string allocation)
        private float _lastApplyTimeMs;
        private long _lastGcAlloc;
        private long _lastMemoryDelta;

        // ══════════════════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (enableProfiling)
            {
                _stopwatch = new Stopwatch(); // COLD ALLOC: Stopwatch[1] — performance profiling — owner: SettingsPanelProfiler
            }
#endif
        }

        // ══════════════════════════════════════════════════════════
        // PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Start profiling session.
        /// Call before settings apply operation.
        /// </summary>
        public void BeginProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!enableProfiling || _stopwatch == null)
                return;

            _gcAllocBefore = GC.GetTotalMemory(false);
            _totalMemoryBefore = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;
            _stopwatch.Restart();
#endif
        }

        /// <summary>
        /// End profiling session and log results.
        /// Call after settings apply operation.
        /// </summary>
        public void EndProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!enableProfiling || _stopwatch == null)
                return;

            _stopwatch.Stop();
            _gcAllocAfter = GC.GetTotalMemory(false);
            _totalMemoryAfter = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64;

            _lastApplyTimeMs = (float)_stopwatch.Elapsed.TotalMilliseconds;
            _lastGcAlloc = _gcAllocAfter - _gcAllocBefore;
            _lastMemoryDelta = _totalMemoryAfter - _totalMemoryBefore;

            if (logResults)
            {
                LogResults();
            }
#endif
        }

        /// <summary>
        /// Get last profiling results.
        /// </summary>
        public void GetLastResults(out float applyTimeMs, out long gcAlloc, out long memoryDelta)
        {
            applyTimeMs = _lastApplyTimeMs;
            gcAlloc = _lastGcAlloc;
            memoryDelta = _lastMemoryDelta;
        }

        /// <summary>
        /// Check if last apply exceeded target time.
        /// </summary>
        public bool ExceededTargetTime()
        {
            return _lastApplyTimeMs > targetApplyTimeMs;
        }

        // ══════════════════════════════════════════════════════════
        // PRIVATE
        // ══════════════════════════════════════════════════════════

        private void LogResults()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[SettingsPanelProfiler] Apply metrics captured.");

            if (_lastApplyTimeMs > targetApplyTimeMs)
            {
                UnityEngine.Debug.LogWarning("[SettingsPanelProfiler] Apply time exceeded target.");
            }

            if (_lastGcAlloc > 0)
            {
                UnityEngine.Debug.LogWarning("[SettingsPanelProfiler] GC allocation detected.");
            }
#endif
        }
    }
}
