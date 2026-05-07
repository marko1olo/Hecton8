using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.World
{
    public sealed partial class HectonMapMagicVegetationBridge
    {

        private void RefreshResidency()
        {
            if (_desiredChunkKeys == null || _desiredChunkDistances == null || _selectedChunkKeys == null || _pendingChunkKeys == null)
                return;

            if (_tileStates.Count == 0 || playerTransform == null)
            {
                ClearAllResidency();
                return;
            }

            TryValidateResidentTileCaches();
            Vector3 playerPosition = playerTransform.position;
            BuildDesiredChunkList(playerPosition);
            TrimPendingQueueToDesired();
            EvictNonResidentChunkPayloads();
            EnforceChunkPoolMemoryGuard();
            TrimPendingQueueToDesired();

            ProcessPendingChunkBuilds();
            bool selectionChanged = SyncSelectedChunksFromDesired();

            if (selectionChanged || _activeSetDirty)
            {
                if (RebuildAndBindActiveBuffers())
                    _activeSetDirty = false;
            }
        }

        private void BuildDesiredChunkList(Vector3 playerPosition)
        {
            _desiredChunkCount = 0;
            for (int i = 0; i < _desiredChunkDistances.Length; i++)
                _desiredChunkDistances[i] = float.PositiveInfinity;

            Vector2 playerPositionXZ = new Vector2(playerPosition.x, playerPosition.z);
            Vector2 planarVelocity = new Vector2(_playerVelocity.x, _playerVelocity.z);
            float planarSpeed = planarVelocity.magnitude;
            bool usePredictiveResidency = planarSpeed >= predictiveMinSpeed;
            Vector2 forward = usePredictiveResidency ? planarVelocity / Mathf.Max(0.0001f, planarSpeed) : Vector2.right;
            Vector2 right = new Vector2(-forward.y, forward.x);
            float forwardRadius = residentRadius + Mathf.Min(predictiveLeadMaxMeters, planarSpeed * predictiveLeadSeconds);
            float rearRadius = residentRadius * rearResidencyScale;
            float lateralRadius = residentRadius * lateralResidencyScale;
            float searchRadius = usePredictiveResidency ? Mathf.Max(forwardRadius, Mathf.Max(rearRadius, lateralRadius)) : residentRadius;
            Dictionary<long, TileRuntimeState>.Enumerator enumerator = _tileStates.GetEnumerator();
            while (enumerator.MoveNext())
            {
                TileRuntimeState state = enumerator.Current.Value;
                if (state == null || state.TerrainData == null)
                    continue;

                int minChunkX = GetChunkRangeStart(playerPosition.x - searchRadius, state.TerrainPosition.x, state.ChunkCountX);
                int maxChunkX = GetChunkRangeEnd(playerPosition.x + searchRadius, state.TerrainPosition.x, state.ChunkCountX);
                int minChunkZ = GetChunkRangeStart(playerPosition.z - searchRadius, state.TerrainPosition.z, state.ChunkCountZ);
                int maxChunkZ = GetChunkRangeEnd(playerPosition.z + searchRadius, state.TerrainPosition.z, state.ChunkCountZ);

                if (minChunkX > maxChunkX || minChunkZ > maxChunkZ)
                    continue;

                for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                {
                    for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                    {
                        GetChunkBounds(state, chunkX, chunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
                        if (!TryEvaluateResidencyCandidate(
                                playerPositionXZ,
                                forward,
                                right,
                                usePredictiveResidency,
                                forwardRadius,
                                rearRadius,
                                lateralRadius,
                                minX,
                                maxX,
                                minZ,
                                maxZ,
                                out float distanceSqr,
                                out float priority))
                        {
                            continue;
                        }

                        ChunkKey key = new ChunkKey(state.TileX, state.TileZ, chunkX, chunkZ);
                        InsertDesiredChunk(key, priority);
                        byte desiredGrassLodTier = GetGrassLodTier(distanceSqr);
                        bool shouldBeCorrupted = IsChunkCorrupted(key);
                        bool hasPayload = _chunkPayloads.TryGetValue(key, out ChunkPayload payload);
                        bool hasInFlightJob = _chunkBuildJobs.TryGetValue(key, out _);
                        bool corruptionMismatch = hasPayload && payload.IsCorrupted != shouldBeCorrupted;
                        if ((!hasPayload || corruptionMismatch) && !hasInFlightJob)
                        {
                            EnqueuePendingChunk(key, priority);
                        }
                        else if (hasPayload && !payload.IsCorrupted && payload.GrassLodTier != desiredGrassLodTier && !hasInFlightJob)
                        {
                            EnqueuePendingChunk(key, priority);
                        }
                    }
                }
            }
        }

        private int ProcessPendingChunkBuilds()
        {
            if (_pendingChunkCount <= 0)
                return 0;

            int buildBudget = Mathf.Min(maxChunkBuildsPerSlowTick, _pendingChunkCount);
            int scheduledCount = 0;

            for (int i = 0; i < buildBudget; i++)
            {
                ChunkKey key = _pendingChunkKeys[0];
                DequeuePendingChunkAt(0);

                if (_chunkBuildJobs.ContainsKey(key))
                    continue;

                long tileKey = PackTileCoord(key.TileX, key.TileZ);
                if (!_tileStates.TryGetValue(tileKey, out TileRuntimeState state) || state == null || state.TerrainData == null)
                    continue;

                byte grassLodTier = ResolveGrassLodTier(state, key.ChunkX, key.ChunkZ, playerTransform.position);
                if (!ScheduleChunkBuild(state, key, tileKey, grassLodTier))
                    continue;

                scheduledCount++;
            }

            return scheduledCount;
        }

        private bool SyncSelectedChunksFromDesired()
        {
            bool changed = false;
            int nextSelectedCount = 0;

            for (int i = 0; i < _desiredChunkCount; i++)
            {
                ChunkKey key = _desiredChunkKeys[i];
                if (!_chunkPayloads.ContainsKey(key))
                    continue;

                EnsureChunkKeyCapacity(ref _selectedChunkKeys, nextSelectedCount + 1);
                if (!changed)
                {
                    if (nextSelectedCount >= _selectedChunkCount || !_selectedChunkKeys[nextSelectedCount].Equals(key))
                        changed = true;
                }

                _selectedChunkKeys[nextSelectedCount] = key;
                nextSelectedCount++;
            }

            if (!changed && nextSelectedCount != _selectedChunkCount)
                changed = true;

            for (int i = nextSelectedCount; i < _selectedChunkCount; i++)
                _selectedChunkKeys[i] = default;

            _selectedChunkCount = nextSelectedCount;
            return changed;
        }

        private bool RebuildAndBindActiveBuffers()
        {
            if (_insideLateFrameJobSwap)
                CompleteAbyssalPathJob(forceComplete: false);

            RebuildDensityQuerySnapshot();
            RebuildAbyssalAnchorSnapshot();
            RebuildAbyssalNavNodeSnapshot();
            RebuildMegaWreckStreamSnapshot();
            RebuildCanopyHeightGrid();
            EnsureBoolCapacity(ref _selectedChunkVisibility, _selectedChunkCount);
            int totalSurfaceCount = 0;
            int totalUnderwaterCount = 0;
            bool hasSurfaceBounds = false;
            bool hasUnderwaterBounds = false;
            Bounds surfaceBounds = default;
            Bounds underwaterBounds = default;
            Camera activeViewCamera = ResolveActiveViewCamera();
            bool hasViewCamera = activeViewCamera != null;
            if (hasViewCamera)
                GeometryUtility.CalculateFrustumPlanes(activeViewCamera, _viewFrustumPlanes);

            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload))
                {
                    _selectedChunkVisibility[i] = false;
                    continue;
                }

                bool isVisible = !hasViewCamera || IsChunkVisible(payload.WorldBounds);
                _selectedChunkVisibility[i] = isVisible;
                if (!isVisible)
                    continue;

                if (payload.HasSurface)
                {
                    totalSurfaceCount += payload.SurfaceCount;
                    EncapsulateBounds(ref surfaceBounds, ref hasSurfaceBounds, payload.WorldBounds);
                }

                if (payload.HasUnderwater)
                {
                    totalUnderwaterCount += payload.UnderwaterCount;
                    EncapsulateBounds(ref underwaterBounds, ref hasUnderwaterBounds, payload.WorldBounds);
                }
            }

            _surfaceActiveCount = totalSurfaceCount;
            _underwaterActiveCount = totalUnderwaterCount;

            if ((totalSurfaceCount > 0 && !TryPrepareRendererWriteBuffer(ref _surfaceBackReaderHandle)) ||
                (totalUnderwaterCount > 0 && !TryPrepareRendererWriteBuffer(ref _underwaterBackReaderHandle)))
            {
                return false;
            }

            if (hasSurfaceBounds)
            {
                surfaceBounds.Expand(drawBoundsPadding);
                _surfaceDrawBounds = surfaceBounds;
            }
            else
            {
                _surfaceDrawBounds = default;
            }

            if (hasUnderwaterBounds)
            {
                underwaterBounds.Expand(drawBoundsPadding);
                _underwaterDrawBounds = underwaterBounds;
            }
            else
            {
                _underwaterDrawBounds = default;
            }

            if (totalSurfaceCount > 0)
            {
                EnsureActiveAggregateBufferCapacity(ref _surfaceAggregateBackBuffers, totalSurfaceCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasSurface)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.SurfaceCount;
                    CopyChunkSliceToAggregate(
                        ResolveChunkPool(isSurface: true, payload),
                        payload.SurfaceOffset,
                        _surfaceAggregateBackBuffers,
                        writeIndex,
                        copyCount);
                    writeIndex += copyCount;
                }

                _surfaceBackCount = totalSurfaceCount;
                _surfaceBackDrawBounds = surfaceBounds;
                _hasSurfaceBackBounds = hasSurfaceBounds;
                SwapActiveAggregateBuffers(ref _surfaceAggregateFrontBuffers, ref _surfaceAggregateBackBuffers);
                SwapAggregateReadState(
                    ref _surfaceFrontCount,
                    ref _surfaceBackCount,
                    ref _surfaceFrontDrawBounds,
                    ref _surfaceBackDrawBounds,
                    ref _hasSurfaceFrontBounds,
                    ref _hasSurfaceBackBounds,
                    ref _surfaceFrontReaderHandle,
                    ref _surfaceBackReaderHandle,
                    ref _surfaceFrontBufferIndex,
                    ref _surfaceBackBufferIndex);
                _surfaceActiveAggregateRevision++;
            }
            else
            {
                _surfaceFrontCount = 0;
                _hasSurfaceFrontBounds = false;
                _surfaceActiveAggregateRevision++;
            }

            if (totalUnderwaterCount > 0)
            {
                EnsureActiveAggregateBufferCapacity(ref _underwaterAggregateBackBuffers, totalUnderwaterCount);

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.UnderwaterCount;
                    CopyChunkSliceToAggregate(
                        ResolveChunkPool(isSurface: false, payload),
                        payload.UnderwaterOffset,
                        _underwaterAggregateBackBuffers,
                        writeIndex,
                        copyCount);
                    writeIndex += copyCount;
                }

                DistortAggregateFlowVectorsByThreat(_underwaterAggregateBackBuffers, totalUnderwaterCount);
                _underwaterBackCount = totalUnderwaterCount;
                _underwaterBackDrawBounds = underwaterBounds;
                _hasUnderwaterBackBounds = hasUnderwaterBounds;
                SwapActiveAggregateBuffers(ref _underwaterAggregateFrontBuffers, ref _underwaterAggregateBackBuffers);
                SwapAggregateReadState(
                    ref _underwaterFrontCount,
                    ref _underwaterBackCount,
                    ref _underwaterFrontDrawBounds,
                    ref _underwaterBackDrawBounds,
                    ref _hasUnderwaterFrontBounds,
                    ref _hasUnderwaterBackBounds,
                    ref _underwaterFrontReaderHandle,
                    ref _underwaterBackReaderHandle,
                    ref _underwaterFrontBufferIndex,
                    ref _underwaterBackBufferIndex);
                _underwaterActiveAggregateRevision++;
            }
            else
            {
                _underwaterFrontCount = 0;
                _hasUnderwaterFrontBounds = false;
                _underwaterActiveAggregateRevision++;
            }

            return true;
        }

        private bool ScheduleChunkBuild(TileRuntimeState state, ChunkKey key, long tileKey, byte grassLodTier)
        {
            if (state == null ||
                !TryGetActiveTileCache(state, out NativeArray<byte> sandMask, out NativeArray<byte> rockMask, out NativeArray<ushort> heightSamples) ||
                state.AlphamapResolution <= 0 ||
                state.HeightmapResolution <= 1)
            {
                return false;
            }

            if (!_nativeMemory.TerrainHoleRecordsNative.IsCreated)
                SyncTerrainHoleNativeCache();

            ChunkPayload payloadHeader = CreateChunkPayloadHeader(state, key.ChunkX, key.ChunkZ);
            payloadHeader.GrassLodTier = grassLodTier;
            bool isCorrupted = IsChunkCorrupted(key);
            payloadHeader.CorruptionState = isCorrupted ? (byte)1 : (byte)0;

            GetChunkBounds(state, key.ChunkX, key.ChunkZ, out float minX, out float maxX, out float minZ, out float maxZ);
            float chunkWidth = math.max(0.01f, maxX - minX);
            float chunkDepth = math.max(0.01f, maxZ - minZ);
            float grassStep = GetGrassStepForTier(grassLodTier);
            int grassCountX = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkWidth / grassStep));
            int grassCountZ = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkDepth / grassStep));
            int kelpCountX = Mathf.Max(1, Mathf.CeilToInt(chunkWidth / kelpStepMeters));
            int kelpCountZ = Mathf.Max(1, Mathf.CeilToInt(chunkDepth / kelpStepMeters));
            int floatingCountX = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkWidth / floatingStepMeters));
            int floatingCountZ = isCorrupted ? 0 : Mathf.Max(1, Mathf.CeilToInt(chunkDepth / floatingStepMeters));

            ChunkBuildJobState jobState = new ChunkBuildJobState
            {
                Key = key,
                TileKey = tileKey,
                TileCacheRevision = state.CacheRevision,
                GrassLodTier = grassLodTier,
                CorruptionState = payloadHeader.CorruptionState,
                PayloadHeader = payloadHeader,
                GrassRecords = AllocateJobRecordArray(grassCountX * grassCountZ),
                FloatingRecords = AllocateJobRecordArray(floatingCountX * floatingCountZ),
                KelpRecords = AllocateJobRecordArray(kelpCountX * kelpCountZ)
            };

            float3 terrainPosition = new float3(state.TerrainPosition.x, state.TerrainPosition.y, state.TerrainPosition.z);
            float3 terrainSize = new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z);
            JobHandle grassHandle = default;
            JobHandle kelpHandle = default;
            JobHandle floatingHandle = default;

            if (jobState.GrassRecords.IsCreated && jobState.GrassRecords.Length > 0)
            {
                var grassJob = new GenerateAnchoredVegetationJob
                {
                    SandMask = sandMask,
                    RockMask = rockMask,
                    HeightSamples = heightSamples,
                    TerrainHoles = _nativeMemory.TerrainHoleRecordsNative,
                    ThreatEchoFlags = _nativeMemory.EcosystemThreatEchoCurrentNative,
                    ArtificialStructures = _nativeMemory.ArtificialStructureRecordsNative,
                    ArtificialStructureHash = _nativeMemory.ArtificialStructureHashFrontNative,
                    TerrainHoleCount = _terrainHoleCount,
                    Output = jobState.GrassRecords,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightResolution = state.HeightmapResolution,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    StepX = chunkWidth / grassCountX,
                    StepZ = chunkDepth / grassCountZ,
                    SampleCountX = grassCountX,
                    TileX = key.TileX,
                    TileZ = key.TileZ,
                    ChunkX = key.ChunkX,
                    ChunkZ = key.ChunkZ,
                    SampleSeedOffset = 0,
                    JitterFraction = grassJitterFraction,
                    SandMaskThreshold = _sandMaskThresholdByte,
                    RockMaskThreshold = _rockMaskThresholdByte,
                    MinimumNormalY = minimumNormalY,
                    NormalOffset = normalOffset,
                    MinWorldYExclusive = waterLevel,
                    MaxWorldYExclusive = float.MaxValue,
                    EdgeDitherDistance = edgeDitherDistance,
                    ScaleMin = grassScaleRange.x,
                    ScaleMax = grassScaleRange.y,
                    HeightScaleMin = 0.35f,
                    HeightScaleMax = 0.8f,
                    WidthScaleMin = 0.8f,
                    WidthScaleMax = 1.1f,
                    TypeId = (int)HectonVegetationInstanceType.Grass,
                    OrganicSemanticType = (int)VegetationSemanticType.OrganicGrass,
                    ColonyCableSemanticType = (int)VegetationSemanticType.ColonyCable,
                    ColonyHullSemanticType = (int)VegetationSemanticType.ColonyHullPlating,
                    ColonyBeamSemanticType = (int)VegetationSemanticType.ColonySupportBeam,
                    DeadZoneSemanticType = (int)VegetationSemanticType.DeadZoneMassiveStructure,
                    WaterLevel = waterLevel,
                    ColonyBiomeStartDepth = colonyBiomeStartDepth,
                    DeadZoneStartDepth = deadZoneStartDepth,
                    VerticalBiomeBlendBand = verticalBiomeBlendBand,
                    TechnoJungleThreshold = technoJungleThreshold,
                    TechnoJungleCellSize = technoJungleCellSize,
                    TechnoJungleSecondaryCellSize = technoJungleSecondaryCellSize,
                    TechnoJungleWallWidth = technoJungleWallWidth,
                    TechnoJungleWarpMeters = technoJungleWarpMeters,
                    TechnoJungleFlowAnisotropy = technoJungleFlowAnisotropy,
                    DeadZoneStructureChance = deadZoneStructureChance,
                    DeadZoneDensityScale = deadZoneDensityScale,
                    AbyssalFlowNoiseScale = abyssalFlowNoiseScale,
                    AbyssalFlowNoiseStrength = abyssalFlowNoiseStrength,
                    AbyssalFlowVerticalStrength = abyssalFlowVerticalStrength,
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    EchoTechnoJungleThresholdBias = 0f,
                    EchoDeadZoneKeepBoost = 0f,
                    IgnorePlacementMasks = 0,
                    CorruptionMode = 0,
                    EnableVerticalBiomeRewrite = 0,
                    ScaleSalt = 0x85EBCA6Bu,
                    WidthSalt = 0xC2B2AE35u,
                    ScaleJitter = proceduralScaleJitter,
                    RotationJitterRadians = proceduralRotationJitterDegrees * Mathf.Deg2Rad,
                    RotationSalt = 0xA24BAEDCu
                };

                grassHandle = grassJob.Schedule(jobState.GrassRecords.Length, DefaultJobBatchSize);
            }

            if (jobState.KelpRecords.IsCreated && jobState.KelpRecords.Length > 0)
            {
                var kelpJob = new GenerateAnchoredVegetationJob
                {
                    SandMask = sandMask,
                    RockMask = rockMask,
                    HeightSamples = heightSamples,
                    TerrainHoles = _nativeMemory.TerrainHoleRecordsNative,
                    ThreatEchoFlags = _nativeMemory.EcosystemThreatEchoCurrentNative,
                    ArtificialStructures = _nativeMemory.ArtificialStructureRecordsNative,
                    ArtificialStructureHash = _nativeMemory.ArtificialStructureHashFrontNative,
                    TerrainHoleCount = _terrainHoleCount,
                    Output = jobState.KelpRecords,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightResolution = state.HeightmapResolution,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    StepX = chunkWidth / kelpCountX,
                    StepZ = chunkDepth / kelpCountZ,
                    SampleCountX = kelpCountX,
                    TileX = key.TileX,
                    TileZ = key.TileZ,
                    ChunkX = key.ChunkX,
                    ChunkZ = key.ChunkZ,
                    SampleSeedOffset = 0x4000,
                    JitterFraction = kelpJitterFraction,
                    SandMaskThreshold = _sandMaskThresholdByte,
                    RockMaskThreshold = _rockMaskThresholdByte,
                    MinimumNormalY = minimumNormalY,
                    NormalOffset = normalOffset,
                    MinWorldYExclusive = float.NegativeInfinity,
                    MaxWorldYExclusive = waterLevel,
                    EdgeDitherDistance = edgeDitherDistance,
                    ScaleMin = kelpScaleRange.x,
                    ScaleMax = kelpScaleRange.y,
                    HeightScaleMin = 0.25f,
                    HeightScaleMax = 1f,
                    WidthScaleMin = 0.65f,
                    WidthScaleMax = 1.1f,
                    TypeId = (int)HectonVegetationInstanceType.GiantKelp,
                    OrganicSemanticType = (int)VegetationSemanticType.OrganicKelp,
                    ColonyCableSemanticType = (int)VegetationSemanticType.ColonyCable,
                    ColonyHullSemanticType = (int)VegetationSemanticType.ColonyHullPlating,
                    ColonyBeamSemanticType = (int)VegetationSemanticType.ColonySupportBeam,
                    DeadZoneSemanticType = (int)VegetationSemanticType.DeadZoneMassiveStructure,
                    WaterLevel = waterLevel,
                    ColonyBiomeStartDepth = colonyBiomeStartDepth,
                    DeadZoneStartDepth = deadZoneStartDepth,
                    VerticalBiomeBlendBand = verticalBiomeBlendBand,
                    TechnoJungleThreshold = technoJungleThreshold,
                    TechnoJungleCellSize = technoJungleCellSize,
                    TechnoJungleSecondaryCellSize = technoJungleSecondaryCellSize,
                    TechnoJungleWallWidth = technoJungleWallWidth,
                    TechnoJungleWarpMeters = technoJungleWarpMeters,
                    TechnoJungleFlowAnisotropy = technoJungleFlowAnisotropy,
                    DeadZoneStructureChance = deadZoneStructureChance,
                    DeadZoneDensityScale = deadZoneDensityScale,
                    AbyssalFlowNoiseScale = abyssalFlowNoiseScale,
                    AbyssalFlowNoiseStrength = abyssalFlowNoiseStrength,
                    AbyssalFlowVerticalStrength = abyssalFlowVerticalStrength,
                    ThreatGridCenter = new float3(_ecosystemThreatGridCenter.x, _ecosystemThreatGridCenter.y, _ecosystemThreatGridCenter.z),
                    ThreatGridCellSize = threatGridCellSize,
                    ThreatGridResolution = _ecosystemThreatGridResolution,
                    EchoTechnoJungleThresholdBias = permanentEchoTechnoJungleThresholdBias,
                    EchoDeadZoneKeepBoost = permanentEchoDeadZoneKeepBoost,
                    IgnorePlacementMasks = isCorrupted ? 1 : 0,
                    CorruptionMode = isCorrupted ? 1 : 0,
                    EnableVerticalBiomeRewrite = isCorrupted ? 0 : 1,
                    ScaleSalt = 0x27D4EB2Fu,
                    WidthSalt = 0x165667B1u,
                    ScaleJitter = proceduralScaleJitter,
                    RotationJitterRadians = proceduralRotationJitterDegrees * Mathf.Deg2Rad,
                    RotationSalt = 0x94D049BBu
                };

                kelpHandle = kelpJob.Schedule(jobState.KelpRecords.Length, DefaultJobBatchSize);
            }

            if (jobState.FloatingRecords.IsCreated && jobState.FloatingRecords.Length > 0)
            {
                var floatingJob = new GenerateFloatingVegetationJob
                {
                    SandMask = sandMask,
                    RockMask = rockMask,
                    HeightSamples = heightSamples,
                    TerrainHoles = _nativeMemory.TerrainHoleRecordsNative,
                    TerrainHoleCount = _terrainHoleCount,
                    Output = jobState.FloatingRecords,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightResolution = state.HeightmapResolution,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    StepX = chunkWidth / floatingCountX,
                    StepZ = chunkDepth / floatingCountZ,
                    SampleCountX = floatingCountX,
                    TileX = key.TileX,
                    TileZ = key.TileZ,
                    ChunkX = key.ChunkX,
                    ChunkZ = key.ChunkZ,
                    SampleSeedOffset = 0x8000,
                    JitterFraction = floatingJitterFraction,
                    SandMaskThreshold = _sandMaskThresholdByte,
                    RockMaskThreshold = _rockMaskThresholdByte,
                    MinimumNormalY = minimumNormalY,
                    WaterLevel = waterLevel,
                    FloatingSurfaceOffset = floatingSurfaceOffset,
                    FloatingSurfaceBand = floatingSurfaceBand,
                    EdgeDitherDistance = edgeDitherDistance,
                    ScaleMin = floatingScaleRange.x,
                    ScaleMax = floatingScaleRange.y,
                    FloatingPatchThreshold = floatingPatchThreshold,
                    FloatingPatchNoiseScale = floatingPatchNoiseScale,
                    FloatingCellSize = floatingCellSize,
                    FloatingSecondaryCellSize = floatingSecondaryCellSize,
                    FloatingWallWidth = floatingWallWidth,
                    FloatingWarpMeters = floatingWarpMeters,
                    FloatingFlowDirection = new float2(_floatingFlowDirectionNormalized.x, _floatingFlowDirectionNormalized.y),
                    FloatingFlowAnisotropy = floatingFlowAnisotropy,
                    ScaleJitter = proceduralScaleJitter,
                    RotationJitterRadians = proceduralRotationJitterDegrees * Mathf.Deg2Rad,
                    RotationSalt = 0xC13FA9A9u
                };

                floatingHandle = floatingJob.Schedule(jobState.FloatingRecords.Length, DefaultJobBatchSize);
            }

            jobState.Handle = JobHandle.CombineDependencies(grassHandle, kelpHandle, floatingHandle);
            _chunkBuildJobs[key] = jobState;
            return true;
        }

        private int FinalizeCompletedChunkBuilds()
        {
            if (_chunkBuildJobs.Count == 0)
                return 0;

            _jobScratchKeys.Clear();
            Dictionary<ChunkKey, ChunkBuildJobState>.Enumerator enumerator = _chunkBuildJobs.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ChunkBuildJobState jobState = enumerator.Current.Value;
                if (jobState != null && jobState.Handle.IsCompleted)
                    _jobScratchKeys.Add(enumerator.Current.Key);
            }

            int completedCount = 0;
            for (int i = 0; i < _jobScratchKeys.Count; i++)
            {
                ChunkKey key = _jobScratchKeys[i];
                if (!_chunkBuildJobs.TryGetValue(key, out ChunkBuildJobState jobState) || jobState == null)
                    continue;

                DispatcherJobSwap.TryComplete(ref jobState.Handle, forceComplete: false);
                if (!jobState.CancelRequested && IsJobStateCurrent(jobState))
                {
                    ReleaseChunkPayloadStorage(key);
                    ChunkPayload payload = BuildChunkPayloadFromJob(jobState);
                    _chunkPayloads[key] = payload;
                    CacheChunkAbyssalNavPayload(key, BuildChunkAbyssalNavPayload(key, jobState, payload));
                    CacheChunkMegaWreckPayload(key, BuildChunkMegaWreckPayload(payload));
                    RegisterChunkPayloadStorage(payload);
                    completedCount++;
                }
                else if (TryGetDesiredChunkPriority(key, out float priority))
                {
                    EnqueuePendingChunk(key, priority);
                }

                DisposeJobState(jobState);
                _chunkBuildJobs.Remove(key);
            }

            if (completedCount > 0)
                _activeSetDirty = true;

            return completedCount;
        }

        private bool IsJobStateCurrent(ChunkBuildJobState jobState)
        {
            if (jobState == null)
                return false;

            if (!_tileStates.TryGetValue(jobState.TileKey, out TileRuntimeState state) || state == null)
                return false;

            return state.CacheRevision == jobState.TileCacheRevision;
        }

        private ChunkPayload BuildChunkPayloadFromJob(ChunkBuildJobState jobState)
        {
            ChunkPayload payload = jobState.PayloadHeader;
            payload.GrassLodTier = jobState.GrassLodTier;
            payload.CorruptionState = jobState.CorruptionState;

            int grassCount = CountValidRecords(jobState.GrassRecords);
            int floatingCount = CountValidRecords(jobState.FloatingRecords);
            int kelpCount = CountValidRecords(jobState.KelpRecords);
            int surfaceCount = grassCount + floatingCount;

            if (surfaceCount > 0)
            {
                if (TryAllocateChunkSliceForWrite(isSurface: true, surfaceCount, out int surfaceOffset, out bool useScratchPool))
                {
                    payload.SurfaceOffset = surfaceOffset;
                    payload.SurfaceCount = surfaceCount;
                    payload.SurfaceEdgeOffset = surfaceOffset;
                    payload.SurfacePoolSet = useScratchPool ? (byte)1 : (byte)0;
                    int writeIndex = surfaceOffset;
                    if (useScratchPool)
                    {
                        WriteJobRecordsToPool(jobState.GrassRecords, ref _surfaceDefragScratchPool, ref writeIndex, _totalUniverseOffset, floraTemplates, _floraTemplateRuntimeDescriptors);
                        WriteJobRecordsToPool(jobState.FloatingRecords, ref _surfaceDefragScratchPool, ref writeIndex, _totalUniverseOffset, floraTemplates, _floraTemplateRuntimeDescriptors);
                    }
                    else
                    {
                        WriteJobRecordsToPool(jobState.GrassRecords, ref _surfaceChunkPool, ref writeIndex, _totalUniverseOffset, floraTemplates, _floraTemplateRuntimeDescriptors);
                        WriteJobRecordsToPool(jobState.FloatingRecords, ref _surfaceChunkPool, ref writeIndex, _totalUniverseOffset, floraTemplates, _floraTemplateRuntimeDescriptors);
                    }
                }
            }

            if (kelpCount > 0)
            {
                if (TryAllocateChunkSliceForWrite(isSurface: false, kelpCount, out int underwaterOffset, out bool useScratchPool))
                {
                    payload.UnderwaterOffset = underwaterOffset;
                    payload.UnderwaterCount = kelpCount;
                    payload.UnderwaterEdgeOffset = underwaterOffset;
                    payload.UnderwaterPoolSet = useScratchPool ? (byte)1 : (byte)0;
                    int writeIndex = underwaterOffset;
                    if (useScratchPool)
                        WriteJobRecordsToPool(jobState.KelpRecords, ref _underwaterDefragScratchPool, ref writeIndex, _totalUniverseOffset, floraTemplates, _floraTemplateRuntimeDescriptors);
                    else
                        WriteJobRecordsToPool(jobState.KelpRecords, ref _underwaterChunkPool, ref writeIndex, _totalUniverseOffset, floraTemplates, _floraTemplateRuntimeDescriptors);
                }
            }

            return payload;
        }

        private void RegisterChunkPayloadStorage(ChunkPayload payload)
        {
            _chunkPayloadUsedBytes += GetChunkPayloadStorageBytes(payload);
        }

        private static int CountValidRecords(NativeArray<JobInstanceRecord> records)
        {
            if (!records.IsCreated)
                return 0;

            int count = 0;
            for (int i = 0; i < records.Length; i++)
            {
                if (records[i].IsValid != 0)
                    count++;
            }

            return count;
        }

        private static void WriteJobRecordsToPool(
            NativeArray<JobInstanceRecord> source,
            ref NativeChunkPool pool,
            ref int writeIndex,
            Vector3 universeOffset,
            FloraDataTemplate[] floraTemplates,
            FloraDataTemplate.RuntimeDescriptor[] floraTemplateRuntimeDescriptors)
        {
            if (!source.IsCreated)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                JobInstanceRecord record = source[i];
                if (record.IsValid == 0)
                    continue;

                ResolveFloraDescriptor(
                    floraTemplates,
                    floraTemplateRuntimeDescriptors,
                    record.Type,
                    record.SemanticType,
                    record.BiomeLayer,
                    record.Variation,
                    out int floraTemplateIndex,
                    out FloraDataTemplate.RuntimeDescriptor floraDescriptor);
                byte geneticTraits = ResolveGeneticTraitByte(floraTemplates, floraTemplateIndex, floraDescriptor);
                pool.Matrices[writeIndex] = ConvertMatrixToStableUniverseSpace(ToMatrix4x4(record.Matrix), universeOffset);
                pool.Metadata[writeIndex] = new HectonVegetationInstanceData(
                    (HectonVegetationInstanceType)record.Type,
                    record.HeightScale,
                    record.WidthScale,
                    ResolveDeterministicVatPhase01(record.Variation, record.Type, record.SemanticType, record.BiomeLayer),
                    floraTemplateIndex,
                    HectonVegetationInstanceData.RuntimeStateIdle,
                    HectonVegetationRuntimeFlagEncoding.Encode(record.BiomeLayer, 0, geneticTraits),
                    floraDescriptor.PulseFrequency,
                    new Vector4(
                        floraDescriptor.BioluminescenceColor.x,
                        floraDescriptor.BioluminescenceColor.y,
                        floraDescriptor.BioluminescenceColor.z,
                        floraDescriptor.BioluminescenceColor.w),
                    floraDescriptor.SwaySpeed,
                    floraDescriptor.BendAmplitude,
                    1f,
                    0f);
                pool.Types[writeIndex] = record.Type;
                pool.SemanticTypes[writeIndex] = record.SemanticType;
                pool.BiomeLayers[writeIndex] = record.BiomeLayer;
                pool.EdgeDistances[writeIndex] = record.EdgeDistance;
                pool.FlowDirections[writeIndex] = new Vector2(record.FlowDirection.x, record.FlowDirection.y);
                pool.FlowVectors[writeIndex] = new Vector3(record.FlowVector.x, record.FlowVector.y, record.FlowVector.z);
                writeIndex++;
            }
        }

        private static float ResolveDeterministicVatPhase01(
            float instanceVariation,
            int type,
            int semanticType,
            byte biomeLayer)
        {
            uint variationBits = math.asuint(math.frac(math.saturate(instanceVariation)));
            uint phaseHash = math.hash(new uint4(
                variationBits,
                unchecked((uint)type),
                unchecked((uint)semanticType),
                biomeLayer));
            return (phaseHash & 0x00FFFFFFu) / 16777215f;
        }

        private static byte ResolveGeneticTraitByte(
            FloraDataTemplate[] floraTemplates,
            int floraTemplateIndex,
            FloraDataTemplate.RuntimeDescriptor descriptor)
        {
            byte geneticTraits = 0;
            ulong authoredGenetics = 0UL;
            FloraDataTemplate.FloraCategory category = FloraDataTemplate.FloraCategory.MicroGrass;
            if (floraTemplates != null && floraTemplateIndex >= 0 && floraTemplateIndex < floraTemplates.Length && floraTemplates[floraTemplateIndex] != null)
            {
                FloraDataTemplate template = floraTemplates[floraTemplateIndex];
                authoredGenetics = template.GeneticsMask;
                category = template.Category;
            }

            if ((authoredGenetics & (ulong)GeneticTraitProfile.GeneticTraitMask.Toxic) != 0UL)
                geneticTraits |= (byte)HectonVegetationGeneticTraits.Poisonous;

            if ((authoredGenetics & (ulong)GeneticTraitProfile.GeneticTraitMask.Medicinal) != 0UL ||
                category == FloraDataTemplate.FloraCategory.HarvestableKelp ||
                category == FloraDataTemplate.FloraCategory.GiantSargassum)
            {
                geneticTraits |= (byte)HectonVegetationGeneticTraits.Edible;
            }

            if ((authoredGenetics & (ulong)GeneticTraitProfile.GeneticTraitMask.Bioluminescent) != 0UL ||
                descriptor.BioluminescenceColor.w > 0.001f)
            {
                geneticTraits |= (byte)HectonVegetationGeneticTraits.EmitsLight;
            }

            return geneticTraits;
        }

        private static void ResolveFloraDescriptor(
            FloraDataTemplate[] floraTemplates,
            FloraDataTemplate.RuntimeDescriptor[] floraTemplateRuntimeDescriptors,
            int type,
            int semanticType,
            byte biomeLayer,
            float variation,
            out int templateIndex,
            out FloraDataTemplate.RuntimeDescriptor descriptor)
        {
            templateIndex = -1;
            descriptor = ResolveFallbackFloraDescriptor(type);
            if (floraTemplates == null || floraTemplateRuntimeDescriptors == null)
                return;

            HectonVegetationInstanceType vegetationType = (HectonVegetationInstanceType)type;
            VegetationSemanticType semantic = (VegetationSemanticType)semanticType;
            VegetationBiomeLayer biome = (VegetationBiomeLayer)biomeLayer;
            FloraDataTemplate.AttachmentSurface requiredAttachmentSurface = ResolveAttachmentSurfaceForSemantic(semantic);
            int candidateCount = 0;
            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null ||
                    template.VegetationType != vegetationType ||
                    template.SemanticType != semantic ||
                    template.BiomeLayer != biome ||
                    !DoesTemplateAttachmentMatch(template.AttachmentSurfaceType, requiredAttachmentSurface))
                {
                    continue;
                }

                candidateCount++;
            }

            if (candidateCount <= 0)
                return;

            int selectedOrdinal = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(variation, 1f) * candidateCount), 0, candidateCount - 1);
            int currentOrdinal = 0;
            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null ||
                    template.VegetationType != vegetationType ||
                    template.SemanticType != semantic ||
                    template.BiomeLayer != biome ||
                    !DoesTemplateAttachmentMatch(template.AttachmentSurfaceType, requiredAttachmentSurface))
                {
                    continue;
                }

                if (currentOrdinal == selectedOrdinal)
                {
                    templateIndex = i;
                    if (i >= 0 && i < floraTemplateRuntimeDescriptors.Length)
                        descriptor = floraTemplateRuntimeDescriptors[i];
                    return;
                }

                currentOrdinal++;
            }
        }

        private static bool DoesTemplateAttachmentMatch(
            FloraDataTemplate.AttachmentSurface templateAttachmentSurface,
            FloraDataTemplate.AttachmentSurface requiredAttachmentSurface)
        {
            return templateAttachmentSurface == FloraDataTemplate.AttachmentSurface.Any ||
                   templateAttachmentSurface == requiredAttachmentSurface;
        }

        private static FloraDataTemplate.AttachmentSurface ResolveAttachmentSurfaceForSemantic(VegetationSemanticType semanticType)
        {
            if (IsSolidStructureSemanticType(semanticType))
                return FloraDataTemplate.AttachmentSurface.Metal;

            return semanticType == VegetationSemanticType.FloatingSargassum
                ? FloraDataTemplate.AttachmentSurface.Any
                : FloraDataTemplate.AttachmentSurface.Seabed;
        }

        private static FloraDataTemplate.RuntimeDescriptor ResolveFallbackFloraDescriptor(int type)
        {
            Vector4 color = type switch
            {
                (int)HectonVegetationInstanceType.GiantKelp => new Vector4(0.11f, 0.52f, 0.47f, 0.42f),
                (int)HectonVegetationInstanceType.Sargassum => new Vector4(0.08f, 0.42f, 0.38f, 0.26f),
                _ => new Vector4(0.10f, 0.48f, 0.34f, 0.22f)
            };
            return new FloraDataTemplate.RuntimeDescriptor
            {
                StableHashId = 0,
                LootHashId = 0,
                VulnerabilityMask = 0u,
                AudioMaterialId = (uint)FloraDataTemplate.AudioMaterialId.Organic,
                BioluminescenceColor = new float4(color.x, color.y, color.z, color.w),
                PulseFrequency = 0.55f,
                HarvestTemplateStableHashId = 0,
                AttachmentSurface = (uint)FloraDataTemplate.AttachmentSurface.Any,
                SwaySpeed = type == (int)HectonVegetationInstanceType.Grass ? 1.35f : (type == (int)HectonVegetationInstanceType.GiantKelp ? 0.62f : 0.78f),
                BendAmplitude = type == (int)HectonVegetationInstanceType.Grass ? 0.72f : (type == (int)HectonVegetationInstanceType.GiantKelp ? 1.18f : 0.94f),
                Reserved0 = 0u
            };
        }

        private static void CopyChunkSliceToAggregate(
            NativeChunkPool pool,
            int sourceOffset,
            ActiveAggregateNativeBufferSet destinationBuffers,
            int destinationOffset,
            int copyCount)
        {
            NativeArray<Matrix4x4>.Copy(pool.Matrices, sourceOffset, destinationBuffers.Matrices, destinationOffset, copyCount);
            NativeArray<HectonVegetationInstanceData>.Copy(pool.Metadata, sourceOffset, destinationBuffers.Metadata, destinationOffset, copyCount);
            NativeArray<int>.Copy(pool.Types, sourceOffset, destinationBuffers.Types, destinationOffset, copyCount);
            NativeArray<int>.Copy(pool.SemanticTypes, sourceOffset, destinationBuffers.SemanticTypes, destinationOffset, copyCount);
            NativeArray<byte>.Copy(pool.BiomeLayers, sourceOffset, destinationBuffers.BiomeLayers, destinationOffset, copyCount);
            NativeArray<Vector2>.Copy(pool.FlowDirections, sourceOffset, destinationBuffers.FlowDirections, destinationOffset, copyCount);
            NativeArray<Vector3>.Copy(pool.FlowVectors, sourceOffset, destinationBuffers.FlowVectors, destinationOffset, copyCount);
        }
    }
}
