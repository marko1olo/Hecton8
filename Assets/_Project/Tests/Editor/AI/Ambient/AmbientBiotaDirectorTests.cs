#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using NSubstitute;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.AI.Ambient;
using Hecton8.Core;
using System;
using System.Reflection;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.AI.Ambient
{
    [TestFixture]
    public class AmbientBiotaDirectorTests
    {
        private AmbientBiotaDirector _director;
        private GameObject _directorGo;

        [SetUp]
        public void SetUp()
        {
            _directorGo = new GameObject("AmbientBiotaDirector");
            _director = _directorGo.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_directorGo != null)
                GameObject.DestroyImmediate(_directorGo);
        }

        [Test]
        public void Tick_ThrowsException_ReleasesBufferPins()
        {
            var playerRuntimeContext = Substitute.For<IPlayerRuntimeContext>();
            var pose = new PlayerRuntimePoseSnapshot
            {
                Flags = (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot,
                RuntimePosition = new float3(1, 1, 1),
                Aup = new AbsoluteUniversePosition { x = 0, y = 0, z = 0, sectorX = 0, sectorY = 0, sectorZ = 0 },
                Forward = new float3(0, 0, 1)
            };
            playerRuntimeContext.TryGetPlayerPoseSnapshot(out Arg.Any<PlayerRuntimePoseSnapshot>()).Returns(x =>
            {
                x[0] = pose;
                return true;
            });

            var registry = Substitute.For<IGlobalRegistry>();
            registry.TryResolve(out Arg.Any<IPlayerRuntimeContext>()).Returns(x =>
            {
                x[0] = playerRuntimeContext;
                return true;
            });

            var vault = Substitute.For<IGlobalDataVault>();
            var aupArray = new NativeArray<AbsoluteUniversePosition>(10, Allocator.Persistent);
            var velArray = new NativeArray<float4>(10, Allocator.Persistent);
            var stateArray = new NativeArray<AmbientBiotaState>(10, Allocator.Persistent);

            vault.TryLockBuffer(BufferID.BiotaStates, out Arg.Any<NativeArray<AmbientBiotaState>>()).Returns(x =>
            {
                x[1] = stateArray;
                return true;
            });
            vault.TryLockBuffer(BufferID.BiotaVelocities, out Arg.Any<NativeArray<float4>>()).Returns(x =>
            {
                x[1] = velArray;
                return true;
            });
            vault.TryLockBuffer(BufferID.BiotaAUPs, out Arg.Any<NativeArray<AbsoluteUniversePosition>>()).Returns(x =>
            {
                x[1] = aupArray;
                return true;
            });

            registry.TryResolve(out Arg.Any<IGlobalDataVault>()).Returns(x =>
            {
                x[0] = vault;
                return true;
            });

            SetPrivateField(_director, "_registry", registry);
            SetPrivateField(_director, "_vault", vault);
            SetPrivateField(_director, "_hasSetupRuntime", true);

            try
            {
                _director.Tick(0.1f);
            }
            catch (Exception)
            {
                // Expected throw
            }

            vault.Received().TryUnlockBuffer(BufferID.BiotaStates);
            vault.Received().TryUnlockBuffer(BufferID.BiotaVelocities);
            vault.Received().TryUnlockBuffer(BufferID.BiotaAUPs);

            if (aupArray.IsCreated) aupArray.Dispose();
            if (velArray.IsCreated) velArray.Dispose();
            if (stateArray.IsCreated) stateArray.Dispose();
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

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
        }
    }
}
#endif
