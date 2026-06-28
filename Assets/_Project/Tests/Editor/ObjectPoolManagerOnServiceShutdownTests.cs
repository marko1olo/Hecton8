using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public class ObjectPoolManagerOnServiceShutdownTests
    {
        private GameObject _go;
        private ObjectPoolManager _manager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject();
            _manager = _go.AddComponent<ObjectPoolManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void OnServiceShutdown_RepeatedCalls_DoNotThrowExceptions()
        {
            _manager.OnServiceShutdown();

            Assert.DoesNotThrow(() => {
                _manager.OnServiceShutdown();
                _manager.OnServiceShutdown();
            });
        }

        [Test]
        public void OnServiceShutdown_UpdatesFlagsAppropriately()
        {
            // Setup
            var shuttingDownField = typeof(ObjectPoolManager).GetField("_serviceShuttingDown", BindingFlags.NonPublic | BindingFlags.Instance);
            shuttingDownField.SetValue(_manager, false);

            var presetsStartedField = typeof(ObjectPoolManager).GetField("_warmupPresetsStarted", BindingFlags.NonPublic | BindingFlags.Instance);
            presetsStartedField.SetValue(_manager, true);

            var activeRuntimeInstanceProp = typeof(ObjectPoolManager).GetProperty("ActiveRuntimeInstance", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
            activeRuntimeInstanceProp.SetValue(null, _manager);

            // Act
            _manager.OnServiceShutdown();

            // Verify
            Assert.IsTrue((bool)shuttingDownField.GetValue(_manager));
            Assert.IsFalse((bool)presetsStartedField.GetValue(_manager));
            Assert.IsNull(activeRuntimeInstanceProp.GetValue(null));
        }
    }
}
