using Den.Tools.Matrices;
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
        [Den.Tools.GUI.ValAttribute("High Y m")] public float highWorldY = 2000f;
        [Den.Tools.GUI.ValAttribute("Low Y m")] public float lowWorldY = -5000f;
        [Den.Tools.GUI.ValAttribute("Descent Radius m")] public float descentRadiusMeters = 16500f;

        [Den.Tools.GUI.ValAttribute("Plate Cell m")] public float plateCellSizeMeters = 2200f;
        [Den.Tools.GUI.ValAttribute("Ridge Height m")] public float ridgeHeightMeters = 1750f;
        [Den.Tools.GUI.ValAttribute("Ridge Multiplier")] public float ridgeMultiplier = 0.22f;
        [Den.Tools.GUI.ValAttribute("Ridge Width m")] public float ridgeWidthMeters = 190f;
        [Den.Tools.GUI.ValAttribute("Junction Width m")] public float junctionWidthMeters = 360f;
        [Den.Tools.GUI.ValAttribute("Plate Uniformity")] public float plateUniformity = 0.86f;
        [Den.Tools.GUI.ValAttribute("Warp m")] public float domainWarpMeters = 480f;
        [Den.Tools.GUI.ValAttribute("Warp Frequency")] public float domainWarpFrequency = 0.00018f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = 880031;

        [Den.Tools.GUI.ValAttribute("Quantize Slopes")] public bool enableSlopeQuantization = true;
        [Den.Tools.GUI.ValAttribute("Plateau Source deg")] public float plateauSourceAngleDegrees = 15f;
        [Den.Tools.GUI.ValAttribute("Plateau Target deg")] public float plateauTargetAngleDegrees = 2f;
        [Den.Tools.GUI.ValAttribute("Cliff Source deg")] public float cliffSourceAngleDegrees = 45f;
        [Den.Tools.GUI.ValAttribute("Cliff Target deg")] public float cliffTargetAngleDegrees = 80f;
        [Den.Tools.GUI.ValAttribute("Quantize Strength")] public float slopeQuantizationStrength = 0.72f;

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
                data.RemoveProduct(this);
                return;
            }

            NativeArray<float> rawHeights = default;
            NativeArray<float> quantizedHeights = default;
            try
            {
                rawHeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                quantizedHeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

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
                    Seed = unchecked((uint)seed)
                };

                var baseJob = new HectonSandboxAbyssalShelfBaseJob
                {
                    OutputHeights01 = rawHeights,
                    Parameters = parameters,
                    Width = width,
                    WorldOriginXZ = new double2(dst.worldPos.x, dst.worldPos.z),
                    CellSizeMeters = sampleCellSizeMeters
                };

                JobHandle handle = baseJob.Schedule(cellCount, ResolveBatchCount(cellCount));
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

                    handle = quantizeJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                    finalHeights = quantizedHeights;
                }

                handle.Complete();
                if (stop != null && stop.stop)
                    return;

                for (int i = 0; i < cellCount; i++)
                    target[i] = finalHeights[i];

                data.StoreProduct(this, dst);
            }
            finally
            {
                if (rawHeights.IsCreated)
                    rawHeights.Dispose();

                if (quantizedHeights.IsCreated)
                    quantizedHeights.Dispose();
            }
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
