using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Gameplay;

namespace Hecton8.Tests.PlayMode
{
    public class PlayerFlashlightPlayModeTests
    {
        private GameObject _playerGo;
        private PlayerFlashlight _flashlight;
        private MockFlashlightListener _mockListener;

        private class MockFlashlightListener : IFlashlightEventListener
        {
            public bool WasEventReceived { get; private set; }
            public FlashlightEventPayload LastPayload { get; private set; }

            public void OnFlashlightEvent(in FlashlightEventPayload payload)
            {
                WasEventReceived = true;
                LastPayload = payload;
            }

            public void Reset()
            {
                WasEventReceived = false;
                LastPayload = default;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("PlayerFlashlightTest");
            _flashlight = _playerGo.AddComponent<PlayerFlashlight>();
            _mockListener = new MockFlashlightListener();
            FlashlightEvents.Register(_mockListener);
        }

        [TearDown]
        public void TearDown()
        {
            FlashlightEvents.Unregister(_mockListener);
            if (_playerGo != null)
            {
                Object.DestroyImmediate(_playerGo);
            }
        }

        [Test]
        public void TurnOff_SetsIsOnToFalse_WhenFlashlightIsOn()
        {
            // Arrange
            _flashlight.TurnOn();
            Assert.IsTrue(_flashlight.IsOn, "Flashlight should be on initially for this test.");

            // Act
            _flashlight.TurnOff();

            // Assert
            Assert.IsFalse(_flashlight.IsOn, "Flashlight should be off after TurnOff is called.");
        }

        [Test]
        public void TurnOff_WhenAlreadyOff_RemainsOff()
        {
            // Arrange
            Assert.IsFalse(_flashlight.IsOn, "Flashlight should be off initially for this test.");

            // Act
            _flashlight.TurnOff();

            // Assert
            Assert.IsFalse(_flashlight.IsOn, "Flashlight should remain off.");
        }

        [Test]
        public void TurnOff_ResetsLowBatteryWarningPlayed_AndFiresToggledEvent()
        {
            // Arrange
            _flashlight.TurnOn();

            // Use reflection to set _lowBatteryWarningPlayed to true
            FieldInfo warningField = typeof(PlayerFlashlight).GetField("_lowBatteryWarningPlayed", BindingFlags.NonPublic | BindingFlags.Instance);
            warningField.SetValue(_flashlight, true);

            _mockListener.Reset();

            // Act
            _flashlight.TurnOff();

            // We must flush events since FlashlightEvents queues them
            FlashlightEvents.FlushPending();

            // Assert
            bool warningPlayed = (bool)warningField.GetValue(_flashlight);
            Assert.IsFalse(warningPlayed, "_lowBatteryWarningPlayed should be reset to false.");

            Assert.IsTrue(_mockListener.WasEventReceived, "Event should have been received.");
            Assert.AreEqual((ushort)FlashlightEventType.Toggled, _mockListener.LastPayload.EventType, "Event type should be Toggled.");
            var payload = _mockListener.LastPayload;
            Assert.IsFalse(FlashlightEventPayload.IsOn(in payload), "Payload should indicate flashlight is off.");
        }

        [Test]
        public void TurnOn_WhenOffAndNotOverheated_SetsIsOnToTrueAndRaisesEvent()
        {
            // Arrange
            SetPrivateField(_flashlight, "_isOn", false);
            SetPrivateField(_flashlight, "_isOverheated", false);
            _mockListener.Reset();

            // Act
            _flashlight.TurnOn();
            FlashlightEvents.FlushPending();

            // Assert
            bool isOn = (bool)GetPrivateField(_flashlight, "_isOn");
            Assert.IsTrue(isOn, "Flashlight should be turned on.");

            Assert.IsTrue(_mockListener.WasEventReceived, "An event should have been raised.");
            Assert.AreEqual((ushort)FlashlightEventType.Toggled, _mockListener.LastPayload.EventType);
            Assert.IsTrue(FlashlightEventPayload.IsOn(_mockListener.LastPayload), "Event payload should indicate the flashlight is on.");
        }

        [Test]
        public void TurnOn_WhenAlreadyOn_DoesNothing()
        {
            // Arrange
            SetPrivateField(_flashlight, "_isOn", true);
            SetPrivateField(_flashlight, "_isOverheated", false);
            _mockListener.Reset();

            // Act
            _flashlight.TurnOn();
            FlashlightEvents.FlushPending();

            // Assert
            bool isOn = (bool)GetPrivateField(_flashlight, "_isOn");
            Assert.IsTrue(isOn, "Flashlight should still be on.");
            Assert.IsFalse(_mockListener.WasEventReceived, "No event should be raised if already on.");
        }

        [Test]
        public void TurnOn_WhenOverheated_DoesNothing()
        {
            // Arrange
            SetPrivateField(_flashlight, "_isOn", false);
            SetPrivateField(_flashlight, "_isOverheated", true);
            _mockListener.Reset();

            // Act
            _flashlight.TurnOn();
            FlashlightEvents.FlushPending();

            // Assert
            bool isOn = (bool)GetPrivateField(_flashlight, "_isOn");
            Assert.IsFalse(isOn, "Flashlight should not turn on when overheated.");
            Assert.IsFalse(_mockListener.WasEventReceived, "No event should be raised if turning on fails due to overheat.");
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Field '$fieldName' not found on type " + obj.GetType().Name);
            field.SetValue(obj, value);
        }

        private object GetPrivateField(object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Field '$fieldName' not found on type " + obj.GetType().Name);
            return field.GetValue(obj);
        }
    }
}
