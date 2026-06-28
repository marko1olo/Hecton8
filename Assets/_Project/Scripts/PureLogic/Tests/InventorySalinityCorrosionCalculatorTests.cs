using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class InventorySalinityCorrosionCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float durability = 1.0f;
            float salinity = 0.5f;
            float rate = 0.1f;
            float time = 1.0f;

            // Act
            float result = InventorySalinityCorrosionCalculator.Compute(durability, salinity, rate, time);

            // Assert
            Assert.AreEqual(0.95f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float durability = 0.01f;
            float salinity = 1.0f;
            float rate = 0.1f;
            float time = 1.0f;

            // Act
            float result = InventorySalinityCorrosionCalculator.Compute(durability, salinity, rate, time);

            // Assert
            Assert.AreEqual(0.0f, result, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float durability = 1.0f;
            float salinity = 0.0f;
            float rate = 0.1f;
            float time = 1.0f;

            // Act
            float result = InventorySalinityCorrosionCalculator.Compute(durability, salinity, rate, time);

            // Assert
            Assert.AreEqual(1.0f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float durability = -0.5f;
            float salinity = -1.0f;
            float rate = -0.1f;
            float time = -1.0f;

            // Act
            float result = InventorySalinityCorrosionCalculator.Compute(durability, salinity, rate, time);

            // Assert
            Assert.AreEqual(0.0f, result, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float durability = 1.0f;
            float salinity = float.PositiveInfinity;
            float rate = 1000000f;
            float time = 1000000f;

            // Act
            float result = InventorySalinityCorrosionCalculator.Compute(durability, salinity, rate, time);

            // Assert
            Assert.AreEqual(1.0f, result, 0.001f, "Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_LogarithmicAcceleration_Case06()
        {
            // Arrange
            float durability = 1.0f;
            float salinity = (float)Math.E; // e ~ 2.718
            float rate = 0.1f;
            float time = 1.0f;
            // effectiveSalinity = 1 + ln(e) = 2.0
            // tickDegradation = 0.1 * 2.0 * 1.0 = 0.2
            // result = 1.0 - 0.2 = 0.8

            // Act
            float result = InventorySalinityCorrosionCalculator.Compute(durability, salinity, rate, time);

            // Assert
            Assert.AreEqual(0.8f, result, 0.001f, "Verify logarithmic acceleration in high salinity (chemosynthetic brine zones) works.");
        }
    }
}
