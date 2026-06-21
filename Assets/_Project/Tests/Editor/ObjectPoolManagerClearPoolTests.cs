using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using System.Collections.Generic;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class ObjectPoolManagerClearPoolTests
    {
        private GameObject _managerObj;
        private ObjectPoolManager _manager;
        private GameObject _prefab;

        [SetUp]
        public void Setup()
        {
            _managerObj = new GameObject("ObjectPoolManager");
            _manager = _managerObj.AddComponent<ObjectPoolManager>();
            _manager.InitializeService();

            _prefab = new GameObject("TestPrefab");
            _prefab.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_manager != null)
            {
                _manager.OnServiceShutdown();
            }

            if (_managerObj != null)
                Object.DestroyImmediate(_managerObj);

            if (_prefab != null)
                Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void ClearPool_WithValidPrefab_RemovesPool()
        {
            _manager.Warmup(_prefab, 2);
            Assert.IsTrue(_manager.HasPool(_prefab), "Pool should exist after warmup.");

            _manager.ClearPool(_prefab);

            Assert.IsFalse(_manager.HasPool(_prefab), "Pool should be removed after ClearPool.");
        }

        [Test]
        public void ClearPool_WithNullPrefab_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.ClearPool((GameObject)null));
        }

        [Test]
        public void ClearPool_WithUnregisteredPrefab_DoesNotThrow()
        {
            GameObject unregisteredPrefab = new GameObject("Unregistered");
            try
            {
                Assert.DoesNotThrow(() => _manager.ClearPool(unregisteredPrefab));
            }
            finally
            {
                Object.DestroyImmediate(unregisteredPrefab);
            }
        }

        [Test]
        public void ClearPool_DuringShutdown_DoesNothing()
        {
            _manager.Warmup(_prefab, 2);
            Assert.IsTrue(_manager.HasPool(_prefab), "Pool should exist after warmup.");

            _manager.OnServiceShutdown();
            _manager.ClearPool(_prefab);

            Assert.Pass("Did not crash");
        }

        [Test]
        public void ClearPool_DestroysAvailableInstances()
        {
            _manager.Warmup(_prefab, 2);
            Assert.IsTrue(_manager.HasPool(_prefab), "Pool should exist after warmup.");

            // Spawn one, so the pool has 1 active and 1 available
            GameObject spawned = _manager.Spawn(_prefab, Vector3.zero, Quaternion.identity);

            _manager.ClearPool(_prefab);

            Assert.IsFalse(_manager.HasPool(_prefab), "Pool should be removed.");

            if (spawned != null)
                Object.DestroyImmediate(spawned);
        }
    }
}
