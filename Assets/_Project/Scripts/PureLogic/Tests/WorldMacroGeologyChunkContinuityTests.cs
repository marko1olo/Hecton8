using NUnit.Framework;
using Hecton8.World;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Guards the macro-geology height field against chunk-aligned discontinuities.
    /// </summary>
    /// <remarks>
    /// Regression cover for R100. BuildSample used to subtract a per-chunk anchor,
    /// floor(posD / ChunkSizeMeters) * ChunkSizeMeters, from the warped world position before
    /// narrowing to float2, intending to preserve ULP precision at large absolute coordinates.
    /// Every consumer of that value is non-periodic fBm on a global simplex lattice, so the
    /// staircase translated the whole noise domain by one chunk at each boundary. Measured on the
    /// shipped code at x = 776704: a 34.46 m height step between samples 0.25 m apart, against
    /// 0.07 m of legitimate variation mid-chunk.
    /// These tests compare boundary behaviour against a mid-chunk control rather than against a
    /// fixed threshold, so they stay valid if terrain amplitude or seed is retuned. Any future
    /// change that reintroduces a domain translation on a chunk stride fails them.
    /// </remarks>
    [TestFixture]
    public class WorldMacroGeologyChunkContinuityTests
    {
        private const uint Seed = 12345u;
        private const double SampleZ = 412345.0;
        private const double StepMeters = 0.25;
        private const double WindowMeters = 4.0;

        // A boundary is only flagged when it is far worse than ordinary terrain, so legitimately
        // steep geology (the field reaches ~5 m/m) cannot trip the test. The historical defect
        // exceeded its control by 475x, so this leaves an enormous margin.
        private const double AllowedBoundaryToControlRatio = 10.0;

        private static double WorstAdjacentDelta(double centreX, in WorldMacroGeologyParams p)
        {
            double worst = 0.0;
            bool hasPrevious = false;
            float previous = 0f;

            for (double x = centreX - WindowMeters; x <= centreX + WindowMeters; x += StepMeters)
            {
                float height = WorldMacroGeologyFields.EvaluateHeightMeters(x, SampleZ, in p);
                if (hasPrevious)
                {
                    double delta = height - previous;
                    if (delta < 0.0)
                        delta = -delta;
                    if (delta > worst)
                        worst = delta;
                }

                previous = height;
                hasPrevious = true;
            }

            return worst;
        }

        [Test]
        public void HeightField_IsContinuous_AcrossChunkBoundariesAtAupScale()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double chunk = p.ChunkSizeMeters;
            Assert.Greater(chunk, 0.0, "ChunkSizeMeters must be positive for this test to mean anything.");

            // Deliberately sampled far from the origin: the defect only appeared once the absolute
            // coordinate was large enough for an anchor to be worth subtracting.
            double firstBoundary = System.Math.Floor(777000.0 / chunk) * chunk;

            for (int i = 0; i < 8; i++)
            {
                double boundary = firstBoundary + i * chunk;
                double atBoundary = WorstAdjacentDelta(boundary, in p);
                double control = WorstAdjacentDelta(boundary + chunk * 0.5, in p);
                double allowed = control * AllowedBoundaryToControlRatio + 1.0;

                Assert.LessOrEqual(
                    atBoundary,
                    allowed,
                    "Chunk-aligned discontinuity at x=" + boundary.ToString("F1") +
                    ". Worst adjacent height delta was " + atBoundary.ToString("F4") +
                    " m over a " + StepMeters.ToString("F2") + " m step, against a mid-chunk control of " +
                    control.ToString("F4") + " m. This is the R100 defect signature: something is " +
                    "translating the noise domain on a chunk stride again.");
            }
        }

        [Test]
        public void HeightField_StaysFinite_AcrossChunkBoundariesAtAupScale()
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);
            double chunk = p.ChunkSizeMeters;
            double firstBoundary = System.Math.Floor(777000.0 / chunk) * chunk;

            for (double x = firstBoundary - WindowMeters; x <= firstBoundary + chunk * 8.0; x += chunk * 0.125)
            {
                float height = WorldMacroGeologyFields.EvaluateHeightMeters(x, SampleZ, in p);
                Assert.IsFalse(
                    float.IsNaN(height) || float.IsInfinity(height),
                    "Non-finite macro-geology height at x=" + x.ToString("F1"));
            }
        }

        [Test]
        public void HeightField_RetainsShelfToAbyssRelief()
        {
            // The continuity test above would also pass on a perfectly flat world, so pin the
            // amplitude too: removing the anchor must not have collapsed the field.
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(Seed);

            float min = float.MaxValue;
            float max = float.MinValue;
            for (double x = 770000.0; x <= 790000.0; x += 25.0)
            {
                float height = WorldMacroGeologyFields.EvaluateHeightMeters(x, SampleZ, in p);
                if (height < min)
                    min = height;
                if (height > max)
                    max = height;
            }

            // Measured range over this transect is ~3213 m; assert well under that so seed or
            // amplitude retuning does not cause false failures.
            Assert.Greater(
                max - min,
                500.0f,
                "Macro-geology relief collapsed over a 20 km transect: range was " +
                (max - min).ToString("F1") + " m. The field should span shelf to abyssal depth.");
        }
    }
}
