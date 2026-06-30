using System.Collections.Generic;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public class ObjectPoolManagerSpawnEdgeCaseTests
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
                GameObject.DestroyImmediate(_managerObj);

            if (_prefab != null)
                GameObject.DestroyImmediate(_prefab);
        }

        [Test]
        public void Spawn_WithNullPrefab_ReturnsNull()
        {
            GameObject result = _manager.Spawn((GameObject)null, Vector3.zero, Quaternion.identity, false);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_DuringShutdown_ReturnsNull()
        {
            _manager.OnServiceShutdown();
            GameObject result = _manager.Spawn(_prefab, Vector3.zero, Quaternion.identity, false);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_MissingPool_WithoutExpand_ReturnsNull()
        {
            GameObject result = _manager.Spawn(_prefab, Vector3.zero, Quaternion.identity, false);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_EmptyPool_WithoutExpand_ReturnsNull()
        {
            _manager.Warmup(_prefab, 1);
            GameObject first = _manager.Spawn(_prefab, Vector3.zero, Quaternion.identity, false);
            Assert.IsNotNull(first, "First spawn should succeed.");

            GameObject second = _manager.Spawn(_prefab, Vector3.zero, Quaternion.identity, false);
            Assert.IsNull(second, "Second spawn from exhausted pool should return null.");
        }

        [Test]
        public void Spawn_GameObject_WithNullPrefab_AndRotation_ReturnsNull()
        {
            GameObject result = _manager.Spawn((GameObject)null, Vector3.zero, Quaternion.identity);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_Component_WithNullPrefab_AndRotation_ReturnsNull()
        {
            GameObject result = _manager.Spawn((Component)null, Vector3.zero, Quaternion.identity);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_Component_WithNullPrefab_AndRotation_AndAllowExpand_ReturnsNull()
        {
            GameObject result = _manager.Spawn((Component)null, Vector3.zero, Quaternion.identity, true);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_GameObject_WithNullPrefab_NoRotation_ReturnsNull()
        {
            GameObject result = _manager.Spawn((GameObject)null, Vector3.zero);
            Assert.IsNull(result);
        }

        [Test]
        public void Spawn_Component_WithNullPrefab_NoRotation_ReturnsNull()
        {
            GameObject result = _manager.Spawn((Component)null, Vector3.zero);
            Assert.IsNull(result);
        }
    }
}
