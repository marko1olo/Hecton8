#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Core
{
    public class GameTickManagerTests
    {
        private GameTickManager _gameTickManager;

        private class StubTickable : ITickable
        {
            public void Tick(float deltaTime) {}
            public void Tick() {}
        }

        [SetUp]
        public void Setup()
        {
            var go = new GameObject("GameTickManager");
            _gameTickManager = go.AddComponent<GameTickManager>();

            // Force initialization to populate internal lists
            var method = typeof(GameTickManager).GetMethod("EnsureInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(_gameTickManager, null);
            }
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameTickManager != null)
            {
                Object.DestroyImmediate(_gameTickManager.gameObject);
            }
        }

        [Test]
        public void Register_ITickable_AddsToTickablesList()
        {
            // Arrange
            var stub = new StubTickable();
            var fieldInfo = typeof(GameTickManager).GetField("_tickables", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fieldInfo, "_tickables field not found");

            // Act
            _gameTickManager.Register(stub);

            // Assert
            var tickListObj = fieldInfo.GetValue(_gameTickManager);
            Assert.IsNotNull(tickListObj, "TickList is null");

            var countProp = tickListObj.GetType().GetProperty("Count");
            Assert.IsNotNull(countProp, "Count property not found on TickList");

            int count = (int)countProp.GetValue(tickListObj);
            Assert.AreEqual(1, count, "Item was not added to the tickables list");
        }
    }
}
#endif
