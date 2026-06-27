using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LaserCutDepthPowerCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float depth1 = LaserCutDepthPowerCalculator.Compute(100f, 2f, 0.5f, 10f); // (100 * 0.5 * 10) / 4 = 125
            float depth2 = LaserCutDepthPowerCalculator.Compute(200f, 2f, 0.5f, 10f); // (200 * 0.5 * 10) / 4 = 250 (Higher power -> deeper)
            float depth3 = LaserCutDepthPowerCalculator.Compute(100f, 4f, 0.5f, 10f); // (100 * 0.5 * 10) / 16 = 31.25 (Larger focus -> shallower)
            float depth4 = LaserCutDepthPowerCalculator.Compute(100f, 2f, 1.0f, 10f); // (100 * 1.0 * 10) / 4 = 250 (More absorptive -> deeper)

            Assert.That(depth1, Is.EqualTo(125f).Within(0.01f));
            Assert.That(depth2, Is.EqualTo(250f).Within(0.01f));
            Assert.That(depth3, Is.EqualTo(31.25f).Within(0.01f));
            Assert.That(depth4, Is.EqualTo(250f).Within(0.01f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float depth = LaserCutDepthPowerCalculator.Compute(float.Epsilon, float.Epsilon, float.Epsilon, float.Epsilon);
            Assert.That(depth, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float depthZeroPower = LaserCutDepthPowerCalculator.Compute(0f, 2f, 0.5f, 10f);
            float depthZeroDiameter = LaserCutDepthPowerCalculator.Compute(100f, 0f, 0.5f, 10f);
            float depthZeroAbsorb = LaserCutDepthPowerCalculator.Compute(100f, 2f, 0f, 10f);
            float depthZeroTime = LaserCutDepthPowerCalculator.Compute(100f, 2f, 0.5f, 0f);

            Assert.That(depthZeroPower, Is.EqualTo(0f));
            Assert.That(depthZeroDiameter, Is.EqualTo(0f));
            Assert.That(depthZeroAbsorb, Is.EqualTo(0f));
            Assert.That(depthZeroTime, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float depthNegativePower = LaserCutDepthPowerCalculator.Compute(-100f, 2f, 0.5f, 10f);
            float depthNegativeDiameter = LaserCutDepthPowerCalculator.Compute(100f, -2f, 0.5f, 10f);

            Assert.That(depthNegativePower, Is.EqualTo(0f));
            Assert.That(depthNegativeDiameter, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float depthNaN = LaserCutDepthPowerCalculator.Compute(float.NaN, 2f, 0.5f, 10f);
            float depthInfinity = LaserCutDepthPowerCalculator.Compute(100f, float.PositiveInfinity, 0.5f, 10f);
            float depthMax = LaserCutDepthPowerCalculator.Compute(float.MaxValue, 1f, 1f, 1f);

            Assert.That(depthNaN, Is.EqualTo(0f));
            Assert.That(depthInfinity, Is.EqualTo(0f));
            Assert.That(depthMax, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
