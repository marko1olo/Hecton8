using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerReleaseOwnedBufferErrorPathTests
    {
        [Test]
        public void ReleaseOwnedBuffer_HandlesDisposeException_WhenBufferIsInvalid()
        {
            var type = typeof(SaveManager).GetNestedType("StaticNativeBuffers", BindingFlags.NonPublic);
            var method = type.GetMethod("ReleaseOwnedBuffer", BindingFlags.NonPublic | BindingFlags.Static);

            // To intentionally trigger an exception to test catch blocks processing NativeArray data,
            // pass an uninitialized array. Accessing uninitialized arrays throws InvalidOperationException.
            NativeArray<byte> buffer = default(NativeArray<byte>);

            var ex = Assert.Throws<TargetInvocationException>(() =>
            {
                method.Invoke(null, new object[] { buffer });
            });

            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException, "Expected InvalidOperationException to be thrown by uninitialized NativeArray properties/Dispose.");
        }
    }
}
