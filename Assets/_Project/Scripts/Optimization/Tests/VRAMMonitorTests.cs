using NUnit.Framework;
using UnityEngine;
using Hecton8.Optimization;
using System.Reflection;

namespace Hecton8.Optimization.Tests
{
    public class VRAMMonitorTests
    {
        private GameObject _go;
        private VRAMMonitor _monitor;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("VRAMMonitorTest");
            _monitor = _go.AddComponent<VRAMMonitor>();
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
        public void SlowTick_ExecutesMeasureVRAMAndCheckThresholds()
        {
            _monitor.SlowTick();

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Stable));
            Assert.That(_monitor.PressureStateCode, Is.EqualTo(0));

            _monitor.GetVRAMBreakdown(out long tex, out long rt, out long total);
            Assert.That(tex, Is.EqualTo(0));
            Assert.That(rt, Is.EqualTo(0));
            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void CheckThresholds_LogsWarning_WhenOverBudget()
        {
            // Set the memory values to simulate over-budget using reflection
            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 1000L, null);

            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 500L,
                RenderTextureMemoryBudgetBytes = 500L,
                TotalVRAMBudgetBytes = 500L
            };
            budgetField.SetValue(_monitor, budget);

            // Call CheckThresholds using reflection
            var checkMethod = typeof(VRAMMonitor).GetMethod("CheckThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.DoesNotThrow(() => checkMethod.Invoke(_monitor, null));
        }

        [Test]
        public void UpdatePressureState_Critical_WhenOverBudget()
        {
            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 500L,
                RenderTextureMemoryBudgetBytes = 500L,
                TotalVRAMBudgetBytes = 500L
            };
            budgetField.SetValue(_monitor, budget);

            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 1000L, null);

            var updateMethod = typeof(VRAMMonitor).GetMethod("UpdatePressureState", BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod.Invoke(_monitor, null);

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Critical));
        }

        [Test]
        public void UpdatePressureState_Warning_WhenNearBudget()
        {
            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 1000L,
                RenderTextureMemoryBudgetBytes = 1000L,
                TotalVRAMBudgetBytes = 1000L
            };
            budgetField.SetValue(_monitor, budget);

            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 900L, null);

            var texUtilProp = typeof(VRAMMonitor).GetProperty("TextureBudgetUtilization", BindingFlags.Public | BindingFlags.Instance);
            texUtilProp.DeclaringType.GetProperty("TextureBudgetUtilization").SetValue(_monitor, 0.9f, null);

            var updateMethod = typeof(VRAMMonitor).GetMethod("UpdatePressureState", BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod.Invoke(_monitor, null);

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Warning));
        }

        [Test]
        public void UpdatePressureState_Stable_WhenBelowBudget()
        {
            var budgetField = typeof(VRAMMonitor).GetField("_budgetThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
            var budget = new VRAMBudgetThresholds {
                TextureMemoryBudgetBytes = 1000L,
                RenderTextureMemoryBudgetBytes = 1000L,
                TotalVRAMBudgetBytes = 1000L
            };
            budgetField.SetValue(_monitor, budget);

            var texProp = typeof(VRAMMonitor).GetProperty("TextureMemoryBytes", BindingFlags.Public | BindingFlags.Instance);
            texProp.DeclaringType.GetProperty("TextureMemoryBytes").SetValue(_monitor, 100L, null);

            var texUtilProp = typeof(VRAMMonitor).GetProperty("TextureBudgetUtilization", BindingFlags.Public | BindingFlags.Instance);
            texUtilProp.DeclaringType.GetProperty("TextureBudgetUtilization").SetValue(_monitor, 0.1f, null);

            var updateMethod = typeof(VRAMMonitor).GetMethod("UpdatePressureState", BindingFlags.NonPublic | BindingFlags.Instance);
            updateMethod.Invoke(_monitor, null);

            Assert.That(_monitor.PressureState, Is.EqualTo(VRAMMonitor.VRAMPressureState.Stable));
        }
    }
}
