#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Power;
using Hecton8.Core;
using System.Reflection;
using System.Collections.Generic;

namespace Hecton8.Tests.Power
{
    [TestFixture]
    public class PowerGridManagerInitializeServiceTests
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
        public void InitializeService_EnsuresStorage_AllocatesAllGrids()
        {
            var allGridsField = typeof(PowerGridManager).GetField("_allGrids", BindingFlags.Static | BindingFlags.NonPublic);

            // Setup null state
            var originalList = (List<PowerGrid>)allGridsField.GetValue(null);
            allGridsField.SetValue(null, null);

            try
            {
                _manager.InitializeService();

                var resultList = (List<PowerGrid>)allGridsField.GetValue(null);
                Assert.IsNotNull(resultList, "_allGrids should be initialized by EnsureStorage.");
            }
            finally
            {
                allGridsField.SetValue(null, originalList);
            }
        }

        [Test]
        public void InitializeService_EnsuresRuntimeBackendsCold_AllocatesBackends()
        {
            var wfcField = typeof(PowerGridManager).GetField("_wfcOutpostPowerBoot", BindingFlags.Instance | BindingFlags.NonPublic);
            var shinobuField = typeof(PowerGridManager).GetField("_shinobuLogisticsRouter", BindingFlags.Instance | BindingFlags.NonPublic);
            var submarineField = typeof(PowerGridManager).GetField("_submarineThermalGridRuntime", BindingFlags.Instance | BindingFlags.NonPublic);

            // Set all to null
            wfcField.SetValue(_manager, null);
            shinobuField.SetValue(_manager, null);
            submarineField.SetValue(_manager, null);

            _manager.InitializeService();

            Assert.IsNotNull(wfcField.GetValue(_manager), "_wfcOutpostPowerBoot should be initialized.");
            Assert.IsNotNull(shinobuField.GetValue(_manager), "_shinobuLogisticsRouter should be initialized.");
            Assert.IsNotNull(submarineField.GetValue(_manager), "_submarineThermalGridRuntime should be initialized.");
        }

        [Test]
        public void InitializeService_InEditMode_DoesNotRegisterServices()
        {
            var dispatcherField = typeof(PowerGridManager).GetField("_dispatcherRegistered", BindingFlags.Instance | BindingFlags.NonPublic);
            var lateFrameField = typeof(PowerGridManager).GetField("_lateFrameRegistered", BindingFlags.Instance | BindingFlags.NonPublic);
            var serviceField = typeof(PowerGridManager).GetField("_serviceRegistered", BindingFlags.Instance | BindingFlags.NonPublic);

            dispatcherField.SetValue(_manager, false);
            lateFrameField.SetValue(_manager, false);
            serviceField.SetValue(_manager, false);

            _manager.InitializeService();

            Assert.IsFalse((bool)dispatcherField.GetValue(_manager), "_dispatcherRegistered should remain false in EditMode");
            Assert.IsFalse((bool)lateFrameField.GetValue(_manager), "_lateFrameRegistered should remain false in EditMode");
            Assert.IsFalse((bool)serviceField.GetValue(_manager), "_serviceRegistered should remain false in EditMode");
        }
    }
}
#endif
