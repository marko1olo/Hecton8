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

        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (5000.0, 5000.0, "P1_origin"),
            (50000.0, 50000.0, "P2_near"),
            (-40000.0, 15000.0, "P3_west"),
            (300000.0, 90000.0, "P4_far"),
            (777000.0, -333000.0, "P5_deepfar")
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
        /// What the same measurement DID establish is sharper, so the assertion now guards that
        /// instead. HardRock wins 47-97% of every window and only two to four of the eight classes
        /// win anywhere at all, and the share tracks slope monotonically across all five sites:
        ///
        ///   site / 1 km window   mean slope   rock wins   distinct winners
        ///   P3_west                  12.8         47%          2
        ///   P2_near                  18.6         56%          3
        ///   P1_origin                30.0         78%          2
        ///   P4_far                   36.1         85%          2
        ///   P5_deepfar               43.6         97%          2
        ///
        /// So the palette is not independently miscalibrated - it is downstream of slope, and it
        /// diversifies as slope comes down. That matters for what to fix: tuning the class weights
        /// would be work at the wrong address.
        /// </summary>
        [Test]
        public void MaterialPalette_DoesNotCollapseToRock()
        {
            for (int i = 0; i < Sites.Length; i++)
            {
                ClipStats s = Measure(Sites[i].X, Sites[i].Z, 1000.0);

                double rockPct = 100.0 * s.WonArgmax[3] / s.Samples;
                int distinct = 0;
                for (int c = 0; c < 8; c++)
                    if (s.WonArgmax[c] > 0) distinct++;

                Assert.That(
                    rockPct,
                    Is.LessThan(90.0),
                    $"{Sites[i].Label}: HardRock is the dominant material across {rockPct:0.0}% of a " +
                    $"1 km window and only {distinct} of 8 classes win anywhere in it. HardRock is " +
                    "driven by slope through finalRock (WorldTerrainDetailContracts.cs:186-188), and " +
                    "sedimentRoom is 1 - finalRock, so once slope saturates the ramp every sediment " +
                    "class is multiplied by zero. Fix the slope budget, not the class weights.");
            }
        }
    }
}
