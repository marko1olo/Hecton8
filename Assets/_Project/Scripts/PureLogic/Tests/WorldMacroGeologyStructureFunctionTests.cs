using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Reports the height field's structure function: the mean absolute height difference between two
    /// points a given distance apart, swept across distances from 25 m to 25 km.
    ///
    /// This is the measurement that identifies WHICH SCALE carries the slope, which no mean-slope
    /// number can. Mean slope at a 12 m probe answers "how steep is it here"; the structure function
    /// answers "what size of thing makes it steep", and only the second one points at a term in the
    /// source.
    ///
    /// How to read it, corrected 2026-08-10 after the first reading was wrong. The column that matters
    /// is diff/lag - the mean gradient contributed at that separation, printed as an angle.
    ///
    /// A FLAT REGION AT FINE LAGS IS NOT EVIDENCE OF FRACTALITY. Below its finest feature any smooth
    /// surface is differentiable, so mean |dh| = |grad h| x lag exactly and diff/lag is constant. That
    /// is what the 25-800 m plateau shows: the field has no content below roughly 200 m, which is the
    /// finest octave of the finest term in stage 1. The plateau's VALUE is the payload - it is simply
    /// the mean gradient magnitude of the field, and 0.288 means 16.1 degrees.
    ///
    /// The informative part is where the curve DECLINES, above about 1600 m. That is where separation
    /// starts to exceed the size of individual landforms and the two samples stop being correlated.
    ///
    /// The first reading of this table treated the fine plateau as proof of a scale-invariant fractal
    /// with Hurst exponent 1, blamed the fBm octave gain of 0.5, and predicted that lowering the gain
    /// would gentle the world. WorldMacroGeologyGainSweepTests then measured the raw generator and
    /// found a fine/coarse ratio of 5.01x at gain 0.50 falling only to 3.70x at gain 0.34 - the gain
    /// is not the lever, and a five-octave fBm is band-limited so the Hurst asymptotics never applied.
    /// Both mistakes are recorded here because the plateau will look like fractality to the next
    /// reader too.
    ///
    /// What the table DOES establish, and this part held up: the mean gradient of the base
    /// shelf/abyss/basin field is 0.288 before a single landform is added, ridges take it to 0.360,
    /// trench/fault/basin to 0.443, and everything after that to 0.454. The world is steep because of
    /// stage 1, stage 3 and stage 4 in that order, and stages 5 through 9 are effectively inert.
    ///
    /// Why it is being run, 2026-08-10. The stage sweep showed the base shelf/abyss/basin field alone
    /// already produces 15.6 degrees of mean slope at the origin and 23.7 at P5_deepfar, before a
    /// single landform is added, and that stages 5 through 9 together contribute about 0.1 degrees.
    /// Stage 1 contains only three terms - a lerp between AbyssDepth and ShelfDepth gated by the shelf
    /// mask, a basin term worth 217 m, and a 28 m roughness at a 1667 m wavelength - and the arithmetic
    /// of none of them obviously reaches 15 degrees. Rather than guess between them, this measures
    /// where the energy is.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyStructureFunctionTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;

        private static readonly double[] LagsMeters =
        {
            25.0, 50.0, 100.0, 200.0, 400.0, 800.0, 1600.0, 3200.0, 6400.0, 12800.0, 25600.0
        };

        /// <summary>
        /// Pairs are taken along both axes and both diagonals from each of many origins, so a field
        /// with a directional grain cannot read as isotropic. 240 origins spread over 240 km at each
        /// lag keeps the estimate stable without making the sweep unaffordable.
        /// </summary>
        private const int OriginsPerLag = 240;
        private const double OriginSpreadMeters = 240000.0;

        private static double MeanAbsoluteDifference(
            double baseX, double baseZ, double lag, int stage, in WorldMacroGeologyParams p)
        {
            double sum = 0.0;
            int count = 0;
            double step = OriginSpreadMeters / OriginsPerLag;

            for (int i = 0; i < OriginsPerLag; i++)
            {
                // Walk the origins along a diagonal so successive samples are not collinear with the
                // pair separation, which would correlate every measurement with the same transect.
                double ox = baseX + i * step * 0.7071;
                double oz = baseZ + i * step * 0.7071;

                float h0 = Sample(ox, oz, stage, in p);
                sum += math.abs(Sample(ox + lag, oz, stage, in p) - h0);
                sum += math.abs(Sample(ox, oz + lag, stage, in p) - h0);
                sum += math.abs(Sample(ox + lag * 0.7071, oz + lag * 0.7071, stage, in p) - h0);
                sum += math.abs(Sample(ox + lag * 0.7071, oz - lag * 0.7071, stage, in p) - h0);
                count += 4;
            }

            return sum / count;
        }

        private static float Sample(double x, double z, int stage, in WorldMacroGeologyParams p)
        {
            return stage >= 9
                ? WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p)
                : WorldMacroGeologyFields.EvaluateHeightMeters(
                    x, z, in p, out WorldMacroGeologyFields.MacroMasks _, stage);
        }

        [Test]
        public void StructureFunction_PerStage_IsReported()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Mean |height difference| between points separated by 'lag', and the angle that");
            sb.AppendLine("difference subtends. The lag with the largest angle is the scale that owns the slope.");
            sb.AppendLine();

            (int Stage, string Label)[] stages =
            {
                (1, "stage 1: base shelf/abyss/basin"),
                (3, "stage 3: +continent relief, +ridges"),
                (4, "stage 4: +trench/fault/basin"),
                (9, "stage 9: final field")
            };

            foreach (var s in stages)
            {
                sb.AppendLine($"  {s.Label}");
                sb.AppendLine($"    {"lag",9}{"mean diff",12}{"diff/lag",11}{"angle",9}");

                double peakAngle = 0.0;
                double peakLag = 0.0;
                foreach (double lag in LagsMeters)
                {
                    double diff = MeanAbsoluteDifference(0.0, 0.0, lag, s.Stage, in p);
                    double ratio = diff / lag;
                    double angle = math.degrees(math.atan(ratio));
                    if (angle > peakAngle) { peakAngle = angle; peakLag = lag; }
                    sb.AppendLine($"    {lag,8:0}m{diff,11:0.0}m{ratio,11:0.000}{angle,8:0.0}");
                }

                sb.AppendLine($"    peak at lag {peakLag:0} m, {peakAngle:0.0} deg. NOTE: the peak lands " +
                              "at the finest lag for");
                sb.AppendLine("    every stage, which does NOT mean the landform is 50 m across - it means the " +
                              "field is");
                sb.AppendLine("    smooth below its finest feature, so diff/lag is just |grad h| there. Read the " +
                              "plateau");
                sb.AppendLine("    VALUE as the mean gradient, and read the lag where the curve starts to fall as " +
                              "the");
                sb.AppendLine("    landform scale.");
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
