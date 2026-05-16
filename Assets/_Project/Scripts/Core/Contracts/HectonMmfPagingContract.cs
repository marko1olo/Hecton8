namespace Hecton8.Core.Contracts
{
    public static class HectonMmfPagingContract
    {
        public const int BTreePageSizeBytes = 4096;
        public const int BTreePageAlignmentBytes = 64;
        public const int MacroDatabaseSectorSizeMeters = 512;
        public const int MacroDatabaseLowTierRadiusMeters = 1000;
        public const int MacroDatabaseMiddleTierRadiusMeters = 2000;
        public const int MacroDatabaseHighTierRadiusMeters = 3000;
        public const int MacroDatabaseUltraTierRadiusMeters = 4000;
        public const int MacroDatabaseDehydrateRadiusMeters = 3000;
        public const int MacroDatabaseMaxPayloadBytes = 256 * 1024;
        public const int MacroDatabaseNativeCacheCapacity = 2048;
        public const int MacroDatabaseMaxQuerySectors = 4096;
        public const long MacroDatabaseInitialFileBytes = 8L * 1024L * 1024L;
        public const long MacroDatabaseMaxFileBytes = 2L * 1024L * 1024L * 1024L;

        static HectonMmfPagingContract()
        {
            HectonContractValidator.RequirePositive(BTreePageSizeBytes, nameof(BTreePageSizeBytes));
            HectonContractValidator.RequirePositive(BTreePageAlignmentBytes, nameof(BTreePageAlignmentBytes));
            HectonContractValidator.RequirePositive(MacroDatabaseSectorSizeMeters, nameof(MacroDatabaseSectorSizeMeters));
            HectonContractValidator.RequirePositive(MacroDatabaseMaxPayloadBytes, nameof(MacroDatabaseMaxPayloadBytes));
        }
    }
}
