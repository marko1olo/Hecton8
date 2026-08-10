using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures the SLOPE distribution of the generated seafloor, because slope is the hinge that
    /// decides three separate things at once and nobody was measuring it:
    ///
    ///   1. Traversability. A submarine or diver moves along the floor; a floor that is mostly cliff
    ///      is not a floor.
    ///   2. The material palette. WorldTerrainSurfaceMaterialResolver derives `finalRock` from slope
    ///      and then multiplies every sediment class by `sedimentRoom = 1 - finalRock`
    ///      (WorldTerrainDetailContracts.cs:209). Where finalRock saturates, SEVEN of the eight
    ///      classes are multiplied by zero - so a too-steep world produces a two-colour splatmap no
    ///      matter how the weights are authored.
    ///   3. Readability of the X-Ray cards. terrain.md's acceptance rows ask for dendritic drainage
    ///      and scree grit; both are sub-metre signals that are invisible when the underlying surface
    ///      is already at the limit of the slope ramp.
    ///
    /// Motivating measurement (Logs/geology_atlas/atlas_report.txt, 2026-08-09): the 40-70 degree
    /// band holds 53.8% of P1_origin_10km, 66.4% of P1_origin_200m, 70.4% of P4_far_1km, 76.9% of
    /// P5_deepfar_1km and 81.1% of P5_deepfar_200m. P1_origin_200m spans 239.2 m of relief across a
    /// 200 m window.
    ///
    /// Note on the ramp: EvaluateDifferentials (WorldMacroGeologyFields.cs:1402) normalises with
    /// `Slope01 = saturate(slope / 1.25f)`, and a gradient of 1.25 is 51.3 degrees. Every surface
    /// steeper than that is indistinguishable downstream. That is why this test reports RAW degrees
    /// from the gradient and not Slope01 - Slope01 cannot see the top of the distribution.
    ///
    /// These tests assert loose CEILINGS, not targets. They are deliberately far looser than
    /// anything that could be called good terrain, so they only fire when the surface is
    /// pathologically steep, and so tuning cannot break them by accident.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologySlopeBudgetTests
    {
        private const uint Seed = 880031u;

        /// <summary>32x32 = 1024 samples per site. Each sample costs 5 height evaluations.</summary>
        private const int SamplesPerAxis = 32;

        /// <summary>
        /// Sites INSIDE the world, chosen by percentile of the in-world 1 km slope distribution by
        /// WorldMacroGeologyInWorldAtlasTests rather than by hand.
        ///
        /// REPLACED 2026-08-10. The previous list was P1_origin (5000, 5000), P2_near (50000, 50000),
        /// P3_west (-40000, 15000), P4_far (300000, 90000) and P5_deepfar (777000, -333000).
        /// WorldExtentMeters is 30000 and is overridden by no scene, prefab or asset in the project,
        /// and ResolveMinimumChunkRange bounds the chunk grid to +/-15000 m, so four of those five were
        /// outside the world - P5_deepfar by a factor of 51.8. Every assertion this fixture made about
        /// P4 and P5 was an assertion about terrain the game will never emit.
        ///
        /// The old list was accidentally representative in SPREAD (12.8 to 46.7 deg against the real
        /// world's 7.6 to 63.0), which is why its conclusions about the shape of the problem held up.
        /// It was wrong about location, not about steepness. The in-world p75 measures 43.4 deg, so the
        /// defect P5_deepfar was reporting is reachable - it just needed a real address.
        /// </summary>
        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (11896.0, -13148.0, "W1_flat"),
            (5635.0, -3130.0, "W2_gentle"),
            (9391.0, -10643.0, "W3_typical"),
            (6887.0, -6887.0, "W4_steep"),
            (-11896.0, 4383.0, "W5_wall")
        };

        private struct SlopeHistogram
        {
            public int Under10;
            public int Under25;
            public int Under35;
            public int Under50;
            public int Over50;
            public int Samples;
            public double MeanDegrees;
            public double MaxDegrees;
            public float MinHeight;
            public float MaxHeight;

            public double PctUnder(int band)
            {
                int count = band switch
                {
                    10 => Under10,
                    25 => Under10 + Under25,
                    35 => Under10 + Under25 + Under35,
                    _ => Under10 + Under25 + Under35 + Under50
                };
                return 100.0 * count / Samples;
            }
        }

        /// <summary>
        /// Slope is measured through the same central-difference path the runtime uses, so the number
        /// here is the number the material resolver and the terrain collider see. The 12 m probe
        /// matches WorldMacroGeologyFields.cs:1388.
        /// </summary>
        private static SlopeHistogram MeasureSlope(double centerX, double centerZ, double windowMeters)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            SlopeHistogram h = default;
            h.MinHeight = float.MaxValue;
            h.MaxHeight = float.MinValue;

            const double probe = 12.0;
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            double sumDegrees = 0.0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;

                    float center = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                    float west = WorldMacroGeologyFields.EvaluateHeightMeters(x - probe, z, in p);
                    float east = WorldMacroGeologyFields.EvaluateHeightMeters(x + probe, z, in p);
                    float south = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - probe, in p);
                    float north = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + probe, in p);

                    float dx = (east - west) / (float)(probe * 2.0);
                    float dz = (north - south) / (float)(probe * 2.0);
                    float gradient = math.sqrt(dx * dx + dz * dz);
                    double degrees = math.degrees(math.atan(gradient));

                    if (degrees < 10.0) h.Under10++;
                    else if (degrees < 25.0) h.Under25++;
                    else if (degrees < 35.0) h.Under35++;
                    else if (degrees < 50.0) h.Under50++;
                    else h.Over50++;

                    sumDegrees += degrees;
                    if (degrees > h.MaxDegrees) h.MaxDegrees = degrees;
                    if (center < h.MinHeight) h.MinHeight = center;
                    if (center > h.MaxHeight) h.MaxHeight = center;
                    h.Samples++;
                }
            }

            h.MeanDegrees = sumDegrees / h.Samples;
            return h;
        }

        /// <summary>
        /// The dominant reading, printed for every site and scale in one place. This test does not
        /// assert - it exists so that the slope profile of the world is a table in the test log
        /// rather than something that has to be inferred from a picture. The assertions live in the
        /// tests below.
        /// </summary>
        [Test]
        public void SlopeProfile_IsReported()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("Slope distribution (raw degrees from central-difference gradient, 12 m probe):");

            double[] scales = { 10000.0, 1000.0, 200.0 };
            for (int s = 0; s < Sites.Length; s++)
            {
                for (int k = 0; k < scales.Length; k++)
                {
                    SlopeHistogram h = MeasureSlope(Sites[s].X, Sites[s].Z, scales[k]);
                    report.AppendLine(
                        $"  {Sites[s].Label,-10} {scales[k],7:0}m  " +
                        $"<10={100.0 * h.Under10 / h.Samples,5:0.0}% " +
                        $"10-25={100.0 * h.Under25 / h.Samples,5:0.0}% " +
                        $"25-35={100.0 * h.Under35 / h.Samples,5:0.0}% " +
                        $"35-50={100.0 * h.Under50 / h.Samples,5:0.0}% " +
                        $">50={100.0 * h.Over50 / h.Samples,5:0.0}%  " +
                        $"mean={h.MeanDegrees,5:0.0} max={h.MaxDegrees,5:0.0}  " +
                        $"relief={h.MaxHeight - h.MinHeight,7:0.0}m");
                }
            }

            TestContext.WriteLine(report.ToString());
            Assert.Pass(report.ToString());
        }

        /// <summary>
        /// Some part of the seafloor must be gentle enough to be a floor. 15% under 25 degrees is a
        /// very low bar - it permits a world that is 85% mountainside - and it exists to catch the
        /// case where the surface has no traversable ground anywhere.
        ///
        /// Asserted per site, because a world where the flat ground all lives in one province is a
        /// different (and worse) failure than a world with no flat ground at all, and only a per-site
        /// assertion can tell them apart.
        /// </summary>
        [Test]
        public void EverySite_HasSomeTraversableGround()
        {
            for (int i = 0; i < Sites.Length; i++)
            {
                SlopeHistogram h = MeasureSlope(Sites[i].X, Sites[i].Z, 1000.0);
                double gentle = h.PctUnder(25);

                Assert.That(
                    gentle,
                    Is.GreaterThan(15.0),
                    $"{Sites[i].Label} has only {gentle:0.0}% of its 1 km window under 25 degrees " +
                    $"(mean {h.MeanDegrees:0.0}deg, max {h.MaxDegrees:0.0}deg, " +
                    $"relief {h.MaxHeight - h.MinHeight:0.0}m). A seafloor that steep is not " +
                    "traversable, and it also collapses the material palette: " +
                    "WorldTerrainDetailContracts.cs:209 multiplies every sediment class by " +
                    "(1 - finalRock), and finalRock saturates on slopes this steep.");
            }
        }

        /// <summary>
        /// Relief must be proportionate to the window. A 200 m window that spans more than 200 m
        /// vertically is, on average, steeper than 45 degrees across its whole width - that is a
        /// wall, not terrain, and it means the meso/micro amplitude terms are scaled for a much
        /// larger footprint than they are being applied to.
        ///
        /// The ratio limit is 1.0 (as much vertical as horizontal) rather than something defensible
        /// like 0.3, so this fires only for unambiguous over-amplitude.
        ///
        /// SAMPLING NOTE, and it is the reason this test measures nine windows instead of one. The
        /// first version of this test sampled a SINGLE 200 m window per site. That is a sample size
        /// of roughly one landform: the ridge lattice has a base cell of 1136-2350 m, so one 200 m
        /// window sees a fraction of one feature and whether it lands on a crest, a flank or a floor
        /// is arbitrary. Measured consequence on 2026-08-09: a change that improved the mean slope at
        /// every site at 1 km and 10 km appeared to make P4_far WORSE by 8.5 degrees at 200 m, purely
        /// because widening the lattice moved a crest under the fixed window. A single small window
        /// cannot distinguish "the terrain got steeper" from "the terrain moved", and reading it as
        /// the former would have reverted a good change. Nine windows spread over 3 km is still a
        /// small sample, but it is large enough that the median does not track one feature.
        /// </summary>
        [Test]
        public void MicroScaleRelief_IsProportionateToTheWindow()
        {
            const double window = 200.0;
            const double spread = 1500.0;

            for (int i = 0; i < Sites.Length; i++)
            {
                var ratios = new System.Collections.Generic.List<double>();
                var means = new System.Collections.Generic.List<double>();

                for (int oz = -1; oz <= 1; oz++)
                {
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        SlopeHistogram h = MeasureSlope(
                            Sites[i].X + ox * spread, Sites[i].Z + oz * spread, window);
                        ratios.Add((h.MaxHeight - h.MinHeight) / window);
                        means.Add(h.MeanDegrees);
                    }
                }

                ratios.Sort();
                means.Sort();
                double medianRatio = ratios[ratios.Count / 2];
                double medianMean = means[means.Count / 2];

                Assert.That(
                    medianRatio,
                    Is.LessThan(1.0),
                    $"{Sites[i].Label} has a median relief:window ratio of {medianRatio:0.00} across " +
                    $"nine 200 m windows (median mean slope {medianMean:0.0}deg, " +
                    $"worst {ratios[ratios.Count - 1]:0.00}, best {ratios[0]:0.00}). The micro-scale " +
                    "amplitude terms are scaled for a larger footprint than they are applied to. " +
                    "terrain.md's 100m acceptance row asks for scree grit, which cannot read on a " +
                    "surface already at the limit of the slope ramp " +
                    "(WorldMacroGeologyFields.cs:1415 saturates Slope01 at 51.3 degrees).");
            }
        }
    }
}
