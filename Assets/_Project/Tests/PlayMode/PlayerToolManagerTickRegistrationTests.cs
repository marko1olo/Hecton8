using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Gameplay;
using Hecton8.Core;

namespace Hecton8.Tests.PlayMode
{
    [TestFixture]
    public class PlayerToolManagerTickRegistrationTests
    {
        private GameObject _go;
        private PlayerToolManager _manager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Tester");
            _go.SetActive(false);
            _manager = _go.AddComponent<PlayerToolManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [UnityTest]
        public IEnumerator TryRegisterToTickManager_DispatcherNull_ReturnsEarly()
        {
            _go.SetActive(true);

            var method = typeof(PlayerToolManager).GetMethod("TryRegisterToTickManager", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "Method TryRegisterToTickManager not found");

            var dispatcher = GlobalRegistry.Dispatcher;
            Assert.IsNull(dispatcher, "Dispatcher should be null for this test to be valid");

            Assert.DoesNotThrow(() => method.Invoke(_manager, null));

            var tickField = typeof(PlayerToolManager).GetField("_registeredToTick", BindingFlags.NonPublic | BindingFlags.Instance);
            var lateTickField = typeof(PlayerToolManager).GetField("_registeredToLateFrame", BindingFlags.NonPublic | BindingFlags.Instance);

            bool isRegisteredToTick = (bool)tickField.GetValue(_manager);
            bool isRegisteredToLateFrame = (bool)lateTickField.GetValue(_manager);

            Assert.IsFalse(isRegisteredToTick, "_registeredToTick should be false when Dispatcher is null");
            Assert.IsFalse(isRegisteredToLateFrame, "_registeredToLateFrame should be false when Dispatcher is null");

            yield return null;
        }
    }
}
