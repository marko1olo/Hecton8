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

        [Test]
        public void Test_ExactBounds_Case06()
        {
            // Exact minimum bound (0.12f)
            Assert.That(AcousticZoneReverbDecay.Calculate(12f, 16.1f, 1f), Is.EqualTo(0.12f).Within(0.001f));

            // Exact maximum bound (10.0f)
            Assert.That(AcousticZoneReverbDecay.Calculate(1000f, 16.1f, 1f), Is.EqualTo(10.0f).Within(0.001f));

            // Just below minimum bound (0.11f) -> clamps to 0.12f
            Assert.That(AcousticZoneReverbDecay.Calculate(11f, 16.1f, 1f), Is.EqualTo(0.12f).Within(0.001f));

            // Just above maximum bound (10.1f) -> clamps to 10.0f
            Assert.That(AcousticZoneReverbDecay.Calculate(1010f, 16.1f, 1f), Is.EqualTo(10.0f).Within(0.001f));

            // Inside bounds (5.0f)
            Assert.That(AcousticZoneReverbDecay.Calculate(500f, 16.1f, 1f), Is.EqualTo(5.0f).Within(0.001f));
        }
    }
}
