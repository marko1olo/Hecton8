using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class ObjectPoolManagerWarmupTests
    {
        private GameObject _go;
        private ObjectPoolManager _manager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestPoolManager");
            _manager = _go.AddComponent<ObjectPoolManager>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_manager != null)
                _manager.OnServiceShutdown();

            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void Warmup_ValidPrefab_SpawnsCorrectCount()
        {
            GameObject prefab = new GameObject("TestPrefab");

            _manager.InitializeService();
            _manager.Warmup(prefab, 5);

            Assert.AreEqual(5, _manager.GetAvailableCount(prefab));

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Warmup_NullPrefab_DoesNothing()
        {
            _manager.InitializeService();

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[ObjectPoolManager] Warmup: prefab is null!");
            _manager.Warmup((GameObject)null, 5);
        }

        [Test]
        public void Warmup_ZeroOrNegativeCount_DoesNothing()
        {
            GameObject prefab = new GameObject("TestPrefab");
            _manager.InitializeService();

            _manager.Warmup(prefab, 0);
            Assert.AreEqual(0, _manager.GetAvailableCount(prefab));

            _manager.Warmup(prefab, -5);
            Assert.AreEqual(0, _manager.GetAvailableCount(prefab));

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void Warmup_WhenShuttingDown_DoesNothing()
        {
            GameObject prefab = new GameObject("TestPrefab");
            _manager.InitializeService();
            _manager.OnServiceShutdown();

            _manager.Warmup(prefab, 5);

            Assert.AreEqual(0, _manager.GetAvailableCount(prefab));

            Object.DestroyImmediate(prefab);
        }
    }
}
