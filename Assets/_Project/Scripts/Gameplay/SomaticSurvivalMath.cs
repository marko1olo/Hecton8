// ============================================================================
// HECTON-8 - SomaticSurvivalMath.cs
// Stateless survival/health math shared by runtime systems and smoke tests.
// ============================================================================

using Unity.Mathematics;

namespace Hecton8.Gameplay
{
    internal static class SomaticSurvivalMath
    {
        private const float RadiationFatigueMinimumScale = 0.65f;
        private const float RadiationFatigueScalePerSecond = 0.005f;
        private const float ThermalShockBoilingThresholdCelsius = 90f;
        private const float ThermalShockFreezingThresholdCelsius = -2f;
        private const float ThermalShockFullSeverityRangeCelsius = 40f;
        private const float NitrogenCriticalBuildUp = 100f;
        private const float NutritionalToxicityDamageScale = 0.45f;
        private const float NutritionalToxicityRegenFloor = 0.35f;
        private const float SuitPunctureBleedDamageFraction = 0.30f;
        private const float HypothermiaFrostStartCelsius = 35f;
        private const float HypothermiaFrostFullCelsius = 28f;

        internal static float ResolveRadiationFatigueScale(float exposureSeconds)
        {
            return math.max(
                RadiationFatigueMinimumScale,
                1f - (math.max(0f, exposureSeconds) * RadiationFatigueScalePerSecond));
        }

        internal static float ResolveNaturalHealthRegenerationMultiplier(float toxicitySeverity01)
        {
            return math.lerp(1f, NutritionalToxicityRegenFloor, math.saturate(toxicitySeverity01));
        }

        internal static float ResolveHypothermiaFrostIntensity01(float internalTemperatureCelsius)
        {
            if (!math.isfinite(internalTemperatureCelsius) || internalTemperatureCelsius >= HypothermiaFrostStartCelsius)
                return 0f;

            return math.saturate(
                (HypothermiaFrostStartCelsius - internalTemperatureCelsius) /
                math.max(0.01f, HypothermiaFrostStartCelsius - HypothermiaFrostFullCelsius));
        }

        internal static float ResolveExternalThermalShockTemperature(
            float fallbackTemperatureCelsius,
            float sampledThermalTemperatureCelsius)
        {
            if (!math.isfinite(sampledThermalTemperatureCelsius))
                return fallbackTemperatureCelsius;

            if (!math.isfinite(fallbackTemperatureCelsius))
                return sampledThermalTemperatureCelsius;

            float fallbackSeverity = ResolveThermalShockSeverity01(fallbackTemperatureCelsius);
            float sampledSeverity = ResolveThermalShockSeverity01(sampledThermalTemperatureCelsius);
            return sampledSeverity > fallbackSeverity
                ? sampledThermalTemperatureCelsius
                : fallbackTemperatureCelsius;
        }

        internal static float ResolveThermalShockSeverity01(float externalTemperatureCelsius)
        {
            if (!math.isfinite(externalTemperatureCelsius))
                return 0f;

            float hotSeverity = math.saturate(
                (externalTemperatureCelsius - ThermalShockBoilingThresholdCelsius) /
                math.max(0.01f, ThermalShockFullSeverityRangeCelsius));
            float coldSeverity = math.saturate(
                (ThermalShockFreezingThresholdCelsius - externalTemperatureCelsius) /
                math.max(0.01f, ThermalShockFullSeverityRangeCelsius));
            return math.max(hotSeverity, coldSeverity);
        }

        internal static float ResolveThermalShockDamagePerSecond(
            float externalTemperatureCelsius,
            float baseTemperatureDamageRate,
            float damageMultiplier)
        {
            float severity01 = ResolveThermalShockSeverity01(externalTemperatureCelsius);
            if (severity01 <= 0f)
                return 0f;

            return math.max(0f, baseTemperatureDamageRate) *
                   math.lerp(3f, 8f, severity01) *
                   math.max(0f, damageMultiplier);
        }

        internal static float ResolveNitrogenBuildUpDelta(
            float ascentMetersPerSecond,
            float ascentOriginDepthMeters,
            float deltaTime)
        {
            _ = ascentMetersPerSecond;
            _ = ascentOriginDepthMeters;
            _ = deltaTime;
            return 0f;
        }

        internal static float ResolveNitrogenNarcosis01(float nitrogenBuildUp)
        {
            _ = nitrogenBuildUp;
            return 0f;
        }

        internal static float ResolveNitrogenTissueLoad(
            float currentLoad,
            float ambientPressure,
            float deltaTime,
            float absorptionRate)
        {
            _ = ambientPressure;
            _ = deltaTime;
            _ = absorptionRate;
            float safeCurrent = math.select(1f, currentLoad, math.isfinite(currentLoad) && currentLoad > 0f);
            return math.max(0f, safeCurrent);
        }

        internal static float ResolvePressureNarcosis01(
            float ambientPressure,
            float narcosisThreshold,
            float narcosisFullRange)
        {
            _ = ambientPressure;
            _ = narcosisThreshold;
            _ = narcosisFullRange;
            return 0f;
        }

        internal static bool ShouldApplyBendsDamage(
            float verticalSpeed,
            float nitrogenLoad,
            float safeAscentRate,
            float nitrogenLoadThreshold)
        {
            _ = verticalSpeed;
            _ = nitrogenLoad;
            _ = safeAscentRate;
            _ = nitrogenLoadThreshold;
            return false;
        }

        internal static float ResolveNitrogenStaminaMultiplier(float nitrogenBuildUp)
        {
            _ = nitrogenBuildUp;
            return 1f;
        }

        internal static float ResolveDecompressionVomitSeverity01(float nitrogenBuildUp)
        {
            _ = nitrogenBuildUp;
            return 0f;
        }

        internal static float ResolveNutritionalToxicityDamagePerSecond(float severity01, float baseDamageRate)
        {
            return math.max(0f, baseDamageRate) * NutritionalToxicityDamageScale * math.saturate(severity01);
        }

        internal static bool ShouldForceSuitPunctureBleeding(float damageAmount, float maxIntegrity)
        {
            if (!math.isfinite(damageAmount) || !math.isfinite(maxIntegrity) || maxIntegrity <= 0f)
                return false;

            return damageAmount >= maxIntegrity * SuitPunctureBleedDamageFraction;
        }
    }
}
