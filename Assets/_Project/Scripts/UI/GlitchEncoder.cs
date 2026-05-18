using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        /// <summary>
        /// Burst path for corrupted diegetic text stored as UTF-16 code units in caller-owned native memory.
        /// </summary>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        public struct DiegeticGlitchXorJob : IJobParallelFor
        {
            [NoAlias]
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
        /// Legacy compatibility path. It never allocates; callers that need corruption must provide a destination buffer.
        /// </summary>
        public static void ApplyDecay(char[] source, int length, float intensity, int seed, out char[] buffer, out int outputLength)
        {
            if (source == null)
            {
                buffer = source;
                outputLength = 0;
                return;
            }

            buffer = source;
            outputLength = math.clamp(length, 0, source.Length);
        }

        /// <summary>
        /// Applies deterministic glyph decay from a source buffer into a caller-owned destination buffer.
        /// </summary>
        public static void ApplyDecayToBuffer(char[] source, int length, char[] destination, float intensity, int seed, out int outputLength)
        {
            unsafe
            {
                ApplyDecayToBuffer(source, length, destination, intensity, seed, null, 0, 0, out outputLength);
            }
        }

        /// <summary>
        /// Applies deterministic glyph decay through a caller-supplied resident GlitchTable.bytes pointer.
        /// </summary>
        public static unsafe void ApplyDecayToBuffer(
            char[] source,
            int length,
            char[] destination,
            float intensity,
            int seed,
            byte* glitchTableBytes,
            int tableLength,
            int readabilityPrefixChars,
            out int outputLength)
        {
            outputLength = 0;
            if (source == null || destination == null)
                return;

            int safeLength = math.clamp(length, 0, math.min(source.Length, destination.Length));
            if (safeLength == 0)
                return;

            float clampedIntensity = math.saturate(math.isfinite(intensity) ? intensity : 0f);
            uint state = unchecked((uint)seed * 747796405u + 2891336453u);
            uint decayThreshold = (uint)math.clamp((int)math.round(clampedIntensity * 1023f), 0, 1023);

            for (int i = 0; i < safeLength; i++)
            {
                char current = source[i];
                if (current <= ' ')
                {
                    destination[i] = current;
                    continue;
                }

                if (i < readabilityPrefixChars && clampedIntensity < 0.9f)
                {
                    destination[i] = current;
                    continue;
                }

                state = unchecked(state * 1664525u + 1013904223u + (uint)(i * 31 + 17));
                if ((state & 1023u) > decayThreshold)
                {
                    destination[i] = current;
                    continue;
                }

                destination[i] = ResolveDecayGlyph(current, state, glitchTableBytes, tableLength);
            }

            outputLength = safeLength;
        }

        /// <summary>
        /// Applies deterministic glyph decay directly into the caller-owned buffer.
        /// </summary>
        public static void ApplyDecayInPlace(char[] buffer, int length, float intensity, int seed)
        {
            if (buffer == null)
                return;

            int outputLength = Mathf.Clamp(length, 0, buffer.Length);
            if (outputLength == 0)
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
        /// Applies deterministic glyph decay directly into a caller-owned native Babel span.
        /// </summary>
        public static unsafe void ApplyDecayInPlace(Span<char> buffer, int length, float intensity, int seed, byte* glitchTableBytes, int tableLength, int readabilityPrefixChars)
        {
            int outputLength = math.clamp(length, 0, buffer.Length);
            if (outputLength == 0)
                return;

            fixed (char* bufferPtr = buffer)
            {
                ApplyDecayInPlace(bufferPtr, outputLength, intensity, (uint)seed, glitchTableBytes, tableLength, readabilityPrefixChars);
            }
        }

        /// <summary>
        /// Applies deterministic glyph decay directly into unmanaged UTF-16 code units.
        /// </summary>
        public static unsafe void ApplyDecayInPlace(char* buffer, int length, float intensity, uint seed, byte* glitchTableBytes, int tableLength, int readabilityPrefixChars)
        {
            if (buffer == null || length <= 0)
                return;

            float clampedIntensity = math.saturate(math.isfinite(intensity) ? intensity : 0f);
            uint state = unchecked(seed * 747796405u + 2891336453u);
            uint threshold = (uint)math.clamp((int)math.round(clampedIntensity * 1023f), 0, 1023);
            int safeReadabilityPrefix = math.max(0, readabilityPrefixChars);

            for (int i = 0; i < length; i++)
            {
                char current = buffer[i];
                if (current <= ' ' || current > 126)
                    continue;

                if (i < safeReadabilityPrefix && clampedIntensity < 0.9f)
                    continue;

                state = unchecked(state * 1664525u + 1013904223u + (uint)(i * 31 + 17));
                if ((state & 1023u) > threshold)
                    continue;

                buffer[i] = ResolveDecayGlyph(current, state, glitchTableBytes, tableLength);
            }
        }

        /// <summary>
        /// Managed mirror of the Burst XOR pass for char[] subtitle buffers that are already owned by UI code.
        /// </summary>
        public static void ApplyXorInPlace(char[] buffer, int length, float intensity, int seed)
        {
            if (buffer == null)
                return;

            int outputLength = Mathf.Clamp(length, 0, buffer.Length);
            if (outputLength == 0)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe char ResolveDecayGlyph(char source, uint state, byte* glitchTableBytes, int tableLength)
        {
            return GlitchTable.ResolveGlyph(source, state, glitchTableBytes, tableLength);
        }
    }
}
