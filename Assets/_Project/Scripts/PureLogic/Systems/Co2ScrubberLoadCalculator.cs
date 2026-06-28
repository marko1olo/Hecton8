using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for Co2ScrubberLoadCalculator.
    /// Extracted from SubmarineAtmosphereSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class Co2ScrubberLoadCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="crewCount">Parameter representing the crewCount (float).</param>
        /// <param name="activityLevel01">Parameter representing the activityLevel01 (float).</param>
        /// <param name="scrubberCapacityKgPerHour">Parameter representing the scrubberCapacityKgPerHour (float).</param>
        /// <param name="co2ProductionPerPersonKgHr">Parameter representing the co2ProductionPerPersonKgHr (float).</param>
        /// <returns>Returns co2RemovalRate, float (netCo2Balance) of type float.</returns>
        public static float Compute(float crewCount, float activityLevel01, float scrubberCapacityKgPerHour, float co2ProductionPerPersonKgHr)
        {
            float safeCrewCount = float.IsNaN(crewCount) || crewCount < 0f ? 0f : crewCount;
            float safeActivity = float.IsNaN(activityLevel01) ? 0f : Math.Clamp(activityLevel01, 0f, 1f);
            float safeCapacity = float.IsNaN(scrubberCapacityKgPerHour) || scrubberCapacityKgPerHour < 0f ? 0f : scrubberCapacityKgPerHour;
            float safeProduction = float.IsNaN(co2ProductionPerPersonKgHr) || co2ProductionPerPersonKgHr < 0f ? 0f : co2ProductionPerPersonKgHr;

            float activityMultiplier = 1f + safeActivity;
            float totalProduction = safeCrewCount * safeProduction * activityMultiplier;

            float netCo2Balance = totalProduction - safeCapacity;

            if (float.IsInfinity(netCo2Balance))
            {
                return netCo2Balance > 0 ? float.MaxValue : float.MinValue;
            }

            return netCo2Balance;
        }
    }
}
