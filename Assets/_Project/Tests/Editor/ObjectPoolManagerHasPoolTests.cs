using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public class ObjectPoolManagerHasPoolTests
    {
        private GameObject _managerObj;
        private ObjectPoolManager _manager;
        private GameObject _prefab;
        private BoxCollider _prefabComponent;

        [SetUp]
        public void Setup()
        {
            _managerObj = new GameObject("ObjectPoolManager");
            _manager = _managerObj.AddComponent<ObjectPoolManager>();
            _manager.InitializeService();

            _prefab = new GameObject("TestPrefab");
            _prefabComponent = _prefab.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_manager != null)
            {
                _manager.OnServiceShutdown();
            }

            if (_managerObj != null)
                GameObject.DestroyImmediate(_managerObj);

            if (_prefab != null)
                GameObject.DestroyImmediate(_prefab);
        }

        [Test]
        public void HasPool_NullGameObject_ReturnsFalse()
        {
            Assert.IsFalse(_manager.HasPool((GameObject)null));
        }

        [Test]
        public void HasPool_NullComponent_ReturnsFalse()
        {
            Assert.IsFalse(_manager.HasPool((Component)null));
        }

        [Test]
        public void HasPool_ServiceShuttingDown_ReturnsFalse()
        {
            _manager.Warmup(_prefab, 1);
            _manager.OnServiceShutdown();
            Assert.IsFalse(_manager.HasPool(_prefab));
        }

        [Test]
        public void HasPool_NoPoolCreated_ReturnsFalse()
        {
            Assert.IsFalse(_manager.HasPool(_prefab));
        }

        [Test]
        public void HasPool_PoolCreated_ReturnsTrue()
        {
            _manager.Warmup(_prefab, 1);
            Assert.IsTrue(_manager.HasPool(_prefab));
        }

        [Test]
        public void HasPool_ComponentOverload_ReturnsTrue()
        {
            _manager.Warmup(_prefab, 1);
            Assert.IsTrue(_manager.HasPool(_prefabComponent));
        }
    }
}
