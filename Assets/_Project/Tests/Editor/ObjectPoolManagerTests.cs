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
        public void TryGetAvailableCountForPooledInstance_NullInstance_ReturnsFalse()
        {
            // Act
            bool result = _poolManager.TryGetAvailableCountForPooledInstance(null, out int count);

            // Assert
            Assert.IsFalse(result, "Expected to return false for null instance.");
            Assert.AreEqual(0, count, "Expected count to be 0 for null instance.");
        }

        [Test]
        public void TryGetAvailableCountForPooledInstance_UnpooledInstance_ReturnsFalse()
        {
            // Arrange
            GameObject unpooledGo = new GameObject("Unpooled");

            // Act
            bool result = _poolManager.TryGetAvailableCountForPooledInstance(unpooledGo, out int count);

            // Assert
            Assert.IsFalse(result, "Expected to return false for unpooled instance.");
            Assert.AreEqual(0, count, "Expected count to be 0 for unpooled instance.");

            Object.DestroyImmediate(unpooledGo);
        }

        [Test]
        public void TryGetAvailableCountForPooledInstance_PooledInstance_ReturnsTrueAndCount()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            var testComponent = prefabGo.AddComponent<BoxCollider>();

            // Allow pool expansion since there's no pre-warmed pool here
            GameObject spawned1 = _poolManager.Spawn(prefabGo, Vector3.zero, Quaternion.identity, true);
            GameObject spawned2 = _poolManager.Spawn(prefabGo, Vector3.zero, Quaternion.identity, true);

            // Ensure count is currently 0 since both are spawned
            bool initialResult = _poolManager.TryGetAvailableCountForPooledInstance(spawned1, out int initialCount);
            Assert.IsTrue(initialResult, "Expected to successfully query count for pooled instance.");
            Assert.AreEqual(0, initialCount, "Expected count to be 0 when all objects are spawned out of the pool.");

            // Despawn spawned2 so that there is 1 available in the pool
            _poolManager.Despawn(spawned2);

            // Act
            // Now query available count using spawned1
            bool result = _poolManager.TryGetAvailableCountForPooledInstance(spawned1, out int count);

            // Assert
            Assert.IsTrue(result, "Expected to return true for pooled instance.");
            Assert.AreEqual(1, count, "Expected count to be 1 since spawned2 was despawned to the pool.");

            Object.DestroyImmediate(prefabGo);
            if (spawned1 != null) Object.DestroyImmediate(spawned1);
            if (spawned2 != null) Object.DestroyImmediate(spawned2);
        }
    }
}
