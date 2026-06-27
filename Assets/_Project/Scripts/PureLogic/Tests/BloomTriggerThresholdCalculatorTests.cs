using NUnit.Framework;
using System;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BloomTriggerThresholdCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            var result = BloomTriggerThresholdCalculator.Compute(15f, 0.8f, 25f, 10f, 0.5f, 20f, 30f);
            Assert.That(result.bloomTriggered, Is.True);
            Assert.That(result.bloomIntensity01, Is.GreaterThan(0f).And.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            var resultExactBoundary = BloomTriggerThresholdCalculator.Compute(10f, 0.5f, 20f, 10f, 0.5f, 20f, 30f);
            Assert.That(resultExactBoundary.bloomTriggered, Is.True);
            Assert.That(resultExactBoundary.bloomIntensity01, Is.EqualTo(0f).Within(0.001f));

            var resultMissingOneCondition = BloomTriggerThresholdCalculator.Compute(9.9f, 0.8f, 25f, 10f, 0.5f, 20f, 30f);
            Assert.That(resultMissingOneCondition.bloomTriggered, Is.False);
            Assert.That(resultMissingOneCondition.bloomIntensity01, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            var result = BloomTriggerThresholdCalculator.Compute(0f, 0f, 0f, 0f, 0f, 0f, 0f);
            Assert.That(result.bloomTriggered, Is.True);
            Assert.That(result.bloomIntensity01, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            var result = BloomTriggerThresholdCalculator.Compute(-5f, -0.2f, -10f, 2f, 0.1f, -20f, -5f);
            Assert.That(result.bloomTriggered, Is.False);
            Assert.That(result.bloomIntensity01, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            var result = BloomTriggerThresholdCalculator.Compute(float.MaxValue, 1f, float.MaxValue, float.MaxValue, 1f, float.MinValue, float.MaxValue);
            Assert.That(result.bloomTriggered, Is.True);
            Assert.That(result.bloomIntensity01, Is.EqualTo(0f).Within(0.001f));

            var resultNaN = BloomTriggerThresholdCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.That(resultNaN.bloomTriggered, Is.True);
            Assert.That(resultNaN.bloomIntensity01, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
