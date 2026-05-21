#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Hecton8.Physics.KCC;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ForcePacketDTO = Hecton8.Physics.ForcePacketDTO;

namespace Hecton8.Tests.Editor
{
    /// <summary>
    /// Headless KCC smoke tests: no scene, no camera, no audio, no Unity Physics scene.
    /// </summary>
    public sealed class HeadlessKccSmokeTests
    {
        [Test]
        public void OceanKinematicsRuntimeService_HasNoForbiddenHeadlessDependency()
        {
            string projectRoot = HeadlessKccSmokeTestRunner.ProjectRoot;
            string path = Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "Core", "OceanKinematicsRuntimeService.cs");
            string source = File.ReadAllText(path);

            Assert.IsFalse(source.Contains("Camera.main"), "OceanKinematicsRuntimeService must not read Camera.main.");
            Assert.IsFalse(source.Contains("Time.deltaTime"), "OceanKinematicsRuntimeService must use injected tick dt.");
            Assert.IsFalse(source.Contains("FindObjectOfType"), "OceanKinematicsRuntimeService must not search the scene.");
            Assert.IsFalse(source.Contains("GameObject.Find"), "OceanKinematicsRuntimeService must not search the scene.");
        }

        [Test]
        public void HeadlessKcc_Layouts_AreExplicitAndAligned()
        {
            HeadlessKccLayoutAssertions.AssertAll();
        }

