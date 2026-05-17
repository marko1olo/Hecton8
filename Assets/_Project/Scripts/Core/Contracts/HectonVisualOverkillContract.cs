namespace Hecton8.Core.Contracts
{
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
