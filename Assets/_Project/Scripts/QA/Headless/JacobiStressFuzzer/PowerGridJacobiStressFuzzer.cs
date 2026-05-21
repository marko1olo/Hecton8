using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Physics;
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
        public const float DefaultResidualTolerance = 0.025f;
        public const float DefaultEnergyEpsilon = 0.5f;
        public const float DefaultPerformanceLimitMicroseconds = 200f;
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

#if UNITY_EDITOR
    public static class PowerJacobiStressFuzzerState
    {
        public static PowerJacobiStressFuzzerResult LastResult;
        public static double3 LastFailureAup;
        public static uint LastFailureNodeHash;
        public static bool HasFailure;
    }
#endif

    public static unsafe class PowerJacobiStressFuzzer
    {
        private const string CsvFailurePath = "Docs/Reports/HEADLESS_JACOBI_FAILURES.csv";
        private const string SuccessReportPath = "Docs/Reports/QA_OPTIMIZATION_REPORT.json";
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_255.bin";
        private const string ProfileCsvPath = "Assets/_Project/Data/fuzzer_topology_profiles.csv";

        public static bool RunDefault(out PowerJacobiStressFuzzerResult result)
        {
            PowerJacobiStressTopologyProfile profile = CreateDefaultProfile();
            TryLoadTopologyProfile(ProfileCsvPath, out profile);
            PowerJacobiStressRunConfig config = CreateDefaultConfig(profile);
            return Run(in config, in profile, CsvFailurePath, SuccessReportPath, DumpPath, out result);
        }

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
            int iterationCount = config.IterationCount > 0
                ? math.clamp(config.IterationCount, 1, 8)
                : ResolveQualityIterationCount(globalQualityWeight);
            PowerJacobiStressRunConfig safeConfig = config;
            safeConfig.NodeCount = nodeCount;
            safeConfig.EdgeCapacity = edgeCapacity;
            safeConfig.FrameCount = frameCount;
            safeConfig.IterationCount = iterationCount;
            safeConfig.GlobalQualityWeight = globalQualityWeight;
            safeConfig.ResidualTolerance = SanitizePositiveOrDefault(config.ResidualTolerance, PowerJacobiStressFuzzerConstants.DefaultResidualTolerance);
            safeConfig.EnergyEpsilon = SanitizePositiveOrDefault(config.EnergyEpsilon, PowerJacobiStressFuzzerConstants.DefaultEnergyEpsilon);
            safeConfig.PerformanceLimitMicroseconds = SanitizePositiveOrDefault(config.PerformanceLimitMicroseconds, PowerJacobiStressFuzzerConstants.DefaultPerformanceLimitMicroseconds);

            NativeArray<PowerNodeDTO> nodes = default;
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

            try
            {
                nodes = new NativeArray<PowerNodeDTO>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                nodeAup = new NativeArray<double3>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                offsets = new NativeArray<int>(nodeCount + 1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                destinations = new NativeArray<int>(edgeCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                conductance = new NativeArray<float>(edgeCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                edgeFlow = new NativeArray<float>(edgeCapacity, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                potentialFront = new NativeArray<float>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                potentialBack = new NativeArray<float>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                demandRate = new NativeArray<float>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                batteryRemainder = new NativeArray<float>(nodeCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                resultBuffer = new NativeArray<PowerJacobiStressFuzzerResult>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                telemetry = new NativeArray<PowerJacobiStressFrameTelemetry>(PowerJacobiStressFuzzerConstants.TelemetryFrameCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                graphCounts = new NativeArray<int>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                csvScratch = new NativeArray<byte>(PowerJacobiStressFuzzerConstants.CsvScratchBytes, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

                WarmBurst(nodes, nodeAup, offsets, destinations, conductance, edgeFlow, potentialFront, potentialBack, demandRate, batteryRemainder, resultBuffer, telemetry, graphCounts, in safeConfig, in profile);

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long loopTicksStart = Stopwatch.GetTimestamp();
                long solverTicks = 0L;

                NativeArray<float> front = potentialFront;
                NativeArray<float> back = potentialBack;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    JobHandle preSimulationHandle = new InjectRandomPotentialsJob
                    {
                        NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                        PotentialFront = front,
                        PotentialBack = back,
                        DemandRate = demandRate,
                        BatteryMilliRemainder = batteryRemainder,
                        NodeCount = nodeCount,
                        FrameIndex = frame
                    }.Schedule();
                    preSimulationHandle.Complete();

                    JobHandle solverHandle = default;
                    long solverStart = Stopwatch.GetTimestamp();
                    for (int iteration = 0; iteration < iterationCount; iteration++)
                    {
                        solverHandle = new PowerVoltageSolverJob
                        {
                            NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                            NodeEdgeOffsets = offsets,
                            EdgeDestinations = destinations,
                            EdgeConductance = conductance,
                            FrontPotential = front,
                            DemandRate = demandRate,
                            BackPotential = back,
                            NodeCount = nodeCount,
                            GlobalQualityWeight = globalQualityWeight,
                            SmoothingFactor = PowerSolverConvergenceMath.ResolveSolverOmega(globalQualityWeight)
                        }.Schedule(nodeCount, PowerJacobiStressFuzzerConstants.DefaultBatchSize, solverHandle);

                        NativeArray<float> swap = front;
                        front = back;
                        back = swap;
                    }

                    solverHandle.Complete();
                    solverTicks += Stopwatch.GetTimestamp() - solverStart;

                    JobHandle simulationHandle = new IntegrateBatteryChargeJob
                    {
                        NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                        NodeEdgeOffsets = offsets,
                        EdgeDestinations = destinations,
                        EdgeConductance = conductance,
                        EdgeCurrentFlow = edgeFlow,
                        BatteryMilliRemainder = batteryRemainder,
                        NodeCount = nodeCount,
                        DeltaTimeSeconds = 1f / 60f
                    }.Schedule(nodeCount, PowerJacobiStressFuzzerConstants.DefaultBatchSize);
                    simulationHandle.Complete();

                    float solverMicroseconds = TicksToMicroseconds(solverTicks, frame + 1);
                    JobHandle postSimulationHandle = new ValidateSolverConvergenceJob
                    {
                        NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
                        NodeAup = nodeAup,
                        LatestPotential = front,
                        PreviousPotential = back,
                        Result = resultBuffer,
                        Telemetry = telemetry,
                        NodeCount = nodeCount,
                        EdgeCount = graphCounts[1],
                        FrameIndex = frame,
                        IterationCount = iterationCount,
                        ResidualTolerance = safeConfig.ResidualTolerance,
                        EnergyEpsilon = safeConfig.EnergyEpsilon,
                        AverageSolverMicroseconds = solverMicroseconds,
                        PerformanceLimitMicroseconds = safeConfig.PerformanceLimitMicroseconds,
                        ExplicitGenerationDrainPresent = safeConfig.ExplicitGenerationDrainPresent
                    }.Schedule();
                    postSimulationHandle.Complete();
                }

                long loopTicks = Stopwatch.GetTimestamp() - loopTicksStart;
                long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
                result = resultBuffer[0];
                result.ManagedBytesDelta = allocatedAfter - allocatedBefore;
                result.SolverTicks = solverTicks;
                result.LoopTicks = loopTicks;
                result.AverageSolverMicroseconds = TicksToMicroseconds(solverTicks, frameCount);
                if (result.ManagedBytesDelta != 0L)
                    result.FailureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagManagedAllocation;
                if (result.AverageSolverMicroseconds > safeConfig.PerformanceLimitMicroseconds)
                    result.FailureFlags |= PowerJacobiStressFuzzerConstants.FailureFlagPerformance;
                resultBuffer[0] = result;

                if (result.FailureFlags != 0u)
                {
                    PowerJacobiStressCsvExporter.WriteFailureCsv(csvFailurePath, nodes, nodeAup, offsets, destinations, conductance, graphCounts[1], csvScratch);
                    if ((result.FailureFlags & PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption) != 0u)
                        PowerJacobiStressBinaryDump.WriteDump(dumpPath, telemetry, csvScratch);
                }
                else
                {
                    PowerJacobiStressReportWriter.WriteSuccessReport(successReportPath, in result, csvScratch);
                }

#if UNITY_EDITOR
                PowerJacobiStressFuzzerState.LastResult = result;
                PowerJacobiStressFuzzerState.LastFailureAup = result.FirstFailureAup;
                PowerJacobiStressFuzzerState.LastFailureNodeHash = result.FirstFailureNodeHash;
                PowerJacobiStressFuzzerState.HasFailure = result.FailureFlags != 0u && result.FirstFailureNodeHash != 0u;
#endif
                return result.FailureFlags == 0u;
            }
            finally
            {
                if (csvScratch.IsCreated) csvScratch.Dispose();
                if (graphCounts.IsCreated) graphCounts.Dispose();
                if (telemetry.IsCreated) telemetry.Dispose();
                if (resultBuffer.IsCreated) resultBuffer.Dispose();
                if (batteryRemainder.IsCreated) batteryRemainder.Dispose();
                if (demandRate.IsCreated) demandRate.Dispose();
                if (potentialBack.IsCreated) potentialBack.Dispose();
                if (potentialFront.IsCreated) potentialFront.Dispose();
                if (edgeFlow.IsCreated) edgeFlow.Dispose();
                if (conductance.IsCreated) conductance.Dispose();
                if (destinations.IsCreated) destinations.Dispose();
                if (offsets.IsCreated) offsets.Dispose();
                if (nodeAup.IsCreated) nodeAup.Dispose();
                if (nodes.IsCreated) nodes.Dispose();
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
            profile.Flags = 1u;
            return profile;
        }

        internal static PowerJacobiStressRunConfig CreateDefaultConfig(in PowerJacobiStressTopologyProfile profile)
        {
            PowerJacobiStressRunConfig config = default;
            config.NodeCount = profile.NodeCount > 0 ? profile.NodeCount : PowerJacobiStressFuzzerConstants.DefaultNodeCount;
            config.EdgeCapacity = profile.EdgeCapacity > 0 ? profile.EdgeCapacity : PowerJacobiStressFuzzerConstants.DefaultEdgeCapacity;
            config.FrameCount = PowerJacobiStressFuzzerConstants.DefaultFrameCount;
            config.GlobalQualityWeight = 1f;
            config.IterationCount = ResolveQualityIterationCount(config.GlobalQualityWeight);
            config.ResidualTolerance = PowerJacobiStressFuzzerConstants.DefaultResidualTolerance;
            config.EnergyEpsilon = PowerJacobiStressFuzzerConstants.DefaultEnergyEpsilon;
            config.PerformanceLimitMicroseconds = PowerJacobiStressFuzzerConstants.DefaultPerformanceLimitMicroseconds;
            config.BaseOriginAup = new double3(9000000000.0, -4000.0, -9000000000.0);
            config.ExplicitGenerationDrainPresent = 1u;
            return config;
        }

        public static bool ValidateRequiredLayouts()
        {
            return UnsafeUtility.SizeOf<PowerNodeDTO>() == PowerGridJacobiConstants.PowerNodeDtoSizeBytes &&
                   UnsafeUtility.SizeOf<FluidCompartmentDTO>() == 32 &&
                   FluidCompartmentLayoutValidator.ValidateFluidCompartmentLayout() &&
                   UnsafeUtility.SizeOf<PowerJacobiStressTopologyProfile>() == 32 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>() == 64 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>() == 128 &&
                   UnsafeUtility.SizeOf<PowerJacobiStressRunConfig>() == 64;
        }

        public static int ResolveQualityIterationCount(float globalQualityWeight)
        {
            float q = SanitizeQuality(globalQualityWeight);
            float curve = math.smoothstep(0f, 1f, q);
            return math.clamp((int)math.round(math.lerp(1f, 8f, curve)), 1, 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeQuality(float globalQualityWeight)
        {
            return math.saturate(math.isfinite(globalQualityWeight) ? globalQualityWeight : 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizePositiveOrDefault(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void WarmBurst(
            NativeArray<PowerNodeDTO> nodes,
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

            new PowerVoltageSolverJob
            {
                NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                NodeEdgeOffsets = offsets,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                FrontPotential = potentialFront,
                DemandRate = demandRate,
                BackPotential = potentialBack,
                NodeCount = config.NodeCount,
                GlobalQualityWeight = config.GlobalQualityWeight,
                SmoothingFactor = PowerSolverConvergenceMath.ResolveSolverOmega(config.GlobalQualityWeight)
            }.Schedule(config.NodeCount, PowerJacobiStressFuzzerConstants.DefaultBatchSize).Complete();

            new IntegrateBatteryChargeJob
            {
                NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                NodeEdgeOffsets = offsets,
                EdgeDestinations = destinations,
                EdgeConductance = conductance,
                EdgeCurrentFlow = edgeFlow,
                BatteryMilliRemainder = batteryRemainder,
                NodeCount = config.NodeCount,
                DeltaTimeSeconds = 1f / 60f
            }.Schedule(config.NodeCount, PowerJacobiStressFuzzerConstants.DefaultBatchSize).Complete();

            new ValidateSolverConvergenceJob
            {
                NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
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
            NativeArray<PowerNodeDTO> nodes,
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
                NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(nodes),
                PotentialFront = potentialFront,
                PotentialBack = potentialBack,
                DemandRate = demandRate,
                BatteryMilliRemainder = batteryRemainder,
                NodeCount = config.NodeCount,
                FrameIndex = 0
            }.Schedule().Complete();

            new InitializeFuzzerResultJob
            {
                NodesPtr = (PowerNodeDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(nodes),
                LatestPotential = potentialFront,
                Result = resultBuffer,
                Telemetry = telemetry,
                NodeCount = config.NodeCount,
                EdgeCount = graphCounts[1],
                ExplicitGenerationDrainPresent = config.ExplicitGenerationDrainPresent
            }.Schedule().Complete();
        }

        private static float TicksToMicroseconds(long ticks, int divisor)
        {
            double us = (double)ticks * 1000000.0 / Stopwatch.Frequency;
            return (float)(us / math.max(1, divisor));
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct GenerateHostileCsrGraphJob : IJob
    {
        [NoAlias] public NativeArray<PowerNodeDTO> Nodes;
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
                PowerNodeDTO node = default;
                node.NodeHash = HashNode(nodeIndex);
                node.Potential = 0f;
                node.MaxCapacity = (nodeIndex % 257) == 0 ? 250000f : 1000f + ((nodeIndex & 31) * 37f);
                node.CurrentStorage = (nodeIndex % 41) == 0 ? 128f : 0f;
                node.Flags = PowerGridJacobiConstants.NodeFlagActive |
                             ((nodeIndex % 257) == 0 ? PowerGridJacobiConstants.NodeFlagSource : 0u) |
                             ((nodeIndex % 41) == 0 ? PowerGridJacobiConstants.NodeFlagBattery : 0u);
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
            double3 sourceAup = NodeAup[source];
            double3 destinationAup = NodeAup[destination];
            double3 delta = destinationAup - sourceAup;
            float distSq = (float)math.min(1000000.0, math.lengthsq(delta));
            float distance = distSq <= 0.0001f ? 0f : distSq * math.rsqrt(math.max(distSq, 0.0001f));
            float baseResistance = 0.0001f + ((source + destination + localEdge) & 31) * 0.00037f;
            float paradoxBoost = source == destination ? 64f : 1f;
            float conductance = paradoxBoost * math.rcp(math.max(0.0001f, baseResistance + distance * 0.00001f));
            if ((source & 511) == 7 && localEdge == 0)
                return float.NaN;
            if ((source & 1023) == 33 && localEdge == 1)
                return float.PositiveInfinity;
            return math.min(10000f, conductance);
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

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InjectRandomPotentialsJob : IJob
    {
        [NoAlias, NativeDisableUnsafePtrRestriction] public PowerNodeDTO* NodesPtr;
        [NoAlias] public NativeArray<float> PotentialFront;
        [NoAlias] public NativeArray<float> PotentialBack;
        [NoAlias] public NativeArray<float> DemandRate;
        [NoAlias] public NativeArray<float> BatteryMilliRemainder;
        public int NodeCount;
        public int FrameIndex;

        public void Execute()
        {
            if (NodesPtr == null)
                return;

            int nodeLimit = math.min(NodeCount, math.min(PotentialFront.Length, math.min(PotentialBack.Length, DemandRate.Length)));
            for (int nodeIndex = 0; nodeIndex < nodeLimit; nodeIndex++)
            {
                ref PowerNodeDTO node = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + nodeIndex);
                float stablePotential = ResolveStablePotential(nodeIndex);
                float demand = ResolveDemand(nodeIndex);

                if (FrameIndex == 0)
                {
                    float injectedPotential = ResolveHostilePotential(nodeIndex, stablePotential);
                    if ((nodeIndex & 1023) == 19)
                        node.InternalResistance = float.NaN;
                    if ((nodeIndex & 2047) == 91)
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

        private static float ResolveDemand(int nodeIndex)
        {
            if ((nodeIndex & 255) == 5)
                return float.MaxValue;
            if ((nodeIndex & 127) == 9)
                return 1f;
            return ((nodeIndex * 13) & 255) * (1f / 255f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize01(float value)
        {
            return math.saturate(math.isfinite(value) ? value : 0f);
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct InitializeFuzzerResultJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public PowerNodeDTO* NodesPtr;
        [ReadOnly, NoAlias] public NativeArray<float> LatestPotential;
        [NoAlias] public NativeArray<PowerJacobiStressFuzzerResult> Result;
        [NoAlias] public NativeArray<PowerJacobiStressFrameTelemetry> Telemetry;
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
                ref PowerNodeDTO node = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + i);
                float potential = Sanitize01(LatestPotential[i]);
                energy += potential + SanitizePositive(node.CurrentStorage);
                hash = Mix(hash, node.NodeHash);
                hash = Mix(hash, math.asuint(potential));
            }

            PowerJacobiStressFuzzerResult result = default;
            result.FinalStateHash = hash;
            result.NodeCount = nodeLimit;
            result.EdgeCount = math.max(0, EdgeCount);
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
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public unsafe struct ValidateSolverConvergenceJob : IJob
    {
        [ReadOnly, NoAlias, NativeDisableUnsafePtrRestriction] public PowerNodeDTO* NodesPtr;
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
                ref PowerNodeDTO node = ref UnsafeUtility.AsRef<PowerNodeDTO>(NodesPtr + i);
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
            profile.Flags = 1u;
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

    public static unsafe class PowerJacobiStressCsvExporter
    {
        public static void WriteFailureCsv(
            string path,
            NativeArray<PowerNodeDTO> nodes,
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
            NativeArray<PowerNodeDTO> nodes,
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
        public static void WriteDump(string path, NativeArray<PowerJacobiStressFrameTelemetry> telemetry, NativeArray<byte> scratch)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0 || !scratch.IsCreated || scratch.Length < 16)
                return;

            PowerJacobiStressCsvExporter.EnsureDirectory(path);
            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            int cursor = 0;
            PowerJacobiStressCsvExporter.AppendAscii(scratch, ref cursor, "H8JACOBI");
            PowerJacobiStressCsvExporter.AppendInt(scratch, ref cursor, telemetry.Length);
            PowerJacobiStressCsvExporter.Flush(stream, scratch, ref cursor);
            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
            int bytes = telemetry.Length * UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>();
            stream.Write(new ReadOnlySpan<byte>(ptr, bytes));
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
