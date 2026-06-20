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
        private GameObject _flashlightObj;
        private PlayerFlashlight _flashlight;
        private MockFlashlightEventListener _mockListener;

        private class MockFlashlightEventListener : IFlashlightEventListener
        {
            public bool EventReceived { get; private set; }
            public FlashlightEventPayload LastPayload { get; private set; }

            public void OnFlashlightEvent(in FlashlightEventPayload payload)
            {
                EventReceived = true;
                LastPayload = payload;
            }

            public void Reset()
            {
                EventReceived = false;
                LastPayload = default;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _flashlightObj = new GameObject("PlayerFlashlightTestObj");
            _flashlight = _flashlightObj.AddComponent<PlayerFlashlight>();

            _mockListener = new MockFlashlightEventListener();
            FlashlightEvents.Register(_mockListener);
        }

        [TearDown]
        public void TearDown()
        {
            FlashlightEvents.Unregister(_mockListener);
            if (_flashlightObj != null)
            {
                Object.DestroyImmediate(_flashlightObj);
            }
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

            Assert.IsTrue(_mockListener.EventReceived, "An event should have been raised.");
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
            Assert.IsFalse(_mockListener.EventReceived, "No event should be raised if already on.");
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
            Assert.IsFalse(_mockListener.EventReceived, "No event should be raised if turning on fails due to overheat.");
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on type {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private object GetPrivateField(object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on type {obj.GetType().Name}");
            return field.GetValue(obj);
        }
    }
}
