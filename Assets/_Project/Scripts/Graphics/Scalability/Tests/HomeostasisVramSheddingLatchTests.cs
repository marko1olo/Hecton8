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
}
