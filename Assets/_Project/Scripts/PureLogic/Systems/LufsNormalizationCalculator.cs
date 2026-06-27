using System;
using System.Numerics;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for LufsNormalizationCalculator.
    /// Extracted from AdaptiveStemAudioMixer.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class LufsNormalizationCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='measuredLUFS'>Parameter representing the measuredLUFS (float).</param>
        /// <param name='targetLUFS'>Parameter representing the targetLUFS (float).</param>
        /// <param name='maxGainDB'>Parameter representing the maxGainDB (float).</param>
        /// <param name='minGainDB'>Parameter representing the minGainDB (float).</param>
        /// <returns>Returns gainDB of type float.</returns>
        public static float Compute(float measuredLUFS, float targetLUFS, float maxGainDB, float minGainDB)
        {
            if (float.IsNaN(measuredLUFS) || float.IsNaN(targetLUFS) || float.IsNaN(maxGainDB) || float.IsNaN(minGainDB) ||
                float.IsInfinity(measuredLUFS) || float.IsInfinity(targetLUFS) || float.IsInfinity(maxGainDB) || float.IsInfinity(minGainDB))
            {
                return 0f;
            }

            if (maxGainDB < minGainDB)
            {
                maxGainDB = Math.Max(maxGainDB, minGainDB);
            }

            float gainDB = targetLUFS - measuredLUFS;
            return Math.Clamp(gainDB, minGainDB, maxGainDB);
        }
    }
}
