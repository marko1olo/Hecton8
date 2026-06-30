using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ActiveSonarAttenuationCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float pingPower = 0.8f;
            float distance = 10f;
            float turbidity = 0.05f; // Round trip 20. Attenuation = exp(-0.05 * 20) = exp(-1) = 0.3678794
            // Act
            float result = ActiveSonarAttenuationCurveCalculator.Compute(pingPower, distance, turbidity);
            // Assert
            Assert.That(result, Is.EqualTo(0.2943035f).Within(0.0001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float resultClampedHigh = ActiveSonarAttenuationCurveCalculator.Compute(2.0f, 0f, 0f);

            // Act & Assert
            Assert.That(resultClampedHigh, Is.EqualTo(1.0f), "Verify boundary constraints clamp correctly to maximum 1.0.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float resultAllZero = ActiveSonarAttenuationCurveCalculator.Compute(0f, 0f, 0f);
            float resultZeroDistance = ActiveSonarAttenuationCurveCalculator.Compute(0.5f, 0f, 0.1f);

            // Act & Assert
            Assert.That(resultAllZero, Is.EqualTo(0f), "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.That(resultZeroDistance, Is.EqualTo(0.5f), "Zero distance means no attenuation.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float resultNegatives = ActiveSonarAttenuationCurveCalculator.Compute(-1f, -10f, -0.1f);

            // Act & Assert
            Assert.That(resultNegatives, Is.EqualTo(0f), "Verify negative inputs clamp gracefully and return 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultLargeDist = ActiveSonarAttenuationCurveCalculator.Compute(1f, 1000000f, 0.1f);
            float resultNaN = ActiveSonarAttenuationCurveCalculator.Compute(float.NaN, 10f, 0.1f);
            float resultInf = ActiveSonarAttenuationCurveCalculator.Compute(1f, float.PositiveInfinity, 0.1f);

            // Act & Assert
            Assert.That(resultLargeDist, Is.EqualTo(0f).Within(0.00001f), "Verify robust calculation and overflow protection (decay goes to 0).");
            Assert.That(resultNaN, Is.EqualTo(0f), "NaN input should return 0.");
            Assert.That(resultInf, Is.EqualTo(0f), "Infinity input should return 0.");
        }
    }
}
