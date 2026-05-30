#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ForcePacketDTO = Hecton8.Physics.ForcePacketDTO;

namespace Hecton8.Physics.KCC.Editor
{
    public struct Shinobu355KccSmokeSummary
    {
        public uint ErrorFlags;
        public uint FailureCount;
        public long ManagedBytesAllocated;
        public float AverageMicrosecondsPerFrame;
        public double DriftErrorMillimeters;
        public uint RollbackPasses;
    }

    public static class Shinobu355KccSmokeRunner
    {
        private const BufferID SmokeStatesBuffer = (BufferID)71810;
        private const BufferID SmokePositionHistoryBuffer = (BufferID)71811;
        private const BufferID SmokeRollbackRingBuffer = (BufferID)71812;
        private const BufferID SmokeResultBuffer = (BufferID)71813;
        private const BufferID SmokeFailureBuffer = (BufferID)71814;
        private const BufferID SmokeTelemetryBuffer = (BufferID)71815;
        private const BufferID SmokeDriftBuffer = (BufferID)71816;
        private const BufferID SmokeDesyncSignalBuffer = (BufferID)71817;
        private const BufferID SmokeProfilesBuffer = (BufferID)71818;
        private const float PerformanceBudgetMicroseconds = 100f;
        private const long RetainedTelemetryVaultBytes = 1024L * 1024L;
        private const double MaxProfileAupMagnitudeMeters = 250000.0d;
        private const float MaxProfileSpeedMetersPerSecond = 2000f;
        private const float MaxProfileInputBiasMagnitude = 4f;
        public const float ConeFallProofSpeedMetersPerSecond = 100f;
        public const float ConeFallProofMinimumTuningSpeedMetersPerSecond = ConeFallProofSpeedMetersPerSecond;
        private const uint BlackBoxDumpVersion = 1u;

        private static readonly byte[] FailureCsvHeader =
        {
            102,114,97,109,101,44,101,110,116,105,116,121,44,102,108,97,103,115,44,97,117,112,95,120,44,97,117,112,95,121,44,97,117,112,95,122,44,
            118,101,108,95,120,44,118,101,108,95,121,44,118,101,108,95,122,44,115,100,102,95,109,44,115,112,101,101,100,95,109,112,115,44,104,97,115,104,10
        };

        private static GlobalDataVault s_lastVault;
        private static VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> s_lastTelemetryHandle;

        static Shinobu355KccSmokeRunner()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeTelemetryVault;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeTelemetryVault;
            EditorApplication.quitting -= DisposeTelemetryVault;
            EditorApplication.quitting += DisposeTelemetryVault;
        }

        public static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        public static bool ValidateApexConeFallContract(out float displacementPerFrameMeters, out float tuningMaxSpeedMetersPerSecond)
        {
            HydrodynamicKccTuningDTO tuning = BuildTuning();
            displacementPerFrameMeters = ConeFallProofSpeedMetersPerSecond * HydrodynamicKccRuntime.KccSmokeFixedDeltaTime;
            tuningMaxSpeedMetersPerSecond = tuning.MaxSpeed;
            return math.isfinite(displacementPerFrameMeters) &&
                   displacementPerFrameMeters > 1.6f &&
                   HydrodynamicKccRuntime.KccSmokeMaxSweepIterations == 8 &&
                   HydrodynamicKccRuntime.KccSmokeDefaultPhantomCount > 1 &&
                   tuningMaxSpeedMetersPerSecond >= ConeFallProofMinimumTuningSpeedMetersPerSecond;
        }

        public static bool Run(out Shinobu355KccSmokeSummary summary)
        {
            summary = default;
            HeadlessKccLayoutAssertions.AssertAll();
            RequireEqual(32, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(), "KccSmokeTestStateDTO size");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(), "KccSmokeTestStateDTO align");
            DisposeTelemetryVault();

