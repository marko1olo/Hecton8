using System.Collections;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.PlayMode
{
    public class ObjectPoolManagerPlayModeTests
    {
        private ObjectPoolManager _poolManager;
        private GameObject _managerObj;

        [SetUp]
        public void Setup()
        {
            _managerObj = new GameObject("ObjectPoolManager");
            _poolManager = _managerObj.AddComponent<ObjectPoolManager>();
            _poolManager.InitializeService();
        }

        [TearDown]
        public void Teardown()
        {
            if (_managerObj != null)
                Object.Destroy(_managerObj);
        }

        [UnityTest]
        public IEnumerator Despawn_ValidInstance_ReturnsToPool()
        {
            GameObject prefab = new GameObject("MockPrefab");
            _poolManager.Warmup(prefab, 1);

            GameObject spawnedInstance = _poolManager.Spawn(prefab, Vector3.zero);
            Assert.IsNotNull(spawnedInstance, "Spawned instance should not be null.");
            Assert.IsTrue(spawnedInstance.activeSelf, "Spawned instance should be active.");

            int availableBeforeDespawn = _poolManager.GetAvailableCount(prefab);

            _poolManager.Despawn(spawnedInstance);

            Assert.IsFalse(spawnedInstance.activeSelf, "Despawned instance should be inactive.");
            Assert.AreEqual(availableBeforeDespawn + 1, _poolManager.GetAvailableCount(prefab), "Available count should increase by 1 after despawning.");

            Object.Destroy(prefab);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Despawn_NullInstance_DoesNothing()
        {
            Assert.DoesNotThrow(() => _poolManager.Despawn((GameObject)null));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Despawn_NoMarker_DestroysInstance()
        {
            GameObject fakeInstance = new GameObject("FakeInstance");

            _poolManager.Despawn(fakeInstance);

            yield return null; // Wait for Destroy

            Assert.IsTrue(fakeInstance == null, "Instance without marker should be destroyed.");
        }

        [UnityTest]
        public IEnumerator Despawn_MissingPool_DestroysInstance()
        {
            GameObject prefab = new GameObject("MockPrefab");
            _poolManager.Warmup(prefab, 1);

            GameObject spawnedInstance = _poolManager.Spawn(prefab, Vector3.zero);
            Assert.IsNotNull(spawnedInstance);

            _poolManager.ClearPool(prefab);

            _poolManager.Despawn(spawnedInstance);

            yield return null; // Wait for Destroy

            Assert.IsTrue(spawnedInstance == null, "Instance with missing pool should be destroyed.");
            Object.Destroy(prefab);
        }
    }
}
