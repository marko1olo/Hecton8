using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Core.Tests
{
    [TestFixture]
    public class ObjectPoolManagerTests
    {
        private GameObject _go;
        private ObjectPoolManager _manager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestPoolManager");
            _manager = _go.AddComponent<ObjectPoolManager>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void OnServiceShutdown_ResetsCoreStateProperties()
        {
            // Arrange
            var shuttingDownField = typeof(ObjectPoolManager).GetField("_serviceShuttingDown", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(shuttingDownField, "Could not find _serviceShuttingDown field");
            shuttingDownField.SetValue(_manager, false);

            var prop = typeof(ObjectPoolManager).GetProperty("ActiveRuntimeInstance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            Assert.IsNotNull(prop, "Could not find ActiveRuntimeInstance property");
            prop.SetValue(null, _manager);

            var warmupStartedField = typeof(ObjectPoolManager).GetField("_warmupPresetsStarted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(warmupStartedField, "Could not find _warmupPresetsStarted field");
            warmupStartedField.SetValue(_manager, true);

            var warmupCompletedField = typeof(ObjectPoolManager).GetField("_warmupPresetsCompleted", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(warmupCompletedField, "Could not find _warmupPresetsCompleted field");
            warmupCompletedField.SetValue(_manager, false);

            // Act
            _manager.OnServiceShutdown();

            // Assert
            bool isShuttingDown = (bool)shuttingDownField.GetValue(_manager);
            Assert.IsTrue(isShuttingDown, "OnServiceShutdown should set _serviceShuttingDown to true.");

            ObjectPoolManager activeInstance = (ObjectPoolManager)prop.GetValue(null);
            Assert.IsNull(activeInstance, "OnServiceShutdown should clear ActiveRuntimeInstance if it matches this instance.");

            bool warmupStarted = (bool)warmupStartedField.GetValue(_manager);
            Assert.IsFalse(warmupStarted, "OnServiceShutdown should set _warmupPresetsStarted to false.");

            bool warmupCompleted = (bool)warmupCompletedField.GetValue(_manager);
            Assert.IsTrue(warmupCompleted, "OnServiceShutdown should set _warmupPresetsCompleted to true.");
        }
[Test]
        public void InitializeService_RegistersWithGlobalRegistry()
        {
            // Arrange
            var registryMirrorProp = typeof(GlobalRegistry).GetProperty("ObjectPoolRuntimeMirror", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(registryMirrorProp, "Could not find ObjectPoolRuntimeMirror property on GlobalRegistry");

            var unregisterMethod = typeof(GlobalRegistry).GetMethod("UnregisterObjectPoolService", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(unregisterMethod, "Could not find UnregisterObjectPoolService method on GlobalRegistry");

            var originalRegistration = registryMirrorProp.GetValue(null);
            if (originalRegistration != null)
            {
                unregisterMethod.Invoke(null, new object[] { originalRegistration });
            }

            var activeInstanceProp = typeof(ObjectPoolManager).GetProperty("ActiveRuntimeInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
            var originalActiveInstance = activeInstanceProp?.GetValue(null);
            if (activeInstanceProp != null)
            {
                activeInstanceProp.SetValue(null, null);
            }

            try
            {
                // Act
                _manager.InitializeService();

                // Assert
                Assert.AreEqual(_manager, registryMirrorProp.GetValue(null), "InitializeService should register the manager with GlobalRegistry");

                var serviceRegisteredField = typeof(ObjectPoolManager).GetField("_serviceRegistered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.IsNotNull(serviceRegisteredField, "Could not find _serviceRegistered field on ObjectPoolManager");
                Assert.IsTrue((bool)serviceRegisteredField.GetValue(_manager), "_serviceRegistered should be true after initialization");

                if (activeInstanceProp != null)
                {
                    Assert.AreEqual(_manager, activeInstanceProp.GetValue(null), "InitializeService should set ActiveRuntimeInstance to this manager");
                }
            }
            finally
            {
                // Cleanup
                var currentRegistration = registryMirrorProp.GetValue(null);
                if (currentRegistration != null)
                {
                    unregisterMethod.Invoke(null, new object[] { currentRegistration });
                }

                if (originalRegistration != null)
                {
                    typeof(GlobalRegistry).GetMethod("RegisterObjectPoolService", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, new object[] { originalRegistration });
                }

                if (activeInstanceProp != null)
                {
                    activeInstanceProp.SetValue(null, originalActiveInstance);
                }
            }
        }

    }
}
