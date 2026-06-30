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
        public void OnServiceShutdown_RepeatedCalls_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                _poolManager.OnServiceShutdown();
                _poolManager.OnServiceShutdown();
            }, "Repeated calls to OnServiceShutdown should not throw errors.");
        }

        [Test]
        public void OnServiceShutdown_SetsServiceShuttingDownFlag()
        {
            // Act
            _poolManager.OnServiceShutdown();

            // Assert
            var shuttingDownField = typeof(ObjectPoolManager).GetField("_serviceShuttingDown", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isShuttingDown = (bool)shuttingDownField.GetValue(_poolManager);

            Assert.IsTrue(isShuttingDown, "OnServiceShutdown should set _serviceShuttingDown to true.");
        }

        [Test]
        public void OnServiceShutdown_SetsServiceRegisteredFlagToFalse()
        {
            // Arrange
            var registeredField = typeof(ObjectPoolManager).GetField("_serviceRegistered", BindingFlags.NonPublic | BindingFlags.Instance);
            registeredField.SetValue(_poolManager, true);

            // Act
            _poolManager.OnServiceShutdown();

            // Assert
            bool isRegistered = (bool)registeredField.GetValue(_poolManager);
            Assert.IsFalse(isRegistered, "OnServiceShutdown should set _serviceRegistered to false.");
        }

        [Test]
        public void OnServiceShutdown_ClearsGlobalRegistryMirror_IfItIsThis()
        {
            // Arrange
            var mirrorProperty = typeof(GlobalRegistry).GetProperty("ObjectPoolRuntimeMirror", BindingFlags.NonPublic | BindingFlags.Static);
            mirrorProperty?.SetValue(null, _poolManager);

            // Act
            _poolManager.OnServiceShutdown();

            // Assert
            Assert.IsNull(mirrorProperty?.GetValue(null), "OnServiceShutdown should set GlobalRegistry.ObjectPoolRuntimeMirror to null if it references the current instance.");
        }
    }
}
