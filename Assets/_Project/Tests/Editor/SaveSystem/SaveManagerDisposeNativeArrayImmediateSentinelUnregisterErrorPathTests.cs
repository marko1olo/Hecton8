using NUnit.Framework;
using System;
using System.Reflection;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.Core;

namespace Hecton8.SaveSystem.Tests
{
    public class SaveManagerDisposeNativeArrayImmediateSentinelUnregisterErrorPathTests
    {
        [Test]
        public void DisposeNativeArray_ImmediateSentinelUnregisterError_CatchesException()
        {
            NativeArray<int> array = new NativeArray<int>(1, Allocator.Temp);

            var expectedException = new InvalidOperationException("Simulated exception from s_testHookDisposeNativeArrayImmediateSentinelUnregisterError");
            typeof(SaveManager).GetField("s_testHookDisposeNativeArrayImmediateSentinelUnregisterError", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, new Action(() => throw expectedException));

            try
            {
                MethodInfo methodInfo = typeof(SaveManager).GetMethod(
                    "DisposeNativeArray",
                    BindingFlags.NonPublic | BindingFlags.Static
                );

                MethodInfo genericMethod = methodInfo.MakeGenericMethod(typeof(int));

                object[] args = new object[] { array, default(JobHandle), false };

                var exception = Assert.Throws<TargetInvocationException>(() =>
                {
                    genericMethod.Invoke(null, args);
                });

                Assert.NotNull(exception.InnerException, "Expected an inner exception.");
                Assert.AreSame(expectedException, exception.InnerException, "The captured exception is not the one we threw.");
            }
            finally
            {
                typeof(SaveManager).GetField("s_testHookDisposeNativeArrayImmediateSentinelUnregisterError", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, null);

                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }
        }
    }
}
