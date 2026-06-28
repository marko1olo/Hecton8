#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class LocalizedMeasurementFormatterEditTests
    {
        private class MockLocalizationManager : ILocalizationManager
        {
            public GameLanguage CurrentLanguage => GameLanguage.English;

            public string GetLocalizedString(string key)
            {
                if (key == "UI_UNIT_CELSIUS" || key == "celsius") return "Mock_C";
                if (key == "UI_UNIT_FAHRENHEIT" || key == "fahrenheit") return "Mock_F";
                return key;
            }
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_Imperial_WithManager_ReturnsLocalized()
        {
            // Arrange
            var language = GameLanguage.English_US;
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

        [Test]
        public void ConvertTemperatureCelsius_Imperial_ReturnsFahrenheit()
        {
            // Arrange
            var language = GameLanguage.English_US;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(20f, language);

            // Assert
            Assert.That(result, Is.EqualTo(68f).Within(0.01f));
        }

        [Test]
        public void ConvertTemperatureCelsius_Metric_ReturnsCelsius()
        {
            // Arrange
            var language = GameLanguage.French;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(20f, language);

            // Assert
            Assert.That(result, Is.EqualTo(20f).Within(0.01f));
        }

        [Test]
        public void ConvertTemperatureCelsius_Imperial_AbsoluteZero_ReturnsFahrenheit()
        {
            // Arrange
            var language = GameLanguage.English_US;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(-273.15f, language);

            // Assert
            Assert.That(result, Is.EqualTo(-459.67f).Within(0.01f));
        }

        [Test]
        public void ConvertTemperatureCelsius_Imperial_FreezingPoint_ReturnsFahrenheit()
        {
            // Arrange
            var language = GameLanguage.English_US;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(0f, language);

            // Assert
            Assert.That(result, Is.EqualTo(32f).Within(0.01f));
        }

        [Test]
        public void ConvertTemperatureCelsius_Imperial_BoilingPoint_ReturnsFahrenheit()
        {
            // Arrange
            var language = GameLanguage.English_US;

            // Act
            var result = LocalizedMeasurementFormatter.ConvertTemperatureCelsius(100f, language);

            // Assert
            Assert.That(result, Is.EqualTo(212f).Within(0.01f));
        }
    }
}
#endif
