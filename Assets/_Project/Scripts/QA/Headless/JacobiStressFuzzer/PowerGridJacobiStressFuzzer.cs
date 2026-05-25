using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Power
{
    public static class PowerJacobiStressFuzzerConstants
    {
        public const int DefaultNodeCount = 5000;
        public const int DefaultFrameCount = 1000;
        public const int MinimumSolverIterationCount = 2;
        public const int MaximumSolverIterationCount = 50;
        public const int DefaultSolverIterationCount = MaximumSolverIterationCount;
        public const int DefaultEdgeCapacity = 20000;
        public const int DefaultBatchSize = 64;
        public const int TelemetryFrameCount = 300;
        public const int CsvScratchBytes = 64 * 1024;
        public const int FailureFlagDivergence = 1 << 0;
        public const int FailureFlagOscillation = 1 << 1;
        public const int FailureFlagMathCorruption = 1 << 2;
        public const int FailureFlagPerformance = 1 << 3;
        public const int FailureFlagThermodynamic = 1 << 4;
        public const int FailureFlagManagedAllocation = 1 << 5;
        public const int FailureFlagLayout = 1 << 6;
        public const int FailureFlagCapacity = 1 << 7;
        public const int FailureFlagRemainderDrift = 1 << 8;
        public const int FailureFlagRollbackDesync = 1 << 9;
        public const int FailureFlagEarlyConverged = 1 << 10;
        public const int FailureFlagInfiniteDivergence = FailureFlagDivergence;
        public const int FailureFlagNanVoltageDetected = FailureFlagMathCorruption;
        public const int FuzzPowerNodeDtoSizeBytes = 32;
        public const uint NodeFlagActive = 1u << 0;
        public const uint NodeFlagSource = 1u << 1;
        public const uint NodeFlagBattery = 1u << 2;
        public const uint NodeFlagBrownout = 1u << 3;
        public const uint NodeFlagOffline = 1u << 4;
        public const uint NodeFlagDamaged = 1u << 5;
        public const uint DumpMagic0 = 0x464A3848u;
        public const uint DumpMagic1 = 0x44363533u;
        public const uint DumpVersion = 1u;
        public const float BrownoutThreshold01 = 0.15f;
        public const float MinimumConductance = 0.000001f;
        public const float DefaultResidualTolerance = 0.025f;
        public const float DefaultEnergyEpsilon = 0.5f;
        public const float DefaultPerformanceLimitMicroseconds = 500000f;
        public const float MaxVoltageThreshold = 16f;
        public const float MaximumConductance = 4096f;
        public const float MaximumEdgeCurrentAbs = MaximumConductance;
        public const float MaximumNetCurrentAbs = 1048576f;
        public const float RemainderDriftEpsilon = 0.001f;
        public const float OmegaMin = 0.55f;
        public const float OmegaMax = 0.92f;
        public const uint ProfileFlagInjectRawFaults = 1u << 0;
        public const uint ProfileFlagInjectCorruptNodeDto = 1u << 1;
        public const uint ProfileFlagForensicFaults =
            ProfileFlagInjectRawFaults |
            ProfileFlagInjectCorruptNodeDto;
    }

    public static class PowerJacobiStressFuzzerBufferIds
    {
        public const BufferID Nodes = (BufferID)35610;
        public const BufferID NodeAup = (BufferID)35611;
        public const BufferID CsrOffsets = (BufferID)35612;
        public const BufferID CsrDestinations = (BufferID)35613;
        public const BufferID CsrConductance = (BufferID)35614;
        public const BufferID CsrFlow = (BufferID)35615;
        public const BufferID PotentialFront = (BufferID)35616;
        public const BufferID PotentialBack = (BufferID)35617;
        public const BufferID DemandRate = (BufferID)35618;
        public const BufferID BatteryRemainder = (BufferID)35619;
        public const BufferID Result = (BufferID)35620;
        public const BufferID StressTelemetry = (BufferID)35621;
        public const BufferID GraphCounts = (BufferID)35622;
        public const BufferID CsvScratch = (BufferID)35623;
        public const BufferID VoltageHistory = (BufferID)35624;
        public const BufferID RollbackFront = (BufferID)35625;
        public const BufferID RollbackBack = (BufferID)35626;
        public const BufferID FuzzState = (BufferID)35627;
        public const BufferID FuzzTelemetry = (BufferID)35628;
        public const BufferID TopologyProfile = (BufferID)35629;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct PowerJacobiStressTopologyProfile
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public int NodeCount;
        [FieldOffset(8)] public int EdgeCapacity;
        [FieldOffset(12)] public float LoopRatio01;
        [FieldOffset(16)] public float StarRatio01;
        [FieldOffset(20)] public float IslandRatio01;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct JacobiFuzzPowerNodeDTO
    {
        [FieldOffset(0)] public uint NodeHash;
        [FieldOffset(4)] public float Potential;
        [FieldOffset(8)] public float MaxCapacity;
        [FieldOffset(12)] public float CurrentStorage;
        [FieldOffset(16)] public uint Flags;
        [FieldOffset(20)] public float InternalResistance;
        [FieldOffset(24)] public uint Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct JacobiFuzzStateDTO
    {
        [FieldOffset(0)] public float HighestResidualRecorded;
        [FieldOffset(4)] public uint FinalIterationCount;
        [FieldOffset(8)] public uint MismatchFlags;
        [FieldOffset(12)] private uint _pad0;
        [FieldOffset(16)] private uint _pad1;
        [FieldOffset(20)] private uint _pad2;
        [FieldOffset(24)] private uint _pad3;
        [FieldOffset(28)] private uint _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct JacobiFuzzTelemetryEntry
    {
        [FieldOffset(0)] public uint IterationIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint MismatchFlags;
        [FieldOffset(12)] public uint MitigationCount;
        [FieldOffset(16)] public float HighestResidual;
        [FieldOffset(20)] public float PreviousResidual;
        [FieldOffset(24)] public float ActiveOmega;
        [FieldOffset(28)] public float SolverMicroseconds;
        [FieldOffset(32)] public float TotalEnergy;
        [FieldOffset(36)] public float RemainderDrift;
        [FieldOffset(40)] public int FirstBadNodeIndex;
        [FieldOffset(44)] public uint FirstBadNodeHash;
        [FieldOffset(48)] public int FailingArrayOffset;
        [FieldOffset(52)] public uint RollbackHash;
        [FieldOffset(56)] public uint BrownoutNodeId;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PowerJacobiStressDumpHeader
    {
        [FieldOffset(0)] public uint Magic0;
        [FieldOffset(4)] public uint Magic1;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint FrameTelemetryCount;
        [FieldOffset(20)] public uint FuzzTelemetryCount;
        [FieldOffset(24)] public uint FrameTelemetryStride;
        [FieldOffset(28)] public uint FuzzTelemetryStride;
        [FieldOffset(32)] public uint ResultStride;
        [FieldOffset(36)] public uint StateStride;
        [FieldOffset(40)] public uint BufferIdMin;
        [FieldOffset(44)] public uint BufferIdMax;
        [FieldOffset(48)] public uint Reserved0;
        [FieldOffset(52)] public uint Reserved1;
        [FieldOffset(56)] public uint Reserved2;
        [FieldOffset(60)] public uint Reserved3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PowerJacobiStressFrameTelemetry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint StateHash;
        [FieldOffset(8)] public uint FailureFlags;
        [FieldOffset(12)] public int NodeCount;
        [FieldOffset(16)] public int EdgeCount;
        [FieldOffset(20)] public int IterationCount;
        [FieldOffset(24)] public float Residual;
        [FieldOffset(28)] public float PreviousResidual;
        [FieldOffset(32)] public float TotalEnergy;
        [FieldOffset(36)] public float AveragePotential;
        [FieldOffset(40)] public float MinPotential;
        [FieldOffset(44)] public float MaxPotential;
        [FieldOffset(48)] public uint FirstBadNodeHash;
        [FieldOffset(52)] public int FirstBadNodeIndex;
        [FieldOffset(56)] public float SolverMicroseconds;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PowerJacobiStressFuzzerResult
    {
        [FieldOffset(0)] public uint FailureFlags;
        [FieldOffset(4)] public uint FinalStateHash;
        [FieldOffset(8)] public int NodeCount;
        [FieldOffset(12)] public int EdgeCount;
        [FieldOffset(16)] public int FrameCount;
        [FieldOffset(20)] public int IterationCount;
        [FieldOffset(24)] public float FinalResidual;
        [FieldOffset(28)] public float MaxResidual;
        [FieldOffset(32)] public float InitialEnergy;
        [FieldOffset(36)] public float FinalEnergy;
        [FieldOffset(40)] public float EnergyDeltaAbs;
        [FieldOffset(44)] public float AverageSolverMicroseconds;
        [FieldOffset(48)] public int FirstFailureFrame;
        [FieldOffset(52)] public int FirstFailureNodeIndex;
        [FieldOffset(56)] public uint FirstFailureNodeHash;
        [FieldOffset(60)] public uint OscillationCount;
        [FieldOffset(64)] public long ManagedBytesDelta;
        [FieldOffset(72)] public long SolverTicks;
        [FieldOffset(80)] public long LoopTicks;
        [FieldOffset(88)] public double3 FirstFailureAup;
        [FieldOffset(112)] public uint ExplicitGenerationDrainPresent;
        [FieldOffset(116)] public uint _pad0;
        [FieldOffset(120)] public uint _pad1;
        [FieldOffset(124)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PowerJacobiStressRunConfig
    {
        [FieldOffset(0)] public int NodeCount;
        [FieldOffset(4)] public int EdgeCapacity;
        [FieldOffset(8)] public int FrameCount;
        [FieldOffset(12)] public int IterationCount;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float ResidualTolerance;
        [FieldOffset(24)] public float EnergyEpsilon;
        [FieldOffset(28)] public float PerformanceLimitMicroseconds;
        [FieldOffset(32)] public double3 BaseOriginAup;
        [FieldOffset(56)] public uint ExplicitGenerationDrainPresent;
        [FieldOffset(60)] public uint Reserved0;
    }

    public static class PowerJacobiStressAupMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 ToBaseLocalFloat3(double3 nodeAup, double3 baseOriginAup)
        {
            double3 localDelta = nodeAup - baseOriginAup;
            return new float3((float)localDelta.x, (float)localDelta.y, (float)localDelta.z);
        }
    }

#if UNITY_EDITOR
    public static class PowerJacobiStressFuzzerState
    {
        // COLD ALLOC: float[300] - editor-only residual line graph samples - owner: PowerJacobiStressFuzzerState
        public static readonly float[] ResidualSamples = new float[PowerJacobiStressFuzzerConstants.TelemetryFrameCount];
        // COLD ALLOC: float[300] - editor-only omega line graph samples - owner: PowerJacobiStressFuzzerState
        public static readonly float[] OmegaSamples = new float[PowerJacobiStressFuzzerConstants.TelemetryFrameCount];
        public static PowerJacobiStressFuzzerResult LastResult;
        public static double3 LastFailureAup;
        public static uint LastFailureNodeHash;
        public static float3 LastFailureDirection;
        public static bool HasFailure;

        public static void CopyTelemetry(NativeArray<JacobiFuzzTelemetryEntry> telemetry)
        {
            int limit = math.min(PowerJacobiStressFuzzerConstants.TelemetryFrameCount, telemetry.IsCreated ? telemetry.Length : 0);
            for (int i = 0; i < limit; i++)
            {
                JacobiFuzzTelemetryEntry entry = telemetry[i];
                ResidualSamples[i] = entry.HighestResidual;
                OmegaSamples[i] = entry.ActiveOmega;
            }

            for (int i = limit; i < PowerJacobiStressFuzzerConstants.TelemetryFrameCount; i++)
            {
                ResidualSamples[i] = 0f;
                OmegaSamples[i] = 0f;
            }
        }
    }
#endif

    public static unsafe class PowerJacobiStressFuzzer
    {
        private const string CsvFailurePath = "Docs/Reports/HEADLESS_JACOBI_FAILURES.csv";
        private const string SuccessReportPath = "Docs/Reports/QA_OPTIMIZATION_REPORT_SHINOBU_356.json";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_356.bin";
        private const string ProfileCsvPath = "Assets/_Project/Data/jacobi_fuzz_profiles.csv";
        private const string LegacyProfileCsvPath = "Assets/_Project/Data/fuzzer_topology_profiles.csv";

        public static bool RunDefault(out PowerJacobiStressFuzzerResult result)
        {
            PowerJacobiStressTopologyProfile profile = CreateDefaultProfile();
#if UNITY_EDITOR
            if (!TryLoadTopologyProfile(ProfileCsvPath, out profile))
                TryLoadTopologyProfile(LegacyProfileCsvPath, out profile);
#endif
            PowerJacobiStressRunConfig config = CreateDefaultConfig(profile);
            return Run(in config, in profile, CsvFailurePath, SuccessReportPath, DumpPath, out result);
        }

        public static bool TryScheduleDefault(out ScheduledRun run, out PowerJacobiStressFuzzerResult immediateResult)
        {
            PowerJacobiStressTopologyProfile profile = CreateDefaultProfile();
#if UNITY_EDITOR
            if (!TryLoadTopologyProfile(ProfileCsvPath, out profile))
                TryLoadTopologyProfile(LegacyProfileCsvPath, out profile);
#endif
            PowerJacobiStressRunConfig config = CreateDefaultConfig(profile);
            return TrySchedule(in config, in profile, CsvFailurePath, SuccessReportPath, DumpPath, out run, out immediateResult);
        }

        public static bool TrySchedule(
            in PowerJacobiStressRunConfig config,
            in PowerJacobiStressTopologyProfile profile,
            string csvFailurePath,
            string successReportPath,
            string dumpPath,
            out ScheduledRun run,
            out PowerJacobiStressFuzzerResult immediateResult)
        {
            return ScheduledRun.TryCreate(in config, in profile, csvFailurePath, successReportPath, dumpPath, out run, out immediateResult);
        }

#if UNITY_EDITOR
        public static bool TryLoadTopologyProfile(string path, out PowerJacobiStressTopologyProfile profile)
        {
            profile = CreateDefaultProfile();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            NativeArray<byte> scratch = default;
            try
            {
                scratch = new NativeArray<byte>(PowerJacobiStressFuzzerConstants.CsvScratchBytes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                int bytesRead = 0;
                while (bytesRead < scratch.Length)
                {
                    int read = stream.Read(new Span<byte>(ptr + bytesRead, scratch.Length - bytesRead));
                    if (read <= 0)
                        break;

                    bytesRead += read;
                }

                return bytesRead > 0 &&
                       PowerJacobiStressTopologyProfileParser.TryParse(new ReadOnlySpan<byte>(ptr, bytesRead), out profile);
            }
            finally
            {
                if (scratch.IsCreated)
                    scratch.Dispose();
            }
        }
#endif

        public static bool Run(
            in PowerJacobiStressRunConfig config,
            in PowerJacobiStressTopologyProfile profile,
            string csvFailurePath,
            string successReportPath,
            string dumpPath,
            out PowerJacobiStressFuzzerResult result)
        {
            result = default;
            if (!ValidateRequiredLayouts())
            {
                result.FailureFlags = PowerJacobiStressFuzzerConstants.FailureFlagLayout;
                result.NodeCount = config.NodeCount;
                result.EdgeCount = config.EdgeCapacity;
                result.FrameCount = config.FrameCount;
                return false;
            }

            float globalQualityWeight = SanitizeQuality(config.GlobalQualityWeight);
            int nodeCount = math.clamp(config.NodeCount, 1, math.max(1, PowerJacobiStressFuzzerConstants.DefaultNodeCount * 2));
            int edgeCapacity = math.max(nodeCount + 1, config.EdgeCapacity);
            int frameCount = math.max(1, config.FrameCount);
            int iterationCount = ResolveIterationCount(config.IterationCount, globalQualityWeight);
            PowerJacobiStressRunConfig safeConfig = config;
            safeConfig.NodeCount = nodeCount;
            safeConfig.EdgeCapacity = edgeCapacity;
            safeConfig.FrameCount = frameCount;
            safeConfig.IterationCount = iterationCount;
            safeConfig.GlobalQualityWeight = globalQualityWeight;
            safeConfig.ResidualTolerance = SanitizePositiveOrDefault(config.ResidualTolerance, PowerJacobiStressFuzzerConstants.DefaultResidualTolerance);
            safeConfig.EnergyEpsilon = SanitizePositiveOrDefault(config.EnergyEpsilon, PowerJacobiStressFuzzerConstants.DefaultEnergyEpsilon);
            safeConfig.PerformanceLimitMicroseconds = SanitizePositiveOrDefault(config.PerformanceLimitMicroseconds, PowerJacobiStressFuzzerConstants.DefaultPerformanceLimitMicroseconds);

            NativeArray<JacobiFuzzPowerNodeDTO> nodes = default;
            NativeArray<double3> nodeAup = default;
            NativeArray<int> offsets = default;
            NativeArray<int> destinations = default;
            NativeArray<float> conductance = default;
            NativeArray<float> edgeFlow = default;
            NativeArray<float> potentialFront = default;
            NativeArray<float> potentialBack = default;
            NativeArray<float> demandRate = default;
            NativeArray<float> batteryRemainder = default;
            NativeArray<PowerJacobiStressFuzzerResult> resultBuffer = default;
            NativeArray<PowerJacobiStressFrameTelemetry> telemetry = default;
            NativeArray<int> graphCounts = default;
            NativeArray<byte> csvScratch = default;
            NativeArray<float> voltageHistory = default;
            NativeArray<float> rollbackFront = default;
            NativeArray<float> rollbackBack = default;
            NativeArray<JacobiFuzzStateDTO> fuzzState = default;
            NativeArray<JacobiFuzzTelemetryEntry> fuzzTelemetry = default;
            NativeArray<PowerJacobiStressTopologyProfile> profileBuffer = default;
            GlobalDataVault ownedVault = null;

            try
            {
                long requiredVaultBytes = EstimateVaultBytes(nodeCount, edgeCapacity, iterationCount);
                long arenaBytes = Math.Max(requiredVaultBytes * 2L, requiredVaultBytes + (8L * 1024L * 1024L));
                ownedVault = CreateIsolatedFuzzerVault(32, arenaBytes);
                IDataVault vault = ownedVault;
                bool buffersReady =
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.Nodes, nodeCount, NativeArrayOptions.UninitializedMemory, out nodes) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.NodeAup, nodeCount, NativeArrayOptions.UninitializedMemory, out nodeAup) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrOffsets, nodeCount + 1, NativeArrayOptions.UninitializedMemory, out offsets) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrDestinations, edgeCapacity, NativeArrayOptions.UninitializedMemory, out destinations) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrConductance, edgeCapacity, NativeArrayOptions.UninitializedMemory, out conductance) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrFlow, edgeCapacity, NativeArrayOptions.UninitializedMemory, out edgeFlow) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.PotentialFront, nodeCount, NativeArrayOptions.UninitializedMemory, out potentialFront) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.PotentialBack, nodeCount, NativeArrayOptions.UninitializedMemory, out potentialBack) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.DemandRate, nodeCount, NativeArrayOptions.UninitializedMemory, out demandRate) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.BatteryRemainder, nodeCount, NativeArrayOptions.UninitializedMemory, out batteryRemainder) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.Result, 1, NativeArrayOptions.UninitializedMemory, out resultBuffer) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.StressTelemetry, PowerJacobiStressFuzzerConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out telemetry) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.GraphCounts, 2, NativeArrayOptions.UninitializedMemory, out graphCounts) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsvScratch, PowerJacobiStressFuzzerConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out csvScratch) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.VoltageHistory, nodeCount * iterationCount, NativeArrayOptions.UninitializedMemory, out voltageHistory) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.RollbackFront, nodeCount, NativeArrayOptions.UninitializedMemory, out rollbackFront) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.RollbackBack, nodeCount, NativeArrayOptions.UninitializedMemory, out rollbackBack) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.FuzzState, 1, NativeArrayOptions.UninitializedMemory, out fuzzState) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.FuzzTelemetry, PowerJacobiStressFuzzerConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out fuzzTelemetry) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.TopologyProfile, 1, NativeArrayOptions.UninitializedMemory, out profileBuffer);

                if (!buffersReady)
                {
                    result.FailureFlags = PowerJacobiStressFuzzerConstants.FailureFlagCapacity;
                    result.NodeCount = nodeCount;
                    result.EdgeCount = edgeCapacity;
                    result.FrameCount = frameCount;
                    return false;
                }

                profileBuffer[0] = profile;
                PowerJacobiStressTopologyProfile activeProfile = profileBuffer[0];

                WarmBurst(nodes, nodeAup, offsets, destinations, conductance, edgeFlow, potentialFront, potentialBack, demandRate, batteryRemainder, resultBuffer, telemetry, graphCounts, in safeConfig, in activeProfile);

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long loopTicksStart = Stopwatch.GetTimestamp();
                long solverStart = Stopwatch.GetTimestamp();
                JobHandle solverHandle = new EvaluateHeadlessJacobiFuzzJob
                {
                    NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                    NodeAup = nodeAup,
                    NodeEdgeOffsets = offsets,
                    EdgeDestinations = destinations,
                    EdgeConductance = conductance,
                    EdgeCurrentFlow = edgeFlow,
                    PotentialFront = potentialFront,
                    PotentialBack = potentialBack,
                    DemandRate = demandRate,
                    BatteryMilliRemainder = batteryRemainder,
                    VoltageHistory = voltageHistory,
                    RollbackFront = rollbackFront,
                    RollbackBack = rollbackBack,
                    Result = resultBuffer,
                    State = fuzzState,
                    StressTelemetry = telemetry,
                    FuzzTelemetry = fuzzTelemetry,
                    GraphCounts = graphCounts,
                    Config = safeConfig,
                    EdgeCount = graphCounts[1]
                }.Schedule();
                solverHandle.Complete();
                long solverTicks = Stopwatch.GetTimestamp() - solverStart;
                float solverMicroseconds = TicksToMicroseconds(solverTicks, 1);

                JobHandle conservationHandle = new VerifyPowerConservationJob
                {
                    NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
                    DemandRate = demandRate,
                    Result = resultBuffer,
                    State = fuzzState,
                    FuzzTelemetry = fuzzTelemetry,
                    NodeCount = nodeCount,
                    EnergyEpsilon = safeConfig.EnergyEpsilon,
                    ExplicitGenerationDrainPresent = safeConfig.ExplicitGenerationDrainPresent,
                    AverageSolverMicroseconds = solverMicroseconds
                }.Schedule();
                conservationHandle.Complete();

                long loopTicks = Stopwatch.GetTimestamp() - loopTicksStart;
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                result = resultBuffer[0];
                result.ManagedBytesDelta = allocatedAfter - allocatedBefore;
                result.SolverTicks = solverTicks;
                result.LoopTicks = loopTicks;
                result.AverageSolverMicroseconds = solverMicroseconds;
                if (result.ManagedBytesDelta != 0L)
                    result.FailureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagManagedAllocation;
                if (result.AverageSolverMicroseconds > safeConfig.PerformanceLimitMicroseconds)
                    result.FailureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagPerformance;
                resultBuffer[0] = result;
                StampFuzzTelemetrySolverMicroseconds(fuzzTelemetry, solverMicroseconds, result.FailureFlags);

                if (result.FailureFlags != 0u)
                {
                    PowerJacobiStressCsvExporter.WriteFailureCsv(csvFailurePath, nodes, nodeAup, offsets, destinations, conductance, graphCounts[1], csvScratch);
                    if ((result.FailureFlags &
                         (PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption |
                          PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence |
                          PowerJacobiStressFuzzerConstants.FailureFlagRollbackDesync)) != 0u)
                    {
                        PowerJacobiStressBinaryDump.WriteDump(dumpPath, telemetry, fuzzTelemetry, result.FailureFlags);
                    }
                }
                else
                {
                    PowerJacobiStressReportWriter.WriteSuccessReport(successReportPath, in result, csvScratch);
                }

