using System.Runtime.InteropServices;

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

namespace Hecton8.Core.Contracts.Physiology
{
    /// <summary>
    /// Shared radiation dose state contract. Owner is SHINOBU_274; mutation consumers read this through DataVault only.
    /// </summary>
    public static class ShinobuRadiationVaultContract
    {
        public const int RadiationStatesBufferId = 72740;
        public const int RadiationStateSizeBytes = 32;

        public const uint FlagIrradiated = 1u << 0;
        public const uint FlagMutated = 1u << 1;
        public const uint FlagCritical = 1u << 2;
        public const uint FlagShielded = 1u << 3;
        public const uint FlagSdfShielded = 1u << 4;
        public const uint FlagBulkheadShielded = 1u << 5;
        public const uint FlagNonFinite = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = ShinobuRadiationVaultContract.RadiationStateSizeBytes)]
    public struct RadiationStateDTO
    {
        [FieldOffset(0)] public float CumulativeDoseRad;
        [FieldOffset(4)] public float CurrentExposureRate;
        [FieldOffset(8)] public float ShieldingFactor01;
        [FieldOffset(12)] public float CellularDegradation01;
        [FieldOffset(16)] public uint EntityHashID;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public byte _pad0;
        [FieldOffset(25)] public byte _pad1;
        [FieldOffset(26)] public byte _pad2;
        [FieldOffset(27)] public byte _pad3;
        [FieldOffset(28)] public byte _pad4;
        [FieldOffset(29)] public byte _pad5;
        [FieldOffset(30)] public byte _pad6;
        [FieldOffset(31)] public byte _pad7;
    }
}
