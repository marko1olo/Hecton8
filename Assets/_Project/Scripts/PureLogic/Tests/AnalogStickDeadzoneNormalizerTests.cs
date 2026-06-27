using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AnalogStickDeadzoneNormalizerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float rawValue = 0.5f;
            float inner = 0.2f;
            float outer = 0.8f;

            // Act
            float result = AnalogStickDeadzoneNormalizer.Normalize(rawValue, inner, outer);

            // Assert: Verify expected output behaviour
            // (0.5 - 0.2) / (0.8 - 0.2) = 0.3 / 0.6 = 0.5
            Assert.AreEqual(0.5f, result, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Test exactly at inner boundary
            float result1 = AnalogStickDeadzoneNormalizer.Normalize(0.2f, 0.2f, 0.8f);

            // Test exactly at outer boundary
            float result2 = AnalogStickDeadzoneNormalizer.Normalize(0.8f, 0.2f, 0.8f);

            // Test beyond outer boundary (should clamp to 1.0)
            float result3 = AnalogStickDeadzoneNormalizer.Normalize(1.2f, 0.2f, 0.8f);

            // Test below inner boundary (should clamp to 0.0)
            float result4 = AnalogStickDeadzoneNormalizer.Normalize(0.1f, 0.2f, 0.8f);

            // Assert
            Assert.AreEqual(0.0f, result1, 0.0001f);
            Assert.AreEqual(1.0f, result2, 0.0001f);
            Assert.AreEqual(1.0f, result3, 0.0001f);
            Assert.AreEqual(0.0f, result4, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float rawValue = 0.0f;
            float inner = 0.0f;
            float outer = 0.0f; // This will be clamped to inner + 0.0001 internally

            // Act
            float result = AnalogStickDeadzoneNormalizer.Normalize(rawValue, inner, outer);

            // Assert
            Assert.AreEqual(0.0f, result, 0.0001f);

            // Test with a raw value slightly above zero but same inner/outer
            float result2 = AnalogStickDeadzoneNormalizer.Normalize(0.5f, 0.0f, 0.0f);
            Assert.AreEqual(1.0f, result2, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float rawValue = -0.5f;
            float inner = -0.2f; // Clamp to 0.0
            float outer = -0.1f; // Clamp to inner + 0.0001 -> 0.0001

            // Act
            float result = AnalogStickDeadzoneNormalizer.Normalize(rawValue, inner, outer);

            // Assert
            // rawValue = -0.5 is less than inner (0.0), so result is 0
            Assert.AreEqual(0.0f, result, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float infinityValue = float.PositiveInfinity;
            float nanValue = float.NaN;

            // Act
            float resultInfinity = AnalogStickDeadzoneNormalizer.Normalize(infinityValue, 0.2f, 0.8f);
            float resultNaN = AnalogStickDeadzoneNormalizer.Normalize(nanValue, 0.2f, 0.8f);

            // Extreme but finite
            float resultLarge = AnalogStickDeadzoneNormalizer.Normalize(float.MaxValue, 0.5f, 0.9f);

            // Assert
            Assert.AreEqual(1.0f, resultInfinity, "Infinity should clamp to 1.0");
            Assert.AreEqual(0.0f, resultNaN, "NaN should fallback to 0.0");
            Assert.AreEqual(1.0f, resultLarge, "Large value should clamp to 1.0");
        }
    }
}
