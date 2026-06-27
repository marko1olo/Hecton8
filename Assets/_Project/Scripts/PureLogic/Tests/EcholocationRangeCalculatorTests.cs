using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class EcholocationRangeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            // High pressure, silent: max range. High ambient noise: reduced. Low reflectivity: shorter.
            float emittedSoundPressure = 100f;
            float ambientNoiseLevel = 10f;
            float targetReflectivity = 0.5f;
            float soundAttenuationPerMeter = 2f;

            // Expected: (100 * 0.5 - 10) / 2 = (50 - 10) / 2 = 20

            // Act
            float result = EcholocationRangeCalculator.Compute(emittedSoundPressure, ambientNoiseLevel, targetReflectivity, soundAttenuationPerMeter);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(20f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float emittedSoundPressure = 10f;
            float ambientNoiseLevel = 20f; // noise > signal
            float targetReflectivity = 1f;
            float soundAttenuationPerMeter = 1f;

            // Expected: signal = 10. noise = 20. 10 - 20 = -10. result should clamp to 0.

            // Act
            float result = EcholocationRangeCalculator.Compute(emittedSoundPressure, ambientNoiseLevel, targetReflectivity, soundAttenuationPerMeter);

            // Assert
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float emittedSoundPressure = 0f;
            float ambientNoiseLevel = 0f;
            float targetReflectivity = 0f;
            float soundAttenuationPerMeter = 0f;

            // Act
            float result = EcholocationRangeCalculator.Compute(emittedSoundPressure, ambientNoiseLevel, targetReflectivity, soundAttenuationPerMeter);

            // Assert
            // No division by zero. Should handle 0 attenuation and 0 pressure.
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float emittedSoundPressure = -100f; // clamped to 0
            float ambientNoiseLevel = -50f; // clamped to 0
            float targetReflectivity = -0.5f; // clamped to 0
            float soundAttenuationPerMeter = -5f; // clamped to 0.0001f

            // Act
            float result = EcholocationRangeCalculator.Compute(emittedSoundPressure, ambientNoiseLevel, targetReflectivity, soundAttenuationPerMeter);

            // Assert
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float emittedSoundPressure = float.MaxValue;
            float ambientNoiseLevel = 0f;
            float targetReflectivity = 1f;
            float soundAttenuationPerMeter = 1f;

            // Act
            float result1 = EcholocationRangeCalculator.Compute(emittedSoundPressure, ambientNoiseLevel, targetReflectivity, soundAttenuationPerMeter);

            float result2 = EcholocationRangeCalculator.Compute(float.PositiveInfinity, 0f, 1f, 1f);
            float result3 = EcholocationRangeCalculator.Compute(100f, 0f, float.NaN, 1f);

            // Assert
            Assert.IsTrue(result1 > 0f); // MaxValue shouldn't overflow to negative or NaN
            Assert.AreEqual(0f, result2); // Infinity should be caught and clamped/ignored
            Assert.AreEqual(0f, result3); // NaN should be caught and clamped/ignored
        }
    }
}
