using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Attributes the world's dominant terrain wavelength to the pipeline stage that introduces it.
    ///
    /// The finding this exists to explain, measured 2026-08-10. Along seven transects at four sites in
    /// three directions and at three lengths, the count of up-down cycles per 10 km came out 18.3, 18.7,
    /// 18.9, 19.0, 19.3, 20.2 and 20.2. A quantity that refuses to vary with place, heading or span is
    /// not terrain responding to geology - it is one term with one hardcoded frequency, applied
    /// everywhere. Nineteen cycles per 10 km is a wavelength of about 526 m, and the vertical travel
    /// figures put roughly 124 m of amplitude on each half cycle. 248 m of rise over 526 m of ground is
    /// 25 degrees, which is the median slope of the entire world measured independently across a 400 km
    /// square. The corrugation is not a contributor to the slope budget. It IS the slope budget.
    ///
    /// So the question is which stage adds it, and the stage dump answers that directly: reversals per
    /// 10 km measured on the same transect after each of the eight stages. The stage where the count
    /// jumps from near zero to near nineteen owns the defect, and every stage after it is innocent no
    /// matter how rough it looks.
    ///
    /// This is deliberately not a search of the source for suspicious frequency literals. There are
    /// dozens, several are gated by masks that are zero at most sites, and picking one by eye is how
    /// five earlier hypotheses in this suite were formed and refuted. The instrument narrows first.
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyStageWavelengthTests
    {
        private const uint Seed = (uint)WorldMacroGeologyFields.DefaultAuthoringSeed;
        private const double StepMeters = 100.0;
        private const double LengthMeters = 100000.0;

        private static readonly string[] StageNames =
        {
            "1 base shelf/abyss/basin",
            "2 +continent relief",
            "3 +ridges",
            "4 +trench/fault/basin",
            "5 +fold belts",
            "6 +volcano/crater/river/lake",
            "7 +strata benches",
            "8 +meso/gravel/talus",
            "9 final (erosion/spires/clamp)"
        };

        private static (int Reversals, double Travel, double Relief, double MeanSlope) Measure(
            double x0, double z0, double dx, double dz, int stage, in WorldMacroGeologyParams p)
        {
            int steps = (int)(LengthMeters / StepMeters) + 1;
            double len = math.sqrt(dx * dx + dz * dz);
            dx /= len;
            dz /= len;

            double travel = 0.0;
            float lo = float.MaxValue, hi = float.MinValue, previous = 0f;
            int reversals = 0, previousSign = 0;

            for (int i = 0; i < steps; i++)
            {
                double d = i * StepMeters;
                double sx = x0 + dx * d;
                double sz = z0 + dz * d;
                float h = stage >= 9
                    ? WorldMacroGeologyFields.EvaluateHeightMeters(sx, sz, in p)
                    : WorldMacroGeologyFields.EvaluateHeightMeters(
                        sx, sz, in p, out WorldMacroGeologyFields.MacroMasks _, stage);

                lo = math.min(lo, h);
                hi = math.max(hi, h);

                if (i > 0)
                {
                    float delta = h - previous;
                    travel += math.abs(delta);
                    int sign = delta > 2f ? 1 : delta < -2f ? -1 : 0;
                    if (sign != 0)
                    {
                        if (previousSign != 0 && sign != previousSign) reversals++;
                        previousSign = sign;
                    }
                }

                previous = h;
            }

            return (reversals, travel, hi - lo, math.degrees(math.atan(travel / (steps - 1) / StepMeters)));
        }

        [Test]
        public void DominantWavelength_IsAttributedToAStage()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Up-down cycles per 10 km after each stage, along {LengthMeters / 1000.0:0} km transects " +
                $"at a {StepMeters:0} m pitch.");
            sb.AppendLine("'wavelen' is 10000 / (cycles per 10 km). 'slope' is the mean absolute gradient.");
            sb.AppendLine();

            (double X, double Z, double DX, double DZ, string Label)[] transects =
            {
                (0.0, 0.0, 1.0, 0.0, "origin east"),
                (777000.0, -333000.0, 1.0, 0.0, "p5_deepfar east")
            };

            foreach (var t in transects)
            {
                sb.AppendLine($"  {t.Label}:");
                sb.AppendLine($"    {"stage",-32}{"cyc/10km",10}{"wavelen",10}{"travel",10}{"relief",9}{"slope",8}");
                double previousCycles = 0.0;
                for (int stage = 1; stage <= 9; stage++)
                {
                    var r = Measure(t.X, t.Z, t.DX, t.DZ, stage, in p);
                    double per10 = r.Reversals / (LengthMeters / 10000.0);
                    string wavelen = per10 > 0.05 ? $"{10000.0 / per10,8:0}m" : "       -";
                    string flag = per10 - previousCycles > 6.0 ? "   <== injected here" : "";
                    sb.AppendLine(
                        $"    {StageNames[stage - 1],-32}{per10,10:0.0}{wavelen,10}{r.Travel,9:0}m" +
                        $"{r.Relief,8:0}m{r.MeanSlope,7:0.0}{flag}");
                    previousCycles = per10;
                }
                sb.AppendLine();
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
