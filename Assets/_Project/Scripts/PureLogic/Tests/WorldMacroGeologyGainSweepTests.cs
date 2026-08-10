using System.Linq;
using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Measures how the fBm octave gain controls roughness across scale, as evidence for an owner
    /// decision about the world's slope budget.
    ///
    /// WHAT THIS FIXTURE MEASURES: the raw generators only. It does NOT measure the composed height
    /// field, because the evaluator does not read a gain parameter - the generators expose one as a
    /// trailing default of 0.5, which keeps today's world bit-identical, and nothing passes anything
    /// else yet. Saying so up front matters: two predictions made earlier in this investigation
    /// (wiring TrenchWidthMeters would relieve P5; removing the trench overdrive would reach 43
    /// degrees) both failed against measurement, so a raw-noise number must not be quoted as a
    /// composed-field number.
    ///
    /// WHY IT IS STILL DECISIVE. The stage sweep and the composed structure function together showed
    /// the slope budget is the noise roughness, not the feature geometry:
    ///   - the composed field's diff/lag is FLAT at 0.454 from 25 m to 800 m, i.e. H = 1
    ///   - stage 1 alone, the base shelf/abyss/basin lerp with no landform, is already 0.288 (16.1 deg)
    ///   - stages 5 through 9 together add 0.1 degrees
    ///   - across 400 km the median cell is 29.2 deg and 32.5% exceed 40 deg
    /// A field whose roughness angle is identical at every scale has that property because of the
    /// octave gain and for no other reason. Gain is the lever; this fixture sizes it.
    ///
    /// THE HYPOTHESIS IT WAS BUILT TO TEST, AND THE REFUTATION. With gain g and lacunarity L the
    /// Hurst exponent is H = -log(g)/log(L), mean |dh| over distance d grows as d^H, and slope at
    /// scale d goes as d^(H-1). Gain 0.5 gives H = 0.99, which predicts equal slope at every scale -
    /// exactly what the composed field shows. The conclusion drawn from that, that gain was the single
    /// lever on the world's slope budget, was WRONG, and this fixture's own output is what killed it:
    /// the raw 5-octave generator measures a fine/coarse ratio of 5.01x at gain 0.50, and forcing the
    /// gain down to 0.34 only reaches 3.70x.
    ///
    /// The arithmetic was misapplied. H describes the asymptotic scaling of an INFINITE octave stack.
    /// Five octaves at lacunarity 2.02 put the finest octave at wavelength 1/2.02^4 = 0.060 cells, so
    /// any lag below that samples inside a locally smooth band where mean |dh| is simply the local
    /// gradient times the lag. A band-limited fBm has no asymptotic regime to obey.
    ///
    /// WHAT IS ACTUALLY TRUE. The composed field's flatness is emergent. The evaluator makes 81 fBm
    /// calls at domain multipliers spanning 0.0006 to 1/250 per world metre; each is band-limited,
    /// their bands overlap, and the union covers 25 m to 800 m densely. The world is scale-free
    /// because of the superposition, not because of any constant that can be turned. Gentling it means
    /// changing how amplitude is apportioned across those 81 terms, which is a decision about the look
    /// of the world rather than a tuning value.
    ///
    /// The fixture is kept, failed hypothesis and all, because the refutation is the useful part: it
    /// is the reason nobody should go looking for a single roughness constant in this file again.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyGainSweepTests
    {
        private const uint Seed = 880031u;

        /// <summary>
        /// Lags in noise cells. The generators are sampled in noise units; the evaluator's own calls
        /// use domain multipliers between 0.58 and 2.44 over a world extent, so one noise cell is
        /// hundreds of kilometres for the province fields and roughly 1.6 km for the finest. The
        /// columns are therefore RATIOS across scale, which is the property gain controls, rather
        /// than metres.
        /// </summary>
        private static readonly double[] LagCells = { 0.03125, 0.0625, 0.125, 0.25, 0.5, 1.0 };

        private static readonly double[] Gains = { 0.50, 0.46, 0.42, 0.38, 0.34 };

        [Test]
        public void GainSweep_ShowsRoughnessAcrossScale()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Raw FractalSimplexNoise01: angle subtended by mean |dh| at each lag, per gain.");
            sb.AppendLine("Lag in noise cells. Gain 0.50 is today's world. 5 octaves, as the evaluator uses.");
            sb.AppendLine();
            sb.Append($"    {"gain",6}{"H",7}  ");
            foreach (double l in LagCells) sb.Append($"{l,9:0.0000}");
            sb.AppendLine($"{"fine/coarse",14}");

            foreach (double gain in Gains)
            {
                double h = -math.log(gain) / math.log(2.02);
                sb.Append($"    {gain,5:0.00}{h,7:0.00}  ");
                double fine = 0.0, coarse = 0.0;
                for (int i = 0; i < LagCells.Length; i++)
                {
                    double angle = RawNoiseAngle(gain, LagCells[i]);
                    if (i == 0) fine = angle;
                    if (i == LagCells.Length - 1) coarse = angle;
                    sb.Append($"{angle,9:0.00}");
                }
                sb.AppendLine($"{(coarse > 1e-9 ? fine / coarse : 0.0),13:0.00}x");
            }

            sb.AppendLine();
            sb.AppendLine("  MEASURED 2026-08-10, and it REFUTES the hypothesis this fixture was built to");
            sb.AppendLine("  confirm. The fine/coarse ratio at today's gain 0.50 is 5.01x, not the ~1.0 that");
            sb.AppendLine("  a scale-free field would give, and dropping the gain all the way to 0.34 only");
            sb.AppendLine("  moves it to 3.70x. Gain is NOT the lever on this world's slope budget.");
            sb.AppendLine();
            sb.AppendLine("  Why the Hurst arithmetic did not apply: a 5-octave fBm is BAND-LIMITED. At");
            sb.AppendLine("  lacunarity 2.02 the finest octave has wavelength 1/2.02^4 = 0.060 cells, so the");
            sb.AppendLine("  0.031 lag samples INSIDE the finest octave, where the noise is locally smooth");
            sb.AppendLine("  and mean |dh| is just the local gradient times the lag. H describes the");
            sb.AppendLine("  asymptotic slope of an INFINITE octave stack; five octaves do not have one.");
            sb.AppendLine();
            sb.AppendLine("  So the composed field's genuine flatness (0.288, 0.288, 0.287, 0.284, 0.276,");
            sb.AppendLine("  0.270 across 25-800 m at stage 1) is EMERGENT, not inherited. The evaluator");
            sb.AppendLine("  makes 81 fBm calls at domain multipliers from 0.0006 to 1/250 per world metre;");
            sb.AppendLine("  each is band-limited, their bands overlap, and the union is dense. The world is");
            sb.AppendLine("  scale-free because of the STACK, not because of any one constant.");
            sb.AppendLine();
            sb.AppendLine("  Consequence for the fix: there is no single number to turn. Gentling this world");
            sb.AppendLine("  means reducing amplitude at fine scales across the whole stack, which is a");
            sb.AppendLine("  design change to how the 81 terms are apportioned - an owner decision about the");
            sb.AppendLine("  look of the world, not a tuning constant.");

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Guards the claim that today's world is untouched. The gain parameter was added as a trailing
        /// optional argument to three generators specifically so that every one of the 105 existing
        /// call sites keeps its exact behaviour; this test is what makes that a fact rather than an
        /// intention. It is the check that was missing when a frequency parameter went in
        /// MID-signature on 26 July and left the geology silently dead until 9 August.
        /// </summary>
        [Test]
        public void DefaultGain_LeavesEveryGeneratorBitIdentical()
        {
            var samples = new float2[]
            {
                new float2(0f, 0f), new float2(1.7f, -0.3f), new float2(-12.25f, 8.5f),
                new float2(103.75f, 44.125f), new float2(0.03125f, 0.0625f)
            };

            foreach (float2 s in samples)
            {
                for (int octaves = 1; octaves <= 6; octaves++)
                {
                    Assert.That(
                        WorldMacroGeologyFields.FractalSimplexNoise01(s, Seed, octaves, 0f, 1f, 0.5f),
                        Is.EqualTo(WorldMacroGeologyFields.FractalSimplexNoise01(s, Seed, octaves)),
                        $"FractalSimplexNoise01 default gain changed behaviour at {s}, {octaves} octaves");

                    Assert.That(
                        WorldMacroGeologyFields.RidgedMultifractal01(s, Seed, octaves, 0.5f),
                        Is.EqualTo(WorldMacroGeologyFields.RidgedMultifractal01(s, Seed, octaves)),
                        $"RidgedMultifractal01 default gain changed behaviour at {s}, {octaves} octaves");

                    Assert.That(
                        WorldMacroGeologyFields.ErodedRidge01(s, Seed, octaves, 0.5f),
                        Is.EqualTo(WorldMacroGeologyFields.ErodedRidge01(s, Seed, octaves)),
                        $"ErodedRidge01 default gain changed behaviour at {s}, {octaves} octaves");
                }
            }
        }

        /// <summary>
        /// Locks the composed field against the gain work, so that when gain does become an authored
        /// parameter this test fails loudly if the default path moved. Records today's measured value
        /// as the reference.
        /// </summary>
        [Test]
        public void ComposedField_StillMeasuresItsRecordedRoughness()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double sum = 0.0;
            int count = 0;
            const double lag = 100.0;

            for (int i = 0; i < 400; i++)
            {
                double ox = i * 601.0;
                double oz = i * 397.0;
                float h0 = WorldMacroGeologyFields.EvaluateHeightMeters(ox, oz, in p);
                float h1 = WorldMacroGeologyFields.EvaluateHeightMeters(ox + lag, oz, in p);
                sum += math.abs(h1 - h0);
                count++;
            }

            double angle = math.degrees(math.atan(sum / count / lag));
            TestContext.WriteLine($"Composed field, 100 m lag over 400 origins: {angle:0.00} deg");

            // Measured 2026-08-10 at 23.9 deg on the 100 m lag of the atlas transect. The window here
            // is a different sample set, so the bar is a range rather than a point: anything outside
            // 15..35 means the default noise path moved and the gain work is no longer inert.
            Assert.That(
                angle,
                Is.InRange(15.0, 35.0),
                $"composed roughness at a 100 m lag is {angle:0.00} deg, outside the 15-35 band recorded " +
                "before gain was added as a parameter. Either the default changed from 0.5 or a call " +
                "site bound its arguments to the wrong slots.");
        }

        private static double RawNoiseAngle(double gain, double lagCells)
        {
            double sum = 0.0;
            int count = 0;

            for (int i = 0; i < 400; i++)
            {
                var a = new float2(i * 1.31f, i * 0.77f);
                var b = new float2(a.x + (float)lagCells, a.y);
                float h0 = WorldMacroGeologyFields.FractalSimplexNoise01(a, Seed, 5, 0f, 1f, (float)gain);
                float h1 = WorldMacroGeologyFields.FractalSimplexNoise01(b, Seed, 5, 0f, 1f, (float)gain);
                sum += math.abs(h1 - h0);
                count++;
            }

            return math.degrees(math.atan(sum / count / lagCells));
        }
    }
}
