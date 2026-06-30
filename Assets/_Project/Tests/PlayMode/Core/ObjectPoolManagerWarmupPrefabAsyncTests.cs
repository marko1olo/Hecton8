using System;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Core
{
    public class ObjectPoolManagerWarmupPrefabAsyncTests
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
        public async Task WarmupPrefabAsync_ValidPrefab_SpawnsCorrectCount()
        {
            var cts = new CancellationTokenSource();
            bool result = await _poolManager.WarmupPrefabAsync(_prefab, 5, cts.Token);
            Assert.IsTrue(result);
            Assert.AreEqual(5, _poolManager.GetAvailableCount(_prefab));
        }
        [Test]
        public async Task WarmupPrefabAsync_NullPrefab_ReturnsTrueAndDoesNothing()
        {
            var cts = new CancellationTokenSource();
            bool result = await _poolManager.WarmupPrefabAsync((GameObject)null, 5, cts.Token);
            Assert.IsTrue(result);
        }
        [Test]
        public async Task WarmupPrefabAsync_ZeroOrNegativeCount_ReturnsTrueAndDoesNothing()
        {
            var cts = new CancellationTokenSource();
            bool resultZero = await _poolManager.WarmupPrefabAsync(_prefab, 0, cts.Token);
            bool resultNegative = await _poolManager.WarmupPrefabAsync(_prefab, -5, cts.Token);
            Assert.IsTrue(resultZero);
            Assert.IsTrue(resultNegative);
            Assert.AreEqual(0, _poolManager.GetAvailableCount(_prefab));
        }
        [Test]
        public async Task WarmupPrefabAsync_WhenShuttingDown_ReturnsTrue()
        {
            var cts = new CancellationTokenSource();
            _poolManager.OnServiceShutdown();
            bool result = await _poolManager.WarmupPrefabAsync(_prefab, 5, cts.Token);
            Assert.IsTrue(result);
            Assert.AreEqual(0, _poolManager.GetAvailableCount(_prefab));
        }
        [Test]
        public void WarmupPrefabAsync_CancellationRequested_ThrowsOperationCanceledException()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await _poolManager.WarmupPrefabAsync(_prefab, 5, cts.Token);
            });
        }
    }
}
