using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.Core.Contracts.Signals;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonSuitHUDExtensionsTests
    {
        private GameObject _gameObject;
        private HectonSuitHUDExtensions _hudExtensions;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestHUD");
            _hudExtensions = _gameObject.AddComponent<HectonSuitHUDExtensions>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void OnFlashlightEvent_BatteryDepleted_UpdatesStateCorrectly()
        {
            // Arrange
            var payload = new FlashlightEventPayload
            {
                EventType = (ushort)FlashlightEventType.BatteryDepleted,
                BatteryPercent = 0f,
                Heat01 = 0.5f,
                StateBits = 1 // Simulating it was previously on
            };

            // Act
            _hudExtensions.OnFlashlightEvent(in payload);

            // Assert
            var type = typeof(HectonSuitHUDExtensions);

            var isOnField = type.GetField("_flashlightOn", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)isOnField.GetValue(_hudExtensions), "Flashlight should be marked as off on BatteryDepleted.");

            var isOverheatedField = type.GetField("_flashlightOverheated", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)isOverheatedField.GetValue(_hudExtensions), "Flashlight should not be marked as overheated on BatteryDepleted.");

            var isFlickeringField = type.GetField("_flashlightFlickering", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)isFlickeringField.GetValue(_hudExtensions), "Flashlight should not be marked as flickering on BatteryDepleted.");
        }

        [Test]
        public void OnFlashlightEvent_Overheat_UpdatesStateCorrectly()
        {
            // Arrange
            var payload = new FlashlightEventPayload
            {
                EventType = (ushort)FlashlightEventType.Overheat,
                BatteryPercent = 50f,
                Heat01 = 1.0f,
                StateBits = 1 // Simulating it was previously on
            };

            // Act
            _hudExtensions.OnFlashlightEvent(in payload);

            // Assert
            var type = typeof(HectonSuitHUDExtensions);

            var isOnField = type.GetField("_flashlightOn", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)isOnField.GetValue(_hudExtensions), "Flashlight should be marked as off on Overheat.");

            var isOverheatedField = type.GetField("_flashlightOverheated", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)isOverheatedField.GetValue(_hudExtensions), "Flashlight should be marked as overheated on Overheat.");

            var isFlickeringField = type.GetField("_flashlightFlickering", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsFalse((bool)isFlickeringField.GetValue(_hudExtensions), "Flashlight should not be marked as flickering on Overheat.");
        }

        [Test]
        public void OnFlashlightEvent_FlickerStart_UpdatesStateCorrectly()
        {
            // Arrange
            var payload = new FlashlightEventPayload
            {
                EventType = (ushort)FlashlightEventType.FlickerStart,
                BatteryPercent = 15f,
                Heat01 = 0.5f,
                StateBits = 1 // Flashlight is on
            };

            // Act
            _hudExtensions.OnFlashlightEvent(in payload);

            // Assert
            var type = typeof(HectonSuitHUDExtensions);

            var isOnField = type.GetField("_flashlightOn", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)isOnField.GetValue(_hudExtensions), "Flashlight should remain on during FlickerStart.");

            var isFlickeringField = type.GetField("_flashlightFlickering", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)isFlickeringField.GetValue(_hudExtensions), "Flashlight should be marked as flickering on FlickerStart.");
        }

        [Test]
        public void OnFlashlightEvent_Toggled_UpdatesStateCorrectly()
        {
            // Arrange
            var payload = new FlashlightEventPayload
            {
                EventType = (ushort)FlashlightEventType.Toggled,
                BatteryPercent = 100f,
                Heat01 = 0f,
                StateBits = 1 // Toggled on
            };

            // Act
            _hudExtensions.OnFlashlightEvent(in payload);

            // Assert
            var type = typeof(HectonSuitHUDExtensions);

            var isOnField = type.GetField("_flashlightOn", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)isOnField.GetValue(_hudExtensions), "Flashlight should be marked as on after being toggled on.");

            var batteryField = type.GetField("_flashlightBattery", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(100f, (float)batteryField.GetValue(_hudExtensions), "Flashlight battery should be updated.");

            var heatField = type.GetField("_flashlightHeat", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(0f, (float)heatField.GetValue(_hudExtensions), "Flashlight heat should be updated.");
        }
    }
}
