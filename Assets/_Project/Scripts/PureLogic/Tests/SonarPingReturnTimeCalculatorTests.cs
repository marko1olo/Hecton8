using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SonarPingReturnTimeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float distance = 1500f;
            float soundSpeed = 1500f;
            float freq = 1000f;
            float temp = 0f;
            float radialVel = 0f;
            float coeff = 4.6f;
            float minSpeed = 1400f;
            float maxSpeed = 1600f;

            // Act
            var result = SonarPingReturnTimeCalculator.Compute(distance, soundSpeed, freq, temp, radialVel, coeff, minSpeed, maxSpeed);

            // Assert
            Assert.That(result.returnTimeSeconds, Is.EqualTo(2f).Within(0.001f), "Return time should be 2 seconds for 1500m at 1500m/s");
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(1000f).Within(0.001f), "Frequency should not shift if relative velocity is 0");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float distance = 1500f;
            float soundSpeed = 1500f;
            float freq = 1000f;
            float temp = 100f; // High temp to force clamping at max speed
            float radialVel = 100f; // Target moving away
            float coeff = 4.6f;
            float minSpeed = 1400f;
            float maxSpeed = 1600f;

            // Act
            var result = SonarPingReturnTimeCalculator.Compute(distance, soundSpeed, freq, temp, radialVel, coeff, minSpeed, maxSpeed);

            // Assert
            Assert.That(result.returnTimeSeconds, Is.EqualTo(1500f * 2f / 1600f).Within(0.001f), "Should clamp sound speed to max");
            // freq * (c / (c + v)) = 1000 * (1600 / 1700) = 941.176
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(941.176f).Within(0.01f), "Doppler shift should calculate with clamped speed");

            // Force min boundary. NOTE: previous test had wrong temp coefficient param vs value
            // We pass temp=0 here to ensure sound speed is 1000 and it clamps to 1400 min
            var result2 = SonarPingReturnTimeCalculator.Compute(1500f, 1000f, 1000f, 0f, 0f, 4.6f, 1400f, 1600f);
            Assert.That(result2.returnTimeSeconds, Is.EqualTo(1500f * 2f / 1400f).Within(0.001f), "Should clamp sound speed to min");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            // Act
            var result = SonarPingReturnTimeCalculator.Compute(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

            // Assert
            Assert.That(result.returnTimeSeconds, Is.EqualTo(0f), "Return time should be 0 for zero distance");
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(0f), "Frequency should be 0 for zero base freq or division by 0");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float distance = -100f;
            float soundSpeed = 1500f;
            float freq = 1000f;
            float temp = 0f;
            float radialVel = -3000f; // Huge negative radial vel
            float coeff = 4.6f;
            float minSpeed = 1400f;
            float maxSpeed = 1600f;

            // Act
            var result = SonarPingReturnTimeCalculator.Compute(distance, soundSpeed, freq, temp, radialVel, coeff, minSpeed, maxSpeed);

            // Assert
            Assert.That(result.returnTimeSeconds, Is.EqualTo(0f), "Negative distance should be clamped to 0");
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(0f), "Negative Doppler frequency should be clamped to 0");
        }

        [Test]
        public void Test_DenominatorZero_Case06()
        {
            var result = SonarPingReturnTimeCalculator.Compute(1500f, 1500f, 1000f, 0f, -1500f, 4.6f, 1400f, 1600f);
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(0f), "Should return 0 freq if denominator is 0 (target moving towards us at speed of sound)");
        }

        [Test]
        public void Test_NegativeClampedSpeed_Case07()
        {
            var result = SonarPingReturnTimeCalculator.Compute(1500f, -1500f, 1000f, 0f, 0f, 4.6f, -1600f, -1400f);
            Assert.That(result.returnTimeSeconds, Is.EqualTo(0f), "Should return 0 time if clamped speed <= 0");
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(0f), "Should return 0 freq if clamped speed <= 0");
        }

        [TestCase(float.NaN, 1500f, 1000f, 0f, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(float.PositiveInfinity, 1500f, 1000f, 0f, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, float.NaN, 1000f, 0f, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, float.PositiveInfinity, 1000f, 0f, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, float.NaN, 0f, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, float.PositiveInfinity, 0f, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, float.NaN, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, float.PositiveInfinity, 0f, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, float.NaN, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, float.PositiveInfinity, 4.6f, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, 0f, float.NaN, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, 0f, float.PositiveInfinity, 1400f, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, 0f, 4.6f, float.NaN, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, 0f, 4.6f, float.PositiveInfinity, 1600f)]
        [TestCase(1500f, 1500f, 1000f, 0f, 0f, 4.6f, 1400f, float.NaN)]
        [TestCase(1500f, 1500f, 1000f, 0f, 0f, 4.6f, 1400f, float.PositiveInfinity)]
        public void Test_AllInvalidInputs_Case08(float dist, float speed, float freq, float temp, float radVel, float coeff, float minSpeed, float maxSpeed)
        {
            var result = SonarPingReturnTimeCalculator.Compute(dist, speed, freq, temp, radVel, coeff, minSpeed, maxSpeed);
            Assert.That(result.returnTimeSeconds, Is.EqualTo(0f), "NaN or Infinity should return 0 time");
            Assert.That(result.dopplerShiftedFrequencyHz, Is.EqualTo(0f), "NaN or Infinity should return 0 freq");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float soundSpeed = 1500f;
            float freq = 1000f;
            float temp = 0f;
            float radialVel = 0f;
            float coeff = 4.6f;
            float minSpeed = 1400f;
            float maxSpeed = 1600f;

            // Act & Assert
            var result = SonarPingReturnTimeCalculator.Compute(float.NaN, soundSpeed, freq, temp, radialVel, coeff, minSpeed, maxSpeed);
            Assert.That(result.returnTimeSeconds, Is.EqualTo(0f), "NaN distance should return 0");

            var result2 = SonarPingReturnTimeCalculator.Compute(1500f, float.PositiveInfinity, freq, temp, radialVel, coeff, minSpeed, maxSpeed);
            Assert.That(result2.dopplerShiftedFrequencyHz, Is.EqualTo(0f), "Infinity sound speed should return 0");

            var result3 = SonarPingReturnTimeCalculator.Compute(1500f, soundSpeed, freq, temp, float.NegativeInfinity, coeff, minSpeed, maxSpeed);
            Assert.That(result3.returnTimeSeconds, Is.EqualTo(0f), "Infinity radial velocity should return 0");

            var result4 = SonarPingReturnTimeCalculator.Compute(1500f, soundSpeed, freq, temp, radialVel, coeff, float.NaN, maxSpeed);
            Assert.That(result4.returnTimeSeconds, Is.EqualTo(0f), "NaN minSpeed should return 0");
        }
    }
}
