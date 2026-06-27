using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WallSlideFrictionCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float velocity = -10f;
            float friction = 50f;
            float gravity = 1f;
            float dt = 0.1f;

            // Act
            float result = WallSlideFrictionCalculator.Compute(velocity, friction, gravity, dt);

            // Assert: Verify expected output behaviour
            // Deceleration = 50 * 1 * 0.1 = 5
            // initial speed = 10, new speed = 5. Result = -5f.
            Assert.AreEqual(-5f, result, 0.001f);
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float velocity = 2f;
            float friction = 100f; // decel = 10
            float gravity = 1f;
            float dt = 0.1f;

            // Act
            float result = WallSlideFrictionCalculator.Compute(velocity, friction, gravity, dt);

            // Assert
            // Clamps to 0 because deceleration (10) > speed (2).
            Assert.AreEqual(0f, result, 0.001f);
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float result1 = WallSlideFrictionCalculator.Compute(0f, 10f, 1f, 0.1f);
            float result2 = WallSlideFrictionCalculator.Compute(10f, 0f, 1f, 0.1f);
            float result3 = WallSlideFrictionCalculator.Compute(10f, 10f, 0f, 0.1f);
            float result4 = WallSlideFrictionCalculator.Compute(10f, 10f, 1f, 0f);

            // Act
            // Assert
            Assert.AreEqual(0f, result1, 0.001f);
            Assert.AreEqual(10f, result2, 0.001f); // Zero friction
            Assert.AreEqual(10f, result3, 0.001f); // Zero gravity
            Assert.AreEqual(10f, result4, 0.001f); // Zero dt
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float velocity = 10f;
            float friction = -5f; // clamped to 0
            float gravity = -1f; // clamped to 0
            float dt = -0.1f; // returned early or 0

            // Act
            float result = WallSlideFrictionCalculator.Compute(velocity, friction, gravity, dt);

            // Assert
            Assert.AreEqual(10f, result, 0.001f);
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float result1 = WallSlideFrictionCalculator.Compute(float.NaN, 10f, 1f, 0.1f);
            float result2 = WallSlideFrictionCalculator.Compute(10f, float.PositiveInfinity, 1f, 0.1f);

            // Act
            // Assert
            Assert.AreEqual(0f, result1, 0.001f); // NaN speed becomes 0
            Assert.AreEqual(0f, result2, 0.001f); // Infinity friction stops instantly
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
