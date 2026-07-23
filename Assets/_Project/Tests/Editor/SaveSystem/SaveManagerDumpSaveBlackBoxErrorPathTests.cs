using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerDumpSaveBlackBoxErrorPathTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            // Clean up the test hook
            typeof(SaveManager).GetField("TestHook_DumpSaveBlackBox", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, null);

            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void DumpSaveBlackBox_CatchesExceptionAndLogsWarning()
        {
            _go = new GameObject("Test_SaveManager");
            var manager = _go.AddComponent<SaveManager>();

            var type = typeof(SaveManager);
            var ensureMethod = type.GetMethod("EnsureSaveTelemetryRing", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(ensureMethod, "Could not find EnsureSaveTelemetryRing method on SaveManager.");
            ensureMethod.Invoke(manager, null);

            // Set up test hook
            Action simulateException = () => throw new InvalidOperationException("Simulated exception during DumpSaveBlackBox");
            var testHookField = typeof(SaveManager).GetField("TestHook_DumpSaveBlackBox", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(testHookField, "Could not find TestHook_DumpSaveBlackBox field.");
            testHookField.SetValue(null, simulateException);

            // Get DumpSaveBlackBox method
            var dumpMethod = type.GetMethod("DumpSaveBlackBox", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(dumpMethod, "Could not find DumpSaveBlackBox method.");

            // Expect the log warning
            LogAssert.Expect(LogType.Warning, "[SaveManager] Save black box dump failed.");

            // Act & Assert
            // We use TargetInvocationException to catch exceptions thrown by reflection
            Assert.DoesNotThrow(() => dumpMethod.Invoke(manager, null), "DumpSaveBlackBox should catch exceptions and not throw them.");
        }
    }
}
