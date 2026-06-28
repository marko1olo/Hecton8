using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PitchTrimCorrectionCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float pitchAngleDeg = 45f;
            float trimGain = 100f;
            float maxTrimForceN = 10000f;
            float pitchAngularVelocity = 2f;
            float dampingCoeff = 50f;

            // Act
            float result = PitchTrimCorrectionCalculator.Compute(pitchAngleDeg, trimGain, maxTrimForceN, pitchAngularVelocity, dampingCoeff);

            // Assert: Verify expected output behaviour
            // pTerm = 45 * 100 = 4500
            // dTerm = 2 * 50 = 100
            // expected = 4500 - 100 = 4400
            Assert.AreEqual(4400f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float pitchAngleDeg = 90f;
            float trimGain = 500f;
            float maxTrimForceN = 20000f; // Limit
            float pitchAngularVelocity = -10f;
            float dampingCoeff = 100f;

            // Act
            float result = PitchTrimCorrectionCalculator.Compute(pitchAngleDeg, trimGain, maxTrimForceN, pitchAngularVelocity, dampingCoeff);

            // Assert
            // pTerm = 90 * 500 = 45000
            // dTerm = -10 * 100 = -1000
            // correction = 45000 - (-1000) = 46000
            // Clamped to 20000
            Assert.AreEqual(20000f, result, 0.001f);

            // Arrange Negative bound
            pitchAngleDeg = -90f;
            pitchAngularVelocity = 10f;

            // Act
            float negativeResult = PitchTrimCorrectionCalculator.Compute(pitchAngleDeg, trimGain, maxTrimForceN, pitchAngularVelocity, dampingCoeff);

            // Assert
            // pTerm = -90 * 500 = -45000
            // dTerm = 10 * 100 = 1000
            // correction = -45000 - 1000 = -46000
            // Clamped to -20000
            Assert.AreEqual(-20000f, negativeResult, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float pitchAngleDeg = 0f;
            float trimGain = 0f;
            float maxTrimForceN = 0f;
            float pitchAngularVelocity = 0f;
            float dampingCoeff = 0f;

            // Act
            float result = PitchTrimCorrectionCalculator.Compute(pitchAngleDeg, trimGain, maxTrimForceN, pitchAngularVelocity, dampingCoeff);

            // Assert
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float pitchAngleDeg = 10f;
            float trimGain = -50f; // should clamp to 0
            float maxTrimForceN = -100f; // should clamp to 0
            float pitchAngularVelocity = 5f;
            float dampingCoeff = -20f; // should clamp to 0

            // Act
            float result = PitchTrimCorrectionCalculator.Compute(pitchAngleDeg, trimGain, maxTrimForceN, pitchAngularVelocity, dampingCoeff);

            // Assert
            // pTerm = 10 * 0 = 0
            // dTerm = 5 * 0 = 0
            // Clamped to 0
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float pitchAngleDeg = float.PositiveInfinity;
            float trimGain = float.NaN;
            float maxTrimForceN = float.NegativeInfinity;
            float pitchAngularVelocity = float.NaN;
            float dampingCoeff = float.PositiveInfinity;

            // Act
            float result = PitchTrimCorrectionCalculator.Compute(pitchAngleDeg, trimGain, maxTrimForceN, pitchAngularVelocity, dampingCoeff);

            // Assert
            // All NaNs and Infinities should be treated as 0
            Assert.AreEqual(0f, result, 0.001f);

            // Extreme large finite numbers
            float largeResult = PitchTrimCorrectionCalculator.Compute(float.MaxValue, 10f, float.MaxValue, -float.MaxValue, 10f);
            Assert.AreEqual(float.MaxValue, largeResult); // Clamped to MaxValue even if addition overflows
        }
    }
}
