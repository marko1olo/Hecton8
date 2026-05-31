namespace Hecton8.Core.Contracts
{
    public enum HectonScalabilityTier : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    public static class ScalabilityContract
    {
        public const int MaxBoidsCount_Low = 256;
        public const int MaxBoidsCount_Middle = 1000;
        public const int MaxBoidsCount_High = 2000;
        public const int MaxBoidsCount_Ultra = 5000;
        public const int HomeostasisFrameTimeWindow = 120;
        public const int HomeostasisBlackBoxCapacity = 300;
        public const int HomeostasisTelemetryCadenceFrames = 60;
        public const int HomeostasisRecoveryArmFrames = 3000;
        public const int HomeostasisRecoveryStepFrames = 60;
        public const float HomeostasisFrostPollSeconds = 5f;
        public const float HomeostasisFpsEwmaAlpha = 0.1f;
        public const float HomeostasisShiEwmaAlpha = 0.12f;
        public const float HomeostasisJitterUnstableSigmaMs = 2.0f;
        public const float HomeostasisLevel1ActivateShi = 0.60f;
        public const float HomeostasisLevel1RestoreShi = 0.50f;
        public const float HomeostasisLevel2ActivateShi = 0.80f;
        public const float HomeostasisLevel2RestoreShi = 0.70f;
        public const float HomeostasisLevel3ActivateShi = 0.95f;
        public const float HomeostasisLevel3RestoreShi = 0.90f;
        public const float HomeostasisSequentialRecoveryShi = 0.30f;
        public const long HomeostasisPersistentNativeBudgetBytes = 131072L;
        public const float TargetFrameMilliseconds = 16.667f;
        public const float PreSimulationBudgetMilliseconds = 1.5f;
        public const float Lod0ScreenRatio01 = 0.20f;
        public const float Lod1ScreenRatio01 = 0.55f;
        public const float Lod2ScreenRatio01 = 0.25f;
        public const float LodFadeDistanceMeters = 2f;
        public const float LowTierVramPressureRatio01 = 0.70f;
        public const float MiddleTierVramPressureRatio01 = 0.78f;
        public const float HighTierVramPressureRatio01 = 0.84f;
        public const float UltraTierVramPressureRatio01 = 0.90f;

        static ScalabilityContract()
        {
            HectonContractValidator.RequirePositive(MaxBoidsCount_Low, nameof(MaxBoidsCount_Low));
            HectonContractValidator.RequirePositive(MaxBoidsCount_Ultra, nameof(MaxBoidsCount_Ultra));
            HectonContractValidator.RequirePositive(TargetFrameMilliseconds, nameof(TargetFrameMilliseconds));
            HectonContractValidator.RequirePositive(PreSimulationBudgetMilliseconds, nameof(PreSimulationBudgetMilliseconds));
            HectonContractValidator.RequireUnit(HomeostasisLevel1ActivateShi, nameof(HomeostasisLevel1ActivateShi));
            HectonContractValidator.RequireUnit(HomeostasisLevel2ActivateShi, nameof(HomeostasisLevel2ActivateShi));
            HectonContractValidator.RequireUnit(HomeostasisLevel3ActivateShi, nameof(HomeostasisLevel3ActivateShi));
            HectonContractValidator.RequireUnit(Lod0ScreenRatio01, nameof(Lod0ScreenRatio01));
            HectonContractValidator.RequireUnit(Lod1ScreenRatio01, nameof(Lod1ScreenRatio01));
            HectonContractValidator.RequireUnit(Lod2ScreenRatio01, nameof(Lod2ScreenRatio01));
        }
    }
}
