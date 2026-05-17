namespace Hecton8.Core.Contracts
{
    public static class HectonPlatformContract
    {
        public const int AbiStructPackBytes = 1;
        public const int SimdAlignmentBytes = 16;
        public const int CacheLineBytes = 64;
        public const int NativePageAlignmentBytes = 4096;

        public const int UniversalMaxComputeThreadsPerGroup = 1024;
        public const int QuestSafeComputeThreadsPerGroup = 256;
        public const int AndroidSafeComputeThreadsPerGroup = 256;
        public const int SteamDeckSafeComputeThreadsPerGroup = 512;
        public const int MetalSafeComputeThreadsPerGroup = 512;
        public const int PcUltraComputeThreadsPerGroup = 1024;

        public const int QuestMaxThreadGroupZ = 64;
        public const int AndroidMaxThreadGroupZ = 64;
        public const int MetalMaxThreadGroupZ = 64;

        public const int SteamDeckMicroSdReadBudgetBytesPerFrameLow = 16 * 1024;
        public const int SteamDeckMicroSdReadBudgetBytesPerFrameMiddle = 32 * 1024;
        public const int SteamDeckMicroSdReadBudgetBytesPerFrameHigh = 64 * 1024;
        public const int SteamDeckMicroSdReadBudgetBytesPerFrameUltra = 128 * 1024;
        public const int SteamDeckMmfPrefetchPageBudgetLow = 1;
        public const int SteamDeckMmfPrefetchPageBudgetUltra = 8;

        public const int ContractBlackBoxFrameCapacity = 300;
        public const int ContractHeartbeatStrideBytes = 32;
        public const int ContractHeartbeatBufferBytes = ContractBlackBoxFrameCapacity * ContractHeartbeatStrideBytes;

        static HectonPlatformContract()
        {
            HectonContractValidator.RequirePositive(AbiStructPackBytes, nameof(AbiStructPackBytes));
            HectonContractValidator.RequirePositive(SimdAlignmentBytes, nameof(SimdAlignmentBytes));
            HectonContractValidator.RequirePositive(CacheLineBytes, nameof(CacheLineBytes));
            HectonContractValidator.RequirePositive(UniversalMaxComputeThreadsPerGroup, nameof(UniversalMaxComputeThreadsPerGroup));
            HectonContractValidator.RequirePositive(QuestSafeComputeThreadsPerGroup, nameof(QuestSafeComputeThreadsPerGroup));
            HectonContractValidator.RequirePositive(MetalSafeComputeThreadsPerGroup, nameof(MetalSafeComputeThreadsPerGroup));
            HectonContractValidator.RequirePositive(SteamDeckMicroSdReadBudgetBytesPerFrameLow, nameof(SteamDeckMicroSdReadBudgetBytesPerFrameLow));
            HectonContractValidator.RequirePositive(ContractBlackBoxFrameCapacity, nameof(ContractBlackBoxFrameCapacity));
            HectonContractValidator.RequirePowerOfTwo(CacheLineBytes, nameof(CacheLineBytes));
            HectonContractValidator.RequirePowerOfTwo(NativePageAlignmentBytes, nameof(NativePageAlignmentBytes));
            HectonContractValidator.RequireLessOrEqual(QuestSafeComputeThreadsPerGroup, UniversalMaxComputeThreadsPerGroup, nameof(QuestSafeComputeThreadsPerGroup));
            HectonContractValidator.RequireLessOrEqual(AndroidSafeComputeThreadsPerGroup, UniversalMaxComputeThreadsPerGroup, nameof(AndroidSafeComputeThreadsPerGroup));
            HectonContractValidator.RequireLessOrEqual(MetalSafeComputeThreadsPerGroup, UniversalMaxComputeThreadsPerGroup, nameof(MetalSafeComputeThreadsPerGroup));
        }
    }
}