#if UNITY_EDITOR
                PowerJacobiStressFuzzerState.LastResult = result;
                PowerJacobiStressFuzzerState.LastFailureAup = result.FirstFailureAup;
                PowerJacobiStressFuzzerState.LastFailureNodeHash = result.FirstFailureNodeHash;
                PowerJacobiStressFuzzerState.LastFailureDirection = result.FirstFailureNodeIndex >= 0 && result.FirstFailureNodeIndex + 1 < nodeAup.Length
                    ? PowerJacobiStressAupMath.ToBaseLocalFloat3(nodeAup[result.FirstFailureNodeIndex + 1], nodeAup[result.FirstFailureNodeIndex])
                    : new float3(0f, 1f, 0f);
                PowerJacobiStressFuzzerState.HasFailure = result.FailureFlags != 0u && result.FirstFailureNodeHash != 0u;
                PowerJacobiStressFuzzerState.CopyTelemetry(fuzzTelemetry);
#endif
                return result.FailureFlags == 0u;
            }
            finally
            {
                if (ownedVault != null)
                    ownedVault.Dispose();
            }
        }

        // COLD EDITOR/CI WRAPPER: stores Vault-resolved views only while a scheduled offline fuzzer chain is pending.
        public sealed class ScheduledRun : IDisposable
        {
            private NativeArray<JacobiFuzzPowerNodeDTO> _nodes;
            private NativeArray<double3> _nodeAup;
            private NativeArray<int> _offsets;
            private NativeArray<int> _destinations;
            private NativeArray<float> _conductance;
            private NativeArray<float> _edgeFlow;
            private NativeArray<float> _potentialFront;
            private NativeArray<float> _potentialBack;
            private NativeArray<float> _demandRate;
            private NativeArray<float> _batteryRemainder;
            private NativeArray<PowerJacobiStressFuzzerResult> _resultBuffer;
            private NativeArray<PowerJacobiStressFrameTelemetry> _telemetry;
            private NativeArray<int> _graphCounts;
            private NativeArray<byte> _csvScratch;
            private NativeArray<float> _voltageHistory;
            private NativeArray<float> _rollbackFront;
            private NativeArray<float> _rollbackBack;
            private NativeArray<JacobiFuzzStateDTO> _fuzzState;
            private NativeArray<JacobiFuzzTelemetryEntry> _fuzzTelemetry;
            private NativeArray<PowerJacobiStressTopologyProfile> _profileBuffer;
            private GlobalDataVault _ownedVault;
            private JobHandle _finalHandle;
            private PowerJacobiStressRunConfig _safeConfig;
            private string _csvFailurePath;
            private string _successReportPath;
            private string _dumpPath;
            private long _solverTicksStart;
            private long _loopTicksStart;
            private int _nodeCount;
            private int _edgeCapacity;
            private bool _scheduled;
            private bool _completed;
            private bool _disposed;
            private PowerJacobiStressFuzzerResult _completedResult;

            public bool IsScheduled()
            {
                return _scheduled && !_disposed;
            }

            public bool IsCompleted()
            {
                return _scheduled && !_disposed && _finalHandle.IsCompleted;
            }

            public float ReadProgress01()
            {
                if (_completed)
                    return 1f;
                return IsCompleted() ? 0.98f : 0.35f;
            }

            internal static bool TryCreate(
                in PowerJacobiStressRunConfig config,
                in PowerJacobiStressTopologyProfile profile,
                string csvFailurePath,
                string successReportPath,
                string dumpPath,
                out ScheduledRun run,
                out PowerJacobiStressFuzzerResult immediateResult)
            {
                run = null;
                immediateResult = default;
                if (!ValidateRequiredLayouts())
                {
                    immediateResult.FailureFlags = PowerJacobiStressFuzzerConstants.FailureFlagLayout;
                    immediateResult.NodeCount = config.NodeCount;
                    immediateResult.EdgeCount = config.EdgeCapacity;
                    immediateResult.FrameCount = config.FrameCount;
                    return false;
                }

                ScheduledRun candidate = new ScheduledRun();
                candidate._csvFailurePath = csvFailurePath;
                candidate._successReportPath = successReportPath;
                candidate._dumpPath = dumpPath;
                if (!candidate.TryAllocateAndSchedule(in config, in profile, out immediateResult))
                {
                    candidate.Dispose();
                    return false;
                }

                run = candidate;
                return true;
            }

            public bool TryComplete(out PowerJacobiStressFuzzerResult result)
            {
                result = default;
                if (!IsCompleted())
                    return false;

                Complete(out result);
                return true;
            }

            public void Complete(out PowerJacobiStressFuzzerResult result)
            {
                if (_completed)
                {
                    result = _completedResult;
                    return;
                }

                if (!_scheduled || _disposed)
                {
                    result = default;
                    return;
                }

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                _finalHandle.Complete();
                long solverTicks = Stopwatch.GetTimestamp() - _solverTicksStart;
                long loopTicks = Stopwatch.GetTimestamp() - _loopTicksStart;
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                // Scheduled editor runs cannot sample inside an already-running Burst job;
                // this records background-chain wall time, while sync CI Run() records isolated solver Complete() time.
                float solverMicroseconds = TicksToMicroseconds(solverTicks, 1);
                result = _resultBuffer[0];
                int actualEdgeCount = _graphCounts.IsCreated && _graphCounts.Length > 1 ? _graphCounts[1] : _edgeCapacity;
                result.EdgeCount = math.max(0, actualEdgeCount);
                result.ManagedBytesDelta = allocatedAfter - allocatedBefore;
                result.SolverTicks = solverTicks;
                result.LoopTicks = loopTicks;
                result.AverageSolverMicroseconds = solverMicroseconds;
                if (result.ManagedBytesDelta != 0L)
                    result.FailureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagManagedAllocation;
                _resultBuffer[0] = result;
                StampFuzzTelemetrySolverMicroseconds(_fuzzTelemetry, solverMicroseconds, result.FailureFlags);

                WriteColdArtifacts(in result);
#if UNITY_EDITOR
                PowerJacobiStressFuzzerState.LastResult = result;
                PowerJacobiStressFuzzerState.LastFailureAup = result.FirstFailureAup;
                PowerJacobiStressFuzzerState.LastFailureNodeHash = result.FirstFailureNodeHash;
                PowerJacobiStressFuzzerState.LastFailureDirection = result.FirstFailureNodeIndex >= 0 && result.FirstFailureNodeIndex + 1 < _nodeAup.Length
                    ? PowerJacobiStressAupMath.ToBaseLocalFloat3(_nodeAup[result.FirstFailureNodeIndex + 1], _nodeAup[result.FirstFailureNodeIndex])
                    : new float3(0f, 1f, 0f);
                PowerJacobiStressFuzzerState.HasFailure = result.FailureFlags != 0u && result.FirstFailureNodeHash != 0u;
                PowerJacobiStressFuzzerState.CopyTelemetry(_fuzzTelemetry);
#endif
                _completedResult = result;
                _completed = true;
                DisposeVaultOnly();
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                if (_scheduled && !_completed)
                    _finalHandle.Complete();
                DisposeVaultOnly();
            }

            private bool TryAllocateAndSchedule(in PowerJacobiStressRunConfig config, in PowerJacobiStressTopologyProfile profile, out PowerJacobiStressFuzzerResult immediateResult)
            {
                immediateResult = default;
                float globalQualityWeight = SanitizeQuality(config.GlobalQualityWeight);
                _nodeCount = math.clamp(config.NodeCount, 1, math.max(1, PowerJacobiStressFuzzerConstants.DefaultNodeCount * 2));
                _edgeCapacity = math.max(_nodeCount + 1, config.EdgeCapacity);
                int frameCount = math.max(1, config.FrameCount);
                int iterationCount = ResolveIterationCount(config.IterationCount, globalQualityWeight);
                _safeConfig = config;
                _safeConfig.NodeCount = _nodeCount;
                _safeConfig.EdgeCapacity = _edgeCapacity;
                _safeConfig.FrameCount = frameCount;
                _safeConfig.IterationCount = iterationCount;
                _safeConfig.GlobalQualityWeight = globalQualityWeight;
                _safeConfig.ResidualTolerance = SanitizePositiveOrDefault(config.ResidualTolerance, PowerJacobiStressFuzzerConstants.DefaultResidualTolerance);
                _safeConfig.EnergyEpsilon = SanitizePositiveOrDefault(config.EnergyEpsilon, PowerJacobiStressFuzzerConstants.DefaultEnergyEpsilon);
                _safeConfig.PerformanceLimitMicroseconds = SanitizePositiveOrDefault(config.PerformanceLimitMicroseconds, PowerJacobiStressFuzzerConstants.DefaultPerformanceLimitMicroseconds);

                long requiredVaultBytes = EstimateVaultBytes(_nodeCount, _edgeCapacity, iterationCount);
                long arenaBytes = Math.Max(requiredVaultBytes * 2L, requiredVaultBytes + (8L * 1024L * 1024L));
                _ownedVault = CreateIsolatedFuzzerVault(32, arenaBytes);
                IDataVault vault = _ownedVault;
                bool buffersReady =
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.Nodes, _nodeCount, NativeArrayOptions.UninitializedMemory, out _nodes) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.NodeAup, _nodeCount, NativeArrayOptions.UninitializedMemory, out _nodeAup) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrOffsets, _nodeCount + 1, NativeArrayOptions.UninitializedMemory, out _offsets) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrDestinations, _edgeCapacity, NativeArrayOptions.UninitializedMemory, out _destinations) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrConductance, _edgeCapacity, NativeArrayOptions.UninitializedMemory, out _conductance) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsrFlow, _edgeCapacity, NativeArrayOptions.UninitializedMemory, out _edgeFlow) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.PotentialFront, _nodeCount, NativeArrayOptions.UninitializedMemory, out _potentialFront) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.PotentialBack, _nodeCount, NativeArrayOptions.UninitializedMemory, out _potentialBack) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.DemandRate, _nodeCount, NativeArrayOptions.UninitializedMemory, out _demandRate) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.BatteryRemainder, _nodeCount, NativeArrayOptions.UninitializedMemory, out _batteryRemainder) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.Result, 1, NativeArrayOptions.UninitializedMemory, out _resultBuffer) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.StressTelemetry, PowerJacobiStressFuzzerConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out _telemetry) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.GraphCounts, 2, NativeArrayOptions.UninitializedMemory, out _graphCounts) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.CsvScratch, PowerJacobiStressFuzzerConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory, out _csvScratch) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.VoltageHistory, _nodeCount * iterationCount, NativeArrayOptions.UninitializedMemory, out _voltageHistory) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.RollbackFront, _nodeCount, NativeArrayOptions.UninitializedMemory, out _rollbackFront) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.RollbackBack, _nodeCount, NativeArrayOptions.UninitializedMemory, out _rollbackBack) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.FuzzState, 1, NativeArrayOptions.UninitializedMemory, out _fuzzState) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.FuzzTelemetry, PowerJacobiStressFuzzerConstants.TelemetryFrameCount, NativeArrayOptions.UninitializedMemory, out _fuzzTelemetry) &&
                    TryResolveFuzzerVaultBuffer(vault, PowerJacobiStressFuzzerBufferIds.TopologyProfile, 1, NativeArrayOptions.UninitializedMemory, out _profileBuffer);
                if (!buffersReady)
                {
                    immediateResult.FailureFlags = PowerJacobiStressFuzzerConstants.FailureFlagCapacity;
                    immediateResult.NodeCount = _nodeCount;
                    immediateResult.EdgeCount = _edgeCapacity;
                    immediateResult.FrameCount = frameCount;
                    return false;
                }

                _profileBuffer[0] = profile;
                _loopTicksStart = Stopwatch.GetTimestamp();
                _solverTicksStart = _loopTicksStart;
                // UNINITIALIZED PROOF: dependency order fully writes graph/scalars/result before EvaluateHeadlessJacobiFuzzJob reads them.
                JobHandle generateHandle = new GenerateHostileCsrGraphJob
                {
                    Nodes = _nodes,
                    NodeAup = _nodeAup,
                    NodeEdgeOffsets = _offsets,
                    EdgeDestinations = _destinations,
                    EdgeConductance = _conductance,
                    EdgeCurrentFlow = _edgeFlow,
                    Counts = _graphCounts,
                    Profile = _profileBuffer[0],
                    BaseOriginAup = _safeConfig.BaseOriginAup,
                    NodeCount = _safeConfig.NodeCount,
                    EdgeCapacity = _safeConfig.EdgeCapacity
                }.Schedule();
                JobHandle injectHandle = new InjectRandomPotentialsJob
                {
                    NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                    PotentialFront = _potentialFront,
                    PotentialBack = _potentialBack,
                    DemandRate = _demandRate,
                    BatteryMilliRemainder = _batteryRemainder,
                    NodeCount = _safeConfig.NodeCount,
                    FrameIndex = 0,
                    ProfileFlags = _profileBuffer[0].Flags
                }.Schedule(generateHandle);
                JobHandle initHandle = new InitializeFuzzerResultJob
                {
                    NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_nodes),
                    LatestPotential = _potentialFront,
                    Result = _resultBuffer,
                    Telemetry = _telemetry,
                    NodeCount = _safeConfig.NodeCount,
                    EdgeCount = _safeConfig.EdgeCapacity,
                    GraphCounts = _graphCounts,
                    ExplicitGenerationDrainPresent = _safeConfig.ExplicitGenerationDrainPresent
                }.Schedule(injectHandle);
                JobHandle solverHandle = new EvaluateHeadlessJacobiFuzzJob
                {
                    NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(_nodes),
                    NodeAup = _nodeAup,
                    NodeEdgeOffsets = _offsets,
                    EdgeDestinations = _destinations,
                    EdgeConductance = _conductance,
                    EdgeCurrentFlow = _edgeFlow,
                    PotentialFront = _potentialFront,
                    PotentialBack = _potentialBack,
                    DemandRate = _demandRate,
                    BatteryMilliRemainder = _batteryRemainder,
                    VoltageHistory = _voltageHistory,
                    RollbackFront = _rollbackFront,
                    RollbackBack = _rollbackBack,
                    Result = _resultBuffer,
                    State = _fuzzState,
                    StressTelemetry = _telemetry,
                    FuzzTelemetry = _fuzzTelemetry,
                    GraphCounts = _graphCounts,
                    Config = _safeConfig,
                    EdgeCount = _safeConfig.EdgeCapacity
                }.Schedule(initHandle);
                _finalHandle = new VerifyPowerConservationJob
                {
                    NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(_nodes),
                    DemandRate = _demandRate,
                    Result = _resultBuffer,
                    State = _fuzzState,
                    FuzzTelemetry = _fuzzTelemetry,
                    NodeCount = _nodeCount,
                    EnergyEpsilon = _safeConfig.EnergyEpsilon,
                    ExplicitGenerationDrainPresent = _safeConfig.ExplicitGenerationDrainPresent,
                    AverageSolverMicroseconds = 0f
                }.Schedule(solverHandle);
                JobHandle.ScheduleBatchedJobs();
                _scheduled = true;
                return true;
            }

            private void WriteColdArtifacts(in PowerJacobiStressFuzzerResult result)
            {
                if (result.FailureFlags != 0u)
                {
                    PowerJacobiStressCsvExporter.WriteFailureCsv(_csvFailurePath, _nodes, _nodeAup, _offsets, _destinations, _conductance, result.EdgeCount, _csvScratch);
                    if ((result.FailureFlags &
                         (PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption |
                          PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence |
                          PowerJacobiStressFuzzerConstants.FailureFlagRollbackDesync)) != 0u)
                    {
                        PowerJacobiStressBinaryDump.WriteDump(_dumpPath, _telemetry, _fuzzTelemetry, result.FailureFlags);
                    }
                }
                else
                {
                    PowerJacobiStressReportWriter.WriteSuccessReport(_successReportPath, in result, _csvScratch);
                }
            }

            private void DisposeVaultOnly()
            {
                if (_disposed)
                    return;

                if (_ownedVault != null)
                    _ownedVault.Dispose();
                _ownedVault = null;
                _disposed = true;
                _scheduled = false;
            }
        }

        public static PowerJacobiStressTopologyProfile CreateDefaultProfile()
        {
            PowerJacobiStressTopologyProfile profile = default;
            profile.ProfileHash = 0xA551255u;
            profile.NodeCount = PowerJacobiStressFuzzerConstants.DefaultNodeCount;
            profile.EdgeCapacity = PowerJacobiStressFuzzerConstants.DefaultEdgeCapacity;
            profile.LoopRatio01 = 0.78f;
            profile.StarRatio01 = 0.20f;
            profile.IslandRatio01 = 0.15f;
            profile.Flags = 0u;
            return profile;
        }

        internal static PowerJacobiStressRunConfig CreateDefaultConfig(in PowerJacobiStressTopologyProfile profile)
        {
            PowerJacobiStressRunConfig config = default;
            config.NodeCount = profile.NodeCount > 0 ? profile.NodeCount : PowerJacobiStressFuzzerConstants.DefaultNodeCount;
            config.EdgeCapacity = profile.EdgeCapacity > 0 ? profile.EdgeCapacity : PowerJacobiStressFuzzerConstants.DefaultEdgeCapacity;
            config.FrameCount = PowerJacobiStressFuzzerConstants.DefaultFrameCount;
            config.GlobalQualityWeight = 1f;
            config.IterationCount = 0;
            config.ResidualTolerance = PowerJacobiStressFuzzerConstants.DefaultResidualTolerance;
            config.EnergyEpsilon = PowerJacobiStressFuzzerConstants.DefaultEnergyEpsilon;
            config.PerformanceLimitMicroseconds = PowerJacobiStressFuzzerConstants.DefaultPerformanceLimitMicroseconds;
            config.BaseOriginAup = new double3(9000000000.0, -4000.0, -9000000000.0);
            config.ExplicitGenerationDrainPresent = 1u;
            return config;
        }

        public static bool ValidateRequiredLayouts()
        {
            return UnsafeUtility.SizeOf<JacobiFuzzPowerNodeDTO>() == PowerJacobiStressFuzzerConstants.FuzzPowerNodeDtoSizeBytes &&
                   UnsafeUtility.AlignOf<JacobiFuzzPowerNodeDTO>() == 4 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressTopologyProfile>() == 32 &&
                   UnsafeUtility.SizeOf<JacobiFuzzStateDTO>() == 32 &&
                   UnsafeUtility.AlignOf<JacobiFuzzStateDTO>() == 4 &&
                   UnsafeUtility.SizeOf<JacobiFuzzTelemetryEntry>() == 64 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressDumpHeader>() == 64 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>() == 64 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>() == 128 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressRunConfig>() == 64 &&
                   (UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>() & 7) == 0 &&
                   (UnsafeUtility.SizeOf<PowerJacobiStressRunConfig>() & 7) == 0 &&
                   ValidateRequiredOffsets();
        }

        private static unsafe bool ValidateRequiredOffsets()
        {
            PowerJacobiStressFuzzerResult result = default;
            PowerJacobiStressRunConfig config = default;
            PowerJacobiStressDumpHeader dumpHeader = default;

            return ByteOffset(ref result, ref result.ManagedBytesDelta) == 64 &&
                   ByteOffset(ref result, ref result.FirstFailureAup) == 88 &&
                   ByteOffset(ref result, ref result.ExplicitGenerationDrainPresent) == 112 &&
                   ByteOffset(ref config, ref config.BaseOriginAup) == 32 &&
                   ByteOffset(ref dumpHeader, ref dumpHeader.Flags) == 12 &&
                   ByteOffset(ref dumpHeader, ref dumpHeader.BufferIdMin) == 40;
        }

        private static unsafe int ByteOffset<TStruct, TField>(ref TStruct owner, ref TField field)
            where TStruct : struct
            where TField : struct
        {
            return (int)((byte*)UnsafeUtility.AddressOf(ref field) - (byte*)UnsafeUtility.AddressOf(ref owner));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQuality(float globalQualityWeight)
        {
            return MathLodApproximation.SaturateFinite(globalQualityWeight, 1f);
        }

        private static int ResolveIterationCount(int requestedIterationCount, float globalQualityWeight)
        {
            if (requestedIterationCount > 0)
            {
                return math.clamp(
                    requestedIterationCount,
                    PowerJacobiStressFuzzerConstants.MinimumSolverIterationCount,
                    PowerJacobiStressFuzzerConstants.MaximumSolverIterationCount);
            }

            return math.clamp(
                (int)MathLodRuntimeConfig.ResolveActiveIterationBudget(globalQualityWeight),
                PowerJacobiStressFuzzerConstants.MinimumSolverIterationCount,
                PowerJacobiStressFuzzerConstants.MaximumSolverIterationCount);
        }

        private static GlobalDataVault CreateIsolatedFuzzerVault(int capacity, long arenaBytes)
        {
            // COLD QA VAULT: avoid GlobalDataVault.Create because it publishes into TryGetLatestCreated.
            GlobalDataVault vault = new GlobalDataVault();
            vault.Initialize(capacity, arenaBytes);
            return vault;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositiveOrDefault(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void WarmBurst(
            NativeArray<JacobiFuzzPowerNodeDTO> nodes,
            NativeArray<double3> nodeAup,
            NativeArray<int> offsets,
            NativeArray<int> destinations,
            NativeArray<float> conductance,
            NativeArray<float> edgeFlow,
            NativeArray<float> potentialFront,
            NativeArray<float> potentialBack,
            NativeArray<float> demandRate,
            NativeArray<float> batteryRemainder,
            NativeArray<PowerJacobiStressFuzzerResult> resultBuffer,
            NativeArray<PowerJacobiStressFrameTelemetry> telemetry,
            NativeArray<int> graphCounts,
            in PowerJacobiStressRunConfig config,
            in PowerJacobiStressTopologyProfile profile)
        {
            InitializeScenario(nodes, nodeAup, offsets, destinations, conductance, edgeFlow, potentialFront, potentialBack, demandRate, batteryRemainder, resultBuffer, telemetry, graphCounts, in config, in profile);

            new ValidateSolverConvergenceJob
            {
                NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
                NodeAup = nodeAup,
                LatestPotential = potentialBack,
                PreviousPotential = potentialFront,
                Result = resultBuffer,
                Telemetry = telemetry,
                NodeCount = config.NodeCount,
                EdgeCount = graphCounts[1],
                FrameIndex = 0,
                IterationCount = config.IterationCount,
                ResidualTolerance = config.ResidualTolerance,
                EnergyEpsilon = config.EnergyEpsilon,
                AverageSolverMicroseconds = 0f,
                PerformanceLimitMicroseconds = config.PerformanceLimitMicroseconds,
                ExplicitGenerationDrainPresent = config.ExplicitGenerationDrainPresent
            }.Schedule().Complete();

            InitializeScenario(nodes, nodeAup, offsets, destinations, conductance, edgeFlow, potentialFront, potentialBack, demandRate, batteryRemainder, resultBuffer, telemetry, graphCounts, in config, in profile);
        }

        private static void InitializeScenario(
            NativeArray<JacobiFuzzPowerNodeDTO> nodes,
            NativeArray<double3> nodeAup,
            NativeArray<int> offsets,
            NativeArray<int> destinations,
            NativeArray<float> conductance,
            NativeArray<float> edgeFlow,
            NativeArray<float> potentialFront,
            NativeArray<float> potentialBack,
            NativeArray<float> demandRate,
            NativeArray<float> batteryRemainder,
            NativeArray<PowerJacobiStressFuzzerResult> resultBuffer,
            NativeArray<PowerJacobiStressFrameTelemetry> telemetry,
            NativeArray<int> graphCounts,
            in PowerJacobiStressRunConfig config,
            in PowerJacobiStressTopologyProfile profile)
        {
            // UNINITIALIZED PROOF: these three jobs fully write graph, scalar, result, and telemetry buffers before the solver reads them.
            new GenerateHostileCsrGraphJob
            {
                Nodes = nodes,
                NodeAup = nodeAup,
                NodeEdgeOffsets = offsets,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                EdgeCurrentFlow = edgeFlow,
                Counts = graphCounts,
                Profile = profile,
                BaseOriginAup = config.BaseOriginAup,
                NodeCount = config.NodeCount,
                EdgeCapacity = config.EdgeCapacity
            }.Schedule().Complete();

            new InjectRandomPotentialsJob
            {
                NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                PotentialFront = potentialFront,
                PotentialBack = potentialBack,
                DemandRate = demandRate,
                BatteryMilliRemainder = batteryRemainder,
                NodeCount = config.NodeCount,
                FrameIndex = 0,
                ProfileFlags = profile.Flags
            }.Schedule().Complete();

            new InitializeFuzzerResultJob
            {
                NodesPtr = (JacobiFuzzPowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
                LatestPotential = potentialFront,
                Result = resultBuffer,
                Telemetry = telemetry,
                NodeCount = config.NodeCount,
                EdgeCount = graphCounts[1],
                GraphCounts = graphCounts,
                ExplicitGenerationDrainPresent = config.ExplicitGenerationDrainPresent
            }.Schedule().Complete();
        }

        private static float TicksToMicroseconds(long ticks, int divisor)
        {
            double us = (double)ticks * 1000000.0 / Stopwatch.Frequency;
            return (float)(us / math.max(1, divisor));
        }

        private static void StampFuzzTelemetrySolverMicroseconds(
            NativeArray<JacobiFuzzTelemetryEntry> fuzzTelemetry,
            float solverMicroseconds,
            uint failureFlags)
        {
            if (!fuzzTelemetry.IsCreated)
                return;

            for (int i = 0; i < fuzzTelemetry.Length; i++)
            {
                JacobiFuzzTelemetryEntry entry = fuzzTelemetry[i];
                entry.SolverMicroseconds = solverMicroseconds;
                entry.MismatchFlags = failureFlags;
                fuzzTelemetry[i] = entry;
            }
        }

        private static long EstimateVaultBytes(int nodeCount, int edgeCapacity, int iterationCount)
        {
            long nodesBytes = (long)nodeCount * UnsafeUtility.SizeOf<JacobiFuzzPowerNodeDTO>();
            long aupBytes = (long)nodeCount * UnsafeUtility.SizeOf<double3>();
            long offsetsBytes = (long)(nodeCount + 1) * UnsafeUtility.SizeOf<int>();
            long edgeBytes = (long)edgeCapacity * (UnsafeUtility.SizeOf<int>() + (2L * UnsafeUtility.SizeOf<float>()));
            long scalarNodeBytes = (long)nodeCount * 6L * UnsafeUtility.SizeOf<float>();
            long historyBytes = (long)nodeCount * math.max(1, iterationCount) * UnsafeUtility.SizeOf<float>();
            long telemetryBytes =
                UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>() +
                UnsafeUtility.SizeOf<JacobiFuzzStateDTO>() +
                UnsafeUtility.SizeOf<PowerJacobiStressTopologyProfile>() +
                (long)PowerJacobiStressFuzzerConstants.TelemetryFrameCount *
                (UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>() + UnsafeUtility.SizeOf<JacobiFuzzTelemetryEntry>()) +
                PowerJacobiStressFuzzerConstants.CsvScratchBytes +
                (2L * UnsafeUtility.SizeOf<int>());
            return nodesBytes + aupBytes + offsetsBytes + edgeBytes + scalarNodeBytes + historyBytes + telemetryBytes;
        }

        private static bool TryResolveFuzzerVaultBuffer<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsAllocationLocked)
                return false;

            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Power,
                options);
            return handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateHostileCsrGraphJob : IJob
    {
        [NoAlias] public NativeArray<JacobiFuzzPowerNodeDTO> Nodes;
        [NoAlias] public NativeArray<double3> NodeAup;
        [NoAlias] public NativeArray<int> NodeEdgeOffsets;
        [NoAlias] public NativeArray<int> EdgeDestinations;
        [NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<float> EdgeCurrentFlow;
        [NoAlias] public NativeArray<int> Counts;
        public PowerJacobiStressTopologyProfile Profile;
        public double3 BaseOriginAup;
        public int NodeCount;
        public int EdgeCapacity;

        public void Execute()
        {
            int nodeCount = math.clamp(NodeCount, 1, math.min(Nodes.Length, NodeAup.Length));
            int edgeCapacity = math.max(0, math.min(EdgeCapacity, math.min(EdgeDestinations.Length, math.min(EdgeConductance.Length, EdgeCurrentFlow.Length))));
            if (NodeEdgeOffsets.Length < nodeCount + 1 || edgeCapacity <= 0)
            {
                WriteCounts(0, 0);
                return;
            }

            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                JacobiFuzzPowerNodeDTO node = default;
                node.NodeHash = HashNode(nodeIndex);
                node.Potential = 0f;
                node.MaxCapacity = (nodeIndex % 257) == 0 ? 250000f : 1000f + ((nodeIndex & 31) * 37f);
                node.CurrentStorage = (nodeIndex % 41) == 0 ? 128f : 0f;
                node.Flags = PowerJacobiStressFuzzerConstants.NodeFlagActive |
                             ((nodeIndex % 257) == 0 ? PowerJacobiStressFuzzerConstants.NodeFlagSource : 0u) |
                             ((nodeIndex % 41) == 0 ? PowerJacobiStressFuzzerConstants.NodeFlagBattery : 0u);
                node.InternalResistance = 0.05f + ((nodeIndex & 15) * 0.015625f);
                Nodes[nodeIndex] = node;
                NodeAup[nodeIndex] = ResolveParadoxAup(nodeIndex);
            }

            int cursor = 0;
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                NodeEdgeOffsets[nodeIndex] = cursor;
                int degree = ResolveDegree(nodeIndex, nodeCount);
                for (int localEdge = 0; localEdge < degree && cursor < edgeCapacity; localEdge++)
                {
                    int destination = ResolveDestination(nodeIndex, localEdge, nodeCount);
                    EdgeDestinations[cursor] = destination;
                    EdgeConductance[cursor] = ResolveConductance(nodeIndex, destination, localEdge);
                    EdgeCurrentFlow[cursor] = 0f;
                    cursor++;
                }
            }

            NodeEdgeOffsets[nodeCount] = cursor;
            for (int clearIndex = cursor; clearIndex < edgeCapacity; clearIndex++)
            {
                EdgeDestinations[clearIndex] = 0;
                EdgeConductance[clearIndex] = 0f;
                EdgeCurrentFlow[clearIndex] = 0f;
            }

            WriteCounts(nodeCount, cursor);
        }

        private int ResolveDegree(int nodeIndex, int nodeCount)
        {
            if (nodeIndex == 0)
                return math.min(ResolveStarDegree(nodeCount), math.max(0, nodeCount - 1));
            int mainLimit = ResolveMainLimit(nodeCount);
            int selfLoopStart = mainLimit + ((nodeCount - mainLimit) / 2);
            int isolatedStart = mainLimit + ((nodeCount - mainLimit) * 3 / 4);
            if (nodeIndex < mainLimit)
                return 3;
            if (nodeIndex < selfLoopStart)
                return 2;
            if (nodeIndex < isolatedStart)
                return 1;
            return 0;
        }

        private int ResolveDestination(int nodeIndex, int localEdge, int nodeCount)
        {
            if (nodeIndex == 0)
                return 1 + (localEdge % math.max(1, math.min(ResolveStarDegree(nodeCount), nodeCount - 1)));

            int mainLimit = ResolveMainLimit(nodeCount);
            if (nodeIndex < mainLimit)
            {
                if (localEdge == 0)
                    return (nodeIndex + 1) % mainLimit;
                if (localEdge == 1)
                    return (nodeIndex + 17) % mainLimit;
                return nodeIndex % 3 == 0 ? math.max(0, nodeIndex - 2) : (nodeIndex + mainLimit - 1) % mainLimit;
            }

            int islandStart = mainLimit;
            int islandEnd = mainLimit + ((nodeCount - mainLimit) / 2);
            if (nodeIndex < islandEnd)
            {
                int islandSize = 25;
                int local = nodeIndex - islandStart;
                int baseIndex = islandStart + (local / islandSize) * islandSize;
                int offset = local % islandSize;
                return baseIndex + ((offset + 1 + localEdge) % islandSize);
            }

            if (nodeIndex < mainLimit + ((nodeCount - mainLimit) * 3 / 4))
                return nodeIndex;

            return nodeIndex;
        }

        private int ResolveStarDegree(int nodeCount)
        {
            float ratio = math.saturate(math.isfinite(Profile.StarRatio01) ? Profile.StarRatio01 : 0.2f);
            return math.clamp((int)math.round(nodeCount * ratio), 1, 1000);
        }

        private int ResolveMainLimit(int nodeCount)
        {
            float islandRatio = math.saturate(math.isfinite(Profile.IslandRatio01) ? Profile.IslandRatio01 : 0.15f);
            float loopRatio = math.saturate(math.isfinite(Profile.LoopRatio01) ? Profile.LoopRatio01 : 0.78f);
            int islandBudget = math.clamp((int)math.round(nodeCount * islandRatio), 1, math.max(1, nodeCount / 2));
            int loopBudget = math.clamp((int)math.round(nodeCount * loopRatio), 3, nodeCount - islandBudget);
            return math.clamp(loopBudget, 3, nodeCount - islandBudget);
        }

        private float ResolveConductance(int source, int destination, int localEdge)
        {
            float3 sourceLocal = PowerJacobiStressAupMath.ToBaseLocalFloat3(NodeAup[source], BaseOriginAup);
            float3 destinationLocal = PowerJacobiStressAupMath.ToBaseLocalFloat3(NodeAup[destination], BaseOriginAup);
            float3 delta = destinationLocal - sourceLocal;
            float distSq = math.min(1000000f, math.lengthsq(delta));
            float distance = distSq <= 0.0001f ? 0f : distSq * math.rsqrt(math.max(distSq, 0.0001f));
            float baseResistance = 0.0001f + ((source + destination + localEdge) & 31) * 0.00037f;
            if ((source & 511) == 7 && localEdge == 0)
                baseResistance = float.PositiveInfinity;
            if ((source & 1023) == 33 && localEdge == 1)
                baseResistance = float.MaxValue;
            float resistance = baseResistance + distance * 0.00001f;
            if (!math.isfinite(resistance) || resistance >= float.MaxValue * 0.25f)
                return 0f;

            float paradoxBoost = source == destination ? 64f : 1f;
            float conductance = paradoxBoost * math.rcp(math.max(0.0001f, resistance));
            if ((source & 511) == 7 && localEdge == 0)
                return 0f;
            return math.min(PowerJacobiStressFuzzerConstants.MaximumConductance, conductance);
        }

        private double3 ResolveParadoxAup(int nodeIndex)
        {
            double wrapX = ((nodeIndex & 64) == 0 ? 1.0 : -1.0) * 1000000000.0;
            double wrapZ = ((nodeIndex & 128) == 0 ? -1.0 : 1.0) * 1000000000.0;
            double localX = (nodeIndex % 71) * 0.5;
            double localY = -4000.0 + ((nodeIndex % 19) * 0.25);
            double localZ = ((nodeIndex / 71) % 71) * 0.5;
            return BaseOriginAup + new double3(wrapX + localX, localY, wrapZ + localZ);
        }

        private void WriteCounts(int nodeCount, int edgeCount)
        {
            if (Counts.IsCreated && Counts.Length >= 2)
            {
                Counts[0] = nodeCount;
                Counts[1] = edgeCount;
            }
        }

        private static uint HashNode(int index)
        {
            uint value = (uint)(index + 1);
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value == 0u ? 1u : value;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InjectRandomPotentialsJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public JacobiFuzzPowerNodeDTO* NodesPtr;
        [NoAlias] public NativeArray<float> PotentialFront;
        [NoAlias] public NativeArray<float> PotentialBack;
        [NoAlias] public NativeArray<float> DemandRate;
        [NoAlias] public NativeArray<float> BatteryMilliRemainder;
        public int NodeCount;
        public int FrameIndex;
        public uint ProfileFlags;

        public void Execute()
        {
            if (NodesPtr == null)
                return;

            int nodeLimit = math.min(NodeCount, math.min(PotentialFront.Length, math.min(PotentialBack.Length, DemandRate.Length)));
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + nodeIndex);
                float stablePotential = ResolveStablePotential(nodeIndex);
                bool injectRawFaults = (ProfileFlags & PowerJacobiStressFuzzerConstants.ProfileFlagInjectRawFaults) != 0u;
                bool injectCorruptDtos = (ProfileFlags & PowerJacobiStressFuzzerConstants.ProfileFlagInjectCorruptNodeDto) != 0u;
                float demand = injectRawFaults ? ResolveHostileDemand(nodeIndex) : ResolveStableDemand(nodeIndex);

                if (FrameIndex == 0)
                {
                    float injectedPotential = injectRawFaults ? ResolveHostilePotential(nodeIndex, stablePotential) : stablePotential;
                    if (injectCorruptDtos && (nodeIndex & 1023) == 19)
                        node.InternalResistance = float.NaN;
                    if (injectCorruptDtos && (nodeIndex & 2047) == 91)
                        node.MaxCapacity = float.MaxValue;

                    PotentialFront[nodeIndex] = injectedPotential;
                    PotentialBack[nodeIndex] = 0f;
                    if ((uint)nodeIndex < (uint)BatteryMilliRemainder.Length)
                        BatteryMilliRemainder[nodeIndex] = 0f;
                    node.Potential = injectedPotential;
                }
                else
                {
                    float front = Sanitize01(PotentialFront[nodeIndex]);
                    float back = Sanitize01(PotentialBack[nodeIndex]);
                    PotentialFront[nodeIndex] = front;
                    PotentialBack[nodeIndex] = back;
                    node.Potential = front;
                }

                DemandRate[nodeIndex] = demand;
            }
        }

        private static float ResolveStablePotential(int nodeIndex)
        {
            uint hash = (uint)(nodeIndex + 1) * 747796405u + 2891336453u;
            hash = ((hash >> ((int)(hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            return (hash & 1023u) * (1f / 1023f);
        }

        private static float ResolveHostilePotential(int nodeIndex, float stablePotential)
        {
            if ((nodeIndex & 2047) == 0)
                return float.MaxValue;
            if ((nodeIndex & 1023) == 17)
                return float.NaN;
            if ((nodeIndex & 511) == 29)
                return float.NegativeInfinity;
            return stablePotential;
        }

        private static float ResolveStableDemand(int nodeIndex)
        {
            if ((nodeIndex & 127) == 9)
                return 1f;
            return ((nodeIndex * 13) & 255) * (1f / 255f);
        }

        private static float ResolveHostileDemand(int nodeIndex)
        {
            if ((nodeIndex & 255) == 5)
                return float.MaxValue;
            return ResolveStableDemand(nodeIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeFuzzerResultJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public JacobiFuzzPowerNodeDTO* NodesPtr;
        [ReadOnly, NoAlias] public NativeArray<float> LatestPotential;
        [NoAlias] public NativeArray<PowerJacobiStressFuzzerResult> Result;
        [NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> Telemetry;
        [ReadOnly, NoAlias] public NativeArray<int> GraphCounts;
        public int NodeCount;
        public int EdgeCount;
        public uint ExplicitGenerationDrainPresent;

        public void Execute()
        {
            if (NodesPtr == null || !Result.IsCreated || Result.Length <= 0)
                return;

            int nodeLimit = math.min(NodeCount, LatestPotential.IsCreated ? LatestPotential.Length : 0);
            float energy = 0f;
            uint hash = 2166136261u;
            for (int i = 0; i < nodeLimit; i++)
            {
                ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + i);
                float potential = Sanitize01(LatestPotential[i]);
                energy += potential + SanitizePositive(node.CurrentStorage);
                hash = Mix(hash, node.NodeHash);
                hash = Mix(hash, math.asuint(potential));
            }

            PowerJacobiStressFuzzerResult result = default;
            result.FinalStateHash = hash;
            result.NodeCount = nodeLimit;
            result.EdgeCount = ResolveActualEdgeCount();
            result.InitialEnergy = energy;
            result.FinalEnergy = energy;
            result.FirstFailureFrame = -1;
            result.FirstFailureNodeIndex = -1;
            result.ExplicitGenerationDrainPresent = ExplicitGenerationDrainPresent;
            Result[0] = result;

            if (Telemetry.IsCreated)
            {
                for (int i = 0; i < Telemetry.Length; i++)
                    Telemetry[i] = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private int ResolveActualEdgeCount()
        {
            if (GraphCounts.IsCreated && GraphCounts.Length > 1)
                return math.max(0, GraphCounts[1]);
            return math.max(0, EdgeCount);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct EvaluateHeadlessJacobiFuzzJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public JacobiFuzzPowerNodeDTO* NodesPtr;
        [ReadOnly, NoAlias] public NativeArray<double3> NodeAup;
        [ReadOnly, NoAlias] public NativeArray<int> NodeEdgeOffsets;
        [ReadOnly, NoAlias] public NativeArray<int> EdgeDestinations;
        [ReadOnly, NoAlias] public NativeArray<float> EdgeConductance;
        [NoAlias] public NativeArray<float> EdgeCurrentFlow;
        [NoAlias] public NativeArray<float> PotentialFront;
        [NoAlias] public NativeArray<float> PotentialBack;
        [ReadOnly, NoAlias] public NativeArray<float> DemandRate;
        [NoAlias] public NativeArray<float> BatteryMilliRemainder;
        [NoAlias] public NativeArray<float> VoltageHistory;
        [NoAlias] public NativeArray<float> RollbackFront;
        [NoAlias] public NativeArray<float> RollbackBack;
        [NoAlias] public NativeArray<PowerJacobiStressFuzzerResult> Result;
        [NoAlias] public NativeArray<JacobiFuzzStateDTO> State;
        [NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> StressTelemetry;
        [NoAlias] public NativeArray<JacobiFuzzTelemetryEntry> FuzzTelemetry;
        [ReadOnly, NoAlias] public NativeArray<int> GraphCounts;
        public PowerJacobiStressRunConfig Config;
        public int EdgeCount;

        public void Execute()
        {
            if (NodesPtr == null ||
                !Result.IsCreated ||
                Result.Length <= 0 ||
                !PotentialFront.IsCreated ||
                !PotentialBack.IsCreated)
            {
                return;
            }

            int nodeLimit = math.min(Config.NodeCount, math.min(PotentialFront.Length, PotentialBack.Length));
            if (nodeLimit <= 0)
                return;

            int iterationLimit = math.clamp(
                Config.IterationCount <= 0 ? PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount : Config.IterationCount,
                PowerJacobiStressFuzzerConstants.MinimumSolverIterationCount,
                PowerJacobiStressFuzzerConstants.MaximumSolverIterationCount);
            int rollbackStart = math.min(64, math.max(0, iterationLimit - 31));
            int rollbackEnd = math.min(iterationLimit - 1, rollbackStart + 29);
            float tolerance = math.max(0.000001f, Config.ResidualTolerance);
            NativeArray<float> readBuffer = PotentialFront;
            NativeArray<float> writeBuffer = PotentialBack;
            bool readIsFront = true;
            uint failureFlags = 0u;
            uint stateHash = 2166136261u;
            uint rollbackHash = 2166136261u;
            uint mitigationCount = 0u;
            int firstBadNode = -1;
            uint firstBadHash = 0u;
            double3 firstBadAup = default;
            int failingArrayOffset = -1;
            float previousResidual = float.MaxValue;
            float highestResidual = 0f;
            float finalResidual = 0f;
            float finalEnergy = 0f;
            int finalIterationCount = iterationLimit;
            int convergenceRun = 0;
            bool replayCompared = false;
            bool rollbackSnapshotValid = false;

            InitializeTelemetryRings();
            EdgeCount = ResolveActualEdgeCount();

            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + nodeIndex);
                float rawPotential = readBuffer[nodeIndex];
                if (!math.isfinite(rawPotential) ||
                    rawPotential > PowerJacobiStressFuzzerConstants.MaxVoltageThreshold ||
                    rawPotential < -PowerJacobiStressFuzzerConstants.MaxVoltageThreshold)
                {
                    failureFlags |= !math.isfinite(rawPotential)
                        ? (uint)PowerJacobiStressFuzzerConstants.FailureFlagNanVoltageDetected
                        : (uint)PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence;
                    CaptureFirstBad(nodeIndex, ref firstBadNode, ref firstBadHash, ref firstBadAup, ref failingArrayOffset);
                }

                if (DemandRate.IsCreated && (uint)nodeIndex < (uint)DemandRate.Length)
                {
                    float rawDemand = DemandRate[nodeIndex];
                    if (!math.isfinite(rawDemand) || rawDemand > PowerJacobiStressFuzzerConstants.MaxVoltageThreshold || rawDemand < 0f)
                    {
                        failureFlags |= !math.isfinite(rawDemand)
                            ? (uint)PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption
                            : (uint)PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence;
                        CaptureFirstBad(nodeIndex, ref firstBadNode, ref firstBadHash, ref firstBadAup, ref failingArrayOffset);
                    }
                }

                float safe = Sanitize01(rawPotential);
                readBuffer[nodeIndex] = safe;
                writeBuffer[nodeIndex] = safe;
                node.Potential = safe;
                if ((uint)nodeIndex < (uint)BatteryMilliRemainder.Length)
                    BatteryMilliRemainder[nodeIndex] = 0f;
            }

            for (int iteration = 0; iteration < iterationLimit; iteration++)
            {
                if (iteration == rollbackStart)
                {
                    CopyPotential(readBuffer, RollbackFront, nodeLimit);
                    rollbackSnapshotValid = true;
                }

                float omega = ResolveOmega(iteration, iterationLimit);
                if (iteration > rollbackEnd &&
                    previousResidual < float.MaxValue &&
                    finalResidual > math.max(previousResidual * 1.08f, previousResidual + tolerance * 0.25f))
                {
                    omega = math.max(PowerJacobiStressFuzzerConstants.OmegaMin, omega * 0.72f);
                    mitigationCount++;
                }

                float residual = 0f;
                float energy = 0f;
                float potentialSum = 0f;
                float minPotential = nodeLimit > 0 ? 1f : 0f;
                float maxPotential = 0f;
                stateHash = 2166136261u;

                for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
                {
                    float solvedPotential = SolveNodePotential(nodeIndex, iteration, rollbackStart, omega, readBuffer);
                    float previous = Sanitize01(readBuffer[nodeIndex]);
                    if (!math.isfinite(solvedPotential) ||
                        solvedPotential > PowerJacobiStressFuzzerConstants.MaxVoltageThreshold ||
                        solvedPotential < -PowerJacobiStressFuzzerConstants.MaxVoltageThreshold)
                    {
                        failureFlags |= !math.isfinite(solvedPotential)
                            ? (uint)PowerJacobiStressFuzzerConstants.FailureFlagNanVoltageDetected
                            : (uint)PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence;
                        CaptureFirstBad(nodeIndex, ref firstBadNode, ref firstBadHash, ref firstBadAup, ref failingArrayOffset);
                    }

                    solvedPotential = Sanitize01(solvedPotential);
                    writeBuffer[nodeIndex] = solvedPotential;
                    ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + nodeIndex);
                    bool corruptNodeDto = !math.isfinite(node.InternalResistance) ||
                                          !math.isfinite(node.MaxCapacity) ||
                                          !math.isfinite(node.CurrentStorage) ||
                                          node.MaxCapacity > 1000000f ||
                                          node.InternalResistance < 0f;
                    if (corruptNodeDto)
                    {
                        failureFlags |= (uint)PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption;
                        CaptureFirstBad(nodeIndex, ref firstBadNode, ref firstBadHash, ref firstBadAup, ref failingArrayOffset);
                    }

                    node.Potential = solvedPotential;
                    if (solvedPotential < PowerJacobiStressFuzzerConstants.BrownoutThreshold01)
                        node.Flags |= PowerJacobiStressFuzzerConstants.NodeFlagBrownout;
                    else
                        node.Flags &= ~PowerJacobiStressFuzzerConstants.NodeFlagBrownout;

                    residual = math.max(residual, math.abs(solvedPotential - previous));
                    energy += solvedPotential + SanitizePositive(node.CurrentStorage);
                    potentialSum += solvedPotential;
                    minPotential = math.min(minPotential, solvedPotential);
                    maxPotential = math.max(maxPotential, solvedPotential);
                    stateHash = Mix(Mix(stateHash, node.NodeHash), math.asuint(solvedPotential));

                    int historyOffset = (iteration * nodeLimit) + nodeIndex;
                    if ((uint)historyOffset < (uint)VoltageHistory.Length)
                        VoltageHistory[historyOffset] = solvedPotential;
                }

                NativeArray<float> swap = readBuffer;
                readBuffer = writeBuffer;
                writeBuffer = swap;
                readIsFront = !readIsFront;
                previousResidual = finalResidual;
                finalResidual = residual;
                highestResidual = math.max(highestResidual, residual);
                finalEnergy = energy;

                if (iteration == rollbackEnd)
                {
                    rollbackHash = ReplayRollbackAndCompare(
                        rollbackStart,
                        rollbackEnd,
                        readBuffer,
                        nodeLimit,
                        ref failureFlags,
                        ref firstBadNode,
                        ref firstBadHash,
                        ref firstBadAup,
                        ref failingArrayOffset);
                    replayCompared = true;
                }

                if (residual <= tolerance && iteration >= rollbackEnd)
                    convergenceRun++;
                else
                    convergenceRun = 0;

                WriteTelemetry(
                    iteration,
                    nodeLimit,
                    residual,
                    previousResidual,
                    energy,
                    potentialSum,
                    minPotential,
                    maxPotential,
                    omega,
                    failureFlags,
                    stateHash,
                    mitigationCount,
                    firstBadNode,
                    firstBadHash,
                    failingArrayOffset,
                    rollbackHash);

                if ((failureFlags &
                     ((uint)PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption |
                      (uint)PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence |
                      (uint)PowerJacobiStressFuzzerConstants.FailureFlagRollbackDesync)) != 0u)
                {
                    finalIterationCount = iteration + 1;
                    FillRemainingHistory(iteration + 1, iterationLimit, nodeLimit, readBuffer);
                    break;
                }

                if (convergenceRun >= 4)
                {
                    finalIterationCount = iteration + 1;
                    failureFlags |= (uint)PowerJacobiStressFuzzerConstants.FailureFlagEarlyConverged;
                    FillRemainingHistory(iteration + 1, iterationLimit, nodeLimit, readBuffer);
                    break;
                }
            }

            if (!replayCompared && rollbackSnapshotValid && iterationLimit > 1)
            {
                rollbackHash = ReplayRollbackAndCompare(
                    rollbackStart,
                    math.min(rollbackEnd, iterationLimit - 1),
                    readBuffer,
                    nodeLimit,
                    ref failureFlags,
                    ref firstBadNode,
                    ref firstBadHash,
                    ref firstBadAup,
                    ref failingArrayOffset);
            }

            if (!readIsFront)
                CopyPotential(readBuffer, PotentialFront, nodeLimit);

            WriteFinalEdgeFlows(readBuffer, nodeLimit);

            PowerJacobiStressFuzzerResult result = Result[0];
            uint reportedFailureFlags = failureFlags & ~(uint)PowerJacobiStressFuzzerConstants.FailureFlagEarlyConverged;
            if (result.FailureFlags == 0u && reportedFailureFlags != 0u)
            {
                result.FirstFailureFrame = finalIterationCount;
                result.FirstFailureNodeIndex = firstBadNode;
                result.FirstFailureNodeHash = firstBadHash;
                result.FirstFailureAup = firstBadAup;
            }

            result.FailureFlags |= reportedFailureFlags;
            result.FinalStateHash = stateHash;
            result.NodeCount = nodeLimit;
            result.EdgeCount = ResolveActualEdgeCount();
            result.FrameCount = finalIterationCount;
            result.IterationCount = finalIterationCount;
            result.FinalResidual = finalResidual;
            result.MaxResidual = highestResidual;
            result.FinalEnergy = finalEnergy;
            result.EnergyDeltaAbs = math.abs(finalEnergy - result.InitialEnergy);
            result.OscillationCount = mitigationCount;
            Result[0] = result;

            if (State.IsCreated && State.Length > 0)
            {
                JacobiFuzzStateDTO state = default;
                state.HighestResidualRecorded = highestResidual;
                state.FinalIterationCount = (uint)finalIterationCount;
                state.MismatchFlags = result.FailureFlags;
                State[0] = state;
            }
        }

        private float SolveNodePotential(int nodeIndex, int iteration, int rollbackStart, float omega, NativeArray<float> readBuffer)
        {
            ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + nodeIndex);
            uint flags = node.Flags;
            if ((flags & (PowerJacobiStressFuzzerConstants.NodeFlagOffline | PowerJacobiStressFuzzerConstants.NodeFlagDamaged)) != 0u)
                return 0f;

            int edgeReadLimit = math.min(EdgeDestinations.Length, EdgeConductance.Length);
            int edgeStart = math.clamp(NodeEdgeOffsets[nodeIndex], 0, edgeReadLimit);
            int edgeEnd = math.clamp(NodeEdgeOffsets[nodeIndex + 1], edgeStart, edgeReadLimit);
            float weightedPotential = 0f;
            float conductanceSum = 0f;
            for (int edgeCursor = edgeStart; edgeCursor < edgeEnd; edgeCursor++)
            {
                int destination = EdgeDestinations[edgeCursor];
                if ((uint)destination >= (uint)Config.NodeCount || (uint)destination >= (uint)readBuffer.Length)
                    continue;

                float conductance = math.clamp(
                    math.isfinite(EdgeConductance[edgeCursor]) ? EdgeConductance[edgeCursor] : 0f,
                    0f,
                    PowerJacobiStressFuzzerConstants.MaximumConductance);
                if (conductance <= PowerJacobiStressFuzzerConstants.MinimumConductance)
                    continue;

                weightedPotential += conductance * Sanitize01(readBuffer[destination]);
                conductanceSum += conductance;
            }

            float generatorRate = (flags & PowerJacobiStressFuzzerConstants.NodeFlagSource) != 0u ? 1f : 0f;
            float demand = DemandRate.IsCreated && (uint)nodeIndex < (uint)DemandRate.Length
                ? DemandRate[nodeIndex]
                : 0f;
            demand = ResolveRollbackDemand(nodeIndex, iteration, rollbackStart, demand);
            float targetPotential = (weightedPotential + generatorRate - demand) * math.rcp(math.max(conductanceSum + 1f, 1f));
            float currentPotential = Sanitize01(readBuffer[nodeIndex]);
            return currentPotential + (targetPotential - currentPotential) * omega;
        }

        private int ResolveActualEdgeCount()
        {
            if (GraphCounts.IsCreated && GraphCounts.Length > 1)
                return math.max(0, math.min(GraphCounts[1], math.min(EdgeDestinations.Length, EdgeConductance.Length)));
            return math.max(0, math.min(EdgeCount, math.min(EdgeDestinations.Length, EdgeConductance.Length)));
        }

        private uint ReplayRollbackAndCompare(
            int startIteration,
            int endIteration,
            NativeArray<float> target,
            int nodeLimit,
            ref uint failureFlags,
            ref int firstBadNode,
            ref uint firstBadHash,
            ref double3 firstBadAup,
            ref int failingArrayOffset)
        {
            CopyPotential(RollbackFront, RollbackBack, nodeLimit);
            NativeArray<float> replayRead = RollbackFront;
            NativeArray<float> replayWrite = RollbackBack;
            for (int iteration = startIteration; iteration <= endIteration; iteration++)
            {
                float omega = ResolveOmega(iteration, math.max(1, Config.IterationCount));
                for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
                    replayWrite[nodeIndex] = Sanitize01(SolveNodePotential(nodeIndex, iteration, startIteration, omega, replayRead));

                NativeArray<float> swap = replayRead;
                replayRead = replayWrite;
                replayWrite = swap;
            }

            uint hash = 2166136261u;
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                uint replayBits = math.asuint(Sanitize01(replayRead[nodeIndex]));
                uint targetBits = math.asuint(Sanitize01(target[nodeIndex]));
                hash = Mix(Mix(hash, replayBits), targetBits);
                if (replayBits != targetBits)
                {
                    failureFlags |= (uint)PowerJacobiStressFuzzerConstants.FailureFlagRollbackDesync;
                    CaptureFirstBad(nodeIndex, ref firstBadNode, ref firstBadHash, ref firstBadAup, ref failingArrayOffset);
                    break;
                }
            }

            return hash;
        }

        private void WriteFinalEdgeFlows(NativeArray<float> latestPotential, int nodeLimit)
        {
            int edgeReadLimit = math.min(EdgeDestinations.Length, math.min(EdgeConductance.Length, EdgeCurrentFlow.Length));
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                int edgeStart = math.clamp(NodeEdgeOffsets[nodeIndex], 0, edgeReadLimit);
                int edgeEnd = math.clamp(NodeEdgeOffsets[nodeIndex + 1], edgeStart, edgeReadLimit);
                float sourcePotential = Sanitize01(latestPotential[nodeIndex]);
                for (int edgeCursor = edgeStart; edgeCursor < edgeEnd; edgeCursor++)
                {
                    int destination = EdgeDestinations[edgeCursor];
                    float destinationPotential = (uint)destination < (uint)nodeLimit ? Sanitize01(latestPotential[destination]) : 0f;
                    float conductance = math.clamp(
                        math.isfinite(EdgeConductance[edgeCursor]) ? EdgeConductance[edgeCursor] : 0f,
                        0f,
                        PowerJacobiStressFuzzerConstants.MaximumConductance);
                    EdgeCurrentFlow[edgeCursor] = math.clamp(
                        (sourcePotential - destinationPotential) * conductance,
                        -PowerJacobiStressFuzzerConstants.MaximumEdgeCurrentAbs,
                        PowerJacobiStressFuzzerConstants.MaximumEdgeCurrentAbs);
                }
            }
        }

        private void FillRemainingHistory(int startIteration, int iterationLimit, int nodeLimit, NativeArray<float> latestPotential)
        {
            for (int iteration = startIteration; iteration < iterationLimit; iteration++)
            {
                int historyBase = iteration * nodeLimit;
                for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
                {
                    int historyOffset = historyBase + nodeIndex;
                    if ((uint)historyOffset < (uint)VoltageHistory.Length)
                        VoltageHistory[historyOffset] = latestPotential[nodeIndex];
                }
            }
        }

        private void WriteTelemetry(
            int iteration,
            int nodeLimit,
            float residual,
            float previousResidual,
            float energy,
            float potentialSum,
            float minPotential,
            float maxPotential,
            float omega,
            uint failureFlags,
            uint stateHash,
            uint mitigationCount,
            int firstBadNode,
            uint firstBadHash,
            int failingArrayOffset,
            uint rollbackHash)
        {
            if (StressTelemetry.IsCreated && StressTelemetry.Length > 0)
            {
                int index = iteration % StressTelemetry.Length;
                PowerJacobiStressFrameTelemetry entry = default;
                entry.FrameIndex = (uint)iteration;
                entry.StateHash = stateHash;
                entry.FailureFlags = failureFlags;
                entry.NodeCount = nodeLimit;
                entry.EdgeCount = math.max(0, EdgeCount);
                entry.IterationCount = iteration + 1;
                entry.Residual = residual;
                entry.PreviousResidual = previousResidual == float.MaxValue ? residual : previousResidual;
                entry.TotalEnergy = energy;
                entry.AveragePotential = nodeLimit > 0 ? potentialSum * math.rcp(nodeLimit) : 0f;
                entry.MinPotential = minPotential;
                entry.MaxPotential = maxPotential;
                entry.FirstBadNodeHash = firstBadHash;
                entry.FirstBadNodeIndex = firstBadNode;
                entry.SolverMicroseconds = 0f;
                StressTelemetry[index] = entry;
            }

            if (FuzzTelemetry.IsCreated && FuzzTelemetry.Length > 0)
            {
                int index = iteration % FuzzTelemetry.Length;
                JacobiFuzzTelemetryEntry entry = default;
                entry.IterationIndex = (uint)iteration;
                entry.StateHash = stateHash;
                entry.MismatchFlags = failureFlags;
                entry.MitigationCount = mitigationCount;
                entry.HighestResidual = residual;
                entry.PreviousResidual = previousResidual == float.MaxValue ? residual : previousResidual;
                entry.ActiveOmega = omega;
                entry.SolverMicroseconds = 0f;
                entry.TotalEnergy = energy;
                entry.RemainderDrift = 0f;
                entry.FirstBadNodeIndex = firstBadNode;
                entry.FirstBadNodeHash = firstBadHash;
                entry.FailingArrayOffset = failingArrayOffset;
                entry.RollbackHash = rollbackHash;
                entry.BrownoutNodeId = firstBadHash;
                FuzzTelemetry[index] = entry;
            }
        }

        private void InitializeTelemetryRings()
        {
            if (StressTelemetry.IsCreated)
            {
                for (int i = 0; i < StressTelemetry.Length; i++)
                {
                    PowerJacobiStressFrameTelemetry entry = default;
                    entry.FirstBadNodeIndex = -1;
                    StressTelemetry[i] = entry;
                }
            }

            if (FuzzTelemetry.IsCreated)
            {
                for (int i = 0; i < FuzzTelemetry.Length; i++)
                {
                    JacobiFuzzTelemetryEntry entry = default;
                    entry.FirstBadNodeIndex = -1;
                    entry.FailingArrayOffset = -1;
                    FuzzTelemetry[i] = entry;
                }
            }
        }

        private void CaptureFirstBad(
            int nodeIndex,
            ref int firstBadNode,
            ref uint firstBadHash,
            ref double3 firstBadAup,
            ref int failingArrayOffset)
        {
            if (firstBadNode >= 0)
                return;

            firstBadNode = nodeIndex;
            ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + nodeIndex);
            firstBadHash = node.NodeHash;
            firstBadAup = (uint)nodeIndex < (uint)NodeAup.Length ? NodeAup[nodeIndex] : default;
            failingArrayOffset = nodeIndex;
        }

        private static void CopyPotential(NativeArray<float> source, NativeArray<float> destination, int nodeLimit)
        {
            int limit = math.min(nodeLimit, math.min(source.IsCreated ? source.Length : 0, destination.IsCreated ? destination.Length : 0));
            for (int i = 0; i < limit; i++)
                destination[i] = source[i];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveOmega(int iteration, int iterationLimit)
        {
            float denominator = math.max(1f, iterationLimit - 1f);
            float phase = math.frac(iteration * 0.03125f);
            float triangle = 1f - math.abs((phase * 2f) - 1f);
            float ramp = math.saturate(iteration * math.rcp(denominator));
            float profile = math.saturate((triangle * 0.72f) + (ramp * 0.28f));
            return math.lerp(PowerJacobiStressFuzzerConstants.OmegaMin, PowerJacobiStressFuzzerConstants.OmegaMax, profile);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveRollbackDemand(int nodeIndex, int iteration, int rollbackStart, float baseDemand)
        {
            float demand = math.saturate(math.max(0f, math.isfinite(baseDemand) ? baseDemand : 0f));
            if (iteration >= rollbackStart && ((nodeIndex + 17) & 255) == 0)
                demand = math.saturate(demand + 0.125f);
            return demand;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct VerifyPowerConservationJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public JacobiFuzzPowerNodeDTO* NodesPtr;
        [ReadOnly, NoAlias] public NativeArray<float> DemandRate;
        [NoAlias] public NativeArray<PowerJacobiStressFuzzerResult> Result;
        [NoAlias] public NativeArray<JacobiFuzzStateDTO> State;
        [NoAlias] public NativeArray<JacobiFuzzTelemetryEntry> FuzzTelemetry;
        public int NodeCount;
        public float EnergyEpsilon;
        public uint ExplicitGenerationDrainPresent;
        public float AverageSolverMicroseconds;

        public void Execute()
        {
            if (NodesPtr == null || !Result.IsCreated || Result.Length <= 0)
                return;

            int nodeLimit = math.min(NodeCount, DemandRate.IsCreated ? DemandRate.Length : NodeCount);
            float generatedWatts = 0f;
            float consumedWatts = 0f;
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + nodeIndex);
                float potential = math.saturate(math.isfinite(node.Potential) ? node.Potential : 0f);
                float capacity = math.min(1000000f, math.max(0f, math.isfinite(node.MaxCapacity) ? node.MaxCapacity : 0f));
                if ((node.Flags & PowerJacobiStressFuzzerConstants.NodeFlagSource) != 0u)
                    generatedWatts += capacity * potential;
                if ((uint)nodeIndex < (uint)DemandRate.Length)
                    consumedWatts += math.saturate(math.max(0f, math.isfinite(DemandRate[nodeIndex]) ? DemandRate[nodeIndex] : 0f));
            }

            PowerJacobiStressFuzzerResult result = Result[0];
            float drift = math.abs((result.FinalEnergy - result.InitialEnergy) * 0.001f);
            float wattDrift = math.abs(generatedWatts - consumedWatts) * 0.001f;
            drift = math.max(drift, wattDrift);
            result.EnergyDeltaAbs = math.max(result.EnergyDeltaAbs, drift);
            result.AverageSolverMicroseconds = AverageSolverMicroseconds;
            if (ExplicitGenerationDrainPresent == 0u && drift > math.max(PowerJacobiStressFuzzerConstants.RemainderDriftEpsilon, EnergyEpsilon))
                result.FailureFlags |= (uint)PowerJacobiStressFuzzerConstants.FailureFlagRemainderDrift;
            Result[0] = result;

            if (FuzzTelemetry.IsCreated)
            {
                for (int i = 0; i < FuzzTelemetry.Length; i++)
                {
                    JacobiFuzzTelemetryEntry entry = FuzzTelemetry[i];
                    entry.SolverMicroseconds = AverageSolverMicroseconds;
                    entry.RemainderDrift = drift;
                    entry.MismatchFlags = result.FailureFlags;
                    FuzzTelemetry[i] = entry;
                }
            }

            if (State.IsCreated && State.Length > 0)
            {
                JacobiFuzzStateDTO state = State[0];
                state.MismatchFlags = result.FailureFlags;
                State[0] = state;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ValidateSolverConvergenceJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public JacobiFuzzPowerNodeDTO* NodesPtr;
        [ReadOnly, NoAlias] public NativeArray<double3> NodeAup;
        [ReadOnly, NoAlias] public NativeArray<float> LatestPotential;
        [ReadOnly, NoAlias] public NativeArray<float> PreviousPotential;
        [NoAlias] public NativeArray<PowerJacobiStressFuzzerResult> Result;
        [NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> Telemetry;
        public int NodeCount;
        public int EdgeCount;
        public int FrameIndex;
        public int IterationCount;
        public float ResidualTolerance;
        public float EnergyEpsilon;
        public float AverageSolverMicroseconds;
        public float PerformanceLimitMicroseconds;
        public uint ExplicitGenerationDrainPresent;

        public void Execute()
        {
            if (NodesPtr == null || !Result.IsCreated || Result.Length <= 0)
                return;

            int nodeLimit = math.min(NodeCount, math.min(LatestPotential.Length, PreviousPotential.Length));
            PowerJacobiStressFuzzerResult result = Result[0];
            float previousFrameResidual = result.FinalResidual;
            float residual = 0f;
            float energy = 0f;
            float potentialSum = 0f;
            float minPotential = nodeLimit > 0 ? 1f : 0f;
            float maxPotential = 0f;
            uint hash = 2166136261u;
            uint failureFlags = result.FailureFlags;
            int firstBadNode = -1;
            uint firstBadHash = 0u;
            double3 firstBadAup = default;

            for (int i = 0; i < nodeLimit; i++)
            {
                ref JacobiFuzzPowerNodeDTO node = ref UnsafeUtility.AsRef<JacobiFuzzPowerNodeDTO>(NodesPtr + i);
                float potential = LatestPotential[i];
                float previousPotential = PreviousPotential[i];
                bool nonFinite = math.isnan(potential) || !math.isfinite(potential) || !math.isfinite(previousPotential) || !math.isfinite(node.Potential);
                if (nonFinite && firstBadNode < 0)
                {
                    firstBadNode = i;
                    firstBadHash = node.NodeHash;
                    firstBadAup = (uint)i < (uint)NodeAup.Length ? NodeAup[i] : default;
                }

                float safePotential = Sanitize01(potential);
                float safePrevious = Sanitize01(previousPotential);
                residual = math.max(residual, math.abs(safePotential - safePrevious));
                energy += safePotential + SanitizePositive(node.CurrentStorage);
                potentialSum += safePotential;
                minPotential = math.min(minPotential, safePotential);
                maxPotential = math.max(maxPotential, safePotential);
                hash = Mix(hash, node.NodeHash);
                hash = Mix(hash, math.asuint(safePotential));
            }

            if (firstBadNode >= 0)
                failureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption;

            if (FrameIndex >= 100 && residual > math.max(ResidualTolerance, 0.0001f))
                failureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagDivergence;

            if (previousFrameResidual > 0f &&
                residual > math.max(previousFrameResidual + ResidualTolerance * 0.25f, previousFrameResidual * 1.08f))
            {
                result.OscillationCount++;
                if (result.OscillationCount >= 3u)
                    failureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagOscillation;
            }
            else if (result.OscillationCount > 0u)
            {
                result.OscillationCount--;
            }

            float energyDeltaAbs = math.abs(energy - result.InitialEnergy);
            if (ExplicitGenerationDrainPresent == 0u && energyDeltaAbs > math.max(0.0001f, EnergyEpsilon))
                failureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagThermodynamic;

            if (AverageSolverMicroseconds > PerformanceLimitMicroseconds)
                failureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagPerformance;

            if (result.FailureFlags == 0u && failureFlags != 0u)
            {
                result.FirstFailureFrame = FrameIndex;
                result.FirstFailureNodeIndex = firstBadNode;
                result.FirstFailureNodeHash = firstBadHash;
                result.FirstFailureAup = firstBadAup;
            }

            result.FailureFlags = failureFlags;
            result.FinalStateHash = hash;
            result.NodeCount = nodeLimit;
            result.EdgeCount = math.max(0, EdgeCount);
            result.FrameCount = FrameIndex + 1;
            result.IterationCount = IterationCount;
            result.FinalResidual = residual;
            result.MaxResidual = math.max(result.MaxResidual, residual);
            result.FinalEnergy = energy;
            result.EnergyDeltaAbs = energyDeltaAbs;
            result.AverageSolverMicroseconds = AverageSolverMicroseconds;
            result.ExplicitGenerationDrainPresent = ExplicitGenerationDrainPresent;
            Result[0] = result;

            if (Telemetry.IsCreated && Telemetry.Length > 0)
            {
                int telemetryIndex = FrameIndex % Telemetry.Length;
                PowerJacobiStressFrameTelemetry entry = default;
                entry.FrameIndex = (uint)FrameIndex;
                entry.StateHash = hash;
                entry.FailureFlags = failureFlags;
                entry.NodeCount = nodeLimit;
                entry.EdgeCount = math.max(0, EdgeCount);
                entry.IterationCount = IterationCount;
                entry.Residual = residual;
                entry.PreviousResidual = previousFrameResidual;
                entry.TotalEnergy = energy;
                entry.AveragePotential = nodeLimit > 0 ? potentialSum * math.rcp(nodeLimit) : 0f;
                entry.MinPotential = minPotential;
                entry.MaxPotential = maxPotential;
                entry.FirstBadNodeHash = firstBadHash;
                entry.FirstBadNodeIndex = firstBadNode;
                entry.SolverMicroseconds = AverageSolverMicroseconds;
                Telemetry[telemetryIndex] = entry;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositive(float value)
        {
            return math.isfinite(value) ? math.max(0f, value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
    }

    #if UNITY_EDITOR
    public static class PowerJacobiStressTopologyProfileParser
    {
        public static bool TryParse(ReadOnlySpan<byte> csvBytes, out PowerJacobiStressTopologyProfile profile)
        {
            profile = PowerJacobiStressFuzzer.CreateDefaultProfile();
            int lineStart = 0;
            while (lineStart < csvBytes.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < csvBytes.Length && csvBytes[lineEnd] != (byte)'\n' && csvBytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, lineEnd - lineStart);
                if (TryParseLine(line, out PowerJacobiStressTopologyProfile parsed))
                {
                    profile = parsed;
                    return true;
                }

                lineStart = lineEnd + 1;
                while (lineStart < csvBytes.Length && (csvBytes[lineStart] == (byte)'\n' || csvBytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return false;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out PowerJacobiStressTopologyProfile profile)
        {
            profile = default;
            line = Trim(line);
            if (line.Length == 0 || line[0] == (byte)'#')
                return false;

            ReadOnlySpan<byte> name = NextField(ref line);
            if (IsHeader(name))
                return false;

            profile.ProfileHash = Fnva32(name);
            profile.NodeCount = math.clamp(ParseInt(NextField(ref line), PowerJacobiStressFuzzerConstants.DefaultNodeCount), 5000, 10000);
            profile.EdgeCapacity = math.max(profile.NodeCount + 1, ParseInt(NextField(ref line), PowerJacobiStressFuzzerConstants.DefaultEdgeCapacity));
            profile.LoopRatio01 = math.saturate(ParseFloat(NextField(ref line), 0.78f));
            profile.StarRatio01 = math.saturate(ParseFloat(NextField(ref line), 0.20f));
            profile.IslandRatio01 = math.saturate(ParseFloat(NextField(ref line), 0.15f));
            ReadOnlySpan<byte> flagsField = NextField(ref line);
            profile.Flags = (uint)math.max(0, ParseInt(flagsField, 0));
            return profile.ProfileHash != 0u;
        }

        private static ReadOnlySpan<byte> NextField(ref ReadOnlySpan<byte> line)
        {
            int comma = line.IndexOf((byte)',');
            if (comma < 0)
            {
                ReadOnlySpan<byte> last = Trim(line);
                line = ReadOnlySpan<byte>.Empty;
                return last;
            }

            ReadOnlySpan<byte> field = Trim(line.Slice(0, comma));
            line = line.Slice(comma + 1);
            return field;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsSpace(value[start]))
                start++;
            while (end >= start && IsSpace(value[end]))
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsSpace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }

        private static bool IsHeader(ReadOnlySpan<byte> value)
        {
            return value.Length >= 4 &&
                   ToLower(value[0]) == (byte)'n' &&
                   ToLower(value[1]) == (byte)'a' &&
                   ToLower(value[2]) == (byte)'m' &&
                   ToLower(value[3]) == (byte)'e';
        }

        private static uint Fnva32(ReadOnlySpan<byte> bytes)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < bytes.Length; i++)
                hash = (hash ^ ToLower(bytes[i])) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static int ParseInt(ReadOnlySpan<byte> value, int fallback)
        {
            value = Trim(value);
            if (value.Length == 0)
                return fallback;

            int sign = 1;
            int index = 0;
            if (value[0] == (byte)'-')
            {
                sign = -1;
                index = 1;
            }

            int result = 0;
            bool any = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                any = true;
                result = (result * 10) + (value[index] - (byte)'0');
                index++;
            }

            return any ? result * sign : fallback;
        }

        private static float ParseFloat(ReadOnlySpan<byte> value, float fallback)
        {
            value = Trim(value);
            if (value.Length == 0)
                return fallback;

            int index = 0;
            float sign = 1f;
            if (value[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float result = 0f;
            bool any = false;
            while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
            {
                any = true;
                result = (result * 10f) + (value[index] - (byte)'0');
                index++;
            }

            if (index < value.Length && value[index] == (byte)'.')
            {
                index++;
                float place = 0.1f;
                while (index < value.Length && value[index] >= (byte)'0' && value[index] <= (byte)'9')
                {
                    any = true;
                    result += (value[index] - (byte)'0') * place;
                    place *= 0.1f;
                    index++;
                }
            }

            return any && math.isfinite(result) ? result * sign : fallback;
        }

        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }
    }
    #endif

    public static unsafe class PowerJacobiStressCsvExporter
    {
        public static void WriteFailureCsv(
            string path,
            NativeArray<JacobiFuzzPowerNodeDTO> nodes,
            NativeArray<double3> nodeAup,
            NativeArray<int> offsets,
            NativeArray<int> destinations,
            NativeArray<float> conductance,
            int edgeCount,
            NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return;

            EnsureDirectory(path);
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            int cursor = 0;
            AppendAscii(scratch, ref cursor, "nodeIndex,nodeHash,aupX,aupY,aupZ,edgeStart,edgeEnd,destination,conductance\n");
            FlushIfNeeded(stream, scratch, ref cursor, 256);

            int nodeLimit = math.min(nodes.Length, math.max(0, offsets.Length - 1));
            int safeEdgeCount = math.min(edgeCount, math.min(destinations.Length, conductance.Length));
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                int edgeStart = math.clamp(offsets[nodeIndex], 0, safeEdgeCount);
                int edgeEnd = math.clamp(offsets[nodeIndex + 1], edgeStart, safeEdgeCount);
                if (edgeStart == edgeEnd)
                {
                    AppendNodePrefix(scratch, ref cursor, nodes, nodeAup, nodeIndex, edgeStart, edgeEnd);
                    AppendAscii(scratch, ref cursor, "-1,0\n");
                    FlushIfNeeded(stream, scratch, ref cursor, 512);
                    continue;
                }

                for (int edgeIndex = edgeStart; edgeIndex < edgeEnd; edgeIndex++)
                {
                    AppendNodePrefix(scratch, ref cursor, nodes, nodeAup, nodeIndex, edgeStart, edgeEnd);
                    AppendInt(scratch, ref cursor, destinations[edgeIndex]);
                    AppendByte(scratch, ref cursor, (byte)',');
                    AppendFloatScaled(scratch, ref cursor, conductance[edgeIndex], 1000000f);
                    AppendByte(scratch, ref cursor, (byte)'\n');
                    FlushIfNeeded(stream, scratch, ref cursor, 512);
                }
            }

            Flush(stream, scratch, ref cursor);
        }

        private static void AppendNodePrefix(
            NativeArray<byte> scratch,
            ref int cursor,
            NativeArray<JacobiFuzzPowerNodeDTO> nodes,
            NativeArray<double3> nodeAup,
            int nodeIndex,
            int edgeStart,
            int edgeEnd)
        {
            AppendInt(scratch, ref cursor, nodeIndex);
            AppendByte(scratch, ref cursor, (byte)',');
            AppendUInt(scratch, ref cursor, nodes[nodeIndex].NodeHash);
            AppendByte(scratch, ref cursor, (byte)',');
            double3 aup = (uint)nodeIndex < (uint)nodeAup.Length ? nodeAup[nodeIndex] : default;
            AppendDoubleScaled(scratch, ref cursor, aup.x, 1000.0);
            AppendByte(scratch, ref cursor, (byte)',');
            AppendDoubleScaled(scratch, ref cursor, aup.y, 1000.0);
            AppendByte(scratch, ref cursor, (byte)',');
            AppendDoubleScaled(scratch, ref cursor, aup.z, 1000.0);
            AppendByte(scratch, ref cursor, (byte)',');
            AppendInt(scratch, ref cursor, edgeStart);
            AppendByte(scratch, ref cursor, (byte)',');
            AppendInt(scratch, ref cursor, edgeEnd);
            AppendByte(scratch, ref cursor, (byte)',');
        }

        internal static void AppendAscii(NativeArray<byte> scratch, ref int cursor, string value)
        {
            for (int i = 0; i < value.Length && cursor < scratch.Length; i++)
                scratch[cursor++] = (byte)(value[i] <= 127 ? value[i] : '?');
        }

        internal static void AppendByte(NativeArray<byte> scratch, ref int cursor, byte value)
        {
            if ((uint)cursor < (uint)scratch.Length)
                scratch[cursor++] = value;
        }

        internal static void AppendInt(NativeArray<byte> scratch, ref int cursor, int value)
        {
            if (value < 0)
            {
                AppendByte(scratch, ref cursor, (byte)'-');
                value = value == int.MinValue ? int.MaxValue : -value;
            }

            AppendUInt(scratch, ref cursor, (uint)value);
        }

        internal static void AppendUInt(NativeArray<byte> scratch, ref int cursor, uint value)
        {
            if (value == 0u)
            {
                AppendByte(scratch, ref cursor, (byte)'0');
                return;
            }

            int start = cursor;
            while (value > 0u && cursor < scratch.Length)
            {
                scratch[cursor++] = (byte)('0' + (value % 10u));
                value /= 10u;
            }

            Reverse(scratch, start, cursor - 1);
        }

        internal static void AppendFloatScaled(NativeArray<byte> scratch, ref int cursor, float value, float scale)
        {
            if (!math.isfinite(value))
            {
                AppendAscii(scratch, ref cursor, "nan");
                return;
            }

            AppendScaled(scratch, ref cursor, value, scale);
        }

        internal static void AppendDoubleScaled(NativeArray<byte> scratch, ref int cursor, double value, double scale)
        {
            if (!math.isfinite(value))
            {
                AppendAscii(scratch, ref cursor, "nan");
                return;
            }

            AppendScaled(scratch, ref cursor, value, scale);
        }

        private static void AppendScaled(NativeArray<byte> scratch, ref int cursor, double value, double scale)
        {
            if (value < 0.0)
            {
                AppendByte(scratch, ref cursor, (byte)'-');
                value = -value;
            }

            ulong scaled = (ulong)math.round(value * scale);
            ulong whole = scaled / (ulong)scale;
            ulong fraction = scaled - (whole * (ulong)scale);
            AppendULong(scratch, ref cursor, whole);
            AppendByte(scratch, ref cursor, (byte)'.');
            ulong divisor = (ulong)scale / 10UL;
            while (divisor > 0UL)
            {
                AppendByte(scratch, ref cursor, (byte)('0' + ((fraction / divisor) % 10UL)));
                divisor /= 10UL;
            }
        }

        private static void AppendULong(NativeArray<byte> scratch, ref int cursor, ulong value)
        {
            if (value == 0UL)
            {
                AppendByte(scratch, ref cursor, (byte)'0');
                return;
            }

            int start = cursor;
            while (value > 0UL && cursor < scratch.Length)
            {
                scratch[cursor++] = (byte)('0' + (value % 10UL));
                value /= 10UL;
            }

            Reverse(scratch, start, cursor - 1);
        }

        private static void Reverse(NativeArray<byte> scratch, int start, int end)
        {
            while (start < end)
            {
                byte tmp = scratch[start];
                scratch[start] = scratch[end];
                scratch[end] = tmp;
                start++;
                end--;
            }
        }

        internal static void FlushIfNeeded(FileStream stream, NativeArray<byte> scratch, ref int cursor, int margin)
        {
            if (cursor + margin >= scratch.Length)
                Flush(stream, scratch, ref cursor);
        }

        internal static void Flush(FileStream stream, NativeArray<byte> scratch, ref int cursor)
        {
            if (cursor <= 0)
                return;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
            stream.Write(new ReadOnlySpan<byte>(ptr, cursor));
            cursor = 0;
        }

        internal static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }

    public static unsafe class PowerJacobiStressBinaryDump
    {
        public static void WriteDump(
            string path,
            NativeArray<PowerJacobiStressFrameTelemetry> telemetry,
            NativeArray<JacobiFuzzTelemetryEntry> fuzzTelemetry,
            uint failureFlags)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !fuzzTelemetry.IsCreated || fuzzTelemetry.Length <= 0)
                return;

            PowerJacobiStressCsvExporter.EnsureDirectory(path);
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            PowerJacobiStressDumpHeader header = default;
            header.Magic0 = PowerJacobiStressFuzzerConstants.DumpMagic0;
            header.Magic1 = PowerJacobiStressFuzzerConstants.DumpMagic1;
            header.Version = PowerJacobiStressFuzzerConstants.DumpVersion;
            header.Flags = failureFlags;
            header.FrameTelemetryCount = (uint)telemetry.Length;
            header.FuzzTelemetryCount = (uint)fuzzTelemetry.Length;
            header.FrameTelemetryStride = (uint)UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>();
            header.FuzzTelemetryStride = (uint)UnsafeUtility.SizeOf<JacobiFuzzTelemetryEntry>();
            header.ResultStride = (uint)UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>();
            header.StateStride = (uint)UnsafeUtility.SizeOf<JacobiFuzzStateDTO>();
            header.BufferIdMin = (uint)PowerJacobiStressFuzzerBufferIds.Nodes;
            header.BufferIdMax = (uint)PowerJacobiStressFuzzerBufferIds.TopologyProfile;
            byte* headerPtr = (byte*)&header;
            stream.Write(new ReadOnlySpan<byte>(headerPtr, UnsafeUtility.SizeOf<PowerJacobiStressDumpHeader>()));
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            int bytes = telemetry.Length * UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>();
            stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
            byte* fuzzPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(fuzzTelemetry);
            int fuzzBytes = fuzzTelemetry.Length * UnsafeUtility.SizeOf<JacobiFuzzTelemetryEntry>();
            stream.Write(new ReadOnlySpan<byte>(fuzzPtr, fuzzBytes));
        }
    }

    public static class PowerJacobiStressReportWriter
    {
        public static void WriteSuccessReport(string path, in PowerJacobiStressFuzzerResult result, NativeArray<byte> scratch)
        {
            if (!scratch.IsCreated || scratch.Length <= 0)
                return;

            PowerJacobiStressCsvExporter.EnsureDirectory(path);
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            int cursor = 0;
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, "{\n  \"Jacobi Stability Verified\": true,\n  \"nodeCount\": ");
            PowerJacobiStressCsvExporter.AppendInt(scratch, ref cursor, result.NodeCount);
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, ",\n  \"edgeCount\": ");
            PowerJacobiStressCsvExporter.AppendInt(scratch, ref cursor, result.EdgeCount);
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, ",\n  \"frameCount\": ");
            PowerJacobiStressCsvExporter.AppendInt(scratch, ref cursor, result.FrameCount);
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, ",\n  \"finalResidual\": ");
            PowerJacobiStressCsvExporter.AppendFloatScaled(scratch, ref cursor, result.FinalResidual, 1000000f);
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, ",\n  \"averageSolverMicroseconds\": ");
            PowerJacobiStressCsvExporter.AppendFloatScaled(scratch, ref cursor, result.AverageSolverMicroseconds, 1000f);
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, ",\n  \"managedBytesDelta\": ");
            PowerJacobiStressCsvExporter.AppendInt(scratch, ref cursor, result.ManagedBytesDelta > int.MaxValue ? int.MaxValue : (int)result.ManagedBytesDelta);
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, "\n}\n");
            PowerJacobiStressCsvExporter.Flush(stream, scratch, ref cursor);
        }
    }

}
