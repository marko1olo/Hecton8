using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AcousticZoneReverbDecayTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = AcousticZoneReverbDecay.Calculate(100f, 50f, 0.2f);
            Assert.That(result, Is.EqualTo(1.61f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = AcousticZoneReverbDecay.Calculate(1000f, 10f, 0.05f);
            Assert.That(result, Is.EqualTo(10f));

            float minResult = AcousticZoneReverbDecay.Calculate(1f, 100f, 1f);
            Assert.That(minResult, Is.EqualTo(0.12f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Assert.That(AcousticZoneReverbDecay.Calculate(100f, 0f, 0.2f), Is.EqualTo(10f));
            Assert.That(AcousticZoneReverbDecay.Calculate(100f, 50f, 0f), Is.EqualTo(10f));
            Assert.That(AcousticZoneReverbDecay.Calculate(0f, 50f, 0.2f), Is.EqualTo(0.12f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Assert.That(AcousticZoneReverbDecay.Calculate(100f, -10f, 0.2f), Is.EqualTo(10f));
            Assert.That(AcousticZoneReverbDecay.Calculate(100f, 50f, -0.2f), Is.EqualTo(10f));
            Assert.That(AcousticZoneReverbDecay.Calculate(-100f, 50f, 0.2f), Is.EqualTo(0.12f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Assert.That(AcousticZoneReverbDecay.Calculate(float.MaxValue, 1f, 1f), Is.EqualTo(10f));
            Assert.That(AcousticZoneReverbDecay.Calculate(100f, float.PositiveInfinity, 1f), Is.EqualTo(0.12f));
            Assert.That(AcousticZoneReverbDecay.Calculate(float.NaN, 1f, 1f), Is.EqualTo(10f));
        }
    }
}
