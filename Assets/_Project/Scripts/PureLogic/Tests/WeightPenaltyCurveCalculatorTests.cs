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
            float penaltyStartFraction = 0.5f;
            float maxSpeedPenalty01 = 0.5f;

            // Act
            float speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);

            // Assert: Verify expected output behaviour
            // 50kg / 100kg = 0.5 load fraction.
            // penaltyStartFraction is 0.5, so we are at the start of the penalty.
            // Expected multiplier should be 1f.
            Assert.AreEqual(1f, speedMultiplier, 0.001f);

            // Increase weight to 75kg
            currentWeightKg = 75f;
            speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);
            // 75kg / 100kg = 0.75 load.
            // penaltyRange = 0.5. activePenalty = (0.75 - 0.5) / 0.5 = 0.5
            // speedMultiplier = 1f - 0.5 * 0.5 = 0.75f
            Assert.AreEqual(0.75f, speedMultiplier, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxCarryKg = 100f;
            float penaltyStartFraction = 0f;
            float maxSpeedPenalty01 = 0.5f;

            // Act
            // Boundary 1: At max weight
            float currentWeightKg = 100f;
            float speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);

            // Assert
            Assert.AreEqual(0.5f, speedMultiplier, 0.001f);

            // Boundary 2: Over max weight
            currentWeightKg = 150f;
            speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);
            Assert.AreEqual(0.5f, speedMultiplier, 0.001f); // should cap at minSpeedMultiplier
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentWeightKg = 0f;
            float maxCarryKg = 0f;
            float penaltyStartFraction = 0f;
            float maxSpeedPenalty01 = 0f;

            // Act
            float speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);

            // Assert
            // 0 weight should yield 1f
            Assert.AreEqual(1f, speedMultiplier, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentWeightKg = -50f;
            float maxCarryKg = -100f;
            float penaltyStartFraction = -0.5f;
            float maxSpeedPenalty01 = -0.5f;

            // Act
            float speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);

            // Assert
            // negative weight clamped to 0 -> load = 0 -> speed = 1f
            Assert.AreEqual(1f, speedMultiplier, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentWeightKg = float.PositiveInfinity;
            float maxCarryKg = float.NaN;
            float penaltyStartFraction = float.NegativeInfinity;
            float maxSpeedPenalty01 = float.NaN;

            // Act
            float speedMultiplier = WeightPenaltyCurveCalculator.Compute(currentWeightKg, maxCarryKg, penaltyStartFraction, maxSpeedPenalty01);

            // Assert
            // infinity and nan clamped gracefully without exception
            Assert.AreEqual(1f, speedMultiplier, 0.001f);
        }
    }
}
