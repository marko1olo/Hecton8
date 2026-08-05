#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Power;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Proves the sealed-door power latch consumes its inputs and cannot chatter.
    /// Mirrors the rails WfcOutpostPowerBootRuntime uses: engage 0.10, release 0.07,
    /// dwell 2.5 s.
    /// </summary>
    public class PowerHysteresisLatchTests
    {
        private const float Engage = 0.10f;
        private const float Release = 0.07f;
        private const float Dwell = 2.5f;

        [Test]
        public void ColdStart_AdoptsMeasuredSideImmediately()
        {
            byte powered = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateUnknown, 0.9f, Engage, Release, 100f, 0f, Dwell, out float poweredSince);
            byte dark = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateUnknown, 0.0f, Engage, Release, 100f, 0f, Dwell, out float darkSince);

            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, powered);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, dark);
            Assert.AreEqual(100f, poweredSince);
            Assert.AreEqual(100f, darkSince);
        }

        [Test]
        public void EngagedLatch_HoldsInsideTheVoltageBand()
        {
            // 0.085 is below the engage rail but above the release rail: the old bare
            // `voltage > 0.1` compare dropped the door here.
            byte state = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateEngaged, 0.085f, Engage, Release, 10f, 0f, Dwell, out float since);

            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state);
            Assert.AreEqual(10f, since, "candidate agrees with the latch, so the dwell clock rearms");
        }

        [Test]
        public void EngagedLatch_DoesNotDropBeforeDwellElapses()
        {
            float since = 10f;
            byte state = PowerHysteresisLatch.StateEngaged;

            state = PowerHysteresisLatch.Evaluate(state, 0.02f, Engage, Release, 10f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state);
            Assert.AreEqual(10f, since);

            state = PowerHysteresisLatch.Evaluate(state, 0.02f, Engage, Release, 12.4f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state, "2.4 s < 2.5 s dwell");
            Assert.AreEqual(10f, since, "dwell anchor must not move while counting");
        }

        [Test]
        public void EngagedLatch_DropsOnceDwellElapses()
        {
            float since = 10f;
            byte state = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateEngaged, 0.02f, Engage, Release, 12.5f, since, Dwell, out since);

            Assert.AreEqual(PowerHysteresisLatch.StateReleased, state);
            Assert.AreEqual(12.5f, since);
            Assert.IsFalse(PowerHysteresisLatch.IsEngaged(state));
        }

        [Test]
        public void SolverRipple_AcrossTheEngageRail_NeverEngagesAReleasedLatch()
        {
            // Ten seconds of 10 Hz ripple straddling 0.10. The bare compare produced
            // five lock/unlock flips per second here.
            byte state = PowerHysteresisLatch.StateReleased;
            float since = 0f;
            int flips = 0;
            for (int step = 0; step < 100; step++)
            {
                float now = step * 0.1f;
                float level = (step & 1) == 0 ? 0.104f : 0.096f;
                byte next = PowerHysteresisLatch.Evaluate(state, level, Engage, Release, now, since, Dwell, out since);
                if (next != state)
                    flips++;
                state = next;
            }

            Assert.AreEqual(0, flips, "a rail-straddling ripple must never latch");
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, state);
        }

        [Test]
        public void ReleasedLatch_EngagesOnlyAfterSustainedOvervoltage()
        {
            byte state = PowerHysteresisLatch.StateReleased;
            float since = 0f;

            state = PowerHysteresisLatch.Evaluate(state, 0.4f, Engage, Release, 0f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, state, "first sample only arms the dwell clock");

            state = PowerHysteresisLatch.Evaluate(state, 0.4f, Engage, Release, 2.49f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, state);

            state = PowerHysteresisLatch.Evaluate(state, 0.4f, Engage, Release, 2.5f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state);
            Assert.IsTrue(PowerHysteresisLatch.IsEngaged(state));
        }

        [Test]
        public void InterruptedExcursion_RestartsTheDwellClock()
        {
            byte state = PowerHysteresisLatch.StateEngaged;
            float since = 0f;

            state = PowerHysteresisLatch.Evaluate(state, 0.01f, Engage, Release, 0f, since, Dwell, out since);
            state = PowerHysteresisLatch.Evaluate(state, 0.01f, Engage, Release, 2.0f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state);

            // One sample back above the release rail rearms the clock.
            state = PowerHysteresisLatch.Evaluate(state, 0.09f, Engage, Release, 2.1f, since, Dwell, out since);
            Assert.AreEqual(2.1f, since);

            state = PowerHysteresisLatch.Evaluate(state, 0.01f, Engage, Release, 2.2f, since, Dwell, out since);
            state = PowerHysteresisLatch.Evaluate(state, 0.01f, Engage, Release, 4.5f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state, "cumulative, non-continuous time must not flip the latch");

            state = PowerHysteresisLatch.Evaluate(state, 0.01f, Engage, Release, 4.71f, since, Dwell, out since);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, state, "2.51 s continuous does flip it");
        }

        [Test]
        public void RewoundClock_ReAnchorsInsteadOfFlipping()
        {
            byte state = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateEngaged, 0.0f, Engage, Release, 3f, 900f, Dwell, out float since);

            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state);
            Assert.AreEqual(3f, since);
        }

        [Test]
        public void NonFiniteInputs_AreTreatedAsUnpoweredAndNeverThrow()
        {
            byte nan = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateUnknown, float.NaN, Engage, Release, float.NaN, float.NaN, float.NaN, out float nanSince);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, nan);
            Assert.AreEqual(0f, nanSince);

            byte infinite = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateUnknown, float.PositiveInfinity, Engage, Release, 5f, 0f, Dwell, out _);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, infinite, "an infinite level sanitizes to 0, not to full power");

            byte negative = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateEngaged, -1000f, Engage, Release, 100f, 0f, Dwell, out _);
            Assert.AreEqual(PowerHysteresisLatch.StateReleased, negative);
        }

        [Test]
        public void InvertedRails_CollapseToASingleRailInsteadOfLatchingBothWays()
        {
            // release > engage would make both candidate tests true at once.
            byte state = PowerHysteresisLatch.Evaluate(
                PowerHysteresisLatch.StateEngaged, 0.09f, 0.05f, 0.5f, 10f, 0f, Dwell, out float since);

            Assert.AreEqual(PowerHysteresisLatch.StateEngaged, state);
            Assert.AreEqual(10f, since);
        }

        [Test]
        public void DwellClamp_EnforcesTheAgentsMdTimeBandFloor()
        {
            Assert.AreEqual(PowerHysteresisLatch.MinimumDwellSeconds, PowerHysteresisLatch.ClampDwellSeconds(0f));
            Assert.AreEqual(PowerHysteresisLatch.MinimumDwellSeconds, PowerHysteresisLatch.ClampDwellSeconds(-5f));
            Assert.AreEqual(PowerHysteresisLatch.MinimumDwellSeconds, PowerHysteresisLatch.ClampDwellSeconds(float.NaN));
            Assert.AreEqual(PowerHysteresisLatch.MaximumDwellSeconds, PowerHysteresisLatch.ClampDwellSeconds(1e9f));
            Assert.AreEqual(Dwell, PowerHysteresisLatch.ClampDwellSeconds(Dwell));
            Assert.GreaterOrEqual(PowerHysteresisLatch.MinimumDwellSeconds, 2f);
        }

                [TestCase(0.1f, 0.3f, 0.07f)]
        [TestCase(0.1f, 0f, 0.1f)]
        [TestCase(0.1f, 1f, 0f)]
        [TestCase(float.NaN, 0.3f, 0f)]
        [TestCase(-0.5f, 0.3f, 0f)]
        [TestCase(0.1f, -0.5f, 0.1f)]
        [TestCase(1.5f, 0.3f, 0.7f)]
        [TestCase(0.1f, 1.5f, 0f)]
        [TestCase(float.PositiveInfinity, 0.3f, 0f)]
        [TestCase(0.1f, float.PositiveInfinity, 0.1f)]
        public void ResolveReleaseLevel_DerivesALowerRail(float engageLevel, float bandFraction, float expectedReleaseLevel)
        {
            Assert.AreEqual(expectedReleaseLevel, PowerHysteresisLatch.ResolveReleaseLevel01(engageLevel, bandFraction), 1e-6f);
        }
    }
}
#endif
