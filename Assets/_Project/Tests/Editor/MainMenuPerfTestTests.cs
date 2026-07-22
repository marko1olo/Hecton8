using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public class MainMenuPerfTestTests
    {
        private GameObject _go;
        private MainMenuPerfTest _component;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _component = _go.AddComponent<MainMenuPerfTest>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
            GameObject root = GameObject.Find("Root");
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Start_InitializesTargetFrameRateAndVSync()
        {
            _component.Start();
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
        }

        [Test]
        public void Start_BuildsDummyHierarchy()
        {
            _component.Start();
            GameObject root = GameObject.Find("Root");
            Assert.IsNotNull(root);
            Assert.AreEqual(10, root.transform.childCount);
        }
    }
}
