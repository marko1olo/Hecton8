#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using UnityEngine.TestTools;
using MoreMountains.Tools;
using System.IO;
using System;
using System.Reflection;
using System.Collections.Generic;

namespace MoreMountains.Tools.Tests
{
    public class MMFindPrefabsByMonoTests
    {
        [TearDown]
        public void TearDown()
        {
            // Reset mock delegates to avoid leaking state to other tests
            MMFindPrefabsByMono.MockGetAllPrefabsInProject = null;
            MMFindPrefabsByMono.MockLoadMainAssetAtPath = null;
            MMFindPrefabsByMono.MockCastToGameObject = null;
        }

        [Test]
        public void DrawSearchMissing_CatchesExceptionAndLogsError()
        {
            // Setup
            string dummyPrefabPath = "Assets/DummyPrefabThatThrowsException.prefab";

            // Mock GetAllPrefabsInProject to return our single dummy prefab path
            MMFindPrefabsByMono.MockGetAllPrefabsInProject = () => new string[] { dummyPrefabPath };

            // Mock LoadMainAssetAtPath to return a scriptable object (just something non-null)
            MMFindPrefabsByMono.MockLoadMainAssetAtPath = (path) => ScriptableObject.CreateInstance<MonoScript>();

            // Mock CastToGameObject to deliberately throw an exception when trying to cast to simulate failure
            MMFindPrefabsByMono.MockCastToGameObject = (asset) =>
            {
                throw new InvalidCastException("Simulated cast exception for testing");
            };

            // Expected Result: We expect the catch block to log an error with the given prefab path
            LogAssert.Expect(LogType.Log, "An error occured with prefab " + dummyPrefabPath);

            // Act: We create the window and invoke PerformSearchMissing (which contains the try-catch block)
            var window = ScriptableObject.CreateInstance<MMFindPrefabsByMono>();
            window.PerformSearchMissing();

            // Assert: The test passes if the expected log is output, verifying the exception was successfully caught
        }
    }
}
#endif
