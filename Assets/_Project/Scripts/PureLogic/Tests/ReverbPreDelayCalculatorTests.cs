using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ReverbPreDelayCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float volume = 1000f; // Dimension ~ 10m
            float speed = 340f;
            float dist = 2f;

            // Act
            float result = ReverbPreDelayCalculator.Compute(volume, speed, dist);

            // Assert: Verify expected output behaviour
            // 2 * 2m / 340m/s = 4/340 = 0.01176s = 11.7647ms
            Assert.That(result, Is.EqualTo(11.7647f).Within(0.01f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float volume = 1000f; // Dimension = 10m. Max allowed dist = 5m
            float speed = 340f;
            float dist = 20f; // Should be clamped to 5m

            // Act
            float result = ReverbPreDelayCalculator.Compute(volume, speed, dist);

            // Assert
            // 2 * 5m / 340 = 10/340 = 0.02941s = 29.4117ms
            Assert.That(result, Is.EqualTo(29.4117f).Within(0.01f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result = ReverbPreDelayCalculator.Compute(0f, 0f, 0f);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float result = ReverbPreDelayCalculator.Compute(-500f, -340f, -5f);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float nanResult = ReverbPreDelayCalculator.Compute(float.NaN, 340f, 2f);
            float infResult = ReverbPreDelayCalculator.Compute(1000f, float.PositiveInfinity, 2f);
            float giantResult = ReverbPreDelayCalculator.Compute(float.MaxValue, 340f, float.MaxValue);

            // Assert
            Assert.That(nanResult, Is.EqualTo(0f), "NaN check failed");
            Assert.That(infResult, Is.EqualTo(0f), "Infinity check failed");
            Assert.That(giantResult, Is.EqualTo(500f), "Verify robust calculation and overflow protection."); // Max clamp 500ms
        }
    }
}
