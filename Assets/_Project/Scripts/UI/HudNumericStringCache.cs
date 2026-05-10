// ============================================================================
// HECTON-8 — HudNumericStringCache.cs
// Obschiy zero-GC kesh chislovyh strok dlya HUD i ekrannyh markerov.
// ============================================================================

using System;
using Hecton8.Core;

namespace Hecton8.UI
{
    /// <summary>
    /// Predvaritelno podgotavlivaet korotkie chislovye stroki dlya HUD-sistem,
    /// chtoby ne sozdavat novye stroki v hot path.
    /// </summary>
    public static class HudNumericStringCache
    {
        /// <summary>
        /// Maksimalnoe znachenie, dlya kotorogo garantirovan gotovyy kesh.
        /// </summary>
        public const int MaxIntegerValue = 5000;

        /// <summary>
        /// Kesh strok ot <c>0</c> do <see cref="MaxIntegerValue"/>.
        /// </summary>
        public static readonly string[] IntStrings = BuildIntStrings();

        private static string[] BuildIntStrings()
        {
            string[] values = new string[MaxIntegerValue + 1];
            char[] digits = new char[16]; // COLD ALLOC: char[16] — numeric cache staging buffer — owner: HudNumericStringCache
            for (int i = 0; i <= MaxIntegerValue; i++)
            {
                if (!ZeroGCFormatter.TryWriteInt(i, digits.AsSpan(), out int length))
                    length = 0;

                values[i] = new string(digits, 0, length);
            }

            return values;
        }
    }
}
