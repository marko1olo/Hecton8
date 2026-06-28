using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BeaconNetworkSignalAttenuationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float transmitPowerDb = 100f;
            float distance = 10f; // log10(10) = 1, spreading loss = 20 * 1 = 20
            float salinityPpt = 35f; // alpha = 0.1 + 0.05 * 35 = 1.85, absorption loss = 1.85 * 10 = 18.5
            // total loss = 20 + 18.5 = 38.5
            // received = 100 - 38.5 = 61.5

            // Act
            float result = BeaconNetworkSignalAttenuationCalculator.Compute(transmitPowerDb, distance, salinityPpt);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(61.5f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float transmitPowerDb = 50f;
            float distance = 0.001f; // at min distance
            float salinityPpt = 100f;

            // Act
            float result = BeaconNetworkSignalAttenuationCalculator.Compute(transmitPowerDb, distance, salinityPpt);

            // Assert
            Assert.AreEqual(50f, result, "Verify boundary constraints clamp correctly (min distance returns max power).");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float transmitPowerDb = 0f;
            float distance = 0f;
            float salinityPpt = 0f;

            // Act
            float result = BeaconNetworkSignalAttenuationCalculator.Compute(transmitPowerDb, distance, salinityPpt);

            // Assert
            Assert.AreEqual(0f, result, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float transmitPowerDb = 10f;
            float distance = -5f; // clamped to 0 -> min distance
            float salinityPpt = -20f; // clamped to 0

            // Act
            float result = BeaconNetworkSignalAttenuationCalculator.Compute(transmitPowerDb, distance, salinityPpt);

            // Assert
            Assert.AreEqual(10f, result, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float transmitPowerDb = float.PositiveInfinity; // clamped to 0
            float distance = float.NaN; // clamped to 0
            float salinityPpt = float.NegativeInfinity; // clamped to 0

            // Act
            float result = BeaconNetworkSignalAttenuationCalculator.Compute(transmitPowerDb, distance, salinityPpt);

            // Assert
            Assert.AreEqual(0f, result, "Verify robust calculation and overflow protection.");
        }
    }
}
