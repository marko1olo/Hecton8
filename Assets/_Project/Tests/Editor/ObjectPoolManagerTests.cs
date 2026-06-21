using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerTests
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
        public void Spawn_WithComponent_SpawnsGameObject()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            var testComponent = prefabGo.AddComponent<BoxCollider>();

            // Act
            GameObject spawned = _poolManager.Spawn(testComponent, Vector3.one, Quaternion.Euler(0, 90, 0));

            // Assert
            Assert.IsNotNull(spawned, "Spawned GameObject should not be null.");
            Assert.AreEqual(Vector3.one, spawned.transform.position, "Spawned position should match requested position.");
            Assert.AreEqual(Quaternion.Euler(0, 90, 0), spawned.transform.rotation, "Spawned rotation should match requested rotation.");
            Assert.IsTrue(spawned.name.StartsWith("TestPrefab"), "Spawned object should originate from the correct prefab.");

            Object.DestroyImmediate(prefabGo);
            Object.DestroyImmediate(spawned);
        }

        [Test]
        public void HasPool_WithComponent_ReturnsTrueIfPoolExists()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            var testComponent = prefabGo.AddComponent<BoxCollider>();

            // Implicitly create pool by spawning
            GameObject spawned = _poolManager.Spawn(testComponent, Vector3.zero, Quaternion.identity);

            // Act
            bool hasPool = _poolManager.HasPool(testComponent);

            // Assert
            Assert.IsTrue(hasPool, "HasPool should return true for a component whose pool exists.");

            Object.DestroyImmediate(prefabGo);
            Object.DestroyImmediate(spawned);
        }

        [Test]
        public void HasPool_WithComponent_ReturnsFalseIfNoPoolExists()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            var testComponent = prefabGo.AddComponent<BoxCollider>();

            // Act
            bool hasPool = _poolManager.HasPool(testComponent);

            // Assert
            Assert.IsFalse(hasPool, "HasPool should return false for a component without a pool.");

            Object.DestroyImmediate(prefabGo);
        }

        [Test]
        public void HasPool_NullComponent_ReturnsFalse()
        {
            // Act
            bool hasPool = _poolManager.HasPool((Component)null);

            // Assert
            Assert.IsFalse(hasPool, "HasPool should return false when passed a null component.");
        }
    }
}
