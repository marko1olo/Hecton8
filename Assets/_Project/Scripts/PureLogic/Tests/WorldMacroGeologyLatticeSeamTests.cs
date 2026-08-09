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
        /// Walks a transect and flags every sample whose height jump from its neighbour is more than
        /// `factor` times the local typical jump. Comparing against a LOCAL median rather than an
        /// absolute threshold is what makes this work on both an abyssal plain and a ridge flank.
        /// </summary>
        private static System.Collections.Generic.List<SeamHit> ScanTransect(
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

        [Test]
        public void HeightSeams_AreReportedWithTheirLatticeCoordinates()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Scanning for C0 height steps at a {StepMeters:0} m pitch. A hit is a jump more than " +
                "8x the local median jump over a +/-80 m window.");
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
                var hits = ScanTransect(t.X, t.Z, t.DX, t.DZ, t.Len, 8f);
                totalHits += hits.Count;
                sb.AppendLine($"  {t.Label}: {hits.Count} step(s)");

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
                        $"    ({h.X,9:0}, {h.Z,8:0})  jump={h.Jump,7:0.00}m vs typical " +
                        $"{h.LocalTypical,6:0.00}m  ratio={h.Jump / h.LocalTypical,5:0}x  " +
                        $"fracOf[crater2500={craterFrac:0.00} volc5555={volcFrac:0.00} plate12000={plateFrac:0.00}]");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"  total steps found: {totalHits}");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Directly exercises the suspected truncation instead of inferring it from height. Samples
        /// the height either side of a province cell boundary at a very fine pitch and asserts that
        /// the field is continuous there.
        ///
        /// The province lattice cell is lerp(55000, 95000) metres wide and its sample coordinate is
        /// warped by up to 32 km, so the boundary cannot be located analytically from outside the
        /// method. Instead this walks a long transect at a fine pitch and asserts on the WORST jump
        /// found, which is where the boundary is if one is crossed at all.
        ///
        /// The bar is 12x the local median. Legitimate terrain cannot produce that at a 2 m pitch:
        /// the steepest surface the generator emits is around 80 degrees, which at 2 m is 11 m of
        /// rise, and the local median on such a face is already several metres.
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
                var hits = ScanTransect(t.X, t.Z, t.DX, t.DZ, 60000.0, 8f);
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
                Is.LessThan(12.0),
                $"Height jumps {worst.Jump:0.00} m between samples {StepMeters:0} m apart at " +
                $"({worst.X:0}, {worst.Z:0}) on the {worstTransect} transect, against a local median " +
                $"jump of {worst.LocalTypical:0.00} m - a {ratio:0}x step. A jump that does not shrink " +
                "with the sampling pitch is a discontinuity, not a slope. The known candidate is " +
                "ResolveProvince (WorldMacroGeologyFields.cs:543-559), which sums over a 3x3 cell " +
                "neighbourhood while culling at a radius of 1.5 cells, so a cell two indices away can " +
                "be inside the cull radius and outside the loop.");
        }
    }
}
