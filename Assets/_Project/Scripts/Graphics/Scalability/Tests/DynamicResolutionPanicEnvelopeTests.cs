using NUnit.Framework;

namespace Hecton8.Graphics.Scalability.Tests
{
    /// <summary>
    /// Locks the continuous panic-collapse contract of <see cref="DynamicResolutionPanicEnvelope"/>, which
    /// <see cref="ThermalDynamicResolutionAdapter"/> consumes in
    /// <c>AdvanceThermalResolutionState</c> to decide how hard render scale collapses.
    ///
    /// Regression guarded: the adapter used to branch on <c>_latestFrameTimeEwmaMs &gt;= PanicFrameTimeMs
    /// || _pressureLevel &gt;= 3</c> and, when true, snap render scale straight to the tier floor,
    /// bypassing both temporal smoothers. One threshold, no release band. Because a lower render scale
    /// lowers frame time, the loop closed on itself: collapse -&gt; frame time falls under the threshold
    /// -&gt; scale ramps back up -&gt; frame time rises over the threshold -&gt; collapse. On middle-tier
    /// hardware sitting near the budget that is a visible resolution pump between 1.0 and the tier floor
    /// every second or so. AGENTS.md:231 rejects binary quality switches; AGENTS.md:239 requires
    /// hysteresis with a 2-3 second minimum band on any scalability switch.
    /// </summary>
    [TestFixture]
    public sealed class DynamicResolutionPanicEnvelopeTests
    {
        private const float Onset = DynamicResolutionPanicEnvelope.DefaultOnsetFrameTimeMs;
        private const float Saturation = DynamicResolutionPanicEnvelope.DefaultSaturationFrameTimeMs;
        private const float Release = DynamicResolutionPanicEnvelope.DefaultReleaseSeconds;
        private const float Tolerance = 1e-4f;

        private static float Authority(float frameTimeMs, float pressureLevel = 0f)
        {
            return DynamicResolutionPanicEnvelope.ResolveInstantAuthority01(
                frameTimeMs,
                Onset,
                Saturation,
                pressureLevel);
        }

        [Test]
        public void Authority_IsZeroAtAndBelowOnset_NoStepWhereTheOldBooleanFired()
        {
            Assert.AreEqual(0f, Authority(0f), Tolerance);
            Assert.AreEqual(0f, Authority(16.66f), Tolerance);
            Assert.AreEqual(0f, Authority(Onset - 0.001f), Tolerance);
            Assert.AreEqual(
                0f,
                Authority(Onset),
                Tolerance,
                "The old code jumped to a full collapse exactly here; the curve must still be zero.");
        }

        [Test]
        public void Authority_SaturatesAtAndAboveSaturationFrameTime()
        {
            Assert.AreEqual(1f, Authority(Saturation), Tolerance);
            Assert.AreEqual(1f, Authority(120f), Tolerance);
        }

        [Test]
        public void Authority_IsMonotonicAndContinuousAcrossTheOnsetBand()
        {
            const int steps = 400;
            float previous = Authority(Onset - 5f);
            float largestStep = 0f;

            for (int i = 0; i <= steps; i++)
            {
                float frameTimeMs = (Onset - 5f) + ((Saturation + 5f) - (Onset - 5f)) * (i / (float)steps);
                float current = Authority(frameTimeMs);
                Assert.GreaterOrEqual(
                    current + Tolerance,
                    previous,
                    "Panic authority must never fall as frame time rises.");

                float step = current - previous;
                if (step > largestStep)
                    largestStep = step;

                previous = current;
            }

            Assert.AreEqual(1f, previous, Tolerance);
            Assert.Less(
                largestStep,
                0.05f,
                "A single 0.06 ms frame-time step must not move authority like a switch would.");
        }

        [Test]
        public void Authority_EmergencyPressureLevelSaturates_LowerLevelsDoNot()
        {
            Assert.AreEqual(0f, Authority(16.66f, 0f), Tolerance);
            Assert.AreEqual(0f, Authority(16.66f, 2f), Tolerance);
            Assert.AreEqual(
                1f,
                Authority(16.66f, 3f),
                Tolerance,
                "Pressure level 3 is the emergency lane and must still collapse immediately.");
        }

        [Test]
        public void Advance_AttackIsImmediate()
        {
            float authority = DynamicResolutionPanicEnvelope.Advance(0f, 1f, 1f / 60f, Release);
            Assert.AreEqual(1f, authority, Tolerance, "A real collapse must not be delayed by the latch.");
        }

        [Test]
        public void Advance_ReleaseHonoursTheHysteresisBand()
        {
            const float deltaSeconds = 1f / 60f;
            float authority = DynamicResolutionPanicEnvelope.Advance(0f, 1f, deltaSeconds, Release);
            int frames = 0;

            while (authority > 0f && frames < 100000)
            {
                authority = DynamicResolutionPanicEnvelope.Advance(authority, 0f, deltaSeconds, Release);
                frames++;
            }

            float elapsedSeconds = frames * deltaSeconds;
            Assert.AreEqual(0f, authority, Tolerance);
            Assert.GreaterOrEqual(
                elapsedSeconds,
                2f,
                "AGENTS.md:239 sets a 2-3 second minimum hysteresis band for a scalability switch.");
            Assert.LessOrEqual(
                elapsedSeconds,
                3f,
                "A longer hold than the band would pin the player at the tier floor for no reason.");
            Assert.AreEqual(
                Release,
                DynamicResolutionPanicEnvelope.ResolveReleaseSeconds(1f, 0f, Release),
                Tolerance);
        }

