using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Burst-safe water ingress, transfer, and mass-shift math for submarine fluid simulation.
    /// </summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public sealed class FluidMathCore : IFluidSim
    {
        public const float WaterDensityKgPerCubicMeter = HectonPhysicsContract.WaterDensityKgPerCubicMeterConst;
        private const float DefaultGravityMetersPerSecondSquared = HectonPhysicsContract.GravityMetersPerSecondSquaredConst;

        /// <inheritdoc />
        public bool IsReady => true;

        /// <inheritdoc />
        public float WaterDensityKilogramsPerCubicMeter => WaterDensityKgPerCubicMeter;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproximateSqrtPositive(float value)
        {
            float safeValue = SanitizeNonNegative(value);
            if (safeValue <= 0f)
                return 0f;

            float magnitude = safeValue * math.rsqrt(math.max(safeValue, HectonPhysicsContract.FluidSqrtEpsilon));
            return math.isfinite(magnitude) ? magnitude : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ApproximateMagnitude(float3 value)
        {
            float3 absValue = math.abs(value);
            float maxAxis = math.cmax(absValue);
            float minAxis = math.cmin(absValue);
            float midAxis = absValue.x + absValue.y + absValue.z - maxAxis - minAxis;
            return maxAxis + (midAxis * HectonPhysicsContract.FluidMagnitudeMidAxisWeight) + (minAxis * HectonPhysicsContract.FluidMagnitudeMinAxisWeight);
        }

        /// <inheritdoc />
        public float ResolveIngressVelocity(float depthMeters)
        {
            return ResolveTorricelliIngressVelocity(depthMeters, DefaultGravityMetersPerSecondSquared);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveTorricelliIngressVelocity(float depthMeters, float gravityMetersPerSecondSquared)
        {
            float safeDepth = SanitizeNonNegative(depthMeters);
            float safeGravity = SanitizeNonNegative(gravityMetersPerSecondSquared);
            float velocity = ApproximateSqrtPositive(2f * safeGravity * safeDepth);
            return math.isfinite(velocity) ? velocity : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveIngressVolume(
            float currentVolume,
            float maxVolume,
            float breachAreaSquareMeters,
            float depthMeters,
            float fixedDeltaTime,
            float dischargeCoefficient,
            float maximumIngressPerSecondNormalized,
            float gravityMetersPerSecondSquared,
            float epsilon)
        {
            float safeCurrentVolume = math.isfinite(currentVolume) ? math.max(0f, currentVolume) : 0f;
            float safeMaxVolume = math.isfinite(maxVolume) ? math.max(0f, maxVolume) : 0f;
            float remainingCapacity = safeMaxVolume - safeCurrentVolume;
            float safeBreachArea = SanitizeNonNegative(breachAreaSquareMeters);
            if (safeBreachArea <= epsilon || remainingCapacity <= epsilon)
                return math.min(safeCurrentVolume, safeMaxVolume);

            float ingressVelocity = ResolveTorricelliIngressVelocity(depthMeters, gravityMetersPerSecondSquared);
            float cd = math.isfinite(dischargeCoefficient)
                ? math.clamp(dischargeCoefficient, HectonPhysicsContract.FluidDischargeCoefficientMin, 1f)
                : HectonPhysicsContract.FluidDischargeCoefficientMin;
            float safeDeltaTime = SanitizeNonNegative(fixedDeltaTime);
            float deltaVolume = ingressVelocity * safeBreachArea * cd * safeDeltaTime;
            if (!math.isfinite(deltaVolume))
                deltaVolume = 0f;

            float safeMaxIngressNormalized = SanitizeNonNegative(
                maximumIngressPerSecondNormalized,
                HectonPhysicsContract.FluidMaximumIngressScaleMin);
            float maxIngressScale = math.max(HectonPhysicsContract.FluidMaximumIngressScaleMin, safeMaxIngressNormalized) * safeDeltaTime;
            float maxIngressThisStep = safeMaxVolume * maxIngressScale;
            deltaVolume = math.clamp(deltaVolume, 0f, math.min(remainingCapacity, maxIngressThisStep));
            float resolvedVolume = safeCurrentVolume + deltaVolume;
            return math.isfinite(resolvedVolume) ? math.min(resolvedVolume, safeMaxVolume) : safeCurrentVolume;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveBulkheadTransferDelta(
            float sourceVolume,
            float destinationVolume,
            float sourceMaxVolume,
            float destinationMaxVolume,
            float doorAreaSquareMeters,
            float fixedDeltaTime,
            float bulkheadFlowCoefficient,
            float maxTransferPerTick,
            float dischargeCoefficient,
            float nearZeroHeadDampingMeters,
            float gravityMetersPerSecondSquared,
            float epsilon)
        {
            if (!TryResolveSafeNormalizedRatio(sourceVolume, sourceMaxVolume, epsilon, out float fillA) ||
                !TryResolveSafeNormalizedRatio(destinationVolume, destinationMaxVolume, epsilon, out float fillB))
            {
                return 0f;
            }

            float transferCoefficient = SanitizeNonNegative(bulkheadFlowCoefficient);
            float perTickTransferCap = math.max(0.01f, SanitizeNonNegative(maxTransferPerTick, 0.01f));
            float safeDoorArea = math.max(epsilon, SanitizeNonNegative(doorAreaSquareMeters, epsilon));
            float characteristicHeightA = math.max(HectonPhysicsContract.FluidCharacteristicHeightMinMeters, SafeCubeRoot(sourceMaxVolume));
            float characteristicHeightB = math.max(HectonPhysicsContract.FluidCharacteristicHeightMinMeters, SafeCubeRoot(destinationMaxVolume));
            float headDifferenceMeters = (fillA * characteristicHeightA) - (fillB * characteristicHeightB);
            float absHeadDifferenceMeters = math.abs(headDifferenceMeters);
            float dampingHeadMeters = math.max(epsilon, SanitizeNonNegative(nearZeroHeadDampingMeters, epsilon));
            float dampingFactor = math.smoothstep(0f, dampingHeadMeters, absHeadDifferenceMeters);
            if (dampingFactor <= epsilon)
                return 0f;

            float velocityMetersPerSecond = ApproximateSqrtPositive(2f * SanitizeNonNegative(gravityMetersPerSecondSquared) * absHeadDifferenceMeters);
            float safeDischargeCoefficient = SanitizeNonNegative(dischargeCoefficient);
            float safeDeltaTime = SanitizeNonNegative(fixedDeltaTime);
            float signedDeltaVolume =
                math.sign(headDifferenceMeters) *
                safeDoorArea *
                safeDischargeCoefficient *
                velocityMetersPerSecond *
                transferCoefficient *
                safeDeltaTime *
                dampingFactor;
            float deltaVolume = math.clamp(signedDeltaVolume, -perTickTransferCap, perTickTransferCap);

            if (deltaVolume > 0f)
            {
                deltaVolume = math.min(deltaVolume, math.min(sourceVolume, destinationMaxVolume - destinationVolume));
            }
            else if (deltaVolume < 0f)
            {
                float transferMagnitude = math.min(-deltaVolume, math.min(destinationVolume, sourceMaxVolume - sourceVolume));
                deltaVolume = -transferMagnitude;
            }

            return math.abs(deltaVolume) <= epsilon || !math.isfinite(deltaVolume) ? 0f : deltaVolume;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ResolveCenterOfMassStep(
            float3 currentCenter,
            float3 targetCenter,
            float blendAlpha,
            float maxCenterDeltaMeters,
            float epsilon,
            out byte isValid)
        {
            isValid = 1;
            float3 blendedCenter = LerpMad(currentCenter, targetCenter, math.saturate(blendAlpha));
            if (!math.all(math.isfinite(blendedCenter)))
            {
                isValid = 0;
                return currentCenter;
            }

            float3 delta = blendedCenter - currentCenter;
            float maxCenterDelta = math.max(epsilon, SanitizeNonNegative(maxCenterDeltaMeters, epsilon));
            float deltaMagnitude = ApproximateMagnitude(delta);
            if (deltaMagnitude > maxCenterDelta)
            {
                if (!TryResolveSafeQuotient(maxCenterDelta, deltaMagnitude, epsilon, out float centerClampScale))
                {
                    isValid = 0;
                    return currentCenter;
                }

                blendedCenter = currentCenter + (delta * centerClampScale);
            }

            return blendedCenter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryResolveSafeNormalizedRatio(float numerator, float denominator, float epsilon, out float ratio)
        {
            ratio = 0f;
            if (math.isnan(numerator) || math.isnan(denominator) ||
                !math.isfinite(numerator) || !math.isfinite(denominator) ||
                denominator <= epsilon)
            {
                return false;
            }

            float candidate = numerator * math.rcp(denominator);
            if (math.isnan(candidate) || !math.isfinite(candidate))
                return false;

            ratio = math.saturate(candidate);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SafeCubeRoot(float value)
        {
            float safeValue = SanitizeNonNegative(value);
            if (safeValue <= 0f)
                return 0f;

            float estimate = math.asfloat((math.asint(safeValue) / 3) + HectonPhysicsContract.CubeRootMagicBias);
            float estimateSq = math.max(estimate * estimate, HectonPhysicsContract.FluidSqrtEpsilon);
            estimate = ((estimate + estimate) + safeValue * math.rcp(estimateSq)) * HectonPhysicsContract.CubeRootNewtonOneThird;
            return math.isfinite(estimate) ? estimate : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 LerpMad(float3 from, float3 to, float t)
        {
            return from + (to - from) * t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveSafeQuotient(float numerator, float denominator, float epsilon, out float quotient)
        {
            quotient = 0f;
            if (math.isnan(numerator) || math.isnan(denominator) ||
                !math.isfinite(numerator) || !math.isfinite(denominator) ||
                math.abs(denominator) <= epsilon)
            {
                return false;
            }

            float candidate = numerator * math.rcp(denominator);
            if (math.isnan(candidate) || !math.isfinite(candidate))
                return false;

            quotient = candidate;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeNonNegative(float value, float fallback = 0f)
        {
            if (!math.isfinite(value))
                return math.isfinite(fallback) ? math.max(0f, fallback) : 0f;

            return math.max(0f, value);
        }
    }
}
