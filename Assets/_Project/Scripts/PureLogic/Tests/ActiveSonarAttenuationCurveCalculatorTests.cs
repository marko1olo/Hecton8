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
            // Arrange: Setup standard test inputs
            float pingPower = 1f;
            float distance = 10f;
            float turbidity = 0.1f;

            // Act
            float result = ActiveSonarAttenuationCurveCalculator.Compute(pingPower, distance, turbidity);

            // Assert: Verify expected output behaviour
            float expected = (float)Math.Exp(-2.0);
            Assert.That(result, Is.EqualTo(expected).Within(0.001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float pingPower = 2f; // Should be clamped to 1f output max
            float distance = 10f;
            float turbidity = 0.1f;

            // Act
            float result = ActiveSonarAttenuationCurveCalculator.Compute(pingPower, distance, turbidity);

            // Assert
            float rawResult = pingPower * (float)Math.Exp(-2.0);
            float expected = Math.Clamp(rawResult, 0f, 1f);
            Assert.That(result, Is.EqualTo(expected).Within(0.001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float resultZeroDistance = ActiveSonarAttenuationCurveCalculator.Compute(1f, 0f, 0f);
            float resultZeroPower = ActiveSonarAttenuationCurveCalculator.Compute(0f, 10f, 0.1f);

            // Assert
            Assert.That(resultZeroDistance, Is.EqualTo(1f).Within(0.0001f), "Zero distance/turbidity should result in no attenuation (1.0).");
            Assert.That(resultZeroPower, Is.EqualTo(0f).Within(0.0001f), "Zero power should return 0.0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float resultNegativePower = ActiveSonarAttenuationCurveCalculator.Compute(-1f, 10f, 0.1f);
            float resultNegativeDistance = ActiveSonarAttenuationCurveCalculator.Compute(1f, -10f, -0.1f);

            // Assert
            Assert.That(resultNegativePower, Is.EqualTo(0f), "Negative ping power should clamp to 0.0.");
            Assert.That(resultNegativeDistance, Is.EqualTo(1f).Within(0.0001f), "Negative distance and turbidity should clamp to 0 resulting in 1.0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float resultNaN = ActiveSonarAttenuationCurveCalculator.Compute(float.NaN, 10f, 0.1f);
            float resultInfDistance = ActiveSonarAttenuationCurveCalculator.Compute(1f, float.PositiveInfinity, 0.1f);
            float resultInfTurbidity = ActiveSonarAttenuationCurveCalculator.Compute(1f, 10f, float.PositiveInfinity);
            float resultInfPower = ActiveSonarAttenuationCurveCalculator.Compute(float.PositiveInfinity, 0f, 0f);

            // Assert
            Assert.That(resultNaN, Is.EqualTo(0f), "NaN inputs should safely return 0.");
            Assert.That(resultInfDistance, Is.EqualTo(0f), "Infinite distance should safely return 0.");
            Assert.That(resultInfTurbidity, Is.EqualTo(0f), "Infinite turbidity should safely return 0.");
            Assert.That(resultInfPower, Is.EqualTo(1f), "Infinite power at zero distance should safely clamp and return 1.");
        }
    }
}
