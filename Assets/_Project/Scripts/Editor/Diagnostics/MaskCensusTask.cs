using System;
using System.Globalization;
using System.IO;
using System.Text;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.Diagnostics
{
    /// <summary>
    /// Counts how much of the WORLD each macro-geology mask actually covers, all 26 of them, and
    /// reports where the shallowest ground is so a spawn point can be chosen from a measurement
    /// rather than a guess.
    ///
    /// WHY THIS EXISTS WHEN WorldMacroGeologyMaskCoverageTests ALREADY MEASURES COVERAGE. That
    /// fixture is a floor gate and covers 8 of the 26 masks (Canyon, River, Lake, Fold, Mesa,
    /// Ridge, Trench, Shelf); the remaining 18 - including Reef, Ledge, CaveEntrance, BrinePool,
    /// Volcano, Dune, Crater, Strata, HardRock, PlateEdge, Terrace, Slump - have no coverage
    /// number anywhere in the repo. A mask nobody counts is a feature nobody can prove exists.
    ///
    /// It also samples INSIDE THE WORLD. That fixture's five sites sit at 5 km, 50 km, 300 km,
    /// 777 km and -40 km, but the world is a 30 km square (WorldExtentMeters = 30000, chunks
    /// +-15 km), so four of its five probes are outside the playable world and their readings
    /// describe extrapolated field, not terrain anyone will stand on. That trap is already on
    /// record for this project; this tool does not repeat it.
    ///
    /// NO GPU REFUSAL GUARD, deliberately: this calls WorldMacroGeologyFields.EvaluateHeightMeters
    /// on the CPU and writes a text file. No RenderTexture, no Blit, no compute dispatch, nothing
    /// that returns zeros without a graphics device.
    /// </summary>
    public static class MaskCensusTask
    {
        private const uint Seed = 880031u;

        /// <summary>
        /// Half-extent of the sampled square in metres. 15 km = the world's own half-extent, so the
        /// census covers the whole playable world and nothing outside it.
        /// </summary>
        private const double WorldHalfMeters = 15000.0;

        /// <summary>
        /// 256x256 = 65536 samples over 30 km, i.e. one sample every 117 m. Coarse for a picture and
        /// ample for a coverage percentage: a feature that occupies less than one 117 m cell across
        /// the entire world is not a feature a player can find.
        /// </summary>
        private const int SamplesPerAxis = 256;

        /// <summary>
        /// A mask counts as present above this value, not merely above zero. Matches the 0.05 used by
        /// WorldMacroGeologyMaskCoverageTests so the two agree, and keeps smoothstep tails and float
        /// dust from turning a dead layer into a plausible-looking small percentage.
        /// </summary>
        private const float PresenceThreshold = 0.05f;

        private static readonly string OutputDir =
            Path.Combine(Directory.GetCurrentDirectory(), "Logs", "mask_census");

        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(OutputDir);
                DoCensus();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MaskCensusTask] FAILED, no census was written: {ex}");
                EditorApplication.Exit(2);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void DoCensus()
        {
            var p = WorldMacroGeologyParams.CreateDefault(Seed);

            string[] names =
            {
                "Shelf", "ShelfBreak", "Ridge", "Trench", "Basin", "Fault", "Crater", "Canyon",
                "HardRock", "PlateEdge", "Terrace", "Slump", "River", "Lake", "Strata", "Fold",
                "Volcano", "Mesa", "Dune", "Continentality", "Reef", "Ledge", "CaveEntrance",
                "BrinePool"
            };

            long[] present = new long[names.Length];
            double[] sum = new double[names.Length];
            float[] peak = new float[names.Length];
            long samples = 0;

            // Shallowest ground found, for the spawn decision. "Shallowest" is the metric that matters
            // because the owner spawns next to the islands: the island tops are the highest terrain in
            // a world whose floor is thousands of metres down.
            float highest = float.MinValue;
            double highestX = 0, highestZ = 0;
            int aboveWater = 0, within50mOfSurface = 0;

            // Diagnostic accumulators for the dead-mask decomposition. slopeProxy is not a mask and is
            // not returned, so it is reconstructed here from the masks that compose it
            // (WorldMacroGeologyFields.cs:1197) - the same sum, so the same number.
            float minDepth = float.MaxValue, maxDepth = float.MinValue;
            float minGateDepth = float.MaxValue, maxGateDepth = float.MinValue;
            double sumGateDepth = 0;
            long reefDepthOk = 0;
            float minSlopeProxy = float.MaxValue, maxSlopeProxy = float.MinValue;
            double sumSlopeProxy = 0;
            long slopeProxyLow = 0;
            long canyonEqualsRiver = 0;
            float minReefNoise = float.MaxValue, maxReefNoise = float.MinValue;
            double sumReefNoise = 0;
            long reefNoiseOver50 = 0;
            long reefBothOk = 0, reefOnShelfOk = 0;

            double step = (WorldHalfMeters * 2.0) / (SamplesPerAxis - 1);
            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = -WorldHalfMeters + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = -WorldHalfMeters + ix * step;

                    float h = WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);

                    float[] vals =
                    {
                        m.Shelf, m.ShelfBreak, m.Ridge, m.Trench, m.Basin, m.Fault, m.Crater, m.Canyon,
                        m.HardRock, m.PlateEdge, m.Terrace, m.Slump, m.River, m.Lake, m.Strata, m.Fold,
                        m.Volcano, m.Mesa, m.Dune, m.Continentality, m.Reef, m.Ledge, m.CaveEntrance,
                        m.BrinePool
                    };

                    for (int k = 0; k < vals.Length; k++)
                    {
                        float v = vals[k];
                        if (v > PresenceThreshold) present[k]++;
                        sum[k] += v;
                        if (v > peak[k]) peak[k] = v;
                    }

                    if (h > highest) { highest = h; highestX = x; highestZ = z; }
                    if (h > p.WaterSurfaceY) aboveWater++;
                    if (h > p.WaterSurfaceY - 50f) within50mOfSurface++;

                    // depth is what the reef gate tests, and it is the mirror of the returned height:
                    // EvaluateHeightMeters returns WaterSurfaceY - depth, so depth = WaterSurfaceY - h.
                    //
                    // BUT THE FINAL h IS THE WRONG DEPTH TO TEST THE GATE AGAINST, and reading it that way
                    // is what made the first run of this census report "93% of the world is inside the reef
                    // window" for a mask that is identically zero. The reef gate sits at
                    // WorldMacroGeologyFields.cs:1507, upstream of the soft ceiling at :1622 and the
                    // clamp(-620, HadalDepth) at :1628, so the depth it sees is not the depth that comes
                    // out. stageDump = 5 returns the height as it stands at the end of the fold-belt stage,
                    // which is the last checkpoint before the reef block - that is the number the gate uses.
                    float depthFinal = p.WaterSurfaceY - h;
                    if (depthFinal < minDepth) minDepth = depthFinal;
                    if (depthFinal > maxDepth) maxDepth = depthFinal;

                    float hAtStage5 = WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks _, 5);
                    float depthAtGate = p.WaterSurfaceY - hAtStage5;
                    if (depthAtGate < minGateDepth) minGateDepth = depthAtGate;
                    if (depthAtGate > maxGateDepth) maxGateDepth = depthAtGate;
                    sumGateDepth += depthAtGate;
                    if (depthAtGate > 20f && depthAtGate < 3500f) reefDepthOk++;

                    float slopeProxy = Mathf.Clamp01(
                        m.ShelfBreak * 0.82f + m.Ridge * 0.72f + m.Fault * 0.65f + m.PlateEdge * 0.40f);
                    if (slopeProxy < minSlopeProxy) minSlopeProxy = slopeProxy;
                    if (slopeProxy > maxSlopeProxy) maxSlopeProxy = slopeProxy;
                    sumSlopeProxy += slopeProxy;
                    if (slopeProxy < 0.35f) slopeProxyLow++;

                    if (m.Canyon == m.River) canyonEqualsRiver++;

                    // THE REEF NOISE ITSELF, called with the identical arguments the reef block uses
                    // (WorldMacroGeologyFields.cs:1512). Every other factor in the reef product has now
                    // been measured and cleared - depth gate passes at 93.2%, reefFade is 1 because every
                    // province recipe carries Reefs >= 0.10 - so this is the last candidate, and guessing
                    // at it twice already cost two runs. warpedPosD is internal, so this uses the raw
                    // position: the domain warp displaces by at most 725 m and cannot turn a varying field
                    // into a constant one, so a constant here is a constant there.
                    float reefNoise = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                        new Unity.Mathematics.double2(x * 0.0015 - 31.4, z * 0.0015 + 88.2),
                        Seed ^ 0x9E8D7C6Fu, 3);
                    if (reefNoise < minReefNoise) minReefNoise = reefNoise;
                    if (reefNoise > maxReefNoise) maxReefNoise = reefNoise;
                    sumReefNoise += reefNoise;
                    if (reefNoise > 0.50f) reefNoiseOver50++;

                    // THE PRODUCT, not the factors. Every factor of the reef mask has now measured alive
                    // on its own - depth inside the gate window 93.2%, reefNoise > 0.50 at 49.98%, recipe
                    // Reefs >= 0.10 in all nine provinces, reefFade therefore 1 - and yet the published
                    // mask is identically 0.0000. Factors being individually alive does not make their
                    // product non-zero if they never overlap, and measuring them one at a time cannot
                    // detect that. This counts the co-occurrence directly.
                    bool depthOk = depthAtGate > 20f && depthAtGate < 3500f;
                    bool patchOk = reefNoise > 0.50f;
                    if (depthOk && patchOk) reefBothOk++;
                    if (m.Shelf > PresenceThreshold && depthOk && patchOk) reefOnShelfOk++;

                    samples++;
                }
            }

            var doc = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            doc.AppendLine("MACRO-GEOLOGY MASK CENSUS");
            doc.AppendLine($"seed {Seed}, world +-{WorldHalfMeters:F0} m, {SamplesPerAxis}x{SamplesPerAxis} " +
                           $"= {samples} samples, {(WorldHalfMeters * 2.0 / (SamplesPerAxis - 1)):F0} m spacing");
            doc.AppendLine($"presence threshold {PresenceThreshold.ToString(ci)}");
            doc.AppendLine();
            doc.AppendLine($"{"mask",-16} {"coverage%",10} {"mean",8} {"peak",8}   verdict");

            int dead = 0, alive = 0;
            for (int k = 0; k < names.Length; k++)
            {
                double pct = 100.0 * present[k] / samples;
                double mean = sum[k] / samples;
                string verdict;
                if (peak[k] <= PresenceThreshold) { verdict = "DEAD - never rises above threshold anywhere"; dead++; }
                else if (pct < 0.5) { verdict = "NEARLY DEAD - under 0.5% of the world"; dead++; }
                else { verdict = "alive"; alive++; }

                doc.AppendLine(
                    $"{names[k],-16} {pct,10:F2} {mean,8:F4} {peak[k],8:F4}   {verdict}");
            }

            doc.AppendLine();
            doc.AppendLine($"SUMMARY: {alive} of {names.Length} masks alive, {dead} dead or nearly dead.");

            // WHY A DEAD MASK IS DEAD. A coverage table says Reef is 0.0000 but not which factor in the
            // product zeroed it, and a mask is always a product of several gates - so the table alone
            // sends the next reader guessing. These three are the measured dead ones, and each is
            // decomposed into the inputs its formula multiplies, sampled over the same grid.
            doc.AppendLine();
            doc.AppendLine("DEAD-MASK DECOMPOSITION (which factor is the zero):");
            doc.AppendLine($"  Reef  = reefPatch * depthGate * recipe.Reefs * reefFade");
            doc.AppendLine($"    FINAL depth (post soft-ceiling and clamp): {minDepth:F1} .. {maxDepth:F1} m " +
                           "- NOT what the gate sees, recorded only to show the two differ");
            doc.AppendLine($"    depth AT THE GATE (stageDump 5, upstream of the ceiling): " +
                           $"{minGateDepth:F1} .. {maxGateDepth:F1} m, mean {sumGateDepth / samples:F1} m");
            doc.AppendLine($"    gate window is 20..3500 m: smoothstep(4500,3500,depth)*smoothstep(-10,20,depth)");
            doc.AppendLine($"    samples with GATE depth inside the window: {reefDepthOk} " +
                           $"({100.0 * reefDepthOk / samples:F3}%)");
            doc.AppendLine($"    reefNoise (the last unmeasured factor): {minReefNoise:F6} .. {maxReefNoise:F6}, " +
                           $"mean {sumReefNoise / samples:F6}");
            doc.AppendLine($"    samples with reefNoise > 0.50 (what reefPatch needs): {reefNoiseOver50} " +
                           $"({100.0 * reefNoiseOver50 / samples:F3}%)");
            doc.AppendLine(maxReefNoise - minReefNoise < 0.0001f
                ? "    VERDICT: reefNoise is CONSTANT - the noise call itself is degenerate, so reefPatch " +
                  "can never rise and no depth or recipe change will ever produce a reef."
                : "    VERDICT: reefNoise varies, so the zero is downstream of it.");
            doc.AppendLine($"    CO-OCCURRENCE: depth-in-window AND reefNoise>0.50 together: {reefBothOk} " +
                           $"({100.0 * reefBothOk / samples:F3}%)");
            doc.AppendLine($"    ...and also on shelf: {reefOnShelfOk} ({100.0 * reefOnShelfOk / samples:F3}%)");
            doc.AppendLine(reefBothOk == 0
                ? "    VERDICT: the two conditions NEVER co-occur - that is the zero, and no single factor " +
                  "shows it."
                : "    VERDICT: the conditions DO co-occur, so a non-zero reef is reachable and the zero " +
                  "lies in a factor this census does not yet read (warped position, or recipe.Reefs " +
                  "resolving to a province with Reefs=0 exactly where the overlap happens).");
            doc.AppendLine($"  Ledge = Strata * smoothstep(0.35, 0.05, slopeProxy)  -> needs slopeProxy BELOW 0.35");
            doc.AppendLine($"    slopeProxy range: {minSlopeProxy:F4} .. {maxSlopeProxy:F4}, mean {sumSlopeProxy / samples:F4}");
            doc.AppendLine($"    samples with slopeProxy < 0.35: {slopeProxyLow} " +
                           $"({100.0 * slopeProxyLow / samples:F3}%)");
            doc.AppendLine($"  Fold  = foldAsymmetry * recipe.Folds * continentality * foldFade");
            doc.AppendLine($"    peak reached {peak[15]:F4} against threshold {PresenceThreshold.ToString(ci)} - " +
                           "computed but pinned just under visibility");
            doc.AppendLine();
            doc.AppendLine("CANYON/RIVER DUPLICATION CHECK:");
            doc.AppendLine($"  identical samples: {canyonEqualsRiver} of {samples} " +
                           $"({100.0 * canyonEqualsRiver / samples:F3}%)");
            doc.AppendLine(canyonEqualsRiver == samples
                ? "  VERDICT: Canyon and River are the SAME VALUE at every sample - one is assigned from the other."
                : "  VERDICT: Canyon and River differ somewhere, so they are not a straight copy.");
            doc.AppendLine();
            doc.AppendLine("SPAWN CANDIDATE (highest ground = island tops, which is what the owner spawns near):");
            doc.AppendLine($"  highest terrain {highest:F1} m at ({highestX:F0}, {highestZ:F0})");
            doc.AppendLine($"  water surface   {p.WaterSurfaceY:F1} m");
            doc.AppendLine($"  samples above water:        {aboveWater} ({100.0 * aboveWater / samples:F3}% of world)");
            doc.AppendLine($"  samples within 50 m of surface: {within50mOfSurface} " +
                           $"({100.0 * within50mOfSurface / samples:F3}%)");
            if (aboveWater == 0)
                doc.AppendLine("  NOTE: no sample broke the surface, so this world has no emergent islands at " +
                               "this sampling density - the 'nearest land' spawn rule has nothing to anchor to.");

            string reportPath = Path.Combine(OutputDir, "mask_census.txt");
            File.WriteAllText(reportPath, doc.ToString(), Encoding.UTF8);
            Debug.Log($"[MaskCensusTask] wrote {reportPath}\n{doc}");
        }
    }
}
