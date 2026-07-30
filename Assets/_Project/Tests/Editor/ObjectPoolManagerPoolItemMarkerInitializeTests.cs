using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerPoolItemMarkerInitializeTests
    {
        private GameObject _gameObject;
        private ObjectPoolManager.PoolItemMarker _marker;
        private ObjectPoolManager _poolManager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestMarkerObject");
            _marker = _gameObject.AddComponent<ObjectPoolManager.PoolItemMarker>();

            var poolManagerGo = new GameObject("TestPoolManager");
            _poolManager = poolManagerGo.AddComponent<ObjectPoolManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null) Object.DestroyImmediate(_gameObject);
            if (_poolManager != null) Object.DestroyImmediate(_poolManager.gameObject);
        }

        private class MockPoolable : MonoBehaviour, IPoolable
        {
            public void OnSpawn() { }
            public void OnDespawn() { }
        }

        [Test]
        public void Initialize_FirstCall_SetsAllFieldsCorrectly()
        {
            // Arrange
            int expectedPrefabId = 42;
            var expectedRenderer = _gameObject.AddComponent<MeshRenderer>();
            var expectedRigidbody = _gameObject.AddComponent<Rigidbody>();
            var expectedDespawnTimer = _gameObject.AddComponent<ObjectPoolManager.DespawnTimer>();

            var mockPoolable = _gameObject.AddComponent<MockPoolable>();
            var poolables = new List<IPoolable> { mockPoolable };

            var rootComponents = new List<Component> { expectedRenderer, expectedRigidbody };

            // Act
            _marker.Initialize(
                _poolManager,
                expectedPrefabId,
                expectedRenderer,
                expectedRigidbody,
                expectedDespawnTimer,
                poolables,
                rootComponents
            );

            // Assert
            Assert.AreEqual(_poolManager, _marker.Owner, "Owner should be set.");
            Assert.AreEqual(expectedPrefabId, _marker.PrefabId, "PrefabId should be set.");
            Assert.AreEqual(expectedRenderer, _marker.RootRenderer, "RootRenderer should be set.");
            Assert.AreEqual(expectedRigidbody, _marker.RootRigidbody, "RootRigidbody should be set.");
            Assert.AreEqual(expectedDespawnTimer, _marker.RootDespawnTimer, "RootDespawnTimer should be set.");

            Assert.AreEqual(1, _marker.PoolableCount, "Poolable count should match.");
            Assert.AreEqual(mockPoolable, _marker.GetPoolable(0), "Poolable should match.");

            bool hasRenderer = _marker.TryGetCachedComponent<MeshRenderer>(out var cachedRenderer);
            Assert.IsTrue(hasRenderer, "Should be able to get cached MeshRenderer.");
            Assert.AreEqual(expectedRenderer, cachedRenderer, "Cached MeshRenderer should match.");
        }

        [Test]
        public void Initialize_SecondCall_DoesNotOverrideInitializedFields()
        {
            // Arrange
            int initialPrefabId = 42;
            var poolables = new List<IPoolable>();
            var rootComponents = new List<Component>();

            _marker.Initialize(
                _poolManager,
                initialPrefabId,
                null,
                null,
                null,
                poolables,
                rootComponents
            );

            var secondPoolManager = new GameObject("SecondPoolManager").AddComponent<ObjectPoolManager>();
            int newPrefabId = 99;
            var newPoolables = new List<IPoolable> { _gameObject.AddComponent<MockPoolable>() };

            // Act
            _marker.Initialize(
                secondPoolManager,
                newPrefabId,
                null,
                null,
                null,
                newPoolables,
                null
            );

            // Assert
            Assert.AreEqual(secondPoolManager, _marker.Owner, "Owner should be updated to secondPoolManager.");
            Assert.AreEqual(initialPrefabId, _marker.PrefabId, "PrefabId should NOT be updated, it should remain initialPrefabId.");
            Assert.AreEqual(0, _marker.PoolableCount, "Poolables should NOT be updated.");

            Object.DestroyImmediate(secondPoolManager.gameObject);
        }

        [Test]
        public void Initialize_WithNullLists_InitializesToEmptyArrays()
        {
            // Act
            _marker.Initialize(
                _poolManager,
                10,
                null,
                null,
                null,
                null,
                null
            );

            // Assert
            Assert.AreEqual(0, _marker.PoolableCount, "Poolable count should be 0 when null list is passed.");

            bool hasComponent = _marker.TryGetCachedComponent<MeshRenderer>(out var _);
            Assert.IsFalse(hasComponent, "Should not crash and return false when searching empty root components.");
        }

        [Test]
        public void Initialize_OnlyUpdatesNullRootReferences()
        {
            // Arrange
            var firstRenderer = _gameObject.AddComponent<MeshRenderer>();
            var firstRigidbody = _gameObject.AddComponent<Rigidbody>();
            var firstDespawnTimer = _gameObject.AddComponent<ObjectPoolManager.DespawnTimer>();

            _marker.Initialize(
                _poolManager,
                1,
                firstRenderer,
                null,
                firstDespawnTimer,
                null,
                null
            );

            var secondRenderer = new GameObject().AddComponent<MeshRenderer>();
            var secondRigidbody = new GameObject().AddComponent<Rigidbody>();
            var secondDespawnTimer = new GameObject().AddComponent<ObjectPoolManager.DespawnTimer>();

            // Act
            _marker.Initialize(
                _poolManager,
                1,
                secondRenderer,
                secondRigidbody,
                secondDespawnTimer,
                null,
                null
            );

            // Assert
            Assert.AreEqual(firstRenderer, _marker.RootRenderer, "RootRenderer should remain the first one.");
            Assert.AreEqual(secondRigidbody, _marker.RootRigidbody, "RootRigidbody should be updated since it was initially null.");
            Assert.AreEqual(firstDespawnTimer, _marker.RootDespawnTimer, "RootDespawnTimer should remain the first one.");

            Object.DestroyImmediate(secondRenderer.gameObject);
            Object.DestroyImmediate(secondRigidbody.gameObject);
            Object.DestroyImmediate(secondDespawnTimer.gameObject);
        }
    }
}
