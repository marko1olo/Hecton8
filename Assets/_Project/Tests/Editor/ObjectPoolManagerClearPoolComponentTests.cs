using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerClearPoolComponentTests
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
            {
                _poolManager.OnServiceShutdown();
            }
            if (_poolRoot != null)
            {
                Object.DestroyImmediate(_poolRoot);
            }
        }

        [Test]
        public void ClearPool_WithComponent_ClearsPool()
        {
            // Arrange
            GameObject prefabGo = new GameObject("TestPrefab");
            var testComponent = prefabGo.AddComponent<BoxCollider>();

            _poolManager.Warmup(testComponent, 2);

            Assert.IsTrue(_poolManager.HasPool(testComponent), "Pool should exist after warmup.");

            // Act
            _poolManager.ClearPool(testComponent);

            // Assert
            Assert.IsFalse(_poolManager.HasPool(testComponent), "Pool should not exist after being cleared.");

            Object.DestroyImmediate(prefabGo);
        }

        [Test]
        public void ClearPool_WithNullComponent_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _poolManager.ClearPool((Component)null), "Clearing a null component pool should not throw an exception.");
        }
    }
}
