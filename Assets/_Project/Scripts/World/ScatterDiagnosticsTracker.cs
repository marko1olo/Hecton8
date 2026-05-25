#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
using System.Globalization;
using Hecton8.Dev;
using UnityEngine;

namespace Hecton8.World
{
    internal static class ScatterDiagnosticsTracker
    {
        private const float RuntimeReportLogIntervalSeconds = 5f;
        private static float _nextRuntimeReportLogTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeLogState()
        {
            _nextRuntimeReportLogTime = 0f;
        }

        public static ScatterRebuildProfileSnapshot BuildRebuildProfileSnapshot(
            in WorldProceduralScatterDirector.ScatterDiagnosticsCommitContext context)
        {
            long reconcileEndTimestamp = context.ReconcileMetrics.EndTimestamp;
            float samplingInputMs = GetElapsedMilliseconds(context.RebuildStartTimestamp, context.SamplingInputsEndTimestamp);
            float samplingWaitMs = GetElapsedMilliseconds(context.SamplingInputsEndTimestamp, context.SamplingCompleteEndTimestamp);
            float samplingPostMs = GetElapsedMilliseconds(context.SamplingCompleteEndTimestamp, context.SamplingEndTimestamp);
            float samplingMs = GetElapsedMilliseconds(context.RebuildStartTimestamp, context.SamplingEndTimestamp);
            float rescueMs = GetElapsedMilliseconds(context.SamplingEndTimestamp, context.RescueEndTimestamp);
            float restoreMs = GetElapsedMilliseconds(context.RescueEndTimestamp, context.RestoreEndTimestamp);
            float reconcileMs = GetElapsedMilliseconds(context.RestoreEndTimestamp, reconcileEndTimestamp);
            float diagnosticsMs = GetElapsedMilliseconds(reconcileEndTimestamp, context.DiagnosticsEndTimestamp);
            float totalMs = GetElapsedMilliseconds(context.RebuildStartTimestamp, context.DiagnosticsEndTimestamp);
            float reconcileCleanupMs = GetElapsedMilliseconds(context.RestoreEndTimestamp, context.ReconcileMetrics.CleanupEndTimestamp);
            float reconcileSpawnMs = GetElapsedMilliseconds(context.ReconcileMetrics.CleanupEndTimestamp, context.ReconcileMetrics.SpawnEndTimestamp);
            float reconcileFaunaMs = GetElapsedMilliseconds(context.ReconcileMetrics.SpawnEndTimestamp, context.ReconcileMetrics.EndTimestamp);

            return new ScatterRebuildProfileSnapshot(
                totalMs,
                samplingMs,
                samplingInputMs,
                samplingWaitMs,
                samplingPostMs,
                rescueMs,
                restoreMs,
                reconcileMs,
                reconcileCleanupMs,
                reconcileSpawnMs,
                reconcileFaunaMs,
                diagnosticsMs,
                context.ReconcileMetrics.RemovedCount,
                context.ReconcileMetrics.RebuiltCount,
                context.ReconcileMetrics.CreatedCount,
                context.ReconcileMetrics.ReusedCount,
                context.EvaluatedCells,
                context.DesiredCount,
                context.ActiveCount,
                context.FloraGpuiActiveCount,
                context.FloraGpuiPrototypeCount,
                context.FloraGpuiReady,
                context.Zone,
                context.Biome,
                context.Pattern,
                context.TopFamily,
                context.Reason);
        }

        public static void EmitRebuildReport(
            Object context,
            bool traceActive,
            bool shouldLog,
            in ScatterRebuildProfileSnapshot snapshot)
        {
            if (!traceActive && !shouldLog)
                return;

            string report =
                "[WorldScatterProfiler] rebuild=" + snapshot.TotalMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms sample=" + snapshot.SamplingMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms input=" + snapshot.SamplingInputMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms wait=" + snapshot.SamplingWaitMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms post=" + snapshot.SamplingPostMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms rescue=" + snapshot.RescueMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms restore=" + snapshot.RestoreMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms reconcile=" + snapshot.ReconcileMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms cleanup=" + snapshot.ReconcileCleanupMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms spawn=" + snapshot.ReconcileSpawnMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms fauna=" + snapshot.ReconcileFaunaMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms diag=" + snapshot.DiagnosticsMs.ToString("0.00", CultureInfo.InvariantCulture) +
                "ms removed=" + snapshot.RemovedCount +
                " rebuilt=" + snapshot.RebuiltCount +
                " created=" + snapshot.CreatedCount +
                " reused=" + snapshot.ReusedCount +
                " cells=" + snapshot.EvaluatedCells +
                " desired=" + snapshot.DesiredCount +
                " active=" + snapshot.ActiveCount +
                " floraGpuiActive=" + snapshot.FloraGpuiActiveCount +
                " floraGpuiPrototypes=" + snapshot.FloraGpuiPrototypeCount +
                " floraGpuiReady=" + snapshot.FloraGpuiReady +
                " zone=" + snapshot.Zone +
                " biome=" + snapshot.Biome +
                " pattern=" + snapshot.Pattern +
                " topFamily=" + snapshot.TopFamily +
                " reason=" + snapshot.Reason;

            if (traceActive)
                RuntimeDiagnosticsTrace.WriteEvent("scatter", report);

            if (ShouldEmitRuntimeReportLog(shouldLog))
                Hecton8.Core.H8Debug.Log(report, context);
        }

        private static bool ShouldEmitRuntimeReportLog(bool shouldLog)
        {
            if (!shouldLog)
                return false;

            if (!Application.isPlaying)
                return true;

            float now = Time.unscaledTime;
            if (now < _nextRuntimeReportLogTime)
                return false;

            _nextRuntimeReportLogTime = now + RuntimeReportLogIntervalSeconds;
            return true;
        }

        private static float GetElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            if (endTimestamp <= startTimestamp)
                return 0f;

            return (float)((endTimestamp - startTimestamp) * 1000.0d / Stopwatch.Frequency);
        }
    }
}
#else
using UnityEngine;

namespace Hecton8.World
{
    internal static class ScatterDiagnosticsTracker
    {
        public static ScatterRebuildProfileSnapshot BuildRebuildProfileSnapshot(
            in WorldProceduralScatterDirector.ScatterDiagnosticsCommitContext context)
        {
            return default;
        }

        public static void EmitRebuildReport(
            Object context,
            bool traceActive,
            bool shouldLog,
            in ScatterRebuildProfileSnapshot snapshot)
        {
        }
    }
}
#endif
