using System;
using System.Numerics;
using System.Globalization;

namespace Hecton8.PureLogic.Systems
{
    /// <summary>
    /// Pure C# mathematical implementation for VoronoiBiomeSeedCalculator.
    /// Extracted from HectonWorldGenerator.cs. Fully stateless and allocation-free.
    /// </summary>
    public static class VoronoiBiomeSeedCalculator
    {
        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <param name="worldPos">Parameter representing the worldPos (Vector3).</param>
        /// <param name="biomeSeedPoints">Parameter representing the biomeSeedPoints (Vector3[]).</param>
        /// <param name="biomeTypes">Parameter representing the biomeTypes (string[]).</param>
        /// <param name="noiseBlend">Parameter representing the noiseBlend (float).</param>
        /// <returns>Returns dominantBiomeType, float (blendFactor) of type string.</returns>
        public static string Compute(Vector3 worldPos, Vector3[] biomeSeedPoints, string[] biomeTypes, float noiseBlend)
        {
            if (float.IsNaN(worldPos.X) || float.IsNaN(worldPos.Y) || float.IsNaN(worldPos.Z) ||
                float.IsInfinity(worldPos.X) || float.IsInfinity(worldPos.Y) || float.IsInfinity(worldPos.Z))
            {
                throw new ArgumentException("worldPos is NaN or Infinity");
            }

            if (float.IsNaN(noiseBlend) || float.IsInfinity(noiseBlend))
            {
                throw new ArgumentException("noiseBlend is NaN or Infinity");
            }

            if (biomeSeedPoints == null || biomeTypes == null)
            {
                throw new ArgumentNullException("biome arrays cannot be null");
            }

            if (biomeSeedPoints.Length == 0 || biomeTypes.Length == 0)
            {
                throw new ArgumentException("biome arrays cannot be empty");
            }

            if (biomeSeedPoints.Length != biomeTypes.Length)
            {
                throw new ArgumentException("biome arrays must have the same length");
            }

            // Boundary Guarding: clamp noiseBlend
            noiseBlend = Math.Max(0f, Math.Min(1f, noiseBlend));

            if (biomeSeedPoints.Length == 1)
            {
                return $"{biomeTypes[0]},1.000";
            }

            float d1 = float.MaxValue;
            float d2 = float.MaxValue;
            int closestIndex = 0;

            for (int i = 0; i < biomeSeedPoints.Length; i++)
            {
                Vector3 pt = biomeSeedPoints[i];
                if (float.IsNaN(pt.X) || float.IsNaN(pt.Y) || float.IsNaN(pt.Z) ||
                    float.IsInfinity(pt.X) || float.IsInfinity(pt.Y) || float.IsInfinity(pt.Z))
                {
                    continue;
                }

                // Protect against extreme values overflowing distance calculation
                float dist;
                try
                {
                    dist = Vector3.Distance(worldPos, pt);
                }
                catch (OverflowException)
                {
                    dist = float.MaxValue;
                }

                if (float.IsNaN(dist) || float.IsInfinity(dist))
                {
                    dist = float.MaxValue;
                }

                if (dist < d1)
                {
                    d2 = d1;
                    d1 = dist;
                    closestIndex = i;
                }
                else if (dist < d2)
                {
                    d2 = dist;
                }
            }

            if (d1 == float.MaxValue)
            {
                return $"{biomeTypes[0]},1.000";
            }

            float rawBlend = 1f;
            if (d2 > 0.000001f)
            {
                rawBlend = 0.5f + 0.5f * ((d2 - d1) / d2);
            }

            float blendFactor = 1.0f - (1.0f - rawBlend) * noiseBlend;

            return $"{biomeTypes[closestIndex]},{blendFactor.ToString("0.000", CultureInfo.InvariantCulture)}";
        }
    }
}
