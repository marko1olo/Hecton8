namespace Hecton.Localization
{
    /// <summary>
    /// Locale-aware measurement conversion helper for HUD and beacon readouts.
    /// </summary>
    public static class LocalizedMeasurementFormatter
    {
        private const float MetersToFeet = 3.2808399f;

        /// <summary>
        /// True when the locale should default to right-to-left text flow.
        /// </summary>
        public static bool IsRightToLeft(GameLanguage language)
        {
            return LocalizationManager.IsRightToLeftLanguage(language);
        }

        /// <summary>
        /// Convert authored meter values to the preferred display unit for the locale.
        /// </summary>
        public static float ConvertDistanceMeters(float meters, GameLanguage language)
        {
            return UsesImperialUnits(language) ? meters * MetersToFeet : meters;
        }

        /// <summary>
        /// Convert authored Celsius values to the preferred display unit for the locale.
        /// </summary>
        public static float ConvertTemperatureCelsius(float celsius, GameLanguage language)
        {
            return UsesImperialUnits(language) ? (celsius * 9f / 5f) + 32f : celsius;
        }

        /// <summary>
        /// Resolve the localization key for the active distance unit.
        /// </summary>
        public static string GetDistanceUnitKey(GameLanguage language)
        {
            return UsesImperialUnits(language)
                ? LocalizationKeys.HUD_UNIT_FEET
                : LocalizationKeys.HUD_UNIT_METERS;
        }

        /// <summary>
        /// Resolve the localization key for the active temperature unit.
        /// </summary>
        public static string GetTemperatureUnitKey(GameLanguage language)
        {
            return UsesImperialUnits(language)
                ? LocalizationKeys.HUD_UNIT_FAHRENHEIT
                : LocalizationKeys.HUD_UNIT_CELSIUS;
        }

        /// <summary>
        /// Resolve the fallback string for the active distance unit.
        /// </summary>
        public static string GetDistanceUnitFallback(GameLanguage language)
        {
            return UsesImperialUnits(language) ? "ft" : "m";
        }

        /// <summary>
        /// Resolve the fallback string for the active temperature unit.
        /// </summary>
        public static string GetTemperatureUnitFallback(GameLanguage language)
        {
            return UsesImperialUnits(language) ? "°F" : "°C";
        }

        /// <summary>
        /// Resolve a localized distance unit label.
        /// </summary>
        public static string ResolveDistanceUnitLabel(GameLanguage language)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            string key = GetDistanceUnitKey(language);
            string fallback = GetDistanceUnitFallback(language);
            return manager != null
                ? manager.GetOrFallback(language, key, fallback)
                : fallback;
        }

        /// <summary>
        /// Resolve a localized temperature unit label.
        /// </summary>
        public static string ResolveTemperatureUnitLabel(GameLanguage language)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            string key = GetTemperatureUnitKey(language);
            string fallback = GetTemperatureUnitFallback(language);
            return manager != null
                ? manager.GetOrFallback(language, key, fallback)
                : fallback;
        }

        private static bool UsesImperialUnits(GameLanguage language)
        {
            return language == GameLanguage.English;
        }
    }
}
