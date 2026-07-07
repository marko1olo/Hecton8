using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hecton8.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Core
{
    public class ObjectPoolManagerWarmupPresetsAsyncExceptionTests
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

            // Ensure static list is always restored just in case
            var type = typeof(ObjectPoolManager);
            var cacheField = type.GetField("s_componentCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (cacheField != null)
            {
                if (cacheField.GetValue(null) == null)
                    cacheField.SetValue(null, new System.Collections.Generic.List<Component>(8));
            }
        }

        [Test]
        public async Task WarmupPresetsAsync_Crash_LogsErrorAndCompletesWarmup()
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

            // Null out static component cache to force a NullReferenceException when InstantiatePooled calls GetComponents(s_componentCache)
            var s_componentCacheField = type.GetField("s_componentCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            s_componentCacheField.SetValue(null, null);

            var cts = new CancellationTokenSource();

            // Unity Test Framework needs to expect the exact error regex
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[ObjectPoolManager\] WarmupPresetsAsync crashed:"));

            bool result = await _poolManager.WarmupPresetsAsync(10.0, cts.Token);

            Assert.IsFalse(result, "Expected WarmupPresetsAsync to return false upon exception.");

            var completedField = type.GetField("_warmupPresetsCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool isCompleted = (bool)completedField.GetValue(_poolManager);

            Assert.IsTrue(isCompleted, "Expected _warmupPresetsCompleted to be true after a crash.");

            // Restore the static cache to not break other tests
            s_componentCacheField.SetValue(null, new System.Collections.Generic.List<Component>(8));
        }
    }
}
