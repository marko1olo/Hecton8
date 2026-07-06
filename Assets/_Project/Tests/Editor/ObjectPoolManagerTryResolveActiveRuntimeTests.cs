using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerTryResolveActiveRuntimeTests
    {
        private GameObject _poolRootRegistered;
        private ObjectPoolManager _poolManagerRegistered;
        private GameObject _poolRootActive;
        private ObjectPoolManager _poolManagerActive;

        [SetUp]
        public void SetUp()
        {
            _poolRootRegistered = new GameObject("ObjectPoolManagerRegistered");
            _poolManagerRegistered = _poolRootRegistered.AddComponent<ObjectPoolManager>();

            _poolRootActive = new GameObject("ObjectPoolManagerActive");
            _poolManagerActive = _poolRootActive.AddComponent<ObjectPoolManager>();

            // Clear any lingering state
            ResetStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            GlobalRegistry.UnregisterObjectPoolService(_poolManagerRegistered);
            GlobalRegistry.UnregisterObjectPoolService(_poolManagerActive);
            UnityEngine.Object.DestroyImmediate(_poolRootRegistered);
            UnityEngine.Object.DestroyImmediate(_poolRootActive);

            ResetStaticState();
        }

        private void ResetStaticState()
        {
            var currentRegistryPool = GlobalRegistry.ObjectPoolRuntimeMirror;
            if (currentRegistryPool != null)
            {
                GlobalRegistry.UnregisterObjectPoolService(currentRegistryPool);
            }

            // Use reflection to clear the ActiveRuntimeInstance private setter
            var property = typeof(ObjectPoolManager).GetProperty("ActiveRuntimeInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (property != null)
            {
                property.SetValue(null, null);
            }
        }

        private void SetActiveRuntimeInstance(ObjectPoolManager manager)
        {
            var property = typeof(ObjectPoolManager).GetProperty("ActiveRuntimeInstance", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (property != null)
            {
                property.SetValue(null, manager);
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
            Assert.That(ObjectPoolManager.ActiveRuntimeInstance, Is.EqualTo(_poolManagerRegistered));
        }

        [Test]
        public void TryResolveActiveRuntime_WhenGlobalRegistryManagerUnusable_AndActiveRuntimeUsable_ReturnsTrueAndSetsTargetToActiveRuntime()
        {
            // Arrange
            // Register an unusable manager (disabled GameObject)
            GlobalRegistry.RegisterObjectPoolService(_poolManagerRegistered);
            _poolRootRegistered.SetActive(false);

            // Set an active runtime instance that is usable
            SetActiveRuntimeInstance(_poolManagerActive);
            _poolRootActive.SetActive(true);

            ObjectPoolManager target = null;

            // Act
            bool result = ObjectPoolManager.TryResolveActiveRuntime(ref target);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(target, Is.EqualTo(_poolManagerActive));

            // Also verifies that ClearRuntimeMirrorIfOwnedBy was called for the unusable registered manager
            Assert.That(GlobalRegistry.ObjectPoolRuntimeMirror, Is.Null);
        }

        [Test]
        public void TryResolveActiveRuntime_WhenBothGlobalRegistryAndActiveRuntimeUnusable_ReturnsFalseAndClearsTarget()
        {
            // Arrange
            // Register an unusable manager
            GlobalRegistry.RegisterObjectPoolService(_poolManagerRegistered);
            _poolRootRegistered.SetActive(false);

            // Set an unusable active runtime instance
            SetActiveRuntimeInstance(_poolManagerActive);
            _poolRootActive.SetActive(false);

            // Set target to an initial value to verify it gets cleared
            ObjectPoolManager target = _poolManagerRegistered;

            // Act
            bool result = ObjectPoolManager.TryResolveActiveRuntime(ref target);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(target, Is.Null, "Target should be set to null when resolution completely fails.");

            // Also verifies that ClearRuntimeMirrorIfOwnedBy was called for both unusable managers
            Assert.That(GlobalRegistry.ObjectPoolRuntimeMirror, Is.Null);
            Assert.That(ObjectPoolManager.ActiveRuntimeInstance, Is.Null);
        }

        [Test]
        public void TryResolveActiveRuntime_WhenNoManagersUsable_ReturnsFalseAndTargetSetToNull()
        {
            // Arrange
            // Ensure no manager is registered
            ResetStaticState();

            // Set target to an initial value to verify it changes to null
            ObjectPoolManager target = _poolManagerRegistered;

            // Act
            bool result = ObjectPoolManager.TryResolveActiveRuntime(ref target);

            // Assert
            Assert.That(result, Is.False);
            Assert.That(target, Is.Null, "Target should be null when resolution fails.");
        }
    }
}
