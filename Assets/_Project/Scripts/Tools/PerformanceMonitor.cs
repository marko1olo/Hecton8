using System;
using System.Globalization;
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
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // Run after most systems
    public sealed class PerformanceMonitor : MonoBehaviour, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
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
        private bool _registeredToTickManager;
        private bool _hotSwapRegistered;
        private float _captureStartTime;
        private float[] _frameTimes;
        private int _frameTimeCount;
        private float _lastLogTime;

        private void Awake()
        {
            EnsureFrameBufferCapacity();
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            _frameTimes = null;
            _frameTimeCount = 0;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registeredToTickManager = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher || currentService == null || !isActiveAndEnabled)
                return;

            TryUnregister();
            TryRegister();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void Tick(float dt)
        {
            if (!_isCapturing) return;

            int captureLimit = ResolveCaptureLimit();
            if (captureLimit <= 0)
                return;

            // Capture current frame data
            float currentFrameTimeMs = dt * 1000f;
            if (_frameTimeCount < captureLimit)
                _frameTimes[_frameTimeCount++] = currentFrameTimeMs;

            // Check if capture is complete
            if (_frameTimeCount >= captureLimit)
            {
                CompleteCapture();
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Periodic logging
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (_autoLogToConsole && now - _lastLogTime >= _logInterval)
            {
                LogCurrentPerformance(currentFrameTimeMs);
                _lastLogTime = now;
            }
#endif
        }

        /// <summary>
        /// Starts capturing performance data for the specified duration.
        /// </summary>
        public void StartCapture()
        {
            if (_isCapturing) return;

            _isCapturing = true;
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            _captureStartTime = now;
            _lastLogTime = now;
            EnsureFrameBufferCapacity();
            _frameTimeCount = 0;

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
            int sampleCount = ResolveSampleCount();
            float elapsed = _isCapturing ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds - _captureStartTime : 0f;
            float mean = 0f;
            float worst = 0f;
            float best = 0f;
            if (sampleCount > 0)
                CalculateStats(_frameTimes, sampleCount, out mean, out worst, out best);

            return
                "capturing=" + _isCapturing +
                " samples=" + sampleCount + "/" + _captureFrameCount +
                " elapsed=" + elapsed.ToString("F1", CultureInfo.InvariantCulture) + "s " +
                "mean=" + mean.ToString("F2", CultureInfo.InvariantCulture) + "ms " +
                "worst=" + worst.ToString("F2", CultureInfo.InvariantCulture) + "ms " +
                "best=" + best.ToString("F2", CultureInfo.InvariantCulture) + "ms " +
                "autoLog=" + _autoLogToConsole +
                " recordEntry=" + _recordBuildPlaytestEntry;
        }

        private void CompleteCapture()
        {
            _isCapturing = false;
            int sampleCount = ResolveSampleCount();
            var snapshot = CreateSnapshot();

            LogCaptureCompleted(snapshot, sampleCount);

#if UNITY_EDITOR
            RecordBuildPlaytestEntry(snapshot);
#endif
        }

        private PerformanceSnapshot CreateSnapshot()
        {
            int sampleCount = ResolveSampleCount();
            if (sampleCount == 0) return default;

            CalculateStats(_frameTimes, sampleCount, out float meanFrameTime, out float worstFrameTime, out float bestFrameTime);

            return new PerformanceSnapshot
            {
                Timestamp = DateTime.Now,
                MeanFrameTime = meanFrameTime,
                WorstFrameTime = worstFrameTime,
                BestFrameTime = bestFrameTime,
                SampleCount = sampleCount
            };
        }

        private void LogCurrentPerformance(float currentFrameTimeMs)
        {
            LogCurrentFrameTime(currentFrameTimeMs, ResolveSampleCount());
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

        private static void CalculateStats(float[] values, int count, out float mean, out float worst, out float best)
        {
            mean = 0f;
            worst = 0f;
            best = 0f;
            if (values == null || count <= 0)
                return;

            float sum = 0f;
            float max = float.MinValue;
            float min = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float value = values[i];
                sum += value;
                if (value > max) max = value;
                if (value < min) min = value;
            }

            mean = sum / count;
            worst = max;
            best = min;
        }

        private int ResolveCaptureLimit()
        {
            if (_frameTimes == null || _frameTimes.Length == 0)
                return 0;

            int configured = _captureFrameCount > 0 ? _captureFrameCount : 1;
            return configured < _frameTimes.Length ? configured : _frameTimes.Length;
        }

        private int ResolveSampleCount()
        {
            if (_frameTimes == null || _frameTimes.Length == 0)
                return 0;

            int captureLimit = ResolveCaptureLimit();
            return _frameTimeCount < captureLimit ? _frameTimeCount : captureLimit;
        }

        private void EnsureFrameBufferCapacity()
        {
            int requiredCapacity = _captureFrameCount > 0 ? _captureFrameCount : 1;
            if (_frameTimes != null && _frameTimes.Length >= requiredCapacity)
                return;

            _frameTimes = new float[requiredCapacity]; // COLD ALLOC: float[captureFrameCount] - bounded performance capture frame-time samples - owner: PerformanceMonitor
            _frameTimeCount = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogCaptureStarted(int targetFrameCount)
        {
            Hecton8.Core.H8Debug.Log($"[PerformanceMonitor] Started performance capture | targetFrames={targetFrameCount}");
        }

        private static void LogCaptureCompleted(PerformanceSnapshot snapshot, int sampleCount)
        {
            if (sampleCount <= 0)
            {
                Hecton8.Core.H8Debug.LogWarning("[PerformanceMonitor] Capture completed with no samples recorded");
                return;
            }

            Hecton8.Core.H8Debug.Log($"[PerformanceMonitor] Capture complete | samples={sampleCount}\n{snapshot.ToDetailedString()}");
        }

        private static void LogCurrentFrameTime(float currentFrameTimeMs, int sampleCount)
        {
            Hecton8.Core.H8Debug.Log("[PerformanceMonitor] Current: " + currentFrameTimeMs.ToString("F2", CultureInfo.InvariantCulture) + "ms | samples=" + sampleCount);
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
            return "Performance Snapshot (" + Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + ")\n" +
                   "Frame Time: Mean=" + MeanFrameTime.ToString("F2", CultureInfo.InvariantCulture) +
                   "ms, Worst=" + WorstFrameTime.ToString("F2", CultureInfo.InvariantCulture) +
                   "ms, Best=" + BestFrameTime.ToString("F2", CultureInfo.InvariantCulture) + "ms\n" +
                   "FPS: Mean=" + MeanFPS.ToString("F1", CultureInfo.InvariantCulture) +
                   ", Worst=" + WorstFPS.ToString("F1", CultureInfo.InvariantCulture) +
                   ", Best=" + BestFPS.ToString("F1", CultureInfo.InvariantCulture) + "\n" +
                   "Samples: " + SampleCount;
        }

        public string ToCompactString()
        {
            return Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " | " +
                   MeanFrameTime.ToString("F1", CultureInfo.InvariantCulture) + "ms (" +
                   MeanFPS.ToString("F0", CultureInfo.InvariantCulture) + "fps)";
        }
    }
}
