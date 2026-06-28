using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AmbientTemperatureDepthGradientCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 0f);
            Assert.Less(result, 25f);
            Assert.Greater(result, 2.0f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100000f, 0f);
            Assert.AreEqual(2.0f, result, 0.01f);
            float result2 = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100000f, 90f);
            Assert.AreEqual(-2.0f, result2, 0.01f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = AmbientTemperatureDepthGradientCalculator.Compute(20f, 0f, 0f);
            Assert.AreEqual(20f, result);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = AmbientTemperatureDepthGradientCalculator.Compute(15f, -50f, -100f);
            Assert.AreEqual(15f, result);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result1 = AmbientTemperatureDepthGradientCalculator.Compute(float.PositiveInfinity, 100f, 0f);
            Assert.AreEqual(0f, result1);
            float result2 = AmbientTemperatureDepthGradientCalculator.Compute(20f, float.NaN, 0f);
            Assert.AreEqual(20f, result2);
        }
    }
}
