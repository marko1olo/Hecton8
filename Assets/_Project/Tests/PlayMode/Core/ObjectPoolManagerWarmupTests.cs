using System.Collections;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Core
{
    public class ObjectPoolManagerWarmupTests
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
            _prefab.AddComponent<BoxCollider>(); // Adding a component so it's not completely empty
        }

        [TearDown]
        public void Teardown()
        {
            if (_poolManagerObj != null)
                Object.DestroyImmediate(_poolManagerObj);

            if (_registryObj != null)
                Object.DestroyImmediate(_registryObj);

            if (_prefab != null)
                Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void Warmup_ValidPrefab_SpawnsCorrectCount()
        {
            // Act
            _poolManager.Warmup(_prefab, 5);

            // Assert
            Assert.AreEqual(5, _poolManager.GetAvailableCount(_prefab));
        }

        [Test]
        public void Warmup_NullPrefab_DoesNothing()
        {
            // Act
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[ObjectPoolManager] Warmup: prefab is null!");
            _poolManager.Warmup((GameObject)null, 5);

            // Assert
            // LogAssert expects the error and no exception should be thrown
        }

        [Test]
        public void Warmup_ZeroOrNegativeCount_DoesNothing()
        {
            // Act
            _poolManager.Warmup(_prefab, 0);
            _poolManager.Warmup(_prefab, -5);

            // Assert
            Assert.AreEqual(0, _poolManager.GetAvailableCount(_prefab));
        }

        [Test]
        public void Warmup_WhenShuttingDown_DoesNothing()
        {
            // Arrange
            _poolManager.OnServiceShutdown();

            // Act
            _poolManager.Warmup(_prefab, 5);

            // Assert
            Assert.AreEqual(0, _poolManager.GetAvailableCount(_prefab));
        }
    }
}
