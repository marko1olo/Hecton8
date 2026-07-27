using System;
using System.Globalization;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Answers one question about the ecosystem's geology biome mapping: does it actually
    /// discriminate, or does every sector collapse into a single lane?
    /// <para>
    /// The mapping's failure mode is silent - a degenerate field produces uniform fauna density with
    /// no error - so this reports the lane spread and the mask extremes rather than a pass/pass.
    /// It evaluates the geology field directly and needs no Play Mode, so it is cheap to run.
    /// </para>
    /// </summary>
    public static class H8_GeologyBiomeLaneProbe
    {
        private const string SeedArgument = "-h8GeologySeed";
        private const string RadiusArgument = "-h8SectorRadius";
        private const float EcosystemSectorEdgeMeters = 1000f;
        private const int DefaultSectorRadius = 16;

        /// <summary>
        /// Mirrors <c>EcosystemDirector.DefaultWaterSurfaceLevelY</c> (14.02f). The runtime prefers a
        /// live <c>ResolveWaterSurfaceLevel()</c> and only falls back to this, so the audit is exact
        /// only when the ocean service agrees with the fallback. Logged so the reader can tell.
        /// </summary>
        private const float DefaultWaterSurfaceLevelY = 14.02f;

        [MenuItem("Hecton8/Diagnostics/Geology Biome Lane Distribution")]
        public static void RunFromMenu()
        {
            Execute(0, DefaultSectorRadius);
        }

        /// <summary>Batch entry point. Invoke with -executeMethod; exits non-zero when degenerate.</summary>
        public static void Run()
        {
            int runtimeWorldSeed = ReadIntArgument(SeedArgument, 0);
            int sectorRadius = ReadIntArgument(RadiusArgument, DefaultSectorRadius);
            bool discriminating = Execute(runtimeWorldSeed, sectorRadius);
            EditorApplication.Exit(discriminating ? 0 : 1);
        }

        private static bool Execute(int runtimeWorldSeed, int sectorRadius)
        {
            float waterSurfaceY = DefaultWaterSurfaceLevelY;

            // Params built the same way the runtime builds them in
            // EcosystemDirector.TryResolveGeologyBiomeParams, so the audit sees the same field the
            // ecosystem will.
            //
            // WaterSurfaceY must be set explicitly and must NOT be left at CreateDefault's 0. Lanes
            // are now resolved from PrimaryZone, ResolveZone gates on DepthMeters, and DepthMeters is
            // max(0, WaterSurfaceY - heightMeters). Leaving it at 0 would audit a world sitting
            // 14.02 m below the one the runtime classifies and quietly report lane counts for terrain
            // that does not exist - the audit/runtime drift the delegation to ClassifyLane exists to
            // prevent.
            WorldMacroGeologyParams parameters = WorldMacroGeologyParams.CreateDefault(
                WorldMacroGeologyFields.CombineWorldSeed(
                    unchecked((uint)WorldMacroGeologyFields.DefaultAuthoringSeed),
                    runtimeWorldSeed));
            parameters.WaterSurfaceY = waterSurfaceY;

            EcosystemGeologyBiomeLanes.LaneDistribution distribution =
                EcosystemGeologyBiomeLanes.SampleLaneDistribution(
                    in parameters,
                    sectorRadius,
                    EcosystemSectorEdgeMeters);

            int edge = (sectorRadius * 2) + 1;
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_GEOLOGYLANES] grid={0}x{0} sectors edge={1}m runtimeWorldSeed={2} resolvedSeed={3} " +
                "waterSurfaceY={4} (lanes are depth-sensitive via PrimaryZone; this must match the " +
                "runtime ocean level for the counts to be exact)",
                edge,
                EcosystemSectorEdgeMeters,
                runtimeWorldSeed,
                parameters.Seed,
                waterSurfaceY));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_GEOLOGYLANES] samples={0} neutral={1} rich={2} scarce={3} nonFinite={4}",
                distribution.SampleCount,
                distribution.NeutralCount,
                distribution.RichCount,
                distribution.ScarceCount,
                distribution.NonFiniteCount));

            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_GEOLOGYLANES] maxTrenchMask={0:F4} maxShelfMask={1:F4} (informational - lanes are " +
                "resolved from PrimaryZone, not from these masks)",
                distribution.MaxTrenchMask,
                distribution.MaxShelfMask));

            // The zone breakdown is what makes the lane counts falsifiable. A rich share with zero
            // contributing shelf zones would mean the mapping is picking up something else.
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "[H8_GEOLOGYLANES] zones behind the lanes: photicShelf={0} shelfBreak={1} (-> rich) " +
                "brineTrench={2} hadalBasin={3} (-> scarce)",
                distribution.PhoticShelfCount,
                distribution.ShelfBreakCount,
                distribution.BrineTrenchCount,
                distribution.HadalBasinCount));

            bool discriminating = distribution.IsDiscriminating;
            if (!discriminating)
            {
                // Not a crash, and that is the point: without this line the ecosystem would look
                // fine while being exactly as uniform as the coordinate hash it replaced.
                Debug.LogError(
                    "[H8_GEOLOGYLANES] DEGENERATE - the mapping produced a single lane over the sampled area. " +
                    "Fauna density is uniform and the geology coupling is inert here. " +
                    "Check the zone breakdown above: if every shelf and trench zone count is 0, the " +
                    "geology field never produced a non-neutral zone over this area.");
            }
            else
            {
                Debug.Log("[H8_GEOLOGYLANES] DISCRIMINATING - more than one lane produced.");
            }

            if (distribution.NonFiniteCount > 0)
            {
                Debug.LogWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "[H8_GEOLOGYLANES] {0} sample(s) returned a non-finite mask and were classified neutral.",
                    distribution.NonFiniteCount));
            }

            Debug.Log("[H8_GEOLOGYLANES] DONE");
            return discriminating;
        }

        private static int ReadIntArgument(string argumentName, int fallbackValue)
        {
            // Fully qualified: this file sits under the Hecton8 namespace root, which contains a
            // Hecton8.Environment namespace that shadows System.Environment during name lookup.
            // Bare `Environment` here is CS0234, the same trap 86df04453 fixed in H8_ShaderCompileGate.
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (!string.Equals(arguments[i], argumentName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (int.TryParse(arguments[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    return parsed;
            }

            return fallbackValue;
        }
    }
}
