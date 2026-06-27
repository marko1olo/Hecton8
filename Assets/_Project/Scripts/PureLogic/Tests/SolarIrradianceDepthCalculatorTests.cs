using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SolarIrradianceDepthCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float depth = 20f;
            float surfaceIrradiance = 1000f;
            float attenuation = 0.045f;
            float efficiency = 0.2f;

            // Act
            float result = SolarIrradianceDepthCalculator.Compute(depth, surfaceIrradiance, attenuation, efficiency);

            // Assert
            // 20 * 0.045 = 0.9. Exp(-0.9) = 0.4065696597. 1000 * 0.4065696597 * 0.2 = 81.3139
            Assert.That(result, Is.EqualTo(81.3139f).Within(0.01f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float depth = 200f; // High depth
            float surfaceIrradiance = 1361f;
            float attenuation = 0.045f;
            float efficiency = 1f;

            // Act
            float result = SolarIrradianceDepthCalculator.Compute(depth, surfaceIrradiance, attenuation, efficiency);

            // Assert
            // 200 * 0.045 = 9. Exp(-9) = 0.0001234
            // 1361 * 0.0001234 * 1 = 0.168
            Assert.That(result, Is.EqualTo(0.168f).Within(0.01f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float depth = 0f;
            float surfaceIrradiance = 0f;
            float attenuation = 0f;
            float efficiency = 0f;

            // Act
            float result = SolarIrradianceDepthCalculator.Compute(depth, surfaceIrradiance, attenuation, efficiency);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify zero inputs are handled without divide-by-zero or exception.");

            float result2 = SolarIrradianceDepthCalculator.Compute(0f, 1000f, 0.045f, 0.5f);
            // Exp(0) = 1, 1000 * 1 * 0.5 = 500
            Assert.That(result2, Is.EqualTo(500f), "Verify zero depth works.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float depth = -10f;
            float surfaceIrradiance = -100f;
            float attenuation = -0.5f;
            float efficiency = -0.1f;

            // Act
            float result = SolarIrradianceDepthCalculator.Compute(depth, surfaceIrradiance, attenuation, efficiency);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float depth = float.NaN;
            float surfaceIrradiance = float.PositiveInfinity;
            float attenuation = float.NaN;
            float efficiency = float.NegativeInfinity;

            // Act
            float result = SolarIrradianceDepthCalculator.Compute(depth, surfaceIrradiance, attenuation, efficiency);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify robust calculation and overflow protection.");

            float extremeDepth = 1000000f;
            float resultExtremeDepth = SolarIrradianceDepthCalculator.Compute(extremeDepth, 1000f, 0.045f, 1f);
            // Will clamp optical depth to 40, exp(-40) ~ 4.2e-18, effectively 0
            Assert.That(resultExtremeDepth, Is.EqualTo(0f).Within(0.0001f));
        }
    }
}
