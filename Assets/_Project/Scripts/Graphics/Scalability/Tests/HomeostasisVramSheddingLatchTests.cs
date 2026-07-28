using Hecton8.Core;
using NUnit.Framework;

namespace Hecton8.Graphics.Scalability.Tests
{
    /// <summary>
    /// Locks the VRAM load-shed band of <see cref="HomeostasisBrain.ResolveVramSheddingLatch"/>, which
    /// <c>HomeostasisBrain.ApplyDictatorPressurePolicy</c> consumes to decide whether
    /// <c>SystemBit.VramShedding</c> and <c>SystemBit.NonCriticalVfx</c> are set and whether the pressure
    /// level is escalated to 2.
    ///
    /// Regression guarded: the dictator used to branch on a bare
    /// <c>vramPressure01 &gt; VramOomThreshold</c> with the <c>else</c> clearing the same bit. One
    /// threshold, entry and exit at the identical value, no dwell. VRAM pressure is resampled every
    /// dispatcher frame from the graphics driver's own allocation total over a fixed budget, so a machine
    /// parked near its graphics budget straddles 0.85 continuously. Every flip escalated the pressure
    /// level to 2, and level 2 makes
    /// <c>ThermalDynamicResolutionAdapter.ResolveThermalPressureCollapse01</c> return 0.5, which lerps the
    /// requested render scale halfway to the tier floor - so the resolution pumped once per frame, the
    /// kill-switch signal republished once per frame, and one frame of allocation noise cost the player
    /// their decorative VFX until sequential restoration walked back to it.
    ///
    /// Shedding lowers the very pressure that triggered it, so a single shared threshold closes the loop
    /// on itself. AGENTS.md:231 rejects binary quality switches, AGENTS.md:239 requires a 2-3 second
    /// minimum band on any scalability switch, and performance.md:122 requires load shedding to be an
    /// authored state machine rather than a panic button.
    /// </summary>
    [TestFixture]
    public sealed class HomeostasisVramSheddingLatchTests
    {
        private const float Arm = HomeostasisBrain.VramShedArmPressure01;
        private const float Release = HomeostasisBrain.VramShedReleasePressure01;
        private const float Band = HomeostasisBrain.VramShedMinimumHoldSeconds;
        private const float FrameSeconds = 1f / 60f;

        private static bool Step(bool latched, float pressure01, float deltaSeconds, ref float holdSeconds)
        {
            return HomeostasisBrain.ResolveVramSheddingLatch(
                latched,
                pressure01,
                deltaSeconds,
                Arm,
                Release,
                Band,
                ref holdSeconds);
        }

        [Test]
        public void Band_IsOrderedAndMeetsTheMinimumDwellLaw()
        {
            Assert.Less(Release, Arm, "Release pressure must sit strictly below arm pressure.");
            Assert.GreaterOrEqual(
                Band,
                2f,
                "AGENTS.md:239 sets a 2-3 second floor on a scalability switch band.");
        }

        [Test]
        public void Latch_DoesNotArmBelowOrExactlyAtTheOldThreshold()
        {
            float hold = 0f;
            Assert.IsFalse(Step(false, 0f, FrameSeconds, ref hold));
            Assert.IsFalse(Step(false, Release, FrameSeconds, ref hold));
            Assert.IsFalse(Step(false, Arm - 0.001f, FrameSeconds, ref hold));
            Assert.IsFalse(
                Step(false, Arm, FrameSeconds, ref hold),
                "The arm point must stay exactly where the old boolean fired, so the fix adds no step.");
        }

        [Test]
        public void Latch_ArmsStrictlyAboveTheArmPressure()
        {
            float hold = 12f;
            Assert.IsTrue(Step(false, Arm + 0.001f, FrameSeconds, ref hold));
            Assert.AreEqual(
                0f,
                hold,
                1e-6f,
                "Arming must restart the dwell so the next release needs a full band.");
        }

