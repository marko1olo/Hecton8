using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {
        public bool TryGetLatestAbyssalPathPayload(out NativeArray<Vector3>.ReadOnly path, out int count)
        {
            count = _abyssalPathCount;
            if (count <= 0)
            {
                path = default;
                return false;
            }

            return TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.AbyssalPathSnapshotHandle,
                BufferID.VegetationAbyssalPathSnapshot,
                count,
                out path);
        }

        /// <summary>
        /// Builds an immediate non-allocating 3D voxel route through the active cave portal graph.
        /// This is restricted to pure cave-voxel traversal and is intended for fauna steering guidance.
        /// </summary>
        public bool TryBuildImmediateAbyssalVoxelRoute(Vector3 startPosition, Vector3 endPosition, Vector3[] outputWaypoints, out int waypointCount)
        {
            waypointCount = 0;
            if (outputWaypoints == null ||
                outputWaypoints.Length < 2 ||
                !IsFinite(startPosition) ||
                !IsFinite(endPosition))
            {
                return false;
            }

            float3 startProbe = new float3(startPosition.x, startPosition.y, startPosition.z);
            float3 endProbe = new float3(endPosition.x, endPosition.y, endPosition.z);
            if (!VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(startProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample startSample) ||
                !VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(endProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample endSample) ||
                startSample.Mode != VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel ||
                endSample.Mode != VoxelDynamicNavGridRuntime.HybridNavigationMode.CaveVoxel)
            {
                return false;
            }

            return VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc(startProbe, endProbe, outputWaypoints, out waypointCount);
        }

        /// <summary>
        /// Builds a bounded native abyssal A* solve between the nearest safe nav nodes to the provided world positions.
        /// </summary>
        public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, out JobHandle handle)
        {
            return TryScheduleAbyssalPath(startPosition, endPosition, 0, out handle);
        }

        /// <summary>
        /// Builds a bounded native abyssal A* solve between the nearest safe nav nodes to the provided world positions.
        /// Species-aware predator fear penalties are applied when <paramref name="traversalSpeciesId"/> is non-zero.
        /// </summary>
        public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, int traversalSpeciesId, out JobHandle handle)
        {
            handle = default;
            if (!IsFinite(startPosition) || !IsFinite(endPosition))
                return false;

            if (_abyssalPathScheduled ||
                _abyssalNavNodeCount <= 0 ||
                !HasAbyssalNavSnapshotPayload(_abyssalNavNodeCount))
            {
                return false;
            }

            EnsurePredatorFearMemoryBuffers();
            SyncPredatorFearNodeSnapshot(_predatorFearSimulationTime);

            float3 startProbe = new float3(startPosition.x, startPosition.y, startPosition.z);
            float3 endProbe = new float3(endPosition.x, endPosition.y, endPosition.z);
            bool hasStartHybridSample = VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(startProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample startHybridSample);
            bool hasEndHybridSample = VoxelDynamicNavGridRuntime.TrySampleHybridNavigation(endProbe, out VoxelDynamicNavGridRuntime.HybridNavigationSample endHybridSample);
            bool hasStartTerrainHeight = startHybridSample.HasTerrainHeight != 0 || TryGetCachedTerrainHeight(startPosition.x, startPosition.z, out startHybridSample.TerrainHeight);
            bool hasEndTerrainHeight = endHybridSample.HasTerrainHeight != 0 || TryGetCachedTerrainHeight(endPosition.x, endPosition.z, out endHybridSample.TerrainHeight);
            VoxelDynamicNavGridRuntime.HybridNavigationMode startNavMode = hasStartHybridSample ? startHybridSample.Mode : VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            VoxelDynamicNavGridRuntime.HybridNavigationMode endNavMode = hasEndHybridSample ? endHybridSample.Mode : VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            bool startUsesHeightmap = startNavMode == VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            bool endUsesHeightmap = endNavMode == VoxelDynamicNavGridRuntime.HybridNavigationMode.OpenWaterHeightmap;
            bool startUsesVoxel = !startUsesHeightmap;
            bool endUsesVoxel = !endUsesHeightmap;

            Vector3 resolvedStartPosition = startPosition;
            if (startUsesHeightmap && hasStartTerrainHeight)
                resolvedStartPosition.y = math.max(startPosition.y, startHybridSample.TerrainHeight + abyssalNavNodeHoverHeight);

            Vector3 resolvedEndPosition = endPosition;
            if (endUsesHeightmap && hasEndTerrainHeight)
                resolvedEndPosition.y = math.max(endPosition.y, endHybridSample.TerrainHeight + abyssalNavNodeHoverHeight);

            int startNode = FindNearestAbyssalNavNodeIndex(resolvedStartPosition);
            int endNode = FindNearestAbyssalNavNodeIndex(resolvedEndPosition);
            if (startNode < 0 || endNode < 0)
                return false;

            bool canReuseLastAbyssalTarget = startUsesHeightmap && endUsesHeightmap;
            if (canReuseLastAbyssalTarget &&
                _hasLastAbyssalPathTarget &&
                _lastAbyssalPathEndNode >= 0 &&
                _lastAbyssalPathEndNode < _abyssalNavNodeCount &&
                (resolvedEndPosition - _lastAbyssalPathTargetPosition).sqrMagnitude < (abyssalPathRetargetDistance * abyssalPathRetargetDistance))
            {
                endNode = _lastAbyssalPathEndNode;
            }

            if (!EnsureAbyssalPathBuffers(_abyssalNavNodeCount))
                return false;

            _abyssalPathCount = 0;
            int fixedPathCapacity = ResolveMaxAbyssalPathWaypointCapacity();
            NativeList<Vector3> rawPath = new NativeList<Vector3>(fixedPathCapacity, Allocator.Persistent);
            NativeList<Vector3> resultPath = new NativeList<Vector3>(fixedPathCapacity, Allocator.Persistent);
            if (!rawPath.IsCreated || !resultPath.IsCreated)
            {
                if (rawPath.IsCreated)
                    rawPath.Dispose();
                if (resultPath.IsCreated)
                    resultPath.Dispose();

                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationAbyssalPathSnapshot,
                    _nativeMemory.AbyssalPathSnapshotHandle.Generation,
                    fixedPathCapacity,
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.VaultResolveFailed,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return false;
            }

            bool scheduled = false;
            NativeArray<VegetationDensityChunkRecord> densityChunksForJob = default;
            NativeArray<float3> densityGridForJob = default;
            NativeArray<float2> threatAttractorGridForJob = default;
            NativeArray<TerrainHoleRecord> terrainHolesForJob = default;
            NativeArray<ArtificialStructureRecord> artificialStructuresForJob = default;
            NativeArray<byte> navPassabilityGridForJob = default;
            NativeArray<byte> threatVoxelGridForSmoothing = default;
            try
            {
                JobHandle pathSourceHandle = default;
                bool scheduledMacroVoxelRoute = false;
                if (startUsesVoxel &&
                    endUsesVoxel &&
                    VoxelDynamicNavGridRuntime.TryBuildMacroPortalRoute(startProbe, endProbe, rawPath))
                {
                    scheduledMacroVoxelRoute = true;
                }

                if (!scheduledMacroVoxelRoute)
                {
                    NativeArray<PredatorFearNodeSnapshot> predatorFearNodesForJob = default;
                    NativeArray<Vector3> navNodesForJob = default;
                    NativeArray<byte> navNodeTypesForJob = default;
                    NativeArray<Vector3> conduitVectorsForJob = default;
                    NativeArray<float> conduitStrengthsForJob = default;
                    NativeArray<int> pathParentsForJob = default;
                    NativeArray<float> pathGScoreForJob = default;
                    NativeArray<float> pathFScoreForJob = default;
                    NativeArray<byte> pathClosedFlagsForJob = default;
                    NativeArray<int> pathHeapNodesForJob = default;
                    NativeArray<int> pathHeapPositionsForJob = default;
                    int predatorFearNodeCountForJob = 0;
                    int predatorFearNodeCount = math.max(0, _predatorFearNodeCount);
                    if (predatorFearNodeCount > 0 &&
                        TryReadVegetationMemoryBuffer(
                            in _nativeMemory.PredatorFearNodesSnapshotHandle,
                            BufferID.VegetationPredatorFearNodeSnapshot,
                            predatorFearNodeCount,
                            out NativeArray<PredatorFearNodeSnapshot> predatorFearSnapshots))
                    {
                        int copyCount = math.min(predatorFearNodeCount, predatorFearSnapshots.Length);
                        predatorFearNodesForJob = H8Memory.Allocate<PredatorFearNodeSnapshot>(
                            copyCount,
                            VegetationMemorySovereigntyConstants.OwnerSystemId,
                            Allocator.Persistent,
                            NativeArrayOptions.UninitializedMemory);
                        if (predatorFearNodesForJob.IsCreated)
                        {
                            for (int i = 0; i < copyCount; i++)
                                predatorFearNodesForJob[i] = predatorFearSnapshots[i];

                            predatorFearNodeCountForJob = copyCount;
                        }
                        else
                        {
                            RecordVegetationMemoryTelemetry(
                                BufferID.VegetationPredatorFearNodeSnapshot,
                                _nativeMemory.PredatorFearNodesSnapshotHandle.Generation,
                                copyCount,
                                0,
                                0,
                                0f,
                                VegetationMemoryTelemetryCode.VaultResolveFailed,
                                VegetationMemoryTelemetryPhase.SlowTick,
                                VegetationMemorySovereigntyConstants.FlagCapacity,
                                default);
                        }
                    }

                    if (!TryCreateAbyssalNavJobSnapshot(
                            _abyssalNavNodeCount,
                            out navNodesForJob,
                            out navNodeTypesForJob,
                            out conduitVectorsForJob,
                            out conduitStrengthsForJob))
                    {
                        H8Memory.Release(ref predatorFearNodesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        return false;
                    }

                    if (!TryCreateAbyssalPathScratch(
                            _abyssalNavNodeCount,
                            out pathParentsForJob,
                            out pathGScoreForJob,
                            out pathFScoreForJob,
                            out pathClosedFlagsForJob,
                            out pathHeapNodesForJob,
                            out pathHeapPositionsForJob))
                    {
                        H8Memory.Release(ref predatorFearNodesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref navNodesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref navNodeTypesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref conduitVectorsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref conduitStrengthsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        return false;
                    }

                    NativeArray<float> threatGridForJob = default;
                    NativeArray<byte> threatVoxelGridForJob = default;
                    if (!TryReadVegetationMemoryBuffer(
                            in _nativeMemory.EcosystemThreatGridHandle,
                            BufferID.VegetationEcosystemThreatGrid,
                            _ecosystemThreatGridCellCount,
                            out NativeArray<float> threatGridSource) ||
                        !TryReadVegetationMemoryBuffer(
                            in _nativeMemory.EcosystemThreatVoxelHandle,
                            BufferID.VegetationEcosystemThreatVoxel,
                            _ecosystemThreatVoxelCellCount,
                            out NativeArray<byte> threatVoxelGridSource) ||
                        !TryCreateReadOnlyAbyssalPathJobSnapshot(
                            threatGridSource.AsReadOnly(),
                            _ecosystemThreatGridCellCount,
                            out threatGridForJob) ||
                        !TryCreateReadOnlyAbyssalPathJobSnapshot(
                            threatVoxelGridSource.AsReadOnly(),
                            _ecosystemThreatVoxelCellCount,
                            out threatVoxelGridForJob))
                    {
                        H8Memory.Release(ref predatorFearNodesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref navNodesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref navNodeTypesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref conduitVectorsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref conduitStrengthsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref pathParentsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref pathGScoreForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref pathFScoreForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref pathClosedFlagsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref pathHeapNodesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref pathHeapPositionsForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref threatGridForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        H8Memory.Release(ref threatVoxelGridForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                        return false;
                    }

                    var astarJob = new NativeAStarJob
                    {
                        Nodes = navNodesForJob,
                        NodeTypes = navNodeTypesForJob,
                        ConduitVectors = conduitVectorsForJob,
                        ConduitStrengths = conduitStrengthsForJob,
                        ThreatGrid = threatGridForJob,
                        ThreatVoxelGrid = threatVoxelGridForJob,
                        ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                        ThreatGridCellSize = threatGridCellSize,
                        ThreatGridResolution = _ecosystemThreatGridResolution,
                        ThreatVoxelDimensions = new int3(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution),
                        ThreatVoxelOrigin = new float3(_ecosystemThreatVoxelOrigin.x, _ecosystemThreatVoxelOrigin.y, _ecosystemThreatVoxelOrigin.z),
                        ThreatVoxelCellSize = new float3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize),
                        WaterLevel = waterLevel,
                        Parents = pathParentsForJob,
                        GScore = pathGScoreForJob,
                        FScore = pathFScoreForJob,
                        ClosedFlags = pathClosedFlagsForJob,
                        HeapNodes = pathHeapNodesForJob,
                        HeapPositions = pathHeapPositionsForJob,
                        Path = rawPath,
                        PredatorFearNodes = predatorFearNodesForJob,
                        PredatorFearNodeCount = predatorFearNodeCountForJob,
                        TraversalSpeciesId = traversalSpeciesId,
                        PredatorFearPenaltyWeight = predatorFearPathPenaltyWeight,
                        StartNode = startNode,
                        EndNode = endNode,
                        StartPosition = new float3(resolvedStartPosition.x, resolvedStartPosition.y, resolvedStartPosition.z),
                        EndPosition = new float3(resolvedEndPosition.x, resolvedEndPosition.y, resolvedEndPosition.z),
                        NeighborRadius = abyssalPathNeighborRadius,
                        VerticalTolerance = abyssalPathVerticalTolerance,
                        ThreatPenaltyWeight = abyssalPathThreatPenaltyWeight,
                        ConduitStartDepth = abyssalConduitStartDepth,
                        ConduitVerticalToleranceBonus = abyssalConduitVerticalToleranceBonus,
                        ConduitMisalignmentPenalty = abyssalConduitMisalignmentPenalty,
                        ConduitAlignmentReward = abyssalConduitAlignmentReward,
                        InteriorTraversalCostMultiplier = abyssalInteriorTraversalCostMultiplier,
                        MaxExpandedNodes = abyssalPathMaxExpandedNodes
                    };

                    pathSourceHandle = astarJob.Schedule();
                    pathSourceHandle = H8Memory.Release(
                        ref navNodesForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref navNodeTypesForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref conduitVectorsForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref conduitStrengthsForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref pathParentsForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref pathGScoreForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref pathFScoreForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref pathClosedFlagsForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref pathHeapNodesForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref pathHeapPositionsForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref threatGridForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    pathSourceHandle = H8Memory.Release(
                        ref threatVoxelGridForJob,
                        pathSourceHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                    if (predatorFearNodesForJob.IsCreated)
                        pathSourceHandle = H8Memory.Release(
                            ref predatorFearNodesForJob,
                            pathSourceHandle,
                            VegetationMemorySovereigntyConstants.OwnerSystemId);
                }

                NativeArray<byte>.ReadOnly navPassabilityGrid = default;
                int3 navPassabilityDimensions = int3.zero;
                float3 navPassabilityOrigin = float3.zero;
                float navPassabilityCellSize = 0f;

                if (startUsesVoxel &&
                    !VoxelDynamicNavGridRuntime.TryGetContainingPassabilityPayload(
                        startProbe,
                        out navPassabilityGrid,
                        out navPassabilityDimensions,
                        out navPassabilityOrigin,
                        out navPassabilityCellSize))
                {
                    VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload(
                        startProbe,
                        out navPassabilityGrid,
                        out navPassabilityDimensions,
                        out navPassabilityOrigin,
                        out navPassabilityCellSize);
                }

                if (navPassabilityGrid.Length <= 0 &&
                    endUsesVoxel &&
                    !VoxelDynamicNavGridRuntime.TryGetContainingPassabilityPayload(
                        endProbe,
                        out navPassabilityGrid,
                        out navPassabilityDimensions,
                        out navPassabilityOrigin,
                        out navPassabilityCellSize))
                {
                    VoxelDynamicNavGridRuntime.TryGetNearestPassabilityPayload(
                        endProbe,
                        out navPassabilityGrid,
                        out navPassabilityDimensions,
                        out navPassabilityOrigin,
                        out navPassabilityCellSize);
                }

                TryCreateReadOnlyAbyssalPathJobSnapshot(
                    navPassabilityGrid,
                    navPassabilityGrid.Length,
                    out navPassabilityGridForJob);

                int smoothingPortalLookAhead = ResolveAbyssalPathPortalLookAhead();
                int smoothingDdaSampleCap = ResolveAbyssalPathDdaSampleCap(abyssalPathSmoothingMaxSamples);
                EnsureAbyssalPathTelemetry();
                _lastAbyssalPathPortalLookAhead = smoothingPortalLookAhead;
                _lastAbyssalPathMaxSamples = smoothingDdaSampleCap;
                int densityChunkCountForJob = 0;
                if (TryPrepareDensityQueryJobSnapshot(
                        true,
                        false,
                        out densityChunksForJob,
                        out densityGridForJob,
                        out threatAttractorGridForJob) &&
                    densityChunksForJob.IsCreated &&
                    densityGridForJob.IsCreated)
                {
                    densityChunkCountForJob = _densityQueryChunkCount;
                }

                TryCreateTerrainHoleJobSnapshot(out terrainHolesForJob);

                int terrainHoleCountForJob = terrainHolesForJob.IsCreated ? _terrainHoleCount : 0;
                TryPrepareArtificialStructureJobSnapshot(out artificialStructuresForJob);

                if (TryReadVegetationMemoryBuffer(
                        in _nativeMemory.EcosystemThreatVoxelHandle,
                        BufferID.VegetationEcosystemThreatVoxel,
                        _ecosystemThreatVoxelCellCount,
                        out NativeArray<byte> threatVoxelGridForSmoothingSource))
                {
                    TryCreateReadOnlyAbyssalPathJobSnapshot(
                        threatVoxelGridForSmoothingSource.AsReadOnly(),
                        _ecosystemThreatVoxelCellCount,
                        out threatVoxelGridForSmoothing);
                }

                var smoothingJob = new StringPullPathJob
                {
                    InputPath = rawPath.AsDeferredJobArray(),
                    DensityChunks = densityChunksForJob,
                    DensityGrid = densityGridForJob,
                    ChunkCount = densityChunkCountForJob,
                    TerrainHoles = terrainHolesForJob,
                    TerrainHoleCount = terrainHoleCountForJob,
                    ArtificialStructures = artificialStructuresForJob,
                    ArtificialStructureHash = default,
                    NavPassabilityGrid = navPassabilityGridForJob.IsCreated ? navPassabilityGridForJob.AsReadOnly() : default,
                    ThreatVoxelGrid = threatVoxelGridForSmoothing,
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    NavPassabilityDimensions = navPassabilityDimensions,
                    NavPassabilityOrigin = navPassabilityOrigin,
                    NavPassabilityCellSize = navPassabilityCellSize,
                    ThreatVoxelDimensions = new int3(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution),
                    ThreatVoxelOrigin = new float3(_ecosystemThreatVoxelOrigin.x, _ecosystemThreatVoxelOrigin.y, _ecosystemThreatVoxelOrigin.z),
                    ThreatVoxelCellSize = new float3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize),
                    SampleSpacing = abyssalPathSmoothingSampleSpacing,
                    MaxSamplesPerSegment = smoothingDdaSampleCap,
                    MaxPortalLookAhead = smoothingPortalLookAhead,
                    KelpWeight = abyssalPathSmoothingKelpWeight,
                    SargassumWeight = abyssalPathSmoothingSargassumWeight,
                    DensityObstacleThreshold = abyssalPathSmoothingObstacleThreshold,
                    OutputPath = resultPath
                };

                long scheduleStartTicks = Stopwatch.GetTimestamp();
                JobHandle smoothingHandle = smoothingJob.Schedule(pathSourceHandle);
                _abyssalPathJob = new AbyssalPathPendingJob
                {
                    RawPath = rawPath,
                    ResultPath = resultPath,
                    DensityChunks = densityChunksForJob,
                    DensityGrid = densityGridForJob,
                    ThreatAttractorGrid = threatAttractorGridForJob,
                    TerrainHoles = terrainHolesForJob,
                    ArtificialStructures = artificialStructuresForJob,
                    NavPassabilityGrid = navPassabilityGridForJob,
                    ThreatVoxelGrid = threatVoxelGridForSmoothing,
                    TargetPosition = resolvedEndPosition,
                    EndNode = endNode,
                    ScheduleTicks = scheduleStartTicks,
                    CanReuseLastTarget = canReuseLastAbyssalTarget,
                    ScheduledMacroVoxelRoute = scheduledMacroVoxelRoute,
                    Handle = smoothingHandle
                };
                _abyssalPathHandle = smoothingHandle;
                _abyssalPathScheduled = true;
                handle = smoothingHandle;
                scheduled = true;

                rawPath = default;
                resultPath = default;
                densityChunksForJob = default;
                densityGridForJob = default;
                threatAttractorGridForJob = default;
                terrainHolesForJob = default;
                artificialStructuresForJob = default;
                navPassabilityGridForJob = default;
                threatVoxelGridForSmoothing = default;
                return true;
            }
            finally
            {
                H8Memory.Release(ref densityChunksForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref densityGridForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref threatAttractorGridForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref terrainHolesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref artificialStructuresForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref navPassabilityGridForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref threatVoxelGridForSmoothing, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (rawPath.IsCreated)
                    rawPath.Dispose();
                if (resultPath.IsCreated)
                    resultPath.Dispose();
                if (!scheduled)
                {
                    _abyssalPathCount = 0;
                    _abyssalPathJob = default;
                    _abyssalPathScheduled = false;
                    _abyssalPathHandle = default;
                }
            }
        }

        private static int ResolveAbyssalPathPortalLookAhead()
        {
            float quality = ResolveAbyssalPathQualityWeight();
            return ResolveAbyssalPathQualityBudget(
                quality,
                LowTierAbyssalPathPortalLookAhead,
                MidTierAbyssalPathPortalLookAhead,
                HighTierAbyssalPathPortalLookAhead);
        }

        private static int ResolveAbyssalPathDdaSampleCap(int configuredSampleCap)
        {
            int safeCap = math.clamp(configuredSampleCap, 1, MaxThreatDdaSteps);
            float quality = ResolveAbyssalPathQualityWeight();
            return ResolveAbyssalPathQualityBudget(
                quality,
                math.min(LowTierAbyssalPathDdaSamples, safeCap),
                math.min(MidTierAbyssalPathDdaSamples, safeCap),
                safeCap);
        }

        private static float ResolveAbyssalPathQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static int ResolveAbyssalPathQualityBudget(float quality, int lowBudget, int midBudget, int highBudget)
        {
            int safeLow = math.max(1, lowBudget);
            int safeMid = math.max(safeLow, midBudget);
            int safeHigh = math.max(safeMid, highBudget);
            float q = math.saturate(quality);
            float lowToMidWeight = SmoothAbyssalPathQuality(q * 2f);
            float midToHighWeight = SmoothAbyssalPathQuality((q - 0.5f) * 2f);
            float lowToMid = math.lerp(safeLow, safeMid, lowToMidWeight);
            float midToHigh = math.lerp(safeMid, safeHigh, midToHighWeight);
            float resolved = math.lerp(lowToMid, midToHigh, midToHighWeight);
            return math.clamp((int)math.round(resolved), safeLow, safeHigh);
        }

        private static float SmoothAbyssalPathQuality(float value)
        {
            float q = math.saturate(value);
            return q * q * (3f - 2f * q);
        }

        private static bool TryCreateReadOnlyAbyssalPathJobSnapshot<T>(
            NativeArray<T>.ReadOnly source,
            int requiredCount,
            out NativeArray<T> snapshot)
            where T : struct
        {
            snapshot = default;
            if (requiredCount <= 0)
                return true;

            if (!source.IsCreated || source.Length < requiredCount)
                return false;

            snapshot = H8Memory.Allocate<T>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            if (!snapshot.IsCreated)
                return false;

            for (int i = 0; i < requiredCount; i++)
                snapshot[i] = source[i];

            return true;
        }

        /// <summary>
        /// Applies an arbitrary caller-owned surface flow-vector field to the active surface payload without binding to a specific ocean backend.
        /// </summary>
        public bool TryApplyExternalSurfaceFlowVectorField(NativeArray<Vector3> flowVectors, int count)
        {
            if (count <= 0 ||
                count != _surfaceFrontCount ||
                !flowVectors.IsCreated)
            {
                return false;
            }

            bool flowDirectionsLocked = false;
            bool flowVectorsLocked = false;
            IDataVault flowDirectionsVault = null;
            IDataVault flowVectorsVault = null;
            try
            {
                if (!TryAcquireAggregateWriteBuffer(ref _surfaceAggregateFrontBuffers.FlowDirectionsHandle, count, out flowDirectionsVault, out NativeArray<Vector2> flowDirections))
                    return false;
                flowDirectionsLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref _surfaceAggregateFrontBuffers.FlowVectorsHandle, count, out flowVectorsVault, out NativeArray<Vector3> surfaceFlowVectors))
                    return false;
                flowVectorsLocked = true;

                int safeCount = math.min(count, math.min(flowDirections.Length, surfaceFlowVectors.Length));
                for (int i = 0; i < safeCount; i++)
                {
                    Vector3 flowVector = flowVectors[i];
                    Vector2 flowDirection = NormalizeFlowDirection(new Vector2(flowVector.x, flowVector.z));
                    surfaceFlowVectors[i] = flowVector;
                    flowDirections[i] = flowDirection;
                }
            }
            finally
            {
                if (flowVectorsLocked)
                    flowVectorsVault.ReleaseWriteLock(in _surfaceAggregateFrontBuffers.FlowVectorsHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (flowDirectionsLocked)
                    flowDirectionsVault.ReleaseWriteLock(in _surfaceAggregateFrontBuffers.FlowDirectionsHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            return true;
        }

        /// <summary>
        /// Marks streamed chunks intersecting the requested zone as corrupted and invalidates their payloads for async rebuild.
        /// </summary>

        private ChunkAbyssalNavPayload BuildChunkAbyssalNavPayload(ChunkKey key, ChunkBuildJobState jobState, ChunkPayload payload)
        {
            ChunkAbyssalNavPayload navPayload = default;
            if (!payload.HasUnderwater)
                return navPayload;

            if (!_tileStates.TryGetValue(jobState.TileKey, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples) ||
                !SliceContainsDeepBiome(ResolveChunkPool(isSurface: false, payload), payload.UnderwaterOffset, payload.UnderwaterCount))
            {
                return navPayload;
            }

            TouchTileCacheState(state);

            if (!math.isfinite(payload.MinX) ||
                !math.isfinite(payload.MaxX) ||
                !math.isfinite(payload.MinZ) ||
                !math.isfinite(payload.MaxZ) ||
                abyssalNavNodeStepMeters <= 0f ||
                !math.isfinite(abyssalNavNodeStepMeters))
            {
                return navPayload;
            }

            float chunkWidth = math.max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = math.max(0.01f, payload.MaxZ - payload.MinZ);
            float inverseNodeStep = math.rcp(math.max(0.01f, abyssalNavNodeStepMeters));
            int sampleCountX = math.max(1, (int)math.floor(chunkWidth * inverseNodeStep));
            int sampleCountZ = math.max(1, (int)math.floor(chunkDepth * inverseNodeStep));
            int holeNodeCount = CountTerrainHolesIntersectingChunk(payload.MinX, payload.MaxX, payload.MinZ, payload.MaxZ);
            int maxNodeCount = sampleCountX * sampleCountZ + holeNodeCount;
            if (maxNodeCount <= 0)
                return navPayload;

            Vector3[] nodes = null;
            Vector3[] conduitVectors = null;
            float[] conduitStrengths = null;
            byte[] nodeTypes = null;
            bool hasExistingPayload = TryGetChunkAbyssalNavPayload(key, out ChunkAbyssalNavPayload existingPayload);
            bool reusedExistingPayload = hasExistingPayload &&
                existingPayload.Nodes != null &&
                existingPayload.Nodes.Length >= maxNodeCount;
            if (reusedExistingPayload)
            {
                nodes = existingPayload.Nodes;
                if (existingPayload.ConduitVectors != null && existingPayload.ConduitVectors.Length >= maxNodeCount)
                    conduitVectors = existingPayload.ConduitVectors;

                if (existingPayload.ConduitStrengths != null && existingPayload.ConduitStrengths.Length >= maxNodeCount)
                    conduitStrengths = existingPayload.ConduitStrengths;

                if (existingPayload.NodeTypes != null && existingPayload.NodeTypes.Length >= maxNodeCount)
                    nodeTypes = existingPayload.NodeTypes;
            }

            EnsureVector3Capacity(ref nodes, maxNodeCount);
            EnsureVector3Capacity(ref conduitVectors, maxNodeCount);
            EnsureFloatCapacity(ref conduitStrengths, maxNodeCount);
            EnsureByteCapacity(ref nodeTypes, maxNodeCount);

            float stepX = chunkWidth * math.rcp(math.max(1, sampleCountX));
            float stepZ = chunkDepth * math.rcp(math.max(1, sampleCountZ));
            int writeIndex = 0;
            for (int sampleZ = 0; sampleZ < sampleCountZ; sampleZ++)
            {
                float worldZ = payload.MinZ + ((sampleZ + 0.5f) * stepZ);
                for (int sampleX = 0; sampleX < sampleCountX; sampleX++)
                {
                    float worldX = payload.MinX + ((sampleX + 0.5f) * stepX);
                    if (!TrySampleCachedTerrainHeight(state, heightSamples, worldX, worldZ, out float terrainY))
                        continue;

                    Vector3 candidate = new Vector3(worldX, terrainY + abyssalNavNodeHoverHeight, worldZ);
                    if (!TryResolveAbyssalNavNodeCandidate(candidate, payload, out Vector3 conduitVector, out float conduitStrength, out NavNodeType nodeType))
                        continue;

                    nodes[writeIndex] = candidate;
                    conduitVectors[writeIndex] = conduitVector;
                    conduitStrengths[writeIndex] = conduitStrength;
                    nodeTypes[writeIndex] = (byte)nodeType;
                    writeIndex++;
                }
            }

            if (holeNodeCount > 0)
            {
                for (int i = 0; i < _terrainHoleCount; i++)
                {
                    TerrainHoleRecord hole = _terrainHoleRecords[i];
                    if (!DoesChunkBoundsIntersectCircle(payload.MinX, payload.MaxX, payload.MinZ, payload.MaxZ, hole.X, hole.Z, hole.RadiusSq) ||
                        !TrySampleCachedTerrainHeight(state, heightSamples, hole.X, hole.Z, out float terrainY))
                    {
                        continue;
                    }

                    Vector3 holeNode = new Vector3(hole.X, terrainY + abyssalNavNodeHoverHeight, hole.Z);
                    nodes[writeIndex] = holeNode;
                    conduitVectors[writeIndex] = Vector3.zero;
                    conduitStrengths[writeIndex] = 0f;
                    nodeTypes[writeIndex] = (byte)NavNodeType.Interior;
                    writeIndex++;
                }
            }

            if (writeIndex <= 0)
                return navPayload;

            navPayload.Nodes = nodes;
            navPayload.ConduitVectors = conduitVectors;
            navPayload.ConduitStrengths = conduitStrengths;
            navPayload.NodeTypes = nodeTypes;
            navPayload.Count = writeIndex;
            return navPayload;
        }

        private bool TryResolveAbyssalNavNodeCandidate(
            Vector3 candidate,
            ChunkPayload payload,
            out Vector3 conduitVector,
            out float conduitStrength,
            out NavNodeType nodeType)
        {
            conduitVector = Vector3.zero;
            conduitStrength = 0f;
            nodeType = NavNodeType.Water;
            if (!IsFinite(candidate))
                return false;

            if (IsInsideRegisteredTerrainHole(candidate.x, candidate.z))
            {
                nodeType = NavNodeType.Interior;
                return true;
            }

            if (TryResolveArtificialStructureAtPosition(candidate, out _))
            {
                nodeType = NavNodeType.Interior;
                return true;
            }

            float obstacleRadiusSq = abyssalNavNodeObstacleRadius * abyssalNavNodeObstacleRadius;
            float maxVerticalDelta = abyssalNavNodeObstacleVerticalWindow;
            float obstacleWeight = 0f;
            float deepAffinity = 0f;
            float flowMagnitudeSum = 0f;
            Vector3 flowVectorSum = Vector3.zero;
            int contributingSamples = 0;
            NativeChunkPool underwaterPool = ResolveChunkPool(isSurface: false, payload);
            int requiredPoolCount = payload.UnderwaterOffset + math.max(0, payload.UnderwaterCount);
            if (!TryReadChunkPoolView(in underwaterPool, requiredPoolCount, out NativeChunkPoolView underwaterView))
            {
                return false;
            }

            int availableLength = math.min(
                underwaterView.Matrices.Length,
                math.min(underwaterView.BiomeLayers.Length, underwaterView.SemanticTypes.Length));
            if (availableLength <= 0)
                return false;

            int startIndex = math.clamp(payload.UnderwaterOffset, 0, availableLength);
            long requestedEnd = (long)payload.UnderwaterOffset + math.max(0, payload.UnderwaterCount);
            long clampedEnd = requestedEnd > availableLength ? availableLength : requestedEnd;
            if (clampedEnd < startIndex)
                clampedEnd = startIndex;

            int end = (int)clampedEnd;
            for (int poolIndex = startIndex; poolIndex < end; poolIndex++)
            {
                Vector3 position = ResolveRuntimePosition(underwaterView.Matrices[poolIndex]);
                if (!IsFinite(position))
                    continue;

                float dx = position.x - candidate.x;
                float dz = position.z - candidate.z;
                float horizontalDistanceSq = (dx * dx) + (dz * dz);
                if (horizontalDistanceSq > obstacleRadiusSq)
                    continue;

                float verticalDelta = math.abs(position.y - candidate.y);
                if (verticalDelta > maxVerticalDelta)
                    continue;

                byte biomeLayer = underwaterView.BiomeLayers[poolIndex];
                int semanticType = underwaterView.SemanticTypes[poolIndex];
                float semanticWeight = ResolveAbyssalNavObstacleWeight(semanticType, biomeLayer);
                if (semanticWeight <= 0f)
                    continue;

                obstacleWeight += semanticWeight;
                if (biomeLayer >= (byte)VegetationBiomeLayer.ColonyGraveyard)
                    deepAffinity += semanticWeight;

                Vector3 flowVector = underwaterView.FlowVectors.IsCreated && poolIndex < underwaterView.FlowVectors.Length
                    ? underwaterView.FlowVectors[poolIndex]
                    : Vector3.zero;
                if (IsFinite(flowVector))
                {
                    flowMagnitudeSum += EstimateLength3D(flowVector);
                    flowVectorSum += flowVector;
                    contributingSamples++;
                }
                if (obstacleWeight > abyssalNavNodeMaxObstacleDensity)
                    return false;
            }

            if (deepAffinity < abyssalNavNodeMinimumDeepAffinity)
                return false;

            float averageCurrentMagnitude = contributingSamples > 0
                ? flowMagnitudeSum * math.rcp(math.max(1, contributingSamples))
                : 0f;
            if (averageCurrentMagnitude > abyssalNavNodeMaxCurrentMagnitude)
                return false;

            float depthMeters = math.max(0f, waterLevel - candidate.y);
            if (depthMeters < abyssalConduitStartDepth ||
                averageCurrentMagnitude < abyssalConduitMinimumFlowMagnitude ||
                contributingSamples <= 0)
            {
                return true;
            }

            if (flowVectorSum.sqrMagnitude <= 0.0001f)
                return true;

            conduitVector = NormalizeVector3Fast(flowVectorSum, Vector3.forward);
            if (abyssalNavNodeMaxCurrentMagnitude <= abyssalConduitMinimumFlowMagnitude)
            {
                conduitStrength = 1f;
                return true;
            }

            float conduitStrengthRange = math.max(
                0.01f,
                abyssalNavNodeMaxCurrentMagnitude - abyssalConduitMinimumFlowMagnitude);
            conduitStrength = math.saturate(
                (averageCurrentMagnitude - abyssalConduitMinimumFlowMagnitude) * math.rcp(conduitStrengthRange));
            return true;
        }

        private bool TryResolveArtificialStructureAtPosition(Vector3 position, out StructureType type)
        {
            for (int i = 0; i < _persistentArtificialStructureCount; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (!structure.Bounds.Contains(position))
                    continue;

                type = structure.Type;
                return true;
            }

            for (int i = 0; i < _megaWreckStreamCount; i++)
            {
                Bounds bounds = GetMegaWreckSectionBounds(_megaWreckStreamSnapshot[i]);
                if (!bounds.Contains(position))
                    continue;

                type = StructureType.MegaWreck;
                return true;
            }

            type = default;
            return false;
        }

        private static bool TrySampleCachedTerrainHeight(TileRuntimeState state, NativeArray<ushort> heightSamples, float worldX, float worldZ, out float terrainHeight)
        {
            terrainHeight = 0f;
            if (state == null || !heightSamples.IsCreated || state.HeightmapResolution <= 1)
                return false;

            long expectedLength = (long)state.HeightmapResolution * state.HeightmapResolution;
            if (expectedLength <= 0L ||
                expectedLength > int.MaxValue ||
                heightSamples.Length < expectedLength ||
                !math.isfinite(worldX) ||
                !math.isfinite(worldZ) ||
                !IsFinite(state.TerrainPosition) ||
                !IsFinite(state.TerrainSize) ||
                state.TerrainSize.x <= 0f ||
                state.TerrainSize.z <= 0f)
            {
                return false;
            }

            float localX = worldX - state.TerrainPosition.x;
            float localZ = worldZ - state.TerrainPosition.z;
            if (localX < 0f || localZ < 0f || localX > state.TerrainSize.x || localZ > state.TerrainSize.z)
                return false;

            float normalizedX = math.saturate(localX * math.rcp(math.max(0.01f, state.TerrainSize.x)));
            float normalizedZ = math.saturate(localZ * math.rcp(math.max(0.01f, state.TerrainSize.z)));
            terrainHeight = state.TerrainPosition.y + SampleHeight(
                normalizedX,
                normalizedZ,
                new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z),
                state.HeightmapResolution,
                heightSamples);
            return true;
        }

        private bool SliceContainsDeepBiome(NativeChunkPool pool, int offset, int count)
        {
            int requiredPoolCount = offset + count;
            if (offset < 0 ||
                count <= 0 ||
                requiredPoolCount < offset)
            {
                return false;
            }

            if (!TryReadChunkPoolView(in pool, requiredPoolCount, out NativeChunkPoolView poolView))
                return false;

            int startIndex = math.clamp(offset, 0, poolView.BiomeLayers.Length);
            long requestedEnd = (long)offset + count;
            long clampedEnd = requestedEnd > poolView.BiomeLayers.Length ? poolView.BiomeLayers.Length : requestedEnd;
            if (clampedEnd < startIndex)
                return false;

            int end = (int)clampedEnd;
            for (int poolIndex = startIndex; poolIndex < end; poolIndex++)
            {
                if (poolView.BiomeLayers[poolIndex] >= (byte)VegetationBiomeLayer.ColonyGraveyard)
                    return true;
            }

            return false;
        }

        private static float ResolveAbyssalNavObstacleWeight(int semanticType, byte biomeLayer)
        {
            switch ((VegetationSemanticType)semanticType)
            {
                case VegetationSemanticType.ColonyCable:
                    return 0.45f;
                case VegetationSemanticType.ColonyHullPlating:
                case VegetationSemanticType.ColonySupportBeam:
                    return 0.75f;
                case VegetationSemanticType.DeadZoneMassiveStructure:
                    return 1f;
                default:
                    return biomeLayer >= (byte)VegetationBiomeLayer.ColonyGraveyard ? 0.2f : 0f;
            }
        }

        private void CacheChunkAbyssalNavPayload(ChunkKey key, ChunkAbyssalNavPayload payload)
        {
            if (payload.Count <= 0 || payload.Nodes == null)
            {
                RemoveChunkAbyssalNavPayload(key);
                return;
            }

            SetChunkAbyssalNavPayload(key, payload);
        }

        private void RemoveChunkAbyssalNavPayload(ChunkKey key)
        {
            int index = FindChunkAbyssalNavPayloadIndex(key);
            if (index < 0)
                return;

            ChunkAbyssalNavPayload payload = _chunkAbyssalNavPayloads[index];
            DisposeChunkAbyssalNavPayload(ref payload);
            RemoveChunkAbyssalNavPayloadAt(index);
        }

        private void DisposeAllChunkAbyssalNavPayloads()
        {
            for (int i = 0; i < _chunkAbyssalNavPayloadCount; i++)
            {
                ChunkAbyssalNavPayload payload = _chunkAbyssalNavPayloads[i];
                DisposeChunkAbyssalNavPayload(ref payload);
                _chunkAbyssalNavPayloads[i] = default;
                _chunkAbyssalNavPayloadKeys[i] = default;
            }

            _chunkAbyssalNavPayloadCount = 0;
        }

        private int FindChunkAbyssalNavPayloadIndex(ChunkKey key)
        {
            for (int i = 0; i < _chunkAbyssalNavPayloadCount; i++)
            {
                if (_chunkAbyssalNavPayloadKeys[i].Equals(key))
                    return i;
            }

            return -1;
        }

        private bool TryGetChunkAbyssalNavPayload(ChunkKey key, out ChunkAbyssalNavPayload payload)
        {
            int index = FindChunkAbyssalNavPayloadIndex(key);
            if (index >= 0)
            {
                payload = _chunkAbyssalNavPayloads[index];
                return true;
            }

            payload = default;
            return false;
        }

        private void SetChunkAbyssalNavPayload(ChunkKey key, ChunkAbyssalNavPayload payload)
        {
            int index = FindChunkAbyssalNavPayloadIndex(key);
            if (index >= 0)
            {
                ChunkAbyssalNavPayload oldPayload = _chunkAbyssalNavPayloads[index];
                if (!ReferenceEquals(oldPayload.Nodes, payload.Nodes))
                    DisposeChunkAbyssalNavPayload(ref oldPayload);

                _chunkAbyssalNavPayloads[index] = payload;
                return;
            }

            if (_chunkAbyssalNavPayloadCount >= _chunkAbyssalNavPayloads.Length)
            {
                RecordChunkQueueCapacityExceeded(_chunkAbyssalNavPayloads.Length, _chunkAbyssalNavPayloadCount);
                DisposeChunkAbyssalNavPayload(ref payload);
                return;
            }

            _chunkAbyssalNavPayloadKeys[_chunkAbyssalNavPayloadCount] = key;
            _chunkAbyssalNavPayloads[_chunkAbyssalNavPayloadCount] = payload;
            _chunkAbyssalNavPayloadCount++;
        }

        private void RemoveChunkAbyssalNavPayloadAt(int index)
        {
            if ((uint)index >= (uint)_chunkAbyssalNavPayloadCount)
                return;

            int last = _chunkAbyssalNavPayloadCount - 1;
            if (index != last)
            {
                _chunkAbyssalNavPayloadKeys[index] = _chunkAbyssalNavPayloadKeys[last];
                _chunkAbyssalNavPayloads[index] = _chunkAbyssalNavPayloads[last];
            }

            _chunkAbyssalNavPayloadKeys[last] = default;
            _chunkAbyssalNavPayloads[last] = default;
            _chunkAbyssalNavPayloadCount = last;
        }

        private static void DisposeChunkAbyssalNavPayload(ref ChunkAbyssalNavPayload payload)
        {
            payload.Nodes = null;
            payload.ConduitVectors = null;
            payload.ConduitStrengths = null;
            payload.NodeTypes = null;
            payload.Count = 0;
        }

        public bool TryGetHLODRegistryPayload(out NativeArray<HLODData>.ReadOnly entries, out int count)
        {
            count = _hlodRegistryCount;
            if (count <= 0)
            {
                entries = default;
                return false;
            }

            return TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.HlodRegistrySnapshotHandle,
                BufferID.VegetationHlodRegistrySnapshot,
                count,
                out entries);
        }

        /// <summary>
        /// Returns the current frustum-culled HLOD payload for distant rendering consumers.
        /// </summary>
        public bool TryGetVisibleHLODPayload(out NativeArray<HLODData>.ReadOnly entries, out int count)
        {
            count = _visibleHlodCount;
            if (count <= 0)
            {
                entries = default;
                return false;
            }

            return TryReadOnlyVegetationMemoryBuffer(
                in _nativeMemory.VisibleHlodSnapshotHandle,
                BufferID.VegetationVisibleHlodSnapshot,
                count,
                out entries);
        }

        /// <summary>
        /// Returns the current terrain-hole streaming payload for cave and interior streaming consumers.
        /// </summary>

        private void RebuildHLODRegistrySnapshot()
        {
            if (_hlodCullScheduled)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition viewerAup))
            {
                _hlodRegistryCount = 0;
                _visibleHlodCount = 0;
                return;
            }

            int registryCount = 0;
            if (megaWreckDefinitions != null)
            {
                for (int i = 0; i < megaWreckDefinitions.Length; i++)
                {
                    if (ShouldRegisterHLOD(megaWreckDefinitions[i].Center, megaWreckDefinitions[i].Size, in viewerAup))
                        registryCount++;
                }
            }

            for (int i = 0; i < _persistentArtificialStructureCount; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (ShouldRegisterHLOD(structure.Bounds.center, structure.Bounds.size, in viewerAup))
                    registryCount++;
            }

            _hlodRegistryCount = registryCount;
            if (registryCount <= 0)
            {
                _visibleHlodCount = 0;
                return;
            }

            EnsureHLODDataCapacity(ref _hlodRegistrySnapshot, registryCount);
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.HlodRegistrySnapshotHandle,
                    BufferID.VegetationHlodRegistrySnapshot,
                    registryCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault registryVault,
                    out NativeArray<HLODData> registrySnapshot))
            {
                _hlodRegistryCount = 0;
                _visibleHlodCount = 0;
                return;
            }

            int writeIndex = 0;
            try
            {
                if (megaWreckDefinitions != null)
                {
                    for (int i = 0; i < megaWreckDefinitions.Length; i++)
                    {
                        MegaWreckDefinition definition = megaWreckDefinitions[i];
                        if (!ShouldRegisterHLOD(definition.Center, definition.Size, in viewerAup))
                            continue;

                        HLODData entry = new HLODData
                        {
                            StructureId = definition.WreckId,
                            Type = StructureType.MegaWreck,
                            Center = definition.Center,
                            Size = definition.Size,
                            Fade01 = 0f
                        };
                        _hlodRegistrySnapshot[writeIndex] = entry;
                        registrySnapshot[writeIndex] = entry;
                        writeIndex++;
                    }
                }

                for (int i = 0; i < _persistentArtificialStructureCount; i++)
                {
                    PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                    if (!ShouldRegisterHLOD(structure.Bounds.center, structure.Bounds.size, in viewerAup))
                        continue;

                    HLODData entry = new HLODData
                    {
                        StructureId = structure.StructureId,
                        Type = structure.Type,
                        Center = structure.Bounds.center,
                        Size = structure.Bounds.size,
                        Fade01 = 0f
                    };
                    _hlodRegistrySnapshot[writeIndex] = entry;
                    registrySnapshot[writeIndex] = entry;
                    writeIndex++;
                }
            }
            finally
            {
                registryVault.ReleaseWriteLock(
                    in _nativeMemory.HlodRegistrySnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool ShouldRegisterHLOD(Vector3 center, Vector3 size, in AbsoluteUniversePosition viewerAup)
        {
            float largestAxis = math.max(size.x, math.max(size.y, size.z));
            if (largestAxis < hlodMinimumStructureSize)
                return false;

            if (!TryResolveAupFromRuntimeOrigin(center, out AbsoluteUniversePosition centerAup))
                return false;

            double distanceSq = AbsoluteUniversePosition.DistanceSq(in centerAup, in viewerAup);
            float maxDistance = hlodMaximumDistance + (largestAxis * 0.5f);
            double maxDistanceSq = maxDistance * maxDistance;
            return distanceSq <= maxDistanceSq;
        }

        private void ScheduleHLODVisibilityCullJob()
        {
            if (_hlodCullScheduled || _hlodRegistryCount <= 0)
                return;

            Camera activeViewCamera = RefreshActiveViewCameraCache();
            if (activeViewCamera == null)
            {
                _visibleHlodCount = 0;
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(activeViewCamera, _viewFrustumPlanes);
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 viewerPosition) ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition viewerAup))
            {
                _visibleHlodCount = 0;
                return;
            }

            float fullyVisibleDistance = math.max(hlodMinimumDistance, residentRadius + 1f);
            int visibleCount = 0;
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _hlodRegistrySnapshot[i];
                if (!TryResolveAupFromRuntimeOrigin(entry.Center, out AbsoluteUniversePosition entryAup))
                {
                    _visibleHlodCount = 0;
                    return;
                }

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in viewerAup, in entryAup);
                entry.Fade01 = ComputeHLODFadeSq01(distanceSq, residentRadius, fullyVisibleDistance);
                _hlodRegistrySnapshot[i] = entry;
                if (IsHLODVisible(entry, viewerPosition))
                    visibleCount++;
            }

            if (!TryMirrorHLODRegistrySnapshotToVault())
            {
                _visibleHlodCount = 0;
                return;
            }

            _visibleHlodCount = visibleCount;
            if (visibleCount <= 0)
                return;

            EnsureHLODDataCapacity(ref _visibleHlodSnapshot, visibleCount);
            int writeIndex = 0;
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _hlodRegistrySnapshot[i];
                if (!IsHLODVisible(entry, viewerPosition))
                    continue;

                _visibleHlodSnapshot[writeIndex] = entry;
                writeIndex++;
            }

            if (!TryMirrorVisibleHLODSnapshotToVault())
                _visibleHlodCount = 0;
        }

        private void CompleteHLODCullJob(bool forceComplete)
        {
            if (!_hlodCullScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _hlodCullHandle, forceComplete))
                return;

            _hlodCullScheduled = false;
            _hlodCullHandle = default;
        }

        private bool TryMirrorHLODRegistrySnapshotToVault()
        {
            if (_hlodRegistryCount <= 0)
                return false;

            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.HlodRegistrySnapshotHandle,
                    BufferID.VegetationHlodRegistrySnapshot,
                    _hlodRegistryCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault registryVault,
                    out NativeArray<HLODData> registrySnapshot))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < _hlodRegistryCount; i++)
                    registrySnapshot[i] = _hlodRegistrySnapshot[i];
            }
            finally
            {
                registryVault.ReleaseWriteLock(
                    in _nativeMemory.HlodRegistrySnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            return true;
        }

        private bool TryMirrorVisibleHLODSnapshotToVault()
        {
            if (_visibleHlodCount <= 0)
                return false;

            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.VisibleHlodSnapshotHandle,
                    BufferID.VegetationVisibleHlodSnapshot,
                    _visibleHlodCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault visibleVault,
                    out NativeArray<HLODData> visibleSnapshot))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < _visibleHlodCount; i++)
                    visibleSnapshot[i] = _visibleHlodSnapshot[i];
            }
            finally
            {
                visibleVault.ReleaseWriteLock(
                    in _nativeMemory.VisibleHlodSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            return true;
        }

        private bool IsHLODVisible(HLODData entry, Vector3 viewerPosition)
        {
            Vector3 delta = entry.Center - viewerPosition;
            float distanceSq = delta.sqrMagnitude;
            float minDistanceSq = residentRadius * residentRadius;
            float maxDistanceSq = hlodMaximumDistance * hlodMaximumDistance;
            if (distanceSq < minDistanceSq || distanceSq > maxDistanceSq)
                return false;

            Vector3 extents = new Vector3(
                math.max(0.5f, entry.Size.x * 0.5f + hlodFrustumPadding),
                math.max(0.5f, entry.Size.y * 0.5f + hlodFrustumPadding),
                math.max(0.5f, entry.Size.z * 0.5f + hlodFrustumPadding));
            Bounds bounds = new Bounds(entry.Center, extents * 2f);
            return GeometryUtility.TestPlanesAABB(_viewFrustumPlanes, bounds);
        }

        private static float ComputeHLODFadeSq01(double distanceSq, float lod0Radius, float fullyVisibleDistance)
        {
            float fadeStart = math.max(0f, lod0Radius);
            float fadeEnd = math.max(fadeStart + 1f, fullyVisibleDistance);
            double fadeStartSq = fadeStart * fadeStart;
            double fadeEndSq = fadeEnd * fadeEnd;
            double rangeSq = math.max(1d, fadeEndSq - fadeStartSq);
            return math.saturate((float)((distanceSq - fadeStartSq) * math.rcp(rangeSq)));
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float max = math.max(ax, math.max(ay, az));
            float min = math.min(ax, math.min(ay, az));
            float mid = ax + ay + az - max - min;
            return max + (mid * 0.375f) + (min * 0.25f);
        }

        private static Vector3 NormalizeVector3Fast(Vector3 vector, Vector3 fallback)
        {
            float magnitudeSq = vector.sqrMagnitude;
            if (magnitudeSq > 0.0001f && IsFinite(vector))
                return vector * math.rsqrt(magnitudeSq);

            float fallbackMagnitudeSq = fallback.sqrMagnitude;
            if (fallbackMagnitudeSq > 0.0001f && IsFinite(fallback))
                return fallback * math.rsqrt(fallbackMagnitudeSq);

            return Vector3.forward;
        }

        private static double ComputeAupDistanceSq(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePositionA, out AbsoluteUniversePosition a) ||
                !TryResolveAupFromRuntimeOrigin(runtimePositionB, out AbsoluteUniversePosition b))
            {
                return double.MaxValue;
            }

            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private AbsoluteUniversePosition ResolveViewerAup(Vector3 fallbackRuntimePosition)
        {
            return TryResolvePlayerAup(out AbsoluteUniversePosition viewerAup)
                ? viewerAup
                : TryResolveAupFromRuntimeOrigin(fallbackRuntimePosition, out AbsoluteUniversePosition fallbackAup)
                    ? fallbackAup
                    : default;
        }

        private int ResolveMaxAbyssalNavNodeCapacity()
        {
            return math.clamp(maxAbyssalNavNodeCapacity, 1, 32768);
        }

        private int ResolveMaxAbyssalPathWaypointCapacity()
        {
            return math.clamp(maxAbyssalPathWaypointCapacity, 2, 32768);
        }

        private void PreallocateAbyssalNavigationBuffers()
        {
            int fixedNodeCapacity = ResolveMaxAbyssalNavNodeCapacity();
            int fixedPathCapacity = ResolveMaxAbyssalPathWaypointCapacity();
            if (!EnsureAbyssalNavNodeListCapacity(fixedNodeCapacity))
                return;

            EnsureVector3Capacity(ref _abyssalNavNodeSnapshot, fixedNodeCapacity);
            EnsureVector3Capacity(ref _abyssalNavConduitVectorsSnapshot, fixedNodeCapacity);
            EnsureFloatCapacity(ref _abyssalNavConduitStrengthSnapshot, fixedNodeCapacity);
            EnsureByteCapacity(ref _abyssalNavNodeTypesSnapshot, fixedNodeCapacity);
            EnsureAbyssalNavSnapshotHandles(fixedNodeCapacity);
            EnsureAbyssalPathBuffers(fixedNodeCapacity);
            EnsureVector3Capacity(ref _abyssalPathSnapshot, fixedPathCapacity);
            EnsureAbyssalPathSnapshotHandle(fixedPathCapacity);
        }

        private bool EnsureAbyssalNavSnapshotHandles(int requiredCapacity)
        {
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null)
                return false;

            int capacity = math.max(1, requiredCapacity);
            _nativeMemory.AbyssalNavNodeSnapshotHandle =
                vault.EnsureGenerationHandle<Vector3>(
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);
            _nativeMemory.AbyssalNavConduitVectorsHandle =
                vault.EnsureGenerationHandle<Vector3>(
                    BufferID.VegetationAbyssalNavConduitVectors,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);
            _nativeMemory.AbyssalNavConduitStrengthsHandle =
                vault.EnsureGenerationHandle<float>(
                    BufferID.VegetationAbyssalNavConduitStrengths,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);
            _nativeMemory.AbyssalNavNodeTypesHandle =
                vault.EnsureGenerationHandle<byte>(
                    BufferID.VegetationAbyssalNavNodeTypes,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return IsExactVegetationMemoryHandle(
                       in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                       BufferID.VegetationAbyssalNavNodeSnapshot) &&
                   IsExactVegetationMemoryHandle(
                       in _nativeMemory.AbyssalNavConduitVectorsHandle,
                       BufferID.VegetationAbyssalNavConduitVectors) &&
                   IsExactVegetationMemoryHandle(
                       in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                       BufferID.VegetationAbyssalNavConduitStrengths) &&
                   IsExactVegetationMemoryHandle(
                       in _nativeMemory.AbyssalNavNodeTypesHandle,
                       BufferID.VegetationAbyssalNavNodeTypes);
        }

        private bool HasAbyssalNavSnapshotPayload(int requiredCount)
        {
            return requiredCount > 0 &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                       BufferID.VegetationAbyssalNavNodeSnapshot,
                       requiredCount,
                       out NativeArray<Vector3>.ReadOnly nodes) &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalNavNodeTypesHandle,
                       BufferID.VegetationAbyssalNavNodeTypes,
                       requiredCount,
                       out NativeArray<byte>.ReadOnly nodeTypes) &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalNavConduitVectorsHandle,
                       BufferID.VegetationAbyssalNavConduitVectors,
                       requiredCount,
                       out NativeArray<Vector3>.ReadOnly conduitVectors) &&
                   TryReadOnlyVegetationMemoryBuffer(
                       in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                       BufferID.VegetationAbyssalNavConduitStrengths,
                       requiredCount,
                       out NativeArray<float>.ReadOnly conduitStrengths) &&
                   nodes.Length >= requiredCount &&
                   nodeTypes.Length >= requiredCount &&
                   conduitVectors.Length >= requiredCount &&
                   conduitStrengths.Length >= requiredCount;
        }

        private bool TryCreateAbyssalNavJobSnapshot(
            int requiredCount,
            out NativeArray<Vector3> nodes,
            out NativeArray<byte> nodeTypes,
            out NativeArray<Vector3> conduitVectors,
            out NativeArray<float> conduitStrengths)
        {
            nodes = default;
            nodeTypes = default;
            conduitVectors = default;
            conduitStrengths = default;
            if (requiredCount <= 0 ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    requiredCount,
                    out NativeArray<Vector3>.ReadOnly sourceNodes) ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeTypesHandle,
                    BufferID.VegetationAbyssalNavNodeTypes,
                    requiredCount,
                    out NativeArray<byte>.ReadOnly sourceNodeTypes) ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavConduitVectorsHandle,
                    BufferID.VegetationAbyssalNavConduitVectors,
                    requiredCount,
                    out NativeArray<Vector3>.ReadOnly sourceConduitVectors) ||
                !TryReadOnlyVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                    BufferID.VegetationAbyssalNavConduitStrengths,
                    requiredCount,
                    out NativeArray<float>.ReadOnly sourceConduitStrengths))
            {
                return false;
            }

            nodes = H8Memory.Allocate<Vector3>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            nodeTypes = H8Memory.Allocate<byte>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            conduitVectors = H8Memory.Allocate<Vector3>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            conduitStrengths = H8Memory.Allocate<float>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            if (!nodes.IsCreated ||
                !nodeTypes.IsCreated ||
                !conduitVectors.IsCreated ||
                !conduitStrengths.IsCreated)
            {
                H8Memory.Release(ref nodes, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref nodeTypes, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref conduitVectors, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref conduitStrengths, VegetationMemorySovereigntyConstants.OwnerSystemId);
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    _nativeMemory.AbyssalNavNodeSnapshotHandle.Generation,
                    requiredCount,
                    0,
                    0,
                    0f,
                    VegetationMemoryTelemetryCode.VaultResolveFailed,
                    VegetationMemoryTelemetryPhase.SlowTick,
                    VegetationMemorySovereigntyConstants.FlagCapacity,
                    default);
                return false;
            }

            for (int i = 0; i < requiredCount; i++)
            {
                nodes[i] = sourceNodes[i];
                nodeTypes[i] = sourceNodeTypes[i];
                conduitVectors[i] = sourceConduitVectors[i];
                conduitStrengths[i] = sourceConduitStrengths[i];
            }

            return true;
        }

        private bool TryCreateAbyssalPathScratch(
            int requiredCount,
            out NativeArray<int> parents,
            out NativeArray<float> gScore,
            out NativeArray<float> fScore,
            out NativeArray<byte> closedFlags,
            out NativeArray<int> heapNodes,
            out NativeArray<int> heapPositions)
        {
            parents = default;
            gScore = default;
            fScore = default;
            closedFlags = default;
            heapNodes = default;
            heapPositions = default;
            if (requiredCount <= 0)
                return false;

            parents = H8Memory.Allocate<int>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            gScore = H8Memory.Allocate<float>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            fScore = H8Memory.Allocate<float>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            closedFlags = H8Memory.Allocate<byte>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            heapNodes = H8Memory.Allocate<int>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            heapPositions = H8Memory.Allocate<int>(
                requiredCount,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            if (parents.IsCreated &&
                gScore.IsCreated &&
                fScore.IsCreated &&
                closedFlags.IsCreated &&
                heapNodes.IsCreated &&
                heapPositions.IsCreated)
            {
                return true;
            }

            H8Memory.Release(ref parents, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref gScore, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref fScore, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref closedFlags, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref heapNodes, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref heapPositions, VegetationMemorySovereigntyConstants.OwnerSystemId);
            RecordVegetationMemoryTelemetry(
                BufferID.VegetationAbyssalPathSnapshot,
                _nativeMemory.AbyssalPathSnapshotHandle.Generation,
                requiredCount,
                0,
                0,
                0f,
                VegetationMemoryTelemetryCode.VaultResolveFailed,
                VegetationMemoryTelemetryPhase.SlowTick,
                VegetationMemorySovereigntyConstants.FlagCapacity,
                default);
            return false;
        }

        private bool TryMirrorAbyssalNavSnapshotsToVault(int requiredCount)
        {
            int safeCount = math.min(requiredCount, _abyssalNavNodeCount);
            if (safeCount <= 0 ||
                _abyssalNavNodeSnapshot == null ||
                _abyssalNavConduitVectorsSnapshot == null ||
                _abyssalNavConduitStrengthSnapshot == null ||
                _abyssalNavNodeTypesSnapshot == null)
            {
                return false;
            }

            safeCount = math.min(safeCount, _abyssalNavNodeSnapshot.Length);
            safeCount = math.min(safeCount, _abyssalNavConduitVectorsSnapshot.Length);
            safeCount = math.min(safeCount, _abyssalNavConduitStrengthSnapshot.Length);
            safeCount = math.min(safeCount, _abyssalNavNodeTypesSnapshot.Length);
            if (safeCount <= 0)
                return false;

            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    safeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault nodeVault,
                    out NativeArray<Vector3> nodes))
            {
                return false;
            }

            try
            {
                if (!TryAcquireVegetationMemoryBuffer(
                        ref _nativeMemory.AbyssalNavNodeTypesHandle,
                        BufferID.VegetationAbyssalNavNodeTypes,
                        safeCount,
                        NativeArrayOptions.UninitializedMemory,
                        out IDataVault typeVault,
                        out NativeArray<byte> nodeTypes))
                {
                    return false;
                }

                try
                {
                    if (!TryAcquireVegetationMemoryBuffer(
                            ref _nativeMemory.AbyssalNavConduitVectorsHandle,
                            BufferID.VegetationAbyssalNavConduitVectors,
                            safeCount,
                            NativeArrayOptions.UninitializedMemory,
                            out IDataVault vectorVault,
                            out NativeArray<Vector3> conduitVectors))
                    {
                        return false;
                    }

                    try
                    {
                        if (!TryAcquireVegetationMemoryBuffer(
                                ref _nativeMemory.AbyssalNavConduitStrengthsHandle,
                                BufferID.VegetationAbyssalNavConduitStrengths,
                                safeCount,
                                NativeArrayOptions.UninitializedMemory,
                                out IDataVault strengthVault,
                                out NativeArray<float> conduitStrengths))
                        {
                            return false;
                        }

                        try
                        {
                            for (int i = 0; i < safeCount; i++)
                            {
                                nodes[i] = _abyssalNavNodeSnapshot[i];
                                nodeTypes[i] = _abyssalNavNodeTypesSnapshot[i];
                                conduitVectors[i] = _abyssalNavConduitVectorsSnapshot[i];
                                conduitStrengths[i] = _abyssalNavConduitStrengthSnapshot[i];
                            }
                        }
                        finally
                        {
                            strengthVault.ReleaseWriteLock(
                                in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                                VegetationMemorySovereigntyConstants.OwnerSystemId);
                        }
                    }
                    finally
                    {
                        vectorVault.ReleaseWriteLock(
                            in _nativeMemory.AbyssalNavConduitVectorsHandle,
                            VegetationMemorySovereigntyConstants.OwnerSystemId);
                    }
                }
                finally
                {
                    typeVault.ReleaseWriteLock(
                        in _nativeMemory.AbyssalNavNodeTypesHandle,
                        VegetationMemorySovereigntyConstants.OwnerSystemId);
                }
            }
            finally
            {
                nodeVault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            return true;
        }

        private bool EnsureAbyssalPathBuffers(int nodeCount)
        {
            int fixedNodeCapacity = ResolveMaxAbyssalNavNodeCapacity();
            int fixedPathCapacity = ResolveMaxAbyssalPathWaypointCapacity();
            if (nodeCount > fixedNodeCapacity)
                return false;

            int requiredCount = math.max(1, nodeCount);
            int requiredPathCapacity = math.min(requiredCount + 2, MaxPathReconstructionIterations + 2);
            if (requiredPathCapacity > fixedPathCapacity)
                return false;

            EnsureVector3Capacity(ref _abyssalPathSnapshot, fixedPathCapacity);
            EnsureAbyssalPathSnapshotHandle(fixedPathCapacity);
            return true;
        }

        private void CompleteAbyssalPathJob(bool forceComplete)
        {
            if (!_abyssalPathScheduled)
                return;

            JobHandle handle = _abyssalPathHandle;
            if (!DispatcherJobSwap.TryComplete(ref handle, forceComplete))
            {
                _abyssalPathHandle = handle;
                _abyssalPathJob.Handle = handle;
                return;
            }

            AbyssalPathPendingJob pending = _abyssalPathJob;
            pending.Handle = handle;
            try
            {
                if (!pending.Cancelled)
                {
                    float funnelMs = ResolveAbyssalPathElapsedMs(Stopwatch.GetTimestamp() - pending.ScheduleTicks);
                    _lastAbyssalPathEndNode = pending.CanReuseLastTarget && !pending.ScheduledMacroVoxelRoute ? pending.EndNode : -1;
                    _lastAbyssalPathTargetPosition = pending.TargetPosition;
                    _hasLastAbyssalPathTarget = pending.CanReuseLastTarget && !pending.ScheduledMacroVoxelRoute;
                    CommitAbyssalPathResult(pending.RawPath, pending.ResultPath, funnelMs);
                }
            }
            finally
            {
                ReleaseAbyssalPathPendingJob(ref pending);
                _abyssalPathJob = default;
                _abyssalPathScheduled = false;
                _abyssalPathHandle = default;
            }
        }

        private static void ReleaseAbyssalPathPendingJob(ref AbyssalPathPendingJob pending)
        {
            H8Memory.Release(ref pending.DensityChunks, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref pending.DensityGrid, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref pending.ThreatAttractorGrid, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref pending.TerrainHoles, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref pending.ArtificialStructures, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref pending.NavPassabilityGrid, VegetationMemorySovereigntyConstants.OwnerSystemId);
            H8Memory.Release(ref pending.ThreatVoxelGrid, VegetationMemorySovereigntyConstants.OwnerSystemId);

            if (pending.RawPath.IsCreated)
                pending.RawPath.Dispose();
            if (pending.ResultPath.IsCreated)
                pending.ResultPath.Dispose();

            pending = default;
        }

        private bool CommitAbyssalPathResult(NativeList<Vector3> rawPath, NativeList<Vector3> resultPath, float funnelMs)
        {
            int rawPathCount = rawPath.IsCreated ? rawPath.Length : 0;
            int resultCount = resultPath.IsCreated ? resultPath.Length : 0;
            _abyssalPathCount = resultCount;
            if (resultCount <= 0)
            {
                RecordAbyssalPathTelemetry(funnelMs, rawPathCount, 0, default, default, true, rawPath);
                return false;
            }

            EnsureVector3Capacity(ref _abyssalPathSnapshot, resultCount);
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalPathSnapshotHandle,
                    BufferID.VegetationAbyssalPathSnapshot,
                    resultCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault pathVault,
                    out NativeArray<Vector3> pathSnapshot))
            {
                _abyssalPathCount = 0;
                RecordAbyssalPathTelemetry(funnelMs, rawPathCount, 0, default, default, true, rawPath);
                return false;
            }

            Vector3 start = default;
            Vector3 end = default;
            bool finite = true;
            try
            {
                int safeCount = math.min(resultCount, pathSnapshot.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    Vector3 waypoint = resultPath[i];
                    if (i == 0)
                        start = waypoint;
                    end = waypoint;
                    if (!IsFinite(waypoint))
                        finite = false;
                    _abyssalPathSnapshot[i] = waypoint;
                    pathSnapshot[i] = waypoint;
                }

                _abyssalPathCount = safeCount;
            }
            finally
            {
                pathVault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalPathSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }

            RecordAbyssalPathTelemetry(funnelMs, rawPathCount, _abyssalPathCount, start, end, finite, rawPath);
            return _abyssalPathCount > 0;
        }

        private bool EnsureAbyssalPathSnapshotHandle(int requiredCapacity)
        {
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null)
                return false;

            _nativeMemory.AbyssalPathSnapshotHandle =
                vault.EnsureGenerationHandle<Vector3>(
                    BufferID.VegetationAbyssalPathSnapshot,
                    math.max(1, requiredCapacity),
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return IsExactVegetationMemoryHandle(
                in _nativeMemory.AbyssalPathSnapshotHandle,
                BufferID.VegetationAbyssalPathSnapshot);
        }

        private bool EnsureAbyssalPathTelemetry()
        {
            IDataVault vault = CacheAbyssalPathTelemetryVaultCold();
            if (vault == null)
                return false;

            if (IsAbyssalPathTelemetryHandleCreated() &&
                vault.TryReadOnlyHandle(in _abyssalPathTelemetryHandle, out NativeArray<AbyssalPathTelemetryEntry>.ReadOnly telemetry) &&
                telemetry.IsCreated &&
                telemetry.Length >= AbyssalPathTelemetryFrameCount)
            {
                return true;
            }

            if (IsAbyssalPathTelemetryHandleCreated())
                vault.ReleaseBuffer(in _abyssalPathTelemetryHandle);

            _abyssalPathTelemetryHandle = vault.EnsureGenerationHandle<AbyssalPathTelemetryEntry>(
                AbyssalPathTelemetryBufferId,
                AbyssalPathTelemetryFrameCount,
                AbyssalPathTelemetryOwner,
                NativeArrayOptions.ClearMemory);
            _abyssalPathTelemetryCursor = 0;
            _abyssalPathTelemetryWrittenCount = 0;
            _abyssalPathTelemetrySequence = 0;
            _abyssalPathTelemetryDumpedForFault = false;
            return IsAbyssalPathTelemetryHandleCreated();
        }

        private IDataVault CacheAbyssalPathTelemetryVaultCold()
        {
            if (_abyssalPathTelemetryVault == null)
                _abyssalPathTelemetryVault = GlobalRegistry.DataVault;

            return _abyssalPathTelemetryVault;
        }

        private bool IsAbyssalPathTelemetryHandleCreated()
        {
            return _abyssalPathTelemetryHandle.BufferID != 0u &&
                   _abyssalPathTelemetryHandle.Generation != 0u;
        }

        private static float ResolveAbyssalPathElapsedMs(long elapsedTicks)
        {
            if (elapsedTicks <= 0)
                return 0f;

            return (float)(elapsedTicks * 1000.0 * math.rcp((double)Stopwatch.Frequency));
        }

        private void RecordAbyssalPathTelemetry(
            float funnelMs,
            int rawCount,
            int outputCount,
            Vector3 start,
            Vector3 end,
            bool finite,
            NativeList<Vector3> rawPath)
        {
            if (!EnsureAbyssalPathTelemetry())
                return;

            if (outputCount <= 0 &&
                TryResolveAbyssalRawPathTelemetry(rawPath, rawCount, out Vector3 rawStart, out Vector3 rawEnd, out bool rawFinite))
            {
                start = rawStart;
                end = rawEnd;
                finite = rawFinite;
            }

            uint flags = 0u;
            if (_lastAbyssalPathPortalLookAhead <= LowTierAbyssalPathPortalLookAhead)
                flags |= 1u;
            if (outputCount <= 0)
                flags |= 2u;
            if (!finite)
                flags |= 4u;

            IDataVault vault = _abyssalPathTelemetryVault;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _abyssalPathTelemetryHandle, AbyssalPathTelemetryOwner, out NativeArray<AbyssalPathTelemetryEntry> telemetry) ||
                !telemetry.IsCreated ||
                telemetry.Length < AbyssalPathTelemetryFrameCount)
            {
                return;
            }

            try
            {
                telemetry[_abyssalPathTelemetryCursor] = new AbyssalPathTelemetryEntry
                {
                    Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                    RawCount = rawCount,
                    OutputCount = outputCount,
                    PortalLookAhead = _lastAbyssalPathPortalLookAhead,
                    MaxDdaSamples = _lastAbyssalPathMaxSamples,
                    FunnelMs = funnelMs,
                    StartX = start.x,
                    StartY = start.y,
                    StartZ = start.z,
                    EndX = end.x,
                    EndY = end.y,
                    EndZ = end.z,
                    Flags = flags,
                    Sequence = _abyssalPathTelemetrySequence
                };

                _abyssalPathTelemetryCursor++;
                if (_abyssalPathTelemetryCursor >= AbyssalPathTelemetryFrameCount)
                    _abyssalPathTelemetryCursor = 0;
                if (_abyssalPathTelemetryWrittenCount < AbyssalPathTelemetryFrameCount)
                    _abyssalPathTelemetryWrittenCount++;
                _abyssalPathTelemetrySequence++;
            }
            finally
            {
                vault.ReleaseWriteLock(in _abyssalPathTelemetryHandle, AbyssalPathTelemetryOwner);
            }

            if (funnelMs > 0.1f)
                GlobalTelemetryBus.PublishPerformanceWarning(AbyssalPathOverBudgetHash, AbyssalPathTelemetryContextHash, funnelMs);

            if (!finite)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(AbyssalPathNanFaultHash, AbyssalPathTelemetryContextHash, flags);
                DumpAbyssalPathTelemetry(AbyssalPathNanFaultHash);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsInfinity(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return IsFiniteAup(in originAup);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
        }

        private static bool TryResolveAbyssalRawPathTelemetry(NativeList<Vector3> rawPath, int rawCount, out Vector3 start, out Vector3 end, out bool finite)
        {
            start = default;
            end = default;
            finite = true;
            if (!rawPath.IsCreated || rawCount <= 0)
                return false;

            int count = math.min(rawCount, rawPath.Length);
            if (count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                Vector3 waypoint = rawPath[i];
                if (i == 0)
                    start = waypoint;
                end = waypoint;
                if (!IsFinite(waypoint))
                    finite = false;
            }

            return true;
        }

        private void DumpAbyssalPathTelemetry(uint reasonHash)
        {
            IDataVault vault = _abyssalPathTelemetryVault;
            if (_abyssalPathTelemetryDumpedForFault ||
                vault == null ||
                !IsAbyssalPathTelemetryHandleCreated() ||
                !vault.TryReadOnlyHandle(in _abyssalPathTelemetryHandle, out NativeArray<AbyssalPathTelemetryEntry>.ReadOnly telemetry) ||
                !telemetry.IsCreated)
            {
                return;
            }

            _abyssalPathTelemetryDumpedForFault = true;
            try
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
                Directory.CreateDirectory(directory);
                string dumpPath = Path.Combine(directory, "Dump_AI_FUNNEL_NAV_POLISH.bin");
                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(reasonHash);
                    writer.Write(AbyssalPathTelemetryFrameCount);
                    writer.Write(_abyssalPathTelemetryCursor);
                    writer.Write(_abyssalPathTelemetrySequence);
                    int validEntryCount = math.clamp(_abyssalPathTelemetryWrittenCount, 0, AbyssalPathTelemetryFrameCount);
                    writer.Write(validEntryCount);
                    int firstEntryIndex = validEntryCount < AbyssalPathTelemetryFrameCount
                        ? 0
                        : _abyssalPathTelemetryCursor;
                    for (int dumpOffset = 0; dumpOffset < validEntryCount; dumpOffset++)
                    {
                        int entryIndex = firstEntryIndex + dumpOffset;
                        if (entryIndex >= AbyssalPathTelemetryFrameCount)
                            entryIndex -= AbyssalPathTelemetryFrameCount;

                        AbyssalPathTelemetryEntry entry = telemetry[entryIndex];
                        writer.Write(entry.Frame);
                        writer.Write(entry.RawCount);
                        writer.Write(entry.OutputCount);
                        writer.Write(entry.PortalLookAhead);
                        writer.Write(entry.MaxDdaSamples);
                        writer.Write(entry.FunnelMs);
                        writer.Write(entry.StartX);
                        writer.Write(entry.StartY);
                        writer.Write(entry.StartZ);
                        writer.Write(entry.EndX);
                        writer.Write(entry.EndY);
                        writer.Write(entry.EndZ);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                    }
                }
            }
            catch (IOException exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(AbyssalPathNanFaultHash, AbyssalPathTelemetryContextHash, exception.HResult);
            }
            catch (UnauthorizedAccessException exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(AbyssalPathNanFaultHash, AbyssalPathTelemetryContextHash, exception.HResult);
            }
        }

        private void DisposeAbyssalPathState()
        {
            if (_abyssalPathScheduled)
                CompleteAbyssalPathJob(forceComplete: true);
            ReleaseAbyssalPathPendingJob(ref _abyssalPathJob);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalPathSnapshotHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.PredatorFearNodesSnapshotHandle);
            IDataVault telemetryVault = _abyssalPathTelemetryVault;
            if (telemetryVault != null && IsAbyssalPathTelemetryHandleCreated())
                telemetryVault.ReleaseBuffer(in _abyssalPathTelemetryHandle);
            _abyssalPathTelemetryHandle = default;
            _abyssalPathHandle = default;
            _abyssalPathScheduled = false;
            _abyssalPathCount = 0;
            _lastAbyssalPathEndNode = -1;
            _abyssalPathTelemetryCursor = 0;
            _abyssalPathTelemetryWrittenCount = 0;
            _abyssalPathTelemetrySequence = 0;
            _lastAbyssalPathPortalLookAhead = 0;
            _lastAbyssalPathMaxSamples = 0;
            _abyssalPathTelemetryDumpedForFault = false;
            _hasLastAbyssalPathTarget = false;
        }

        private void DisposeHLODRegistryState()
        {
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.HlodRegistrySnapshotHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.VisibleHlodSnapshotHandle);
            _hlodCullHandle = default;
            _hlodCullScheduled = false;
            _hlodRegistryCount = 0;
            _visibleHlodCount = 0;
        }

        private void InvalidateAbyssalPathState()
        {
            if (_abyssalPathScheduled)
            {
                AbyssalPathPendingJob pending = _abyssalPathJob;
                pending.Cancelled = true;
                _abyssalPathJob = pending;
            }

            _abyssalPathCount = 0;
            _lastAbyssalPathEndNode = -1;
            _hasLastAbyssalPathTarget = false;
        }

        private bool EnsureAbyssalNavNodeListCapacity(int requiredCount)
        {
            int fixedCapacity = ResolveMaxAbyssalNavNodeCapacity();
            if (requiredCount > fixedCapacity)
                return false;

            return true;
        }

        private void ShiftChunkAbyssalNavPayloads(Vector3 offset)
        {
            if (_chunkAbyssalNavPayloadCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int keyIndex = 0; keyIndex < _chunkAbyssalNavPayloadCount; keyIndex++)
            {
                ChunkAbyssalNavPayload payload = _chunkAbyssalNavPayloads[keyIndex];
                if (payload.Count <= 0 || payload.Nodes == null)
                    continue;

                for (int nodeIndex = 0; nodeIndex < payload.Count; nodeIndex++)
                    payload.Nodes[nodeIndex] += offset;

                _chunkAbyssalNavPayloads[keyIndex] = payload;
            }
        }

        private void ShiftAbyssalNavSnapshots(Vector3 offset)
        {
            if (_abyssalNavNodeCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                _abyssalNavNodeSnapshot[i] += offset;
            }

            TryMirrorAbyssalNavSnapshotsToVault(_abyssalNavNodeCount);
        }

        private void ShiftHLODRegistrySnapshots(Vector3 offset)
        {
            if (offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _hlodRegistrySnapshot[i];
                entry.Center += offset;
                _hlodRegistrySnapshot[i] = entry;
            }

            if (_hlodRegistryCount > 0 && !TryMirrorHLODRegistrySnapshotToVault())
                return;

            for (int i = 0; i < _visibleHlodCount; i++)
            {
                HLODData entry = _visibleHlodSnapshot[i];
                entry.Center += offset;
                _visibleHlodSnapshot[i] = entry;
            }

            if (_visibleHlodCount <= 0)
                return;

            TryMirrorVisibleHLODSnapshotToVault();
        }

        private void ShiftAbyssalPathSnapshot(Vector3 offset)
        {
            if (_abyssalPathCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _abyssalPathCount; i++)
                _abyssalPathSnapshot[i] += offset;

            TryMirrorAbyssalPathSnapshotToVault();
        }

        private bool TryMirrorAbyssalPathSnapshotToVault()
        {
            if (_abyssalPathCount <= 0)
                return false;

            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalPathSnapshotHandle,
                    BufferID.VegetationAbyssalPathSnapshot,
                    _abyssalPathCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<Vector3> pathSnapshot))
            {
                return false;
            }

            try
            {
                int safeCount = math.min(_abyssalPathCount, pathSnapshot.Length);
                for (int i = 0; i < safeCount; i++)
                    pathSnapshot[i] = _abyssalPathSnapshot[i];

                _abyssalPathCount = safeCount;
                return safeCount > 0;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalPathSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private void BuildFlowFieldNavSupportGrid(NativeArray<float> navSupportGrid, Vector3 gridCenter)
        {
            if (!navSupportGrid.IsCreated ||
                navSupportGrid.Length < _ecosystemThreatGridCellCount ||
                _ecosystemThreatGridResolution <= 0 ||
                _abyssalNavNodeCount <= 0 ||
                threatGridCellSize <= 0f ||
                !math.isfinite(threatGridCellSize) ||
                !IsFinite(gridCenter))
            {
                return;
            }

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int stencilRadius = math.max(0, flowFieldNavStencilRadiusCells);
            float inverseThreatGridCellSize = math.rcp(math.max(0.0001f, threatGridCellSize));
            float supportRadius = math.max(1f, stencilRadius + 0.25f);
            float inverseSupportRadiusSq = math.rcp(math.max(1f, supportRadius * supportRadius));
            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                Vector3 node = _abyssalNavNodeSnapshot[i];
                if (!IsFinite(node))
                    continue;

                int centerX = (int)math.round((node.x - gridCenter.x) * inverseThreatGridCellSize) + halfExtent;
                int centerZ = (int)math.round((node.z - gridCenter.z) * inverseThreatGridCellSize) + halfExtent;
                if (centerX < 0 || centerZ < 0 || centerX >= _ecosystemThreatGridResolution || centerZ >= _ecosystemThreatGridResolution)
                    continue;

                for (int offsetZ = -stencilRadius; offsetZ <= stencilRadius; offsetZ++)
                {
                    int cellZ = centerZ + offsetZ;
                    if (cellZ < 0 || cellZ >= _ecosystemThreatGridResolution)
                        continue;

                    for (int offsetX = -stencilRadius; offsetX <= stencilRadius; offsetX++)
                    {
                        int cellX = centerX + offsetX;
                        if (cellX < 0 || cellX >= _ecosystemThreatGridResolution)
                            continue;

                        float distanceSq = (offsetX * offsetX) + (offsetZ * offsetZ);
                        float support01 = 1f - math.saturate(distanceSq * inverseSupportRadiusSq);
                        int index = (cellZ * _ecosystemThreatGridResolution) + cellX;
                        float clampedSupport = math.saturate(support01);
                        if (navSupportGrid[index] < clampedSupport)
                            navSupportGrid[index] = clampedSupport;
                    }
                }
            }
        }

        private int FindNearestAbyssalNavNodeIndex(Vector3 position)
        {
            if (_abyssalNavNodeCount <= 0 ||
                _abyssalNavNodeSnapshot == null ||
                _abyssalNavNodeSnapshot.Length <= 0 ||
                !IsFinite(position))
            {
                return -1;
            }

            int safeNodeCount = math.min(_abyssalNavNodeCount, _abyssalNavNodeSnapshot.Length);
            if (safeNodeCount <= 0)
                return -1;

            int bestIndex = -1;
            float bestDistanceSq = float.PositiveInfinity;
            for (int i = 0; i < safeNodeCount; i++)
            {
                Vector3 candidate = _abyssalNavNodeSnapshot[i];
                if (!IsFinite(candidate))
                    continue;

                float distanceSq = (candidate - position).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                bestIndex = i;
            }

            return bestIndex;
        }

    }
}
