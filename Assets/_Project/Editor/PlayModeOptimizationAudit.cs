// HECTON-8 - PlayModeOptimizationAudit.cs
// Editor-side audit session for play mode optimization regressions.
// Aggregates runtime profiler, scatter, tick, pool, and smoke-test signals
// into one repeatable report for performance passes.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Tracks play-mode optimization regressions by listening to Unity logs and profiler output.
    /// </summary>
    internal static class PlayModeOptimizationAudit
    {
        private static readonly Regex _RuntimeProfilerRegex = new Regex(
            @"\[RuntimeProfiler\] window=\d+ frame=(?<frame>[\d\.]+)ms main=(?<main>[\d\.]+)ms gc=(?<gc>\d+)B mem=(?<mem>[\d\.]+)MB setPass=(?<setpass>\d+) batches=(?<batches>\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _ScatterProfilerRegex = new Regex(
            @"\[WorldScatterProfiler\] rebuild=(?<rebuild>[\d\.]+)ms sample=(?<sample>[\d\.]+)ms rescue=(?<rescue>[\d\.]+)ms restore=(?<restore>[\d\.]+)ms reconcile=(?<reconcile>[\d\.]+)ms",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _TickProfilerRegex = new Regex(
            @"\[TickProfiler\] SlowTick spike total=(?<total>[\d\.]+)ms",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _ToolSmokeCompleteRegex = new Regex(
            @"\[ToolSmoke\] COMPLETE pass=(?<pass>\d+) fail=(?<fail>\d+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex _PoolPrefabRegex = new Regex(
            @"\[ObjectPoolManager\] '(?<prefab>[^']+)'",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> _poolWarningPrefabs = new HashSet<string>(StringComparer.Ordinal);
        private static readonly StringBuilder _summaryBuilder = new StringBuilder(1024);

        private static bool _active;
        private static bool _startedProfiler;
        private static double _startedAtEditorTime;
        private static int _errorCount;
        private static int _warningCount;
        private static int _runtimeProfilerWindowCount;
        private static int _runtimeBudgetViolationCount;
        private static int _worldScatterSampleCount;
        private static int _worldScatterSpikeCount;
        private static int _slowTickSpikeCount;
        private static int _poolOnDemandCount;
        private static int _poolExpandCount;
        private static int _toolSmokePassCount;
        private static int _toolSmokeFailCount;
        private static int _builderSmokeFailCount;
        private static int _fieldToolSmokeFailCount;
        private static float _maxRuntimeFrameMs;
        private static float _maxRuntimeMainMs;
        private static int _maxRuntimeGcBytes;
        private static float _maxRuntimeMemoryMb;
        private static int _maxRuntimeSetPass;
        private static int _maxRuntimeBatches;
        private static float _maxScatterRebuildMs;
        private static float _maxScatterSampleMs;
        private static float _maxScatterReconcileMs;
        private static float _maxSlowTickMs;
        private static string _lastSummary = "No audit session started.";

        private const float ScatterSpikeThresholdMs = 40f;
        private const float SlowTickSpikeThresholdMs = 20f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            UnregisterCallbacks();
            Application.logMessageReceived -= HandleLogMessage;

            _active = false;
            _startedProfiler = false;
            _startedAtEditorTime = 0d;
            ResetSession();
            _summaryBuilder.Clear();

        }

        public static bool IsActive => _active;

        private static void RegisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        private static void UnregisterCallbacks()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        }

        public static void Start(
            bool startRuntimeProfiler = true,
            float sampleWindowSeconds = 2f,
            bool logEveryProfilerWindow = true)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[OptimizationAudit] Can only start in play mode.");
                return;
            }

            Stop(logSummary: false);
            ResetSession();

            _active = true;
            _startedAtEditorTime = EditorApplication.timeSinceStartup;
            RegisterCallbacks();

            Application.logMessageReceived -= HandleLogMessage;
            Application.logMessageReceived += HandleLogMessage;

            if (startRuntimeProfiler)
            {
                PlayModePerformanceMonitor.Start(
                    sampleWindowSeconds: sampleWindowSeconds,
                    logBudgetViolations: true,
                    logEveryWindow: logEveryProfilerWindow);
                _startedProfiler = true;
            }

            Debug.Log("[OptimizationAudit] Started play-mode optimization audit.");
        }

        public static void Stop(bool logSummary = true)
        {
            if (!_active && !_startedProfiler)
                return;

            UnregisterCallbacks();
            Application.logMessageReceived -= HandleLogMessage;

            if (_startedProfiler && PlayModePerformanceMonitor.IsActive)
                PlayModePerformanceMonitor.Stop();

            _startedProfiler = false;

            if (logSummary && (_active || _runtimeProfilerWindowCount > 0 || _worldScatterSampleCount > 0))
                Debug.Log(BuildSummary());

            _active = false;
        }

        public static void LogSummary()
        {
            Debug.Log(BuildSummary());
        }

        public static string DescribeStatus()
        {
            return
                $"[OptimizationAudit] active={_active} windows={_runtimeProfilerWindowCount} " +
                $"runtimeViolations={_runtimeBudgetViolationCount} scatterSpikes={_worldScatterSpikeCount} " +
                $"slowTickSpikes={_slowTickSpikeCount} poolOnDemand={_poolOnDemandCount} poolExpands={_poolExpandCount} " +
                $"toolFail={_toolSmokeFailCount} builderFail={_builderSmokeFailCount} fieldFail={_fieldToolSmokeFailCount} " +
                $"last=\"{_lastSummary}\"";
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                Stop(logSummary: _active);
        }

        private static void ResetSession()
        {
            _errorCount = 0;
            _warningCount = 0;
            _runtimeProfilerWindowCount = 0;
            _runtimeBudgetViolationCount = 0;
            _worldScatterSampleCount = 0;
            _worldScatterSpikeCount = 0;
            _slowTickSpikeCount = 0;
            _poolOnDemandCount = 0;
            _poolExpandCount = 0;
            _toolSmokePassCount = 0;
            _toolSmokeFailCount = 0;
            _builderSmokeFailCount = 0;
            _fieldToolSmokeFailCount = 0;
            _maxRuntimeFrameMs = 0f;
            _maxRuntimeMainMs = 0f;
            _maxRuntimeGcBytes = 0;
            _maxRuntimeMemoryMb = 0f;
            _maxRuntimeSetPass = 0;
            _maxRuntimeBatches = 0;
            _maxScatterRebuildMs = 0f;
            _maxScatterSampleMs = 0f;
            _maxScatterReconcileMs = 0f;
            _maxSlowTickMs = 0f;
            _poolWarningPrefabs.Clear();
            _lastSummary = "Audit running.";
        }

        private static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!_active || string.IsNullOrEmpty(condition))
                return;

            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                _errorCount++;
            else if (type == LogType.Warning)
                _warningCount++;

            ParseRuntimeProfiler(condition);
            ParseScatterProfiler(condition);
            ParseTickProfiler(condition);
            ParsePoolWarnings(condition);
            ParseSmokeSignals(condition);
        }

        private static void ParseRuntimeProfiler(string message)
        {
            Match match = _RuntimeProfilerRegex.Match(message);
            if (!match.Success)
                return;

            _runtimeProfilerWindowCount++;
            float frameMs = ParseFloat(match, "frame");
            float mainMs = ParseFloat(match, "main");
            int gcBytes = ParseInt(match, "gc");
            float memoryMb = ParseFloat(match, "mem");
            int setPass = ParseInt(match, "setpass");
            int batches = ParseInt(match, "batches");

            _maxRuntimeFrameMs = Mathf.Max(_maxRuntimeFrameMs, frameMs);
            _maxRuntimeMainMs = Mathf.Max(_maxRuntimeMainMs, mainMs);
            _maxRuntimeGcBytes = Mathf.Max(_maxRuntimeGcBytes, gcBytes);
            _maxRuntimeMemoryMb = Mathf.Max(_maxRuntimeMemoryMb, memoryMb);
            _maxRuntimeSetPass = Mathf.Max(_maxRuntimeSetPass, setPass);
            _maxRuntimeBatches = Mathf.Max(_maxRuntimeBatches, batches);

            if (PlayModePerformanceMonitor.IsBudgetExceeded(frameMs, mainMs, gcBytes, memoryMb, setPass, batches))
                _runtimeBudgetViolationCount++;
        }

        private static void ParseScatterProfiler(string message)
        {
            Match match = _ScatterProfilerRegex.Match(message);
            if (!match.Success)
                return;

            _worldScatterSampleCount++;
            float rebuildMs = ParseFloat(match, "rebuild");
            float sampleMs = ParseFloat(match, "sample");
            float reconcileMs = ParseFloat(match, "reconcile");
            _maxScatterRebuildMs = Mathf.Max(_maxScatterRebuildMs, rebuildMs);
            _maxScatterSampleMs = Mathf.Max(_maxScatterSampleMs, sampleMs);
            _maxScatterReconcileMs = Mathf.Max(_maxScatterReconcileMs, reconcileMs);
            if (rebuildMs > ScatterSpikeThresholdMs)
                _worldScatterSpikeCount++;
        }

        private static void ParseTickProfiler(string message)
        {
            Match match = _TickProfilerRegex.Match(message);
            if (!match.Success)
                return;

            float totalMs = ParseFloat(match, "total");
            _maxSlowTickMs = Mathf.Max(_maxSlowTickMs, totalMs);
            if (totalMs > SlowTickSpikeThresholdMs)
                _slowTickSpikeCount++;
        }

        private static void ParsePoolWarnings(string message)
        {
            if (!message.StartsWith("[ObjectPoolManager]", StringComparison.Ordinal))
                return;

            Match match = _PoolPrefabRegex.Match(message);
            if (match.Success)
                _poolWarningPrefabs.Add(match.Groups["prefab"].Value);

            if (message.IndexOf("Pool created on-demand", StringComparison.Ordinal) >= 0)
                _poolOnDemandCount++;

            if (message.IndexOf("Pool exhausted", StringComparison.Ordinal) >= 0)
                _poolExpandCount++;
        }

        private static void ParseSmokeSignals(string message)
        {
            if (message.StartsWith("[ToolSmoke] PASS ", StringComparison.Ordinal))
            {
                _toolSmokePassCount++;
                return;
            }

            if (message.StartsWith("[ToolSmoke] FAIL ", StringComparison.Ordinal) ||
                message.StartsWith("[ToolSmoke] EXCEPTION ", StringComparison.Ordinal))
            {
                _toolSmokeFailCount++;
                return;
            }

            Match toolCompleteMatch = _ToolSmokeCompleteRegex.Match(message);
            if (toolCompleteMatch.Success)
            {
                _toolSmokePassCount = Mathf.Max(_toolSmokePassCount, ParseInt(toolCompleteMatch, "pass"));
                _toolSmokeFailCount = Mathf.Max(_toolSmokeFailCount, ParseInt(toolCompleteMatch, "fail"));
                return;
            }

            if (message.StartsWith("[BuilderSmoke] FAIL", StringComparison.Ordinal))
            {
                _builderSmokeFailCount++;
                return;
            }

            if (message.StartsWith("[FieldToolSmoke] FAIL", StringComparison.Ordinal) ||
                message.StartsWith("[FieldToolSmoke] EXCEPTION", StringComparison.Ordinal) ||
                message.StartsWith("[FieldToolSmoke] TIMEOUT", StringComparison.Ordinal))
            {
                _fieldToolSmokeFailCount++;
            }
        }

        private static string BuildSummary()
        {
            double durationSeconds = _startedAtEditorTime > 0d
                ? Math.Max(0d, EditorApplication.timeSinceStartup - _startedAtEditorTime)
                : 0d;

            _summaryBuilder.Clear();
            _summaryBuilder.Append("[OptimizationAudit] duration=")
                .Append(durationSeconds.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("s errors=")
                .Append(_errorCount)
                .Append(" warnings=")
                .Append(_warningCount)
                .Append(" runtimeWindows=")
                .Append(_runtimeProfilerWindowCount)
                .Append(" runtimeViolations=")
                .Append(_runtimeBudgetViolationCount)
                .Append(" maxFrame=")
                .Append(_maxRuntimeFrameMs.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("ms maxMain=")
                .Append(_maxRuntimeMainMs.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("ms maxGc=")
                .Append(_maxRuntimeGcBytes)
                .Append("B maxMem=")
                .Append(_maxRuntimeMemoryMb.ToString("0.0", CultureInfo.InvariantCulture))
                .Append("MB scatterSamples=")
                .Append(_worldScatterSampleCount)
                .Append(" scatterSpikes=")
                .Append(_worldScatterSpikeCount)
                .Append(" maxScatter=")
                .Append(_maxScatterRebuildMs.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("ms maxScatterSample=")
                .Append(_maxScatterSampleMs.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("ms maxScatterReconcile=")
                .Append(_maxScatterReconcileMs.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("ms slowTickSpikes=")
                .Append(_slowTickSpikeCount)
                .Append(" maxSlowTick=")
                .Append(_maxSlowTickMs.ToString("0.00", CultureInfo.InvariantCulture))
                .Append("ms poolOnDemand=")
                .Append(_poolOnDemandCount)
                .Append(" poolExpands=")
                .Append(_poolExpandCount)
                .Append(" uniquePoolPrefabs=")
                .Append(_poolWarningPrefabs.Count)
                .Append(" toolPass=")
                .Append(_toolSmokePassCount)
                .Append(" toolFail=")
                .Append(_toolSmokeFailCount)
                .Append(" builderFail=")
                .Append(_builderSmokeFailCount)
                .Append(" fieldFail=")
                .Append(_fieldToolSmokeFailCount);

            if (_poolWarningPrefabs.Count > 0)
            {
                _summaryBuilder.Append(" pools=");
                int index = 0;
                foreach (string prefabName in _poolWarningPrefabs)
                {
                    if (index > 0)
                        _summaryBuilder.Append(',');

                    _summaryBuilder.Append(prefabName);
                    index++;
                    if (index >= 8)
                        break;
                }
            }

            if (PlayModePerformanceMonitor.IsActive)
            {
                _summaryBuilder.Append(" monitor=\"")
                    .Append(PlayModePerformanceMonitor.DescribeStatus())
                    .Append('"');
            }

            _lastSummary = _summaryBuilder.ToString();
            return _lastSummary;
        }

        private static float ParseFloat(Match match, string groupName)
        {
            return float.TryParse(
                match.Groups[groupName].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value)
                ? value
                : 0f;
        }

        private static int ParseInt(Match match, string groupName)
        {
            return int.TryParse(
                match.Groups[groupName].Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value)
                ? value
                : 0;
        }
    }
}
