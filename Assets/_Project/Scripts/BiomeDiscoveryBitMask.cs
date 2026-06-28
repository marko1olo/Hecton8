using System.Collections.Generic;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Packed bit-word helpers for 108-biome discovery persistence.
    /// </summary>
    internal static class BiomeDiscoveryBitMask
    {
        public const int MinBiomeId = 1;
        public const int MaxBiomeId = 108;
        public const int InvalidBiomeId = -1;
        public const int WordBitCount = 64;
        public const int WordCount = ((MaxBiomeId - MinBiomeId + 1) + WordBitCount - 1) / WordBitCount;
        private const int BiomeCount = MaxBiomeId - MinBiomeId + 1;
        private const int LastWordValidBitCount = BiomeCount - ((WordCount - 1) * WordBitCount);
        private const ulong LastWordValidBitMask = (1UL << LastWordValidBitCount) - 1UL;

        public static bool IsValidBiomeId(int biomeId)
        {
            return biomeId >= MinBiomeId && biomeId <= MaxBiomeId;
        }

        public static bool HasExpectedCapacity(long[] words)
        {
            return words != null && words.Length >= WordCount;
        }

        public static void EnsureCapacity(ref long[] words)
        {
            if (HasExpectedCapacity(words))
                return;

            // COLD ALLOC: long[WordCount] - packed discovered biome persistence - owner: BiomeDiscoveryBitMask
            words = new long[WordCount];
        }

        public static bool HasAnySet(long[] words)
        {
            if (words == null)
                return false;

            int wordCount = words.Length < WordCount ? words.Length : WordCount;
            for (int i = 0; i < wordCount; i++)
            {
                if (SanitizeWord(i, words[i]) != 0L)
                    return true;
            }

            return false;
        }

        public static bool Contains(long[] words, int biomeId)
        {
            if (!IsValidBiomeId(biomeId) || words == null)
                return false;

            int zeroBasedBiomeIndex = biomeId - MinBiomeId;
            int wordIndex = zeroBasedBiomeIndex >> 6;
            if (wordIndex >= words.Length)
                return false;

            int bitIndex = zeroBasedBiomeIndex & (WordBitCount - 1);
            ulong mask = 1UL << bitIndex;
            return (((ulong)words[wordIndex]) & mask) != 0UL;
        }

        public static void Pack(HashSet<int> discoveredBiomeIds, long[] words)
        {
            if (words == null)
                return;

            Clear(words);

            if (discoveredBiomeIds == null)
                return;

            HashSet<int>.Enumerator biomeEnumerator = discoveredBiomeIds.GetEnumerator();
            while (biomeEnumerator.MoveNext())
            {
                int biomeId = biomeEnumerator.Current;
                if (!IsValidBiomeId(biomeId))
                    continue;

                int zeroBasedBiomeIndex = biomeId - MinBiomeId;
                int wordIndex = zeroBasedBiomeIndex >> 6;
                int bitIndex = zeroBasedBiomeIndex & (WordBitCount - 1);
                ulong mask = 1UL << bitIndex;
                words[wordIndex] = (long)(((ulong)words[wordIndex]) | mask);
            }
        }

        public static void Unpack(long[] words, HashSet<int> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            if (words == null)
                return;

            for (int biomeId = MinBiomeId; biomeId <= MaxBiomeId; biomeId++)
            {
                if (Contains(words, biomeId))
                    destination.Add(biomeId);
            }
        }

        public static int ResolveFallbackLastDiscoveredId(long[] words)
        {
            if (words == null)
                return InvalidBiomeId;

            for (int biomeId = MinBiomeId; biomeId <= MaxBiomeId; biomeId++)
            {
                if (Contains(words, biomeId))
                    return biomeId;
            }

            return InvalidBiomeId;
        }

        public static long SanitizeWord(int wordIndex, long value)
        {
            if (wordIndex < 0 || wordIndex >= WordCount)
                return 0L;

            if (wordIndex != WordCount - 1)
                return value;

            return (long)(((ulong)value) & LastWordValidBitMask);
        }

        public static bool SanitizeWords(long[] words)
        {
            if (words == null)
                return false;

            int wordCount = words.Length < WordCount ? words.Length : WordCount;
            bool changed = false;
            for (int i = 0; i < wordCount; i++)
            {
                long sanitizedWord = SanitizeWord(i, words[i]);
                if (sanitizedWord == words[i])
                    continue;

                words[i] = sanitizedWord;
                changed = true;
            }

            return changed;
        }

        private static void Clear(long[] words)
        {
            for (int i = 0; i < words.Length; i++)
                words[i] = 0L;
        }
    
        #region JulesLink_BiomeDiscoveryBitmaskTracker
        private static void JulesLink_BiomeDiscoveryBitmaskTracker() { _ = typeof(Hecton8.PureLogic.Systems.BiomeDiscoveryBitmaskTracker); }
        #endregion
}
}
