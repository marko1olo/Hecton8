using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PressureEqualizationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float internalPressure = 100000f; // 1 atm
            float externalPressure = 50000f;  // 0.5 atm
            float airlockVolume = 10f; // 10 cubic meters
            float flowRate = 1f; // 1 cubic meter per second

            // Act
            float result = PressureEqualizationCalculator.Compute(internalPressure, externalPressure, airlockVolume, flowRate);

            // Assert: Verify expected output behaviour
            // expected = (10 / 1) * (50000 / 100000) = 5
            Assert.AreEqual(5f, result, 0.001f, "Verify standard calculations return expected results.");

            // Large differential, small valve: long time
            float longTime = PressureEqualizationCalculator.Compute(100000f, 1000f, 10f, 0.1f);
            // expected = (10 / 0.1) * (99000 / 100000) = 100 * 0.99 = 99
            Assert.AreEqual(99f, longTime, 0.001f, "Large differential, small valve should yield long time.");

            // Large valve: fast
            float fastTime = PressureEqualizationCalculator.Compute(100000f, 1000f, 10f, 10f);
            // expected = (10 / 10) * (99000 / 100000) = 1 * 0.99 = 0.99
            Assert.AreEqual(0.99f, fastTime, 0.001f, "Large valve should yield fast time.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)

            // Equal pressures: zero time.
            float equalPressure = PressureEqualizationCalculator.Compute(100000f, 100000f, 10f, 1f);
            Assert.AreEqual(0f, equalPressure, 0.001f, "Equal pressures should yield zero time.");

            float tinyDelta = PressureEqualizationCalculator.Compute(100000.001f, 100000f, 10f, 1f);
            Assert.AreEqual(0f, tinyDelta, 0.001f, "Tiny delta pressure should yield zero time.");

            float smallValve = PressureEqualizationCalculator.Compute(100000f, 50000f, 10f, 0.00001f);
            Assert.AreEqual(float.MaxValue, smallValve, "Tiny valve should yield max value (infinite time).");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float zeroVolume = PressureEqualizationCalculator.Compute(100000f, 50000f, 0f, 1f);
            Assert.AreEqual(0f, zeroVolume, "Zero volume should yield zero time.");

            float zeroFlow = PressureEqualizationCalculator.Compute(100000f, 50000f, 10f, 0f);
            Assert.AreEqual(float.MaxValue, zeroFlow, "Zero flow should yield infinite time (float.MaxValue).");

            float zeroPressureBoth = PressureEqualizationCalculator.Compute(0f, 0f, 10f, 1f);
            Assert.AreEqual(0f, zeroPressureBoth, "Zero pressure on both sides should yield zero time.");

            float zeroPressureOneSide = PressureEqualizationCalculator.Compute(0f, 100000f, 10f, 1f);
            Assert.AreEqual(10f, zeroPressureOneSide, 0.001f, "Zero pressure on one side should yield Max time based on volume/flow.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float negativePressure = PressureEqualizationCalculator.Compute(-100000f, 50000f, 10f, 1f);
            // Negative pressure should be clamped to 0.
            // So delta = 50000. time = (10/1) * (50000/50000) = 10
            Assert.AreEqual(10f, negativePressure, 0.001f, "Negative pressure should clamp to zero.");

            float negativeVolume = PressureEqualizationCalculator.Compute(100000f, 50000f, -10f, 1f);
            // Negative volume clamped to 0 -> zero time
            Assert.AreEqual(0f, negativeVolume, 0.001f, "Negative volume should clamp to zero.");

            float negativeFlow = PressureEqualizationCalculator.Compute(100000f, 50000f, 10f, -1f);
            // Negative flow clamped to 0 -> infinite time
            Assert.AreEqual(float.MaxValue, negativeFlow, "Negative flow should clamp to zero.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float infinityPressure = PressureEqualizationCalculator.Compute(float.PositiveInfinity, 50000f, 10f, 1f);
            // Infinity handled as 0 -> delta = 50000 -> time = 10 * 1 = 10
            Assert.AreEqual(10f, infinityPressure, 0.001f, "Infinity pressure should be handled safely.");

            float nanPressure = PressureEqualizationCalculator.Compute(float.NaN, 50000f, 10f, 1f);
            // NaN handled as 0 -> delta = 50000 -> time = 10
            Assert.AreEqual(10f, nanPressure, 0.001f, "NaN pressure should be handled safely.");

            float extremeFlow = PressureEqualizationCalculator.Compute(100000f, 50000f, 10f, float.MaxValue);
            Assert.AreEqual(0f, extremeFlow, 0.001f, "Extreme flow should yield zero time.");

            float extremeVolume = PressureEqualizationCalculator.Compute(100000f, 50000f, float.MaxValue, 1f);
            // float.MaxValue / 1 * 0.5 = float.MaxValue / 2
            Assert.AreEqual(float.MaxValue / 2f, extremeVolume, float.MaxValue / 100f, "Extreme volume should yield float.MaxValue/2 for this pressure diff.");
        }
    }
}
