using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.Core;
using Hecton8.Inventory;

namespace Hecton8.Tests
{
    public class PlayerInventoryDisposeTests
    {
        [Test]
        public void DisposeNativeArray_HandlesUnregisterPointerException()
        {
            // Set up an array to dispose
            var array = new NativeArray<int>(1, Allocator.Temp);

            // To test DisposeNativeArray, we will use reflection to force it to fail.
            // But doing so with _records reflection breaks value type `ref` copy back when exception is thrown.
            // Let's modify the way we test it. We just need an exception inside UnregisterPointer.
            // What if we pass an invalid pointer? UnregisterPointer takes void* or IntPtr. It doesn't throw on invalid pointers, it just checks against the list.

            // How can we make UnregisterPointer throw?
            // The only exception UnregisterPointer might throw natively is during EnterMutationGate spinning, but that's a SpinWait loop, it doesn't throw.
            // It could throw IndexOutOfRangeException if _count is larger than _records.Length.
            // It could throw NullReferenceException if _records is null.

            FieldInfo recordsField = typeof(NativeMemorySentinel).GetField("_records", BindingFlags.NonPublic | BindingFlags.Static);
            object originalRecords = recordsField.GetValue(null);

            FieldInfo countField = typeof(NativeMemorySentinel).GetField("_count", BindingFlags.NonPublic | BindingFlags.Static);
            int originalCount = (int)countField.GetValue(null);

            // Using reflection to invoke DisposeNativeArray and catching the exception
            // Wait, we need to make sure we don't double-free the array. We can use a different allocator that is easier, or just let it leak if it fails?
            // Allocator.Temp throws if we double-free.
            // DisposeNativeArray sets `array = default;` inside its finally block.
            // But since reflection throws, the `ref` arg is NOT copied back to `args` array!
            // However, the memory is actually disposed by `array.Dispose()` inside `DisposeNativeArray`.
            // So `array.Dispose()` in our finally block will double-free.
            // We shouldn't call `array.Dispose()` in finally if we know `DisposeNativeArray` already did it.

            // Let's check `DisposeNativeArray`:
            /*
            try
            {
                array.Dispose();
            }
            ...
            finally
            {
                array = default;
            }
            */
            // Yes, it ALWAYS disposes it, even if UnregisterPointer throws.
            // Wait, what if `array.Dispose()` itself throws?

            bool disposed = false;
            try
            {
                // Inject failure
                recordsField.SetValue(null, null);
                countField.SetValue(null, 1);

                var methodInfo = typeof(PlayerInventory).GetMethod("DisposeNativeArray", BindingFlags.NonPublic | BindingFlags.Static);
                var genericMethod = methodInfo.MakeGenericMethod(typeof(int));

                var args = new object[] { array };

                // When genericMethod is invoked, it will hit NullReferenceException in UnregisterPointer.
                // That exception is caught and stored. Then `array.Dispose()` runs, successfully disposing the memory.
                // Then `array = default;` is set on the boxed struct.
                // Finally, the caught NullReferenceException is thrown.

                var ex = Assert.Throws<TargetInvocationException>(() => genericMethod.Invoke(null, args));

                // Memory was disposed during the invocation.
                disposed = true;

                Assert.IsInstanceOf<NullReferenceException>(ex.InnerException);
            }
            finally
            {
                // Restore state
                recordsField.SetValue(null, originalRecords);
                countField.SetValue(null, originalCount);

                // If it wasn't disposed inside the method (e.g. exception thrown BEFORE array.Dispose()), we must dispose it.
                // But it's Temp allocated, and NativeArray checks validity.
                if (!disposed && array.IsCreated)
                {
                    array.Dispose();
                }
            }
        }
    }
}
