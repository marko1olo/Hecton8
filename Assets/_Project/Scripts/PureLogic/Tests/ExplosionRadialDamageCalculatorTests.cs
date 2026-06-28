using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ExplosionRadialDamageCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float damage = ExplosionRadialDamageCalculator.Compute(5f, 10f, 100f, 10f);
            Assert.That(damage, Is.EqualTo(100f * 0.25f + 10f * 0.75f));
            float damage2 = ExplosionRadialDamageCalculator.Compute(9.999f, 10f, 100f, 10f);
            Assert.That(damage2, Is.GreaterThan(0));
            Assert.That(damage2, Is.LessThan(11f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float edgeDamage = ExplosionRadialDamageCalculator.Compute(10f, 10f, 100f, 10f);
            Assert.That(edgeDamage, Is.EqualTo(10f));

            float outOfBoundsDamage = ExplosionRadialDamageCalculator.Compute(10.1f, 10f, 100f, 10f);
            Assert.That(outOfBoundsDamage, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float epicenterDamage = ExplosionRadialDamageCalculator.Compute(0f, 10f, 100f, 10f);
            Assert.That(epicenterDamage, Is.EqualTo(100f));

            float zeroRadiusDamage = ExplosionRadialDamageCalculator.Compute(5f, 0f, 100f, 10f);
            Assert.That(zeroRadiusDamage, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float negDistanceDamage = ExplosionRadialDamageCalculator.Compute(-5f, 10f, 100f, 10f);
            Assert.That(negDistanceDamage, Is.EqualTo(100f)); // clamped to 0

            float negRadiusDamage = ExplosionRadialDamageCalculator.Compute(5f, -10f, 100f, 10f);
            Assert.That(negRadiusDamage, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float infinityDamage = ExplosionRadialDamageCalculator.Compute(float.PositiveInfinity, 10f, 100f, 10f);
            Assert.That(infinityDamage, Is.EqualTo(0f));

            float nanDamage = ExplosionRadialDamageCalculator.Compute(float.NaN, 10f, 100f, 10f);
            Assert.That(nanDamage, Is.EqualTo(0f));

            float extremeDamage = ExplosionRadialDamageCalculator.Compute(1000f, 1000000f, 1e20f, 10f);
            Assert.That(extremeDamage, Is.GreaterThan(0));
        }
    }
}
