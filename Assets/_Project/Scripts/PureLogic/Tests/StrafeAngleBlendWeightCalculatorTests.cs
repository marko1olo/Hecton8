using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StrafeAngleBlendWeightCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 facing = new Vector3(0, 0, 1);

            // Act & Assert: Verify expected output behaviour
            // 0 deg
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(0, 0, 1), facing, 90f), Is.EqualTo(0f).Within(0.001f));

            // 90 deg right
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 0), facing, 90f), Is.EqualTo(1f).Within(0.001f));

            // 90 deg left
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(-1, 0, 0), facing, 90f), Is.EqualTo(-1f).Within(0.001f));

            // 45 deg right
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 1), facing, 90f), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 facing = new Vector3(0, 0, 1);

            // Act & Assert
            // > 90 deg right -> clamped to 1.0 (since max angle is 180, 180/90 = 2, wait, Math.Clamp clamps to -1..1!)
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, -1), facing, 90f), Is.EqualTo(1f).Within(0.001f));

            // < -90 deg left -> clamped to -1.0
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(-1, 0, -1), facing, 90f), Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 facing = new Vector3(0, 0, 1);
            Vector3 zero = Vector3.Zero;

            // Act & Assert
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(zero, facing, 90f), Is.EqualTo(0f));
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(facing, zero, 90f), Is.EqualTo(0f));
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(zero, zero, 90f), Is.EqualTo(0f));

            // Zero angle deg
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 0), facing, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 facing = new Vector3(0, 0, 1);

            // Act & Assert
            // Negative angle
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 0), facing, -90f), Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 facing = new Vector3(0, 0, 1);

            // Act & Assert
            // NaN
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(float.NaN, 0, 0), facing, 90f), Is.EqualTo(0f));
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 0), new Vector3(0, float.NaN, 1), 90f), Is.EqualTo(0f));
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 0), facing, float.NaN), Is.EqualTo(0f));

            // Infinity
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(float.PositiveInfinity, 0, 0), facing, 90f), Is.EqualTo(0f));
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(1, 0, 0), facing, float.PositiveInfinity), Is.EqualTo(0f));

            // Extremely large vectors
            float max = float.MaxValue;
            Assert.That(StrafeAngleBlendWeightCalculator.Compute(new Vector3(max, 0, 0), facing, 90f), Is.EqualTo(0f));
        }
    }
}
