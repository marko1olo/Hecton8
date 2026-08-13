#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Optimization.Tests
{
    [TestFixture]
    public class VRAMMonitorTests
    {
        private GameObject _go;
        private VRAMMonitor _monitor;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("VRAMMonitorTest");
            _monitor = _go.AddComponent<VRAMMonitor>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void SlowTick_ExecutesWithoutException()
        {
            var thresholdsField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            if (thresholdsField != null)
            {
                var thresholds = new VRAMBudgetThresholds();
                thresholdsField.SetValue(_monitor, thresholds);
            }

            Assert.DoesNotThrow(() => _monitor.SlowTick());
        }
    }
}
#endif
