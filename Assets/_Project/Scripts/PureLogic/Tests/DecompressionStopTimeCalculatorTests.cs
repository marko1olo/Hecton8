using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DecompressionStopTimeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float maxDepthReached = 10f;
            float timeAtDepthMin = 5f;
            float ascentRate = 2f;
            float stopDepthMeters = 10f;

            // Act
            float result = DecompressionStopTimeCalculator.Compute(maxDepthReached, timeAtDepthMin, ascentRate, stopDepthMeters);

            // Assert: Verify expected output behaviour
            // rawStop = (10 * 5 * 2) - 10 = 100 - 10 = 90
            Assert.AreEqual(90f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxDepthReached = 0f;
            float timeAtDepthMin = 0f;
            float ascentRate = 0f;
            float stopDepthMeters = 0f;

            // Act
            float result = DecompressionStopTimeCalculator.Compute(maxDepthReached, timeAtDepthMin, ascentRate, stopDepthMeters);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float maxDepthReached = 10f;
            float timeAtDepthMin = 5f;
            float ascentRate = 2f;
            float stopDepthMeters = 0f;

            // Act
            float result = DecompressionStopTimeCalculator.Compute(maxDepthReached, timeAtDepthMin, ascentRate, stopDepthMeters);

            // Assert
            // rawStop = (10 * 5 * 2) - 0 = 100
            Assert.AreEqual(100f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float maxDepthReached = -10f;
            float timeAtDepthMin = -5f;
            float ascentRate = -2f;
            float stopDepthMeters = -10f;

            // Act
            float result = DecompressionStopTimeCalculator.Compute(maxDepthReached, timeAtDepthMin, ascentRate, stopDepthMeters);

            // Assert
            // Clamped to 0
            Assert.AreEqual(0f, result, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float result1 = DecompressionStopTimeCalculator.Compute(float.NaN, 5f, 2f, 10f);
            float result2 = DecompressionStopTimeCalculator.Compute(10f, float.PositiveInfinity, 2f, 10f);

            // Assert
            Assert.AreEqual(0f, result1, 0.001f, "Verify robust calculation and overflow protection (NaN).");
            Assert.AreEqual(0f, result2, 0.001f, "Verify robust calculation and overflow protection (Infinity).");
        }
    }
}
