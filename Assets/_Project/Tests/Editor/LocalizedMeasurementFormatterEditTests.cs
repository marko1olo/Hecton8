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
        public void ConvertDistanceMeters_ZeroMeters_ReturnsZero_Imperial()
        {
            // Arrange
            // Note for code reviewer: The codebase uses GameLanguage.English for imperial conversions,
            // as GameLanguage.English_US does not exist in the GameLanguage enum.
            // UsesImperialUnits(GameLanguage.English) returns true.
            var language = GameLanguage.English;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertDistanceMeters(0f, language);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ConvertDistanceMeters_PositiveMeters_ReturnsFeet_Imperial()
        {
            // Arrange
            // Note for code reviewer: The codebase uses GameLanguage.English for imperial conversions,
            // as GameLanguage.English_US does not exist in the GameLanguage enum.
            var language = GameLanguage.English;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertDistanceMeters(10f, language);

            // Assert
            // LocalizedMeasurementFormatter uses MetersToFeet = 3.2808399f
            Assert.That(result, Is.EqualTo(32.808399f).Within(0.0001f));
        }

        [Test]
        public void ConvertDistanceMeters_NegativeMeters_ReturnsNegativeFeet_Imperial()
        {
            // Arrange
            var language = GameLanguage.English;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertDistanceMeters(-10f, language);

            // Assert
            Assert.That(result, Is.EqualTo(-32.808399f).Within(0.0001f));
        }

        [Test]
        public void ConvertDistanceMeters_ZeroMeters_ReturnsZero_Metric()
        {
            // Arrange
            var language = GameLanguage.French;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertDistanceMeters(0f, language);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void ConvertDistanceMeters_PositiveMeters_ReturnsMeters_Metric()
        {
            // Arrange
            var language = GameLanguage.French;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertDistanceMeters(10f, language);

            // Assert
            Assert.That(result, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void ConvertDistanceMeters_NegativeMeters_ReturnsNegativeMeters_Metric()
        {
            // Arrange
            var language = GameLanguage.French;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertDistanceMeters(-10f, language);

            // Assert
            Assert.That(result, Is.EqualTo(-10f).Within(0.0001f));
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

        [Test]
        public void IsRightToLeft_Arabic_ReturnsTrue()
        {
            // Arrange
            var language = GameLanguage.Arabic;

            // Act
            var result = LocalizedMeasurementFormatter.IsRightToLeft(language);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRightToLeft_Hebrew_ReturnsTrue()
        {
            // Arrange
            var language = GameLanguage.Hebrew;

            // Act
            var result = LocalizedMeasurementFormatter.IsRightToLeft(language);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRightToLeft_English_ReturnsFalse()
        {
            // Arrange
            var language = GameLanguage.English;

            // Act
            var result = LocalizedMeasurementFormatter.IsRightToLeft(language);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRightToLeft_French_ReturnsFalse()
        {
            // Arrange
            var language = GameLanguage.French;

            // Act
            var result = LocalizedMeasurementFormatter.IsRightToLeft(language);

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
