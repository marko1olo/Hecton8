namespace Hecton8.Core.Memory.Defrag
{
    /// <summary>
    /// Shared constants for the core memory defrag assembly boundary. The phase enum lives in the IDataVault contract.
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
