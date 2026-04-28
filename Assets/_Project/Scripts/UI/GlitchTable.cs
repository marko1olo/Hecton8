namespace Hecton8.UI
{
    /// <summary>
    /// Binary substitution ledger for deterministic zero-allocation HUD decay.
    /// Mirrors Assets/_Project/Data/UI/GlitchTable.bytes so the hot path never has to touch TextAsset IO.
    /// </summary>
    internal static class GlitchTable
    {
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
    }
}
