using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PressureHullIntegrityStressCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentDepth = 1500f;
            float crushDepth = 1000f;
            float impactForce = 50f;
            float structuralIntegrity = 2f;

            // Act
            float result = PressureHullIntegrityStressCalculator.Compute(currentDepth, crushDepth, impactForce, structuralIntegrity);

            // Assert: Verify expected output behaviour
            // pressureDamage = 1500 - 1000 = 500
            // totalDamage = (500 * 0.72) + (50 * 0.28) = 360 + 14 = 374
            // result = 374 * 2 = 748
            Assert.AreEqual(748f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentDepth = 1000f; // Exactly at crush depth
            float crushDepth = 1000f;
            float impactForce = 10f;
            float structuralIntegrity = 1f;

            // Act
            float result = PressureHullIntegrityStressCalculator.Compute(currentDepth, crushDepth, impactForce, structuralIntegrity);

            // Assert
            // pressureDamage = 0
            // totalDamage = (0 * 0.72) + (10 * 0.28) = 2.8
            // result = 2.8 * 1 = 2.8
            Assert.AreEqual(2.8f, result, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentDepth = 0f;
            float crushDepth = 0f;
            float impactForce = 0f;
            float structuralIntegrity = 0f;

            // Act
            float result = PressureHullIntegrityStressCalculator.Compute(currentDepth, crushDepth, impactForce, structuralIntegrity);

            // Assert
            // pressureDamage = 0
            // result = 0 * 0.01 = 0
            Assert.AreEqual(0f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentDepth = -500f;
            float crushDepth = -1000f;
            float impactForce = -50f;
            float structuralIntegrity = -5f;

            // Act
            float result = PressureHullIntegrityStressCalculator.Compute(currentDepth, crushDepth, impactForce, structuralIntegrity);

            // Assert
            // pressureDamage = -500 - (-1000) = 500
            // totalDamage = (500 * 0.72) + (-50 * 0.28) = 360 - 14 = 346
            // result = 346 * 0.01 = 3.46
            Assert.AreEqual(3.46f, result, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentDepth = float.PositiveInfinity;
            float crushDepth = 1000f;
            float impactForce = 50f;
            float structuralIntegrity = float.NaN;

            // Act
            float result = PressureHullIntegrityStressCalculator.Compute(currentDepth, crushDepth, impactForce, structuralIntegrity);

            // Assert
            // result should be 0f due to NaN/Infinity safety check
            Assert.AreEqual(0f, result, 0.001f, "Verify robust calculation and overflow protection.");
        }
    }
}
