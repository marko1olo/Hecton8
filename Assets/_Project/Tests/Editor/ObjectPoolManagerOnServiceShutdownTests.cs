using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerOnServiceShutdownTests
    {
        private ObjectPoolManager _poolManager;
        private GameObject _poolRoot;

        [SetUp]
        public void SetUp()
        {
            _poolRoot = new GameObject("ObjectPoolManager");
            _poolManager = _poolRoot.AddComponent<ObjectPoolManager>();

        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_poolRoot);
        }

        [Test]
        public void OnServiceShutdown_RepeatedCalls_UpdatesFlagsCorrectly()
        {

            // Arrange
            _poolManager.InitializeService();

            Assert.IsTrue(_poolManager.IsServiceReady, "Service should be ready after InitializeService.");
            Assert.AreEqual(ServiceHeartbeatState.Ready, _poolManager.HeartbeatState, "HeartbeatState should be Ready.");

            // Act - First shutdown
            _poolManager.OnServiceShutdown();

            // Assert - State after first shutdown
            Assert.IsFalse(_poolManager.IsServiceReady, "Service should not be ready after first shutdown.");
            Assert.AreEqual(ServiceHeartbeatState.NotStarted, _poolManager.HeartbeatState, "HeartbeatState should be NotStarted after first shutdown.");

            var activeRuntimeProp = typeof(ObjectPoolManager).GetProperty("ActiveRuntimeInstance", BindingFlags.NonPublic | BindingFlags.Static);
            var activeInstance = activeRuntimeProp?.GetValue(null);
            Assert.IsNull(activeInstance, "ActiveRuntimeInstance should be null after shutdown.");

            // Act - Second shutdown (repeated call)
            _poolManager.OnServiceShutdown();

            // Assert - State after second shutdown
            Assert.IsFalse(_poolManager.IsServiceReady, "Service should still not be ready after repeated shutdown.");
            Assert.AreEqual(ServiceHeartbeatState.NotStarted, _poolManager.HeartbeatState, "HeartbeatState should still be NotStarted after repeated shutdown.");
        }

        [Test]
        public void OnServiceShutdown_ActiveRuntimeMirror_SetsToNull()
        {
            // Arrange
            _poolManager.InitializeService();

            // Set mirror manually to ensure it handles the exact match case
            GlobalRegistry.ObjectPoolRuntimeMirror = _poolManager;
            Assert.AreEqual(_poolManager, GlobalRegistry.ObjectPoolRuntimeMirror, "Mirror should be set to our instance.");

            // Act
            _poolManager.OnServiceShutdown();

            // Assert
            Assert.IsNull(GlobalRegistry.ObjectPoolRuntimeMirror, "Mirror should be set to null if it matched this instance.");

        }
    }
}
