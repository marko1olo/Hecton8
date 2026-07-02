using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ThermoclineResistanceCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = ThermoclineResistanceCalculator.Compute(100f, 100f, 20f, 10f, 1f);
            Assert.That(result, Is.GreaterThan(0f));
            Assert.That(result, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float resultEdge1 = ThermoclineResistanceCalculator.Compute(90f, 100f, 20f, 10f, 1f);
            Assert.That(resultEdge1, Is.EqualTo(0f));

            float resultEdge2 = ThermoclineResistanceCalculator.Compute(110f, 100f, 20f, 10f, 1f);
            Assert.That(resultEdge2, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = ThermoclineResistanceCalculator.Compute(0f, 0f, 0f, 0f, 0f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = ThermoclineResistanceCalculator.Compute(-100f, -100f, 20f, -10f, 1f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = ThermoclineResistanceCalculator.Compute(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
            Assert.That(float.IsNaN(result), Is.False);
            Assert.That(float.IsInfinity(result), Is.False);

            float resultNaN = ThermoclineResistanceCalculator.Compute(float.NaN, 100f, 20f, 10f, 1f);
            Assert.That(resultNaN, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ClampBounds_Case06()
        {
            // Test max bound
            float resultMax = ThermoclineResistanceCalculator.Compute(100f, 100f, 20f, 1000f, 1000f);
            Assert.That(resultMax, Is.EqualTo(1f));

            // Test min bound
            float resultMin = ThermoclineResistanceCalculator.Compute(100f, 100f, 20f, -10f, 1f);
            Assert.That(resultMin, Is.EqualTo(0f));
        }
    }
}
