using System.Collections.Generic;
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
    /// <summary>
    /// MapMagic 2 custom node that detects closed basins and emits brine pool masks.
    /// </summary>
    [System.Serializable]
    [GeneratorMenu(
        menu = "Hecton",
        name = "Anomaly Basin Detector",
        disengageable = true,
        colorType = typeof(MatrixWorld))]
    [UnityEngine.Scripting.Preserve]
    public sealed class HectonAnomalyMapMagicNode : Generator, IMultiInlet, IMultiOutlet, ICustomComplexity
    {
        private const string NativeMemoryOwner = nameof(HectonAnomalyMapMagicNode);
        private const string HeightLabel = "anomalyHeightmap";
        private const string BasinMaskLabel = "anomalyBasinMask";
        private const string CandidateMaskLabel = "anomalyCandidateMask";
        private const string BasinRecordsLabel = "anomalyBasinRecords";
        private const string FloodHeapLabel = "anomalyFloodHeap";
        private const string VisitedStampLabel = "anomalyVisitedStamp";
        private const string AcceptedCellsLabel = "anomalyAcceptedCells";
        private const int CellCountTelemetryThreshold = 1048576;
        private const int FloodCellTelemetryThreshold = 65536;
        private const uint CellCountWarningHash = 0x414E4343u;
        private const uint FloodClampWarningHash = 0x414E4643u;
        private const uint NoBasinWarningHash = 0x414E4E42u;
        private const uint AnomalyNodeContextHash = 0x414E4F44u;

        /// <summary>Input heightmap matrix.</summary>
        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        /// <summary>Brine pool basin mask output.</summary>
        [Den.Tools.GUI.ValAttribute("Brine Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> brineMaskOut = new Outlet<MatrixWorld>();

        /// <summary>Deepest basin point output.</summary>
        [Den.Tools.GUI.ValAttribute("Deepest Points", "Outlet")]
        public readonly Outlet<TransitionsList> deepestPointsOut = new Outlet<TransitionsList>();

        /// <summary>Fallback normalized height scale in meters when MatrixWorld does not specify Y size.</summary>
        [Den.Tools.GUI.ValAttribute("Height Scale")]
        public float heightScaleMeters = 1000f;

        /// <summary>Minimum accepted basin depth in meters.</summary>
        [Den.Tools.GUI.ValAttribute("Min Depth")]
        public float minimumDepthMeters = 3f;

        /// <summary>Maximum flood cells per candidate. Draft tiles reduce this value.</summary>
        [Den.Tools.GUI.ValAttribute("Max Flood")]
        public int maxFloodCells = 65536;

        /// <summary>Height comparison epsilon in meters.</summary>
        [Den.Tools.GUI.ValAttribute("Epsilon")]
        public float heightEpsilonMeters = 0.01f;

        /// <inheritdoc />
        public float Complexity => math.max(1f, maxFloodCells / 8192f);

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
            yield return brineMaskOut;
            yield return deepestPointsOut;
        }

        /// <inheritdoc />
        public override (string, int) GetCodeFileLine() => GetCodeFileLineBase();

        /// <inheritdoc />
        public override void Generate(TileData data, StopToken stop)
        {
            MatrixWorld src = data.ReadInletProduct(heightIn);
            if (src == null)
                return;

            MatrixWorld brineMask = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            TransitionsList deepestPoints = new TransitionsList();
            if (!enabled)
            {
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
                return;
            }

            int cellCount = src.arr != null ? src.arr.Length : 0;
            int width = math.max(1, src.rect.size.x);
            int height = math.max(1, src.rect.size.z);
            if (cellCount <= 0 || width * height > cellCount)
            {
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
                return;
            }

            NativeArray<float> heightmap = default;
            NativeArray<byte> basinMask = default;
            NativeArray<byte> candidateMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<int> floodHeap = default;
            NativeArray<int> visitedStamp = default;
            NativeArray<int> acceptedCells = default;

            try
            {
                // COLD ALLOC: NativeArray anomaly buffers[cellCount] — MapMagic graph product generation — owner: HectonAnomalyMapMagicNode
                heightmap = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                basinMask = new NativeArray<byte>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                candidateMask = new NativeArray<byte>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                basinRecords = new NativeArray<AnomalyBasinRecord>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                floodHeap = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                visitedStamp = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                acceptedCells = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobBuffers(heightmap, basinMask, candidateMask, basinRecords, floodHeap, visitedStamp, acceptedCells);

                float resolvedHeightScale = ResolveHeightScaleMeters(src, data, heightScaleMeters);
                for (int i = 0; i < cellCount; i++)
                    heightmap[i] = math.saturate(src.arr[i]) * resolvedHeightScale;

                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = width,
                    Height = height,
                    CellSizeMeters = ResolveCellSizeMeters(src),
                    MinimumDepthMeters = minimumDepthMeters,
                    MaxFloodCells = data.isDraft ? math.max(256, maxFloodCells / 4) : math.max(256, maxFloodCells),
                    EqualHeightEpsilon = heightEpsilonMeters
                };
                PublishColdPathTelemetry(cellCount, settings.MaxFloodCells);

                JobHandle handle = HectonAnomalyEngine.ScheduleClosedBasinDetection(
                    heightmap,
                    basinMask,
                    basinRecords,
                    candidateMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    settings);

                // COLD SYNC JOB: MapMagic Generate must publish concrete matrix and object products before returning.
                handle.Complete();

                if (stop != null && stop.stop)
                    return;

                CopyMaskToMatrix(basinMask, brineMask.arr);
                CopyDeepestPoints(basinRecords, src, resolvedHeightScale, deepestPoints);
                if (deepestPoints.count == 0)
                    GlobalTelemetryBus.PublishPerformanceWarning(NoBasinWarningHash, AnomalyNodeContextHash, cellCount);
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
            }
            finally
            {
                DisposeTracked(ref heightmap);
                DisposeTracked(ref basinMask);
                DisposeTracked(ref candidateMask);
                DisposeTracked(ref basinRecords);
                DisposeTracked(ref floodHeap);
                DisposeTracked(ref visitedStamp);
                DisposeTracked(ref acceptedCells);
            }
        }

        private static void CopyMaskToMatrix(NativeArray<byte> source, float[] destination)
        {
            int count = math.min(source.Length, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = source[i] != 0 ? 1f : 0f;
        }

        private static void CopyDeepestPoints(
            NativeArray<AnomalyBasinRecord> records,
            MatrixWorld src,
            float resolvedHeightScale,
            TransitionsList output)
        {
            float cellSize = ResolveCellSizeMeters(src);
            Vector3 worldPos = (Vector3)src.worldPos;
            for (int i = 0; i < records.Length; i++)
            {
                AnomalyBasinRecord record = records[i];
                if (record.Valid == 0)
                    continue;

                float x = worldPos.x + (record.DeepestX + 0.5f) * cellSize;
                float z = worldPos.z + (record.DeepestZ + 0.5f) * cellSize;
                float y = record.DeepestHeight;
                var transition = new Transition(x, y, z)
                {
                    terrainHeight = resolvedHeightScale > 0.0001f ? record.LipHeight / resolvedHeightScale : 0f,
                    hash = record.BasinId
                };
                output.Add(transition);
            }
        }

        private static float ResolveHeightScaleMeters(MatrixWorld matrix, TileData data, float fallbackHeightScaleMeters)
        {
            if (matrix.worldSize.y > 0.0001f)
                return matrix.worldSize.y;

            if (data != null && data.globals != null && data.globals.height > 0.0001f)
                return data.globals.height;

            return math.max(0.001f, fallbackHeightScaleMeters);
        }

        private static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            return math.max(0.001f, matrix.worldSize.x / safeWidth);
        }

        private static void PublishColdPathTelemetry(int cellCount, int resolvedMaxFloodCells)
        {
            if (cellCount >= CellCountTelemetryThreshold)
                GlobalTelemetryBus.PublishPerformanceWarning(CellCountWarningHash, AnomalyNodeContextHash, cellCount);

            if (resolvedMaxFloodCells >= FloodCellTelemetryThreshold)
                GlobalTelemetryBus.PublishPerformanceWarning(FloodClampWarningHash, AnomalyNodeContextHash, resolvedMaxFloodCells);
        }

        private static void RegisterTempJobBuffers(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<byte> candidateMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells)
        {
            NativeMemorySentinel.RegisterNativeArray(heightmap, NativeMemoryOwner, HeightLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(basinMask, NativeMemoryOwner, BasinMaskLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(candidateMask, NativeMemoryOwner, CandidateMaskLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(basinRecords, NativeMemoryOwner, BasinRecordsLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(floodHeap, NativeMemoryOwner, FloodHeapLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(visitedStamp, NativeMemoryOwner, VisitedStampLabel, NativeAllocationLifetime.TempJob);
            NativeMemorySentinel.RegisterNativeArray(acceptedCells, NativeMemoryOwner, AcceptedCellsLabel, NativeAllocationLifetime.TempJob);
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }
    }
}
