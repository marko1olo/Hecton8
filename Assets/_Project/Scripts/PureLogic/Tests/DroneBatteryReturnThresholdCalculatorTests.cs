using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DroneBatteryReturnThresholdCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float currentBattery = 0.5f;
            float distance = 100f;
            float drainPerMeter = 0.001f;
            float margin = 0.05f; // 100 * 0.001 + 0.05 = 0.15

            // Act
            bool mustReturn = DroneBatteryReturnThresholdCalculator.Compute(currentBattery, distance, drainPerMeter, margin);

            // Assert
            Assert.That(mustReturn, Is.False, "0.5 is > 0.15, so no return needed.");

            // Act again near threshold
            bool mustReturnAtThreshold = DroneBatteryReturnThresholdCalculator.Compute(0.15f, distance, drainPerMeter, margin);
            Assert.That(mustReturnAtThreshold, Is.True, "0.15 <= 0.15, should return.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            // High margin, clamping test
            float battery = 0.8f;
            float distance = 50f;
            float drain = 0.01f;
            float margin = 1.5f; // Clamps to 1.0. Req: 50 * 0.01 + 1.0 = 1.5. Return is true.

            // Act
            bool mustReturn = DroneBatteryReturnThresholdCalculator.Compute(battery, distance, drain, margin);

            // Assert
            Assert.That(mustReturn, Is.True, "Margin > 1 should clamp to 1, causing a required return for any battery < 1.5");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float battery = 0.5f;
            float distance = 0f;
            float drain = 0f;
            float margin = 0f;

            // Act
            bool mustReturn = DroneBatteryReturnThresholdCalculator.Compute(battery, distance, drain, margin);
            bool zeroBatteryReturn = DroneBatteryReturnThresholdCalculator.Compute(0f, distance, drain, margin);

            // Assert
            Assert.That(mustReturn, Is.False, "Zero distance and margin, 0.5 battery shouldn't return.");
            Assert.That(zeroBatteryReturn, Is.True, "Zero battery with 0 required should return (0 <= 0).");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float battery = -0.5f; // clamps to 0
            float distance = -100f; // clamps to 0
            float drain = -0.01f; // clamps to 0
            float margin = -0.1f; // clamps to 0

            // Act
            bool mustReturn = DroneBatteryReturnThresholdCalculator.Compute(battery, distance, drain, margin);
            bool highBatteryNegativeInputs = DroneBatteryReturnThresholdCalculator.Compute(0.5f, distance, drain, margin);

            // Assert
            Assert.That(mustReturn, Is.True, "Negative battery clamped to 0, required is 0, 0 <= 0 is true.");
            Assert.That(highBatteryNegativeInputs, Is.False, "0.5 battery with 0 required should be false.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float battery = float.NaN; // becomes 0
            float distance = float.PositiveInfinity; // becomes 0
            float drain = float.NaN; // becomes 0
            float margin = float.NegativeInfinity; // becomes 0

            // Act
            bool mustReturnNaN = DroneBatteryReturnThresholdCalculator.Compute(battery, distance, drain, margin);

            // Extreme high real values
            bool mustReturnHigh = DroneBatteryReturnThresholdCalculator.Compute(1f, 100000f, 100f, 1f);

            // Assert
            Assert.That(mustReturnNaN, Is.True, "NaNs and infinities become 0. 0 <= 0 is true.");
            Assert.That(mustReturnHigh, Is.True, "Extreme high requires massive battery, 1.0 <= huge is true.");
        }
    }
}
