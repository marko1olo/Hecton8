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

        public class DummyPlayerTool : PlayerTool
        {
            public float ToolTickDelta { get; private set; }

            public override void ToolTick(float delta)
            {
                ToolTickDelta = delta;
            }

            public override void UsePrimary(float delta) {}
            public override void UseSecondary(float delta) {}

            protected override void OnHolstered() {}
            protected override void OnDrawn() {}
        }

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

        [Test]
        public void Tick_WithCurrentTool_AdvancesRuntimeClockSeconds()
        {
            var toolGo = new GameObject("DummyTool");
            var tool = toolGo.AddComponent<DummyPlayerTool>();

            var toolField = typeof(PlayerToolManager).GetField("_currentTool", BindingFlags.NonPublic | BindingFlags.Instance);
            toolField.SetValue(_manager, tool);

            // Set swapState to Raising (2) to skip ToolTick and just test AdvanceRuntimeActiveIntent which updates _toolRuntimeClockSeconds
            var swapStateField = typeof(PlayerToolManager).GetField("_swapState", BindingFlags.NonPublic | BindingFlags.Instance);
            swapStateField.SetValue(_manager, 2);

            float delta = 0.5f;

            _manager.Tick(delta);

            var clockField = typeof(PlayerTool).GetField("_toolRuntimeClockSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            float clockVal = (float)clockField.GetValue(tool);

            Assert.That(clockVal, Is.EqualTo(delta));

            UnityEngine.Object.DestroyImmediate(toolGo);
        }

        [Test]
        public void Tick_WithCurrentToolAndIdleSwapState_CallsToolTick()
        {
            var toolGo = new GameObject("DummyTool");
            var tool = toolGo.AddComponent<DummyPlayerTool>();

            var toolField = typeof(PlayerToolManager).GetField("_currentTool", BindingFlags.NonPublic | BindingFlags.Instance);
            toolField.SetValue(_manager, tool);

            var swapStateField = typeof(PlayerToolManager).GetField("_swapState", BindingFlags.NonPublic | BindingFlags.Instance);
            swapStateField.SetValue(_manager, 0);

            float delta = 0.5f;

            _manager.Tick(delta);

            Assert.That(tool.ToolTickDelta, Is.EqualTo(delta));

            UnityEngine.Object.DestroyImmediate(toolGo);
        }

        [Test]
        public void Tick_ExternallyDockedTool_ReturnsEarly()
        {
            var toolGo = new GameObject("DummyTool");
            var tool = toolGo.AddComponent<DummyPlayerTool>();

            var toolField = typeof(PlayerToolManager).GetField("_currentTool", BindingFlags.NonPublic | BindingFlags.Instance);
            toolField.SetValue(_manager, tool);

            var externalDockField = typeof(PlayerToolManager).GetField("_externallyDockedTool", BindingFlags.NonPublic | BindingFlags.Instance);
            externalDockField.SetValue(_manager, tool);

            var swapStateField = typeof(PlayerToolManager).GetField("_swapState", BindingFlags.NonPublic | BindingFlags.Instance);
            swapStateField.SetValue(_manager, 0);

            float delta = 0.5f;

            _manager.Tick(delta);

            Assert.That(tool.ToolTickDelta, Is.EqualTo(0f));

            UnityEngine.Object.DestroyImmediate(toolGo);
        }

        [Test]
        public void TryRegisterToTickManager_NotInPlayMode_ReturnsEarlyWithoutException()
        {
            var method = typeof(PlayerToolManager).GetMethod("TryRegisterToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.DoesNotThrow(() => method.Invoke(_manager, null));
        }

        [Test]
        public void TryRegisterToTickManager_AlreadyRegistered_ReturnsEarlyWithoutException()
        {
            var method = typeof(PlayerToolManager).GetMethod("TryRegisterToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            var tickField = typeof(PlayerToolManager).GetField("_registeredToTick", BindingFlags.NonPublic | BindingFlags.Instance);
            var lateTickField = typeof(PlayerToolManager).GetField("_registeredToLateFrame", BindingFlags.NonPublic | BindingFlags.Instance);

            tickField.SetValue(_manager, true);
            lateTickField.SetValue(_manager, true);

            Assert.DoesNotThrow(() => method.Invoke(_manager, null));
        }

    }
}
#endif
