using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Isolates the slope cost of the ridge term at WorldMacroGeologyFields.cs:855, octave by octave.
    ///
    /// How this term was reached. The stage sweep (WorldMacroGeologyStageSlopeSweepTests, 2026-08-09)
    /// put the steepness in stage 3 (+ridges): mean slope over the P1_origin 200 m window goes
    /// 31.3 -> 39.4 deg across that stage and 31.8 -> 44.1 deg at P5_deepfar, while stages 5 to 8
    /// together move it by less than half a degree. The width transects
    /// (WorldMacroGeologyFeatureWidthTests) then ruled out compressed mask transitions - Shelf spans
    /// 2875-13900 m against an authored 5200 m and Ridge spans 2875-5625 m against an authored
    /// 2350 m, so the masks are as wide as authored or wider. But the same transects recorded a
    /// steepest height gradient of 74-77 deg at a 25 m sampling pitch, meaning ~108 m steps between
    /// adjacent samples. Wide masks plus near-vertical height steps leaves only a short-wavelength
    /// term inside stage 3.
    ///
    /// Stage 3 is two lines. :856 multiplies by the smooth `ridgeMask`. :855 does NOT - it applies
    /// `ErodedRidge01(warpedPos * 0.00088f, seed, 5)` at an amplitude of
    /// RidgeHeightMeters * 0.65 = 1007 m. `warpedPos` is in METRES, so 0.00088 is a base lattice cell
    /// of 1136 m, and five octaves at 2x each put the finest cell at 71 m.
    ///
    /// Two properties of ErodedRidge01 make that worse than the amplitude ratio suggests:
    ///   - `n = 1 - abs(snoise)` has a cusp at the ridge line, so the derivative flips sign instead
    ///     of vanishing there. `n*n*(3-2n)` softens the corner but does not remove it.
    ///   - `weight = saturate(0.35f + n * 0.9f)` is multifractal weighting: on a crest, where the
    ///     previous octave is near 1, the next octave keeps FULL amplitude instead of being
    ///     attenuated. Fine detail is concentrated exactly where the surface is already steep.
    ///
    /// This fixture measures, it does not judge. The per-octave table is the deliverable; the one
    /// assertion only guards that the measurement is meaningful (that octaves actually change the
    /// answer, so the numbers are not all the same constant).
    /// </summary>
    [TestFixture]
    public sealed class WorldMacroGeologyRidgeOctaveSlopeTests
    {
        private const uint Seed = 880031u;

        /// <summary>The exact constants of the term under test, read off :855.</summary>
        private const float RidgeTermFrequency = 0.00088f;
        private const float RidgeTermAmplitudeMeters = 1550f * 0.65f;
        private const uint RidgeTermSeedMix = 0x3F2A1C9Bu;

        private const int SamplesPerAxis = 40;

        private static (double Mean, double Max, double Relief) MeasureTermSlope(
            double centerX, double centerZ, double windowMeters, int octaves, float amplitude)
        {
            const double probe = 12.0;
            double half = windowMeters * 0.5;
            double step = windowMeters / (SamplesPerAxis - 1);
            double sum = 0.0, max = 0.0;
            float minH = float.MaxValue, maxH = float.MinValue;
            int count = 0;

            for (int iz = 0; iz < SamplesPerAxis; iz++)
            {
                double z = centerZ - half + iz * step;
                for (int ix = 0; ix < SamplesPerAxis; ix++)
                {
                    double x = centerX - half + ix * step;

                    float c = Term(x, z, octaves, amplitude);
                    float w = Term(x - probe, z, octaves, amplitude);
                    float e = Term(x + probe, z, octaves, amplitude);
                    float s = Term(x, z - probe, octaves, amplitude);
                    float n = Term(x, z + probe, octaves, amplitude);

                    float dx = (e - w) / (float)(probe * 2.0);
                    float dz = (n - s) / (float)(probe * 2.0);
                    double deg = math.degrees(math.atan(math.sqrt(dx * dx + dz * dz)));

                    sum += deg;
                    if (deg > max) max = deg;
                    if (c < minH) minH = c;
                    if (c > maxH) maxH = c;
                    count++;
                }
            }

            return (sum / count, max, maxH - minH);
        }

        /// <summary>
        /// The :855 term in isolation. Sampled on the raw world position rather than the warped one,
        /// because the tectonic warp is a slowly varying translation (its own frequency is 0.62 of a
        /// 30 km normalised extent) and contributes almost nothing to slope at these scales, while
        /// reproducing it here would couple this measurement to code it is not testing.
        /// </summary>
        private static float Term(double x, double z, int octaves, float amplitude)
        {
            float2 p = new float2((float)x, (float)z) * RidgeTermFrequency;
            return WorldMacroGeologyFields.ErodedRidge01(p, Seed ^ RidgeTermSeedMix, octaves) * amplitude;
        }

        [Test]
        public void RidgeTerm_SlopeCostPerOctave_IsReported()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(
                $"Ridge term (WorldMacroGeologyFields.cs:855): ErodedRidge01(pos * {RidgeTermFrequency}, " +
                $"seed, N) * {RidgeTermAmplitudeMeters:0}m");
            sb.AppendLine(
                $"  base lattice cell = {1f / RidgeTermFrequency:0}m; cell at octave N = base / 2^(N-1)");
            sb.AppendLine();
            sb.AppendLine("  octaves  finest cell   mean slope   max slope   relief over 200m window");

            for (int oct = 1; oct <= 5; oct++)
            {
                double finestCell = (1.0 / RidgeTermFrequency) / math.pow(2.0, oct - 1);
                var r = MeasureTermSlope(5000.0, 5000.0, 200.0, oct, RidgeTermAmplitudeMeters);
                sb.AppendLine(
                    $"  {oct,7}  {finestCell,10:0}m  {r.Mean,10:0.0}deg  {r.Max,8:0.0}deg  {r.Relief,10:0.0}m");
            }

            sb.AppendLine();
            sb.AppendLine("  Same term at reduced amplitude, 5 octaves (isolating amplitude from octave count):");
            float[] amps = { RidgeTermAmplitudeMeters, RidgeTermAmplitudeMeters * 0.5f, RidgeTermAmplitudeMeters * 0.25f };
            foreach (float a in amps)
            {
                var r = MeasureTermSlope(5000.0, 5000.0, 200.0, 5, a);
                sb.AppendLine($"  amp={a,6:0}m  mean={r.Mean,6:0.0}deg  max={r.Max,6:0.0}deg  relief={r.Relief,7:0.0}m");
            }

            sb.AppendLine();
            sb.AppendLine("  Octave sweep at 1 km and 10 km, full amplitude (which scale each octave owns):");
            double[] windows = { 1000.0, 10000.0 };
            foreach (double win in windows)
            {
                for (int oct = 3; oct <= 5; oct++)
                {
                    var r = MeasureTermSlope(5000.0, 5000.0, win, oct, RidgeTermAmplitudeMeters);
                    sb.AppendLine($"  window={win,6:0}m octaves={oct}  mean={r.Mean,6:0.0}deg  relief={r.Relief,8:0.0}m");
                }
            }

            TestContext.WriteLine(sb.ToString());
            Assert.Pass(sb.ToString());
        }

        /// <summary>
        /// Guards the measurement above: if adding octaves 4 and 5 did not change the mean slope, then
        /// either ErodedRidge01 ignores its octave argument or this test is not sampling the field it
        /// thinks it is, and the table is worthless either way.
        ///
        /// This check exists because a degenerate noise function returning a plausible constant is
        /// exactly how this project's geology was dead for two weeks (see the 2026-07-26 misbinding
        /// fixed in 56c647caeb): the failure mode of a noise call is a smooth wrong answer, not an
        /// exception.
        /// </summary>
        [Test]
        public void OctaveCount_ActuallyChangesTheField()
        {
            var three = MeasureTermSlope(5000.0, 5000.0, 200.0, 3, RidgeTermAmplitudeMeters);
            var five = MeasureTermSlope(5000.0, 5000.0, 200.0, 5, RidgeTermAmplitudeMeters);

            Assert.That(
                math.abs(five.Mean - three.Mean),
                Is.GreaterThan(0.01),
                $"3 octaves and 5 octaves produced the same mean slope ({three.Mean:0.000} deg vs " +
                $"{five.Mean:0.000} deg). ErodedRidge01 is not responding to its octave count, so " +
                "every number in the per-octave table is meaningless.");

            Assert.That(
                five.Relief,
                Is.GreaterThan(0.5),
                $"The ridge term varies by only {five.Relief:0.000} m across a 200 m window at full " +
                "amplitude, which would mean the field is effectively constant here - the same " +
                "degeneracy signature as the 2026-07-26 seed/frequency misbinding.");
        }
    }
}
