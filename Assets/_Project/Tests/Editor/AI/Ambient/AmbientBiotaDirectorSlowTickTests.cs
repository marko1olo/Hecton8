#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Hecton8.AI.Ambient;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Mathematics;
using Hecton8.World;
using Unity.Jobs;

namespace Hecton8.Tests.AI.Ambient
{
    [TestFixture]
    public class AmbientBiotaDirectorSlowTickTests
    {
        private GameObject _go;
        private AmbientBiotaDirector _director;
        private IDataVault _mockVault;
        private IEcosystemDirectorService _mockEcosystem;
        private IPlayerRuntimeContext _mockPlayerRuntimeContext;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestAmbientBiotaDirector");
            _director = _go.AddComponent<AmbientBiotaDirector>();

            _mockVault = Substitute.For<IDataVault>();
            _mockEcosystem = Substitute.For<IEcosystemDirectorService>();
            _mockPlayerRuntimeContext = Substitute.For<IPlayerRuntimeContext>();

            SetPrivateField(_director, "_vault", _mockVault);
            SetPrivateField(_director, "_ecosystem", _mockEcosystem);
            SetPrivateField(_director, "_playerRuntimeContext", _mockPlayerRuntimeContext);
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void SlowTick_WhenJobPending_ReturnsEarly()
        {
            SetPrivateField(_director, "_jobPending", true);

            _director.SlowTick();

            _mockPlayerRuntimeContext.DidNotReceive().TryGetPlayerPoseSnapshot(out _);
            _mockEcosystem.DidNotReceive().TryGetBiomassAvailability(Arg.Any<Vector3>(), out _, out _, out _);
        }

        [Test]
        public void SlowTick_WhenVaultBuffersNotReady_ReturnsEarly()
        {
            SetPrivateField(_director, "_jobPending", false);
            // By default _capacity is 0, making HasVaultBuffersReadyNoGrow() return false

            _director.SlowTick();

            _mockPlayerRuntimeContext.DidNotReceive().TryGetPlayerPoseSnapshot(out _);
        }

        [Test]
        public void TryCapturePlayerPose_WhenPlayerContextIsNull_ReturnsFalse()
        {
            SetPrivateField(_director, "_playerRuntimeContext", null);

            var method = typeof(AmbientBiotaDirector).GetMethod("TryCapturePlayerPose", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = new object[] { default(PlayerRuntimePoseSnapshot) };
            bool result = (bool)method.Invoke(_director, parameters);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryCapturePlayerPose_WhenPlayerPoseFails_ReturnsFalse()
        {
            PlayerRuntimePoseSnapshot emptySnapshot = default;
            _mockPlayerRuntimeContext.TryGetPlayerPoseSnapshot(out _)
                .Returns(x =>
                {
                    x[0] = emptySnapshot;
                    return false;
                });

            var method = typeof(AmbientBiotaDirector).GetMethod("TryCapturePlayerPose", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = new object[] { default(PlayerRuntimePoseSnapshot) };
            bool result = (bool)method.Invoke(_director, parameters);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryCapturePlayerPose_WhenPoseHasNoRootFlag_ReturnsFalse()
        {
            PlayerRuntimePoseSnapshot snapshot = default;
            snapshot.Flags = 0; // Missing HasPlayerRoot flag
            snapshot.RuntimePosition = new float3(0, 0, 0);

            _mockPlayerRuntimeContext.TryGetPlayerPoseSnapshot(out _)
                .Returns(x =>
                {
                    x[0] = snapshot;
                    return true;
                });

            var method = typeof(AmbientBiotaDirector).GetMethod("TryCapturePlayerPose", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] parameters = new object[] { default(PlayerRuntimePoseSnapshot) };
            bool result = (bool)method.Invoke(_director, parameters);

            Assert.IsFalse(result);
        }

        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }
    }
}
#endif
