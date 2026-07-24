using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Hecton8.SaveSystem;
using System.Runtime.Serialization;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerReleaseOwnedBufferUnregisterPointerErrorPathTests
    {
        [Test]
        public void ReleaseOwnedBuffer_HandlesUnregisterPointerException()
        {
            var type = typeof(SaveManager).GetNestedType("StaticNativeBuffers", BindingFlags.NonPublic);
            var method = type.GetMethod("ReleaseOwnedBuffer", BindingFlags.NonPublic | BindingFlags.Static);
            var hookField = type.GetField("s_TestReleaseOwnedBufferUnregisterHook", BindingFlags.NonPublic | BindingFlags.Static);

            // ReleaseOwnedBuffer takes a NativeArray<byte> buffer argument directly, as verified in source.
            NativeArray<byte> buffer = new NativeArray<byte>(1, Allocator.Temp);
            var expectedException = new InvalidOperationException("Test exception from UnregisterPointer");

            hookField?.SetValue(null, new Action(() => throw expectedException));

            try
            {
                var ex = Assert.Throws<TargetInvocationException>(() =>
                {
                    method.Invoke(null, new object[] { buffer });
                });
                Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException, "Expected InvalidOperationException to be thrown by our hook.");
                Assert.AreSame(expectedException, ex.InnerException, "Expected exception instance was not the same.");

                // Assert that the fallback behavior correctly disposed the array despite the exception.
                // In Unity, accessing an element of a disposed NativeArray throws an InvalidOperationException.
                Assert.Throws<InvalidOperationException>(() =>
                {
                    var b = buffer[0];
                }, "Expected the buffer to be disposed by the fallback behavior.");
            }
            finally
            {
                hookField?.SetValue(null, null);
                // Do not attempt to re-dispose, the fallback correctly disposed it,
                // and double-disposing throws exceptions.
            }
        }
    }
}
