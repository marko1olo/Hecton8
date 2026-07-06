#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Inventory;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class PlayerToolManagerTickTests
    {
        private GameObject _go;
        private PlayerToolManager _manager;
        private PlayerInventory _inventory;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Tester");
            _manager = _go.AddComponent<PlayerToolManager>();
            _inventory = _go.AddComponent<PlayerInventory>();

            var field = typeof(PlayerToolManager).GetField("playerInventory", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(_manager, _inventory);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void Tick_WithDefaultState_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.Tick(0.1f));
        }
    }
}
#endif
