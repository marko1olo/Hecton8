using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class ObjectPoolManagerCanDespawnWithoutDestroyTests
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
            if (_poolRoot != null)
                Object.DestroyImmediate(_poolRoot);
        }

        [Test]
        public void CanDespawnWithoutDestroy_WithNullInstance_ReturnsFalse()
        {
            bool result = _poolManager.CanDespawnWithoutDestroy(null);
            Assert.IsFalse(result, "Expected false for null instance.");
        }

        [Test]
        public void CanDespawnWithoutDestroy_WithNormalInstantiatedObject_ReturnsFalse()
        {
            GameObject normalObject = new GameObject("NormalObject");

            bool result = _poolManager.CanDespawnWithoutDestroy(normalObject);

            Assert.IsFalse(result, "Expected false for an object not spawned from the pool.");

            Object.DestroyImmediate(normalObject);
        }

        [Test]
        public void CanDespawnWithoutDestroy_WithNormalObjectWhenPoolsInitialized_ReturnsFalse()
        {
            GameObject prefabGo = new GameObject("TestPrefab");
            GameObject spawned = _poolManager.Spawn(prefabGo, Vector3.zero, Quaternion.identity, true);

            GameObject normalObject = new GameObject("NormalObject");

            bool result = _poolManager.CanDespawnWithoutDestroy(normalObject);

            Assert.IsFalse(result, "Expected false for an object not spawned from the pool, even when pools are initialized.");

            Object.DestroyImmediate(normalObject);
            Object.DestroyImmediate(prefabGo);
            if (spawned != null) Object.DestroyImmediate(spawned);
        }

        [Test]
        public void CanDespawnWithoutDestroy_WithPooledInstance_ReturnsTrue()
        {
            GameObject prefabGo = new GameObject("TestPrefab");

            // Spawn with allowExpand=true to create the pool and marker
            GameObject spawned = _poolManager.Spawn(prefabGo, Vector3.zero, Quaternion.identity, true);

            bool result = _poolManager.CanDespawnWithoutDestroy(spawned);

            Assert.IsTrue(result, "Expected true for an object spawned from the pool.");

            Object.DestroyImmediate(prefabGo);
            if (spawned != null) Object.DestroyImmediate(spawned);
        }

        [Test]
        public void CanDespawnWithoutDestroy_WithClearedPool_ReturnsFalse()
        {
            GameObject prefabGo = new GameObject("TestPrefab");

            // Spawn with allowExpand=true to create the pool and marker
            GameObject spawned = _poolManager.Spawn(prefabGo, Vector3.zero, Quaternion.identity, true);

            // Clear the pool so it no longer exists in the manager's dictionary
            _poolManager.ClearPool(prefabGo);

            bool result = _poolManager.CanDespawnWithoutDestroy(spawned);

            Assert.IsFalse(result, "Expected false because the pool for the instance's prefab was cleared.");

            Object.DestroyImmediate(prefabGo);
            if (spawned != null) Object.DestroyImmediate(spawned);
        }
    }
}
