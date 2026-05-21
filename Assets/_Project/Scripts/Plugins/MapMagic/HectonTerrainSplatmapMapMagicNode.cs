using System.Collections.Generic;
using Den.Tools.Matrices;
using Hecton8.Core;
using Hecton8.World;
using MapMagic.Nodes;
using MapMagic.Products;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MapMagic.Nodes.MatrixGenerators
{
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Slope Cavity Splatmap Burst",
        disengageable = true)]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonTerrainSplatmapMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonTerrainSplatmapMapMagicNode);
        private const string HeightLabel = "height";
        private const string SedimentLabel = "sediment";
        private const string WeightsLabel = "weights";
        private const string SlopeWeightsLabel = "slopeWeights";

        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Sediment", "Inlet")]
        public readonly Inlet<MatrixWorld> sedimentIn = new Inlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Sand", "Outlet")]
        public readonly Outlet<MatrixWorld> sandOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Rock", "Outlet")]
        public readonly Outlet<MatrixWorld> rockOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Silt", "Outlet")]
        public readonly Outlet<MatrixWorld> siltOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Cavity", "Outlet")]
        public readonly Outlet<MatrixWorld> cavityOut = new Outlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Slope Weight", "Outlet")]
        public readonly Outlet<MatrixWorld> slopeWeightOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IInlet<object>[] _inletCache;
        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        [Den.Tools.GUI.ValAttribute("Rock Slope")]
        public float rockSlopeThresholdDegrees = 45f;

        [Den.Tools.GUI.ValAttribute("Slope Blend")]
        public float slopeBlendWidthDegrees = 6f;

        [Den.Tools.GUI.ValAttribute("Cavity Strength")]
        public float cavityStrength = 0.08f;

        [Den.Tools.GUI.ValAttribute("Sediment Strength")]
        public float sedimentStrength = 1f;

        public float Complexity => 1.2f;

        public float Progress(TileData data) => data.GetProgress(this);

        public IEnumerable<IInlet<object>> Inlets()
        {
            if (_inletCache == null)
            {
                // COLD ALLOC: IInlet<object>[2] - MapMagic port enumeration cache - owner: HectonTerrainSplatmapMapMagicNode
                _inletCache = new IInlet<object>[2];
                _inletCache[0] = heightIn;
                _inletCache[1] = sedimentIn;
            }

            return _inletCache;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[5] - MapMagic port enumeration cache - owner: HectonTerrainSplatmapMapMagicNode
                _outletCache = new IOutlet<object>[5];
                _outletCache[0] = sandOut;
                _outletCache[1] = rockOut;
                _outletCache[2] = siltOut;
                _outletCache[3] = cavityOut;
                _outletCache[4] = slopeWeightOut;
            }

            return _outletCache;
        }

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld heightSource = data.ReadInletProduct(heightIn);
            if (heightSource == null)
                return;

            MatrixWorld sedimentSource = data.ReadInletProduct(sedimentIn);
            MatrixWorld sand = new MatrixWorld(heightSource.rect, heightSource.worldPos, heightSource.worldSize);
            MatrixWorld rock = new MatrixWorld(heightSource.rect, heightSource.worldPos, heightSource.worldSize);
            MatrixWorld silt = new MatrixWorld(heightSource.rect, heightSource.worldPos, heightSource.worldSize);
            MatrixWorld cavity = new MatrixWorld(heightSource.rect, heightSource.worldPos, heightSource.worldSize);
            MatrixWorld slopeWeight = new MatrixWorld(heightSource.rect, heightSource.worldPos, heightSource.worldSize);

            if (!enabled)
            {
                FillFallbackFlatSplats(sand.arr, rock.arr, silt.arr, cavity.arr, slopeWeight.arr);
                StoreOutputs(data, sand, rock, silt, cavity, slopeWeight);
                return;
            }

            int cellCount = ResolveCellCount(heightSource, out int width, out int height);
            if (cellCount <= 0)
            {
                FillFallbackFlatSplats(sand.arr, rock.arr, silt.arr, cavity.arr, slopeWeight.arr);
                StoreOutputs(data, sand, rock, silt, cavity, slopeWeight);
                return;
            }

            NativeArray<float> heights = default;
            NativeArray<float> sediment = default;
            NativeArray<float4> weights = default;
            NativeArray<float> slopeWeights = default;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                heights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                sediment = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                weights = new NativeArray<float4>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                slopeWeights = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobBuffers(heights, sediment, weights, slopeWeights);

                CopyMatrixToNative(heightSource.arr, heights);
                if (sedimentSource != null)
                    CopyMatrixToNative(sedimentSource.arr, sediment);

                var job = new WorldProceduralTerrainSlopeCavitySplatmapJob
                {
                    Heights01 = heights,
                    Sediment01 = sediment,
                    Weights = weights,
                    SlopeWeights01 = slopeWeights,
                    Width = width,
                    Height = height,
                    CellSizeMeters = ResolveCellSizeMeters(heightSource),
                    HeightScaleMeters = ResolveHeightScaleMeters(heightSource, data),
                    RockSlopeThresholdDegrees = math.clamp(rockSlopeThresholdDegrees, 0f, 89f),
                    SlopeBlendWidthDegrees = math.max(0.001f, slopeBlendWidthDegrees),
                    CavityStrength = math.max(0f, cavityStrength),
                    SedimentStrength = math.max(0f, sedimentStrength)
                };

                handle = job.Schedule(cellCount, ResolveBatchCount(cellCount));
                scheduled = true;

                // COLD SYNC JOB: MapMagic Generate must publish concrete splat matrices before returning to the graph.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                scheduled = false;

                if (stop != null && stop.stop)
                    return;

                CopyWeightsToMatrices(weights, slopeWeights, sand.arr, rock.arr, silt.arr, cavity.arr, slopeWeight.arr);
                data.SetProgress(this, Complexity);
                StoreOutputs(data, sand, rock, silt, cavity, slopeWeight);
            }
            finally
            {
                if (scheduled)
                {
                    // COLD SYNC JOB: finalizer guard for MapMagic generator teardown.
                    DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                }

                DisposeTracked(ref heights);
                DisposeTracked(ref sediment);
                DisposeTracked(ref weights);
                DisposeTracked(ref slopeWeights);
            }
        }

        private static int ResolveCellCount(MatrixWorld matrix, out int width, out int height)
        {
            width = math.max(1, matrix.rect.size.x);
            height = math.max(1, matrix.rect.size.z);
            int cellCount = matrix.arr != null ? matrix.arr.Length : 0;
            return cellCount > 0 && width * height <= cellCount ? cellCount : 0;
        }

        private static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001f, matrix.worldSize.x / safeWidth);
        }

        private static float ResolveHeightScaleMeters(MatrixWorld matrix, TileData data)
        {
            if (matrix.worldSize.y > 0.0001f)
                return matrix.worldSize.y;

            if (data != null && data.globals != null && data.globals.height > 0.0001f)
                return data.globals.height;

            return 1000f;
        }

        private static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }

        private static void RegisterTempJobBuffers(
            NativeArray<float> heights,
            NativeArray<float> sediment,
            NativeArray<float4> weights,
            NativeArray<float> slopeWeights)
        {
            NativeMemorySentinel.RegisterNativeArray(heights, NativeMemoryOwner, HeightLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(sediment, NativeMemoryOwner, SedimentLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(weights, NativeMemoryOwner, WeightsLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(slopeWeights, NativeMemoryOwner, SlopeWeightsLabel, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static void CopyMatrixToNative(float[] source, NativeArray<float> destination)
        {
            int count = math.min(destination.Length, source != null ? source.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = math.saturate(source[i]);
        }

        private static void CopyWeightsToMatrices(
            NativeArray<float4> source,
            NativeArray<float> slopeWeights,
            float[] sand,
            float[] rock,
            float[] silt,
            float[] cavity,
            float[] slopeWeight)
        {
            int count = math.min(source.Length, math.min(sand != null ? sand.Length : 0, rock != null ? rock.Length : 0));
            count = math.min(count, math.min(silt != null ? silt.Length : 0, cavity != null ? cavity.Length : 0));
            count = math.min(count, math.min(slopeWeights.Length, slopeWeight != null ? slopeWeight.Length : 0));
            for (int i = 0; i < count; i++)
            {
                float4 value = math.saturate(source[i]);
                sand[i] = value.x;
                rock[i] = value.y;
                silt[i] = value.z;
                cavity[i] = value.w;
                slopeWeight[i] = math.saturate(slopeWeights[i]);
            }
        }

        private static void FillFallbackFlatSplats(float[] sand, float[] rock, float[] silt, float[] cavity, float[] slopeWeight)
        {
            int count = math.min(sand != null ? sand.Length : 0, rock != null ? rock.Length : 0);
            count = math.min(count, math.min(silt != null ? silt.Length : 0, cavity != null ? cavity.Length : 0));
            count = math.min(count, slopeWeight != null ? slopeWeight.Length : 0);
            for (int i = 0; i < count; i++)
            {
                sand[i] = 1f;
                rock[i] = 0f;
                silt[i] = 0f;
                cavity[i] = 0f;
                slopeWeight[i] = 0f;
            }
        }

        private void StoreOutputs(
            TileData data,
            MatrixWorld sand,
            MatrixWorld rock,
            MatrixWorld silt,
            MatrixWorld cavity,
            MatrixWorld slopeWeight)
        {
            data.StoreProduct(sandOut, sand);
            data.StoreProduct(rockOut, rock);
            data.StoreProduct(siltOut, silt);
            data.StoreProduct(cavityOut, cavity);
            data.StoreProduct(slopeWeightOut, slopeWeight);
        }
    }
}
