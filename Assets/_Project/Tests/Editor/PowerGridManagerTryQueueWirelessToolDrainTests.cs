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
    }
}
