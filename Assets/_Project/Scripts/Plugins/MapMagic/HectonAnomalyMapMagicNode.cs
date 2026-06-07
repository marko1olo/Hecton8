using System.Collections.Generic;
using System.Diagnostics;
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
    /// MapMagic 2 custom node that detects brine basins, chthonic pillar anchors, and deep fissure masks.
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
        private const string BasinLipMaskLabel = "anomalyBasinLipMask";
        private const string CandidateMaskLabel = "anomalyCandidateMask";
        private const string BasinRecordsLabel = "anomalyBasinRecords";
        private const string FeatureRecordsLabel = "anomalyFeatureRecords";
        private const string FissureMaskLabel = "anomalyFissureMask";
        private const string FloodHeapLabel = "anomalyFloodHeap";
        private const string VisitedStampLabel = "anomalyVisitedStamp";
        private const string AcceptedCellsLabel = "anomalyAcceptedCells";
        private const int CellCountTelemetryThreshold = 1048576;
        private const int FloodCellTelemetryThreshold = 65536;
        private const uint CellCountWarningHash = 0x414E4343u;
        private const uint FloodClampWarningHash = 0x414E4643u;
        private const uint NoBasinWarningHash = 0x414E4E42u;
        private const uint SolveBudgetWarningHash = 0x414E5342u;
        private const uint AnomalyNodeContextHash = 0x414E4F44u;
        private const float ChthonicPillarTectonicBoundaryFrequency = 0.0065f;
        private const uint ChthonicPillarTectonicBoundarySeed = 83117u;
        private const float ChthonicPillarMinimumTectonicBoundaryMask = 0.55f;
        private static readonly long SolvePerformanceWarningBudgetTicks = System.Math.Max(1L, Stopwatch.Frequency / 5000L);

        /// <summary>Input heightmap matrix.</summary>
        [Den.Tools.GUI.ValAttribute("Heightmap", "Inlet")]
        public readonly Inlet<MatrixWorld> heightIn = new Inlet<MatrixWorld>();

        /// <summary>BrinePoolMask output used by MapMagic terrain texture swaps.</summary>
        [Den.Tools.GUI.ValAttribute("BrinePoolMask", "Outlet")]
        public readonly Outlet<MatrixWorld> brineMaskOut = new Outlet<MatrixWorld>();

        /// <summary>Positive basin-edge lip overlay output used by MapMagic height and normal-map blends.</summary>
        [Den.Tools.GUI.ValAttribute("BrineLipRidgeMask", "Outlet")]
        public readonly Outlet<MatrixWorld> brineLipRidgeOut = new Outlet<MatrixWorld>();

        /// <summary>Deepest basin point output.</summary>
        [Den.Tools.GUI.ValAttribute("Deepest Points", "Outlet")]
        public readonly Outlet<TransitionsList> deepestPointsOut = new Outlet<TransitionsList>();

        /// <summary>Ridge-intersection pillar coordinate output.</summary>
        [Den.Tools.GUI.ValAttribute("Pillar AUP", "Outlet")]
        public readonly Outlet<TransitionsList> pillarCoordinatesOut = new Outlet<TransitionsList>();

        /// <summary>Deep fissure candidate mask output.</summary>
        [Den.Tools.GUI.ValAttribute("Fissure Mask", "Outlet")]
        public readonly Outlet<MatrixWorld> fissureMaskOut = new Outlet<MatrixWorld>();

        [System.NonSerialized] private IInlet<object>[] _inletCache;
        [System.NonSerialized] private IOutlet<object>[] _outletCache;

        /// <summary>Fallback normalized height scale in meters when MatrixWorld does not specify Y size.</summary>
        [Den.Tools.GUI.ValAttribute("Height Scale")]
        public float heightScaleMeters = 1000f;

        /// <summary>Minimum accepted basin depth in meters.</summary>
        [Den.Tools.GUI.ValAttribute("Min Depth")]
        public float minimumDepthMeters = 50f;

        /// <summary>Minimum local ridge-intersection prominence before a pillar coordinate is exported.</summary>
        [Den.Tools.GUI.ValAttribute("Pillar Prominence")]
        public float pillarProminenceMeters = 35f;

        /// <summary>Minimum descending ridge arms required before a pillar coordinate is exported.</summary>
        [Den.Tools.GUI.ValAttribute("Pillar Arms")]
        public int pillarRidgeArms = 3;

        /// <summary>Minimum local trough depth before a deep fissure mask cell is exported.</summary>
        [Den.Tools.GUI.ValAttribute("Fissure Depth")]
        public float fissureDepthMeters = 25f;

        /// <summary>Primary biome id packed into fissure influence cells for fog and audio consumers.</summary>
        [Den.Tools.GUI.ValAttribute("Fissure Biome")]
        public int fissurePrimaryBiomeId = 79;

        /// <summary>Maximum pillar anchors per runtime tile that may request deterministic resource binding.</summary>
        [Den.Tools.GUI.ValAttribute("Pillar Resource Cap")]
        public int maxRuntimePillarResourceBindings = 64;

        /// <summary>Maximum flood cells per candidate. Draft tiles reduce this value.</summary>
        [Den.Tools.GUI.ValAttribute("Max Flood")]
        public int maxFloodCells = 65536;

        /// <summary>Height comparison epsilon in meters.</summary>
        [Den.Tools.GUI.ValAttribute("Epsilon")]
        public float heightEpsilonMeters = 0.01f;

        /// <summary>Positive rim height exported around accepted basin masks.</summary>
        [Den.Tools.GUI.ValAttribute("Brine Lip Height")]
        public float brineLipHeightMeters = 1.25f;

        /// <summary>Cell falloff width for the exported basin lip ridge overlay.</summary>
        [Den.Tools.GUI.ValAttribute("Brine Lip Falloff")]
        public int brineLipFalloffCells = 2;

        /// <inheritdoc />
        public float Complexity => math.max(1f, maxFloodCells / 8192f);

        /// <inheritdoc />
        public float Progress(TileData data) => data.GetProgress(this);

        /// <inheritdoc />
        public IEnumerable<IInlet<object>> Inlets()
        {
            if (_inletCache == null)
            {
                // COLD ALLOC: IInlet<object>[1] - MapMagic port enumeration cache - owner: HectonAnomalyMapMagicNode
                _inletCache = new IInlet<object>[1];
                _inletCache[0] = heightIn;
            }

            return _inletCache;
        }

        /// <inheritdoc />
        public IEnumerable<IOutlet<object>> Outlets()
        {
            if (_outletCache == null)
            {
                // COLD ALLOC: IOutlet<object>[5] - MapMagic port enumeration cache - owner: HectonAnomalyMapMagicNode
                _outletCache = new IOutlet<object>[5];
                _outletCache[0] = brineMaskOut;
                _outletCache[1] = deepestPointsOut;
                _outletCache[2] = pillarCoordinatesOut;
                _outletCache[3] = fissureMaskOut;
                _outletCache[4] = brineLipRidgeOut;
            }

            return _outletCache;
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
            MatrixWorld brineLipRidgeMask = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            MatrixWorld fissureMaskMatrix = new MatrixWorld(src.rect, src.worldPos, src.worldSize);
            TransitionsList deepestPoints = new TransitionsList();
            TransitionsList pillarCoordinates = new TransitionsList();
            if (!enabled)
            {
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(brineLipRidgeOut, brineLipRidgeMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
                data.StoreProduct(pillarCoordinatesOut, pillarCoordinates);
                data.StoreProduct(fissureMaskOut, fissureMaskMatrix);
                return;
            }

            int cellCount = src.arr != null ? src.arr.Length : 0;
            int width = math.max(1, src.rect.size.x);
            int height = math.max(1, src.rect.size.z);
            if (!HasValidMatrixCellContract(width, height, cellCount))
            {
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(brineLipRidgeOut, brineLipRidgeMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
                data.StoreProduct(pillarCoordinatesOut, pillarCoordinates);
                data.StoreProduct(fissureMaskOut, fissureMaskMatrix);
                return;
            }

            Vector3 sourceWorldPos = (Vector3)src.worldPos;
            if (!IsFiniteVector3(sourceWorldPos))
            {
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(brineLipRidgeOut, brineLipRidgeMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
                data.StoreProduct(pillarCoordinatesOut, pillarCoordinates);
                data.StoreProduct(fissureMaskOut, fissureMaskMatrix);
                return;
            }

            NativeArray<float> heightmap = default;
            NativeArray<byte> basinMask = default;
            NativeArray<float> basinLipMask = default;
            NativeArray<byte> candidateMask = default;
            NativeArray<AnomalyBasinRecord> basinRecords = default;
            NativeArray<AnomalyFeatureRecord> featureRecords = default;
            NativeArray<byte> fissureMask = default;
            NativeArray<int> floodHeap = default;
            NativeArray<int> visitedStamp = default;
            NativeArray<int> acceptedCells = default;
            int heightmapRegistrationId = 0;
            int basinMaskRegistrationId = 0;
            int basinLipMaskRegistrationId = 0;
            int candidateMaskRegistrationId = 0;
            int basinRecordsRegistrationId = 0;
            int featureRecordsRegistrationId = 0;
            int fissureMaskRegistrationId = 0;
            int floodHeapRegistrationId = 0;
            int visitedStampRegistrationId = 0;
            int acceptedCellsRegistrationId = 0;

            try
            {
                // COLD ALLOC: NativeArray anomaly buffers[cellCount] — MapMagic graph product generation — owner: HectonAnomalyMapMagicNode
                heightmap = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                basinMask = new NativeArray<byte>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                basinLipMask = new NativeArray<float>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                candidateMask = new NativeArray<byte>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                basinRecords = new NativeArray<AnomalyBasinRecord>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                featureRecords = new NativeArray<AnomalyFeatureRecord>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                fissureMask = new NativeArray<byte>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                floodHeap = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                visitedStamp = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.ClearMemory);
                acceptedCells = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobBuffers(
                    heightmap,
                    basinMask,
                    basinLipMask,
                    candidateMask,
                    basinRecords,
                    featureRecords,
                    fissureMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    out heightmapRegistrationId,
                    out basinMaskRegistrationId,
                    out basinLipMaskRegistrationId,
                    out candidateMaskRegistrationId,
                    out basinRecordsRegistrationId,
                    out featureRecordsRegistrationId,
                    out fissureMaskRegistrationId,
                    out floodHeapRegistrationId,
                    out visitedStampRegistrationId,
                    out acceptedCellsRegistrationId);

                float resolvedHeightScale = ResolveHeightScaleMeters(src, data, heightScaleMeters);
                for (int i = 0; i < cellCount; i++)
                {
                    float sample = src.arr[i];
                    heightmap[i] = math.isfinite(sample) ? math.saturate(sample) * resolvedHeightScale : 0f;
                }

                float cellSizeMeters = ResolveCellSizeMeters(src);
                var settings = new AnomalyBasinDetectionSettings
                {
                    Width = width,
                    Height = height,
                    CellSizeMeters = cellSizeMeters,
                    MinimumDepthMeters = minimumDepthMeters,
                    MaxFloodCells = data.isDraft ? math.max(256, maxFloodCells / 4) : math.max(256, maxFloodCells),
                    EqualHeightEpsilon = heightEpsilonMeters
                };
                PublishColdPathTelemetry(cellCount, settings.MaxFloodCells);

                long solveStartTicks = Stopwatch.GetTimestamp();
                JobHandle handle = HectonAnomalyEngine.ScheduleClosedBasinDetection(
                    heightmap,
                    basinMask,
                    basinRecords,
                    candidateMask,
                    floodHeap,
                    visitedStamp,
                    acceptedCells,
                    settings);

                uint fissureInfluencePacked = HectonAnomalyEngine.PackBiomeInfluenceCell(
                    (byte)math.clamp(fissurePrimaryBiomeId, 0, 255),
                    0,
                    255,
                    (byte)(WorldProceduralFieldSampler.BiomeInfluenceFlags.Hazard |
                           WorldProceduralFieldSampler.BiomeInfluenceFlags.VolumetricDepth));
                var ridgeSettings = new AnomalyRidgeDetectionSettings
                {
                    Width = width,
                    Height = height,
                    CellSizeMeters = cellSizeMeters,
                    OriginAup = new double3(sourceWorldPos.x, sourceWorldPos.y, sourceWorldPos.z),
                    MinimumPillarProminenceMeters = pillarProminenceMeters,
                    MinimumPillarRidgeArms = pillarRidgeArms,
                    MinimumFissureDepthMeters = fissureDepthMeters,
                    EqualHeightEpsilon = heightEpsilonMeters,
                    FissureInfluencePacked = fissureInfluencePacked,
                    RequireTectonicBoundary = 1,
                    TectonicBoundaryFrequency = ChthonicPillarTectonicBoundaryFrequency,
                    TectonicBoundarySeed = ChthonicPillarTectonicBoundarySeed,
                    MinimumTectonicBoundaryMask = ChthonicPillarMinimumTectonicBoundaryMask
                };
                handle = HectonAnomalyEngine.ScheduleRidgeFeatureDetection(
                    heightmap,
                    featureRecords,
                    fissureMask,
                    ridgeSettings,
                    handle);
                handle = MapMagicBridge.ScheduleBrineBasinLipRidgeOverlay(
                    basinMask,
                    basinLipMask,
                    width,
                    height,
                    brineLipFalloffCells,
                    brineLipHeightMeters,
                    handle);

                // COLD SYNC JOB: MapMagic Generate must publish concrete matrix and object products before returning.
                DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
                PublishSolvePerformanceWarningIfNeeded(Stopwatch.GetTimestamp() - solveStartTicks);

                if (stop != null && stop.stop)
                    return;

                CopyMaskToMatrix(basinMask, brineMask.arr);
                CopyHeightOffsetToMatrix(basinLipMask, brineLipRidgeMask.arr, resolvedHeightScale);
                CopyMaskToMatrix(fissureMask, fissureMaskMatrix.arr);
                CopyDeepestPoints(basinRecords, src, resolvedHeightScale, deepestPoints);
                CopyPillarCoordinates(featureRecords, pillarCoordinates);
                if (Application.isPlaying)
                {
                    HectonAnomalyResourceBinding.TryBindChthonicPillarResources(
                        featureRecords,
                        maxRuntimePillarResourceBindings);
                }

                if (deepestPoints.count == 0)
                    GlobalTelemetryBus.PublishPerformanceWarning(NoBasinWarningHash, AnomalyNodeContextHash, cellCount);
                data.StoreProduct(brineMaskOut, brineMask);
                data.StoreProduct(brineLipRidgeOut, brineLipRidgeMask);
                data.StoreProduct(deepestPointsOut, deepestPoints);
                data.StoreProduct(pillarCoordinatesOut, pillarCoordinates);
                data.StoreProduct(fissureMaskOut, fissureMaskMatrix);
            }
            finally
            {
                DisposeTracked(ref heightmap, ref heightmapRegistrationId);
                DisposeTracked(ref basinMask, ref basinMaskRegistrationId);
                DisposeTracked(ref basinLipMask, ref basinLipMaskRegistrationId);
                DisposeTracked(ref candidateMask, ref candidateMaskRegistrationId);
                DisposeTracked(ref basinRecords, ref basinRecordsRegistrationId);
                DisposeTracked(ref featureRecords, ref featureRecordsRegistrationId);
                DisposeTracked(ref fissureMask, ref fissureMaskRegistrationId);
                DisposeTracked(ref floodHeap, ref floodHeapRegistrationId);
                DisposeTracked(ref visitedStamp, ref visitedStampRegistrationId);
                DisposeTracked(ref acceptedCells, ref acceptedCellsRegistrationId);
            }
        }

        private static void CopyMaskToMatrix(NativeArray<byte> source, float[] destination)
        {
            int count = math.min(source.Length, destination != null ? destination.Length : 0);
            for (int i = 0; i < count; i++)
                destination[i] = source[i] != 0 ? 1f : 0f;
        }

        private static void CopyHeightOffsetToMatrix(NativeArray<float> sourceMeters, float[] destination, float heightScaleMeters)
        {
            int count = math.min(sourceMeters.Length, destination != null ? destination.Length : 0);
            float invHeightScale = 1f / ResolvePositiveFinite(heightScaleMeters, 0.001f);
            for (int i = 0; i < count; i++)
            {
                float meters = sourceMeters[i];
                destination[i] = math.isfinite(meters) ? math.max(0f, meters) * invHeightScale : 0f;
            }
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
                if (!math.isfinite(record.DeepestHeight) || !math.isfinite(record.LipHeight))
                    continue;

                float x = worldPos.x + (record.DeepestX + 0.5f) * cellSize;
                float z = worldPos.z + (record.DeepestZ + 0.5f) * cellSize;
                float y = worldPos.y + record.DeepestHeight;
                var transition = new Transition(x, y, z)
                {
                    terrainHeight = resolvedHeightScale > 0.0001f ? record.LipHeight / resolvedHeightScale : 0f,
                    hash = record.BasinId
                };
                output.Add(transition);
            }
        }

        private static void CopyPillarCoordinates(NativeArray<AnomalyFeatureRecord> records, TransitionsList output)
        {
            for (int i = 0; i < records.Length; i++)
            {
                AnomalyFeatureRecord record = records[i];
                if (record.Valid == 0 || record.Kind != (byte)AnomalyFeatureKind.ChthonicPillar)
                    continue;
                if (!IsFiniteDouble3(new double3(record.AupX, record.AupY, record.AupZ)) || !math.isfinite(record.Strength01))
                    continue;

                var transition = new Transition((float)record.AupX, (float)record.AupY, (float)record.AupZ)
                {
                    terrainHeight = record.Strength01,
                    hash = record.Index
                };
                output.Add(transition);
            }
        }

        private static float ResolveHeightScaleMeters(MatrixWorld matrix, TileData data, float fallbackHeightScaleMeters)
        {
            if (math.isfinite(matrix.worldSize.y) && matrix.worldSize.y > 0.0001f)
                return matrix.worldSize.y;

            if (data != null && data.globals != null && math.isfinite(data.globals.height) && data.globals.height > 0.0001f)
                return data.globals.height;

            return ResolvePositiveFinite(fallbackHeightScaleMeters, 0.001f);
        }

        private static float ResolveCellSizeMeters(MatrixWorld matrix)
        {
            int safeWidth = math.max(1, matrix.rect.size.x - 1);
            float cellSize = matrix.worldSize.x / safeWidth;
            return ResolvePositiveFinite(cellSize, 0.001f);
        }

        private static bool HasValidMatrixCellContract(int width, int height, int cellCount)
        {
            if (width <= 0 || height <= 0 || cellCount <= 0)
                return false;

            long required = (long)width * height;
            return required <= cellCount;
        }

        private static bool IsFiniteVector3(Vector3 value)
        {
            return math.all(math.isfinite(new float3(value.x, value.y, value.z)));
        }

        private static bool IsFiniteDouble3(double3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static float ResolvePositiveFinite(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void PublishColdPathTelemetry(int cellCount, int resolvedMaxFloodCells)
        {
            if (cellCount >= CellCountTelemetryThreshold)
                GlobalTelemetryBus.PublishPerformanceWarning(CellCountWarningHash, AnomalyNodeContextHash, cellCount);

            if (resolvedMaxFloodCells >= FloodCellTelemetryThreshold)
                GlobalTelemetryBus.PublishPerformanceWarning(FloodClampWarningHash, AnomalyNodeContextHash, resolvedMaxFloodCells);
        }

        private static void PublishSolvePerformanceWarningIfNeeded(long elapsedTicks)
        {
            if (elapsedTicks <= SolvePerformanceWarningBudgetTicks)
                return;

            float elapsedMs = elapsedTicks * (1000f / Stopwatch.Frequency);
            GlobalTelemetryBus.PublishPerformanceWarning(SolveBudgetWarningHash, AnomalyNodeContextHash, elapsedMs);
        }

        private static void RegisterTempJobBuffers(
            NativeArray<float> heightmap,
            NativeArray<byte> basinMask,
            NativeArray<float> basinLipMask,
            NativeArray<byte> candidateMask,
            NativeArray<AnomalyBasinRecord> basinRecords,
            NativeArray<AnomalyFeatureRecord> featureRecords,
            NativeArray<byte> fissureMask,
            NativeArray<int> floodHeap,
            NativeArray<int> visitedStamp,
            NativeArray<int> acceptedCells,
            out int heightmapRegistrationId,
            out int basinMaskRegistrationId,
            out int basinLipMaskRegistrationId,
            out int candidateMaskRegistrationId,
            out int basinRecordsRegistrationId,
            out int featureRecordsRegistrationId,
            out int fissureMaskRegistrationId,
            out int floodHeapRegistrationId,
            out int visitedStampRegistrationId,
            out int acceptedCellsRegistrationId)
        {
            heightmapRegistrationId = RegisterTempJobArray(heightmap, HeightLabel);
            basinMaskRegistrationId = RegisterTempJobArray(basinMask, BasinMaskLabel);
            basinLipMaskRegistrationId = RegisterTempJobArray(basinLipMask, BasinLipMaskLabel);
            candidateMaskRegistrationId = RegisterTempJobArray(candidateMask, CandidateMaskLabel);
            basinRecordsRegistrationId = RegisterTempJobArray(basinRecords, BasinRecordsLabel);
            featureRecordsRegistrationId = RegisterTempJobArray(featureRecords, FeatureRecordsLabel);
            fissureMaskRegistrationId = RegisterTempJobArray(fissureMask, FissureMaskLabel);
            floodHeapRegistrationId = RegisterTempJobArray(floodHeap, FloodHeapLabel);
            visitedStampRegistrationId = RegisterTempJobArray(visitedStamp, VisitedStampLabel);
            acceptedCellsRegistrationId = RegisterTempJobArray(acceptedCells, AcceptedCellsLabel);
        }

        private static int RegisterTempJobArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            int registrationId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeAllocationLifetime.TempJob);
            if (registrationId <= 0)
                throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");

            return registrationId;
        }

        private static void DisposeTracked<T>(ref NativeArray<T> array, ref int registrationId) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.Unregister(registrationId);
            registrationId = 0;
            array.Dispose();
            array = default;
        }
    }
}
