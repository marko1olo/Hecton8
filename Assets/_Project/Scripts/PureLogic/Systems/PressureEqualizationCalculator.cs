using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for PressureEqualizationCalculator.
    /// Extracted from SubmarineAtmosphereSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class PressureEqualizationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="internalPressurePa">Parameter representing the internalPressurePa (float).</param>
        /// <param name="externalPressurePa">Parameter representing the externalPressurePa (float).</param>
        /// <param name="airlockVolumeM3">Parameter representing the airlockVolumeM3 (float).</param>
        /// <param name="valveFlowRateM3PerSec">Parameter representing the valveFlowRateM3PerSec (float).</param>
        /// <returns>Returns equalizationTimeSeconds of type float.</returns>
        public static float Compute(float internalPressurePa, float externalPressurePa, float airlockVolumeM3, float valveFlowRateM3PerSec, float epsilonTolerance = 0.0001f)
        {
            // Step 1 - Parameter Validation
            if (float.IsNaN(internalPressurePa) || float.IsInfinity(internalPressurePa))
                internalPressurePa = 0f;
            if (float.IsNaN(externalPressurePa) || float.IsInfinity(externalPressurePa))
                externalPressurePa = 0f;
            if (float.IsNaN(airlockVolumeM3) || float.IsInfinity(airlockVolumeM3))
                airlockVolumeM3 = float.MaxValue;
            if (float.IsNaN(valveFlowRateM3PerSec) || float.IsInfinity(valveFlowRateM3PerSec))
                valveFlowRateM3PerSec = 0f;

            internalPressurePa = Math.Max(0f, internalPressurePa);
            externalPressurePa = Math.Max(0f, externalPressurePa);
            airlockVolumeM3 = Math.Max(0f, airlockVolumeM3);
            valveFlowRateM3PerSec = Math.Max(0f, valveFlowRateM3PerSec);

            float pressureDelta = Math.Abs(internalPressurePa - externalPressurePa);

            // Equal pressures: zero time.
            if (pressureDelta < epsilonTolerance || airlockVolumeM3 < epsilonTolerance)
            {
                return 0f;
            }

            // If valve is closed or broken, equalization time is infinite, but we clamp to a reasonable max or return float.MaxValue
            // As per constraints: Large differential, small valve: long time. Large valve: fast.
            if (valveFlowRateM3PerSec < epsilonTolerance)
            {
                // We could return float.MaxValue or clamp to a large number. Let's return float.MaxValue for infinite time.
                return float.MaxValue;
            }

            // Step 2 - Business Logic
            // The time required to equalize a volume with a specific valve flow rate.
            // Equalization time depends on the total volume to exchange and the flow rate.
            // Assuming compressible fluid (air), a simple proxy model:
            // time = (Volume / FlowRate) * (PressureDelta / max(Internal, External))
            float maxPressure = Math.Max(internalPressurePa, externalPressurePa);

            // Step 3 - Boundary Guarding
            // We use double for intermediate calculation to avoid overflow for large volumes
            double timeToEqualizeDouble = ((double)airlockVolumeM3 / (double)valveFlowRateM3PerSec) * ((double)pressureDelta / (double)maxPressure);
            float timeToEqualize;

            if (double.IsNaN(timeToEqualizeDouble) || timeToEqualizeDouble < 0.0)
            {
                timeToEqualize = float.MaxValue;
            }
            else if (timeToEqualizeDouble > float.MaxValue)
            {
                timeToEqualize = float.MaxValue;
            }
            else
            {
                timeToEqualize = (float)timeToEqualizeDouble;
            }

            // Step 4 - Output Return
            return timeToEqualize;
        }
    }
}
