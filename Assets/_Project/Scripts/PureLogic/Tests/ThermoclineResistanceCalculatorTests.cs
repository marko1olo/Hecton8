using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ThermoclineResistanceCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = ThermoclineResistanceCalculator.Compute(100f, 100f, 20f, 10f, 1f);
            Assert.That(result, Is.GreaterThan(0f));
            Assert.That(result, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float resultEdge1 = ThermoclineResistanceCalculator.Compute(90f, 100f, 20f, 10f, 1f);
            Assert.That(resultEdge1, Is.EqualTo(0f));

            float resultEdge2 = ThermoclineResistanceCalculator.Compute(110f, 100f, 20f, 10f, 1f);
            Assert.That(resultEdge2, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = ThermoclineResistanceCalculator.Compute(0f, 0f, 0f, 0f, 0f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = ThermoclineResistanceCalculator.Compute(-100f, -100f, 20f, -10f, 1f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = ThermoclineResistanceCalculator.Compute(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
            Assert.That(float.IsNaN(result), Is.False);
            Assert.That(float.IsInfinity(result), Is.False);

            float resultNaN = ThermoclineResistanceCalculator.Compute(float.NaN, 100f, 20f, 10f, 1f);
            Assert.That(resultNaN, Is.EqualTo(0f));
        }

        // ── HectonPlayerMovement's SHIPPED band, not generic math ──────────────────────────────────────
        // The cases above prove the calculator. These prove the constants the player actually swims through
        // (thermoclineDepthMeters 120, thermoclineThicknessMeters 16, thermoclineResistanceForce 0.08), which
        // is the part a tuning edit can silently break. Consumed as a drag multiplier of 1 + result by
        // HectonPlayerMovement.ResolveThermoclineDragMultiplier.
        private const float ShippedBandDepth = 120f;
        private const float ShippedBandThickness = 16f;
        private const float ShippedResistanceForce = 0.08f;

        private static float ShippedBand(float depth, float speed)
        {
            return ThermoclineResistanceCalculator.Compute(
                depth, ShippedBandDepth, ShippedBandThickness, speed, ShippedResistanceForce);
        }

        [Test]
        public void ShippedBand_IsIdentityOutsideTheBand()
        {
            // A nonzero result at any of these depths would apply a permanent drag penalty to the whole dive.
            Assert.That(ShippedBand(0f, 4f), Is.EqualTo(0f));
            Assert.That(ShippedBand(111f, 4f), Is.EqualTo(0f));
            Assert.That(ShippedBand(112f, 4f), Is.EqualTo(0f));
            Assert.That(ShippedBand(128f, 4f), Is.EqualTo(0f));
            Assert.That(ShippedBand(4000f, 4f), Is.EqualTo(0f));
        }

        [Test]
        public void ShippedBand_StationaryPlayerIsNotGlued()
        {
            // Resistance scales with crossing speed, so the band resists transit and never traps a player
            // who has stopped inside it.
            Assert.That(ShippedBand(ShippedBandDepth, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void ShippedForce_KeepsNormalSwimSpeedsBelowTheClamp()
        {
            // WHY THE DEFAULT IS 0.08 AND NOT THE KCC HOST'S 0.35: force 0.35 saturates the clamp at
            // 1/0.35 = 2.86 m/s, below normal swim speed, so every crossing would produce identical doubled
            // drag - a binary wall whose strength no longer varies with how fast the player crosses it.
            Assert.That(ShippedBand(ShippedBandDepth, 4f), Is.LessThan(1f));
            Assert.That(ShippedBand(ShippedBandDepth, 10f), Is.LessThan(1f));
            Assert.That(
                ShippedBand(ShippedBandDepth, 10f),
                Is.GreaterThan(ShippedBand(ShippedBandDepth, 4f)));

            // The rejected value, pinned so a future tuning pass cannot reintroduce it unnoticed.
            Assert.That(
                ThermoclineResistanceCalculator.Compute(
                    ShippedBandDepth, ShippedBandDepth, ShippedBandThickness, 4f, 0.35f),
                Is.EqualTo(1f));
        }

        [Test]
        public void ShippedBand_FallsOffSymmetricallyFromTheCentre()
        {
            float centre = ShippedBand(ShippedBandDepth, 4f);
            Assert.That(centre, Is.GreaterThan(0f));
            Assert.That(ShippedBand(114f, 4f), Is.EqualTo(ShippedBand(126f, 4f)).Within(0.0001f));
            Assert.That(centre, Is.GreaterThan(ShippedBand(126f, 4f)));
        }

        [Test]
        public void Test_ClampBounds_Case06()
        {
            // Test max bound
            float resultMax = ThermoclineResistanceCalculator.Compute(100f, 100f, 20f, 1000f, 1000f);
            Assert.That(resultMax, Is.EqualTo(1f));

            // Test min bound
            float resultMin = ThermoclineResistanceCalculator.Compute(100f, 100f, 20f, -10f, 1f);
            Assert.That(resultMin, Is.EqualTo(0f));
        }
    }
}
