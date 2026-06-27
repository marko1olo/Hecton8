using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StressSpawnEscalationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float stress = 0.5f;
            float baseRate = 1.0f;
            float mult = 2.0f;
            float maxRate = 5.0f;

            // Act
            float result = StressSpawnEscalationCalculator.Compute(stress, baseRate, mult, maxRate);

            // Assert: base(1.0) + stress(0.5) * mult(2.0) = 2.0
            Assert.AreEqual(2.0f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float stress = 1.0f;
            float baseRate = 1.0f;
            float mult = 10.0f;
            float maxRate = 5.0f;

            // Act
            float result = StressSpawnEscalationCalculator.Compute(stress, baseRate, mult, maxRate);

            // Assert: base(1.0) + stress(1.0) * mult(10.0) = 11.0. Clamped to max(5.0)
            Assert.AreEqual(5.0f, result, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float stress = 0f;
            float baseRate = 0f;
            float mult = 0f;
            float maxRate = 0f;

            // Act
            float result = StressSpawnEscalationCalculator.Compute(stress, baseRate, mult, maxRate);

            // Assert
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float stress = -0.5f;
            float baseRate = -1.0f;
            float mult = -2.0f;
            float maxRate = -5.0f;

            // Act
            float result = StressSpawnEscalationCalculator.Compute(stress, baseRate, mult, maxRate);

            // Assert: all negative inputs clamped to 0
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float stress = float.PositiveInfinity;
            float baseRate = float.NaN;
            float mult = float.PositiveInfinity;
            float maxRate = float.NaN;

            // Act
            float result = StressSpawnEscalationCalculator.Compute(stress, baseRate, mult, maxRate);

            // Assert: handled gracefully, NaN and Infinity become 0.
            Assert.AreEqual(0f, result, 0.001f);
        }
    }
}
