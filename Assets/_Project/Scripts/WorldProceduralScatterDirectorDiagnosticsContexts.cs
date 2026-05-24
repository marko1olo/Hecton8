namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal readonly struct ScatterDiagnosticsCommitContext
        {
            public readonly long RebuildStartTimestamp;
            public readonly long SamplingInputsEndTimestamp;
            public readonly long SamplingCompleteEndTimestamp;
            public readonly long SamplingEndTimestamp;
            public readonly long RescueEndTimestamp;
            public readonly long RestoreEndTimestamp;
            public readonly ScatterReconcileMetrics ReconcileMetrics;
            public readonly long DiagnosticsEndTimestamp;
            public readonly int EvaluatedCells;
            public readonly int DesiredCount;
            public readonly int ActiveCount;
            public readonly int FloraGpuiActiveCount;
            public readonly int FloraGpuiPrototypeCount;
            public readonly bool FloraGpuiReady;
            public readonly string Zone;
            public readonly string Biome;
            public readonly string Pattern;
            public readonly string TopFamily;
            public readonly string Reason;

            public ScatterDiagnosticsCommitContext(
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
                RebuildStartTimestamp = rebuildStartTimestamp;
                SamplingInputsEndTimestamp = samplingInputsEndTimestamp;
                SamplingCompleteEndTimestamp = samplingCompleteEndTimestamp;
                SamplingEndTimestamp = samplingEndTimestamp;
                RescueEndTimestamp = rescueEndTimestamp;
                RestoreEndTimestamp = restoreEndTimestamp;
                ReconcileMetrics = reconcileMetrics;
                DiagnosticsEndTimestamp = diagnosticsEndTimestamp;
                EvaluatedCells = evaluatedCells;
                DesiredCount = desiredCount;
                ActiveCount = activeCount;
                FloraGpuiActiveCount = floraGpuiActiveCount;
                FloraGpuiPrototypeCount = floraGpuiPrototypeCount;
                FloraGpuiReady = floraGpuiReady;
                Zone = zone;
                Biome = biome;
                Pattern = pattern;
                TopFamily = topFamily;
                Reason = reason;
            }
        }
    }
}
