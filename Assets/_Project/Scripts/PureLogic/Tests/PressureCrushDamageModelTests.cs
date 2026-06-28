using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PressureCrushDamageModelTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float depth = 200f;
            float threshold = 100f;
            float maxDamageRate = 10f;
            float exponent = 2f;

            // Act
            float result = PressureCrushDamageModel.Evaluate(depth, threshold, maxDamageRate, exponent);

            // Assert
            // (200 / 100)^2 - 1 = 3. 3 * 10 = 30
            Assert.That(result, Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float depth = 100f; // Exactly at threshold
            float threshold = 100f;
            float maxDamageRate = 10f;
            float exponent = 2f;

            // Act
            float result = PressureCrushDamageModel.Evaluate(depth, threshold, maxDamageRate, exponent);

            // Assert: At threshold: near zero
            Assert.That(result, Is.EqualTo(0f).Within(0.001f));

            // Above threshold
            float depth2 = 50f;
            float result2 = PressureCrushDamageModel.Evaluate(depth2, threshold, maxDamageRate, exponent);
            Assert.That(result2, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float depth = 0f;
            float threshold = 0f;
            float maxDamageRate = 0f;
            float exponent = 0f;

            // Act
            float result = PressureCrushDamageModel.Evaluate(depth, threshold, maxDamageRate, exponent);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float depth = -100f;
            float threshold = -50f;
            float maxDamageRate = -10f;
            float exponent = -2f;

            // Act
            float result = PressureCrushDamageModel.Evaluate(depth, threshold, maxDamageRate, exponent);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float depth = float.MaxValue;
            float threshold = 1f;
            float maxDamageRate = 1f;
            float exponent = 10f;

            // Act
            float result = PressureCrushDamageModel.Evaluate(depth, threshold, maxDamageRate, exponent);

            // Assert
            Assert.That(result, Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void Test_NaNInputs_Case06()
        {
            Assert.That(PressureCrushDamageModel.Evaluate(float.NaN, 100f, 10f, 2f), Is.EqualTo(0f));
            Assert.That(PressureCrushDamageModel.Evaluate(200f, float.NaN, 10f, 2f), Is.EqualTo(0f));
            Assert.That(PressureCrushDamageModel.Evaluate(200f, 100f, float.NaN, 2f), Is.EqualTo(0f));
            Assert.That(PressureCrushDamageModel.Evaluate(200f, 100f, 10f, float.NaN), Is.EqualTo(0f));
        }
    }
}
