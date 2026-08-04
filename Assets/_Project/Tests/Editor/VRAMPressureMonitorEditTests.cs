#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;

namespace Hecton8.Tests.Editor
{
    public class VRAMPressureMonitorEditTests
    {
        private VRAMPressureMonitor _monitor;
        private GameObject _gameObject;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("VRAMPressureMonitor_Test");
            _monitor = _gameObject.AddComponent<VRAMPressureMonitor>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void VRAMPressureMonitor_CanBeInstantiated()
        {
            Assert.IsNotNull(_monitor, "VRAMPressureMonitor should be instantiable.");
        }
    }
}
#endif
