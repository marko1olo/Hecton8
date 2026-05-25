using System.Runtime.CompilerServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Physics;
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
            return KinematicCcdContractMath.ShouldSchedule(velocity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveHitFraction(float hitDistance, float sweepDistance, float skinWidth)
        {
            return KinematicCcdContractMath.ResolveHitFraction(hitDistance, sweepDistance, skinWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveRollbackDistance(float hitDistance, float sweepDistance, float skinWidth)
        {
            return KinematicCcdContractMath.ResolveRollbackDistance(hitDistance, sweepDistance, skinWidth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 NormalizeOrFallback(float3 value, float3 fallback)
        {
            return KinematicCcdContractMath.NormalizeOrFallback(value, fallback);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ProjectOnCollisionPlane(float3 velocity, float3 unitNormal)
        {
            return KinematicCcdContractMath.ProjectOnCollisionPlane(velocity, unitNormal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float KineticEnergy(float mass, float velocityMagnitudeSq)
        {
            return KinematicCcdContractMath.KineticEnergy(mass, velocityMagnitudeSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LostKineticEnergy(float mass, float beforeVelocitySq, float afterVelocitySq)
        {
            return KinematicCcdContractMath.LostKineticEnergy(mass, beforeVelocitySq, afterVelocitySq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCornerNormal(float3 primaryNormal, float3 candidateNormal)
        {
            return KinematicCcdContractMath.IsCornerNormal(primaryNormal, candidateNormal);
        }
    }
}
