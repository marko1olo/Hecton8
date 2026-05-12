using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Hecton8.Tools
{
    /// <summary>
    /// Cheap deterministic haptic waveform evaluators for LRA/ERM motors.
    /// </summary>
    public static class HapticWaveformLibrary
    {
        /// <summary>
        /// Triangle waveform in 0..1, phase-shifted for immediate attack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateTriangle01(float elapsedSeconds, float frequencyHz)
        {
            float phase = ResolveFinitePhase(elapsedSeconds, frequencyHz) + 0.25f;
            float cycle = phase - math.floor(phase);
            return 1f - math.abs((cycle * 2f) - 1f);
        }

        /// <summary>
        /// Square waveform in 0..1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSquare01(float elapsedSeconds, float frequencyHz)
        {
            float cycle = ResolveFinitePhase(elapsedSeconds, frequencyHz);
            cycle -= math.floor(cycle);
            return cycle < 0.5f ? 1f : 0f;
        }

        /// <summary>
        /// Saw waveform in 0..1.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSaw01(float elapsedSeconds, float frequencyHz)
        {
            float cycle = ResolveFinitePhase(elapsedSeconds, frequencyHz);
            return cycle - math.floor(cycle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveFinitePhase(float elapsedSeconds, float frequencyHz)
        {
            float safeElapsed = math.isfinite(elapsedSeconds) ? math.max(0f, elapsedSeconds) : 0f;
            float safeFrequency = math.isfinite(frequencyHz) ? math.max(0f, frequencyHz) : 0f;
            return safeElapsed * safeFrequency;
        }
    }
}
