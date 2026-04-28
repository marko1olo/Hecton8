using System;
using System.Collections.Generic;
using Hecton8.BuildTools;
using UnityEngine;
using Unity.Profiling;
using Hecton8.Core;

namespace Hecton8.Tools
{
    /// <summary>
    /// Performance monitoring system that captures frame time budgets after major passes.
    /// Provides guardrail data for CPU optimization decisions.
    /// </summary>
    [DefaultExecutionOrder(1000)] // Run after most systems
    public sealed class PerformanceMonitor : MonoBehaviour, ITickable, IUpdatable
    {
        [Header("Monitoring Settings")]
        [SerializeField, Tooltip("Frames to capture for averaging")]
        private int _captureFrameCount = 300; // 5 seconds at 60fps

        [SerializeField, Tooltip("Enable automatic logging to console")]
        private bool _autoLogToConsole = true;

        [SerializeField, Tooltip("Log interval in seconds")]
        private float _logInterval = 10f;

        [Header("Playtest Entry")]
        [SerializeField, Tooltip("Optional version label written into BuildPlaytestLog on capture completion.")]
        private string _buildVersionLabel = "dev-local";

        [SerializeField, Tooltip("Main irritant captured for this profiling pass.")]
        private string _mainIrritant = string.Empty;

        [SerializeField, Tooltip("Main visual flaw captured for this profiling pass.")]
        private string _mainVisualFlaw = string.Empty;

        [SerializeField, Tooltip("Main UX flaw captured for this profiling pass.")]
        private string _mainUxFlaw = string.Empty;

        [SerializeField, Tooltip("Main content gap captured for this profiling pass.")]
        private string _mainContentGap = string.Empty;

        [SerializeField, Tooltip("Mark this profiling pass as blocker when performance is not shippable.")]
        private bool _isBlocker;

        [SerializeField, Tooltip("Optional notes appended to the recorded playtest entry.")]
        private string _playtestNotes = string.Empty;

        [SerializeField, Tooltip("Record BuildPlaytestEntry automatically when a capture completes.")]
        private bool _recordBuildPlaytestEntry = true;

        // Performance counters
        private static readonly ProfilerCounterValue<float> _frameTimeCounter =
            new ProfilerCounterValue<float>(ProfilerCategory.Internal, "Frame Time", ProfilerMarkerDataUnit.TimeNanoseconds);

        private static readonly ProfilerCounterValue<int> _drawCallsCounter =
            new ProfilerCounterValue<int>(ProfilerCategory.Render, "Draw Calls", ProfilerMarkerDataUnit.Count);

        private static readonly ProfilerCounterValue<int> _batchesCounter =
            new ProfilerCounterValue<int>(ProfilerCategory.Render, "Batches", ProfilerMarkerDataUnit.Count);

        private static readonly ProfilerCounterValue<int> _trianglesCounter =
            new ProfilerCounterValue<int>(ProfilerCategory.Render, "Triangles", ProfilerMarkerDataUnit.Count);

        // State
        private bool _isCapturing;
        private float _captureStartTime;
        private List<float> _frameTimes;
        private float _lastLogTime;

        // Singleton access
        public static PerformanceMonitor Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureFrameBufferCapacity();
        }

        private void OnEnable()
        {
            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
        }

