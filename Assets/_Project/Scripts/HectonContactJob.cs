using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Burst-safe contact and multi-body solver math kernels.
    /// </summary>
    internal static class HectonContactJob
    {
        internal struct InelasticImpactResult
        {
            public bool ExceedsYield;
            public float RelativeSpeedMetersPerSecond;
            public float NormalClosingSpeedMetersPerSecond;
            public float KineticEnergyJoules;
            public float Severity01;
            public float MaxImpulsePerContact;
            public byte IntegrityDelta;
            public float3 TangentialVelocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static InelasticImpactResult ResolveInelasticImpact(
            float dominantMassKilograms,
            float3 dominantVelocity,
            float3 otherVelocity,
            float3 contactNormal,
            int contactCount,
            float hullYieldThresholdJoules)
        {
            InelasticImpactResult result = default;

            float safeMass = math.max(0.0001f, dominantMassKilograms);
            float3 safeNormal = SafeNormal(contactNormal, new float3(0f, 1f, 0f));
            float3 relativeVelocity = dominantVelocity - otherVelocity;
            float relativeSpeedSq = math.lengthsq(relativeVelocity);
            if (relativeSpeedSq <= 0.000001f)
                return result;

            float relativeSpeed = ResolveApproxMagnitude(relativeSpeedSq);
            float kineticEnergy = 0.5f * safeMass * relativeSpeedSq;
            if (!(kineticEnergy > hullYieldThresholdJoules))
            {
                result.RelativeSpeedMetersPerSecond = relativeSpeed;
                result.KineticEnergyJoules = kineticEnergy;
                return result;
            }

            float closingSpeed = math.max(0f, -math.dot(relativeVelocity, safeNormal));
            float safeContactCount = math.max(1, contactCount);
            float maxImpulse = (safeMass * closingSpeed) * math.rcp(safeContactCount);
            float severity01 = math.saturate((kineticEnergy - hullYieldThresholdJoules) / math.max(hullYieldThresholdJoules, 1f));
            byte integrityDelta = (byte)math.clamp((int)math.round(math.lerp(24f, 255f, severity01)), 1, 255);

            result.ExceedsYield = true;
            result.RelativeSpeedMetersPerSecond = relativeSpeed;
            result.NormalClosingSpeedMetersPerSecond = closingSpeed;
            result.KineticEnergyJoules = kineticEnergy;
            result.Severity01 = severity01;
            result.MaxImpulsePerContact = math.max(0f, maxImpulse);
            result.IntegrityDelta = integrityDelta;
            result.TangentialVelocity = relativeVelocity - (safeNormal * math.dot(relativeVelocity, safeNormal));
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveReducedMass(
            float anchorMassKilograms,
            float payloadMassKilograms,
            bool payloadActsAsWorldAnchor)
        {
            float safeAnchorMass = math.max(anchorMassKilograms, 0.0001f);
            if (payloadActsAsWorldAnchor || !math.isfinite(payloadMassKilograms))
                return safeAnchorMass;

            float safePayloadMass = math.max(payloadMassKilograms, 0.0001f);
            return (safeAnchorMass * safePayloadMass) / math.max(safeAnchorMass + safePayloadMass, 0.0001f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveCriticalDamping(
            float springStiffness,
            float reducedMassKilograms,
            float overDampingMultiplier)
        {
            float criticalDamping = 2f * ResolveApproxMagnitude(math.max(0f, springStiffness) * math.max(0f, reducedMassKilograms));
            return criticalDamping * math.max(1f, overDampingMultiplier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveProjectedPdAcceleration(
            float3 anchorPosition,
            float3 payloadPosition,
            float3 anchorVelocity,
            float3 payloadVelocity,
            float3 projectionAxis,
            float springStiffness,
            float dampingCoefficient)
        {
            float3 axis = SafeNormal(projectionAxis, new float3(0f, 0f, 1f));
            float positionError = math.dot(anchorPosition - payloadPosition, axis);
            float velocityError = math.dot(anchorVelocity - payloadVelocity, axis);
            float requestedAcceleration = (positionError * math.max(0f, springStiffness)) + (velocityError * math.max(0f, dampingCoefficient));
            return math.max(0f, requestedAcceleration);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ResolveTractorBeamPdForce(
            float3 targetPosition,
            float3 currentPosition,
            float3 targetVelocity,
            float3 currentVelocity,
            float springStiffness,
            float dampingCoefficient,
            float maxForceMagnitude)
        {
            if (!math.all(math.isfinite(targetPosition)) ||
                !math.all(math.isfinite(currentPosition)) ||
                !math.all(math.isfinite(targetVelocity)) ||
                !math.all(math.isfinite(currentVelocity)))
            {
                return float3.zero;
            }

            float3 positionError = targetPosition - currentPosition;
            float3 relativeVelocity = currentVelocity - targetVelocity;
            float3 force = (positionError * math.max(0f, springStiffness)) -
                           (relativeVelocity * math.max(0f, dampingCoefficient));
            if (!math.all(math.isfinite(force)))
                return float3.zero;

            float forceMagnitudeSq = math.lengthsq(force);
            float safeMaxForce = math.max(0f, maxForceMagnitude);
            if (safeMaxForce > 0f && forceMagnitudeSq > safeMaxForce * safeMaxForce)
                force *= safeMaxForce * math.rsqrt(forceMagnitudeSq);

            return force;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ResolveTractorBeamPdAcceleration(
            float3 targetPosition,
            float3 currentPosition,
            float3 targetVelocity,
            float3 currentVelocity,
            float springStiffness,
            float dampingCoefficient,
            float reducedMassKilograms,
            float maxAccelerationMagnitude)
        {
            float safeReducedMass = math.max(0.0001f, reducedMassKilograms);
            float maxForce = math.max(0f, maxAccelerationMagnitude) * safeReducedMass;
            float3 force = ResolveTractorBeamPdForce(
                targetPosition,
                currentPosition,
                targetVelocity,
                currentVelocity,
                springStiffness,
                dampingCoefficient,
                maxForce);
            return force / safeReducedMass;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ResolveTractorBeamPdVelocityChange(
            float3 targetPosition,
            float3 currentPosition,
            float3 targetVelocity,
            float3 currentVelocity,
            float springStiffness,
            float dampingCoefficient,
            float reducedMassKilograms,
            float maxVelocityChangeMagnitude)
        {
            float safeReducedMass = math.max(0.0001f, reducedMassKilograms);
            float maxForce = math.max(0f, maxVelocityChangeMagnitude) * safeReducedMass;
            float3 force = ResolveTractorBeamPdForce(
                targetPosition,
                currentPosition,
                targetVelocity,
                currentVelocity,
                springStiffness,
                dampingCoefficient,
                maxForce);
            return force / safeReducedMass;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float ResolveExponentialBlendAlpha(float deltaTime, float sharpness)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            float safeSharpness = math.max(0f, sharpness);
            float x = safeDeltaTime * safeSharpness;
            return math.saturate(x / (1f + x));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ResolveSteeringArc(
            float3 currentSteer,
            float3 desiredSteer,
            float deltaTime,
            float turnRate)
        {
            float3 up = new float3(0f, 1f, 0f);
            float3 currentDirection = SafeNormal(currentSteer, new float3(0f, 0f, 1f));
            float3 desiredDirection = SafeNormal(desiredSteer, currentDirection);
            float alpha = ResolveExponentialBlendAlpha(deltaTime, turnRate);
            quaternion currentRotation = quaternion.LookRotationSafe(currentDirection, up);
            quaternion desiredRotation = quaternion.LookRotationSafe(desiredDirection, up);
            quaternion smoothedRotation = math.slerp(currentRotation, desiredRotation, alpha);
            return SafeNormal(math.mul(smoothedRotation, new float3(0f, 0f, 1f)), desiredDirection);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static float3 ProjectVelocityAlongSurface(float3 velocity, float3 surfaceNormal)
        {
            float3 safeVelocity = math.select(float3.zero, velocity, math.all(math.isfinite(velocity)));
            float normalMagnitudeSq = math.lengthsq(surfaceNormal);
            if (normalMagnitudeSq < 0.1f || !math.all(math.isfinite(surfaceNormal)))
                return float3.zero;

            float3 safeNormal = surfaceNormal * math.rsqrt(normalMagnitudeSq);
            return safeVelocity - (safeNormal * math.dot(safeVelocity, safeNormal));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 SafeNormal(float3 value, float3 fallback)
        {
            float magnitudeSq = math.lengthsq(value);
            if (magnitudeSq <= 0.000001f || !math.all(math.isfinite(value)))
                return fallback;

            return value * math.rsqrt(magnitudeSq);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveApproxMagnitude(float magnitudeSq)
        {
            if (magnitudeSq <= 0f || !math.isfinite(magnitudeSq))
                return 0f;

            return magnitudeSq * math.rsqrt(magnitudeSq);
        }
    }
}
