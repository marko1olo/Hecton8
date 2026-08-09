using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Localises WHICH pipeline stage makes the seafloor too steep, by measuring mean slope after
    /// every stage of EvaluateHeightMeters instead of guessing from amplitude constants.
    ///
    /// Why a sweep and not arithmetic: the amplitude constant of a term does not predict its slope
    /// contribution, because slope is amplitude DIVIDED BY WAVELENGTH and both are buried in the
    /// call. A 45 m fracture term at a 400 m wavelength is gentle; a 15 m talus term at a 14 m
    /// wavelength is a cliff. Reading the constants ranks them in the wrong order.
    ///
    /// Motivating measurement (WorldMacroGeologySlopeBudgetTests, 2026-08-09): the slope budget is
    /// not uniformly bad, it is bimodal. P2_near (Shelf=92%) reads mean 18.3 deg and P3_west
    /// (Basin=99%) reads mean 13.6 deg - both healthy - while P1_origin, P4_far and P5_deepfar read
    /// mean 43.3/45.0/47.1 deg. The three bad sites are the ones the atlas shows as Ridge+Fault
    /// heavy. So the steepness is injected by something gated on ridge/fault, not by the base field.
    ///
    /// This fixture asserts nothing about the geology. It is a measuring instrument whose output is
    /// the point, and it fails only if the instrument itself is broken (a stage that cannot possibly
    /// be inert returning a byte-identical result to the stage before it).
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyStageSlopeSweepTests
    {
        private const uint Seed = 880031u;
        private const int SamplesPerAxis = 24;

        /// <summary>
        /// Two pathological sites and one healthy control. P5_deepfar is the worst reading (0.0% of
        /// its 200 m window under 25 deg) and P3_west the best (mean 13.6 deg), so any stage that
        /// explains the difference must show up as a gap between these two columns.
        /// </summary>
        private static readonly (double X, double Z, string Label)[] Sites =
        {
            (5000.0, 5000.0, "P1_origin  BAD"),
            (300000.0, 90000.0, "P4_far     BAD"),
            (777000.0, -333000.0, "P5_deepfar WORST"),
            (-40000.0, 15000.0, "P3_west    control")
        };

        private static readonly (int Stage, string Name)[] Stages =
        {
            (1, "base shelf/abyss"),
            (2, "+continentRelief"),
            (3, "+ridges"),
            (4, "+trench/fault/basin"),
            (5, "+folds"),
            (6, "+volcano/crater/river/lake/mesa/dune/reef"),
            (7, "+strata"),
            (8, "+mesoFracture/gravel/talus"),
            (0, "FULL (+coastal/spires/ceiling)")
        };

        private struct StageReading
        {
            public double MeanDegrees;
            public double MaxDegrees;
            public double Relief;
            public double PctOver35;
        }

        private static StageReading MeasureStage(
            double centerX, double centerZ, double windowMeters, int stage)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            const double probe = 12.0;
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);

            double sum = 0.0;
            double max = 0.0;
            int over35 = 0;
            int count = 0;
            float minH = float.MaxValue;
            float maxH = float.MinValue;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;

                    float c = WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p, out _, stage);
                    float w = WorldMacroGeologyFields.EvaluateHeightMeters(x - probe, z, in p, out _, stage);
                    float e = WorldMacroGeologyFields.EvaluateHeightMeters(x + probe, z, in p, out _, stage);
                    float s = WorldMacroGeologyFields.EvaluateHeightMeters(x, z - probe, in p, out _, stage);
                    float n = WorldMacroGeologyFields.EvaluateHeightMeters(x, z + probe, in p, out _, stage);

                    float dx = (e - w) / (float)(probe * 2.0);
                    float dz = (n - s) / (float)(probe * 2.0);
                    double deg = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));

                    sum += deg;
                    if (deg > max) max = deg;
                    if (deg >= 35.0) over35++;
                    if (c < minH) minH = c;
                    if (c > maxH) maxH = c;
                    count++;
                }
            }

            return new StageReading
            {
                MeanDegrees = sum / count,
                MaxDegrees = max,
                Relief = maxH - minH,
                PctOver35 = 100.0 * over35 / count
            };
        }

        /// <summary>
        /// The whole point of this fixture: mean slope after each stage, at the scale where the
        /// defect is worst (200 m), for every site. Read the DELTA between consecutive rows - the
        /// stage that owns the problem is the one whose row jumps on the BAD sites and not on the
        /// control.
        /// </summary>
        [Test]
        public void StageSweep_At200m_IsReported()
        {
            ReportSweep(200.0);
        }

        /// <summary>
        /// Same sweep at 1 km. A term whose wavelength is longer than 200 m can only be seen here,
        /// and terrain.md's meso acceptance row is a 1 km card, so this is the scale the spec judges.
        /// </summary>
        [Test]
        public void StageSweep_At1km_IsReported()
        {
            ReportSweep(1000.0);
        }

        private static void ReportSweep(double window)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Mean slope (deg) after each pipeline stage, {window:0} m window, 12 m probe:");
            sb.Append("  ".PadRight(48));
            for (int s = 0; s < Sites.Length; s++)
                sb.Append(Sites[s].Label.PadLeft(20));
            sb.AppendLine();

            var previous = new double[Sites.Length];
            for (int k = 0; k < Stages.Length; k++)
            {
                sb.Append($"  {Stages[k].Stage}. {Stages[k].Name}".PadRight(48));
                for (int s = 0; s < Sites.Length; s++)
                {
                    StageReading r = MeasureStage(Sites[s].X, Sites[s].Z, window, Stages[k].Stage);
                    double delta = k == 0 ? 0.0 : r.MeanDegrees - previous[s];
                    previous[s] = r.MeanDegrees;
                    string cell = k == 0
                        ? $"{r.MeanDegrees,6:0.0}"
                        : $"{r.MeanDegrees,6:0.0} ({delta,+5:0.0})";
                    sb.Append(cell.PadLeft(20));
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("Relief (m) and %>35deg at the final stage:");
            for (int s = 0; s < Sites.Length; s++)
            {
                StageReading full = MeasureStage(Sites[s].X, Sites[s].Z, window, 0);
                sb.AppendLine(
                    $"  {Sites[s].Label,-20} relief={full.Relief,8:0.0}m  " +
                    $"max={full.MaxDegrees,5:0.0}deg  >35deg={full.PctOver35,5:0.0}%");
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Guards the instrument, not the geology. Stage 3 adds the ridge crest and stage 4 adds
        /// trench/fault/basin; on a ridge-heavy site neither can be a no-op. If either returns a mean
        /// slope identical to the stage before it, the early-return was inserted in the wrong place
        /// and every conclusion drawn from the sweep above is void.
        ///
        /// This exists because the stage dump advertised eight stages and implemented three until
        /// 2026-08-09 - a caller asking for stage 7 silently received the full pipeline. A sweep
        /// built on an unverified instrument reproduces that failure with more decimal places.
        /// </summary>
        [Test]
        public void StageDumpEarlyReturns_ActuallyTakeEffect()
        {
            // P1_origin: the atlas measures Ridge=45.9% and Fault=52.1% in its 200 m window.
            StageReading s2 = MeasureStage(5000.0, 5000.0, 200.0, 2);
            StageReading s3 = MeasureStage(5000.0, 5000.0, 200.0, 3);
            StageReading s4 = MeasureStage(5000.0, 5000.0, 200.0, 4);
            StageReading s8 = MeasureStage(5000.0, 5000.0, 200.0, 8);

            Assert.That(
                math.abs(s3.MeanDegrees - s2.MeanDegrees),
                Is.GreaterThan(0.001),
                $"Stage 3 (+ridges) returned the same mean slope as stage 2 ({s2.MeanDegrees:0.000} deg) " +
                "on a window the atlas measures as 45.9% ridge. The stage-3 early-return is misplaced " +
                "or the ridge term is inert.");

            Assert.That(
                math.abs(s4.MeanDegrees - s3.MeanDegrees),
                Is.GreaterThan(0.001),
                $"Stage 4 (+trench/fault/basin) returned the same mean slope as stage 3 " +
                $"({s3.MeanDegrees:0.000} deg) on a window the atlas measures as 52.1% fault.");

            Assert.That(
                math.abs(s8.MeanDegrees - s4.MeanDegrees),
                Is.GreaterThan(0.001),
                $"Stage 8 returned the same mean slope as stage 4 ({s4.MeanDegrees:0.000} deg), so " +
                "every stage from 5 to 8 is inert here - or the stage-8 early-return never fires.");
        }
    }
}
