#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using NSubstitute;
using UnityEngine;
using Hecton8.AI.Ambient;
using Hecton8.Core.Memory;
using Unity.Collections;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class AmbientBiotaDirectorErrorTests
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
        public void TryPinBiotaJobBuffers_ExceptionInTryBlock_BubblesUpAndReleasesPins()
        {
            // Setup vault
            var vault = Substitute.For<IDataVault>();
            vault.IsCompactionFenceActive.Returns(false);

            // Allow locks but throw on resolve buffers
            vault.TryLockBuffer(Arg.Any<BufferID>(), Arg.Any<SystemID>()).Returns(true);

            // Set private fields using Reflection
            SetPrivateField(_director, "_vault", vault);

            // To test the exception block we need to cause an exception
            // The method TryPinBiotaJobBuffers uses a try-finally block.
            // When an exception is thrown in try, it skips to finally where !success is true,
            // releasing the pins, and then the exception naturally bubbles up.
            vault.When(x => x.TryLockBuffer(BufferID.BiotaVelocities, SystemID.AmbientBiota)).Do(x => { throw new InvalidOperationException("Test exception"); });

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

            // Verify pins were released for BiotaAUPs which successfully locked before the exception
            // We use buffer lock bits internally, calling TryUnlockBuffer with BiotaAUPs
            vault.Received().TryUnlockBuffer(BufferID.BiotaAUPs, SystemID.AmbientBiota);

            // Check state was reset
            Assert.IsFalse((bool)GetPrivateField(_director, "_jobBuffersPinned"), "Job buffers should not be pinned");
            Assert.IsNull(GetPrivateField(_director, "_jobBufferPinVault"), "Pin vault should be null");
            Assert.AreEqual(0u, GetPrivateField(_director, "_jobBufferPinMask"), "Pin mask should be 0");
        }

        private static object GetPrivateField(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field.GetValue(obj);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(obj, value);
        }
    }
}
#endif
