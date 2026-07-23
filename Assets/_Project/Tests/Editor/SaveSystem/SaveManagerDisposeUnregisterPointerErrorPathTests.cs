using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.SaveSystem;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class SaveManagerDisposeUnregisterPointerErrorPathTests
    {
        [TearDown]
        public void TearDown()
        {
            var type = typeof(SaveManager);
            var hookField = type.GetField("TestHook_UnregisterPointer_SimulateException", BindingFlags.NonPublic | BindingFlags.Static);
            if (hookField != null)
            {
                hookField.SetValue(null, null);
            }
        }

        [Test]
        public void DisposeNativeArray_DeferDisposal_CatchesExceptionFromUnregisterPointer()
        {
            var array = new NativeArray<int>(1, Allocator.Temp);
            var expectedException = new InvalidOperationException("Test exception from UnregisterPointer hook");

            var type = typeof(SaveManager);
            var hookField = type.GetField("TestHook_UnregisterPointer_SimulateException", BindingFlags.NonPublic | BindingFlags.Static);
            hookField.SetValue(null, new Action(() => throw expectedException));

            var method = type.GetMethod("DisposeNativeArray", BindingFlags.NonPublic | BindingFlags.Static);
            var genericMethod = method.MakeGenericMethod(typeof(int));

            // arguments for ref NativeArray<T> array, JobHandle dependency = default, bool deferDisposal = false
            object[] args = new object[] { array, default(JobHandle), true };

            var ex = Assert.Throws<TargetInvocationException>(() =>
            {
                genericMethod.Invoke(null, args);
            });

            Assert.AreSame(expectedException, ex.InnerException);

            // Re-fetch array as it's passed by ref
            var outArray = (NativeArray<int>)args[0];
            if (outArray.IsCreated)
            {
                outArray.Dispose();
            }
        }

        [Test]
        public void DisposeNativeArray_Immediate_CatchesExceptionFromUnregisterPointer()
        {
            var array = new NativeArray<int>(1, Allocator.Temp);
            var expectedException = new InvalidOperationException("Test exception from UnregisterPointer hook");

            var type = typeof(SaveManager);
            var hookField = type.GetField("TestHook_UnregisterPointer_SimulateException", BindingFlags.NonPublic | BindingFlags.Static);
            hookField.SetValue(null, new Action(() => throw expectedException));

            var method = type.GetMethod("DisposeNativeArray", BindingFlags.NonPublic | BindingFlags.Static);
            var genericMethod = method.MakeGenericMethod(typeof(int));

            // arguments for ref NativeArray<T> array, JobHandle dependency = default, bool deferDisposal = false
            object[] args = new object[] { array, default(JobHandle), false };

            var ex = Assert.Throws<TargetInvocationException>(() =>
            {
                genericMethod.Invoke(null, args);
            });

            Assert.AreSame(expectedException, ex.InnerException);

            var outArray = (NativeArray<int>)args[0];
            if (outArray.IsCreated)
            {
                outArray.Dispose();
            }
        }
    }
}
