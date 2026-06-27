using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerWarmupEdgeCaseTests
    {
        private ObjectPoolManager _poolManager;
        private GameObject _poolRoot;

        [SetUp]
        public void SetUp()
        {
            _poolRoot = new GameObject("ObjectPoolManager");
            _poolManager = _poolRoot.AddComponent<ObjectPoolManager>();
            _poolManager.InitializeService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_poolManager != null)
                _poolManager.OnServiceShutdown();

            if (_poolRoot != null)
                Object.DestroyImmediate(_poolRoot);
        }

        [Test]
        public void Warmup_WithZeroCount_DoesNotAllocateOrThrow()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            prefabGo.AddComponent<BoxCollider>(); // Adding a component so it's not completely empty

            // Act & Assert
            Assert.DoesNotThrow(() => _poolManager.Warmup(prefabGo, 0), "Warmup with 0 count should not throw.");

            // Should not create pool since count <= 0 skips pool preparation
            Assert.IsFalse(_poolManager.HasPool(prefabGo), "Warmup with 0 count should not create a pool.");
            Assert.AreEqual(0, _poolManager.GetAvailableCount(prefabGo), "Expected 0 available count after warming up 0 items.");

            Object.DestroyImmediate(prefabGo);
        }

        [Test]
        public void Warmup_WithNegativeCount_DoesNotAllocateOrThrow()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            prefabGo.AddComponent<BoxCollider>(); // Adding a component so it's not completely empty

            // Act & Assert
            Assert.DoesNotThrow(() => _poolManager.Warmup(prefabGo, -5), "Warmup with negative count should not throw.");

            // Should not create pool since count <= 0 skips pool preparation
            Assert.IsFalse(_poolManager.HasPool(prefabGo), "Warmup with negative count should not create a pool.");
            Assert.AreEqual(0, _poolManager.GetAvailableCount(prefabGo), "Expected 0 available count after warming up negative items.");

            Object.DestroyImmediate(prefabGo);
        }
    }
}
