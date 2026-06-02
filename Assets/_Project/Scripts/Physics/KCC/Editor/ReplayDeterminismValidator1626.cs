#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Physics.KCC.Editor
{
    public struct ReplayDeterminismValidation1626Summary
    {
        public uint ErrorFlags;
        public uint FailureCode;
        public uint FirstFailureFrame;
        public uint StateHash;
        public float MaxDriftMillimeters;
    }

    public static class ReplayDeterminismValidator1626
    {
        private const int ReplayFrameCount = 12;
        private const BufferID ReplayFramesBuffer = BufferID.ShinobuInputReplayFrames;
        private const BufferID ReplayTelemetryBuffer = BufferID.ShinobuInputReplayTelemetry;
        private const BufferID ReplayResultsBuffer = BufferID.ShinobuInputReplayValidationResults;
        private const double InjectedAupDriftMeters = 0.000001d;
        private const float FuzzerEpsilonMeters = 0.0000005f;

        public static bool Run(bool injectAupDrift, out ReplayDeterminismValidation1626Summary summary)
        {
            summary = default;
            GlobalDataVault vault = GlobalDataVault.Create(32, 16L * 1024L * 1024L);
            try
            {
                VaultGenerationHandle<ReplayFrameDTO> replayHandle =
                    vault.EnsureGenerationHandle<ReplayFrameDTO>(
                        ReplayFramesBuffer,
                        ReplayFrameCount,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<MemoryStateTelemetryEntry> telemetryHandle =
                    vault.EnsureGenerationHandle<MemoryStateTelemetryEntry>(
                        ReplayTelemetryBuffer,
                        HydrodynamicKccRuntime.KccSmokeTelemetryFrames,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestResultDTO> resultHandle =
                    vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(
                        ReplayResultsBuffer,
                        1,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);

                if (!vault.TryResolveHandle(in replayHandle, out NativeArray<ReplayFrameDTO> frames) ||
                    !vault.TryResolveHandle(in telemetryHandle, out NativeArray<MemoryStateTelemetryEntry> telemetry) ||
                    !vault.TryResolveHandle(in resultHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results))
                {
                    summary.ErrorFlags = HydrodynamicKccRuntime.KccSmokeFailureAllocation;
                    return false;
                }

                KinematicStateDTO initialState = BuildInitialState();
                HydrodynamicKccTuningDTO tuning = BuildTuning();
                SeedReplayFrames(frames, initialState, tuning, injectAupDrift);
                results[0] = default;
                for (int i = 0; i < telemetry.Length; i++)
                    telemetry[i] = default;

                HydrodynamicKccRuntime.ValidateReplayDeterminismJob job = default;
                job.Frames = frames;
                job.Telemetry = telemetry;
                job.Results = results;
                job.InitialState = initialState;
                job.Tuning = tuning;
                job.ReplayEpsilonMeters = FuzzerEpsilonMeters;
                job.InjectVelocityErrorMetersPerSecond = 0f;
                job.FrameCount = ReplayFrameCount;
                JobHandle handle = job.Schedule();
                if (!DispatcherJobFence.TryComplete(ref handle, forceComplete: true))
                {
                    summary.ErrorFlags = HydrodynamicKccRuntime.KccSmokeFailureAllocation;
                    return false;
                }

                HydrodynamicKccRuntime.KccSmokeTestResultDTO result = results[0];
                summary.ErrorFlags = result.ErrorFlags;
                summary.FirstFailureFrame = result.FirstFailureFrame;
                summary.StateHash = result.StateHash;
                summary.MaxDriftMillimeters = result.MaxDriftMillimeters;
                summary.FailureCode = ResolveFirstFailureCode(telemetry);
                return injectAupDrift
                    ? (result.ErrorFlags & HydrodynamicKccRuntime.KccSmokeFailurePrecisionDrift) != 0u &&
                      summary.FailureCode == HydrodynamicKccRuntime.ReplayDeterminismFailureDrift
                    : result.ErrorFlags == HydrodynamicKccRuntime.KccSmokeFailureNone;
            }
            finally
            {
                vault.Dispose();
                H8Memory.Shutdown();
                NativeMemorySentinel.ResetForSubsystemReload();
            }
        }

        private static KinematicStateDTO BuildInitialState()
        {
            KinematicStateDTO state = default;
            state.AUP_Position.x = 1000.0d;
            state.AUP_Position.y = -250.0d;
            state.AUP_Position.z = 2000.0d;
            state.Velocity = float3.zero;
            state.AngularVelocity = float3.zero;
            state.Mass = 80f;
            state.Flags = 0u;
            state.DragCoefficient = 0f;
            return state;
        }

        private static HydrodynamicKccTuningDTO BuildTuning()
        {
            HydrodynamicKccTuningDTO tuning = default;
            tuning.BaseDrag = 0f;
            tuning.FluidDensity = 1027f;
            tuning.MaxSpeed = 2000f;
            tuning.GravityMultiplier = 0f;
            tuning.BuoyancyScalar = 0f;
            tuning.CapsuleRadius = 0.42f;
            tuning.CapsuleHeight = 1.8f;
            tuning.SkinWidth = 0.04f;
            tuning.GlobalQualityWeight = 0.5f;
            tuning.WaterSurfaceY = 0f;
            tuning.MockInputFrequency = 0f;
            tuning.MockInputAmplitude = 0f;
            tuning.VisualSyncSharpness = 1f;
            tuning.WakeThreshold = 1f;
            tuning.ProfileHash = 0x1626u;
            tuning.Flags = 0u;
            return tuning;
        }

        private static void SeedReplayFrames(
            NativeArray<ReplayFrameDTO> frames,
            KinematicStateDTO initialState,
            HydrodynamicKccTuningDTO tuning,
            bool injectAupDrift)
        {
            double3 aup = initialState.AUP_Position;
            float3 velocity = initialState.Velocity;
            float3 input = default;
            input.x = 0.25f;
            input.y = 0.05f;
            input.z = -0.125f;
            float dt = HydrodynamicKccRuntime.KccSmokeFixedDeltaTime;
            float drive = math.lerp(220f, 620f, tuning.GlobalQualityWeight);
            for (int i = 0; i < frames.Length; i++)
            {
                velocity += input * drive * dt;
                double3 delta = default;
                delta.x = velocity.x * dt;
                delta.y = velocity.y * dt;
                delta.z = velocity.z * dt;
                aup = HydrodynamicKccMath.QuantizeMillimeter(aup + delta);
                double3 recordedAup = aup;
                if (injectAupDrift && i == 5)
                    recordedAup.x += InjectedAupDriftMeters;
                uint flags = 0x1626u;
                uint stateHash = HydrodynamicKccMath.HashState(aup, velocity, (uint)i, flags);
                ReplayFrameDTO frame = default;
                frame.RecordedAup = recordedAup;
                frame.Tick = i;
                frame.InputMoveAxis = input;
                frame.Velocity = velocity;
                frame.DeltaTime = dt;
                frame.Frame = (uint)i;
                frame.InputFlags = flags;
                frame.StateHash = stateHash;
                frame.InputHash = HydrodynamicKccMath.HashState(recordedAup, input, (uint)i, flags);
                frames[i] = frame;
            }
        }

        private static uint ResolveFirstFailureCode(NativeArray<MemoryStateTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated)
                return 0u;

            for (int i = 0; i < telemetry.Length; i++)
            {
                uint failureCode = telemetry[i].FailureCode;
                if (failureCode != 0u)
                    return failureCode;
            }

            return 0u;
        }
    }
}
#endif
