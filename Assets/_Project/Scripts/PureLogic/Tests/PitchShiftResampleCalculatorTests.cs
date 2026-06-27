using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PitchShiftResampleCalculatorTests
    {
        private const float FloatTolerance = 0.0001f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float originalSampleRate = 44100f;

            // Act
            float resultZero = PitchShiftResampleCalculator.Compute(0f, originalSampleRate);
            float resultUpOctave = PitchShiftResampleCalculator.Compute(12f, originalSampleRate);
            float resultDownOctave = PitchShiftResampleCalculator.Compute(-12f, originalSampleRate);
            float resultRatio1Test = PitchShiftResampleCalculator.Compute(0f, 1.0f);
            float resultRatio2Test = PitchShiftResampleCalculator.Compute(12f, 1.0f);
            float resultRatioHalfTest = PitchShiftResampleCalculator.Compute(-12f, 1.0f);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(44100f, resultZero, FloatTolerance, "0 semitones should return the original sample rate.");
            Assert.AreEqual(88200f, resultUpOctave, FloatTolerance, "12 semitones up should double the sample rate.");
            Assert.AreEqual(22050f, resultDownOctave, FloatTolerance, "-12 semitones down should halve the sample rate.");

            // Required ratios verification
            Assert.AreEqual(1.0f, resultRatio1Test, FloatTolerance, "0 semitones ratio should be 1.0");
            Assert.AreEqual(2.0f, resultRatio2Test, FloatTolerance, "12 semitones ratio should be 2.0");
            Assert.AreEqual(0.5f, resultRatioHalfTest, FloatTolerance, "-12 semitones ratio should be 0.5");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxRate = 384000f;
            float outOfBoundsRate = 400000f;

            // Act
            float clampedRateResult = PitchShiftResampleCalculator.Compute(0f, outOfBoundsRate);
            float highSemitoneResult = PitchShiftResampleCalculator.Compute(120f, 1.0f);
            float lowSemitoneResult = PitchShiftResampleCalculator.Compute(-120f, 1.0f);
            float outOfBoundsHighSemitoneResult = PitchShiftResampleCalculator.Compute(150f, 1.0f);
            float outOfBoundsLowSemitoneResult = PitchShiftResampleCalculator.Compute(-150f, 1.0f);

            // Assert
            Assert.AreEqual(maxRate, clampedRateResult, FloatTolerance, "Original sample rate should be clamped to 384000.");
            Assert.AreEqual(MathF.Pow(2f, 120f / 12f), highSemitoneResult, FloatTolerance, "120 semitones should correspond to 2^10.");
            Assert.AreEqual(MathF.Pow(2f, -120f / 12f), lowSemitoneResult, FloatTolerance, "-120 semitones should correspond to 2^-10.");
            Assert.AreEqual(highSemitoneResult, outOfBoundsHighSemitoneResult, FloatTolerance, "Semitones should be clamped to 120.");
            Assert.AreEqual(lowSemitoneResult, outOfBoundsLowSemitoneResult, FloatTolerance, "Semitones should be clamped to -120.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            // Act
            float resultZeroRate = PitchShiftResampleCalculator.Compute(12f, 0f);
            float resultBothZero = PitchShiftResampleCalculator.Compute(0f, 0f);

            // Assert
            Assert.AreEqual(0f, resultZeroRate, FloatTolerance, "0 sample rate should return 0.");
            Assert.AreEqual(0f, resultBothZero, FloatTolerance, "0 sample rate and 0 semitones should return 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float resultNegativeRate = PitchShiftResampleCalculator.Compute(12f, -44100f);

            // Assert
            Assert.AreEqual(0f, resultNegativeRate, FloatTolerance, "Negative sample rate should be clamped to 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float resultNaNRate = PitchShiftResampleCalculator.Compute(12f, float.NaN);
            float resultInfRate = PitchShiftResampleCalculator.Compute(12f, float.PositiveInfinity);
            float resultNaNSemitones = PitchShiftResampleCalculator.Compute(float.NaN, 44100f);
            float resultInfSemitones = PitchShiftResampleCalculator.Compute(float.NegativeInfinity, 44100f);

            // Assert
            Assert.AreEqual(0f, resultNaNRate, FloatTolerance, "NaN sample rate should fall back to 0.");
            Assert.AreEqual(0f, resultInfRate, FloatTolerance, "Infinity sample rate should fall back to 0.");
            Assert.AreEqual(44100f, resultNaNSemitones, FloatTolerance, "NaN semitones should fall back to 0 semitones.");
            Assert.AreEqual(44100f, resultInfSemitones, FloatTolerance, "Infinity semitones should fall back to 0 semitones.");
        }
    }
}
