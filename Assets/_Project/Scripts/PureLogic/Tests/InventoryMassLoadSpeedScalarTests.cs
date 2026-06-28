using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class InventoryMassLoadSpeedScalarTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float mass = 50f;
            float capacity = 100f;
            float baseSpeed = 0.5f;

            // Act
            float result = InventoryMassLoadSpeedScalar.Calculate(mass, capacity, baseSpeed);

            // Assert: log2(1.5) = ~0.5849625
            // 1 - (1 - 0.5) * 0.5849625 = 1 - 0.5 * 0.5849625 = 0.7075
            Assert.That(result, Is.EqualTo(0.70751876f).Within(0.001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float capacity = 100f;
            float baseSpeed = 0.5f;

            // Act
            float resultZero = InventoryMassLoadSpeedScalar.Calculate(0f, capacity, baseSpeed);
            float resultMax = InventoryMassLoadSpeedScalar.Calculate(100f, capacity, baseSpeed);
            float resultOver = InventoryMassLoadSpeedScalar.Calculate(200f, capacity, baseSpeed);

            // Assert
            Assert.That(resultZero, Is.EqualTo(1.0f).Within(0.0001f), "0 mass = 1.0 scalar");
            Assert.That(resultMax, Is.EqualTo(baseSpeed).Within(0.0001f), "capacity mass = threshold");
            Assert.That(resultOver, Is.EqualTo(baseSpeed).Within(0.0001f), "overcapacity = survival speed floor");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float mass = 0f;
            float capacity = 0f;
            float baseSpeed = 0f;

            // Act
            float result = InventoryMassLoadSpeedScalar.Calculate(mass, capacity, baseSpeed);

            // Assert
            // capacity is clamped to 0.001f, mass is 0 -> load = 0.
            Assert.That(result, Is.EqualTo(1.0f).Within(0.0001f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float mass = -50f;
            float capacity = -100f;
            float baseSpeed = 0.5f;

            // Act
            float result = InventoryMassLoadSpeedScalar.Calculate(mass, capacity, baseSpeed);

            // Assert
            // Negative mass -> 0.
            // Negative capacity -> 0.001f
            // load -> 0.
            Assert.That(result, Is.EqualTo(1.0f).Within(0.0001f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float mass = float.MaxValue;
            float capacity = 100f;
            float baseSpeed = 0.5f;

            // Act
            float result1 = InventoryMassLoadSpeedScalar.Calculate(mass, capacity, baseSpeed);
            float resultNaN = InventoryMassLoadSpeedScalar.Calculate(float.NaN, float.NaN, float.NaN);

            // Assert
            Assert.That(result1, Is.EqualTo(baseSpeed).Within(0.0001f), "Verify robust calculation and overflow protection.");
            Assert.That(resultNaN, Is.EqualTo(1.0f).Within(0.0001f), "NaNs should fallback to 0 mass, 1 capacity, 0 baseSpeed -> 1.0 scalar.");
        }
    }
}
