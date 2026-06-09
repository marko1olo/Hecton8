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
    /// MapMagic base height adapter for the HECTON macro geology source fields.
    /// </summary>
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Macro Geology Base",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonSandboxAbyssalShelfMapMagicNode : Generator, IOutlet<MatrixWorld>
    {
        private const string NativeMemoryOwner = nameof(HectonSandboxAbyssalShelfMapMagicNode);
        private const double SyncCompletionWarningMilliseconds = 0.2;
        private static readonly uint _invalidMatrixWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.InvalidMatrix"));
        private static readonly uint _syncCompletionWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SyncCompletionMs"));
        private static readonly uint _mapMagicContextHash =
            unchecked((uint)LocHash.Compute("MapMagic.SandboxAbyssalShelf"));

        [Den.Tools.GUI.ValAttribute("High Y m")] public float highWorldY = 2000f;
        [Den.Tools.GUI.ValAttribute("Low Y m")] public float lowWorldY = -10000f;
        [Den.Tools.GUI.ValAttribute("Descent Radius m")] public float descentRadiusMeters = 15000f;
        [Den.Tools.GUI.ValAttribute("Exponential Falloff")] public float macroExponentialFalloff = 3.1f;
        [Den.Tools.GUI.ValAttribute("Shelf Run m")] public float shelfRunMeters = 15000f;
        [Den.Tools.GUI.ValAttribute("Shelf Slope deg")] public float shelfTargetSlopeDegrees = 30f;

        [Den.Tools.GUI.ValAttribute("Plate Cell m")] public float plateCellSizeMeters = 4200f;
        [Den.Tools.GUI.ValAttribute("Ridge Height m")] public float ridgeHeightMeters = 700f;
        [Den.Tools.GUI.ValAttribute("Ridge Multiplier")] public float ridgeMultiplier = 0.08f;
        [Den.Tools.GUI.ValAttribute("Ridge Width m")] public float ridgeWidthMeters = 1450f;
        [Den.Tools.GUI.ValAttribute("Junction Width m")] public float junctionWidthMeters = 2800f;
        [Den.Tools.GUI.ValAttribute("Plate Uniformity")] public float plateUniformity = 0.78f;
        [Den.Tools.GUI.ValAttribute("Warp m")] public float domainWarpMeters = 1450f;
        [Den.Tools.GUI.ValAttribute("Warp Frequency")] public float domainWarpFrequency = 0.00011f;
        [Den.Tools.GUI.ValAttribute("Slope Noise Frequency")] public float slopeNoiseFrequency = 0.00003125f;
        [Den.Tools.GUI.ValAttribute("Trench Depth m")] public float trenchDepthMeters = 5000f;
        [Den.Tools.GUI.ValAttribute("Trench Width m")] public float trenchWidthMeters = 780f;
        [Den.Tools.GUI.ValAttribute("Trench Sharpness")] public float trenchSharpness = 2.4f;
        [Den.Tools.GUI.ValAttribute("Island Radius m")] public float islandCenterRadiusMeters = 2600f;
        [Den.Tools.GUI.ValAttribute("Island Junction")] public float islandJunctionThreshold = 0.58f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = WorldMacroGeologyFields.DefaultAuthoringSeed;

        [Den.Tools.GUI.ValAttribute("Quantize Slopes")] public bool enableSlopeQuantization = true;
        [Den.Tools.GUI.ValAttribute("Plateau Source deg")] public float plateauSourceAngleDegrees = 8f;
        [Den.Tools.GUI.ValAttribute("Plateau Target deg")] public float plateauTargetAngleDegrees = 2.5f;
        [Den.Tools.GUI.ValAttribute("Cliff Source deg")] public float cliffSourceAngleDegrees = 45f;
        [Den.Tools.GUI.ValAttribute("Cliff Target deg")] public float cliffTargetAngleDegrees = 52f;
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
            int rawHeightsRegistrationId = 0;
            int quantizedHeightsRegistrationId = 0;
            JobHandle generationHandle = default;
            bool generationHandleScheduled = false;
            try
            {
                rawHeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                quantizedHeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                rawHeightsRegistrationId = RegisterTempJobArray(rawHeights, nameof(rawHeights));
                quantizedHeightsRegistrationId = RegisterTempJobArray(quantizedHeights, nameof(quantizedHeights));

                double sampleCellSizeMeters = ResolveCellSizeMeters(dst);
                AbsoluteUniversePosition worldOriginAup = HectonSandboxAbyssalShelfMath.BuildAupXZ(
                    dst.worldPos.x,
                    dst.worldPos.z,
                    AbsoluteUniversePosition.CellSizeMeters);
                uint worldSeed = WorldMacroGeologyFields.CombineWorldSeed(
                    unchecked((uint)seed),
                    ResolveRuntimeWorldSeed());
                var parameters = new HectonSandboxAbyssalShelfParams
                {
                    AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters,
                    DescentRadiusMeters = math.max(15000.0, shelfRunMeters > 0f ? shelfRunMeters : descentRadiusMeters),
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
                    SlopeNoiseFrequency = math.max(0.000001f, slopeNoiseFrequency),
                    MacroExponentialFalloff = math.max(0.1f, macroExponentialFalloff),
                    ShelfRunMeters = math.max(1f, shelfRunMeters),
                    ShelfTargetSlopeDegrees = math.clamp(shelfTargetSlopeDegrees, 1f, 75f),
                    TrenchDepthMeters = math.max(0f, trenchDepthMeters),
                    TrenchWidthMeters = math.max(1f, trenchWidthMeters),
                    TrenchSharpness = math.max(0.35f, trenchSharpness),
                    IslandCenterRadiusMeters = math.max(1f, islandCenterRadiusMeters),
                    IslandJunctionThreshold = math.saturate(islandJunctionThreshold),
                    Seed = worldSeed,
                    MacroGeologyArtifactVersion = WorldMacroGeologyFields.ArtifactVersion
                };

                var baseJob = new HectonSandboxAbyssalShelfBaseJob
                {
                    OutputHeights01 = rawHeights,
                    Parameters = parameters,
                    Width = width,
                    WorldOriginAup = worldOriginAup,
                    CellSizeMeters = sampleCellSizeMeters
                };

                generationHandle = baseJob.Schedule(cellCount, ResolveBatchCount(cellCount));
                generationHandleScheduled = true;
                NativeArray<float> finalHeights = rawHeights;

                if (enableSlopeQuantization && width > 2 && height > 2)
                {
                    float plateauTargetAngle = math.clamp(plateauTargetAngleDegrees, 1f, 60f);
                    float plateauSourceAngle = math.clamp(plateauSourceAngleDegrees, plateauTargetAngle + 0.001f, 45f);
                    float cliffSourceAngle = math.clamp(cliffSourceAngleDegrees, plateauSourceAngle + 0.001f, 62f);
                    double centerAupX = worldOriginAup.GridX * (double)AbsoluteUniversePosition.CellSizeMeters +
                        worldOriginAup.LocalX +
                        dst.worldSize.x * 0.5;
                    double centerAupZ = worldOriginAup.GridZ * (double)AbsoluteUniversePosition.CellSizeMeters +
                        worldOriginAup.LocalZ +
                        dst.worldSize.z * 0.5;
                    float noisyCliffTargetAngle = HectonSandboxAbyssalShelfMath.EvaluateSlopeTargetAngleDegrees(
                        new double2(centerAupX, centerAupZ),
                        in parameters);
                    float cliffTargetLimit = math.min(math.max(22f, cliffTargetAngleDegrees), 62f);
                    float cliffTargetAngle = math.clamp(math.min(noisyCliffTargetAngle, cliffTargetLimit), 22f, 38f);
                    float cliffRampEndAngle = math.min(89f, cliffSourceAngle + math.max(1f, 62f - cliffSourceAngle));
                    var quantizeJob = new HectonSandboxSlopeQuantizationJob
                    {
                        InputHeights01 = rawHeights,
                        OutputHeights01 = quantizedHeights,
                        Width = width,
                        Height = height,
                        CellSizeMeters = (float)math.max(0.001, sampleCellSizeMeters),
                        LowWorldY = parameters.LowWorldY,
                        HighWorldY = parameters.HighWorldY,
                        PlateauSourceGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(plateauSourceAngle),
                        PlateauTargetGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(plateauTargetAngle),
                        CliffSourceGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffSourceAngle),
                        CliffRampEndGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffRampEndAngle),
                        CliffTargetGradient = HectonSandboxAbyssalShelfMath.SlopeAngleDegreesToGradient(cliffTargetAngle),
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

                DisposeTracked(ref rawHeights, ref rawHeightsRegistrationId);
                DisposeTracked(ref quantizedHeights, ref quantizedHeightsRegistrationId);
            }
        }

        private static int RegisterTempJobArray(NativeArray<float> array, string label)
        {
            int registrationId = NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeAllocationLifetime.TempJob);
            if (registrationId <= 0)
                throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");

            return registrationId;
        }

        private static void DisposeTracked(ref NativeArray<float> array, ref int registrationId)
        {
            System.Exception cleanupException = null;

            if (registrationId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(registrationId);
                }
                catch (System.Exception exception)
                {
                    cleanupException = exception;
                }
                finally
                {
                    registrationId = 0;
                }
            }

            if (array.IsCreated)
            {
                try
                {
                    array.Dispose();
                }
                catch (System.Exception exception)
                {
                    if (cleanupException == null)
                        cleanupException = exception;
                }
                finally
                {
                    array = default;
                }
            }
            else
            {
                array = default;
            }

            if (cleanupException != null)
                throw cleanupException;
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

        private static int ResolveRuntimeWorldSeed()
        {
            return global::HectonWorldGenerator.TryGetActiveRuntimeWorldSeed(out int runtimeWorldSeed)
                ? runtimeWorldSeed
                : 0;
        }
    }
}
