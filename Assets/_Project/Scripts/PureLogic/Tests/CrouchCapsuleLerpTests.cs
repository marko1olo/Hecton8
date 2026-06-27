using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CrouchCapsuleLerpTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float currentHeight = 2.0f;
            float targetHeight = 1.0f;
            float crouchSpeed = 1.0f;
            float deltaTime = 0.5f;

            // Act
            float result = CrouchCapsuleLerp.Calculate(currentHeight, targetHeight, crouchSpeed, deltaTime);

            // Assert
            Assert.AreEqual(1.5f, result, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float currentHeight = 1.5f;
            float targetHeight = 1.0f;
            float crouchSpeed = 1.0f;
            float deltaTime = 1.0f; // Step is 1.0, diff is 0.5. Should clamp to targetHeight.

            // Act
            float result = CrouchCapsuleLerp.Calculate(currentHeight, targetHeight, crouchSpeed, deltaTime);

            // Assert
            Assert.AreEqual(1.0f, result, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float currentHeight = 2.0f;
            float targetHeight = 1.0f;
            float crouchSpeed = 0.0f;
            float deltaTime = 0.5f;

            // Act
            float result = CrouchCapsuleLerp.Calculate(currentHeight, targetHeight, crouchSpeed, deltaTime);

            // Assert
            Assert.AreEqual(2.0f, result, 0.0001f); // No movement if crouchSpeed is 0
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentHeight = 2.0f;
            float targetHeight = 1.0f;
            float crouchSpeed = -1.0f;
            float deltaTime = 0.5f;

            // Act
            float result = CrouchCapsuleLerp.Calculate(currentHeight, targetHeight, crouchSpeed, deltaTime);

            // Assert
            // Crouch speed should be clamped to 0
            Assert.AreEqual(2.0f, result, 0.0001f);

            // Delta time < 0
            result = CrouchCapsuleLerp.Calculate(2.0f, 1.0f, 1.0f, -0.5f);
            Assert.AreEqual(2.0f, result, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float result1 = CrouchCapsuleLerp.Calculate(float.NaN, 1.0f, 1.0f, 0.5f);
            Assert.IsTrue(float.IsNaN(result1));

            float result2 = CrouchCapsuleLerp.Calculate(2.0f, float.PositiveInfinity, 1.0f, 0.5f);
            Assert.AreEqual(2.0f, result2);

            float result3 = CrouchCapsuleLerp.Calculate(float.MaxValue, 0f, 1.0f, 0.5f);
            Assert.AreEqual(float.MaxValue - 0.5f, result3);
        }
    }
}
