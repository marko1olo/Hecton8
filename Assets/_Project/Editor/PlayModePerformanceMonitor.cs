// ============================================================================
// HECTON-8 - PlayModePerformanceMonitor.cs
// Editor-side runtime performance monitor for play mode profiling.
// Samples ProfilerRecorder counters without requiring scene bootstrap objects.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Hecton8.Editor
{
    /// <summary>
    /// Runs a lightweight play-mode performance monitor from the Unity Editor.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModePerformanceMonitor
    {
        private const int RecorderCapacity = 1;
        private const float BytesPerMegabyte = 1024f * 1024f;
        private const float WarningRepeatCooldownSeconds = 10f;

        private static readonly string[] _FrameTimeCandidates = { "CPU Total Frame Time", "Frame Time" };
        private static readonly string[] _MainThreadCandidates = { "Main Thread" };
        private static readonly string[] _GcAllocCandidates = { "GC Allocated In Frame" };
        private static readonly string[] _SystemMemoryCandidates = { "System Used Memory", "Total Used Memory" };
        private static readonly string[] _SetPassCandidates = { "SetPass Calls Count" };
        private static readonly string[] _BatchesCandidates = { "Batches Count" };

        private static readonly List<ProfilerRecorderHandle> _availableHandles = new List<ProfilerRecorderHandle>(256);
        private static readonly StringBuilder _reportBuilder = new StringBuilder(512);

        private static ProfilerRecorder _frameTimeRecorder;
        private static ProfilerRecorder _mainThreadRecorder;
        private static ProfilerRecorder _gcAllocRecorder;
        private static ProfilerRecorder _systemMemoryRecorder;
        private static ProfilerRecorder _setPassRecorder;
        private static ProfilerRecorder _batchesRecorder;

        private static bool _active;
        private static bool _logBudgetViolations;
        private static bool _logEveryWindow;
        private static int _windowCount;
        private static int _violationWindowStreak;
        private static bool _lastWindowExceededBudget;
        private static double _lastViolationWarningAtEditorTime;
        private static double _lastEditorTimestamp;
        private static float _sampleWindowSeconds = 2f;
        private static float _sampleElapsed;
        private static float _lastFrameTimeMs;
        private static float _lastMainThreadMs;
        private static int _lastGcAllocBytes;
        private static float _lastSystemMemoryMb;
        private static int _lastSetPassCalls;
        private static int _lastBatches;
        private static float _peakFrameTimeMs;
        private static float _peakMainThreadMs;
        private static int _peakGcAllocBytes;
        private static float _peakSystemMemoryMb;
        private static int _peakSetPassCalls;
        private static int _peakBatches;
        private static float _frameTimeBudgetMs = 16.67f;
        private static float _mainThreadBudgetMs = 12f;
        private static int _gcAllocBudgetBytes;
        private static float _systemMemoryBudgetMb = 4096f;
        private static int _setPassBudget = 600;
        private static int _batchesBudget = 1800;
        private static string _frameTimeStat = "Unresolved";
        private static string _mainThreadStat = "Unresolved";
        private static string _gcAllocStat = "Unresolved";
        private static string _systemMemoryStat = "Unresolved";
        private static string _setPassStat = "Unresolved";
        private static string _batchesStat = "Unresolved";
        private static string _lastReport = "None";

        static PlayModePerformanceMonitor()
        {
            RegisterCallbacks();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            Stop();

            _logBudgetViolations = false;
            _logEveryWindow = false;
            _windowCount = 0;
            _violationWindowStreak = 0;
            _lastWindowExceededBudget = false;
            _lastViolationWarningAtEditorTime = 0d;
            _lastEditorTimestamp = 0d;
            _sampleWindowSeconds = 2f;
            _sampleElapsed = 0f;
            _lastFrameTimeMs = 0f;
            _lastMainThreadMs = 0f;
            _lastGcAllocBytes = 0;
            _lastSystemMemoryMb = 0f;
            _lastSetPassCalls = 0;
            _lastBatches = 0;
            _peakFrameTimeMs = 0f;
            _peakMainThreadMs = 0f;
            _peakGcAllocBytes = 0;
            _peakSystemMemoryMb = 0f;
            _peakSetPassCalls = 0;
            _peakBatches = 0;
            _frameTimeBudgetMs = 16.67f;
            _mainThreadBudgetMs = 12f;
            _gcAllocBudgetBytes = 0;
            _systemMemoryBudgetMb = 4096f;
            _setPassBudget = 600;
            _batchesBudget = 1800;
            _frameTimeStat = "Unresolved";
            _mainThreadStat = "Unresolved";
            _gcAllocStat = "Unresolved";
            _systemMemoryStat = "Unresolved";
            _setPassStat = "Unresolved";
            _batchesStat = "Unresolved";
            _lastReport = "None";
            _availableHandles.Clear();
            _reportBuilder.Clear();

            RegisterCallbacks();
        }

        public static bool IsActive => _active;

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static void Start(
            float sampleWindowSeconds = 2f,
            bool logBudgetViolations = true,
            bool logEveryWindow = true)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[RuntimeProfiler] Play mode monitor can only start in play mode.");
                return;
            }

            Stop();

            _sampleWindowSeconds = Mathf.Clamp(sampleWindowSeconds, 0.5f, 60f);
            _logBudgetViolations = logBudgetViolations;
            _logEveryWindow = logEveryWindow;
            ResolveRecorders();

            _active =
                _frameTimeRecorder.Valid ||
                _mainThreadRecorder.Valid ||
                _gcAllocRecorder.Valid ||
                _systemMemoryRecorder.Valid ||
                _setPassRecorder.Valid ||
                _batchesRecorder.Valid;

            ResetSampleWindow();
            _windowCount = 0;
            _lastEditorTimestamp = EditorApplication.timeSinceStartup;

            if (!_active)
            {
                _lastReport = "No profiler stats resolved.";
                Debug.LogWarning("[RuntimeProfiler] Editor monitor could not resolve profiler stats.");
                return;
            }

            EditorApplication.update -= Update;
            EditorApplication.update += Update;
            Debug.Log("[RuntimeProfiler] Editor play-mode monitor started.");
        }

        public static void Stop()
        {
            EditorApplication.update -= Update;
            DisposeRecorder(ref _frameTimeRecorder);
            DisposeRecorder(ref _mainThreadRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            DisposeRecorder(ref _systemMemoryRecorder);
            DisposeRecorder(ref _setPassRecorder);
            DisposeRecorder(ref _batchesRecorder);
            _active = false;
            _sampleElapsed = 0f;
            _lastEditorTimestamp = 0d;
        }

        public static void LogStatus()
        {
            Debug.Log(DescribeStatus());
        }

        public static void LogAvailableCounters()
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

            Debug.Log(_reportBuilder.ToString());
        }

        public static string DescribeStatus()
        {
            return
                $"[RuntimeProfiler] active={_active} windows={_windowCount} " +
                $"frame={_lastFrameTimeMs:0.00}ms main={_lastMainThreadMs:0.00}ms " +
                $"gc={_lastGcAllocBytes}B mem={_lastSystemMemoryMb:0.0}MB " +
                $"setPass={_lastSetPassCalls} batches={_lastBatches} report={_lastReport}";
        }

        internal static bool IsBudgetExceeded(
            float frameMs,
            float mainThreadMs,
            int gcAllocBytes,
            float systemMemoryMb,
            int setPassCalls,
            int batches)
        {
            return
                frameMs > _frameTimeBudgetMs ||
                mainThreadMs > _mainThreadBudgetMs ||
                gcAllocBytes > _gcAllocBudgetBytes ||
                systemMemoryMb > _systemMemoryBudgetMb ||
                setPassCalls > _setPassBudget ||
                batches > _batchesBudget;
        }

        private static void Update()
        {
            if (!_active)
                return;

            if (!EditorApplication.isPlaying)
            {
                Stop();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = _lastEditorTimestamp <= 0d ? 0f : (float)(now - _lastEditorTimestamp);
            _lastEditorTimestamp = now;
            if (deltaTime <= 0f)
                return;

            _sampleElapsed += deltaTime;
            SampleRecorders();

            if (_sampleElapsed >= _sampleWindowSeconds)
                FlushSampleWindow();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                Stop();
            }
        }

        private static void ResolveRecorders()
        {
            _frameTimeRecorder = StartRecorder(_FrameTimeCandidates, ref _frameTimeStat);
            _mainThreadRecorder = StartRecorder(_MainThreadCandidates, ref _mainThreadStat);
            _gcAllocRecorder = StartRecorder(_GcAllocCandidates, ref _gcAllocStat);
            _systemMemoryRecorder = StartRecorder(_SystemMemoryCandidates, ref _systemMemoryStat);
            _setPassRecorder = StartRecorder(_SetPassCandidates, ref _setPassStat);
            _batchesRecorder = StartRecorder(_BatchesCandidates, ref _batchesStat);
        }

        private static ProfilerRecorder StartRecorder(string[] candidates, ref string debugStatName)
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

        private static void SampleRecorders()
        {
            _lastFrameTimeMs = ReadMilliseconds(_frameTimeRecorder);
            _lastMainThreadMs = ReadMilliseconds(_mainThreadRecorder);
            _lastGcAllocBytes = ReadIntValue(_gcAllocRecorder);
            _lastSystemMemoryMb = ReadMegabytes(_systemMemoryRecorder);
            _lastSetPassCalls = ReadIntValue(_setPassRecorder);
            _lastBatches = ReadIntValue(_batchesRecorder);

            _peakFrameTimeMs = Mathf.Max(_peakFrameTimeMs, _lastFrameTimeMs);
            _peakMainThreadMs = Mathf.Max(_peakMainThreadMs, _lastMainThreadMs);
            _peakGcAllocBytes = Mathf.Max(_peakGcAllocBytes, _lastGcAllocBytes);
            _peakSystemMemoryMb = Mathf.Max(_peakSystemMemoryMb, _lastSystemMemoryMb);
            _peakSetPassCalls = Mathf.Max(_peakSetPassCalls, _lastSetPassCalls);
            _peakBatches = Mathf.Max(_peakBatches, _lastBatches);
        }

        private static void FlushSampleWindow()
        {
            _windowCount++;
            _lastWindowExceededBudget = IsBudgetExceeded(
                _peakFrameTimeMs,
                _peakMainThreadMs,
                _peakGcAllocBytes,
                _peakSystemMemoryMb,
                _peakSetPassCalls,
                _peakBatches);

            if (_lastWindowExceededBudget)
                _violationWindowStreak++;
            else
                _violationWindowStreak = 0;

            _reportBuilder.Clear();
            _reportBuilder.Append("[RuntimeProfiler] window=")
                .Append(_windowCount)
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

            _lastReport = _reportBuilder.ToString();

            if (_logEveryWindow || (_lastWindowExceededBudget && _logBudgetViolations))
            {
                if (_lastWindowExceededBudget && _logBudgetViolations && ShouldEmitViolationWarning())
                    Debug.LogWarning(_lastReport);
                else
                    Debug.Log(_lastReport);
            }

            ResetSampleWindow();
        }

        private static void ResetSampleWindow()
        {
            _sampleElapsed = 0f;
            _peakFrameTimeMs = 0f;
            _peakMainThreadMs = 0f;
            _peakGcAllocBytes = 0;
            _peakSystemMemoryMb = 0f;
            _peakSetPassCalls = 0;
            _peakBatches = 0;
            _lastFrameTimeMs = 0f;
            _lastMainThreadMs = 0f;
            _lastGcAllocBytes = 0;
            _lastSystemMemoryMb = 0f;
            _lastSetPassCalls = 0;
            _lastBatches = 0;
        }

        private static bool ShouldEmitViolationWarning()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_violationWindowStreak <= 1)
            {
                _lastViolationWarningAtEditorTime = now;
                return true;
            }

            if (now - _lastViolationWarningAtEditorTime >= WarningRepeatCooldownSeconds)
            {
                _lastViolationWarningAtEditorTime = now;
                return true;
            }

            return false;
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
