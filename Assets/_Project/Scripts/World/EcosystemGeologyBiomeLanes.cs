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

        /// <summary>Counts of each lane over a sampled sector grid, plus the mask extremes and the
        /// specific zones that produced the two non-neutral lanes.</summary>
        public struct LaneDistribution
        {
            public int SampleCount;
            public int NeutralCount;
            public int RichCount;
            public int ScarceCount;
            public float MaxTrenchMask;
            public float MaxShelfMask;
            public int NonFiniteCount;

            /// <summary>Zone breakdown behind the non-neutral lanes. Without this the lane counts are
            /// unfalsifiable: a plausible-looking rich share cannot be told apart from a threshold
            /// accident, which is exactly how the previous mask-threshold mapping went unchallenged.</summary>
            public int PhoticShelfCount;
            public int ShelfBreakCount;
            public int BrineTrenchCount;
            public int HadalBasinCount;

            /// <summary>
            /// True when the mapping produced more than one lane. A single-lane result means the
            /// classification is degenerate over the sampled area and fauna density is once again
            /// uniform, which is the exact regression this mapping was written to remove.
            /// </summary>
            public bool IsDiscriminating =>
                (NeutralCount > 0 ? 1 : 0) + (RichCount > 0 ? 1 : 0) + (ScarceCount > 0 ? 1 : 0) > 1;
        }

        /// <summary>
        /// Classifies one macro geology sample by the zone the geology field already resolved.
        /// <para>
        /// This delegates to <see cref="WorldMacroGeologyFields.ResolveZone"/> via
        /// <see cref="WorldMacroGeologySample.PrimaryZone"/> rather than testing masks itself. The
        /// first version of this method compared <c>ShelfMask &gt; 0.5</c> and
        /// <c>TrenchMask &gt; 0.8</c>, which looked reasonable and was wrong on two counts: the
        /// authority requires <c>ShelfMask &gt; 0.68</c> AND <c>DepthMeters &lt; 260</c> for a photic
        /// shelf, and it resolves a shelf break from <c>ShelfBreakMask</c> - a different field
        /// entirely. Being depth-blind, the old test labelled 700 m abyssal terrain a rich photic
        /// shelf and reported half the world as rich, which no compile gate could catch because the
        /// number looked plausible.
        /// </para>
        /// <para>
        /// Non-finite masks need no guard here: every comparison inside <c>ResolveZone</c> is false
        /// for NaN, so a degenerate sample falls through to <c>AbyssalPlain</c>, and
        /// <c>Unknown</c> (an unsanitised params bail-out) maps to the neutral lane. Both are the
        /// safe direction - no carrying-capacity bias.
        /// </para>
        /// </summary>
        public static int ClassifyLane(in WorldMacroGeologySample sample)
        {
            switch (sample.PrimaryZone)
            {
                // Photic shelf and shelf break: sunlit or upwelling-fed, dense kelp, schooling prey.
                case WorldMacroGeologyZone.PhoticShelf:
                case WorldMacroGeologyZone.ShelfBreak:
                    return LaneRich;

                // Trench and hadal basin: thin food column, pressure-adapted hunters only.
                case WorldMacroGeologyZone.BrineTrench:
                case WorldMacroGeologyZone.HadalBasin:
                    return LaneScarce;

                // AbyssalPlain, SedimentFan, ColdSeepField, FaultRidge and Unknown stay neutral.
                // ColdSeepField is deliberately NOT rich: it is chemosynthetically productive in
                // reality, but promoting it would change fauna density on ~20% of sampled world
                // without any ecology authority asking for it. That is a design call, not a bug fix.
                default:
                    return LaneNeutral;
            }
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

                    switch (sample.PrimaryZone)
                    {
                        case WorldMacroGeologyZone.PhoticShelf:
                            distribution.PhoticShelfCount++;
                            break;
                        case WorldMacroGeologyZone.ShelfBreak:
                            distribution.ShelfBreakCount++;
                            break;
                        case WorldMacroGeologyZone.BrineTrench:
                            distribution.BrineTrenchCount++;
                            break;
                        case WorldMacroGeologyZone.HadalBasin:
                            distribution.HadalBasinCount++;
                            break;
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
