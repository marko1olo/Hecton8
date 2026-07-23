using NUnit.Framework;
using System;
using System.Reflection;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.Core;

namespace Hecton8.SaveSystem.Tests
{
    public class SaveManagerDisposeTransientNativeArrayBestEffortErrorPathTests
    {
        [Test]
        public void DisposeTransientNativeArrayBestEffort_CatchesException()
        {
            NativeArray<int> array = new NativeArray<int>(1, Allocator.Temp);
            Exception capturedException = null;

            var expectedException = new InvalidOperationException("Test exception from DisposeNativeArrayTestHook");
            typeof(SaveManager).GetField("DisposeNativeArrayTestHook", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, new Action(() => throw expectedException));

            try
            {
                MethodInfo methodInfo = typeof(SaveManager).GetMethod(
                    "DisposeTransientNativeArrayBestEffort",
                    BindingFlags.NonPublic | BindingFlags.Static
                );

                Assert.NotNull(methodInfo, "DisposeTransientNativeArrayBestEffort method not found.");

                MethodInfo genericMethod = methodInfo.MakeGenericMethod(typeof(int));

                object[] args = new object[] { array, capturedException, default(JobHandle), false, "TestSentinel" };

                genericMethod.Invoke(null, args);

                capturedException = args[1] as Exception;

                Assert.NotNull(capturedException, "Expected exception to be captured in firstException.");
                Assert.AreSame(expectedException, capturedException, "The captured exception is not the one we threw.");
            }
            finally
            {
                typeof(SaveManager).GetField("DisposeNativeArrayTestHook", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.SetValue(null, null);

                if (array.IsCreated)
                {
                    array.Dispose();
                }
            }
        }
    }
}
