using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WeightPenaltyCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentWeightKg = 50f;
            float maxCarryKg = 100f;
            float penaltyStartFraction = 0.4f; // Penalty starts at 40kg
            float maxSpeedPenalty01 = 0.5f;

            // Act
            float result = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);

            // Assert: Verify expected output behaviour
            // loadFraction = 50 / 100 = 0.5
            // penalizedFraction = (0.5 - 0.4) / (1.0 - 0.4) = 0.1 / 0.6 = 0.1666...
            // multiplier = 1 - (0.1666... * 0.5) = 1 - 0.08333... = 0.91666...
            Assert.That(result, Is.EqualTo(0.9166667f).Within(0.0001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxCarryKg = 100f;
            float penaltyStartFraction = 0.5f;
            float maxSpeedPenalty01 = 0.5f;

            // Act & Assert

            // Exactly at penalty start
            Assert.That(WeightPenaltyCurveCalculator.Compute(50f, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01), Is.EqualTo(1f), "Penalty should be 0 exactly at penalty start.");

            // Just below penalty start
            Assert.That(WeightPenaltyCurveCalculator.Compute(49.9f, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01), Is.EqualTo(1f), "Penalty should be 0 below penalty start.");

            // Exactly at max carry
            Assert.That(WeightPenaltyCurveCalculator.Compute(100f, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01), Is.EqualTo(0.5f), "Penalty should be max at max carry.");

            // Over max carry (clamped)
            Assert.That(WeightPenaltyCurveCalculator.Compute(150f, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01), Is.EqualTo(0.5f), "Penalty should be clamped over max carry.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act & Assert
            float result = WeightPenaltyCurveCalculator.Compute(0f, 0f, 0f, 0f);

            Assert.That(result, Is.EqualTo(1f), "Verify zero inputs are handled without divide-by-zero or exception.");

            float result2 = WeightPenaltyCurveCalculator.Compute(50f, 0f, 0.5f, 0.5f);
            Assert.That(result2, Is.EqualTo(0.5f), "Verify zero maxCarry handles correctly and clamps to max penalty.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float result = WeightPenaltyCurveCalculator.Compute(-10f, -50f, -0.5f, -0.5f);
            // clamped currentWeight = 0, clamped maxCarry = 0.0001
            // loadFraction = 0. penaltyStart = 0. penalized = 0. multiplier = 1 - (0 * 0) = 1.

            // Assert
            Assert.That(result, Is.EqualTo(1f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act & Assert
            float infResult = WeightPenaltyCurveCalculator.Compute(float.PositiveInfinity, 100f, 0.5f, 0.5f);
            Assert.That(infResult, Is.EqualTo(1f), "Infinity current weight should be sanitized to 0, resulting in 1 multiplier.");

            float nanResult = WeightPenaltyCurveCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.That(nanResult, Is.EqualTo(1f), "NaN inputs should be sanitized to safe defaults, resulting in 1 multiplier.");

            float extremeResult = WeightPenaltyCurveCalculator.Compute(1e30f, 1e20f, 0.5f, 0.5f);
            Assert.That(extremeResult, Is.EqualTo(0.5f), "Extreme values should correctly clamp to max penalty.");
        }
    }
}
