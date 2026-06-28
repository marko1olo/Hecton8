using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CeilingConcavityAirPocketVolumeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 normal = new Vector3(0.5f, -0.5f, 0.5f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, 5f, 2f, 3f);
            Assert.That(volume, Is.GreaterThan(0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 normal = new Vector3(0f, -1f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, 10f, 5f, 5f);
            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 normal = new Vector3(1f, 0f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, 0f, 0f, 0f);
            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 normal = new Vector3(1f, 0f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, -10f, -5f, -2f);
            Assert.That(volume, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 normal = new Vector3(1f, 0f, 0f);
            float volume = CeilingConcavityAirPocketVolumeCalculator.Compute(normal, float.PositiveInfinity, float.NaN, float.PositiveInfinity);
            Assert.That(volume, Is.EqualTo(0f));
        }
    }
}
