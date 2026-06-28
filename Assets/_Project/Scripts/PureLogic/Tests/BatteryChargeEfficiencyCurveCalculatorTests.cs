#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BatteryChargeEfficiencyCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs (below 90% capacity, efficiency should be 1.0)
            float currentCharge = 50f;
            float maxCapacity = 100f;
            float chargePower = 10f; // 10 units per second
            float deltaTime = 1f;

            // Act
            float result = BatteryChargeEfficiencyCurveCalculator.Compute(currentCharge, maxCapacity, chargePower, deltaTime);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(60f, result, 0.0001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Exactly at 90% capacity
            float currentCharge = 90f;
            float maxCapacity = 100f;
            float chargePower = 10f;
            float deltaTime = 0.5f;

            // Act
            float result = BatteryChargeEfficiencyCurveCalculator.Compute(currentCharge, maxCapacity, chargePower, deltaTime);

            // Assert
            Assert.AreEqual(95f, result, 0.0001f, "Verify boundary constraints clamp correctly.");

            // Just above 90%, efficiency starts to drop
            float result2 = BatteryChargeEfficiencyCurveCalculator.Compute(91f, 100f, 10f, 1f);
            Assert.AreEqual(100f, result2, 0.0001f, "Verify boundary constraints clamp correctly.");

            // Test efficiency calculation explicitly without clamping
            float result3 = BatteryChargeEfficiencyCurveCalculator.Compute(95f, 100f, 10f, 0.1f);
            Assert.AreEqual(95.55f, result3, 0.0001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float resultZeroDelta = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 100f, 10f, 0f);
            float resultZeroPower = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 100f, 0f, 1f);
            float resultZeroMax = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 0f, 10f, 1f);
            float resultZeroCharge = BatteryChargeEfficiencyCurveCalculator.Compute(0f, 100f, 10f, 1f);

            // Assert
            Assert.AreEqual(50f, resultZeroDelta, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(50f, resultZeroPower, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(0f, resultZeroMax, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(10f, resultZeroCharge, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float resultNegCharge = BatteryChargeEfficiencyCurveCalculator.Compute(-10f, 100f, 10f, 1f);
            float resultNegMax = BatteryChargeEfficiencyCurveCalculator.Compute(50f, -100f, 10f, 1f);
            float resultNegPower = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 100f, -10f, 1f);
            float resultNegDelta = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 100f, 10f, -1f);

            // Assert
            Assert.AreEqual(10f, resultNegCharge, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(0f, resultNegMax, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(50f, resultNegPower, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(50f, resultNegDelta, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultOvercharge = BatteryChargeEfficiencyCurveCalculator.Compute(150f, 100f, 10f, 1f);
            float resultMassivePower = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 100f, 1000000f, 1f);
            float resultMassiveTime = BatteryChargeEfficiencyCurveCalculator.Compute(50f, 100f, 10f, 1000000f);
            float resultNaN = BatteryChargeEfficiencyCurveCalculator.Compute(float.NaN, 100f, 10f, 1f);
            float resultInf = BatteryChargeEfficiencyCurveCalculator.Compute(float.PositiveInfinity, 100f, 10f, 1f);

            // Assert
            Assert.AreEqual(100f, resultOvercharge, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(100f, resultMassivePower, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(100f, resultMassiveTime, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(0f, resultNaN, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(100f, resultInf, "Verify robust calculation and overflow protection.");
        }
    }
}
#endif
