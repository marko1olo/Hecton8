using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Separates two explanations for a steep world that every slope statistic so far has been unable
    /// to tell apart: authored amplitude, or a field that oscillates.
    ///
    /// The measurement is the ratio of TOTAL VERTICAL TRAVEL along a transect to the NET RELIEF of that
    /// transect. Walk a straight line and sum every metre of rise and fall; then take the highest point
    /// minus the lowest. A landform that goes down once and stays down has a ratio near 1. A ramp with
    /// a couple of terraces is 2 or 3. A field that climbs and falls through its own range over and
    /// over has a ratio in the tens, and that is a different defect entirely - it means the vertical
    /// budget is being spent repeatedly instead of once, so no amount of widening any single feature
    /// can flatten it.
    ///
    /// Why the distinction matters here. Measured 2026-08-10 across a 400 km square, the median cell of
    /// this world sits at 29.2 degrees and 32.5% of cells exceed 40. Relief is NOT saturated - it grows
    /// with window size and tops out near 5100 m at about 30 km, which is a sane shape. Those two facts
    /// only fit together if the field crosses its range many times inside that 30 km. 5100 m spread
    /// monotonically over 30 km would be 9.6 degrees, and the world measures three times that.
    ///
    /// For reference: Earth's abyssal plains are under 1 degree, continental slopes 3-6, and the
    /// steepest sustained ocean topography rarely exceeds 30. This is not a request to match Earth. It
    /// is the scale against which a median of 29 degrees should be read before it is called authored.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyProfileSinuosityTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        /// <summary>
        /// 100 m pitch. Total vertical travel is scale dependent by nature - sample finer and you find
        /// more wiggle - so the pitch has to be stated with the number and held constant across the
        /// comparison. 100 m resolves landforms and ignores grit, which is the scale the macro fields
        /// are supposed to be authoring at.
        /// </summary>
        private const double StepMeters = 100.0;

        private static (double Travel, double Relief, int Reversals) Profile(
            double x0, double z0, double dx, double dz, double lengthMeters, in WorldMacroGeologyParams p)
        {
            int steps = (int)(lengthMeters / StepMeters) + 1;
            double len = math.sqrt(dx * dx + dz * dz);
            dx /= len;
            dz /= len;

            double travel = 0.0;
            float lo = float.MaxValue;
            float hi = float.MinValue;
            float previous = 0f;
            int reversals = 0;
            int previousSign = 0;

            for (int i = 0; i < steps; i++)
            {
                double d = i * StepMeters;
                float h = WorldMacroGeologyFields.EvaluateHeightMeters(x0 + dx * d, z0 + dz * d, in p);
                lo = math.min(lo, h);
                hi = math.max(hi, h);

                if (i > 0)
                {
                    float delta = h - previous;
                    travel += math.abs(delta);

                    // A reversal is a change of direction that is large enough not to be noise on a
                    // flat stretch; 2 m over 100 m is about 1 degree.
                    int sign = delta > 2f ? 1 : delta < -2f ? -1 : 0;
                    if (sign != 0)
                    {
                        if (previousSign != 0 && sign != previousSign) reversals++;
                        previousSign = sign;
                    }
                }

                previous = h;
            }

            return (travel, hi - lo, reversals);
        }

        [Test]
        public void VerticalTravelVersusNetRelief_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Total vertical travel against net relief along straight transects, {StepMeters:0} m pitch.");
            sb.AppendLine("ratio 1 = one descent. 2-4 = a ramp with terraces. Tens = the field oscillates.");
            sb.AppendLine();
            sb.AppendLine($"  {"transect",-28}{"length",9}{"travel",11}{"relief",10}{"ratio",8}{"reversals",11}{"per 10km",10}");

            // In-world transects first, then two that deliberately leave the world, so the difference
            // between the two regimes is visible in one table instead of being asserted about.
            (double X, double Z, double DX, double DZ, double Len, string Label)[] transects =
            {
                (-15000.0, 0.0, 1.0, 0.0, 30000.0, "IN: west-east, whole world"),
                (0.0, -15000.0, 0.0, 1.0, 30000.0, "IN: south-north, whole world"),
                (-15000.0, -7500.0, 1.0, 0.0, 30000.0, "IN: west-east, south half"),
                (-7500.0, -7500.0, 1.0, 1.0, 21000.0, "IN: SW-NE diagonal"),
                (0.0, 0.0, 1.0, 0.0, 200000.0, "OUT: origin east 200 km"),
                (777000.0, -333000.0, 1.0, 0.0, 200000.0, "OUT: 26 world-extents away")
            };

            foreach (var t in transects)
            {
                var r = Profile(t.X, t.Z, t.DX, t.DZ, t.Len, in p);
                double ratio = r.Travel / math.max(1.0, r.Relief);
                double per10km = r.Reversals / (t.Len / 10000.0);
                sb.AppendLine(
                    $"  {t.Label,-28}{t.Len / 1000.0,8:0}k{r.Travel,10:0}m{r.Relief,9:0}m{ratio,8:0.0}" +
                    $"{r.Reversals,11}{per10km,10:0.0}");
            }

            sb.AppendLine();
            sb.AppendLine(
                "  A reversal is a direction change of more than 2 m per 100 m step, i.e. about 1 deg. " +
                "Reversals per 10 km is the count of distinct up-down cycles at landform scale: it is " +
                "the wavelength of the terrain, read directly.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Checks whether the reversal count measures the terrain or measures the ruler.
        ///
        /// This test exists because the reversal count MISLED this investigation. On 2026-08-10 the
        /// per-10-km count came out between 18.3 and 20.2 at every site, heading and transect length,
        /// and that constancy was read as evidence of one dominant wavelength near 526 m, injected by
        /// one term with one hardcoded frequency. The structure function then showed the field is
        /// scale-invariant from 25 m to 800 m - mean |dh| divided by lag holds at 0.288, 0.288, 0.287,
        /// 0.284, 0.276, 0.270 - which is fBm at gain 0.5, and a scale-free field has no dominant
        /// wavelength to find.
        ///
        /// On such a field the reversal count is set by the SAMPLING PITCH: halve the pitch and the
        /// walk resolves finer wiggles, so the count roughly doubles. If that is what happens here, the
        /// constancy across sites was never a fact about the terrain - it was a fact about the 100 m
        /// step every transect happened to share.
        ///
        /// The check is left in the suite permanently rather than deleted after answering, because the
        /// error it caught is not a one-off: any threshold-crossing statistic on a fractal surface is a
        /// measurement of its own scale unless the scale is swept.
        /// </summary>
        [Test]
        public void ReversalCount_IsAPropertyOfThePitch_NotTheTerrain()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double[] pitches = { 25.0, 50.0, 100.0, 200.0, 400.0 };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Reversals per 10 km along the same 100 km transect, at different sampling pitches.");
            sb.AppendLine("Scale-free field: count scales as 1/pitch. Real wavelength: count is constant.");
            sb.AppendLine();
            sb.AppendLine($"    {"pitch",8}{"cyc/10km",11}{"implied wavelen",18}{"vs 100m pitch",16}");

            // Measured in a first pass so the ratio column is populated for every row. The first
            // version of this printed 0.00x for the 25 m and 50 m rows because the reference was only
            // assigned when the loop reached 100 m - a table that silently reported the two most
            // important rows as zero.
            var counts = new double[pitches.Length];
            for (int k = 0; k < pitches.Length; k++)
                counts[k] = ReversalsPer10Km(pitches[k], in p);

            double reference = 0.0;
            for (int k = 0; k < pitches.Length; k++)
                if (math.abs(pitches[k] - 100.0) < 1e-6) reference = counts[k];

            for (int k = 0; k < pitches.Length; k++)
            {
                sb.AppendLine($"    {pitches[k],7:0}m{counts[k],11:0.0}" +
                              $"{10000.0 / math.max(0.01, counts[k]),16:0}m" +
                              $"{(reference > 0 ? counts[k] / reference : 0.0),15:0.00}x");
            }

            sb.AppendLine();
            sb.AppendLine("  If the ratios track 100/pitch (4.0x, 2.0x, 1.0x, 0.5x, 0.25x) the count is the");
            sb.AppendLine("  ruler. If they are all near 1.0x there is a genuine wavelength in the field.");
            sb.AppendLine();
            sb.AppendLine("  MEASURED 2026-08-10: 54.3, 30.9, 17.5, 9.7, 6.5 - i.e. 3.10x, 1.77x, 1.00x,");
            sb.AppendLine("  0.55x, 0.37x. The count is the ruler. Slightly sublinear because the direction");
            sb.AppendLine("  threshold is scaled with the pitch alongside it.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        private static double ReversalsPer10Km(double pitch, in WorldMacroGeologyParams p)
        {
            int steps = (int)(100000.0 / pitch) + 1;
            float previous = 0f;
            int reversals = 0, previousSign = 0;
            // The 2 m direction-change threshold is scaled with the pitch, so the test compares like
            // with like: at a 25 m step a 2 m rise is 4.6 degrees, at 400 m it is 0.3.
            float threshold = (float)(2.0 * pitch / 100.0);

            for (int i = 0; i < steps; i++)
            {
                float h = WorldMacroGeologyFields.EvaluateHeightMeters(i * pitch, 0.0, in p);
                if (i > 0)
                {
                    float delta = h - previous;
                    int sign = delta > threshold ? 1 : delta < -threshold ? -1 : 0;
                    if (sign != 0)
                    {
                        if (previousSign != 0 && sign != previousSign) reversals++;
                        previousSign = sign;
                    }
                }
                previous = h;
            }

            return reversals / 10.0;
        }

        /// <summary>
        /// Locks the property that separates landforms from noise: crossing the world must not climb
        /// and descend its entire vertical range several times over.
        ///
        /// RE-AIMED 2026-08-10, and the correction matters more than the assertion. This test used to
        /// walk 200 km from four origins, two of which - p4_far at (300000, 90000) and p5_deepfar at
        /// (777000, -333000) - are outside the world. WorldExtentMeters is 30000 and
        /// ResolveMinimumChunkRange bounds the chunk grid to +/-15000 m, so a 200 km transect leaves
        /// the world after the first 7% of its length and spends the rest measuring terrain the game
        /// will never emit. It reported 93145 m of travel over 5084 m of relief, ratio 18.3, against a
        /// bar of 12 - a failure earned almost entirely outside the map.
        ///
        /// Measured in-world instead, a 30 km transect gives a ratio near 3.9. So the bar of 12 was
        /// never the right shape either: the ratio grows with transect length, because a longer walk
        /// crosses more shelf boundaries, and a threshold quoted without a length is not a threshold.
        ///
        /// The bar is now 6.0 on a transect that spans the world exactly once. What that encodes is a
        /// design expectation, stated plainly so it can be argued with: a 30 km world carrying a
        /// 2860 m shelf-to-abyss drop should contain about ONE descent, the way a continental margin
        /// does, and 6.0 allows three before it complains. The world measures 3.9, so this now passes -
        /// and it passes on a real property rather than on a wider bar.
        ///
        /// CAVEAT this test must carry: total vertical travel is itself pitch-dependent on a fractal
        /// surface, and this one is measured at a fixed 100 m step. The ratio is a statement about the
        /// field AS SAMPLED AT 100 m, not an absolute - it would grow at a finer pitch and shrink at a
        /// coarser one, and quoting it without the pitch would be quoting half a measurement.
        /// </summary>
        [Test]
        public void MacroProfile_DoesNotOscillateThroughItsOwnRange()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var failures = new System.Collections.Generic.List<string>();

            // Transects that span the world once, staying inside the +/-15 km the chunk grid emits.
            const double half = 15000.0;
            const double worldSpan = half * 2.0;

            (double X, double Z, double DX, double DZ, string Label)[] transects =
            {
                (-half, 0.0, 1.0, 0.0, "west-east through the origin"),
                (0.0, -half, 0.0, 1.0, "south-north through the origin"),
                (-half, -half + 7500.0, 1.0, 0.0, "west-east through the southern half"),
                (-half + 7500.0, -half, 0.0, 1.0, "south-north through the western half")
            };

            foreach (var t in transects)
            {
                var r = Profile(t.X, t.Z, t.DX, t.DZ, worldSpan, in p);
                double ratio = r.Travel / math.max(1.0, r.Relief);
                if (ratio > 6.0)
                    failures.Add($"{t.Label}: {r.Travel:0} m of travel over {r.Relief:0} m of relief, " +
                                 $"ratio {ratio:0.0}, {r.Reversals} reversals across the 30 km world");
            }

            Assert.That(
                failures,
                Is.Empty,
                "Crossing the 30 km world climbs and descends its whole vertical range several times " +
                "instead of describing one margin:\n  " + string.Join("\n  ", failures) +
                "\n\nThe fix is not a wider feature. WidthNormalisedGate pins each shelf transition to " +
                "a fixed width in metres regardless of the field's wavelength, so stretching the " +
                "province structure yields fewer walls of identical steepness - measured, it moved the " +
                "world's median slope 4 degrees while cutting the transition's share of the world from " +
                "58.6% to 22.8%. Only widening the band AND enlarging the domain together changes the " +
                "shape of the profile.");
        }
    }
}
