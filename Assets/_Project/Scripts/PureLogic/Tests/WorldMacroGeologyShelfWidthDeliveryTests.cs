using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Asks whether WidthNormalisedGate actually delivers the width it is asked for, by measuring the
    /// shelf mask's 0.1-to-0.9 crossing distance in metres against the authored parameter.
    ///
    /// Why this is the question left standing. Three interventions have now been measured and they do
    /// not agree:
    ///   - reducing the lerp RANGE from 2860 m to 500 m drops the base field's median slope by 16.2 deg
    ///   - widening the BAND from 5200 m to 20800 m drops it by only 4.9
    ///   - stretching the province structure until the band covers 22.8% of the world instead of 58.6%
    ///     drops the full world's median by 4.0
    /// If the band were genuinely 5200 m wide and genuinely covering 56% of the world at 36.6 deg mean,
    /// widening it fourfold would have to flatten those cells about fourfold and the median would
    /// collapse. It does not. So either the band is not as wide as it is asked to be, or the slope in
    /// those cells is not coming from the band at all.
    ///
    /// The suspicion is structural rather than a typo. The gate estimates the distance to the isoline
    /// as (value - isoline) / |grad value|, which is a FIRST-ORDER extrapolation. It is asked to
    /// extrapolate 2600 m in a field whose own finest octave has a 5.4 km wavelength, so the linear
    /// term is not a good description over the range it is being used across. A first-order estimate
    /// used beyond its radius of validity does not fail loudly; it returns a number of the right
    /// magnitude and the wrong value, which is exactly what a band measured between 2875 m and 13900 m
    /// against an authored 5200 looks like.
    ///
    /// This fixture measures the delivered width directly, so the gate is judged on what it produces
    /// rather than on what its call site claims.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyShelfWidthDeliveryTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;
        private static readonly float HalfExtent = WorldMacroGeologyFields.MinimumWorldExtentMeters * 0.5f;

        /// <summary>
        /// 25 m pitch. Fine enough to place a 0.1-to-0.9 crossing of a band expected to be thousands of
        /// metres wide to within a percent, coarse enough that a 30 km transect is 1200 samples.
        /// </summary>
        private const double StepMeters = 25.0;

        /// <summary>
        /// Walks transects across the world and records every complete 0.1-to-0.9 (or 0.9-to-0.1)
        /// crossing of the shelf mask, in metres. Partial crossings that reverse before completing are
        /// discarded rather than counted short, because a half-crossing measured as a full one would
        /// bias the answer toward the conclusion this test exists to check.
        /// </summary>
        private static System.Collections.Generic.List<double> MeasureCrossings(
            in WorldMacroGeologyParams p, int transectCount)
        {
            var widths = new System.Collections.Generic.List<double>();
            int steps = (int)(HalfExtent * 2.0 / StepMeters);

            for (int t = 0; t < transectCount; t++)
            {
                double z = -HalfExtent + (t + 0.5) * (HalfExtent * 2.0 / transectCount);

                int crossingStart = -1;
                int direction = 0;

                for (int i = 0; i < steps; i++)
                {
                    double x = -HalfExtent + i * StepMeters;
                    WorldMacroGeologyFields.EvaluateHeightMeters(
                        x, z, in p, out WorldMacroGeologyFields.MacroMasks m);
                    float v = m.Shelf;

                    if (direction == 0)
                    {
                        if (v < 0.1f) { crossingStart = i; direction = 1; }
                        else if (v > 0.9f) { crossingStart = i; direction = -1; }
                        continue;
                    }

                    if (direction == 1)
                    {
                        if (v < 0.1f) crossingStart = i;
                        else if (v > 0.9f)
                        {
                            widths.Add((i - crossingStart) * StepMeters);
                            crossingStart = i;
                            direction = -1;
                        }
                    }
                    else
                    {
                        if (v > 0.9f) crossingStart = i;
                        else if (v < 0.1f)
                        {
                            widths.Add((i - crossingStart) * StepMeters);
                            crossingStart = i;
                            direction = 1;
                        }
                    }
                }
            }

            return widths;
        }

        private static (double Median, double P10, double P90, int Count) Summarise(
            System.Collections.Generic.List<double> widths)
        {
            if (widths.Count == 0) return (0.0, 0.0, 0.0, 0);
            widths.Sort();
            return (
                widths[widths.Count / 2],
                widths[(int)(widths.Count * 0.10)],
                widths[(int)math.min(widths.Count - 1, (int)(widths.Count * 0.90))],
                widths.Count);
        }

        [Test]
        public void ShelfBandWidth_DeliveredVsAuthored_IsReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Shelf mask 0.1-to-0.9 crossing distance measured on 24 transects across the " +
                $"{HalfExtent * 2 / 1000.0:0} km world at a {StepMeters:0} m pitch.");
            sb.AppendLine("A gate that delivers its authored width would track the first column.");
            sb.AppendLine();
            sb.AppendLine($"    {"authored",10}{"delivered p50",15}{"p10",9}{"p90",9}{"crossings",11}{"ratio",8}");

            float[] authored = { 2600f, 5200f, 10400f, 20800f, 41600f };
            foreach (float w in authored)
            {
                WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
                p.ShelfBreakWidthMeters = w;
                var summary = Summarise(MeasureCrossings(in p, 24));

                sb.AppendLine(
                    $"    {w,9:0}m{summary.Median,14:0}m{summary.P10,8:0}m{summary.P90,8:0}m" +
                    $"{summary.Count,11}{summary.Median / w,8:0.00}");
            }

            sb.AppendLine();
            sb.AppendLine("  ratio 1.00 means the authored width is what the terrain gets. A ratio that");
            sb.AppendLine("  falls as the authored width rises means the gate saturates and the widest");
            sb.AppendLine("  authored values are decorative.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// The load-bearing property. ShelfBreakWidthMeters is the only lever measured to have real
        /// authority over the world's slope, and the whole case for tuning terrain through it rather
        /// than through the authored depth constants rests on it being proportional. If doubling the
        /// authored width does not roughly double the delivered band, the parameter is a dial that
        /// stops turning and the slope budget has to be found somewhere else.
        ///
        /// The bar is deliberately loose - a doubling must deliver at least 1.4x - because the band is
        /// measured across a noisy field and the crossing distance legitimately varies with the local
        /// gradient. 1.4 is far enough above 1.0 to distinguish a working dial from a stuck one.
        /// </summary>
        [Test]
        public void DoublingTheAuthoredWidth_RoughlyDoublesTheDeliveredBand()
        {
            WorldMacroGeologyParams narrow = WorldMacroGeologyParams.CreateDefault(Seed);
            narrow.ShelfBreakWidthMeters = 5200f;
            WorldMacroGeologyParams wide = WorldMacroGeologyParams.CreateDefault(Seed);
            wide.ShelfBreakWidthMeters = 10400f;

            var narrowSummary = Summarise(MeasureCrossings(in narrow, 24));
            var wideSummary = Summarise(MeasureCrossings(in wide, 24));

            Assert.That(narrowSummary.Count, Is.GreaterThan(8),
                "too few shelf crossings inside the world to measure a band width at all");

            double ratio = wideSummary.Median / math.max(1.0, narrowSummary.Median);

            Assert.That(
                ratio,
                Is.GreaterThan(1.4),
                $"Doubling ShelfBreakWidthMeters from 5200 to 10400 m moved the delivered 0.1-to-0.9 " +
                $"band only from {narrowSummary.Median:0} m to {wideSummary.Median:0} m, a factor of " +
                $"{ratio:0.00}. WidthNormalisedGate estimates the distance to the isoline with a " +
                "first-order extrapolation, (value - isoline) / |grad|, and is being asked to " +
                "extrapolate thousands of metres in a field whose finest octave is 5.4 km. Beyond its " +
                "radius of validity that estimate returns a plausible number rather than a correct " +
                "one, so the authored width stops governing the terrain while still appearing to be " +
                "read. This is the same failure class as the width parameters that were never read at " +
                "all, one level further in: read, used, and not in control.");
        }
    }
}
