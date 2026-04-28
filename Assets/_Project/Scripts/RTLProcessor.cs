using System;

namespace Hecton.Localization
{
    /// <summary>
    /// Zero-allocation RTL visual-order processor for Arabic and Hebrew localized labels.
    /// </summary>
    public static class RTLProcessor
    {
        private const int DefaultCapacity = 1024;

        [ThreadStatic] private static char[] s_visualBuffer;

        public static ReadOnlySpan<char> ToVisualOrder(ReadOnlySpan<char> logical)
        {
            if (logical.Length == 0)
                return ReadOnlySpan<char>.Empty;

            char[] buffer = GetBuffer(logical.Length);
            int writeIndex = 0;
            int readIndex = logical.Length - 1;
            while (readIndex >= 0)
            {
                int clusterStart = readIndex;
                while (clusterStart > 0 && IsCombiningMark(logical[clusterStart]))
                    clusterStart--;

                int clusterLength = readIndex - clusterStart + 1;
                logical.Slice(clusterStart, clusterLength).CopyTo(buffer.AsSpan(writeIndex, clusterLength));
                writeIndex += clusterLength;
                readIndex = clusterStart - 1;
            }

            return buffer.AsSpan(0, writeIndex);
        }

        public static bool TryGetVisualBuffer(ReadOnlySpan<char> logical, out char[] buffer, out int length)
        {
            if (logical.Length == 0)
            {
                buffer = GetBuffer(1);
                length = 0;
                return true;
            }

            ReadOnlySpan<char> visual = ToVisualOrder(logical);
            buffer = s_visualBuffer;
            length = visual.Length;
            return buffer != null;
        }

        private static char[] GetBuffer(int requiredLength)
        {
            if (requiredLength <= 0)
                requiredLength = 1;

            char[] buffer = s_visualBuffer;
            if (buffer != null && buffer.Length >= requiredLength)
                return buffer;

            int capacity = DefaultCapacity;
            while (capacity < requiredLength)
                capacity <<= 1;

            // COLD ALLOC: char[capacity] — thread-local RTL staging buffer — owner: RTLProcessor
            buffer = new char[capacity];
            s_visualBuffer = buffer;
            return buffer;
        }

        private static bool IsCombiningMark(char value)
        {
            return IsArabicCombiningMark(value) || IsHebrewCombiningMark(value);
        }

        private static bool IsArabicCombiningMark(char value)
        {
            return (value >= '\u0610' && value <= '\u061A') ||
                   (value >= '\u064B' && value <= '\u065F') ||
                   value == '\u0670' ||
                   (value >= '\u06D6' && value <= '\u06ED');
        }

        private static bool IsHebrewCombiningMark(char value)
        {
            return (value >= '\u0591' && value <= '\u05BD') ||
                   value == '\u05BF' ||
                   (value >= '\u05C1' && value <= '\u05C2') ||
                   (value >= '\u05C4' && value <= '\u05C5') ||
                   value == '\u05C7';
        }
    }
}
