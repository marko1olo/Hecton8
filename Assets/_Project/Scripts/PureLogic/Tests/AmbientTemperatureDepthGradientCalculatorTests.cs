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

        /// <summary>
        /// maxLatitude is both a clamp bound and a divisor. Cases 01-05 always used the
        /// default 90f, so two degenerate paths went undetected: maxLatitude == 0 evaluated
        /// 0f/0f and returned NaN, and a negative maxLatitude gave Math.Clamp inverted bounds,
        /// throwing ArgumentException out of an allocation-free calculator.
        /// </summary>
        [Test]
        public void Test_DegenerateMaxLatitude_Case06()
        {
            float zeroBand = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 0f, 0f);
            Assert.That(float.IsNaN(zeroBand), Is.False, "zero maxLatitude must not divide by zero");
            Assert.That(zeroBand, Is.LessThan(25f), "temperature must still fall with depth");

            Assert.DoesNotThrow(
                () => AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 45f, -90f),
                "negative maxLatitude must not throw from Math.Clamp inverted bounds");

            float negativeBand = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 45f, -90f);
            float positiveBand = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 45f, 90f);
            Assert.That(negativeBand, Is.EqualTo(positiveBand).Within(0.001f),
                "a sign slip on the band reads as its magnitude");

            float nonFiniteBand = AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 45f, float.NaN);
            Assert.That(float.IsNaN(nonFiniteBand), Is.False, "non-finite maxLatitude must not leak");
        }

        /// <summary>
        /// Guard additions must not alter results for the default 90-degree band.
        /// </summary>
        [Test]
        public void Test_DefaultBandBehaviourUnchanged_Case07()
        {
            Assert.That(AmbientTemperatureDepthGradientCalculator.Compute(25f, 100f, 0f),
                Is.EqualTo(10.4612f).Within(0.01f));
            Assert.That(AmbientTemperatureDepthGradientCalculator.Compute(25f, 100000f, 90f),
                Is.EqualTo(-2f).Within(0.01f));
            Assert.That(AmbientTemperatureDepthGradientCalculator.Compute(15f, -50f, -100f),
                Is.EqualTo(15f).Within(0.001f));
        }
    }
}
