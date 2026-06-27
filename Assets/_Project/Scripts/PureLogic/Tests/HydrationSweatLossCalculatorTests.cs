using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HydrationSweatLossCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float exertionLevel01 = 0.5f;
            float ambientTempCelsius = 20f;
            float baseSweatRate = 0.5f;
            float heatThreshold = 25f;

            // Act
            float result = HydrationSweatLossCalculator.Compute(exertionLevel01, ambientTempCelsius, baseSweatRate, heatThreshold);

            // Assert: base(0.5) + exertion(0.5 * 0.5) + heatStress(0) = 0.75
            Assert.AreEqual(0.75f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float exertionLevel01 = 1.5f; // clamped to 1.0
            float ambientTempCelsius = 30f; // 30 - 30 = 0 heat stress
            float baseSweatRate = 0.5f;
            float heatThreshold = 30f;

            // Act
            float result = HydrationSweatLossCalculator.Compute(exertionLevel01, ambientTempCelsius, baseSweatRate, heatThreshold);

            // Assert: base(0.5) + exertion(0.5 * 1.0) + heatStress(0) = 1.0
            Assert.AreEqual(1.0f, result, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float exertionLevel01 = 0f;
            float ambientTempCelsius = 0f;
            float baseSweatRate = 0f;
            float heatThreshold = 0f;

            // Act
            float result = HydrationSweatLossCalculator.Compute(exertionLevel01, ambientTempCelsius, baseSweatRate, heatThreshold);

            // Assert: All zero should yield 0 without issues.
            Assert.AreEqual(0f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float exertionLevel01 = -1f; // Clamped to 0
            float ambientTempCelsius = -10f;
            float baseSweatRate = 0.1f;
            float heatThreshold = 0f; // heatStress: max(0, -10 - 0) = 0

            // Act
            float result = HydrationSweatLossCalculator.Compute(exertionLevel01, ambientTempCelsius, baseSweatRate, heatThreshold);

            // Assert: base(0.1) + exertion(0) + heatStress(0) = 0.1
            Assert.AreEqual(0.1f, result, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float exertionLevel01 = float.NaN;
            float ambientTempCelsius = float.PositiveInfinity;
            float baseSweatRate = 0.5f;
            float heatThreshold = 25f;

            // Act
            float result = HydrationSweatLossCalculator.Compute(exertionLevel01, ambientTempCelsius, baseSweatRate, heatThreshold);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify robust calculation and overflow protection.");
        }
    }
}
