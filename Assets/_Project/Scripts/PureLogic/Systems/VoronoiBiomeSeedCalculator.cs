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
        /// Allocation-free biome resolve. Returns the winning seed as an index and the blend
        /// factor as a float, so no string is built and no precision is lost.
        /// </summary>
        /// <remarks>
        /// Prefer this over <see cref="Compute"/> anywhere biome resolution runs per voxel,
        /// per cell or per chunk. <see cref="Compute"/> formats its result into a string
        /// (<c>"biome,0.123"</c>), which allocates on every call and quantises the blend
        /// factor to three decimals — the caller then has to parse it back. World generation
        /// is a bulk path, and the project's zero-GC law does not allow that there.
        /// This overload never throws, never allocates and takes no delegate. It is still not
        /// Burst-legal, because <paramref name="biomeSeedPoints"/> is a managed array; a
        /// Burst caller must pass an unmanaged buffer through its own overload.
        /// </remarks>
        /// <param name="worldPos">Sample position.</param>
        /// <param name="biomeSeedPoints">Seed points. Non-finite entries are skipped.</param>
        /// <param name="noiseBlend">Blend authority, clamped to 0..1.</param>
        /// <param name="biomeIndex">Index of the nearest valid seed, or <c>-1</c> when none is usable.</param>
        /// <param name="blendFactor">Blend factor in 0..1; <c>1</c> when a single seed dominates.</param>
        /// <returns><c>true</c> when a valid seed was found and the outputs are usable.</returns>
        public static bool TryCompute(
            Vector3 worldPos,
            Vector3[] biomeSeedPoints,
            float noiseBlend,
            out int biomeIndex,
            out float blendFactor)
        {
            biomeIndex = -1;
            blendFactor = 0f;

            if (biomeSeedPoints == null || biomeSeedPoints.Length == 0)
                return false;

            if (!IsFinite(worldPos))
                return false;

            if (float.IsNaN(noiseBlend) || float.IsInfinity(noiseBlend))
                noiseBlend = 0f;
            noiseBlend = Math.Max(0f, Math.Min(1f, noiseBlend));

            float d1 = float.MaxValue;
            float d2 = float.MaxValue;
            int closest = -1;

            for (int i = 0; i < biomeSeedPoints.Length; i++)
            {
                Vector3 pt = biomeSeedPoints[i];
                if (!IsFinite(pt))
                    continue;

                // Squared distance keeps the scan branch-light and cannot overflow to a
                // throwing path; the ordering it produces is identical to true distance.
                float dx = worldPos.X - pt.X;
                float dy = worldPos.Y - pt.Y;
                float dz = worldPos.Z - pt.Z;
                float distSq = (dx * dx) + (dy * dy) + (dz * dz);
                if (float.IsNaN(distSq))
                    continue;

                if (distSq < d1)
                {
                    d2 = d1;
                    d1 = distSq;
                    closest = i;
                }
                else if (distSq < d2)
                {
                    d2 = distSq;
                }
            }

            if (closest < 0)
                return false;

            biomeIndex = closest;

            if (d2 >= float.MaxValue)
            {
                // Only one usable seed: it owns the sample outright.
                blendFactor = 1f;
                return true;
            }

            // Back to linear distance for the ratio so the curve matches Compute.
            float near = (float)Math.Sqrt(d1);
            float second = (float)Math.Sqrt(d2);

            float rawBlend = 1f;
            if (second > 0.000001f)
                rawBlend = 0.5f + (0.5f * ((second - near) / second));

            blendFactor = Math.Max(0f, Math.Min(1f, 1f - ((1f - rawBlend) * noiseBlend)));
            return true;
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }

        /// <summary>
        /// Computes/evaluates the mathematical model.
        /// </summary>
        /// <remarks>
        /// Allocates a formatted string on every call and quantises the blend factor to three
        /// decimals. Use <see cref="TryCompute"/> in any bulk or per-sample path.
        /// </remarks>
        /// <param name="worldPos">Parameter representing the worldPos (Vector3).</param>
        /// <param name="biomeSeedPoints">Parameter representing the biomeSeedPoints (Vector3[]).</param>
        /// <param name="biomeTypes">Parameter representing the biomeTypes (string[]).</param>
        /// <param name="noiseBlend">Parameter representing the noiseBlend (float).</param>
        /// <returns>Returns dominantBiomeType, float (blendFactor) of type string.</returns>
        public static string Compute(Vector3 worldPos, Vector3[] biomeSeedPoints, string[] biomeTypes, float noiseBlend, Func<Vector3, Vector3, float> distanceFunc = null)
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
                    var distFn = distanceFunc ?? Vector3.Distance;
                    dist = distFn(worldPos, pt);
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
