using System;

namespace Hecton.Localization
{
    /// <summary>
    /// Zero-allocation RTL logical text bridge for localized labels.
    /// TextMeshPro owns bidi shaping through TMP_Text.isRightToLeftText.
    /// </summary>
    public static class RTLProcessor
    {
        private const int DefaultCapacity = 1024;

        [ThreadStatic] private static char[] s_visualBuffer;

        public static ReadOnlySpan<char> ToVisualOrder(ReadOnlySpan<char> logical)
        {
            return logical;
        }

        public static bool TryGetVisualBuffer(ReadOnlySpan<char> logical, out char[] buffer, out int length)
        {
            if (logical.Length == 0)
            {
                buffer = GetBuffer(1);
                length = 0;
                return true;
            }

            buffer = GetBuffer(logical.Length);
            logical.CopyTo(buffer.AsSpan(0, logical.Length));
            length = logical.Length;
            return true;
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

    }
}