        private void OnDisable()
        {
            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Tick(float dt)
        {
            if (!_isCapturing) return;

            // Capture current frame data
            float currentFrameTimeMs = dt * 1000f;
            _frameTimes.Add(currentFrameTimeMs);

            // Check if capture is complete
            if (_frameTimes.Count >= _captureFrameCount)
            {
                CompleteCapture();
            }

            // Periodic logging
            if (_autoLogToConsole && Time.unscaledTime - _lastLogTime >= _logInterval)
            {
                LogCurrentPerformance(currentFrameTimeMs);
                _lastLogTime = Time.unscaledTime;
            }
        }

        /// <summary>
        /// Starts capturing performance data for the specified duration.
        /// </summary>
        public void StartCapture()
        {
            if (_isCapturing) return;

            _isCapturing = true;
            _captureStartTime = Time.unscaledTime;
            _lastLogTime = Time.unscaledTime;
            EnsureFrameBufferCapacity();
            _frameTimes.Clear();

            LogCaptureStarted(_captureFrameCount);
        }

        /// <summary>
        /// Stops capturing and returns the results.
        /// </summary>
        public PerformanceSnapshot StopCapture()
        {
            if (!_isCapturing) return default;

            _isCapturing = false;
            return CreateSnapshot();
        }

        /// <summary>
        /// Gets current performance snapshot without stopping capture.
        /// </summary>
        public PerformanceSnapshot GetCurrentSnapshot()
        {
            return CreateSnapshot();
        }

        /// <summary>
        /// Returns a compact status string for console or tooling use.
        /// </summary>
        public string DescribeStatus()
        {
            int sampleCount = _frameTimes != null ? _frameTimes.Count : 0;
            float elapsed = _isCapturing ? Time.unscaledTime - _captureStartTime : 0f;
            float mean = sampleCount > 0 ? CalculateMean(_frameTimes) : 0f;
            float worst = sampleCount > 0 ? CalculateMax(_frameTimes) : 0f;
            float best = sampleCount > 0 ? CalculateMin(_frameTimes) : 0f;

            return
                $"capturing={_isCapturing} samples={sampleCount}/{_captureFrameCount} elapsed={elapsed:F1}s " +
                $"mean={mean:F2}ms worst={worst:F2}ms best={best:F2}ms " +
                $"autoLog={_autoLogToConsole} recordEntry={_recordBuildPlaytestEntry}";
        }

        private void CompleteCapture()
        {
            _isCapturing = false;
            int sampleCount = _frameTimes != null ? _frameTimes.Count : 0;
            var snapshot = CreateSnapshot();

            LogCaptureCompleted(snapshot, sampleCount);

#if UNITY_EDITOR
            RecordBuildPlaytestEntry(snapshot);
#endif
        }

        private PerformanceSnapshot CreateSnapshot()
        {
            if (_frameTimes.Count == 0) return default;

            // Calculate statistics
            float meanFrameTime = CalculateMean(_frameTimes);
            float worstFrameTime = CalculateMax(_frameTimes);
            float bestFrameTime = CalculateMin(_frameTimes);

            return new PerformanceSnapshot
            {
                Timestamp = DateTime.Now,
                MeanFrameTime = meanFrameTime,
                WorstFrameTime = worstFrameTime,
                BestFrameTime = bestFrameTime,
                SampleCount = _frameTimes.Count
            };
        }

        private void LogCurrentPerformance(float currentFrameTimeMs)
        {
            LogCurrentFrameTime(currentFrameTimeMs, _frameTimes != null ? _frameTimes.Count : 0);
        }

#if UNITY_EDITOR
        private void RecordBuildPlaytestEntry(PerformanceSnapshot snapshot)
        {
            if (!_recordBuildPlaytestEntry || snapshot.SampleCount <= 0)
                return;

            BuildPlaytestEntry entry = BuildPlaytestEntry.Create(
                version: string.IsNullOrWhiteSpace(_buildVersionLabel) ? "dev-local" : _buildVersionLabel.Trim(),
                fpsMean: snapshot.MeanFPS,
                fpsWorst: snapshot.WorstFPS,
                mainIrritant: _mainIrritant,
                mainVisualFlaw: _mainVisualFlaw,
                mainUXFlaw: _mainUxFlaw,
                mainContentGap: _mainContentGap,
                isBlocker: _isBlocker,
                notes: ComposePlaytestNotes(snapshot));

            BuildPlaytestLog.RecordEntry(entry);
        }

        private string ComposePlaytestNotes(PerformanceSnapshot snapshot)
        {
            string baseNotes = string.IsNullOrWhiteSpace(_playtestNotes) ? string.Empty : _playtestNotes.Trim();
            string metricsNote = snapshot.ToCompactString();

            if (string.IsNullOrEmpty(baseNotes))
                return metricsNote;

            return baseNotes + " | " + metricsNote;
        }
#endif

        private static float CalculateMean(List<float> values)
        {
            if (values.Count == 0) return 0f;

            float sum = 0f;
            foreach (float value in values)
                sum += value;

            return sum / values.Count;
        }

        private static float CalculateMax(List<float> values)
        {
            if (values.Count == 0) return 0f;

            float max = float.MinValue;
            foreach (float value in values)
                if (value > max) max = value;

            return max;
        }

        private static float CalculateMin(List<float> values)
        {
            if (values.Count == 0) return float.MaxValue;

            float min = float.MaxValue;
            foreach (float value in values)
                if (value < min) min = value;

            return min;
        }

        private static float CalculateMean(List<int> values)
        {
            if (values.Count == 0) return 0f;

            float sum = 0f;
            foreach (int value in values)
                sum += value;

            return sum / values.Count;
        }

        private void EnsureFrameBufferCapacity()
        {
            int requiredCapacity = Mathf.Max(1, _captureFrameCount);
            if (_frameTimes != null && _frameTimes.Capacity >= requiredCapacity)
                return;

            _frameTimes = new List<float>(requiredCapacity); // COLD ALLOC: bounded capture buffer sized to the configured sample window
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogCaptureStarted(int targetFrameCount)
        {
            Debug.Log($"[PerformanceMonitor] Started performance capture | targetFrames={targetFrameCount}");
        }

        private static void LogCaptureCompleted(PerformanceSnapshot snapshot, int sampleCount)
        {
            if (sampleCount <= 0)
            {
                Debug.LogWarning("[PerformanceMonitor] Capture completed with no samples recorded");
                return;
            }

            Debug.Log($"[PerformanceMonitor] Capture complete | samples={sampleCount}\n{snapshot.ToDetailedString()}");
        }

        private static void LogCurrentFrameTime(float currentFrameTimeMs, int sampleCount)
        {
            Debug.Log($"[PerformanceMonitor] Current: {currentFrameTimeMs:F2}ms | samples={sampleCount}");
        }
#else
        private static void LogCaptureStarted(int targetFrameCount) { }
        private static void LogCaptureCompleted(PerformanceSnapshot snapshot, int sampleCount) { }
        private static void LogCurrentFrameTime(float currentFrameTimeMs, int sampleCount) { }
#endif

        private void OnValidate()
        {
            if (_captureFrameCount < 1)
                _captureFrameCount = 1;

            if (_logInterval < 0.1f)
                _logInterval = 0.1f;
        }
    }

    /// <summary>
    /// Snapshot of performance data at a point in time.
    /// </summary>
    [Serializable]
    public struct PerformanceSnapshot
    {
        public DateTime Timestamp;
        public float MeanFrameTime;    // milliseconds
        public float WorstFrameTime;   // milliseconds
        public float BestFrameTime;    // milliseconds
        public int SampleCount;

        public float MeanFPS => MeanFrameTime > 0 ? 1000f / MeanFrameTime : 0f;
        public float WorstFPS => WorstFrameTime > 0 ? 1000f / WorstFrameTime : 0f;
        public float BestFPS => BestFrameTime > 0 ? 1000f / BestFrameTime : 0f;

        public string ToDetailedString()
        {
            return $"Performance Snapshot ({Timestamp:yyyy-MM-dd HH:mm:ss})\n" +
                   $"Frame Time: Mean={MeanFrameTime:F2}ms, Worst={WorstFrameTime:F2}ms, Best={BestFrameTime:F2}ms\n" +
                   $"FPS: Mean={MeanFPS:F1}, Worst={WorstFPS:F1}, Best={BestFPS:F1}\n" +
                   $"Samples: {SampleCount}";
        }

        public string ToCompactString()
        {
            return $"{Timestamp:HH:mm:ss} | {MeanFrameTime:F1}ms ({MeanFPS:F0}fps)";
        }
    }
}
