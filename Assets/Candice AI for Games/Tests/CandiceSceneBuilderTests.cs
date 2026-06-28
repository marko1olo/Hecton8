using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceSceneBuilderTests
    {
        private CandiceSceneBuilder _sceneBuilder;
        private GameObject _sceneBuilderObject;

        [SetUp]
        public void SetUp()
        {
            _sceneBuilderObject = new GameObject("SceneBuilder");
            _sceneBuilder = _sceneBuilderObject.AddComponent<CandiceSceneBuilder>();

            CandiceSceneBuilder.sceneBuilderObjects = new GameObject[9];
        }

        [TearDown]
        public void TearDown()
        {
            if (_sceneBuilderObject != null)
            {
                Object.DestroyImmediate(_sceneBuilderObject);
            }
        }

        [UnityTest]
        public IEnumerator Reset_DestroysOtherObjects_ExceptSceneBuilder()
        {
            GameObject objectToDestroy1 = new GameObject("TestObject1");
            GameObject objectToDestroy2 = new GameObject("TestObject2");

            _sceneBuilder.Reset();

            yield return null; // wait for end of frame

            Assert.That(objectToDestroy1 == null, Is.True, "Object 1 should have been destroyed.");
            Assert.That(objectToDestroy2 == null, Is.True, "Object 2 should have been destroyed.");
            Assert.That(_sceneBuilderObject != null, Is.True, "SceneBuilder should NOT have been destroyed.");
        }
    }
}
