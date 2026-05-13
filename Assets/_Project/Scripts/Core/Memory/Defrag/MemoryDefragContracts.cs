namespace Hecton8.Core.Memory.Defrag
{
    /// <summary>
    /// Dispatcher phase contract for native memory compaction.
    /// </summary>
    public enum MemoryDefragPhase : byte
    {
        PreSimulation = 0
    }

    /// <summary>
    /// Shared constants for the core memory defrag assembly boundary.
    /// </summary>
    public static class MemoryDefragContracts
    {
        public const long FragmentationFreeThresholdBytes = 100L * 1024L * 1024L;
        public const long LargestContiguousBlockThresholdBytes = 10L * 1024L * 1024L;
        public const long MaxMoveBytesPerSlice = 5L * 1024L * 1024L;
        public const long MassiveMoveThresholdBytes = 50L * 1024L * 1024L;
        public const float WatchdogMilliseconds = 1.0f;
    }
}
