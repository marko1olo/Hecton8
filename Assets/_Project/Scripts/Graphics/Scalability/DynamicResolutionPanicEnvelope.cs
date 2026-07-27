using System;

namespace Hecton8.Graphics.Scalability
{
    /// <summary>
    /// Continuous panic-collapse envelope for the dynamic-resolution governor.
    ///
    /// Unity-free by construction: only <see cref="System.MathF"/> and float primitives are used, so the
    /// curve can be executed and asserted outside the editor.
    ///
    /// Why this exists: <c>ThermalDynamicResolutionAdapter</c> previously took a hard boolean branch
    /// (<c>frameTimeEwmaMs &gt;= 33</c>) that snapped render scale to the tier floor in one frame and
    /// bypassed both temporal smoothers. The branch had one threshold and no release band, so the loop
    /// self-oscillated: collapse -> frame time falls below 33 -> scale ramps back up -> frame time rises
    /// past 33 -> collapse. AGENTS.md:231 rejects binary quality switches, and AGENTS.md:239 requires
    /// hysteresis on any scalability switch with a minimum band of 2-3 seconds.
    ///
    /// The envelope replaces that branch with a scalar authority in [0,1]:
    /// - it is exactly 0 at the onset frame time, so there is no discontinuity where the old branch fired;
    /// - it rises smoothly to 1 at the saturation frame time, so a genuine collapse still responds fast;
    /// - it decays no faster than <c>1 / releaseSeconds</c> per second, which is the hysteresis band;
    /// - applied through <see cref="ApplyCollapse"/> it can only lower a scale, never raise one, so
    ///   recovery always stays on the smoothed path.
    /// </summary>
    public static class DynamicResolutionPanicEnvelope
    {
        /// <summary>Frame time in milliseconds at which panic authority leaves zero.</summary>
        public const float DefaultOnsetFrameTimeMs = 33.0f;

        /// <summary>Frame time in milliseconds at which panic authority reaches full collapse.</summary>
        public const float DefaultSaturationFrameTimeMs = 50.0f;

        /// <summary>
        /// Seconds a saturated envelope needs to fall back to zero. 2.5 s sits inside the 2-3 s
        /// hysteresis band required by AGENTS.md:239.
        /// </summary>
        public const float DefaultReleaseSeconds = 2.5f;

        /// <summary>Homeostasis pressure level that represents a full emergency collapse.</summary>
        public const float EmergencyPressureLevel = 3f;

        /// <summary>
        /// Instantaneous collapse demand from the two emergency inputs the governor owns: the smoothed
        /// frame time and the homeostasis pressure level. Returns the stronger of the two.
        /// </summary>
        public static float ResolveInstantAuthority01(
            float frameTimeEwmaMs,
            float onsetFrameTimeMs,
            float saturationFrameTimeMs,
            float pressureLevel)
        {
            float onset = IsFinite(onsetFrameTimeMs) ? onsetFrameTimeMs : DefaultOnsetFrameTimeMs;
            float saturation = IsFinite(saturationFrameTimeMs) ? saturationFrameTimeMs : DefaultSaturationFrameTimeMs;
            if (saturation <= onset)
                saturation = onset + 1f;

            float frameAuthority = SmoothRange01(frameTimeEwmaMs, onset, saturation);
            float pressureAuthority = SmoothRange01(pressureLevel, EmergencyPressureLevel - 1f, EmergencyPressureLevel);
            return frameAuthority >= pressureAuthority ? frameAuthority : pressureAuthority;
        }

        /// <summary>
        /// Advances the latched envelope one tick. Attack is immediate because the instant term is already
        /// continuous in its input; release is rate limited to <paramref name="releaseSeconds"/>, which is
        /// what removes the oscillation.
        ///
        /// Non-finite handling is deliberately asymmetric. A garbage collapse demand resolves to zero -
        /// dropping the player's resolution to the tier floor on a NaN is a visible harm, and the frame
        /// budget is still defended by the governor's separate continuous frame-pressure term. A garbage
        /// delta resolves to zero elapsed time, so it can never release the latch early.
        /// </summary>
        public static float Advance(
            float previousAuthority01,
            float instantAuthority01,
            float deltaSeconds,
            float releaseSeconds)
        {
            float previous = Saturate01(previousAuthority01, 0f);
            float instant = Saturate01(instantAuthority01, 0f);
            float safeRelease = IsFinite(releaseSeconds) && releaseSeconds > 0f
                ? releaseSeconds
                : DefaultReleaseSeconds;
            float safeDelta = IsFinite(deltaSeconds) && deltaSeconds > 0f ? deltaSeconds : 0f;
            float released = previous - (safeDelta / safeRelease);
            float advanced = instant >= released ? instant : released;
            return Saturate01(advanced, instant);
        }

        /// <summary>
        /// Blends a smoothed render scale toward the collapse floor by the current authority, clamped so
        /// the envelope can only ever reduce the scale. At authority 1 with a lower floor this reproduces
        /// the old instant drop; at authority 0 it is a no-op; while recovering it returns the smoothed
        /// value untouched.
        /// </summary>
        public static float ApplyCollapse(float smoothedScale, float collapseScale, float authority01)
        {
            if (!IsFinite(smoothedScale))
                return IsFinite(collapseScale) ? collapseScale : 0f;

            if (!IsFinite(collapseScale))
                return smoothedScale;

            float authority = Saturate01(authority01, 0f);
            float blended = smoothedScale + (collapseScale - smoothedScale) * authority;
            if (!IsFinite(blended))
                return smoothedScale;

            return blended < smoothedScale ? blended : smoothedScale;
        }

        /// <summary>
        /// Minimum wall-clock seconds the envelope needs to fall from <paramref name="fromAuthority01"/>
        /// to <paramref name="toAuthority01"/> with no further collapse demand. Callers use it to state
        /// the realised hysteresis band.
        /// </summary>
        public static float ResolveReleaseSeconds(
            float fromAuthority01,
            float toAuthority01,
            float releaseSeconds)
        {
            float from = Saturate01(fromAuthority01, 0f);
            float to = Saturate01(toAuthority01, 0f);
            if (to >= from)
                return 0f;

            float safeRelease = IsFinite(releaseSeconds) && releaseSeconds > 0f
                ? releaseSeconds
                : DefaultReleaseSeconds;
            return (from - to) * safeRelease;
        }

        private static float SmoothRange01(float value, float edge0, float edge1)
        {
            if (!IsFinite(value))
                return 0f;

            float width = edge1 - edge0;
            if (!IsFinite(width) || width <= 0f)
                width = 0.0001f;

            return Smooth01((value - edge0) / width);
        }

        private static float Smooth01(float value)
        {
            float t = Saturate01(value, 0f);
            return t * t * (3f - 2f * t);
        }

        private static float Saturate01(float value, float fallback)
        {
            if (!IsFinite(value))
                return fallback;

            if (value < 0f)
                return 0f;

            return value > 1f ? 1f : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
