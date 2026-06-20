#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using System.Reflection;
using System.Collections.Generic;

namespace Hecton8.Core.Editor.Tests
{
    [TestFixture]
    public class GameTickManagerTests
    {
        private class StubTickable : ITickable
        {
            public int TickCount { get; private set; }

            public void Tick(float deltaTime)
            {
                TickCount++;
            }
        }

        private GameTickManager _manager;
        private GameObject _managerGameObject;

        [SetUp]
        public void Setup()
        {
            _managerGameObject = new GameObject("GameTickManager");
            _manager = _managerGameObject.AddComponent<GameTickManager>();

            var ensureInit = typeof(GameTickManager).GetMethod("EnsureInitialized", BindingFlags.Instance | BindingFlags.NonPublic);
            if (ensureInit != null)
            {
                ensureInit.Invoke(_manager, null);
            }
        }

        [TearDown]
        public void Teardown()
        {
            if (_managerGameObject != null)
            {
                Object.DestroyImmediate(_managerGameObject);
            }
        }

        [Test]
        public void Unregister_ITickable_RemovesFromCollection()
        {
            // Arrange
            var tickable = new StubTickable();
            _manager.Register(tickable);

            // Act
            _manager.Unregister(tickable);

            // Assert
            var tickablesField = typeof(GameTickManager).GetField("_tickables", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(tickablesField, "Could not find _tickables field");

            var tickablesList = tickablesField.GetValue(_manager);
            var countProp = tickablesList.GetType().GetProperty("Count");
            Assert.IsNotNull(countProp, "Could not find Count property");

            int count = (int)countProp.GetValue(tickablesList);
            Assert.AreEqual(0, count, "Tickable was not removed from the collection.");
        }

        [Test]
        public void Unregister_ITickable_DoesNotThrowOnNull()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _manager.Unregister((ITickable)null));
        }
    }
}
#endif
