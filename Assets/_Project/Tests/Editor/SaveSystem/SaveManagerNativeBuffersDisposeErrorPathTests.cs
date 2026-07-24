using System;
using System.Reflection;
using Hecton8.SaveSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public class SaveManagerNativeBuffersDisposeErrorPathTests
    {
        [Test]
        public void ShutdownServiceState_CatchesExceptionFromNativeBuffersDispose()
        {
            var go = new GameObject("Test_SaveManager");
            var manager = go.AddComponent<SaveManager>();

            var type = typeof(SaveManager);

            var nativeBuffersField = type.GetField("_nativeBuffers", BindingFlags.NonPublic | BindingFlags.Instance);
            var nativeBuffers = nativeBuffersField.GetValue(manager);

            var nativeBufferSetType = nativeBuffers.GetType();
            var testHookField = nativeBufferSetType.GetField("TestHook_DisposeThrow", BindingFlags.NonPublic | BindingFlags.Instance);

            Action throwAction = () => { throw new InvalidOperationException("Simulated nativeBuffers dispose failure"); };
            testHookField.SetValue(nativeBuffers, throwAction);

            // Re-assign in case nativeBuffers is a struct
            if (nativeBufferSetType.IsValueType)
            {
                nativeBuffersField.SetValue(manager, nativeBuffers);
            }

            var shutdownMethod = type.GetMethod("ShutdownServiceState", BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                var ex = Assert.Throws<TargetInvocationException>(() => shutdownMethod.Invoke(manager, null));
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException, "Expected InvalidOperationException to be caught and re-thrown.");
                Assert.AreEqual("Simulated nativeBuffers dispose failure", ex.InnerException.Message);
            }
            finally
            {
                testHookField.SetValue(nativeBuffers, null);
                if (nativeBufferSetType.IsValueType)
                {
                    nativeBuffersField.SetValue(manager, nativeBuffers);
                }
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
