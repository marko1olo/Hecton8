using Hecton8.Physics.KCC;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Pins the seafloor the owner has approved, per site, so it cannot rot silently.
    ///
    /// WHY A RATCHET AT ALL. Everything this suite measures is a statistic over a noise field, and a
    /// statistic is exactly the kind of thing that drifts one edit at a time without any single edit
    /// looking wrong. The slope budget took nine refuted hypotheses to pin down and the material
    /// palette took an intervention experiment; neither result should have to be rediscovered
    /// because a later tuning pass moved a smoothstep bound by 0.02.
    ///
    /// WHY IT COMPARES COMPOSITION AND NOT A SINGLE NUMBER. A ratchet on one aggregate is a ratchet
    /// that can be satisfied by breaking something else: flatten the cliffs and improve the plains
    /// and a world-wide median does not move at all. So every site is pinned separately AND in both
    /// directions - a site that becomes markedly gentler fails exactly as loudly as one that becomes
    /// steeper, because the owner ruled that the steep dramatic seafloor IS the design and quietly
    /// smoothing it is the regression this project is most exposed to.
    ///
    /// WHAT IS DELIBERATELY NOT PINNED. Nothing here asserts a target. These are the values measured
    /// on 2026-08-10 after the owner approved them; the bands are wide enough that ordinary retuning
    /// passes through, and narrow enough that a structural change cannot. When a bound fires, the
    /// question is "was this change intended?" - not "which number do I edit to make it green?". If
    /// the change was intended, move the recorded value and say so in the commit.
    ///
    /// This fixture must fail if the world changes. That is its entire job.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyApprovedWorldRatchetTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;
        private const int SamplesPerAxis = 32;

        /// <summary>
        /// The approved seafloor, measured 2026-08-10 over 1 km windows at the in-world atlas sites.
        ///
        /// MeanSlope is the shape of the ground. RockShare is the share of the window where HardRock
        /// wins the material argmax - it is pinned because the palette fix is the thing most likely
        /// to be undone by accident, having been wrong for a long time without anyone noticing.
        /// </summary>
        private static readonly (double X, double Z, string Label,
                                 double MeanSlope, double RockShare)[] Approved =
        {
            (11896.0, -13148.0, "W1_flat",     9.4,  0.3),
            (5635.0,   -3130.0, "W2_gentle",  17.9, 14.1),
            (9391.0,  -10643.0, "W3_typical", 31.3, 67.7),
            (6887.0,   -6887.0, "W4_steep",   43.7, 95.5),
            (-11896.0,  4383.0, "W5_wall",    57.5, 93.8)
        };

        /// <summary>
        /// Degrees of mean slope a site may move before this fires. 4 degrees is roughly a sixth of
        /// the spread between W1 and W5, so ordinary retuning passes and a structural change cannot.
        /// </summary>
        private const double SlopeToleranceDegrees = 4.0;

        /// <summary>
        /// Percentage points the rock share may move. Wider than the slope band because the argmax
        /// of eight competing classes is a coarser instrument than a gradient: near a 50/50 boundary
        /// a small weight change flips many cells at once.
        /// </summary>
        private const double RockShareTolerancePoints = 12.0;

        private static double MeanSlopeDegrees(double centerX, double centerZ, double windowMeters)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            const double probe = 12.0;
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            double sum = 0.0;
            int count = 0;

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
                    sum += math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                    count++;
                }
            }

            return sum / count;
        }

        private static double RockSharePercent(double centerX, double centerZ, double windowMeters)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            int rockWins = 0;
            int count = 0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;
                    WorldMacroGeologySample macro = WorldMacroGeologyFields.Evaluate(x, z, in p);
                    WorldTerrainSurfaceMaterialWeights w =
                        WorldTerrainSurfaceMaterialResolver.Resolve(in macro, (float)x, (float)z, Seed);

                    float[] all =
                    {
                        w.ShellSand, w.LimestoneShelf, w.ClaySilt, w.HardRock,
                        w.BrineSaltCrust, w.ManganeseNodulePlain, w.ReefRubble, w.SeepCrust
                    };
                    int best = 0;
                    for (int c = 1; c < 8; c++) if (all[c] > all[best]) best = c;
                    if (best == 3) rockWins++;
                    count++;
                }
            }

            return 100.0 * rockWins / count;
        }

        /// <summary>
        /// Each site must still look like itself, in BOTH directions. Getting gentler is a failure
        /// here, not an improvement - see the fixture summary.
        /// </summary>
        [Test]
        public void EverySite_StillMatchesTheApprovedWorld()
        {
            var drifted = new System.Collections.Generic.List<string>();
            var report = new System.Text.StringBuilder();
            report.AppendLine(
                $"    {"site",-11}{"slope",8}{"was",8}{"rock%",8}{"was",8}");

            foreach (var site in Approved)
            {
                double slope = MeanSlopeDegrees(site.X, site.Z, 1000.0);
                double rock = RockSharePercent(site.X, site.Z, 1000.0);

                report.AppendLine(
                    $"    {site.Label,-11}{slope,7:0.0}d{site.MeanSlope,7:0.0}d" +
                    $"{rock,7:0.0}%{site.RockShare,7:0.0}%");

                if (math.abs(slope - site.MeanSlope) > SlopeToleranceDegrees)
                {
                    drifted.Add(
                        $"{site.Label}: mean slope {slope:0.0} deg against the approved " +
                        $"{site.MeanSlope:0.0} deg (tolerance {SlopeToleranceDegrees:0.0}). " +
                        (slope < site.MeanSlope
                            ? "This site got GENTLER, which is a regression here: the owner ruled " +
                              "that the steep dramatic seafloor is the intended design."
                            : "This site got STEEPER."));
                }

                if (math.abs(rock - site.RockShare) > RockShareTolerancePoints)
                {
                    drifted.Add(
                        $"{site.Label}: HardRock wins {rock:0.0}% of the window against the " +
                        $"approved {site.RockShare:0.0}% (tolerance {RockShareTolerancePoints:0.0} " +
                        "points). The palette was measured wrong for a long time before anyone " +
                        "noticed - curvature was painting rock on 9 degree ground - so a large move " +
                        "here needs an explanation, not a new number.");
                }
            }

            TestContext.WriteLine(report.ToString());

            Assert.That(
                drifted,
                Is.Empty,
                "The generated seafloor no longer matches the world the owner approved on " +
                "2026-08-10:\n  " + string.Join("\n  ", drifted) +
                "\n\nIf the change was deliberate, update the Approved table in this fixture and " +
                "say why in the commit message. If it was not, something upstream moved terrain or " +
                "material output without meaning to.\n\n" + report.ToString());
        }

        /// <summary>
        /// The world-wide shape, pinned separately from the per-site table because the two fail for
        /// different reasons: a site can drift because one landform moved under a fixed window,
        /// while these move only if the generator itself changed.
        ///
        /// The relief column is the guard that makes the slope numbers meaningful. A change that
        /// flattens the world into a plane posts a beautiful median and is a catastrophe, and only
        /// a preserved depth range distinguishes the two.
        /// </summary>
        [Test]
        public void TheWorldWideShape_StillMatchesTheApprovedWorld()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            float halfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;
            const int cellsPerAxis = 48;
            const double probe = 20.0;

            var slopes = new double[cellsPerAxis * cellsPerAxis];
            double step = (halfExtent * 2.0) / (cellsPerAxis - 1);
            float lo = float.MaxValue, hi = float.MinValue;
            int k = 0;
            int walkable = 0;

            for (int iz = 0; iz < cellsPerAxis; iz++)
            {
                double z = -halfExtent + iz * step;
                for (int ix = 0; ix < cellsPerAxis; ix++)
                {
                    double x = -halfExtent + ix * step;
                    float c = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
                    lo = math.min(lo, c);
                    hi = math.max(hi, c);

                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - probe, z, in p);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + probe, z, in p);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - probe, in p);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + probe, in p);
                    float dx = (e - w) / (float)(probe * 2.0);
                    float dz = (n - s) / (float)(probe * 2.0);
                    double deg = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));
                    if (deg < KccEnvironmentProfileDTO.DefaultMaxSlopeAngleDegrees) walkable++;
                    slopes[k++] = deg;
                }
            }

            System.Array.Sort(slopes);
            double median = slopes[slopes.Length / 2];
            double relief = hi - lo;
            double walkablePercent = 100.0 * walkable / slopes.Length;

            // Measured 2026-08-10 over the +/-15 km the chunk grid actually emits.
            const double approvedMedian = 28.4;
            const double approvedReliefMeters = 4926.0;

            var failures = new System.Collections.Generic.List<string>();

            if (math.abs(median - approvedMedian) > SlopeToleranceDegrees)
            {
                failures.Add(
                    $"world median slope {median:0.0} deg against the approved {approvedMedian:0.0}");
            }

            // Relief is allowed to rise freely and only guarded downward: losing depth range is the
            // failure mode that masquerades as an improvement.
            if (relief < approvedReliefMeters * 0.80)
            {
                failures.Add(
                    $"world relief {relief:0} m against the approved {approvedReliefMeters:0} m - " +
                    "the depth range is collapsing, which flatters every slope statistic while " +
                    "destroying the world");
            }

            string detail =
                $"median {median:0.0} deg, relief {relief:0} m, " +
                $"{walkablePercent:0.0}% of the world under the controller's " +
                $"{KccEnvironmentProfileDTO.DefaultMaxSlopeAngleDegrees:0} deg limit.";

            TestContext.WriteLine(detail);

            Assert.That(
                failures,
                Is.Empty,
                "The world-wide shape moved away from what the owner approved:\n  " +
                string.Join("\n  ", failures) + "\n\n" + detail);
        }
    }
}
