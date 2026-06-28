using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SubseaVehicleDopplerReverbShiftCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float initialFrequency = 1.0f;
            Vector3 emitterPos = new Vector3(10f, 0f, 0f); // Emitter is at X=10
            Vector3 listenerPos = new Vector3(0f, 0f, 0f); // Listener is at origin
            float speedOfSound = 1500f; // Typical speed of sound in water

            // Emitter moving towards listener (-X direction), listener stationary
            Vector3 emitterVelTowards = new Vector3(-10f, 0f, 0f);
            Vector3 listenerVel = Vector3.Zero;

            // Act: Emitter moving towards listener
            float resultTowards = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFrequency, emitterPos, emitterVelTowards, listenerPos, listenerVel, speedOfSound);

            // Emitter moving away from listener (+X direction)
            Vector3 emitterVelAway = new Vector3(10f, 0f, 0f);
            float resultAway = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFrequency, emitterPos, emitterVelAway, listenerPos, listenerVel, speedOfSound);

            // Assert: Verify expected output behaviour
            // Pitch increases when moving towards
            Assert.That(resultTowards, Is.GreaterThan(initialFrequency), "Pitch should increase when moving towards the listener.");
            // Pitch decreases when moving away
            Assert.That(resultAway, Is.LessThan(initialFrequency), "Pitch should decrease when moving away from the listener.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float initialFrequency = 100f;
            Vector3 emitterPos = new Vector3(10f, 0f, 0f);
            Vector3 listenerPos = new Vector3(0f, 0f, 0f);
            float speedOfSound = 1500f;

            // Extremly fast velocity towards the listener (will be clamped)
            Vector3 extremeVelTowards = new Vector3(-2000f, 0f, 0f);

            // Extremly fast velocity away from the listener (will be clamped)
            Vector3 extremeVelAway = new Vector3(2000f, 0f, 0f);

            // Act
            float maxPitch = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFrequency, emitterPos, extremeVelTowards, listenerPos, Vector3.Zero, speedOfSound);
            float minPitch = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFrequency, emitterPos, extremeVelAway, listenerPos, Vector3.Zero, speedOfSound);

            // Assert
            // The max clamp should be 1.2f ratio
            Assert.That(maxPitch, Is.EqualTo(initialFrequency * 1.2f).Within(0.001f), "Max pitch ratio should be clamped to 1.2");
            // The min clamp should be 0.8333333f ratio
            Assert.That(minPitch, Is.EqualTo(initialFrequency * 0.8333333f).Within(0.001f), "Min pitch ratio should be clamped to 0.8333333");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float initialFrequency = 50f;
            Vector3 zeroPos = Vector3.Zero;
            Vector3 zeroVel = Vector3.Zero;
            float zeroSpeedOfSound = 0f;

            // Act
            float resultSamePos = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFrequency, zeroPos, zeroVel, zeroPos, zeroVel, 1500f);
            float resultZeroSoundSpeed = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFrequency, new Vector3(1,0,0), zeroVel, zeroPos, zeroVel, zeroSpeedOfSound);

            // Assert
            // When distance is zero, it should return initial frequency
            Assert.That(resultSamePos, Is.EqualTo(initialFrequency), "Zero distance should return initial frequency without shift.");
            // When speed of sound is zero, it should return initial frequency
            Assert.That(resultZeroSoundSpeed, Is.EqualTo(initialFrequency), "Zero speed of sound should return initial frequency.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float initialFreq = -100f; // Invalid negative frequency
            Vector3 emitterPos = new Vector3(10f, -10f, 5f);
            Vector3 listenerPos = new Vector3(0f, 0f, 0f);
            Vector3 listenerVel = new Vector3(-5f, -5f, -5f);
            Vector3 emitterVel = new Vector3(5f, 5f, 5f);
            float speedOfSound = -1500f; // Invalid negative speed of sound

            // Act
            float resultNegativeFreq = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFreq, emitterPos, emitterVel, listenerPos, listenerVel, 1500f);
            float resultNegativeSoundSpeed = SubseaVehicleDopplerReverbShiftCalculator.Compute(100f, emitterPos, emitterVel, listenerPos, listenerVel, speedOfSound);

            // Assert
            Assert.That(resultNegativeFreq, Is.EqualTo(0f), "Negative initial frequency should result in 0 output.");
            Assert.That(resultNegativeSoundSpeed, Is.EqualTo(100f), "Negative speed of sound should result in returning the initial frequency unchanged.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float initialFreq = 100f;
            Vector3 emitterPos = new Vector3(float.MaxValue, 0f, 0f); // Very large position
            Vector3 listenerPos = Vector3.Zero;
            Vector3 emitterVel = new Vector3(float.NaN, 0f, 0f); // NaN input
            Vector3 listenerVel = Vector3.Zero;
            float speedOfSound = 1500f;

            // Act
            float resultNaNVel = SubseaVehicleDopplerReverbShiftCalculator.Compute(initialFreq, emitterPos, emitterVel, listenerPos, listenerVel, speedOfSound);

            float resultInfFreq = SubseaVehicleDopplerReverbShiftCalculator.Compute(float.PositiveInfinity, new Vector3(10,0,0), Vector3.Zero, listenerPos, listenerVel, speedOfSound);

            // Assert
            Assert.That(resultNaNVel, Is.EqualTo(initialFreq), "NaN velocity should gracefully fallback to initial frequency.");
            Assert.That(resultInfFreq, Is.EqualTo(0f), "Infinity frequency should be clamped to 0.");
        }
    }
}
