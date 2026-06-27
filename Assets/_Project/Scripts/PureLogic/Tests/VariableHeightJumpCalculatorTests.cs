using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VariableHeightJumpCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float heldTime = 0.5f;
            float maxJumpTime = 1.0f;
            float minJumpVelocity = 5.0f;
            float maxJumpVelocity = 10.0f;

            // Act
            float result = VariableHeightJumpCalculator.Compute(heldTime, maxJumpTime, minJumpVelocity, maxJumpVelocity);

            // Assert: Verify expected output behaviour
            // smoothstep(0.5) = 0.5^2 * (3 - 2*0.5) = 0.25 * 2 = 0.5
            // result = 5.0 + 5.0 * 0.5 = 7.5
            Assert.AreEqual(7.5f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxJumpTime = 1.0f;
            float minJumpVelocity = 5.0f;
            float maxJumpVelocity = 10.0f;

            // Act
            float resultZero = VariableHeightJumpCalculator.Compute(0f, maxJumpTime, minJumpVelocity, maxJumpVelocity);
            float resultMax = VariableHeightJumpCalculator.Compute(1f, maxJumpTime, minJumpVelocity, maxJumpVelocity);
            float resultOver = VariableHeightJumpCalculator.Compute(2f, maxJumpTime, minJumpVelocity, maxJumpVelocity);

            // Assert
            Assert.AreEqual(5.0f, resultZero, 0.001f);
            Assert.AreEqual(10.0f, resultMax, 0.001f);
            Assert.AreEqual(10.0f, resultOver, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result = VariableHeightJumpCalculator.Compute(0.5f, 0f, 5.0f, 10.0f);

            // Assert
            Assert.AreEqual(10.0f, result, 0.001f); // Zero max time should return max velocity immediately
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float resultNegTime = VariableHeightJumpCalculator.Compute(-0.5f, 1.0f, 5.0f, 10.0f);

            // Assert
            Assert.AreEqual(5.0f, resultNegTime, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float resultInf = VariableHeightJumpCalculator.Compute(float.PositiveInfinity, 1.0f, 5.0f, 10.0f);
            float resultNaN = VariableHeightJumpCalculator.Compute(float.NaN, 1.0f, 5.0f, 10.0f);

            // Assert
            Assert.AreEqual(10.0f, resultInf, 0.001f);
            Assert.AreEqual(5.0f, resultNaN, 0.001f);
        }
    }
}
