using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerInitializeServiceTests
    {
        private ObjectPoolManager _poolManager;
        private GameObject _poolRoot;
        private FieldInfo _poolsField;
        private FieldInfo _poolMarkerCacheField;
        private FieldInfo _serviceShuttingDownField;
        private FieldInfo _serviceRegisteredField;

        [SetUp]
        public void SetUp()
        {
            _poolRoot = new GameObject("ObjectPoolManager_InitTests");
            _poolManager = _poolRoot.AddComponent<ObjectPoolManager>();

            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            _poolsField = typeof(ObjectPoolManager).GetField("_pools", bindingFlags);
            _poolMarkerCacheField = typeof(ObjectPoolManager).GetField("_poolMarkerCache", bindingFlags);
            _serviceShuttingDownField = typeof(ObjectPoolManager).GetField("_serviceShuttingDown", bindingFlags);
            _serviceRegisteredField = typeof(ObjectPoolManager).GetField("_serviceRegistered", bindingFlags);
        }

        [TearDown]
        public void TearDown()
        {

            if (_poolManager != null)
            {
                // Unregister the service manually or clear the mirror
                var mirrorField = typeof(GlobalRegistry).GetProperty("ObjectPoolRuntimeMirror", BindingFlags.Public | BindingFlags.Static);
                if (mirrorField != null && mirrorField.GetValue(null) == (object)_poolManager)
                {
                    mirrorField.SetValue(null, null);
                }
            }

            if (_poolRoot != null)
            {
                Object.DestroyImmediate(_poolRoot);
            }
        }

        [Test]
        public void InitializeService_SetsUpDictionariesAndState()
        {
            // Arrange
            _serviceShuttingDownField.SetValue(_poolManager, true);

            // Act
            _poolManager.InitializeService();

            // Assert
            var poolsDict = _poolsField.GetValue(_poolManager) as IDictionary;
            Assert.IsNotNull(poolsDict, "Expected _pools dictionary to be allocated.");

            var markerCacheDict = _poolMarkerCacheField.GetValue(_poolManager) as IDictionary;
            Assert.IsNotNull(markerCacheDict, "Expected _poolMarkerCache dictionary to be allocated.");

            bool isShuttingDown = (bool)_serviceShuttingDownField.GetValue(_poolManager);
            Assert.IsFalse(isShuttingDown, "Expected _serviceShuttingDown to be reset to false.");

            bool isRegistered = (bool)_serviceRegisteredField.GetValue(_poolManager);
            Assert.IsTrue(isRegistered, "Expected _serviceRegistered to be true.");

            // Verify GlobalRegistry registration
            var mirrorProp = typeof(GlobalRegistry).GetProperty("ObjectPoolRuntimeMirror", BindingFlags.Public | BindingFlags.Static);
            if (mirrorProp != null)
            {
                var mirrorValue = mirrorProp.GetValue(null);
                Assert.AreEqual(_poolManager, mirrorValue, "Expected GlobalRegistry.ObjectPoolRuntimeMirror to be set.");
            }
        }
    }
}
