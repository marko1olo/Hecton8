using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Gameplay
{
    public class SeafloorDrillToolTests
    {
        private GameObject _drillGameObject;
        private SeafloorDrillTool _drillTool;

        [SetUp]
        public void Setup()
        {
            _drillGameObject = new GameObject("SeafloorDrillToolTest");
            _drillTool = _drillGameObject.AddComponent<SeafloorDrillTool>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_drillGameObject != null)
            {
                Object.DestroyImmediate(_drillGameObject);
            }
        }

        [Test]
        public void Activate_SetsActiveStateToTrue()
        {
            // Arrange
            FieldInfo activeField = typeof(SeafloorDrillTool).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(activeField, "Could not find _active field on SeafloorDrillTool");

            // Ensure starting state is false
            activeField.SetValue(_drillTool, false);

            // Act
            _drillTool.Activate();

            // Assert
            bool isActive = (bool)activeField.GetValue(_drillTool);
            Assert.IsTrue(isActive, "Activate() did not set _active to true.");
        }

        [Test]
        public void Deactivate_SetsActiveStateToFalse()
        {
            // Arrange
            FieldInfo activeField = typeof(SeafloorDrillTool).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(activeField, "Could not find _active field on SeafloorDrillTool");

            // Ensure starting state is true
            activeField.SetValue(_drillTool, true);

            // Act
            _drillTool.Deactivate();

            // Assert
            bool isActive = (bool)activeField.GetValue(_drillTool);
            Assert.IsFalse(isActive, "Deactivate() did not set _active to false.");
        }
    }
}
