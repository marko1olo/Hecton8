using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AnchorStabilityScoreCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            // Flat, large contact: 1.0. Max slope: near 0.
            float contactAreaM2 = 10f;
            float terrainSlopeAngleDeg = 0f;
            float maxStableSlope = 45f;
            float foundationStrength = 1f;

            // Act
            float result = AnchorStabilityScoreCalculator.Compute(contactAreaM2, terrainSlopeAngleDeg, maxStableSlope, foundationStrength);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(1.0f, result, "Large contact area on flat terrain should give 1.0 stability.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float contactAreaM2 = 1000f; // Very large
            float terrainSlopeAngleDeg = 45f; // Exactly max slope
            float maxStableSlope = 45f;
            float foundationStrength = 100f; // Very large

            // Act
            float result = AnchorStabilityScoreCalculator.Compute(contactAreaM2, terrainSlopeAngleDeg, maxStableSlope, foundationStrength);

            // Assert
            Assert.AreEqual(0.0f, result, "Slope at max should give 0.0 stability despite large contact area.");

            float resultHalfSlope = AnchorStabilityScoreCalculator.Compute(contactAreaM2, 22.5f, maxStableSlope, foundationStrength);
            Assert.AreEqual(0.5f, resultHalfSlope, "Slope at half max should give 0.5 stability for large contact area.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float contactAreaM2 = 0f;
            float terrainSlopeAngleDeg = 0f;
            float maxStableSlope = 0f;
            float foundationStrength = 0f;

            // Act
            float result = AnchorStabilityScoreCalculator.Compute(contactAreaM2, terrainSlopeAngleDeg, maxStableSlope, foundationStrength);

            // Assert
            Assert.AreEqual(0.0f, result, "Zero values should handle divide-by-zero safely and return 0.0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float contactAreaM2 = -10f;
            float terrainSlopeAngleDeg = -5f;
            float maxStableSlope = -45f;
            float foundationStrength = -1f;

            // Act
            float result = AnchorStabilityScoreCalculator.Compute(contactAreaM2, terrainSlopeAngleDeg, maxStableSlope, foundationStrength);

            // Assert
            Assert.AreEqual(0.0f, result, "Negative inputs should be clamped and return 0.0 stability.");

            float validResultWithNegativeSlope = AnchorStabilityScoreCalculator.Compute(10f, -5f, 45f, 1f);
            Assert.AreEqual(1.0f, validResultWithNegativeSlope, "Negative slope angle is clamped to 0, returning 1.0 stability.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float contactAreaM2 = float.PositiveInfinity;
            float terrainSlopeAngleDeg = float.NaN;
            float maxStableSlope = float.MaxValue;
            float foundationStrength = float.PositiveInfinity;

            // Act
            float result = AnchorStabilityScoreCalculator.Compute(contactAreaM2, terrainSlopeAngleDeg, maxStableSlope, foundationStrength);

            // Assert
            Assert.AreEqual(0.0f, result, "Infinity and NaN should be gracefully handled and result clamped properly.");
        }
    }
}
