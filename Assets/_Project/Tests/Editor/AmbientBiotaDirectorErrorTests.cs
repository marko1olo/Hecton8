#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using System;
using System.Reflection;
using Unity.Collections;
using Hecton8.AI.Ambient;
using Hecton8.Core;
using NSubstitute;

namespace Hecton8.Tests.Editor
{
    public sealed class AmbientBiotaDirectorErrorTests
    {
        [Test]
        public void WriteTelemetryHeartbeat_TryResolveTelemetryBuffersThrows_ReleasesGuardAndPropagates()
        {
            var go = new GameObject("AmbientBiotaDirector_Test");
            var director = go.AddComponent<AmbientBiotaDirector>();

            var mockVault = Substitute.For<IDataVault>();
            mockVault.IsCompactionFenceActive.Returns(false);
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
    }
}
#endif
