using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ModuleThermalDissipationRateTests
    {
        private const float AirDensity = 1.2f;
        private const float AirSpecificHeat = 1005f;
        private const float MinRoomVolume = 0.001f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            float currentTemp = 20f;
            float wattage = 5000f; // 5kW
            float coolant = 1000f; // 1kW cooling
            float volume = 10f;    // 10 m^3
            float dt = 1f;         // 1s

            float delta = ModuleThermalDissipationRate.Calculate(currentTemp, wattage, coolant, volume, dt, AirDensity, AirSpecificHeat, MinRoomVolume);

            Assert.That(delta, Is.GreaterThan(0.33f));
            Assert.That(delta, Is.LessThan(0.34f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float currentTemp = 20f;
            float wattage = 5000f;
            float coolant = 5000f; // Perfect cooling
            float volume = 10f;
            float dt = 1f;

            float delta = ModuleThermalDissipationRate.Calculate(currentTemp, wattage, coolant, volume, dt, AirDensity, AirSpecificHeat, MinRoomVolume);

            Assert.That(delta, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float delta = ModuleThermalDissipationRate.Calculate(0f, 0f, 0f, 0f, 0f, AirDensity, AirSpecificHeat, MinRoomVolume);
            Assert.That(delta, Is.EqualTo(0f));

            float delta2 = ModuleThermalDissipationRate.Calculate(0f, 100f, 0f, 0f, 1f, AirDensity, AirSpecificHeat, MinRoomVolume);
            Assert.That(delta2, Is.GreaterThan(0f));
            Assert.That(float.IsFinite(delta2), Is.True);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float delta = ModuleThermalDissipationRate.Calculate(-100f, -500f, -200f, -5f, -1f, AirDensity, AirSpecificHeat, MinRoomVolume);
            Assert.That(delta, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float delta = ModuleThermalDissipationRate.Calculate(float.MaxValue, float.MaxValue, float.MaxValue / 2f, 100f, 1f, AirDensity, AirSpecificHeat, MinRoomVolume);
            Assert.That(float.IsNaN(delta), Is.False);

            float deltaNaN = ModuleThermalDissipationRate.Calculate(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.That(deltaNaN, Is.EqualTo(0f));
        }
    }
}
