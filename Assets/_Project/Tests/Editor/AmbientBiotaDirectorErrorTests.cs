#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Hecton8.AI.Ambient;
using Hecton8.Core;
using NSubstitute;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class AmbientBiotaDirectorErrorTests
    {
        private GameObject _gameObject;
        private AmbientBiotaDirector _director;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("AmbientBiotaDirectorTest");
            _director = _gameObject.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void WriteTelemetryHeartbeat_TryResolveTelemetryBuffersThrows_ReleasesGuardAndPropagates()
        {
            var go = new GameObject("AmbientBiotaDirector_Test");
            var director = go.AddComponent<AmbientBiotaDirector>();

            var mockVault = Substitute.For<IGlobalDataVault>();
            mockVault.TryAcquireMutationGuard(Arg.Any<ulong>()).Returns(true);

            var vaultField = typeof(AmbientBiotaDirector).GetField("_vault", BindingFlags.Instance | BindingFlags.NonPublic);
            vaultField.SetValue(director, mockVault);

            var telemetryRingHandleField = typeof(AmbientBiotaDirector).GetField("_telemetryRingHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            var dummyHandle = new VaultGenerationHandle<AmbientBiotaTelemetryEntry> { BufferID = (uint)BufferID.BiotaTelemetryRing };
            telemetryRingHandleField.SetValue(director, dummyHandle);

            mockVault.When(x => x.TryResolveHandle(in dummyHandle, out Arg.Any<NativeArray<AmbientBiotaTelemetryEntry>>()))
                .Do(x => { throw new InvalidOperationException("Simulated resolve exception"); });

            var method = typeof(AmbientBiotaDirector).GetMethod("WriteTelemetryHeartbeat", BindingFlags.Instance | BindingFlags.NonPublic);

            var ex = Assert.Throws<TargetInvocationException>(() => method.Invoke(director, null));
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException.Message, Is.EqualTo("Simulated resolve exception"));

            var maskField = typeof(AmbientBiotaDirector).GetField("TelemetryMutationGuardMask", BindingFlags.Static | BindingFlags.NonPublic);
            ulong expectedMask = (ulong)maskField.GetValue(null);

            mockVault.Received(1).ReleaseMutationGuard(expectedMask);

            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void LateFrameTick_WhenExceptionInTryBlock_FinallyBlockExecutesAndReleasesPins()
        {
            // Setup director to have a job pending and job buffers pinned
            SetPrivateField(_director, "_jobPending", true);
            SetPrivateField(_director, "_jobBuffersPinned", true);
            SetPrivateField(_director, "_jobBufferPinMask", 7u); // non-zero mask
            SetPrivateField(_director, "_activeJobHandle", new JobHandle());

            // To test the finally block executing and properly freeing resources
            // when an exception is thrown in the Try block, we invoke LateFrameTick
            // on a background thread. This forces Unity methods (like Graphics.DrawMeshInstancedIndirect
            // used inside LateFrameTick -> RenderIndirectBiota) to throw an InvalidOperationException
            // since they can only be called from the main thread.

            Assert.Throws<AggregateException>(() =>
            {
                Task.Run(() => _director.LateFrameTick()).Wait();
            });

            // Even though an exception was thrown during the try block, the finally block
            // should still run. Because completedJob evaluated to true (since _jobPending was true
            // and the empty JobHandle was completed), ReleaseBiotaJobBufferPins() should have been called.

            bool jobBuffersPinned = GetPrivateField<bool>(_director, "_jobBuffersPinned");
            Assert.IsFalse(jobBuffersPinned, "Finally block should have cleared _jobBuffersPinned");
        }

        [Test]
        public void TryPinBiotaJobBuffers_ExceptionInTryBlock_BubblesUpAndReleasesPins()
        {
            // Setup vault
            var vault = Substitute.For<IGlobalDataVault>();

            // When locking BiotaStates (first call), succeed.
            var stateArray = new NativeArray<AmbientBiotaState>(10, Allocator.Persistent);
            vault.TryLockBuffer(BufferID.BiotaStates, out Arg.Any<NativeArray<AmbientBiotaState>>()).Returns(x =>
            {
                x[1] = stateArray;
                return true;
            });

            // Set private fields using Reflection
            SetPrivateField(_director, "_vault", vault);

            // Throw exception on locking BiotaVelocities
            vault.When(x => x.TryLockBuffer(BufferID.BiotaVelocities, out Arg.Any<NativeArray<float4>>())).Do(x => { throw new InvalidOperationException("Test exception"); });

            var methodInfo = typeof(AmbientBiotaDirector).GetMethod("TryPinBiotaJobBuffers", BindingFlags.NonPublic | BindingFlags.Instance);

            bool exceptionCaught = false;
            try
            {
                methodInfo.Invoke(_director, null);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
            {
                exceptionCaught = true;
            }

            Assert.IsTrue(exceptionCaught, "Expected TryPinBiotaJobBuffers to let the InvalidOperationException bubble up");

            // Verify pins were released for BiotaStates which successfully locked before the exception
            vault.Received().TryUnlockBuffer(BufferID.BiotaStates);

            // Check state was reset
            Assert.IsFalse((bool)GetPrivateField(_director, "_jobBuffersPinned"), "Job buffers should not be pinned");
            Assert.IsNull(GetPrivateField(_director, "_jobBufferPinVault"), "Pin vault should be null");
            Assert.AreEqual(0u, GetPrivateField(_director, "_jobBufferPinMask"), "Pin mask should be 0");

            if (stateArray.IsCreated) stateArray.Dispose();
        }

        private void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(instance, value);
        }

        private object GetPrivateField(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(instance);
        }

        private T GetPrivateField<T>(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field.GetValue(instance);
        }
    }
}
#endif
