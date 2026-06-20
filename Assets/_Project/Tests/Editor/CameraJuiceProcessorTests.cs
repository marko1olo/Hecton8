using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class CameraJuiceProcessorTests
    {
        private CameraJuiceProcessor _processor;
        private SuitData _suitData;

        [SetUp]
        public void Setup()
        {
            _processor = new CameraJuiceProcessor();
            _suitData = ScriptableObject.CreateInstance<SuitData>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_suitData != null)
            {
                Object.DestroyImmediate(_suitData);
            }
        }

        [Test]
        public void Process_WithZeroDeltaTime_ReturnsZeroOutput()
        {
            var input = new CameraJuiceInput { deltaTime = 0f };
            var output = _processor.Process(in input, _suitData);

            Assert.AreEqual(Vector3.zero, output.localPositionOffset);
            Assert.AreEqual(0f, output.rollOffset);
            Assert.AreEqual(0f, output.pitchOffset);
            Assert.AreEqual(0f, output.fovOffset);
            Assert.AreEqual(0, output.stepEvent);
        }

        [Test]
        public void Process_DryGroundWalk_WithMovement_AppliesHeadBob()
        {
            _suitData.maxWalkSpeed = 5f;
            _suitData.bobTransitionSpeed = 10f;
            _suitData.bobVerticalAmplitude = 0.1f;
            _suitData.bobHorizontalAmplitude = 0.05f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.DryGroundWalk,
                isGrounded = 1,
                hasMovementInput = 1,
                horizontalSpeed = 5f,
                heavyCarryLoad = 0f
            };

            // Need to process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            // We expect some non-zero offsets from head bob
            Assert.AreNotEqual(Vector3.zero, output.localPositionOffset);
        }

        [Test]
        public void Process_SubmergeChange_RegistersSplashDip()
        {
            _suitData.submergeThreshold = 0.85f;
            _suitData.splashImmersionRateThreshold = 0.5f;
            _suitData.splashMinVerticalSpeed = 1.0f;
            _suitData.splashCameraDip = 0.05f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                immersionRatio = 0.5f,
                verticalVelocity = -2f // Falling down
            };

            // First frame not submerged
            _processor.Process(in input, _suitData);

            // Second frame submerged quickly
            input.immersionRatio = 0.9f;
            var output = _processor.Process(in input, _suitData);

            // Splash dip should affect Y offset downwards
            Assert.Less(output.localPositionOffset.y, 0f);
        }

        [Test]
        public void Process_CollisionShake_RecoversOverTime()
        {
            _suitData.enableCollisionShake = true;
            _suitData.collisionShakeThreshold = 1f;
            _suitData.collisionShakeMaxVelocity = 10f;
            _suitData.collisionShakeMaxAmplitude = 0.5f;
            _suitData.collisionShakeMaxPitch = 5f;
            _suitData.collisionShakeRecoveryOmega = 10f;

            _processor.RegisterCollisionImpulse(10f, _suitData);

            var input = new CameraJuiceInput { deltaTime = 0.1f };

            // First frame after impact should have high offsets
            var initialOutput = _processor.Process(in input, _suitData);

            // After 1 second of recovery, offsets should be smaller or zero
            input.deltaTime = 1.0f;
            var finalOutput = _processor.Process(in input, _suitData);

            Assert.Less(Mathf.Abs(finalOutput.localPositionOffset.x), Mathf.Abs(initialOutput.localPositionOffset.x));
            Assert.Less(Mathf.Abs(finalOutput.localPositionOffset.y), Mathf.Abs(initialOutput.localPositionOffset.y));
            Assert.Less(Mathf.Abs(finalOutput.pitchOffset), Mathf.Abs(initialOutput.pitchOffset));
        }

        [Test]
        public void Process_DepthFovCompression_NarrowsFov()
        {
            _suitData.enableDepthFovCompression = true;
            _suitData.depthFovCompressionStart = 10f;
            _suitData.depthFovCompressionEnd = 100f;
            _suitData.depthFovCompressionMax = 10f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                depth = 100f // Max depth compression
            };

            var output = _processor.Process(in input, _suitData);

            // We expect FOV offset to be negative (narrowing)
            Assert.AreEqual(-10f, output.fovOffset, 0.001f);
        }

        [Test]
        public void Process_SwimLocomotion_AppliesSwimBob()
        {
            _suitData.enableSwimBob = true;
            _suitData.maxSwimSpeed = 5f;
            _suitData.swimBobTransitionSpeed = 10f;
            _suitData.swimBobVerticalAmplitude = 0.1f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.Swim,
                hasMovementInput = 1,
                swimSpeed = 5f
            };

            // Need to process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            // We expect some non-zero offsets from swim bob
            Assert.AreNotEqual(0f, output.localPositionOffset.y);
        [Test]
        public void ClearActionBob_ResetsActionBobIntensityToZero()
        {
            // Arrange
            var processor = new CameraJuiceProcessor();

            // Set up initial state where action bob intensity is non-zero
            processor.RegisterActionBob(1.0f);

            // Get private field using reflection
            var fieldInfo = typeof(CameraJuiceProcessor).GetField("_actionBobIntensity", BindingFlags.NonPublic | BindingFlags.Instance);

            // Verify our setup worked
            float initialIntensity = (float)fieldInfo.GetValue(processor);
            Assert.AreEqual(1.0f, initialIntensity, "Setup failed: _actionBobIntensity was not set correctly.");

            // Act
            processor.ClearActionBob();

            // Assert
            float clearedIntensity = (float)fieldInfo.GetValue(processor);
            Assert.AreEqual(0f, clearedIntensity, "ClearActionBob did not reset _actionBobIntensity to 0.");
        }
    }
}
