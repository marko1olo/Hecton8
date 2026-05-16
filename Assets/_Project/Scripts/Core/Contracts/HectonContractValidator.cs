using System;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class HectonContractValidator
    {
        public static void RequireFinite(float value, string name)
        {
            if (!math.isfinite(value))
                throw new InvalidOperationException(name + " is non-finite.");
        }

        public static void RequireFinite(double value, string name)
        {
            if (!math.isfinite(value))
                throw new InvalidOperationException(name + " is non-finite.");
        }

        public static void RequirePositive(float value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0f)
                throw new InvalidOperationException(name + " must be positive.");
        }

        public static void RequirePositive(double value, string name)
        {
            RequireFinite(value, name);
            if (value <= 0.0d)
                throw new InvalidOperationException(name + " must be positive.");
        }

        public static void RequirePositive(int value, string name)
        {
            if (value <= 0)
                throw new InvalidOperationException(name + " must be positive.");
        }

        public static void RequirePowerOfTwo(int value, string name)
        {
            RequirePositive(value, name);
            if ((value & (value - 1)) != 0)
                throw new InvalidOperationException(name + " must be a power of two.");
        }

        public static void RequireLessOrEqual(int value, int maximum, string name)
        {
            if (value > maximum)
                throw new InvalidOperationException(name + " exceeds maximum.");
        }

        public static void RequireGreaterOrEqual(int value, int minimum, string name)
        {
            if (value < minimum)
                throw new InvalidOperationException(name + " is below minimum.");
        }

        public static void RequireUnit(float value, string name)
        {
            RequireFinite(value, name);
            if (value < 0f || value > 1f)
                throw new InvalidOperationException(name + " must be within 0..1.");
        }
    }

    public static class HectonContractVersion
    {
        public static readonly ulong HashLo;
        public static readonly ulong HashHi;

        static HectonContractVersion()
        {
            ulong lo = 14695981039346656037UL;
            lo = Mix(lo, HectonPhysicsContract.AupSectorSizeMetersInt);
            lo = MixFloat(lo, HectonPhysicsContract.GravityMetersPerSecondSquaredConst);
            lo = MixFloat(lo, HectonPhysicsContract.WaterDensityKgPerCubicMeterConst);
            lo = MixFloat(lo, HectonPhysicsContract.HydrostaticPressureKPaPerMeter);
            lo = MixFloat(lo, HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst);
            lo = MixFloat(lo, HectonSurvivalContract.StandardOxygenKPa);
            lo = MixFloat(lo, HectonSurvivalContract.DefaultPlayerOxygenKPaPerSecond);
            lo = MixFloat(lo, HectonEcologyContract.LotkaBirthRate);
            lo = MixFloat(lo, HectonEcologyContract.LotkaFeedRate);
            lo = MixFloat(lo, HectonEcologyContract.WorldPreyBirthRatePerSecond);
            lo = MixFloat(lo, HectonEcologyContract.WorldPredationRatePerSecond);
            lo = MixFloat(lo, ScalabilityContract.HomeostasisLevel3ActivateShi);
            lo = Mix(lo, HectonMmfPagingContract.BTreePageSizeBytes);
            lo = Mix(lo, HectonSignalLaneContract.WfcOutpostStateChangedSignal);
            lo = Mix(lo, HectonPlatformContract.UniversalMaxComputeThreadsPerGroup);
            lo = Mix(lo, HectonPlatformContract.SteamDeckMicroSdReadBudgetBytesPerFrameLow);
            lo = Mix(lo, HectonDataSovereigntyContract.SystemIdContracts);
            lo = Mix(lo, HectonVisualOverkillContract.UltraTierPomTaps);

            ulong hi = 1099511628211UL;
            hi = Mix(hi, HectonLoreContract.IndustrialShiftBoardA);
            hi = Mix(hi, HectonLoreContract.SurvivorRouteScratch);
            hi = Mix(hi, HectonVaultOffsetContract.EditorBreadcrumbBase);
            hi = MixFloat(hi, ScalabilityContract.Lod0ScreenRatio01);
            hi = MixFloat(hi, ScalabilityContract.Lod1ScreenRatio01);
            hi = MixFloat(hi, ScalabilityContract.Lod2ScreenRatio01);
            hi = MixFloat(hi, HectonVisualOverkillContract.UltraTierSaltCrystalSpawnChance01);
            hi = Mix(hi, HectonDataSovereigntyContract.BlackBoxFrameCapacity);
            hi = Mix(hi, unchecked((uint)(lo & 0xFFFFFFFFu)));
            hi = Mix(hi, unchecked((uint)(lo >> 32)));

            HashLo = lo != 0UL ? lo : 1UL;
            HashHi = hi != 0UL ? hi : 1UL;
        }

        public static bool IsValid => HashLo != 0UL && HashHi != 0UL;

        private static ulong Mix(ulong hash, int value)
        {
            return Mix(hash, unchecked((uint)value));
        }

        private static ulong Mix(ulong hash, uint value)
        {
            unchecked
            {
                hash ^= value & 0xFFu;
                hash *= 1099511628211UL;
                hash ^= (value >> 8) & 0xFFu;
                hash *= 1099511628211UL;
                hash ^= (value >> 16) & 0xFFu;
                hash *= 1099511628211UL;
                hash ^= (value >> 24) & 0xFFu;
                hash *= 1099511628211UL;
                return hash;
            }
        }

        private static ulong MixFloat(ulong hash, float value)
        {
            int quantized = (int)math.round(value * 1000000f);
            return Mix(hash, quantized);
        }
    }

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

    public static class HectonVisualOverkillContract
    {
        public const int LowTierDearLieLutSamples = 64;
        public const int LowTierTriangleNoiseOctaves = 1;
        public const int LowTierDotProductVisionSamples = 1;
        public const int LowTierRaymarchSteps = 0;
        public const int LowTierPomTaps = 0;
        public const int LowTierSssSamples = 0;
        public const int LowTierWakeSiltParticles = 128;
        public const int LowTierVisorSaltCrystalBudget = 0;
        public const int LowTierHullDentDecalBudget = 16;

        public const int MiddleTierDearLieLutSamples = 128;
        public const int MiddleTierTriangleNoiseOctaves = 2;
        public const int MiddleTierRaymarchSteps = 8;
        public const int MiddleTierPomTaps = 4;
        public const int MiddleTierSssSamples = 2;
        public const int MiddleTierWakeSiltParticles = 512;
        public const int MiddleTierVisorSaltCrystalBudget = 128;
        public const int MiddleTierHullDentDecalBudget = 64;

        public const int HighTierRaymarchSteps = 32;
        public const int HighTierPomTaps = 12;
        public const int HighTierSssSamples = 6;
        public const int HighTierWakeSiltParticles = 4096;
        public const int HighTierVisorSaltCrystalBudget = 1024;
        public const int HighTierHullDentDecalBudget = 256;

        public const int UltraTierRaymarchSteps = 64;
        public const int UltraTierPomTaps = 16;
        public const int UltraTierSssSamples = 8;
        public const int UltraTierWakeSiltParticles = 8192;
        public const int UltraTierVisorSaltCrystalBudget = 2048;
        public const int UltraTierHullDentDecalBudget = 512;

        public const float LowTierWakeSiltStepMeters = 4.0f;
        public const float UltraTierWakeSiltStepMeters = 0.75f;
        public const float LowTierSaltCrystalSpawnChance01 = 0.0f;
        public const float UltraTierSaltCrystalSpawnChance01 = 0.85f;
        public const float LowTierHullDentNormalBlend01 = 0.25f;
        public const float UltraTierHullDentNormalBlend01 = 0.95f;

        static HectonVisualOverkillContract()
        {
            HectonContractValidator.RequirePositive(LowTierDearLieLutSamples, nameof(LowTierDearLieLutSamples));
            HectonContractValidator.RequirePositive(UltraTierRaymarchSteps, nameof(UltraTierRaymarchSteps));
            HectonContractValidator.RequirePositive(UltraTierPomTaps, nameof(UltraTierPomTaps));
            HectonContractValidator.RequirePositive(UltraTierWakeSiltParticles, nameof(UltraTierWakeSiltParticles));
            HectonContractValidator.RequireUnit(LowTierSaltCrystalSpawnChance01, nameof(LowTierSaltCrystalSpawnChance01));
            HectonContractValidator.RequireUnit(UltraTierSaltCrystalSpawnChance01, nameof(UltraTierSaltCrystalSpawnChance01));
            HectonContractValidator.RequireUnit(LowTierHullDentNormalBlend01, nameof(LowTierHullDentNormalBlend01));
            HectonContractValidator.RequireUnit(UltraTierHullDentNormalBlend01, nameof(UltraTierHullDentNormalBlend01));
            HectonContractValidator.RequireGreaterOrEqual(UltraTierPomTaps, HighTierPomTaps, nameof(UltraTierPomTaps));
            HectonContractValidator.RequireGreaterOrEqual(HighTierWakeSiltParticles, MiddleTierWakeSiltParticles, nameof(HighTierWakeSiltParticles));
        }
    }
}
