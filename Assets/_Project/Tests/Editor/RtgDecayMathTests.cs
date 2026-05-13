using Hecton8.Power.Generators;
using NUnit.Framework;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class RtgDecayMathTests
    {
        [Test]
        public void PadeDecay_ReturnsOneAtZero()
        {
            Assert.That(RtgDecayMath.ResolvePadeExpNegative(0f), Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void PadeDecay_StaysFiniteForLargeInput()
        {
            float value = RtgDecayMath.ResolvePadeExpNegative(1000000f);
            Assert.That(math.isfinite(value), Is.True);
            Assert.That(value, Is.InRange(0f, 1f));
        }

        [Test]
        public void PadeDecay_NegativeInputDoesNotDivideByZero()
        {
            float value = RtgDecayMath.ResolvePadeExpNegative(-250f);
            Assert.That(math.isfinite(value), Is.True);
            Assert.That(value, Is.EqualTo(1f).Within(0.000001f));
        }

        [Test]
        public void PadeDecay_TracksHalfLifeCheckpoint()
        {
            float value = RtgDecayMath.ResolvePadeExpNegative(0.6931471805599453f);
            Assert.That(value, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void DecayFactor_HalvesAtConfiguredHalfLife()
        {
            float value = RtgDecayMath.ResolveDecayFactor(100f, 40f, 60f);
            Assert.That(value, Is.EqualTo(0.5f).Within(0.01f));
        }
    }
}
