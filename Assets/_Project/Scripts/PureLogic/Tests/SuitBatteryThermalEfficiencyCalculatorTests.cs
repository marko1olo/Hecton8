using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SuitBatteryThermalEfficiencyCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float temp1 = 10f;
            float temp2 = 5f;
            float temp3 = 15f;
            float drainRate = 10f;

            // Act
            float result1 = SuitBatteryThermalEfficiencyCalculator.Compute(temp1, drainRate);
            float result2 = SuitBatteryThermalEfficiencyCalculator.Compute(temp2, drainRate);
            float result3 = SuitBatteryThermalEfficiencyCalculator.Compute(temp3, drainRate);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(1.25f, result1, 0.001f);
            Assert.AreEqual(1.375f, result2, 0.001f);
            Assert.AreEqual(1.125f, result3, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float temp0 = 0f;
            float temp20 = 20f;
            float drainRate = 10f;

            // Act
            float result0 = SuitBatteryThermalEfficiencyCalculator.Compute(temp0, drainRate);
            float result20 = SuitBatteryThermalEfficiencyCalculator.Compute(temp20, drainRate);

            // Assert
            Assert.AreEqual(1.5f, result0, 0.001f);
            Assert.AreEqual(1.0f, result20, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float temp = 10f;
            float drainRate = 0f;

            // Act
            float result = SuitBatteryThermalEfficiencyCalculator.Compute(temp, drainRate);

            // Assert
            Assert.AreEqual(1.0f, result, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float temp = 10f;
            float drainRate = -5f;

            // Act
            float result = SuitBatteryThermalEfficiencyCalculator.Compute(temp, drainRate);

            // Assert
            Assert.AreEqual(1.0f, result, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float drainRate = 10f;

            // Act
            float resultNanTemp = SuitBatteryThermalEfficiencyCalculator.Compute(float.NaN, drainRate);
            float resultInfTemp = SuitBatteryThermalEfficiencyCalculator.Compute(float.PositiveInfinity, drainRate);
            float resultNegInfTemp = SuitBatteryThermalEfficiencyCalculator.Compute(float.NegativeInfinity, drainRate);

            float resultNanDrain = SuitBatteryThermalEfficiencyCalculator.Compute(10f, float.NaN);
            float resultInfDrain = SuitBatteryThermalEfficiencyCalculator.Compute(10f, float.PositiveInfinity);

            float resultExtremelyCold = SuitBatteryThermalEfficiencyCalculator.Compute(-1000f, drainRate);
            float resultExtremelyHot = SuitBatteryThermalEfficiencyCalculator.Compute(1000f, drainRate);

            // Assert
            Assert.AreEqual(1.0f, resultNanTemp, 0.001f);
            Assert.AreEqual(1.0f, resultInfTemp, 0.001f);
            Assert.AreEqual(1.0f, resultNegInfTemp, 0.001f);

            Assert.AreEqual(1.0f, resultNanDrain, 0.001f);
            Assert.AreEqual(1.0f, resultInfDrain, 0.001f);

            Assert.AreEqual(1.5f, resultExtremelyCold, 0.001f);
            Assert.AreEqual(1.0f, resultExtremelyHot, 0.001f);
        }
    }
}
