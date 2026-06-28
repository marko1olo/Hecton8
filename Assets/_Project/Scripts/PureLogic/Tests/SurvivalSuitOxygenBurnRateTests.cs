using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SurvivalSuitOxygenBurnRateTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float baseO2Rate = 1.0f;
            float movementStaminaBurn = 1.0f;
            float ambientPressure = 1.0f;

            // Act
            float result = SurvivalSuitOxygenBurnRate.Calculate(baseO2Rate, movementStaminaBurn, ambientPressure);

            // Assert: Verify expected output behaviour
            // 1.0 * (1.0^1.5) * (1.0 + 1.0^2) = 1 * 1 * 2 = 2.0
            Assert.AreEqual(2.0f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float baseO2Rate = 0.5f;
            float movementStaminaBurn = 0.5f;
            float ambientPressure = 1.5f;

            // Act
            float result = SurvivalSuitOxygenBurnRate.Calculate(baseO2Rate, movementStaminaBurn, ambientPressure);

            // Assert
            // 0.5 * (1.5^1.5) * (1.0 + 0.5^2) = 0.5 * 1.837 * 1.25 = 1.148
            Assert.AreEqual(1.148f, result, 0.005f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float baseO2Rate = 0.0f;
            float movementStaminaBurn = 0.0f;
            float ambientPressure = 0.0f;

            // Act
            float result = SurvivalSuitOxygenBurnRate.Calculate(baseO2Rate, movementStaminaBurn, ambientPressure);

            // Assert
            // ambientPressure clamps to 1.0
            // 0.0 * 1.0^1.5 * (1.0 + 0.0^2) = 0.0 * 1.0 * 1.0 = 0.0
            Assert.AreEqual(0.0f, result, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float baseO2Rate = -5.0f;
            float movementStaminaBurn = -2.0f;
            float ambientPressure = -10.0f;

            // Act
            float result = SurvivalSuitOxygenBurnRate.Calculate(baseO2Rate, movementStaminaBurn, ambientPressure);

            // Assert
            // clamps: baseO2=0, moveStam=0, pressure=1
            // 0 * 1^1.5 * (1 + 0^2) = 0
            Assert.AreEqual(0.0f, result, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float baseO2Rate = 1000f;
            float movementStaminaBurn = 10f;
            float ambientPressure = 10f;

            // Act
            float result = SurvivalSuitOxygenBurnRate.Calculate(baseO2Rate, movementStaminaBurn, ambientPressure);

            // Assert
            // 1000 * 10^1.5 * (1 + 10^2) = 1000 * 31.62277 * 101 = 3193899.9
            // Test for appropriate magnitude
            Assert.IsTrue(result > 3000000f && result < 3500000f);
        }

        [Test]
        public void Test_NaNInputs_Case06()
        {
            // Arrange
            float baseO2Rate = float.NaN;
            float movementStaminaBurn = float.PositiveInfinity;
            float ambientPressure = float.NegativeInfinity;

            // Act
            float result = SurvivalSuitOxygenBurnRate.Calculate(baseO2Rate, movementStaminaBurn, ambientPressure);

            // Assert
            Assert.AreEqual(0.0f, result, 0.001f);
        }
    }
}
