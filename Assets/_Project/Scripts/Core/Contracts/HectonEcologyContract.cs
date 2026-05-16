using System.Runtime.CompilerServices;

namespace Hecton8.Core.Contracts
{
    public static class HectonEcologyContract
    {
        public const int PopulationTelemetryCapacity = 300;
        public const int PopulationCounterCapacity = 16;
        public const int PopulationCoefficientCapacity = 1;
        public const int DefaultMaxEntities = 8192;
        public const int DefaultMaxSectors = 256;
        public const int EntityDeathSignalLaneCapacity = 64;
        public const int DefaultCullEventCapacity = EntityDeathSignalLaneCapacity;
        public const int DefaultFreeRingCapacity = 1024;
        public const int MaxCoefficientJsonBytes = 16 * 1024;
        public const int CoefficientFileReadBufferBytes = 2048;
        public const float ColdTickDeltaSeconds = 1f;
        public const float DefaultBiomassPerEntity = 128f;
        public const int DefaultMaxActivePreyPerSector = 64;
        public const float StressCullThreshold01 = 0.8f;
        public const float DefaultStressCullFraction01 = 0.25f;
        public const float LotkaBirthRate = 0.03f;
        public const float LotkaDeathRate = 0.018f;
        public const float LotkaDeltaTimeSeconds = HectonPhysicsContract.FixedDeltaTimeSeconds;
        public const float LotkaFeedRate = 0.000006f;
        public const float LotkaPredatorConversion = 0.35f;
        public const float LotkaPreyCarryingCapacity = 10000f;
        public const float LotkaStablePredatorBiomass = 714.2857f;
        public const float LotkaStablePreyBiomass = 8571.4287f;
        public const float LotkaObservedPredatorMax = 714.2857f;
        public const float LotkaObservedPreyMax = 9020.165f;
        public const int LotkaIntegrationSteps = 1000000;
        public const float WorldPreyBirthRatePerSecond = 0.012f;
        public const float WorldPredationRatePerSecond = 0.00045f;
        public const float WorldPredatorGrowthRatePerSecond = 0.00014f;
        public const float WorldPredatorDeathRatePerSecond = 0.006f;
        public const float WorldReproductionFoodThreshold01 = 0.62f;
        public const float BiomassMacroCellSizeMeters = 50f;
        public const float ApexSpawnGateCacheCellSizeMeters = 10f;
        public const float MigrationCellSizeMeters = 100f;
        public const float SpawnReactivateDistanceLowMeters = 32f;
        public const float SpawnReactivateDistanceMiddleMeters = 64f;
        public const float SpawnReactivateDistanceHighMeters = 96f;
        public const float SpawnReactivateDistanceUltraMeters = 128f;

        private static readonly float s_LotkaBirthRate = LotkaBirthRate;
        private static readonly float s_LotkaFeedRate = LotkaFeedRate;
        private static readonly float s_LotkaPreyCarryingCapacity = LotkaPreyCarryingCapacity;

        static HectonEcologyContract()
        {
            HectonContractValidator.RequirePositive(DefaultMaxEntities, nameof(DefaultMaxEntities));
            HectonContractValidator.RequirePositive(DefaultMaxSectors, nameof(DefaultMaxSectors));
            HectonContractValidator.RequirePositive(s_LotkaBirthRate, nameof(LotkaBirthRateRef));
            HectonContractValidator.RequirePositive(LotkaDeathRate, nameof(LotkaDeathRate));
            HectonContractValidator.RequirePositive(s_LotkaFeedRate, nameof(LotkaFeedRateRef));
            HectonContractValidator.RequirePositive(s_LotkaPreyCarryingCapacity, nameof(LotkaPreyCarryingCapacityRef));
            HectonContractValidator.RequirePositive(WorldPreyBirthRatePerSecond, nameof(WorldPreyBirthRatePerSecond));
            HectonContractValidator.RequirePositive(WorldPredationRatePerSecond, nameof(WorldPredationRatePerSecond));
            HectonContractValidator.RequirePositive(WorldPredatorGrowthRatePerSecond, nameof(WorldPredatorGrowthRatePerSecond));
            HectonContractValidator.RequirePositive(WorldPredatorDeathRatePerSecond, nameof(WorldPredatorDeathRatePerSecond));
            HectonContractValidator.RequireUnit(WorldReproductionFoodThreshold01, nameof(WorldReproductionFoodThreshold01));
            HectonContractValidator.RequireUnit(StressCullThreshold01, nameof(StressCullThreshold01));
            HectonContractValidator.RequireUnit(DefaultStressCullFraction01, nameof(DefaultStressCullFraction01));
        }

        public static ref readonly float LotkaBirthRateRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_LotkaBirthRate; }
        }

        public static ref readonly float LotkaFeedRateRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_LotkaFeedRate; }
        }

        public static ref readonly float LotkaPreyCarryingCapacityRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_LotkaPreyCarryingCapacity; }
        }
    }
}
