using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for Co2ScrubberEfficiencyModel.
    /// Extracted from ShinobuPhysiologyRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class Co2ScrubberEfficiencyModel
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='usageHours'>Parameter representing the usageHours (float).</param>
        /// <param name='ambientTempCelsius'>Parameter representing the ambientTempCelsius (float).</param>
        /// <param name='maxEfficiency'>Parameter representing the maxEfficiency (float).</param>
        /// <param name='degradationRate'>Parameter representing the degradationRate (float).</param>
        /// <returns>Returns current scrubber efficiency 0.0-1.0 of type float.</returns>
        public static float Evaluate(float usageHours, float ambientTempCelsius, float maxEfficiency, float degradationRate)
        {
            if (float.IsNaN(usageHours) || float.IsInfinity(usageHours)) usageHours = 0f;
            if (float.IsNaN(ambientTempCelsius) || float.IsInfinity(ambientTempCelsius)) ambientTempCelsius = 20f;
            if (float.IsNaN(maxEfficiency) || float.IsInfinity(maxEfficiency)) maxEfficiency = 1f;
            if (float.IsNaN(degradationRate) || float.IsInfinity(degradationRate)) degradationRate = 0f;

            usageHours = Math.Max(0f, usageHours);
            maxEfficiency = Math.Max(0f, maxEfficiency);
            degradationRate = Math.Max(0f, degradationRate);

            // High temp accelerates decay. Default room temp is 20C.
            // Let's assume decay scale = 1.0 at 20C, and scales linearly.
            float tempScale = 1.0f + Math.Max(0f, (ambientTempCelsius - 20f) * 0.05f);

            float decay = usageHours * degradationRate * tempScale;
            float currentEfficiency = Math.Max(0f, maxEfficiency - decay);

            return Math.Min(1f, currentEfficiency);
        }
    }
}
