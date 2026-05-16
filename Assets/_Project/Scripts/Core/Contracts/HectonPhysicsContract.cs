using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class HectonPhysicsContract
    {
        public const double AupSectorSizeMetersDouble = 5000.0d;
        public const int AupSectorSizeMetersInt = (int)AupSectorSizeMetersDouble;
        public const float AupSectorSizeMetersFloat = (float)AupSectorSizeMetersDouble;
        public const float WaterDensityKgPerCubicMeterConst = 1025f;
        public const float GravityMetersPerSecondSquaredConst = 9.81f;
        public const float HydrostaticPressureKPaPerMeter = WaterDensityKgPerCubicMeterConst * GravityMetersPerSecondSquaredConst * 0.001f;
        public const float FixedDeltaTimeSeconds = 0.020f;
        public const float SoundSpeedWaterMetersPerSecondConst = 1480f;
        public const float SoundSpeedAirMetersPerSecondConst = 343f;
        public const float KinematicCcdSpeedGateMetersPerSecondSq = 25f;
        public const float KinematicCcdRollbackFractionBias = 0.01f;
        public const float KinematicCcdMinVectorMagnitudeSq = 0.000001f;
        public const float KinematicCcdCornerNormalDotThreshold = 0.45f;
        public const float MassiveLostKineticEnergyJoules = 1500f;
        public const float FluidSqrtEpsilon = 0.000001f;
        public const float FluidDistanceEpsilon = 0.0001f;
        public const float FluidDischargeCoefficientMin = 0.05f;
        public const float FluidMaximumIngressScaleMin = 0.01f;
        public const float FluidCharacteristicHeightMinMeters = 0.1f;
        public const float FluidMagnitudeMidAxisWeight = 0.375f;
        public const float FluidMagnitudeMinAxisWeight = 0.125f;
        public const int CubeRootMagicBias = 709921077;
        public const float CubeRootNewtonOneThird = 0.33333334f;
        public const float DeterministicMillimeterScale = 1000f;
        public const float DeterministicInvMillimeterScale = 0.001f;
        public const float DeterministicMaxQuantizedMillimeterFloat = 2147483000f;
        public const float DeterministicMinQuantizedMillimeterFloat = -2147483000f;
        public const int DeterministicMaxQuantizedMillimeter = 2147483647;
        public const int DeterministicMinQuantizedMillimeter = -2147483647 - 1;
        public const float DeterministicPi = 3.14159265358979323846f;
        public const float DeterministicTwoPi = 6.28318530717958647692f;
        public const float DeterministicInvTwoPi = 0.15915494309189533577f;
        public const float DeterministicMaxWrapInput = 13493037000f;
        public const double AupMaxFloatSafeMeters = 1000000000000.0d;
        public const double AupMaxDistanceReturnMeters = 1000000000.0d;

        private static readonly double s_AupSectorSizeMeters = AupSectorSizeMetersDouble;
        private static readonly double s_OneOverAupSectorSizeMeters = math.rcp(AupSectorSizeMetersDouble);
        private static readonly float s_WaterDensityKgPerCubicMeter = WaterDensityKgPerCubicMeterConst;
        private static readonly float s_GravityMetersPerSecondSquared = GravityMetersPerSecondSquaredConst;
        private static readonly float s_HydrostaticPressureKPaPerMeter = HydrostaticPressureKPaPerMeter;
        private static readonly float s_OneOverGravityMetersPerSecondSquared = math.rcp(GravityMetersPerSecondSquaredConst);
        private static readonly float s_SoundSpeedWaterMetersPerSecond = SoundSpeedWaterMetersPerSecondConst;
        private static readonly float s_OneOverSoundSpeedWaterMetersPerSecond = math.rcp(SoundSpeedWaterMetersPerSecondConst);
        private static readonly float s_SoundSpeedAirMetersPerSecond = SoundSpeedAirMetersPerSecondConst;
        private static readonly float s_OneOverSoundSpeedAirMetersPerSecond = math.rcp(SoundSpeedAirMetersPerSecondConst);

        static HectonPhysicsContract()
        {
            HectonContractValidator.RequirePositive(s_AupSectorSizeMeters, nameof(AupSectorSizeMetersDouble));
            HectonContractValidator.RequirePositive(s_OneOverAupSectorSizeMeters, nameof(OneOverAupSectorSizeMeters));
            HectonContractValidator.RequirePositive(s_WaterDensityKgPerCubicMeter, nameof(WaterDensityKgPerCubicMeter));
            HectonContractValidator.RequirePositive(s_GravityMetersPerSecondSquared, nameof(GravityMetersPerSecondSquared));
            HectonContractValidator.RequirePositive(s_HydrostaticPressureKPaPerMeter, nameof(HydrostaticPressureKPaPerMeter));
            HectonContractValidator.RequirePositive(s_OneOverGravityMetersPerSecondSquared, nameof(OneOverGravityMetersPerSecondSquared));
            HectonContractValidator.RequirePositive(s_SoundSpeedWaterMetersPerSecond, nameof(SoundSpeedWaterMetersPerSecond));
            HectonContractValidator.RequirePositive(s_OneOverSoundSpeedWaterMetersPerSecond, nameof(OneOverSoundSpeedWaterMetersPerSecond));
            HectonContractValidator.RequirePositive(s_SoundSpeedAirMetersPerSecond, nameof(SoundSpeedAirMetersPerSecond));
            HectonContractValidator.RequirePositive(s_OneOverSoundSpeedAirMetersPerSecond, nameof(OneOverSoundSpeedAirMetersPerSecond));
        }

        public static ref readonly double AupSectorSizeMeters
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_AupSectorSizeMeters; }
        }

        public static ref readonly double OneOverAupSectorSizeMeters
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_OneOverAupSectorSizeMeters; }
        }

        public static ref readonly float WaterDensityKgPerCubicMeter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_WaterDensityKgPerCubicMeter; }
        }

        public static ref readonly float GravityMetersPerSecondSquared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_GravityMetersPerSecondSquared; }
        }

        public static ref readonly float HydrostaticPressureKilopascalsPerMeter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_HydrostaticPressureKPaPerMeter; }
        }

        public static ref readonly float OneOverGravityMetersPerSecondSquared
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_OneOverGravityMetersPerSecondSquared; }
        }

        public static ref readonly float SoundSpeedWaterMetersPerSecond
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_SoundSpeedWaterMetersPerSecond; }
        }

        public static ref readonly float OneOverSoundSpeedWaterMetersPerSecond
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_OneOverSoundSpeedWaterMetersPerSecond; }
        }

        public static ref readonly float SoundSpeedAirMetersPerSecond
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_SoundSpeedAirMetersPerSecond; }
        }

        public static ref readonly float OneOverSoundSpeedAirMetersPerSecond
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return ref s_OneOverSoundSpeedAirMetersPerSecond; }
        }
    }
}
