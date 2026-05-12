using System.Runtime.CompilerServices;

namespace Hecton8.Core
{
    /// <summary>
    /// Deterministic LCG seed composition for replayable HECTON-8 runtime streams.
    /// </summary>
    public static class DeterministicReplaySeed
    {
        /// <summary>Numerical subject hash used for MathGuard-triggered replay dumps.</summary>
        public const uint MathGuardSubjectHash = 0x4D475244u;

        /// <summary>LCG multiplier used by the deterministic replay stream.</summary>
        public const uint LcgMultiplier = 1664525u;

        /// <summary>LCG increment used by the deterministic replay stream.</summary>
        public const uint LcgIncrement = 1013904223u;

        private const uint GoldenRatioMix = 0x9E3779B9u;
        private const uint EntityMix = 747796405u;
        private const uint NonZeroFallbackSeed = 0xA341316Cu;

        /// <summary>
        /// Composes a deterministic non-zero seed from session, frame, subject, and stream hashes.
        /// </summary>
        /// <param name="sessionSeed">Session or world seed.</param>
        /// <param name="currentFrameIndex">Current deterministic frame index.</param>
        /// <param name="subjectHash">Entity or system subject hash.</param>
        /// <param name="streamHash">Random stream discriminator.</param>
        /// <returns>Non-zero LCG seed.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComposeSeed(
            uint sessionSeed,
            uint currentFrameIndex,
            uint subjectHash,
            uint streamHash)
        {
            uint seed = sessionSeed ^ GoldenRatioMix;
            seed = unchecked((seed * LcgMultiplier) + LcgIncrement + currentFrameIndex);
            seed ^= unchecked(subjectHash * EntityMix);
            seed = unchecked((seed * LcgMultiplier) + LcgIncrement + streamHash);
            seed ^= currentFrameIndex << 16;
            return seed != 0u ? seed : NonZeroFallbackSeed;
        }

        /// <summary>
        /// Advances a deterministic LCG state by one step.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Next(uint state)
        {
            return unchecked((state * LcgMultiplier) + LcgIncrement);
        }

        /// <summary>
        /// Advances a deterministic LCG state and returns a [0, 1) float.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Next01(ref uint state)
        {
            state = Next(state);
            return (state >> 8) * (1f / 16777216f);
        }
    }
}
