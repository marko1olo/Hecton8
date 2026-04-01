// ============================================================================
// HECTON-8 - RuntimePerformanceProfiler.cs
// Runtime profiler bridge for frame-time, GC and memory budgets.
// Uses ProfilerRecorder and GameTickManager instead of native Update.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Dev
{
    /// <summary>
    /// Captures runtime performance counters and reports budget violations.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Runtime Performance Profiler")]
    public sealed class RuntimePerformanceProfiler : MonoBehaviour, ITickable, ISlowTickable
    {
        private const int RecorderCapacity = 1;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float FallbackSlowTickInterval = 0.5f;

        private static readonly string[] _FrameTimeCandidates =
        {
            "CPU Total Frame Time",
            "Frame Time"
        };

        private static readonly string[] _MainThreadCandidates = { "Main Thread" };
        private static readonly string[] _GcAllocCandidates = { "GC Allocated In Frame" };
        private static readonly string[] _SystemMemoryCandidates = { "System Used Memory", "Total Used Memory" };
        private static readonly string[] _SetPassCandidates = { "SetPass Calls Count" };
        private static readonly string[] _BatchesCandidates = { "Batches Count" };

        [Header("Execution")]
        [SerializeField] private bool startProfilingOnEnable = true;
        [SerializeField] private bool logBudgetViolations = true;
        [SerializeField] private bool logEveryWindow = false;
        [SerializeField] private float sampleWindowSeconds = 5f;

        [Header("Budgets")]
        [SerializeField] private float frameTimeBudgetMs = 16.67f;
        [SerializeField] private float mainThreadBudgetMs = 12f;
        [SerializeField] private int gcAllocBudgetBytes = 0;
        [SerializeField] private float systemMemoryBudgetMb = 4096f;
        [SerializeField] private int setPassBudget = 600;
        [SerializeField] private int batchesBudget = 1800;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugProfilingActive;
        [SerializeField] private int _debugWindowCount;
        [SerializeField] private bool _debugLastWindowExceededBudget;
        [SerializeField] private int _debugTickCount;
        [SerializeField] private int _debugFallbackUpdateCount;
        [SerializeField] private float _debugLastDeltaTime;
        [SerializeField] private bool _debugUsingFallbackUpdate;
        [SerializeField] private float _debugLastFrameTimeMs;
        [SerializeField] private float _debugLastMainThreadMs;
        [SerializeField] private int _debugLastGcAllocBytes;
        [SerializeField] private float _debugLastSystemMemoryMb;
        [SerializeField] private int _debugLastSetPassCalls;
        [SerializeField] private int _debugLastBatches;
        [SerializeField] private float _debugPeakFrameTimeMs;
        [SerializeField] private float _debugPeakMainThreadMs;
        [SerializeField] private int _debugPeakGcAllocBytes;
        [SerializeField] private float _debugPeakSystemMemoryMb;
        [SerializeField] private int _debugPeakSetPassCalls;
        [SerializeField] private int _debugPeakBatches;
        [SerializeField] private string _debugFrameTimeStat = "Unresolved";
        [SerializeField] private string _debugMainThreadStat = "Unresolved";
        [SerializeField] private string _debugGcAllocStat = "Unresolved";
        [SerializeField] private string _debugSystemMemoryStat = "Unresolved";
        [SerializeField] private string _debugSetPassStat = "Unresolved";
        [SerializeField] private string _debugBatchesStat = "Unresolved";
        [SerializeField] private string _debugLastReport = "None";

        private readonly List<ProfilerRecorderHandle> _availableHandles = new List<ProfilerRecorderHandle>(256);
        private readonly StringBuilder _reportBuilder = new StringBuilder(512);

        private ProfilerRecorder _frameTimeRecorder;
        private ProfilerRecorder _mainThreadRecorder;
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _systemMemoryRecorder;
        private ProfilerRecorder _setPassRecorder;
        private ProfilerRecorder _batchesRecorder;

        private bool _registeredTick;
        private bool _registeredSlowTick;
        private float _sampleElapsed;
        private float _fallbackSlowTickElapsed;
        private float _peakFrameTimeMs;
        private float _peakMainThreadMs;
        private int _peakGcAllocBytes;
        private float _peakSystemMemoryMb;
        private int _peakSetPassCalls;
        private int _peakBatches;

        private void Awake()
        {
            ClampSettings();
        }

        private void OnEnable()
        {
            ClampSettings();
            RegisterWithTickManager();
            if (startProfilingOnEnable)
                StartProfiling();
        }

        private void OnDisable()
        {
            StopProfiling();
            UnregisterFromTickManager();
        }

        private void Update()
        {
            if (_registeredTick && _registeredSlowTick)
                return;

            RegisterWithTickManager();
            if (_registeredTick && _registeredSlowTick)
                return;

            if (!_debugProfilingActive)
                return;

            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
                return;

            _debugUsingFallbackUpdate = true;
            _debugFallbackUpdateCount++;
            _debugLastDeltaTime = deltaTime;
            Tick(deltaTime);

            _fallbackSlowTickElapsed += deltaTime;
            if (_fallbackSlowTickElapsed >= FallbackSlowTickInterval)
            {
                _fallbackSlowTickElapsed = 0f;
                SlowTick();
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ClampSettings();
        }
#endif

        [ContextMenu("Start Runtime Performance Profiling")]
        public void StartProfiling()
        {
            StopProfiling();

            ResolveRecorders();
            _debugProfilingActive =
                _frameTimeRecorder.Valid ||
                _mainThreadRecorder.Valid ||
                _gcAllocRecorder.Valid ||
                _systemMemoryRecorder.Valid ||
                _setPassRecorder.Valid ||
                _batchesRecorder.Valid;

            ResetSampleWindow();

            if (!_debugProfilingActive)
            {
                _debugLastReport = "No profiler stats resolved.";
                Debug.LogWarning("[RuntimeProfiler] No profiler stats were resolved. Use context menu to inspect available counters.", this);
            }
        }

        /// <summary>
        /// Applies development-friendly sampling settings for live profiling sessions.
        /// </summary>
        public void ConfigureForDevRun(
            bool autoStartOnEnable = true,
            bool enableBudgetViolationLogging = true,
            bool enableWindowLogging = true,
            float sampleWindow = 2f)
        {
            startProfilingOnEnable = autoStartOnEnable;
            logBudgetViolations = enableBudgetViolationLogging;
            logEveryWindow = enableWindowLogging;
            sampleWindowSeconds = sampleWindow;
            ClampSettings();
        }

        [ContextMenu("Stop Runtime Performance Profiling")]
        public void StopProfiling()
        {
            DisposeRecorder(ref _frameTimeRecorder);
            DisposeRecorder(ref _mainThreadRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            DisposeRecorder(ref _systemMemoryRecorder);
            DisposeRecorder(ref _setPassRecorder);
            DisposeRecorder(ref _batchesRecorder);

            _debugProfilingActive = false;
        }

        [ContextMenu("Log Available Runtime Profiler Counters")]
        public void LogAvailableCounters()
        {
            ProfilerRecorderHandle.GetAvailable(_availableHandles);
            _reportBuilder.Clear();
            _reportBuilder.Append("[RuntimeProfiler] available counters=").Append(_availableHandles.Count);

            int appended = 0;
            for (int i = 0; i < _availableHandles.Count && appended < 32; i++)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(_availableHandles[i]);
                string name = description.Name;
                if (!ContainsAnyKeyword(name, "Frame", "Main", "GC", "Memory", "Batch", "SetPass"))
                    continue;

                _reportBuilder.Append(" | ")
                    .Append(description.Category.Name)
                    .Append(':')
                    .Append(name);
                appended++;
            }

            Debug.Log(_reportBuilder.ToString(), this);
        }

        public void Tick(float deltaTime)
        {
            if (!_debugProfilingActive)
                return;

            _debugTickCount++;
            _debugLastDeltaTime = deltaTime;
            _debugUsingFallbackUpdate = !_registeredTick || !_registeredSlowTick;
            _sampleElapsed += deltaTime;
            SampleRecorders();

            if (_sampleElapsed >= sampleWindowSeconds)
                FlushSampleWindow();
        }

        public void SlowTick()
        {
            if (!_debugProfilingActive)
                return;

            _debugPeakFrameTimeMs = _peakFrameTimeMs;
            _debugPeakMainThreadMs = _peakMainThreadMs;
            _debugPeakGcAllocBytes = _peakGcAllocBytes;
            _debugPeakSystemMemoryMb = _peakSystemMemoryMb;
            _debugPeakSetPassCalls = _peakSetPassCalls;
            _debugPeakBatches = _peakBatches;
        }

        public string DescribeStatus()
        {
            return
                $"active={_debugProfilingActive} windows={_debugWindowCount} " +
                $"frame={_debugLastFrameTimeMs:0.00}ms main={_debugLastMainThreadMs:0.00}ms " +
                $"gc={_debugLastGcAllocBytes}B mem={_debugLastSystemMemoryMb:0.0}MB " +
                $"setPass={_debugLastSetPassCalls} batches={_debugLastBatches} " +
                $"ticks={_debugTickCount} fallback={_debugFallbackUpdateCount} dt={_debugLastDeltaTime:0.000}";
        }

        [ContextMenu("Log Runtime Performance Profiling Status")]
        public void LogStatusToConsole()
        {
            Debug.Log("[RuntimeProfiler] " + DescribeStatus(), this);
        }

        private void RegisterWithTickManager()
        {
            if (GameTickManager.Instance == null)
                return;

            if (!_registeredTick)
            {
                GameTickManager.Instance.Register((ITickable)this);
                _registeredTick = true;
            }

            if (!_registeredSlowTick)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredSlowTick = true;
            }
        }

        private void UnregisterFromTickManager()
        {
            if (GameTickManager.Instance == null)
                return;

            if (_registeredTick)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                _registeredTick = false;
            }

            if (_registeredSlowTick)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredSlowTick = false;
            }
        }

        private void ResolveRecorders()
        {
            _frameTimeRecorder = StartRecorder(_FrameTimeCandidates, ref _debugFrameTimeStat);
            _mainThreadRecorder = StartRecorder(_MainThreadCandidates, ref _debugMainThreadStat);
            _gcAllocRecorder = StartRecorder(_GcAllocCandidates, ref _debugGcAllocStat);
            _systemMemoryRecorder = StartRecorder(_SystemMemoryCandidates, ref _debugSystemMemoryStat);
            _setPassRecorder = StartRecorder(_SetPassCandidates, ref _debugSetPassStat);
            _batchesRecorder = StartRecorder(_BatchesCandidates, ref _debugBatchesStat);
        }

        private ProfilerRecorder StartRecorder(string[] candidates, ref string debugStatName)
        {
            debugStatName = "Unresolved";
            ProfilerRecorderHandle.GetAvailable(_availableHandles);

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                for (int handleIndex = 0; handleIndex < _availableHandles.Count; handleIndex++)
                {
                    ProfilerRecorderDescription description =
                        ProfilerRecorderHandle.GetDescription(_availableHandles[handleIndex]);
                    string descriptionName = description.Name;
                    if (!MatchesCandidate(descriptionName, candidate))
                        continue;

                    try
                    {
                        ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                            description.Category,
                            descriptionName,
                            RecorderCapacity,
                            ProfilerRecorderOptions.Default);
                        if (recorder.Valid)
                        {
                            debugStatName = $"{description.Category.Name}:{descriptionName}";
                            return recorder;
                        }
                    }
                    catch (ArgumentException)
                    {
                    }
                }
            }

            return default;
        }

        private void SampleRecorders()
        {
            _debugLastFrameTimeMs = ReadMilliseconds(_frameTimeRecorder);
            _debugLastMainThreadMs = ReadMilliseconds(_mainThreadRecorder);
            _debugLastGcAllocBytes = ReadIntValue(_gcAllocRecorder);
            _debugLastSystemMemoryMb = ReadMegabytes(_systemMemoryRecorder);
            _debugLastSetPassCalls = ReadIntValue(_setPassRecorder);
            _debugLastBatches = ReadIntValue(_batchesRecorder);

            _peakFrameTimeMs = Mathf.Max(_peakFrameTimeMs, _debugLastFrameTimeMs);
            _peakMainThreadMs = Mathf.Max(_peakMainThreadMs, _debugLastMainThreadMs);
            _peakGcAllocBytes = Mathf.Max(_peakGcAllocBytes, _debugLastGcAllocBytes);
            _peakSystemMemoryMb = Mathf.Max(_peakSystemMemoryMb, _debugLastSystemMemoryMb);
            _peakSetPassCalls = Mathf.Max(_peakSetPassCalls, _debugLastSetPassCalls);
            _peakBatches = Mathf.Max(_peakBatches, _debugLastBatches);
        }

        private void FlushSampleWindow()
        {
            _debugWindowCount++;
            _debugLastWindowExceededBudget =
                _peakFrameTimeMs > frameTimeBudgetMs ||
                _peakMainThreadMs > mainThreadBudgetMs ||
                _peakGcAllocBytes > gcAllocBudgetBytes ||
                _peakSystemMemoryMb > systemMemoryBudgetMb ||
                _peakSetPassCalls > setPassBudget ||
                _peakBatches > batchesBudget;

            _reportBuilder.Clear();
            _reportBuilder.Append("[RuntimeProfiler] window=")
                .Append(_debugWindowCount)
                .Append(" frame=")
                .Append(_peakFrameTimeMs.ToString("0.00"))
                .Append("ms main=")
                .Append(_peakMainThreadMs.ToString("0.00"))
                .Append("ms gc=")
                .Append(_peakGcAllocBytes)
                .Append("B mem=")
                .Append(_peakSystemMemoryMb.ToString("0.0"))
                .Append("MB setPass=")
                .Append(_peakSetPassCalls)
                .Append(" batches=")
                .Append(_peakBatches);

            _debugLastReport = _reportBuilder.ToString();

            if (logEveryWindow || (_debugLastWindowExceededBudget && logBudgetViolations))
            {
                if (_debugLastWindowExceededBudget)
                    Debug.LogWarning(_debugLastReport, this);
                else
                    Debug.Log(_debugLastReport, this);
            }

            ResetSampleWindow();
        }

        private void ResetSampleWindow()
        {
            _sampleElapsed = 0f;
            _fallbackSlowTickElapsed = 0f;
            _debugTickCount = 0;
            _debugFallbackUpdateCount = 0;
            _debugLastDeltaTime = 0f;
            _debugUsingFallbackUpdate = false;
            _peakFrameTimeMs = 0f;
            _peakMainThreadMs = 0f;
            _peakGcAllocBytes = 0;
            _peakSystemMemoryMb = 0f;
            _peakSetPassCalls = 0;
            _peakBatches = 0;
        }

        private void ClampSettings()
        {
            sampleWindowSeconds = Mathf.Clamp(sampleWindowSeconds, 0.5f, 60f);
            frameTimeBudgetMs = Mathf.Clamp(frameTimeBudgetMs, 1f, 100f);
            mainThreadBudgetMs = Mathf.Clamp(mainThreadBudgetMs, 1f, 100f);
            gcAllocBudgetBytes = Mathf.Clamp(gcAllocBudgetBytes, 0, 4 * 1024 * 1024);
            systemMemoryBudgetMb = Mathf.Clamp(systemMemoryBudgetMb, 128f, 16384f);
            setPassBudget = Mathf.Clamp(setPassBudget, 1, 10000);
            batchesBudget = Mathf.Clamp(batchesBudget, 1, 20000);
        }

        private static float ReadMilliseconds(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0f;

            if (recorder.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds)
                return (float)(recorder.LastValue / 1000000.0d);

            return (float)recorder.LastValue;
        }

        private static float ReadMegabytes(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0f;

            if (recorder.UnitType == ProfilerMarkerDataUnit.Bytes)
                return (float)(recorder.LastValue / BytesPerMegabyte);

            return (float)recorder.LastValue;
        }

        private static int ReadIntValue(ProfilerRecorder recorder)
        {
            if (!recorder.Valid)
                return 0;

            long value = recorder.LastValue;
            if (value <= 0L)
                return 0;

            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();

            recorder = default;
        }

        private static bool MatchesCandidate(string value, string candidate)
        {
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(candidate))
                return false;

            return string.Equals(value, candidate, StringComparison.OrdinalIgnoreCase) ||
                   value.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAnyKeyword(string value, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value) || keywords == null)
                return false;

            for (int i = 0; i < keywords.Length; i++)
            {
                if (value.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
