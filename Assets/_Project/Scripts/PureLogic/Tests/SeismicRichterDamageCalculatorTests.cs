using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SeismicRichterDamageCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = SeismicRichterDamageCalculator.Compute(7f, 1f, 0f, 0f);
            Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = SeismicRichterDamageCalculator.Compute(10f, 0.5f, 0f, 0f);
            Assert.That(result, Is.EqualTo(1f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = SeismicRichterDamageCalculator.Compute(0f, 0f, 0f, 0f);
            Assert.That(result, Is.GreaterThanOrEqualTo(0f).And.LessThan(0.01f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = SeismicRichterDamageCalculator.Compute(-2f, -100f, -0.5f, -0.5f);
            Assert.That(float.IsNaN(result), Is.False);
            Assert.That(result, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float resultInfinity = SeismicRichterDamageCalculator.Compute(float.PositiveInfinity, 10f, 0f, 0f);
            float resultNaN = SeismicRichterDamageCalculator.Compute(float.NaN, 10f, 0f, 0f);
            Assert.That(resultInfinity, Is.EqualTo(0f));
            Assert.That(resultNaN, Is.EqualTo(0f));
        }

        [Test]
        public void Test_IntegrityAndDamping_Reduction()
        {
            float resultNoProtection = SeismicRichterDamageCalculator.Compute(7f, 1f, 0f, 0f);
            float resultHighProtection = SeismicRichterDamageCalculator.Compute(7f, 1f, 0.9f, 0.5f);
            Assert.That(resultHighProtection, Is.LessThan(resultNoProtection));
        }

        [Test]
        public void Test_NegligibleDamage_AtDistance()
        {
            float result = SeismicRichterDamageCalculator.Compute(2f, 100f, 0f, 0f);
            Assert.That(result, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
