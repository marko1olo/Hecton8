using System;
using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Environment;
using MapMagic.Products;
using MapMagic.Terrains;
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
            if (outputWaypoints == null || outputWaypoints.Length < 2)
                return false;

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

            EnsureAbyssalPathBuffers(_abyssalNavNodeCount);
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
                MaxSamplesPerSegment = abyssalPathSmoothingMaxSamples,
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

            int safeCount = Mathf.Min(
                count,
                Mathf.Min(_surfaceAggregateFrontBuffers.FlowDirections.Length, _surfaceAggregateFrontBuffers.FlowVectors.Length));
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

            float chunkWidth = Mathf.Max(0.01f, payload.MaxX - payload.MinX);
            float chunkDepth = Mathf.Max(0.01f, payload.MaxZ - payload.MinZ);
            int sampleCountX = Mathf.Max(1, Mathf.FloorToInt(chunkWidth / abyssalNavNodeStepMeters));
            int sampleCountZ = Mathf.Max(1, Mathf.FloorToInt(chunkDepth / abyssalNavNodeStepMeters));
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

            float stepX = chunkWidth / sampleCountX;
            float stepZ = chunkDepth / sampleCountZ;
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
            int end = Mathf.Min(underwaterPool.Matrices.Length, payload.UnderwaterOffset + payload.UnderwaterCount);
            for (int poolIndex = Mathf.Max(0, payload.UnderwaterOffset); poolIndex < end; poolIndex++)
            {
                Vector3 position = ResolveRuntimePosition(underwaterPool.Matrices[poolIndex]);
                float dx = position.x - candidate.x;
                float dz = position.z - candidate.z;
                float horizontalDistanceSq = (dx * dx) + (dz * dz);
                if (horizontalDistanceSq > obstacleRadiusSq)
                    continue;

                float verticalDelta = Mathf.Abs(position.y - candidate.y);
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

                Vector3 flowVector = underwaterPool.FlowVectors[poolIndex];
                flowMagnitudeSum += flowVector.magnitude;
                flowVectorSum += flowVector;
                contributingSamples++;
                if (obstacleWeight > abyssalNavNodeMaxObstacleDensity)
                    return false;
            }

            if (deepAffinity < abyssalNavNodeMinimumDeepAffinity)
                return false;

            float averageCurrentMagnitude = contributingSamples > 0
                ? flowMagnitudeSum / contributingSamples
                : 0f;
            if (averageCurrentMagnitude > abyssalNavNodeMaxCurrentMagnitude)
                return false;

            float depthMeters = Mathf.Max(0f, waterLevel - candidate.y);
            if (depthMeters < abyssalConduitStartDepth ||
                averageCurrentMagnitude < abyssalConduitMinimumFlowMagnitude ||
                contributingSamples <= 0)
            {
                return true;
            }

            if (flowVectorSum.sqrMagnitude <= 0.0001f)
                return true;

            conduitVector = flowVectorSum.normalized;
            if (abyssalNavNodeMaxCurrentMagnitude <= abyssalConduitMinimumFlowMagnitude)
            {
                conduitStrength = 1f;
                return true;
            }

            conduitStrength = Mathf.Clamp01(
                (averageCurrentMagnitude - abyssalConduitMinimumFlowMagnitude) /
                Mathf.Max(0.01f, abyssalNavNodeMaxCurrentMagnitude - abyssalConduitMinimumFlowMagnitude));
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

            float localX = worldX - state.TerrainPosition.x;
            float localZ = worldZ - state.TerrainPosition.z;
            if (localX < 0f || localZ < 0f || localX > state.TerrainSize.x || localZ > state.TerrainSize.z)
                return false;

            float normalizedX = Mathf.Clamp01(localX / Mathf.Max(0.01f, state.TerrainSize.x));
            float normalizedZ = Mathf.Clamp01(localZ / Mathf.Max(0.01f, state.TerrainSize.z));
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

            int end = Mathf.Min(pool.BiomeLayers.Length, offset + count);
            for (int poolIndex = Mathf.Max(0, offset); poolIndex < end; poolIndex++)
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

            Vector3 viewerPosition = playerTransform != null ? playerTransform.position : Vector3.zero;
            AbsoluteUniversePosition viewerAup = ResolveViewerAup(viewerPosition);
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
            float largestAxis = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
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
            Vector3 viewerPosition = playerTransform != null ? playerTransform.position : activeViewCamera.transform.position;
            AbsoluteUniversePosition viewerAup = ResolveViewerAup(activeViewCamera.transform.position);
            float fullyVisibleDistance = Mathf.Max(hlodMinimumDistance, residentRadius + 1f);
            for (int i = 0; i < _hlodRegistryCount; i++)
            {
                HLODData entry = _nativeMemory.HlodRegistrySnapshotNative[i];
                AbsoluteUniversePosition entryAup = AbsoluteUniversePosition.FromRuntimePosition(entry.Center);
                double distanceSq = AbsoluteUniversePosition.DistanceSq(in viewerAup, in entryAup);
                float distance = distanceSq > 0d ? (float)math.sqrt(distanceSq) : 0f;
                entry.Fade01 = ComputeHLODFade01(distance, residentRadius, fullyVisibleDistance);
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

            if (!VegetationJobRecovery.TryComplete(ref _hlodCullHandle, forceComplete))
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

        private static float ComputeHLODFade01(float distance, float lod0Radius, float fullyVisibleDistance)
        {
            float fadeStart = Mathf.Max(0f, lod0Radius);
            float fadeEnd = Mathf.Max(fadeStart + 1f, fullyVisibleDistance);
            return Mathf.Clamp01((distance - fadeStart) / (fadeEnd - fadeStart));
        }

        private static double ComputeAupDistanceSq(Vector3 runtimePositionA, Vector3 runtimePositionB)
        {
            AbsoluteUniversePosition a = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionA);
            AbsoluteUniversePosition b = AbsoluteUniversePosition.FromRuntimePosition(runtimePositionB);
            return AbsoluteUniversePosition.DistanceSq(in a, in b);
        }

        private AbsoluteUniversePosition ResolveViewerAup(Vector3 fallbackRuntimePosition)
        {
            Vector3 viewerRuntimePosition = playerTransform != null ? playerTransform.position : fallbackRuntimePosition;
            return AbsoluteUniversePosition.FromRuntimePosition(viewerRuntimePosition);
        }

        private void EnsureAbyssalNavGraphHashCapacity(int requiredCapacity)
        {
            int safeCapacity = Mathf.Max(1, requiredCapacity);
            if (!_nativeMemory.AbyssalNavGraphHashNative.IsCreated)
            {
                // COLD ALLOC: NativeParallelMultiHashMap<int,int>[safeCapacity] - spatial hash for immutable abyssal nav-node lookup - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalNavGraphHashNative = new NativeParallelMultiHashMap<int, int>(safeCapacity, Allocator.Persistent);
                return;
            }

            if (_nativeMemory.AbyssalNavGraphHashNative.Capacity < safeCapacity)
                _nativeMemory.AbyssalNavGraphHashNative.Capacity = safeCapacity;
            else if (_nativeMemory.AbyssalNavGraphHashNative.Capacity > safeCapacity * 4)
                _nativeMemory.AbyssalNavGraphHashNative.Capacity = safeCapacity;
        }

        private void EnsureAbyssalPathBuffers(int nodeCount)
        {
            int requiredCount = Mathf.Max(1, nodeCount);
            EnsureNativeCapacity(ref _nativeMemory.AbyssalPathParentsNative, requiredCount);
            EnsureFloatNativeCapacity(ref _nativeMemory.AbyssalPathGScoreNative, requiredCount);
            EnsureFloatNativeCapacity(ref _nativeMemory.AbyssalPathFScoreNative, requiredCount);
            EnsureByteNativeCapacity(ref _nativeMemory.AbyssalPathClosedFlagsNative, requiredCount);
            EnsureNativeCapacity(ref _nativeMemory.AbyssalPathHeapNodesNative, requiredCount);
            EnsureNativeCapacity(ref _nativeMemory.AbyssalPathHeapPositionsNative, requiredCount);
            EnsureVector3Capacity(ref _abyssalPathSnapshot, requiredCount + 2);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalPathSnapshotNative, requiredCount + 2);

            if (!_nativeMemory.AbyssalPathRawResultNative.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[requiredCount+2] - raw abyssal A* path before Burst string-pulling - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalPathRawResultNative = new NativeList<Vector3>(requiredCount + 2, Allocator.Persistent);
            }
            else if (_nativeMemory.AbyssalPathRawResultNative.Capacity < requiredCount + 2)
            {
                _nativeMemory.AbyssalPathRawResultNative.Capacity = requiredCount + 2;
            }
            else if (_nativeMemory.AbyssalPathRawResultNative.Capacity > (requiredCount + 2) * 4)
            {
                _nativeMemory.AbyssalPathRawResultNative.Capacity = requiredCount + 2;
            }

            if (!_nativeMemory.AbyssalPathResultNative.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[requiredCount+2] - latest smoothed abyssal path waypoint result - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalPathResultNative = new NativeList<Vector3>(requiredCount + 2, Allocator.Persistent);
            }
            else if (_nativeMemory.AbyssalPathResultNative.Capacity < requiredCount + 2)
            {
                _nativeMemory.AbyssalPathResultNative.Capacity = requiredCount + 2;
            }
            else if (_nativeMemory.AbyssalPathResultNative.Capacity > (requiredCount + 2) * 4)
            {
                _nativeMemory.AbyssalPathResultNative.Capacity = requiredCount + 2;
            }
        }

        private void CompleteAbyssalPathJob(bool forceComplete)
        {
            if (!_abyssalPathScheduled)
                return;

            if (!VegetationJobRecovery.TryComplete(ref _abyssalPathHandle, forceComplete))
                return;

            _abyssalPathScheduled = false;
            _abyssalPathCount = _nativeMemory.AbyssalPathResultNative.IsCreated ? _nativeMemory.AbyssalPathResultNative.Length : 0;
            if (_abyssalPathCount <= 0)
                return;

            EnsureVector3Capacity(ref _abyssalPathSnapshot, _abyssalPathCount);
            EnsureVector3NativeCapacity(ref _nativeMemory.AbyssalPathSnapshotNative, _abyssalPathCount);
            for (int i = 0; i < _abyssalPathCount; i++)
            {
                Vector3 waypoint = _nativeMemory.AbyssalPathResultNative[i];
                _abyssalPathSnapshot[i] = waypoint;
                _nativeMemory.AbyssalPathSnapshotNative[i] = waypoint;
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
            DisposeNativeList(ref _nativeMemory.AbyssalPathRawResultNative, disposeHandle);
            DisposeNativeList(ref _nativeMemory.AbyssalPathResultNative, disposeHandle);
            _abyssalPathHandle = default;
            _abyssalPathScheduled = false;
            _abyssalPathCount = 0;
            _lastAbyssalPathEndNode = -1;
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

        private void EnsureAbyssalNavNodeListCapacity(int requiredCount)
        {
            int nextCapacity = Mathf.NextPowerOfTwo(Mathf.Max(1, requiredCount));
            if (!_nativeMemory.AbyssalNavNodes.IsCreated)
            {
                // COLD ALLOC: NativeList<Vector3>[nextCapacity] - active abyssal safe-node snapshot list for pathfinding consumers - owner: HectonMapMagicVegetationBridge
                _nativeMemory.AbyssalNavNodes = new NativeList<Vector3>(nextCapacity, Allocator.Persistent);
                return;
            }

            if (_nativeMemory.AbyssalNavNodes.Capacity < nextCapacity)
                _nativeMemory.AbyssalNavNodes.Capacity = nextCapacity;
            else if (_nativeMemory.AbyssalNavNodes.Capacity > nextCapacity * 4)
                _nativeMemory.AbyssalNavNodes.Capacity = nextCapacity;
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
            if (!_nativeMemory.FlowNavSupportGridNative.IsCreated || _ecosystemThreatGridResolution <= 0 || _abyssalNavNodeCount <= 0)
                return;

            int halfExtent = _ecosystemThreatGridResolution >> 1;
            int stencilRadius = Mathf.Max(0, flowFieldNavStencilRadiusCells);
            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                Vector3 node = _abyssalNavNodeSnapshot[i];
                int centerX = Mathf.RoundToInt((node.x - gridCenter.x) / threatGridCellSize) + halfExtent;
                int centerZ = Mathf.RoundToInt((node.z - gridCenter.z) / threatGridCellSize) + halfExtent;
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

                        float distance = Mathf.Sqrt((offsetX * offsetX) + (offsetZ * offsetZ));
                        float support01 = 1f - Mathf.Clamp01(distance / Mathf.Max(1f, stencilRadius + 0.25f));
                        int index = (cellZ * _ecosystemThreatGridResolution) + cellX;
                        float clampedSupport = Mathf.Clamp01(support01);
                        if (_nativeMemory.FlowNavSupportGridNative[index] < clampedSupport)
                            _nativeMemory.FlowNavSupportGridNative[index] = clampedSupport;
                    }
                }
            }
        }

        private int FindNearestAbyssalNavNodeIndex(Vector3 position)
        {
            if (_abyssalNavNodeCount <= 0 || !_nativeMemory.AbyssalNavNodeSnapshotNative.IsCreated)
                return -1;

            if (TryFindNearestAbyssalNavNodeIndexFromHash(position, out int hashedIndex))
                return hashedIndex;

            int bestIndex = -1;
            float bestDistanceSq = float.PositiveInfinity;
            for (int i = 0; i < _abyssalNavNodeCount; i++)
            {
                Vector3 candidate = _abyssalNavNodeSnapshot[i];
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
                abyssalNavGraphCellSize <= 0f)
            {
                return false;
            }

            int baseCellX = Mathf.FloorToInt((position.x - _abyssalNavGraphOrigin.x) / abyssalNavGraphCellSize);
            int baseCellY = Mathf.FloorToInt((position.y - _abyssalNavGraphOrigin.y) / abyssalNavGraphCellSize);
            int baseCellZ = Mathf.FloorToInt((position.z - _abyssalNavGraphOrigin.z) / abyssalNavGraphCellSize);
            float bestDistanceSq = float.PositiveInfinity;
            int searchRadiusCells = Mathf.Clamp(Mathf.CeilToInt(abyssalPathNeighborRadius / Mathf.Max(1f, abyssalNavGraphCellSize)), 1, 3);
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
                                if ((uint)nodeIndex >= _abyssalNavNodeCount)
                                    continue;

                                foundAny = true;
                                Vector3 candidate = _abyssalNavNodeSnapshot[nodeIndex];
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
            float safeCellSize = Mathf.Max(0.01f, cellSize);
            int cellX = Mathf.FloorToInt((position.x - origin.x) / safeCellSize);
            int cellY = Mathf.FloorToInt((position.y - origin.y) / safeCellSize);
            int cellZ = Mathf.FloorToInt((position.z - origin.z) / safeCellSize);
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
