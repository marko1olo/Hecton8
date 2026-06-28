using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WaterPressureWeaponMultiplierTests
    {
        private const float RefDensity = 1000f;
        private const float DecayConst = 0.0069314718f;
        private const float MinExp = -80f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            var resultSurface = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 0f, RefDensity, RefDensity, DecayConst, MinExp);
            Assert.That(resultSurface.adjustedVelocity, Is.EqualTo(100f).Within(0.001f));
            Assert.That(resultSurface.adjustedRange, Is.EqualTo(200f).Within(0.001f));

            var result100m = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, RefDensity, RefDensity, DecayConst, MinExp);
            Assert.That(result100m.adjustedVelocity, Is.EqualTo(50f).Within(0.1f));
            Assert.That(result100m.adjustedRange, Is.EqualTo(100f).Within(0.1f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            var resultClamp = WaterPressureWeaponMultiplier.Calculate(100f, 200f, -50f, RefDensity, RefDensity, DecayConst, MinExp);
            Assert.That(resultClamp.adjustedVelocity, Is.EqualTo(100f).Within(0.001f));
            Assert.That(resultClamp.adjustedRange, Is.EqualTo(200f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            var resultZeroVelocity = WaterPressureWeaponMultiplier.Calculate(0f, 0f, 100f, RefDensity, RefDensity, DecayConst, MinExp);
            Assert.That(resultZeroVelocity.adjustedVelocity, Is.EqualTo(0f).Within(0.001f));
            Assert.That(resultZeroVelocity.adjustedRange, Is.EqualTo(0f).Within(0.001f));

            var resultZeroDensity = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, 0f, RefDensity, DecayConst, MinExp);
            Assert.That(resultZeroDensity.adjustedVelocity, Is.EqualTo(100f).Within(0.1f));
            Assert.That(resultZeroDensity.adjustedRange, Is.EqualTo(200f).Within(0.1f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            var resultNegDensity = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 100f, -1f, RefDensity, DecayConst, MinExp);
            Assert.That(resultNegDensity.adjustedVelocity, Is.EqualTo(100f).Within(0.1f));
            Assert.That(resultNegDensity.adjustedRange, Is.EqualTo(200f).Within(0.1f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            var resultExtremeDepth = WaterPressureWeaponMultiplier.Calculate(100f, 200f, 1000000f, RefDensity, RefDensity, DecayConst, MinExp);
            Assert.That(resultExtremeDepth.adjustedVelocity, Is.EqualTo(0f).Within(0.001f));
            Assert.That(resultExtremeDepth.adjustedRange, Is.EqualTo(0f).Within(0.001f));

            var resultNaN = WaterPressureWeaponMultiplier.Calculate(float.NaN, 200f, 100f, RefDensity, RefDensity, DecayConst, MinExp);
            Assert.That(resultNaN.adjustedVelocity, Is.EqualTo(0f).Within(0.001f));
            Assert.That(resultNaN.adjustedRange, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
