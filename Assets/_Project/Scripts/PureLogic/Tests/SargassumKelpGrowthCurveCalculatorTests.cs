using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SargassumKelpGrowthCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentSize = 10f;
            float maxClusterSize = 100f;
            float growthRate = 0.5f;
            float deltaHours = 1f;

            // Act
            float newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert: Verify expected output behaviour
            // 10 + (0.5 * 10 * (1 - 10/100) * 1) = 10 + (5 * 0.9) = 10 + 4.5 = 14.5
            Assert.AreEqual(14.5f, newSize, 0.0001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxClusterSize = 100f;
            float growthRate = 0.5f;
            float deltaHours = 1f;

            // Act
            float hitBoundary = SargassumKelpGrowthCurveCalculator.Compute(100f, maxClusterSize, growthRate, deltaHours);
            float exceedBoundary = SargassumKelpGrowthCurveCalculator.Compute(150f, maxClusterSize, growthRate, deltaHours);
            float almostBoundary = SargassumKelpGrowthCurveCalculator.Compute(99.99f, maxClusterSize, 10f, deltaHours);

            // Assert
            Assert.AreEqual(100f, hitBoundary, 0.0001f, "Verify boundary constraints clamp correctly at max.");
            Assert.AreEqual(100f, exceedBoundary, 0.0001f, "Verify boundary constraints clamp correctly above max.");
            Assert.AreEqual(100f, almostBoundary, 0.0001f, "Verify fast growth at boundary is clamped to max.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float zeroCurrent = SargassumKelpGrowthCurveCalculator.Compute(0f, 100f, 0.5f, 1f);
            float zeroMax = SargassumKelpGrowthCurveCalculator.Compute(10f, 0f, 0.5f, 1f);
            float zeroRate = SargassumKelpGrowthCurveCalculator.Compute(10f, 100f, 0f, 1f);
            float zeroTime = SargassumKelpGrowthCurveCalculator.Compute(10f, 100f, 0.5f, 0f);

            // Assert
            Assert.AreEqual(0f, zeroCurrent, 0.0001f, "Verify zero inputs are handled without divide-by-zero or exception (0 current).");
            Assert.AreEqual(0f, zeroMax, 0.0001f, "Verify zero inputs are handled without divide-by-zero or exception (0 max).");
            Assert.AreEqual(10f, zeroRate, 0.0001f, "Verify zero inputs are handled without divide-by-zero or exception (0 rate).");
            Assert.AreEqual(10f, zeroTime, 0.0001f, "Verify zero inputs are handled without divide-by-zero or exception (0 time).");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float negCurrent = SargassumKelpGrowthCurveCalculator.Compute(-10f, 100f, 0.5f, 1f);
            float negMax = SargassumKelpGrowthCurveCalculator.Compute(10f, -100f, 0.5f, 1f);
            float negRate = SargassumKelpGrowthCurveCalculator.Compute(10f, 100f, -0.5f, 1f);
            float negTime = SargassumKelpGrowthCurveCalculator.Compute(10f, 100f, 0.5f, -1f);

            // Assert
            Assert.AreEqual(0f, negCurrent, 0.0001f, "Verify negative inputs clamp gracefully (neg current).");
            Assert.AreEqual(0f, negMax, 0.0001f, "Verify negative inputs clamp gracefully (neg max).");
            Assert.AreEqual(10f, negRate, 0.0001f, "Verify negative inputs clamp gracefully (neg rate).");
            Assert.AreEqual(10f, negTime, 0.0001f, "Verify negative inputs clamp gracefully (neg time).");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float infCurrent = SargassumKelpGrowthCurveCalculator.Compute(float.PositiveInfinity, 100f, 0.5f, 1f);
            float nanMax = SargassumKelpGrowthCurveCalculator.Compute(10f, float.NaN, 0.5f, 1f);
            float maxValues = SargassumKelpGrowthCurveCalculator.Compute(float.MaxValue * 0.5f, float.MaxValue, float.MaxValue, float.MaxValue);

            // Assert
            Assert.AreEqual(0f, infCurrent, 0.0001f, "Verify robust calculation and overflow protection (Infinity).");
            Assert.AreEqual(10f, nanMax, 0.0001f, "Verify robust calculation and overflow protection (NaN).");
            Assert.AreEqual(float.MaxValue, maxValues, 0.0001f, "Verify robust calculation and overflow protection (MaxValue overflow clamp).");
        }
    }
}
