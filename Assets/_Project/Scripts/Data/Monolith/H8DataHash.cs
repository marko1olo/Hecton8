using System;
using System.Runtime.CompilerServices;

namespace Hecton8.Data
{
    /// <summary>
    /// Build-time canonical FNV-1a hashing for Data Monolith IDs.
    /// Runtime systems consume only the resulting numeric IDs.
    /// </summary>
    public static class H8DataHash
    {
        /// <summary>FNV-1a 32-bit offset basis.</summary>
        public const uint Fnv1A32Offset = 2166136261u;

        /// <summary>FNV-1a 32-bit prime.</summary>
        public const uint Fnv1A32Prime = 16777619u;

        /// <summary>
        /// Computes the canonical 32-bit FNV-1a hash for an authored ID.
        /// </summary>
        /// <param name="value">Canonical content ID. IDs are expected to be ASCII.</param>
        /// <returns>Non-zero hash. Empty input returns zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeFnv1A32(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = Fnv1A32Offset;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if ((uint)(c - 'A') <= 25u)
                        c = (char)(c + 32);

                    hash ^= (byte)c;
                    hash *= Fnv1A32Prime;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        /// <summary>
        /// Computes the canonical 32-bit FNV-1a hash without requiring MemoryExtensions.AsSpan().
        /// </summary>
        /// <param name="value">Canonical content ID. IDs are expected to be ASCII.</param>
        /// <returns>Non-zero hash. Null or empty input returns zero.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ComputeFnv1A32(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            unchecked
            {
                uint hash = Fnv1A32Offset;
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if ((uint)(c - 'A') <= 25u)
                        c = (char)(c + 32);

                    hash ^= (byte)c;
                    hash *= Fnv1A32Prime;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        /// <summary>
        /// Mixes four recipe ingredient hashes into a deterministic 128-bit bitset.
        /// </summary>
        /// <param name="hash">Ingredient hash.</param>
        /// <param name="mask0">Bits 0-31.</param>
        /// <param name="mask1">Bits 32-63.</param>
        /// <param name="mask2">Bits 64-95.</param>
        /// <param name="mask3">Bits 96-127.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddHashToRecipeMask(uint hash, ref uint mask0, ref uint mask1, ref uint mask2, ref uint mask3)
        {
            if (hash == 0u)
                return;

            uint bitIndex = hash & 127u;
            uint bit = 1u << (int)(bitIndex & 31u);
            switch (bitIndex >> 5)
            {
                case 0u:
                    mask0 |= bit;
                    break;
                case 1u:
                    mask1 |= bit;
                    break;
                case 2u:
                    mask2 |= bit;
                    break;
                default:
                    mask3 |= bit;
                    break;
            }
        }

        /// <summary>
        /// Mixes one authored hash into a 128-bit recipe bitmask stored as two ulong lanes.
        /// </summary>
        /// <param name="hash">Ingredient hash.</param>
        /// <param name="mask0">Bits 0-63.</param>
        /// <param name="mask1">Bits 64-127.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddHashToRecipeMask(uint hash, ref ulong mask0, ref ulong mask1)
        {
            if (hash == 0u)
                return;

            uint bitIndex = hash & 127u;
            ulong bit = 1UL << (int)(bitIndex & 63u);
            if (bitIndex < 64u)
                mask0 |= bit;
            else
                mask1 |= bit;
        }
    }
}
