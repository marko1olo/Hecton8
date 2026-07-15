using NUnit.Framework;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PlayerEffortLoadCalculatorTests
    {
        [Test]
        public void ComputeLoadRatio_ReportsOverCapacityWithoutClamping()
        {
            float ratio = PlayerEffortLoadCalculator.ComputeLoadRatio(300f, 200f);

            Assert.That(ratio, Is.EqualTo(1.5f).Within(0.0001f));
        }

        [Test]
        public void ComputeLoad01_SaturatesOverCapacityForPresentation()
        {
            float load01 = PlayerEffortLoadCalculator.ComputeLoad01(300f, 200f);

            Assert.That(load01, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ComputeMovementMultiplier_UsesCanonicalWeightPenaltyFloor()
        {
            float halfLoad = PlayerEffortLoadCalculator.ComputeMovementMultiplier(100f, 200f, 0.5f);
            float fullLoad = PlayerEffortLoadCalculator.ComputeMovementMultiplier(200f, 200f, 0.5f);
            float overLoad = PlayerEffortLoadCalculator.ComputeMovementMultiplier(300f, 200f, 0.5f);

            Assert.That(halfLoad, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(fullLoad, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(overLoad, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void ComputeUpwardSwimMultiplier_LerpsToConfiguredFloor()
        {
            Assert.That(PlayerEffortLoadCalculator.ComputeUpwardSwimMultiplier(0f, 0.6f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(PlayerEffortLoadCalculator.ComputeUpwardSwimMultiplier(0.5f, 0.6f), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(PlayerEffortLoadCalculator.ComputeUpwardSwimMultiplier(1f, 0.6f), Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void ShouldTriggerCriticalStaminaFailure_RequiresCriticalLoadAndLowStamina()
        {
            Assert.IsFalse(PlayerEffortLoadCalculator.ShouldTriggerCriticalStaminaFailure(1.49f, 0.05f, 1.5f, 0.1f));
            Assert.IsFalse(PlayerEffortLoadCalculator.ShouldTriggerCriticalStaminaFailure(1.5f, 0.1f, 1.5f, 0.1f));
            Assert.IsTrue(PlayerEffortLoadCalculator.ShouldTriggerCriticalStaminaFailure(1.5f, 0.099f, 1.5f, 0.1f));
        }

        [Test]
        public void ComputeMetabolicMultiplier_IncreasesUnderLoadSprintAndUpwardSwimPenalty()
        {
            float baselineEnergy = PlayerEffortLoadCalculator.ComputeEnergyMetabolicMultiplier(0f, 0f, 0.15f, 1f, false, true, 8f);
            float loadedEnergy = PlayerEffortLoadCalculator.ComputeEnergyMetabolicMultiplier(1f, 1f, 0.15f, 0.6f, true, true, 8f);
            float loadedOxygen = PlayerEffortLoadCalculator.ComputeOxygenMetabolicMultiplier(1f, 1f, 0.6f, true, true, 6f);

            Assert.That(baselineEnergy, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(loadedEnergy, Is.GreaterThan(4f));
            Assert.That(loadedOxygen, Is.GreaterThan(2f));
        }

        [Test]
        public void ComputeMetabolicMultiplier_DoesNotTaxIdleLoad()
        {
            float idleEnergy = PlayerEffortLoadCalculator.ComputeEnergyMetabolicMultiplier(1f, 0f, 0.15f, 0.6f, false, true, 8f);
            float idleOxygen = PlayerEffortLoadCalculator.ComputeOxygenMetabolicMultiplier(1f, 0f, 0.6f, false, true, 6f);

            Assert.That(idleEnergy, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(idleOxygen, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ComputeMetabolicMultiplier_ClampsNonFiniteAndMaximum()
        {
            float energy = PlayerEffortLoadCalculator.ComputeEnergyMetabolicMultiplier(
                float.PositiveInfinity,
                float.NaN,
                float.PositiveInfinity,
                float.NaN,
                true,
                true,
                2f);
            float oxygen = PlayerEffortLoadCalculator.ComputeOxygenMetabolicMultiplier(
                float.PositiveInfinity,
                float.NaN,
                float.NaN,
                true,
                true,
                1.5f);

            Assert.That(energy, Is.InRange(1f, 2f));
            Assert.That(oxygen, Is.InRange(1f, 1.5f));
        }

        [Test]
        public void NonFiniteInputs_FallBackToSafeDefaults()
        {
            float loadRatio = PlayerEffortLoadCalculator.ComputeLoadRatio(float.NaN, float.PositiveInfinity);
            float movementMultiplier = PlayerEffortLoadCalculator.ComputeMovementMultiplier(float.PositiveInfinity, float.NaN, float.NaN);
            float upwardSwimMultiplier = PlayerEffortLoadCalculator.ComputeUpwardSwimMultiplier(float.NaN, float.PositiveInfinity);
            bool criticalFailure = PlayerEffortLoadCalculator.ShouldTriggerCriticalStaminaFailure(float.NaN, float.NaN, 1.5f, 0.1f);

            Assert.That(loadRatio, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(movementMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(upwardSwimMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.IsFalse(criticalFailure);
        }
    }
}
