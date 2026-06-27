using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ArmorPenetrationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float mass = 10f;
            float velocity = 2f; // Power = 10 * 4 = 40
            float hardness = 5f;
            float thickness = 8f; // Resistance = 40
            // Act
            float result = ArmorPenetrationCalculator.Compute(mass, velocity, hardness, thickness);
            // Assert: Verify expected output behaviour
            Assert.That(result, Is.EqualTo(1.0f).Within(0.0001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs
            float mass = 10f;
            float velocity = 1f; // Power = 10
            float hardness = 100f;
            float thickness = 1f; // Resistance = 100
            // Act
            float result = ArmorPenetrationCalculator.Compute(mass, velocity, hardness, thickness);
            // Assert
            Assert.That(result, Is.EqualTo(0.1f).Within(0.0001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float mass = 0f;
            float velocity = 0f;
            float hardness = 0f;
            float thickness = 0f;
            // Act
            float result = ArmorPenetrationCalculator.Compute(mass, velocity, hardness, thickness);
            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float mass = -10f;
            float velocity = 50f;
            float hardness = 10f;
            float thickness = 10f;
            // Act
            float result = ArmorPenetrationCalculator.Compute(mass, velocity, hardness, thickness);
            // Assert
            Assert.That(result, Is.EqualTo(0f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float mass = float.MaxValue;
            float velocity = float.MaxValue;
            float hardness = 10f;
            float thickness = 10f;
            // Act
            float result = ArmorPenetrationCalculator.Compute(mass, velocity, hardness, thickness);
            // Assert
            Assert.That(result, Is.EqualTo(1f), "Verify robust calculation and overflow protection.");
        }
    }
}
