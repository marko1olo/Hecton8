using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerTryResolveActiveRuntimeTests
    {
        private GameObject _poolRootRegistered;
        private ObjectPoolManager _poolManagerRegistered;

        [SetUp]
        public void SetUp()
        {
            _poolRootRegistered = new GameObject("ObjectPoolManagerRegistered");
            _poolManagerRegistered = _poolRootRegistered.AddComponent<ObjectPoolManager>();

            // Clear any lingering state
            ResetStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            GlobalRegistry.UnregisterObjectPoolService(_poolManagerRegistered);
            UnityEngine.Object.DestroyImmediate(_poolRootRegistered);

            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var currentRegistryPool = GlobalRegistry.ObjectPoolRuntimeMirror;
            if (currentRegistryPool != null)
            {
                GlobalRegistry.UnregisterObjectPoolService(currentRegistryPool);
            }
        }

        [Test]
        public void TryResolveActiveRuntime_WhenGlobalRegistryHasUsableManager_ReturnsTrueAndSetsTarget()
        {
            // Arrange
            GlobalRegistry.RegisterObjectPoolService(_poolManagerRegistered);
            _poolRootRegistered.SetActive(true);

            ObjectPoolManager target = null;

            // Act
            bool result = ObjectPoolManager.TryResolveActiveRuntime(ref target);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(target, Is.EqualTo(_poolManagerRegistered));
        }

        [Test]
        public void TryResolveActiveRuntime_WhenNoManagersUsable_ReturnsFalseAndTargetRemainsUnchanged()
        {
            // Arrange
            // Ensure no manager is registered
            ResetStaticState();

            // Set target to an initial value to verify it does not change
            ObjectPoolManager target = _poolManagerRegistered;

            // Act
            bool result = ObjectPoolManager.TryResolveActiveRuntime(ref target);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(target, Is.EqualTo(_poolManagerRegistered), "Target should remain unchanged when resolution fails.");
        }
    }
}
