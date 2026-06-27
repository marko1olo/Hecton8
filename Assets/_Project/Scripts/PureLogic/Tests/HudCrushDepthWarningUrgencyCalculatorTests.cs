using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HudCrushDepthWarningUrgencyCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = HudCrushDepthWarningUrgencyCalculator.Compute(50f, 100f, 10f);
            Assert.AreEqual(0.6f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float resultLow = HudCrushDepthWarningUrgencyCalculator.Compute(-10f, 100f, 0f);
            Assert.AreEqual(0f, resultLow, 0.001f);

            float resultHigh = HudCrushDepthWarningUrgencyCalculator.Compute(110f, 100f, 10f);
            Assert.AreEqual(1f, resultHigh, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = HudCrushDepthWarningUrgencyCalculator.Compute(0f, 0f, 0f);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = HudCrushDepthWarningUrgencyCalculator.Compute(-50f, -100f, -10f);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float resultMax = HudCrushDepthWarningUrgencyCalculator.Compute(float.MaxValue, float.MaxValue, float.MaxValue);
            Assert.AreEqual(1f, resultMax, 0.001f);

            float resultNaN = HudCrushDepthWarningUrgencyCalculator.Compute(float.NaN, 100f, 10f);
            Assert.AreEqual(0f, resultNaN, 0.001f);
        }
    }
}
