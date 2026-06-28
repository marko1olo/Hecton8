using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class O2BarPulseRateCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float o2Level01 = 0.4f;
            float warningThreshold = 0.5f;
            float criticalThreshold = 0.1f;
            float baseFrequencyHz = 1.0f;
            float maxFrequencyHz = 5.0f;

            // Act
            float result = O2BarPulseRateCalculator.Compute(o2Level01, warningThreshold, criticalThreshold, baseFrequencyHz, maxFrequencyHz);

            // Assert: Verify expected output behaviour
            // 0.4 is 1/4 of the way between 0.5 and 0.1, ratio is 0.25
            // 1.0 + 0.25 * (5.0 - 1.0) = 2.0
            Assert.AreEqual(2.0f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float warningThreshold = 0.5f;
            float criticalThreshold = 0.1f;
            float baseFrequencyHz = 1.0f;
            float maxFrequencyHz = 5.0f;

            // Act & Assert
            // Above warning
            Assert.AreEqual(0.0f, O2BarPulseRateCalculator.Compute(0.6f, warningThreshold, criticalThreshold, baseFrequencyHz, maxFrequencyHz), 0.001f, "Verify above warning returns 0Hz.");

            // At warning
            Assert.AreEqual(baseFrequencyHz, O2BarPulseRateCalculator.Compute(warningThreshold, warningThreshold, criticalThreshold, baseFrequencyHz, maxFrequencyHz), 0.001f, "Verify at warning returns base frequency.");

            // At critical
            Assert.AreEqual(maxFrequencyHz, O2BarPulseRateCalculator.Compute(criticalThreshold, warningThreshold, criticalThreshold, baseFrequencyHz, maxFrequencyHz), 0.001f, "Verify at critical returns max frequency.");

            // Below critical
            Assert.AreEqual(maxFrequencyHz, O2BarPulseRateCalculator.Compute(0.0f, warningThreshold, criticalThreshold, baseFrequencyHz, maxFrequencyHz), 0.001f, "Verify below critical returns max frequency.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result1 = O2BarPulseRateCalculator.Compute(0f, 0f, 0f, 0f, 0f);
            float result2 = O2BarPulseRateCalculator.Compute(0.5f, 0.5f, 0.5f, 1f, 5f);

            // Assert
            Assert.AreEqual(0.0f, result1, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(0.0f, result2, 0.001f, "Verify identical thresholds are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act & Assert
            // O2 level clamped to 0
            float res1 = O2BarPulseRateCalculator.Compute(-0.5f, 0.5f, 0.1f, 1.0f, 5.0f);
            Assert.AreEqual(5.0f, res1, 0.001f, "Verify negative O2 level clamps to 0 and returns max frequency.");

            // Thresholds clamped
            float res2 = O2BarPulseRateCalculator.Compute(0.5f, -0.5f, -0.9f, 1.0f, 5.0f);
            Assert.AreEqual(0.0f, res2, 0.001f, "Verify negative thresholds clamp to 0 and safely fallback to 0Hz.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act & Assert
            // NaN O2
            Assert.AreEqual(0.0f, O2BarPulseRateCalculator.Compute(float.NaN, 0.5f, 0.1f, 1.0f, 5.0f), 0.001f, "Verify NaN O2 gracefully returns 0.");

            // Infinity O2
            Assert.AreEqual(0.0f, O2BarPulseRateCalculator.Compute(float.PositiveInfinity, 0.5f, 0.1f, 1.0f, 5.0f), 0.001f, "Verify Infinity O2 gracefully returns 0.");

            // Extreme values
            Assert.AreEqual(5000000.0f, O2BarPulseRateCalculator.Compute(0.1f, 0.5f, 0.1f, 1.0f, 5000000.0f), 0.001f, "Verify large maxFrequency limits correctly without precision failure.");
        }
    }
}
