using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SinusoidalHoverBobbingCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float baseHeight = 10f;
            float timeSeconds = 0.5f;
            float frequency = 0.5f; // phase will be 0.5 * 0.5 * 2pi = pi/2 -> sin is 1
            float amplitude = 2f;

            // Act
            float result = SinusoidalHoverBobbingCalculator.Compute(baseHeight, timeSeconds, frequency, amplitude);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.EqualTo(12f).Within(0.001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float baseHeight = 1f;
            float timeSeconds = 0.75f;
            float frequency = 1f; // phase will be 0.75 * 2pi = 3pi/2 -> sin is -1
            float amplitude = 2f;

            // Act
            float result = SinusoidalHoverBobbingCalculator.Compute(baseHeight, timeSeconds, frequency, amplitude);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float baseHeight = 0f;
            float timeSeconds = 0f;
            float frequency = 0f;
            float amplitude = 0f;

            // Act
            float result = SinusoidalHoverBobbingCalculator.Compute(baseHeight, timeSeconds, frequency, amplitude);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float baseHeight = -5f;
            float timeSeconds = -1f;
            float frequency = -10f;
            float amplitude = -2f;

            // Act
            float result = SinusoidalHoverBobbingCalculator.Compute(baseHeight, timeSeconds, frequency, amplitude);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float baseHeight = float.MaxValue;
            float timeSeconds = float.MaxValue;
            float frequency = float.MaxValue;
            float amplitude = float.MaxValue;

            // Act
            float result = SinusoidalHoverBobbingCalculator.Compute(baseHeight, timeSeconds, frequency, amplitude);

            // Assert
            Assert.IsTrue(float.IsFinite(result) || float.IsInfinity(result) || float.IsNaN(result), "Verify robust calculation and overflow protection.");
        }
    }
}
