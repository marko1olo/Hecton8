using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation corruption encoder for diegetic HUD decay states.
    /// </summary>
    public static class GlitchEncoder
    {
        private static readonly char[] s_decayGlyphs = { '#', '%', '&', '/', '?', '+', '=', '*', 'X', '0' };
        [System.ThreadStatic] private static char[] _stagingBuffer;

        /// <summary>
        /// Applies deterministic glyph decay into a thread-local staging buffer.
        /// </summary>
        public static void ApplyDecay(char[] source, int length, float intensity, int seed, out char[] buffer, out int outputLength)
        {
            outputLength = Mathf.Max(0, length);
            if (source == null || outputLength == 0)
            {
                buffer = source;
                outputLength = 0;
                return;
            }

            buffer = GetBuffer(outputLength);
            float clampedIntensity = Mathf.Clamp01(intensity);
            uint state = unchecked((uint)seed * 747796405u + 2891336453u);
            int decayThreshold = Mathf.Clamp(Mathf.RoundToInt(clampedIntensity * 1023f), 0, 1023);

            for (int i = 0; i < outputLength; i++)
            {
                char current = source[i];
                if (current <= ' ')
                {
                    buffer[i] = current;
                    continue;
                }

                state = unchecked(state * 1664525u + 1013904223u + (uint)(i * 31 + 17));
                if ((state & 1023u) > decayThreshold)
                {
                    buffer[i] = current;
                    continue;
                }

                buffer[i] = ResolveDecayGlyph(current, state);
            }
        }

        /// <summary>
        /// Applies deterministic glyph decay directly into the caller-owned buffer.
        /// </summary>
        public static void ApplyDecayInPlace(char[] buffer, int length, float intensity, int seed)
        {
            int outputLength = Mathf.Max(0, length);
            if (buffer == null || outputLength == 0)
                return;

            float clampedIntensity = Mathf.Clamp01(intensity);
            uint state = unchecked((uint)seed * 747796405u + 2891336453u);
            int decayThreshold = Mathf.Clamp(Mathf.RoundToInt(clampedIntensity * 1023f), 0, 1023);

            for (int i = 0; i < outputLength; i++)
            {
                char current = buffer[i];
                if (current <= ' ')
                    continue;

                state = unchecked(state * 1664525u + 1013904223u + (uint)(i * 31 + 17));
                if ((state & 1023u) > decayThreshold)
                    continue;

                buffer[i] = ResolveDecayGlyph(current, state);
            }
        }

        private static char ResolveDecayGlyph(char source, uint state)
        {
            if (source >= '0' && source <= '9')
                return (char)('0' + (state % 10u));

            return s_decayGlyphs[state % (uint)s_decayGlyphs.Length];
        }

        private static char[] GetBuffer(int requiredLength)
        {
            char[] buffer = _stagingBuffer;
            if (buffer != null && buffer.Length >= requiredLength)
                return buffer;

            int capacity = 128;
            while (capacity < requiredLength)
                capacity <<= 1;

            _stagingBuffer = new char[capacity]; // COLD ALLOC: char[capacity] — thread-local glitch staging buffer — owner: GlitchEncoder
            return _stagingBuffer;
        }
    }
}
