using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Optional rigidbody-side metadata provider for procedural impact material synthesis.
    /// Kept in contracts so AI/audio/VFX consumers can query metadata without referencing physics runtime types.
    /// </summary>
    public interface IImpactMaterialProvider
    {
        byte ImpactAudioMaterialId { get; }
    }

    /// <summary>
    /// Optional rigidbody-side metadata provider for procedural impact material synthesis.
    /// </summary>
    public interface IPhysicsImpactMaterialProvider : IImpactMaterialProvider
    {
    }

    /// <summary>
    /// Runtime-owned collider LOD participant controlled by the global physics hysteresis gate.
    /// </summary>
    public interface IPhysicsColliderLodHysteresisSink
    {
        /// <summary>
        /// Enables or disables simplified collider LOD based on distance hysteresis.
        /// </summary>
        /// <param name="allowSimplifiedColliderLod">True after the body stays outside the LOD0 radius long enough.</param>
        void SetColliderLodDistanceGate(bool allowSimplifiedColliderLod);
    }

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

namespace Hecton8.Core.Contracts.Physics
{
    /// <summary>
    /// Primitive-only deterministic math helpers for gameplay owners that must not reference the physics runtime assembly.
    /// </summary>
    public static class DeterministicContractMath
    {
        public const uint FnvOffsetBasis = 2166136261u;
        public const uint FnvPrime = 16777619u;
        private const float MillimeterScale = HectonPhysicsContract.DeterministicMillimeterScale;
        private const float InvMillimeterScale = HectonPhysicsContract.DeterministicInvMillimeterScale;
        private const float MaxQuantizedMillimeterFloat = HectonPhysicsContract.DeterministicMaxQuantizedMillimeterFloat;
        private const float MinQuantizedMillimeterFloat = HectonPhysicsContract.DeterministicMinQuantizedMillimeterFloat;
        private const int MaxQuantizedMillimeter = HectonPhysicsContract.DeterministicMaxQuantizedMillimeter;
        private const int MinQuantizedMillimeter = HectonPhysicsContract.DeterministicMinQuantizedMillimeter;
        private const float Pi = HectonPhysicsContract.DeterministicPi;
        private const float TwoPi = HectonPhysicsContract.DeterministicTwoPi;
        private const float InvTwoPi = HectonPhysicsContract.DeterministicInvTwoPi;
        private const float MaxWrapInput = HectonPhysicsContract.DeterministicMaxWrapInput;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SnapMillimeter(float value)
        {
            if (!(value <= float.MaxValue && value >= -float.MaxValue))
                return 0f;

            return QuantizeMillimeter(value) * InvMillimeterScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int QuantizeMillimeter(float value)
        {
            if (!(value <= float.MaxValue && value >= -float.MaxValue))
                return 0;

            float scaled = value * MillimeterScale;
            if (scaled >= MaxQuantizedMillimeterFloat)
                return MaxQuantizedMillimeter;
            if (scaled <= MinQuantizedMillimeterFloat)
                return MinQuantizedMillimeter;

            return scaled >= 0f ? (int)(scaled + 0.5f) : (int)(scaled - 0.5f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1a(uint hash, uint value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1a(uint hash, int value)
        {
            return Fnv1a(hash, unchecked((uint)value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1a(uint hash, long value)
        {
            ulong bits = unchecked((ulong)value);
            hash = Fnv1a(hash, (uint)bits);
            return Fnv1a(hash, (uint)(bits >> 32));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Fnv1aQuantizedMillimeter(uint hash, float value)
        {
            return Fnv1a(hash, QuantizeMillimeter(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinApprox(float radians)
        {
            float x = WrapSignedPi(radians);
            float sign = 1f;
            if (x < 0f)
            {
                x = -x;
                sign = -1f;
            }

            if (x > Pi)
            {
                x = TwoPi - x;
                sign = -sign;
            }

            float y = x * (Pi - x);
            float denominator = (5f * Pi * Pi) - (4f * y);
            return sign * ((16f * y) / (denominator > 0.000001f ? denominator : 0.000001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float WrapSignedPi(float radians)
        {
            if (!(radians <= MaxWrapInput && radians >= -MaxWrapInput))
                return 0f;

            return WrapPi(radians);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float WrapPi(float radians)
        {
            int turns = (int)(radians * InvTwoPi);
            float x = radians - (turns * TwoPi);
            if (x > Pi)
                x -= TwoPi;
            else if (x < -Pi)
                x += TwoPi;

            return x;
        }
    }

    /// <summary>
    /// Burst-safe contract math for kinematic CCD producers outside the physics assembly.
    /// </summary>
    public static class KinematicCcdContractMath
    {
        public const float SpeedGateMetersPerSecondSq = HectonPhysicsContract.KinematicCcdSpeedGateMetersPerSecondSq;
        public const float RollbackFractionBias = HectonPhysicsContract.KinematicCcdRollbackFractionBias;
        public const float MinVectorMagnitudeSq = HectonPhysicsContract.KinematicCcdMinVectorMagnitudeSq;
        public const float CornerNormalDotThreshold = HectonPhysicsContract.KinematicCcdCornerNormalDotThreshold;
        public const float MassiveLostKineticEnergyJoules = HectonPhysicsContract.MassiveLostKineticEnergyJoules;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSchedule(float3 velocity)
        {
            return math.all(math.isfinite(velocity)) &&
                   math.lengthsq(velocity) >= SpeedGateMetersPerSecondSq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveHitFraction(float hitDistance, float sweepDistance, float skinWidth)
        {
            float denominator = math.max(HectonPhysicsContract.FluidDistanceEpsilon, sweepDistance + math.max(0f, skinWidth));
            return math.saturate(hitDistance * math.rcp(denominator));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRollbackDistance(float hitDistance, float sweepDistance, float skinWidth)
        {
            float hitFraction = ResolveHitFraction(hitDistance, sweepDistance, skinWidth);
            return math.max(0f, sweepDistance * math.max(0f, hitFraction - RollbackFractionBias));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            if (!math.all(math.isfinite(value)))
                return fallback;

            float lengthSq = math.lengthsq(value);
            if (lengthSq <= MinVectorMagnitudeSq)
                return fallback;

            return value * math.rsqrt(math.max(lengthSq, MinVectorMagnitudeSq));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ProjectOnCollisionPlane(float3 velocity, float3 unitNormal)
        {
            return velocity - (math.dot(velocity, unitNormal) * unitNormal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float KineticEnergy(float mass, float velocityMagnitudeSq)
        {
            return 0.5f * math.max(HectonPhysicsContract.DeterministicInvMillimeterScale, mass) * math.max(0f, velocityMagnitudeSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LostKineticEnergy(float mass, float beforeVelocitySq, float afterVelocitySq)
        {
            return math.max(0f, KineticEnergy(mass, beforeVelocitySq) - KineticEnergy(mass, afterVelocitySq));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCornerNormal(float3 primaryNormal, float3 candidateNormal)
        {
            float3 primary = NormalizeOrFallback(primaryNormal, new float3(0f, 1f, 0f));
            float3 candidate = NormalizeOrFallback(candidateNormal, primary);
            return math.dot(primary, candidate) <= CornerNormalDotThreshold;
        }
    }
}
