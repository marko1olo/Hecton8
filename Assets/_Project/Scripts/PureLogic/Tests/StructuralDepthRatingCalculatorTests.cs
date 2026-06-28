using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StructuralDepthRatingCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float depthMeters = 150f;
            float crushDepthRating = 100f;
            float hullIntegrity01 = 1f;
            float fatigueAccumulated = 0f;

            // Act
            float result = StructuralDepthRatingCalculator.Compute(depthMeters, crushDepthRating, hullIntegrity01, fatigueAccumulated);

            // Assert
            // 50 exceedance / 100 rating = 0.5 ratio
            // Base stress = 0.5 + 0.5 * 0.5 = 0.75
            // modifiers: (1 + 0) * (2 - 1) = 1
            // stress = 0.75 * 1 = 0.75
            Assert.That(result, Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float depthMeters = 100f;
            float crushDepthRating = 100f;

            // Act
            float resultBelowRating = StructuralDepthRatingCalculator.Compute(depthMeters - 0.1f, crushDepthRating, 1f, 0f);
            float resultAtRating = StructuralDepthRatingCalculator.Compute(depthMeters, crushDepthRating, 1f, 0f);

            // double depth -> catastrophic (1.0 or high stress)
            float resultAtDoubleDepth = StructuralDepthRatingCalculator.Compute(200f, crushDepthRating, 1f, 0f);

            // Assert
            Assert.That(resultBelowRating, Is.EqualTo(0f), "Below rating should be 0 stress");
            Assert.That(resultAtRating, Is.EqualTo(0.5f).Within(0.001f), "At rating should be moderate (0.5) stress");
            Assert.That(resultAtDoubleDepth, Is.EqualTo(1f), "Double depth should hit catastrophic (1.0 clamped) stress");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float depthMeters = 0f;
            float crushDepthRating = 0f; // will be clamped to 0.001
            float hullIntegrity01 = 0f;
            float fatigueAccumulated = 0f;

            // Act
            float result = StructuralDepthRatingCalculator.Compute(depthMeters, crushDepthRating, hullIntegrity01, fatigueAccumulated);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float depthMeters = -100f;
            float crushDepthRating = -50f;
            float hullIntegrity01 = -0.5f;
            float fatigueAccumulated = -1f;

            // Act
            float result = StructuralDepthRatingCalculator.Compute(depthMeters, crushDepthRating, hullIntegrity01, fatigueAccumulated);

            // Assert: Clamped correctly, so depth is 0, crush depth is 0.001, so depth <= crush depth -> 0
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float resultNaN = StructuralDepthRatingCalculator.Compute(float.NaN, 100f, 1f, 0f);
            float resultInf = StructuralDepthRatingCalculator.Compute(100f, float.PositiveInfinity, 1f, 0f);

            float largeDepth = 1e10f;
            float crushDepthRating = 100f;
            float resultLarge = StructuralDepthRatingCalculator.Compute(largeDepth, crushDepthRating, 1f, 0f);

            // Assert
            Assert.That(resultNaN, Is.EqualTo(0f));
            Assert.That(resultInf, Is.EqualTo(0f));
            Assert.That(resultLarge, Is.EqualTo(1f), "Large value should clamp to 1.0 stress fraction");
        }
    }
}
