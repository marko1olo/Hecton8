using Den.Tools;
using Den.Tools.Matrices;
using Hecton8.Core;
using Hecton8.World;
using MapMagic.Nodes;
using MapMagic.Products;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MapMagic.Nodes.MatrixGenerators
{
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Biome Matrix Post Process",
        disengageable = true)]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonBiomeMatrixMapMagicPostProcessNode : Generator, IInlet<MatrixWorld>, IOutlet<MatrixWorld>
    {
        private const string TectonicSpineFamilyId = "biome.family.tectonic_spine";
        private const string NativeMemoryOwner = nameof(HectonBiomeMatrixMapMagicPostProcessNode);
        private const string BufferALabel = "heightA";
        private const string BufferBLabel = "heightB";

        [Den.Tools.GUI.ValAttribute("Thermal")] public bool enableThermalWeathering = true;
        [Den.Tools.GUI.ValAttribute("Thermal Iterations")] public int thermalIterations = 1;
        [Den.Tools.GUI.ValAttribute("Talus Angle")] public float talusAngleDegrees = 32f;
        [Den.Tools.GUI.ValAttribute("Thermal Strength")] public float thermalStrength = 0.18f;

        [Den.Tools.GUI.ValAttribute("Tectonic Spine")] public bool enableTectonicSpineDisplacement = true;
        [Den.Tools.GUI.ValAttribute("Require Family")] public bool requireTectonicSpineFamily = true;
        [Den.Tools.GUI.ValAttribute("Family Id")] public string biomeFamilyId = TectonicSpineFamilyId;
        [Den.Tools.GUI.ValAttribute("Ridge Strength")] public float tectonicStrength = 0.12f;
        [Den.Tools.GUI.ValAttribute("Ridge Frequency")] public float tectonicFrequency = 0.0065f;
        [Den.Tools.GUI.ValAttribute("Ridge Sharpness")] public float tectonicRidgeSharpness = 3.25f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int tectonicSeed = 83117;

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(this);
            if (src == null)
                return;

            if (!enabled || (!enableThermalWeathering && !ShouldApplyTectonicDisplacement()))
            {
                data.StoreProduct(this, src);
                return;
            }

            if (stop != null && stop.stop)
                return;

            MatrixWorld dst = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            float[] source = src.arr;
            float[] target = dst.arr;
            int cellCount = source != null ? source.Length : 0;
            if (cellCount <= 0 || target == null || target.Length < cellCount)
            {
                data.StoreProduct(this, src);
                return;
            }

            NativeArray<float> bufferA = default;
            NativeArray<float> bufferB = default;
            try
            {
                bufferA = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                bufferB = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobBuffers(bufferA, bufferB);

                for (int i = 0; i < cellCount; i++)
                    bufferA[i] = math.saturate(source[i]);

                NativeArray<float> current = bufferA;
                NativeArray<float> next = bufferB;
                int width = math.max(1, src.rect.size.x);
                int height = math.max(1, src.rect.size.z);
                float cellSizeMeters = ResolveCellSizeMeters(src);
                float heightScaleMeters = math.max(0.001f, data.globals.height);
                JobHandle handle = default;
                bool hasScheduledWork = false;
                bool stopRequested = false;

                if (ShouldApplyTectonicDisplacement() && width > 1 && height > 1)
                {
                    var tectonicJob = new WorldProceduralTerrainTectonicDisplacementJob
                    {
                        InputHeights01 = current,
                        OutputHeights01 = next,
                        Width = width,
                        Height = height,
                        WorldOriginXZ = new float2(src.worldPos.x, src.worldPos.z),
                        CellSizeMeters = cellSizeMeters,
                        Strength01 = math.saturate(tectonicStrength),
                        Frequency = math.max(0.0001f, tectonicFrequency),
                        RidgeSharpness = math.max(0.5f, tectonicRidgeSharpness),
                        Seed = unchecked((uint)tectonicSeed)
                    };

                    handle = tectonicJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                    hasScheduledWork = true;
                    Swap(ref current, ref next);
                }

                if (enableThermalWeathering && width > 2 && height > 2)
                {
                    int iterations = math.max(0, thermalIterations);
                    for (int iteration = 0; iteration < iterations; iteration++)
                    {
                        if (stop != null && stop.stop)
                        {
                            stopRequested = true;
                            break;
                        }

                        var thermalJob = new WorldProceduralTerrainThermalWeatheringJob
                        {
                            InputHeights01 = current,
                            OutputHeights01 = next,
                            Width = width,
                            Height = height,
                            CellSizeMeters = cellSizeMeters,
                            HeightScaleMeters = heightScaleMeters,
                            TalusAngleDegrees = talusAngleDegrees,
                            Strength = thermalStrength
                        };

                        handle = thermalJob.Schedule(cellCount, ResolveBatchCount(cellCount), handle);
                        hasScheduledWork = true;
                        Swap(ref current, ref next);
                    }
                }

                if (hasScheduledWork)
                {
                    // COLD SYNC JOB: MapMagic Generate must publish concrete matrix products before returning to the graph.
                    DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                }

                if (stopRequested)
                    return;

                for (int i = 0; i < cellCount; i++)
                    target[i] = current[i];

                data.StoreProduct(this, dst);
            }
            finally
            {
                DisposeTracked(ref bufferA);
                DisposeTracked(ref bufferB);
            }
        }

        private static void RegisterTempJobBuffers(NativeArray<float> bufferA, NativeArray<float> bufferB)
        {
            NativeMemorySentinel.RegisterNativeArray(bufferA, NativeMemoryOwner, BufferALabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(bufferB, NativeMemoryOwner, BufferBLabel, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTracked(ref NativeArray<float> array)
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private bool ShouldApplyTectonicDisplacement()
        {
            if (!enableTectonicSpineDisplacement)
                return false;

            return !requireTectonicSpineFamily ||
                   string.Equals(biomeFamilyId, TectonicSpineFamilyId, System.StringComparison.OrdinalIgnoreCase);
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
    }
}
