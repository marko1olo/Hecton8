using Hecton8.PureLogic.Kinematics;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Proves the thermocline resistance term is a real, consumed calculation rather than a dead variable.
    ///
    /// HectonPlayerMovement.ResolveThermoclineDragMultiplier converts this calculator's 0..1 resistance into
    /// a drag MULTIPLIER of 1 + resistance, then multiplies it into the swim drag coefficient. The band
    /// therefore has to be zero-valued outside itself, or every dive at every depth would be slowed. These
    /// assertions pin exactly that: outside the band the multiplier must be identity.
    ///
    /// NOTE ON EXECUTION: Hecton8.EditModeTests.asmdef carries defineConstraints NEVER_COMPILE_TESTS, so
    /// this file is not compiled by Unity today. That is pre-existing standing debt across the whole test
    /// tree, not a property of this test. The numbers below were verified independently against the compiled
    /// Hecton8.PureLogic assembly.
    /// </summary>
    public sealed class ThermoclineResistanceEditTests
    {
        private const float BandDepth = 120f;
        private const float BandThickness = 16f;
        // Matches HectonPlayerMovement's shipped default. Deliberately NOT the KCC host's 0.35, which
        // saturates the calculator's clamp at 2.86 m/s and turns the band into a binary wall.
        private const float ResistanceForce = 0.08f;
        private const float CrossingSpeed = 4f;

        private static float Resistance(float depth, float speed)
        {
            return ThermoclineResistanceCalculator.Compute(
                depth, BandDepth, BandThickness, speed, ResistanceForce);
        }

        [Test]
        public void OutsideBand_ContributesNothing()
        {
            // Half-band is 8 m, so 111 m and 129 m are outside. A nonzero result here would apply a
            // permanent drag penalty at all depths - the exact "invisible wall" failure this gating avoids.
            Assert.That(Resistance(111f, CrossingSpeed), Is.EqualTo(0f));
            Assert.That(Resistance(129f, CrossingSpeed), Is.EqualTo(0f));
            Assert.That(Resistance(0f, CrossingSpeed), Is.EqualTo(0f));
            Assert.That(Resistance(4000f, CrossingSpeed), Is.EqualTo(0f));
        }

        [Test]
        public void BandEdge_IsExactlyZero_AndCentreIsMaximal()
        {
            // distanceToThermocline >= halfThickness returns 0, so the edge is closed at zero and the
            // falloff is continuous into it - no step discontinuity as the player crosses in.
            Assert.That(Resistance(BandDepth - 8f, CrossingSpeed), Is.EqualTo(0f));
            Assert.That(Resistance(BandDepth + 8f, CrossingSpeed), Is.EqualTo(0f));

            float centre = Resistance(BandDepth, CrossingSpeed);
            Assert.That(centre, Is.GreaterThan(Resistance(BandDepth + 6f, CrossingSpeed)));
            Assert.That(centre, Is.GreaterThan(0f));
        }

        [Test]
        public void StationaryPlayer_IsNotGlued()
        {
            // The speed term means the band resists TRANSIT. A stationary player at the exact centre must
            // get no resistance, or the layer becomes glue that traps the player at 120 m.
            Assert.That(Resistance(BandDepth, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void Result_IsAlwaysAUsableMultiplier()
        {
            // Consumed as 1 + resistance, so it must stay in 0..1 for the drag multiplier to stay in 1..2.
            // An unclamped value here would multiply swim drag without bound.
            foreach (float speed in new[] { 0.5f, 4f, 40f, 4000f })
            {
                float resistance = Resistance(BandDepth, speed);
                Assert.That(resistance, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void ShippedForce_KeepsNormalSwimSpeedsBelowTheClamp()
        {
            // THE REASON THE DEFAULT IS 0.08 AND NOT THE SOURCE SYSTEM'S 0.35. Once the clamp is reached the
            // resistance stops varying with speed, so the band would apply identical drag to a drift and a
            // sprint. These two assertions are what make the term continuous over the playable range.
            Assert.That(Resistance(BandDepth, 4f), Is.LessThan(1f));
            Assert.That(Resistance(BandDepth, 10f), Is.LessThan(1f));
            Assert.That(Resistance(BandDepth, 10f), Is.GreaterThan(Resistance(BandDepth, 4f)));
        }

        [Test]
        public void NonFiniteAndDegenerateInputs_FailSafeToZero()
        {
            Assert.That(Resistance(float.NaN, CrossingSpeed), Is.EqualTo(0f));
            Assert.That(Resistance(float.PositiveInfinity, CrossingSpeed), Is.EqualTo(0f));
            Assert.That(Resistance(BandDepth, float.NaN), Is.EqualTo(0f));
            Assert.That(
                ThermoclineResistanceCalculator.Compute(BandDepth, BandDepth, 0f, CrossingSpeed, ResistanceForce),
                Is.EqualTo(0f));
            Assert.That(
                ThermoclineResistanceCalculator.Compute(BandDepth, BandDepth, BandThickness, CrossingSpeed, 0f),
                Is.EqualTo(0f));
        }
    }
}
