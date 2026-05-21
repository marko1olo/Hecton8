using System.Runtime.InteropServices;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.Thermodynamics;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

public sealed class PowerGridJacobiStressFuzzerEditTests
{
    [Test]
    public void StressFuzzerDtos_AreArm64Aligned()
    {
        Assert.IsTrue(PowerJacobiStressFuzzer.ValidateRequiredLayouts());
        Assert.AreEqual(32, UnsafeUtility.SizeOf<PowerNodeDTO>());
        Assert.AreEqual(32, UnsafeUtility.SizeOf<FluidCompartmentDTO>());
        Assert.AreEqual(16, UnsafeUtility.SizeOf<ThermalCellDTO>());
        Assert.AreEqual(16, UnsafeUtility.SizeOf<ThermalSolverConvergenceStateDTO>());
        Assert.AreEqual(64, UnsafeUtility.SizeOf<ThermalResidualSlot64>());
        Assert.AreEqual(32, UnsafeUtility.SizeOf<PowerJacobiStressTopologyProfile>());
        Assert.AreEqual(64, UnsafeUtility.SizeOf<PowerJacobiStressFrameTelemetry>());
        Assert.AreEqual(128, UnsafeUtility.SizeOf<PowerJacobiStressFuzzerResult>());
        Assert.AreEqual(0, Marshal.OffsetOf<PowerJacobiStressFuzzerResult>(nameof(PowerJacobiStressFuzzerResult.FailureFlags)).ToInt32());
        Assert.AreEqual(64, Marshal.OffsetOf<PowerJacobiStressFuzzerResult>(nameof(PowerJacobiStressFuzzerResult.ManagedBytesDelta)).ToInt32());
        Assert.AreEqual(88, Marshal.OffsetOf<PowerJacobiStressFuzzerResult>(nameof(PowerJacobiStressFuzzerResult.FirstFailureAup)).ToInt32());
        Assert.AreEqual(112, Marshal.OffsetOf<PowerJacobiStressFuzzerResult>(nameof(PowerJacobiStressFuzzerResult.ExplicitGenerationDrainPresent)).ToInt32());
    }

    [Test]
    public void StressFuzzer_QualityWeightMapsContinuouslyToIterationBudget()
    {
        Assert.AreEqual(1, PowerJacobiStressFuzzer.ResolveQualityIterationCount(0f));
        Assert.GreaterOrEqual(PowerJacobiStressFuzzer.ResolveQualityIterationCount(0.5f), 4);
        Assert.AreEqual(8, PowerJacobiStressFuzzer.ResolveQualityIterationCount(1f));
    }

    [Test]
    public void StressFuzzer_LoadsColdCsvTopologyProfile()
    {
        bool loaded = PowerJacobiStressFuzzer.TryLoadTopologyProfile(
            "Assets/_Project/Data/fuzzer_topology_profiles.csv",
            out PowerJacobiStressTopologyProfile profile);

        Assert.IsTrue(loaded);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, profile.NodeCount);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultEdgeCapacity, profile.EdgeCapacity);
    }

    [Test]
    public void HostileCsrGraph_ConvergesWithoutNanOscillationOrEnergyFault()
    {
        bool passed = PowerJacobiStressFuzzer.RunDefault(out PowerJacobiStressFuzzerResult result);

        Assert.AreEqual(0u, result.FailureFlags, "FailureFlags=" + result.FailureFlags +
            " residual=" + result.FinalResidual +
            " avgSolverUs=" + result.AverageSolverMicroseconds +
            " managedBytesDelta=" + result.ManagedBytesDelta);
        Assert.IsTrue(passed);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultNodeCount, result.NodeCount);
        Assert.AreEqual(PowerJacobiStressFuzzerConstants.DefaultFrameCount, result.FrameCount);
    }
}
