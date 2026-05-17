using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
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
            lo = Mix(lo, HectonSignalLaneContract.SignalLaneRegistryHash);
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
}
