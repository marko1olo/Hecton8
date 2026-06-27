using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WeldHeatDissipationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float weldTemp = 1000f;
            float waterTemp = 20f;
            float area = 0.5f;
            float coeff = 200f; // Rapid cooling
            float dt = 1f;
            float mass = 2f;
            float specificHeat = 500f; // J/kgK

            // Act
            var result = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, area, coeff, dt, mass, specificHeat);

            // Assert
            Assert.That(result.newWeldTemp, Is.LessThan(weldTemp));
            Assert.That(result.newWeldTemp, Is.GreaterThan(waterTemp));
            Assert.That(result.heatDissipatedJoules, Is.GreaterThan(0f));

            // Expected new temp = 20 + 980 * exp(-(200*0.5)/(2*500) * 1) = 20 + 980 * exp(-0.1) = 20 + 980 * 0.904837 = 906.74
            Assert.That(result.newWeldTemp, Is.EqualTo(906.74f).Within(0.1f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Weld at water temp: no dissipation.
            float weldTemp = 20f;
            float waterTemp = 20f;
            float area = 0.5f;
            float coeff = 200f;
            float dt = 1f;
            float mass = 2f;
            float specificHeat = 500f;

            // Act
            var result = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, area, coeff, dt, mass, specificHeat);

            // Assert
            Assert.That(result.newWeldTemp, Is.EqualTo(weldTemp));
            Assert.That(result.heatDissipatedJoules, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Zero area, zero coeff, zero dt
            float weldTemp = 1000f;
            float waterTemp = 20f;

            var result1 = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, 0f, 200f, 1f, 2f, 500f);
            var result2 = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, 0.5f, 0f, 1f, 2f, 500f);
            var result3 = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, 0.5f, 200f, 0f, 2f, 500f);

            // Assert
            Assert.That(result1.newWeldTemp, Is.EqualTo(weldTemp));
            Assert.That(result2.newWeldTemp, Is.EqualTo(weldTemp));
            Assert.That(result3.newWeldTemp, Is.EqualTo(weldTemp));

            Assert.That(result1.heatDissipatedJoules, Is.EqualTo(0f));
            Assert.That(result2.heatDissipatedJoules, Is.EqualTo(0f));
            Assert.That(result3.heatDissipatedJoules, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Negative area, coeff, dt should clamp to 0
            float weldTemp = 1000f;
            float waterTemp = 20f;

            // Act
            var result = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, -0.5f, -200f, -1f, 2f, 500f);

            // Assert
            Assert.That(result.newWeldTemp, Is.EqualTo(weldTemp));
            Assert.That(result.heatDissipatedJoules, Is.EqualTo(0f));

            // Negative mass / specific heat should throw
            Assert.Throws<ArgumentOutOfRangeException>(() => WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, 0.5f, 200f, 1f, -2f, 500f));
            Assert.Throws<ArgumentOutOfRangeException>(() => WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, 0.5f, 200f, 1f, 2f, -500f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: NaN and Infinity
            float weldTemp = 1000f;
            float waterTemp = 20f;

            // Act
            var resultNaN = WeldHeatDissipationCalculator.Compute(float.NaN, waterTemp, 0.5f, 200f, 1f, 2f, 500f);
            var resultInf = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, float.PositiveInfinity, 200f, 1f, 2f, 500f);

            // Assert
            Assert.That(resultNaN.heatDissipatedJoules, Is.EqualTo(0f));
            Assert.That(resultNaN.newWeldTemp, Is.EqualTo(0f)); // Safe default

            Assert.That(resultInf.heatDissipatedJoules, Is.EqualTo(0f));
            Assert.That(resultInf.newWeldTemp, Is.EqualTo(weldTemp));

            // Very large dt (instant cooling)
            var resultLongTime = WeldHeatDissipationCalculator.Compute(weldTemp, waterTemp, 0.5f, 200f, 1000000f, 2f, 500f);
            Assert.That(resultLongTime.newWeldTemp, Is.EqualTo(waterTemp).Within(0.01f));
        }
    }
}
