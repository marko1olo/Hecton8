using System;
using System.Collections.Generic;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerDisposeErrorPathTests
    {
        [TearDown]
        public void TearDown()
        {
            SaveManager.Test_OnBeforeShutdownServiceState = null;

            // Clear the known instances list just in case
            var type = typeof(SaveManager);
            var knownInstancesField = type.GetField("s_KnownInstances", BindingFlags.NonPublic | BindingFlags.Static);
            if (knownInstancesField != null)
            {
                var list = knownInstancesField.GetValue(null) as List<SaveManager>;
                if (list != null)
                {
                    list.Clear();
                }
            }
        }

        [Test]
        public void DisposeEditorNativeBuffersForLifecycle_CatchesExceptionFromShutdown()
        {
            var go = new GameObject("Test_SaveManager");
            var manager = go.AddComponent<SaveManager>();

            // Inject our test manager into s_KnownInstances
            var type = typeof(SaveManager);
            var knownInstancesField = type.GetField("s_KnownInstances", BindingFlags.NonPublic | BindingFlags.Static);
            var list = knownInstancesField.GetValue(null) as List<SaveManager>;
            list.Add(manager);

            // Throw an exception when our internal test hook is invoked
            SaveManager.Test_OnBeforeShutdownServiceState = () => throw new InvalidOperationException("Simulated shutdown failure");

            // Expect the log warning
            LogAssert.Expect(LogType.Warning, "[SaveManager] Editor lifecycle native buffer shutdown fault: Simulated shutdown failure");

            // Call the private lifecycle method
            var method = type.GetMethod("DisposeEditorNativeBuffersForLifecycle", BindingFlags.NonPublic | BindingFlags.Static);
            method.Invoke(null, null);

            // Clean up
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
