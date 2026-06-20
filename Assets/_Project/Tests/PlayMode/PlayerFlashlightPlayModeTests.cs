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
        }

        [TearDown]
        public void TearDown()
        {
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

            MockFlashlightListener listener = new MockFlashlightListener();
            FlashlightEvents.Register(listener);

            // Act
            _flashlight.TurnOff();

            // We must flush events since FlashlightEvents queues them
            FlashlightEvents.FlushPending();

            // Assert
            bool warningPlayed = (bool)warningField.GetValue(_flashlight);
            Assert.IsFalse(warningPlayed, "_lowBatteryWarningPlayed should be reset to false.");

            Assert.IsTrue(listener.WasEventReceived, "Event should have been received.");
            Assert.AreEqual((ushort)FlashlightEventType.Toggled, listener.LastPayload.EventType, "Event type should be Toggled.");
            Assert.IsFalse(FlashlightEventPayload.IsOn(in listener.LastPayload), "Payload should indicate flashlight is off.");

            // Cleanup
            FlashlightEvents.Unregister(listener);
        }
    }
}
