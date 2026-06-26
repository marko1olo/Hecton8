using System;
using System.Reflection;
using Hecton8.Core;
using NSubstitute;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS

namespace Hecton8.Tests.AI.Ambient
{
    public class AmbientBiotaDirectorTests
    {
        private GameObject _go;
        private AmbientBiotaDirector _director;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("AmbientBiotaDirector");
            _director = _go.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void UploadPackedGpuInstances_ThrowsExceptionInTryBlock_UnlocksBufferInFinally()
        {
            // Reflection info
            var methodInfo = typeof(AmbientBiotaDirector).GetMethod("UploadPackedGpuInstances", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(methodInfo, "UploadPackedGpuInstances method not found");

            var gpuInstanceType = typeof(AmbientBiotaDirector).GetNestedType("AmbientBiotaGpuInstance", BindingFlags.NonPublic);
            int stride = UnsafeUtility.SizeOf(gpuInstanceType);

            int capacity = 10;
            // Create a GraphicsBuffer mapped for writing
            var destination = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, capacity, stride);

            int count = 1;
            var aups = new NativeArray<AbsoluteUniversePosition>(count, Allocator.Temp);
            var velocities = new NativeArray<float4>(count, Allocator.Temp);
            var states = new NativeArray<AmbientBiotaState>(count, Allocator.Temp);

            // To cause an exception INSIDE the try block, we dispose the native arrays.
            // When the loop tries to access aups[i] or states[i], Unity throws an InvalidOperationException.
            aups.Dispose();

            bool exceptionThrown = false;
            try
            {
                object[] parameters = new object[]
                {
                    destination,
                    aups,
                    velocities,
                    states,
                    capacity,
                    1, // targetActiveCount
                    0  // out int visibleCount
                };

                try
                {
                    methodInfo.Invoke(_director, parameters);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }
            finally
            {
                if (velocities.IsCreated) velocities.Dispose();
                if (states.IsCreated) states.Dispose();

                // Unity will complain if we dispose a locked buffer. Since we expect the finally block
                // inside UploadPackedGpuInstances to have unlocked the buffer, this Dispose() should succeed without error.
                destination.Dispose();
            }

            Assert.IsTrue(exceptionThrown, "Expected an exception to be thrown due to disposed NativeArray.");
        }
    }
}
#endif
