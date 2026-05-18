namespace Hecton8.UI
{
    /// <summary>
    /// Binary substitution ledger for deterministic zero-allocation HUD decay.
    /// Mirrors Assets/_Project/Data/UI/GlitchTable.bytes so the hot path never has to touch TextAsset IO.
    /// </summary>
    internal static class GlitchTable
    {
        public const int ByteLength = 64;
        public const int EmergencyMockLength = 16;

        private static readonly byte[] s_glyphBytes =
        {
            (byte)'#', (byte)'%', (byte)'&', (byte)'/', (byte)'?', (byte)'+', (byte)'=', (byte)'*',
            (byte)'X', (byte)'0', (byte)'1', (byte)'A', (byte)'E', (byte)'H', (byte)'K', (byte)'M',
            (byte)'N', (byte)'T', (byte)'V', (byte)'W', (byte)'Y', (byte)'Z', (byte)'[', (byte)']',
            (byte)'{', (byte)'}', (byte)'<', (byte)'>', (byte)'|', (byte)'!', (byte)';', (byte)':',
            (byte)'#', (byte)'%', (byte)'&', (byte)'/', (byte)'?', (byte)'+', (byte)'=', (byte)'*',
            (byte)'X', (byte)'0', (byte)'1', (byte)'A', (byte)'E', (byte)'H', (byte)'K', (byte)'M',
            (byte)'N', (byte)'T', (byte)'V', (byte)'W', (byte)'Y', (byte)'Z', (byte)'[', (byte)']',
            (byte)'{', (byte)'}', (byte)'<', (byte)'>', (byte)'|', (byte)'!', (byte)';', (byte)':'
        };

        private static readonly byte[] s_digitBytes =
        {
            (byte)'0', (byte)'1', (byte)'2', (byte)'3',
            (byte)'4', (byte)'5', (byte)'6', (byte)'7',
            (byte)'8', (byte)'9', (byte)'0', (byte)'7',
            (byte)'3', (byte)'8', (byte)'2', (byte)'9'
        };

        private const int GlyphMask = 63;
        private const int DigitMask = 15;

        public static char ResolveGlyph(char source, uint state)
        {
            if (source >= '0' && source <= '9')
                return (char)s_digitBytes[(state + source) & DigitMask];

            return (char)s_glyphBytes[(state + source) & GlyphMask];
        }

        public static unsafe char ResolveGlyph(char source, uint state, byte* bytes, int length)
        {
            if (bytes == null || length <= 0)
                return ResolveGlyph(source, state);

            int index = (int)((state + source) & 0x7FFFFFFFu);
            if ((length & (length - 1)) == 0)
                index &= length - 1;
            else
                index %= length;

            byte value = bytes[index];
            return value >= 32 && value <= 126 ? (char)value : ResolveGlyph(source, state);
        }

        public static unsafe void CopyEmbeddedGlyphsTo(byte* destination, int length)
        {
            if (destination == null || length <= 0)
                return;

            int count = length < ByteLength ? length : ByteLength;
            for (int i = 0; i < count; i++)
                destination[i] = s_glyphBytes[i & GlyphMask];
        }

        public static unsafe bool IsValidGlyphTable(byte* bytes, int length)
        {
            if (bytes == null || length <= 0)
                return false;

            int count = length < ByteLength ? length : ByteLength;
            for (int i = 0; i < count; i++)
            {
                byte value = bytes[i];
                if (value < 33 || value > 126 || value == (byte)'"')
                    return false;
            }

            return true;
        }

        public static unsafe void GenerateEmergencyMockGlitchTable(byte* destination, int length)
        {
            if (destination == null || length <= 0)
                return;

            for (int i = 0; i < length; i++)
            {
                switch (i & 15)
                {
                    case 0: destination[i] = (byte)'@'; break;
                    case 1: destination[i] = (byte)'#'; break;
                    case 2: destination[i] = (byte)'$'; break;
                    case 3: destination[i] = (byte)'%'; break;
                    case 4: destination[i] = (byte)'&'; break;
                    case 5: destination[i] = (byte)'?'; break;
                    case 6: destination[i] = (byte)'!'; break;
                    case 7: destination[i] = (byte)'*'; break;
                    case 8: destination[i] = (byte)'X'; break;
                    case 9: destination[i] = (byte)'0'; break;
                    case 10: destination[i] = (byte)'1'; break;
                    case 11: destination[i] = (byte)'/'; break;
                    case 12: destination[i] = (byte)'+'; break;
                    case 13: destination[i] = (byte)'='; break;
                    case 14: destination[i] = (byte)'|'; break;
                    default: destination[i] = (byte)';'; break;
                }
            }
        }
    }
}
