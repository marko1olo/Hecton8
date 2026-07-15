using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Canonical pure math for player carry load, derived stamina availability, and movement encumbrance gates.
    /// </summary>
    public static class PlayerEffortLoadCalculator
    {
        public const float DefaultCarryCapacityKg = 200f;
        public const float DefaultMinimumMovementMultiplier = 0.5f;
        public const float DefaultMinimumUpwardSwimMultiplier = 0.6f;
        public const float DefaultCriticalEncumbranceRatio = 1.5f;
        public const float DefaultCriticalStaminaFailureThreshold01 = 0.1f;
        public const float DefaultMaximumEnergyMetabolicMultiplier = 8f;
        public const float DefaultMaximumOxygenMetabolicMultiplier = 6f;

        public static float ComputeLoadRatio(float carriedMassKg, float carryCapacityKg)
        {
            float safeMassKg = SanitizeNonNegative(carriedMassKg);
            float safeCarryCapacityKg = SanitizeCarryCapacity(carryCapacityKg);
            return safeMassKg / safeCarryCapacityKg;
        }

        public static float ComputeLoad01(float carriedMassKg, float carryCapacityKg)
        {
            return Clamp01(ComputeLoadRatio(carriedMassKg, carryCapacityKg));
        }

        public static float ComputeMovementMultiplier(float carriedMassKg, float carryCapacityKg, float minimumMovementMultiplier)
        {
            float safeMinimumMovementMultiplier = Clamp01(minimumMovementMultiplier);
            return WeightPenaltyCurveCalculator.Compute(
                carriedMassKg,
                carryCapacityKg,
                0f,
                1f - safeMinimumMovementMultiplier);
        }

        public static float ComputeMovementMultiplierFromLoad(float load01, float minimumMovementMultiplier)
        {
            return ComputeMovementMultiplier(Clamp01(load01), 1f, minimumMovementMultiplier);
        }

        public static float ComputeUpwardSwimMultiplier(float load01, float minimumUpwardSwimMultiplier)
        {
            float safeLoad01 = Clamp01(load01);
            float safeMinimumUpwardSwimMultiplier = Clamp01(minimumUpwardSwimMultiplier);
            return 1f + ((safeMinimumUpwardSwimMultiplier - 1f) * safeLoad01);
        }

        public static bool IsCriticalInventoryLoad(float loadRatio, float criticalEncumbranceRatio)
        {
            if (!IsFinite(loadRatio))
                return false;

            float safeCriticalEncumbranceRatio = Math.Max(0f, SanitizeFinite(criticalEncumbranceRatio, DefaultCriticalEncumbranceRatio));
            return loadRatio >= safeCriticalEncumbranceRatio;
        }

        public static bool ShouldTriggerCriticalStaminaFailure(float loadRatio, float stamina01, float criticalEncumbranceRatio, float staminaFailureThreshold01)
        {
            if (!IsFinite(loadRatio) || !IsFinite(stamina01))
                return false;

            float safeStamina01 = Clamp01(stamina01);
            float safeFailureThreshold01 = Clamp01(staminaFailureThreshold01);
            return IsCriticalInventoryLoad(loadRatio, criticalEncumbranceRatio) && safeStamina01 < safeFailureThreshold01;
        }

        public static float ComputeEnergyMetabolicMultiplier(
            float load01,
            float movementIntent01,
            float movementStaminaDrainMultiplier,
            float upwardSwimMultiplier,
            bool isSprinting,
            bool isSubmerged,
            float maximumMultiplier)
        {
            float safeLoad01 = Clamp01(load01);
            float safeIntent01 = Clamp01(movementIntent01);
            float safeDrainMultiplier = Math.Max(0f, SanitizeFinite(movementStaminaDrainMultiplier, 0f));
            float safeUpwardSwimMultiplier = Clamp01(upwardSwimMultiplier);
            float safeMaximumMultiplier = Math.Max(1f, SanitizeFinite(maximumMultiplier, DefaultMaximumEnergyMetabolicMultiplier));

            float effortLoad01 = safeLoad01 * safeIntent01;
            float loadTax = 1f + effortLoad01 * (0.75f + effortLoad01 * 1.25f);
            float intentTax = 1f + safeIntent01 * Math.Max(0f, safeDrainMultiplier);
            float sprintTax = isSprinting ? 1.35f : 1f;
            float upwardTax = isSubmerged ? 1f + (1f - safeUpwardSwimMultiplier) * 1.5f * safeIntent01 : 1f;
            float multiplier = loadTax * intentTax * sprintTax * upwardTax;
            return Math.Max(1f, Math.Min(safeMaximumMultiplier, multiplier));
        }

        public static float ComputeOxygenMetabolicMultiplier(
            float load01,
            float movementIntent01,
            float upwardSwimMultiplier,
            bool isSprinting,
            bool isSubmerged,
            float maximumMultiplier)
        {
            float safeLoad01 = Clamp01(load01);
            float safeIntent01 = Clamp01(movementIntent01);
            float safeUpwardSwimMultiplier = Clamp01(upwardSwimMultiplier);
            float safeMaximumMultiplier = Math.Max(1f, SanitizeFinite(maximumMultiplier, DefaultMaximumOxygenMetabolicMultiplier));

            float effortLoad01 = safeLoad01 * safeIntent01;
            float loadTax = 1f + effortLoad01 * 0.45f;
            float intentTax = 1f + safeIntent01 * 0.55f;
            float sprintTax = isSprinting ? 1.2f : 1f;
            float upwardTax = isSubmerged ? 1f + (1f - safeUpwardSwimMultiplier) * 1.1f * safeIntent01 : 1f;
            float multiplier = loadTax * intentTax * sprintTax * upwardTax;
            return Math.Max(1f, Math.Min(safeMaximumMultiplier, multiplier));
        }

        public static float SanitizeStaminaAvailability01(float stamina01)
        {
            return Clamp01(stamina01);
        }

        private static float SanitizeCarryCapacity(float carryCapacityKg)
        {
            return Math.Max(0.001f, SanitizeFinite(carryCapacityKg, DefaultCarryCapacityKg));
        }

        private static float SanitizeNonNegative(float value)
        {
            return Math.Max(0f, SanitizeFinite(value, 0f));
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return IsFinite(value) ? value : fallback;
        }

        private static float Clamp01(float value)
        {
            if (!IsFinite(value))
                return 0f;

            return Math.Max(0f, Math.Min(1f, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
