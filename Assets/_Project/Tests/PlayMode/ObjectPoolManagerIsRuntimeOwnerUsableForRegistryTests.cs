using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.PlayMode
{
    public class ObjectPoolManagerIsRuntimeOwnerUsableForRegistryTests
    {
        private GameObject _managerObject;
        private ObjectPoolManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerObject = new GameObject("ObjectPoolManager");
            _manager = _managerObject.AddComponent<ObjectPoolManager>();
            _manager.InitializeService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
            {
                _manager.OnServiceShutdown();
            }

            if (_managerObject != null)
            {
                Object.DestroyImmediate(_managerObject);
            }
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_WithValidRuntime_ReturnsTrue()
        {
            bool result = ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_manager);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_WithNullRuntime_ReturnsFalse()
        {
            bool result = ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(null);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_WithInactiveGameObject_ReturnsFalse()
        {
            _managerObject.SetActive(false);

            bool result = ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_manager);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_WithDisabledComponent_ReturnsFalse()
        {
            _manager.enabled = false;

            bool result = ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_manager);

            Assert.That(result, Is.False);
        }

        [Test]
        public void IsRuntimeOwnerUsableForRegistry_WhenShuttingDown_ReturnsFalse()
        {
            _manager.OnServiceShutdown();

            bool result = ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(_manager);

            Assert.That(result, Is.False);
        }
    }
}
