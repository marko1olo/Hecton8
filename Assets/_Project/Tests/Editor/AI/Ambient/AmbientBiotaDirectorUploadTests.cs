#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading.Tasks;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;
using Hecton8.World;
using NUnit.Framework;
using NSubstitute;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.AI.Ambient
{
    public class AmbientBiotaDirectorUploadTests
    {
        private AmbientBiotaDirector _director;
        private GameObject _directorGameObject;

        [SetUp]
        public void SetUp()
        {
            _directorGameObject = new GameObject("AmbientBiotaDirector_Test");
            _director = _directorGameObject.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_directorGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_directorGameObject);
            }
        }

        private static object InvokePrivateMethod(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(method, $"Expected private method '{methodName}' to exist.");
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        [Test]
        public void UploadPackedGpuInstances_OnException_UnlocksBuffer()
        {
            GraphicsBuffer dest = null;
            NativeArray<AbsoluteUniversePosition> aups = default;
            NativeArray<float4> velocities = default;
            NativeArray<AmbientBiotaState> states = default;

            try
            {
                dest = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 10, 64);

                aups = new NativeArray<AbsoluteUniversePosition>(10, Allocator.Temp);
                velocities = new NativeArray<float4>(10, Allocator.Temp);
                states = new NativeArray<AmbientBiotaState>(10, Allocator.Temp);

                for (int i = 0; i < 10; i++)
                {
                    aups[i] = new AbsoluteUniversePosition();
                    velocities[i] = new float4(1, 0, 0, 0);
                    states[i] = new AmbientBiotaState { StateFlags = AmbientBiotaState.FlagActive, ScaleMeters = 1f, LifetimeSeconds = 10f, AgeSeconds = 1f, Emission01 = 1f };
                }

                // Call the method in a separate thread. This triggers Unity's main thread guard on JobHandles and Collections.
                // specifically, because we dispose the NativeArray, it will trigger an ObjectDisposedException or InvalidOperationException inside TryBuildGpuInstance!
                aups.Dispose();

                var t = Task.Run(() => {
                    object[] args = new object[] { dest, aups, velocities, states, 10, 10, 0 };
                    InvokePrivateMethod(_director, "UploadPackedGpuInstances", args);
                });

                var ex = Assert.Throws<AggregateException>(() => t.Wait());
                Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>().Or.InstanceOf<ObjectDisposedException>());
            }
            finally
            {
                if (aups.IsCreated) aups.Dispose();
                if (velocities.IsCreated) velocities.Dispose();
                if (states.IsCreated) states.Dispose();
                if (dest != null) dest.Release();
            }
        }
    }
}
#endif
