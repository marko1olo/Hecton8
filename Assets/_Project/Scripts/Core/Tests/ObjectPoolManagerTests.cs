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
    }
}
