using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BioluminescencePulseFrequencyCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float creatureStressLevel01 = 0f;
            float depthMeters = 0f;
            float baseFrequencyHz = 2.0f;
            float stressFrequencyMultiplier = 1.5f;
            float depthFrequencyMultiplier = 0.1f;

            // Act
            float result = BioluminescencePulseFrequencyCalculator.Compute(
                creatureStressLevel01, depthMeters, baseFrequencyHz,
                stressFrequencyMultiplier, depthFrequencyMultiplier);

            // Assert: Verify expected output behaviour
            // No stress, surface -> result = baseFrequencyHz
            Assert.That(result, Is.EqualTo(2.0f).Within(0.001f));

            // Further happy path: Max stress, deep
            float resultMax = BioluminescencePulseFrequencyCalculator.Compute(
                1f, 100f, 2.0f, 1.5f, 0.1f);
            // 2.0 * 1.5 + 100 * 0.1 = 3.0 + 10.0 = 13.0
            Assert.That(resultMax, Is.EqualTo(13.0f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Stress slightly below 0, slightly above 1
            float resultBelow = BioluminescencePulseFrequencyCalculator.Compute(
                -0.1f, 0f, 2.0f, 1.5f, 0f); // stress clamped to 0
            Assert.That(resultBelow, Is.EqualTo(2.0f).Within(0.001f));

            float resultAbove = BioluminescencePulseFrequencyCalculator.Compute(
                1.1f, 0f, 2.0f, 1.5f, 0f); // stress clamped to 1
            Assert.That(resultAbove, Is.EqualTo(3.0f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float resultAllZero = BioluminescencePulseFrequencyCalculator.Compute(
                0f, 0f, 0f, 0f, 0f);

            // Assert
            Assert.That(resultAllZero, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // baseFreq negative, depth negative, multiplier negative
            float resultNeg = BioluminescencePulseFrequencyCalculator.Compute(
                0.5f, -100f, -5.0f, -2.0f, -1.0f);

            // Assert
            // negative depth -> clamped to 0
            // negative baseFreq -> clamped to 0
            // negative stressMultiplier -> clamped to 0
            // negative depthMult -> clamped to 0
            // output should be 0
            Assert.That(resultNeg, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultInf = BioluminescencePulseFrequencyCalculator.Compute(
                float.PositiveInfinity, float.NaN, float.NaN, float.NegativeInfinity, float.PositiveInfinity);

            // Assert
            // fallback behavior check (should not be NaN or Infinity if possible, or handle gracefully)
            Assert.That(float.IsNaN(resultInf), Is.False);
        }

        [Test]
        public void Test_SanitizeFinite_Fallbacks_Case06()
        {
            // Arrange: Trigger NaN/Infinity fallbacks
            // Fallbacks: stress=0, depth=0, baseFreq=0, stressMult=1, depthMult=0
            float resultNaN = BioluminescencePulseFrequencyCalculator.Compute(
                float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);

            float resultInf = BioluminescencePulseFrequencyCalculator.Compute(
                float.PositiveInfinity, float.NegativeInfinity, float.PositiveInfinity, float.NegativeInfinity, float.PositiveInfinity);

            // Assert
            Assert.That(resultNaN, Is.EqualTo(0f).Within(0.001f));
            Assert.That(resultInf, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_ConstraintGuarding_Case07()
        {
            // Arrange: Explicit constraints checking
            // Stress clamping above 1f
            float resultClampStress = BioluminescencePulseFrequencyCalculator.Compute(
                1.5f, 5f, 10f, 2f, 0.5f);

            // Expected: stress=1f
            // stressMult = 2f, base = 10f, depth = 5f, depthMult = 0.5f
            // effectStressMult = 1 + 1 * (2-1) = 2
            // appliedFreq = 10 * 2 = 20
            // depthBoost = 5 * 0.5 = 2.5
            // rawFreq = 22.5
            Assert.That(resultClampStress, Is.EqualTo(22.5f).Within(0.001f));

            // Stress clamping below 0f
            float resultClampStressLow = BioluminescencePulseFrequencyCalculator.Compute(
                -0.5f, 5f, 10f, 2f, 0.5f);

            // Expected: stress=0f
            // effectStressMult = 1 + 0 * (2-1) = 1
            // appliedFreq = 10 * 1 = 10
            // depthBoost = 5 * 0.5 = 2.5
            // rawFreq = 12.5
            Assert.That(resultClampStressLow, Is.EqualTo(12.5f).Within(0.001f));
        }
    }
}
