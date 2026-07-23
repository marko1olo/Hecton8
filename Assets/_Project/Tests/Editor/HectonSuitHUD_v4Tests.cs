using NUnit.Framework;
using UnityEngine;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonSuitHUD_v4Tests
    {
        private GameObject _hudObject;
        private HectonSuitHUD_v4 _hud;
        private GameObject _cameraObject;
        private Camera _camera;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject("TestHUD");
            _hud = _hudObject.AddComponent<HectonSuitHUD_v4>();
            _canvas = _hudObject.AddComponent<Canvas>();

            _cameraObject = new GameObject("TestCamera");
            _camera = _cameraObject.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hudObject);
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void SetHudCamera_UpdatesHudCameraProperty()
        {
            // Arrange
            Assert.IsNull(_hud.HudCamera, "Initial camera should be null.");

            // Act
            _hud.SetHudCamera(_camera);

            // Assert
            Assert.AreEqual(_camera, _hud.HudCamera, "HudCamera property should match the assigned camera.");
        }

        [Test]
        public void SetHudCamera_WithCanvas_UpdatesCanvasWorldCamera()
        {
            // Arrange
            var type = typeof(HectonSuitHUD_v4);
            var canvasField = type.GetField("_canvas", BindingFlags.NonPublic | BindingFlags.Instance);
            if (canvasField != null)
            {
                canvasField.SetValue(_hud, _canvas);
            }

            Assert.IsNull(_canvas.worldCamera, "Initial canvas world camera should be null.");

            // Act
            _hud.SetHudCamera(_camera);

            // Assert
            Assert.AreEqual(_camera, _canvas.worldCamera, "Canvas worldCamera should be updated to match the assigned camera.");
        }
    }
}