        [Test]
        public void Advance_ContainsNonFiniteInput()
        {
            Assert.AreEqual(0f, DynamicResolutionPanicEnvelope.Advance(float.NaN, 0f, 1f / 60f, Release), Tolerance);
            Assert.AreEqual(
                0f,
                DynamicResolutionPanicEnvelope.Advance(0f, float.PositiveInfinity, 1f / 60f, Release),
                Tolerance,
                "A garbage collapse demand must be discarded, never escalated into a resolution drop the player sees.");
            Assert.AreEqual(
                1f,
                DynamicResolutionPanicEnvelope.Advance(1f, 0f, float.NaN, Release),
                Tolerance,
                "A garbage delta must not release the latch early.");
            Assert.AreEqual(0f, Authority(float.NaN), Tolerance);
        }

        [Test]
        public void ApplyCollapse_IsANoOpWithoutAuthority()
        {
            Assert.AreEqual(0.93f, DynamicResolutionPanicEnvelope.ApplyCollapse(0.93f, 0.70f, 0f), Tolerance);
        }

        [Test]
        public void ApplyCollapse_ReachesTheFloorAtFullAuthority()
        {
            Assert.AreEqual(0.70f, DynamicResolutionPanicEnvelope.ApplyCollapse(1.00f, 0.70f, 1f), Tolerance);
            Assert.AreEqual(0.85f, DynamicResolutionPanicEnvelope.ApplyCollapse(1.00f, 0.70f, 0.5f), Tolerance);
        }

        [Test]
        public void ApplyCollapse_NeverRaisesAScale()
        {
            Assert.AreEqual(
                0.62f,
                DynamicResolutionPanicEnvelope.ApplyCollapse(0.62f, 0.85f, 1f),
                Tolerance,
                "Recovery must stay on the smoothed path even while the envelope is latched.");
            Assert.AreEqual(0.62f, DynamicResolutionPanicEnvelope.ApplyCollapse(0.62f, 0.85f, 0.4f), Tolerance);
        }

        /// <summary>
        /// Closed-loop proof against a plant where render scale drives frame time. This models the
        /// collapse/recovery loop the envelope governs, not the whole adapter policy: the plant is
        /// <c>frameMs = load * scale^2</c>, recovery pulls toward 1.0, the collapse floor is the
        /// middle-tier 0.7, and the frame-time input is the same 0.18-alpha EWMA the adapter uses. The
        /// load is chosen so 1.0 is over the panic threshold and 0.7 is under it, which is exactly the
        /// hardware band where the old boolean branch limit-cycled.
        /// </summary>
        [Test]
        public void ClosedLoop_BooleanBranchOscillates_EnvelopeDoesNot()
        {
            int booleanReversals = SimulateReversals(useEnvelope: false);
            int envelopeReversals = SimulateReversals(useEnvelope: true);

            Assert.Greater(
                booleanReversals,
                8,
                "The removed boolean branch must be shown to actually pump before the fix is credited.");
            Assert.LessOrEqual(
                envelopeReversals,
                2,
                "The envelope must settle instead of pumping render scale at the threshold.");
        }

        private static int SimulateReversals(bool useEnvelope)
        {
            const float deltaSeconds = 1f / 30f;
            const float ewmaAlpha = 0.18f;
            const float smoothingAlpha = 0.232f;
            const float collapseFloor = 0.70f;
            const float recoveryTarget = 1.00f;
            const float plantLoadMs = 40f;
            const int frames = 900;
            const float reversalEpsilon = 0.002f;

            float scale = recoveryTarget;
            float frameTimeEwmaMs = plantLoadMs;
            float authority = 0f;
            int reversals = 0;
            int previousDirection = 0;

            for (int i = 0; i < frames; i++)
            {
                float plantFrameMs = plantLoadMs * scale * scale;
                frameTimeEwmaMs += (plantFrameMs - frameTimeEwmaMs) * ewmaAlpha;

                float smoothed = scale + (recoveryTarget - scale) * smoothingAlpha;
                float nextScale;

                if (useEnvelope)
                {
                    authority = DynamicResolutionPanicEnvelope.Advance(
                        authority,
                        Authority(frameTimeEwmaMs),
                        deltaSeconds,
                        Release);
                    nextScale = DynamicResolutionPanicEnvelope.ApplyCollapse(smoothed, collapseFloor, authority);
                }
                else
                {
                    nextScale = frameTimeEwmaMs >= Onset ? collapseFloor : smoothed;
                }

                float delta = nextScale - scale;
                int direction = delta > reversalEpsilon ? 1 : (delta < -reversalEpsilon ? -1 : previousDirection);
                if (direction != 0 && previousDirection != 0 && direction != previousDirection)
                    reversals++;

                previousDirection = direction;
                scale = nextScale;
            }

            return reversals;
        }
    }
}
