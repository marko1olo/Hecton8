using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WaterPressureWeaponMultiplierTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Standard decay: 50% at 100m. e^(-0.0069314718 * 100 * (1000/1000)) = 0.5
            var result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 1000f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(50f).Within(0.001f));
            Assert.That(result.adjustedRange, Is.EqualTo(100f).Within(0.001f));

            // Double density -> 25% at 100m. e^(-0.0069314718 * 100 * (2000/1000)) = 0.25
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 2000f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(25f).Within(0.001f));
            Assert.That(result.adjustedRange, Is.EqualTo(50f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Negative depth clamped to 0. e^0 = 1.
            var result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, -50f, 1000f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f).Within(0.001f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f).Within(0.001f));

            // Negative density clamped to 0. e^0 = 1.
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, -50f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f).Within(0.001f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f).Within(0.001f));

            // Minimum exponent. e^(-1)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 1000000f, 1000f, 1000f, 0.0069314718f, -1f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f * (float)Math.Exp(-1f)).Within(0.001f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f * (float)Math.Exp(-1f)).Within(0.001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case03()
        {
            // NaN base velocity -> returns (0,0)
            var result = WaterPressureWeaponMultiplier.Calculate(float.NaN, 200f, 100f, 1000f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(0f));
            Assert.That(result.adjustedRange, Is.EqualTo(0f));

            // Infinity base range -> returns (baseVelocity, 0)
            result = WaterPressureWeaponMultiplier.Calculate(100f, float.PositiveInfinity, 100f, 1000f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(0f));

            // Zero reference density -> returns (baseVelocity, baseRange)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 1000f, 0f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f));

            // NaN depthMeters -> returns (baseVelocity, baseRange)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, float.NaN, 1000f, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f));

            // NaN waterDensity -> returns (baseVelocity, baseRange)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, float.NaN, 1000f, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f));

            // NaN referenceDensity -> returns (baseVelocity, baseRange)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 1000f, float.NaN, 0.0069314718f, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f));

            // NaN decayConstant -> returns (baseVelocity, baseRange)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 1000f, 1000f, float.NaN, -80f);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f));

            // NaN minExponent -> returns (baseVelocity, baseRange)
            result = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 1000f, 1000f, 0.0069314718f, float.NaN);
            Assert.That(result.adjustedVelocity, Is.EqualTo(100f));
            Assert.That(result.adjustedRange, Is.EqualTo(200f));
        }
    }
}