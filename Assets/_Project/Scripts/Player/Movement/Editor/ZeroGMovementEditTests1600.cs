using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Player.Movement.Editor
{
    public sealed class ZeroGMovementEditTests1600
    {
        private const string RuntimeSourcePath = "Assets/_Project/Scripts/Player/Movement/ZeroGMovementRuntime.cs";
        private const string JobsSourcePath = "Assets/_Project/Scripts/Player/Movement/ZeroGMovementJobs.cs";

        [Test]
        public void Layouts_AreExplicitAndArm64Aligned()
        {
            Assert.IsTrue(ZeroGMovementLayoutVerifier.ValidateRuntimeLayouts());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGMovementStateDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGInputStateDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGTuningDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGSurfaceHitDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGSolverOutputDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGTelemetryEntry>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ZeroGTestResultDTO>() & 7);
        }

        [Test]
        public void Drift10K_NoForcePreservesVelocity()
        {
            NativeArray<ZeroGTestResultDTO> result = new NativeArray<ZeroGTestResultDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGDrift10KAssertionJob job = new ZeroGDrift10KAssertionJob
                {
                    Result = result,
                    InitialVelocity = new float3(1.25f, -0.5f, 2.75f),
                    DeltaTime = 0.0166666675f,
                    Iterations = 10000u
                };

                job.Run();
                ZeroGTestResultDTO dto = result[0];
                Assert.AreEqual(0u, dto.FaultMask);
                Assert.LessOrEqual(dto.MaxVelocityError, 0.00001f);
                Assert.LessOrEqual(dto.MaxPositionError, 0.0005f);
            }
            finally
            {
                result.Dispose();
            }
        }

        [Test]
        public void RotationFuzzer_KeepsQuaternionFiniteAndUnitLength()
        {
            NativeArray<ZeroGTestResultDTO> result = new NativeArray<ZeroGTestResultDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGRotationFuzzerJob job = new ZeroGRotationFuzzerJob
                {
                    Result = result,
                    Iterations = 10000u,
                    Seed = 0x1600u
                };

                job.Run();
                ZeroGTestResultDTO dto = result[0];
                Assert.AreEqual(0u, dto.FaultMask);
                Assert.LessOrEqual(dto.MaxOrientationError, 0.0001f);
            }
            finally
            {
                result.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_HorizonLockCorrectsExactInversion()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = double3.zero;
                stateDto.Orientation = quaternion.AxisAngle(new float3(1.0f, 0.0f, 0.0f), math.PI);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.HorizonLock;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 12.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 16.0f;
                tuningDto.PropellantDrainPerSecond = 0.035f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(20.0f, 20.0f, 20.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.05f,
                    Frame = 73u
                };

                job.Run();

                float3 correctedUp = math.rotate(state[0].Orientation, new float3(0.0f, 1.0f, 0.0f));
                Assert.Greater(correctedUp.y, -0.95f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.HorizonLocked);
                Assert.IsTrue(math.all(math.isfinite(state[0].Orientation.value)));
                Assert.AreEqual(1.0f, math.length(state[0].Orientation.value), 0.0001f);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_ReflectsAndDepenetratesFromOrbitWall()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = new double3(1.65, 0.0, 0.0);
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = new float3(4.0f, 0.0f, 0.0f);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 12.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(2.0f, 2.0f, 2.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 1u
                };

                job.Run();
                Assert.AreNotEqual(0u, output[0].Flags & ZeroGSolverOutputDTO.FlagCollision);
                Assert.Less(state[0].LinearVelocity.x, 0.0f);
                Assert.LessOrEqual((float)state[0].AUP_Position.x, 1.51f);
                Assert.Greater(surface[0].PenetrationMeters, 0.0f);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_DepenetratesMultiAxisCornerInSingleStep()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = new double3(1.7, 1.7, 0.0);
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = new float3(2.0f, 2.0f, 0.0f);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 12.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(2.0f, 2.0f, 2.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 24u
                };

                job.Run();
                float3 local = (float3)state[0].AUP_Position;
                Assert.LessOrEqual(local.x, 1.5f);
                Assert.LessOrEqual(local.y, 1.5f);
                Assert.Less(state[0].LinearVelocity.x, 0.0f);
                Assert.Less(state[0].LinearVelocity.y, 0.0f);
                Assert.AreEqual(-0.7071067f, surface[0].Normal.x, 0.0001f);
                Assert.AreEqual(-0.7071067f, surface[0].Normal.y, 0.0001f);
                Assert.Greater(surface[0].PenetrationMeters, 0.28f);
                Assert.AreNotEqual(0u, output[0].Flags & ZeroGSolverOutputDTO.FlagCollision);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_HonorsBoundedSubstepsForHighVelocityContact()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = new double3(1.2, 0.0, 0.0);
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = new float3(40.0f, 0.0f, 0.0f);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 50.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(2.0f, 2.0f, 2.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 4u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.05f,
                    Frame = 9u
                };

                job.Run();
                Assert.AreEqual(4u, tuning[0].MaxSubsteps);
                Assert.AreEqual(1, cursor[0]);
                Assert.AreNotEqual(0u, output[0].Flags & ZeroGSolverOutputDTO.FlagCollision);
                Assert.AreEqual(9u, output[0].Frame);
                Assert.AreEqual(9u, surface[0].Frame);
                Assert.IsTrue(math.all(math.isfinite(state[0].LinearVelocity)));
                Assert.IsTrue(math.all(math.isfinite((float3)state[0].AUP_Position)));
                Assert.LessOrEqual(math.abs((float)state[0].AUP_Position.x), 1.51f);
                Assert.Greater(surface[0].CollisionImpulse, 0.0f);
                Assert.Greater(surface[0].PenetrationMeters, 0.0f);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_PushAndGlideWorksNearSurfaceWithoutPenetration()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = new double3(1.2, 0.0, 0.0);
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = float3.zero;
                stateDto.AngularMomentum = float3.zero;
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.PushAndGlide;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 25.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(2.0f, 2.0f, 2.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 16u
                };

                job.Run();
                Assert.AreNotEqual(0u, surface[0].Flags & ZeroGSurfaceHitFlags.Valid);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.SurfaceContact);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.Depenetrated);
                Assert.AreEqual(-1.0f, surface[0].Normal.x, 0.00001f);
                Assert.AreEqual(0.3f, surface[0].DistanceMeters, 0.00001f);
                Assert.AreEqual(0.0f, surface[0].PenetrationMeters, 0.00001f);
                Assert.AreEqual(-3.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(3.0f, surface[0].CollisionImpulse, 0.00001f);
                Assert.AreNotEqual(0u, output[0].Flags & ZeroGSolverOutputDTO.FlagCollision);
                Assert.AreEqual(1, cursor[0]);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_HeldPushDoesNotRepeatUntilReleased()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = new double3(1.2, 0.0, 0.0);
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = float3.zero;
                stateDto.AngularMomentum = float3.zero;
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.PushAndGlide;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 12.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(2.0f, 2.0f, 2.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 17u
                };

                job.Run();
                Assert.AreEqual(-3.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
                Assert.AreNotEqual(0u, state[0].LastActionMask & ZeroGInputActions.PushAndGlide);

                job.Frame = 18u;
                job.Run();
                Assert.AreEqual(-3.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
                Assert.AreEqual(0u, output[0].Flags & ZeroGSolverOutputDTO.FlagCollision);
                Assert.AreEqual(0.0f, surface[0].CollisionImpulse, 0.00001f);
                Assert.AreNotEqual(0u, state[0].LastActionMask & ZeroGInputActions.PushAndGlide);

                inputDto.ActionMask = 0u;
                input[0] = inputDto;
                job.Frame = 19u;
                job.Run();
                Assert.AreEqual(0u, state[0].LastActionMask & ZeroGInputActions.PushAndGlide);

                tuningDto.SurfaceProbeRadiusMeters = 2.0f;
                tuning[0] = tuningDto;
                inputDto.ActionMask = ZeroGInputActions.PushAndGlide;
                input[0] = inputDto;
                job.Frame = 20u;
                job.Run();
                Assert.LessOrEqual(state[0].LinearVelocity.x, -5.9f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
                Assert.AreNotEqual(0u, state[0].LastActionMask & ZeroGInputActions.PushAndGlide);
                Assert.AreEqual(4, cursor[0]);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void SurfaceProbeQuality_IsContinuousNotBinaryTiered()
        {
            string source = ReadJobsSource();
            string collision = ExtractMethodBody(source, "ResolveAnalyticOrbitSurface");

            AssertHasToken(collision, "hit.QualityProbeWeight = quality", "ResolveAnalyticOrbitSurface");
            AssertNoToken(collision, "quality <", "ResolveAnalyticOrbitSurface");
            AssertNoToken(collision, "quality >", "ResolveAnalyticOrbitSurface");
            AssertNoToken(collision, "LowTierProbe", "ResolveAnalyticOrbitSurface");
        }

        [Test]
        public void IntegrationJob_ZeroPropellantRejectsThrusterAcceleration()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = double3.zero;
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = float3.zero;
                stateDto.AngularMomentum = float3.zero;
                stateDto.SuitPropellant01 = 0.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.LocalThrustAxis = new float3(1.0f, 0.0f, 0.0f);
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.Thruster;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 50.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 25.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(8.0f, 8.0f, 8.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 3u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 13u
                };

                job.Run();
                Assert.AreEqual(0.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreEqual(0.0f, math.length(state[0].LinearVelocity), 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PropellantDry);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0.0f, output[0].Propellant01, 0.00001f);
                Assert.AreEqual(0.0f, telemetry[0].Propellant01, 0.00001f);
                Assert.AreEqual(0u, output[0].FaultCode);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_BrakeAssistConsumesPropellantAndFailsDry()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = double3.zero;
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = new float3(4.0f, 0.0f, 0.0f);
                stateDto.AngularMomentum = new float3(1.0f, 0.0f, 0.0f);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.BrakeAssist;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 12.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(8.0f, 8.0f, 8.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 25u
                };

                job.Run();
                Assert.Less(state[0].LinearVelocity.x, 4.0f);
                Assert.Less(state[0].AngularMomentum.x, 1.0f);
                Assert.Less(state[0].SuitPropellant01, 1.0f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PropellantDry);

                stateDto.SuitPropellant01 = 0.0f;
                stateDto.LinearVelocity = new float3(4.0f, 0.0f, 0.0f);
                stateDto.AngularMomentum = new float3(1.0f, 0.0f, 0.0f);
                state[0] = stateDto;
                job.Frame = 26u;
                job.Run();

                Assert.AreEqual(4.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(1.0f, state[0].AngularMomentum.x, 0.00001f);
                Assert.AreEqual(0.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PropellantDry);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);

                tuningDto.PropellantDrainPerSecond = 0.0f;
                tuning[0] = tuningDto;
                stateDto.SuitPropellant01 = 0.0f;
                stateDto.LinearVelocity = new float3(4.0f, 0.0f, 0.0f);
                stateDto.AngularMomentum = new float3(1.0f, 0.0f, 0.0f);
                state[0] = stateDto;
                job.Frame = 27u;
                job.Run();

                Assert.Greater(tuning[0].PropellantDrainPerSecond, 0.0f);
                Assert.AreEqual(4.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(1.0f, state[0].AngularMomentum.x, 0.00001f);
                Assert.AreEqual(0.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PropellantDry);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_LastPropellantFractionScalesThrusterImpulse()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = double3.zero;
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = float3.zero;
                stateDto.AngularMomentum = float3.zero;
                stateDto.SuitPropellant01 = 0.01f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.LocalThrustAxis = new float3(1.0f, 0.0f, 0.0f);
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.Thruster;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 10.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 25.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(8.0f, 8.0f, 8.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 14u
                };

                job.Run();
                Assert.AreEqual(0.1f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(0.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PropellantDry);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0.0f, output[0].Propellant01, 0.00001f);
                Assert.AreEqual(0u, output[0].FaultCode);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void IntegrationJob_NonFiniteSourceSetsFaultWhileSanitizingState()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = double3.zero;
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = new float3(float.NaN, 3.0f, 0.0f);
                stateDto.AngularMomentum = float3.zero;
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.LocalThrustAxis = new float3(1.0f, 0.0f, 0.0f);
                inputDto.LocalAngularAxis = new float3(0.0f, 1.0f, 0.0f);
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.Thruster | ZeroGInputActions.HorizonLock;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 25.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(8.0f, 8.0f, 8.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 2u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 15u
                };

                job.Run();
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, state[0].FaultCode);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.NaNDetected);
                Assert.IsTrue(math.all(math.isfinite(state[0].LinearVelocity)));
                Assert.AreEqual(0.0f, math.length(state[0].LinearVelocity), 0.00001f);
                Assert.AreEqual(0.0f, math.length(state[0].AngularMomentum), 0.00001f);
                Assert.AreEqual(1.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.HorizonLocked);
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, output[0].FaultCode);
                Assert.AreNotEqual(0u, output[0].Flags & ZeroGSolverOutputDTO.FlagFault);
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, telemetry[0].FaultCode);
                Assert.AreNotEqual(0u, telemetry[0].Flags & ZeroGMovementStateFlags.NaNDetected);
                Assert.AreEqual(1, cursor[0]);

                stateDto.LinearVelocity = float3.zero;
                stateDto.SuitPropellant01 = float.NaN;
                state[0] = stateDto;
                inputDto.LocalThrustAxis = new float3(1.0f, 0.0f, 0.0f);
                inputDto.ActionMask = ZeroGInputActions.Thruster;
                input[0] = inputDto;
                job.Frame = 16u;
                job.Run();

                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, state[0].FaultCode);
                Assert.AreEqual(0.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreEqual(0.0f, math.length(state[0].LinearVelocity), 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.NaNDetected);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PropellantDry);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, output[0].FaultCode);
                Assert.AreEqual(0.0f, output[0].Propellant01, 0.00001f);

                stateDto.AUP_Position = new double3(1.0e40, 0.0, 0.0);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.LinearVelocity = new float3(2.0f, 0.0f, 0.0f);
                state[0] = stateDto;
                job.Frame = 17u;
                job.Run();

                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, state[0].FaultCode);
                Assert.AreEqual(stateDto.AUP_Position.x, state[0].AUP_Position.x);
                Assert.AreEqual(2.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.NaNDetected);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, output[0].FaultCode);

                stateDto.AUP_Position = new double3(1.0e20, 0.0, 0.0);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.LinearVelocity = new float3(2.0f, 0.0f, 0.0f);
                stateDto.AngularMomentum = float3.zero;
                stateDto.LastActionMask = 0u;
                state[0] = stateDto;
                inputDto.LocalThrustAxis = new float3(1.0f, 0.0f, 0.0f);
                inputDto.LocalAngularAxis = new float3(0.0f, 1.0f, 0.0f);
                inputDto.ActionMask = ZeroGInputActions.Thruster | ZeroGInputActions.HorizonLock | ZeroGInputActions.PushAndGlide;
                input[0] = inputDto;
                job.Frame = 18u;
                job.Run();

                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, state[0].FaultCode);
                Assert.AreEqual(stateDto.AUP_Position.x, state[0].AUP_Position.x);
                Assert.AreEqual(2.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(0.0f, math.length(output[0].LocalPosition), 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.NaNDetected);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.HorizonLocked);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, output[0].FaultCode);

                stateDto.AUP_Position = double3.zero;
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.LinearVelocity = new float3(2.0f, 0.0f, 0.0f);
                stateDto.AngularMomentum = float3.zero;
                state[0] = stateDto;
                inputDto.LocalThrustAxis = new float3(float.NaN, 0.0f, 0.0f);
                inputDto.LocalAngularAxis = new float3(0.0f, 1.0f, 0.0f);
                inputDto.ActionMask = ZeroGInputActions.Thruster | ZeroGInputActions.HorizonLock;
                input[0] = inputDto;
                job.Frame = 19u;
                job.Run();

                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, state[0].FaultCode);
                Assert.AreEqual(2.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreEqual(0.0f, math.length(state[0].AngularMomentum), 0.00001f);
                Assert.AreEqual(1.0f, state[0].SuitPropellant01, 0.00001f);
                Assert.AreEqual(1.0f, state[0].HorizonLockWeight, 0.00001f);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.NaNDetected);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.HorizonLocked);
                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, output[0].FaultCode);

                stateDto.AUP_Position = new double3(7.25, 0.0, 0.0);
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.LinearVelocity = float3.zero;
                stateDto.AngularMomentum = float3.zero;
                stateDto.LastActionMask = 0u;
                state[0] = stateDto;
                inputDto.LocalThrustAxis = new float3(float.NaN, 0.0f, 0.0f);
                inputDto.LocalAngularAxis = float3.zero;
                inputDto.ActionMask = ZeroGInputActions.PushAndGlide;
                input[0] = inputDto;
                job.Frame = 20u;
                job.Run();

                Assert.AreEqual(ZeroGMovementFaultCodes.NonFinite, state[0].FaultCode);
                Assert.AreEqual(0.0f, math.length(state[0].LinearVelocity), 0.00001f);
                Assert.AreEqual(0u, state[0].LastActionMask & ZeroGInputActions.PushAndGlide);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);

                inputDto.LocalThrustAxis = float3.zero;
                input[0] = inputDto;
                job.Frame = 21u;
                job.Run();

                Assert.AreEqual(ZeroGMovementFaultCodes.None, state[0].FaultCode);
                Assert.AreEqual(-3.0f, state[0].LinearVelocity.x, 0.00001f);
                Assert.AreNotEqual(0u, state[0].LastActionMask & ZeroGInputActions.PushAndGlide);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void DeterministicInputSignal_PacksSixDofAxesWithoutManagedProvider()
        {
            InputSignal signal = default;
            signal.MoveDelta = new float2(0.25f, -0.5f);
            signal.LookDelta = new float2(0.5f, -0.25f);
            signal.VerticalDelta = 0.75f;
            signal.ActionsBitmask =
                (uint)PlayerInputAction.Jump |
                (uint)PlayerInputAction.Sprint |
                (uint)PlayerInputAction.Interact;
            signal.Frame = 31u;
            signal.Sequence = 1u;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackDeterministicInputSignal(
                in signal,
                32u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO input));

            Assert.AreEqual(0.25f, input.LocalThrustAxis.x, 0.00001f);
            Assert.AreEqual(0.75f, input.LocalThrustAxis.y, 0.00001f);
            Assert.AreEqual(-0.5f, input.LocalThrustAxis.z, 0.00001f);
            Assert.AreEqual(0.02f, input.LocalAngularAxis.x, 0.00001f);
            Assert.AreEqual(0.04f, input.LocalAngularAxis.y, 0.00001f);
            Assert.AreEqual(0.0f, input.LocalAngularAxis.z, 0.00001f);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.PushAndGlide);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.HorizonLock);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.BrakeAssist);
            Assert.AreEqual(32u, input.Frame);
            Assert.AreEqual(32L, input.SimulationTick);
            Assert.AreNotEqual(0u, input.Flags & ZeroGMovementStateFlags.ExternalInput);
        }

        [Test]
        public void DeterministicInputSignal_RejectsNonFiniteAxes()
        {
            InputSignal signal = default;
            signal.MoveDelta = new float2(float.NaN, 0.5f);
            signal.LookDelta = new float2(0.25f, -0.25f);
            signal.VerticalDelta = 1.0f;
            signal.ActionsBitmask = (uint)PlayerInputAction.Sprint;
            signal.Frame = 35u;
            signal.Sequence = 7u;

            Assert.IsFalse(ZeroGMovementRuntime.TryPackDeterministicInputSignal(
                in signal,
                36u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO input));

            Assert.AreEqual(0u, input.ActionMask);
            Assert.AreEqual(0u, input.Flags);
        }

        [Test]
        public void DeterministicInputSignalFreshness_RejectsZeroFutureAndStaleFrames()
        {
            InputSignal signal = default;
            signal.Frame = 100u;
            signal.Sequence = 9u;

            Assert.IsTrue(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 100u));
            Assert.IsTrue(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 102u));
            Assert.IsFalse(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 103u));

            signal.Frame = 0u;
            Assert.IsFalse(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 100u));

            signal.Frame = 101u;
            Assert.IsFalse(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 100u));

            signal.Frame = 100u;
            signal.Sequence = 0u;
            Assert.IsFalse(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 100u));

            signal.Sequence = 9u;
            Assert.IsFalse(ZeroGMovementRuntime.IsFreshInputSignalForFrame(in signal, 0u));
        }

        [Test]
        public void ExternalAuthorityInput_DropsNonFinitePayloadWithoutPartialMovement()
        {
            ZeroGInputStateDTO corrupt = default;
            corrupt.LocalThrustAxis = new float3(float.NaN, 1.0f, 0.0f);
            corrupt.LocalAngularAxis = new float3(0.25f, 0.0f, float.PositiveInfinity);
            corrupt.ViewOrientation = quaternion.identity;
            corrupt.GlobalQualityWeight = 1.0f;
            corrupt.ActionMask =
                ZeroGInputActions.ExternalAuthority |
                ZeroGInputActions.Thruster |
                ZeroGInputActions.PushAndGlide |
                ZeroGInputActions.BrakeAssist;

            ZeroGInputStateDTO sanitized = ZeroGMovementRuntime.SanitizeExternalAuthorityInput(corrupt);

            Assert.AreEqual(0.0f, math.lengthsq(sanitized.LocalThrustAxis), 0.00001f);
            Assert.AreEqual(0.0f, math.lengthsq(sanitized.LocalAngularAxis), 0.00001f);
            Assert.AreEqual(0u, sanitized.ActionMask);
            Assert.AreNotEqual(0u, sanitized.Flags & ZeroGMovementStateFlags.SignalDrop);

            ZeroGInputStateDTO finite = default;
            finite.LocalThrustAxis = new float3(0.25f, 0.5f, 0.0f);
            finite.LocalAngularAxis = new float3(0.0f, 0.0f, 0.5f);
            finite.ViewOrientation = quaternion.identity;
            finite.GlobalQualityWeight = 0.75f;
            finite.ActionMask = ZeroGInputActions.ExternalAuthority | ZeroGInputActions.Thruster;

            ZeroGInputStateDTO finiteSanitized = ZeroGMovementRuntime.SanitizeExternalAuthorityInput(finite);

            Assert.Greater(math.lengthsq(finiteSanitized.LocalThrustAxis), 0.0f);
            Assert.Greater(math.lengthsq(finiteSanitized.LocalAngularAxis), 0.0f);
            Assert.AreNotEqual(0u, finiteSanitized.ActionMask & ZeroGInputActions.ExternalAuthority);
            Assert.AreNotEqual(0u, finiteSanitized.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreEqual(0u, finiteSanitized.Flags & ZeroGMovementStateFlags.SignalDrop);
        }

        [Test]
        public void ExternalAuthorityInput_MasksUnsupportedActionBits()
        {
            ZeroGInputStateDTO input = default;
            input.LocalThrustAxis = new float3(0.0f, 1.0f, 0.0f);
            input.LocalAngularAxis = new float3(0.0f, 0.0f, 1.0f);
            input.ViewOrientation = quaternion.identity;
            input.GlobalQualityWeight = 1.0f;
            input.ActionMask =
                ZeroGInputActions.ExternalAuthority |
                ZeroGInputActions.Thruster |
                ZeroGInputActions.PushAndGlide |
                0x00FF0000u;

            ZeroGInputStateDTO sanitized = ZeroGMovementRuntime.SanitizeExternalAuthorityInput(input);

            Assert.AreEqual(0u, sanitized.ActionMask & 0x00FF0000u);
            Assert.AreNotEqual(0u, sanitized.ActionMask & ZeroGInputActions.ExternalAuthority);
            Assert.AreNotEqual(0u, sanitized.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreNotEqual(0u, sanitized.ActionMask & ZeroGInputActions.PushAndGlide);
            Assert.AreEqual(0u, sanitized.Flags & ZeroGMovementStateFlags.SignalDrop);
        }

        [Test]
        public void IntegrationJob_MasksUnsupportedActionBitsFromLatch()
        {
            NativeArray<ZeroGMovementStateDTO> state = new NativeArray<ZeroGMovementStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGInputStateDTO> input = new NativeArray<ZeroGInputStateDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTuningDTO> tuning = new NativeArray<ZeroGTuningDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSurfaceHitDTO> surface = new NativeArray<ZeroGSurfaceHitDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGSolverOutputDTO> output = new NativeArray<ZeroGSolverOutputDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<ZeroGTelemetryEntry> telemetry = new NativeArray<ZeroGTelemetryEntry>(300, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            try
            {
                ZeroGMovementStateDTO stateDto = default;
                stateDto.AUP_Position = double3.zero;
                stateDto.Orientation = quaternion.identity;
                stateDto.LinearVelocity = float3.zero;
                stateDto.AngularMomentum = float3.zero;
                stateDto.SuitPropellant01 = 1.0f;
                stateDto.RadiusMeters = 0.5f;
                stateDto.Restitution = 0.6f;
                stateDto.HorizonLockWeight = 1.0f;
                stateDto.LastActionMask = 0x00FF0000u;
                state[0] = stateDto;

                ZeroGInputStateDTO inputDto = default;
                inputDto.ViewOrientation = quaternion.identity;
                inputDto.GlobalQualityWeight = 1.0f;
                inputDto.ActionMask = ZeroGInputActions.ExternalAuthority | 0x00FF0000u;
                input[0] = inputDto;

                ZeroGTuningDTO tuningDto = default;
                tuningDto.ThrustAcceleration = 6.0f;
                tuningDto.AngularAcceleration = 2.0f;
                tuningDto.MaxSpeedMetersPerSecond = 12.0f;
                tuningDto.MaxAngularRadiansPerSecond = 4.0f;
                tuningDto.RadiusMeters = 0.5f;
                tuningDto.Restitution = 0.6f;
                tuningDto.PushImpulseVelocityChange = 3.0f;
                tuningDto.DepenetrationSlopMeters = 0.01f;
                tuningDto.HorizonLockStrength = 2.0f;
                tuningDto.PropellantDrainPerSecond = 1.0f;
                tuningDto.GlobalQualityWeight = 1.0f;
                tuningDto.SurfaceProbeRadiusMeters = 0.5f;
                tuningDto.OrbitBoundsHalfExtents = new float3(2.0f, 2.0f, 2.0f);
                tuningDto.HorizonUp = new float3(0.0f, 1.0f, 0.0f);
                tuningDto.MaxSubsteps = 1u;
                tuningDto.CameraTraumaScale = 0.18f;
                tuningDto.HapticScale = 0.2f;
                tuning[0] = tuningDto;

                ZeroGPhysicsIntegrationJob job = new ZeroGPhysicsIntegrationJob
                {
                    State = state,
                    Input = input,
                    Tuning = tuning,
                    SurfaceHit = surface,
                    Output = output,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    CameraAup = double3.zero,
                    DeltaTime = 0.02f,
                    Frame = 61u
                };

                job.Run();

                Assert.AreEqual(0u, state[0].LastActionMask);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ThrusterActive);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.PushAndGlide);
                Assert.AreEqual(0u, state[0].Flags & ZeroGMovementStateFlags.HorizonLocked);
                Assert.AreNotEqual(0u, state[0].Flags & ZeroGMovementStateFlags.ExternalInput);
                Assert.AreEqual(ZeroGMovementFaultCodes.None, state[0].FaultCode);
            }
            finally
            {
                cursor.Dispose();
                telemetry.Dispose();
                output.Dispose();
                surface.Dispose();
                tuning.Dispose();
                input.Dispose();
                state.Dispose();
            }
        }

        [Test]
        public void DeterministicInputSignal_DashMapsToPushWithoutImplicitThruster()
        {
            InputSignal signal = default;
            signal.ActionsBitmask = (uint)PlayerInputAction.Dash;
            signal.Frame = 40u;
            signal.Sequence = 2u;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackDeterministicInputSignal(
                in signal,
                41u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO input));

            Assert.AreEqual(0.0f, math.length(input.LocalThrustAxis), 0.00001f);
            Assert.AreEqual(0u, input.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.PushAndGlide);
            Assert.AreEqual(41u, input.Frame);
        }

        [Test]
        public void DeterministicInputSignal_PrimaryAndSecondaryMapToOpposedRoll()
        {
            InputSignal primary = default;
            primary.ActionsBitmask = (uint)PlayerInputAction.PrimaryFire;
            primary.Frame = 51u;
            primary.Sequence = 3u;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackDeterministicInputSignal(
                in primary,
                52u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO primaryInput));

            InputSignal secondary = default;
            secondary.ActionsBitmask = (uint)PlayerInputAction.SecondaryFire;
            secondary.Frame = 52u;
            secondary.Sequence = 4u;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackDeterministicInputSignal(
                in secondary,
                53u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO secondaryInput));

            InputSignal both = default;
            both.ActionsBitmask = (uint)PlayerInputAction.PrimaryFire | (uint)PlayerInputAction.SecondaryFire;
            both.Frame = 53u;
            both.Sequence = 5u;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackDeterministicInputSignal(
                in both,
                54u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO bothInput));

            Assert.AreEqual(1.0f, primaryInput.LocalAngularAxis.z, 0.00001f);
            Assert.AreEqual(-1.0f, secondaryInput.LocalAngularAxis.z, 0.00001f);
            Assert.AreEqual(0.0f, bothInput.LocalAngularAxis.z, 0.00001f);
            Assert.AreEqual(0u, primaryInput.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreEqual(0u, secondaryInput.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreEqual(0u, bothInput.ActionMask & ZeroGInputActions.Thruster);
        }

        [Test]
        public void InputStateSignal_PacksSixDofAxesWithoutMappingToolFireToRoll()
        {
            ushort flags = 0;
            InputStateSignal signal = default;
            signal.State.Frame = 80u;
            signal.State.Sequence = 6u;
            signal.State.MoveX = InputState.QuantizeUnit(0.5f, ref flags);
            signal.State.MoveY = InputState.QuantizeUnit(-0.25f, ref flags);
            signal.State.LookX = InputState.QuantizeLook(0.25f, ref flags);
            signal.State.LookY = InputState.QuantizeLook(-0.5f, ref flags);
            signal.State.Vertical = InputState.QuantizeUnit(-0.75f, ref flags);
            signal.State.Flags = flags;
            signal.State.ButtonsBitmask =
                (uint)PlayerInputAction.PrimaryFire |
                (uint)PlayerInputAction.SecondaryFire |
                (uint)PlayerInputAction.Jump |
                (uint)PlayerInputAction.Interact |
                (uint)PlayerInputAction.Sprint;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackInputStateSignal(
                in signal,
                81u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO input));

            Assert.AreEqual(0.5f, input.LocalThrustAxis.x, 0.0001f);
            Assert.AreEqual(-0.75f, input.LocalThrustAxis.y, 0.0001f);
            Assert.AreEqual(-0.25f, input.LocalThrustAxis.z, 0.0001f);
            Assert.AreEqual(0.04f, input.LocalAngularAxis.x, 0.0001f);
            Assert.AreEqual(0.02f, input.LocalAngularAxis.y, 0.0001f);
            Assert.AreEqual(0.0f, input.LocalAngularAxis.z, 0.00001f);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.Thruster);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.PushAndGlide);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.HorizonLock);
            Assert.AreNotEqual(0u, input.ActionMask & ZeroGInputActions.BrakeAssist);
            Assert.AreEqual(81u, input.Frame);
            Assert.AreEqual(81L, input.SimulationTick);
            Assert.AreNotEqual(0u, input.Flags & ZeroGMovementStateFlags.ExternalInput);
        }

        [Test]
        public void InputStateSignalFreshness_RejectsZeroFutureAndStaleFrames()
        {
            InputStateSignal signal = default;
            signal.State.Frame = 100u;
            signal.State.Sequence = 2u;

            Assert.IsTrue(ZeroGMovementRuntime.TryPackInputStateSignal(
                in signal,
                102u,
                quaternion.identity,
                0.08f,
                out ZeroGInputStateDTO input));
            Assert.AreEqual(102u, input.Frame);

            Assert.IsFalse(ZeroGMovementRuntime.TryPackInputStateSignal(
                in signal,
                103u,
                quaternion.identity,
                0.08f,
                out _));

            signal.State.Frame = 0u;
            Assert.IsFalse(ZeroGMovementRuntime.TryPackInputStateSignal(
                in signal,
                100u,
                quaternion.identity,
                0.08f,
                out _));

            signal.State.Frame = 101u;
            Assert.IsFalse(ZeroGMovementRuntime.TryPackInputStateSignal(
                in signal,
                100u,
                quaternion.identity,
                0.08f,
                out _));

            signal.State.Frame = 100u;
            signal.State.Sequence = 0u;
            Assert.IsFalse(ZeroGMovementRuntime.TryPackInputStateSignal(
                in signal,
                100u,
                quaternion.identity,
                0.08f,
                out _));
        }

        [Test]
        public void InputStateSignalRoute_PrecedesLegacyInputSignalFallback()
        {
            string source = ReadRuntimeSource();
            string inputRoute = ExtractMethodBody(source, "TryBuildDeterministicSignalInput");
            string packRoute = ExtractMethodBody(source, "TryPackInputStateSignal");

            AssertTokenBefore(inputRoute, "TryBuildInputStateSignalInput", "CoreDeterminismSignals.TryGetLatestInput", "TryBuildDeterministicSignalInput");
            AssertHasToken(packRoute, "float3 localAngular = new float3(", "TryPackInputStateSignal");
            AssertHasToken(packRoute, "-look.y * angularScale", "TryPackInputStateSignal");
            AssertHasToken(packRoute, "look.x * angularScale", "TryPackInputStateSignal");
            AssertHasToken(packRoute, "0.0f);", "TryPackInputStateSignal");
            AssertTokenBefore(packRoute, "look.x * angularScale", "0.0f);", "TryPackInputStateSignal");
            AssertNoToken(packRoute, "PlayerInputAction.PrimaryFire", "TryPackInputStateSignal");
            AssertNoToken(packRoute, "PlayerInputAction.SecondaryFire", "TryPackInputStateSignal");
        }

        [Test]
        public void RuntimeHotPaths_DoNotUseColdLookupOrSceneQueries()
        {
            string source = ReadRuntimeSource();
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "FixedTick"), "FixedTick");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "PostFixedTick"), "PostFixedTick");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "LateFrameTick"), "LateFrameTick");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "WriteFrameInput"), "WriteFrameInput");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "BuildMockInput"), "BuildMockInput");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TryBuildDeterministicSignalInput"), "TryBuildDeterministicSignalInput");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TryBuildInputStateSignalInput"), "TryBuildInputStateSignalInput");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TryPackInputStateSignal"), "TryPackInputStateSignal");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TryPackDeterministicInputSignal"), "TryPackDeterministicInputSignal");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "SanitizeExternalAuthorityInput"), "SanitizeExternalAuthorityInput");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "InputDtoContainsNonFinite"), "InputDtoContainsNonFinite");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TelemetryEntryContainsNonFinite"), "TelemetryEntryContainsNonFinite");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TryResolveTelemetryLastIndex"), "TryResolveTelemetryLastIndex");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "StateSnapshotContainsNonFinite"), "StateSnapshotContainsNonFinite");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "OutputSnapshotContainsNonFinite"), "OutputSnapshotContainsNonFinite");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TuningSnapshotContainsNonFinite"), "TuningSnapshotContainsNonFinite");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "IsFreshInputSignal"), "IsFreshInputSignal");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "IsFreshInputSignalForFrame"), "IsFreshInputSignalForFrame");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "IsFreshInputStateSignalForFrame"), "IsFreshInputStateSignalForFrame");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "TryEnsureRuntimeOwnership"), "TryEnsureRuntimeOwnership");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "ScheduleSolver"), "ScheduleSolver");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "CompletePendingJob"), "CompletePendingJob");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "FlushVisualSyncReadback"), "FlushVisualSyncReadback");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "ApplyReadbackToTransform"), "ApplyReadbackToTransform");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "EmitReadbackSignals"), "EmitReadbackSignals");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "LocalDoubleFitsFloat3"), "LocalDoubleFitsFloat3");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "OutputSignalPayloadIsFinite"), "OutputSignalPayloadIsFinite");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "SanitizePresentationHapticScale"), "SanitizePresentationHapticScale");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "ResolveRuntimeOriginAupDouble"), "ResolveRuntimeOriginAupDouble");
            AssertNoForbiddenHotPathToken(ExtractMethodBody(source, "ResolveViewOrientation"), "ResolveViewOrientation");
        }

        [Test]
        public void ColdConfigurationApi_DoesNotLeakIntoHotPathsOrLegacyKcc()
        {
            string source = ReadRuntimeSource();
            string configure = ExtractMethodBody(source, "ConfigureCold");

            AssertHasToken(configure, "Application.isPlaying && _runtimeActive", "ConfigureCold");
            AssertHasToken(configure, "return false", "ConfigureCold");
            AssertHasToken(configure, "_authoritativeTransform = target", "ConfigureCold");
            AssertHasToken(configure, "_orientationSource = orientationSource != null ? orientationSource : target", "ConfigureCold");
            AssertHasToken(configure, "return true", "ConfigureCold");
            AssertNoForbiddenHotPathToken(configure, "ConfigureCold");

            AssertNoToken(source, "HectonPlayerMovement", "ZeroGMovementRuntime");
            AssertNoToken(source, "HydrodynamicKccRuntime", "ZeroGMovementRuntime");
            AssertNoToken(source, "PlayerKinematicsRuntime", "ZeroGMovementRuntime");

            AssertNoToken(ExtractMethodBody(source, "FixedTick"), "ConfigureCold", "FixedTick");
            AssertNoToken(ExtractMethodBody(source, "PostFixedTick"), "ConfigureCold", "PostFixedTick");
            AssertNoToken(ExtractMethodBody(source, "LateFrameTick"), "ConfigureCold", "LateFrameTick");
            AssertNoToken(ExtractMethodBody(source, "WriteFrameInput"), "ConfigureCold", "WriteFrameInput");
            AssertNoToken(ExtractMethodBody(source, "TryEnsureRuntimeOwnership"), "ConfigureCold", "TryEnsureRuntimeOwnership");
            AssertNoToken(ExtractMethodBody(source, "ScheduleSolver"), "ConfigureCold", "ScheduleSolver");
            AssertNoToken(ExtractMethodBody(source, "CompletePendingJob"), "ConfigureCold", "CompletePendingJob");
            AssertNoToken(ExtractMethodBody(source, "FlushVisualSyncReadback"), "ConfigureCold", "FlushVisualSyncReadback");
        }

        [Test]
        public void PresentationReadback_IsOnlyFlushedFromLateFrame()
        {
            string source = ReadRuntimeSource();
            string complete = ExtractMethodBody(source, "CompletePendingJob");
            AssertNoToken(complete, "ApplyReadbackToTransform", "CompletePendingJob");
            AssertNoToken(complete, "EmitReadbackSignals", "CompletePendingJob");

            string postFixed = ExtractMethodBody(source, "PostFixedTick");
            AssertNoToken(postFixed, "FlushVisualSyncReadback", "PostFixedTick");

            string lateFrame = ExtractMethodBody(source, "LateFrameTick");
            AssertHasToken(lateFrame, "FlushVisualSyncReadback", "LateFrameTick");
            Assert.AreEqual(1, CountOccurrences(source, "FlushVisualSyncReadback();"));

            string flush = ExtractMethodBody(source, "FlushVisualSyncReadback");
            AssertHasToken(flush, "ApplyReadbackToTransform", "FlushVisualSyncReadback");
            AssertHasToken(flush, "EmitReadbackSignals", "FlushVisualSyncReadback");
        }

        [Test]
        public void VisualSyncTransform_RejectsDoubleValuesOutsideUnityFloatRange()
        {
            string source = ReadRuntimeSource();
            string apply = ExtractMethodBody(source, "ApplyReadbackToTransform");
            string rangeGuard = ExtractMethodBody(source, "LocalDoubleFitsFloat3");

            AssertHasToken(apply, "LocalDoubleFitsFloat3(localDouble)", "ApplyReadbackToTransform");
            AssertTokenBefore(apply, "LocalDoubleFitsFloat3(localDouble)", "new Vector3((float)localDouble.x", "ApplyReadbackToTransform");
            AssertHasToken(rangeGuard, "math.isfinite(value)", "LocalDoubleFitsFloat3");
            AssertHasToken(rangeGuard, "math.abs(value)", "LocalDoubleFitsFloat3");
            AssertHasToken(rangeGuard, "float.MaxValue", "LocalDoubleFitsFloat3");
        }

        [Test]
        public void VisualSyncSignals_RejectNonFinitePayloadBeforePublishing()
        {
            string source = ReadRuntimeSource();
            string emit = ExtractMethodBody(source, "EmitReadbackSignals");
            string signalGuard = ExtractMethodBody(source, "OutputSignalPayloadIsFinite");
            string hapticScale = ExtractMethodBody(source, "SanitizePresentationHapticScale");

            AssertHasToken(emit, "OutputSignalPayloadIsFinite(in output)", "EmitReadbackSignals");
            AssertTokenBefore(emit, "OutputSignalPayloadIsFinite(in output)", "CameraJuiceSignals.TryPublishImpact", "EmitReadbackSignals");
            AssertTokenBefore(emit, "OutputSignalPayloadIsFinite(in output)", "SignalBus<HapticRequest>.TryPushTracked", "EmitReadbackSignals");
            AssertTokenBefore(emit, "math.isfinite(hapticSource)", "SignalBus<HapticRequest>.TryPushTracked", "EmitReadbackSignals");

            AssertHasToken(signalGuard, "math.all(math.isfinite(output.LocalPosition))", "OutputSignalPayloadIsFinite");
            AssertHasToken(signalGuard, "math.all(math.isfinite(output.CollisionNormal))", "OutputSignalPayloadIsFinite");
            AssertHasToken(signalGuard, "math.isfinite(output.CollisionImpulse)", "OutputSignalPayloadIsFinite");
            AssertHasToken(signalGuard, "math.isfinite(output.CameraTrauma01)", "OutputSignalPayloadIsFinite");

            AssertHasToken(hapticScale, "math.isfinite(value)", "SanitizePresentationHapticScale");
            AssertHasToken(hapticScale, "math.max(0.0f, value)", "SanitizePresentationHapticScale");
        }

        [Test]
        public void ReadbackAndTelemetryPatch_AreBoundToCompletedFrame()
        {
            string source = ReadRuntimeSource();
            string complete = ExtractMethodBody(source, "CompletePendingJob");
            AssertHasToken(complete, "uint completedFrame = _scheduledFrame", "CompletePendingJob");
            AssertHasToken(complete, "PatchLastTelemetryElapsed(completedFrame", "CompletePendingJob");
            AssertHasToken(complete, "TryReadHeldReadback(completedFrame", "CompletePendingJob");

            string readback = ExtractMethodBody(source, "TryReadHeldReadback");
            AssertHasToken(readback, "uint expectedFrame", "TryReadHeldReadback");
            AssertHasToken(readback, "state.Frame == 0u", "TryReadHeldReadback");
            AssertHasToken(readback, "output.Frame == 0u", "TryReadHeldReadback");
            AssertHasToken(readback, "state.StateHash == 0u", "TryReadHeldReadback");
            AssertHasToken(readback, "output.StateHash == 0u", "TryReadHeldReadback");
            AssertHasToken(readback, "StateSnapshotContainsNonFinite(in state)", "TryReadHeldReadback");
            AssertHasToken(readback, "OutputSnapshotContainsNonFinite(in output)", "TryReadHeldReadback");
            AssertHasToken(readback, "state.Frame != expectedFrame", "TryReadHeldReadback");
            AssertHasToken(readback, "output.Frame != expectedFrame", "TryReadHeldReadback");
            AssertHasToken(readback, "output.StateHash != state.StateHash", "TryReadHeldReadback");
            AssertTokenBefore(readback, "StateSnapshotContainsNonFinite(in state)", "return true", "TryReadHeldReadback");
            AssertTokenBefore(readback, "OutputSnapshotContainsNonFinite(in output)", "return true", "TryReadHeldReadback");

            string telemetry = ExtractMethodBody(source, "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "uint expectedFrame", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "!math.isfinite(elapsedMs)", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "elapsedMs < 0.0f", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "elapsedMs = 0.0f", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "budgetExceeded = false", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "math.min(elapsedMs, MaxRecordedSolverElapsedMs)", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "elapsedMs > JobBudgetExceededMs", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "TryResolveTelemetryLastIndex(cursorBuffer[0], telemetry.Length, out int index)", "PatchLastTelemetryElapsed");
            AssertHasToken(telemetry, "entry.Frame != expectedFrame", "PatchLastTelemetryElapsed");
            AssertTokenBefore(telemetry, "!math.isfinite(elapsedMs)", "entry.SolverComputeTimeMs", "PatchLastTelemetryElapsed");
            AssertTokenBefore(telemetry, "TryResolveTelemetryLastIndex(cursorBuffer[0], telemetry.Length, out int index)", "entry.SolverComputeTimeMs", "PatchLastTelemetryElapsed");
            AssertTokenBefore(telemetry, "entry.Frame != expectedFrame", "entry.SolverComputeTimeMs", "PatchLastTelemetryElapsed");

            string elapsed = ExtractMethodBody(source, "ResolveElapsedJobMs");
            AssertHasToken(elapsed, "long frequency = Stopwatch.Frequency", "ResolveElapsedJobMs");
            AssertHasToken(elapsed, "frequency <= 0L", "ResolveElapsedJobMs");
            AssertHasToken(elapsed, "double.IsNaN(elapsedMs)", "ResolveElapsedJobMs");
            AssertHasToken(elapsed, "double.IsInfinity(elapsedMs)", "ResolveElapsedJobMs");
            AssertHasToken(elapsed, "MaxRecordedSolverElapsedMs", "ResolveElapsedJobMs");
        }

        [Test]
        public void TryReadState_RejectsMismatchedStateOutputSnapshot()
        {
            string source = ReadRuntimeSource();
            string reader = ExtractMethodBody(source, "TryReadState");
            AssertHasToken(reader, "state.Frame != output.Frame", "TryReadState");
            AssertHasToken(reader, "output.StateHash != state.StateHash", "TryReadState");
            AssertTokenBefore(reader, "state.Frame != output.Frame", "return true", "TryReadState");
            AssertTokenBefore(reader, "output.StateHash != state.StateHash", "return true", "TryReadState");
            AssertHasToken(reader, "state = default", "TryReadState");
            AssertHasToken(reader, "output = default", "TryReadState");
            AssertHasToken(reader, "tuning = default", "TryReadState");
        }

        [Test]
        public void TryReadState_RejectsUninitializedOrNonFiniteSnapshot()
        {
            string source = ReadRuntimeSource();
            string reader = ExtractMethodBody(source, "TryReadState");
            string stateGuard = ExtractMethodBody(source, "StateSnapshotContainsNonFinite");
            string outputGuard = ExtractMethodBody(source, "OutputSnapshotContainsNonFinite");
            string tuningGuard = ExtractMethodBody(source, "TuningSnapshotContainsNonFinite");

            AssertHasToken(reader, "state.Frame == 0u", "TryReadState");
            AssertHasToken(reader, "state.StateHash == 0u", "TryReadState");
            AssertHasToken(reader, "output.Frame == 0u", "TryReadState");
            AssertHasToken(reader, "output.StateHash == 0u", "TryReadState");
            AssertHasToken(reader, "StateSnapshotContainsNonFinite(in state)", "TryReadState");
            AssertHasToken(reader, "OutputSnapshotContainsNonFinite(in output)", "TryReadState");
            AssertHasToken(reader, "TuningSnapshotContainsNonFinite(in tuning)", "TryReadState");
            AssertTokenBefore(reader, "state.Frame == 0u", "return true", "TryReadState");
            AssertTokenBefore(reader, "StateSnapshotContainsNonFinite(in state)", "return true", "TryReadState");
            AssertTokenBefore(reader, "TuningSnapshotContainsNonFinite(in tuning)", "return true", "TryReadState");

            AssertHasToken(stateGuard, "math.isfinite(state.AUP_Position)", "StateSnapshotContainsNonFinite");
            AssertHasToken(stateGuard, "math.isfinite(state.Orientation.value)", "StateSnapshotContainsNonFinite");
            AssertHasToken(stateGuard, "math.isfinite(state.LinearVelocity)", "StateSnapshotContainsNonFinite");
            AssertHasToken(outputGuard, "math.isfinite(output.LinearVelocity)", "OutputSnapshotContainsNonFinite");
            AssertHasToken(outputGuard, "math.isfinite(output.Propellant01)", "OutputSnapshotContainsNonFinite");
            AssertHasToken(tuningGuard, "math.isfinite(tuning.OrbitBoundsHalfExtents)", "TuningSnapshotContainsNonFinite");
            AssertHasToken(tuningGuard, "math.isfinite(tuning.HorizonUp)", "TuningSnapshotContainsNonFinite");
            AssertHasToken(tuningGuard, "tuning.MaxSubsteps == 0u", "TuningSnapshotContainsNonFinite");
        }

        [Test]
        public void DataVaultWriteLocks_AreSingleAndReleasedInFinally()
        {
            string source = ReadRuntimeSource();
            Assert.AreEqual(1, CountOccurrences(source, "TryAcquireWriteLock"));
            string externalInput = ExtractMethodBody(source, "TryWriteExternalInput");
            Assert.AreEqual(1, CountOccurrences(externalInput, "TryAcquireWriteLock"));
            AssertHasToken(externalInput, "finally", "TryWriteExternalInput");
            AssertHasToken(externalInput, "ReleaseWriteLock", "TryWriteExternalInput");

            AssertNoToken(ExtractMethodBody(source, "FixedTick"), "TryAcquireWriteLock", "FixedTick");
            AssertNoToken(ExtractMethodBody(source, "PostFixedTick"), "TryAcquireWriteLock", "PostFixedTick");
            AssertNoToken(ExtractMethodBody(source, "LateFrameTick"), "TryAcquireWriteLock", "LateFrameTick");
            AssertNoToken(ExtractMethodBody(source, "WriteFrameInput"), "TryAcquireWriteLock", "WriteFrameInput");
            AssertNoToken(ExtractMethodBody(source, "TryBuildDeterministicSignalInput"), "TryAcquireWriteLock", "TryBuildDeterministicSignalInput");
            AssertNoToken(ExtractMethodBody(source, "TryPackDeterministicInputSignal"), "TryAcquireWriteLock", "TryPackDeterministicInputSignal");
            AssertNoToken(ExtractMethodBody(source, "ScheduleSolver"), "TryAcquireWriteLock", "ScheduleSolver");
            AssertNoToken(ExtractMethodBody(source, "CompletePendingJob"), "TryAcquireWriteLock", "CompletePendingJob");
            AssertNoToken(ExtractMethodBody(source, "FlushVisualSyncReadback"), "TryAcquireWriteLock", "FlushVisualSyncReadback");
        }

        [Test]
        public void FrameInputMutationGuard_DoesNotCallExternalReaders()
        {
            string source = ReadRuntimeSource();
            string body = ExtractMethodBody(source, "WriteFrameInput");
            string guardedRegion = ExtractBetween(
                body,
                "TryAcquireMutationGuard(FrameInputGuardMask)",
                "ReleaseMutationGuard(FrameInputGuardMask)");

            AssertNoToken(guardedRegion, "BuildSerializedTuning", "FrameInputGuard");
            AssertNoToken(guardedRegion, "TryBuildDeterministicSignalInput", "FrameInputGuard");
            AssertNoToken(guardedRegion, "BuildMockInput", "FrameInputGuard");
            AssertNoToken(guardedRegion, "ResolveFrameQualityWeight", "FrameInputGuard");
            AssertNoToken(guardedRegion, "CoreDeterminismSignals", "FrameInputGuard");
            AssertNoToken(guardedRegion, "ResolveViewOrientation", "FrameInputGuard");
        }

        [Test]
        public void FixedTick_DoesNotScheduleSolverWhenFrameInputWriteFails()
        {
            string source = ReadRuntimeSource();
            string body = ExtractMethodBody(source, "FixedTick");

            AssertHasToken(body, "uint previousFrame = _scheduledFrame", "FixedTick");
            AssertHasToken(body, "TryEnsureRuntimeOwnership", "FixedTick");
            AssertTokenBefore(body, "TryEnsureRuntimeOwnership", "EnsureBuffers(false)", "FixedTick");
            AssertHasToken(body, "if (frame == previousFrame)", "FixedTick");
            AssertTokenBefore(body, "if (frame == previousFrame)", "ScheduleSolver", "FixedTick");
        }

        [Test]
        public void FixedTick_RetriesDeferredColdBootstrapBeforeScheduling()
        {
            string source = ReadRuntimeSource();
            string fixedTick = ExtractMethodBody(source, "FixedTick");
            string ownership = ExtractMethodBody(source, "TryEnsureRuntimeOwnership");

            AssertHasToken(fixedTick, "if (!TryEnsureRuntimeOwnership())", "FixedTick");
            AssertTokenBefore(fixedTick, "TryEnsureRuntimeOwnership", "WriteFrameInput", "FixedTick");
            AssertTokenBefore(fixedTick, "TryEnsureRuntimeOwnership", "ScheduleSolver", "FixedTick");

            AssertHasToken(ownership, "ReferenceEquals(s_activeRuntime, this)", "TryEnsureRuntimeOwnership");
            AssertHasToken(ownership, "s_activeRuntime != null", "TryEnsureRuntimeOwnership");
            AssertHasToken(ownership, "FinishNonOwnerReplacementTeardown", "TryEnsureRuntimeOwnership");
            AssertHasToken(ownership, "_dataVault == null", "TryEnsureRuntimeOwnership");
            AssertHasToken(ownership, "EnsureBuffers(true)", "TryEnsureRuntimeOwnership");
            AssertHasToken(ownership, "s_activeRuntime = this", "TryEnsureRuntimeOwnership");
            AssertNoToken(ownership, "TryRegisterFixed", "TryEnsureRuntimeOwnership");
            AssertNoToken(ownership, "TryAcquireWriteLock", "TryEnsureRuntimeOwnership");
        }

        [Test]
        public void DataVaultHotSwap_DefersBufferReleaseUntilActiveJobCompletes()
        {
            string source = ReadRuntimeSource();
            string hotSwap = ExtractMethodBody(source, "OnGlobalRegistryServiceReplaced");
            string deferredApply = ExtractMethodBody(source, "ApplyPendingDataVaultReplacementWhenSafe");
            string complete = ExtractMethodBody(source, "CompletePendingJob");
            string flush = ExtractMethodBody(source, "FlushVisualSyncReadback");

            AssertHasToken(hotSwap, "_pendingReplacementVault = currentService as IDataVault", "OnGlobalRegistryServiceReplaced");
            AssertHasToken(hotSwap, "ApplyPendingDataVaultReplacementWhenSafe", "OnGlobalRegistryServiceReplaced");
            AssertNoToken(hotSwap, "ReleaseVaultBuffers", "OnGlobalRegistryServiceReplaced");
            AssertHasToken(deferredApply, "_jobScheduled || _jobBuffersLocked", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertHasToken(deferredApply, "_hasPendingVisualSyncReadback", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertHasToken(deferredApply, "ReleaseVaultBuffers", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertHasToken(complete, "ApplyPendingDataVaultReplacementWhenSafe", "CompletePendingJob");
            AssertHasToken(flush, "ApplyPendingDataVaultReplacementWhenSafe", "FlushVisualSyncReadback");
        }

        [Test]
        public void PendingDataVaultReplacement_BlocksStaleExternalAccessAndScheduling()
        {
            string source = ReadRuntimeSource();
            string fixedTick = ExtractMethodBody(source, "FixedTick");
            string writeExternal = ExtractMethodBody(source, "TryWriteExternalInput");
            string readVault = ExtractMethodBody(source, "TryGetCachedVault");

            AssertHasToken(fixedTick, "_hasPendingReplacementVault", "FixedTick");
            AssertHasToken(writeExternal, "_hasPendingReplacementVault", "TryWriteExternalInput");
            AssertHasToken(readVault, "_hasPendingReplacementVault", "TryGetCachedVault");
            AssertTokenBefore(fixedTick, "_hasPendingReplacementVault", "ScheduleSolver", "FixedTick");
            AssertTokenBefore(writeExternal, "_hasPendingReplacementVault", "TryAcquireWriteLock", "TryWriteExternalInput");
        }

        [Test]
        public void DataVaultFence_BlocksExternalWriteAndCachedReads()
        {
            string source = ReadRuntimeSource();
            string writeExternal = ExtractMethodBody(source, "TryWriteExternalInput");
            string readVault = ExtractMethodBody(source, "TryGetCachedVault");

            AssertHasToken(writeExternal, "IsAllocationLocked", "TryWriteExternalInput");
            AssertHasToken(writeExternal, "IsCompactionFenceActive", "TryWriteExternalInput");
            AssertTokenBefore(writeExternal, "IsAllocationLocked", "TryAcquireWriteLock", "TryWriteExternalInput");
            AssertTokenBefore(writeExternal, "IsCompactionFenceActive", "TryAcquireWriteLock", "TryWriteExternalInput");

            AssertHasToken(readVault, "IDataVault cachedVault", "TryGetCachedVault");
            AssertHasToken(readVault, "IsAllocationLocked", "TryGetCachedVault");
            AssertHasToken(readVault, "IsCompactionFenceActive", "TryGetCachedVault");
            AssertTokenBefore(readVault, "IsAllocationLocked", "vault = cachedVault", "TryGetCachedVault");
            AssertTokenBefore(readVault, "IsCompactionFenceActive", "vault = cachedVault", "TryGetCachedVault");
        }

        [Test]
        public void DataVaultFence_BlocksFrameInputAndJobBufferGuards()
        {
            string source = ReadRuntimeSource();
            string writeFrameInput = ExtractMethodBody(source, "WriteFrameInput");
            string acquireJobBuffers = ExtractMethodBody(source, "TryAcquireJobBufferViews");
            string initializer = ExtractMethodBody(source, "GenerateEmergencyMockData");

            AssertHasToken(writeFrameInput, "IsAllocationLocked", "WriteFrameInput");
            AssertHasToken(writeFrameInput, "IsCompactionFenceActive", "WriteFrameInput");
            AssertTokenBefore(writeFrameInput, "IsAllocationLocked", "BuildSerializedTuning", "WriteFrameInput");
            AssertTokenBefore(writeFrameInput, "IsCompactionFenceActive", "BuildSerializedTuning", "WriteFrameInput");
            AssertTokenBefore(writeFrameInput, "IsAllocationLocked", "TryAcquireMutationGuard(FrameInputGuardMask)", "WriteFrameInput");
            AssertTokenBefore(writeFrameInput, "IsCompactionFenceActive", "TryAcquireMutationGuard(FrameInputGuardMask)", "WriteFrameInput");

            AssertHasToken(acquireJobBuffers, "IsAllocationLocked", "TryAcquireJobBufferViews");
            AssertHasToken(acquireJobBuffers, "IsCompactionFenceActive", "TryAcquireJobBufferViews");
            AssertTokenBefore(acquireJobBuffers, "IsAllocationLocked", "TryAcquireMutationGuard(JobGuardMask)", "TryAcquireJobBufferViews");
            AssertTokenBefore(acquireJobBuffers, "IsCompactionFenceActive", "TryAcquireMutationGuard(JobGuardMask)", "TryAcquireJobBufferViews");

            AssertTokenBefore(initializer, "IsAllocationLocked", "Transform target", "GenerateEmergencyMockData");
            AssertTokenBefore(initializer, "IsCompactionFenceActive", "Transform target", "GenerateEmergencyMockData");
        }

        [Test]
        public void PendingDisable_BlocksExternalAccessAndScheduling()
        {
            string source = ReadRuntimeSource();
            string onDisable = ExtractMethodBody(source, "OnDisable");
            string fixedTick = ExtractMethodBody(source, "FixedTick");
            string writeExternal = ExtractMethodBody(source, "TryWriteExternalInput");
            string readVault = ExtractMethodBody(source, "TryGetCachedVault");

            AssertHasToken(onDisable, "_hasPendingVisualSyncReadback", "OnDisable");
            AssertTokenBefore(onDisable, "_hasPendingVisualSyncReadback", "FinishDisableTeardown", "OnDisable");
            AssertHasToken(fixedTick, "_pendingDisableTeardown", "FixedTick");
            AssertHasToken(writeExternal, "_pendingDisableTeardown", "TryWriteExternalInput");
            AssertHasToken(readVault, "_pendingDisableTeardown", "TryGetCachedVault");
            AssertTokenBefore(fixedTick, "_pendingDisableTeardown", "ScheduleSolver", "FixedTick");
            AssertTokenBefore(writeExternal, "_pendingDisableTeardown", "TryAcquireWriteLock", "TryWriteExternalInput");
        }

        [Test]
        public void DataVaultBufferOpenFailures_RecordNumericFaultBeforeFailClosedReturn()
        {
            string source = ReadRuntimeSource();
            string writeFrameInput = ExtractMethodBody(source, "WriteFrameInput");
            string acquireJobBuffers = ExtractMethodBody(source, "TryAcquireJobBufferViews");

            string normalizedWriteFrameInput = writeFrameInput.Replace("\r\n", "\n");
            string normalizedAcquireJobBuffers = acquireJobBuffers.Replace("\r\n", "\n");
            AssertHasToken(
                normalizedWriteFrameInput,
                "RecordVaultAccessDenied();\n                    return _scheduledFrame;",
                "WriteFrameInput");
            AssertHasToken(
                normalizedAcquireJobBuffers,
                "RecordVaultAccessDenied();\n                    return false;",
                "TryAcquireJobBufferViews");
            AssertHasToken(acquireJobBuffers, "telemetry.Length != TelemetryCapacity", "TryAcquireJobBufferViews");
            AssertHasToken(acquireJobBuffers, "telemetryCursor.Length != 1", "TryAcquireJobBufferViews");
            AssertTokenBefore(acquireJobBuffers, "telemetry.Length != TelemetryCapacity", "_jobBuffersLocked = true", "TryAcquireJobBufferViews");
            AssertTokenBefore(acquireJobBuffers, "telemetryCursor.Length != 1", "_jobBuffersLocked = true", "TryAcquireJobBufferViews");
        }

        [Test]
        public void TelemetryBootstrap_DoesNotExposeUninitializedRingEntry()
        {
            string source = ReadRuntimeSource();
            string jobs = ReadJobsSource();
            string initializer = ExtractMethodBody(source, "GenerateEmergencyMockData");
            string reader = ExtractMethodBody(source, "TryReadLastTelemetry");
            string stateHash = ExtractMethodBody(jobs, "ComputeStateHash");

            AssertHasToken(initializer, "NativeArray<ZeroGTelemetryEntry> telemetryBuffer", "GenerateEmergencyMockData");
            AssertHasToken(initializer, "for (int i = 0; i < telemetryBuffer.Length; i++)", "GenerateEmergencyMockData");
            AssertHasToken(initializer, "telemetryBuffer[i] = default", "GenerateEmergencyMockData");
            AssertHasToken(stateHash, "return hash != 0u ? hash : 1u", "ComputeStateHash");
            AssertHasToken(reader, "TryResolveTelemetryLastIndex(cursor[0], telemetry.Length, out int index)", "TryReadLastTelemetry");
            AssertHasToken(reader, "ZeroGTelemetryEntry candidate = telemetry[index]", "TryReadLastTelemetry");
            AssertHasToken(reader, "candidate.Frame == 0u", "TryReadLastTelemetry");
            AssertHasToken(reader, "candidate.StateHash == 0u", "TryReadLastTelemetry");
            AssertHasToken(reader, "TelemetryEntryContainsNonFinite", "TryReadLastTelemetry");
            AssertTokenBefore(reader, "TryResolveTelemetryLastIndex(cursor[0], telemetry.Length, out int index)", "ZeroGTelemetryEntry candidate = telemetry[index]", "TryReadLastTelemetry");
            AssertTokenBefore(reader, "candidate.Frame == 0u", "entry = candidate", "TryReadLastTelemetry");
            AssertTokenBefore(reader, "candidate.StateHash == 0u", "entry = candidate", "TryReadLastTelemetry");
            AssertTokenBefore(reader, "TelemetryEntryContainsNonFinite", "entry = candidate", "TryReadLastTelemetry");

            string resolver = ExtractMethodBody(source, "TryResolveTelemetryLastIndex");
            AssertHasToken(resolver, "telemetryLength != TelemetryCapacity", "TryResolveTelemetryLastIndex");
            AssertHasToken(resolver, "cursor < 0", "TryResolveTelemetryLastIndex");
            AssertHasToken(resolver, "cursor >= telemetryLength", "TryResolveTelemetryLastIndex");
            AssertHasToken(resolver, "cursor == 0 ? telemetryLength - 1 : cursor - 1", "TryResolveTelemetryLastIndex");
        }

        [Test]
        public void EmergencyMockInitialization_BuildsExternalStateBeforeMutationGuard()
        {
            string source = ReadRuntimeSource();
            string initializer = ExtractMethodBody(source, "GenerateEmergencyMockData");

            AssertTokenBefore(initializer, "Transform target", "TryAcquireMutationGuard(InitializationGuardMask)", "GenerateEmergencyMockData");
            AssertTokenBefore(initializer, "ResolveRuntimeOriginAupDouble", "TryAcquireMutationGuard(InitializationGuardMask)", "GenerateEmergencyMockData");
            AssertTokenBefore(initializer, "BuildSerializedTuning", "TryAcquireMutationGuard(InitializationGuardMask)", "GenerateEmergencyMockData");
            string guardedRegion = ExtractBetween(
                initializer,
                "TryAcquireMutationGuard(InitializationGuardMask)",
                "ReleaseMutationGuard(InitializationGuardMask)");
            AssertNoToken(guardedRegion, "Transform target", "InitializationGuard");
            AssertNoToken(guardedRegion, "ResolveRuntimeOriginAupDouble", "InitializationGuard");
            AssertNoToken(guardedRegion, "BuildSerializedTuning", "InitializationGuard");
        }

        [Test]
        public void OnEnable_RejectsSecondRuntimeBeforeDispatcherRegistration()
        {
            string source = ReadRuntimeSource();
            string body = ExtractMethodBody(source, "OnEnable");

            AssertHasToken(body, "s_activeRuntime != null", "OnEnable");
            AssertHasToken(body, "!ReferenceEquals(s_activeRuntime, this)", "OnEnable");
            AssertHasToken(body, "_runtimeActive = false", "OnEnable");
            AssertTokenBefore(body, "s_activeRuntime != null", "TryRegisterHotSwapListener", "OnEnable");
            AssertTokenBefore(body, "s_activeRuntime != null", "TryRegisterFixed", "OnEnable");
            AssertTokenBefore(body, "s_activeRuntime != null", "TryRegisterPostFixed", "OnEnable");
            AssertTokenBefore(body, "s_activeRuntime != null", "TryRegisterLateFrame", "OnEnable");
        }

        [Test]
        public void DataVaultReplacement_DoesNotStealExistingRuntimeOwnership()
        {
            string source = ReadRuntimeSource();
            string body = ExtractMethodBody(source, "ApplyPendingDataVaultReplacementWhenSafe");

            AssertHasToken(body, "s_activeRuntime != null", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertHasToken(body, "!ReferenceEquals(s_activeRuntime, this)", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertHasToken(body, "FinishNonOwnerReplacementTeardown", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertTokenBefore(body, "s_activeRuntime != null", "ReleaseVaultBuffers", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertTokenBefore(body, "s_activeRuntime != null", "EnsureBuffers(true)", "ApplyPendingDataVaultReplacementWhenSafe");
            AssertTokenBefore(body, "s_activeRuntime != null", "s_activeRuntime = this", "ApplyPendingDataVaultReplacementWhenSafe");

            string teardown = ExtractMethodBody(source, "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "TryUnregisterPostFixed", "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "TryUnregisterFixed", "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "TryUnregisterLateFrame", "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "TryUnregisterHotSwapListener", "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "ClearVaultHandlesWithoutRelease", "FinishNonOwnerReplacementTeardown");
            AssertNoToken(teardown, "ReleaseVaultBuffers", "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "_dataVault = null", "FinishNonOwnerReplacementTeardown");
            AssertHasToken(teardown, "_runtimeActive = false", "FinishNonOwnerReplacementTeardown");

            string clearHandles = ExtractMethodBody(source, "ClearVaultHandlesWithoutRelease");
            AssertHasToken(clearHandles, "_stateHandle = default", "ClearVaultHandlesWithoutRelease");
            AssertHasToken(clearHandles, "_telemetryCursorHandle = default", "ClearVaultHandlesWithoutRelease");
            AssertHasToken(clearHandles, "_buffersInitialized = false", "ClearVaultHandlesWithoutRelease");
            AssertNoToken(clearHandles, "ReleaseBuffer", "ClearVaultHandlesWithoutRelease");
        }

        [Test]
        public void PhysicsIntegrationJob_HotPathDoesNotUseSceneQueriesOrImmediateFences()
        {
            string source = ReadJobsSource();
            string execute = ExtractMethodBody(source, "Execute");
            string nonFiniteGuard = ExtractMethodBody(source, "SourceContainsNonFinite");
            string collision = ExtractMethodBody(source, "ResolveAnalyticOrbitSurface");
            string clearance = ExtractMethodBody(source, "EvaluateClearance");

            AssertNoForbiddenJobToken(execute, "ZeroGPhysicsIntegrationJob.Execute");
            AssertNoForbiddenJobToken(nonFiniteGuard, "SourceContainsNonFinite");
            AssertNoForbiddenJobToken(collision, "ResolveAnalyticOrbitSurface");
            AssertNoForbiddenJobToken(clearance, "EvaluateClearance");
        }

        private static string ReadRuntimeSource()
        {
            Assert.IsTrue(File.Exists(RuntimeSourcePath), RuntimeSourcePath);
            return File.ReadAllText(RuntimeSourcePath);
        }

        private static string ReadJobsSource()
        {
            Assert.IsTrue(File.Exists(JobsSourcePath), JobsSourcePath);
            return File.ReadAllText(JobsSourcePath);
        }

        private static void AssertNoForbiddenHotPathToken(string body, string methodName)
        {
            AssertNoToken(body, "GlobalRegistry.Get<", methodName);
            AssertNoToken(body, "GetComponent", methodName);
            AssertNoToken(body, "FindObject", methodName);
            AssertNoToken(body, "Physics.", methodName);
            AssertNoToken(body, "Raycast", methodName);
            AssertNoToken(body, "SphereCast", methodName);
            AssertNoToken(body, "TryGetLatestCreated", methodName);
        }

        private static void AssertNoForbiddenJobToken(string body, string methodName)
        {
            AssertNoToken(body, "GlobalRegistry.Get<", methodName);
            AssertNoToken(body, "GetComponent", methodName);
            AssertNoToken(body, "FindObject", methodName);
            AssertNoToken(body, "Physics.", methodName);
            AssertNoToken(body, "Raycast", methodName);
            AssertNoToken(body, "SphereCast", methodName);
            AssertNoToken(body, ".Complete(", methodName);
            AssertNoToken(body, "Schedule(", methodName);
            AssertNoToken(body, "NativeList", methodName);
            AssertNoToken(body, "NativeHashMap", methodName);
            AssertNoToken(body, "List<", methodName);
            AssertNoToken(body, "Dictionary<", methodName);
            AssertNoToken(body, "foreach", methodName);
        }

        private static void AssertNoToken(string body, string token, string methodName)
        {
            Assert.IsFalse(body.Contains(token), methodName + " contains forbidden token " + token);
        }

        private static void AssertHasToken(string body, string token, string methodName)
        {
            Assert.IsTrue(body.Contains(token), methodName + " does not contain required token " + token);
        }

        private static void AssertTokenBefore(string body, string firstToken, string secondToken, string methodName)
        {
            int first = body.IndexOf(firstToken, System.StringComparison.Ordinal);
            int second = body.IndexOf(secondToken, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(first, 0, methodName + " missing " + firstToken);
            Assert.GreaterOrEqual(second, 0, methodName + " missing " + secondToken);
            Assert.Less(first, second, methodName + " token order invalid");
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(token, index, System.StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                index += token.Length;
            }

            return count;
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int signatureIndex = FindMethodSignatureIndex(source, methodName);

            int openBrace = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(openBrace, 0, methodName);

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(openBrace, i - openBrace + 1);
            }

            Assert.Fail(methodName);
            return string.Empty;
        }

        private static string ExtractBetween(string source, string startToken, string endToken)
        {
            int start = source.IndexOf(startToken, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, startToken);
            int end = source.IndexOf(endToken, start, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(end, 0, endToken);
            return source.Substring(start, end - start);
        }

        private static int FindMethodSignatureIndex(string source, string methodName)
        {
            int searchIndex = 0;
            while (searchIndex < source.Length)
            {
                int signatureIndex = source.IndexOf(methodName + "(", searchIndex, System.StringComparison.Ordinal);
                if (signatureIndex < 0)
                    break;

                int lineStart = source.LastIndexOf('\n', signatureIndex);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                int lineEnd = source.IndexOf('\n', signatureIndex);
                lineEnd = lineEnd < 0 ? source.Length : lineEnd;
                string line = source.Substring(lineStart, lineEnd - lineStart);
                bool looksLikeDeclaration =
                    line.Contains(" public ") ||
                    line.Contains(" private ") ||
                    line.Contains(" internal ") ||
                    line.Contains(" protected ") ||
                    line.TrimStart().StartsWith("public ", System.StringComparison.Ordinal) ||
                    line.TrimStart().StartsWith("private ", System.StringComparison.Ordinal) ||
                    line.TrimStart().StartsWith("internal ", System.StringComparison.Ordinal) ||
                    line.TrimStart().StartsWith("protected ", System.StringComparison.Ordinal);

                if (looksLikeDeclaration && !line.Contains(";"))
                    return signatureIndex;

                searchIndex = signatureIndex + methodName.Length + 1;
            }

            Assert.Fail(methodName);
            return -1;
        }
    }
}
