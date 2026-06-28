using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ParasiteLatchDragCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            int count = 32;
            Vector3 vel = new Vector3(10f, 0f, 0f);
            float drag = 0.5f;
            Vector3 pull = new Vector3(0f, 10f, 0f);

            // Act
            Vector3 result = ParasiteLatchDragCalculator.Compute(count, vel, drag, pull);

            // Assert: Verify expected output behaviour
            Assert.AreNotEqual(Vector3.Zero, result);
            // 32/64 = 0.5. scale = 0.25. dragPenalty = -10 * 0.5 * 0.25 = -1.25. pullOffset = 10 * 0.25 = 2.5
            Assert.AreEqual(new Vector3(-1.25f, 2.5f, 0f), result);
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            int maxCount = 64;
            Vector3 vel = new Vector3(10f, 10f, 10f);

            // Act
            Vector3 resMax = ParasiteLatchDragCalculator.Compute(maxCount, vel, 1f, Vector3.Zero);
            Vector3 resOverMax = ParasiteLatchDragCalculator.Compute(maxCount + 100, vel, 1f, Vector3.Zero);

            // Assert
            Assert.AreEqual(resMax, resOverMax);
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            int count = 0;
            Vector3 vel = Vector3.Zero;
            float drag = 0f;
            Vector3 pull = Vector3.Zero;

            // Act
            Vector3 result = ParasiteLatchDragCalculator.Compute(count, vel, drag, pull);

            // Assert
            Assert.AreEqual(Vector3.Zero, result);
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            int count = -10;
            Vector3 vel = new Vector3(10f, 10f, 10f);
            float drag = -1f;
            Vector3 pull = new Vector3(-10f, -10f, -10f);

            // Act
            Vector3 result1 = ParasiteLatchDragCalculator.Compute(count, vel, 1f, pull);
            Vector3 result2 = ParasiteLatchDragCalculator.Compute(10, vel, drag, pull);

            // Assert
            Assert.AreEqual(Vector3.Zero, result1); // count is -10
            // drag is -1 clamped to 0. scale = (10/64)^2 = 0.0244140625. pullOffset = pull * scale
            // -10 * 0.0244140625 = -0.244140625
            Assert.AreEqual(new Vector3(-0.244140625f, -0.244140625f, -0.244140625f), result2);
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            int count = 10;
            Vector3 vel = new Vector3(float.PositiveInfinity, 0f, 0f);
            float drag = float.NaN;
            Vector3 pull = new Vector3(0f, float.NaN, 0f);

            // Act
            Vector3 result = ParasiteLatchDragCalculator.Compute(count, vel, drag, pull);

            // Assert
            Assert.AreEqual(Vector3.Zero, result);
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
