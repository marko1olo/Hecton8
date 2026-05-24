namespace Hecton8.World
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal readonly struct ScatterRebuildProfileSnapshot
    {
        public readonly float TotalMs;
        public readonly float SamplingMs;
        public readonly float SamplingInputMs;
        public readonly float SamplingWaitMs;
        public readonly float SamplingPostMs;
        public readonly float RescueMs;
        public readonly float RestoreMs;
        public readonly float ReconcileMs;
        public readonly float ReconcileCleanupMs;
        public readonly float ReconcileSpawnMs;
        public readonly float ReconcileFaunaMs;
        public readonly float DiagnosticsMs;
        public readonly int RemovedCount;
        public readonly int RebuiltCount;
        public readonly int CreatedCount;
        public readonly int ReusedCount;
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

        public ScatterRebuildProfileSnapshot(
            float totalMs,
            float samplingMs,
            float samplingInputMs,
            float samplingWaitMs,
            float samplingPostMs,
            float rescueMs,
            float restoreMs,
            float reconcileMs,
            float reconcileCleanupMs,
            float reconcileSpawnMs,
            float reconcileFaunaMs,
            float diagnosticsMs,
            int removedCount,
            int rebuiltCount,
            int createdCount,
            int reusedCount,
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
            TotalMs = totalMs;
            SamplingMs = samplingMs;
            SamplingInputMs = samplingInputMs;
            SamplingWaitMs = samplingWaitMs;
            SamplingPostMs = samplingPostMs;
            RescueMs = rescueMs;
            RestoreMs = restoreMs;
            ReconcileMs = reconcileMs;
            ReconcileCleanupMs = reconcileCleanupMs;
            ReconcileSpawnMs = reconcileSpawnMs;
            ReconcileFaunaMs = reconcileFaunaMs;
            DiagnosticsMs = diagnosticsMs;
            RemovedCount = removedCount;
            RebuiltCount = rebuiltCount;
            CreatedCount = createdCount;
            ReusedCount = reusedCount;
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
