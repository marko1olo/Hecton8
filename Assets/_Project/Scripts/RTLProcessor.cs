using System;

namespace Hecton.Localization
{
    /// <summary>
    /// Zero-allocation RTL visual-order bridge for localized labels.
    /// </summary>
    public static class RTLProcessor
    {
        private const int DefaultCapacity = 1024;

        [ThreadStatic] private static char[] s_visualBuffer;

        public static ReadOnlySpan<char> ToVisualOrder(ReadOnlySpan<char> logical)
        {
            if (logical.Length == 0)
                return ReadOnlySpan<char>.Empty;

            char[] buffer = EnsureBuffer(logical.Length);
            logical.CopyTo(buffer.AsSpan(0, logical.Length));
            TryReverseVisualOrderInPlace(buffer, logical.Length);
            return buffer.AsSpan(0, logical.Length);
        }

        public static bool TryGetVisualBuffer(ReadOnlySpan<char> logical, out char[] buffer, out int length)
        {
            if (logical.Length == 0)
            {
                buffer = EnsureBuffer(1);
                length = 0;
                return true;
            }

            buffer = EnsureBuffer(logical.Length);
            logical.CopyTo(buffer.AsSpan(0, logical.Length));
            length = logical.Length;
            TryReverseVisualOrderInPlace(buffer, length);
            return true;
        }

        public static bool TryReverseVisualOrderInPlace(char[] buffer, int length)
        {
            if (buffer == null || length <= 1 || length > buffer.Length)
                return false;

            int left = 0;
            int right = length - 1;
            while (left < right)
            {
                char temp = buffer[left];
                buffer[left] = buffer[right];
                buffer[right] = temp;
                left++;
                right--;
            }

            return true;
        }

        private static char[] EnsureBuffer(int requiredLength)
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
