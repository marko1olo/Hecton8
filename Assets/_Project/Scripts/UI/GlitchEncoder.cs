using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Zero-allocation corruption encoder for diegetic HUD decay states.
    /// </summary>
    public static class GlitchEncoder
    {
        [System.ThreadStatic] private static char[] _stagingBuffer;

        /// <summary>
        /// Burst path for corrupted diegetic text stored as UTF-16 code units in caller-owned native memory.
        /// </summary>
        [BurstCompile]
        public struct DiegeticGlitchXorJob : IJobParallelFor
        {
            public NativeArray<ushort> Buffer;
            public int Length;
            public float Intensity01;
            public uint Seed;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)Length || !Buffer.IsCreated)
                    return;

                ushort current = Buffer[index];
                if (current <= 32u)
                    return;

                uint state = unchecked(Seed * 747796405u + 2891336453u + ((uint)index * 1664525u));
                state ^= state >> 13;
                state *= 1274126177u;
                int threshold = math.clamp((int)math.round(math.saturate(Intensity01) * 1023f), 0, 1023);
                if ((state & 1023u) > (uint)threshold)
                    return;

                Buffer[index] = (ushort)(current ^ (ushort)(1u << (int)((state >> 10) & 3u)));
            }
        }

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

        /// <summary>
        /// Managed mirror of the Burst XOR pass for char[] subtitle buffers that are already owned by UI code.
        /// </summary>
        public static void ApplyXorInPlace(char[] buffer, int length, float intensity, int seed)
        {
            int outputLength = Mathf.Max(0, length);
            if (buffer == null || outputLength == 0)
                return;

            float clampedIntensity = Mathf.Clamp01(intensity);
            uint seedValue = unchecked((uint)seed);
            int threshold = Mathf.Clamp(Mathf.RoundToInt(clampedIntensity * 1023f), 0, 1023);
            for (int i = 0; i < outputLength; i++)
            {
                char current = buffer[i];
                if (current <= ' ')
                    continue;

                uint state = unchecked(seedValue * 747796405u + 2891336453u + ((uint)i * 1664525u));
                state ^= state >> 13;
                state *= 1274126177u;
                if ((state & 1023u) > (uint)threshold)
                    continue;

                buffer[i] = (char)(current ^ (char)(1u << (int)((state >> 10) & 3u)));
            }
        }

        private static char ResolveDecayGlyph(char source, uint state)
        {
            return GlitchTable.ResolveGlyph(source, state);
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
