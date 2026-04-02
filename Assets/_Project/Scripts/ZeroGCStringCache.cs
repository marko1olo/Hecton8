namespace Hecton8.Core
{
    /// <summary>
    /// Reuses uppercase string projections for frequently repeated UI labels.
    /// </summary>
    public static class ZeroGCStringCache
    {
        private const int CacheSize = 64;

        private static readonly string[] _sourceCache = new string[CacheSize];
        private static readonly string[] _upperCache = new string[CacheSize];

        /// <summary>
        /// Returns an uppercase invariant projection and reuses a cached value when the source string repeats.
        /// </summary>
        /// <param name="input">Source string to convert.</param>
        /// <returns>Cached uppercase projection for repeated inputs, or a freshly created uppercase string on miss.</returns>
        public static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int hash = (input.GetHashCode() & int.MaxValue) % CacheSize;
            string cachedSource = _sourceCache[hash];
            if (!string.IsNullOrEmpty(cachedSource) && string.Equals(cachedSource, input, System.StringComparison.Ordinal))
                return _upperCache[hash];

            string upperValue = input.ToUpperInvariant();
            _sourceCache[hash] = input;
            _upperCache[hash] = upperValue;
            return upperValue;
        }
    }
}
