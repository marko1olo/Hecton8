using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SargassumKelpDragCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float density = 0.5f;
            float speed = 5.0f;
            float weight = 50.0f;

            // Act
            float result = SargassumKelpDragCalculator.Compute(density, speed, weight);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.GreaterThan(0.0f).And.LessThan(1.0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float density = 1.0f;
            float speed = 100.0f;
            float weight = 1000.0f;

            // Act
            float result = SargassumKelpDragCalculator.Compute(density, speed, weight);

            // Assert
            Assert.That(result, Is.EqualTo(0.0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float density = 0.0f;
            float speed = 0.0f;
            float weight = 0.0f;

            // Act
            float result = SargassumKelpDragCalculator.Compute(density, speed, weight);

            // Assert
            Assert.That(result, Is.EqualTo(1.0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float density = -0.5f;
            float speed = -10.0f;
            float weight = -50.0f;

            // Act
            float result = SargassumKelpDragCalculator.Compute(density, speed, weight);

            // Assert
            Assert.That(result, Is.EqualTo(1.0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float density = 10.0f; // should clamp to 1.0
            float speed = float.PositiveInfinity;
            float weight = float.PositiveInfinity;

            // Act
            float result = SargassumKelpDragCalculator.Compute(density, speed, weight);

            // Assert
            Assert.That(result, Is.EqualTo(0.0f));
        }

        [Test]
        public void Test_NaNInputs_Case06()
        {
            // Arrange
            float density = float.NaN;
            float speed = float.NaN;
            float weight = float.NaN;

            // Act
            float result = SargassumKelpDragCalculator.Compute(density, speed, weight);

            // Assert
            // NaN clamped to 0 density -> 1.0 multiplier
            Assert.That(float.IsNaN(result), Is.False);
        }
    }
}
