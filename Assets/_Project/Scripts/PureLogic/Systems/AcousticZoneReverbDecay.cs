using System;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for AcousticZoneReverbDecay.
    /// Extracted from AcousticZoneController.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class AcousticZoneReverbDecay
    {
        private const float SabineEquationConstant = 0.161f;
        private const float SabineMinimumRt60Seconds = 0.12f;
        private const float SabineMaximumRt60Seconds = 10f;

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='volumeM3'>Parameter representing the volumeM3 (float).</param>
        /// <param name='surfaceAreaM2'>Parameter representing the surfaceAreaM2 (float).</param>
        /// <param name='averageAbsorptionCoefficient'>Parameter representing the averageAbsorptionCoefficient (float).</param>
        /// <returns>Returns Decay time in seconds of type float.</returns>
        public static float Calculate(float volumeM3, float surfaceAreaM2, float averageAbsorptionCoefficient)
        {
            if (surfaceAreaM2 <= 0f || averageAbsorptionCoefficient <= 0f)
                return SabineMaximumRt60Seconds;

            float absorptionArea = surfaceAreaM2 * averageAbsorptionCoefficient;
            if (absorptionArea <= 0f)
                return SabineMaximumRt60Seconds;

            float rt60 = (SabineEquationConstant * volumeM3) / absorptionArea;

            if (float.IsNaN(rt60) || float.IsInfinity(rt60))
                return SabineMaximumRt60Seconds;

            return Math.Clamp(rt60, SabineMinimumRt60Seconds, SabineMaximumRt60Seconds);
        }
    }
}
