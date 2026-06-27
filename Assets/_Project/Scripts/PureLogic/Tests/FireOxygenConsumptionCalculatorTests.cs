using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FireOxygenConsumptionCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float fireIntensity01 = 0.5f;
            float compartmentVolumeM3 = 100f;
            float o2Fraction = 0.2f;
            float maxO2ConsumptionRate = 10f;

            // Act
            float result = FireOxygenConsumptionCalculator.Compute(fireIntensity01, compartmentVolumeM3, o2Fraction, maxO2ConsumptionRate);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(5f, result, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxO2ConsumptionRate = 20f;

            // Act & Assert
            // Fire intensity clamped to 1.0 max
            Assert.AreEqual(20f, FireOxygenConsumptionCalculator.Compute(1.5f, 50f, 0.5f, maxO2ConsumptionRate), "Verify fire intensity clamps to 1.0.");
            // Max consumption rate preserved
            Assert.AreEqual(20f, FireOxygenConsumptionCalculator.Compute(1.0f, 50f, 0.5f, maxO2ConsumptionRate), "Verify boundary max consumption rate.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float maxO2ConsumptionRate = 15f;

            // Act & Assert
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0f, 100f, 0.2f, maxO2ConsumptionRate), "Verify no fire intensity means zero consumption.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, 0f, 0.2f, maxO2ConsumptionRate), "Verify zero volume means zero consumption.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, 100f, 0f, maxO2ConsumptionRate), "Verify zero oxygen means zero consumption.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, 100f, 0.2f, 0f), "Verify zero max rate means zero consumption.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float maxO2ConsumptionRate = 10f;

            // Act & Assert
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(-0.5f, 100f, 0.2f, maxO2ConsumptionRate), "Verify negative fire intensity clamps to 0.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, -100f, 0.2f, maxO2ConsumptionRate), "Verify negative volume clamps to 0.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, 100f, -0.2f, maxO2ConsumptionRate), "Verify negative oxygen clamps to 0.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, 100f, 0.2f, -10f), "Verify negative max rate clamps to 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float extremeValue = 1e30f;

            // Act & Assert
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(float.NaN, 100f, 0.2f, 10f), "Verify NaN fire intensity is handled gracefully.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, float.PositiveInfinity, 0.2f, 10f), "Verify positive infinity volume is handled gracefully.");
            Assert.AreEqual(0f, FireOxygenConsumptionCalculator.Compute(0.5f, 100f, 0.2f, float.NegativeInfinity), "Verify negative infinity max rate is handled gracefully.");

            float extremeResult = FireOxygenConsumptionCalculator.Compute(1.0f, extremeValue, 1.0f, extremeValue);
            Assert.AreEqual(extremeValue, extremeResult, "Verify extremely large parameters do not overflow in standard arithmetic if clamped properly.");
        }
    }
}
