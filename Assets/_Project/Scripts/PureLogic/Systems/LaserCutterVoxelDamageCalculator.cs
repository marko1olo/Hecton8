using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for LaserCutterVoxelDamageCalculator.
    /// Extracted from LaserCutter.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LaserCutterVoxelDamageCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="laserPower">Parameter representing the laserPower (float).</param>
        /// <param name="distance">Parameter representing the distance (float).</param>
        /// <param name="materialHardness">Parameter representing the materialHardness (float).</param>
        /// <returns>Returns Damage applied of type float.</returns>
        public static float Compute(float laserPower, float distance, float materialHardness)
        {
            if (float.IsNaN(laserPower) || float.IsInfinity(laserPower) ||
                float.IsNaN(distance) || float.IsInfinity(distance) ||
                float.IsNaN(materialHardness) || float.IsInfinity(materialHardness))
            {
                return 0f;
            }

            if (laserPower <= 0f || distance < 0f || materialHardness < 0f)
            {
                return 0f;
            }

            // Inverse square falloff for heat delivered to voxel
            float distanceFalloff = 1f / Math.Max(1f, distance * distance);
            float deliveredHeat = laserPower * distanceFalloff;

            // Voxel material absorbs some of the heat
            float damage = deliveredHeat - materialHardness;

            // Ensure physical realism (no negative volume/damage)
            return Math.Max(0f, damage);
        }
    }
}
