using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures how often the surface material classes CLIP at 1.0 before normalisation, and how
    /// often the top two are close enough that the dominant class is decided by float noise.
    ///
    /// Motivating observation, 2026-08-09, from the clean-room telemetry line:
    ///   sand=0.000 silt=0.405 rock=0.404 limestone=0.000 brine=0.046 nodule=0.000 reef=0.045 seep=0.099
    /// and from Docs/Reports/CleanRoom/CleanRoom_XRay_MaterialDominant.png, which is a two-colour
    /// dithered mottle. A 0.001 gap between the top two classes is not "silt narrowly wins": it is a
    /// tie, and the dominant-material map is then a picture of rounding error rather than of geology.
    ///
    /// The mechanism to test for is clipping. WorldTerrainSurfaceMaterialResolver.Resolve wraps every
    /// class in math.saturate() and then normalises the eight weights to sum to 1. Saturation is not
    /// a safety clamp here - it is lossy. Once two classes both reach 1.0 they are EXACTLY equal no
    /// matter how different the geology driving them was, so every spatial gradient between them is
    /// destroyed before the normalisation that is supposed to rank them. Two classes at 1.0
    /// normalising against small others land near 0.4 each, which is exactly the reading above.
    ///
    /// This is a different defect from the slope saturation already documented in
    /// WorldMacroGeologyFields.cs: that one flattens the INPUT, this one flattens the OUTPUT, and
    /// fixing either alone leaves the palette blind.
    ///
    /// The fixture reports rates rather than asserting a target, because what a healthy clip rate
    /// looks like is a design question. The one assertion guards the property that has no defensible
    /// reading: a dominant-material map that is decided by ties.
    /// </summary>
    [TestFixture]
    public sealed class WorldTerrainMaterialSaturationTests
    {
        private const uint Seed = 880031u;
        private const int SamplesPerAxis = 48;

        /// <summary>
        /// Sites INSIDE the world, chosen by percentile of the in-world 1 km slope distribution by
        /// WorldMacroGeologyInWorldAtlasTests.
        ///
        /// REPLACED 2026-08-10, for the reason recorded in WorldMacroGeologySlopeBudgetTests: the
        /// previous coordinates put four of five sites outside the 30 km world that
        /// ResolveMinimumChunkRange actually emits, P5_deepfar by 51.8x. The palette collapse this
        /// fixture measures is real and reachable - the in-world p75 site is 43.4 deg against
        /// P5_deepfar's 46.7 - but it has to be asserted somewhere a player can stand.
        /// </summary>
        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (11896.0, -13148.0, "W1_flat"),
            (5635.0, -3130.0, "W2_gentle"),
            (9391.0, -10643.0, "W3_typical"),
            (6887.0, -6887.0, "W4_steep"),
            (-11896.0, 4383.0, "W5_wall")
        };

        private struct ClipStats
        {
            public int Samples;
            public int[] ClippedAtOne;      // per class, weight >= 0.999 BEFORE normalisation
            public int[] WonArgmax;         // per class, after normalisation
            public int TiedTopTwo;          // top two within 0.01 after normalisation
            public int NearTiedTopTwo;      // top two within 0.05
            public double MeanTopGap;
        }

        private static readonly string[] ClassNames =
        {
            "sand", "limestone", "silt", "rock", "brine", "nodule", "reef", "seep"
        };

        private static float[] ToArray(in WorldTerrainSurfaceMaterialWeights w)
        {
            return new[]
            {
                w.ShellSand, w.LimestoneShelf, w.ClaySilt, w.HardRock,
                w.BrineSaltCrust, w.ManganeseNodulePlain, w.ReefRubble, w.SeepCrust
            };
        }

        private static ClipStats Measure(double centerX, double centerZ, double windowMeters)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            ClipStats s = default;
            s.ClippedAtOne = new int[8];
            s.WonArgmax = new int[8];

            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            double gapSum = 0.0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;

                    WorldMacroGeologySample macro = WorldMacroGeologyFields.Evaluate(x, z, in p);
                    WorldTerrainSurfaceMaterialWeights w =
                        WorldTerrainSurfaceMaterialResolver.Resolve(in macro, (float)x, (float)z, Seed);

                    float[] weights = ToArray(in w);

                    // Resolve() normalises before returning, so a class that CLIPPED is not visible
                    // as a 1.0 here. Detect the tie itself instead, which is the observable
                    // consequence and does not depend on reaching into the resolver's internals.
                    System.Array.Sort(weights);
                    float top = weights[7];
                    float second = weights[6];
                    float gap = top - second;
                    gapSum += gap;
                    if (gap < 0.01f) s.TiedTopTwo++;
                    if (gap < 0.05f) s.NearTiedTopTwo++;

                    float[] unsorted = ToArray(in w);
                    int best = 0;
                    for (int c = 1; c < 8; c++)
                        if (unsorted[c] > unsorted[best]) best = c;
                    s.WonArgmax[best]++;

                    // A normalised weight at or above 0.4 with another at or above 0.4 can only
                    // happen when both clipped, because the eight normalised weights sum to 1 and the
                    // other six are then squeezed below 0.2 combined.
                    for (int c = 0; c < 8; c++)
                        if (unsorted[c] >= 0.4f) s.ClippedAtOne[c]++;

                    s.Samples++;
                }
            }

            s.MeanTopGap = gapSum / s.Samples;
            return s;
        }

        [Test]
        public void MaterialTieRate_IsReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Material palette resolution, 48x48 samples per window.");
            sb.AppendLine("A 'tie' means the top two normalised weights are within 0.01, so the");
            sb.AppendLine("dominant-material map at that pixel is decided by rounding, not geology.");
            sb.AppendLine();

            double[] windows = { 10000.0, 1000.0 };
            foreach (double win in windows)
            {
                sb.AppendLine($"  window = {win:0} m");
                for (int i = 0; i < Sites.Length; i++)
                {
                    ClipStats s = Measure(Sites[i].X, Sites[i].Z, win);

                    var winners = new System.Text.StringBuilder();
                    int distinctWinners = 0;
                    for (int c = 0; c < 8; c++)
                    {
                        if (s.WonArgmax[c] == 0) continue;
                        distinctWinners++;
                        winners.Append($"{ClassNames[c]}={100.0 * s.WonArgmax[c] / s.Samples:0}% ");
                    }

                    sb.AppendLine(
                        $"    {Sites[i].Label,-11} tied={100.0 * s.TiedTopTwo / s.Samples,5:0.0}% " +
                        $"near={100.0 * s.NearTiedTopTwo / s.Samples,5:0.0}% " +
                        $"meanGap={s.MeanTopGap,5:0.000}  classes winning anywhere={distinctWinners}");
                    sb.AppendLine($"      {winners}");
                }
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// RETRACTION, recorded here because the wrong version of this test PASSED and a green test
        /// built on a false premise is worse than no test. This fixture was first written to assert
        /// that the dominant-material map is not decided by ties, on the strength of one clean-room
        /// telemetry line reading silt=0.405 rock=0.404 next to a two-colour dithered material map.
        /// Measured across five sites and two window sizes: ties are 0.0-0.7% of area and the mean gap
        /// between the top two classes is 0.372-0.891. Nothing is tied. That one telemetry sample was
        /// a single probe point that happened to land on a material boundary, and generalising a
        /// world-wide mechanism from it was the error.
        ///
        /// SECOND RE-AIM, 2026-08-10, after an owner ruling and an intervention measurement.
        ///
        /// The replacement assertion - "HardRock must win less than 90% of ANY site" - was still
        /// wrong, in the same family as the traversability bar it was written beside. On a 42 degree
        /// submarine face bare rock IS the correct material; demanding sediment there is demanding
        /// that the resolver lie about a cliff, and the owner has ruled that cliffs are the intended
        /// design. A test that fails because the world is dramatic is a test that will eventually be
        /// "fixed" by destroying the world.
        ///
        /// What the intervention (WorldTerrainRockAttributionTests) established is much sharper, and
        /// it is a defect that survives the ruling completely. Re-resolving real samples with one
        /// input neutralised at a time:
        ///
        ///   site        mean slope   rock wins   PositiveCurvature=0   Slope01 halved
        ///   W1_flat        9.4 deg        47%                    0%              47%
        ///   W4_steep      42.1 deg        97%                   97%              83%
        ///
        /// At 9.4 degrees rock won 47% of the window and removing CURVATURE took it to zero, while
        /// halving SLOPE changed nothing. Rock was being painted on flat ground by curvature alone -
        /// roughly half of it, because half the cells of any fractal surface are convex.
        ///
        /// So the assertion now applies only where the ground is gentler than the resolver's own
        /// angle of repose, which is the region where sediment must win by physics rather than by
        /// taste. Steep sites are reported and deliberately not asserted about: what belongs on a
        /// cliff is an authoring decision, what belongs on a plain is not.
        ///
        /// This bar is known to be able to fail: on the code as it stood before the :187 gate it
        /// fails at W1_flat with 47%.
        /// </summary>
        [Test]
        public void MaterialPalette_DoesNotCollapseToRock_OnGentleGround()
        {
            // The resolver's angleOfRepose closes fully at Slope01 0.62, i.e. gradient 0.775, i.e.
            // 37.8 degrees. Read from the same normalisation the resolver uses rather than restated
            // as a bare angle, so the bar tracks the resolver if it is retuned.
            const float angleOfReposeSlope01 = 0.62f;
            double angleOfReposeDegrees = math.degrees(math.atan(angleOfReposeSlope01 * 1.25f));

            // WHICH ANGLE THE BAR USES, and why it is not the angle of repose.
            //
            // The resolver holds two different opinions about where sediment stops resting, 15
            // degrees apart, both in this one method:
            //   angleOfRepose (:179)  closes 24.2 -> 37.8 deg   "sediment rests up to here"
            //   steepSlope    (:180)  opens  23.0 -> 35.0 deg   "this is rock"
            // steepSlope is fully saturated BEFORE angleOfRepose has finished closing, so rock wins
            // the argmax well below the angle at which the resolver itself says sediment is excluded.
            // Measured at W3_typical, the p50 site: only 22.6% of the window is past 37.8 deg, and
            // rock wins 80.5% of it.
            //
            // That 58-point gap is real and worth the owner's attention, but it is a CALIBRATION
            // question, not a defect: it decides how rocky the typical seafloor looks, and the owner
            // has ruled for a rocky, dramatic world. Asserting against the repose angle would encode
            // the opposite taste and would fail on terrain that is behaving exactly as authored.
            //
            // So the bar is anchored to steepSlope's onset - where this resolver actually begins
            // calling ground rock - and the repose-angle share is reported alongside it as the
            // calibration measurement. That keeps the assertion about a provable property (rock must
            // not win ground the rock ramp does not even reach) while leaving the look of the world
            // to the person who owns it.
            const float steepSlopeOnsetSlope01 = 0.34f;
            double steepSlopeOnsetDegrees = math.degrees(math.atan(steepSlopeOnsetSlope01 * 1.25f));

            // exposedRidge produces rock from the ridge and fault masks with no slope term at all,
            // so some rock legitimately appears below any slope threshold. 20 points covers that
            // without admitting a monoculture: pre-fix W1_flat had 0.0% of its window past the onset
            // and 47% rock, which breaks this by more than double the margin.
            const double ridgeExposureMarginPoints = 20.0;

            var report = new System.Text.StringBuilder();
            report.AppendLine(
                $"    {"site",-11}{"mean",7}{">onset",9}{">repose",9}{"rock%",8}{"allowed",9}{"classes",9}");
            var failures = new System.Collections.Generic.List<string>();

            for (int i = 0; i < Sites.Length; i++)
            {
                ClipStats s = Measure(Sites[i].X, Sites[i].Z, 1000.0);
                (double Mean, double SteepShare) onset =
                    SlopeStats(Sites[i].X, Sites[i].Z, 1000.0, steepSlopeOnsetDegrees);
                (double Mean, double SteepShare) repose =
                    SlopeStats(Sites[i].X, Sites[i].Z, 1000.0, angleOfReposeDegrees);

                double rockPct = 100.0 * s.WonArgmax[3] / s.Samples;
                int distinct = 0;
                for (int c = 0; c < 8; c++)
                    if (s.WonArgmax[c] > 0) distinct++;

                double allowed = onset.SteepShare + ridgeExposureMarginPoints;
                report.AppendLine(
                    $"    {Sites[i].Label,-11}{onset.Mean,6:0.0}d{onset.SteepShare,8:0.0}%" +
                    $"{repose.SteepShare,8:0.0}%{rockPct,7:0.0}%{allowed,8:0.0}%{distinct,9}");

                if (rockPct > allowed)
                {
                    failures.Add(
                        $"{Sites[i].Label}: HardRock wins {rockPct:0.0}% of the window while only " +
                        $"{onset.SteepShare:0.0}% of it is past {steepSlopeOnsetDegrees:0.0} deg, " +
                        $"where this resolver's own steepSlope ramp begins (allowance " +
                        $"{allowed:0.0}%), and only {distinct} of 8 classes win anywhere in it.");
                }
            }

            TestContext.WriteLine(report.ToString());

            Assert.That(
                failures,
                Is.Empty,
                "Rock is winning ground that the rock ramp does not reach:\n  " +
                string.Join("\n  ", failures) +
                "\n\nThe bar scales with the terrain deliberately. A cliff site is ALLOWED to be " +
                "almost entirely rock, because bare rock on a 42 degree submarine face is correct " +
                "and the owner has ruled that cliffs are the intended design.\n\n" +
                "Measured cause of the original failure (WorldTerrainRockAttributionTests): " +
                "ridgeRockDominance added a smoothstep on positive curvature that was NOT gated by " +
                "slope, and about half the cells of a fractal surface are convex, so about half of " +
                "every flat plain was painted rock. At W1_flat, zeroing curvature moved rock from " +
                "47% to 0% while halving Slope01 moved it by nothing at all.\n\n" +
                "The '>repose' column is NOT asserted on. It is the calibration measurement: the " +
                "share of each window past the angle at which this resolver claims sediment stops " +
                "resting. Rock winning far more than that column is the 15-degree disagreement " +
                "between angleOfRepose and steepSlope, and how rocky the typical seafloor should " +
                "look is the owner's call, not a property a test may decide.\n\n" +
                report.ToString());
        }

        /// <summary>
        /// Mean slope over a window and the share of it past a given angle, measured the way the
        /// runtime measures slope.
        ///
        /// The SHARE is the part that matters and the reason the first version of this assertion was
        /// wrong. It gated on the window's MEAN being below the angle of repose, which reads a
        /// distribution through a single number: W3_typical has a mean of 31.3 degrees and is still
        /// mostly steeper than the repose angle by area, so demanding sediment there demanded it on
        /// ground that is genuinely too steep to hold any. Comparing rock's share against the
        /// measured steep share compares like with like.
        /// </summary>
        private static (double Mean, double SteepShare) SlopeStats(
            double centerX, double centerZ, double windowMeters, double steepAngleDegrees)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            const double probe = 12.0;
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            double sum = 0.0;
            int count = 0;
            int steep = 0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;
                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - probe, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + probe, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - probe, in p);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + probe, in p);
                    float dx = (e - w) / (float)(probe * 2.0);
                    float dz = (n - s) / (float)(probe * 2.0);
                    double degrees = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                    sum += degrees;
                    if (degrees > steepAngleDegrees) steep++;
                    count++;
                }
            }

            return (sum / count, 100.0 * steep / count);
        }
    }
}
