using System;
using System.Reflection;
using NUnit.Framework;
using Hecton8.SaveSystem;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public class SaveManagerDisposeEditorNativeBuffersForLifecycleExceptionTests
    {
        [Test]
        public void DisposeEditorNativeBuffersForLifecycle_CatchesStaticNativeBuffersDisposeException()
        {
            var saveManagerType = typeof(SaveManager);
            var staticNativeBuffersType = saveManagerType.GetNestedType("StaticNativeBuffers", BindingFlags.NonPublic);

            var testHookField = staticNativeBuffersType.GetField("TestHook_DisposeThrow", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(testHookField, "TestHook_DisposeThrow field not found.");

            Action throwAction = () => { throw new InvalidOperationException("Simulated dispose exception"); };
            testHookField.SetValue(null, throwAction);

            try
            {
                var disposeMethod = saveManagerType.GetMethod("DisposeEditorNativeBuffersForLifecycle", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.IsNotNull(disposeMethod, "DisposeEditorNativeBuffersForLifecycle method not found.");

                LogAssert.Expect(LogType.Warning, "[SaveManager] Editor lifecycle native buffer shutdown fault: Simulated dispose exception");

                disposeMethod.Invoke(null, null);
            }
            finally
            {
                testHookField.SetValue(null, null);
            }
        }
    }
}
