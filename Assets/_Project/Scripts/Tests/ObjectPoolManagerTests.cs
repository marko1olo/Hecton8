using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests
{
    [TestFixture]
    public class ObjectPoolManagerTests
    {
        private GameObject _go;
        private ObjectPoolManager _poolManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPoolManager");
            _poolManager = _go.AddComponent<ObjectPoolManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_NullRuntime_ReturnsFalse()
        {
            // Act & Assert
            Assert.That(ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(null), Is.False);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_InactiveRuntime_ReturnsFalse()
        {
            // Arrange
            _go.SetActive(false);

            // Act & Assert
            Assert.That(ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_poolManager), Is.False);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_ShuttingDownRuntime_ReturnsFalse()
        {
            // Arrange
            _poolManager.OnServiceShutdown();

            // Act & Assert
            Assert.That(ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_poolManager), Is.False);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_ValidRuntime_ReturnsTrue()
        {
            // Act & Assert
            Assert.That(ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_poolManager), Is.True);
        }
    }
}
