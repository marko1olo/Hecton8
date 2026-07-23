using NUnit.Framework;
using System;
using System.Reflection;
using Hecton8.SaveSystem;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerErrorPathEditTests
    {
        [Test]
        public void PublishSaveStatus_CatchesExceptionAndReports()
        {
            var type = typeof(SaveManager);
            var hookField = type.GetField("PublishSaveLifecycleTestHook", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var publishMethod = type.GetMethod("PublishSaveStatus", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(uint), typeof(uint), typeof(byte), typeof(float), typeof(uint) }, null);

            Action throwAction = () => throw new InvalidOperationException("Test exception in lifecycle");
            hookField.SetValue(null, throwAction);

            // Use LogAssert to prevent the test runner from failing the test when an error is logged.
            // We use Regex to match the exception loosely in case the logging format varies.
            LogAssert.Expect(LogType.Error, new Regex(".*Test exception in lifecycle.*"));

            try
            {
                // Action: Invoke method which will trigger exception via test hook
                publishMethod.Invoke(null, new object[] { 1u, 1u, (byte)1, 0.5f, 1u });
            }
            finally
            {
                hookField.SetValue(null, null);
            }
        }
    }
}
