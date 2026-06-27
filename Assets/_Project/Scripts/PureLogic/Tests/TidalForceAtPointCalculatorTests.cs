using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class TidalForceAtPointCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Full moon overhead: max tidal.
            float resultFullMoon = TidalForceAtPointCalculator.Compute(0f, 0f, 10f, 1f);
            Assert.AreEqual(1.0f, resultFullMoon, 0.01f);

            // New moon: second peak.
            float resultNewMoon = TidalForceAtPointCalculator.Compute(180f, 0f, 10f, 1f);
            Assert.AreEqual(0.5f, resultNewMoon, 0.01f);

            // Quarter moon: minimal.
            float resultQuarterMoon = TidalForceAtPointCalculator.Compute(90f, 0f, 10f, 1f);
            Assert.AreEqual(0.0f, resultQuarterMoon, 0.01f);

            // Equatorial: higher than polar.
            float resultPolar = TidalForceAtPointCalculator.Compute(0f, 90f, 10f, 1f);
            Assert.AreEqual(0.0f, resultPolar, 0.01f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Negative latitude clamps to -90
            float resultNegLat = TidalForceAtPointCalculator.Compute(0f, -100f, 10f, 1f);
            Assert.AreEqual(0.0f, resultNegLat, 0.01f);

            // Angle out of bounds 0-360 wraps around
            float resultWrap = TidalForceAtPointCalculator.Compute(360f, 0f, 10f, 1f);
            Assert.AreEqual(1.0f, resultWrap, 0.01f);

            float resultWrapNeg = TidalForceAtPointCalculator.Compute(-180f, 0f, 10f, 1f);
            Assert.AreEqual(0.5f, resultWrapNeg, 0.01f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Zero inputs
            float resultZero = TidalForceAtPointCalculator.Compute(0f, 0f, 0f, 0f);
            Assert.AreEqual(0.0f, resultZero, 0.01f);

            // Zero grav param
            float resultZeroGrav = TidalForceAtPointCalculator.Compute(0f, 0f, 10f, 0f);
            Assert.AreEqual(0.0f, resultZeroGrav, 0.01f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Negative amplitude and grav param clamp to 0
            float resultNegAmp = TidalForceAtPointCalculator.Compute(0f, 0f, -10f, 1f);
            Assert.AreEqual(0.0f, resultNegAmp, 0.01f);

            float resultNegGrav = TidalForceAtPointCalculator.Compute(0f, 0f, 10f, -1f);
            Assert.AreEqual(0.0f, resultNegGrav, 0.01f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Extremely large parameters
            float resultLarge = TidalForceAtPointCalculator.Compute(0f, 0f, float.MaxValue, float.MaxValue);
            // Will overflow but shouldn't crash, and normalizes to 1 or 0
            Assert.IsTrue(resultLarge >= 0f && resultLarge <= 1f);

            // NaN inputs
            float resultNaN = TidalForceAtPointCalculator.Compute(float.NaN, 0f, 10f, 1f);
            Assert.AreEqual(0.0f, resultNaN, 0.01f);

            // Infinity inputs
            float resultInf = TidalForceAtPointCalculator.Compute(float.PositiveInfinity, 0f, 10f, 1f);
            Assert.AreEqual(0.0f, resultInf, 0.01f);
        }
    }
}
