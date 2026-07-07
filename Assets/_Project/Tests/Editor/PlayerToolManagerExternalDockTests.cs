#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class PlayerToolManagerExternalDockTests
    {
        private class DummyPlayerTool : PlayerTool
        {
        }

        private GameObject _managerGo;
        private PlayerToolManager _manager;
        private GameObject _toolGo;
        private DummyPlayerTool _dummyTool;

        [SetUp]
        public void Setup()
        {
            _managerGo = new GameObject("PlayerToolManagerGo");
            _manager = _managerGo.AddComponent<PlayerToolManager>();

            _toolGo = new GameObject("DummyToolGo");
            _dummyTool = _toolGo.AddComponent<DummyPlayerTool>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_managerGo != null)
                UnityEngine.Object.DestroyImmediate(_managerGo);

            if (_toolGo != null)
                UnityEngine.Object.DestroyImmediate(_toolGo);
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Field {fieldName} not found");
            field.SetValue(target, value);
        }

        private object GetSwapStateEnum(string name)
        {
            var enumType = typeof(PlayerToolManager).GetNestedType("SwapState", BindingFlags.NonPublic);
            Assert.That(enumType, Is.Not.Null, "Enum SwapState not found");
            return Enum.Parse(enumType, name);
        }

        [Test]
        public void TryBeginExternalToolDock_NullTool_ReturnsFalse()
        {
            // Arrange
            SetPrivateField(_manager, "_currentTool", _dummyTool);
            SetPrivateField(_manager, "_swapState", GetSwapStateEnum("Idle"));
            SetPrivateField(_manager, "_externallyDockedTool", null);

            // Act
            bool result = _manager.TryBeginExternalToolDock(null);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryBeginExternalToolDock_CurrentToolIsNull_ReturnsFalse()
        {
            // Arrange
            SetPrivateField(_manager, "_currentTool", null);
            SetPrivateField(_manager, "_swapState", GetSwapStateEnum("Idle"));
            SetPrivateField(_manager, "_externallyDockedTool", null);

            // Act
            bool result = _manager.TryBeginExternalToolDock(_dummyTool);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryBeginExternalToolDock_ToolIsNotCurrentTool_ReturnsFalse()
        {
            // Arrange
            var otherToolGo = new GameObject("OtherTool");
            var otherTool = otherToolGo.AddComponent<DummyPlayerTool>();

            SetPrivateField(_manager, "_currentTool", otherTool);
            SetPrivateField(_manager, "_swapState", GetSwapStateEnum("Idle"));
            SetPrivateField(_manager, "_externallyDockedTool", null);

            // Act
            bool result = _manager.TryBeginExternalToolDock(_dummyTool);

            // Assert
            Assert.That(result, Is.False);

            UnityEngine.Object.DestroyImmediate(otherToolGo);
        }

        [Test]
        public void TryBeginExternalToolDock_SwapStateNotIdle_ReturnsFalse()
        {
            // Arrange
            SetPrivateField(_manager, "_currentTool", _dummyTool);
            SetPrivateField(_manager, "_swapState", GetSwapStateEnum("Lowering")); // Not Idle
            SetPrivateField(_manager, "_externallyDockedTool", null);

            // Act
            bool result = _manager.TryBeginExternalToolDock(_dummyTool);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void TryBeginExternalToolDock_AlreadyDocked_ReturnsFalse()
        {
            // Arrange
            var otherToolGo = new GameObject("OtherTool");
            var otherTool = otherToolGo.AddComponent<DummyPlayerTool>();

            SetPrivateField(_manager, "_currentTool", _dummyTool);
            SetPrivateField(_manager, "_swapState", GetSwapStateEnum("Idle"));
            SetPrivateField(_manager, "_externallyDockedTool", otherTool); // Already docked

            // Act
            bool result = _manager.TryBeginExternalToolDock(_dummyTool);

            // Assert
            Assert.That(result, Is.False);

            UnityEngine.Object.DestroyImmediate(otherToolGo);
        }

        [Test]
        public void TryBeginExternalToolDock_ValidConditions_ReturnsTrueAndDocks()
        {
            // Arrange
            SetPrivateField(_manager, "_currentTool", _dummyTool);
            SetPrivateField(_manager, "_swapState", GetSwapStateEnum("Idle"));
            SetPrivateField(_manager, "_externallyDockedTool", null);

            // Act
            bool result = _manager.TryBeginExternalToolDock(_dummyTool);

            // Assert
            Assert.That(result, Is.True);

            var dockedTool = typeof(PlayerToolManager).GetField("_externallyDockedTool", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_manager);
            Assert.That(dockedTool, Is.EqualTo(_dummyTool));
        }
    }
}
#endif
