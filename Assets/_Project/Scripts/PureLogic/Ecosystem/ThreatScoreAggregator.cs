using System;
using System.Numerics;

namespace Hecton8.PureLogic.Ecosystem
{
    /// <summary>
    /// Pure C# mathematical implementation for ThreatScoreAggregator.
    /// Extracted from EncounterDirector.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class ThreatScoreAggregator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name='hazardDistances'>Parameter representing the hazardDistances (float[]).</param>
        /// <param name='hazardWeights'>Parameter representing the hazardWeights (float[]).</param>
        /// <param name='hazardStrengths'>Parameter representing the hazardStrengths (float[]).</param>
        /// <param name='perceptionRadius'>Parameter representing the perceptionRadius (float).</param>
        /// <returns>Returns totalThreatScore of type float.</returns>
        public static float Calculate(float[] hazardDistances, float[] hazardWeights, float[] hazardStrengths, float perceptionRadius)
        {
            if (hazardDistances == null || hazardWeights == null || hazardStrengths == null)
            {
                return 0f;
            }

            int length = Math.Min(hazardDistances.Length, Math.Min(hazardWeights.Length, hazardStrengths.Length));

            if (length == 0 || float.IsNaN(perceptionRadius) || float.IsInfinity(perceptionRadius) || perceptionRadius <= 0f)
            {
                return 0f;
            }

            float totalThreatScore = 0f;

            for (int i = 0; i < length; i++)
            {
                float distance = hazardDistances[i];
                float weight = hazardWeights[i];
                float strength = hazardStrengths[i];

                if (float.IsNaN(distance) || float.IsNaN(weight) || float.IsNaN(strength))
                {
                    continue;
                }

                if (distance < 0f) distance = 0f;
                if (weight < 0f) weight = 0f;
                if (strength < 0f) strength = 0f;

                if (distance > perceptionRadius)
                {
                    continue;
                }

                float normalizedDistance = distance / perceptionRadius;
                // Distance falloff applied: 1.0 at 0 distance, 0.0 at perceptionRadius
                float falloff = 1f - normalizedDistance;

                float threat = weight * strength * falloff;

                if (!float.IsInfinity(threat))
                {
                    totalThreatScore += threat;
                }
            }

            return Math.Max(0f, totalThreatScore);
        }
    }
}
