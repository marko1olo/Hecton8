namespace Hecton8.Narrative
{
    /// <summary>
    /// Fixed 1024-bit discovery mask for audio-log save persistence.
    /// </summary>
    internal static class AudioLogDiscoveryBitMask
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

            // COLD ALLOC: long[16] — 1024 packed audio-log discovery flags, exactly 128 bytes — owner: AudioLogDiscoveryBitMask
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