            GlobalDataVault vault = GlobalDataVault.Create(128, 128L * 1024L * 1024L);
            try
            {
                int phantomCount = HydrodynamicKccRuntime.KccSmokeDefaultPhantomCount;
                int frameCount = HydrodynamicKccRuntime.KccSmokeDefaultFrameCount;
                int historyCount = phantomCount * frameCount;
                int rollbackCount = phantomCount * (HydrodynamicKccRuntime.KccSmokeRollbackWindowFrames + 1);

                VaultGenerationHandle<KinematicStateDTO> statesHandle = vault.EnsureGenerationHandle<KinematicStateDTO>(
                    BufferID.ShinobuHydroKccStates,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccInputDTO> inputsHandle = vault.EnsureGenerationHandle<HydrodynamicKccInputDTO>(
                    BufferID.ShinobuHydroKccInputs,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<float3> proposedHandle = vault.EnsureGenerationHandle<float3>(
                    BufferID.ShinobuHydroKccProposedVelocities,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccFaultFlagDTO> faultHandle = vault.EnsureGenerationHandle<HydrodynamicKccFaultFlagDTO>(
                    BufferID.ShinobuHydroKccFaultFlags,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<float> sdfHandle = vault.EnsureGenerationHandle<float>(
                    BufferID.ShinobuKccEnvironmentSdf,
                    HydrodynamicKccRuntime.KccSmokeSdfCellCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStateHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(
                    SmokeStatesBuffer,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<double3> historyHandle = vault.EnsureGenerationHandle<double3>(
                    SmokePositionHistoryBuffer,
                    historyCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<KinematicStateDTO> rollbackHandle = vault.EnsureGenerationHandle<KinematicStateDTO>(
                    SmokeRollbackRingBuffer,
                    rollbackCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestResultDTO> resultHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(
                    SmokeResultBuffer,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failureHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(
                    SmokeFailureBuffer,
                    HydrodynamicKccRuntime.KccSmokeMaxFailureRecords,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetryHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(
                    SmokeTelemetryBuffer,
                    HydrodynamicKccRuntime.KccSmokeTelemetryFrames,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> driftHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(
                    SmokeDriftBuffer,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<DesyncDetectedSignal> desyncHandle = vault.EnsureGenerationHandle<DesyncDetectedSignal>(
                    SmokeDesyncSignalBuffer,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeProfileDTO> profilesHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeProfileDTO>(
                    SmokeProfilesBuffer,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);

                Require(statesHandle.BufferID != rollbackHandle.BufferID, "SHINOBU_355 NoAlias violation: states and rollback lanes share a BufferID.");
                Require(vault.TryResolveHandle(in statesHandle, out NativeArray<KinematicStateDTO> states), "Failed to resolve KCC states.");
                Require(vault.TryResolveHandle(in inputsHandle, out NativeArray<HydrodynamicKccInputDTO> inputs), "Failed to resolve KCC inputs.");
                Require(vault.TryResolveHandle(in proposedHandle, out NativeArray<float3> proposed), "Failed to resolve KCC proposed velocities.");
                Require(vault.TryResolveHandle(in faultHandle, out NativeArray<HydrodynamicKccFaultFlagDTO> faults), "Failed to resolve KCC faults.");
                Require(vault.TryResolveHandle(in sdfHandle, out NativeArray<float> sdf), "Failed to resolve KCC SDF.");
                Require(vault.TryResolveHandle(in smokeStateHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStates), "Failed to resolve smoke states.");
                Require(vault.TryResolveHandle(in historyHandle, out NativeArray<double3> history), "Failed to resolve smoke history.");
                Require(vault.TryResolveHandle(in rollbackHandle, out NativeArray<KinematicStateDTO> rollback), "Failed to resolve smoke rollback ring.");
                Require(vault.TryResolveHandle(in resultHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results), "Failed to resolve smoke result.");
                Require(vault.TryResolveHandle(in failureHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failures), "Failed to resolve smoke failures.");
                Require(vault.TryResolveHandle(in telemetryHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry), "Failed to resolve smoke telemetry.");
                Require(vault.TryResolveHandle(in driftHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> drift), "Failed to resolve smoke drift probe.");
                Require(vault.TryResolveHandle(in desyncHandle, out NativeArray<DesyncDetectedSignal> desync), "Failed to resolve smoke desync lane.");
                Require(vault.TryResolveHandle(in profilesHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeProfileDTO> profiles), "Failed to resolve smoke profiles.");
                int safePhantomCount = ValidateSmokeBuffers(
                    phantomCount,
                    frameCount,
                    states,
                    inputs,
                    proposed,
                    faults,
                    smokeStates,
                    history,
                    rollback,
                    results,
                    failures,
                    telemetry,
                    drift,
                    desync,
                    profiles,
                    sdf);

                double3 sectorOrigin = new double3(99000.0d, -1500.0d, 99000.0d);
                HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO sdfInfo = BuildSdfInfo(sectorOrigin);
                HydrodynamicKccTuningDTO tuning = BuildTuning();
                int profileCount = TryLoadProfiles(profiles);

                JobHandle geometryHandle = new HydrodynamicKccRuntime.GenerateMockTestGeometryJob
                {
                    Sdf = sdf,
                    Info = sdfInfo
                }.Schedule();
                DispatcherJobFence.TryComplete(ref geometryHandle, forceComplete: true);

                Initialize(states, smokeStates, profiles, profileCount, safePhantomCount, sectorOrigin, tuning);
                SeedResultAndDrift(results, drift, sectorOrigin);
                WarmBurst(states, inputs, proposed, faults, sdf, history, rollback, smokeStates, results, failures, telemetry, drift, desync, sdfInfo, tuning, sectorOrigin, safePhantomCount);

                Initialize(states, smokeStates, profiles, profileCount, safePhantomCount, sectorOrigin, tuning);
                SeedResultAndDrift(results, drift, sectorOrigin);
                desync[0] = default;

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long startTicks = Stopwatch.GetTimestamp();
                JobHandle simulationHandle = new HydrodynamicKccRuntime.EvaluateHeadlessKccFrameLoopJob
                {
                    States = states,
                    Inputs = inputs,
                    ProposedVelocities = proposed,
                    FaultFlags = faults,
                    Sdf = sdf,
                    PositionHistory = history,
                    RollbackStateRing = rollback,
                    SmokeStates = smokeStates,
                    Results = results,
                    Failures = failures,
                    Telemetry = telemetry,
                    DriftProbe = drift,
                    MockDesyncSignals = desync,
                    SdfInfo = sdfInfo,
                    Tuning = tuning,
                    SectorOriginAup = sectorOrigin,
                    EntityCount = safePhantomCount,
                    FrameCount = frameCount,
                    SimulationTickDelta = HydrodynamicKccRuntime.KccSmokeFixedDeltaTime,
                    Seed = HydrodynamicKccRuntime.KccSmokeSourceHash
                }.Schedule();
                DispatcherJobFence.TryComplete(ref simulationHandle, forceComplete: true);
                JobHandle verifyHandle = new HydrodynamicKccRuntime.VerifyCollisionEscapeJob
                {
                    PositionHistory = history,
                    Sdf = sdf,
                    Results = results,
                    Failures = failures,
                    SdfInfo = sdfInfo,
                    EntityCount = safePhantomCount,
                    FrameCount = frameCount
                }.Schedule();
                DispatcherJobFence.TryComplete(ref verifyHandle, forceComplete: true);
                JobHandle precisionHandle = new HydrodynamicKccRuntime.AnalyzePrecisionDriftJob
                {
                    DriftProbe = drift,
                    Results = results,
                    FrameCount = frameCount
                }.Schedule();
                DispatcherJobFence.TryComplete(ref precisionHandle, forceComplete: true);

                long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                HydrodynamicKccRuntime.KccSmokeTestResultDTO result = results[0];
                result.AverageMicrosecondsPerFrame = (float)((double)elapsedTicks * 1000000.0d / Stopwatch.Frequency / frameCount);
                if (result.AverageMicrosecondsPerFrame > PerformanceBudgetMicroseconds)
                    result.ErrorFlags |= HydrodynamicKccRuntime.KccSmokeFailurePerformance;
                long allocatedBytes = allocatedAfter - allocatedBefore;
                if (allocatedBytes != 0L)
                    result.ErrorFlags |= HydrodynamicKccRuntime.KccSmokeFailureAllocation;
                results[0] = result;

                summary = new Shinobu355KccSmokeSummary
                {
                    ErrorFlags = result.ErrorFlags,
                    FailureCount = result.FailureCount,
                    ManagedBytesAllocated = allocatedBytes,
                    AverageMicrosecondsPerFrame = result.AverageMicrosecondsPerFrame,
                    DriftErrorMillimeters = result.DriftErrorMillimeters,
                    RollbackPasses = result.SuccessfulRollbackCount
                };

                StampMeasuredTelemetryMicroseconds(telemetry, result.AverageMicrosecondsPerFrame);
                RetainTelemetrySnapshot(telemetry);
                WriteReport(summary, result, "UNITY_EDITOR_JOB_RUN_PENDING_EXTERNAL_COMPILE_WALL", true);
                if (result.ErrorFlags == HydrodynamicKccRuntime.KccSmokeFailureNone)
                {
                    HeadlessKccFailureGizmo.ClearFailure();
                    return true;
                }

                int failureCount = (int)math.min(result.FailureCount, (uint)failures.Length);
                if (failureCount > 0)
                {
                    HydrodynamicKccRuntime.KccSmokeFailureRecordDTO failure = failures[0];
                    HeadlessKccFailureGizmo.SetFailure(failure.Aup, failure.PreviousAup, failure.Velocity, failure.InputVector);
                }
                else
                {
                    HeadlessKccFailureGizmo.SetFailure(result.FirstFailureAup);
                }

                WriteFailureCsv(failures, failureCount);
                WriteBlackBoxDump(telemetry, result);
                return false;
            }
            finally
            {
                vault.Dispose();
            }
        }

        public static bool TryGetLastTelemetry(out NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            return s_lastVault != null &&
                   s_lastTelemetryHandle.BufferID != 0u &&
                   s_lastVault.TryReadOnlyHandle(in s_lastTelemetryHandle, out telemetry) &&
                   telemetry.Length > 0;
        }

        public static ScheduledRun StartScheduledRun()
        {
            HeadlessKccLayoutAssertions.AssertAll();
            RequireEqual(32, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(), "KccSmokeTestStateDTO size");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(), "KccSmokeTestStateDTO align");
            DisposeTelemetryVault();

            GlobalDataVault vault = GlobalDataVault.Create(128, 128L * 1024L * 1024L);
            try
            {
                int phantomCount = HydrodynamicKccRuntime.KccSmokeDefaultPhantomCount;
                int frameCount = HydrodynamicKccRuntime.KccSmokeDefaultFrameCount;
                int historyCount = phantomCount * frameCount;
                int rollbackCount = phantomCount * (HydrodynamicKccRuntime.KccSmokeRollbackWindowFrames + 1);

                VaultGenerationHandle<KinematicStateDTO> statesHandle = vault.EnsureGenerationHandle<KinematicStateDTO>(
                    BufferID.ShinobuHydroKccStates,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccInputDTO> inputsHandle = vault.EnsureGenerationHandle<HydrodynamicKccInputDTO>(
                    BufferID.ShinobuHydroKccInputs,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<float3> proposedHandle = vault.EnsureGenerationHandle<float3>(
                    BufferID.ShinobuHydroKccProposedVelocities,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccFaultFlagDTO> faultHandle = vault.EnsureGenerationHandle<HydrodynamicKccFaultFlagDTO>(
                    BufferID.ShinobuHydroKccFaultFlags,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<float> sdfHandle = vault.EnsureGenerationHandle<float>(
                    BufferID.ShinobuKccEnvironmentSdf,
                    HydrodynamicKccRuntime.KccSmokeSdfCellCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStateHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(
                    SmokeStatesBuffer,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<double3> historyHandle = vault.EnsureGenerationHandle<double3>(
                    SmokePositionHistoryBuffer,
                    historyCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<KinematicStateDTO> rollbackHandle = vault.EnsureGenerationHandle<KinematicStateDTO>(
                    SmokeRollbackRingBuffer,
                    rollbackCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestResultDTO> resultHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(
                    SmokeResultBuffer,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failureHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(
                    SmokeFailureBuffer,
                    HydrodynamicKccRuntime.KccSmokeMaxFailureRecords,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetryHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(
                    SmokeTelemetryBuffer,
                    HydrodynamicKccRuntime.KccSmokeTelemetryFrames,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> driftHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(
                    SmokeDriftBuffer,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<DesyncDetectedSignal> desyncHandle = vault.EnsureGenerationHandle<DesyncDetectedSignal>(
                    SmokeDesyncSignalBuffer,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeProfileDTO> profilesHandle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeProfileDTO>(
                    SmokeProfilesBuffer,
                    phantomCount,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);

                Require(statesHandle.BufferID != rollbackHandle.BufferID, "SHINOBU_355 NoAlias violation: states and rollback lanes share a BufferID.");
                Require(vault.TryResolveHandle(in statesHandle, out NativeArray<KinematicStateDTO> states), "Failed to resolve KCC states.");
                Require(vault.TryResolveHandle(in inputsHandle, out NativeArray<HydrodynamicKccInputDTO> inputs), "Failed to resolve KCC inputs.");
                Require(vault.TryResolveHandle(in proposedHandle, out NativeArray<float3> proposed), "Failed to resolve KCC proposed velocities.");
                Require(vault.TryResolveHandle(in faultHandle, out NativeArray<HydrodynamicKccFaultFlagDTO> faults), "Failed to resolve KCC faults.");
                Require(vault.TryResolveHandle(in sdfHandle, out NativeArray<float> sdf), "Failed to resolve KCC SDF.");
                Require(vault.TryResolveHandle(in smokeStateHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStates), "Failed to resolve smoke states.");
                Require(vault.TryResolveHandle(in historyHandle, out NativeArray<double3> history), "Failed to resolve smoke history.");
                Require(vault.TryResolveHandle(in rollbackHandle, out NativeArray<KinematicStateDTO> rollback), "Failed to resolve smoke rollback ring.");
                Require(vault.TryResolveHandle(in resultHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results), "Failed to resolve smoke result.");
                Require(vault.TryResolveHandle(in failureHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failures), "Failed to resolve smoke failures.");
                Require(vault.TryResolveHandle(in telemetryHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry), "Failed to resolve smoke telemetry.");
                Require(vault.TryResolveHandle(in driftHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> drift), "Failed to resolve smoke drift probe.");
                Require(vault.TryResolveHandle(in desyncHandle, out NativeArray<DesyncDetectedSignal> desync), "Failed to resolve smoke desync lane.");
                Require(vault.TryResolveHandle(in profilesHandle, out NativeArray<HydrodynamicKccRuntime.KccSmokeProfileDTO> profiles), "Failed to resolve smoke profiles.");
                int safePhantomCount = ValidateSmokeBuffers(
                    phantomCount,
                    frameCount,
                    states,
                    inputs,
                    proposed,
                    faults,
                    smokeStates,
                    history,
                    rollback,
                    results,
                    failures,
                    telemetry,
                    drift,
                    desync,
                    profiles,
                    sdf);

                double3 sectorOrigin = new double3(99000.0d, -1500.0d, 99000.0d);
                HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO sdfInfo = BuildSdfInfo(sectorOrigin);
                HydrodynamicKccTuningDTO tuning = BuildTuning();
                int profileCount = TryLoadProfiles(profiles);
                SeedResultAndDrift(results, drift, sectorOrigin);
                desync[0] = default;

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long startTicks = Stopwatch.GetTimestamp();
                JobHandle geometryHandle = new HydrodynamicKccRuntime.GenerateMockTestGeometryJob
                {
                    Sdf = sdf,
                    Info = sdfInfo
                }.Schedule();
                JobHandle initHandle = new HydrodynamicKccRuntime.InitializeSmokePhantomsJob
                {
                    States = states,
                    SmokeStates = smokeStates,
                    Profiles = profiles,
                    ProfileCount = profileCount,
                    SectorOriginAup = sectorOrigin,
                    Tuning = tuning
                }.Schedule(safePhantomCount, 32);
                JobHandle setupHandle = JobHandle.CombineDependencies(geometryHandle, initHandle);
                JobHandle simulationHandle = new HydrodynamicKccRuntime.EvaluateHeadlessKccFrameLoopJob
                {
                    States = states,
                    Inputs = inputs,
                    ProposedVelocities = proposed,
                    FaultFlags = faults,
                    Sdf = sdf,
                    PositionHistory = history,
                    RollbackStateRing = rollback,
                    SmokeStates = smokeStates,
                    Results = results,
                    Failures = failures,
                    Telemetry = telemetry,
                    DriftProbe = drift,
                    MockDesyncSignals = desync,
                    SdfInfo = sdfInfo,
                    Tuning = tuning,
                    SectorOriginAup = sectorOrigin,
                    EntityCount = safePhantomCount,
                    FrameCount = frameCount,
                    SimulationTickDelta = HydrodynamicKccRuntime.KccSmokeFixedDeltaTime,
                    Seed = HydrodynamicKccRuntime.KccSmokeSourceHash
                }.Schedule(setupHandle);
                JobHandle verifyHandle = new HydrodynamicKccRuntime.VerifyCollisionEscapeJob
                {
                    PositionHistory = history,
                    Sdf = sdf,
                    Results = results,
                    Failures = failures,
                    SdfInfo = sdfInfo,
                    EntityCount = safePhantomCount,
                    FrameCount = frameCount
                }.Schedule(simulationHandle);
                JobHandle finalHandle = new HydrodynamicKccRuntime.AnalyzePrecisionDriftJob
                {
                    DriftProbe = drift,
                    Results = results,
                    FrameCount = frameCount
                }.Schedule(verifyHandle);

                return new ScheduledRun(vault, results, failures, telemetry, finalHandle, startTicks, allocatedBefore, frameCount);
            }
            catch
            {
                vault.Dispose();
                throw;
            }
        }

        public sealed class ScheduledRun : IDisposable
        {
            private GlobalDataVault _vault;
            private NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> _results;
            private NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> _failures;
            private NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> _telemetry;
            private JobHandle _finalHandle;
            private long _startTicks;
            private long _allocatedBefore;
            private int _frameCount;
            private bool _finalizeAttempted;
            private bool _disposed;

            public bool IsDone;
            public bool Passed;
            public float Progress;
            public Shinobu355KccSmokeSummary Summary;

            internal ScheduledRun(
                GlobalDataVault vault,
                NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results,
                NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failures,
                NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry,
                JobHandle finalHandle,
                long startTicks,
                long allocatedBefore,
                int frameCount)
            {
                _vault = vault;
                _results = results;
                _failures = failures;
                _telemetry = telemetry;
                _finalHandle = finalHandle;
                _startTicks = startTicks;
                _allocatedBefore = allocatedBefore;
                _frameCount = frameCount;
                Progress = 0.15f;
            }

            public bool Poll()
            {
                if (_disposed || IsDone)
                    return true;

                long elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
                float elapsedSeconds = (float)((double)elapsedTicks / Stopwatch.Frequency);
                Progress = math.min(0.92f, 0.15f + elapsedSeconds * 0.18f);
                if (!_finalHandle.IsCompleted)
                    return false;

                DispatcherJobFence.TryComplete(ref _finalHandle, forceComplete: true);
                FinalizeCompletedRun(elapsedTicks, GC.GetAllocatedBytesForCurrentThread() - _allocatedBefore);
                return true;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                if (!IsDone && !_finalizeAttempted)
                {
                    DispatcherJobFence.TryComplete(ref _finalHandle, forceComplete: true);
                    long elapsedTicks = Stopwatch.GetTimestamp() - _startTicks;
                    FinalizeCompletedRun(elapsedTicks, GC.GetAllocatedBytesForCurrentThread() - _allocatedBefore);
                }

                if (_vault != null)
                    _vault.Dispose();

                _vault = null;
                _disposed = true;
            }

            private void FinalizeCompletedRun(long elapsedTicks, long managedBytesAllocated)
            {
                if (IsDone || _finalizeAttempted)
                    return;
                _finalizeAttempted = true;

                HydrodynamicKccRuntime.KccSmokeTestResultDTO result = _results[0];
                result.AverageMicrosecondsPerFrame = (float)((double)elapsedTicks * 1000000.0d / Stopwatch.Frequency / _frameCount);
                _results[0] = result;

                Summary = new Shinobu355KccSmokeSummary
                {
                    ErrorFlags = result.ErrorFlags,
                    FailureCount = result.FailureCount,
                    ManagedBytesAllocated = managedBytesAllocated,
                    AverageMicrosecondsPerFrame = result.AverageMicrosecondsPerFrame,
                    DriftErrorMillimeters = result.DriftErrorMillimeters,
                    RollbackPasses = result.SuccessfulRollbackCount
                };

                StampMeasuredTelemetryMicroseconds(_telemetry, result.AverageMicrosecondsPerFrame);
                RetainTelemetrySnapshot(_telemetry);
                WriteReport(Summary, result, "UNITY_EDITOR_SCHEDULED_JOB_PENDING_IMPORT_PROOF", false);
                Passed = result.ErrorFlags == HydrodynamicKccRuntime.KccSmokeFailureNone;
                if (Passed)
                {
                    HeadlessKccFailureGizmo.ClearFailure();
                }
                else
                {
                    int failureCount = (int)math.min(result.FailureCount, (uint)_failures.Length);
                    if (failureCount > 0)
                    {
                        HydrodynamicKccRuntime.KccSmokeFailureRecordDTO failure = _failures[0];
                        HeadlessKccFailureGizmo.SetFailure(failure.Aup, failure.PreviousAup, failure.Velocity, failure.InputVector);
                    }
                    else
                    {
                        HeadlessKccFailureGizmo.SetFailure(result.FirstFailureAup);
                    }

                    WriteFailureCsv(_failures, failureCount);
                    WriteBlackBoxDump(_telemetry, result);
                }

                Progress = 1f;
                IsDone = true;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new FatalArchitectureException(message);
        }

        private static void RequireEqual(int expected, int actual, string label)
        {
            if (expected != actual)
                throw new FatalArchitectureException(label + " expected=" + expected + " actual=" + actual);
        }

        private static void DisposeTelemetryVault()
        {
            s_lastTelemetryHandle = default;
            if (s_lastVault == null)
                return;

            s_lastVault.Dispose();
            s_lastVault = null;
        }

        private static void RetainTelemetrySnapshot(NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry)
        {
            Require(telemetry.IsCreated && telemetry.Length > 0, "SHINOBU_355 telemetry snapshot source is empty.");
            DisposeTelemetryVault();

            GlobalDataVault vault = GlobalDataVault.Create(4, RetainedTelemetryVaultBytes);
            bool retained = false;
            try
            {
                VaultGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> handle = vault.EnsureGenerationHandle<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(
                    SmokeTelemetryBuffer,
                    telemetry.Length,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory);
                Require(vault.TryAcquireWriteLock(in handle, SystemID.Physics, out NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> snapshot), "Failed to acquire retained telemetry snapshot write lock.");
                try
                {
                    Require(snapshot.Length >= telemetry.Length, "Retained telemetry snapshot shorter than source telemetry.");
                    for (int i = 0; i < telemetry.Length; i++)
                        snapshot[i] = telemetry[i];
                }
                finally
                {
                    vault.ReleaseWriteLock(in handle, SystemID.Physics);
                }

                s_lastVault = vault;
                s_lastTelemetryHandle = handle;
                retained = true;
            }
            finally
            {
                if (!retained)
                    vault.Dispose();
            }
        }

        private static void StampMeasuredTelemetryMicroseconds(
            NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry,
            float averageMicroseconds)
        {
            if (!telemetry.IsCreated)
                return;

            for (int i = 0; i < telemetry.Length; i++)
            {
                HydrodynamicKccRuntime.KccSmokeTelemetryEntry entry = telemetry[i];
                entry.BurstExecutionMicroseconds = averageMicroseconds;
                telemetry[i] = entry;
            }
        }

        private static int ValidateSmokeBuffers(
            int phantomCount,
            int frameCount,
            NativeArray<KinematicStateDTO> states,
            NativeArray<HydrodynamicKccInputDTO> inputs,
            NativeArray<float3> proposed,
            NativeArray<HydrodynamicKccFaultFlagDTO> faults,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStates,
            NativeArray<double3> history,
            NativeArray<KinematicStateDTO> rollback,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results,
            NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failures,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry,
            NativeArray<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> drift,
            NativeArray<DesyncDetectedSignal> desync,
            NativeArray<HydrodynamicKccRuntime.KccSmokeProfileDTO> profiles,
            NativeArray<float> sdf)
        {
            int safePhantomCount = math.min(phantomCount, math.min(states.Length, smokeStates.Length));
            Require(safePhantomCount == phantomCount, "SHINOBU_355 unsafe schedule count: state/smoke lanes are shorter than phantom count.");
            RequireLength(inputs, safePhantomCount, "KCC inputs");
            RequireLength(proposed, safePhantomCount, "KCC proposed velocities");
            RequireLength(faults, safePhantomCount, "KCC fault flags");
            RequireLength(profiles, safePhantomCount, "smoke profiles");
            RequireLength(history, safePhantomCount * frameCount, "smoke position history");
            RequireLength(rollback, safePhantomCount * (HydrodynamicKccRuntime.KccSmokeRollbackWindowFrames + 1), "smoke rollback ring");
            RequireLength(results, 1, "smoke result");
            RequireLength(failures, HydrodynamicKccRuntime.KccSmokeMaxFailureRecords, "smoke failures");
            RequireLength(telemetry, HydrodynamicKccRuntime.KccSmokeTelemetryFrames, "smoke telemetry");
            RequireLength(drift, 1, "smoke drift");
            RequireLength(desync, 1, "smoke desync");
            RequireLength(sdf, HydrodynamicKccRuntime.KccSmokeSdfCellCount, "smoke SDF");
            return safePhantomCount;
        }

        private static void RequireLength<T>(NativeArray<T> array, int minimumLength, string label)
            where T : struct
        {
            Require(array.IsCreated && array.Length >= minimumLength, label + " length below required smoke capacity.");
        }

        private static void Initialize(
            NativeArray<KinematicStateDTO> states,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStates,
            NativeArray<HydrodynamicKccRuntime.KccSmokeProfileDTO> profiles,
            int profileCount,
            int phantomCount,
            double3 sectorOrigin,
            HydrodynamicKccTuningDTO tuning)
        {
            JobHandle initializeHandle = new HydrodynamicKccRuntime.InitializeSmokePhantomsJob
            {
                States = states,
                SmokeStates = smokeStates,
                Profiles = profiles,
                ProfileCount = profileCount,
                SectorOriginAup = sectorOrigin,
                Tuning = tuning
            }.Schedule(phantomCount, 32);
            DispatcherJobFence.TryComplete(ref initializeHandle, forceComplete: true);
        }

        private static void SeedResultAndDrift(
            NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results,
            NativeArray<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> drift,
            double3 sectorOrigin)
        {
            results[0] = default;
            drift[0] = new HydrodynamicKccRuntime.KccSmokeDriftProbeDTO
            {
                StartAup = sectorOrigin,
                CurrentAup = sectorOrigin,
                StepMeters = 10.0d,
                LastFrame = 0u,
                Flags = 0u
            };
        }

        private static void WarmBurst(
            NativeArray<KinematicStateDTO> states,
            NativeArray<HydrodynamicKccInputDTO> inputs,
            NativeArray<float3> proposed,
            NativeArray<HydrodynamicKccFaultFlagDTO> faults,
            NativeArray<float> sdf,
            NativeArray<double3> history,
            NativeArray<KinematicStateDTO> rollback,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTestStateDTO> smokeStates,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTestResultDTO> results,
            NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failures,
            NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry,
            NativeArray<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO> drift,
            NativeArray<DesyncDetectedSignal> desync,
            HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO sdfInfo,
            HydrodynamicKccTuningDTO tuning,
            double3 sectorOrigin,
            int entityCount)
        {
            JobHandle warmupHandle = new HydrodynamicKccRuntime.EvaluateHeadlessKccFrameLoopJob
            {
                States = states,
                Inputs = inputs,
                ProposedVelocities = proposed,
                FaultFlags = faults,
                Sdf = sdf,
                PositionHistory = history,
                RollbackStateRing = rollback,
                SmokeStates = smokeStates,
                Results = results,
                Failures = failures,
                Telemetry = telemetry,
                DriftProbe = drift,
                MockDesyncSignals = desync,
                SdfInfo = sdfInfo,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                EntityCount = entityCount,
                FrameCount = 16,
                SimulationTickDelta = HydrodynamicKccRuntime.KccSmokeFixedDeltaTime,
                Seed = 0xA551u
            }.Schedule();
            DispatcherJobFence.TryComplete(ref warmupHandle, forceComplete: true);
        }

        private static HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO BuildSdfInfo(double3 sectorOrigin)
        {
            const float cellSize = 4f;
            double3 half = new double3(
                HydrodynamicKccRuntime.KccSmokeSdfDimX * cellSize * 0.5f,
                HydrodynamicKccRuntime.KccSmokeSdfDimY * cellSize * 0.5f,
                HydrodynamicKccRuntime.KccSmokeSdfDimZ * cellSize * 0.5f);
            return new HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO
            {
                OriginAup = sectorOrigin - half,
                Dimensions = new int3(
                    HydrodynamicKccRuntime.KccSmokeSdfDimX,
                    HydrodynamicKccRuntime.KccSmokeSdfDimY,
                    HydrodynamicKccRuntime.KccSmokeSdfDimZ),
                CellSizeMeters = cellSize,
                SurfaceOffsetMeters = 0f,
                CapsuleRadiusMeters = 0.35f,
                Flags = 1u,
                ProfileHash = HydrodynamicKccRuntime.KccSmokeSourceHash
            };
        }

        private static HydrodynamicKccTuningDTO BuildTuning()
        {
            return new HydrodynamicKccTuningDTO
            {
                BaseDrag = 0.005f,
                FluidDensity = 1025f,
                MaxSpeed = 950f,
                GravityMultiplier = 0.15f,
                BuoyancyScalar = 1.02f,
                CapsuleRadius = 0.35f,
                CapsuleHeight = 1.8f,
                SkinWidth = 0.035f,
                GlobalQualityWeight = 1f,
                WaterSurfaceY = 0f,
                MockInputFrequency = 7.5f,
                MockInputAmplitude = 1f,
                VisualSyncSharpness = 48f,
                WakeThreshold = 0.25f,
                ProfileHash = HydrodynamicKccRuntime.KccSmokeSourceHash,
                Flags = 1u
            };
        }

        private static int TryLoadProfiles(NativeArray<HydrodynamicKccRuntime.KccSmokeProfileDTO> profiles)
        {
            string path = Path.Combine(ProjectRoot, "kcc_test_profiles.csv");
            if (!File.Exists(path))
                return 0;

            byte[] bytes = File.ReadAllBytes(path);
            int cursor = 0;
            int count = 0;
            while (count < profiles.Length && TryReadLine(bytes, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == (byte)'#' || EqualsAscii(FirstToken(line), "name"))
                    continue;
                if (TryReadProfile(line, out HydrodynamicKccRuntime.KccSmokeProfileDTO profile))
                    profiles[count++] = profile;
            }

            return count;
        }

        private static bool TryReadProfile(ReadOnlySpan<byte> line, out HydrodynamicKccRuntime.KccSmokeProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            ReadOnlySpan<byte> name = NextToken(line, ref cursor);
            if (!TryReadDouble(NextToken(line, ref cursor), out double x)) return false;
            if (!TryReadDouble(NextToken(line, ref cursor), out double y)) return false;
            if (!TryReadDouble(NextToken(line, ref cursor), out double z)) return false;
            if (!TryReadFloat(NextToken(line, ref cursor), out float vx)) return false;
            if (!TryReadFloat(NextToken(line, ref cursor), out float vy)) return false;
            if (!TryReadFloat(NextToken(line, ref cursor), out float vz)) return false;
            if (!TryReadFloat(NextToken(line, ref cursor), out float bx)) bx = 0f;
            if (!TryReadFloat(NextToken(line, ref cursor), out float by)) by = 0f;
            if (!TryReadFloat(NextToken(line, ref cursor), out float bz)) bz = 1f;
            if (!TryReadFloat(NextToken(line, ref cursor), out float speedScale)) speedScale = 1f;
            if (!IsProfileAupInRange(x) || !IsProfileAupInRange(y) || !IsProfileAupInRange(z))
                return false;
            if (!IsProfileSpeedInRange(vx) || !IsProfileSpeedInRange(vy) || !IsProfileSpeedInRange(vz))
                return false;
            float3 inputBias = new float3(bx, by, bz);
            if (!HydrodynamicKccMath.IsFinite(inputBias) ||
                math.lengthsq(inputBias) > MaxProfileInputBiasMagnitude * MaxProfileInputBiasMagnitude)
            {
                return false;
            }

            profile = new HydrodynamicKccRuntime.KccSmokeProfileDTO
            {
                StartAup = new double3(x, y, z),
                StartVelocity = new float3(vx, vy, vz),
                InputBias = inputBias,
                SpeedScale = math.clamp(speedScale, 0.01f, 4f),
                ProfileHash = HashFnv1A(name),
                Flags = 1u
            };
            return true;
        }

        private static bool IsProfileAupInRange(double value)
        {
            return math.isfinite(value) && math.abs(value) <= MaxProfileAupMagnitudeMeters;
        }

        private static bool IsProfileSpeedInRange(float value)
        {
            return math.isfinite(value) && math.abs(value) <= MaxProfileSpeedMetersPerSecond;
        }

        private static void WriteReport(
            Shinobu355KccSmokeSummary summary,
            HydrodynamicKccRuntime.KccSmokeTestResultDTO result,
            string evidenceClass,
            bool appliesCiBudget)
        {
            string directory = Path.Combine(ProjectRoot, "Docs", "Reports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "QA_OPTIMIZATION_REPORT.json");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> buffer = stackalloc byte[1024];
                int cursor = 0;
                cursor = AppendAscii(buffer, cursor, "{\"shinobu355KccSmoke\":{\"summary\":\"");
                cursor = AppendAscii(buffer, cursor, summary.ErrorFlags == 0u ? "STATIC_SMOKE_PASS_PENDING_IMPORT_PROOF" : "STATIC_SMOKE_FAIL");
                cursor = AppendAscii(buffer, cursor, "\",\"evidence_class\":\"");
                cursor = AppendAscii(buffer, cursor, evidenceClass);
                cursor = AppendAscii(buffer, cursor, "\",\"oop_static_report\":\"Docs/Reports/QA_OPTIMIZATION_OOP_REPORT.json");
                cursor = AppendAscii(buffer, cursor, "\",\"frames\":");
                cursor = AppendInt(buffer, cursor, HydrodynamicKccRuntime.KccSmokeDefaultFrameCount);
                cursor = AppendAscii(buffer, cursor, ",\"phantoms\":");
                cursor = AppendInt(buffer, cursor, HydrodynamicKccRuntime.KccSmokeDefaultPhantomCount);
                cursor = AppendAscii(buffer, cursor, ",\"avg_us_per_frame\":");
                cursor = AppendDoubleFixed3(buffer, cursor, summary.AverageMicrosecondsPerFrame);
                cursor = AppendAscii(buffer, cursor, ",\"avg_us_role\":\"");
                cursor = AppendAscii(buffer, cursor, appliesCiBudget ? "ci_budget_measurement" : "editor_wall_clock_not_ci_budget");
                cursor = AppendAscii(buffer, cursor, "\",\"performance_budget_us\":");
                cursor = AppendDoubleFixed3(buffer, cursor, PerformanceBudgetMicroseconds);
                cursor = AppendAscii(buffer, cursor, ",\"performance_budget_applied\":");
                cursor = AppendAscii(buffer, cursor, appliesCiBudget ? "true" : "false");
                cursor = AppendAscii(buffer, cursor, ",\"managed_alloc_bytes\":");
                cursor = AppendLong(buffer, cursor, summary.ManagedBytesAllocated);
                cursor = AppendAscii(buffer, cursor, ",\"drift_error_mm\":");
                cursor = AppendDoubleFixed3(buffer, cursor, summary.DriftErrorMillimeters);
                cursor = AppendAscii(buffer, cursor, ",\"rollback_passes\":");
                cursor = AppendUInt(buffer, cursor, summary.RollbackPasses);
                cursor = AppendAscii(buffer, cursor, ",\"rebases\":");
                cursor = AppendUInt(buffer, cursor, result.RebaseCount);
                cursor = AppendAscii(buffer, cursor, ",\"error_flags\":");
                cursor = AppendUInt(buffer, cursor, summary.ErrorFlags);
                cursor = AppendAscii(buffer, cursor, "}}\n");
                stream.Write(buffer.Slice(0, cursor));
            }
        }

        private static void WriteFailureCsv(NativeArray<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO> failures, int count)
        {
            string directory = Path.Combine(ProjectRoot, "Docs", "Reports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "HEADLESS_KCC_FAILURES.csv");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(FailureCsvHeader, 0, FailureCsvHeader.Length);
                Span<byte> line = stackalloc byte[384];
                int safeCount = math.clamp(count, 0, failures.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    HydrodynamicKccRuntime.KccSmokeFailureRecordDTO failure = failures[i];
                    int cursor = 0;
                    cursor = AppendUInt(line, cursor, failure.Frame);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendUInt(line, cursor, failure.EntityIndex);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendUInt(line, cursor, failure.FailureFlags);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.Aup.x);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.Aup.y);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.Aup.z);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.Velocity.x);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.Velocity.y);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.Velocity.z);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.SdfMeters);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendDoubleFixed3(line, cursor, failure.SpeedMetersPerSecond);
                    cursor = AppendComma(line, cursor);
                    cursor = AppendUInt(line, cursor, failure.StateHash);
                    line[cursor++] = 10;
                    stream.Write(line.Slice(0, cursor));
                }
            }
        }

        private static unsafe void WriteBlackBoxDump(
            NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry> telemetry,
            HydrodynamicKccRuntime.KccSmokeTestResultDTO result)
        {
            string directory = Path.Combine(ProjectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_SHINOBU_355.bin");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                int entrySize = UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>();
                uint newestFrame = result.FirstFailureFrame != 0u ? result.FirstFailureFrame : (uint)HydrodynamicKccRuntime.KccSmokeDefaultFrameCount;
                int safeEntryCount = telemetry.IsCreated ? math.min(telemetry.Length, (int)math.min(newestFrame, (uint)int.MaxValue)) : 0;
                uint oldestFrame = safeEntryCount > 0 ? newestFrame - (uint)safeEntryCount + 1u : 0u;
                Span<byte> header = stackalloc byte[32];
                header[0] = 72;
                header[1] = 56;
                header[2] = 75;
                header[3] = 67;
                header[4] = 67;
                header[5] = 51;
                header[6] = 53;
                header[7] = 53;
                WriteUInt32LE(header, 8, BlackBoxDumpVersion);
                WriteUInt32LE(header, 12, (uint)safeEntryCount);
                WriteUInt32LE(header, 16, (uint)entrySize);
                WriteUInt32LE(header, 20, oldestFrame);
                WriteUInt32LE(header, 24, newestFrame);
                WriteUInt32LE(header, 28, HydrodynamicKccRuntime.KccSmokeSourceHash);
                stream.Write(header);
                for (uint frame = oldestFrame; frame <= newestFrame && safeEntryCount > 0; frame++)
                {
                    int ringIndex = (int)(frame % (uint)telemetry.Length);
                    HydrodynamicKccRuntime.KccSmokeTelemetryEntry entry = telemetry[ringIndex];
                    void* ptr = UnsafeUtility.AddressOf(ref entry);
                    stream.Write(new ReadOnlySpan<byte>(ptr, entrySize));
                }
            }
        }

        private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
        {
            if (cursor >= text.Length)
            {
                line = default;
                return false;
            }

            int start = cursor;
            while (cursor < text.Length && text[cursor] != 10 && text[cursor] != 13)
                cursor++;
            line = text.Slice(start, cursor - start);
            while (cursor < text.Length && (text[cursor] == 10 || text[cursor] == 13))
                cursor++;
            return true;
        }

        private static ReadOnlySpan<byte> FirstToken(ReadOnlySpan<byte> line)
        {
            int cursor = 0;
            return NextToken(line, ref cursor);
        }

        private static ReadOnlySpan<byte> NextToken(ReadOnlySpan<byte> line, ref int cursor)
        {
            if (cursor >= line.Length)
                return ReadOnlySpan<byte>.Empty;
            int start = cursor;
            while (cursor < line.Length && line[cursor] != 44)
                cursor++;
            ReadOnlySpan<byte> token = Trim(line.Slice(start, cursor - start));
            if (cursor < line.Length && line[cursor] == 44)
                cursor++;
            return token;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
        {
            int start = 0;
            int end = text.Length - 1;
            while (start < text.Length && text[start] <= 32)
                start++;
            while (end >= start && text[end] <= 32)
                end--;
            return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> text, out float value)
        {
            bool ok = TryReadDouble(text, out double d);
            value = (float)d;
            return ok && math.isfinite(value);
        }

        private static bool TryReadDouble(ReadOnlySpan<byte> text, out double value)
        {
            value = 0d;
            if (text.Length == 0)
                return false;
            int cursor = 0;
            int sign = 1;
            if (text[cursor] == 45)
            {
                sign = -1;
                cursor++;
            }
            else if (text[cursor] == 43)
            {
                cursor++;
            }

            bool any = false;
            long whole = 0L;
            while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
            {
                any = true;
                int digit = text[cursor] - 48;
                if (whole > (long.MaxValue - digit) / 10L)
                    return false;
                whole = whole * 10L + digit;
                cursor++;
            }

            double fraction = 0d;
            double scale = 0.1d;
            if (cursor < text.Length && text[cursor] == 46)
            {
                cursor++;
                while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
                {
                    any = true;
                    fraction += (text[cursor] - 48) * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            int exponent = 0;
            if (cursor < text.Length && (text[cursor] == 69 || text[cursor] == 101))
            {
                cursor++;
                int exponentSign = 1;
                if (cursor < text.Length && text[cursor] == 45)
                {
                    exponentSign = -1;
                    cursor++;
                }
                else if (cursor < text.Length && text[cursor] == 43)
                {
                    cursor++;
                }

                bool exponentAny = false;
                while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
                {
                    exponentAny = true;
                    if (exponent < 308)
                        exponent = math.min(308, exponent * 10 + text[cursor] - 48);
                    cursor++;
                }

                if (!exponentAny)
                    return false;
                exponent *= exponentSign;
            }

            if (!any || cursor != text.Length)
                return false;

            value = sign * (whole + fraction);
            if (exponent != 0)
                value *= Pow10Signed(exponent);
            return math.isfinite(value);
        }

        private static double Pow10Signed(int exponent)
        {
            int count = math.abs(exponent);
            double result = 1d;
            for (int i = 0; i < count; i++)
                result *= 10d;

            return exponent < 0 ? 1d / result : result;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> value, string literal)
        {
            if (value.Length != literal.Length)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                byte a = value[i];
                if (a >= 65 && a <= 90)
                    a = (byte)(a + 32);
                if (a != (byte)literal[i])
                    return false;
            }
            return true;
        }

        private static uint HashFnv1A(ReadOnlySpan<byte> text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                byte b = text[i];
                if (b >= 65 && b <= 90)
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }

        private static int AppendComma(Span<byte> buffer, int cursor)
        {
            buffer[cursor++] = 44;
            return cursor;
        }

        private static int AppendAscii(Span<byte> buffer, int cursor, string text)
        {
            for (int i = 0; i < text.Length; i++)
                buffer[cursor++] = (byte)text[i];
            return cursor;
        }

        private static int AppendUInt(Span<byte> buffer, int cursor, uint value)
        {
            Span<char> chars = stackalloc char[16];
            value.TryFormat(chars, out int written);
            for (int i = 0; i < written; i++)
                buffer[cursor++] = (byte)chars[i];
            return cursor;
        }

        private static int AppendInt(Span<byte> buffer, int cursor, int value)
        {
            Span<char> chars = stackalloc char[16];
            value.TryFormat(chars, out int written);
            for (int i = 0; i < written; i++)
                buffer[cursor++] = (byte)chars[i];
            return cursor;
        }

        private static int AppendLong(Span<byte> buffer, int cursor, long value)
        {
            Span<char> chars = stackalloc char[32];
            value.TryFormat(chars, out int written);
            for (int i = 0; i < written; i++)
                buffer[cursor++] = (byte)chars[i];
            return cursor;
        }

        private static void WriteUInt32LE(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static int AppendDoubleFixed3(Span<byte> buffer, int cursor, double value)
        {
            if (!math.isfinite(value))
            {
                buffer[cursor++] = 48;
                return cursor;
            }
            if (value < 0d)
            {
                buffer[cursor++] = 45;
                value = -value;
            }
            long whole = (long)value;
            long fraction = (long)math.round((value - whole) * 1000d);
            if (fraction >= 1000L)
            {
                whole++;
                fraction -= 1000L;
            }
            cursor = AppendLong(buffer, cursor, whole);
            buffer[cursor++] = 46;
            buffer[cursor++] = (byte)(48 + ((fraction / 100L) % 10L));
            buffer[cursor++] = (byte)(48 + ((fraction / 10L) % 10L));
            buffer[cursor++] = (byte)(48 + (fraction % 10L));
            return cursor;
        }
    }

    public static class HeadlessKccLayoutAssertions
    {
        public static void AssertAll()
        {
            AssertExplicit(typeof(KinematicStateDTO), nameof(KinematicStateDTO));
            AssertExplicit(typeof(ForcePacketDTO), nameof(ForcePacketDTO));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeTestStateDTO), nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeProfileDTO), nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO), nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeTestResultDTO), nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO), nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry), nameof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry));
            AssertExplicit(typeof(HydrodynamicKccRuntime.KccSmokeDriftProbeDTO), nameof(HydrodynamicKccRuntime.KccSmokeDriftProbeDTO));

            RequireEqual(64, UnsafeUtility.SizeOf<KinematicStateDTO>(), "KinematicStateDTO size");
            RequireEqual(32, UnsafeUtility.SizeOf<ForcePacketDTO>(), "ForcePacketDTO size");
            RequireEqual(8, UnsafeUtility.AlignOf<KinematicStateDTO>(), "KinematicStateDTO align");
            RequireEqual(8, UnsafeUtility.AlignOf<ForcePacketDTO>(), "ForcePacketDTO align");
            RequireEqual(0, Marshal.OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.AUP_Position)).ToInt32(), "KinematicStateDTO.AUP_Position offset");
            RequireEqual(24, Marshal.OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Velocity)).ToInt32(), "KinematicStateDTO.Velocity offset");
            RequireEqual(48, Marshal.OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Mass)).ToInt32(), "KinematicStateDTO.Mass offset");
            RequireEqual(0, Marshal.OffsetOf<ForcePacketDTO>(nameof(ForcePacketDTO.ForceVector)).ToInt32(), "ForcePacketDTO.ForceVector offset");
            RequireEqual(12, Marshal.OffsetOf<ForcePacketDTO>(nameof(ForcePacketDTO.TorqueScalar)).ToInt32(), "ForcePacketDTO.TorqueScalar offset");
            RequireEqual(24, Marshal.OffsetOf<ForcePacketDTO>(nameof(ForcePacketDTO._pad0)).ToInt32(), "ForcePacketDTO._pad0 offset");

            RequireEqual(32, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(), "KccSmokeTestStateDTO size");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(), "KccSmokeTestStateDTO align");
            RequireEqual(64, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeProfileDTO>(), "KccSmokeProfileDTO size");
            RequireEqual(64, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(), "KccSmokeVoxelSdfInfoDTO size");
            RequireEqual(128, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(), "KccSmokeTestResultDTO size");
            RequireEqual(128, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(), "KccSmokeFailureRecordDTO size");
            RequireEqual(64, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(), "KccSmokeTelemetryEntry size");
            RequireEqual(64, UnsafeUtility.SizeOf<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(), "KccSmokeDriftProbeDTO size");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeProfileDTO>(), "KccSmokeProfileDTO align");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(), "KccSmokeVoxelSdfInfoDTO align");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(), "KccSmokeTestResultDTO align");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(), "KccSmokeFailureRecordDTO align");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(), "KccSmokeTelemetryEntry align");
            RequireEqual(8, UnsafeUtility.AlignOf<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(), "KccSmokeDriftProbeDTO align");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(0, nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO.TestPlayerAUP), "KccSmokeTestStateDTO.TestPlayerAUP");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(24, nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO.CurrentFrameCount), "KccSmokeTestStateDTO.CurrentFrameCount");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestStateDTO>(28, nameof(HydrodynamicKccRuntime.KccSmokeTestStateDTO.MismatchFlags), "KccSmokeTestStateDTO.MismatchFlags");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeProfileDTO>(0, nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO.StartAup), "KccSmokeProfileDTO.StartAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeProfileDTO>(24, nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO.StartVelocity), "KccSmokeProfileDTO.StartVelocity");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeProfileDTO>(36, nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO.InputBias), "KccSmokeProfileDTO.InputBias");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeProfileDTO>(48, nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO.SpeedScale), "KccSmokeProfileDTO.SpeedScale");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeProfileDTO>(52, nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO.ProfileHash), "KccSmokeProfileDTO.ProfileHash");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeProfileDTO>(56, nameof(HydrodynamicKccRuntime.KccSmokeProfileDTO.Flags), "KccSmokeProfileDTO.Flags");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(0, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.OriginAup), "KccSmokeVoxelSdfInfoDTO.OriginAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(24, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.Dimensions), "KccSmokeVoxelSdfInfoDTO.Dimensions");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(36, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.CellSizeMeters), "KccSmokeVoxelSdfInfoDTO.CellSizeMeters");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(40, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.SurfaceOffsetMeters), "KccSmokeVoxelSdfInfoDTO.SurfaceOffsetMeters");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(44, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.CapsuleRadiusMeters), "KccSmokeVoxelSdfInfoDTO.CapsuleRadiusMeters");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(48, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.Flags), "KccSmokeVoxelSdfInfoDTO.Flags");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO>(52, nameof(HydrodynamicKccRuntime.KccSmokeVoxelSdfInfoDTO.ProfileHash), "KccSmokeVoxelSdfInfoDTO.ProfileHash");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(0, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.ErrorFlags), "KccSmokeTestResultDTO.ErrorFlags");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(4, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.FailureCount), "KccSmokeTestResultDTO.FailureCount");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(16, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.FirstFailureAup), "KccSmokeTestResultDTO.FirstFailureAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(40, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.FirstFailureVelocity), "KccSmokeTestResultDTO.FirstFailureVelocity");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(56, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.AverageMicrosecondsPerFrame), "KccSmokeTestResultDTO.AverageMicrosecondsPerFrame");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(72, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.DriftErrorMillimeters), "KccSmokeTestResultDTO.DriftErrorMillimeters");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTestResultDTO>(92, nameof(HydrodynamicKccRuntime.KccSmokeTestResultDTO.SuccessfulRollbackCount), "KccSmokeTestResultDTO.SuccessfulRollbackCount");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(0, nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO.Aup), "KccSmokeFailureRecordDTO.Aup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(24, nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO.Velocity), "KccSmokeFailureRecordDTO.Velocity");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(36, nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO.SdfMeters), "KccSmokeFailureRecordDTO.SdfMeters");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(40, nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO.Frame), "KccSmokeFailureRecordDTO.Frame");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(56, nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO.PreviousAup), "KccSmokeFailureRecordDTO.PreviousAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeFailureRecordDTO>(80, nameof(HydrodynamicKccRuntime.KccSmokeFailureRecordDTO.InputVector), "KccSmokeFailureRecordDTO.InputVector");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(0, nameof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry.FirstAup), "KccSmokeTelemetryEntry.FirstAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(24, nameof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry.HighestPenetrationDepth), "KccSmokeTelemetryEntry.HighestPenetrationDepth");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(36, nameof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry.BurstExecutionMicroseconds), "KccSmokeTelemetryEntry.BurstExecutionMicroseconds");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(40, nameof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry.Frame), "KccSmokeTelemetryEntry.Frame");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>(44, nameof(HydrodynamicKccRuntime.KccSmokeTelemetryEntry.StateHash), "KccSmokeTelemetryEntry.StateHash");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(0, nameof(HydrodynamicKccRuntime.KccSmokeDriftProbeDTO.StartAup), "KccSmokeDriftProbeDTO.StartAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(24, nameof(HydrodynamicKccRuntime.KccSmokeDriftProbeDTO.CurrentAup), "KccSmokeDriftProbeDTO.CurrentAup");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(48, nameof(HydrodynamicKccRuntime.KccSmokeDriftProbeDTO.StepMeters), "KccSmokeDriftProbeDTO.StepMeters");
            RequireOffset<HydrodynamicKccRuntime.KccSmokeDriftProbeDTO>(56, nameof(HydrodynamicKccRuntime.KccSmokeDriftProbeDTO.LastFrame), "KccSmokeDriftProbeDTO.LastFrame");
        }

        private static void AssertExplicit(Type type, string name)
        {
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            if (layout == null || layout.Value != LayoutKind.Explicit)
                throw new FatalArchitectureException(name + " must be LayoutKind.Explicit.");
        }

        private static void RequireEqual(int expected, int actual, string label)
        {
            if (expected != actual)
                throw new FatalArchitectureException(label + " expected=" + expected + " actual=" + actual);
        }

        private static void RequireOffset<T>(int expected, string fieldName, string label)
            where T : struct
        {
            int actual = Marshal.OffsetOf<T>(fieldName).ToInt32();
            RequireEqual(expected, actual, label + " offset");
        }
    }

    internal sealed class HeadlessKccSmokeTesterWindow : EditorWindow
    {
        private Button _runButton;
        private Label _statusLabel;
        private ProgressBar _progressBar;
        private HeadlessKccSmokeTelemetryGraphElement _graph;
        private Shinobu355KccSmokeRunner.ScheduledRun _scheduledRun;
        private Shinobu355KccSmokeSummary _lastSummary;

        [MenuItem("HECTON-8/Kinematics/Headless Smoke Tester")]
        public static void Open()
        {
            GetWindow<HeadlessKccSmokeTesterWindow>("Headless Smoke Tester");
        }

        public void CreateGUI()
        {
            _runButton = new Button(RunSmokeTest) { text = "RUN 10,000 FRAME SMOKE TEST" };
            _statusLabel = new Label("PENDING");
            _progressBar = new ProgressBar { title = "KCC Physics Smoke Tester", lowValue = 0f, highValue = 1f, value = 0f };
            _graph = new HeadlessKccSmokeTelemetryGraphElement();
            rootVisualElement.Add(_runButton);
            rootVisualElement.Add(_progressBar);
            rootVisualElement.Add(_graph);
            rootVisualElement.Add(_statusLabel);
        }

        private void OnDisable()
        {
            EditorApplication.update -= PollSmokeTest;
            if (_scheduledRun == null)
                return;

            _scheduledRun.Dispose();
            _scheduledRun = null;
        }

        private void RunSmokeTest()
        {
            if (_scheduledRun != null)
                return;

            _progressBar.value = 0.15f;
            _statusLabel.text = "RUNNING | scheduled Burst pipeline";
            _statusLabel.style.color = Color.white;
            _runButton.SetEnabled(false);
            try
            {
                _scheduledRun = Shinobu355KccSmokeRunner.StartScheduledRun();
                EditorApplication.update -= PollSmokeTest;
                EditorApplication.update += PollSmokeTest;
            }
            catch (Exception ex)
            {
                _runButton.SetEnabled(true);
                _progressBar.value = 1f;
                _statusLabel.text = "START FAILED | " + ex.GetType().Name;
                _statusLabel.style.color = Color.red;
            }
        }

        private void PollSmokeTest()
        {
            if (_scheduledRun == null)
            {
                EditorApplication.update -= PollSmokeTest;
                return;
            }

            bool done;
            try
            {
                done = _scheduledRun.Poll();
            }
            catch (Exception ex)
            {
                _scheduledRun.Dispose();
                _scheduledRun = null;
                _runButton.SetEnabled(true);
                _progressBar.value = 1f;
                _statusLabel.text = "RUN FAILED | " + ex.GetType().Name;
                _statusLabel.style.color = Color.red;
                EditorApplication.update -= PollSmokeTest;
                return;
            }

            _progressBar.value = _scheduledRun.Progress;
            if (!done)
                return;

            _lastSummary = _scheduledRun.Summary;
            bool passed = _scheduledRun.Passed;
            _scheduledRun.Dispose();
            _scheduledRun = null;
            _runButton.SetEnabled(true);
            _progressBar.value = 1f;
            _statusLabel.text = passed
                ? "PASS | avg_us=" + _lastSummary.AverageMicrosecondsPerFrame.ToString("F3")
                : "FAIL | flags=" + _lastSummary.ErrorFlags + " | failures=" + _lastSummary.FailureCount;
            _statusLabel.style.color = passed ? Color.green : Color.red;
            _graph.MarkDirtyRepaint();
            SceneView.RepaintAll();
            EditorApplication.update -= PollSmokeTest;
        }
    }

    internal sealed class HeadlessKccSmokeTelemetryGraphElement : VisualElement
    {
        public HeadlessKccSmokeTelemetryGraphElement()
        {
            style.height = 112f;
            style.marginTop = 6f;
            style.marginBottom = 6f;
            generateVisualContent += DrawTelemetry;
        }

        private void DrawTelemetry(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            Painter2D painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMax - 1f));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - 1f));
            painter.Stroke();

            if (rect.width <= 2f || rect.height <= 2f)
                return;

            if (!Shinobu355KccSmokeRunner.TryGetLastTelemetry(out NativeArray<HydrodynamicKccRuntime.KccSmokeTelemetryEntry>.ReadOnly telemetry))
                return;

            int count = math.min(telemetry.Length, HydrodynamicKccRuntime.KccSmokeTelemetryFrames);
            if (count <= 1)
                return;

            float maxDepth = 0.001f;
            for (int i = 0; i < count; i++)
                maxDepth = math.max(maxDepth, math.max(0f, telemetry[i].HighestPenetrationDepth));

            painter.lineWidth = 2f;
            painter.strokeColor = new Color(0.1f, 0.85f, 0.35f, 1f);
            painter.BeginPath();
            float invMax = math.rcp(maxDepth);
            float stepX = rect.width / math.max(1f, count - 1f);
            for (int i = 0; i < count; i++)
            {
                float depth = math.max(0f, telemetry[i].HighestPenetrationDepth);
                float x = rect.xMin + (i * stepX);
                float y = rect.yMax - math.saturate(depth * invMax) * rect.height;
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }

    [InitializeOnLoad]
    internal static class HeadlessKccFailureGizmo
    {
        private static bool s_hasFailure;
        private static double3 s_failureAup;
        private static double3 s_previousAup;
        private static double3 s_gizmoOriginAup;
        private static float3 s_velocity;
        private static float3 s_inputVector;

        static HeadlessKccFailureGizmo()
        {
            SceneView.duringSceneGui -= DrawFailure;
            SceneView.duringSceneGui += DrawFailure;
        }

        public static void SetFailure(double3 aup)
        {
            SetFailure(aup, aup, float3.zero, float3.zero);
        }

        public static void SetFailure(double3 aup, double3 previousAup, float3 velocity, float3 inputVector)
        {
            s_hasFailure = true;
            s_failureAup = aup;
            s_previousAup = previousAup;
            s_gizmoOriginAup = previousAup;
            s_velocity = velocity;
            s_inputVector = inputVector;
            SceneView.RepaintAll();
        }

        public static void ClearFailure()
        {
            s_hasFailure = false;
            SceneView.RepaintAll();
        }

        private static void DrawFailure(SceneView sceneView)
        {
            if (!s_hasFailure)
                return;

            float3 localFailure = HydrodynamicKccMath.ResolveLocalFloat3(s_failureAup, s_gizmoOriginAup);
            float3 localPrevious = HydrodynamicKccMath.ResolveLocalFloat3(s_previousAup, s_gizmoOriginAup);
            Vector3 pos = new Vector3(localFailure.x, localFailure.y, localFailure.z);
            Vector3 prev = new Vector3(localPrevious.x, localPrevious.y, localPrevious.z);
            Vector3 velocity = new Vector3(s_velocity.x, s_velocity.y, s_velocity.z);
            Vector3 input = new Vector3(s_inputVector.x, s_inputVector.y, s_inputVector.z);
            float pulse = 1f + 0.18f * MathLodApproximation.ApproxSinBhaskara((float)EditorApplication.timeSinceStartup * 7f);
            float radius = 12f * pulse;
            Handles.color = Color.red;
            Handles.DrawWireDisc(pos, Vector3.up, radius);
            Handles.DrawWireDisc(pos, Vector3.right, radius);
            Handles.DrawLine(pos + Vector3.left * (radius * 1.35f), pos + Vector3.right * (radius * 1.35f));
            Handles.DrawLine(pos + Vector3.up * (radius * 1.35f), pos + Vector3.down * (radius * 1.35f));
            if ((pos - prev).sqrMagnitude > 0.000001f)
            {
                Handles.color = Color.green;
                Handles.DrawAAPolyLine(4f, prev, pos);
            }

            if (velocity.sqrMagnitude > 0.000001f)
            {
                Handles.color = Color.yellow;
                Handles.ArrowHandleCap(0, pos, Quaternion.LookRotation(velocity.normalized, Vector3.up), radius * 1.2f, EventType.Repaint);
            }

            if (input.sqrMagnitude > 0.000001f)
            {
                Handles.color = Color.cyan;
                Handles.DrawLine(pos, pos + input.normalized * (radius * 0.9f));
            }

            Handles.Label(pos + Vector3.up * (radius * 1.5f), "KCC FAIL");
        }
    }
}
#endif
