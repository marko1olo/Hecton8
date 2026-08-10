using Hecton8.Physics.KCC;
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

            /// <summary>
            /// Every measured slope in degrees, sorted. Kept alongside the fixed buckets because a
            /// bucket boundary is a decision about what matters, and the one that matters here -
            /// the controller's MaxSlopeAngle - is 48 degrees, which falls inside the 35..50 bucket
            /// and therefore cannot be read off the counters at all. Asking the raw samples gives
            /// the exact percentage under any threshold, so the bar can track the controller rather
            /// than being rounded to whichever bucket edge happens to sit nearby.
            /// </summary>
            public double[] SortedDegrees;

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

            /// <summary>Exact share of samples strictly below an arbitrary angle, in percent.</summary>
            public double PctUnderDegrees(double degrees)
            {
                if (SortedDegrees == null || SortedDegrees.Length == 0) return 0.0;
                int count = 0;
                for (int i = 0; i < SortedDegrees.Length; i++)
                {
                    if (SortedDegrees[i] >= degrees) break;
                    count++;
                }
                return 100.0 * count / SortedDegrees.Length;
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
            var all = new double[SamplesPerAxis * SamplesPerAxis];

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
                    all[h.Samples] = degrees;
                    if (degrees > h.MaxDegrees) h.MaxDegrees = degrees;
                    if (center < h.MinHeight) h.MinHeight = center;
                    if (center > h.MaxHeight) h.MaxHeight = center;
                    h.Samples++;
                }
            }

            System.Array.Sort(all);
            h.SortedDegrees = all;
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
        /// Some part of the seafloor must be gentle enough that the character controller can stand
        /// on it. The bar is the CONTROLLER'S OWN LIMIT, not a real-world analogy.
        ///
        /// RE-AIMED 2026-08-10 after an owner ruling, and the correction is about whose standard a
        /// test is allowed to encode. This assertion used to demand 15% of every site under 25
        /// degrees, a figure taken from Earth (abyssal plains under 1 degree, continental slopes
        /// 3-6). HECTON-8 is deliberately not that world: the owner's ruling is that a dramatic,
        /// cliffed seafloor with four shelf-to-abyss descents across 30 km is the intended design,
        /// and steepness is a feature. A test built on the Earth analogy was therefore failing the
        /// terrain for being what it was authored to be - and worse, the obvious way to make it pass
        /// is to flatten the world, so a green run would have meant the design was destroyed.
        ///
        /// What survives re-aiming is the property that does not depend on taste: ground the player
        /// can actually stand on has to exist. KccEnvironmentProfileDTO.DefaultMaxSlopeAngleDegrees
        /// is the angle above which HydrodynamicKccRuntime drives the slide branch instead of
        /// letting the character walk (:2033, :1321), so it is the game's own definition of a floor.
        /// The bar is read from that const rather than mirrored, so if the controller is retuned the
        /// terrain bar follows it instead of silently disagreeing.
        ///
        /// 15% is kept: a site that is 85% unwalkable is still permitted, which is a very steep
        /// world by any standard and exactly what was asked for. This fires only when a site has
        /// essentially nowhere to stand.
        /// </summary>
        [Test]
        public void EverySite_HasSomeTraversableGround()
        {
            const double minimumWalkablePercent = 15.0;
            double walkableLimitDegrees = KccEnvironmentProfileDTO.DefaultMaxSlopeAngleDegrees;

            for (int i = 0; i < Sites.Length; i++)
            {
                SlopeHistogram h = MeasureSlope(Sites[i].X, Sites[i].Z, 1000.0);
                double gentle = h.PctUnderDegrees(walkableLimitDegrees);

                Assert.That(
                    gentle,
                    Is.GreaterThan(minimumWalkablePercent),
                    $"{Sites[i].Label} has only {gentle:0.0}% of its 1 km window under " +
                    $"{walkableLimitDegrees:0} degrees, the controller's own MaxSlopeAngle " +
                    $"(mean {h.MeanDegrees:0.0}deg, max {h.MaxDegrees:0.0}deg, " +
                    $"relief {h.MaxHeight - h.MinHeight:0.0}m). Above that angle " +
                    "HydrodynamicKccRuntime slides the character instead of walking them, so this " +
                    "site has almost nowhere to stand. This is NOT a request to flatten the world - " +
                    "a steep dramatic seafloor is the intended design - it is a request that some " +
                    "standable ground exist at every site.");
            }
        }

        /// <summary>
        /// Relief must be proportionate to the window SOMEWHERE IN THE WORLD - not everywhere.
        ///
        /// RE-AIMED 2026-08-10, same owner ruling as EverySite_HasSomeTraversableGround, and the
        /// distinction is worth stating precisely because the first version of this test was not
        /// merely mis-tuned, it was asking the wrong question.
        ///
        /// It used to demand that the MEDIAN of nine 200 m windows at EVERY site have less vertical
        /// relief than horizontal extent. Applied at W4_steep and W5_wall - sites selected BY
        /// CONSTRUCTION as the p75 and p98 of the world's own slope distribution - that demands the
        /// steepest quarter of a deliberately cliffed world contain no cliffs. It is a bar that the
        /// design cannot satisfy and should not have to: a 200 m window on a wall is a wall, and the
        /// owner has ruled that walls are the point.
        ///
        /// The real property, the one that separates "this world has dramatic cliffs" from "the
        /// micro amplitude terms are scaled for a larger footprint than they are applied to", is a
        /// statement about the WORLD, not about its steepest sites: if the median 200 m window taken
        /// anywhere in the world is a cliff, then nowhere is calm and the amplitude is genuinely
        /// misscaled. So the sample is now a grid spanning the emitted world rather than nine
        /// windows around hand-picked steep probes, and the assertion is on that world-wide median.
        ///
        /// This can still fail, which is the point of keeping it: raise the meso amplitude enough
        /// that the typical square of seafloor becomes a wall and it fires. It just no longer fires
        /// for the world being dramatic where it was authored to be dramatic.
        /// </summary>
        [Test]
        public void MicroScaleRelief_IsProportionateToTheWindow()
        {
            const double window = 200.0;
            const int gridPerAxis = 7;
            float halfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;

            // Inset by one window so every sample sits fully inside the terrain the chunk grid emits.
            double span = (halfExtent - window) * 2.0;
            double stride = span / (gridPerAxis - 1);

            var ratios = new System.Collections.Generic.List<double>();
            var means = new System.Collections.Generic.List<double>();

            for (int iz = 0; iz < gridPerAxis; iz++)
            {
                double z = -halfExtent + window + iz * stride;
                for (int ix = 0; ix < gridPerAxis; ix++)
                {
                    double x = -halfExtent + window + ix * stride;
                    SlopeHistogram h = MeasureSlope(x, z, window);
                    ratios.Add((h.MaxHeight - h.MinHeight) / window);
                    means.Add(h.MeanDegrees);
                }
            }

            ratios.Sort();
            means.Sort();
            double medianRatio = ratios[ratios.Count / 2];
            double medianMean = means[means.Count / 2];
            int cliffCount = 0;
            foreach (double r in ratios) if (r >= 1.0) cliffCount++;

            Assert.That(
                medianRatio,
                Is.LessThan(1.0),
                $"The MEDIAN 200 m window across the whole 30 km world has a relief:window ratio of " +
                $"{medianRatio:0.00} (median mean slope {medianMean:0.0}deg, worst " +
                $"{ratios[ratios.Count - 1]:0.00}, best {ratios[0]:0.00}, and {cliffCount} of " +
                $"{ratios.Count} sampled windows are at or past 1.00). Individual cliffs are the " +
                "intended design and are not what this measures - this fires only when the TYPICAL " +
                "square of seafloor drops more than its own width, which means the meso/micro " +
                "amplitude terms are scaled for a larger footprint than they are applied to and " +
                "there is nowhere calm left in the world.");
        }
    }
}
