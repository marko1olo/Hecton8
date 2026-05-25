// ============================================================================
// HECTON-8 - HudNumericStringCache.cs
// Zero-GC numeric char cache for HUD and screen markers.
// ============================================================================

using System;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Prepares short numeric char buffers so HUD systems do not allocate strings in hot paths.
    /// </summary>
    public static class HudNumericStringCache
    {
        /// <summary>
        /// Maximum integer value guaranteed by the cache.
        /// </summary>
        public const int MaxIntegerValue = 5000;

        /// <summary>
        /// Cached char buffers from <c>0</c> to <see cref="MaxIntegerValue"/>.
        /// </summary>
        public static readonly char[][] IntChars = BuildIntChars();

        public static ReadOnlySpan<char> GetIntSpan(int value)
        {
            int clamped = Math.Clamp(value, 0, MaxIntegerValue);
            char[] buffer = IntChars[clamped];
            return buffer == null ? ReadOnlySpan<char>.Empty : buffer.AsSpan();
        }

        private static char[][] BuildIntChars()
        {
            char[][] values = new char[MaxIntegerValue + 1][];
            char[] digits = new char[16]; // COLD ALLOC: numeric cache staging buffer - owner: HudNumericStringCache
            for (int i = 0; i <= MaxIntegerValue; i++)
            {
                if (!ZeroGCFormatter.TryWriteInt(i, digits.AsSpan(), out int length))
                    length = 0;

                char[] entry = new char[length]; // COLD ALLOC: per-number HUD numeric char cache - owner: HudNumericStringCache
                Array.Copy(digits, entry, length);
                values[i] = entry;
            }

            return values;
        }
    }
}
