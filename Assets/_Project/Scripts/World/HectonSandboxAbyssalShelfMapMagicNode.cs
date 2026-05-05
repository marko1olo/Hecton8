using Den.Tools.Matrices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.World;
using MapMagic.Nodes;
using MapMagic.Products;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapMagic.Nodes.MatrixGenerators
{
    /// <summary>
    /// Sandbox-only MapMagic base height generator for the HECTON planetary shelf foundation.
    /// </summary>
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Sandbox Abyssal Shelf Base",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonSandboxAbyssalShelfMapMagicNode : Generator, IOutlet<MatrixWorld>
    {
        private const string NativeMemoryOwner = nameof(HectonSandboxAbyssalShelfMapMagicNode);
        private const double SyncCompletionWarningMilliseconds = 4.0;
        private static readonly uint _invalidMatrixWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.InvalidMatrix"));
        private static readonly uint _syncCompletionWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SyncCompletionMs"));
        private static readonly uint _mapMagicContextHash =
            unchecked((uint)LocHash.Compute("MapMagic.SandboxAbyssalShelf"));

        [Den.Tools.GUI.ValAttribute("High Y m")] public float highWorldY = 2000f;
        [Den.Tools.GUI.ValAttribute("Low Y m")] public float lowWorldY = -5000f;
        [Den.Tools.GUI.ValAttribute("Descent Radius m")] public float descentRadiusMeters = 17500f;
        [Den.Tools.GUI.ValAttribute("Exponential Falloff")] public float macroExponentialFalloff = 3.1f;

        [Den.Tools.GUI.ValAttribute("Plate Cell m")] public float plateCellSizeMeters = 4200f;
        [Den.Tools.GUI.ValAttribute("Ridge Height m")] public float ridgeHeightMeters = 700f;
        [Den.Tools.GUI.ValAttribute("Ridge Multiplier")] public float ridgeMultiplier = 0.08f;
        [Den.Tools.GUI.ValAttribute("Ridge Width m")] public float ridgeWidthMeters = 1450f;
        [Den.Tools.GUI.ValAttribute("Junction Width m")] public float junctionWidthMeters = 2800f;
        [Den.Tools.GUI.ValAttribute("Plate Uniformity")] public float plateUniformity = 0.78f;
        [Den.Tools.GUI.ValAttribute("Warp m")] public float domainWarpMeters = 1450f;
        [Den.Tools.GUI.ValAttribute("Warp Frequency")] public float domainWarpFrequency = 0.00011f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = 880031;

        [Den.Tools.GUI.ValAttribute("Quantize Slopes")] public bool enableSlopeQuantization = true;
        [Den.Tools.GUI.ValAttribute("Flat Dead deg")] public float plateauSourceAngleDegrees = 4f;
        [Den.Tools.GUI.ValAttribute("Target Slope deg")] public float plateauTargetAngleDegrees = 30f;
        [Den.Tools.GUI.ValAttribute("Steep Source deg")] public float cliffSourceAngleDegrees = 40f;
        [Den.Tools.GUI.ValAttribute("Steep Full deg")] public float cliffTargetAngleDegrees = 58f;
        [Den.Tools.GUI.ValAttribute("Quantize Strength")] public float slopeQuantizationStrength = 1f;

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            if (stop != null && stop.stop)
                return;

            if (!enabled)
            {
                data.RemoveProduct(this);
                return;
            }

            MatrixWorld dst = new MatrixWorld(
                data.area.full.rect,
                data.area.full.worldPos,
                data.area.full.worldSize,
                data.globals.height);

            float[] target = dst.arr;
            int cellCount = target != null ? target.Length : 0;
            int width = math.max(1, dst.rect.size.x);
            int height = math.max(1, dst.rect.size.z);
            if (cellCount <= 0 || width * height > cellCount)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _invalidMatrixWarningHash,
                    _mapMagicContextHash,
                    cellCount);
                data.RemoveProduct(this);
                return;
            }

            NativeArray<float> rawHeights = default;
            NativeArray<float> quantizedHeights = default;
            JobHandle generationHandle = default;
            bool generationHandleScheduled = false;
            try
            {
                rawHeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                quantizedHeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobArray(rawHeights, nameof(rawHeights));
                RegisterTempJobArray(quantizedHeights, nameof(quantizedHeights));

                double sampleCellSizeMeters = ResolveCellSizeMeters(dst);
                var parameters = new HectonSandboxAbyssalShelfParams
                {
                    AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters,
                    DescentRadiusMeters = math.max(15000.0, descentRadiusMeters),
                    PlateCellSizeMeters = math.max(1f, plateCellSizeMeters),
                    HighWorldY = math.max(highWorldY, lowWorldY + 1f),
                    LowWorldY = lowWorldY,
                    RidgeHeightMeters = math.max(0f, ridgeHeightMeters),
                    RidgeMultiplier = math.max(0f, ridgeMultiplier),
                    RidgeWidthMeters = math.max(0.001f, ridgeWidthMeters),
                    JunctionWidthMeters = math.max(0.001f, junctionWidthMeters),
                    PlateUniformity = math.saturate(plateUniformity),
                    DomainWarpMeters = math.max(0f, domainWarpMeters),
                    DomainWarpFrequency = math.max(0.000001f, domainWarpFrequency),
                    MacroExponentialFalloff = math.max(0.1f, macroExponentialFalloff),
                    Seed = unchecked((uint)seed)
                };

                var baseJob = new HectonSandboxAbyssalShelfBaseJob
                {
                    OutputHeights01 = rawHeights,
                    Parameters = parameters,
                    Width = width,
                    WorldOriginAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                        dst.worldPos.x,
                        dst.worldPos.z,
                        parameters.AupCellSizeMeters),
                    CellSizeMeters = sampleCellSizeMeters
                };

                generationHandle = baseJob.Schedule(cellCount, ResolveBatchCount(cellCount));
                generationHandleScheduled = true;
                NativeArray<float> finalHeights = rawHeights;

                if (enableSlopeQuantization && width > 2 && height > 2)
                {
                    var quantizeJob = new HectonSandboxSlopeQuantizationJob
                    {
                        InputHeights01 = rawHeights,
                        OutputHeights01 = quantizedHeights,
                        Width = width,
                        Height = height,
                        CellSizeMeters = (float)math.max(0.001, sampleCellSizeMeters),
                        LowWorldY = parameters.LowWorldY,
                        HighWorldY = parameters.HighWorldY,
                        PlateauSourceAngleDegrees = plateauSourceAngleDegrees,
                        PlateauTargetAngleDegrees = plateauTargetAngleDegrees,
                        CliffSourceAngleDegrees = cliffSourceAngleDegrees,
                        CliffTargetAngleDegrees = cliffTargetAngleDegrees,
                        Strength = slopeQuantizationStrength
                    };

                    generationHandle = quantizeJob.Schedule(cellCount, ResolveBatchCount(cellCount), generationHandle);
                    finalHeights = quantizedHeights;
                }

                long completeStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                DispatcherJobSwap.TryComplete(ref generationHandle, forceComplete: true);
                generationHandleScheduled = false;
                double completeMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp() - completeStartTimestamp) *
                    1000.0 /
                    System.Diagnostics.Stopwatch.Frequency;
                if (completeMilliseconds > SyncCompletionWarningMilliseconds)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _syncCompletionWarningHash,
                        _mapMagicContextHash,
                        (float)completeMilliseconds);
                }

                if (stop != null && stop.stop)
                    return;

                NativeArray<float>.Copy(finalHeights, target, cellCount);

                data.StoreProduct(this, dst);
            }
            finally
            {
                if (generationHandleScheduled)
                    DispatcherJobSwap.TryComplete(ref generationHandle, forceComplete: true);

                if (rawHeights.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(rawHeights);
                    rawHeights.Dispose();
                }

                if (quantizedHeights.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(quantizedHeights);
                    quantizedHeights.Dispose();
                }
            }
        }

        private static void RegisterTempJobArray(NativeArray<float> array, string label)
        {
            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeAllocationLifetime.TempJob);
        }

        private static double ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001, matrix.worldSize.x / safeWidth);
        }

        private static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }
    }
}
