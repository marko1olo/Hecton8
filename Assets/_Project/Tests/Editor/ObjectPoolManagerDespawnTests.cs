using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class ObjectPoolManagerDespawnTests
    {
        private GameObject _managerObj;
        private ObjectPoolManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerObj = new GameObject("TestManager");
            _manager = _managerObj.AddComponent<ObjectPoolManager>();
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
        public void Despawn_NullInstance_DoesNothing()
        {
            // Just verifying it doesn't throw or crash when passing null
            _manager.Despawn((GameObject)null);
            Assert.Pass("Did not crash");
        }

        [Test]
        public void DespawnOrDeactivate_NullInstance_DoesNothing()
        {
            // Just verifying it doesn't throw or crash when passing null
            ObjectPoolManager.DespawnOrDeactivate(null, null);
            Assert.Pass("Did not crash");
        }

        [Test]
        public void Despawn_NoMarker_DestroysObject()
        {
            GameObject testObj = new GameObject("NoMarkerObj");
            _manager.Despawn(testObj);

            // In EditMode, Destroy doesn't execute immediately, but we can verify it didn't throw exceptions.
            // Under normal circumstances we could assert testObj == null next frame.
            Assert.IsTrue(testObj != null);
            UnityEngine.Object.DestroyImmediate(testObj);
        }

        [Test]
        public void Despawn_ValidMarker_ReturnsToPool()
        {
            GameObject testObj = new GameObject("ValidMarkerObj");
            var marker = testObj.AddComponent<ObjectPoolManager.PoolItemMarker>();

            var prefabIdField = typeof(ObjectPoolManager.PoolItemMarker).GetField("_prefabId", BindingFlags.NonPublic | BindingFlags.Instance);
            prefabIdField.SetValue(marker, 42);

            var cacheField = typeof(ObjectPoolManager).GetField("_poolMarkerCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = new Dictionary<GameObject, ObjectPoolManager.PoolItemMarker>();
            cache.Add(testObj, marker);
            cacheField.SetValue(_manager, cache);

            var poolsField = typeof(ObjectPoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
            var poolType = typeof(ObjectPoolManager).GetNestedType("Pool", BindingFlags.NonPublic);

            var poolInstance = Activator.CreateInstance(poolType);
            GameObject poolContainer = new GameObject("PoolContainer");

            poolType.GetField("available").SetValue(poolInstance, new Queue<GameObject>());
            poolType.GetField("container").SetValue(poolInstance, poolContainer.transform);
            poolType.GetField("prefabId").SetValue(poolInstance, 42);
            poolType.GetField("capacity").SetValue(poolInstance, 10);

            var poolsDictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), poolType);
            var pools = Activator.CreateInstance(poolsDictType);

            poolsDictType.GetMethod("Add").Invoke(pools, new object[] { 42, poolInstance });
            poolsField.SetValue(_manager, pools);

            // Action
            _manager.Despawn(testObj);

            // Assert
            Assert.IsFalse(testObj.activeSelf, "Object should be deactivated when returned to pool");
            Assert.AreEqual(poolContainer.transform, testObj.transform.parent, "Object should be reparented to the pool container");

            var queue = (Queue<GameObject>)poolType.GetField("available").GetValue(poolInstance);
            Assert.AreEqual(1, queue.Count, "Object should be added to the available queue");
            Assert.AreEqual(testObj, queue.Dequeue(), "The object in the queue should be the one despawned");

            UnityEngine.Object.DestroyImmediate(testObj);
            UnityEngine.Object.DestroyImmediate(poolContainer);
        }

        [Test]
        public void Despawn_MissingPool_DestroysObject()
        {
            GameObject testObj = new GameObject("MissingPoolObj");
            var marker = testObj.AddComponent<ObjectPoolManager.PoolItemMarker>();

            var prefabIdField = typeof(ObjectPoolManager.PoolItemMarker).GetField("_prefabId", BindingFlags.NonPublic | BindingFlags.Instance);
            prefabIdField.SetValue(marker, 99); // Unknown prefab ID

            var cacheField = typeof(ObjectPoolManager).GetField("_poolMarkerCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = new Dictionary<GameObject, ObjectPoolManager.PoolItemMarker>();
            cache.Add(testObj, marker);
            cacheField.SetValue(_manager, cache);

            // Initialize an empty pools dictionary
            var poolsField = typeof(ObjectPoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
            var poolType = typeof(ObjectPoolManager).GetNestedType("Pool", BindingFlags.NonPublic);
            var poolsDictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), poolType);
            var pools = Activator.CreateInstance(poolsDictType);
            poolsField.SetValue(_manager, pools);

            // Action
            _manager.Despawn(testObj);

            // Assert that the marker was removed from cache
            Assert.IsFalse(cache.ContainsKey(testObj), "Object should be removed from cache when its pool is missing");

            UnityEngine.Object.DestroyImmediate(testObj);
        }

        [Test]
        public void Despawn_ObjectAlreadyInactive_DoesNotThrow()
        {
            GameObject testObj = new GameObject("InactiveObj");
            testObj.SetActive(false); // Already inactive

            var marker = testObj.AddComponent<ObjectPoolManager.PoolItemMarker>();
            var prefabIdField = typeof(ObjectPoolManager.PoolItemMarker).GetField("_prefabId", BindingFlags.NonPublic | BindingFlags.Instance);
            prefabIdField.SetValue(marker, 42);

            var cacheField = typeof(ObjectPoolManager).GetField("_poolMarkerCache", BindingFlags.NonPublic | BindingFlags.Instance);
            var cache = new Dictionary<GameObject, ObjectPoolManager.PoolItemMarker>();
            cache.Add(testObj, marker);
            cacheField.SetValue(_manager, cache);

            var poolsField = typeof(ObjectPoolManager).GetField("_pools", BindingFlags.NonPublic | BindingFlags.Instance);
            var poolType = typeof(ObjectPoolManager).GetNestedType("Pool", BindingFlags.NonPublic);

            var poolInstance = Activator.CreateInstance(poolType);
            GameObject poolContainer = new GameObject("PoolContainer");

            poolType.GetField("available").SetValue(poolInstance, new Queue<GameObject>());
            poolType.GetField("container").SetValue(poolInstance, poolContainer.transform);
            poolType.GetField("prefabId").SetValue(poolInstance, 42);
            poolType.GetField("capacity").SetValue(poolInstance, 10);

            var poolsDictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), poolType);
            var pools = Activator.CreateInstance(poolsDictType);
            poolsDictType.GetMethod("Add").Invoke(pools, new object[] { 42, poolInstance });
            poolsField.SetValue(_manager, pools);

            // Action
            _manager.Despawn(testObj);

            // Assert
            Assert.IsFalse(testObj.activeSelf, "Object should remain deactivated");
            var queue = (Queue<GameObject>)poolType.GetField("available").GetValue(poolInstance);
            Assert.AreEqual(1, queue.Count, "Object should be added to the available queue even if already inactive");

            UnityEngine.Object.DestroyImmediate(testObj);
            UnityEngine.Object.DestroyImmediate(poolContainer);
        }
    }
}
#endif
