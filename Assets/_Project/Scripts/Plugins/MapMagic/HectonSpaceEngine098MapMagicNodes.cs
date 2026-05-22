using System.Collections.Generic;
using System.Diagnostics;
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
        menu = "Hecton/SpaceEngine 0.9.8",
        name = "RidgedTerrain",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonSpaceEngine098RidgedMultifractalMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonSpaceEngine098RidgedMultifractalMapMagicNode);
        private const uint BarrierWarningHash = 0x53385242u;
        private const uint InvalidMatrixWarningHash = 0x53385249u;
        private const uint ContextHash = 0x5338524Eu;

        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Height", "Outlet")]
        public readonly Outlet<MatrixWorld> heightOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IInlet<object>[] _inletCache;
        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        [Den.Tools.GUI.ValAttribute("Amplitude m")] public float amplitudeMeters = 420f;
        [Den.Tools.GUI.ValAttribute("Frequency")] public float frequency = 0.00042f;
        [Den.Tools.GUI.ValAttribute("Octaves")] public int octaves = 6;
        [Den.Tools.GUI.ValAttribute("Gain")] public float gain = 2f;
        [Den.Tools.GUI.ValAttribute("Warp")] public float warp = 0.72f;
        [Den.Tools.GUI.ValAttribute("First Octave")] public float firstOctaveValue = 0.86f;
        [Den.Tools.GUI.ValAttribute("Lacunarity")] public float noiseLacunarity = SpaceEngine098TerrainMath.DefaultLacunarity;
        [Den.Tools.GUI.ValAttribute("H")] public float noiseH = SpaceEngine098TerrainMath.DefaultH;
        [Den.Tools.GUI.ValAttribute("Offset")] public float noiseOffset = SpaceEngine098TerrainMath.DefaultOffset;
        [Den.Tools.GUI.ValAttribute("Ridge Smooth")] public float noiseRidgeSmooth = SpaceEngine098TerrainMath.DefaultRidgeSmooth;
        [Den.Tools.GUI.ValAttribute("Height Scale m")] public float heightScaleMeters = 7000f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = 880031;

        public float Complexity => math.max(1f, math.clamp(octaves, 2, 12) * 0.75f);
        public float Progress(TileData data) => data.GetProgress(this);

        public IEnumerable<IInlet<object>> Inlets()
        {
            if (_inletCache == null)
            {
                // COLD ALLOC: IInlet<object>[1] - MapMagic port enumeration cache - owner: HectonSpaceEngine098RidgedMultifractalMapMagicNode
                _inletCache = new IInlet<object>[1];
                _inletCache[0] = heightIn;
            }

            return _inletCache;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[1] - MapMagic port enumeration cache - owner: HectonSpaceEngine098RidgedMultifractalMapMagicNode
                _outletCache = new IOutlet<object>[1];
                _outletCache[0] = heightOut;
            }

            return _outletCache;
        }

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(heightIn);
            if (src == null)
                return;

            if (!enabled)
            {
                data.StoreProduct(heightOut, src);
                return;
            }

            int cellCount = HectonSpaceEngine098MapMagicUtility.ResolveCellCount(src, out int width, out _);
            if (cellCount <= 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidMatrixWarningHash, ContextHash, cellCount);
                data.StoreProduct(heightOut, src);
                return;
            }

            MatrixWorld dst = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            NativeArray<float> input = default;
            NativeArray<float> output = default;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                // COLD ALLOC: NativeArray<float>[cellCount * 2] - MapMagic SpaceEngine ridged terrain product generation - owner: HectonSpaceEngine098RidgedMultifractalMapMagicNode
                input = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                output = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(input, NativeMemoryOwner, "ridgedInput");
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(output, NativeMemoryOwner, "ridgedOutput");
                HectonSpaceEngine098MapMagicUtility.CopyMatrixToNative(src.arr, input);

                var parameters = new SpaceEngine098RidgedMultifractalParams
                {
                    Frequency = math.max(0.0000001f, frequency),
                    Strength01 = math.max(0f, amplitudeMeters) / HectonSpaceEngine098MapMagicUtility.ResolveHeightScaleMeters(src, data, heightScaleMeters),
                    Gain = math.max(0f, gain),
                    Warp = math.max(0f, warp),
                    FirstOctaveValue = math.max(0f, firstOctaveValue),
                    Lacunarity = math.max(1.0001f, noiseLacunarity),
                    H = math.max(0.0001f, noiseH),
                    Offset = noiseOffset,
                    RidgeSmooth = math.max(0f, noiseRidgeSmooth),
                    Octaves = math.clamp(octaves, 2, 12)
                };

                handle = new SpaceEngine098RidgedMultifractalJob
                {
                    InputHeights01 = input,
                    OutputHeights01 = output,
                    Width = width,
                    WorldOriginXZ = new double2(src.worldPos.x, src.worldPos.z),
                    CellSizeMeters = HectonSpaceEngine098MapMagicUtility.ResolveCellSizeMeters(src),
                    Parameters = parameters,
                    Seed = HectonSpaceEngine098MapMagicUtility.ResolveSeed(seed, src)
                }.Schedule(cellCount, HectonSpaceEngine098MapMagicUtility.ResolveBatchCount(cellCount));
                scheduled = true;

                HectonSpaceEngine098MapMagicUtility.CompleteColdMapMagicJob(ref handle, ref scheduled, BarrierWarningHash, ContextHash);
                if (stop != null && stop.stop)
                    return;

                HectonSpaceEngine098MapMagicUtility.CopyNativeToMatrix(output, dst.arr);
                data.SetProgress(this, Complexity);
                data.StoreProduct(heightOut, dst);
            }
            finally
            {
                if (scheduled)
                    HectonSpaceEngine098MapMagicUtility.CompleteColdMapMagicJob(ref handle, ref scheduled, BarrierWarningHash, ContextHash);

                HectonSpaceEngine098MapMagicUtility.DisposeTracked(input);
                HectonSpaceEngine098MapMagicUtility.DisposeTracked(output);
                input = default;
                output = default;
            }
        }
    }

    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton/SpaceEngine 0.9.8",
        name = "CraterKernel",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonSpaceEngine098CraterMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonSpaceEngine098CraterMapMagicNode);
        private const uint BarrierWarningHash = 0x53384342u;
        private const uint InvalidMatrixWarningHash = 0x53384349u;
        private const uint ContextHash = 0x5338434Eu;

        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Height", "Outlet")]
        public readonly Outlet<MatrixWorld> heightOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IInlet<object>[] _inletCache;
        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        [Den.Tools.GUI.ValAttribute("Crater Count")] public int craterCount = 3;
        [Den.Tools.GUI.ValAttribute("Radius m")] public float radiusMeters = 850f;
        [Den.Tools.GUI.ValAttribute("Amplitude m")] public float amplitudeMeters = 115f;
        [Den.Tools.GUI.ValAttribute("Height Scale m")] public float heightScaleMeters = 7000f;
        [Den.Tools.GUI.ValAttribute("Peak Radius")] public float radPeak = 0.03f;
        [Den.Tools.GUI.ValAttribute("Inner Radius")] public float radInner = 0.15f;
        [Den.Tools.GUI.ValAttribute("Rim Radius")] public float radRim = 0.2f;
        [Den.Tools.GUI.ValAttribute("Outer Radius")] public float radOuter = 0.8f;
        [Den.Tools.GUI.ValAttribute("Floor Height")] public float heightFloor = -0.1f;
        [Den.Tools.GUI.ValAttribute("Peak Height")] public float heightPeak = 0.6f;
        [Den.Tools.GUI.ValAttribute("Rim Height")] public float heightRim = 1f;
        [Den.Tools.GUI.ValAttribute("Distortion")] public float distortion = 1f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = 880031;

        public float Complexity => math.max(1f, math.max(1, craterCount) * 0.35f);
        public float Progress(TileData data) => data.GetProgress(this);

        public IEnumerable<IInlet<object>> Inlets()
        {
            if (_inletCache == null)
            {
                // COLD ALLOC: IInlet<object>[1] - MapMagic port enumeration cache - owner: HectonSpaceEngine098CraterMapMagicNode
                _inletCache = new IInlet<object>[1];
                _inletCache[0] = heightIn;
            }

            return _inletCache;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[1] - MapMagic port enumeration cache - owner: HectonSpaceEngine098CraterMapMagicNode
                _outletCache = new IOutlet<object>[1];
                _outletCache[0] = heightOut;
            }

            return _outletCache;
        }

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(heightIn);
            if (src == null)
                return;

            if (!enabled || craterCount <= 0)
            {
                data.StoreProduct(heightOut, src);
                return;
            }

            int cellCount = HectonSpaceEngine098MapMagicUtility.ResolveCellCount(src, out int width, out _);
            if (cellCount <= 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidMatrixWarningHash, ContextHash, cellCount);
                data.StoreProduct(heightOut, src);
                return;
            }

            MatrixWorld dst = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            NativeArray<float> input = default;
            NativeArray<float> output = default;
            NativeArray<float3> craterCenters = default;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                int safeCraterCount = math.clamp(craterCount, 1, 32);
                // COLD ALLOC: NativeArray crater buffers[cellCount * 2 + craterCount] - MapMagic SpaceEngine crater product generation - owner: HectonSpaceEngine098CraterMapMagicNode
                input = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                output = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                craterCenters = new NativeArray<float3>(safeCraterCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(input, NativeMemoryOwner, "craterInput");
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(output, NativeMemoryOwner, "craterOutput");
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(craterCenters, NativeMemoryOwner, "craterCentersAup");
                HectonSpaceEngine098MapMagicUtility.CopyMatrixToNative(src.arr, input);

                uint resolvedSeed = HectonSpaceEngine098MapMagicUtility.ResolveSeed(seed, src);
                float safeRadius = math.max(0.001f, radiusMeters);
                handle = new SpaceEngine098CraterPlacementJob
                {
                    CraterAupCenters = craterCenters,
                    WorldOriginXZ = new double2(src.worldPos.x, src.worldPos.z),
                    WorldSizeXZ = new double2(src.worldSize.x, src.worldSize.z),
                    RadiusMeters = safeRadius,
                    Seed = resolvedSeed
                }.Schedule(safeCraterCount, HectonSpaceEngine098MapMagicUtility.ResolveBatchCount(safeCraterCount));

                var profile = new SpaceEngine098CraterProfile
                {
                    RadPeak = math.max(0.0001f, radPeak),
                    RadInner = math.max(0.0001f, radInner),
                    RadRim = math.max(radInner + 0.0001f, radRim),
                    RadOuter = math.max(radRim + 0.0001f, radOuter),
                    HeightFloor = heightFloor,
                    HeightPeak = heightPeak,
                    HeightRim = heightRim,
                    Distortion = math.max(0f, distortion)
                };

                handle = new SpaceEngine098ApplyCraterHeightJob
                {
                    InputHeights01 = input,
                    OutputHeights01 = output,
                    CraterAupCenters = craterCenters,
                    Width = width,
                    WorldOriginXZ = new double2(src.worldPos.x, src.worldPos.z),
                    CellSizeMeters = HectonSpaceEngine098MapMagicUtility.ResolveCellSizeMeters(src),
                    RadiusMeters = safeRadius,
                    Amplitude01 = math.max(0f, amplitudeMeters) / HectonSpaceEngine098MapMagicUtility.ResolveHeightScaleMeters(src, data, heightScaleMeters),
                    Profile = profile
                }.Schedule(cellCount, HectonSpaceEngine098MapMagicUtility.ResolveBatchCount(cellCount), handle);
                scheduled = true;

                HectonSpaceEngine098MapMagicUtility.CompleteColdMapMagicJob(ref handle, ref scheduled, BarrierWarningHash, ContextHash);
                if (stop != null && stop.stop)
                    return;

                HectonSpaceEngine098MapMagicUtility.CopyNativeToMatrix(output, dst.arr);
                data.SetProgress(this, Complexity);
                data.StoreProduct(heightOut, dst);
            }
            finally
            {
                if (scheduled)
                    HectonSpaceEngine098MapMagicUtility.CompleteColdMapMagicJob(ref handle, ref scheduled, BarrierWarningHash, ContextHash);

                HectonSpaceEngine098MapMagicUtility.DisposeTracked(input);
                HectonSpaceEngine098MapMagicUtility.DisposeTracked(output);
                HectonSpaceEngine098MapMagicUtility.DisposeTracked(craterCenters);
                input = default;
                output = default;
                craterCenters = default;
            }
        }
    }

    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton/SpaceEngine 0.9.8",
        name = "RiftFissure",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonSpaceEngine098RilleMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonSpaceEngine098RilleMapMagicNode);
        private const uint BarrierWarningHash = 0x53384642u;
        private const uint InvalidMatrixWarningHash = 0x53384649u;
        private const uint ContextHash = 0x5338464Eu;

        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        [Den.Tools.GUI.ValAttribute("Height", "Outlet")]
        public readonly Outlet<MatrixWorld> heightOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IInlet<object>[] _inletCache;
        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        [Den.Tools.GUI.ValAttribute("Cell Frequency")] public float cellFrequency = 0.0018f;
        [Den.Tools.GUI.ValAttribute("Depth m")] public float depthMeters = 260f;
        [Den.Tools.GUI.ValAttribute("Rim Lift m")] public float rimLiftMeters = 18f;
        [Den.Tools.GUI.ValAttribute("Narrowness")] public float narrowness = 250f;
        [Den.Tools.GUI.ValAttribute("Sharpness")] public float sharpness = 1.75f;
        [Den.Tools.GUI.ValAttribute("Warp m")] public float domainWarpMeters = 520f;
        [Den.Tools.GUI.ValAttribute("Warp Frequency")] public float domainWarpFrequency = 0.00028f;
        [Den.Tools.GUI.ValAttribute("Height Scale m")] public float heightScaleMeters = 7000f;
        [Den.Tools.GUI.ValAttribute("Seed")] public int seed = 880031;

        public float Complexity => 3f;
        public float Progress(TileData data) => data.GetProgress(this);

        public IEnumerable<IInlet<object>> Inlets()
        {
            if (_inletCache == null)
            {
                // COLD ALLOC: IInlet<object>[1] - MapMagic port enumeration cache - owner: HectonSpaceEngine098RilleMapMagicNode
                _inletCache = new IInlet<object>[1];
                _inletCache[0] = heightIn;
            }

            return _inletCache;
        }

        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[1] - MapMagic port enumeration cache - owner: HectonSpaceEngine098RilleMapMagicNode
                _outletCache = new IOutlet<object>[1];
                _outletCache[0] = heightOut;
            }

            return _outletCache;
        }

        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(heightIn);
            if (src == null)
                return;

            if (!enabled)
            {
                data.StoreProduct(heightOut, src);
                return;
            }

            int cellCount = HectonSpaceEngine098MapMagicUtility.ResolveCellCount(src, out int width, out _);
            if (cellCount <= 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(InvalidMatrixWarningHash, ContextHash, cellCount);
                data.StoreProduct(heightOut, src);
                return;
            }

            MatrixWorld dst = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            NativeArray<float> input = default;
            NativeArray<float> output = default;
            JobHandle handle = default;
            bool scheduled = false;

            try
            {
                // COLD ALLOC: NativeArray<float>[cellCount * 2] - MapMagic SpaceEngine rille product generation - owner: HectonSpaceEngine098RilleMapMagicNode
                input = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                output = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(input, NativeMemoryOwner, "rilleInput");
                HectonSpaceEngine098MapMagicUtility.RegisterTempJobArray(output, NativeMemoryOwner, "rilleOutput");
                HectonSpaceEngine098MapMagicUtility.CopyMatrixToNative(src.arr, input);
                float heightScale = HectonSpaceEngine098MapMagicUtility.ResolveHeightScaleMeters(src, data, heightScaleMeters);

                var parameters = new SpaceEngine098RilleParams
                {
                    CellFrequency = math.max(0.0000001f, cellFrequency),
                    Depth01 = math.max(0f, depthMeters) / heightScale,
                    Narrowness = math.max(1f, narrowness),
                    Sharpness = math.max(0.25f, sharpness),
                    DomainWarpMeters = math.max(0f, domainWarpMeters),
                    DomainWarpFrequency = math.max(0.0000001f, domainWarpFrequency),
                    RimLift01 = math.max(0f, rimLiftMeters) / heightScale
                };

                handle = new SpaceEngine098RilleFissureJob
                {
                    InputHeights01 = input,
                    OutputHeights01 = output,
                    Width = width,
                    WorldOriginXZ = new double2(src.worldPos.x, src.worldPos.z),
                    CellSizeMeters = HectonSpaceEngine098MapMagicUtility.ResolveCellSizeMeters(src),
                    Parameters = parameters,
                    Seed = HectonSpaceEngine098MapMagicUtility.ResolveSeed(seed, src)
                }.Schedule(cellCount, HectonSpaceEngine098MapMagicUtility.ResolveBatchCount(cellCount));
                scheduled = true;

                HectonSpaceEngine098MapMagicUtility.CompleteColdMapMagicJob(ref handle, ref scheduled, BarrierWarningHash, ContextHash);
                if (stop != null && stop.stop)
                    return;

                HectonSpaceEngine098MapMagicUtility.CopyNativeToMatrix(output, dst.arr);
                data.SetProgress(this, Complexity);
                data.StoreProduct(heightOut, dst);
            }
            finally
            {
                if (scheduled)
                    HectonSpaceEngine098MapMagicUtility.CompleteColdMapMagicJob(ref handle, ref scheduled, BarrierWarningHash, ContextHash);

                HectonSpaceEngine098MapMagicUtility.DisposeTracked(input);
                HectonSpaceEngine098MapMagicUtility.DisposeTracked(output);
                input = default;
                output = default;
            }
        }
    }

    internal static class HectonSpaceEngine098MapMagicUtility
    {
        private const float BarrierWarningThresholdMs = 2f;
        private const double AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters;

        internal static int ResolveCellCount(MatrixWorld matrix, out int width, out int height)
        {
            width = math.max(1, matrix.rect.size.x);
            height = math.max(1, matrix.rect.size.z);
            int cellCount = matrix.arr != null ? matrix.arr.Length : 0;
            return cellCount > 0 && width * height <= cellCount ? cellCount : 0;
        }

        internal static void RegisterTempJobArray<T>(NativeArray<T> array, string owner, string label)
            where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(array, owner, label, NativeAllocationLifetime.TempJob);
        }

        internal static void DisposeTracked<T>(NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
        }

        internal static void CopyMatrixToNative(float[] source, NativeArray<float> destination)
        {
            int count = math.min(destination.Length, source != null ? source.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = math.saturate(source[i]);
        }

        internal static void CopyNativeToMatrix(NativeArray<float> source, float[] destination)
        {
            int count = math.min(source.Length, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = math.saturate(source[i]);
        }

        internal static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001f, matrix.worldSize.x / safeWidth);
        }

        internal static float ResolveHeightScaleMeters(MatrixWorld matrix, TileData data, float fallbackHeightScaleMeters)
        {
            if (matrix.worldSize.y > 0.0001f)
                return matrix.worldSize.y;

            if (data != null && data.globals != null && data.globals.height > 0.0001f)
                return data.globals.height;

            return math.max(0.001f, fallbackHeightScaleMeters);
        }

        internal static int ResolveBatchCount(int cellCount)
        {
            return math.max(1, math.min(64, cellCount / 16));
        }

        internal static uint ResolveSeed(int nodeSeed, MatrixWorld matrix)
        {
            unchecked
            {
                uint seed = (uint)nodeSeed;
                IWorldSeedProvider provider = GlobalRegistry.WorldSeedProvider;
                if (provider != null && provider.IsInitialized)
                    seed ^= (uint)provider.RuntimeWorldSeed * 0x9E3779B9u;

                int aupCellX = (int)math.floor(matrix.worldPos.x / AupCellSizeMeters);
                int aupCellZ = (int)math.floor(matrix.worldPos.z / AupCellSizeMeters);
                return SpaceEngine098TerrainMath.MixSeed(seed, aupCellX, aupCellZ);
            }
        }

        internal static void CompleteColdMapMagicJob(
            ref JobHandle handle,
            ref bool scheduled,
            uint warningHash,
            uint contextHash)
        {
            long start = Stopwatch.GetTimestamp();
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            scheduled = false;
            float elapsedMs = (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            if (elapsedMs >= BarrierWarningThresholdMs)
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, elapsedMs);
        }
    }
}
