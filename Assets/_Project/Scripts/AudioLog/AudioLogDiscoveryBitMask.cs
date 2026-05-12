using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Fixed 1024-bit discovery mask for audio-log save persistence.
    /// </summary>
    public static class AudioLogDiscoveryBitMask
    {
        public const int MaxLogCount = 1024;
        public const int WordCount = 16;
        public const int ByteCount = 128;

        public static bool HasExpectedCapacity(long[] words)
        {
            return words != null && words.Length == WordCount;
        }

        public static void EnsureCapacity(ref long[] words)
        {
            if (HasExpectedCapacity(words))
                return;

            // COLD ALLOC: long[16] - 1024 packed audio-log discovery flags, exactly 128 bytes - owner: AudioLogDiscoveryBitMask
            words = new long[WordCount];
        }

        public static void Clear(long[] words)
        {
            if (!HasExpectedCapacity(words))
                return;

            for (int i = 0; i < WordCount; i++)
                words[i] = 0L;
        }

        public static bool HasAnySet(long[] words)
        {
            if (!HasExpectedCapacity(words))
                return false;

            for (int i = 0; i < WordCount; i++)
            {
                if (words[i] != 0L)
                    return true;
            }

            return false;
        }

        public static bool HasAllSet(long[] words, int maxExclusive)
        {
            if (!HasExpectedCapacity(words))
                return false;

            int safeMax = math.clamp(maxExclusive, 0, MaxLogCount);
            int fullWords = safeMax >> 6;
            int remainderBits = safeMax & 63;

            for (int i = 0; i < fullWords; i++)
            {
                if (unchecked((ulong)words[i]) != ulong.MaxValue)
                    return false;
            }

            if (remainderBits <= 0)
                return true;

            ulong remainderMask = (1UL << remainderBits) - 1UL;
            return (unchecked((ulong)words[fullWords]) & remainderMask) == remainderMask;
        }

        public static bool TryGetNextSetIndex(long[] words, int startIndex, int maxExclusive, out int index)
        {
            index = -1;
            if (!HasExpectedCapacity(words))
                return false;

            int safeMax = math.clamp(maxExclusive, 0, MaxLogCount);
            int safeStart = math.clamp(startIndex, 0, safeMax);
            if (safeStart >= safeMax)
                return false;

            int wordIndex = safeStart >> 6;
            int bitOffset = safeStart & 63;
            ulong word = unchecked((ulong)words[wordIndex]) & (ulong.MaxValue << bitOffset);

            while (wordIndex < WordCount)
            {
                if (word != 0UL)
                {
                    int candidate = (wordIndex << 6) + math.tzcnt(word);
                    if (candidate < safeMax)
                    {
                        index = candidate;
                        return true;
                    }

                    return false;
                }

                wordIndex++;
                word = wordIndex < WordCount ? unchecked((ulong)words[wordIndex]) : 0UL;
            }

            return false;
        }

        public static bool TryGetNextUnsetIndex(long[] words, int startIndex, int maxExclusive, out int index)
        {
            index = -1;
            if (!HasExpectedCapacity(words))
                return false;

            int safeMax = math.clamp(maxExclusive, 0, MaxLogCount);
            int safeStart = math.clamp(startIndex, 0, safeMax);
            if (safeStart >= safeMax)
                return false;

            int wordIndex = safeStart >> 6;
            int bitOffset = safeStart & 63;
            ulong word = ~unchecked((ulong)words[wordIndex]) & (ulong.MaxValue << bitOffset);

            while (wordIndex < WordCount)
            {
                if (word != 0UL)
                {
                    int candidate = (wordIndex << 6) + math.tzcnt(word);
                    if (candidate < safeMax)
                    {
                        index = candidate;
                        return true;
                    }

                    return false;
                }

                wordIndex++;
                word = wordIndex < WordCount ? ~unchecked((ulong)words[wordIndex]) : 0UL;
            }

            return false;
        }

        public static bool IsSet(long[] words, int index)
        {
            if (!HasExpectedCapacity(words) || (uint)index >= MaxLogCount)
                return false;

            int wordIndex = index >> 6;
            ulong bitMask = 1UL << (index & 63);
            return (unchecked((ulong)words[wordIndex]) & bitMask) != 0UL;
        }

        public static void Set(long[] words, int index)
        {
            if (!HasExpectedCapacity(words) || (uint)index >= MaxLogCount)
                return;

            int wordIndex = index >> 6;
            ulong bitMask = 1UL << (index & 63);
            words[wordIndex] = unchecked((long)(unchecked((ulong)words[wordIndex]) | bitMask));
        }
    }
}
