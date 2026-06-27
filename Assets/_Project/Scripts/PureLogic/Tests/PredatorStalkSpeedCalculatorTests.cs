using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PredatorStalkSpeedCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float dist = 5f;
            float stalkRadius = 10f;
            float stalkSpeed = 2f;
            float chaseSpeed = 8f;
            float awareness = 0f;

            // Act
            float result = PredatorStalkSpeedCalculator.Compute(dist, stalkRadius, stalkSpeed, chaseSpeed, awareness);

            // Assert: inside radius, no awareness -> stalkSpeed
            Assert.That(result, Is.EqualTo(2f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float dist = 10f; // Exactly on boundary
            float stalkRadius = 10f;
            float stalkSpeed = 3f;
            float chaseSpeed = 7f;
            float awareness = 0.5f;

            // Act
            float result = PredatorStalkSpeedCalculator.Compute(dist, stalkRadius, stalkSpeed, chaseSpeed, awareness);

            // Assert: exactly on radius, aware 50% -> halfway between stalk and chase
            Assert.That(result, Is.EqualTo(5f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float result = PredatorStalkSpeedCalculator.Compute(0f, 0f, 0f, 0f, 0f);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float result = PredatorStalkSpeedCalculator.Compute(-5f, -10f, -2f, -8f, -0.5f);

            // Act & Assert: Should clamp all negatives to 0 (except stalkRadius to tiny positive), resulting in 0
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float resultNaN = PredatorStalkSpeedCalculator.Compute(float.NaN, float.PositiveInfinity, 10f, 20f, float.NaN);

            // Assert
            Assert.That(float.IsNaN(resultNaN), Is.False);
            Assert.That(float.IsInfinity(resultNaN), Is.False);
        }

        [Test]
        public void Test_FarFromPrey_Case06()
        {
            // Arrange
            float dist = 50f;
            float stalkRadius = 10f;
            float stalkSpeed = 2f;
            float chaseSpeed = 10f;
            float awareness = 0f;

            // Act
            float result = PredatorStalkSpeedCalculator.Compute(dist, stalkRadius, stalkSpeed, chaseSpeed, awareness);

            // Assert: outside radius, no awareness -> chaseSpeed
            Assert.That(result, Is.EqualTo(10f));
        }

        [Test]
        public void Test_FullAwareness_Case07()
        {
            // Arrange
            float dist = 5f;
            float stalkRadius = 10f;
            float stalkSpeed = 2f;
            float chaseSpeed = 10f;
            float awareness = 1f;

            // Act
            float result = PredatorStalkSpeedCalculator.Compute(dist, stalkRadius, stalkSpeed, chaseSpeed, awareness);

            // Assert: inside radius, full awareness -> chaseSpeed
            Assert.That(result, Is.EqualTo(10f));
        }
    }
}
