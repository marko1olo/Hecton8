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

            float relativeSpeed = math.sqrt(relativeSpeedSq);
            float kineticEnergy = 0.5f * safeMass * relativeSpeedSq;
            if (!(kineticEnergy > hullYieldThresholdJoules))
            {
                result.RelativeSpeedMetersPerSecond = relativeSpeed;
                result.KineticEnergyJoules = kineticEnergy;
                return result;
            }

            float closingSpeed = math.max(0f, -math.dot(relativeVelocity, safeNormal));
            float safeContactCount = math.max(1, contactCount);
            float maxImpulse = (safeMass * closingSpeed) / safeContactCount;
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

        internal static float ResolveCriticalDamping(
            float springStiffness,
            float reducedMassKilograms,
            float overDampingMultiplier)
        {
            float criticalDamping = 2f * math.sqrt(math.max(0f, springStiffness) * math.max(0f, reducedMassKilograms));
            return criticalDamping * math.max(1f, overDampingMultiplier);
        }

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

        private static float3 SafeNormal(float3 value, float3 fallback)
        {
            float magnitudeSq = math.lengthsq(value);
            if (magnitudeSq <= 0.000001f || !math.all(math.isfinite(value)))
                return fallback;

            return value * math.rsqrt(magnitudeSq);
        }
    }
}
