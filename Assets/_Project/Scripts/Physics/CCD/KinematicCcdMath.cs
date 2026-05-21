using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.Physics.CCD
{
    /// <summary>
    /// Burst-safe math helpers for kinematic CCD rollback and collision-plane deflection.
    /// </summary>
    public static class KinematicCcdMath
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
