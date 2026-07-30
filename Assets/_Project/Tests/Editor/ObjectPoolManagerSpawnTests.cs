using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerSpawnTests
    {
        private ObjectPoolManager _poolManager;
        private GameObject _poolRoot;
        private GameObject _prefabGo;

        [SetUp]
        public void SetUp()
        {
            _poolRoot = new GameObject("ObjectPoolManager");
            _poolManager = _poolRoot.AddComponent<ObjectPoolManager>();
            _poolManager.InitializeService();

            _prefabGo = new GameObject("TestPrefab");
            _prefabGo.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_poolManager != null) _poolManager.OnServiceShutdown();
            if (_prefabGo != null) Object.DestroyImmediate(_prefabGo);
            if (_poolRoot != null) Object.DestroyImmediate(_poolRoot);
        }

        [Test]
        public void Spawn_GameObject_ReturnsValidInstanceAndDecreasesAvailableCount()
        {
            // Arrange
            _poolManager.Warmup(_prefabGo, 1);

            // Initial available count should be 1
            bool initialResult = _poolManager.TryGetAvailableCountForPooledInstance(_prefabGo, out int initialCount);
            Assert.IsTrue(initialResult, "Expected to successfully query count for pooled instance.");
            Assert.AreEqual(1, initialCount, "Expected count to be 1 initially.");

            // Act
            GameObject spawned = _poolManager.Spawn(_prefabGo, Vector3.one, Quaternion.Euler(0, 90, 0));

            // Assert
            Assert.IsNotNull(spawned, "Spawned GameObject should not be null.");
            Assert.AreEqual(Vector3.one, spawned.transform.position, "Spawned position should match requested position.");
            Assert.AreEqual(Quaternion.Euler(0, 90, 0), spawned.transform.rotation, "Spawned rotation should match requested rotation.");
            Assert.IsTrue(spawned.name.StartsWith("TestPrefab"), "Spawned object should originate from the correct prefab.");

            // Available count should drop
            bool finalResult = _poolManager.TryGetAvailableCountForPooledInstance(spawned, out int finalCount);
            Assert.IsTrue(finalResult, "Expected to successfully query count for pooled instance.");
            Assert.AreEqual(0, finalCount, "Expected count to be 0 after spawning.");
        }

        [Test]
        public void Spawn_Component_ReturnsValidInstanceAndDecreasesAvailableCount()
        {
            // Arrange
            Component prefabComponent = _prefabGo.GetComponent<BoxCollider>();
            _poolManager.Warmup(prefabComponent, 1);

            // Initial available count should be 1
            bool initialResult = _poolManager.TryGetAvailableCountForPooledInstance(_prefabGo, out int initialCount);
            Assert.IsTrue(initialResult, "Expected to successfully query count for pooled instance.");
            Assert.AreEqual(1, initialCount, "Expected count to be 1 initially.");

            // Act
            GameObject spawned = _poolManager.Spawn(prefabComponent, Vector3.one, Quaternion.Euler(0, 90, 0));

            // Assert
            Assert.IsNotNull(spawned, "Spawned GameObject should not be null.");
            Assert.AreEqual(Vector3.one, spawned.transform.position, "Spawned position should match requested position.");
            Assert.AreEqual(Quaternion.Euler(0, 90, 0), spawned.transform.rotation, "Spawned rotation should match requested rotation.");
            Assert.IsTrue(spawned.name.StartsWith("TestPrefab"), "Spawned object should originate from the correct prefab.");

            // Available count should drop
            bool finalResult = _poolManager.TryGetAvailableCountForPooledInstance(spawned, out int finalCount);
            Assert.IsTrue(finalResult, "Expected to successfully query count for pooled instance.");
            Assert.AreEqual(0, finalCount, "Expected count to be 0 after spawning.");
        }
    }
}
