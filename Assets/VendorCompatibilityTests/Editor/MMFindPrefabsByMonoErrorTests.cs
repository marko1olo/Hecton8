#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using MoreMountains.Tools;

namespace Hecton8.Tests.Editor
{
    public class MMFindPrefabsByMonoErrorTests
    {
        private Texture2D _dummyTexture;
        private string _fakePrefabPath = "Assets/FakePrefabNotGameObject.prefab";

        [SetUp]
        public void Setup()
        {
            _dummyTexture = new Texture2D(1, 1);
        }

        [TearDown]
        public void Teardown()
        {
            MMFindPrefabsByMono._getAllPrefabsInProjectMock = null;
            MMFindPrefabsByMono._loadMainAssetAtPathMock = null;

            if (_dummyTexture != null)
            {
                Object.DestroyImmediate(_dummyTexture);
            }
        }

        }


        [Test]
        public void PerformSearchMissing_WhenAssetIsNotGameObject_LogsErrorAndSkips()
        {
            MMFindPrefabsByMono._getAllPrefabsInProjectMock = () => new string[] { _fakePrefabPath };
            MMFindPrefabsByMono._loadMainAssetAtPathMock = (path) => _dummyTexture;

            var window = ScriptableObject.CreateInstance<MMFindPrefabsByMono>();

            // Trigger the log error we expect
            LogAssert.Expect(LogType.Log, "An error occured with prefab " + _fakePrefabPath);

            // Execute the extracted logic method which triggers the catch block without GUI code execution.
            window.PerformSearchMissing();

            Object.DestroyImmediate(window);
        }
    }
}
#endif
