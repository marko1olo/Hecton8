using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BuoyancyDensityRatioMathTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Equal densities: zero net force.
            Assert.That(BuoyancyDensityRatioMath.Calculate(1000f, 1000f, 1f, 9.81f), Is.EqualTo(0f).Within(0.001f));

            // Less dense (player 500, fluid 1000): positive lift.
            Assert.That(BuoyancyDensityRatioMath.Calculate(500f, 1000f, 1f, 9.81f), Is.EqualTo(500f * 1f * 9.81f).Within(0.001f));

            // More dense (player 2000, fluid 1000): negative (sinks).
            Assert.That(BuoyancyDensityRatioMath.Calculate(2000f, 1000f, 1f, 9.81f), Is.EqualTo(-1000f * 1f * 9.81f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Zero gravity: zero force
            Assert.That(BuoyancyDensityRatioMath.Calculate(500f, 1000f, 1f, 0f), Is.EqualTo(0f));

            // Zero displaced volume: zero force
            Assert.That(BuoyancyDensityRatioMath.Calculate(500f, 1000f, 0f, 9.81f), Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // All zeros
            Assert.That(BuoyancyDensityRatioMath.Calculate(0f, 0f, 0f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Negative density should be clamped to 0
            // player -100 (clamp 0), fluid 1000 -> 1000 * 1 * 9.81 = 9810
            Assert.That(BuoyancyDensityRatioMath.Calculate(-100f, 1000f, 1f, 9.81f), Is.EqualTo(9810f).Within(0.001f));

            // Negative fluid density should be clamped to 0
            // player 500, fluid -100 (clamp 0) -> (0 - 500) * 1 * 9.81 = -4905
            Assert.That(BuoyancyDensityRatioMath.Calculate(500f, -100f, 1f, 9.81f), Is.EqualTo(-4905f).Within(0.001f));

            // Negative volume should be clamped to 0
            Assert.That(BuoyancyDensityRatioMath.Calculate(500f, 1000f, -10f, 9.81f), Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // NaNs
            Assert.That(BuoyancyDensityRatioMath.Calculate(float.NaN, 1000f, 1f, 9.81f), Is.EqualTo(9810f).Within(0.001f));
            Assert.That(BuoyancyDensityRatioMath.Calculate(500f, float.NaN, 1f, 9.81f), Is.EqualTo(-4905f).Within(0.001f));

            // Infinity
            Assert.That(BuoyancyDensityRatioMath.Calculate(float.PositiveInfinity, 1000f, 1f, 9.81f), Is.EqualTo(9810f).Within(0.001f)); // clamped/zeroed
        }
    }
}
