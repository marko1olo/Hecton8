using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class WeldHeatDissipationCalculatorTests
    {
        [Test]
        public void Compute_ValidInputs_CalculatesDissipationCorrectly()
        {
            // Arrange
            float weldTemp = 1000f;
            float waterTemp = 20f;
            float area = 0.01f;
            float htc = 500f;
            float dt = 1f;
            float mass = 1f;
            float shc = 500f;

            // Act
            var (heatDissipated, newWeldTemp) = WeldHeatDissipationCalculator.Compute(
                weldTemp, waterTemp, area, htc, dt, mass, shc);

            // Assert
            // heatCapacity = mass * shc = 500
            // k = (htc * area) / heatCapacity = (500 * 0.01) / 500 = 0.01
            // tempDifference = 1000 - 20 = 980
            // expFactor = exp(-0.01) ~= 0.9900498
            // expectedNewTemp = 20 + 980 * 0.9900498 = 990.2488
            // expectedDissipated = 500 * (1000 - 990.2488) = 4875.583
            Assert.That(newWeldTemp, Is.EqualTo(990.2488f).Within(0.001f));
            Assert.That(heatDissipated, Is.EqualTo(4875.583f).Within(0.01f));
        }

        [Test]
        public void Compute_NaNOrInfinityInputs_ReturnsZeroAndSafeTemp()
        {
            // If weldTemp is NaN, safeTemp should be 0
            var (heat1, temp1) = WeldHeatDissipationCalculator.Compute(
                float.NaN, 20f, 0.01f, 500f, 1f, 1f, 500f);
            Assert.That(heat1, Is.EqualTo(0f));
            Assert.That(temp1, Is.EqualTo(0f));

            // If another input is NaN, safeTemp should be weldTemp
            var (heat2, temp2) = WeldHeatDissipationCalculator.Compute(
                1000f, float.PositiveInfinity, 0.01f, 500f, 1f, 1f, 500f);
            Assert.That(heat2, Is.EqualTo(0f));
            Assert.That(temp2, Is.EqualTo(1000f));
        }

        [Test]
        public void Compute_InvalidMassOrSpecificHeatCapacity_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WeldHeatDissipationCalculator.Compute(1000f, 20f, 0.01f, 500f, 1f, 0f, 500f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WeldHeatDissipationCalculator.Compute(1000f, 20f, 0.01f, 500f, 1f, -1f, 500f));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WeldHeatDissipationCalculator.Compute(1000f, 20f, 0.01f, 500f, 1f, 1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WeldHeatDissipationCalculator.Compute(1000f, 20f, 0.01f, 500f, 1f, 1f, -500f));
        }

        [Test]
        public void Compute_ZeroTempDiffOrAreaOrTime_NoDissipation()
        {
            // Temp Diff = 0
            var (heat1, temp1) = WeldHeatDissipationCalculator.Compute(
                20f, 20f, 0.01f, 500f, 1f, 1f, 500f);
            Assert.That(heat1, Is.EqualTo(0f));
            Assert.That(temp1, Is.EqualTo(20f));

            // Area = 0
            var (heat2, temp2) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, 0f, 500f, 1f, 1f, 500f);
            Assert.That(heat2, Is.EqualTo(0f));
            Assert.That(temp2, Is.EqualTo(1000f));

            // Time = 0
            var (heat3, temp3) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, 0.01f, 500f, 0f, 1f, 500f);
            Assert.That(heat3, Is.EqualTo(0f));
            Assert.That(temp3, Is.EqualTo(1000f));

            // Heat Transfer Coeff = 0
            var (heat4, temp4) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, 0.01f, 0f, 1f, 1f, 500f);
            Assert.That(heat4, Is.EqualTo(0f));
            Assert.That(temp4, Is.EqualTo(1000f));
        }

        [Test]
        public void Compute_NegativeDeltaTimeOrAreaOrCoeff_ClampsToZero()
        {
            // Negative Delta Time
            var (heat1, temp1) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, 0.01f, 500f, -1f, 1f, 500f);
            Assert.That(heat1, Is.EqualTo(0f));
            Assert.That(temp1, Is.EqualTo(1000f));

            // Negative Area
            var (heat2, temp2) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, -0.01f, 500f, 1f, 1f, 500f);
            Assert.That(heat2, Is.EqualTo(0f));
            Assert.That(temp2, Is.EqualTo(1000f));

            // Negative Heat Transfer Coeff
            var (heat3, temp3) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, 0.01f, -500f, 1f, 1f, 500f);
            Assert.That(heat3, Is.EqualTo(0f));
            Assert.That(temp3, Is.EqualTo(1000f));
        }

        [Test]
        public void Compute_PrecisionOvershoot_ClampsToWaterTemp()
        {
            // When weldTemp > waterTemp, newTemp shouldn't drop below waterTemp
            var (heat1, temp1) = WeldHeatDissipationCalculator.Compute(
                1000f, 20f, 0.01f, 500f, 10000000000000f, 1f, 500f);
            Assert.That(temp1, Is.EqualTo(20f));
            Assert.That(heat1, Is.EqualTo(490000f).Within(0.01f)); // 500 * (1000 - 20)

            // When weldTemp < waterTemp, newTemp shouldn't rise above waterTemp
            var (heat2, temp2) = WeldHeatDissipationCalculator.Compute(
                0f, 20f, 0.01f, 500f, 10000000000000f, 1f, 500f);
            Assert.That(temp2, Is.EqualTo(20f));
            Assert.That(heat2, Is.EqualTo(-10000f).Within(0.01f)); // 500 * (0 - 20)
        }
    }
}
