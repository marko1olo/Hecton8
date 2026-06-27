using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ServoTorqueLoadCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float loadKg = 10f;
            float armAngleDeg = 0f; // horizontal
            float armLengthM = 2f;
            float gravity = 9.81f;
            float servoEfficiency = 0.8f;

            // Act
            float result = ServoTorqueLoadCalculator.Compute(loadKg, armAngleDeg, armLengthM, gravity, servoEfficiency);

            // Assert: Verify expected output behaviour
            // Force = 10 * 9.81 = 98.1. Length = 2. Torque = 196.2. requiredTorque = 196.2 / 0.8 = 245.25
            Assert.AreEqual(245.25f, result, 0.01f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float loadKg = 10f;
            float armAngleDeg = 90f; // vertical down
            float armLengthM = 2f;
            float gravity = 9.81f;
            float servoEfficiency = 0f; // should clamp to 0.01f

            // Act
            float result = ServoTorqueLoadCalculator.Compute(loadKg, armAngleDeg, armLengthM, gravity, servoEfficiency);

            // Assert
            // angle 90 means Cos(90) = 0. required torque is 0.
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float loadKg = 0f;
            float armAngleDeg = 0f;
            float armLengthM = 0f;
            float gravity = 0f;
            float servoEfficiency = 0f;

            // Act
            float result = ServoTorqueLoadCalculator.Compute(loadKg, armAngleDeg, armLengthM, gravity, servoEfficiency);

            // Assert
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float loadKg = -5f; // clamped to 0
            float armAngleDeg = -180f; // horizontal
            float armLengthM = -2f; // clamped to 0
            float gravity = 9.81f;
            float servoEfficiency = -1f; // clamped to 0.01f

            // Act
            float result = ServoTorqueLoadCalculator.Compute(loadKg, armAngleDeg, armLengthM, gravity, servoEfficiency);

            // Assert
            Assert.AreEqual(0f, result);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float loadKg = float.PositiveInfinity;
            float armAngleDeg = float.NaN;
            float armLengthM = 10f;
            float gravity = float.PositiveInfinity;
            float servoEfficiency = float.NaN;

            // Act
            float result = ServoTorqueLoadCalculator.Compute(loadKg, armAngleDeg, armLengthM, gravity, servoEfficiency);

            // Assert
            // loadKg, gravity become 0/9.81. NaN becomes 0, 1.
            // force = 0 * 9.81 = 0.
            Assert.AreEqual(0f, result);

            // Extreme but finite
            float extremeResult = ServoTorqueLoadCalculator.Compute(1e10f, 0f, 10f, 9.81f, 1f);
            Assert.IsTrue(extremeResult > 0f);
            Assert.IsFalse(float.IsNaN(extremeResult));
        }
    }
}
