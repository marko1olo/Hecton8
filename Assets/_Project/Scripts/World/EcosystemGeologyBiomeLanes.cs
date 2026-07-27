using Unity.Mathematics;

namespace Hecton8.World
{
    /// <summary>
    /// Canonical mapping from a macro geology sample onto the ecosystem's three biome lanes, plus a
    /// read-only distribution audit over a sector grid.
    /// <para>
    /// This exists as a separate unit for one reason: the mapping is the load-bearing part of keying
    /// fauna to real terrain, and its dominant failure mode is silent. If the geology field never
    /// produces a mask above the thresholds, every sector collapses to <see cref="LaneNeutral"/>, the
    /// ecosystem looks exactly as uniform as the coordinate hash it replaced, and nothing errors.
    /// <see cref="SampleLaneDistribution"/> makes that failure observable instead of invisible.
    /// </para>
    /// </summary>
    public static class EcosystemGeologyBiomeLanes
    {
        /// <summary>Neutral lane. No carrying-capacity bias.</summary>
        public const int LaneNeutral = 0;

        /// <summary>Rich lane: photic shelf, dense kelp, schooling prey. +0.08 carrying capacity.</summary>
        public const int LaneRich = 1;

        /// <summary>Scarce lane: abyssal trench, thin food column, pressure-adapted hunters. -0.05 carrying capacity.</summary>
        public const int LaneScarce = 2;

        /// <summary>Trench mask above which a sector is classified as the scarce lane.</summary>
        public const float TrenchMaskThreshold = 0.8f;

        /// <summary>Shelf mask above which a sector is classified as the rich lane.</summary>
        public const float ShelfMaskThreshold = 0.5f;

        /// <summary>Counts of each lane over a sampled sector grid, plus the mask extremes that produced them.</summary>
        public struct LaneDistribution
        {
            public int SampleCount;
            public int NeutralCount;
            public int RichCount;
            public int ScarceCount;
            public float MaxTrenchMask;
            public float MaxShelfMask;
            public int NonFiniteCount;

            /// <summary>
            /// True when the mapping produced more than one lane. A single-lane result means the
            /// classification is degenerate over the sampled area and fauna density is once again
            /// uniform, which is the exact regression this mapping was written to remove.
            /// </summary>
            public bool IsDiscriminating =>
                (NeutralCount > 0 ? 1 : 0) + (RichCount > 0 ? 1 : 0) + (ScarceCount > 0 ? 1 : 0) > 1;
        }

        /// <summary>
        /// Classifies one macro geology sample. Trench wins where both masks overlap: it is the
        /// stronger structure, and a trench cutting through a shelf is still a trench.
        /// </summary>
        public static int ClassifyLane(in WorldMacroGeologySample sample)
        {
            float trenchMask = math.select(0f, sample.TrenchMask, math.isfinite(sample.TrenchMask));
            float shelfMask = math.select(0f, sample.ShelfMask, math.isfinite(sample.ShelfMask));

            if (trenchMask > TrenchMaskThreshold)
                return LaneScarce;

            if (shelfMask > ShelfMaskThreshold)
                return LaneRich;

            return LaneNeutral;
        }

        /// <summary>
        /// Evaluates the geology field at the centre of every sector in a square grid and reports the
        /// resulting lane distribution. Read-only and allocation-free; intended for diagnostics, not
        /// for the runtime path, because it runs the full macro geology stack once per sector.
        /// </summary>
        /// <param name="parameters">Geology parameters, built the same way the runtime builds them.</param>
        /// <param name="sectorRadius">Half-width of the sampled grid in sectors. 16 samples 33x33.</param>
        /// <param name="sectorEdgeMeters">Sector edge length, matching the ecosystem sector grid.</param>
        /// <param name="originSectorX">Sector-space X centre of the sampled grid.</param>
        /// <param name="originSectorZ">Sector-space Z centre of the sampled grid.</param>
        public static LaneDistribution SampleLaneDistribution(
            in WorldMacroGeologyParams parameters,
            int sectorRadius,
            float sectorEdgeMeters,
            int originSectorX = 0,
            int originSectorZ = 0)
        {
            LaneDistribution distribution = default;
            int safeRadius = math.clamp(sectorRadius, 0, 512);
            float safeEdge = math.max(1f, sectorEdgeMeters);

            for (int z = -safeRadius; z <= safeRadius; z++)
            {
                for (int x = -safeRadius; x <= safeRadius; x++)
                {
                    // Sector centre in absolute world metres, in double for the same reason the
                    // runtime uses double: a float32 centre loses whole-metre resolution at the
                    // 777 km AUP range and would smear the classification across sector borders.
                    double centerX = ((double)(originSectorX + x) + 0.5d) * safeEdge;
                    double centerZ = ((double)(originSectorZ + z) + 0.5d) * safeEdge;
                    WorldMacroGeologySample sample = WorldMacroGeologyFields.Evaluate(centerX, centerZ, in parameters);

                    bool masksFinite = math.isfinite(sample.TrenchMask) && math.isfinite(sample.ShelfMask);
                    if (!masksFinite)
                        distribution.NonFiniteCount++;
                    else
                    {
                        distribution.MaxTrenchMask = math.max(distribution.MaxTrenchMask, sample.TrenchMask);
                        distribution.MaxShelfMask = math.max(distribution.MaxShelfMask, sample.ShelfMask);
                    }

                    switch (ClassifyLane(in sample))
                    {
                        case LaneRich:
                            distribution.RichCount++;
                            break;
                        case LaneScarce:
                            distribution.ScarceCount++;
                            break;
                        default:
                            distribution.NeutralCount++;
                            break;
                    }

                    distribution.SampleCount++;
                }
            }

            return distribution;
        }
    }
}
