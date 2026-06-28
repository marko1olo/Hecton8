using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for RadiationDoseAccumulator.
    /// Extracted from HectonSurvivalSystem.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class RadiationDoseAccumulator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="currentDoseSv">Parameter representing the currentDoseSv (float).</param>
        /// <param name="exposureRateSvPerHour">Parameter representing the exposureRateSvPerHour (float).</param>
        /// <param name="recoveryRateSvPerHour">Parameter representing the recoveryRateSvPerHour (float).</param>
        /// <param name="deltaTime">Parameter representing the deltaTime (float).</param>
        /// <returns>Returns newDoseSv of type float.</returns>
        public static float Calculate(float currentDoseSv, float exposureRateSvPerHour, float recoveryRateSvPerHour, float deltaTime)
        {
            if (float.IsNaN(currentDoseSv) || float.IsInfinity(currentDoseSv)) currentDoseSv = 0f;
            if (float.IsNaN(exposureRateSvPerHour) || float.IsInfinity(exposureRateSvPerHour)) exposureRateSvPerHour = 0f;
            if (float.IsNaN(recoveryRateSvPerHour) || float.IsInfinity(recoveryRateSvPerHour)) recoveryRateSvPerHour = 0f;
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) deltaTime = 0f;

            currentDoseSv = Math.Max(0f, currentDoseSv);
            exposureRateSvPerHour = Math.Max(0f, exposureRateSvPerHour);
            recoveryRateSvPerHour = Math.Max(0f, recoveryRateSvPerHour);
            deltaTime = Math.Max(0f, deltaTime);

            float deltaHours = deltaTime / 3600f;
            float netRate = exposureRateSvPerHour - recoveryRateSvPerHour;

            float accumulatedDose = currentDoseSv + (netRate * deltaHours);

            return Math.Max(0f, accumulatedDose);
        }
    }
}
