using System.Collections.Generic;
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
    /// MapMagic 2 custom generator wrapping the HECTON-8 Burst erosion kernels.
    /// </summary>
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Hydraulic Erosion Burst",
        disengageable = true)]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonHydraulicErosionMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        /// <summary>Input heightmap matrix.</summary>
        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        /// <summary>Eroded heightmap output.</summary>
        [Den.Tools.GUI.ValAttribute("Eroded Height", "Outlet")]
        public readonly Outlet<MatrixWorld> erodedHeightOut = new Outlet<MatrixWorld>();

        /// <summary>Strictly normalized sediment deposition output.</summary>
        [Den.Tools.GUI.ValAttribute("Sediment Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> sedimentMaskOut = new Outlet<MatrixWorld>();

        /// <summary>Strictly normalized hydraulic wear output.</summary>
        [Den.Tools.GUI.ValAttribute("Wear Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> wearMaskOut = new Outlet<MatrixWorld>();

        /// <summary>Total droplet count. Draft generation uses a reduced count.</summary>
        [Den.Tools.GUI.ValAttribute("Droplets")]
        public int dropletCount = 1000000;

        /// <summary>Maximum droplet steps.</summary>
        [Den.Tools.GUI.ValAttribute("Lifetime")]
        public int maxLifetime = 64;

        /// <summary>Deterministic seed.</summary>
        [Den.Tools.GUI.ValAttribute("Seed")]
        public int seed = 190863;

        /// <summary>Boundary overlap processed around chunk core.</summary>
        [Den.Tools.GUI.ValAttribute("Margin")]
        public int marginPixels = 4;

        /// <summary>Number of weighted spawn candidates per droplet.</summary>
        [Den.Tools.GUI.ValAttribute("Spawn Candidates")]
        public int spawnCandidateCount = 8;

        /// <summary>Spawn bias for slight depressions.</summary>
        [Den.Tools.GUI.ValAttribute("Depression Spawn")]
        public float depressionSpawnBias = 12f;

        /// <summary>Spawn bias for existing channels.</summary>
        [Den.Tools.GUI.ValAttribute("Channel Spawn")]
        public float channelSpawnBias = 4f;

        /// <summary>Direction inertia.</summary>
        [Den.Tools.GUI.ValAttribute("Inertia")]
        public float inertia = 0.05f;

        /// <summary>Sediment capacity multiplier.</summary>
        [Den.Tools.GUI.ValAttribute("Capacity")]
        public float capacityFactor = 4f;

        /// <summary>Minimum sediment capacity.</summary>
        [Den.Tools.GUI.ValAttribute("Min Capacity")]
        public float minCapacity = 0.0001f;

        /// <summary>Erosion rate.</summary>
        [Den.Tools.GUI.ValAttribute("Erosion")]
        public float erosionRate = 0.35f;

        /// <summary>Deposition rate.</summary>
        [Den.Tools.GUI.ValAttribute("Deposition")]
        public float depositRate = 0.18f;

        /// <summary>Evaporation rate.</summary>
        [Den.Tools.GUI.ValAttribute("Evaporation")]
        public float evaporationRate = 0.015f;

        /// <summary>Droplet gravity.</summary>
        [Den.Tools.GUI.ValAttribute("Gravity")]
        public float gravity = 4f;

        /// <summary>Local flat fill strength for sandy plains.</summary>
        [Den.Tools.GUI.ValAttribute("Flat Fill")]
        public float depressionFillStrength = 0.85f;

        /// <summary>Enables thermal slumping after hydraulic erosion.</summary>
        [Den.Tools.GUI.ValAttribute("Thermal Slump")]
        public bool enableThermalSlumping = true;

        /// <summary>Thermal slumping iterations.</summary>
        [Den.Tools.GUI.ValAttribute("Slump Iterations")]
        public int thermalIterations = 2;

        /// <summary>Critical talus angle in degrees.</summary>
        [Den.Tools.GUI.ValAttribute("Talus Angle")]
        public float talusAngleDegrees = 45f;

        /// <summary>Thermal slumping strength.</summary>
        [Den.Tools.GUI.ValAttribute("Slump Strength")]
        public float thermalStrength = 0.32f;

        /// <inheritdoc />
        public float Complexity => math.max(1, dropletCount / 50000f) + math.max(0, thermalIterations);

        /// <inheritdoc />
        public float Progress(TileData data) => data.GetProgress(this);

        /// <inheritdoc />
        public IEnumerable<IInlet<object>> Inlets()
        {
            yield return heightIn;
        }

        /// <inheritdoc />
        public IEnumerable<IOutlet<object>> Outlets()
        {
            yield return erodedHeightOut;
            yield return sedimentMaskOut;
            yield return wearMaskOut;
        }

        /// <inheritdoc />
        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        /// <inheritdoc />
        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(heightIn);
            if (src == null)
                return;

            MatrixWorld eroded = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            MatrixWorld sedimentMask = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            MatrixWorld wearMask = new MatrixWorld(src.rect, src.worldPos, src.worldSize);

            if (!enabled)
            {
                CopyMatrix(src.arr, eroded.arr);
                data.StoreProduct(erodedHeightOut, eroded);
                data.StoreProduct(sedimentMaskOut, sedimentMask);
                data.StoreProduct(wearMaskOut, wearMask);
                return;
            }

            int cellCount = src.arr != null ? src.arr.Length : 0;
            int width = math.max(1, src.rect.size.x);
            int height = math.max(1, src.rect.size.z);
            if (cellCount <= 0 || width * height > cellCount)
            {
                data.StoreProduct(erodedHeightOut, src);
                data.StoreProduct(sedimentMaskOut, sedimentMask);
                data.StoreProduct(wearMaskOut, wearMask);
                return;
            }

            NativeArray<float> heightA = default;
            NativeArray<float> heightB = default;
            NativeArray<float> sediment = default;
            NativeArray<float> wear = default;

            try
            {
                heightA = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                heightB = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                wear = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);

                for (int i = 0; i < cellCount; i++)
                    heightA[i] = math.saturate(src.arr[i]);

                int safeMargin = math.clamp(marginPixels, 0, math.max(0, math.min(width, height) / 4));
                int coreWidth = math.max(1, width - safeMargin * 2);
                int coreHeight = math.max(1, height - safeMargin * 2);
                int resolvedDroplets = data.isDraft ? math.max(1, dropletCount / 4) : math.max(1, dropletCount);

                var erosionJob = new HydraulicErosionJob
                {
                    Heightmap = heightA,
                    SedimentMask = sediment,
                    WearMask = wear,
                    Width = width,
                    Height = height,
                    CoreOffsetX = safeMargin,
                    CoreOffsetZ = safeMargin,
                    CoreWidth = coreWidth,
                    CoreHeight = coreHeight,
                    DropletCount = resolvedDroplets,
                    MaxLifetime = math.max(1, maxLifetime),
                    Seed = unchecked((uint)seed),
                    Inertia = inertia,
                    CapacityFactor = capacityFactor,
                    MinCapacity = minCapacity,
                    ErosionRate = erosionRate,
                    DepositRate = depositRate,
                    EvaporationRate = evaporationRate,
                    Gravity = gravity,
                    InitialWater = 1f,
                    InitialSpeed = 1f,
                    DepressionFillStrength = depressionFillStrength,
                    DepressionSpawnBias = depressionSpawnBias,
                    ChannelSpawnBias = channelSpawnBias,
                    SpawnCandidateCount = math.max(1, spawnCandidateCount),
                    MinWater = 0.01f
                };

                JobHandle handle = erosionJob.Schedule();
                NativeArray<float> current = heightA;
                NativeArray<float> next = heightB;

                if (enableThermalSlumping && width > 2 && height > 2)
                {
                    int iterations = math.max(0, thermalIterations);
                    float cellSizeMeters = ResolveCellSizeMeters(src);
                    float heightScaleMeters = math.max(0.001f, src.worldSize.y > 0f ? src.worldSize.y : data.globals.height);

                    for (int i = 0; i < iterations; i++)
                    {
                        if (stop != null && stop.stop)
                            break;

                        var slumpJob = new ThermalSlumpingJob
                        {
                            InputHeights01 = current,
                            OutputHeights01 = next,
                            WearMask = wear,
                            Width = width,
                            Height = height,
                            CellSizeMeters = cellSizeMeters,
                            HeightScaleMeters = heightScaleMeters,
                            TalusAngleDegrees = talusAngleDegrees,
                            Strength = thermalStrength,
                            WriteWearMask = true
                        };

                        handle = slumpJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                        Swap(ref current, ref next);
                    }
                }

                // COLD SYNC JOB: MapMagic Generate must publish concrete matrix products before returning to the graph.
                handle.Complete();

                if (stop != null && stop.stop)
                    return;

                CopyNativeToMatrix(current, eroded.arr);
                CopyNormalizedMask(sediment, sedimentMask.arr);
                CopyNormalizedMask(wear, wearMask.arr);

                data.SetProgress(this, Complexity);
                data.StoreProduct(erodedHeightOut, eroded);
                data.StoreProduct(sedimentMaskOut, sedimentMask);
                data.StoreProduct(wearMaskOut, wearMask);
            }
            finally
            {
                if (heightA.IsCreated)
                    heightA.Dispose();
                if (heightB.IsCreated)
                    heightB.Dispose();
                if (sediment.IsCreated)
                    sediment.Dispose();
                if (wear.IsCreated)
                    wear.Dispose();
            }
        }

        private static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001f, matrix.worldSize.x / safeWidth);
        }

        private static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }

        private static void Swap(ref NativeArray<float> current, ref NativeArray<float> next)
        {
            NativeArray<float> swap = current;
            current = next;
            next = swap;
        }

        private static void CopyMatrix(float[] source, float[] destination)
        {
            int count = math.min(source != null ? source.Length : 0, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private static void CopyNativeToMatrix(NativeArray<float> source, float[] destination)
        {
            int count = math.min(source.Length, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = math.saturate(source[i]);
        }

        private static void CopyNormalizedMask(NativeArray<float> source, float[] destination)
        {
            int count = math.min(source.Length, destination != null ? destination.Length : 0);
            float maxValue = 0f;
            for (int i = 0; i < count; i++)
                maxValue = math.max(maxValue, source[i]);

            float invMax = maxValue > 0.000001f ? 1f / maxValue : 0f;
            for (int i = 0; i < count; i++)
                destination[i] = math.saturate(source[i] * invMax);
        }
    }
}
