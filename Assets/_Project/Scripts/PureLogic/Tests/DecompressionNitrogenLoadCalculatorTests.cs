using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DecompressionNitrogenLoadCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs (e.g. diving)
            float currentLoad = 0.79f; // surface N2 pressure
            float breathingGasPressure = 1.58f; // ~10m depth on air
            float halflimeMinutes = 5f;
            float deltaMinutes = 5f;

            // Act
            float result = DecompressionNitrogenLoadCalculator.Compute(currentLoad, breathingGasPressure, halflimeMinutes, deltaMinutes);

            // Assert: Verify expected output behaviour
            // After 1 halftime (5 mins), load should be exactly halfway between 0.79 and 1.58
            // 0.79 + (1.58 - 0.79) * 0.5 = 1.185
            Assert.That(result, Is.EqualTo(1.185f).Within(0.01f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentLoad = 2.0f;
            float breathingGasPressure = 2.0f; // Equilibrium
            float halflimeMinutes = 1f;
            float deltaMinutes = 10f;

            // Act
            float result = DecompressionNitrogenLoadCalculator.Compute(currentLoad, breathingGasPressure, halflimeMinutes, deltaMinutes);

            // Assert: Should remain at equilibrium
            Assert.That(result, Is.EqualTo(2.0f).Within(0.001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentLoad = 0f;
            float breathingGasPressure = 0f;
            float halflimeMinutes = 0f; // Should clamp to small positive safe halftime
            float deltaMinutes = 0f;

            // Act
            float result = DecompressionNitrogenLoadCalculator.Compute(currentLoad, breathingGasPressure, halflimeMinutes, deltaMinutes);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentLoad = -5f; // Should clamp to 0
            float breathingGasPressure = -10f; // Should clamp to 0
            float halflimeMinutes = -2f; // Should clamp to small safe halftime
            float deltaMinutes = -1f; // Should clamp to 0

            // Act
            float result = DecompressionNitrogenLoadCalculator.Compute(currentLoad, breathingGasPressure, halflimeMinutes, deltaMinutes);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentLoad = float.PositiveInfinity;
            float breathingGasPressure = float.NaN;
            float halflimeMinutes = float.PositiveInfinity;
            float deltaMinutes = float.NaN;

            // Act
            float result = DecompressionNitrogenLoadCalculator.Compute(currentLoad, breathingGasPressure, halflimeMinutes, deltaMinutes);

            // Assert
            Assert.That(float.IsNaN(result), Is.False, "Result must not be NaN.");
            Assert.That(float.IsInfinity(result), Is.False, "Result must not be Infinity.");
            Assert.That(result, Is.GreaterThanOrEqualTo(0f), "Verify robust calculation and overflow protection.");
        }
    }
}
