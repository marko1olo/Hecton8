using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for RepairRateMaterialCalculator.
    /// Extracted from RepairTool.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class RepairRateMaterialCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="toolCharge01">Parameter representing the toolCharge01 (float).</param>
        /// <param name="materialHardness">Parameter representing the materialHardness (float).</param>
        /// <param name="baseRepairRate">Parameter representing the baseRepairRate (float).</param>
        /// <param name="depthPressureMultiplier">Parameter representing the depthPressureMultiplier (float).</param>
        /// <returns>Returns repairRatePerSecond of type float.</returns>
        public static float Compute(float toolCharge01, float materialHardness, float baseRepairRate, float depthPressureMultiplier)
        {
            if (float.IsNaN(toolCharge01) || float.IsNaN(materialHardness) || float.IsNaN(baseRepairRate) || float.IsNaN(depthPressureMultiplier))
                return 0f;
            if (float.IsInfinity(toolCharge01) || float.IsInfinity(materialHardness) || float.IsInfinity(baseRepairRate) || float.IsInfinity(depthPressureMultiplier))
                return 0f;

            float charge = toolCharge01 < 0f ? 0f : (toolCharge01 > 1f ? 1f : toolCharge01);
            float baseRate = baseRepairRate < 0f ? 0f : baseRepairRate;

            if (materialHardness <= 0f || depthPressureMultiplier <= 0f)
                return 0f;

            float rate = (baseRate * charge) / (materialHardness * depthPressureMultiplier);

            if (float.IsInfinity(rate) || float.IsNaN(rate))
                return 0f;

            return rate;
        }
    }
}