        [Test]
        public void Latch_HoldsForTheFullBandEvenWhenPressureCollapsesInstantly()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.05f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            float billed = 0f;
            int guard = 0;
            while (latched && guard < 10000)
            {
                latched = Step(latched, 0f, FrameSeconds, ref hold);
                guard++;
                if (latched)
                    billed += FrameSeconds;
            }

            Assert.IsFalse(latched, "The latch must eventually release once pressure is at zero.");
            Assert.GreaterOrEqual(
                billed + (2f * FrameSeconds),
                Band,
                "Release happened before the minimum dwell had been billed.");
            Assert.Less(billed, Band + (4f * FrameSeconds), "Release overshot the dwell band.");
        }

        [Test]
        public void Latch_NeverReleasesWhilePressureStaysInsideTheBand()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.05f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            for (int i = 0; i < 600; i++)
            {
                latched = Step(latched, Arm - 0.02f, FrameSeconds, ref hold);
                Assert.IsTrue(latched, "Pressure inside the band must hold the latch, at step " + i);
            }
        }

        [Test]
        public void Latch_ReleasesWhenPressureReachesTheReleasePressureExactly()
        {
            float hold = Band;
            Assert.IsFalse(
                Step(true, Release, FrameSeconds, ref hold),
                "Release is inclusive at the release pressure.");
            Assert.AreEqual(0f, hold, 1e-6f);
        }

        [Test]
        public void Latch_DoesNotLimitCycleOnSamplesStraddlingTheArmPressure()
        {
            float hold = 0f;
            bool latched = false;
            int transitions = 0;
            for (int i = 0; i < 600; i++)
            {
                float pressure01 = (i & 1) == 0 ? Arm + 0.001f : Arm - 0.001f;
                bool next = Step(latched, pressure01, FrameSeconds, ref hold);
                if (next != latched)
                    transitions++;

                latched = next;
            }

            Assert.AreEqual(
                1,
                transitions,
                "Driver allocation noise across the arm pressure must produce exactly one arm and no release.");
            Assert.IsTrue(latched);
        }

        [Test]
        public void Latch_RearmingRestartsTheDwellFromZero()
        {
            float hold = Band + 10f;
            bool latched = Step(true, 0f, FrameSeconds, ref hold);
            Assert.IsFalse(latched);

            latched = Step(latched, Arm + 0.05f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            float billed = 0f;
            while (latched && billed < Band - (2f * FrameSeconds))
            {
                latched = Step(latched, 0f, FrameSeconds, ref hold);
                billed += FrameSeconds;
            }

            Assert.IsTrue(latched, "A re-armed latch must serve a fresh full dwell, not inherit the old one.");
        }

        [Test]
        public void Latch_BillsNothingForNonFiniteOrNegativeDeltaSeconds()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.05f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            for (int i = 0; i < 1000; i++)
            {
                latched = Step(latched, 0f, float.NaN, ref hold);
                Assert.IsTrue(latched);
                latched = Step(latched, 0f, -5f, ref hold);
                Assert.IsTrue(latched);
                latched = Step(latched, 0f, 0f, ref hold);
                Assert.IsTrue(latched, "A stalled clock must never release the latch, at step " + i);
            }
        }

        [Test]
        public void Latch_TreatsNonFinitePressureAsWorstCaseAndKeepsShedding()
        {
            float hold = Band + 10f;
            Assert.IsTrue(
                Step(true, float.NaN, FrameSeconds, ref hold),
                "A garbage pressure sample must not be read as headroom.");
            Assert.IsTrue(Step(true, float.PositiveInfinity, FrameSeconds, ref hold));
        }

        [Test]
        public void Latch_ClampsAnInvertedOverrideSoTheBandCannotCrossOver()
        {
            float hold = 0f;
            bool latched = HomeostasisBrain.ResolveVramSheddingLatch(
                false,
                0.60f,
                FrameSeconds,
                0.50f,
                0.90f,
                Band,
                ref hold);
            Assert.IsTrue(latched, "Arm pressure 0.50 must still arm at 0.60.");

            float billed = 0f;
            int guard = 0;
            while (latched && guard < 10000)
            {
                latched = HomeostasisBrain.ResolveVramSheddingLatch(
                    latched,
                    0.40f,
                    FrameSeconds,
                    0.50f,
                    0.90f,
                    Band,
                    ref hold);
                guard++;
                if (latched)
                    billed += FrameSeconds;
            }

            Assert.IsFalse(
                latched,
                "Release pressure is clamped to arm pressure, so 0.40 must release after the dwell.");
            Assert.GreaterOrEqual(billed + (2f * FrameSeconds), Band);
        }
    }

    /// <summary>
    /// Locks the dispatcher slow-lane cadence band that <c>HomeostasisBrain.ApplyDictatorPressurePolicy</c>
    /// uses to decide whether <c>SystemBit.AiOneHz</c> is set.
    ///
    /// Regression guarded: the dictator used to set that bit from a bare <c>systemHealth &gt; 0.95f</c> -
    /// a raw literal copy of the arm half of the authored level-3 band, with no release half and no dwell.
    /// <c>SystemBit.AiOneHz</c> IS <c>SystemBit.SlowTick2Hz</c>, so
    /// <c>SystemDispatcher.ApplyHomeostasisKillSwitch</c> writes it into <c>_homeostasisSlowTick2Hz</c> and
    /// <c>ResolveSlowTickIntervalSeconds</c> then returns 1.0 s instead of 0.1 s. A single-tick health spike
    /// therefore stretched the slow-tick interval of every owner on that lane tenfold for one tick and back,
    /// and a pacing clock counting ticks on a quality-varying lane is how quality state ended up moving
    /// gameplay rate once already (commit 4b307afde), which SYSTEMS_CONTRACTS.md:141 forbids.
    ///
    /// The band is now the authored level-3 activate/restore pair plus the AGENTS.md:239 minimum dwell,
    /// served by the same pure latch the VRAM shed band uses, so this fixture also proves the two bands
    /// share one hysteresis idiom instead of two.
    /// </summary>
    [TestFixture]
    public sealed class HomeostasisSlowTickCadenceLatchTests
    {
        private const float Arm = HomeostasisBrain.SlowTickCadenceArmShi;
        private const float Release = HomeostasisBrain.SlowTickCadenceReleaseShi;
        private const float Dwell = HomeostasisBrain.SlowTickCadenceMinimumHoldSeconds;
        private const float FrameSeconds = 1f / 60f;

        private static bool Step(bool latched, float systemHealth01, float deltaSeconds, ref float holdSeconds)
        {
            return HomeostasisBrain.ResolveVramSheddingLatch(
                latched,
                systemHealth01,
                deltaSeconds,
                Arm,
                Release,
                Dwell,
                ref holdSeconds);
        }

        [Test]
        public void Band_IsOrderedAndMeetsTheMinimumDwellLaw()
        {
            Assert.Less(Release, Arm, "The release point must sit strictly below the arm point.");
            Assert.GreaterOrEqual(
                Arm - Release,
                0.01f,
                "Arm and release must not collapse onto one sample - that is the defect, not the fix.");
            Assert.GreaterOrEqual(
                Dwell,
                2f,
                "AGENTS.md:239 sets a 2-3 second floor on an AI/solver-cadence switch band.");
        }

        [Test]
        public void Latch_DoesNotArmAtOrBelowTheAuthoredArmPoint()
        {
            float hold = 0f;
            Assert.IsFalse(Step(false, 0f, FrameSeconds, ref hold));
            Assert.IsFalse(Step(false, Release, FrameSeconds, ref hold));
            Assert.IsFalse(Step(false, Arm - 0.001f, FrameSeconds, ref hold));
            Assert.IsFalse(
                Step(false, Arm, FrameSeconds, ref hold),
                "The arm point must stay exactly where the old literal fired, so the fix adds no step.");
        }

        [Test]
        public void Latch_ArmsStrictlyAboveTheArmPointAndRestartsTheDwell()
        {
            float hold = 9f;
            Assert.IsTrue(Step(false, Arm + 0.001f, FrameSeconds, ref hold));
            Assert.AreEqual(
                0f,
                hold,
                1e-6f,
                "Arming must restart the dwell so the next release needs a full band.");
        }

        [Test]
        public void Latch_HoldsTheCadenceBitForTheFullDwellWhenHealthCollapsesOnTheNextTick()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.01f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            float billed = 0f;
            int guard = 0;
            while (latched && guard < 10000)
            {
                latched = Step(latched, 0f, FrameSeconds, ref hold);
                guard++;
                if (latched)
                    billed += FrameSeconds;
            }

            Assert.IsFalse(latched, "The cadence bit must eventually release at zero health pressure.");
            Assert.GreaterOrEqual(
                billed + (2f * FrameSeconds),
                Dwell,
                "A one-tick health spike released the cadence bit before the dwell was billed - that is the " +
                "0.1 s to 1.0 s slow-lane flip this band exists to stop.");
            Assert.Less(billed, Dwell + (4f * FrameSeconds), "Release overshot the dwell band.");
        }

        [Test]
        public void Latch_NeverReleasesWhileHealthStaysInsideTheBand()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.01f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            for (int i = 0; i < 600; i++)
            {
                latched = Step(latched, Arm - 0.005f, FrameSeconds, ref hold);
                Assert.IsTrue(latched, "Health inside the band must hold the cadence bit, at step " + i);
            }
        }

        [Test]
        public void Latch_ReleasesOnlyOnceHealthReachesTheReleasePoint()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.01f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            for (int i = 0; i < 600; i++)
            {
                latched = Step(latched, Release + 0.001f, FrameSeconds, ref hold);
                Assert.IsTrue(
                    latched,
                    "Health above the release point must hold the cadence bit, at step " + i);
            }

            Assert.IsFalse(
                Step(latched, Release, FrameSeconds, ref hold),
                "Release is inclusive at the authored level-3 restore point once the dwell is billed.");
        }

        [Test]
        public void Latch_DoesNotLimitCycleOnSamplesStraddlingTheArmPoint()
        {
            float hold = 0f;
            bool latched = false;
            int transitions = 0;
            for (int i = 0; i < 600; i++)
            {
                float systemHealth01 = (i & 1) == 0 ? Arm + 0.001f : Arm - 0.001f;
                bool next = Step(latched, systemHealth01, FrameSeconds, ref hold);
                if (next != latched)
                    transitions++;

                latched = next;
            }

            Assert.AreEqual(
                1,
                transitions,
                "Health samples straddling the arm point must produce exactly one arm and no release - " +
                "every extra transition is one 10x flip of the dispatcher slow-tick interval.");
            Assert.IsTrue(latched);
        }

        [Test]
        public void Latch_TreatsNonFiniteHealthAsWorstCaseAndKeepsTheCadenceBitSet()
        {
            float hold = Dwell + 10f;
            Assert.IsTrue(
                Step(true, float.NaN, FrameSeconds, ref hold),
                "A garbage health sample must not be read as recovered headroom.");
            Assert.IsTrue(Step(true, float.PositiveInfinity, FrameSeconds, ref hold));
        }

        [Test]
        public void Latch_BillsNothingForAStalledOrNegativeClock()
        {
            float hold = 0f;
            bool latched = Step(false, Arm + 0.01f, FrameSeconds, ref hold);
            Assert.IsTrue(latched);

            for (int i = 0; i < 1000; i++)
            {
                latched = Step(latched, 0f, float.NaN, ref hold);
                Assert.IsTrue(latched);
                latched = Step(latched, 0f, -5f, ref hold);
                Assert.IsTrue(latched);
                latched = Step(latched, 0f, 0f, ref hold);
                Assert.IsTrue(latched, "A stalled clock must never release the band early, at step " + i);
            }
        }
    }
}
