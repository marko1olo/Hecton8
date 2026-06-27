using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for LaserCutDepthPowerCalculator.
    /// Extracted from LaserCutter.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LaserCutDepthPowerCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="beamPowerWatts">Parameter representing the beamPowerWatts (float).</param>
        /// <param name="focusDiameterMm">Parameter representing the focusDiameterMm (float).</param>
        /// <param name="materialAbsorptivity">Parameter representing the materialAbsorptivity (float).</param>
        /// <param name="pulseDurationMs">Parameter representing the pulseDurationMs (float).</param>
        /// <returns>Returns cutDepthMm of type float.</returns>
        public static float Compute(float beamPowerWatts, float focusDiameterMm, float materialAbsorptivity, float pulseDurationMs)
        {
            if (float.IsNaN(beamPowerWatts) || float.IsNaN(focusDiameterMm) || float.IsNaN(materialAbsorptivity) || float.IsNaN(pulseDurationMs) ||
                float.IsInfinity(beamPowerWatts) || float.IsInfinity(focusDiameterMm) || float.IsInfinity(materialAbsorptivity) || float.IsInfinity(pulseDurationMs))
            {
                return 0f;
            }

            if (focusDiameterMm <= 0f)
            {
                return 0f;
            }

            beamPowerWatts = Math.Max(0f, beamPowerWatts);
            materialAbsorptivity = Math.Max(0f, materialAbsorptivity);
            pulseDurationMs = Math.Max(0f, pulseDurationMs);

            float depth = (beamPowerWatts * materialAbsorptivity * pulseDurationMs) / (focusDiameterMm * focusDiameterMm);

            if (float.IsNaN(depth) || float.IsInfinity(depth) || depth < 0f)
            {
                return 0f;
            }
            return depth;
        }
    }
}
