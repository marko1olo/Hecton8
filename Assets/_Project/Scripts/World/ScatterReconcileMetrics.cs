namespace Hecton8.World
{
    internal readonly struct ScatterReconcileMetrics
    {
        public readonly int RemovedCount;
        public readonly int RebuiltCount;
        public readonly int CreatedCount;
        public readonly int ReusedCount;
        public readonly long CleanupEndTimestamp;
        public readonly long SpawnEndTimestamp;
        public readonly long EndTimestamp;

        public ScatterReconcileMetrics(
            int removedCount,
            int rebuiltCount,
            int createdCount,
            int reusedCount,
            long cleanupEndTimestamp,
            long spawnEndTimestamp,
            long endTimestamp)
        {
            RemovedCount = removedCount;
            RebuiltCount = rebuiltCount;
            CreatedCount = createdCount;
            ReusedCount = reusedCount;
            CleanupEndTimestamp = cleanupEndTimestamp;
            SpawnEndTimestamp = spawnEndTimestamp;
            EndTimestamp = endTimestamp;
        }
    }
}
