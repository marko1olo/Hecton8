using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for ArmorPenetrationCalculator.
    /// Extracted from CombatDamageRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ArmorPenetrationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="projectileMass">Parameter representing the projectileMass (float).</param>
        /// <param name="impactVelocity">Parameter representing the impactVelocity (float).</param>
        /// <param name="armorHardness">Parameter representing the armorHardness (float).</param>
        /// <param name="armorThickness">Parameter representing the armorThickness (float).</param>
        /// <returns>Returns penetrationRatio 0.0=blocked, 1.0=full penetration of type float.</returns>
        public static float Compute(float projectileMass, float impactVelocity, float armorHardness, float armorThickness)
        {
            if (float.IsNaN(projectileMass) || float.IsNaN(impactVelocity) || float.IsNaN(armorHardness) || float.IsNaN(armorThickness))
            {
                return 0f;
            }
            if (float.IsInfinity(projectileMass) || float.IsInfinity(impactVelocity))
            {
                if (float.IsInfinity(armorHardness) || float.IsInfinity(armorThickness))
                {
                    return 0f; // Undefined comparison, default to blocked
                }
                return 1f; // Infinite penetration
            }
            if (float.IsInfinity(armorHardness) || float.IsInfinity(armorThickness))
            {
                return 0f;
            }

            if (projectileMass <= 0f || impactVelocity <= 0f)
            {
                return 0f;
            }
            if (armorHardness <= 0f || armorThickness <= 0f)
            {
                return 1f; // No armor, full penetration
            }

            float penetrationPower = projectileMass * (impactVelocity * impactVelocity);
            float armorResistance = armorHardness * armorThickness;

            float ratio = penetrationPower / armorResistance;

            if (float.IsNaN(ratio))
            {
                return 0f;
            }

            return Math.Clamp(ratio, 0f, 1f);
        }
    }
}
