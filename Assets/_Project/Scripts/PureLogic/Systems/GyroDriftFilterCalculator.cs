using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for GyroDriftFilterCalculator.
    /// Extracted from InputDispatcher.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class GyroDriftFilterCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='gyroSample'>Parameter representing the gyroSample (float).</param>
        /// <param name='previousFiltered'>Parameter representing the previousFiltered (float).</param>
        /// <param name='cutoffFrequencyHz'>Parameter representing the cutoffFrequencyHz (float).</param>
        /// <param name='sampleRateHz'>Parameter representing the sampleRateHz (float).</param>
        /// <returns>Returns filteredGyroValue of type float.</returns>
        public static float Compute(float gyroSample, float previousFiltered, float cutoffFrequencyHz, float sampleRateHz)
        {
            if (float.IsNaN(gyroSample) || float.IsNaN(previousFiltered) || float.IsNaN(cutoffFrequencyHz) || float.IsNaN(sampleRateHz))
                return previousFiltered;

            if (float.IsInfinity(gyroSample) || float.IsInfinity(previousFiltered) || float.IsInfinity(cutoffFrequencyHz) || float.IsInfinity(sampleRateHz))
                return previousFiltered;

            if (sampleRateHz <= 0f)
                return previousFiltered;

            if (cutoffFrequencyHz <= 0f)
                return gyroSample;

            double dt = 1.0 / (double)sampleRateHz;
            double rc = 1.0 / (2.0 * Math.PI * (double)cutoffFrequencyHz);

            if (double.IsInfinity(rc))
                return previousFiltered;

            double alpha = dt / (rc + dt);

            if (alpha < 0.0) alpha = 0.0;
            if (alpha > 1.0) alpha = 1.0;

            double driftEstimate = alpha * (double)gyroSample + (1.0 - alpha) * (double)previousFiltered;

            if (double.IsNaN(driftEstimate) || double.IsInfinity(driftEstimate))
                return previousFiltered;

            float result = (float)driftEstimate;

            if (float.IsNaN(result) || float.IsInfinity(result))
                return previousFiltered;

            return result;
        }
    }
}
