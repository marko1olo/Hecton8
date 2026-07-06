using System.Reflection;
using Hecton8.Power;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridManagerLateFrameTickTests
    {
        private GameObject _managerGo;
        private PowerGridManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("PowerGridManager");
            _manager = _managerGo.AddComponent<PowerGridManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_managerGo);
        }

        [Test]
        public void LateFrameTick_WhenNotInitialized_ReturnsEarly()
        {
            // Arrange
            var isInitializedField = typeof(PowerGridManager).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            var timeField = typeof(PowerGridManager).GetField("_timeSinceLastSlowTick", BindingFlags.NonPublic | BindingFlags.Instance);
            var telemetryPendingField = typeof(PowerGridManager).GetField("_telemetryPublishPending", BindingFlags.NonPublic | BindingFlags.Instance);

            isInitializedField.SetValue(_manager, false);
            timeField.SetValue(_manager, 0.5f);
            telemetryPendingField.SetValue(_manager, true);

            // Act
            _manager.LateFrameTick();

            // Assert
            float time = (float)timeField.GetValue(_manager);
            Assert.AreEqual(0.5f, time, "Time should not be incremented because LateFrameTick should return early.");
            bool isPending = (bool)telemetryPendingField.GetValue(_manager);
            Assert.IsTrue(isPending, "Telemetry publish pending flag should NOT be cleared since method returned early.");
        }

        [Test]
        public void LateFrameTick_WhenInitialized_IncrementsTimeButDoesNotReset_IfBelowInterval()
        {
            // Arrange
            var isInitializedField = typeof(PowerGridManager).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            var timeField = typeof(PowerGridManager).GetField("_timeSinceLastSlowTick", BindingFlags.NonPublic | BindingFlags.Instance);

            isInitializedField.SetValue(_manager, true);
            timeField.SetValue(_manager, 0.1f);

            // Act
            _manager.LateFrameTick();

            // Assert
            float time = (float)timeField.GetValue(_manager);
            Assert.GreaterOrEqual(time, 0.1f, "Time should have been incremented.");
            Assert.Less(time, 1.0f, "Time should not have crossed the interval threshold.");
        }

        [Test]
        public void LateFrameTick_WhenInitialized_ResetsTime_IfAboveInterval()
        {
            // Arrange
            var isInitializedField = typeof(PowerGridManager).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            var timeField = typeof(PowerGridManager).GetField("_timeSinceLastSlowTick", BindingFlags.NonPublic | BindingFlags.Instance);

            isInitializedField.SetValue(_manager, true);
            timeField.SetValue(_manager, 1.5f);

            // Act
            _manager.LateFrameTick();

            // Assert
            float time = (float)timeField.GetValue(_manager);
            Assert.AreEqual(0f, time, "Time should have been reset to 0 because it crossed the SLOW_TICK_INTERVAL threshold.");
        }

        [Test]
        public void LateFrameTick_WhenTelemetryPending_PublishesAndClearsFlag()
        {
            // Arrange
            var isInitializedField = typeof(PowerGridManager).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            var telemetryPendingField = typeof(PowerGridManager).GetField("_telemetryPublishPending", BindingFlags.NonPublic | BindingFlags.Instance);

            isInitializedField.SetValue(_manager, true);
            telemetryPendingField.SetValue(_manager, true);

            // Act
            _manager.LateFrameTick();

            // Assert
            bool isPending = (bool)telemetryPendingField.GetValue(_manager);
            Assert.IsFalse(isPending, "Telemetry publish pending flag should be cleared after LateFrameTick.");
        }

        [Test]
        public void LateFrameTick_WhenTelemetryNotPending_LeavesFlagFalse()
        {
            // Arrange
            var isInitializedField = typeof(PowerGridManager).GetField("_isInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            var telemetryPendingField = typeof(PowerGridManager).GetField("_telemetryPublishPending", BindingFlags.NonPublic | BindingFlags.Instance);

            isInitializedField.SetValue(_manager, true);
            telemetryPendingField.SetValue(_manager, false);

            // Act
            _manager.LateFrameTick();

            // Assert
            bool isPending = (bool)telemetryPendingField.GetValue(_manager);
            Assert.IsFalse(isPending, "Telemetry publish pending flag should remain false if not pending.");
        }
    }
}
