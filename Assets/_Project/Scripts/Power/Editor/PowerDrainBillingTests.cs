#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Power;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Proves the PowerDrainSignal billing math consumes every variable it is handed:
    /// the watt figure, the tick delta, the Reason discriminator, and the Flags byte.
    /// The defect this guards is a signal charged to nothing, so the tests assert on
    /// energy actually produced rather than on the code path taken.
    /// </summary>
    public class PowerDrainBillingTests
    {
        private const float Tolerance = 1e-5f;

        /// <summary>LaserCutterDodJobs.cs:417 - wattsAtPowerOne (180) * power, at full cutting power.</summary>
        private const float LaserCutterWattsAtFullPower = 180f;

        private const float SixtyHzDelta = 1f / 60f;

        [Test]
        public void HandheldToolDrain_IsBillable()
        {
            Assert.IsTrue(PowerDrainBilling.IsBillableAsWirelessToolDrain(
                PowerDrainBilling.ReasonUnattributedTool, 0));
        }

        [Test]
        public void FabricationDrain_IsSkippedBecauseThePowerRatingRouteAlreadyChargedIt()
        {
            // Fabricator.cs:502 returns -craftPowerDraw from IPowerComponent.PowerRating and
            // PowerGrid.cs:2863 sums it into node demand. Billing it here too would charge
            // one craft twice.
            Assert.IsFalse(PowerDrainBilling.IsBillableAsWirelessToolDrain(
                PowerDrainBilling.ReasonFabrication, 0));
        }

        [Test]
        public void PausedDrain_IsNotBilled()
        {
            Assert.IsFalse(PowerDrainBilling.IsBillableAsWirelessToolDrain(
                PowerDrainBilling.ReasonUnattributedTool, PowerDrainBilling.FlagPaused));
        }

        [Test]
        public void OneSecondOfCuttingAtFullPower_CostsTheFullWattSecondFigure()
        {
            // The whole point of the packet: 180 W drawn for one second must cost 180 W*s.
            float residual = 0f;
            float billed = 0f;
            for (int frame = 0; frame < 60; frame++)
            {
                billed += PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                    LaserCutterWattsAtFullPower, SixtyHzDelta, residual, out residual);
            }

            Assert.AreEqual(180f, billed, 1e-3f, "a full second of cutting must cost 180 W*s, not zero");
            Assert.AreEqual(0f, residual, Tolerance);
        }

        [Test]
        public void SingleFrameOfCutting_ConvertsWattsToWattSecondsWithTheDelta()
        {
            float submitted = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                LaserCutterWattsAtFullPower, SixtyHzDelta, 0f, out float residual);

            Assert.AreEqual(3f, submitted, Tolerance, "180 W over 1/60 s is 3 W*s");
            Assert.AreEqual(0f, residual, Tolerance);
        }

        [Test]
        public void ZeroWatts_SubmitsNothing()
        {
            float submitted = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                0f, SixtyHzDelta, 0f, out float residual);

            Assert.AreEqual(0f, submitted, Tolerance);
            Assert.AreEqual(0f, residual, Tolerance);
        }

        [Test]
        public void SubFloorTrickle_IsCarriedThenSubmitted_NotDiscarded()
        {
            // 0.02 W over 0.02 s is 0.0004 W*s - under the owner's 0.0001 rejection floor
            // scaled by the submit guard. Discarding it would make a low-draw tool free forever.
            const float Watts = 0.02f;
            const float Delta = 0.02f;

            float submitted = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                Watts, Delta, 0f, out float residual);
            Assert.AreEqual(0f, submitted, Tolerance, "first tick is under the submit floor");
            Assert.AreEqual(0.0004f, residual, Tolerance, "and is carried, not lost");

            submitted = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                Watts, Delta, residual, out residual);
            Assert.AreEqual(0f, submitted, Tolerance, "second tick still under the floor");
            Assert.AreEqual(0.0008f, residual, Tolerance);

            submitted = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                Watts, Delta, residual, out residual);
            Assert.AreEqual(0.0012f, submitted, Tolerance, "third tick crosses the floor and bills the whole carry");
            Assert.AreEqual(0f, residual, Tolerance, "carry is cleared once it has been submitted");
        }

        [Test]
        public void FrameHitch_IsTruncatedSoItCannotBillALumpThePlayerNeverSpent()
        {
            float submitted = PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                100f, 10f, 0f, out _);

            Assert.AreEqual(25f, submitted, Tolerance, "10 s of delta is truncated to the 0.25 s window");
        }

        [Test]
        public void NonFiniteInputs_ChargeNothing()
        {
            Assert.AreEqual(0f, PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                float.NaN, SixtyHzDelta, 0f, out _), Tolerance);
            Assert.AreEqual(0f, PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                float.PositiveInfinity, SixtyHzDelta, 0f, out _), Tolerance);
            Assert.AreEqual(0f, PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                LaserCutterWattsAtFullPower, float.NaN, 0f, out _), Tolerance);
            Assert.AreEqual(0f, PowerDrainBilling.ResolveSubmittedEnergyWattSeconds(
                LaserCutterWattsAtFullPower, -1f, 0f, out _), Tolerance);
        }

        [Test]
        public void CarriedResidual_IsBounded()
        {
            Assert.AreEqual(
                PowerDrainBilling.MaxCarriedResidualWattSeconds,
                PowerDrainBilling.SanitizeResidual(500f),
                Tolerance,
                "an unpayable trickle must not grow without bound");
            Assert.AreEqual(0f, PowerDrainBilling.SanitizeResidual(float.NaN), Tolerance);
            Assert.AreEqual(0f, PowerDrainBilling.SanitizeResidual(-3f), Tolerance);
        }

        [Test]
        public void WattAccumulator_SumsRealPayloadsAndRejectsGarbage()
        {
            float total = PowerDrainBilling.AccumulateBillableWatts(0f, 180f);
            total = PowerDrainBilling.AccumulateBillableWatts(total, 90f);
            Assert.AreEqual(270f, total, Tolerance, "two cutter hits in one frame both cost power");

            Assert.AreEqual(270f, PowerDrainBilling.AccumulateBillableWatts(total, float.NaN), Tolerance);
            Assert.AreEqual(270f, PowerDrainBilling.AccumulateBillableWatts(total, float.NegativeInfinity), Tolerance);
            Assert.AreEqual(270f, PowerDrainBilling.AccumulateBillableWatts(total, -50f), Tolerance);
            Assert.AreEqual(270f, PowerDrainBilling.AccumulateBillableWatts(total, 0f), Tolerance);
        }

        [Test]
        public void PartialGrant_ReportsTheUnpaidRemainderAsShortfall()
        {
            Assert.AreEqual(6f, PowerDrainBilling.ResolveUnpaidEnergyWattSeconds(10f, 4f), Tolerance);
            Assert.AreEqual(10f, PowerDrainBilling.ResolveUnpaidEnergyWattSeconds(10f, 0f), Tolerance,
                "a dead battery bank means the whole draw went unpaid");
            Assert.AreEqual(0f, PowerDrainBilling.ResolveUnpaidEnergyWattSeconds(10f, 10f), Tolerance);
            Assert.AreEqual(0f, PowerDrainBilling.ResolveUnpaidEnergyWattSeconds(10f, 12f), Tolerance);
            Assert.AreEqual(0f, PowerDrainBilling.ResolveUnpaidEnergyWattSeconds(0f, 0f), Tolerance);
        }

        [Test]
        public void EnergyTotals_SaturateInsteadOfDrifting()
        {
            Assert.AreEqual(7f, PowerDrainBilling.AccumulateEnergyWattSeconds(3f, 4f), Tolerance);
            Assert.AreEqual(3f, PowerDrainBilling.AccumulateEnergyWattSeconds(3f, float.NaN), Tolerance);
            Assert.AreEqual(3f, PowerDrainBilling.AccumulateEnergyWattSeconds(3f, -4f), Tolerance);
            Assert.AreEqual(
                PowerDrainBilling.MaxAccumulatedEnergyWattSeconds,
                PowerDrainBilling.AccumulateEnergyWattSeconds(
                    PowerDrainBilling.MaxAccumulatedEnergyWattSeconds, 1000f),
                PowerDrainBilling.MaxAccumulatedEnergyWattSeconds * 1e-6f);
        }
    }
}
#endif
