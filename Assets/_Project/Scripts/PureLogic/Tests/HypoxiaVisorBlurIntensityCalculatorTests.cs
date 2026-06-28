using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HypoxiaVisorBlurIntensityCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            var result = HypoxiaVisorBlurIntensityCalculator.Compute(0.85f, 5f, 1f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result.X, Is.EqualTo(0f));
            Assert.That(result.Y, Is.EqualTo(0f));

            var result2 = HypoxiaVisorBlurIntensityCalculator.Compute(0.4f, 5f, 1f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result2.X, Is.GreaterThan(0f));
            Assert.That(result2.Y, Is.GreaterThan(0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            var result = HypoxiaVisorBlurIntensityCalculator.Compute(0.8f, 5f, 1f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result.X, Is.EqualTo(0f));

            var result2 = HypoxiaVisorBlurIntensityCalculator.Compute(0.2f, 5f, 1f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result2.X, Is.EqualTo(0.75f));

            var result3 = HypoxiaVisorBlurIntensityCalculator.Compute(0.199f, 5f, 1f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result3.X, Is.GreaterThan(0.75f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            var result = HypoxiaVisorBlurIntensityCalculator.Compute(0f, 0f, 0f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result.X, Is.EqualTo(1f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            var result = HypoxiaVisorBlurIntensityCalculator.Compute(-1f, -10f, -5f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result.X, Is.EqualTo(1f));
        }

        [Test]
        public void Test_NaNInputs_Case06()
        {
            var result = HypoxiaVisorBlurIntensityCalculator.Compute(float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result.X, Is.EqualTo(0f));
            Assert.That(result.Y, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            var result = HypoxiaVisorBlurIntensityCalculator.Compute(0.1f, 1000f, 1000f, 0.8f, 0.2f, 10f, 0.5f, 5f, 10f);
            Assert.That(result.X, Is.EqualTo(10f));
            Assert.That(result.Y, Is.EqualTo(5f));
        }
    }
}
