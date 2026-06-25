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
            SetPrivateField(_director, "_playerRuntimeContext", playerRuntimeContext);

            var vault = Substitute.For<IDataVault>();
            vault.IsCompactionFenceActive.Returns(false);

            var aupArray = new NativeArray<AbsoluteUniversePosition>(128, Allocator.Temp);
            var velArray = new NativeArray<float4>(128, Allocator.Temp);
            var stateArray = new NativeArray<AmbientBiotaState>(128, Allocator.Temp);

            stateArray.Dispose(); // Dispose to cause an InvalidOperationException when scheduled

            vault.TryGetBufferView(BufferID.BiotaAUPs, out Arg.Any<NativeArray<AbsoluteUniversePosition>>()).Returns(x => { x[1] = aupArray; return true; });
            vault.TryGetBufferView(BufferID.BiotaVelocities, out Arg.Any<NativeArray<float4>>()).Returns(x => { x[1] = velArray; return true; });
            vault.TryGetBufferView(BufferID.BiotaStates, out Arg.Any<NativeArray<AmbientBiotaState>>()).Returns(x => { x[1] = stateArray; return true; });

            SetPrivateField(_director, "_vault", vault);
            SetPrivateField(_director, "_capacity", 128);
            SetPrivateField(_director, "survivalCapacity", 128);

            SetPrivateField(_director, "_biotaAupHandle", new VaultGenerationHandle<AbsoluteUniversePosition>(BufferID.BiotaAUPs, 1));
            SetPrivateField(_director, "_biotaVelocityHandle", new VaultGenerationHandle<float4>(BufferID.BiotaVelocities, 1));
            SetPrivateField(_director, "_biotaStateHandle", new VaultGenerationHandle<AmbientBiotaState>(BufferID.BiotaStates, 1));

            vault.GetBufferGeneration(BufferID.BiotaAUPs).Returns(1u);
            vault.GetBufferGeneration(BufferID.BiotaVelocities).Returns(1u);
            vault.GetBufferGeneration(BufferID.BiotaStates).Returns(1u);

            vault.TryLockBuffer(BufferID.BiotaAUPs).Returns(true);
            vault.TryLockBuffer(BufferID.BiotaVelocities).Returns(true);
            vault.TryLockBuffer(BufferID.BiotaStates).Returns(true);

            SetPrivateField(_director, "_jobBuffersPinned", false);
            SetPrivateField(_director, "_jobBufferPinVault", vault);
            SetPrivateField(_director, "_jobBufferPinMask", 0u);

            bool threwException = false;
            try
            {
                _director.Tick(0.02f);
            }
            catch (Exception)
            {
                threwException = true;
            }

            Assert.IsTrue(threwException, "Expected Tick to throw an exception when scheduling a job with disposed NativeArrays.");

            Assert.IsFalse((bool)GetPrivateField<bool>(_director, "_jobBuffersPinned"));
            Assert.AreEqual(0u, (uint)GetPrivateField<uint>(_director, "_jobBufferPinMask"));

            vault.Received().TryUnlockBuffer(BufferID.BiotaStates);
            vault.Received().TryUnlockBuffer(BufferID.BiotaVelocities);
            vault.Received().TryUnlockBuffer(BufferID.BiotaAUPs);

            if (aupArray.IsCreated) aupArray.Dispose();
            if (velArray.IsCreated) velArray.Dispose();
            if (stateArray.IsCreated) stateArray.Dispose();
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                field.SetValue(target, value);
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return (T)field.GetValue(target);
            return default;
        }
    }
}
#endif
