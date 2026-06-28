using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SoundShadowOcclusionCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float obstacleSize = 10f;
            float distanceToObstacle = 10f;
            float sourceDistance = 30f;
            float frequency = 1000f;

            // Act
            float result = SoundShadowOcclusionCalculator.Compute(obstacleSize, distanceToObstacle, sourceDistance, frequency);

            // Assert: Verify expected output behaviour
            Assert.IsTrue(result > 0f && result <= 1f, "Occlusion should be between 0 and 1.");
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float resultObstacleBehindSource = SoundShadowOcclusionCalculator.Compute(10f, 40f, 30f, 1000f);
            float resultObstacleAtSource = SoundShadowOcclusionCalculator.Compute(10f, 30f, 30f, 1000f);

            // Act & Assert
            Assert.AreEqual(0f, resultObstacleBehindSource, "Obstacle behind source should return 0.");
            Assert.AreEqual(0f, resultObstacleAtSource, "Obstacle at source distance or further should return 0.");
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float resultZeroObstacle = SoundShadowOcclusionCalculator.Compute(0f, 10f, 30f, 1000f);
            float resultZeroDistance = SoundShadowOcclusionCalculator.Compute(10f, 0f, 30f, 1000f);
            float resultZeroFrequency = SoundShadowOcclusionCalculator.Compute(10f, 10f, 30f, 0f);

            // Act & Assert
            Assert.AreEqual(0f, resultZeroObstacle, "Zero obstacle size should return 0.");
            Assert.AreEqual(0f, resultZeroDistance, "Zero distance to obstacle should return 0.");
            Assert.AreEqual(0f, resultZeroFrequency, "Zero frequency should return 0.");
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float resultNegativeObstacle = SoundShadowOcclusionCalculator.Compute(-5f, 10f, 30f, 1000f);
            float resultNegativeDistance = SoundShadowOcclusionCalculator.Compute(10f, -10f, 30f, 1000f);
            float resultNegativeFreq = SoundShadowOcclusionCalculator.Compute(10f, 10f, 30f, -100f);

            // Act & Assert
            Assert.AreEqual(0f, resultNegativeObstacle, "Negative obstacle size should clamp/return 0.");
            Assert.AreEqual(0f, resultNegativeDistance, "Negative distance to obstacle should clamp/return 0.");
            Assert.AreEqual(0f, resultNegativeFreq, "Negative frequency should clamp/return 0.");
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultInfinityObstacle = SoundShadowOcclusionCalculator.Compute(float.PositiveInfinity, 10f, 30f, 1000f);
            float resultNaNInput = SoundShadowOcclusionCalculator.Compute(float.NaN, 10f, 30f, 1000f);
            float resultMaxVal = SoundShadowOcclusionCalculator.Compute(float.MaxValue, float.MaxValue / 2f, float.MaxValue, float.MaxValue);

            // Act & Assert
            Assert.AreEqual(1f, resultInfinityObstacle, "Infinite obstacle size should return 1.");
            Assert.AreEqual(0f, resultNaNInput, "NaN input should return 0.");
            Assert.IsTrue(resultMaxVal >= 0f && resultMaxVal <= 1f, "Extreme large values should clamp between 0 and 1.");
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
