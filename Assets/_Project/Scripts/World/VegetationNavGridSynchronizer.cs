using System;
using System.Diagnostics;
using System.IO;
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

            int navPassabilityLength = math.max(0, navPassabilityGrid.Length);
            if (!TryAcquireAbyssalPathStagingBuffer(
                    _abyssalNavNodeCount,
                    fixedPathCapacity,
                    navPassabilityLength,
                    out IDataVault pathStagingVault,
                    out NativeArray<AbyssalPathStagingPoint> pathStaging))
            {
                RecordVegetationMemoryTelemetry(
                    BufferID.VegetationAbyssalPathStagingPacked,
                    _nativeMemory.AbyssalPathStagingHandle.Generation,
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

            int navPassabilityLengthForJob = 0;
            bool scheduled = false;
            NativeArray<VegetationDensityChunkRecord> densityChunksForJob = default;
            NativeArray<float3> densityGridForJob = default;
            NativeArray<float2> threatAttractorGridForJob = default;
            NativeArray<TerrainHoleRecord> terrainHolesForJob = default;
            NativeArray<ArtificialStructureRecord> artificialStructuresForJob = default;
            NativeArray<byte> threatVoxelGridForSmoothing = default;
            IDataVault pathReadPinVault = null;
            uint pathReadPinMask = 0u;
            try
            {
                ResetAbyssalPathStaging(pathStaging);
                if (navPassabilityLength > 0 &&
                    TryCopyNavPassabilityToPathStaging(navPassabilityGrid, navPassabilityLength, pathStaging))
                {
                    navPassabilityLengthForJob = navPassabilityLength;
                }

                JobHandle pathSourceHandle = default;
                bool scheduledMacroVoxelRoute = false;
                if (startUsesVoxel &&
                    endUsesVoxel &&
                    VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc(startProbe, endProbe, _abyssalPathSnapshot, out int macroWaypointCount) &&
                    TryCopyManagedAbyssalPathToNative(_abyssalPathSnapshot, macroWaypointCount, fixedPathCapacity, pathStaging))
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
                    int predatorFearNodeCountForJob = 0;
                    int predatorFearNodeCount = math.max(0, _predatorFearNodeCount);
                    if (predatorFearNodeCount > 0)
                    {
                        if (TryLockAbyssalPathReadBuffer(
                                BufferID.VegetationPredatorFearNodeSnapshot,
                                AbyssalPathPinPredatorFear,
                                ref pathReadPinVault,
                                ref pathReadPinMask) &&
                            TryReadVegetationMemoryBuffer(
                                in _nativeMemory.PredatorFearNodesSnapshotHandle,
                                BufferID.VegetationPredatorFearNodeSnapshot,
                                predatorFearNodeCount,
                                out predatorFearNodesForJob))
                        {
                            predatorFearNodeCountForJob = math.min(predatorFearNodeCount, predatorFearNodesForJob.Length);
                        }
                        else
                        {
                            ReleaseAbyssalPathReadPin(
                                pathReadPinVault,
                                AbyssalPathPinPredatorFear,
                                BufferID.VegetationPredatorFearNodeSnapshot,
                                ref pathReadPinMask);
                            RecordVegetationMemoryTelemetry(
                                BufferID.VegetationPredatorFearNodeSnapshot,
                                _nativeMemory.PredatorFearNodesSnapshotHandle.Generation,
                                predatorFearNodeCount,
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
                            ref pathReadPinVault,
                            ref pathReadPinMask,
                            out navNodesForJob,
                            out navNodeTypesForJob,
                            out conduitVectorsForJob,
                            out conduitStrengthsForJob))
                    {
                        return false;
                    }

                    NativeArray<float> threatGridForJob = default;
                    NativeArray<byte> threatVoxelGridForJob = default;
                    if (!TryLockAbyssalPathReadBuffer(
                            BufferID.VegetationEcosystemThreatGrid,
                            AbyssalPathPinThreatGrid,
                            ref pathReadPinVault,
                            ref pathReadPinMask) ||
                        !TryLockAbyssalPathReadBuffer(
                            BufferID.VegetationEcosystemThreatVoxel,
                            AbyssalPathPinThreatVoxel,
                            ref pathReadPinVault,
                            ref pathReadPinMask) ||
                        !TryReadVegetationMemoryBuffer(
                            in _nativeMemory.EcosystemThreatGridHandle,
                            BufferID.VegetationEcosystemThreatGrid,
                            _ecosystemThreatGridCellCount,
                            out threatGridForJob) ||
                        !TryReadVegetationMemoryBuffer(
                            in _nativeMemory.EcosystemThreatVoxelHandle,
                            BufferID.VegetationEcosystemThreatVoxel,
                            _ecosystemThreatVoxelCellCount,
                            out threatVoxelGridForJob))
                    {
                        return false;
                    }

                    threatVoxelGridForSmoothing = threatVoxelGridForJob;

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
                        PathStaging = pathStaging,
                        PathCapacity = fixedPathCapacity,
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
                }

                int smoothingPortalLookAhead = ResolveAbyssalPathPortalLookAhead();
                int smoothingDdaSampleCap = ResolveAbyssalPathDdaSampleCap(abyssalPathSmoothingMaxSamples);
                EnsureAbyssalPathTelemetry();
                _lastAbyssalPathPortalLookAhead = smoothingPortalLookAhead;
                _lastAbyssalPathMaxSamples = smoothingDdaSampleCap;
                int densityChunkCountForJob = 0;
                if (TryPinDensityQueryJobSnapshot(
                        true,
                        false,
                        AbyssalPathPinDensityChunks,
                        AbyssalPathPinDensityGrid,
                        AbyssalPathPinThreatAttractorGrid,
                        ref pathReadPinVault,
                        ref pathReadPinMask,
                        out densityChunksForJob,
                        out densityGridForJob,
                        out threatAttractorGridForJob,
                        out int densityChunkCount,
                        out _) &&
                    densityChunksForJob.IsCreated &&
                    densityGridForJob.IsCreated)
                {
                    densityChunkCountForJob = math.min(densityChunkCount, densityChunksForJob.Length);
                }

                int terrainHoleCountForJob = 0;
                int terrainHoleCount = math.max(0, _terrainHoleCount);
                if (terrainHoleCount > 0)
                {
                    if (TryLockAbyssalPathReadBuffer(
                            BufferID.VegetationTerrainHoleRecords,
                            AbyssalPathPinTerrainHoles,
                            ref pathReadPinVault,
                            ref pathReadPinMask) &&
                        TryReadVegetationMemoryBuffer(
                            in _nativeMemory.TerrainHoleRecordsHandle,
                            BufferID.VegetationTerrainHoleRecords,
                            terrainHoleCount,
                            out terrainHolesForJob))
                    {
                        terrainHoleCountForJob = math.min(terrainHoleCount, terrainHolesForJob.Length);
                    }
                    else
                    {
                        ReleaseAbyssalPathReadPin(
                            pathReadPinVault,
                            AbyssalPathPinTerrainHoles,
                            BufferID.VegetationTerrainHoleRecords,
                            ref pathReadPinMask);
                    }
                }

                int artificialStructureCountForJob = 0;
                int artificialStructureCount = math.max(0, _artificialStructureCount);
                if (artificialStructureCount > 0 &&
                    TryLockAbyssalPathReadBuffer(
                        BufferID.VegetationArtificialStructureRecords,
                        AbyssalPathPinArtificialStructures,
                        ref pathReadPinVault,
                        ref pathReadPinMask))
                {
                    if (TryReadVegetationMemoryBuffer(
                            in _nativeMemory.ArtificialStructureRecordsHandle,
                            BufferID.VegetationArtificialStructureRecords,
                            artificialStructureCount,
                            out artificialStructuresForJob))
                    {
                        artificialStructureCountForJob = math.min(artificialStructureCount, artificialStructuresForJob.Length);
                    }
                    else
                    {
                        ReleaseAbyssalPathReadPin(
                            pathReadPinVault,
                            AbyssalPathPinArtificialStructures,
                            BufferID.VegetationArtificialStructureRecords,
                            ref pathReadPinMask);
                    }
                }

                if (!threatVoxelGridForSmoothing.IsCreated &&
                    TryLockAbyssalPathReadBuffer(
                        BufferID.VegetationEcosystemThreatVoxel,
                        AbyssalPathPinThreatVoxel,
                        ref pathReadPinVault,
                        ref pathReadPinMask))
                {
                    if (TryReadVegetationMemoryBuffer(
                            in _nativeMemory.EcosystemThreatVoxelHandle,
                            BufferID.VegetationEcosystemThreatVoxel,
                            _ecosystemThreatVoxelCellCount,
                            out NativeArray<byte> threatVoxelGridForSmoothingSource))
                    {
                        threatVoxelGridForSmoothing = threatVoxelGridForSmoothingSource;
                    }
                    else
                    {
                        ReleaseAbyssalPathReadPin(
                            pathReadPinVault,
                            AbyssalPathPinThreatVoxel,
                            BufferID.VegetationEcosystemThreatVoxel,
                            ref pathReadPinMask);
                    }
                }

                var smoothingJob = new StringPullPathJob
                {
                    PathStaging = pathStaging,
                    DensityChunks = densityChunksForJob,
                    DensityGrid = densityGridForJob,
                    ChunkCount = densityChunkCountForJob,
                    TerrainHoles = terrainHolesForJob,
                    TerrainHoleCount = terrainHoleCountForJob,
                    ArtificialStructures = artificialStructuresForJob,
                    ArtificialStructureCount = artificialStructureCountForJob,
                    ArtificialStructureHash = default,
                    NavPassabilityLength = navPassabilityLengthForJob,
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
                    PathCapacity = fixedPathCapacity
                };

                long scheduleStartTicks = Stopwatch.GetTimestamp();
                JobHandle smoothingHandle = smoothingJob.Schedule(pathSourceHandle);
                _abyssalPathJob = new AbyssalPathPendingJob
                {
                    PathStaging = pathStaging,
                    DensityChunks = densityChunksForJob,
                    DensityGrid = densityGridForJob,
                    ThreatAttractorGrid = threatAttractorGridForJob,
                    TerrainHoles = terrainHolesForJob,
                    ArtificialStructures = artificialStructuresForJob,
                    ThreatVoxelGrid = threatVoxelGridForSmoothing,
                    PathStagingVault = pathStagingVault,
                    ReadPinVault = pathReadPinVault,
                    ReadPinMask = pathReadPinMask,
                    PathCapacity = fixedPathCapacity,
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

                pathStaging = default;
                pathStagingVault = null;
                pathReadPinVault = null;
                pathReadPinMask = 0u;
                densityChunksForJob = default;
                densityGridForJob = default;
                threatAttractorGridForJob = default;
                terrainHolesForJob = default;
                artificialStructuresForJob = default;
                threatVoxelGridForSmoothing = default;
                return true;
            }
            finally
            {
                ReleaseAbyssalPathStagingWriteLock(pathStagingVault);
                ReleaseAbyssalPathReadPins(pathReadPinVault, pathReadPinMask);
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

        private static bool TryCopyNavPassabilityToPathStaging(
            NativeArray<byte>.ReadOnly source,
            int requiredCount,
            NativeArray<AbyssalPathStagingPoint> staging)
        {
            if (requiredCount <= 0)
                return true;

            if (!source.IsCreated ||
                !staging.IsCreated ||
                source.Length < requiredCount ||
                staging.Length < requiredCount)
            {
                return false;
            }

            for (int i = 0; i < requiredCount; i++)
            {
                AbyssalPathStagingPoint point = staging[i];
                point.ScratchFlags = source[i];
                staging[i] = point;
            }

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

            return WriteExternalSurfaceFlowVectorsOneLock(flowVectors, count) &&
                   WriteExternalSurfaceFlowDirectionsOneLock(flowVectors, count);
        }

        private bool WriteExternalSurfaceFlowVectorsOneLock(NativeArray<Vector3> flowVectors, int count)
        {
            if (!TryAcquireAggregateWriteBuffer(
                    ref _surfaceAggregateFrontBuffers.FlowVectorsHandle,
                    count,
                    out IDataVault vault,
                    out NativeArray<Vector3> surfaceFlowVectors))
            {
                return false;
            }

            try
            {
                int safeCount = math.min(count, surfaceFlowVectors.Length);
                for (int i = 0; i < safeCount; i++)
                    surfaceFlowVectors[i] = flowVectors[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _surfaceAggregateFrontBuffers.FlowVectorsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteExternalSurfaceFlowDirectionsOneLock(NativeArray<Vector3> flowVectors, int count)
        {
            if (!TryAcquireAggregateWriteBuffer(
                    ref _surfaceAggregateFrontBuffers.FlowDirectionsHandle,
                    count,
                    out IDataVault vault,
                    out NativeArray<Vector2> flowDirections))
            {
                return false;
            }

            try
            {
                int safeCount = math.min(count, flowDirections.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    Vector3 flowVector = flowVectors[i];
                    flowDirections[i] = NormalizeFlowDirection(new Vector2(flowVector.x, flowVector.z));
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _surfaceAggregateFrontBuffers.FlowDirectionsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
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

            if (registryCount <= 0)
            {
                _hlodRegistryCount = 0;
                _visibleHlodCount = 0;
                return;
            }

            EnsureHLODDataCapacity(ref _hlodRegistrySnapshot, registryCount);
            int writeIndex = 0;
            if (megaWreckDefinitions != null)
            {
                for (int i = 0; i < megaWreckDefinitions.Length; i++)
                {
                    MegaWreckDefinition definition = megaWreckDefinitions[i];
                    if (!ShouldRegisterHLOD(definition.Center, definition.Size, in viewerAup))
                        continue;

                    _hlodRegistrySnapshot[writeIndex] = new HLODData
                    {
                        StructureId = definition.WreckId,
                        Type = StructureType.MegaWreck,
                        Center = definition.Center,
                        Size = definition.Size,
                        Fade01 = 0f
                    };
                    writeIndex++;
                }
            }

            for (int i = 0; i < _persistentArtificialStructureCount; i++)
            {
                PersistentArtificialStructureRecord structure = _persistentArtificialStructures[i];
                if (!ShouldRegisterHLOD(structure.Bounds.center, structure.Bounds.size, in viewerAup))
                    continue;

                _hlodRegistrySnapshot[writeIndex] = new HLODData
                {
                    StructureId = structure.StructureId,
                    Type = structure.Type,
                    Center = structure.Bounds.center,
                    Size = structure.Bounds.size,
                    Fade01 = 0f
                };
                writeIndex++;
            }

            _hlodRegistryCount = writeIndex;
            if (writeIndex <= 0 || !TryMirrorHLODRegistrySnapshotToVault())
            {
                _hlodRegistryCount = 0;
                _visibleHlodCount = 0;
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
            EnsureAbyssalPathTelemetryDumpPayloadCold();
            EnsureAbyssalPathTelemetry();
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
            ref IDataVault readPinVault,
            ref uint readPinMask,
            out NativeArray<Vector3> nodes,
            out NativeArray<byte> nodeTypes,
            out NativeArray<Vector3> conduitVectors,
            out NativeArray<float> conduitStrengths)
        {
            nodes = default;
            nodeTypes = default;
            conduitVectors = default;
            conduitStrengths = default;
            if (requiredCount <= 0)
                return false;

            if (!TryLockAbyssalPathReadBuffer(
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    AbyssalPathPinNavNodes,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryLockAbyssalPathReadBuffer(
                    BufferID.VegetationAbyssalNavNodeTypes,
                    AbyssalPathPinNavNodeTypes,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryLockAbyssalPathReadBuffer(
                    BufferID.VegetationAbyssalNavConduitVectors,
                    AbyssalPathPinConduitVectors,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryLockAbyssalPathReadBuffer(
                    BufferID.VegetationAbyssalNavConduitStrengths,
                    AbyssalPathPinConduitStrengths,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    requiredCount,
                    out nodes) ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavNodeTypesHandle,
                    BufferID.VegetationAbyssalNavNodeTypes,
                    requiredCount,
                    out nodeTypes) ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavConduitVectorsHandle,
                    BufferID.VegetationAbyssalNavConduitVectors,
                    requiredCount,
                    out conduitVectors) ||
                !TryReadVegetationMemoryBuffer(
                    in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                    BufferID.VegetationAbyssalNavConduitStrengths,
                    requiredCount,
                    out conduitStrengths) ||
                nodes.Length < requiredCount ||
                nodeTypes.Length < requiredCount ||
                conduitVectors.Length < requiredCount ||
                conduitStrengths.Length < requiredCount)
            {
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
                nodes = default;
                nodeTypes = default;
                conduitVectors = default;
                conduitStrengths = default;
                return false;
            }

            return true;
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

            return CopyAbyssalNavNodesToVault(safeCount) &&
                   CopyAbyssalNavNodeTypesToVault(safeCount) &&
                   CopyAbyssalNavConduitVectorsToVault(safeCount) &&
                   CopyAbyssalNavConduitStrengthsToVault(safeCount);
        }

        private bool CopyAbyssalNavNodesToVault(int safeCount)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    BufferID.VegetationAbyssalNavNodeSnapshot,
                    safeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<Vector3> nodes))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < safeCount; i++)
                    nodes[i] = _abyssalNavNodeSnapshot[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalNavNodeSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyAbyssalNavNodeTypesToVault(int safeCount)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalNavNodeTypesHandle,
                    BufferID.VegetationAbyssalNavNodeTypes,
                    safeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<byte> nodeTypes))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < safeCount; i++)
                    nodeTypes[i] = _abyssalNavNodeTypesSnapshot[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalNavNodeTypesHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyAbyssalNavConduitVectorsToVault(int safeCount)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalNavConduitVectorsHandle,
                    BufferID.VegetationAbyssalNavConduitVectors,
                    safeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<Vector3> conduitVectors))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < safeCount; i++)
                    conduitVectors[i] = _abyssalNavConduitVectorsSnapshot[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalNavConduitVectorsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyAbyssalNavConduitStrengthsToVault(int safeCount)
        {
            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalNavConduitStrengthsHandle,
                    BufferID.VegetationAbyssalNavConduitStrengths,
                    safeCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault vault,
                    out NativeArray<float> conduitStrengths))
            {
                return false;
            }

            try
            {
                for (int i = 0; i < safeCount; i++)
                    conduitStrengths[i] = _abyssalNavConduitStrengthSnapshot[i];

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalNavConduitStrengthsHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
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
            EnsureAbyssalPathStagingHandles(nodeCount, fixedPathCapacity, 0);
            return true;
        }

        private bool EnsureAbyssalPathStagingHandles(int nodeCapacity, int fixedPathCapacity, int extraCapacity)
        {
            IDataVault vault = _vegetationMemoryVault;
            if (vault == null || vault.IsCompactionFenceActive)
                return false;

            int stagingPointSize = UnsafeUtility.SizeOf<AbyssalPathStagingPoint>();
            if (stagingPointSize != 64 || (stagingPointSize & 7) != 0)
                return false;

            int capacity = math.max(math.max(2, fixedPathCapacity), math.max(math.max(1, nodeCapacity), math.max(1, extraCapacity)));
            _nativeMemory.AbyssalPathStagingHandle =
                vault.EnsureGenerationHandle<AbyssalPathStagingPoint>(
                    BufferID.VegetationAbyssalPathStagingPacked,
                    capacity,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    NativeArrayOptions.UninitializedMemory);

            return IsExactVegetationMemoryHandle(
                in _nativeMemory.AbyssalPathStagingHandle,
                BufferID.VegetationAbyssalPathStagingPacked);
        }

        private bool TryAcquireAbyssalPathStagingBuffer(
            int nodeCapacity,
            int fixedPathCapacity,
            int extraCapacity,
            out IDataVault vault,
            out NativeArray<AbyssalPathStagingPoint> pathStaging)
        {
            vault = null;
            pathStaging = default;
            if (!EnsureAbyssalPathStagingHandles(nodeCapacity, fixedPathCapacity, extraCapacity))
                return false;

            return TryAcquireVegetationMemoryBuffer(
                ref _nativeMemory.AbyssalPathStagingHandle,
                BufferID.VegetationAbyssalPathStagingPacked,
                math.max(math.max(2, fixedPathCapacity), math.max(math.max(1, nodeCapacity), math.max(1, extraCapacity))),
                NativeArrayOptions.UninitializedMemory,
                out vault,
                out pathStaging);
        }

        private void ReleaseAbyssalPathStagingWriteLock(IDataVault vault)
        {
            if (vault == null || !IsExactVegetationMemoryHandle(in _nativeMemory.AbyssalPathStagingHandle, BufferID.VegetationAbyssalPathStagingPacked))
                return;

            vault.ReleaseWriteLock(in _nativeMemory.AbyssalPathStagingHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private bool TryLockAbyssalPathReadBuffer(
            BufferID bufferId,
            uint pinBit,
            ref IDataVault readPinVault,
            ref uint readPinMask)
        {
            return TryPinVegetationReadBuffer(bufferId, pinBit, ref readPinVault, ref readPinMask);
        }

        private static void ReleaseAbyssalPathReadPins(IDataVault vault, uint pinMask)
        {
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinPredatorFear, BufferID.VegetationPredatorFearNodeSnapshot);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinNavNodes, BufferID.VegetationAbyssalNavNodeSnapshot);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinNavNodeTypes, BufferID.VegetationAbyssalNavNodeTypes);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinConduitVectors, BufferID.VegetationAbyssalNavConduitVectors);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinConduitStrengths, BufferID.VegetationAbyssalNavConduitStrengths);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinThreatGrid, BufferID.VegetationEcosystemThreatGrid);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinThreatVoxel, BufferID.VegetationEcosystemThreatVoxel);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinArtificialStructures, BufferID.VegetationArtificialStructureRecords);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinTerrainHoles, BufferID.VegetationTerrainHoleRecords);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinDensityChunks, BufferID.VegetationDensityQueryChunks);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinDensityGrid, BufferID.VegetationDensityQueryGrid);
            TryUnlockAbyssalPathReadPin(vault, pinMask, AbyssalPathPinThreatAttractorGrid, BufferID.VegetationThreatAttractorGrid);
        }

        private static void ReleaseAbyssalPathReadPin(IDataVault vault, uint pinBit, BufferID bufferId, ref uint pinMask)
        {
            if (vault == null || (pinMask & pinBit) == 0u)
                return;

            vault.TryUnlockBuffer(bufferId, VegetationMemorySovereigntyConstants.OwnerSystemId);
            pinMask &= ~pinBit;
        }

        private static void TryUnlockAbyssalPathReadPin(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, VegetationMemorySovereigntyConstants.OwnerSystemId);
        }

        private static void ResetAbyssalPathStaging(NativeArray<AbyssalPathStagingPoint> staging)
        {
            if (!staging.IsCreated || staging.Length <= 0)
                return;

            AbyssalPathStagingPoint meta = staging[0];
            meta.RawCount = 0;
            meta.ResultCount = 0;
            meta.RawFlags = 0;
            meta.ResultFlags = 0;
            staging[0] = meta;
        }

        private static int ResolveAbyssalPathCapacity(NativeArray<AbyssalPathStagingPoint> staging, int pathCapacity)
        {
            if (!staging.IsCreated || staging.Length <= 0)
                return 0;

            int requestedCapacity = pathCapacity > 0 ? pathCapacity : staging.Length;
            return math.clamp(requestedCapacity, 0, staging.Length);
        }

        private static int ReadAbyssalRawPathCount(NativeArray<AbyssalPathStagingPoint> staging, int pathCapacity)
        {
            if (!staging.IsCreated || staging.Length <= 0)
                return 0;

            return math.clamp(staging[0].RawCount, 0, ResolveAbyssalPathCapacity(staging, pathCapacity));
        }

        private static int ReadAbyssalResultPathCount(NativeArray<AbyssalPathStagingPoint> staging, int pathCapacity)
        {
            if (!staging.IsCreated || staging.Length <= 0)
                return 0;

            return math.clamp(staging[0].ResultCount, 0, ResolveAbyssalPathCapacity(staging, pathCapacity));
        }

        private static uint ReadAbyssalPathFlags(NativeArray<AbyssalPathStagingPoint> staging)
        {
            if (!staging.IsCreated || staging.Length <= 0)
                return 0u;

            AbyssalPathStagingPoint meta = staging[0];
            return unchecked((uint)(meta.RawFlags | meta.ResultFlags));
        }

        private static bool TryCopyManagedAbyssalPathToNative(
            Vector3[] source,
            int sourceCount,
            int pathCapacity,
            NativeArray<AbyssalPathStagingPoint> staging)
        {
            ResetAbyssalPathStaging(staging);
            int safePathCapacity = ResolveAbyssalPathCapacity(staging, pathCapacity);
            if (source == null ||
                sourceCount <= 0 ||
                !staging.IsCreated ||
                safePathCapacity <= 0)
            {
                return false;
            }

            if (sourceCount > source.Length || sourceCount > safePathCapacity)
            {
                AbyssalPathStagingPoint overflowMeta = staging[0];
                overflowMeta.RawFlags |= AbyssalPathOverflowFlag;
                staging[0] = overflowMeta;
                return false;
            }

            for (int i = 0; i < sourceCount; i++)
            {
                AbyssalPathStagingPoint entry = staging[i];
                entry.Raw = source[i];
                staging[i] = entry;
            }

            AbyssalPathStagingPoint meta = staging[0];
            meta.RawCount = sourceCount;
            staging[0] = meta;
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
                    AbyssalPathCommitRecord commit = StageAbyssalPathResultForCommit(pending.PathStaging, pending.PathCapacity, funnelMs);
                    ReleaseAbyssalPathStagingWriteLock(pending.PathStagingVault);
                    pending.PathStagingVault = null;
                    pending.PathStaging = default;
                    if (commit.OutputCount > 0 && !PublishAbyssalPathSnapshotFromStagedResult(commit.OutputCount))
                    {
                        _abyssalPathCount = 0;
                        commit.OutputCount = 0;
                        commit.Flags |= 8u;
                    }

                    RecordAbyssalPathTelemetry(in commit);
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

        private void ReleaseAbyssalPathPendingJob(ref AbyssalPathPendingJob pending)
        {
            ReleaseAbyssalPathStagingWriteLock(pending.PathStagingVault);
            ReleaseAbyssalPathReadPins(pending.ReadPinVault, pending.ReadPinMask);

            pending = default;
        }

        private AbyssalPathCommitRecord StageAbyssalPathResultForCommit(
            NativeArray<AbyssalPathStagingPoint> pathStaging,
            int pathCapacity,
            float funnelMs)
        {
            AbyssalPathCommitRecord commit = new AbyssalPathCommitRecord
            {
                FunnelMs = funnelMs,
                Finite = true
            };
            int rawPathCount = ReadAbyssalRawPathCount(pathStaging, pathCapacity);
            int resultCount = ReadAbyssalResultPathCount(pathStaging, pathCapacity);
            commit.RawCount = rawPathCount;
            commit.Flags = ReadAbyssalPathFlags(pathStaging);
            if (resultCount <= 0)
            {
                TryResolveAbyssalRawPathTelemetry(pathStaging, pathCapacity, rawPathCount, out commit.Start, out commit.End, out commit.Finite);
                return commit;
            }

            if (_abyssalPathSnapshot == null || _abyssalPathSnapshot.Length < resultCount)
            {
                commit.Flags |= 8u;
                TryResolveAbyssalRawPathTelemetry(pathStaging, pathCapacity, rawPathCount, out commit.Start, out commit.End, out commit.Finite);
                return commit;
            }

            int safeCount = math.min(resultCount, _abyssalPathSnapshot.Length);
            if (!pathStaging.IsCreated || safeCount <= 0)
            {
                TryResolveAbyssalRawPathTelemetry(pathStaging, pathCapacity, rawPathCount, out commit.Start, out commit.End, out commit.Finite);
                return commit;
            }

            for (int i = 0; i < safeCount; i++)
            {
                Vector3 waypoint = pathStaging[i].Result;
                if (i == 0)
                    commit.Start = waypoint;
                commit.End = waypoint;
                if (!IsFinite(waypoint))
                    commit.Finite = false;
                _abyssalPathSnapshot[i] = waypoint;
            }

            commit.OutputCount = safeCount;
            return commit;
        }

        private bool PublishAbyssalPathSnapshotFromStagedResult(int resultCount)
        {
            if (resultCount <= 0 || _abyssalPathSnapshot == null || _abyssalPathSnapshot.Length < resultCount)
                return false;

            if (!TryAcquireVegetationMemoryBuffer(
                    ref _nativeMemory.AbyssalPathSnapshotHandle,
                    BufferID.VegetationAbyssalPathSnapshot,
                    resultCount,
                    NativeArrayOptions.UninitializedMemory,
                    out IDataVault pathVault,
                    out NativeArray<Vector3> pathSnapshot))
            {
                return false;
            }

            try
            {
                int safeCount = math.min(resultCount, pathSnapshot.Length);
                for (int i = 0; i < safeCount; i++)
                    pathSnapshot[i] = _abyssalPathSnapshot[i];

                _abyssalPathCount = safeCount;
                return safeCount > 0;
            }
            finally
            {
                pathVault.ReleaseWriteLock(
                    in _nativeMemory.AbyssalPathSnapshotHandle,
                    VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
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
                return IsAbyssalPathTelemetryDumpPayloadReady();
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
            return IsAbyssalPathTelemetryHandleCreated() &&
                   IsAbyssalPathTelemetryDumpPayloadReady();
        }

        private bool EnsureAbyssalPathTelemetryDumpPayloadCold()
        {
            if (_abyssalPathTelemetryDumpPayload.IsCreated &&
                _abyssalPathTelemetryDumpPayload.Length >= AbyssalPathTelemetryDumpPayloadBytes)
            {
                return true;
            }

            H8Memory.Release(ref _abyssalPathTelemetryDumpPayload, AbyssalPathTelemetryOwner);
            if (_abyssalPathTelemetryDumpPayload.IsCreated)
                return false;

            NativeArray<byte> replacement = default;
            try
            {
                replacement = H8Memory.Allocate<byte>(
                    AbyssalPathTelemetryDumpPayloadBytes,
                    AbyssalPathTelemetryOwner,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (!replacement.IsCreated ||
                    replacement.Length < AbyssalPathTelemetryDumpPayloadBytes)
                {
                    H8Memory.Release(ref replacement, AbyssalPathTelemetryOwner);
                    return false;
                }

                _abyssalPathTelemetryDumpPayload = replacement;
                replacement = default;
            }
            catch
            {
                return false;
            }
            finally
            {
                H8Memory.Release(ref replacement, AbyssalPathTelemetryOwner);
            }

            return _abyssalPathTelemetryDumpPayload.IsCreated &&
                   _abyssalPathTelemetryDumpPayload.Length >= AbyssalPathTelemetryDumpPayloadBytes;
        }

        private bool IsAbyssalPathTelemetryDumpPayloadReady()
        {
            return _abyssalPathTelemetryDumpPayload.IsCreated &&
                   _abyssalPathTelemetryDumpPayload.Length >= AbyssalPathTelemetryDumpPayloadBytes;
        }

        private IDataVault CacheAbyssalPathTelemetryVaultCold()
        {
            if (_abyssalPathTelemetryVault == null)
                _abyssalPathTelemetryVault = _vegetationMemoryVault;

            return _abyssalPathTelemetryVault;
        }

        private void RebindAbyssalPathTelemetryVaultCold(IDataVault currentVault)
        {
            IDataVault previousVault = _abyssalPathTelemetryVault;
            if (previousVault != null && !ReferenceEquals(previousVault, currentVault))
                ReleaseAbyssalPathTelemetryResources(previousVault);

            _abyssalPathTelemetryVault = currentVault;
            if (currentVault != null && IsAbyssalPathTelemetryDumpPayloadReady())
                EnsureAbyssalPathTelemetry();
        }

        private void ReleaseAbyssalPathTelemetryResources(IDataVault vault)
        {
            if (vault != null && IsAbyssalPathTelemetryHandleCreated())
                vault.ReleaseBuffer(in _abyssalPathTelemetryHandle);

            _abyssalPathTelemetryHandle = default;
            _abyssalPathTelemetryCursor = 0;
            _abyssalPathTelemetryWrittenCount = 0;
            _abyssalPathTelemetrySequence = 0;
            _abyssalPathTelemetryDumpedForFault = false;
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

        private void RecordAbyssalPathTelemetry(in AbyssalPathCommitRecord commit)
        {
            if (!EnsureAbyssalPathTelemetry())
                return;

            uint flags = 0u;
            if (commit.OutputCount <= 0)
                flags |= 2u;
            if (!commit.Finite)
                flags |= 4u;
            flags |= commit.Flags;

            IDataVault vault = _abyssalPathTelemetryVault;
            if (vault == null)
                return;

            if (!vault.TryAcquireWriteLock(in _abyssalPathTelemetryHandle, AbyssalPathTelemetryOwner, out NativeArray<AbyssalPathTelemetryEntry> telemetry))
                return;

            try
            {
                if (!telemetry.IsCreated || telemetry.Length < AbyssalPathTelemetryFrameCount)
                    return;

                telemetry[_abyssalPathTelemetryCursor] = new AbyssalPathTelemetryEntry
                {
                    Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                    RawCount = commit.RawCount,
                    OutputCount = commit.OutputCount,
                    PortalLookAhead = _lastAbyssalPathPortalLookAhead,
                    MaxDdaSamples = _lastAbyssalPathMaxSamples,
                    FunnelMs = commit.FunnelMs,
                    StartX = commit.Start.x,
                    StartY = commit.Start.y,
                    StartZ = commit.Start.z,
                    EndX = commit.End.x,
                    EndY = commit.End.y,
                    EndZ = commit.End.z,
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

            if (commit.FunnelMs > 0.1f)
                GlobalTelemetryBus.PublishPerformanceWarning(AbyssalPathOverBudgetHash, AbyssalPathTelemetryContextHash, commit.FunnelMs);

            if (!commit.Finite)
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

        private static bool TryResolveAbyssalRawPathTelemetry(
            NativeArray<AbyssalPathStagingPoint> pathStaging,
            int pathCapacity,
            int rawCount,
            out Vector3 start,
            out Vector3 end,
            out bool finite)
        {
            start = default;
            end = default;
            finite = true;
            if (!pathStaging.IsCreated || rawCount <= 0)
                return false;

            int count = math.min(rawCount, ReadAbyssalRawPathCount(pathStaging, pathCapacity));
            if (count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                Vector3 waypoint = pathStaging[i].Raw;
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

            int validEntryCount = math.clamp(_abyssalPathTelemetryWrittenCount, 0, AbyssalPathTelemetryFrameCount);
            int totalBytes = AbyssalPathTelemetryDumpHeaderBytes + validEntryCount * AbyssalPathTelemetryDumpRowBytes;
            if (!_abyssalPathTelemetryDumpPayload.IsCreated ||
                _abyssalPathTelemetryDumpPayload.Length < AbyssalPathTelemetryDumpPayloadBytes ||
                totalBytes > _abyssalPathTelemetryDumpPayload.Length)
            {
                return;
            }

            NativeArray<byte> payload = _abyssalPathTelemetryDumpPayload;
            try
            {
                WriteUInt32LittleEndian(payload, 0, reasonHash);
                WriteInt32LittleEndian(payload, 4, AbyssalPathTelemetryFrameCount);
                WriteInt32LittleEndian(payload, 8, _abyssalPathTelemetryCursor);
                WriteUInt32LittleEndian(payload, 12, _abyssalPathTelemetrySequence);
                WriteInt32LittleEndian(payload, 16, validEntryCount);
                int firstEntryIndex = validEntryCount < AbyssalPathTelemetryFrameCount
                    ? 0
                    : _abyssalPathTelemetryCursor;
                for (int dumpOffset = 0; dumpOffset < validEntryCount; dumpOffset++)
                {
                    int entryIndex = firstEntryIndex + dumpOffset;
                    if (entryIndex >= AbyssalPathTelemetryFrameCount)
                        entryIndex -= AbyssalPathTelemetryFrameCount;

                    AbyssalPathTelemetryEntry entry = telemetry[entryIndex];
                    int offset = AbyssalPathTelemetryDumpHeaderBytes + dumpOffset * AbyssalPathTelemetryDumpRowBytes;
                    WriteInt32LittleEndian(payload, offset, entry.Frame);
                    WriteInt32LittleEndian(payload, offset + 4, entry.RawCount);
                    WriteInt32LittleEndian(payload, offset + 8, entry.OutputCount);
                    WriteInt32LittleEndian(payload, offset + 12, entry.PortalLookAhead);
                    WriteInt32LittleEndian(payload, offset + 16, entry.MaxDdaSamples);
                    WriteFloat32LittleEndian(payload, offset + 20, entry.FunnelMs);
                    WriteFloat32LittleEndian(payload, offset + 24, entry.StartX);
                    WriteFloat32LittleEndian(payload, offset + 28, entry.StartY);
                    WriteFloat32LittleEndian(payload, offset + 32, entry.StartZ);
                    WriteFloat32LittleEndian(payload, offset + 36, entry.EndX);
                    WriteFloat32LittleEndian(payload, offset + 40, entry.EndY);
                    WriteFloat32LittleEndian(payload, offset + 44, entry.EndZ);
                    WriteUInt32LittleEndian(payload, offset + 48, entry.Flags);
                    WriteUInt32LittleEndian(payload, offset + 52, entry.Sequence);
                }

                _abyssalPathTelemetryDumpedForFault = NativeFaultDumpWriter.TryWriteAll(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs", "Dump_AI_FUNNEL_NAV_POLISH.bin")),
                    payload,
                    totalBytes);
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

        private static void WriteFloat32LittleEndian(NativeArray<byte> payload, int offset, float value)
        {
            WriteUInt32LittleEndian(payload, offset, math.asuint(value));
        }

        private static void WriteInt32LittleEndian(NativeArray<byte> payload, int offset, int value)
        {
            WriteUInt32LittleEndian(payload, offset, unchecked((uint)value));
        }

        private static void WriteUInt32LittleEndian(NativeArray<byte> payload, int offset, uint value)
        {
            payload[offset] = (byte)value;
            payload[offset + 1] = (byte)(value >> 8);
            payload[offset + 2] = (byte)(value >> 16);
            payload[offset + 3] = (byte)(value >> 24);
        }

        private void DisposeAbyssalPathState()
        {
            if (_abyssalPathScheduled)
                CompleteAbyssalPathJob(forceComplete: true);
            ReleaseAbyssalPathPendingJob(ref _abyssalPathJob);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalPathSnapshotHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.AbyssalPathStagingHandle);
            ReleaseVegetationMemoryBuffer(ref _nativeMemory.PredatorFearNodesSnapshotHandle);
            ReleaseAbyssalPathTelemetryResources(_abyssalPathTelemetryVault);
            H8Memory.Release(ref _abyssalPathTelemetryDumpPayload, AbyssalPathTelemetryOwner);
            if (_abyssalPathTelemetryDumpPayload.IsCreated)
                return;

            _abyssalPathTelemetryVault = null;
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
