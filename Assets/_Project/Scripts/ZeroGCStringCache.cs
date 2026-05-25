namespace Hecton8.Core
{
    public static class ZeroGCStringCache
    {
        /// <summary>
        /// Legacy string API kept for compatibility. It returns the original stable reference because
        /// allocating an uppercase managed string would violate the zero-GC presentation contract.
        /// </summary>
        public static string GetStableReference(string input)
        {
            return input;
        }

        public static bool TryWriteUpperAscii(
            System.ReadOnlySpan<char> input,
            System.Span<char> destination,
            out int charsWritten)
        {
            charsWritten = 0;
            if (input.Length > destination.Length)
                return false;

            for (int i = 0; i < input.Length; i++)
            {
                char value = input[i];
                destination[i] = value >= 'a' && value <= 'z'
                    ? (char)(value - 32)
                    : value;
            }

            charsWritten = input.Length;
            return true;
        }
    }
}
