using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Gameplay;

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
        public void Process_DryInteriorWalk_WithMovement_AppliesScaledHeadBob()
        {
            _suitData.maxWalkSpeed = 5f;
            _suitData.bobTransitionSpeed = 10f;
            _suitData.bobVerticalAmplitude = 0.1f;
            _suitData.bobHorizontalAmplitude = 0.05f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.DryInteriorWalk,
                isGrounded = 1,
                hasMovementInput = 1,
                horizontalSpeed = 5f,
                heavyCarryLoad = 0f
            };

            // Process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            Assert.AreNotEqual(Vector3.zero, output.localPositionOffset);
        }

        [Test]
        public void Process_ShallowWadeWalk_WithMovement_AppliesBobAndSurfaceBob()
        {
            _suitData.maxWalkSpeed = 5f;
            _suitData.bobTransitionSpeed = 10f;
            _suitData.bobVerticalAmplitude = 0.1f;
            _suitData.bobHorizontalAmplitude = 0.05f;
            // _suitData.surfaceBobFrequency = 2f;
            // _suitData.surfaceBobAmplitude = 0.1f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.ShallowWadeWalk,
                isGrounded = 1,
                hasMovementInput = 1,
                horizontalSpeed = 5f,
                immersionRatio = 0.5f,
                heavyCarryLoad = 0f
            };

            // Process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            Assert.AreNotEqual(Vector3.zero, output.localPositionOffset);
        }

        [Test]
        public void Process_ExosuitLocomotion_WithMovement_AppliesScaledHeadBob()
        {
            _suitData.maxWalkSpeed = 5f;
            _suitData.bobTransitionSpeed = 10f;
            _suitData.bobVerticalAmplitude = 0.1f;
            _suitData.bobHorizontalAmplitude = 0.05f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.ExosuitLocomotion,
                isGrounded = 1,
                hasMovementInput = 1,
                horizontalSpeed = 5f,
                heavyCarryLoad = 0f
            };

            // Process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            Assert.AreNotEqual(Vector3.zero, output.localPositionOffset);
        }

        [Test]
        public void Process_SurfaceSwim_AppliesSwimAndSurfaceBob()
        {
            _suitData.maxSwimSpeed = 5f;
            _suitData.swimBobTransitionSpeed = 10f;
            _suitData.swimBobVerticalAmplitude = 0.1f;
            // _suitData.surfaceBobFrequency = 2f;
            // _suitData.surfaceBobAmplitude = 0.1f;
            _suitData.swimBobRollAmplitude = 5f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.SurfaceSwim,
                hasMovementInput = 1,
                swimSpeed = 5f
            };

            // Process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            Assert.AreNotEqual(0f, output.localPositionOffset.y);
            Assert.AreNotEqual(0f, output.rollOffset);
        }

        [Test]
        public void Process_DeepSwim_AppliesDeepSwimEffects()
        {
            _suitData.maxSwimSpeed = 5f;
            _suitData.swimBobTransitionSpeed = 10f;
            _suitData.swimBobVerticalAmplitude = 0.1f;
            _suitData.swimBobRollAmplitude = 5f;
            _suitData.idleSwayAmplitudeY = 0.05f;

            var input = new CameraJuiceInput
            {
                deltaTime = 0.1f,
                locomotionMode = PlayerLocomotionMode.UnderwaterSwim,
                hasMovementInput = 1,
                swimSpeed = 5f,
                immersionRatio = 1.0f,
                depth = 50f
            };

            // Process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            Assert.AreNotEqual(0f, output.localPositionOffset.y);
            Assert.AreNotEqual(0f, output.rollOffset);
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
        public void RegisterSplash_UpdatesSplashDipFieldsAndRecoversOverTime()
        {
            _suitData.splashCameraDip = 0.5f;

            _processor.RegisterSplash(10f, _suitData);

            var input = new CameraJuiceInput { deltaTime = 0.1f };

            // First frame after splash should have high offsets
            var initialOutput = _processor.Process(in input, _suitData);

            // After 1 second of recovery, offsets should be smaller or zero
            input.deltaTime = 1.0f;
            var finalOutput = _processor.Process(in input, _suitData);

            Assert.Less(initialOutput.localPositionOffset.y, 0f); // initial dip should be negative Y
            Assert.Less(Mathf.Abs(finalOutput.localPositionOffset.y), Mathf.Abs(initialOutput.localPositionOffset.y));
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
                locomotionMode = PlayerLocomotionMode.UnderwaterSwim,
                hasMovementInput = 1,
                swimSpeed = 5f
            };

            // Need to process a few frames to let the spring/bob timer accumulate
            _processor.Process(in input, _suitData);
            var output = _processor.Process(in input, _suitData);

            // We expect some non-zero offsets from swim bob
            Assert.AreNotEqual(0f, output.localPositionOffset.y);
        }

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

        [Test]
        public void RegisterSonarPingImpulse_LowIntensity_ReturnsEarly()
        {
            // Arrange
            float initialShakeY = GetPrivateField<float>("_collisionShakeY");
            float initialShakeYVel = GetPrivateField<float>("_collisionShakeYVel");
            float initialShakePitch = GetPrivateField<float>("_collisionShakePitch");
            float initialShakePitchVel = GetPrivateField<float>("_collisionShakePitchVel");

            // Act
            _processor.RegisterSonarPingImpulse(0.00005f); // Less than 0.0001f

            // Assert
            Assert.AreEqual(initialShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(initialShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(initialShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(initialShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSonarPingImpulse_ValidIntensity_UpdatesShakeFields()
        {
            // Arrange
            float intensity = 0.5f;
            float expectedAmplitude = intensity * 0.0042f;
            float expectedShakeY = -expectedAmplitude; // math.min(0, -amplitude)
            float expectedShakeYVel = expectedAmplitude * 16f; // math.max(0, amplitude * 16f)
            float expectedShakePitch = -intensity * 0.12f; // math.min(0, -intensity * 0.12f)
            float expectedShakePitchVel = intensity * 0.95f; // math.max(0, intensity * 0.95f)

            // Act
            _processor.RegisterSonarPingImpulse(intensity);

            // Assert
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSonarPingImpulse_ClampsIntensityToOne()
        {
            // Arrange
            float intensity = 2.0f; // Above 1.0f, should be saturated to 1.0f
            float saturatedIntensity = 1.0f;
            float expectedAmplitude = saturatedIntensity * 0.0042f;
            float expectedShakeY = -expectedAmplitude;
            float expectedShakeYVel = expectedAmplitude * 16f;
            float expectedShakePitch = -saturatedIntensity * 0.12f;
            float expectedShakePitchVel = saturatedIntensity * 0.95f;

            // Act
            _processor.RegisterSonarPingImpulse(intensity);

            // Assert
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSonarPingImpulse_AccumulatesCorrectly_WhenCalledMultipleTimes()
        {
            // Arrange
            float firstIntensity = 0.2f;
            float secondIntensity = 0.8f;

            // Expected values after second call (0.8f intensity should dominate due to min/max)
            float expectedAmplitude = secondIntensity * 0.0042f;
            float expectedShakeY = -expectedAmplitude;
            float expectedShakeYVel = expectedAmplitude * 16f;
            float expectedShakePitch = -secondIntensity * 0.12f;
            float expectedShakePitchVel = secondIntensity * 0.95f;

            // Act
            _processor.RegisterSonarPingImpulse(firstIntensity);
            _processor.RegisterSonarPingImpulse(secondIntensity);

            // Assert
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));

            // Test that a smaller impulse doesn't overwrite a larger one
            _processor.RegisterSonarPingImpulse(0.1f);

            // Assert values remain the same as the largest impulse
            Assert.AreEqual(expectedShakeY, GetPrivateField<float>("_collisionShakeY"));
            Assert.AreEqual(expectedShakeYVel, GetPrivateField<float>("_collisionShakeYVel"));
            Assert.AreEqual(expectedShakePitch, GetPrivateField<float>("_collisionShakePitch"));
            Assert.AreEqual(expectedShakePitchVel, GetPrivateField<float>("_collisionShakePitchVel"));
        }

        [Test]
        public void RegisterSplash_WithNullSuit_DoesNothing()
        {
            // Arrange
            float initialDip = GetPrivateField<float>("_splashDipCurrent");
            float initialVelocity = GetPrivateField<float>("_splashDipVelocity");

            // Act
            _processor.RegisterSplash(1.0f, null);

            // Assert
            Assert.AreEqual(initialDip, GetPrivateField<float>("_splashDipCurrent"));
            Assert.AreEqual(initialVelocity, GetPrivateField<float>("_splashDipVelocity"));
        }

        [Test]
        public void RegisterSplash_WithValidSuit_CalculatesCorrectDipAndVelocity()
        {
            // Arrange
            float testIntensity = 2.5f;
            float expectedSplashCameraDip = 0.5f;
            _suitData.splashCameraDip = expectedSplashCameraDip;

            float expectedDip = -testIntensity * expectedSplashCameraDip;
            float expectedVelocity = -expectedDip * 2f;

            // Act
            _processor.RegisterSplash(testIntensity, _suitData);

            // Assert
            Assert.AreEqual(expectedDip, GetPrivateField<float>("_splashDipCurrent"));
            Assert.AreEqual(expectedVelocity, GetPrivateField<float>("_splashDipVelocity"));
        }

        private T GetPrivateField<T>(string fieldName)
        {
            var fieldInfo = typeof(CameraJuiceProcessor).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo == null)
                throw new System.Exception($"Field '{fieldName}' not found.");
            return (T)fieldInfo.GetValue(_processor);
        }
    }
}
