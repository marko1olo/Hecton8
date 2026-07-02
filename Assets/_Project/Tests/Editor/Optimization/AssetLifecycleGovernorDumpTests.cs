using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Hecton8.Optimization;
using Hecton8.Core.Memory;

namespace Hecton8.Optimization.Tests
{
    public class AssetLifecycleGovernorDumpTests
    {
        [Test]
        public void DumpHeapTelemetryToFile_ExceptionPath_HandledCorrectly()
        {
            var gameObject = new GameObject();
            var governor = gameObject.AddComponent<AssetLifecycleGovernor>();
            var method = typeof(AssetLifecycleGovernor).GetMethod("DumpHeapTelemetryToFile", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "DumpHeapTelemetryToFile method not found.");

            var telemetry = new NativeArray<AssetHeapTelemetryEntry>(10, Allocator.Temp);

            // Using an invalid character in the filename to trigger an exception
            // inside Path.Combine or similar file operations. The method handles
            // Exception in catch block and releases the allocated memory in finally.
            string invalidFileName = "inval\0id.bin";

            try
            {
                method.Invoke(governor, new object[] { invalidFileName, telemetry });
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"Method threw an exception which was supposed to be caught internally: {ex.InnerException}");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Method threw an exception which was supposed to be caught internally: {ex}");
            }
            finally
            {
                if (telemetry.IsCreated)
                    telemetry.Dispose();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
