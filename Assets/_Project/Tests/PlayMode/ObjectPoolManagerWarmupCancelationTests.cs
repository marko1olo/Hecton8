using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Core;

namespace Hecton8.Tests.PlayMode
{
    public class ObjectPoolManagerWarmupCancelationTests
    {
        private GameObject _managerObject;
        private ObjectPoolManager _manager;
        private GameObject _registryObj;

        [SetUp]
        public void SetUp()
        {
            _registryObj = new GameObject("[PrefabRegistry]");
            _registryObj.AddComponent<PrefabRegistry>();

            _managerObject = new GameObject("ObjectPoolManager");
            _manager = _managerObject.AddComponent<ObjectPoolManager>();
            _manager.InitializeService();
        }

        [TearDown]
        public void TearDown()
        {
            if (_manager != null)
                _manager.OnServiceShutdown();

            if (_managerObject != null)
                UnityEngine.Object.DestroyImmediate(_managerObject);

            if (_registryObj != null)
                UnityEngine.Object.DestroyImmediate(_registryObj);
        }

        [Test]
        public void WarmupPresetsAsync_WhenCanceledBeforeStart_ReturnsFalseAndResetsStartedFlag()
        {
            // Arrange
            GameObject dummyPrefab = new GameObject("DummyPrefab");

            _manager.warmupPresets = new WarmupEntry[] {
                new WarmupEntry { prefab = dummyPrefab, count = 100 }
            };

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            // Set _warmupPresetsStarted to true via reflection to verify it gets reset
            FieldInfo startedField = typeof(ObjectPoolManager).GetField("_warmupPresetsStarted", BindingFlags.NonPublic | BindingFlags.Instance);
            startedField.SetValue(_manager, true);

            // Act
            // The method actually catches OperationCanceledException, sets _warmupPresetsStarted = false, and returns false.
            var task = _manager.WarmupPresetsAsync(10.0, cts.Token);
            bool result = task.GetAwaiter().GetResult();

            // Assert
            Assert.IsFalse(result, "WarmupPresetsAsync should return false when canceled.");

            // Verify the state flag was reset
            bool isStarted = (bool)startedField.GetValue(_manager);
            Assert.IsFalse(isStarted, "_warmupPresetsStarted should be reset to false when an OperationCanceledException occurs.");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(dummyPrefab);
        }
    }
}
