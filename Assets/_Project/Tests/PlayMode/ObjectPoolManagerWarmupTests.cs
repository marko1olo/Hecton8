using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Core;

namespace Hecton8.Tests.PlayMode
{
    public class ObjectPoolManagerWarmupTests
    {
        private GameObject _managerObject;
        private ObjectPoolManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("ObjectPoolManager");
            _manager = _managerObject.AddComponent<ObjectPoolManager>();
            _manager.InitializeService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                _manager.OnServiceShutdown();
            }

            if (_managerObject != null)
            {
                Object.Destroy(_managerObject);
            }
        }

        [Test]
        public void Warmup_WithValidPrefabAndCount_AllocatesInstances()
        {
            GameObject prefab = new GameObject("TestPrefab");

            _manager.Warmup(prefab, 5);

            int availableCount = _manager.GetAvailableCount(prefab);
            Assert.AreEqual(5, availableCount);

            Object.Destroy(prefab);
        }

        [Test]
        public void Warmup_ValidPrefab_InstancesAreCreatedAndDeactivated()
        {
            GameObject prefab = new GameObject("TestPrefab");
            prefab.AddComponent<BoxCollider>();

            _manager.Warmup(prefab, 3);

            Assert.IsTrue(_manager.HasPool(prefab), "Pool should exist after warmup.");
            Assert.AreEqual(3, _manager.GetAvailableCount(prefab), "Available count should be 3.");

            Object.Destroy(prefab);
        }

        [Test]
        public void Warmup_WithNullPrefab_DoesNotThrow()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[ObjectPoolManager] Warmup: prefab is null!");
            Assert.DoesNotThrow(() =>
            {
                _manager.Warmup((GameObject)null, 5);
            });
        }

        [Test]
        public void Warmup_WithCountZeroOrLess_DoesNotAllocate()
        {
            GameObject prefab = new GameObject("TestPrefab");

            _manager.Warmup(prefab, 0);
            Assert.AreEqual(0, _manager.GetAvailableCount(prefab));

            _manager.Warmup(prefab, -5);
            Assert.AreEqual(0, _manager.GetAvailableCount(prefab));

            Object.Destroy(prefab);
        }

        [Test]
        public void Warmup_WhenShuttingDown_DoesNotAllocate()
        {
            GameObject prefab = new GameObject("TestPrefab");

            _manager.OnServiceShutdown();
            _manager.Warmup(prefab, 5);

            Assert.AreEqual(0, _manager.GetAvailableCount(prefab));

            Object.Destroy(prefab);
        }
    }
}
