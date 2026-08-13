using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public class SceneInstantiationGateTests
    {
        private GameObject _go;
        private SceneInstantiationGate _gate;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestGate");
            _gate = _go.AddComponent<SceneInstantiationGate>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void SceneInstantiationGate_IsCreated()
        {
            Assert.IsNotNull(_gate);
        }
    }
}
