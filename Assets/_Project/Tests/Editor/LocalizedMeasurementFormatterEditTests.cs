using NUnit.Framework;
using System;
using Hecton8.Core;

namespace Hecton.Localization.Tests
{
    public class LocalizedMeasurementFormatterEditTests
    {
        private class MockLocalizationManager : ILocalizationTextReadModel
        {
            public ushort ActiveLanguageId => 0;

            public string GetOrFallback(string key, string fallback) => fallback;

            public string GetFormatted(string key, params object[] args) => string.Empty;

            public ReadOnlySpan<char> GetRawSpanOrFallback(int keyHash, ReadOnlySpan<char> fallback)
            {
                if (keyHash == LocHash.Compute(LocalizationKeys.HUD_UNIT_CELSIUS.AsSpan()))
                {
                    return "Mock_C".AsSpan();
                }
                if (keyHash == LocHash.Compute(LocalizationKeys.HUD_UNIT_FAHRENHEIT.AsSpan()))
                {
                    return "Mock_F".AsSpan();
                }
                return fallback;
            }
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_Imperial_NullManager_ReturnsFallback()
        {
            // Arrange
            var language = GameLanguage.English;

            // Act
            var result = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabelSpan(language, null);

            // Assert
            Assert.AreEqual("°F", result.ToString());
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_Metric_NullManager_ReturnsFallback()
        {
            // Arrange
            var language = GameLanguage.French;

            // Act
            var result = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabelSpan(language, null);

            // Assert
            Assert.AreEqual("°C", result.ToString());
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_Imperial_WithManager_ReturnsLocalized()
        {
            // Arrange
            var language = GameLanguage.English;
            var manager = new MockLocalizationManager();

            // Act
            var result = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabelSpan(language, manager);

            // Assert
            Assert.AreEqual("Mock_F", result.ToString());
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_Metric_WithManager_ReturnsLocalized()
        {
            // Arrange
            var language = GameLanguage.French;
            var manager = new MockLocalizationManager();

            // Act
            var result = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabelSpan(language, manager);

            // Assert
            Assert.AreEqual("Mock_C", result.ToString());
        }
    }
}
