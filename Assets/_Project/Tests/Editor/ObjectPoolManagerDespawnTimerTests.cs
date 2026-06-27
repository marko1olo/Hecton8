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
    public class ObjectPoolManagerDespawnTimerTests
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
        public void StartTimer_ZeroDelay_CallsDespawnNowOrDestroy_AndReturnsToPool()
        {
            GameObject testObj = new GameObject("TestTimerObj");
            var timer = testObj.AddComponent<ObjectPoolManager.DespawnTimer>();
            var marker = testObj.AddComponent<ObjectPoolManager.PoolItemMarker>();

            var ownerField = typeof(ObjectPoolManager.PoolItemMarker).GetField("_owner", BindingFlags.NonPublic | BindingFlags.Instance);
            ownerField.SetValue(marker, _manager);

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

            // Set static runtime active instance so IsRuntimeOwnerUsableForRegistry doesn't block the mock
            var runtimeActiveField = typeof(ObjectPoolManager).GetField("s_runtimeActive", BindingFlags.NonPublic | BindingFlags.Static);
            var prevRuntimeActive = runtimeActiveField.GetValue(null);
            runtimeActiveField.SetValue(null, _manager);

            try
            {
                // Action: Start timer with 0 delay
                timer.StartTimer(0f);

                // Assert: Timer should not be active
                var activeField = typeof(ObjectPoolManager.DespawnTimer).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
                bool isActive = (bool)activeField.GetValue(timer);
                Assert.IsFalse(isActive, "Timer should not be activated when delay is 0 or less");

                // Assert: Object should be returned to pool
                Assert.IsFalse(testObj.activeSelf, "Object should be deactivated when returned to pool");
                Assert.AreEqual(poolContainer.transform, testObj.transform.parent, "Object should be reparented to the pool container");

                var queue = (Queue<GameObject>)poolType.GetField("available").GetValue(poolInstance);
                Assert.AreEqual(1, queue.Count, "Object should be added to the available queue");
                Assert.AreEqual(testObj, queue.Dequeue(), "The object in the queue should be the one despawned");
            }
            finally
            {
                runtimeActiveField.SetValue(null, prevRuntimeActive);
                UnityEngine.Object.DestroyImmediate(testObj);
                UnityEngine.Object.DestroyImmediate(poolContainer);
            }
        }

        [Test]
        public void StartTimer_NegativeDelay_CallsDespawnNowOrDestroy()
        {
            GameObject testObj = new GameObject("TestTimerObj");
            var timer = testObj.AddComponent<ObjectPoolManager.DespawnTimer>();

            // Action
            timer.StartTimer(-1f);

            // Assert
            var activeField = typeof(ObjectPoolManager.DespawnTimer).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
            bool isActive = (bool)activeField.GetValue(timer);

            Assert.IsFalse(isActive, "Timer should not be activated when delay is negative");

            UnityEngine.Object.DestroyImmediate(testObj);
        }
    }
}
#endif
