using System.Collections.Generic;
using System.Reflection;
using Hecton8.Power;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridManagerTryQueueWirelessToolDrainTests
    {
        private GameObject _go;
        private PowerGridManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PowerGridManagerObj");
            _manager = _go.AddComponent<PowerGridManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        private void SetPrivateField(string fieldName, object value, bool isStatic = false)
        {
            FieldInfo field = typeof(PowerGridManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(isStatic ? null : _manager, value);
            }
            else
            {
                Assert.Fail($"Field {fieldName} not found");
            }
        }

        [Test]
        public void TryQueueWirelessToolDrain_NegativeEnergy_ReturnsFalseAndZero()
        {
            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 100f);
            SetPrivateField("_allGrids", new List<PowerGrid>(), true);

            bool result = _manager.TryQueueWirelessToolDrain(-10f, out float granted);

            Assert.IsFalse(result);
            Assert.AreEqual(0f, granted);
        }

        [Test]
        public void TryQueueWirelessToolDrain_ZeroEnergy_ReturnsFalseAndZero()
        {
            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 100f);
            SetPrivateField("_allGrids", new List<PowerGrid>(), true);

            bool result = _manager.TryQueueWirelessToolDrain(0f, out float granted);

            Assert.IsFalse(result);
            Assert.AreEqual(0f, granted);
        }

        [Test]
        public void TryQueueWirelessToolDrain_LowAvailableEnergy_ReturnsFalseAndZero()
        {
            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 0.00005f); // Less than 0.0001f
            SetPrivateField("_allGrids", new List<PowerGrid>(), true);

            bool result = _manager.TryQueueWirelessToolDrain(10f, out float granted);

            Assert.IsFalse(result);
            Assert.AreEqual(0f, granted);
        }

        [Test]
        public void TryQueueWirelessToolDrain_SlowTickFinalizationPending_ReturnsFalseAndZero()
        {
            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 100f);
            SetPrivateField("_slowTickFinalizationPending", true);
            SetPrivateField("_allGrids", new List<PowerGrid>(), true);

            bool result = _manager.TryQueueWirelessToolDrain(10f, out float granted);

            Assert.IsFalse(result);
            Assert.AreEqual(0f, granted);
        }

        [Test]
        public void TryQueueWirelessToolDrain_NullGrids_ReturnsFalseAndZero()
        {
            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 100f);
            SetPrivateField("_slowTickFinalizationPending", false);
            SetPrivateField("_allGrids", null, true);

            bool result = _manager.TryQueueWirelessToolDrain(10f, out float granted);

            Assert.IsFalse(result);
            Assert.AreEqual(0f, granted);
        }

        private void SetPowerGridPrivateField(PowerGrid grid, string fieldName, object value)
        {
            FieldInfo field = typeof(PowerGrid).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(grid, value);
            }
            else
            {
                Assert.Fail($"PowerGrid field {fieldName} not found");
            }
        }

        private List<T> GetPowerGridPrivateField<T>(PowerGrid grid, string fieldName)
        {
            FieldInfo field = typeof(PowerGrid).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return (List<T>)field.GetValue(grid);
            }
            Assert.Fail($"PowerGrid field {fieldName} not found");
            return null;
        }

        private void ConfigurePowerGridBattery(PowerGrid grid, float wirelessAvailableEnergy)
        {
            SetPowerGridPrivateField(grid, "_hasBatteryBanks", true);
            SetPowerGridPrivateField(grid, "_cachedWirelessToolAvailableEnergyWattSeconds", wirelessAvailableEnergy);
            SetPowerGridPrivateField(grid, "_totalBatteryStoredEnergyWattSeconds", wirelessAvailableEnergy);
            SetPowerGridPrivateField(grid, "_totalBatteryCapacityWattSeconds", wirelessAvailableEnergy);
            SetPowerGridPrivateField(grid, "_batteryEmergencyReserveActive", false);

            var ids = GetPowerGridPrivateField<int>(grid, "_batteryReserveComponentIds");
            ids.Clear();
            ids.Add(42);

            var available = GetPowerGridPrivateField<float>(grid, "_batteryReserveComponentWirelessAvailableEnergyWattSeconds");
            available.Clear();
            available.Add(wirelessAvailableEnergy);

            var reserved = GetPowerGridPrivateField<float>(grid, "_batteryReserveComponentReservedWirelessEnergyWattSeconds");
            reserved.Clear();
            reserved.Add(0f);

            var states = GetPowerGridPrivateField<byte>(grid, "_batteryReserveComponentStates");
            states.Clear();
            states.Add(0);
        }

        [Test]
        public void TryQueueWirelessToolDrain_SuccessfulDrain_ReturnsTrueAndGrantedAmount()
        {
            var grid = new PowerGrid(16, null);
            ConfigurePowerGridBattery(grid, 500f);

            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 500f);
            SetPrivateField("_slowTickFinalizationPending", false);
            SetPrivateField("_allGrids", new List<PowerGrid> { grid }, true);

            bool result = _manager.TryQueueWirelessToolDrain(200f, out float granted);

            Assert.IsTrue(result);
            Assert.AreEqual(200f, granted);

            // Verify grid state
            FieldInfo cachedField = typeof(PowerGrid).GetField("_cachedWirelessToolAvailableEnergyWattSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            float remainingOnGrid = (float)cachedField.GetValue(grid);
            Assert.AreEqual(300f, remainingOnGrid);

            var reserved = GetPowerGridPrivateField<float>(grid, "_batteryReserveComponentReservedWirelessEnergyWattSeconds");
            Assert.AreEqual(200f, reserved[0]);

            // Verify manager state
            FieldInfo managerAvailableField = typeof(PowerGridManager).GetField("_wirelessToolAvailableEnergyWattSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            float managerAvailable = (float)managerAvailableField.GetValue(_manager);
            Assert.AreEqual(300f, managerAvailable);
        }

        [Test]
        public void TryQueueWirelessToolDrain_CappedAtMaxWirelessToolDrain_ReturnsTrueAndCappedAmount()
        {
            var grid = new PowerGrid(16, null);
            ConfigurePowerGridBattery(grid, 10000f);

            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 10000f);
            SetPrivateField("_slowTickFinalizationPending", false);
            SetPrivateField("_allGrids", new List<PowerGrid> { grid }, true);

            // MaxWirelessToolDrainWattSeconds in PowerGridManager is 4096f
            bool result = _manager.TryQueueWirelessToolDrain(5000f, out float granted);

            Assert.IsTrue(result);
            Assert.AreEqual(4096f, granted);

            FieldInfo cachedField = typeof(PowerGrid).GetField("_cachedWirelessToolAvailableEnergyWattSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            float remainingOnGrid = (float)cachedField.GetValue(grid);
            Assert.AreEqual(10000f - 4096f, remainingOnGrid);
        }

        [Test]
        public void TryQueueWirelessToolDrain_MultipleGrids_DrainsSequentially()
        {
            var grid1 = new PowerGrid(16, null);
            ConfigurePowerGridBattery(grid1, 300f);

            var grid2 = new PowerGrid(16, null);
            ConfigurePowerGridBattery(grid2, 500f);

            SetPrivateField("_wirelessToolAvailableEnergyWattSeconds", 800f);
            SetPrivateField("_slowTickFinalizationPending", false);
            SetPrivateField("_allGrids", new List<PowerGrid> { grid1, grid2 }, true);

            bool result = _manager.TryQueueWirelessToolDrain(500f, out float granted);

            Assert.IsTrue(result);
            Assert.AreEqual(500f, granted);

            FieldInfo cachedField = typeof(PowerGrid).GetField("_cachedWirelessToolAvailableEnergyWattSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            float remainingGrid1 = (float)cachedField.GetValue(grid1);
            float remainingGrid2 = (float)cachedField.GetValue(grid2);

            Assert.AreEqual(0f, remainingGrid1);
            Assert.AreEqual(300f, remainingGrid2);
        }
    }
}