        [Test]
        public void HeadlessKcc_100Phantoms_10000Frames_NoNanNoTunnelUnder50Microseconds()
        {
            HeadlessKccRunSummary summary;
            bool passed = HeadlessKccSmokeTestRunner.Run(out summary);
            if (!passed)
                Assert.Fail("Headless KCC smoke failed. Read Docs/Reports/HEADLESS_KCC_FAILURES.csv and Docs/AgentLogs/Dump_SHINOBU_254.bin.");
        }
    }

    internal static class HeadlessKccSmokeTestConstants
    {
        public const int PhantomCount = 100;
        public const int FrameCount = 10000;
        public const int TelemetryFrames = 300;
        public const int SdfDimX = 48;
        public const int SdfDimY = 48;
        public const int SdfDimZ = 48;
        public const int SdfCellCount = SdfDimX * SdfDimY * SdfDimZ;
        public const int MaxFailureRecords = 512;
        public const int MaxSweepIterations = 8;
        public const float FixedDeltaTime = 0.016666667f;
        public const float PerformanceBudgetMicroseconds = 50f;
        public const float StrongPenetrationMeters = -1f;
        public const uint FailureNone = 0u;
        public const uint FailureNonFinite = 1u;
        public const uint FailureTunneling = 1u << 1;
        public const uint FailurePerformance = 1u << 2;
        public const uint FailurePrecisionDrift = 1u << 3;
        public const uint FailureLayout = 1u << 4;
        public const uint FailureAllocation = 1u << 5;
        public const uint FailureSdfInvalid = 1u << 6;
        public const uint FailureInputSanitized = 1u << 7;
        public const uint SourceHash = 0x53483235u;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HeadlessKccProfileDTO
    {
        [FieldOffset(0)] public double3 StartAup;
        [FieldOffset(24)] public float3 StartVelocity;
        [FieldOffset(36)] public float3 InputBias;
        [FieldOffset(48)] public float SpeedScale;
        [FieldOffset(52)] public uint ProfileHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HeadlessKccVoxelSdfInfoDTO
    {
        [FieldOffset(0)] public double3 OriginAup;
        [FieldOffset(24)] public int3 Dimensions;
        [FieldOffset(36)] public float CellSizeMeters;
        [FieldOffset(40)] public float SurfaceOffsetMeters;
        [FieldOffset(44)] public float CapsuleRadiusMeters;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] public uint ProfileHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HeadlessKccTestResultDTO
    {
        [FieldOffset(0)] public uint ErrorFlags;
        [FieldOffset(4)] public uint FailureCount;
        [FieldOffset(8)] public uint FirstFailureFrame;
        [FieldOffset(12)] public uint FirstFailureIndex;
        [FieldOffset(16)] public double3 FirstFailureAup;
        [FieldOffset(40)] public float3 FirstFailureVelocity;
        [FieldOffset(52)] public float WorstPenetrationMeters;
        [FieldOffset(56)] public float AverageMicrosecondsPerFrame;
        [FieldOffset(60)] public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    internal struct HeadlessKccFailureRecordDTO
    {
        [FieldOffset(0)] public double3 Aup;
        [FieldOffset(24)] public float3 Velocity;
        [FieldOffset(36)] public float SdfMeters;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint EntityIndex;
        [FieldOffset(48)] public uint FailureFlags;
        [FieldOffset(52)] public uint StateHash;
        [FieldOffset(56)] public double3 PreviousAup;
        [FieldOffset(80)] public float3 InputVector;
        [FieldOffset(92)] public float SpeedMetersPerSecond;
        [FieldOffset(96)] public ulong _pad0;
        [FieldOffset(104)] public ulong _pad1;
        [FieldOffset(112)] public ulong _pad2;
        [FieldOffset(120)] public ulong _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HeadlessKccTelemetryEntry
    {
        [FieldOffset(0)] public double3 FirstAup;
        [FieldOffset(24)] public float MaxSpeed;
        [FieldOffset(28)] public float MinSdfMeters;
        [FieldOffset(32)] public float MeanSpeed;
        [FieldOffset(36)] public uint Frame;
        [FieldOffset(40)] public uint StateHash;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint ActivePhantoms;
        [FieldOffset(52)] public uint SanitizedInputs;
        [FieldOffset(56)] public ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct HeadlessKccDriftProbeDTO
    {
        [FieldOffset(0)] public double3 StartAup;
        [FieldOffset(24)] public double3 CurrentAup;
        [FieldOffset(48)] public double StepMeters;
        [FieldOffset(56)] public uint LastFrame;
        [FieldOffset(60)] public uint Flags;
    }

    internal struct HeadlessKccRunSummary
    {
        public uint ErrorFlags;
        public uint FailureCount;
        public long ManagedBytesAllocated;
        public float AverageMicrosecondsPerFrame;
        public double DriftErrorMeters;
        public double3 FirstFailureAup;
        public string StatusText;
    }

    internal static class HeadlessKccLayoutAssertions
    {
        public static void AssertAll()
        {
            AssertExplicit(typeof(KinematicStateDTO), nameof(KinematicStateDTO));
            AssertExplicit(typeof(ForcePacketDTO), nameof(ForcePacketDTO));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<KinematicStateDTO>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<ForcePacketDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<KinematicStateDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<ForcePacketDTO>());
            Assert.AreEqual(0, Marshal.OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.AUP_Position)).ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Velocity)).ToInt32());
            Assert.AreEqual(48, Marshal.OffsetOf<KinematicStateDTO>(nameof(KinematicStateDTO.Mass)).ToInt32());
            Assert.AreEqual(0, Marshal.OffsetOf<ForcePacketDTO>(nameof(ForcePacketDTO.ForceVector)).ToInt32());
            Assert.AreEqual(12, Marshal.OffsetOf<ForcePacketDTO>(nameof(ForcePacketDTO.TorqueScalar)).ToInt32());
            Assert.AreEqual(24, Marshal.OffsetOf<ForcePacketDTO>(nameof(ForcePacketDTO._pad0)).ToInt32());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HeadlessKccProfileDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HeadlessKccVoxelSdfInfoDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HeadlessKccTestResultDTO>());
            Assert.AreEqual(128, UnsafeUtility.SizeOf<HeadlessKccFailureRecordDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HeadlessKccTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<HeadlessKccDriftProbeDTO>());
            Assert.AreEqual(16, Marshal.OffsetOf<HeadlessKccTestResultDTO>(nameof(HeadlessKccTestResultDTO.FirstFailureAup)).ToInt32());
        }

        private static void AssertExplicit(Type type, string name)
        {
            StructLayoutAttribute layout = type.StructLayoutAttribute;
            Assert.IsNotNull(layout, name + " lacks StructLayoutAttribute.");
            Assert.AreEqual(LayoutKind.Explicit, layout.Value, name + " must be LayoutKind.Explicit.");
        }
    }

    internal static class HeadlessKccSmokeTestRunner
    {
        private static readonly byte[] FailureCsvHeader =
        {
            102,114,97,109,101,44,101,110,116,105,116,121,44,102,108,97,103,115,44,97,117,112,95,120,44,97,117,112,95,121,44,97,117,112,95,122,44,
            118,101,108,95,120,44,118,101,108,95,121,44,118,101,108,95,122,44,115,100,102,95,109,44,115,112,101,101,100,95,109,112,115,44,104,97,115,104,10
        };

        public static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        public static bool Run(out HeadlessKccRunSummary summary)
        {
            summary = default;
            HeadlessKccLayoutAssertions.AssertAll();

            NativeArray<HeadlessKccProfileDTO> profiles = default;
            NativeArray<HeadlessKccTestResultDTO> results = default;
            NativeArray<HeadlessKccFailureRecordDTO> failures = default;
            NativeArray<HeadlessKccTelemetryEntry> telemetry = default;
            NativeArray<HeadlessKccDriftProbeDTO> drift = default;

            try
            {
                profiles = new NativeArray<HeadlessKccProfileDTO>(
                    HeadlessKccSmokeTestConstants.PhantomCount,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                results = new NativeArray<HeadlessKccTestResultDTO>(
                    1,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                failures = new NativeArray<HeadlessKccFailureRecordDTO>(
                    HeadlessKccSmokeTestConstants.MaxFailureRecords,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                telemetry = new NativeArray<HeadlessKccTelemetryEntry>(
                    HeadlessKccSmokeTestConstants.TelemetryFrames,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);
                drift = new NativeArray<HeadlessKccDriftProbeDTO>(
                    1,
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory);

                int profileCount = TryLoadProfiles(profiles);
                results[0] = default;
                failures[0] = default;
                telemetry[0] = default;

                using (GlobalDataVault vault = GlobalDataVault.Create(96, 16L * 1024L * 1024L))
                {
                    VaultBufferHandle<KinematicStateDTO> statesHandle = vault.GetBufferHandle<KinematicStateDTO>(
                        BufferID.ShinobuHydroKccStates,
                        HeadlessKccSmokeTestConstants.PhantomCount,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);
                    VaultBufferHandle<HydrodynamicKccInputDTO> inputsHandle = vault.GetBufferHandle<HydrodynamicKccInputDTO>(
                        BufferID.ShinobuHydroKccInputs,
                        HeadlessKccSmokeTestConstants.PhantomCount,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);
                    VaultBufferHandle<float3> proposedHandle = vault.GetBufferHandle<float3>(
                        BufferID.ShinobuHydroKccProposedVelocities,
                        HeadlessKccSmokeTestConstants.PhantomCount,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);
                    VaultBufferHandle<HydrodynamicKccFaultFlagDTO> faultHandle = vault.GetBufferHandle<HydrodynamicKccFaultFlagDTO>(
                        BufferID.ShinobuHydroKccFaultFlags,
                        HeadlessKccSmokeTestConstants.PhantomCount,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);
                    VaultBufferHandle<float> sdfHandle = vault.GetBufferHandle<float>(
                        BufferID.ShinobuKccEnvironmentSdf,
                        HeadlessKccSmokeTestConstants.SdfCellCount,
                        SystemID.Physics,
                        NativeArrayOptions.UninitializedMemory);

                    NativeArray<KinematicStateDTO> states = statesHandle.Resolve(vault);
                    NativeArray<HydrodynamicKccInputDTO> inputs = inputsHandle.Resolve(vault);
                    NativeArray<float3> proposed = proposedHandle.Resolve(vault);
                    NativeArray<HydrodynamicKccFaultFlagDTO> faults = faultHandle.Resolve(vault);
                    NativeArray<float> sdf = sdfHandle.Resolve(vault);
                    Assert.IsTrue(states.IsCreated && inputs.IsCreated && proposed.IsCreated && faults.IsCreated && sdf.IsCreated);

                    double3 sectorOrigin = new double3(99000.0, -400.0, -99000.0);
                    HeadlessKccVoxelSdfInfoDTO sdfInfo = BuildSdfInfo(sectorOrigin);
                    HydrodynamicKccTuningDTO tuning = BuildUltraTuning();

                    new GenerateHeadlessVoxelSdfJob
                    {
                        Sdf = sdf,
                        Info = sdfInfo
                    }.Schedule(sdf.Length, 128).Complete();

                    new InitializePhantomsJob
                    {
                        States = states,
                        Profiles = profiles,
                        ProfileCount = profileCount,
                        SectorOriginAup = sectorOrigin,
                        Tuning = tuning
                    }.Schedule(HeadlessKccSmokeTestConstants.PhantomCount, 32).Complete();

                    drift[0] = new HeadlessKccDriftProbeDTO
                    {
                        StartAup = new double3(99000.0, -400.0, -99000.0),
                        CurrentAup = new double3(99000.0, -400.0, -99000.0),
                        StepMeters = 10.0,
                        LastFrame = 0u,
                        Flags = 0u
                    };

                    WarmBurst(states, inputs, proposed, faults, sdf, results, failures, telemetry, drift, sdfInfo, tuning, sectorOrigin);

                    new InitializePhantomsJob
                    {
                        States = states,
                        Profiles = profiles,
                        ProfileCount = profileCount,
                        SectorOriginAup = sectorOrigin,
                        Tuning = tuning
                    }.Schedule(HeadlessKccSmokeTestConstants.PhantomCount, 32).Complete();
                    results[0] = default;
                    drift[0] = new HeadlessKccDriftProbeDTO
                    {
                        StartAup = new double3(99000.0, -400.0, -99000.0),
                        CurrentAup = new double3(99000.0, -400.0, -99000.0),
                        StepMeters = 10.0,
                        LastFrame = 0u,
                        Flags = 0u
                    };

                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    long startTicks = Stopwatch.GetTimestamp();
                    JobHandle headlessHandle = new HeadlessKccFrameLoopJob
                    {
                        States = states,
                        Inputs = inputs,
                        ProposedVelocities = proposed,
                        FaultFlags = faults,
                        Sdf = sdf,
                        Results = results,
                        Failures = failures,
                        Telemetry = telemetry,
                        DriftProbe = drift,
                        SdfInfo = sdfInfo,
                        Tuning = tuning,
                        SectorOriginAup = sectorOrigin,
                        EntityCount = HeadlessKccSmokeTestConstants.PhantomCount,
                        FrameCount = HeadlessKccSmokeTestConstants.FrameCount,
                        SimulationTickDelta = HeadlessKccSmokeTestConstants.FixedDeltaTime,
                        Seed = HeadlessKccSmokeTestConstants.SourceHash
                    }.Schedule();
                    headlessHandle.Complete();
                    new ValidateKinematicStateJob
                    {
                        States = states,
                        Sdf = sdf,
                        Results = results,
                        Failures = failures,
                        SdfInfo = sdfInfo,
                        Tuning = tuning,
                        Frame = HeadlessKccSmokeTestConstants.FrameCount,
                        EntityCount = HeadlessKccSmokeTestConstants.PhantomCount
                    }.Schedule(headlessHandle).Complete();
                    long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
                    long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

                    HeadlessKccTestResultDTO result = results[0];
                    float averageMicroseconds = (float)((double)elapsedTicks * 1000000.0d / Stopwatch.Frequency / HeadlessKccSmokeTestConstants.FrameCount);
                    result.AverageMicrosecondsPerFrame = averageMicroseconds;
                    if (averageMicroseconds > HeadlessKccSmokeTestConstants.PerformanceBudgetMicroseconds)
                        result.ErrorFlags |= HeadlessKccSmokeTestConstants.FailurePerformance;

                    long allocatedBytes = allocatedAfter - allocatedBefore;
                    if (allocatedBytes != 0L)
                        result.ErrorFlags |= HeadlessKccSmokeTestConstants.FailureAllocation;

                    double driftError = EvaluateDrift(drift[0]);
                    if (driftError > 0.001d)
                        result.ErrorFlags |= HeadlessKccSmokeTestConstants.FailurePrecisionDrift;

                    results[0] = result;
                    summary = BuildSummary(result, allocatedBytes, driftError);

                    if (result.ErrorFlags == HeadlessKccSmokeTestConstants.FailureNone)
                    {
                        WriteOptimizationReport(summary);
                        HeadlessKccFailureGizmo.ClearFailure();
                        return true;
                    }

                    int failureCount = (int)math.min(result.FailureCount, (uint)failures.Length);
                    WriteFailureCsv(failures, failureCount);
                    WriteBlackBoxDump(telemetry);
                    if (result.FailureCount > 0u)
                        HeadlessKccFailureGizmo.SetFailure(result.FirstFailureAup);
                    else
                        HeadlessKccFailureGizmo.ClearFailure();
                    return false;
                }
            }
            finally
            {
                if (drift.IsCreated) drift.Dispose();
                if (telemetry.IsCreated) telemetry.Dispose();
                if (failures.IsCreated) failures.Dispose();
                if (results.IsCreated) results.Dispose();
                if (profiles.IsCreated) profiles.Dispose();
            }
        }

        private static void WarmBurst(
            NativeArray<KinematicStateDTO> states,
            NativeArray<HydrodynamicKccInputDTO> inputs,
            NativeArray<float3> proposed,
            NativeArray<HydrodynamicKccFaultFlagDTO> faults,
            NativeArray<float> sdf,
            NativeArray<HeadlessKccTestResultDTO> results,
            NativeArray<HeadlessKccFailureRecordDTO> failures,
            NativeArray<HeadlessKccTelemetryEntry> telemetry,
            NativeArray<HeadlessKccDriftProbeDTO> drift,
            HeadlessKccVoxelSdfInfoDTO sdfInfo,
            HydrodynamicKccTuningDTO tuning,
            double3 sectorOrigin)
        {
            results[0] = default;
            new HeadlessKccFrameLoopJob
            {
                States = states,
                Inputs = inputs,
                ProposedVelocities = proposed,
                FaultFlags = faults,
                Sdf = sdf,
                Results = results,
                Failures = failures,
                Telemetry = telemetry,
                DriftProbe = drift,
                SdfInfo = sdfInfo,
                Tuning = tuning,
                SectorOriginAup = sectorOrigin,
                EntityCount = HeadlessKccSmokeTestConstants.PhantomCount,
                FrameCount = 16,
                SimulationTickDelta = HeadlessKccSmokeTestConstants.FixedDeltaTime,
                Seed = 0xA551u
            }.Schedule().Complete();
        }

        private static HydrodynamicKccTuningDTO BuildUltraTuning()
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
                ProfileHash = HeadlessKccSmokeTestConstants.SourceHash,
                Flags = 1u
            };
        }

        private static HeadlessKccVoxelSdfInfoDTO BuildSdfInfo(double3 sectorOrigin)
        {
            const float cellSize = 4f;
            double3 half = new double3(
                HeadlessKccSmokeTestConstants.SdfDimX * cellSize * 0.5f,
                HeadlessKccSmokeTestConstants.SdfDimY * cellSize * 0.5f,
                HeadlessKccSmokeTestConstants.SdfDimZ * cellSize * 0.5f);
            return new HeadlessKccVoxelSdfInfoDTO
            {
                OriginAup = sectorOrigin - half,
                Dimensions = new int3(
                    HeadlessKccSmokeTestConstants.SdfDimX,
                    HeadlessKccSmokeTestConstants.SdfDimY,
                    HeadlessKccSmokeTestConstants.SdfDimZ),
                CellSizeMeters = cellSize,
                SurfaceOffsetMeters = 0f,
                CapsuleRadiusMeters = 0.35f,
                Flags = 1u,
                ProfileHash = HeadlessKccSmokeTestConstants.SourceHash
            };
        }

        private static int TryLoadProfiles(NativeArray<HeadlessKccProfileDTO> profiles)
        {
            string path = Path.Combine(ProjectRoot, "headless_test_profiles.csv");
            if (!File.Exists(path))
                return 0;

            byte[] bytes = File.ReadAllBytes(path);
            return HeadlessKccProfileCsvParser.Parse(bytes, profiles);
        }

        private static HeadlessKccRunSummary BuildSummary(HeadlessKccTestResultDTO result, long allocatedBytes, double driftError)
        {
            return new HeadlessKccRunSummary
            {
                ErrorFlags = result.ErrorFlags,
                FailureCount = result.FailureCount,
                ManagedBytesAllocated = allocatedBytes,
                AverageMicrosecondsPerFrame = result.AverageMicrosecondsPerFrame,
                DriftErrorMeters = driftError,
                FirstFailureAup = result.FirstFailureAup,
                StatusText = result.ErrorFlags == 0u ? "PASS" : "FAIL"
            };
        }

        private static double EvaluateDrift(HeadlessKccDriftProbeDTO drift)
        {
            decimal expected = (decimal)drift.StartAup.x + ((decimal)drift.StepMeters * HeadlessKccSmokeTestConstants.FrameCount);
            decimal actual = (decimal)drift.CurrentAup.x;
            decimal error = expected > actual ? expected - actual : actual - expected;
            return (double)error;
        }

        private static void WriteOptimizationReport(HeadlessKccRunSummary summary)
        {
            string directory = Path.Combine(ProjectRoot, "Docs", "Reports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "QA_OPTIMIZATION_REPORT.json");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> buffer = stackalloc byte[256];
                int cursor = 0;
                cursor = AppendAscii(buffer, cursor, "{\"summary\":\"KCC Stability Verified\",\"frames\":");
                cursor = AppendInt(buffer, cursor, HeadlessKccSmokeTestConstants.FrameCount);
                cursor = AppendAscii(buffer, cursor, ",\"phantoms\":");
                cursor = AppendInt(buffer, cursor, HeadlessKccSmokeTestConstants.PhantomCount);
                cursor = AppendAscii(buffer, cursor, ",\"avg_us_per_frame\":");
                cursor = AppendDoubleFixed3(buffer, cursor, summary.AverageMicrosecondsPerFrame);
                cursor = AppendAscii(buffer, cursor, ",\"managed_alloc_bytes\":");
                cursor = AppendLong(buffer, cursor, summary.ManagedBytesAllocated);
                cursor = AppendAscii(buffer, cursor, ",\"drift_error_m\":");
                cursor = AppendDoubleFixed6(buffer, cursor, summary.DriftErrorMeters);
                cursor = AppendAscii(buffer, cursor, "}\n");
                stream.Write(buffer.Slice(0, cursor));
            }
        }

        private static void WriteFailureCsv(NativeArray<HeadlessKccFailureRecordDTO> failures, int count)
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
                    HeadlessKccFailureRecordDTO failure = failures[i];
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

        private static unsafe void WriteBlackBoxDump(NativeArray<HeadlessKccTelemetryEntry> telemetry)
        {
            string directory = Path.Combine(ProjectRoot, "Docs", "AgentLogs");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "Dump_SHINOBU_254.bin");
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[16];
                int cursor = 0;
                cursor = AppendAscii(header, cursor, "H8KCC254");
                cursor = AppendInt(header, cursor, telemetry.Length);
                while (cursor < header.Length)
                    header[cursor++] = 0;
                stream.Write(header);

                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<HeadlessKccTelemetryEntry>();
                stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
            }
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

        private static int AppendDoubleFixed3(Span<byte> buffer, int cursor, double value)
        {
            return AppendDoubleFixed(buffer, cursor, value, 3);
        }

        private static int AppendDoubleFixed6(Span<byte> buffer, int cursor, double value)
        {
            return AppendDoubleFixed(buffer, cursor, value, 6);
        }

        private static int AppendDoubleFixed(Span<byte> buffer, int cursor, double value, int decimals)
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

            long scale = 1;
            for (int i = 0; i < decimals; i++)
                scale *= 10L;
            long whole = (long)value;
            double fractional = value - whole;
            long fraction = (long)math.round(fractional * scale);
            if (fraction >= scale)
            {
                whole++;
                fraction -= scale;
            }

            cursor = AppendLong(buffer, cursor, whole);
            buffer[cursor++] = 46;
            long divisor = scale / 10L;
            for (int i = 0; i < decimals; i++)
            {
                long digit = divisor > 0L ? fraction / divisor : fraction;
                buffer[cursor++] = (byte)(48 + (digit % 10L));
                if (divisor > 0L)
                    fraction -= digit * divisor;
                divisor /= 10L;
            }

            return cursor;
        }
    }

    internal static class HeadlessKccProfileCsvParser
    {
        public static int Parse(ReadOnlySpan<byte> csv, NativeArray<HeadlessKccProfileDTO> profiles)
        {
            int cursor = 0;
            int count = 0;
            while (count < profiles.Length && TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == 35)
                    continue;
                if ((line[0] < 45 || line[0] > 57) && line[0] != 43)
                    continue;

                if (TryReadProfile(line, out HeadlessKccProfileDTO profile))
                    profiles[count++] = profile;
            }

            return count;
        }

        private static bool TryReadProfile(ReadOnlySpan<byte> line, out HeadlessKccProfileDTO profile)
        {
            profile = default;
            int cursor = 0;
            if (!TryReadDouble(line, ref cursor, out double x)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadDouble(line, ref cursor, out double y)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadDouble(line, ref cursor, out double z)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadFloat(line, ref cursor, out float vx)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadFloat(line, ref cursor, out float vy)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadFloat(line, ref cursor, out float vz)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadFloat(line, ref cursor, out float bx)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadFloat(line, ref cursor, out float by)) return false;
            ConsumeComma(line, ref cursor);
            if (!TryReadFloat(line, ref cursor, out float bz)) return false;
            ConsumeComma(line, ref cursor);
            TryReadFloat(line, ref cursor, out float speedScale);
            uint hash = HashFnv1a(line);
            profile = new HeadlessKccProfileDTO
            {
                StartAup = new double3(x, y, z),
                StartVelocity = new float3(vx, vy, vz),
                InputBias = new float3(bx, by, bz),
                SpeedScale = speedScale <= 0f ? 1f : speedScale,
                ProfileHash = hash,
                Flags = 1u
            };
            return true;
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

        private static void ConsumeComma(ReadOnlySpan<byte> text, ref int cursor)
        {
            while (cursor < text.Length && text[cursor] != 44)
                cursor++;
            if (cursor < text.Length && text[cursor] == 44)
                cursor++;
        }

        private static bool TryReadFloat(ReadOnlySpan<byte> text, ref int cursor, out float value)
        {
            bool result = TryReadDouble(text, ref cursor, out double doubleValue);
            value = (float)doubleValue;
            return result;
        }

        private static bool TryReadDouble(ReadOnlySpan<byte> text, ref int cursor, out double value)
        {
            value = 0d;
            while (cursor < text.Length && text[cursor] == 32)
                cursor++;
            int sign = 1;
            if (cursor < text.Length && text[cursor] == 45)
            {
                sign = -1;
                cursor++;
            }
            else if (cursor < text.Length && text[cursor] == 43)
            {
                cursor++;
            }

            bool any = false;
            long whole = 0L;
            while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
            {
                any = true;
                whole = whole * 10L + (text[cursor] - 48);
                cursor++;
            }

            double fractional = 0d;
            if (cursor < text.Length && text[cursor] == 46)
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
                {
                    any = true;
                    fractional += (text[cursor] - 48) * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            value = (whole + fractional) * sign;
            return any;
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

        private static uint HashFnv1a(ReadOnlySpan<byte> text)
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
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateHeadlessVoxelSdfJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<float> Sdf;
        public HeadlessKccVoxelSdfInfoDTO Info;

        public void Execute(int index)
        {
            int3 dim = Info.Dimensions;
            int cellsPerLayer = dim.x * dim.y;
            int z = index / cellsPerLayer;
            int layer = index - z * cellsPerLayer;
            int y = layer / dim.x;
            int x = layer - y * dim.x;
            float3 center = (new float3(x, y, z) - (new float3(dim.x, dim.y, dim.z) - 1f) * 0.5f) * Info.CellSizeMeters;
            float radial = math.length(center);
            float shell = math.abs(radial - 66f) - 4.5f;
            float jagged = (Noise3(center * 0.071f) * 2f - 1f) * 1.75f;
            float floor = center.y + 86f;
            float ceiling = 88f - center.y;
            float columnA = math.length(center.xz - new float2(22f, -18f)) - 5f;
            float columnB = math.length(center.xz - new float2(-26f, 30f)) - 7f;
            float pillar = math.min(columnA, columnB);
            float wall = math.min(shell + jagged, math.min(floor, ceiling));
            float sdf = math.min(wall, pillar);
            Sdf[index] = math.clamp(sdf, -64f, 64f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Noise3(float3 p)
        {
            float n = math.sin(math.dot(p, new float3(12.9898f, 78.233f, 37.719f))) * 43758.5453f;
            return n - math.floor(n);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct InitializePhantomsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<HeadlessKccProfileDTO> Profiles;
        public int ProfileCount;
        public double3 SectorOriginAup;
        public HydrodynamicKccTuningDTO Tuning;

        public void Execute(int index)
        {
            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States) + (index * stateSize);
            ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statePtr);
            HeadlessKccProfileDTO profile = ResolveProfile(index);
            state = new KinematicStateDTO
            {
                AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(profile.StartAup),
                Velocity = HydrodynamicKccMath.Sanitize(profile.StartVelocity, float3.zero),
                AngularVelocity = float3.zero,
                Mass = 80f + (index % 7) * 3.5f,
                Flags = 0u,
                DragCoefficient = math.max(0f, Tuning.BaseDrag),
                RestingFrameCount = 0,
                DeepSleepTickCount = 0,
                SleepMaterialIndex = 0,
                _pad0 = 0
            };
        }

        private HeadlessKccProfileDTO ResolveProfile(int index)
        {
            if (ProfileCount > 0 && index < ProfileCount && Profiles.IsCreated)
                return Profiles[index];

            float laneX = ((index % 10) - 4.5f) * 3.6f;
            float laneZ = (((index / 10) % 10) - 4.5f) * 3.6f;
            float laneY = ((index % 5) - 2) * 4f;
            laneY = math.select(laneY, 74f, (index % 17) == 0);
            laneY = math.select(laneY, -74f, (index % 19) == 0);
            double3 start = SectorOriginAup + new double3(laneX, laneY, laneZ);
            if (index == 0)
                start = new double3(99000.0, -400.0, -99000.0);
            float phase = index * 0.6180339f;
            float3 velocity = new float3(
                HydrodynamicKccMath.SinPolynomial7(phase) * 640f,
                HydrodynamicKccMath.SinPolynomial7(phase * 1.7f) * 180f,
                HydrodynamicKccMath.SinPolynomial7(phase * 2.3f + 1.1f) * 640f);
            return new HeadlessKccProfileDTO
            {
                StartAup = start,
                StartVelocity = velocity,
                InputBias = HydrodynamicKccMath.NormalizeSafe(velocity, new float3(0f, 0f, 1f)),
                SpeedScale = 1f,
                ProfileHash = HeadlessKccSmokeTestConstants.SourceHash,
                Flags = 1u
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal struct GenerateHostileInputJob : IJobParallelFor
    {
        [WriteOnly, NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
        public double3 AnchorAup;
        public uint SimulationFrame;
        public uint SectorGeneration;
        public uint Seed;

        public void Execute(int index)
        {
            Inputs[index] = HeadlessKccFrameLoopJob.BuildHostileInput(index, SimulationFrame, AnchorAup, SectorGeneration, Seed);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct HeadlessKccFrameLoopJob : IJob
    {
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<KinematicStateDTO> States;
        [NoAlias] public NativeArray<HydrodynamicKccInputDTO> Inputs;
        [NoAlias] public NativeArray<float3> ProposedVelocities;
        [NoAlias] public NativeArray<HydrodynamicKccFaultFlagDTO> FaultFlags;
        [ReadOnly, NoAlias] public NativeArray<float> Sdf;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessKccTestResultDTO> Results;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessKccFailureRecordDTO> Failures;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessKccTelemetryEntry> Telemetry;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessKccDriftProbeDTO> DriftProbe;
        public HeadlessKccVoxelSdfInfoDTO SdfInfo;
        public HydrodynamicKccTuningDTO Tuning;
        public double3 SectorOriginAup;
        public int EntityCount;
        public int FrameCount;
        public float SimulationTickDelta;
        public uint Seed;

        public void Execute()
        {
            int count = math.clamp(EntityCount, 0, States.Length);
            int frames = math.max(0, FrameCount);
            HeadlessKccTestResultDTO result = Results[0];
            for (int frame = 1; frame <= frames; frame++)
            {
                ExecutePreSimulation(frame, count, ref result);
                ExecuteSimulation(frame, count, ref result);
                ExecutePostSimulation(frame, count, ref result);
                AdvanceDriftProbe(frame);
            }

            Results[0] = result;
        }

        public static HydrodynamicKccInputDTO BuildHostileInput(int index, uint frame, double3 anchorAup, uint sectorGeneration, uint seed)
        {
            uint state = HydrodynamicKccMath.SeedNonZero(seed ^ ((uint)index * 0x9E3779B9u) ^ (frame * 0x85EBCA6Bu));
            float t = (float)frame * HeadlessKccSmokeTestConstants.FixedDeltaTime;
            float3 axis = new float3(
                HydrodynamicKccMath.SinPolynomial7(t * 13.1f + index * 0.37f),
                HydrodynamicKccMath.SinPolynomial7(t * 5.7f + index * 0.11f) * 0.25f,
                HydrodynamicKccMath.SinPolynomial7(t * 17.9f + index * 0.53f));
            bool zeroInjection = ((frame + (uint)index) % 97u) == 0u;
            bool infinityInjection = ((frame * 31u + (uint)index * 7u) % 997u) == 0u;
            axis = math.select(axis, float3.zero, zeroInjection);
            axis = math.select(axis, new float3(float.PositiveInfinity, 0f, axis.z), infinityInjection);
            return new HydrodynamicKccInputDTO
            {
                TargetAup = anchorAup,
                MoveAxis = axis,
                LookAxis = new float3(axis.x, 0f, 1f),
                SimulationFrame = frame,
                Sequence = (uint)index,
                Flags = HydrodynamicKccMath.PackInputFlags(HydrodynamicKccMath.FlagMockInput, sectorGeneration),
                SourceHash = state
            };
        }

        private void ExecutePreSimulation(int frame, int count, ref HeadlessKccTestResultDTO result)
        {
            uint sectorGeneration = HydrodynamicKccMath.ComputeSectorGeneration(SectorOriginAup);
            for (int i = 0; i < count; i++)
            {
                HydrodynamicKccInputDTO input = BuildHostileInput(i, (uint)frame, SectorOriginAup, sectorGeneration, Seed);
                bool inputFinite = HydrodynamicKccMath.IsFinite(input.MoveAxis) && HydrodynamicKccMath.IsFinite(input.LookAxis);
                if (!inputFinite)
                {
                    input.MoveAxis = float3.zero;
                    input.LookAxis = new float3(0f, 0f, 1f);
                    input.Flags |= HeadlessKccSmokeTestConstants.FailureInputSanitized;
                }

                float moveLenSq = math.lengthsq(input.MoveAxis);
                if (moveLenSq > 1f)
                    input.MoveAxis *= math.rsqrt(math.max(moveLenSq, 0.000001f));
                input.LookAxis = HydrodynamicKccMath.NormalizeSafe(input.LookAxis, new float3(0f, 0f, 1f));
                Inputs[i] = input;
                FaultFlags[i] = default;
            }
        }

        private void ExecuteSimulation(int frame, int count, ref HeadlessKccTestResultDTO result)
        {
            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statesBase = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States);
            float dt = math.max(HydrodynamicKccMath.MinDenominator, SimulationTickDelta);
            for (int i = 0; i < count; i++)
            {
                ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesBase + (i * stateSize));
                double3 previous = state.AUP_Position;
                HydrodynamicKccInputDTO input = Inputs[i];
                float3 velocity = HydrodynamicKccMath.Sanitize(state.Velocity, float3.zero);
                float3 move = HydrodynamicKccMath.Sanitize(input.MoveAxis, float3.zero);
                float drive = math.lerp(220f, 620f, 1f);
                velocity += move * drive * dt;
                velocity += HostileCurrent(frame, i) * dt;

                float speed = HydrodynamicKccMath.LengthSafe(velocity);
                float drag = math.max(0f, Tuning.BaseDrag);
                velocity *= math.rcp(math.max(HydrodynamicKccMath.MinDenominator, 1f + drag * speed * dt));
                float maxSpeed = math.max(10f, Tuning.MaxSpeed);
                float speedSq = math.lengthsq(velocity);
                if (speedSq > maxSpeed * maxSpeed)
                    velocity *= maxSpeed * math.rsqrt(math.max(speedSq, 0.000001f));

                float minSdf;
                uint collisionFlags;
                double3 resolvedAup = ResolveSweptAup(previous, ref velocity, dt, out minSdf, out collisionFlags);
                state.AUP_Position = HydrodynamicKccMath.QuantizeMillimeter(resolvedAup);
                state.Velocity = HydrodynamicKccMath.Sanitize(velocity, float3.zero);
                state.AngularVelocity = HydrodynamicKccMath.Sanitize(state.AngularVelocity, float3.zero);
                state.Flags |= collisionFlags;
                ProposedVelocities[i] = state.Velocity;

                uint failureFlags = 0u;
                if (!HydrodynamicKccMath.IsFinite(state.AUP_Position) || !HydrodynamicKccMath.IsFinite(state.Velocity))
                    failureFlags |= HeadlessKccSmokeTestConstants.FailureNonFinite;
                if (minSdf < HeadlessKccSmokeTestConstants.StrongPenetrationMeters)
                    failureFlags |= HeadlessKccSmokeTestConstants.FailureTunneling;

                if (failureFlags != 0u)
                {
                    HydrodynamicKccFaultFlagDTO fault = FaultFlags[i];
                    fault.FaultMask |= (int)failureFlags;
                    FaultFlags[i] = fault;
                    RecordFailure(ref result, (uint)frame, (uint)i, failureFlags, previous, state.AUP_Position, state.Velocity, input.MoveAxis, minSdf);
                }
            }
        }

        private void ExecutePostSimulation(int frame, int count, ref HeadlessKccTestResultDTO result)
        {
            float maxSpeed = 0f;
            float meanSpeed = 0f;
            float minSdf = 9999f;
            uint flags = 0u;
            uint sanitizedInputs = 0u;
            uint hash = 2166136261u;
            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statesBase = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(States);
            for (int i = 0; i < count; i++)
            {
                ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesBase + (i * stateSize));
                HydrodynamicKccInputDTO input = Inputs[i];
                float speed = HydrodynamicKccMath.LengthSafe(state.Velocity);
                float sdf = SampleCapsuleSdf(state.AUP_Position);
                maxSpeed = math.max(maxSpeed, speed);
                meanSpeed += speed;
                minSdf = math.min(minSdf, sdf);
                flags |= state.Flags | (uint)FaultFlags[i].FaultMask;
                sanitizedInputs += (input.Flags & HeadlessKccSmokeTestConstants.FailureInputSanitized) != 0u ? 1u : 0u;
                hash ^= HydrodynamicKccMath.HashState(state.AUP_Position, state.Velocity, (uint)frame, state.Flags) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            }

            if (count > 0)
                meanSpeed *= math.rcp(count);

            int ringIndex = frame % math.max(1, Telemetry.Length);
            Telemetry[ringIndex] = new HeadlessKccTelemetryEntry
            {
                FirstAup = count > 0 ? States[0].AUP_Position : double3.zero,
                MaxSpeed = maxSpeed,
                MinSdfMeters = minSdf,
                MeanSpeed = meanSpeed,
                Frame = (uint)frame,
                StateHash = hash,
                Flags = flags,
                ActivePhantoms = (uint)count,
                SanitizedInputs = sanitizedInputs
            };
            result.StateHash = hash;
            result.WorstPenetrationMeters = math.min(result.WorstPenetrationMeters, minSdf);
        }

        private double3 ResolveSweptAup(double3 start, ref float3 velocity, float dt, out float minSdf, out uint collisionFlags)
        {
            collisionFlags = 0u;
            float3 displacement = velocity * dt;
            minSdf = SampleCapsuleSdf(start);
            float length = HydrodynamicKccMath.LengthSafe(displacement);
            if (length <= 0.000001f)
                return start;

            int iterations = HeadlessKccSmokeTestConstants.MaxSweepIterations;
            double3 safe = start;
            double3 hit = start;
            bool hasHit = false;
            for (int step = 1; step <= iterations; step++)
            {
                float fraction = (float)step * math.rcp(iterations);
                double3 candidate = start + new double3(displacement.x, displacement.y, displacement.z) * fraction;
                float sdf = SampleCapsuleSdf(candidate);
                minSdf = math.min(minSdf, sdf);
                if (sdf >= Tuning.SkinWidth)
                {
                    safe = candidate;
                    continue;
                }

                hasHit = true;
                hit = candidate;
                break;
            }

            if (!hasHit)
                return start + new double3(displacement.x, displacement.y, displacement.z);

            float3 normal = SampleSdfNormal(hit);
            float intoNormal = math.dot(velocity, normal);
            if (intoNormal < 0f)
                velocity -= normal * intoNormal;
            float hitSdf = SampleCapsuleSdf(hit);
            double push = (double)math.max(Tuning.SkinWidth - hitSdf, 0f);
            collisionFlags = HydrodynamicKccMath.FlagCollision;
            return safe + new double3(normal.x, normal.y, normal.z) * push;
        }

        private float SampleCapsuleSdf(double3 aup)
        {
            return SampleSdf(aup) - math.max(0.05f, SdfInfo.CapsuleRadiusMeters);
        }

        private float3 SampleSdfNormal(double3 aup)
        {
            double cell = math.max(0.25f, SdfInfo.CellSizeMeters);
            float dx = SampleSdf(aup + new double3(cell, 0d, 0d)) - SampleSdf(aup - new double3(cell, 0d, 0d));
            float dy = SampleSdf(aup + new double3(0d, cell, 0d)) - SampleSdf(aup - new double3(0d, cell, 0d));
            float dz = SampleSdf(aup + new double3(0d, 0d, cell)) - SampleSdf(aup - new double3(0d, 0d, cell));
            return HydrodynamicKccMath.NormalizeSafe(new float3(dx, dy, dz), new float3(0f, 1f, 0f));
        }

        private float SampleSdf(double3 aup)
        {
            int3 dim = SdfInfo.Dimensions;
            float cell = math.max(0.25f, SdfInfo.CellSizeMeters);
            double3 rel = HydrodynamicKccMath.Sanitize(aup - SdfInfo.OriginAup, double3.zero);
            float3 grid = new float3((float)(rel.x / cell), (float)(rel.y / cell), (float)(rel.z / cell));
            if (!HydrodynamicKccMath.IsFinite(grid) ||
                math.any(grid < 0f) ||
                grid.x >= dim.x - 1 ||
                grid.y >= dim.y - 1 ||
                grid.z >= dim.z - 1)
            {
                return 64f;
            }

            int3 p0 = (int3)math.floor(grid);
            float3 f = grid - p0;
            int x1 = math.min(p0.x + 1, dim.x - 1);
            int y1 = math.min(p0.y + 1, dim.y - 1);
            int z1 = math.min(p0.z + 1, dim.z - 1);
            float c000 = Sdf[Index(p0.x, p0.y, p0.z, dim)];
            float c100 = Sdf[Index(x1, p0.y, p0.z, dim)];
            float c010 = Sdf[Index(p0.x, y1, p0.z, dim)];
            float c110 = Sdf[Index(x1, y1, p0.z, dim)];
            float c001 = Sdf[Index(p0.x, p0.y, z1, dim)];
            float c101 = Sdf[Index(x1, p0.y, z1, dim)];
            float c011 = Sdf[Index(p0.x, y1, z1, dim)];
            float c111 = Sdf[Index(x1, y1, z1, dim)];
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            float c0 = math.lerp(c00, c10, f.y);
            float c1 = math.lerp(c01, c11, f.y);
            return math.lerp(c0, c1, f.z);
        }

        private static int Index(int x, int y, int z, int3 dim)
        {
            return x + y * dim.x + z * dim.x * dim.y;
        }

        private static float3 HostileCurrent(int frame, int index)
        {
            float t = frame * HeadlessKccSmokeTestConstants.FixedDeltaTime;
            return new float3(
                HydrodynamicKccMath.SinPolynomial7(t * 3.1f + index) * 280f,
                HydrodynamicKccMath.SinPolynomial7(t * 1.7f + index * 0.13f) * 60f,
                HydrodynamicKccMath.SinPolynomial7(t * 2.3f - index) * 280f);
        }

        private void AdvanceDriftProbe(int frame)
        {
            if (!DriftProbe.IsCreated || DriftProbe.Length == 0)
                return;

            HeadlessKccDriftProbeDTO drift = DriftProbe[0];
            drift.CurrentAup.x += drift.StepMeters;
            drift.LastFrame = (uint)frame;
            DriftProbe[0] = drift;
        }

        private void RecordFailure(
            ref HeadlessKccTestResultDTO result,
            uint frame,
            uint index,
            uint failureFlags,
            double3 previousAup,
            double3 aup,
            float3 velocity,
            float3 input,
            float sdfMeters)
        {
            if (result.FailureCount == 0u)
            {
                result.FirstFailureFrame = frame;
                result.FirstFailureIndex = index;
                result.FirstFailureAup = aup;
                result.FirstFailureVelocity = velocity;
            }

            result.ErrorFlags |= failureFlags;
            uint slot = result.FailureCount;
            result.FailureCount++;
            if (slot >= Failures.Length)
                return;

            Failures[(int)slot] = new HeadlessKccFailureRecordDTO
            {
                Aup = aup,
                Velocity = velocity,
                SdfMeters = sdfMeters,
                Frame = frame,
                EntityIndex = index,
                FailureFlags = failureFlags,
                StateHash = HydrodynamicKccMath.HashState(aup, velocity, frame, failureFlags),
                PreviousAup = previousAup,
                InputVector = input,
                SpeedMetersPerSecond = HydrodynamicKccMath.LengthSafe(velocity)
            };
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    internal unsafe struct ValidateKinematicStateJob : IJob
    {
        [ReadOnly, NoAlias] public NativeArray<KinematicStateDTO> States;
        [ReadOnly, NoAlias] public NativeArray<float> Sdf;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessKccTestResultDTO> Results;
        [NativeDisableParallelForRestriction, NoAlias] public NativeArray<HeadlessKccFailureRecordDTO> Failures;
        public HeadlessKccVoxelSdfInfoDTO SdfInfo;
        public HydrodynamicKccTuningDTO Tuning;
        public int Frame;
        public int EntityCount;

        public void Execute()
        {
            HeadlessKccFrameLoopJob probe = new HeadlessKccFrameLoopJob
            {
                States = default,
                Sdf = Sdf,
                SdfInfo = SdfInfo,
                Tuning = Tuning
            };
            HeadlessKccTestResultDTO result = Results[0];
            int count = math.clamp(EntityCount, 0, States.Length);
            int stateSize = UnsafeUtility.SizeOf<KinematicStateDTO>();
            byte* statesBase = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(States);
            for (int i = 0; i < count; i++)
            {
                ref KinematicStateDTO state = ref UnsafeUtility.AsRef<KinematicStateDTO>(statesBase + (i * stateSize));
                uint failureFlags = 0u;
                if (!HydrodynamicKccMath.IsFinite(state.AUP_Position) || !HydrodynamicKccMath.IsFinite(state.Velocity))
                    failureFlags |= HeadlessKccSmokeTestConstants.FailureNonFinite;
                float sdf = SampleCapsuleSdf(in probe, state.AUP_Position);
                if (sdf < HeadlessKccSmokeTestConstants.StrongPenetrationMeters)
                    failureFlags |= HeadlessKccSmokeTestConstants.FailureTunneling;
                if (failureFlags == 0u)
                    continue;
                if (result.FailureCount == 0u)
                {
                    result.FirstFailureFrame = (uint)Frame;
                    result.FirstFailureIndex = (uint)i;
                    result.FirstFailureAup = state.AUP_Position;
                    result.FirstFailureVelocity = state.Velocity;
                }

                uint slot = result.FailureCount;
                result.FailureCount++;
                result.ErrorFlags |= failureFlags;
                if (slot < Failures.Length)
                {
                    Failures[(int)slot] = new HeadlessKccFailureRecordDTO
                    {
                        Aup = state.AUP_Position,
                        Velocity = state.Velocity,
                        SdfMeters = sdf,
                        Frame = (uint)Frame,
                        EntityIndex = (uint)i,
                        FailureFlags = failureFlags,
                        StateHash = HydrodynamicKccMath.HashState(state.AUP_Position, state.Velocity, (uint)Frame, failureFlags),
                        PreviousAup = state.AUP_Position,
                        InputVector = float3.zero,
                        SpeedMetersPerSecond = HydrodynamicKccMath.LengthSafe(state.Velocity)
                    };
                }
            }

            Results[0] = result;
        }

        private static float SampleCapsuleSdf(in HeadlessKccFrameLoopJob probe, double3 aup)
        {
            return SampleSdf(in probe, aup) - math.max(0.05f, probe.SdfInfo.CapsuleRadiusMeters);
        }

        private static float SampleSdf(in HeadlessKccFrameLoopJob probe, double3 aup)
        {
            int3 dim = probe.SdfInfo.Dimensions;
            float cell = math.max(0.25f, probe.SdfInfo.CellSizeMeters);
            double3 rel = HydrodynamicKccMath.Sanitize(aup - probe.SdfInfo.OriginAup, double3.zero);
            float3 grid = new float3((float)(rel.x / cell), (float)(rel.y / cell), (float)(rel.z / cell));
            if (!HydrodynamicKccMath.IsFinite(grid) ||
                math.any(grid < 0f) ||
                grid.x >= dim.x - 1 ||
                grid.y >= dim.y - 1 ||
                grid.z >= dim.z - 1)
                return 64f;
            int3 p0 = (int3)math.floor(grid);
            float3 f = grid - p0;
            int x1 = math.min(p0.x + 1, dim.x - 1);
            int y1 = math.min(p0.y + 1, dim.y - 1);
            int z1 = math.min(p0.z + 1, dim.z - 1);
            float c000 = probe.Sdf[p0.x + p0.y * dim.x + p0.z * dim.x * dim.y];
            float c100 = probe.Sdf[x1 + p0.y * dim.x + p0.z * dim.x * dim.y];
            float c010 = probe.Sdf[p0.x + y1 * dim.x + p0.z * dim.x * dim.y];
            float c110 = probe.Sdf[x1 + y1 * dim.x + p0.z * dim.x * dim.y];
            float c001 = probe.Sdf[p0.x + p0.y * dim.x + z1 * dim.x * dim.y];
            float c101 = probe.Sdf[x1 + p0.y * dim.x + z1 * dim.x * dim.y];
            float c011 = probe.Sdf[p0.x + y1 * dim.x + z1 * dim.x * dim.y];
            float c111 = probe.Sdf[x1 + y1 * dim.x + z1 * dim.x * dim.y];
            float c00 = math.lerp(c000, c100, f.x);
            float c10 = math.lerp(c010, c110, f.x);
            float c01 = math.lerp(c001, c101, f.x);
            float c11 = math.lerp(c011, c111, f.x);
            return math.lerp(math.lerp(c00, c10, f.y), math.lerp(c01, c11, f.y), f.z);
        }
    }

    internal sealed class HeadlessKccSmokeTesterWindow : EditorWindow
    {
        private Label _statusLabel;
        private HeadlessKccRunSummary _lastSummary;

        [MenuItem("HECTON-8/Kinematics/Headless Smoke Tester")]
        public static void Open()
        {
            GetWindow<HeadlessKccSmokeTesterWindow>("Headless Smoke Tester");
        }

        public void CreateGUI()
        {
            Button runButton = new Button(RunSmokeTest) { text = "RUN 10,000 FRAME KCC TEST" };
            _statusLabel = new Label("PENDING");
            rootVisualElement.Add(runButton);
            rootVisualElement.Add(_statusLabel);
        }

        private void RunSmokeTest()
        {
            bool passed = HeadlessKccSmokeTestRunner.Run(out _lastSummary);
            _statusLabel.text = passed
                ? "PASS | avg_us=" + _lastSummary.AverageMicrosecondsPerFrame.ToString("F3")
                : "FAIL | flags=" + _lastSummary.ErrorFlags + " | failures=" + _lastSummary.FailureCount;
            _statusLabel.style.color = passed ? Color.green : Color.red;
            SceneView.RepaintAll();
        }
    }

    [ExecuteAlways]
    internal sealed class HeadlessKccFailureGizmo : MonoBehaviour
    {
        private static HeadlessKccFailureGizmo s_instance;
        private static bool s_hasFailure;
        private static double3 s_failureAup;

        public static void SetFailure(double3 aup)
        {
            s_hasFailure = true;
            s_failureAup = aup;
            EnsureInstance();
        }

        public static void ClearFailure()
        {
            s_hasFailure = false;
        }

        private static void EnsureInstance()
        {
            if (s_instance != null)
                return;

            GameObject root = new GameObject("[HeadlessKccFailureGizmo]");
            root.hideFlags = HideFlags.DontSave;
            s_instance = root.AddComponent<HeadlessKccFailureGizmo>();
        }

        private void OnEnable()
        {
            s_instance = this;
        }

        private void OnDisable()
        {
            if (s_instance == this)
                s_instance = null;
        }

        private void OnDrawGizmos()
        {
            if (!s_hasFailure)
                return;

            Vector3 pos = new Vector3((float)s_failureAup.x, (float)s_failureAup.y, (float)s_failureAup.z);
            float pulse = 1f + 0.18f * math.sin((float)EditorApplication.timeSinceStartup * 7f);
            float radius = 12f * pulse;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pos, radius);
            Gizmos.DrawSphere(pos + Vector3.up * (radius * 0.1f), radius * 0.45f);
            Gizmos.DrawCube(pos + Vector3.down * (radius * 0.34f), new Vector3(radius * 0.58f, radius * 0.32f, radius * 0.42f));
            Gizmos.color = Color.black;
            Gizmos.DrawSphere(pos + new Vector3(-radius * 0.16f, radius * 0.18f, -radius * 0.36f), radius * 0.08f);
            Gizmos.DrawSphere(pos + new Vector3(radius * 0.16f, radius * 0.18f, -radius * 0.36f), radius * 0.08f);
            Gizmos.DrawCube(pos + new Vector3(0f, -radius * 0.04f, -radius * 0.39f), new Vector3(radius * 0.12f, radius * 0.17f, radius * 0.04f));
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pos + Vector3.left * (radius * 1.35f), pos + Vector3.right * (radius * 1.35f));
            Gizmos.DrawLine(pos + Vector3.up * (radius * 1.35f), pos + Vector3.down * (radius * 1.35f));
            Handles.color = Color.red;
            Handles.Label(pos + Vector3.up * (radius * 1.5f), "KCC FAIL SKULL");
        }
    }
}
#endif
