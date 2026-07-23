using NUnit.Framework;
using UnityEngine;
using Hecton8.Power;
using System.Reflection;
using System.Collections.Generic;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridManagerOnServiceShutdownTests
    {
        private GameObject _managerGo;
        private PowerGridManager _manager;

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("PowerGridManager");
            _manager = _managerGo.AddComponent<PowerGridManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_managerGo != null)
            {
                Object.DestroyImmediate(_managerGo);
            }
        }

        [Test]
        public void OnServiceShutdown_DisposesAndClearsAllGrids()
        {
            var bindingFlags = BindingFlags.Static | BindingFlags.NonPublic;
            var allGridsField = typeof(PowerGridManager).GetField("_allGrids", bindingFlags);

            // Backup original list just in case
            var originalList = (List<PowerGrid>)allGridsField.GetValue(null);

            try
            {
                var gridList = new List<PowerGrid>();
                var fakeGrid = new PowerGrid(1);
                gridList.Add(fakeGrid);

                allGridsField.SetValue(null, gridList);

                _manager.OnServiceShutdown();

                var resultList = (List<PowerGrid>)allGridsField.GetValue(null);

                if (resultList != null)
                {
                    Assert.AreEqual(0, resultList.Count, "Expected _allGrids to be cleared.");
                }
            }
            finally
            {
                // Restore static state
                allGridsField.SetValue(null, originalList);
            }
        }

        [Test]
        public void OnServiceShutdown_ResetsStateAndDoesNotThrow()
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic;

            typeof(PowerGridManager).GetField("_wirelessToolAvailableEnergyWattSeconds", bindingFlags).SetValue(_manager, 100f);
            typeof(PowerGridManager).GetField("_nextPowerColdTickTime", bindingFlags).SetValue(_manager, 5f);
            typeof(PowerGridManager).GetField("_powerBrownoutSignalFrame", bindingFlags).SetValue(_manager, 10u);
            typeof(PowerGridManager).GetField("_slowTickFinalizationPending", bindingFlags).SetValue(_manager, true);
            typeof(PowerGridManager).GetField("_telemetryPublishPending", bindingFlags).SetValue(_manager, true);

            Assert.DoesNotThrow(() => _manager.OnServiceShutdown());

            var energyValue = (float)typeof(PowerGridManager).GetField("_wirelessToolAvailableEnergyWattSeconds", bindingFlags).GetValue(_manager);
            Assert.AreEqual(0f, energyValue);

            var coldTickValue = (float)typeof(PowerGridManager).GetField("_nextPowerColdTickTime", bindingFlags).GetValue(_manager);
            Assert.AreEqual(0f, coldTickValue);

            var signalFrame = (uint)typeof(PowerGridManager).GetField("_powerBrownoutSignalFrame", bindingFlags).GetValue(_manager);
            Assert.AreEqual(0u, signalFrame);

            var slowTickFinalizationPending = (bool)typeof(PowerGridManager).GetField("_slowTickFinalizationPending", bindingFlags).GetValue(_manager);
            Assert.IsFalse(slowTickFinalizationPending);

            var telemetryPublishPending = (bool)typeof(PowerGridManager).GetField("_telemetryPublishPending", bindingFlags).GetValue(_manager);
            Assert.IsFalse(telemetryPublishPending);
        }
    }
}
