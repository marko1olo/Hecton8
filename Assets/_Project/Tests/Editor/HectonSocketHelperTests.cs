using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Building;

namespace Hecton8.Tests.Editor
{
    public class HectonSocketHelperTests
    {
        private GameObject _gameObject;
        private HectonSocketHelper _socketHelper;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("TestSocket");
            _socketHelper = _gameObject.AddComponent<HectonSocketHelper>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void CreateSocketHelper_AddsComponent()
        {
            Assert.IsNotNull(_socketHelper);
        }

        [Test]
        public void SnapToSurface_IsDisabledForPhysXHygiene_LogsWarning()
        {
            // Use reflection to set values to known states so we can expect the exact log message
            var type = typeof(HectonSocketHelper);

            var distanceField = type.GetField("snapRayDistance", BindingFlags.NonPublic | BindingFlags.Instance);
            distanceField.SetValue(_socketHelper, 5.5f);

            var maskField = type.GetField("snapLayerMask", BindingFlags.NonPublic | BindingFlags.Instance);
            LayerMask testMask = 1 << 0;
            maskField.SetValue(_socketHelper, testMask);

            string expectedMessage = "[SocketHelper] Snap to Surface is disabled for X_005 PhysX hygiene. " +
                                     "Route this editor action through the construction surface owner before re-enabling. " +
                                     "Configured probe: 5.5m, mask 1.";

            LogAssert.Expect(LogType.Warning, expectedMessage);

            var methodInfo = type.GetMethod("SnapToSurface", BindingFlags.NonPublic | BindingFlags.Instance);
            methodInfo.Invoke(_socketHelper, null);
        }
    }
}
