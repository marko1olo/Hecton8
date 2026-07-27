using NUnit.Framework;
using Hecton8.World;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Pins two properties of the macro-geology height field that nothing else guards:
    /// it is deterministic for a given seed, and it actually consumes that seed.
    /// </summary>
    /// <remarks>
    /// Both failures are silent and expensive. A determinism break (a stray FloatMode.Fast on the
    /// evaluation path, cached mutable state, or an unseeded RNG) desyncs clients and makes save
    /// restore return different ground than it stored. A dropped seed is worse in a subtler way: every
    /// world generates identically, which reads as "procedural generation is boring" rather than as a
    /// bug, and no exception is ever thrown.
    /// Measured values at the time of writing, seed 12345, x=777123, z=412345 - all heights differ
    /// substantially per seed (-572.09, -3797.09, -363.19, -395.84, -1726.72 for seeds
    /// 0/1/12345/999983/0xDEADBEEF), so the margins here are not tight.
    /// Note the distinction this test protects: the FIELD consumes its seed correctly. Whether callers
    /// pass a real world seed rather than 0 is a separate plumbing concern owned elsewhere, and these
    /// tests deliberately do not assert anything about call sites.
    /// </remarks>
    [TestFixture]
    public class WorldMacroGeologyDeterminismTests
    {
        private const double SampleX = 777123.0;
        private const double SampleZ = 412345.0;

        private static float Height(double x, double z, uint seed)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(seed);
            return WorldMacroGeologyFields.EvaluateHeightMeters(x, z, in p);
        }

        [Test]
        public void SameSeed_ProducesBitIdenticalHeight()
        {
            // Sampled far from the origin so any coordinate-precision regression shows up here too.
            float first = Height(SampleX, SampleZ, 12345u);
            float second = Height(SampleX, SampleZ, 12345u);

            Assert.AreEqual(
                first,
                second,
                0f,
                "Macro-geology height is not deterministic for a fixed seed: got " + first + " then " +
                second + ". Look for FloatMode.Fast on the evaluation path, mutable static cache state, " +
                "or an unseeded random source.");
        }

        [Test]
        public void SameSeed_IsDeterministicAcrossAScatterOfCoordinates()
        {
            // One coordinate could pass by luck; sweep a spread of magnitudes and signs.
            double[] xs = { -412345.0, 0.0, 1234.5, 512.0, 777123.0 };
            double[] zs = { 9876.5, -777000.0, 0.0, 412345.0, 31.25 };

            for (int i = 0; i < xs.Length; i++)
            {
                for (int j = 0; j < zs.Length; j++)
                {
                    float a = Height(xs[i], zs[j], 999983u);
                    float b = Height(xs[i], zs[j], 999983u);
                    Assert.AreEqual(
                        a,
                        b,
                        0f,
                        "Non-deterministic macro-geology height at x=" + xs[i] + " z=" + zs[j]);
                }
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentTerrain()
        {
            // Guards the seed actually reaching the noise. Aggregated over many samples rather than
            // one point, so a coincidental collision at a single coordinate cannot mask a dropped seed.
            double checksumA = SeedChecksum(0u);
            double checksumB = SeedChecksum(12345u);
            double checksumC = SeedChecksum(0xDEADBEEFu);

            Assert.AreNotEqual(
                checksumA,
                checksumB,
                "Seeds 0 and 12345 produced an identical 2000-sample checksum (" + checksumA +
                "). The macro-geology field is ignoring its seed, so every world would generate the same.");
            Assert.AreNotEqual(
                checksumB,
                checksumC,
                "Seeds 12345 and 0xDEADBEEF produced an identical 2000-sample checksum (" + checksumB + ").");
        }

        private static double SeedChecksum(uint seed)
        {
            WorldMacroGeologyParams p = WorldMacroGeologyParams.CreateDefault(seed);
            double sum = 0.0;
            for (int i = 0; i < 2000; i++)
            {
                sum += WorldMacroGeologyFields.EvaluateHeightMeters(770000.0 + i * 31.0, SampleZ, in p);
            }

            return sum;
        }

        [Test]
        public void HeightStaysFinite_ForExtremeSeedsAndCoordinates()
        {
            uint[] seeds = { 0u, 1u, uint.MaxValue, 0xDEADBEEFu };
            double[] coords = { 0.0, -777000.0, 777000.0, 1e6 };

            for (int s = 0; s < seeds.Length; s++)
            {
                for (int c = 0; c < coords.Length; c++)
                {
                    float h = Height(coords[c], coords[coords.Length - 1 - c], seeds[s]);
                    Assert.IsFalse(
                        float.IsNaN(h) || float.IsInfinity(h),
                        "Non-finite macro-geology height for seed " + seeds[s] + " at x=" + coords[c]);
                }
            }
        }
    }
}
