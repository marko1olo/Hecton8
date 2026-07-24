using System;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Core
{
                public class ObjectPoolManagerWarmupPresetsAsyncTests
    {
        private GameObject _registryObj;
        private PrefabRegistry _registry;
        private GameObject _poolManagerObj;
        private ObjectPoolManager _poolManager;
        private GameObject _prefab;

        [SetUp]
        public void Setup()
        {
            _registryObj = new GameObject("[PrefabRegistry]");
            _registry = _registryObj.AddComponent<PrefabRegistry>();

            _poolManagerObj = new GameObject("ObjectPoolManager");
            _poolManager = _poolManagerObj.AddComponent<ObjectPoolManager>();
            _poolManager.InitializeService();

            _prefab = new GameObject("TestPrefab");
            _prefab.AddComponent<BoxCollider>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_poolManagerObj != null)
                UnityEngine.Object.DestroyImmediate(_poolManagerObj);

            if (_registryObj != null)
                UnityEngine.Object.DestroyImmediate(_registryObj);

            if (_prefab != null)
                UnityEngine.Object.DestroyImmediate(_prefab);
        }

        [Test]
        public async Task WarmupPresetsAsync_WithoutPresets_ReturnsTrueImmediately()
        {
            var cts = new CancellationTokenSource();
            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task WarmupPresetsAsync_WhenShuttingDown_ReturnsFalse()
        {
            var cts = new CancellationTokenSource();
            _poolManager.OnServiceShutdown();
            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);
            Assert.IsFalse(result);
        }

        [Test]
        public async Task WarmupPresetsAsync_AlreadyCompleted_ReturnsTrue()
        {
            var cts = new CancellationTokenSource();
            await _poolManager.WarmupPresetsAsync(10.0, cts.Token);
            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);
            Assert.IsTrue(result);
        }

        [Test]
        public async Task WarmupPresetsAsync_CancellationRequested_ReturnsFalseAndResetsStartedFlag()
        {
            // Set up a valid preset to ensure we enter the loop
            var type = typeof(ObjectPoolManager);
            var warmupPresetsField = type.GetField("warmupPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var presetType = type.GetNestedType("WarmupEntry", System.Reflection.BindingFlags.NonPublic);
            var entryArray = Array.CreateInstance(presetType, 1);
            var entry = Activator.CreateInstance(presetType);

            var prefabField = presetType.GetField("prefab");
            prefabField.SetValue(entry, _prefab);

            var countField = presetType.GetField("count");
            countField.SetValue(entry, 10);

            entryArray.SetValue(entry, 0);
            warmupPresetsField.SetValue(_poolManager, entryArray);

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Pre-cancel

            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);

            Assert.IsFalse(result, "Expected WarmupPresetsAsync to return false when canceled.");

            var startedField = type.GetField("_warmupPresetsStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool isStarted = (bool)startedField.GetValue(_poolManager);
            Assert.IsFalse(isStarted, "Expected _warmupPresetsStarted to be false.");
        }

        [Test]
        public async Task WarmupPresetsAsync_WithValidPresets_AllocatesInstances()
        {
            var type = typeof(ObjectPoolManager);
            var warmupPresetsField = type.GetField("warmupPresets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var presetType = type.GetNestedType("WarmupEntry", System.Reflection.BindingFlags.NonPublic);
            var entryArray = Array.CreateInstance(presetType, 1);
            var entry = Activator.CreateInstance(presetType);

            var prefabField = presetType.GetField("prefab");
            prefabField.SetValue(entry, _prefab);

            var countField = presetType.GetField("count");
            countField.SetValue(entry, 5);

            entryArray.SetValue(entry, 0);
            warmupPresetsField.SetValue(_poolManager, entryArray);

            var cts = new CancellationTokenSource();
            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);

            Assert.IsTrue(result);
            Assert.AreEqual(5, _poolManager.GetAvailableCount(_prefab));
        }

        [Test]
        public async Task WarmupPresetsAsync_WaitTimeout_ReturnsFalse()
        {
            var type = typeof(ObjectPoolManager);
            type.GetField("_warmupPresetsStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_poolManager, true);

            var cts = new CancellationTokenSource();
            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);
            Assert.IsFalse(result);
        }
    }
}
