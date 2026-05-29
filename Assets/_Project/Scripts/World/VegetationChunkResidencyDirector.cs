using System;
using Hecton8.Core;
using Hecton8.Core.Memory;
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
            float planarVelocityX = _playerVelocity.x;
            float planarVelocityZ = _playerVelocity.z;
            float planarSpeedSq = (planarVelocityX * planarVelocityX) + (planarVelocityZ * planarVelocityZ);
            float predictiveMinSpeedSq = predictiveMinSpeed * predictiveMinSpeed;
            bool usePredictiveResidency = planarSpeedSq >= predictiveMinSpeedSq;
            float invPlanarSpeed = usePredictiveResidency ? math.rsqrt(math.max(planarSpeedSq, 0.000001f)) : 0f;
            float planarSpeed = usePredictiveResidency ? planarSpeedSq * invPlanarSpeed : 0f;
            Vector2 forward = usePredictiveResidency
                ? new Vector2(planarVelocityX * invPlanarSpeed, planarVelocityZ * invPlanarSpeed)
                : Vector2.right;
            Vector2 right = new Vector2(-forward.y, forward.x);
            float forwardRadius = residentRadius + Mathf.Min(predictiveLeadMaxMeters, planarSpeed * predictiveLeadSeconds);
            float rearRadius = residentRadius * rearResidencyScale;
            float lateralRadius = residentRadius * lateralResidencyScale;
            float searchRadius = usePredictiveResidency ? Mathf.Max(forwardRadius, Mathf.Max(rearRadius, lateralRadius)) : residentRadius;
            FixedTileStateMap.Enumerator enumerator = _tileStates.GetEnumerator();
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
                        bool corruptionMismatch = hasPayload && payload.IsCorrupted != shouldBeCorrupted;
                        if (!hasPayload || corruptionMismatch)
                        {
                            EnqueuePendingChunk(key, priority);
                        }
                        else if (hasPayload && !payload.IsCorrupted && payload.GrassLodTier != desiredGrassLodTier)
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
                if (!HasAvailableChunkBuildJobSlot())
                    break;

                ChunkKey key = _pendingChunkKeys[0];
                DequeuePendingChunkAt(0);

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

                if (_selectedChunkKeys == null || nextSelectedCount >= _selectedChunkKeys.Length)
                {
                    RecordChunkQueueCapacityExceeded(
                        _selectedChunkKeys != null ? _selectedChunkKeys.Length : 0,
                        nextSelectedCount);
                    break;
                }

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
            Camera activeViewCamera = RefreshActiveViewCameraCache();
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
                if (!EnsureActiveAggregateBufferCapacity(
                        ref _surfaceAggregateBackBuffers,
                        ResolveSurfaceAggregateMatrixBufferId(_surfaceBackBufferIndex),
                        ResolveSurfaceAggregateMatrixDirtyPageBufferId(_surfaceBackBufferIndex),
                        ResolveSurfaceAggregateMetadataDirtyPageBufferId(_surfaceBackBufferIndex),
                        totalSurfaceCount))
                {
                    return false;
                }

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasSurface)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.SurfaceCount;
                    if (!CopyChunkSliceToAggregate(
                        ResolveChunkPool(isSurface: true, payload),
                        payload.SurfaceOffset,
                        ref _surfaceAggregateBackBuffers,
                        writeIndex,
                        copyCount))
                    {
                        return false;
                    }

                    writeIndex += copyCount;
                }

                if (!MarkActiveAggregateDirtyPagesOneLock(ref _surfaceAggregateBackBuffers.MatrixDirtyPagesHandle, totalSurfaceCount) ||
                    !MarkActiveAggregateDirtyPagesOneLock(ref _surfaceAggregateBackBuffers.MetadataDirtyPagesHandle, totalSurfaceCount))
                    return false;

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
                if (!EnsureActiveAggregateBufferCapacity(
                        ref _underwaterAggregateBackBuffers,
                        ResolveUnderwaterAggregateMatrixBufferId(_underwaterBackBufferIndex),
                        ResolveUnderwaterAggregateMatrixDirtyPageBufferId(_underwaterBackBufferIndex),
                        ResolveUnderwaterAggregateMetadataDirtyPageBufferId(_underwaterBackBufferIndex),
                        totalUnderwaterCount))
                {
                    return false;
                }

                int writeIndex = 0;
                for (int i = 0; i < _selectedChunkCount; i++)
                {
                    if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload) || !payload.HasUnderwater)
                        continue;
                    if (!_selectedChunkVisibility[i])
                        continue;

                    int copyCount = payload.UnderwaterCount;
                    if (!CopyChunkSliceToAggregate(
                        ResolveChunkPool(isSurface: false, payload),
                        payload.UnderwaterOffset,
                        ref _underwaterAggregateBackBuffers,
                        writeIndex,
                        copyCount))
                    {
                        return false;
                    }

                    writeIndex += copyCount;
                }

                if (!MarkActiveAggregateDirtyPagesOneLock(ref _underwaterAggregateBackBuffers.MatrixDirtyPagesHandle, totalUnderwaterCount) ||
                    !MarkActiveAggregateDirtyPagesOneLock(ref _underwaterAggregateBackBuffers.MetadataDirtyPagesHandle, totalUnderwaterCount))
                    return false;

                DistortAggregateFlowVectorsByThreat(ref _underwaterAggregateBackBuffers, totalUnderwaterCount);
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

            TouchTileCacheState(state);
            if (!TryCopyChunkTileCacheForJob(
                    sandMask,
                    rockMask,
                    heightSamples,
                    out NativeArray<byte> sandMaskForJob,
                    out NativeArray<byte> rockMaskForJob,
                    out NativeArray<ushort> heightSamplesForJob))
            {
                return false;
            }

            if (_terrainHoleCount > 0 && _nativeMemory.TerrainHoleRecordsHandle.BufferID == 0u)
                SyncTerrainHoleNativeCache();
            if (!TryCreateTerrainHoleJobSnapshot(out NativeArray<TerrainHoleRecord> terrainHoles))
            {
                H8Memory.Release(ref sandMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref rockMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref heightSamplesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                return false;
            }
            int terrainHoleCountForJob = terrainHoles.IsCreated ? terrainHoles.Length : 0;
            if (!TryPrepareArtificialStructureJobSnapshot(out NativeArray<ArtificialStructureRecord> artificialStructures))
            {
                H8Memory.Release(ref sandMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref rockMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref heightSamplesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref terrainHoles, VegetationMemorySovereigntyConstants.OwnerSystemId);
                return false;
            }

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
                PayloadHeader = payloadHeader
            };

            NativeArray<JobInstanceRecord> grassRecords = AllocateJobRecordArray(grassCountX * grassCountZ);
            NativeArray<JobInstanceRecord> floatingRecords = AllocateJobRecordArray(floatingCountX * floatingCountZ);
            NativeArray<JobInstanceRecord> kelpRecords = AllocateJobRecordArray(kelpCountX * kelpCountZ);

            float3 terrainPosition = new float3(state.TerrainPosition.x, state.TerrainPosition.y, state.TerrainPosition.z);
            float3 terrainSize = new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z);
            NativeArray<byte> threatEchoFlags = default;
            if (!_threatPropagationScheduled &&
                _ecosystemThreatGridCellCount > 0 &&
                TryReadVegetationMemoryBuffer(
                    in _nativeMemory.EcosystemThreatEchoHandle,
                    BufferID.VegetationEcosystemThreatEcho,
                    _ecosystemThreatGridCellCount,
                    out NativeArray<byte> echoFlags))
            {
                threatEchoFlags = H8Memory.Allocate<byte>(
                    _ecosystemThreatGridCellCount,
                    VegetationMemorySovereigntyConstants.OwnerSystemId,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);
                if (threatEchoFlags.IsCreated && threatEchoFlags.Length >= _ecosystemThreatGridCellCount)
                    NativeArray<byte>.Copy(echoFlags, threatEchoFlags, _ecosystemThreatGridCellCount);
                else
                    H8Memory.Release(ref threatEchoFlags, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
            bool hasBuiltAnyChunkSlice = false;
            bool scheduled = false;
            int jobSlot = -1;
            try
            {
                if (!IsJobStateCurrent(jobState) || !TryAcquireChunkBuildJobSlot(out jobSlot))
                    return false;

                JobHandle buildHandle = default;
                if (grassRecords.IsCreated && grassRecords.Length > 0)
                {
                    var grassJob = new GenerateAnchoredVegetationJob
                    {
                        SandMask = sandMaskForJob,
                        RockMask = rockMaskForJob,
                        HeightSamples = heightSamplesForJob,
                        TerrainHoles = terrainHoles,
                        ThreatEchoFlags = threatEchoFlags,
                        ArtificialStructures = artificialStructures,
                        ArtificialStructureHash = default,
                        TerrainHoleCount = terrainHoleCountForJob,
                        Output = grassRecords,
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
                        ApplyOrganicKelpPlacementRules = 0,
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
                        RotationSalt = 0xA24BAEDCu
                    };

                    buildHandle = CombineOptionalHandles(
                        buildHandle,
                        grassJob.Schedule(grassRecords.Length, DefaultJobBatchSize));
                    hasBuiltAnyChunkSlice = true;
                }

                if (kelpRecords.IsCreated && kelpRecords.Length > 0)
                {
                    var kelpJob = new GenerateAnchoredVegetationJob
                    {
                        SandMask = sandMaskForJob,
                        RockMask = rockMaskForJob,
                        HeightSamples = heightSamplesForJob,
                        TerrainHoles = terrainHoles,
                        ThreatEchoFlags = threatEchoFlags,
                        ArtificialStructures = artificialStructures,
                        ArtificialStructureHash = default,
                        TerrainHoleCount = terrainHoleCountForJob,
                        Output = kelpRecords,
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
                        ApplyOrganicKelpPlacementRules = 1,
                        OrganicKelpMaxDepthBelowSurface = math.min(
                            OrganicKelpMaxDepthBelowSurfaceMeters,
                            math.max(0f, waterLevel - kelpMinHeight)),
                        OrganicKelpMinimumNormalY = OrganicKelpMaxSlopeNormalY,
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
                        RotationSalt = 0x94D049BBu
                    };

                    buildHandle = CombineOptionalHandles(
                        buildHandle,
                        kelpJob.Schedule(kelpRecords.Length, DefaultJobBatchSize));
                    hasBuiltAnyChunkSlice = true;
                }

                if (floatingRecords.IsCreated && floatingRecords.Length > 0)
                {
                    var floatingJob = new GenerateFloatingVegetationJob
                    {
                        SandMask = sandMaskForJob,
                        RockMask = rockMaskForJob,
                        HeightSamples = heightSamplesForJob,
                        TerrainHoles = terrainHoles,
                        TerrainHoleCount = terrainHoleCountForJob,
                        Output = floatingRecords,
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
                        RotationSalt = 0xC13FA9A9u
                    };

                    buildHandle = CombineOptionalHandles(
                        buildHandle,
                        floatingJob.Schedule(floatingRecords.Length, DefaultJobBatchSize));
                    hasBuiltAnyChunkSlice = true;
                }

                if (!hasBuiltAnyChunkSlice)
                    return false;

                _chunkBuildJobs[jobSlot] = new ChunkBuildPendingJob
                {
                    Active = true,
                    JobState = jobState,
                    SandMaskSnapshot = sandMaskForJob,
                    RockMaskSnapshot = rockMaskForJob,
                    HeightSamplesSnapshot = heightSamplesForJob,
                    GrassRecords = grassRecords,
                    FloatingRecords = floatingRecords,
                    KelpRecords = kelpRecords,
                    TerrainHoles = terrainHoles,
                    ArtificialStructures = artificialStructures,
                    ThreatEchoFlags = threatEchoFlags,
                    Handle = buildHandle
                };
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                {
                    ReleaseJobRecordArray(ref grassRecords);
                    ReleaseJobRecordArray(ref floatingRecords);
                    ReleaseJobRecordArray(ref kelpRecords);
                    H8Memory.Release(ref sandMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                    H8Memory.Release(ref rockMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                    H8Memory.Release(ref heightSamplesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                    H8Memory.Release(ref threatEchoFlags, VegetationMemorySovereigntyConstants.OwnerSystemId);
                    H8Memory.Release(ref terrainHoles, VegetationMemorySovereigntyConstants.OwnerSystemId);
                    H8Memory.Release(ref artificialStructures, VegetationMemorySovereigntyConstants.OwnerSystemId);
                }
            }
        }

        private static bool TryCopyChunkTileCacheForJob(
            NativeArray<byte> sandMask,
            NativeArray<byte> rockMask,
            NativeArray<ushort> heightSamples,
            out NativeArray<byte> sandMaskForJob,
            out NativeArray<byte> rockMaskForJob,
            out NativeArray<ushort> heightSamplesForJob)
        {
            sandMaskForJob = default;
            rockMaskForJob = default;
            heightSamplesForJob = default;
            if (!sandMask.IsCreated ||
                !rockMask.IsCreated ||
                !heightSamples.IsCreated ||
                sandMask.Length <= 0 ||
                rockMask.Length <= 0 ||
                heightSamples.Length <= 0)
            {
                return false;
            }

            sandMaskForJob = H8Memory.Allocate<byte>(
                sandMask.Length,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            rockMaskForJob = H8Memory.Allocate<byte>(
                rockMask.Length,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            heightSamplesForJob = H8Memory.Allocate<ushort>(
                heightSamples.Length,
                VegetationMemorySovereigntyConstants.OwnerSystemId,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            if (!sandMaskForJob.IsCreated ||
                sandMaskForJob.Length < sandMask.Length ||
                !rockMaskForJob.IsCreated ||
                rockMaskForJob.Length < rockMask.Length ||
                !heightSamplesForJob.IsCreated ||
                heightSamplesForJob.Length < heightSamples.Length)
            {
                H8Memory.Release(ref sandMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref rockMaskForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                H8Memory.Release(ref heightSamplesForJob, VegetationMemorySovereigntyConstants.OwnerSystemId);
                return false;
            }

            NativeArray<byte>.Copy(sandMask, sandMaskForJob, sandMask.Length);
            NativeArray<byte>.Copy(rockMask, rockMaskForJob, rockMask.Length);
            NativeArray<ushort>.Copy(heightSamples, heightSamplesForJob, heightSamples.Length);
            return true;
        }

        private int FinalizeCompletedChunkBuilds()
        {
            int completedCount = 0;
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (!_chunkBuildJobs[i].Active || !_chunkBuildJobs[i].Handle.IsCompleted)
                    continue;

                if (FinalizeChunkBuildJob(i))
                    completedCount++;
            }

            return completedCount;
        }

        private bool HasAvailableChunkBuildJobSlot()
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (!_chunkBuildJobs[i].Active)
                    return true;
            }

            return false;
        }

        private bool TryAcquireChunkBuildJobSlot(out int slot)
        {
            for (int i = 0; i < _chunkBuildJobs.Length; i++)
            {
                if (_chunkBuildJobs[i].Active)
                    continue;

                slot = i;
                return true;
            }

            slot = -1;
            return false;
        }

        private bool FinalizeChunkBuildJob(int slot)
        {
            ChunkBuildPendingJob pending = _chunkBuildJobs[slot];
            if (!DispatcherJobSwap.TryComplete(ref pending.Handle, forceComplete: false))
                return false;

            try
            {
                if (pending.Cancelled || !IsJobStateCurrent(pending.JobState))
                    return false;

                ChunkKey key = pending.JobState.Key;
                ReleaseChunkPayloadStorage(key);
                ChunkPayload payload = BuildChunkPayloadFromJob(
                    pending.JobState,
                    pending.GrassRecords,
                    pending.FloatingRecords,
                    pending.KelpRecords);
                if (!SetChunkPayload(key, payload))
                {
                    ReleaseChunkPayloadStorage(payload);
                    return false;
                }

                CacheChunkAbyssalNavPayload(key, BuildChunkAbyssalNavPayload(key, pending.JobState, payload));
                CacheChunkMegaWreckPayload(key, BuildChunkMegaWreckPayload(payload));
                RegisterChunkPayloadStorage(payload);
                _activeSetDirty = true;
                return true;
            }
            finally
            {
                ReleaseChunkBuildPendingJob(ref pending);
                _chunkBuildJobs[slot] = default;
            }
        }

        private bool IsJobStateCurrent(ChunkBuildJobState jobState)
        {
            if (!_tileStates.TryGetValue(jobState.TileKey, out TileRuntimeState state) || state == null)
                return false;

            return state.CacheRevision == jobState.TileCacheRevision;
        }

        private ChunkPayload BuildChunkPayloadFromJob(
            ChunkBuildJobState jobState,
            NativeArray<JobInstanceRecord> grassRecords,
            NativeArray<JobInstanceRecord> floatingRecords,
            NativeArray<JobInstanceRecord> kelpRecords)
        {
            ChunkPayload payload = jobState.PayloadHeader;
            payload.GrassLodTier = jobState.GrassLodTier;
            payload.CorruptionState = jobState.CorruptionState;

            int grassCount = CountValidRecords(grassRecords);
            int floatingCount = CountValidRecords(floatingRecords);
            int kelpCount = CountValidRecords(kelpRecords);
            int surfaceCount = grassCount + floatingCount;

            if (surfaceCount > 0)
            {
                if (TryAllocateChunkSliceForWrite(isSurface: true, surfaceCount, out int surfaceOffset, out bool useScratchPool))
                {
                    int writeIndex = surfaceOffset;
                    bool wroteSurface;
                    if (useScratchPool)
                    {
                        wroteSurface =
                            WriteJobRecordsToPool(grassRecords, ref _surfaceDefragScratchPool, ref writeIndex, _totalUniverseOffsetDouble, floraTemplates, _floraTemplateRuntimeDescriptors) &&
                            WriteJobRecordsToPool(floatingRecords, ref _surfaceDefragScratchPool, ref writeIndex, _totalUniverseOffsetDouble, floraTemplates, _floraTemplateRuntimeDescriptors);
                    }
                    else
                    {
                        wroteSurface =
                            WriteJobRecordsToPool(grassRecords, ref _surfaceChunkPool, ref writeIndex, _totalUniverseOffsetDouble, floraTemplates, _floraTemplateRuntimeDescriptors) &&
                            WriteJobRecordsToPool(floatingRecords, ref _surfaceChunkPool, ref writeIndex, _totalUniverseOffsetDouble, floraTemplates, _floraTemplateRuntimeDescriptors);
                    }

                    if (wroteSurface && writeIndex == surfaceOffset + surfaceCount)
                    {
                        payload.SurfaceOffset = surfaceOffset;
                        payload.SurfaceCount = surfaceCount;
                        payload.SurfaceEdgeOffset = surfaceOffset;
                        payload.SurfacePoolSet = useScratchPool ? (byte)1 : (byte)0;
                    }
                    else if (useScratchPool)
                        FreeChunkSlice(ref _surfaceDefragScratchFreeBlocks, ref _surfaceDefragScratchFreeBlockCount, surfaceOffset, surfaceCount);
                    else
                        FreeChunkSlice(ref _surfacePoolFreeBlocks, ref _surfacePoolFreeBlockCount, surfaceOffset, surfaceCount);
                }
            }

            if (kelpCount > 0)
            {
                if (TryAllocateChunkSliceForWrite(isSurface: false, kelpCount, out int underwaterOffset, out bool useScratchPool))
                {
                    int writeIndex = underwaterOffset;
                    bool wroteUnderwater = useScratchPool
                        ? WriteJobRecordsToPool(kelpRecords, ref _underwaterDefragScratchPool, ref writeIndex, _totalUniverseOffsetDouble, floraTemplates, _floraTemplateRuntimeDescriptors)
                        : WriteJobRecordsToPool(kelpRecords, ref _underwaterChunkPool, ref writeIndex, _totalUniverseOffsetDouble, floraTemplates, _floraTemplateRuntimeDescriptors);

                    if (wroteUnderwater && writeIndex == underwaterOffset + kelpCount)
                    {
                        payload.UnderwaterOffset = underwaterOffset;
                        payload.UnderwaterCount = kelpCount;
                        payload.UnderwaterEdgeOffset = underwaterOffset;
                        payload.UnderwaterPoolSet = useScratchPool ? (byte)1 : (byte)0;
                    }
                    else if (useScratchPool)
                        FreeChunkSlice(ref _underwaterDefragScratchFreeBlocks, ref _underwaterDefragScratchFreeBlockCount, underwaterOffset, kelpCount);
                    else
                        FreeChunkSlice(ref _underwaterPoolFreeBlocks, ref _underwaterPoolFreeBlockCount, underwaterOffset, kelpCount);
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

        private bool WriteJobRecordsToPool(
            NativeArray<JobInstanceRecord> source,
            ref NativeChunkPool pool,
            ref int writeIndex,
            double3 universeOffset,
            FloraDataTemplate[] floraTemplates,
            FloraDataTemplate.RuntimeDescriptor[] floraTemplateRuntimeDescriptors)
        {
            if (!source.IsCreated)
                return true;

            int validCount = CountValidRecords(source);
            if (validCount <= 0)
                return true;

            int startIndex = writeIndex;
            int requiredCount = writeIndex + validCount;
            if (writeIndex < 0 || requiredCount < writeIndex)
                return false;

            if (!TryReadChunkPoolView(in pool, requiredCount, out NativeChunkPoolView poolView) ||
                !WriteJobRecordMatricesToPool(source, ref pool.MatricesHandle, requiredCount, startIndex, universeOffset) ||
                !WriteJobRecordMetadataToPool(source, ref pool.MetadataHandle, requiredCount, startIndex, floraTemplates, floraTemplateRuntimeDescriptors) ||
                !WriteJobRecordTypesToPool(source, ref pool.TypesHandle, requiredCount, startIndex) ||
                !WriteJobRecordSemanticTypesToPool(source, ref pool.SemanticTypesHandle, requiredCount, startIndex) ||
                !WriteJobRecordBiomeLayersToPool(source, ref pool.BiomeLayersHandle, requiredCount, startIndex) ||
                !WriteJobRecordEdgeDistancesToPool(source, ref pool.EdgeDistancesHandle, requiredCount, startIndex) ||
                !WriteJobRecordFlowDirectionsToPool(source, ref pool.FlowDirectionsHandle, requiredCount, startIndex) ||
                !WriteJobRecordFlowVectorsToPool(source, ref pool.FlowVectorsHandle, requiredCount, startIndex))
            {
                return false;
            }

            writeIndex = startIndex + validCount;
            return writeIndex <= poolView.Capacity;
        }

        private bool WriteJobRecordMatricesToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<Matrix4x4> handle,
            int requiredCount,
            int startIndex,
            double3 universeOffset)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<Matrix4x4> matrices))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;

                    if ((uint)write >= (uint)matrices.Length)
                        return false;

                    matrices[write] = ConvertMatrixToStableUniverseSpace(ToMatrix4x4(record.Matrix), universeOffset);
                    write++;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordMetadataToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<HectonVegetationInstanceData> handle,
            int requiredCount,
            int startIndex,
            FloraDataTemplate[] floraTemplates,
            FloraDataTemplate.RuntimeDescriptor[] floraTemplateRuntimeDescriptors)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<HectonVegetationInstanceData> metadata))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;

                    if ((uint)write >= (uint)metadata.Length)
                        return false;

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
                    metadata[write] = new HectonVegetationInstanceData(
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
                    write++;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordTypesToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<int> handle,
            int requiredCount,
            int startIndex)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<int> types))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;
                    if ((uint)write >= (uint)types.Length)
                        return false;
                    types[write++] = record.Type;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordSemanticTypesToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<int> handle,
            int requiredCount,
            int startIndex)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<int> semanticTypes))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;
                    if ((uint)write >= (uint)semanticTypes.Length)
                        return false;
                    semanticTypes[write++] = record.SemanticType;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordBiomeLayersToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<byte> handle,
            int requiredCount,
            int startIndex)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<byte> biomeLayers))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;
                    if ((uint)write >= (uint)biomeLayers.Length)
                        return false;
                    biomeLayers[write++] = record.BiomeLayer;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordEdgeDistancesToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<float> handle,
            int requiredCount,
            int startIndex)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<float> edgeDistances))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;
                    if ((uint)write >= (uint)edgeDistances.Length)
                        return false;
                    edgeDistances[write++] = record.EdgeDistance;
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordFlowDirectionsToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<Vector2> handle,
            int requiredCount,
            int startIndex)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<Vector2> flowDirections))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;
                    if ((uint)write >= (uint)flowDirections.Length)
                        return false;
                    flowDirections[write++] = new Vector2(record.FlowDirection.x, record.FlowDirection.y);
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool WriteJobRecordFlowVectorsToPool(
            NativeArray<JobInstanceRecord> source,
            ref VaultGenerationHandle<Vector3> handle,
            int requiredCount,
            int startIndex)
        {
            if (!TryAcquireAggregateWriteBuffer(ref handle, requiredCount, out IDataVault vault, out NativeArray<Vector3> flowVectors))
                return false;

            try
            {
                int write = startIndex;
                for (int i = 0; i < source.Length; i++)
                {
                    JobInstanceRecord record = source[i];
                    if (record.IsValid == 0)
                        continue;
                    if ((uint)write >= (uint)flowVectors.Length)
                        return false;
                    flowVectors[write++] = new Vector3(record.FlowVector.x, record.FlowVector.y, record.FlowVector.z);
                }

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, VegetationMemorySovereigntyConstants.OwnerSystemId);
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

        private bool CopyChunkSliceToAggregate(
            NativeChunkPool pool,
            int sourceOffset,
            ref ActiveAggregateNativeBufferSet destinationBuffers,
            int destinationOffset,
            int copyCount)
        {
            if (copyCount <= 0)
                return true;

            int requiredCount = destinationOffset + copyCount;
            int sourceRequiredCount = sourceOffset + copyCount;
            if (sourceOffset < 0 ||
                destinationOffset < 0 ||
                requiredCount < destinationOffset ||
                sourceRequiredCount < sourceOffset ||
                !TryReadChunkPoolView(in pool, sourceRequiredCount, out NativeChunkPoolView poolView))
            {
                return false;
            }

            bool matricesLocked = false;
            bool metadataLocked = false;
            bool typesLocked = false;
            bool semanticTypesLocked = false;
            bool biomeLayersLocked = false;
            bool flowDirectionsLocked = false;
            bool flowVectorsLocked = false;
            IDataVault matricesVault = null;
            IDataVault metadataVault = null;
            IDataVault typesVault = null;
            IDataVault semanticTypesVault = null;
            IDataVault biomeLayersVault = null;
            IDataVault flowDirectionsVault = null;
            IDataVault flowVectorsVault = null;
            try
            {
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.MatricesHandle, requiredCount, out matricesVault, out NativeArray<Matrix4x4> matrices))
                    return false;
                matricesLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.MetadataHandle, requiredCount, out metadataVault, out NativeArray<HectonVegetationInstanceData> metadata))
                    return false;
                metadataLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.TypesHandle, requiredCount, out typesVault, out NativeArray<int> types))
                    return false;
                typesLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.SemanticTypesHandle, requiredCount, out semanticTypesVault, out NativeArray<int> semanticTypes))
                    return false;
                semanticTypesLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.BiomeLayersHandle, requiredCount, out biomeLayersVault, out NativeArray<byte> biomeLayers))
                    return false;
                biomeLayersLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.FlowDirectionsHandle, requiredCount, out flowDirectionsVault, out NativeArray<Vector2> flowDirections))
                    return false;
                flowDirectionsLocked = true;
                if (!TryAcquireAggregateWriteBuffer(ref destinationBuffers.FlowVectorsHandle, requiredCount, out flowVectorsVault, out NativeArray<Vector3> flowVectors))
                    return false;
                flowVectorsLocked = true;
                NativeArray<Matrix4x4>.Copy(poolView.Matrices, sourceOffset, matrices, destinationOffset, copyCount);
                NativeArray<HectonVegetationInstanceData>.Copy(poolView.Metadata, sourceOffset, metadata, destinationOffset, copyCount);
                NativeArray<int>.Copy(poolView.Types, sourceOffset, types, destinationOffset, copyCount);
                NativeArray<int>.Copy(poolView.SemanticTypes, sourceOffset, semanticTypes, destinationOffset, copyCount);
                NativeArray<byte>.Copy(poolView.BiomeLayers, sourceOffset, biomeLayers, destinationOffset, copyCount);
                NativeArray<Vector2>.Copy(poolView.FlowDirections, sourceOffset, flowDirections, destinationOffset, copyCount);
                NativeArray<Vector3>.Copy(poolView.FlowVectors, sourceOffset, flowVectors, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                if (flowVectorsLocked)
                    flowVectorsVault.ReleaseWriteLock(in destinationBuffers.FlowVectorsHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (flowDirectionsLocked)
                    flowDirectionsVault.ReleaseWriteLock(in destinationBuffers.FlowDirectionsHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (biomeLayersLocked)
                    biomeLayersVault.ReleaseWriteLock(in destinationBuffers.BiomeLayersHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (semanticTypesLocked)
                    semanticTypesVault.ReleaseWriteLock(in destinationBuffers.SemanticTypesHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (typesLocked)
                    typesVault.ReleaseWriteLock(in destinationBuffers.TypesHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (metadataLocked)
                    metadataVault.ReleaseWriteLock(in destinationBuffers.MetadataHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
                if (matricesLocked)
                    matricesVault.ReleaseWriteLock(in destinationBuffers.MatricesHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }
    }
}
