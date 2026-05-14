using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Burst;
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
        public bool TryGetLatestAbyssalPathPayload(out NativeArray<Vector3> path, out int count)
        {
            CompleteAbyssalPathJob(forceComplete: false);
            path = _nativeMemory.AbyssalPathSnapshotNative;
            count = _abyssalPathCount;
            return count > 0 && path.IsCreated;
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
        /// Schedules a bounded native abyssal A* solve between the nearest safe nav nodes to the provided world positions.
        /// </summary>
        public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, out JobHandle handle)
        {
            return TryScheduleAbyssalPath(startPosition, endPosition, 0, out handle);
        }

        /// <summary>
        /// Schedules a bounded native abyssal A* solve between the nearest safe nav nodes to the provided world positions.
        /// Species-aware predator fear penalties are applied when <paramref name="traversalSpeciesId"/> is non-zero.
        /// </summary>
        public bool TryScheduleAbyssalPath(Vector3 startPosition, Vector3 endPosition, int traversalSpeciesId, out JobHandle handle)
        {
            handle = default;
            CompleteAbyssalPathJob(forceComplete: false);
            if (!IsFinite(startPosition) || !IsFinite(endPosition))
                return false;

            if (_abyssalPathScheduled ||
                _abyssalNavNodeCount <= 0 ||
                !_nativeMemory.AbyssalNavNodeSnapshotNative.IsCreated)
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

            if (_nativeMemory.AbyssalPathRawResultNative.IsCreated)
                _nativeMemory.AbyssalPathRawResultNative.Clear();
            if (_nativeMemory.AbyssalPathResultNative.IsCreated)
                _nativeMemory.AbyssalPathResultNative.Clear();
            _abyssalPathCount = 0;

            JobHandle pathSourceHandle = default;
            bool scheduledMacroVoxelRoute = false;
            if (startUsesVoxel &&
                endUsesVoxel &&
                VoxelDynamicNavGridRuntime.TryBuildMacroPortalRoute(startProbe, endProbe, _nativeMemory.AbyssalPathRawResultNative))
            {
                scheduledMacroVoxelRoute = true;
            }

            if (!scheduledMacroVoxelRoute)
            {
                var astarJob = new NativeAStarJob
                {
                    Nodes = _nativeMemory.AbyssalNavNodeSnapshotNative,
                    NodeTypes = _nativeMemory.AbyssalNavNodeTypesSnapshotNative,
                    ConduitVectors = _nativeMemory.AbyssalNavConduitVectorsSnapshotNative,
                    ConduitStrengths = _nativeMemory.AbyssalNavConduitStrengthSnapshotNative,
                    ThreatGrid = _nativeMemory.EcosystemThreatGridCurrentNative,
                    ThreatVoxelGrid = _nativeMemory.EcosystemThreatVoxelCurrentNative,
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    ThreatVoxelDimensions = new int3(_ecosystemThreatGridResolution, _ecosystemThreatGridResolutionY, _ecosystemThreatGridResolution),
                    ThreatVoxelOrigin = new float3(_ecosystemThreatVoxelOrigin.x, _ecosystemThreatVoxelOrigin.y, _ecosystemThreatVoxelOrigin.z),
                    ThreatVoxelCellSize = new float3(threatGridCellSize, thermalGridVerticalCellSize, threatGridCellSize),
                    WaterLevel = waterLevel,
                    Parents = _nativeMemory.AbyssalPathParentsNative,
                    GScore = _nativeMemory.AbyssalPathGScoreNative,
                    FScore = _nativeMemory.AbyssalPathFScoreNative,
                    ClosedFlags = _nativeMemory.AbyssalPathClosedFlagsNative,
                    HeapNodes = _nativeMemory.AbyssalPathHeapNodesNative,
                    HeapPositions = _nativeMemory.AbyssalPathHeapPositionsNative,
                    Path = _nativeMemory.AbyssalPathRawResultNative,
                    PredatorFearNodes = _nativeMemory.PredatorFearNodesSnapshotNative,
                    PredatorFearNodeCount = _predatorFearNodeCount,
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

            NativeArray<byte> navPassabilityGrid = default;
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

            if (!navPassabilityGrid.IsCreated &&
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

            HectonQualityTier scalabilityTier = GlobalRegistry.ScalabilityTier;
            int smoothingPortalLookAhead = ResolveAbyssalPathPortalLookAhead(scalabilityTier);
            int smoothingDdaSampleCap = ResolveAbyssalPathDdaSampleCap(scalabilityTier, abyssalPathSmoothingMaxSamples);
            EnsureAbyssalPathTelemetry();
            _lastAbyssalPathPortalLookAhead = smoothingPortalLookAhead;
            _lastAbyssalPathMaxSamples = smoothingDdaSampleCap;
            var smoothingJob = new StringPullPathJob
            {
                InputPath = _nativeMemory.AbyssalPathRawResultNative.AsDeferredJobArray(),
                DensityChunks = _nativeMemory.DensityQueryChunksNative,
                DensityGrid = _nativeMemory.DensityQueryGridNative,
                ChunkCount = _densityQueryChunkCount,
                TerrainHoles = _nativeMemory.TerrainHoleRecordsNative,
                TerrainHoleCount = _terrainHoleCount,
                ArtificialStructures = _nativeMemory.ArtificialStructureRecordsNative,
                ArtificialStructureHash = _nativeMemory.ArtificialStructureHashFrontNative,
                NavPassabilityGrid = navPassabilityGrid,
                ThreatVoxelGrid = _nativeMemory.EcosystemThreatVoxelCurrentNative,
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
                OutputPath = _nativeMemory.AbyssalPathResultNative
            };

            _abyssalPathHandle = smoothingJob.Schedule(pathSourceHandle);
            _abyssalPathScheduled = true;
            _lastAbyssalPathEndNode = canReuseLastAbyssalTarget && !scheduledMacroVoxelRoute ? endNode : -1;
            _lastAbyssalPathTargetPosition = resolvedEndPosition;
            _hasLastAbyssalPathTarget = canReuseLastAbyssalTarget && !scheduledMacroVoxelRoute;
            handle = _abyssalPathHandle;
            return true;
        }

        private static int ResolveAbyssalPathPortalLookAhead(HectonQualityTier tier)
        {
            switch (tier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return HighTierAbyssalPathPortalLookAhead;
                case HectonQualityTier.Mid:
                    return MidTierAbyssalPathPortalLookAhead;
                default:
                    return LowTierAbyssalPathPortalLookAhead;
            }
        }

        private static int ResolveAbyssalPathDdaSampleCap(HectonQualityTier tier, int configuredSampleCap)
        {
            int safeCap = math.clamp(configuredSampleCap, 1, MaxThreatDdaSteps);
            switch (tier)
            {
                case HectonQualityTier.High:
                case HectonQualityTier.Ultra:
                    return safeCap;
                case HectonQualityTier.Mid:
                    return math.min(safeCap, MidTierAbyssalPathDdaSamples);
                default:
                    return math.min(safeCap, LowTierAbyssalPathDdaSamples);
            }
        }

        /// <summary>
        /// Applies an arbitrary caller-owned surface flow-vector field to the active surface payload without binding to a specific ocean backend.
        /// </summary>
        public bool TryApplyExternalSurfaceFlowVectorField(NativeArray<Vector3> flowVectors, int count)
        {
            if (count <= 0 ||
                count != _surfaceFrontCount ||
                !flowVectors.IsCreated ||
                !_surfaceAggregateFrontBuffers.FlowDirections.IsCreated ||
                !_surfaceAggregateFrontBuffers.FlowVectors.IsCreated)
            {
                return false;
            }

            int safeCount = math.min(
                count,
                math.min(_surfaceAggregateFrontBuffers.FlowDirections.Length, _surfaceAggregateFrontBuffers.FlowVectors.Length));
            for (int i = 0; i < safeCount; i++)
            {
                Vector3 flowVector = flowVectors[i];
                Vector2 flowDirection = NormalizeFlowDirection(new Vector2(flowVector.x, flowVector.z));
                _surfaceAggregateFrontBuffers.FlowVectors[i] = flowVector;
                _surfaceAggregateFrontBuffers.FlowDirections[i] = flowDirection;
            }

            return true;
        }

        /// <summary>
        /// Marks streamed chunks intersecting the requested zone as corrupted and invalidates their payloads for async rebuild.
        /// </summary>

        private ChunkAbyssalNavPayload BuildChunkAbyssalNavPayload(ChunkKey key, ChunkBuildJobState jobState, ChunkPayload payload)
        {
            ChunkAbyssalNavPayload navPayload = default;
            if (jobState == null || !payload.HasUnderwater)
                return navPayload;

            if (!_tileStates.TryGetValue(jobState.TileKey, out TileRuntimeState state) ||
                state == null ||
                !TryGetActiveTileCache(state, out _, out _, out NativeArray<ushort> heightSamples) ||
                !SliceContainsDeepBiome(ResolveChunkPool(isSurface: false, payload), payload.UnderwaterOffset, payload.UnderwaterCount))
            {
                return navPayload;
            }

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

            NativeArray<Vector3> nodes = default;
            NativeArray<Vector3> conduitVectors = default;
            NativeArray<float> conduitStrengths = default;
            NativeArray<byte> nodeTypes = default;
            bool hasExistingPayload = _chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload existingPayload);
            bool reusedExistingPayload = hasExistingPayload &&
                existingPayload.Nodes.IsCreated &&
                existingPayload.Nodes.Length >= maxNodeCount;
            if (reusedExistingPayload)
            {
                nodes = existingPayload.Nodes;
                if (existingPayload.ConduitVectors.IsCreated && existingPayload.ConduitVectors.Length >= maxNodeCount)
                {
                    conduitVectors = existingPayload.ConduitVectors;
                }
                else
                {
                    DisposeNativeArray(ref existingPayload.ConduitVectors);
                }

                if (existingPayload.ConduitStrengths.IsCreated && existingPayload.ConduitStrengths.Length >= maxNodeCount)
                {
                    conduitStrengths = existingPayload.ConduitStrengths;
                }
                else
                {
                    DisposeNativeArray(ref existingPayload.ConduitStrengths);
                }

                if (existingPayload.NodeTypes.IsCreated && existingPayload.NodeTypes.Length >= maxNodeCount)
                {
                    nodeTypes = existingPayload.NodeTypes;
                }
                else
                {
                    DisposeNativeArray(ref existingPayload.NodeTypes);
                }
            }
            else if (hasExistingPayload && existingPayload.Nodes.IsCreated)
            {
                DisposeChunkAbyssalNavPayload(ref existingPayload);
            }

            EnsureInactiveNativeCapacity(ref nodes, maxNodeCount);

            EnsureInactiveNativeCapacity(ref conduitVectors, maxNodeCount);

            EnsureInactiveNativeCapacity(ref conduitStrengths, maxNodeCount);

            EnsureInactiveNativeCapacity(ref nodeTypes, maxNodeCount);

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
            {
                if (!reusedExistingPayload)
                {
                    DisposeNativeArray(ref nodes);
                    DisposeNativeArray(ref conduitVectors);
                    DisposeNativeArray(ref conduitStrengths);
                    DisposeNativeArray(ref nodeTypes);
                }

                return navPayload;
            }

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
            if (!underwaterPool.Matrices.IsCreated ||
                !underwaterPool.BiomeLayers.IsCreated ||
                !underwaterPool.SemanticTypes.IsCreated)
            {
                return false;
            }

            int availableLength = math.min(
                underwaterPool.Matrices.Length,
                math.min(underwaterPool.BiomeLayers.Length, underwaterPool.SemanticTypes.Length));
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
                Vector3 position = ResolveRuntimePosition(underwaterPool.Matrices[poolIndex]);
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

                byte biomeLayer = underwaterPool.BiomeLayers[poolIndex];
                int semanticType = underwaterPool.SemanticTypes[poolIndex];
                float semanticWeight = ResolveAbyssalNavObstacleWeight(semanticType, biomeLayer);
                if (semanticWeight <= 0f)
                    continue;

                obstacleWeight += semanticWeight;
                if (biomeLayer >= (byte)VegetationBiomeLayer.ColonyGraveyard)
                    deepAffinity += semanticWeight;

                Vector3 flowVector = underwaterPool.FlowVectors.IsCreated && poolIndex < underwaterPool.FlowVectors.Length
                    ? underwaterPool.FlowVectors[poolIndex]
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
            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
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

        private static bool SliceContainsDeepBiome(NativeChunkPool pool, int offset, int count)
        {
            if (!pool.BiomeLayers.IsCreated || count <= 0)
                return false;

            int startIndex = math.clamp(offset, 0, pool.BiomeLayers.Length);
            long requestedEnd = (long)offset + count;
            long clampedEnd = requestedEnd > pool.BiomeLayers.Length ? pool.BiomeLayers.Length : requestedEnd;
            if (clampedEnd < startIndex)
                return false;

            int end = (int)clampedEnd;
            for (int poolIndex = startIndex; poolIndex < end; poolIndex++)
            {
                if (pool.BiomeLayers[poolIndex] >= (byte)VegetationBiomeLayer.ColonyGraveyard)
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
            if (payload.Count <= 0 || !payload.Nodes.IsCreated)
            {
                RemoveChunkAbyssalNavPayload(key);
                return;
            }

            _chunkAbyssalNavPayloads[key] = payload;
        }

        private void RemoveChunkAbyssalNavPayload(ChunkKey key)
        {
            if (_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload))
                DisposeChunkAbyssalNavPayload(ref payload);

            _chunkAbyssalNavPayloads.Remove(key);
        }

        private void DisposeAllChunkAbyssalNavPayloads()
        {
            if (_chunkAbyssalNavPayloads.Count <= 0)
                return;

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkAbyssalNavPayload>.Enumerator enumerator = _chunkAbyssalNavPayloads.GetEnumerator();
            while (enumerator.MoveNext())
                _evictionKeys.Add(enumerator.Current.Key);

            for (int i = 0; i < _evictionKeys.Count; i++)
                RemoveChunkAbyssalNavPayload(_evictionKeys[i]);
        }

        private static void DisposeChunkAbyssalNavPayload(ref ChunkAbyssalNavPayload payload)
        {
            DisposeNativeArray(ref payload.Nodes);
            DisposeNativeArray(ref payload.ConduitVectors);
            DisposeNativeArray(ref payload.ConduitStrengths);
            DisposeNativeArray(ref payload.NodeTypes);
            payload.Count = 0;
        }

        public bool TryGetHLODRegistryPayload(out NativeArray<HLODData> entries, out int count)
        {
            entries = _nativeMemory.HlodRegistrySnapshotNative;
            count = _hlodRegistryCount;
            return count > 0 && entries.IsCreated;
        }

        /// <summary>
        /// Returns the current frustum-culled HLOD payload for distant rendering consumers.
        /// </summary>
        public bool TryGetVisibleHLODPayload(out NativeArray<HLODData> entries, out int count)
        {
            CompleteHLODCullJob(forceComplete: false);
            entries = _nativeMemory.VisibleHlodSnapshotNative;
            count = _visibleHlodCount;
            return count > 0 && entries.IsCreated;
        }

        /// <summary>
        /// Returns the current terrain-hole streaming payload for cave and interior streaming consumers.
        /// </summary>

        private void RebuildHLODRegistrySnapshot()
        {
            CompleteHLODCullJob(forceComplete: false);
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

            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
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
            EnsureNativeCapacity(ref _nativeMemory.HlodRegistrySnapshotNative, registryCount);

            int writeIndex = 0;
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
                    _nativeMemory.HlodRegistrySnapshotNative[writeIndex] = entry;
                    writeIndex++;
                }
            }

            for (int i = 0; i < _persistentArtificialStructures.Count; i++)
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
                _nativeMemory.HlodRegistrySnapshotNative[writeIndex] = entry;
                writeIndex++;
            }
        }

        private bool ShouldRegisterHLOD(Vector3 center, Vector3 size, in AbsoluteUniversePosition viewerAup)
        {
            float largestAxis = math.max(size.x, math.max(size.y, size.z));
            if (largestAxis < hlodMinimumStructureSize)
                return false;

            AbsoluteUniversePosition centerAup = AbsoluteUniversePosition.FromRuntimePosition(center);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in centerAup, in viewerAup);
            float maxDistance = hlodMaximumDistance + (largestAxis * 0.5f);
            double maxDistanceSq = maxDistance * maxDistance;
            return distanceSq <= maxDistanceSq;
        }

        private void ScheduleHLODVisibilityCullJob()
        {
            if (_hlodCullScheduled || _hlodRegistryCount <= 0 || !_nativeMemory.HlodRegistrySnapshotNative.IsCreated)
                return;

            Camera activeViewCamera = ResolveActiveViewCamera();
            if (activeViewCamera == null)
            {
                _visibleHlodCount = 0;
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(activeViewCamera, _viewFrustumPlanes);
            EnsureFloat4NativeCapacity(ref _nativeMemory.HlodFrustumPlanesNative, 6);
            for (int i = 0; i < 6; i++)
            {
                Plane plane = _viewFrustumPlanes[i];
                _nativeMemory.HlodFrustumPlanesNative[i] = new float4(plane.normal.x, plane.normal.y, plane.normal.z, plane.distance);
            }

            EnsureByteNativeCapacity(ref _nativeMemory.HlodVisibleFlagsNative, _hlodRegistryCount);
            if (!TryResolvePlayerRuntimePositionFromAup(out Vector3 viewerPosition) ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition viewerAup))
            {
                _visibleHlodCount = 0;
                return;
            }

            float fullyVisibleDistance = math.max(hlodMinimumDistance, residentRadius + 1f);
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _nativeMemory.HlodRegistrySnapshotNative[i];
                AbsoluteUniversePosition entryAup = AbsoluteUniversePosition.FromRuntimePosition(entry.Center);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in viewerAup, in entryAup);
                entry.Fade01 = ComputeHLODFadeSq01(distanceSq, residentRadius, fullyVisibleDistance);
                _hlodRegistrySnapshot[i] = entry;
                _nativeMemory.HlodRegistrySnapshotNative[i] = entry;
            }

            var job = new CullHLODInstancesJob
            {
                Registry = _nativeMemory.HlodRegistrySnapshotNative,
                FrustumPlanes = _nativeMemory.HlodFrustumPlanesNative,
                VisibleFlags = _nativeMemory.HlodVisibleFlagsNative,
                ViewerPosition = new float3(viewerPosition.x, viewerPosition.y, viewerPosition.z),
                MinimumDistanceSq = residentRadius * residentRadius,
                MaximumDistanceSq = hlodMaximumDistance * hlodMaximumDistance,
                FrustumPadding = hlodFrustumPadding
            };

            _hlodCullHandle = job.Schedule(_hlodRegistryCount, 16);
            _hlodCullScheduled = true;
        }

        private void CompleteHLODCullJob(bool forceComplete)
        {
            if (!_hlodCullScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _hlodCullHandle, forceComplete))
                return;

            _hlodCullScheduled = false;

            int visibleCount = 0;
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                if (_nativeMemory.HlodVisibleFlagsNative[i] != 0)
                    visibleCount++;
            }

            _visibleHlodCount = visibleCount;
            if (visibleCount <= 0)
                return;

            EnsureHLODDataCapacity(ref _visibleHlodSnapshot, visibleCount);
            EnsureNativeCapacity(ref _nativeMemory.VisibleHlodSnapshotNative, visibleCount);
            int writeIndex = 0;
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                if (_nativeMemory.HlodVisibleFlagsNative[i] == 0)
                    continue;

                HLODData entry = _nativeMemory.HlodRegistrySnapshotNative[i];
                _visibleHlodSnapshot[writeIndex] = entry;
                _nativeMemory.VisibleHlodSnapshotNative[writeIndex] = entry;
                writeIndex++;
            }
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
            AbsoluteUniversePosition a = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionA);
            AbsoluteUniversePosition b = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionB);
            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private AbsoluteUniversePosition ResolveViewerAup(Vector3 fallbackRuntimePosition)
        {
            return TryResolvePlayerAup(out AbsoluteUniversePosition viewerAup)
                ? viewerAup
                : AbsoluteUniversePosition.FromRuntimePosition(fallbackRuntimePosition);
        }

        private int ResolveMaxAbyssalNavNodeCapacity()
        {
            return math.clamp(maxAbyssalNavNodeCapacity, 1, 32768);
        }

        private int ResolveMaxAbyssalPathWaypointCapacity()
        {
            return math.clamp(maxAbyssalPathWaypointCapacity, 2, 32768);
        }

        private int ResolveAbyssalNavGraphHashCapacity()
        {
            int fixedNodeCapacity = ResolveMaxAbyssalNavNodeCapacity();
            return math.max(1, fixedNodeCapacity * DefaultAbyssalNavHashEntriesPerNode);
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
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalNavNodeSnapshotNative, fixedNodeCapacity);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalNavConduitVectorsSnapshotNative, fixedNodeCapacity);
            EnsureFloatNativeCapacity(ref _nativeMemory.AbyssalNavConduitStrengthSnapshotNative, fixedNodeCapacity);
            EnsureByteNativeCapacity(ref _nativeMemory.AbyssalNavNodeTypesSnapshotNative, fixedNodeCapacity);
            if (!EnsureAbyssalNavGraphHashCapacity(fixedNodeCapacity * DefaultAbyssalNavHashEntriesPerNode))
                return;

            EnsureAbyssalPathBuffers(fixedNodeCapacity);
            EnsureVector3Capacity(ref _abyssalPathSnapshot, fixedPathCapacity);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalPathSnapshotNative, fixedPathCapacity);
        }

        private bool EnsureAbyssalNavGraphHashCapacity(int requiredCapacity)
        {
            int fixedCapacity = ResolveAbyssalNavGraphHashCapacity();
            int requiredSafeCapacity = math.max(1, requiredCapacity);
            if (!_nativeMemory.AbyssalNavGraphHashNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[fixedCapacity] - fixed abyssal nav-node spatial hash; never resized during gameplay - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalNavGraphHashNative = new NativeParallelMultiHashMap<int, int>(fixedCapacity, Allocator.Persistent);
                RegisterTrackedNativeParallelMultiHashMap(_nativeMemory.AbyssalNavGraphHashNative, nameof(_nativeMemory.AbyssalNavGraphHashNative));
                return fixedCapacity >= requiredSafeCapacity;
            }

            if (_nativeMemory.AbyssalNavGraphHashNative.Capacity < requiredSafeCapacity)
                return false;

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

            EnsureNativeCapacity(ref _nativeMemory.AbyssalPathParentsNative, fixedNodeCapacity);
            EnsureFloatNativeCapacity(ref _nativeMemory.AbyssalPathGScoreNative, fixedNodeCapacity);
            EnsureFloatNativeCapacity(ref _nativeMemory.AbyssalPathFScoreNative, fixedNodeCapacity);
            EnsureByteNativeCapacity(ref _nativeMemory.AbyssalPathClosedFlagsNative, fixedNodeCapacity);
            EnsureNativeCapacity(ref _nativeMemory.AbyssalPathHeapNodesNative, fixedNodeCapacity);
            EnsureNativeCapacity(ref _nativeMemory.AbyssalPathHeapPositionsNative, fixedNodeCapacity);
            EnsureVector3Capacity(ref _abyssalPathSnapshot, fixedPathCapacity);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalPathSnapshotNative, fixedPathCapacity);

            if (!_nativeMemory.AbyssalPathRawResultNative.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[fixedPathCapacity] - fixed raw abyssal A* path before Burst string-pulling - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalPathRawResultNative = new NativeList<Vector3>(fixedPathCapacity, Allocator.Persistent);
                RegisterTrackedNativeList(_nativeMemory.AbyssalPathRawResultNative, nameof(_nativeMemory.AbyssalPathRawResultNative));
            }
            else if (_nativeMemory.AbyssalPathRawResultNative.Capacity < requiredPathCapacity)
                return false;

            if (!_nativeMemory.AbyssalPathResultNative.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[fixedPathCapacity] - fixed latest smoothed abyssal path waypoint result - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalPathResultNative = new NativeList<Vector3>(fixedPathCapacity, Allocator.Persistent);
                RegisterTrackedNativeList(_nativeMemory.AbyssalPathResultNative, nameof(_nativeMemory.AbyssalPathResultNative));
            }
            else if (_nativeMemory.AbyssalPathResultNative.Capacity < requiredPathCapacity)
                return false;

            return true;
        }

        private void CompleteAbyssalPathJob(bool forceComplete)
        {
            if (!_abyssalPathScheduled)
                return;

            long completeStartTicks = Stopwatch.GetTimestamp();
            if (!DispatcherJobSwap.TryComplete(ref _abyssalPathHandle, forceComplete))
                return;

            float funnelMs = ResolveAbyssalPathElapsedMs(Stopwatch.GetTimestamp() - completeStartTicks);
            _abyssalPathScheduled = false;
            int rawPathCount = _nativeMemory.AbyssalPathRawResultNative.IsCreated ? _nativeMemory.AbyssalPathRawResultNative.Length : 0;
            _abyssalPathCount = _nativeMemory.AbyssalPathResultNative.IsCreated ? _nativeMemory.AbyssalPathResultNative.Length : 0;
            if (_abyssalPathCount <= 0)
            {
                RecordAbyssalPathTelemetry(funnelMs, rawPathCount, 0, default, default, true);
                return;
            }

            EnsureVector3Capacity(ref _abyssalPathSnapshot, _abyssalPathCount);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalPathSnapshotNative, _abyssalPathCount);
            Vector3 start = default;
            Vector3 end = default;
            bool finite = true;
            for (int i = 0; i < _abyssalPathCount; i++)
            {
                Vector3 waypoint = _nativeMemory.AbyssalPathResultNative[i];
                if (i == 0)
                    start = waypoint;
                end = waypoint;
                if (!IsFinite(waypoint))
                    finite = false;
                _abyssalPathSnapshot[i] = waypoint;
                _nativeMemory.AbyssalPathSnapshotNative[i] = waypoint;
            }

            RecordAbyssalPathTelemetry(funnelMs, rawPathCount, _abyssalPathCount, start, end, finite);
        }

        private void EnsureAbyssalPathTelemetry()
        {
            if (_abyssalPathTelemetry.IsCreated)
                return;

            _abyssalPathTelemetry = new NativeArray<AbyssalPathTelemetryEntry>(
                AbyssalPathTelemetryFrameCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            RegisterTrackedNativeArray(_abyssalPathTelemetry, nameof(_abyssalPathTelemetry));
            _abyssalPathTelemetryCursor = 0;
            _abyssalPathTelemetryWrittenCount = 0;
            _abyssalPathTelemetrySequence = 0;
            _abyssalPathTelemetryDumpedForFault = false;
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
            bool finite)
        {
            EnsureAbyssalPathTelemetry();
            if (outputCount <= 0 &&
                TryResolveAbyssalRawPathTelemetry(rawCount, out Vector3 rawStart, out Vector3 rawEnd, out bool rawFinite))
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

            _abyssalPathTelemetry[_abyssalPathTelemetryCursor] = new AbyssalPathTelemetryEntry
            {
                Frame = Time.frameCount,
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

        private bool TryResolveAbyssalRawPathTelemetry(int rawCount, out Vector3 start, out Vector3 end, out bool finite)
        {
            start = default;
            end = default;
            finite = true;
            if (!_nativeMemory.AbyssalPathRawResultNative.IsCreated || rawCount <= 0)
                return false;

            int count = math.min(rawCount, _nativeMemory.AbyssalPathRawResultNative.Length);
            if (count <= 0)
                return false;

            for (int i = 0; i < count; i++)
            {
                Vector3 waypoint = _nativeMemory.AbyssalPathRawResultNative[i];
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
            if (_abyssalPathTelemetryDumpedForFault || !_abyssalPathTelemetry.IsCreated)
                return;

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

                        AbyssalPathTelemetryEntry entry = _abyssalPathTelemetry[entryIndex];
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
            catch (Exception exception)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(AbyssalPathNanFaultHash, AbyssalPathTelemetryContextHash, exception.HResult);
            }
        }

        private void DisposeAbyssalPathState()
        {
            JobHandle disposeHandle = _abyssalPathScheduled ? _abyssalPathHandle : default;
            DisposeNativeArray(ref _nativeMemory.AbyssalPathSnapshotNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalPathParentsNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalPathGScoreNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalPathFScoreNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalPathClosedFlagsNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalPathHeapNodesNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.AbyssalPathHeapPositionsNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.PredatorFearNodesSnapshotNative, disposeHandle);
            DisposeNativeArray(ref _abyssalPathTelemetry);
            DisposeNativeList(ref _nativeMemory.AbyssalPathRawResultNative, disposeHandle, nameof(_nativeMemory.AbyssalPathRawResultNative));
            DisposeNativeList(ref _nativeMemory.AbyssalPathResultNative, disposeHandle, nameof(_nativeMemory.AbyssalPathResultNative));
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
            JobHandle disposeHandle = _hlodCullScheduled ? _hlodCullHandle : default;
            DisposeNativeArray(ref _nativeMemory.HlodRegistrySnapshotNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.VisibleHlodSnapshotNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.HlodVisibleFlagsNative, disposeHandle);
            DisposeNativeArray(ref _nativeMemory.HlodFrustumPlanesNative, disposeHandle);
            _hlodCullHandle = default;
            _hlodCullScheduled = false;
            _hlodRegistryCount = 0;
            _visibleHlodCount = 0;
        }

        private void InvalidateAbyssalPathState()
        {
            if (_abyssalPathScheduled)
                CompleteAbyssalPathJob(forceComplete: true);

            _abyssalPathCount = 0;
            _lastAbyssalPathEndNode = -1;
            _hasLastAbyssalPathTarget = false;
            if (_nativeMemory.AbyssalPathRawResultNative.IsCreated)
                _nativeMemory.AbyssalPathRawResultNative.Clear();
            if (_nativeMemory.AbyssalPathResultNative.IsCreated)
                _nativeMemory.AbyssalPathResultNative.Clear();
        }

        private bool EnsureAbyssalNavNodeListCapacity(int requiredCount)
        {
            int fixedCapacity = ResolveMaxAbyssalNavNodeCapacity();
            if (requiredCount > fixedCapacity)
                return false;

            if (!_nativeMemory.AbyssalNavNodes.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[fixedCapacity] - fixed active abyssal safe-node snapshot list for pathfinding consumers - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalNavNodes = new NativeList<Vector3>(fixedCapacity, Allocator.Persistent);
                RegisterTrackedNativeList(_nativeMemory.AbyssalNavNodes, nameof(_nativeMemory.AbyssalNavNodes));
                return true;
            }

            return _nativeMemory.AbyssalNavNodes.Capacity >= requiredCount;
        }

        private void ShiftChunkAbyssalNavPayloads(Vector3 offset)
        {
            if (_chunkAbyssalNavPayloads.Count <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            _evictionKeys.Clear();
            Dictionary<ChunkKey, ChunkAbyssalNavPayload>.Enumerator enumerator = _chunkAbyssalNavPayloads.GetEnumerator();
            while (enumerator.MoveNext())
                _evictionKeys.Add(enumerator.Current.Key);

            for (int keyIndex = 0; keyIndex < _evictionKeys.Count; keyIndex++)
            {
                ChunkKey key = _evictionKeys[keyIndex];
                if (!_chunkAbyssalNavPayloads.TryGetValue(key, out ChunkAbyssalNavPayload payload) || payload.Count <= 0 || !payload.Nodes.IsCreated)
                    continue;

                for (int nodeIndex = 0; nodeIndex < payload.Count; nodeIndex++)
                    payload.Nodes[nodeIndex] += offset;

                _chunkAbyssalNavPayloads[key] = payload;
            }
        }

        private void ShiftAbyssalNavSnapshots(Vector3 offset)
        {
            if (_abyssalNavNodeCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                _abyssalNavNodeSnapshot[i] += offset;
                if (_nativeMemory.AbyssalNavNodeSnapshotNative.IsCreated && i < _nativeMemory.AbyssalNavNodeSnapshotNative.Length)
                    _nativeMemory.AbyssalNavNodeSnapshotNative[i] = _abyssalNavNodeSnapshot[i];
            }

            if (_nativeMemory.AbyssalNavNodes.IsCreated)
            {
                for (int i = 0; i < _abyssalNavNodeCount; i++)
                    _nativeMemory.AbyssalNavNodes[i] = _abyssalNavNodeSnapshot[i];
            }
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
                if (_nativeMemory.HlodRegistrySnapshotNative.IsCreated && i < _nativeMemory.HlodRegistrySnapshotNative.Length)
                    _nativeMemory.HlodRegistrySnapshotNative[i] = entry;
            }

            for (int i = 0; i < _visibleHlodCount; i++)
            {
                HLODData entry = _visibleHlodSnapshot[i];
                entry.Center += offset;
                _visibleHlodSnapshot[i] = entry;
                if (_nativeMemory.VisibleHlodSnapshotNative.IsCreated && i < _nativeMemory.VisibleHlodSnapshotNative.Length)
                    _nativeMemory.VisibleHlodSnapshotNative[i] = entry;
            }
        }

        private void ShiftAbyssalPathSnapshot(Vector3 offset)
        {
            if (_abyssalPathCount <= 0 || offset.sqrMagnitude <= 0.000001f)
                return;

            for (int i = 0; i < _abyssalPathCount; i++)
            {
                _abyssalPathSnapshot[i] += offset;
                if (_nativeMemory.AbyssalPathSnapshotNative.IsCreated && i < _nativeMemory.AbyssalPathSnapshotNative.Length)
                    _nativeMemory.AbyssalPathSnapshotNative[i] = _abyssalPathSnapshot[i];
            }

            if (_nativeMemory.AbyssalPathResultNative.IsCreated)
            {
                for (int i = 0; i < _abyssalPathCount && i < _nativeMemory.AbyssalPathResultNative.Length; i++)
                    _nativeMemory.AbyssalPathResultNative[i] = _abyssalPathSnapshot[i];
            }
        }

        private void BuildFlowFieldNavSupportGrid(Vector3 gridCenter)
        {
            if (!_nativeMemory.FlowNavSupportGridNative.IsCreated ||
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
                        if (_nativeMemory.FlowNavSupportGridNative[index] < clampedSupport)
                            _nativeMemory.FlowNavSupportGridNative[index] = clampedSupport;
                    }
                }
            }
        }

        private int FindNearestAbyssalNavNodeIndex(Vector3 position)
        {
            if (_abyssalNavNodeCount <= 0 ||
                !_nativeMemory.AbyssalNavNodeSnapshotNative.IsCreated ||
                _abyssalNavNodeSnapshot == null ||
                _abyssalNavNodeSnapshot.Length <= 0 ||
                !IsFinite(position))
            {
                return -1;
            }

            int safeNodeCount = math.min(_abyssalNavNodeCount, _abyssalNavNodeSnapshot.Length);
            safeNodeCount = math.min(safeNodeCount, _nativeMemory.AbyssalNavNodeSnapshotNative.Length);
            if (safeNodeCount <= 0)
                return -1;

            if (TryFindNearestAbyssalNavNodeIndexFromHash(position, out int hashedIndex))
                return hashedIndex;

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

        private bool TryFindNearestAbyssalNavNodeIndexFromHash(Vector3 position, out int bestIndex)
        {
            bestIndex = -1;
            if (!_nativeMemory.AbyssalNavGraphHashNative.IsCreated ||
                _abyssalNavNodeCount <= 0 ||
                _abyssalNavNodeSnapshot == null ||
                _abyssalNavNodeSnapshot.Length <= 0 ||
                abyssalNavGraphCellSize <= 0f ||
                !math.isfinite(abyssalNavGraphCellSize) ||
                !IsFinite(position) ||
                !IsFinite(_abyssalNavGraphOrigin))
            {
                return false;
            }

            int safeNodeCount = math.min(_abyssalNavNodeCount, _abyssalNavNodeSnapshot.Length);
            safeNodeCount = math.min(safeNodeCount, _nativeMemory.AbyssalNavNodeSnapshotNative.IsCreated
                ? _nativeMemory.AbyssalNavNodeSnapshotNative.Length
                : 0);
            if (safeNodeCount <= 0)
                return false;

            float inverseCellSize = math.rcp(math.max(0.01f, abyssalNavGraphCellSize));
            int baseCellX = (int)math.floor((position.x - _abyssalNavGraphOrigin.x) * inverseCellSize);
            int baseCellY = (int)math.floor((position.y - _abyssalNavGraphOrigin.y) * inverseCellSize);
            int baseCellZ = (int)math.floor((position.z - _abyssalNavGraphOrigin.z) * inverseCellSize);
            float bestDistanceSq = float.PositiveInfinity;
            int searchRadiusCells = math.clamp((int)math.ceil(abyssalPathNeighborRadius * math.rcp(math.max(1f, abyssalNavGraphCellSize))), 1, 3);
            for (int radius = 0; radius <= searchRadiusCells; radius++)
            {
                bool foundAny = false;
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetZ = -radius; offsetZ <= radius; offsetZ++)
                    {
                        for (int offsetX = -radius; offsetX <= radius; offsetX++)
                        {
                            int key = HashSpatialCell(baseCellX + offsetX, baseCellY + offsetY, baseCellZ + offsetZ);
                            if (!_nativeMemory.AbyssalNavGraphHashNative.TryGetFirstValue(key, out int nodeIndex, out NativeParallelMultiHashMapIterator<int> iterator))
                                continue;

                            do
                            {
                                if ((uint)nodeIndex >= safeNodeCount)
                                    continue;

                                foundAny = true;
                                Vector3 candidate = _abyssalNavNodeSnapshot[nodeIndex];
                                if (!IsFinite(candidate))
                                    continue;

                                float distanceSq = (candidate - position).sqrMagnitude;
                                if (distanceSq >= bestDistanceSq)
                                    continue;

                                bestDistanceSq = distanceSq;
                                bestIndex = nodeIndex;
                            }
                            while (_nativeMemory.AbyssalNavGraphHashNative.TryGetNextValue(out nodeIndex, ref iterator));
                        }
                    }
                }

                if (foundAny && bestIndex >= 0)
                    return true;
            }

            return false;
        }

        private static int ComputeAbyssalNavGraphHashKey(Vector3 position, Vector3 origin, float cellSize)
        {
            if (!IsFinite(position) || !IsFinite(origin) || !math.isfinite(cellSize))
                return HashSpatialCell(0, 0, 0);

            float inverseCellSize = math.rcp(math.max(0.01f, cellSize));
            int cellX = (int)math.floor((position.x - origin.x) * inverseCellSize);
            int cellY = (int)math.floor((position.y - origin.y) * inverseCellSize);
            int cellZ = (int)math.floor((position.z - origin.z) * inverseCellSize);
            return HashSpatialCell(cellX, cellY, cellZ);
        }

        private static int HashSpatialCell(int x, int y, int z)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)x) * 16777619u;
                hash = (hash ^ (uint)y) * 16777619u;
                hash = (hash ^ (uint)z) * 16777619u;
                return (int)hash;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CullHLODInstancesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<HLODData> Registry;
            [ReadOnly] public NativeArray<float4> FrustumPlanes;
            [WriteOnly] public NativeArray<byte> VisibleFlags;
            public float3 ViewerPosition;
            public float MinimumDistanceSq;
            public float MaximumDistanceSq;
            public float FrustumPadding;

            public void Execute(int index)
            {
                if (!VisibleFlags.IsCreated || index < 0 || index >= VisibleFlags.Length || !Registry.IsCreated || index >= Registry.Length)
                    return;

                HLODData entry = Registry[index];
                float3 center = new float3(entry.Center.x, entry.Center.y, entry.Center.z);
                float3 delta = center - ViewerPosition;
                float distanceSq = math.lengthsq(delta);
                if (distanceSq < MinimumDistanceSq || distanceSq > MaximumDistanceSq)
                {
                    VisibleFlags[index] = 0;
                    return;
                }

                float3 extents = new float3(
                    math.max(0.5f, entry.Size.x * 0.5f + FrustumPadding),
                    math.max(0.5f, entry.Size.y * 0.5f + FrustumPadding),
                    math.max(0.5f, entry.Size.z * 0.5f + FrustumPadding));
                if (!IsVisible(center, extents))
                {
                    VisibleFlags[index] = 0;
                    return;
                }

                VisibleFlags[index] = 1;
            }

            private bool IsVisible(float3 center, float3 extents)
            {
                if (!FrustumPlanes.IsCreated || FrustumPlanes.Length < 6)
                    return true;

                for (int i = 0; i < 6; i++)
                {
                    float4 plane = FrustumPlanes[i];
                    float3 normal = plane.xyz;
                    float projectedRadius = math.dot(math.abs(normal), extents);
                    float signedDistance = math.dot(normal, center) + plane.w;
                    if (signedDistance + projectedRadius < 0f)
                        return false;
                }

                return true;
            }
        }
    }
}
