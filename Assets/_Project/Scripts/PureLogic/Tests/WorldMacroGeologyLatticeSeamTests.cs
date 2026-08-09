using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Detects C0 steps in the height field along straight lines, which is the signature of a cell
    /// lattice whose summation neighbourhood is truncated.
    ///
    /// Why this is worth a fixture of its own: a truncation step is invisible to every slope statistic
    /// already in this suite. Mean slope, maximum slope and relief all read normally, because the step
    /// is a bounded jump on a measure-zero set rather than a steep face. It shows up as a crease in a
    /// curvature X-Ray and as a seam in a render, and otherwise passes silently.
    ///
    /// SEEN, 2026-08-09, in Docs/Reports/CleanRoom/CleanRoom_XRay_Curvature.png: straight polygonal
    /// boundaries meeting at angles, which no noise field produces - a straight edge in a procedural
    /// field means an integer lattice.
    ///
    /// The suspect, found by reading rather than by measuring, is ResolveProvince
    /// (WorldMacroGeologyFields.cs:543-559). It sums a smooth per-cell weight over a 3x3 neighbourhood
    /// anchored at floor(sampleP), and culls at a radius of 1.5 CELLS. Those two do not agree. A cell
    /// two indices away has its centre at cell + hash with hash in [0,1), so for a sample near the far
    /// edge of its own cell that centre can sit as close as 1.0 cells - inside the cull radius, and
    /// outside the 3x3 loop. Crossing the cell boundary admits it to the neighbourhood in one step.
    ///
    /// The method's own comment at :533-537 states the opposite conclusion: "For a FIXED cell, dist is
    /// a C-infinity function of position, so the normalized blend is smooth EVERYWHERE ... therefore
    /// NO 1px seam line along province borders". That reasoning is correct for a fixed SET of cells
    /// and the set is what moves. The comment is the reason this fixture exists: a documented
    /// smoothness claim that the code does not deliver is worse than an undocumented one, because it
    /// stops the next reader from checking.
    ///
    /// The test does not assume the province lattice is the culprit. It scans for steps and reports
    /// where they are, so the answer survives being wrong about the cause.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyLatticeSeamTests
    {
        private const uint Seed = 880031u;

        /// <summary>
        /// 2 m pitch. A truncation step is a jump between two ADJACENT samples however fine the
        /// sampling, whereas legitimate terrain variation shrinks with the pitch - so a fine pitch is
        /// what separates the two. At 2 m even a 45 degree slope only moves 2 m of height.
        /// </summary>
        private const double StepMeters = 2.0;

        private struct SeamHit
        {
            public double X;
            public double Z;
            public float Jump;
            public float LocalTypical;
        }

        /// <summary>
        /// Walks a transect and flags every sample whose SECOND difference is more than `factor`
        /// times the local typical second difference.
        ///
        /// Second difference, not first. A first-difference scan over 180 km of transect found no
        /// step discontinuities at all (2026-08-09: 7 mild hits at 8-11x, all in consecutive runs,
        /// which is a steep face and not a step). But the artefact being chased is visible in a
        /// CURVATURE X-Ray, and curvature is a second derivative - it reveals a kink in the gradient,
        /// which by definition leaves the height itself continuous and produces no first-difference
        /// jump. Scanning the wrong derivative order returns a clean bill of health for a field that
        /// is visibly creased.
        ///
        /// Every clamp, saturate, min, max and abs in the pipeline is a candidate: each is continuous
        /// and each has a corner, and the locus of that corner in a smooth field is a curve.
        ///
        /// Comparing against a LOCAL median rather than an absolute threshold is what makes this work
        /// on both an abyssal plain and a ridge flank.
        /// </summary>
        private static System.Collections.Generic.List<SeamHit> ScanCurvature(
            double startX, double startZ, double dirX, double dirZ, double lengthMeters, float factor)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            int steps = (int)(lengthMeters / StepMeters);
            double len = math.sqrt(dirX * dirX + dirZ * dirZ);
            dirX /= len;
            dirZ /= len;

            var heights = new float[steps];
            for (int i = 0; i < steps; i++)
            {
                double d = i * StepMeters;
                heights[i] = WorldMacroGeologyFields.EvaluateHeightMeters(
                    startX + dirX * d, startZ + dirZ * d, in p);
            }

            var curvature = new float[steps - 2];
            for (int i = 1; i < steps - 1; i++)
                curvature[i - 1] = math.abs(heights[i + 1] - 2f * heights[i] + heights[i - 1]);

            var hits = new System.Collections.Generic.List<SeamHit>();
            const int window = 40;
            var scratch = new float[window * 2 + 1];

            for (int i = window; i < curvature.Length - window; i++)
            {
                for (int k = -window; k <= window; k++)
                    scratch[k + window] = curvature[i + k];
                System.Array.Sort(scratch);
                float median = scratch[window];

                if (median > 1e-5f && curvature[i] > median * factor)
                {
                    double d = (i + 1) * StepMeters;
                    hits.Add(new SeamHit
                    {
                        X = startX + dirX * d,
                        Z = startZ + dirZ * d,
                        Jump = curvature[i],
                        LocalTypical = median
                    });
                }
            }

            return hits;
        }

        [Test]
        public void GradientKinks_AreReportedWithTheirLatticeCoordinates()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Scanning for C1 gradient kinks at a {StepMeters:0} m pitch. A hit is a second difference " +
                "more than 8x the local median second difference over a +/-80 m window.");
            sb.AppendLine();

            (double X, double Z, double DX, double DZ, double Len, string Label)[] transects =
            {
                (0.0, 1500.0, 1.0, 0.0, 60000.0, "east from origin, 60 km"),
                (1500.0, 0.0, 0.0, 1.0, 60000.0, "north from origin, 60 km"),
                (280000.0, 90000.0, 1.0, 0.0, 60000.0, "east through P4_far, 60 km")
            };

            int totalHits = 0;
            foreach (var t in transects)
            {
                var hits = ScanCurvature(t.X, t.Z, t.DX, t.DZ, t.Len, 8f);
                totalHits += hits.Count;
                sb.AppendLine($"  {t.Label}: {hits.Count} kink(s)");

                int shown = 0;
                foreach (var h in hits)
                {
                    if (shown++ >= 8) { sb.AppendLine("    ..."); break; }

                    // Report the fractional position on each candidate lattice. A step that always
                    // lands at a fraction near 0 identifies its lattice; one scattered across all
                    // fractions is not a lattice artefact at all.
                    double craterFrac = h.X / 2500.0;
                    craterFrac -= math.floor(craterFrac);
                    double volcFrac = h.X * 0.00018;
                    volcFrac -= math.floor(volcFrac);
                    double plateFrac = h.X / 12000.0;
                    plateFrac -= math.floor(plateFrac);

                    sb.AppendLine(
                        $"    ({h.X,9:0}, {h.Z,8:0})  curv={h.Jump,7:0.000}m vs typical " +
                        $"{h.LocalTypical,6:0.000}m  ratio={h.Jump / h.LocalTypical,5:0}x  " +
                        $"fracOf[crater2500={craterFrac:0.00} volc5555={volcFrac:0.00} plate12000={plateFrac:0.00}]");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"  total kinks found: {totalHits}");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Locks the property the first-difference scan established and the curvature scan could not:
        /// the height field has no STEP discontinuities. Measured 2026-08-09 over 180 km of transect
        /// at a 2 m pitch, the largest first difference anywhere was 11x its local median and occurred
        /// in runs of consecutive samples, which is a steep face rather than a step.
        ///
        /// This is worth locking because a step is what a truncated cell neighbourhood, a mismatched
        /// cull radius or a per-chunk coordinate anchor all produce, and this file has a documented
        /// history of exactly that: WorldMacroGeologyFields.cs:653-673 records a 34.46 m cliff every
        /// 512 m caused by a chunk anchor, against 0.07 m of legitimate mid-chunk variation.
        ///
        /// The bar is 20x the local median. Legitimate terrain cannot reach it at a 2 m pitch: the
        /// steepest surface the generator emits is around 80 degrees, which is 11 m of rise over 2 m,
        /// and on such a face the local median is already several metres.
        /// </summary>
        [Test]
        public void HeightField_HasNoStepDiscontinuities()
        {
            var worst = new SeamHit { Jump = 0f, LocalTypical = 1f };
            string worstTransect = "<none>";

            (double X, double Z, double DX, double DZ, string Label)[] transects =
            {
                (0.0, 1500.0, 1.0, 0.0, "east from origin"),
                (1500.0, 0.0, 0.0, 1.0, "north from origin"),
                (280000.0, 90000.0, 1.0, 0.0, "east through P4_far")
            };

            foreach (var t in transects)
            {
                var hits = ScanFirstDifference(t.X, t.Z, t.DX, t.DZ, 60000.0, 8f);
                foreach (var h in hits)
                {
                    if (h.Jump / h.LocalTypical > worst.Jump / worst.LocalTypical)
                    {
                        worst = h;
                        worstTransect = t.Label;
                    }
                }
            }

            double ratio = worst.Jump / worst.LocalTypical;

            Assert.That(
                ratio,
                Is.LessThan(20.0),
                $"Height jumps {worst.Jump:0.00} m between samples {StepMeters:0} m apart at " +
                $"({worst.X:0}, {worst.Z:0}) on the {worstTransect} transect, against a local median " +
                $"jump of {worst.LocalTypical:0.00} m - a {ratio:0}x step. A jump that does not shrink " +
                "with the sampling pitch is a discontinuity, not a slope, and the usual causes are a " +
                "cell neighbourhood that does not cover its own cull radius, or a coordinate anchor " +
                "that steps with the chunk.");
        }

        /// <summary>
        /// First differences, for the step assertion above. Kept separate from ScanCurvature rather
        /// than parameterised, because the two answer different questions and sharing one routine is
        /// how the assertion above came to be checking second differences while its failure message
        /// still described steps - a test that reports one quantity and measures another.
        /// </summary>
        private static System.Collections.Generic.List<SeamHit> ScanFirstDifference(
            double startX, double startZ, double dirX, double dirZ, double lengthMeters, float factor)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            int steps = (int)(lengthMeters / StepMeters);
            double len = math.sqrt(dirX * dirX + dirZ * dirZ);
            dirX /= len;
            dirZ /= len;

            var heights = new float[steps];
            for (int i = 0; i < steps; i++)
            {
                double d = i * StepMeters;
                heights[i] = WorldMacroGeologyFields.EvaluateHeightMeters(
                    startX + dirX * d, startZ + dirZ * d, in p);
            }

            var jumps = new float[steps - 1];
            for (int i = 1; i < steps; i++)
                jumps[i - 1] = math.abs(heights[i] - heights[i - 1]);

            var hits = new System.Collections.Generic.List<SeamHit>();
            const int window = 40;
            var scratch = new float[window * 2 + 1];

            for (int i = window; i < jumps.Length - window; i++)
            {
                for (int k = -window; k <= window; k++)
                    scratch[k + window] = jumps[i + k];
                System.Array.Sort(scratch);
                float median = scratch[window];

                if (median > 1e-4f && jumps[i] > median * factor)
                {
                    double d = i * StepMeters;
                    hits.Add(new SeamHit
                    {
                        X = startX + dirX * d,
                        Z = startZ + dirZ * d,
                        Jump = jumps[i],
                        LocalTypical = median
                    });
                }
            }

            return hits;
        }
    }
}
