using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class PlayerFlashlightEditTests
    {
        private GameObject _gameObject;
        private PlayerFlashlight _flashlight;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestFlashlight");
            _flashlight = _gameObject.AddComponent<PlayerFlashlight>();

            MethodInfo resetMethod = typeof(FlashlightEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            if (resetMethod != null)
                resetMethod.Invoke(null, null);

            SignalBus<FlashlightEventPayload>.Configure(16, 16, 4, 0x464C4556u);
            SignalBus<FlashlightEventPayload>.EnsureInitialized();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);

            MethodInfo resetMethod = typeof(FlashlightEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            if (resetMethod != null)
                resetMethod.Invoke(null, null);
        }

        [Test]
        public void TurnOff_WhenOn_SetsIsOffAndFiresEvent()
        {
            // Arrange
            _flashlight.TurnOn();
            Assert.IsTrue(_flashlight.IsOn);

            // Act
            _flashlight.TurnOff();

            // Assert
            Assert.IsFalse(_flashlight.IsOn);

            var snapshot = SignalBus<FlashlightEventPayload>.GetFrameSnapshot();
            // Should be 2 events now: TurnOn, TurnOff
            Assert.AreEqual(2, snapshot.Length);
            Assert.AreEqual((ushort)FlashlightEventType.Toggled, snapshot[1].EventType);
            Assert.IsFalse(FlashlightEventPayload.IsOn(in snapshot[1]));
        }

        [Test]
        public void TurnOff_WhenAlreadyOff_DoesNothing()
        {
            // Arrange
            Assert.IsFalse(_flashlight.IsOn);

            // Act
            _flashlight.TurnOff();

            // Assert
            Assert.IsFalse(_flashlight.IsOn);

            var snapshot = SignalBus<FlashlightEventPayload>.GetFrameSnapshot();
            // No events should be fired because it was already off
            Assert.AreEqual(0, snapshot.Length);
        }
    }
}
