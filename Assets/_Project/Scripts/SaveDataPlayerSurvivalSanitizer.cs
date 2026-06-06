using Unity.Mathematics;

namespace Hecton8.SaveSystem
{
    internal static class SaveDataPlayerSurvivalSanitizer
    {
        internal static void SanitizePlayerStats(ref PlayerStatsDTO value)
        {
            value.oxygen = SanitizeNonNegativeFinite(value.oxygen);
            value.energy = SanitizeNonNegativeFinite(value.energy);
            value.integrity = SanitizeNonNegativeFinite(value.integrity);
            value.weight = SanitizeNonNegativeFinite(value.weight);
            value.hunger = SanitizeNonNegativeFinite(value.hunger);
            value.thirst = SanitizeNonNegativeFinite(value.thirst);

            value.currentLifeDurationSeconds = SanitizeNonNegativeFinite(value.currentLifeDurationSeconds);
            value.currentLifePeakDepthMeters = SanitizeNonNegativeFinite(value.currentLifePeakDepthMeters);
            value.currentLifeLowestOxygenNormalized = Sanitize01(value.currentLifeLowestOxygenNormalized, 1f);
            value.currentLifeLowestEnergyNormalized = Sanitize01(value.currentLifeLowestEnergyNormalized, 1f);
            value.currentLifeLowestIntegrityNormalized = Sanitize01(value.currentLifeLowestIntegrityNormalized, 1f);

            value.bleedingSecondsRemaining = SanitizeNonNegativeFinite(value.bleedingSecondsRemaining);
            value.bleedingDamagePerSecond = SanitizeNonNegativeFinite(value.bleedingDamagePerSecond);
            value.bleedingSeverity01 = Sanitize01(value.bleedingSeverity01, 0f);
            value.fractureSecondsRemaining = SanitizeNonNegativeFinite(value.fractureSecondsRemaining);
            value.fracturePenalty01 = Sanitize01(value.fracturePenalty01, 0f);
            value.environmentTemperature = SanitizeFinite(value.environmentTemperature, 0f);
            value.coldStressSeverity01 = Sanitize01(value.coldStressSeverity01, 0f);
            value.heatStressSeverity01 = Sanitize01(value.heatStressSeverity01, 0f);
            value.nitrogenBuildUp = math.clamp(
                SanitizeNonNegativeFinite(value.nitrogenBuildUp),
                0f,
                SaveData.PlayerStatsNitrogenBuildUpHardCap);

            SanitizePosition(ref value.lastDeathPosX, ref value.lastDeathPosY, ref value.lastDeathPosZ);
            value.lastDeathLifeDurationSeconds = SanitizeNonNegativeFinite(value.lastDeathLifeDurationSeconds);
            value.lastDeathPeakDepthMeters = SanitizeNonNegativeFinite(value.lastDeathPeakDepthMeters);
            value.lastDeathLowestOxygenNormalized = Sanitize01(value.lastDeathLowestOxygenNormalized, 1f);
            value.lastDeathLowestEnergyNormalized = Sanitize01(value.lastDeathLowestEnergyNormalized, 1f);
            value.lastDeathLowestIntegrityNormalized = Sanitize01(value.lastDeathLowestIntegrityNormalized, 1f);

            SanitizePosition(ref value.posX, ref value.posY, ref value.posZ);
            SanitizeQuaternion(ref value.rotX, ref value.rotY, ref value.rotZ, ref value.rotW);
            SanitizeVelocity(ref value.velX, ref value.velY, ref value.velZ);
        }

        internal static void SanitizePlayerKinematicState(ref PlayerKinematicStateDTO value)
        {
            SanitizePosition(ref value.posX, ref value.posY, ref value.posZ);
            SanitizeQuaternion(ref value.rotX, ref value.rotY, ref value.rotZ, ref value.rotW);
            SanitizeVelocity(ref value.velX, ref value.velY, ref value.velZ);
        }

