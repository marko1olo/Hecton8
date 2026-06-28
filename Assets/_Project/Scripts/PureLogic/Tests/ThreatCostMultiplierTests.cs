using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ThreatCostMultiplierTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float baseCost = 100f;
            float normalTemp = 20f;
            float normalDepth = 100f;

            // Act
            float result = ThreatCostMultiplier.Calculate(baseCost, normalTemp, normalDepth);

            // Assert: Verify expected output behaviour
            // Neither multiplier should activate (temp >= 0, depth <= 500)
            Assert.AreEqual(100f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float baseCost = 100f;
            float freezingTemp = ThreatCostMultiplier.FreezingTemperatureThreshold;
            float thresholdDepth = ThreatCostMultiplier.ExtremeDepthThreshold;

            // Act
            float result = ThreatCostMultiplier.Calculate(baseCost, freezingTemp, thresholdDepth);

            // Assert
            // No changes should occur exactly on boundaries
            Assert.AreEqual(100f, result, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float baseCost = 0f;
            float zeroTemp = 0f;
            float zeroDepth = 0f;

            // Act
            float result = ThreatCostMultiplier.Calculate(baseCost, zeroTemp, zeroDepth);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float baseCost = -50f;
            float negativeTemp = -10f; // -10C is below freezing
            float negativeDepth = -100f;

            // Act
            float result = ThreatCostMultiplier.Calculate(baseCost, negativeTemp, negativeDepth);

            // Assert
            // Base cost should clamp to 0, so overall result is 0
            Assert.AreEqual(0f, result, 0.001f, "Verify negative base costs clamp to zero.");
        }

        [Test]
        public void Test_NegativeTemp_IncreasesCost()
        {
            // Arrange
            float baseCost = 100f;
            float negativeTemp = -20f; // cold
            float normalDepth = 100f;

            // Act
            float result = ThreatCostMultiplier.Calculate(baseCost, negativeTemp, normalDepth);

            // Assert
            // multiplier = 1 + (20 * 0.05) = 2.0
            Assert.AreEqual(200f, result, 0.001f, "Temperatures below freezing increase threat cost");
        }

        [Test]
        public void Test_ExtremeDepth_ReducesCost()
        {
            // Arrange
            float baseCost = 100f;
            float normalTemp = 20f;
            float extremeDepth = 1000f; // depthFactor = 1000/500 = 2.0, depthMultiplier = 1/2 = 0.5

            // Act
            float result = ThreatCostMultiplier.Calculate(baseCost, normalTemp, extremeDepth);

            // Assert
            Assert.AreEqual(50f, result, 0.001f, "Extreme depths reduce threat cost");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float infinityBaseCost = float.PositiveInfinity;
            float nanTemp = float.NaN;
            float nanDepth = float.NaN;

            // Act
            float result1 = ThreatCostMultiplier.Calculate(infinityBaseCost, 20f, 100f);
            float result2 = ThreatCostMultiplier.Calculate(100f, nanTemp, nanDepth);

            // Assert
            Assert.AreEqual(0f, result1, "Infinity base cost should return 0");
            Assert.AreEqual(100f, result2, 0.001f, "NaN temp/depth should revert to safe defaults");
        }
    }
}
