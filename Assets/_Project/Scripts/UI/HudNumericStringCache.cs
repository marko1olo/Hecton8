// ============================================================================
// HECTON-8 — HudNumericStringCache.cs
// Общий zero-GC кэш числовых строк для HUD и экранных маркеров.
// ============================================================================

namespace Hecton8.UI
{
    /// <summary>
    /// Предварительно подготавливает короткие числовые строки для HUD-систем,
    /// чтобы не создавать новые строки в hot path.
    /// </summary>
    public static class HudNumericStringCache
    {
        /// <summary>
        /// Максимальное значение, для которого гарантирован готовый кэш.
        /// </summary>
        public const int MaxIntegerValue = 5000;

        /// <summary>
        /// Кэш строк от <c>0</c> до <see cref="MaxIntegerValue"/>.
        /// </summary>
        public static readonly string[] IntStrings = BuildIntStrings();

        private static string[] BuildIntStrings()
        {
            string[] values = new string[MaxIntegerValue + 1];
            for (int i = 0; i <= MaxIntegerValue; i++)
                values[i] = i.ToString();

            return values;
        }
    }
}
