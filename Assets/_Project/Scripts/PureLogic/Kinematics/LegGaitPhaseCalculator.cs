using System;
using System.Numerics;

namespace Hecton8.PureLogic.Kinematics
{
    /// <summary>
    /// Pure C# mathematical implementation for LegGaitPhaseCalculator.
    /// Extracted from ProceduralCrabLegIKRuntime.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LegGaitPhaseCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="legIndex">Parameter representing the legIndex (int).</param>
        /// <param name="totalLegs">Parameter representing the totalLegs (int).</param>
        /// <param name="gaitCycleTime">Parameter representing the gaitCycleTime (float).</param>
        /// <param name="currentTime">Parameter representing the currentTime (float).</param>
        /// <returns>Returns phaseOffset 0.0-1.0, bool (isInSwingPhase) of type float.</returns>
        public static float Compute(int legIndex, int totalLegs, float gaitCycleTime, float currentTime)
        {
            if (totalLegs <= 0 || float.IsNaN(gaitCycleTime) || float.IsInfinity(gaitCycleTime) || gaitCycleTime <= 0f)
            {
                return 0f;
            }

            if (float.IsNaN(currentTime) || float.IsInfinity(currentTime))
            {
                return 0f;
            }

            // Opposite legs out of phase by 0.5. 4-leg: 0.25 offset. Phase cycles consistently.
            float spatialOffset = (float)legIndex / totalLegs;
            float temporalPhase = currentTime / gaitCycleTime;

            float phase = (spatialOffset + temporalPhase) % 1.0f;

            if (phase < 0f)
            {
                phase += 1.0f;
            }

            return phase;
        }
    }
}
