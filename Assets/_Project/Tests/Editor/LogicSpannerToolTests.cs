#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class LogicSpannerToolTests
    {
        private GameObject _gameObject;
        private LogicSpannerTool _tool;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("LogicSpannerTool_Test");
            _tool = _gameObject.AddComponent<LogicSpannerTool>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void BuildLegacyOperationalDirectiveString_ReturnsIdleDirective_ByDefault()
        {
            var expected = "Acquire a source node to arm a bypass cable.";
            Assert.AreEqual(expected, _tool.BuildLegacyOperationalDirectiveString());
        }
    }
}
#endif
