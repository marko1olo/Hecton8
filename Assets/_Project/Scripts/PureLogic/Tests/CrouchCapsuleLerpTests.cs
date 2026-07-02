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
            Assert.That(result, Is.EqualTo(1.5f).Within(0.0001f));
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
            Assert.That(result, Is.EqualTo(1.0f).Within(0.0001f));
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
            Assert.That(result, Is.EqualTo(2.0f).Within(0.0001f)); // No movement if crouchSpeed is 0
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
            Assert.That(result, Is.EqualTo(2.0f).Within(0.0001f));

            // Delta time < 0
            result = CrouchCapsuleLerp.Calculate(2.0f, 1.0f, 1.0f, -0.5f);
            Assert.That(result, Is.EqualTo(2.0f).Within(0.0001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float result1 = CrouchCapsuleLerp.Calculate(float.NaN, 1.0f, 1.0f, 0.5f);
            Assert.That(result1, Is.NaN);

            float result2 = CrouchCapsuleLerp.Calculate(2.0f, float.PositiveInfinity, 1.0f, 0.5f);
            Assert.That(result2, Is.EqualTo(2.0f));

            float result3 = CrouchCapsuleLerp.Calculate(float.MaxValue, 0f, 1.0f, 0.5f);
            Assert.That(result3, Is.EqualTo(float.MaxValue - 0.5f));
        }

        [Test]
        public void Test_EpsilonAndMinMax_Case06()
        {
            // Arrange & Act & Assert for MinValue/MaxValue limits
            // Step size will be 0 due to 0 crouch speed.
            float result4 = CrouchCapsuleLerp.Calculate(float.MinValue, float.MaxValue, 0f, 0.5f);
            Assert.That(result4, Is.EqualTo(float.MinValue));

            // Small step
            float result5 = CrouchCapsuleLerp.Calculate(1.0f, 2.0f, float.Epsilon, 1.0f);
            Assert.That(result5, Is.EqualTo(1.0f + float.Epsilon));

            // Very small difference, very small step
            float result6 = CrouchCapsuleLerp.Calculate(1.0f, 1.0f + float.Epsilon, float.Epsilon, 1.0f);
            Assert.That(result6, Is.EqualTo(1.0f + float.Epsilon));

            // Target max value, current min value
            // Diff calculation might overflow to Infinity, so Math.Sign might be 1 or 0 depending on float behavior.
            // diff = targetHeight - currentHeight = MaxValue - MinValue = Infinity.
            // Math.Abs(Infinity) <= step (e.g. 1*0.5) is false.
            // Math.Sign(Infinity) is generally handled in standard libraries (usually returns 1).
            // Current implementation returns: currentHeight + Math.Sign(diff) * step
            // MinValue + 1 * 0.5 = MinValue. (Adding a small value to MinValue doesn't change the float due to precision).
            float result7 = CrouchCapsuleLerp.Calculate(float.MinValue, float.MaxValue, 1.0f, 0.5f);
            Assert.That(result7, Is.EqualTo(float.MinValue));

            // Testing clamp to 0 speed on extremly negative
            float result8 = CrouchCapsuleLerp.Calculate(1.0f, 2.0f, float.MinValue, 1.0f);
            Assert.That(result8, Is.EqualTo(1.0f));
        }
    }
}
