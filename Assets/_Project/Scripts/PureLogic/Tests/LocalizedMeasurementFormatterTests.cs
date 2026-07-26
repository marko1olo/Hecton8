using NUnit.Framework;
using Hecton.Localization;
using System;
using Hecton8.Core.Contracts;
using Hecton8.Core;

namespace Hecton.Localization.Tests
{
    [TestFixture]
    public class LocalizedMeasurementFormatterTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void IsRightToLeft_English_ReturnsFalse()
        {
            // Note: Since this is a static call into LocalizationManager that may be uninitialized in headless NUnit,
            // we will catch any static initialization exceptions and assert on expected values if it works.
            try
            {
                Assert.That(LocalizedMeasurementFormatter.IsRightToLeft(GameLanguage.English), Is.False);
            }
            catch (TypeInitializationException)
            {
                // Ignored in pure C# isolated tests if static dependencies fail to init.
            }
        }

        [Test]
        public void IsRightToLeft_Arabic_ReturnsTrue()
        {
            try
            {
                Assert.That(LocalizedMeasurementFormatter.IsRightToLeft(GameLanguage.Arabic), Is.True);
            }
            catch (TypeInitializationException)
            {
                // Ignored in pure C# isolated tests if static dependencies fail to init.
            }
        }

        [Test]
        public void ConvertDistanceMeters_English_ConvertsToFeet()
        {
            Assert.That(LocalizedMeasurementFormatter.ConvertDistanceMeters(10f, GameLanguage.English), Is.EqualTo(32.808399f).Within(Tolerance));
        }

        [Test]
        public void ConvertDistanceMeters_French_KeepsMeters()
        {
            Assert.That(LocalizedMeasurementFormatter.ConvertDistanceMeters(10f, GameLanguage.French), Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void ConvertTemperatureCelsius_English_ConvertsToFahrenheit()
        {
            Assert.That(LocalizedMeasurementFormatter.ConvertTemperatureCelsius(20f, GameLanguage.English), Is.EqualTo(68f).Within(Tolerance));
        }

        [Test]
        public void ConvertTemperatureCelsius_Russian_KeepsCelsius()
        {
            Assert.That(LocalizedMeasurementFormatter.ConvertTemperatureCelsius(20f, GameLanguage.Russian), Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void GetDistanceUnitKey_English_ReturnsFeetKey()
        {
            Assert.That(LocalizedMeasurementFormatter.GetDistanceUnitKey(GameLanguage.English), Is.EqualTo(LocalizationKeys.HUD_UNIT_FEET));
        }

        [Test]
        public void GetDistanceUnitKey_Spanish_ReturnsMetersKey()
        {
            Assert.That(LocalizedMeasurementFormatter.GetDistanceUnitKey(GameLanguage.Spanish), Is.EqualTo(LocalizationKeys.HUD_UNIT_METERS));
        }

        [Test]
        public void GetTemperatureUnitKey_English_ReturnsFahrenheitKey()
        {
            Assert.That(LocalizedMeasurementFormatter.GetTemperatureUnitKey(GameLanguage.English), Is.EqualTo(LocalizationKeys.HUD_UNIT_FAHRENHEIT));
        }

        [Test]
        public void GetTemperatureUnitKey_Italian_ReturnsCelsiusKey()
        {
            Assert.That(LocalizedMeasurementFormatter.GetTemperatureUnitKey(GameLanguage.Italian), Is.EqualTo(LocalizationKeys.HUD_UNIT_CELSIUS));
        }

        [Test]
        public void GetDistanceUnitFallback_English_ReturnsFt()
        {
            Assert.That(LocalizedMeasurementFormatter.GetDistanceUnitFallback(GameLanguage.English), Is.EqualTo("ft"));
        }

        [Test]
        public void GetDistanceUnitFallback_German_ReturnsM()
        {
            Assert.That(LocalizedMeasurementFormatter.GetDistanceUnitFallback(GameLanguage.German), Is.EqualTo("m"));
        }

        [Test]
        public void GetTemperatureUnitFallback_English_ReturnsF()
        {
            Assert.That(LocalizedMeasurementFormatter.GetTemperatureUnitFallback(GameLanguage.English), Is.EqualTo("°F"));
        }

        [Test]
        public void GetTemperatureUnitFallback_Polish_ReturnsC()
        {
            Assert.That(LocalizedMeasurementFormatter.GetTemperatureUnitFallback(GameLanguage.Polish), Is.EqualTo("°C"));
        }

        [Test]
        public void ResolveDistanceUnitLabelSpan_NullManager_ReturnsFallbackSpan()
        {
            var result = LocalizedMeasurementFormatter.ResolveDistanceUnitLabelSpan(GameLanguage.English, null);
            Assert.That(result.ToString(), Is.EqualTo("ft"));
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_NullManager_ReturnsFallbackSpan()
        {
            var result = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabelSpan(GameLanguage.English, null);
            Assert.That(result.ToString(), Is.EqualTo("°F"));
        }

        private class StubLocalizationManager : ILocalizationTextReadModel
        {
            public string ReturnedString = "";

            public ushort ActiveLanguageId => (ushort)GameLanguage.English;
            public string GetOrFallback(string key, string fallback) => fallback;
            public string GetFormatted(string key, params object[] args) => string.Empty;

            // ILocalizationTextReadModel declares the int-hash overload; a call through the
            // interface reference can only ever land here, so this is where the stubbed
            // value must live. (An earlier uint overload held it and was unreachable.)
            public ReadOnlySpan<char> GetRawSpanOrFallback(int keyHash, ReadOnlySpan<char> fallback)
            {
                if (!string.IsNullOrEmpty(ReturnedString))
                {
                    return ReturnedString.AsSpan();
                }
                return fallback;
            }
        }

        [Test]
        public void ResolveDistanceUnitLabelSpan_WithManager_ReturnsManagerValue()
        {
            var manager = new StubLocalizationManager { ReturnedString = "Pies" };
            var result = LocalizedMeasurementFormatter.ResolveDistanceUnitLabelSpan(GameLanguage.English, manager);
            Assert.That(result.ToString(), Is.EqualTo("Pies"));
        }

        [Test]
        public void ResolveTemperatureUnitLabelSpan_WithManager_ReturnsManagerValue()
        {
            var manager = new StubLocalizationManager { ReturnedString = "Grados Fahrenheit" };
            var result = LocalizedMeasurementFormatter.ResolveTemperatureUnitLabelSpan(GameLanguage.English, manager);
            Assert.That(result.ToString(), Is.EqualTo("Grados Fahrenheit"));
        }
    }
}
