using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CausticIntensityDepthCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float depth = 10f;
            float surfaceIntensity = 2.0f;
            float attenDepth = 10f;
            float clarity = 1.0f;

            // Act
            float result = CausticIntensityDepthCalculator.Compute(depth, surfaceIntensity, attenDepth, clarity);

            // Assert
            Assert.That(result, Is.EqualTo(1.0f).Within(0.001f));

            // Additional check at depth 0
            float surfaceResult = CausticIntensityDepthCalculator.Compute(0f, surfaceIntensity, attenDepth, clarity);
            Assert.That(surfaceResult, Is.EqualTo(surfaceIntensity).Within(0.001f));

            // Check with murky water (clarity 0.5)
            float murkyResult = CausticIntensityDepthCalculator.Compute(depth, surfaceIntensity, attenDepth, 0.5f);
            Assert.That(murkyResult, Is.LessThan(1.0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float depth = 1000f; // Very deep
            float surfaceIntensity = 1.0f;
            float attenDepth = 10f;
            float clarity = 1.0f;

            // Act
            float result = CausticIntensityDepthCalculator.Compute(depth, surfaceIntensity, attenDepth, clarity);

            // Assert
            Assert.That(result, Is.GreaterThanOrEqualTo(0f));
            Assert.That(result, Is.LessThan(0.001f));

            // Test with negative depth, should be clamped to 0
            float negDepthResult = CausticIntensityDepthCalculator.Compute(-5f, surfaceIntensity, attenDepth, clarity);
            Assert.That(negDepthResult, Is.EqualTo(surfaceIntensity).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float depth = 10f;
            float surfaceIntensity = 0f;
            float attenDepth = 0f;
            float clarity = 0f;

            // Act
            float result1 = CausticIntensityDepthCalculator.Compute(depth, surfaceIntensity, 10f, 1f);
            float result2 = CausticIntensityDepthCalculator.Compute(depth, 1f, attenDepth, 1f);
            float result3 = CausticIntensityDepthCalculator.Compute(depth, 1f, 10f, clarity);

            // Assert
            Assert.That(result1, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result2, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result3, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float depth = 10f;

            // Act
            float negSurfResult = CausticIntensityDepthCalculator.Compute(depth, -2f, 10f, 1f);
            float negAttenResult = CausticIntensityDepthCalculator.Compute(depth, 1f, -10f, 1f);
            float negClarityResult = CausticIntensityDepthCalculator.Compute(depth, 1f, 10f, -1f);

            // Assert
            Assert.That(negSurfResult, Is.EqualTo(0f).Within(0.001f));
            Assert.That(negAttenResult, Is.EqualTo(0f).Within(0.001f));
            Assert.That(negClarityResult, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float extremeDepth = float.MaxValue;

            // Act
            float nanResult = CausticIntensityDepthCalculator.Compute(float.NaN, 1f, 10f, 1f);
            float infResult = CausticIntensityDepthCalculator.Compute(float.PositiveInfinity, 1f, 10f, 1f);

            // Extreme values in standard computation
            float extremeResult = CausticIntensityDepthCalculator.Compute(extremeDepth, 1f, 10f, 1f);

            // Assert
            Assert.That(nanResult, Is.EqualTo(0f));
            Assert.That(infResult, Is.EqualTo(0f));
            Assert.That(extremeResult, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
