#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using NSubstitute;
using Hecton8.Core;

namespace Hecton8.AI.Ambient.Tests
{
    public class AmbientBiotaDirectorErrorTests
    {
        private GameObject _directorGameObject;
        private AmbientBiotaDirector _director;

        [SetUp]
        public void Setup()
        {
            _directorGameObject = new GameObject("AmbientBiotaDirectorTest");
            _director = _directorGameObject.AddComponent<AmbientBiotaDirector>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_directorGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_directorGameObject);
            }
        }

        [Test]
        public void Tick_ExceptionInTryBlock_EnsuresJobBufferPinsAreReleased()
        {
            // Set capacity
            SetPrivateField(_director, "_capacity", 100);

            // Dummy handles to bypass initialization checks
            var dummyAups = new VaultGenerationHandle<AbsoluteUniversePosition>(1, 1, BufferID.BiotaAUPs, (uint)SystemID.AmbientBiota);
            var dummyVels = new VaultGenerationHandle<float4>(1, 1, BufferID.BiotaVelocities, (uint)SystemID.AmbientBiota);
            var dummyStates = new VaultGenerationHandle<AmbientBiotaState>(1, 1, BufferID.BiotaStates, (uint)SystemID.AmbientBiota);

            SetPrivateField(_director, "_biotaAupHandle", dummyAups);
            SetPrivateField(_director, "_biotaVelocityHandle", dummyVels);
            SetPrivateField(_director, "_biotaStateHandle", dummyStates);

            var mockVault = Substitute.For<IDataVault>();
            SetPrivateField(_director, "_vault", mockVault);

            SetPrivateField(_director, "_jobPending", false);
            SetPrivateField(_director, "_activeBiotaCount", 0);

            var mockPlayerCtx = Substitute.For<IPlayerRuntimeContext>();
            var poseSnapshot = new PlayerRuntimePoseSnapshot { Aup = new AbsoluteUniversePosition(), RuntimePosition = float3.zero, Forward = new float3(0,0,1) };
            mockPlayerCtx.TryGetPoseSnapshot(out Arg.Any<PlayerRuntimePoseSnapshot>()).Returns(x =>
            {
                x[0] = poseSnapshot;
                return true;
            });
            SetPrivateField(_director, "_playerRuntimeContext", mockPlayerCtx);

            var mockBucketer = Substitute.For<ISimulationBucketerRuntime>();
            SetPrivateField(_director, "_bucketer", mockBucketer);

            // We mock the vault to throw an exception precisely when TryResolveBiotaBuffers is called
            // inside the `try` block. `TryPinBiotaJobBuffers` calls it once, so we throw on the second call.
            int aupViewCount = 0;
            mockVault.When(x => x.OpenView(Arg.Any<uint>(), Arg.Any<int>(), out Arg.Any<NativeArray<AbsoluteUniversePosition>>()))
                .Do(x => {
                    aupViewCount++;
                    if (aupViewCount == 2) // 1 in TryPinBiotaJobBuffers, 2 in Tick try block
                    {
                        throw new InvalidOperationException("Simulated exception inside Tick try block");
                    }
                    x[2] = new NativeArray<AbsoluteUniversePosition>(10, Allocator.TempJob);
                });

            mockVault.TryLockBuffer(Arg.Any<uint>(), Arg.Any<uint>()).Returns(true);

            Assert.Throws<InvalidOperationException>(() => _director.Tick(0.016f));

            bool jobBuffersPinned = GetPrivateField<bool>(_director, "_jobBuffersPinned");
            Assert.IsFalse(jobBuffersPinned, "Job buffers should be unpinned after an exception inside the Tick try block.");
        }

        [Test]
        public void SlowTick_ExceptionInTryBlock_EnsuresJobBufferPinsAreReleased()
        {
            // Set capacity
            SetPrivateField(_director, "_capacity", 100);

            var dummyAups = new VaultGenerationHandle<AbsoluteUniversePosition>(1, 1, BufferID.BiotaAUPs, (uint)SystemID.AmbientBiota);
            var dummyVels = new VaultGenerationHandle<float4>(1, 1, BufferID.BiotaVelocities, (uint)SystemID.AmbientBiota);
            var dummyStates = new VaultGenerationHandle<AmbientBiotaState>(1, 1, BufferID.BiotaStates, (uint)SystemID.AmbientBiota);
            var dummyMacros = new VaultGenerationHandle<int>(1, 1, BufferID.BiotaMacroHydrationCounters, (uint)SystemID.AmbientBiota);
            var dummyTelemetry = new VaultGenerationHandle<AmbientBiotaTelemetryEntry>(1, 1, BufferID.BiotaTelemetryRing, (uint)SystemID.AmbientBiota);
            var dummyCursor = new VaultGenerationHandle<int>(1, 1, BufferID.BiotaTelemetryCursor, (uint)SystemID.AmbientBiota);

            SetPrivateField(_director, "_biotaAupHandle", dummyAups);
            SetPrivateField(_director, "_biotaVelocityHandle", dummyVels);
            SetPrivateField(_director, "_biotaStateHandle", dummyStates);
            SetPrivateField(_director, "_macroHydrationCounterHandle", dummyMacros);
            SetPrivateField(_director, "_telemetryRingHandle", dummyTelemetry);
            SetPrivateField(_director, "_telemetryCursorHandle", dummyCursor);

            var mockVault = Substitute.For<IDataVault>();
            SetPrivateField(_director, "_vault", mockVault);

            SetPrivateField(_director, "_jobPending", false);
            _director.spawnBudgetPerSlowTick = 10;

            var mockPlayerCtx = Substitute.For<IPlayerRuntimeContext>();
            var poseSnapshot = new PlayerRuntimePoseSnapshot { Aup = new AbsoluteUniversePosition(), RuntimePosition = float3.zero, Forward = new float3(0,0,1) };
            mockPlayerCtx.TryGetPoseSnapshot(out Arg.Any<PlayerRuntimePoseSnapshot>()).Returns(x =>
            {
                x[0] = poseSnapshot;
                return true;
            });
            SetPrivateField(_director, "_playerRuntimeContext", mockPlayerCtx);

            var mockBucketer = Substitute.For<ISimulationBucketerRuntime>();
            SetPrivateField(_director, "_bucketer", mockBucketer);

            var mockFlow = Substitute.For<IAbyssalFlowReadModel>();
            SetPrivateField(_director, "_abyssalFlowReadModel", mockFlow);

            int aupViewCount = 0;
            mockVault.When(x => x.OpenView(Arg.Any<uint>(), Arg.Any<int>(), out Arg.Any<NativeArray<AbsoluteUniversePosition>>()))
                .Do(x => {
                    aupViewCount++;
                    if (aupViewCount == 3) // 1 in HasVaultBuffersReady, 2 in TryPinBiotaJobBuffers, 3 in SlowTick try block
                    {
                        throw new InvalidOperationException("Simulated exception inside SlowTick try block");
                    }
                    x[2] = new NativeArray<AbsoluteUniversePosition>(10, Allocator.TempJob);
                });

            mockVault.TryLockBuffer(Arg.Any<uint>(), Arg.Any<uint>()).Returns(true);

            Assert.Throws<InvalidOperationException>(() => _director.SlowTick());

            bool jobBuffersPinned = GetPrivateField<bool>(_director, "_jobBuffersPinned");
            Assert.IsFalse(jobBuffersPinned, "Job buffers should be unpinned after an exception inside the SlowTick try block.");
        }

        private void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
            else throw new Exception($"Field '{fieldName}' not found on type '{target.GetType().Name}'.");
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) return (T)field.GetValue(target);
            throw new Exception($"Field '{fieldName}' not found on type '{target.GetType().Name}'.");
        }
    }
}
#endif
