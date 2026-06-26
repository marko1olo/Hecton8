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

namespace Hecton8.Tests.AI.Ambient
{
    [TestFixture]
    public class AmbientBiotaDirectorExceptionTests
    {
        private AmbientBiotaDirector _director;
        private GameObject _go;
        private IDataVault _mockVault;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("AmbientBiotaDirectorTest");
            _director = _go.AddComponent<AmbientBiotaDirector>();
            _mockVault = Substitute.For<IDataVault>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field.GetValue(obj);
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(obj, value);
        }

        private object CallPrivateMethod(object obj, string methodName, params object[] args)
        {
            var method = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            return method.Invoke(obj, args);
        }

        [Test]
        public void WriteTelemetryHeartbeat_TryResolveTelemetryBuffers_ThrowsException_ExceptionIsCaughtAndFinallyExecutes()
        {
            SetPrivateField(_director, "_vault", _mockVault);

            ulong TelemetryMutationGuardMask = GetPrivateField<ulong>(_director, "TelemetryMutationGuardMask");
            _mockVault.TryAcquireMutationGuard(TelemetryMutationGuardMask).Returns(true);

            var ringHandle = new VaultGenerationHandle<AmbientBiotaTelemetryEntry>
            {
                BufferID = (uint)BufferID.BiotaTelemetryRing,
                Generation = 1u,
                SystemID = (uint)SystemID.AmbientBiota
            };
            SetPrivateField(_director, "_telemetryRingHandle", ringHandle);

            _mockVault.When(x => x.TryResolveHandle(in ringHandle, out Arg.Any<NativeArray<AmbientBiotaTelemetryEntry>>()))
                .Do(x => throw new InvalidOperationException("Test Exception"));

            try
            {
                CallPrivateMethod(_director, "WriteTelemetryHeartbeat");
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"Exception was not caught internally: {ex.InnerException}");
            }

            _mockVault.Received(1).ReleaseMutationGuard(TelemetryMutationGuardMask);
        }
    }
}
#endif
