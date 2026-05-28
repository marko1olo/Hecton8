using Hecton8.Power;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public sealed class PowerGridJacobiStressFuzzerEditTests
{
    [Test]
    public void StressFuzzerDtos_AreArm64Aligned()
    {
        JacobiFuzzPowerNodeDTO node = default;
        JacobiFuzzStateDTO state = default;
        PowerJacobiStressDumpHeader dumpHeader = default;
        PowerJacobiStressFuzzerResult result = default;
        PowerJacobiStressRunConfig config = default;

        Assert.IsTrue(PowerJacobiStressFuzzer.ValidateRequiredLayouts());
        Assert.AreEqual(32, UnsafeUtility.SizeOf<JacobiFuzzPowerNodeDTO>());
        Assert.AreEqual(4, UnsafeUtility.AlignOf<JacobiFuzzPowerNodeDTO>());
        Assert.AreEqual(0, ByteOffset(ref node, ref node.NodeHash));
        Assert.AreEqual(4, ByteOffset(ref node, ref node.Potential));
        Assert.AreEqual(8, ByteOffset(ref node, ref node.MaxCapacity));
        Assert.AreEqual(12, ByteOffset(ref node, ref node.CurrentStorage));
        Assert.AreEqual(16, ByteOffset(ref node, ref node.Flags));
        Assert.AreEqual(20, ByteOffset(ref node, ref node.InternalResistance));
        Assert.AreEqual(32, UnsafeUtility.SizeOf<JacobiFuzzStateDTO>());
        Assert.AreEqual(0, ByteOffset(ref state, ref state.HighestResidualRecorded));
        Assert.AreEqual(4, ByteOffset(ref state, ref state.FinalIterationCount));
        Assert.AreEqual(8, ByteOffset(ref state, ref state.MismatchFlags));
        Assert.AreEqual(32, UnsafeUtility.SizeOf<PowerJacobiStressTopologyProfile>());
        Assert.AreEqual(64, UnsafeUtility.SizeOf<JacobiFuzzTelemetryEntry>());
        Assert.AreEqual(64, UnsafeUtility.SizeOf<PowerJacobiStressDumpHeader>());
        Assert.AreEqual(0, ByteOffset(ref dumpHeader, ref dumpHeader.Magic0));
        Assert.AreEqual(12, ByteOffset(ref dumpHeader, ref dumpHeader.Flags));
        Assert.AreEqual(16, ByteOffset(ref dumpHeader, ref dumpHeader.FrameTelemetryCount));
        Assert.AreEqual(40, ByteOffset(ref dumpHeader, ref dumpHeader.BufferIdMin));
        Assert.AreEqual(64, UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>());
        Assert.AreEqual(128, UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>());
        Assert.AreEqual(0, UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>() & 7);
        Assert.AreEqual(0, ByteOffset(ref result, ref result.FailureFlags));
        Assert.AreEqual(64, ByteOffset(ref result, ref result.ManagedBytesDelta));
        Assert.AreEqual(88, ByteOffset(ref result, ref result.FirstFailureAup));
        Assert.AreEqual(112, ByteOffset(ref result, ref result.ExplicitGenerationDrainPresent));
        Assert.AreEqual(64, UnsafeUtility.SizeOf<PowerJacobiStressRunConfig>());
        Assert.AreEqual(0, UnsafeUtility.SizeOf<PowerJacobiStressRunConfig>() & 7);
        Assert.AreEqual(32, ByteOffset(ref config, ref config.BaseOriginAup));
    }

    [Test]
    public void StressFuzzer_MandatoryCoverageConstantsRemainFixed()
    {
        Assert.AreEqual(5000, PowerJacobiStressFuzzerConstants.DefaultNodeCount);
        Assert.AreEqual(1000, PowerJacobiStressFuzzerConstants.DefaultFrameCount);
        Assert.AreEqual(50, PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount);
    }

    [Test]
    public void StressFuzzer_LoadsColdCsvTopologyProfile()
    {
        bool loaded = PowerJacobiStressFuzzer.TryLoadTopologyProfile(
            "Assets/_Project/Data/jacobi_fuzz_profiles.csv",
            out PowerJacobiStressTopologyProfile profile);

        Assert.IsTrue(loaded);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, profile.NodeCount);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultEdgeCapacity, profile.EdgeCapacity);
        Assert.AreEqual(0u, profile.Flags);
    }

    [Test]
    public void HostileCsrGraph_DefaultRunExercisesSolverBeforeForensicFaults()
    {
        bool passed = PowerJacobiStressFuzzer.RunDefault(out PowerJacobiStressFuzzerResult result);

        Assert.AreEqual(result.FailureFlags == 0u, passed);
        uint forensicFlags = (uint)(PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption |
                                    PowerJacobiStressFuzzerConstants.FailureFlagNanVoltageDetected |
                                    PowerJacobiStressFuzzerConstants.FailureFlagRollbackDesync |
                                    PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence);
        Assert.AreEqual(0u, result.FailureFlags & forensicFlags);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, result.NodeCount);
        Assert.GreaterOrEqual(result.FrameCount, PowerJacobiStressFuzzerConstants.MinimumSolverIterationCount);
        Assert.LessOrEqual(result.FrameCount, PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount);
        Assert.GreaterOrEqual(result.IterationCount, PowerJacobiStressFuzzerConstants.MinimumSolverIterationCount);
        Assert.LessOrEqual(result.IterationCount, PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount);
    }

    [Test]
    public void HostileCsrGraph_GlobalQualityWeightScalesIterationBudgetWithoutGc()
    {
        PowerJacobiStressTopologyProfile profile = PowerJacobiStressFuzzer.CreateDefaultProfile();
        PowerJacobiStressRunConfig looseConfig = CreateTestConfig(in profile);
        looseConfig.GlobalQualityWeight = 0f;
        looseConfig.IterationCount = 0;
        looseConfig.PerformanceLimitMicroseconds = float.MaxValue;

        PowerJacobiStressRunConfig strictConfig = looseConfig;
        strictConfig.GlobalQualityWeight = 1f;

        bool loosePassed = PowerJacobiStressFuzzer.Run(
            in looseConfig,
            in profile,
            "Docs/Reports/POWER_GRID_1422_LOOSE_FAILURES.csv",
            "Docs/Reports/POWER_GRID_1422_LOOSE_SUCCESS.json",
            "Docs/AgentLogs/Dump_1422_PowerGrid_Loose.bin",
            out PowerJacobiStressFuzzerResult looseResult);

        bool strictPassed = PowerJacobiStressFuzzer.Run(
            in strictConfig,
            in profile,
            "Docs/Reports/POWER_GRID_1422_STRICT_FAILURES.csv",
            "Docs/Reports/POWER_GRID_1422_STRICT_SUCCESS.json",
            "Docs/AgentLogs/Dump_1422_PowerGrid_Strict.bin",
            out PowerJacobiStressFuzzerResult strictResult);

        Assert.AreEqual(looseResult.FailureFlags == 0u, loosePassed);
        Assert.AreEqual(strictResult.FailureFlags == 0u, strictPassed);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, looseResult.NodeCount);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, strictResult.NodeCount);
        Assert.AreEqual(0L, looseResult.ManagedBytesDelta);
        Assert.AreEqual(0L, strictResult.ManagedBytesDelta);
        Assert.Less(looseResult.IterationCount, strictResult.IterationCount);
        Assert.LessOrEqual(looseResult.IterationCount, 3);
        Assert.GreaterOrEqual(strictResult.IterationCount, 40);
    }

    [Test]
    public void HostileCsrGraph_ReportsForensicFailureForInjectedFaults()
    {
        PowerJacobiStressTopologyProfile profile = PowerJacobiStressFuzzer.CreateDefaultProfile();
        profile.Flags = PowerJacobiStressFuzzerConstants.ProfileFlagForensicFaults;
        PowerJacobiStressRunConfig config = CreateTestConfig(in profile);

        bool passed = PowerJacobiStressFuzzer.Run(
            in config,
            in profile,
            "Docs/Reports/HEADLESS_JACOBI_FAILURES.csv",
            "Docs/Reports/QA_OPTIMIZATION_REPORT_SHINOBU_356.json",
            "Docs/AgentLogs/Dump_SHINOBU_356.bin",
            out PowerJacobiStressFuzzerResult result);

        Assert.IsFalse(passed);
        Assert.AreNotEqual(0u, result.FailureFlags);
        uint forensicFlags = (uint)(PowerJacobiStressFuzzerConstants.FailureFlagMathCorruption |
                                    PowerJacobiStressFuzzerConstants.FailureFlagNanVoltageDetected |
                                    PowerJacobiStressFuzzerConstants.FailureFlagInfiniteDivergence);
        Assert.AreNotEqual(0u, result.FailureFlags & forensicFlags);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, result.NodeCount);
        Assert.GreaterOrEqual(result.FrameCount, 1);
        Assert.LessOrEqual(result.FrameCount, PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount);
        Assert.GreaterOrEqual(result.IterationCount, 1);
        Assert.LessOrEqual(result.IterationCount, PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount);
    }

    private static PowerJacobiStressRunConfig CreateTestConfig(in PowerJacobiStressTopologyProfile profile)
    {
        PowerJacobiStressRunConfig config = default;
        config.NodeCount = profile.NodeCount;
        config.EdgeCapacity = profile.EdgeCapacity;
        config.FrameCount = PowerJacobiStressFuzzerConstants.DefaultFrameCount;
        config.IterationCount = PowerJacobiStressFuzzerConstants.DefaultSolverIterationCount;
        config.GlobalQualityWeight = 1f;
        config.ResidualTolerance = PowerJacobiStressFuzzerConstants.DefaultResidualTolerance;
        config.EnergyEpsilon = PowerJacobiStressFuzzerConstants.DefaultEnergyEpsilon;
        config.PerformanceLimitMicroseconds = PowerJacobiStressFuzzerConstants.DefaultPerformanceLimitMicroseconds;
        config.BaseOriginAup = new double3(9000000000.0, -4000.0, -9000000000.0);
        config.ExplicitGenerationDrainPresent = 1u;
        return config;
    }

    private static unsafe int ByteOffset<TStruct, TField>(ref TStruct owner, ref TField field)
        where TStruct : struct
        where TField : struct
    {
        return (int)((byte*)UnsafeUtility.AddressOf(ref field) - (byte*)UnsafeUtility.AddressOf(ref owner));
    }
}
