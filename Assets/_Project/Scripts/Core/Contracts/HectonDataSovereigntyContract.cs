namespace Hecton8.Core.Contracts
{
    public static class HectonDataSovereigntyContract
    {
        public const byte DataOwnerGlobalDataVault = 1;
        public const byte DataOwnerSignalBusTypedLane = 2;
        public const byte DataOwnerReadOnlySpanView = 3;
        public const byte LocalNativeArrayOwnershipForbidden = 1;

        public const uint SystemIdContracts = 0x43545243u;
        public const uint SystemIdGlobalDataVault = 0x47445654u;
        public const uint SystemIdSignalBus = 0x53474C4Eu;
        public const uint SystemIdMmfPaging = 0x4D4D4650u;
        public const uint SystemIdBlackBox = 0x42424F58u;

        public const int VaultOverrideFloatStrideBytes = 4;
        public const int VaultOverrideDoubleStrideBytes = 8;
        public const int VaultOverrideUlongStrideBytes = 8;
        public const int VaultOverrideMaxContractEntries = 512;
        public const int TypedSignalLaneMaxCount = 255;
        public const int ReadOnlySpanMinBridgeBytes = 16;
        public const int BlackBoxFrameCapacity = HectonPlatformContract.ContractBlackBoxFrameCapacity;
        public const int BlackBoxEntryBytes = HectonPlatformContract.ContractHeartbeatStrideBytes;

        static HectonDataSovereigntyContract()
        {
            HectonContractValidator.RequirePositive(VaultOverrideFloatStrideBytes, nameof(VaultOverrideFloatStrideBytes));
            HectonContractValidator.RequirePositive(VaultOverrideMaxContractEntries, nameof(VaultOverrideMaxContractEntries));
            HectonContractValidator.RequirePositive(TypedSignalLaneMaxCount, nameof(TypedSignalLaneMaxCount));
            HectonContractValidator.RequirePositive(BlackBoxFrameCapacity, nameof(BlackBoxFrameCapacity));
            HectonContractValidator.RequirePowerOfTwo(VaultOverrideFloatStrideBytes, nameof(VaultOverrideFloatStrideBytes));
            HectonContractValidator.RequirePowerOfTwo(VaultOverrideDoubleStrideBytes, nameof(VaultOverrideDoubleStrideBytes));
        }
    }
}
