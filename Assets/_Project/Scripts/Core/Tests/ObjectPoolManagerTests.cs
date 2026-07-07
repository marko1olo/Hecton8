using UnityEngine.TestTools;
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
        public async System.Threading.Tasks.Task WarmupPresetsAsync_LogsErrorAndRecovers_OnException()
        {
            var registryGo = new GameObject("TestRegistry");
            try
            {
                var registry = registryGo.AddComponent<PrefabRegistry>();
                typeof(PrefabRegistry).GetField("s_activeRuntimeInstance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).SetValue(null, registry);

                var warmupEntriesType = typeof(ObjectPoolManager).GetNestedType("WarmupEntry", System.Reflection.BindingFlags.NonPublic);
                var warmupEntriesArray = System.Array.CreateInstance(warmupEntriesType, 1);
                var entry = System.Activator.CreateInstance(warmupEntriesType);

                var dummyPrefab = new GameObject("DummyPrefab");
                warmupEntriesType.GetField("prefab", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(entry, dummyPrefab);
                warmupEntriesType.GetField("count", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).SetValue(entry, 1);

                warmupEntriesArray.SetValue(entry, 0);

                typeof(ObjectPoolManager).GetField("warmupPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(_manager, warmupEntriesArray);

                UnityEngine.Object.DestroyImmediate(dummyPrefab);

                UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[ObjectPoolManager\] WarmupPresetsAsync crashed:.*"));

                var token = new System.Threading.CancellationToken();
                var task = _manager.WarmupPresetsAsync(10.0, token);
                bool result = await task;

                Assert.IsFalse(result, "WarmupPresetsAsync should return false when an exception occurs.");

                var completedField = typeof(ObjectPoolManager).GetField("_warmupPresetsCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                bool isCompleted = (bool)completedField.GetValue(_manager);
                Assert.IsTrue(isCompleted, "_warmupPresetsCompleted should be true after an exception.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(registryGo);
            }
        }
}
}
