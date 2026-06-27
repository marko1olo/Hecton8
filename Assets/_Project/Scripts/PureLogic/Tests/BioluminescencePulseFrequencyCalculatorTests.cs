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
            Assert.AreEqual(2.0f, result, 0.001f);

            // Further happy path: Max stress, deep
            float resultMax = BioluminescencePulseFrequencyCalculator.Compute(
                1f, 100f, 2.0f, 1.5f, 0.1f);
            // 2.0 * 1.5 + 100 * 0.1 = 3.0 + 10.0 = 13.0
            Assert.AreEqual(13.0f, resultMax, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Stress slightly below 0, slightly above 1
            float resultBelow = BioluminescencePulseFrequencyCalculator.Compute(
                -0.1f, 0f, 2.0f, 1.5f, 0f); // stress clamped to 0
            Assert.AreEqual(2.0f, resultBelow, 0.001f);

            float resultAbove = BioluminescencePulseFrequencyCalculator.Compute(
                1.1f, 0f, 2.0f, 1.5f, 0f); // stress clamped to 1
            Assert.AreEqual(3.0f, resultAbove, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float resultAllZero = BioluminescencePulseFrequencyCalculator.Compute(
                0f, 0f, 0f, 0f, 0f);

            // Assert
            Assert.AreEqual(0f, resultAllZero, 0.001f);
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
            Assert.AreEqual(0f, resultNeg, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultInf = BioluminescencePulseFrequencyCalculator.Compute(
                float.PositiveInfinity, float.NaN, float.NaN, float.NegativeInfinity, float.PositiveInfinity);

            // Assert
            // fallback behavior check (should not be NaN or Infinity if possible, or handle gracefully)
            Assert.IsFalse(float.IsNaN(resultInf));
        }
    }
}
