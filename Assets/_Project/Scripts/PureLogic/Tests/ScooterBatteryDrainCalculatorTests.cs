using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ScooterBatteryDrainCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float thrustOutput01 = 0.5f;
            float maxDrainRateWatts = 1800f; // 1800 Watts
            float batteryCapacityWh = 100f; // 100 Watt-hours
            float currentCharge01 = 1.0f; // 100% full
            float deltaTime = 10f; // 10 seconds

            // Act
            float newCharge = ScooterBatteryDrainCalculator.Compute(thrustOutput01, maxDrainRateWatts, batteryCapacityWh, currentCharge01, deltaTime);

            // Assert: Verify expected output behaviour
            // 1800 Watts * 0.5 thrust = 900 Watts
            // 900 Watts * (10 / 3600) hours = 2.5 Watt-hours
            // 2.5 Wh / 100 Wh = 0.025 normalized drain
            // New charge = 1.0 - 0.025 = 0.975
            Assert.That(0.975f, Is.EqualTo(newCharge).Within(0.0001f));
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float thrustOutput01 = 1.5f; // Clamped to 1.0
            float maxDrainRateWatts = 3600f;
            float batteryCapacityWh = 100f;
            float currentCharge01 = 0.01f; // Almost empty
            float deltaTime = 3600f; // 1 hour

            // Act
            float newCharge = ScooterBatteryDrainCalculator.Compute(thrustOutput01, maxDrainRateWatts, batteryCapacityWh, currentCharge01, deltaTime);

            // Assert
            // Clamped thrust = 1.0
            // Drain = 3600 Watts * 1 hour = 3600 Wh
            // Normalized drain = 3600 / 100 = 36
            // New charge = max(0, 0.01 - 36) = 0
            Assert.That(0f, Is.EqualTo(newCharge).Within(0.0001f));
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float noThrust = ScooterBatteryDrainCalculator.Compute(0f, 1000f, 100f, 1f, 1f);
            float zeroCapacity = ScooterBatteryDrainCalculator.Compute(1f, 1000f, 0f, 1f, 1f);
            float zeroTime = ScooterBatteryDrainCalculator.Compute(1f, 1000f, 100f, 1f, 0f);

            // Assert
            Assert.That(1f, Is.EqualTo(noThrust).Within(0.0001f));
            Assert.That(1f, Is.EqualTo(zeroCapacity).Within(0.0001f));
            Assert.That(1f, Is.EqualTo(zeroTime).Within(0.0001f));
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float negativeThrust = ScooterBatteryDrainCalculator.Compute(-0.5f, 1000f, 100f, 1f, 1f);
            float negativeTime = ScooterBatteryDrainCalculator.Compute(1f, 1000f, 100f, 1f, -1f);
            float negativeCharge = ScooterBatteryDrainCalculator.Compute(1f, 1000f, 100f, -0.5f, 1f);

            // Assert
            Assert.That(1f, Is.EqualTo(negativeThrust).Within(0.0001f));
            Assert.That(1f, Is.EqualTo(negativeTime).Within(0.0001f));
            Assert.That(0f, Is.EqualTo(negativeCharge).Within(0.0001f)); // Since initial charge is negative, it gets clamped to 0. And drain doesn't occur, so output 0.
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float nanThrust = ScooterBatteryDrainCalculator.Compute(float.NaN, 1000f, 100f, 0.5f, 1f);
            float infinityTime = ScooterBatteryDrainCalculator.Compute(1f, 1000f, 100f, 0.5f, float.PositiveInfinity);

            // Assert
            Assert.That(0.5f, Is.EqualTo(nanThrust).Within(0.0001f));
            Assert.That(0.5f, Is.EqualTo(infinityTime).Within(0.0001f));
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
