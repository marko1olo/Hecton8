using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ScarcityPriceSpikeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentSupply = 5f;
            float demandRate = 10f;
            float basePrice = 100f;
            float scarcityElasticity = 5f;

            // Act
            float result = ScarcityPriceSpikeCalculator.Compute(currentSupply, demandRate, basePrice, scarcityElasticity);

            // Assert: Verify expected output behaviour
            // Scarcity level = (10 - 5) / 10 = 0.5
            // Multiplier = 1 + (0.5 * 5) = 1 + 2.5 = 3.5
            // Price = 100 * 3.5 = 350
            Assert.AreEqual(350f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentSupply = 10f;
            float demandRate = 10f;
            float basePrice = 100f;
            float scarcityElasticity = 5f;

            // Act
            float result1 = ScarcityPriceSpikeCalculator.Compute(currentSupply, demandRate, basePrice, scarcityElasticity); // Abundant supply (supply >= demand)
            float result2 = ScarcityPriceSpikeCalculator.Compute(15f, 10f, 100f, 5f); // Oversupply
            float result3 = ScarcityPriceSpikeCalculator.Compute(0f, 10f, 100f, 5f); // Zero supply (max multiplier)

            // Assert
            Assert.AreEqual(100f, result1, 0.001f, "Verify boundary constraints clamp correctly: Supply == Demand");
            Assert.AreEqual(100f, result2, 0.001f, "Verify boundary constraints clamp correctly: Supply > Demand");
            Assert.AreEqual(600f, result3, 0.001f, "Verify boundary constraints clamp correctly: Zero Supply (max multiplier = 1 + 5 = 6)");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result1 = ScarcityPriceSpikeCalculator.Compute(0f, 0f, 100f, 5f); // Zero demand
            float result2 = ScarcityPriceSpikeCalculator.Compute(5f, 10f, 0f, 5f); // Zero base price
            float result3 = ScarcityPriceSpikeCalculator.Compute(5f, 10f, 100f, 0f); // Zero elasticity

            // Assert
            Assert.AreEqual(100f, result1, 0.001f, "Verify zero demand doesn't cause divide-by-zero, just returns base price.");
            Assert.AreEqual(0f, result2, 0.001f, "Verify zero base price returns 0.");
            Assert.AreEqual(100f, result3, 0.001f, "Verify zero elasticity doesn't change the base price.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float result1 = ScarcityPriceSpikeCalculator.Compute(-5f, 10f, 100f, 5f); // Negative supply -> should act as 0
            float result2 = ScarcityPriceSpikeCalculator.Compute(5f, -10f, 100f, 5f); // Negative demand -> should act as 0
            float result3 = ScarcityPriceSpikeCalculator.Compute(5f, 10f, -100f, 5f); // Negative price -> should act as 0
            float result4 = ScarcityPriceSpikeCalculator.Compute(5f, 10f, 100f, -5f); // Negative elasticity -> should act as 0

            // Assert
            Assert.AreEqual(600f, result1, 0.001f, "Negative supply clamped to 0 -> price spikes to max.");
            Assert.AreEqual(100f, result2, 0.001f, "Negative demand clamped to 0 -> base price.");
            Assert.AreEqual(0f, result3, 0.001f, "Negative price clamped to 0 -> 0 price.");
            Assert.AreEqual(100f, result4, 0.001f, "Negative elasticity clamped to 0 -> base price.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float nanResult = ScarcityPriceSpikeCalculator.Compute(float.NaN, 10f, 100f, 5f);
            float infResult = ScarcityPriceSpikeCalculator.Compute(5f, float.PositiveInfinity, 100f, 5f);
            float maxPriceResult = ScarcityPriceSpikeCalculator.Compute(0f, 10f, float.MaxValue, float.MaxValue);

            // Assert
            Assert.AreEqual(0f, nanResult, "Verify robust calculation: NaN handled.");
            Assert.AreEqual(0f, infResult, "Verify robust calculation: Infinity handled.");
            Assert.AreEqual(float.MaxValue, maxPriceResult, "Verify robust calculation and overflow protection: clamp to MaxValue.");
        }
    }
}
