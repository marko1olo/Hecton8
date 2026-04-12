#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Diagnostics;
using Hecton8.Dev;
using UnityEngine;

namespace Hecton8.World
{
    internal static class ScatterDiagnosticsTracker
    {
        public static ScatterRebuildProfileSnapshot BuildRebuildProfileSnapshot(
            long rebuildStartTimestamp,
            long samplingInputsEndTimestamp,
            long samplingCompleteEndTimestamp,
            long samplingEndTimestamp,
            long rescueEndTimestamp,
            long restoreEndTimestamp,
            in ScatterReconcileMetrics reconcileMetrics,
            long diagnosticsEndTimestamp,
            int evaluatedCells,
            int desiredCount,
            int activeCount,
            int floraGpuiActiveCount,
            int floraGpuiPrototypeCount,
            bool floraGpuiReady,
            string zone,
            string biome,
            string pattern,
            string topFamily,
            string reason)
        {
            long reconcileEndTimestamp = reconcileMetrics.EndTimestamp;
            float samplingInputMs = GetElapsedMilliseconds(rebuildStartTimestamp, samplingInputsEndTimestamp);
            float samplingWaitMs = GetElapsedMilliseconds(samplingInputsEndTimestamp, samplingCompleteEndTimestamp);
            float samplingPostMs = GetElapsedMilliseconds(samplingCompleteEndTimestamp, samplingEndTimestamp);
            float samplingMs = GetElapsedMilliseconds(rebuildStartTimestamp, samplingEndTimestamp);
            float rescueMs = GetElapsedMilliseconds(samplingEndTimestamp, rescueEndTimestamp);
            float restoreMs = GetElapsedMilliseconds(rescueEndTimestamp, restoreEndTimestamp);
            float reconcileMs = GetElapsedMilliseconds(restoreEndTimestamp, reconcileEndTimestamp);
            float diagnosticsMs = GetElapsedMilliseconds(reconcileEndTimestamp, diagnosticsEndTimestamp);
            float totalMs = GetElapsedMilliseconds(rebuildStartTimestamp, diagnosticsEndTimestamp);
            float reconcileCleanupMs = GetElapsedMilliseconds(restoreEndTimestamp, reconcileMetrics.CleanupEndTimestamp);
            float reconcileSpawnMs = GetElapsedMilliseconds(reconcileMetrics.CleanupEndTimestamp, reconcileMetrics.SpawnEndTimestamp);
            float reconcileFaunaMs = GetElapsedMilliseconds(reconcileMetrics.SpawnEndTimestamp, reconcileMetrics.EndTimestamp);

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
                reconcileMetrics.RemovedCount,
                reconcileMetrics.RebuiltCount,
                reconcileMetrics.CreatedCount,
                reconcileMetrics.ReusedCount,
                evaluatedCells,
                desiredCount,
                activeCount,
                floraGpuiActiveCount,
                floraGpuiPrototypeCount,
                floraGpuiReady,
                zone,
                biome,
                pattern,
                topFamily,
                reason);
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
                $"[WorldScatterProfiler] rebuild={snapshot.TotalMs:0.00}ms sample={snapshot.SamplingMs:0.00}ms input={snapshot.SamplingInputMs:0.00}ms wait={snapshot.SamplingWaitMs:0.00}ms post={snapshot.SamplingPostMs:0.00}ms rescue={snapshot.RescueMs:0.00}ms " +
                $"restore={snapshot.RestoreMs:0.00}ms reconcile={snapshot.ReconcileMs:0.00}ms cleanup={snapshot.ReconcileCleanupMs:0.00}ms " +
                $"spawn={snapshot.ReconcileSpawnMs:0.00}ms fauna={snapshot.ReconcileFaunaMs:0.00}ms diag={snapshot.DiagnosticsMs:0.00}ms " +
                $"removed={snapshot.RemovedCount} rebuilt={snapshot.RebuiltCount} created={snapshot.CreatedCount} reused={snapshot.ReusedCount} " +
                $"cells={snapshot.EvaluatedCells} desired={snapshot.DesiredCount} active={snapshot.ActiveCount} floraGpuiActive={snapshot.FloraGpuiActiveCount} " +
                $"floraGpuiPrototypes={snapshot.FloraGpuiPrototypeCount} floraGpuiReady={snapshot.FloraGpuiReady} " +
                $"zone={snapshot.Zone} biome={snapshot.Biome} pattern={snapshot.Pattern} topFamily={snapshot.TopFamily} reason={snapshot.Reason}";

            if (traceActive)
                RuntimeDiagnosticsTrace.WriteEvent("scatter", report);

            if (shouldLog)
                UnityEngine.Debug.Log(report, context);
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
            long rebuildStartTimestamp,
            long samplingInputsEndTimestamp,
            long samplingCompleteEndTimestamp,
            long samplingEndTimestamp,
            long rescueEndTimestamp,
            long restoreEndTimestamp,
            in ScatterReconcileMetrics reconcileMetrics,
            long diagnosticsEndTimestamp,
            int evaluatedCells,
            int desiredCount,
            int activeCount,
            int floraGpuiActiveCount,
            int floraGpuiPrototypeCount,
            bool floraGpuiReady,
            string zone,
            string biome,
            string pattern,
            string topFamily,
            string reason)
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
