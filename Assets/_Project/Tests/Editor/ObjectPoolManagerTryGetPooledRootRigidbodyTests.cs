using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class ObjectPoolManagerTryGetPooledRootRigidbodyTests
    {
        private GameObject _managerObj;
        private ObjectPoolManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerObj = new GameObject("TestManager");
            _manager = _managerObj.AddComponent<ObjectPoolManager>();
            _manager.InitializeService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerObj != null)
            {
                UnityEngine.Object.DestroyImmediate(_managerObj);
            }
        }

        [Test]
        public void TryGetPooledRootRigidbody_NullInstance_ReturnsFalse()
        {
            bool result = _manager.TryGetPooledRootRigidbody(null, out Rigidbody rigidbody);
            Assert.IsFalse(result);
            Assert.IsNull(rigidbody);
        }

        [Test]
        public void TryGetPooledRootRigidbody_NoMarker_ReturnsFalse()
        {
            GameObject testObj = new GameObject("NoMarkerObj");
            bool result = _manager.TryGetPooledRootRigidbody(testObj, out Rigidbody rigidbody);
            Assert.IsFalse(result);
            Assert.IsNull(rigidbody);
            UnityEngine.Object.DestroyImmediate(testObj);
        }

        [Test]
        public void TryGetPooledRootRigidbody_HasMarkerWithRigidbody_ReturnsTrue()
        {
            GameObject prefabGo = new GameObject("TestPrefabWithRb");
            prefabGo.AddComponent<Rigidbody>();

            GameObject spawned = _manager.Spawn(prefabGo, Vector3.zero, Quaternion.identity);

            bool result = _manager.TryGetPooledRootRigidbody(spawned, out Rigidbody outRigidbody);

            Assert.IsTrue(result);
            Assert.IsNotNull(outRigidbody);
            Assert.AreEqual(spawned.GetComponent<Rigidbody>(), outRigidbody);

            UnityEngine.Object.DestroyImmediate(prefabGo);
            UnityEngine.Object.DestroyImmediate(spawned);
        }

        [Test]
        public void TryGetPooledRootRigidbody_HasMarkerWithoutRigidbody_ReturnsFalse()
        {
            GameObject prefabGo = new GameObject("TestPrefabWithoutRb");
            prefabGo.AddComponent<BoxCollider>();

            GameObject spawned = _manager.Spawn(prefabGo, Vector3.zero, Quaternion.identity);

            bool result = _manager.TryGetPooledRootRigidbody(spawned, out Rigidbody outRigidbody);

            Assert.IsFalse(result);
            Assert.IsNull(outRigidbody);

            UnityEngine.Object.DestroyImmediate(prefabGo);
            UnityEngine.Object.DestroyImmediate(spawned);
        }
    }
}
#endif
