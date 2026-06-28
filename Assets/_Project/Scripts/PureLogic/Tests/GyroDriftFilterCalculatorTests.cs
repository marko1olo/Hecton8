using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class GyroDriftFilterCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float cutoffFreq = 5.0f;
            float sampleRate = 60.0f;

            // RC = 1 / (2 * pi * 5) = 0.0318
            // dt = 1 / 60 = 0.01667
            // alpha = dt / (RC + dt) = 0.01667 / (0.0318 + 0.01667) = 0.344

            // Act: Apply a DC offset of 10f continuously.
            float previousDrift = 0f;
            float gyroSampleDC = 10f;
            for (int i = 0; i < 60; i++)
            {
                previousDrift = GyroDriftFilterCalculator.Compute(gyroSampleDC, previousDrift, cutoffFreq, sampleRate);
            }

            // Assert: DC offset (drift) is filtered out in the overall system, meaning the drift estimate converges to the DC offset.
            Assert.That(previousDrift, Is.EqualTo(10f).Within(0.1f), "DC offset (drift) should be fully captured by the low-pass estimate.");

            // Act: Apply a high frequency rotation of 100f for one frame starting from 0 drift
            float outHighFreq = GyroDriftFilterCalculator.Compute(100f, 0f, cutoffFreq, sampleRate);

            // Assert: High frequency rotation passes through the high-pass filter, meaning the low-pass estimate barely reacts.
            Assert.That(outHighFreq < 40f, Is.True, "High frequency rotation should not heavily affect the low-pass drift estimate.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float sampleRate = 60.0f;

            // Case A: Cutoff frequency approaching 0
            float outLowCutoff = GyroDriftFilterCalculator.Compute(10f, 5f, 0.0001f, sampleRate);
            Assert.That(outLowCutoff, Is.EqualTo(5f).Within(0.1f), "Extremely low cutoff heavily favors previous value.");

            // Case B: Extremely high cutoff frequency
            float outHighCutoff = GyroDriftFilterCalculator.Compute(10f, 5f, 1000000f, sampleRate);
            Assert.That(outHighCutoff, Is.EqualTo(10f).Within(0.1f), "Extremely high cutoff heavily favors new sample.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values

            // Zero sample rate
            float outZeroSampleRate = GyroDriftFilterCalculator.Compute(10f, 5f, 10f, 0f);
            Assert.That(outZeroSampleRate, Is.EqualTo(5f), "Zero sample rate should return previous value");

            // Zero cutoff frequency
            float outZeroCutoff = GyroDriftFilterCalculator.Compute(10f, 5f, 0f, 60f);
            Assert.That(outZeroCutoff, Is.EqualTo(10f), "Zero cutoff should return gyro sample");

            // Zero gyro sample
            float outZeroSample = GyroDriftFilterCalculator.Compute(0f, 5f, 10f, 60f);
            Assert.That(outZeroSample > 0f && outZeroSample < 5f, Is.True, "Zero gyro sample with positive drift should return positive decayed value");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            // Negative cutoff
            float outNegCutoff = GyroDriftFilterCalculator.Compute(10f, 5f, -10f, 60f);
            Assert.That(outNegCutoff, Is.EqualTo(10f), "Negative cutoff should return gyro sample");

            // Negative sample rate
            float outNegSampleRate = GyroDriftFilterCalculator.Compute(10f, 5f, 10f, -60f);
            Assert.That(outNegSampleRate, Is.EqualTo(5f), "Negative sample rate should return previous value");

            // Negative sample and previous
            float outNegSample = GyroDriftFilterCalculator.Compute(-10f, -5f, 10f, 60f);
            Assert.That(outNegSample > -10f && outNegSample < -5f, Is.True, "Negative signals should be filtered normally");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float prev = 5f;

            // NaN
            Assert.That(GyroDriftFilterCalculator.Compute(float.NaN, prev, 10f, 60f), Is.EqualTo(prev), "NaN sample returns previous");
            Assert.That(GyroDriftFilterCalculator.Compute(10f, prev, float.NaN, 60f), Is.EqualTo(prev), "NaN cutoff returns previous");
            Assert.That(GyroDriftFilterCalculator.Compute(10f, prev, 10f, float.NaN), Is.EqualTo(prev), "NaN sampleRate returns previous");

            // Infinity
            Assert.That(GyroDriftFilterCalculator.Compute(float.PositiveInfinity, prev, 10f, 60f), Is.EqualTo(prev), "Infinity sample returns previous");
            Assert.That(GyroDriftFilterCalculator.Compute(10f, prev, float.PositiveInfinity, 60f), Is.EqualTo(prev), "Infinity cutoff returns previous");
            Assert.That(GyroDriftFilterCalculator.Compute(10f, prev, 10f, float.PositiveInfinity), Is.EqualTo(prev), "Infinity sampleRate returns previous");

            // MaxValue
            float outMax = GyroDriftFilterCalculator.Compute(float.MaxValue, prev, 10f, 60f);
            Assert.That(float.IsInfinity(outMax), Is.False, "MaxValue input should not result in infinity due to double calculation");
            Assert.That(float.IsNaN(outMax), Is.False, "MaxValue input should not result in NaN");
        }
    }
}
