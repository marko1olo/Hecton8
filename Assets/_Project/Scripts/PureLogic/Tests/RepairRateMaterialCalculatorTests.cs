using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class RepairRateMaterialCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float rate = RepairRateMaterialCalculator.Compute(1f, 1f, 100f, 1f);
            Assert.That(rate, Is.EqualTo(100f));

            float softRate = RepairRateMaterialCalculator.Compute(1f, 0.5f, 100f, 1f);
            Assert.That(softRate, Is.EqualTo(200f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float maxChargeRate = RepairRateMaterialCalculator.Compute(2f, 1f, 100f, 1f);
            Assert.That(maxChargeRate, Is.EqualTo(100f));

            float minChargeRate = RepairRateMaterialCalculator.Compute(-1f, 1f, 100f, 1f);
            Assert.That(minChargeRate, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float rateNoCharge = RepairRateMaterialCalculator.Compute(0f, 1f, 100f, 1f);
            Assert.That(rateNoCharge, Is.EqualTo(0f));

            float rateZeroHardness = RepairRateMaterialCalculator.Compute(1f, 0f, 100f, 1f);
            Assert.That(rateZeroHardness, Is.EqualTo(0f));

            float rateZeroDepth = RepairRateMaterialCalculator.Compute(1f, 1f, 100f, 0f);
            Assert.That(rateZeroDepth, Is.EqualTo(0f));

            float rateZeroBase = RepairRateMaterialCalculator.Compute(1f, 1f, 0f, 1f);
            Assert.That(rateZeroBase, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float rateNegativeHardness = RepairRateMaterialCalculator.Compute(1f, -1f, 100f, 1f);
            Assert.That(rateNegativeHardness, Is.EqualTo(0f));

            float rateNegativeDepth = RepairRateMaterialCalculator.Compute(1f, 1f, 100f, -1f);
            Assert.That(rateNegativeDepth, Is.EqualTo(0f));

            float rateNegativeBase = RepairRateMaterialCalculator.Compute(1f, 1f, -100f, 1f);
            Assert.That(rateNegativeBase, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float rateNan = RepairRateMaterialCalculator.Compute(float.NaN, 1f, 100f, 1f);
            Assert.That(rateNan, Is.EqualTo(0f));

            float rateInf = RepairRateMaterialCalculator.Compute(1f, float.PositiveInfinity, 100f, 1f);
            Assert.That(rateInf, Is.EqualTo(0f));

            // 100 / (1e20 * 1e20) is a float denormal (~1e-38) when the backend computes the
            // divisor in double, and an exact 0 when the float product overflows to +Inf and
            // hits the guard. Both mean "no meaningful repair"; assert negligible, not bit-exact.
            float extremeHardness = RepairRateMaterialCalculator.Compute(1f, 1e20f, 100f, 1e20f);
            Assert.That(extremeHardness, Is.EqualTo(0f).Within(1e-9f));
        }
    }
}
