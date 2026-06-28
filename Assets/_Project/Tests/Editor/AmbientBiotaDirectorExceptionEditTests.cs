#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using NSubstitute;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Hecton8.AI.Ambient;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class AmbientBiotaDirectorExceptionEditTests
    {
        private GameObject _go;
        private AmbientBiotaDirector _director;
        private MethodInfo _tryEnsureGraphicsResourcesColdMethod;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AmbientBiotaDirectorTestObj");
            _director = _go.AddComponent<AmbientBiotaDirector>();
            _tryEnsureGraphicsResourcesColdMethod = typeof(AmbientBiotaDirector).GetMethod(
                "TryEnsureGraphicsResourcesCold",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void TryEnsureGraphicsResourcesCold_WhenExceptionThrown_CatchesAndReturnsFalse()
        {
            // Capacity <= 0 returns false immediately in EnsureGraphicsResources, bypassing GraphicsBuffer creation.
            // To force an exception in EnsureGraphicsResources, we need a capacity > 0 but invalid.
            // For AmbientBiotaGpuInstance, passing int.MaxValue will exceed buffer size limits
            // and Unity throws ArgumentException or InvalidOperationException from GraphicsBuffer constructor.

            bool result = false;

            // Just invoke with int.MaxValue. If it throws ArgumentException, it'll be caught.
            result = (bool)_tryEnsureGraphicsResourcesColdMethod.Invoke(_director, new object[] { int.MaxValue });

            Assert.IsFalse(result, "Exceeding buffer size limits should throw, be caught, and return false.");
        }

        [Test]
        public void TryPinBiotaJobBuffersReleasesPinsWhenExceptionThrown()
        {
            // 1. Arrange a fake vault
            IGlobalDataVault vault = Substitute.For<IGlobalDataVault>();

            // When locking BiotaStates (first call), succeed.
            var stateArray = new NativeArray<AmbientBiotaState>(10, Allocator.Persistent);
            vault.TryLockBuffer(BufferID.BiotaStates, out Arg.Any<NativeArray<AmbientBiotaState>>()).Returns(x =>
            {
                x[1] = stateArray;
                return true;
            });

            // When locking BiotaVelocities (second call), throw an exception.
            vault.When(v => v.TryLockBuffer(BufferID.BiotaVelocities, out Arg.Any<NativeArray<float4>>()))
                 .Do(callInfo => { throw new InvalidOperationException("Test exception in try block"); });

            // Set the private _vault field
            SetPrivateField(_director, "_vault", vault);

            // 2. Act
            Exception caughtException = null;
            try
            {
                InvokePrivateMethod(_director, "TryPinBiotaJobBuffers");
            }
            catch (TargetInvocationException ex)
            {
                caughtException = ex.InnerException;
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }

            // 3. Assert
            Assert.IsNotNull(caughtException, "Expected InvalidOperationException to be thrown (unwrapped from TargetInvocationException)");
            Assert.IsInstanceOf<InvalidOperationException>(caughtException, "Expected caught exception to be InvalidOperationException");

            // Ensure TryUnlockBuffer was called for the successful lock (BiotaStates) in the finally block
            vault.Received(1).TryUnlockBuffer(BufferID.BiotaStates);

            if (stateArray.IsCreated) stateArray.Dispose();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
            field.SetValue(target, value);
        }

        private static object InvokePrivateMethod(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Method {methodName} not found on {target.GetType()}");
            return method.Invoke(target, null);
        }
    }
}
#endif
