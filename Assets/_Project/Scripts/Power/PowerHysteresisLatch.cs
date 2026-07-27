// ============================================================================
// HECTON-8 - PowerHysteresisLatch.cs
//
// Unity-free, allocation-free Schmitt-trigger latch for power thresholds.
//
// A bare `voltage > threshold` compare is not a switch, it is a chattering
// contact. Grid potential produced by an iterative relaxation solve creeps and
// jitters, so a single rail flips the consumer every solve tick while the level
// sits on the rail. AGENTS.md:239 requires hysteresis on any switch with a
// minimum band of 3-5 meters or 2-3 seconds; logistics.md ("Failure And
// Readability") requires machinery response to agree with network truth rather
// than strobe against it.
//
// This helper owns no state. The caller keeps a `byte` latch state plus a
// `float` dwell anchor per switch and passes them back in, so the same math is
// usable from a POCO owner, a native SOA lane, or an EditMode test.
//
// PURE C#: no UnityEngine, no Unity.Mathematics, no Burst. Compiles and runs
// outside the editor.
// ============================================================================

namespace Hecton8.Power
{
    /// <summary>
    /// Deterministic dual-rail (Schmitt) latch with a dwell band, used by power
    /// consumers whose visible state must not oscillate while the solved node
    /// potential sits on a threshold.
    /// </summary>
    public static class PowerHysteresisLatch
    {
        /// <summary>Latch has never been evaluated; the next evaluation adopts the measured side immediately.</summary>
        public const byte StateUnknown = 0;

        /// <summary>Latch is open: the consumer is unpowered.</summary>
        public const byte StateReleased = 1;

        /// <summary>Latch is closed: the consumer is powered.</summary>
        public const byte StateEngaged = 2;

        /// <summary>AGENTS.md:239 time-band floor. A shorter dwell is clamped up to this value.</summary>
        public const float MinimumDwellSeconds = 2f;

        /// <summary>Upper clamp so a corrupt tuning value cannot freeze a latch for the whole session.</summary>
        public const float MaximumDwellSeconds = 30f;

        /// <summary>True when the latch is closed and the consumer should behave as powered.</summary>
        public static bool IsEngaged(byte latchState)
        {
            return latchState == StateEngaged;
        }

        /// <summary>
        /// Advances a latch by one sample.
        /// </summary>
        /// <param name="latchState">Previous latch state; <see cref="StateUnknown"/> on first evaluation.</param>
        /// <param name="level01">Measured normalized level, for example a solved node potential.</param>
        /// <param name="engageLevel01">Upper rail. A released latch closes only above this level.</param>
        /// <param name="releaseLevel01">Lower rail. An engaged latch opens only at or below this level.</param>
        /// <param name="nowSeconds">Monotonic unscaled seconds sampled from the dispatcher clock.</param>
        /// <param name="candidateSinceSeconds">Timestamp the opposing candidate first became true.</param>
        /// <param name="dwellSeconds">Continuous time the opposing candidate must hold before the latch flips.</param>
        /// <param name="nextCandidateSinceSeconds">Updated dwell anchor the caller must store back.</param>
        /// <returns>The new latch state.</returns>
        public static byte Evaluate(
            byte latchState,
            float level01,
            float engageLevel01,
            float releaseLevel01,
            float nowSeconds,
            float candidateSinceSeconds,
            float dwellSeconds,
            out float nextCandidateSinceSeconds)
        {
            float level = Saturate(level01);
            float engage = Saturate(engageLevel01);
            float release = Saturate(releaseLevel01);
            if (release > engage)
                release = engage;

            float now = SanitizeSeconds(nowSeconds);
            float dwell = ClampDwellSeconds(dwellSeconds);

            if (latchState != StateReleased && latchState != StateEngaged)
            {
                // Cold start. Adopt the measured side at once so the first published
                // state is truthful; the dwell band only governs later transitions.
                nextCandidateSinceSeconds = now;
                return level > engage ? StateEngaged : StateReleased;
            }

            bool engaged = latchState == StateEngaged;

            // An engaged latch tests against the lower rail, a released latch against
            // the upper rail. That voltage gap alone rejects small-signal jitter; the
            // dwell band below rejects a slow sweep across the whole gap.
            bool candidateEngaged = engaged ? level > release : level > engage;
            if (candidateEngaged == engaged)
            {
                // Candidate agrees with the latch: rearm the dwell clock so an
                // opposing excursion has to hold continuously, not cumulatively.
                nextCandidateSinceSeconds = now;
                return latchState;
            }

            float since = SanitizeSeconds(candidateSinceSeconds);
            float elapsed = now - since;
            if (elapsed < 0f)
            {
                // Clock rewound (scene reload, data vault rebind). Re-anchor rather
                // than treat the negative span as a satisfied dwell.
                nextCandidateSinceSeconds = now;
                return latchState;
            }

            if (elapsed < dwell)
            {
                nextCandidateSinceSeconds = since;
                return latchState;
            }

            nextCandidateSinceSeconds = now;
            return candidateEngaged ? StateEngaged : StateReleased;
        }

        /// <summary>Clamps a dwell request into the band AGENTS.md:239 allows.</summary>
        public static float ClampDwellSeconds(float dwellSeconds)
        {
            if (!IsFinite(dwellSeconds) || dwellSeconds < MinimumDwellSeconds)
                return MinimumDwellSeconds;

            return dwellSeconds > MaximumDwellSeconds ? MaximumDwellSeconds : dwellSeconds;
        }

        /// <summary>
        /// Derives a lower rail as a fraction below the upper rail, so a tuning
        /// change to the engage level cannot silently invert the two rails.
        /// </summary>
        public static float ResolveReleaseLevel01(float engageLevel01, float bandFraction01)
        {
            float engage = Saturate(engageLevel01);
            float fraction = Saturate(bandFraction01);
            return engage - (engage * fraction);
        }

        private static float Saturate(float value)
        {
            if (!IsFinite(value) || value < 0f)
                return 0f;

            return value > 1f ? 1f : value;
        }

        private static float SanitizeSeconds(float value)
        {
            if (!IsFinite(value) || value < 0f)
                return 0f;

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
