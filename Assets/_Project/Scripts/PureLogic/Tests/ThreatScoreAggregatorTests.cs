using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ThreatScoreAggregatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float[] distances = new float[] { 5f, 10f, 20f };
            float[] weights = new float[] { 2f, 1f, 0.5f };
            float[] strengths = new float[] { 10f, 5f, 2f };
            float radius = 10f;

            // Act
            float result = ThreatScoreAggregator.Calculate(distances, weights, strengths, radius);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(10f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float[] distances = new float[] { 10f }; // exactly on radius
            float[] weights = new float[] { 5f };
            float[] strengths = new float[] { 5f };
            float radius = 10f;

            // Act
            float result = ThreatScoreAggregator.Calculate(distances, weights, strengths, radius);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float[] distances = new float[] { 0f };
            float[] weights = new float[] { 0f };
            float[] strengths = new float[] { 0f };
            float radius = 10f;

            // Act
            float result = ThreatScoreAggregator.Calculate(distances, weights, strengths, radius);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");

            // Check zero radius
            float resultZeroRadius = ThreatScoreAggregator.Calculate(distances, weights, strengths, 0f);
            Assert.AreEqual(0f, resultZeroRadius, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float[] distances = new float[] { -5f }; // Clamped to 0
            float[] weights = new float[] { -2f }; // Clamped to 0
            float[] strengths = new float[] { -10f }; // Clamped to 0
            float radius = 10f;

            // Act
            float result = ThreatScoreAggregator.Calculate(distances, weights, strengths, radius);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float[] distances = new float[] { float.NaN, 0f, 0f };
            float[] weights = new float[] { 1f, float.PositiveInfinity, 1f };
            float[] strengths = new float[] { 1f, 1f, float.PositiveInfinity };
            float radius = 10f;

            // Act
            float result = ThreatScoreAggregator.Calculate(distances, weights, strengths, radius);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify robust calculation and overflow protection.");
        }
    }
}
