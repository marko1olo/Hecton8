#if UNITY_EDITOR
using System;
using System.Reflection;
using Hecton.Localization;
using Hecton.UI.MainMenu;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.Tests.PlayMode
{
    public class SaveSlotUIOnGlobalRegistryServiceReplacedTests
    {
        private GameObject _go;
        private SaveSlotUI _saveSlotUI;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("SaveSlotUI_Test");
            _go.AddComponent<Button>();
            _saveSlotUI = _go.AddComponent<SaveSlotUI>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_LocalizationRuntime_UpdatesLocalizationField()
        {
            var mockLocalization = new MockLocalizationManager();

            _saveSlotUI.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.LocalizationRuntime, null, mockLocalization);

            var locField = typeof(SaveSlotUI).GetField("_localization", BindingFlags.NonPublic | BindingFlags.Instance);
            var locValue = locField.GetValue(_saveSlotUI);

            Assert.AreSame(mockLocalization, locValue);
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_NotLocalizationRuntime_IgnoresService()
        {
            var mockLocalization = new MockLocalizationManager();
            var locField = typeof(SaveSlotUI).GetField("_localization", BindingFlags.NonPublic | BindingFlags.Instance);
            locField.SetValue(_saveSlotUI, null);

            _saveSlotUI.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.Player, null, mockLocalization);

            var locValue = locField.GetValue(_saveSlotUI);

            Assert.IsNull(locValue);
        }

        [Test]
        public void OnGlobalRegistryServiceReplaced_WithSlotId_CallsApplyPresentation()
        {
            var mockLocalization = new MockLocalizationManager();
            var slotIdField = typeof(SaveSlotUI).GetField("_slotId", BindingFlags.NonPublic | BindingFlags.Instance);
            slotIdField.SetValue(_saveSlotUI, "Slot_1");

            Assert.DoesNotThrow(() => {
                _saveSlotUI.OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot.LocalizationRuntime, null, mockLocalization);
            });

            var locField = typeof(SaveSlotUI).GetField("_localization", BindingFlags.NonPublic | BindingFlags.Instance);
            var locValue = locField.GetValue(_saveSlotUI);
            Assert.AreSame(mockLocalization, locValue);
        }

        private class MockLocalizationManager : ILocalizationTextReadModel
        {
            public ushort ActiveLanguageId => 0;
            public string GetOrFallback(string key, string fallback) => fallback;
            public string GetFormatted(string key, params object[] args) => string.Empty;
            public ReadOnlySpan<char> GetRawSpanOrFallback(int keyHash, ReadOnlySpan<char> fallback) => fallback;
        }
    }
}
#endif