        internal static bool PlayerStatsEqual(in PlayerStatsDTO a, in PlayerStatsDTO b)
        {
            return Approximately(a.oxygen, b.oxygen) &&
                   Approximately(a.energy, b.energy) &&
                   Approximately(a.integrity, b.integrity) &&
                   Approximately(a.weight, b.weight) &&
                   Approximately(a.hunger, b.hunger) &&
                   Approximately(a.thirst, b.thirst) &&
                   Approximately(a.currentLifeDurationSeconds, b.currentLifeDurationSeconds) &&
                   Approximately(a.currentLifePeakDepthMeters, b.currentLifePeakDepthMeters) &&
                   Approximately(a.currentLifeLowestOxygenNormalized, b.currentLifeLowestOxygenNormalized) &&
                   Approximately(a.currentLifeLowestEnergyNormalized, b.currentLifeLowestEnergyNormalized) &&
                   Approximately(a.currentLifeLowestIntegrityNormalized, b.currentLifeLowestIntegrityNormalized) &&
                   a.injuryFlags == b.injuryFlags &&
                   Approximately(a.bleedingSecondsRemaining, b.bleedingSecondsRemaining) &&
                   Approximately(a.bleedingDamagePerSecond, b.bleedingDamagePerSecond) &&
                   Approximately(a.bleedingSeverity01, b.bleedingSeverity01) &&
                   Approximately(a.fractureSecondsRemaining, b.fractureSecondsRemaining) &&
                   Approximately(a.fracturePenalty01, b.fracturePenalty01) &&
                   Approximately(a.environmentTemperature, b.environmentTemperature) &&
                   Approximately(a.coldStressSeverity01, b.coldStressSeverity01) &&
                   Approximately(a.heatStressSeverity01, b.heatStressSeverity01) &&
                   Approximately(a.nitrogenBuildUp, b.nitrogenBuildUp) &&
                   a.hasLastDeathRecord == b.hasLastDeathRecord &&
                   a.lastDeathCause == b.lastDeathCause &&
                   Approximately(a.lastDeathPosX, b.lastDeathPosX) &&
                   Approximately(a.lastDeathPosY, b.lastDeathPosY) &&
                   Approximately(a.lastDeathPosZ, b.lastDeathPosZ) &&
                   Approximately(a.lastDeathLifeDurationSeconds, b.lastDeathLifeDurationSeconds) &&
                   Approximately(a.lastDeathPeakDepthMeters, b.lastDeathPeakDepthMeters) &&
                   Approximately(a.lastDeathLowestOxygenNormalized, b.lastDeathLowestOxygenNormalized) &&
                   Approximately(a.lastDeathLowestEnergyNormalized, b.lastDeathLowestEnergyNormalized) &&
                   Approximately(a.lastDeathLowestIntegrityNormalized, b.lastDeathLowestIntegrityNormalized) &&
                   Approximately(a.posX, b.posX) &&
                   Approximately(a.posY, b.posY) &&
                   Approximately(a.posZ, b.posZ) &&
                   Approximately(a.rotX, b.rotX) &&
                   Approximately(a.rotY, b.rotY) &&
                   Approximately(a.rotZ, b.rotZ) &&
                   Approximately(a.rotW, b.rotW) &&
                   Approximately(a.velX, b.velX) &&
                   Approximately(a.velY, b.velY) &&
                   Approximately(a.velZ, b.velZ);
        }

        internal static bool PlayerKinematicStateEqual(in PlayerKinematicStateDTO a, in PlayerKinematicStateDTO b)
        {
            return Approximately(a.posX, b.posX) &&
                   Approximately(a.posY, b.posY) &&
                   Approximately(a.posZ, b.posZ) &&
                   Approximately(a.rotX, b.rotX) &&
                   Approximately(a.rotY, b.rotY) &&
                   Approximately(a.rotZ, b.rotZ) &&
                   Approximately(a.rotW, b.rotW) &&
                   Approximately(a.velX, b.velX) &&
                   Approximately(a.velY, b.velY) &&
                   Approximately(a.velZ, b.velZ) &&
                   a.flags == b.flags;
        }

        private static void SanitizePosition(ref float x, ref float y, ref float z)
        {
            if (math.all(math.isfinite(new float3(x, y, z))))
                return;

            x = 0f;
            y = 0f;
            z = 0f;
        }

        private static void SanitizeQuaternion(ref float x, ref float y, ref float z, ref float w)
        {
            float4 quaternion = new float4(x, y, z, w);
            float lengthSq = math.lengthsq(quaternion);
            if (!math.all(math.isfinite(quaternion)) || !math.isfinite(lengthSq) || lengthSq <= 0.000001f)
            {
                x = 0f;
                y = 0f;
                z = 0f;
                w = 1f;
                return;
            }

            float invLength = math.rsqrt(lengthSq);
            x *= invLength;
            y *= invLength;
            z *= invLength;
            w *= invLength;
        }

        private static void SanitizeVelocity(ref float x, ref float y, ref float z)
        {
            float3 velocity = new float3(x, y, z);
            float speedSq = math.lengthsq(velocity);
            if (!math.all(math.isfinite(velocity)) || !math.isfinite(speedSq) || speedSq <= 0.000001f)
            {
                x = 0f;
                y = 0f;
                z = 0f;
                return;
            }

            if (speedSq <= SaveData.PlayerKinematicVelocityHardCapSq)
                return;

            velocity *= SaveData.PlayerKinematicVelocityHardCapMetersPerSecond * math.rsqrt(speedSq);
            x = velocity.x;
            y = velocity.y;
            z = velocity.z;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static float SanitizeNonNegativeFinite(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        private static double SanitizeNonNegativeFinite(double value)
        {
            return math.isfinite(value) ? math.max(0d, value) : 0d;
        }

        private static float Sanitize01(float value, float fallback)
        {
            return math.isfinite(value) ? math.saturate(value) : math.saturate(fallback);
        }

        private static bool Approximately(float a, float b)
        {
            return math.abs(a - b) <= 0.000001f;
        }

        private static bool Approximately(double a, double b)
        {
            return math.abs(a - b) <= 0.000001d;
        }
    }
}
