using Hecton8.World;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.PureLogic.Tests
{
    /// <summary>
    /// Characterises <see cref="WorldMacroGeologyFields.DoubleFractalSimplexNoise01"/> at the
    /// argument shape the macro-geology and material code actually calls it with.
    ///
    /// The method is declared
    /// <c>DoubleFractalSimplexNoise01(double2 posD, float frequency, uint seed, int octaves = 5)</c>
    /// and there is exactly one overload. Every call site in
    /// <c>WorldMacroGeologyFields.cs</c> and <c>WorldTerrainDetailContracts.cs</c> passes THREE
    /// arguments in the shape <c>(scaledPosition, seed ^ constant, smallInteger)</c>, with the
    /// spatial frequency already multiplied into the position. Under C#'s implicit conversions that
    /// binds <c>seed ^ constant</c> to <c>frequency</c> (uint -&gt; float) and the small integer to
    /// <c>seed</c> (int literal -&gt; uint), leaving <c>octaves</c> at its default of 5.
    ///
    /// A frequency near 2^31 multiplied into the position inside
    /// <c>DoubleSimplex2D</c> (<c>double2 p = posD * frequency</c>) drives the skewed lattice
    /// coordinate far past <c>int</c> range before it is cast at
    /// <c>int cellX = (int)skewedFloor.x</c>, so the lattice cell can stop depending on position.
    ///
    /// These tests do not assert that this is a defect - they MEASURE whether the returned field
    /// still varies with position at the call shape in use, so the answer is a number rather than
    /// an argument. Read the failure text: it reports the actual values, not a verdict.
    /// </summary>
    [TestFixture]
    public sealed class MacroGeologyNoiseCallShapeTests
    {
        private const uint WorldSeed = 880031u;

        /// <summary>
        /// Reproduces the exact call shape used at
        /// <c>WorldTerrainDetailContracts.cs</c> for <c>provinceJitter</c>:
        /// <c>DoubleFractalSimplexNoise01(new double2(x, z) / 900.0, seed ^ 0x51A7E531u, 3)</c>.
        /// Two points 70 km apart must not return the same value from a spatial noise field.
        /// </summary>
        [Test]
        public void ThreeArgumentCallShape_StillVariesWithPosition()
        {
            float a = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                new double2(1024.0, 2048.0) / 900.0, WorldSeed ^ 0x51A7E531u, 3);
            float b = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                new double2(71680.0, 43008.0) / 900.0, WorldSeed ^ 0x51A7E531u, 3);
            float c = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                new double2(-15000.0, 9000.0) / 900.0, WorldSeed ^ 0x51A7E531u, 3);

            Assert.That(
                math.abs(a - b) + math.abs(a - c),
                Is.GreaterThan(1e-6f),
                $"Noise returned a position-independent value at the three-argument call shape. " +
                $"a={a:R} b={b:R} c={c:R}. The second argument binds to the 'frequency' parameter, " +
                $"so frequency = (float)(seed ^ 0x51A7E531u) = " +
                $"{(float)(WorldSeed ^ 0x51A7E531u):R}.");
        }

        /// <summary>
        /// Control: the same noise called with the DECLARED four-argument shape, with a sane
        /// spatial frequency and the world seed in the seed slot. If this varies while the test
        /// above does not, the difference is the argument binding and nothing else.
        /// </summary>
        [Test]
        public void FourArgumentCallShape_VariesWithPosition()
        {
            float a = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                new double2(1024.0, 2048.0), 1f / 900f, WorldSeed ^ 0x51A7E531u, 3);
            float b = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                new double2(71680.0, 43008.0), 1f / 900f, WorldSeed ^ 0x51A7E531u, 3);

            Assert.That(
                math.abs(a - b),
                Is.GreaterThan(1e-6f),
                $"Declared four-argument shape produced no positional variation either: " +
                $"a={a:R} b={b:R}. That would move the defect below the call shape, into " +
                $"DoubleSimplex2D itself.");
        }

        /// <summary>
        /// The field must stay inside 0..1 whatever the binding does, because every consumer feeds
        /// it straight into <c>smoothstep</c>/<c>lerp</c> as a normalised mask.
        /// </summary>
        [Test]
        public void ThreeArgumentCallShape_StaysNormalised()
        {
            for (int i = 0; i < 32; i++)
            {
                double x = -20000.0 + i * 1700.0;
                float v = WorldMacroGeologyFields.DoubleFractalSimplexNoise01(
                    new double2(x, x * 0.37) / 240.0, WorldSeed ^ 0xB34ACE21u, 3);

                Assert.IsTrue(math.isfinite(v), $"Non-finite noise at x={x}: {v:R}");
                Assert.That(v, Is.InRange(0f, 1f), $"Noise left 0..1 at x={x}: {v:R}");
            }
        }
    }
}
