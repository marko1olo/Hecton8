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
        public void Warmup_ValidPrefab_PreAllocatesCorrectAmount()
        {
            // Arrange
            GameObject prefabRegistryGo = new GameObject("[PrefabRegistry]");
            prefabRegistryGo.AddComponent<PrefabRegistry>();

            GameObject prefab = new GameObject("TestPrefab");
            int warmupCount = 5;

            // Act
            _manager.Warmup(prefab, warmupCount);

            // Assert
            var poolsField = typeof(ObjectPoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(poolsField, "Could not find _pools field");

            var poolsDict = poolsField.GetValue(_manager) as System.Collections.IDictionary;
            Assert.IsNotNull(poolsDict, "Pools dictionary should be initialized");
            Assert.AreEqual(1, poolsDict.Count, "There should be one pool created.");

            // Extract the available queue from the pool
            var enumerator = poolsDict.GetEnumerator();
            enumerator.MoveNext();
            var poolInstance = enumerator.Value;

            var poolType = poolInstance.GetType();
            var availableField = poolType.GetField("available", BindingFlags.Public | BindingFlags.Instance);
            var availableQueue = availableField.GetValue(poolInstance) as System.Collections.ICollection;

            Assert.IsNotNull(availableQueue, "Available queue should not be null.");
            Assert.AreEqual(warmupCount, availableQueue.Count, $"Pool queue should have {warmupCount} instances pre-allocated.");

            // Teardown objects
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(prefabRegistryGo);
        }

        [Test]
        public void Warmup_NullPrefab_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _manager.Warmup((GameObject)null, 5));
        }

        [Test]
        public void Warmup_ZeroOrNegativeCount_DoesNotAllocate()
        {
            // Arrange
            GameObject prefab = new GameObject("TestPrefab");

            // Act
            _manager.Warmup(prefab, 0);
            _manager.Warmup(prefab, -5);

            // Assert
            var poolsField = typeof(ObjectPoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
            var poolsDict = poolsField.GetValue(_manager) as System.Collections.IDictionary;

            if (poolsDict != null)
            {
                Assert.AreEqual(0, poolsDict.Count, "Should not create a pool for 0 or negative count");
            }

            Object.DestroyImmediate(prefab);
        }
    }
}
