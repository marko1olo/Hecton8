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
            for (int i = 0; i < _selectedChunkCount; i++)
            {
                if (!_chunkPayloads.TryGetValue(_selectedChunkKeys[i], out ChunkPayload payload))
                {
                    _selectedChunkVisibility[i] = false;
                    continue;
                }

                // GPU per-instance frustum+HZB culling in HectonIndirectVegetationRenderer
                // owns visibility. A CPU frustum gate here is only re-evaluated on residency
                // changes, so chunks behind the camera at that moment never entered the
                // aggregate buffer and could not reappear when the camera turned — GPU
                // culling can hide instances but cannot add missing ones. The view-dependent
                // gate also churned the aggregate instance count, defeating the dirty-page
                // upload path. Every resident chunk stays in the buffer.
                _selectedChunkVisibility[i] = true;

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


        private struct ChunkJobSharedData
        {
            public NativeArray<byte> SandMask;
            public NativeArray<byte> RockMask;
            public NativeArray<ushort> HeightSamples;
            public NativeArray<TerrainHoleRecord> TerrainHoles;
            public int TerrainHoleCount;
            public NativeArray<ArtificialStructureRecord> ArtificialStructures;
            public int ArtificialStructureCount;
            public NativeArray<byte> ThreatEchoFlags;
            public float3 TerrainPosition;
            public float3 TerrainSize;
            public int AlphamapResolution;
            public int HeightmapResolution;
            public float MinX, MinZ, MaxX, MaxZ;
            public int TileX, TileZ, ChunkX, ChunkZ;
            public bool IsCorrupted;
        }

        private GenerateAnchoredVegetationJob CreateGrassJob(
            in ChunkJobSharedData shared,
            NativeArray<JobInstanceRecord> grassRecords,
            int grassCountX, int grassCountZ, float chunkWidth, float chunkDepth,
            float minimumNormalY, float normalOffset, float waterLevel, float edgeDitherDistance,
            uint proceduralScaleJitter)
        {
            return new GenerateAnchoredVegetationJob
            {
                SandMask = shared.SandMask,
                RockMask = shared.RockMask,
                HeightSamples = shared.HeightSamples,
                TerrainHoles = shared.TerrainHoles,
                ThreatEchoFlags = shared.ThreatEchoFlags,
                ArtificialStructures = shared.ArtificialStructures,
                ArtificialStructureHash = default,
                TerrainHoleCount = shared.TerrainHoleCount,
                ArtificialStructureCount = shared.ArtificialStructureCount,
                Output = grassRecords,
                TerrainPosition = shared.TerrainPosition,
                TerrainSize = shared.TerrainSize,
                AlphamapResolution = shared.AlphamapResolution,
                HeightResolution = shared.HeightmapResolution,
                MinX = shared.MinX,
                MinZ = shared.MinZ,
                MaxX = shared.MaxX,
                MaxZ = shared.MaxZ,
                StepX = chunkWidth / grassCountX,
                StepZ = chunkDepth / grassCountZ,
                SampleCountX = grassCountX,
                TileX = shared.TileX,
                TileZ = shared.TileZ,
                ChunkX = shared.ChunkX,
                ChunkZ = shared.ChunkZ,
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
        }

        private GenerateAnchoredVegetationJob CreateKelpJob(
            in ChunkJobSharedData shared,
            NativeArray<JobInstanceRecord> kelpRecords,
            int kelpCountX, int kelpCountZ, float chunkWidth, float chunkDepth,
            float minimumNormalY, float normalOffset, float waterLevel, float edgeDitherDistance,
            uint proceduralScaleJitter)
        {
            return new GenerateAnchoredVegetationJob
            {
                SandMask = shared.SandMask,
                RockMask = shared.RockMask,
                HeightSamples = shared.HeightSamples,
                TerrainHoles = shared.TerrainHoles,
                ThreatEchoFlags = shared.ThreatEchoFlags,
                ArtificialStructures = shared.ArtificialStructures,
                ArtificialStructureHash = default,
                TerrainHoleCount = shared.TerrainHoleCount,
                ArtificialStructureCount = shared.ArtificialStructureCount,
                Output = kelpRecords,
                TerrainPosition = shared.TerrainPosition,
                TerrainSize = shared.TerrainSize,
                AlphamapResolution = shared.AlphamapResolution,
                HeightResolution = shared.HeightmapResolution,
                MinX = shared.MinX,
                MinZ = shared.MinZ,
                MaxX = shared.MaxX,
                MaxZ = shared.MaxZ,
                StepX = chunkWidth / kelpCountX,
                StepZ = chunkDepth / kelpCountZ,
                SampleCountX = kelpCountX,
                TileX = shared.TileX,
                TileZ = shared.TileZ,
                ChunkX = shared.ChunkX,
                ChunkZ = shared.ChunkZ,
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
                IgnorePlacementMasks = shared.IsCorrupted ? 1 : 0,
                CorruptionMode = shared.IsCorrupted ? 1 : 0,
                EnableVerticalBiomeRewrite = shared.IsCorrupted ? 0 : 1,
                ScaleSalt = 0x27D4EB2Fu,
                WidthSalt = 0x165667B1u,
                ScaleJitter = proceduralScaleJitter,
                RotationSalt = 0x94D049BBu
            };
        }

        private GenerateFloatingVegetationJob CreateFloatingJob(
            in ChunkJobSharedData shared,
            NativeArray<JobInstanceRecord> floatingRecords,
            int floatingCountX, int floatingCountZ, float chunkWidth, float chunkDepth,
            float minimumNormalY, float waterLevel, float edgeDitherDistance,
            uint proceduralScaleJitter)
        {
            return new GenerateFloatingVegetationJob
            {
                SandMask = shared.SandMask,
                RockMask = shared.RockMask,
                HeightSamples = shared.HeightSamples,
                TerrainHoles = shared.TerrainHoles,
                TerrainHoleCount = shared.TerrainHoleCount,
                Output = floatingRecords,
                TerrainPosition = shared.TerrainPosition,
                TerrainSize = shared.TerrainSize,
                AlphamapResolution = shared.AlphamapResolution,
                HeightResolution = shared.HeightmapResolution,
                MinX = shared.MinX,
                MinZ = shared.MinZ,
                MaxX = shared.MaxX,
                MaxZ = shared.MaxZ,
                StepX = chunkWidth / floatingCountX,
                StepZ = chunkDepth / floatingCountZ,
                SampleCountX = floatingCountX,
                TileX = shared.TileX,
                TileZ = shared.TileZ,
                ChunkX = shared.ChunkX,
                ChunkZ = shared.ChunkZ,
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
        }

        private bool ScheduleChunkBuild(TileRuntimeState state, ChunkKey key, long tileKey, byte grassLodTier)
        {
            if (state == null ||
                state.AlphamapResolution <= 0 ||
                state.HeightmapResolution <= 1)
            {
                return false;
            }

            NativeArray<TerrainHoleRecord> terrainHoles = default;
            int terrainHoleCountForJob = 0;
            NativeArray<ArtificialStructureRecord> artificialStructures = default;
            int artificialStructureCountForJob = 0;
            IDataVault chunkBuildReadPinVault = null;
            uint chunkBuildReadPinMask = 0u;
            BufferID tileSandMaskBufferId = 0;
            BufferID tileRockMaskBufferId = 0;
            BufferID tileHeightSamplesBufferId = 0;
            if (!TryPinActiveTileCacheForJob(
                    state,
                    ref chunkBuildReadPinVault,
                    ref chunkBuildReadPinMask,
                    out NativeArray<byte> sandMaskForJob,
                    out NativeArray<byte> rockMaskForJob,
                    out NativeArray<ushort> heightSamplesForJob,
                    out tileSandMaskBufferId,
                    out tileRockMaskBufferId,
                    out tileHeightSamplesBufferId))
            {
                return false;
            }

            TouchTileCacheState(state);
            if (_terrainHoleCount > 0 && _nativeMemory.TerrainHoleRecordsHandle.BufferID == 0u)
                SyncTerrainHoleNativeCache();

            int terrainHoleCount = _terrainHoleCount > 0 ? _terrainHoleCount : 0;
            if (terrainHoleCount > 0)
            {
                if (!TryPinChunkBuildReadBuffer(
                        BufferID.VegetationTerrainHoleRecords,
                        ChunkBuildPinTerrainHoles,
                        ref chunkBuildReadPinVault,
                        ref chunkBuildReadPinMask) ||
                    !TryReadVegetationMemoryBuffer(
                        in _nativeMemory.TerrainHoleRecordsHandle,
                        BufferID.VegetationTerrainHoleRecords,
                        terrainHoleCount,
                        out terrainHoles))
                {
                    ReleaseChunkBuildReadPins(
                        chunkBuildReadPinVault,
                        chunkBuildReadPinMask,
                        tileSandMaskBufferId,
                        tileRockMaskBufferId,
                        tileHeightSamplesBufferId);
                    return false;
                }

                terrainHoleCountForJob = math.min(terrainHoleCount, terrainHoles.Length);
            }

            int artificialStructureCount = _artificialStructureCount > 0 ? _artificialStructureCount : 0;
            if (artificialStructureCount > 0)
            {
                if (!TryPinChunkBuildReadBuffer(
                        BufferID.VegetationArtificialStructureRecords,
                        ChunkBuildPinArtificialStructures,
                        ref chunkBuildReadPinVault,
                        ref chunkBuildReadPinMask) ||
                    !TryReadVegetationMemoryBuffer(
                        in _nativeMemory.ArtificialStructureRecordsHandle,
                        BufferID.VegetationArtificialStructureRecords,
                        artificialStructureCount,
                        out artificialStructures))
                {
                    ReleaseChunkBuildReadPins(
                        chunkBuildReadPinVault,
                        chunkBuildReadPinMask,
                        tileSandMaskBufferId,
                        tileRockMaskBufferId,
                        tileHeightSamplesBufferId);
                    return false;
                }

                artificialStructureCountForJob = math.min(artificialStructureCount, artificialStructures.Length);
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

            NativeArray<JobInstanceRecord> grassRecords = default;
            NativeArray<JobInstanceRecord> floatingRecords = default;
            NativeArray<JobInstanceRecord> kelpRecords = default;

            float3 terrainPosition = new float3(state.TerrainPosition.x, state.TerrainPosition.y, state.TerrainPosition.z);
            float3 terrainSize = new float3(state.TerrainSize.x, state.TerrainSize.y, state.TerrainSize.z);
            NativeArray<byte> threatEchoFlags = default;
            if (!_threatPropagationScheduled &&
                _ecosystemThreatGridCellCount > 0 &&
                TryPinChunkBuildReadBuffer(
                    BufferID.VegetationEcosystemThreatEcho,
                    ChunkBuildPinThreatEcho,
                    ref chunkBuildReadPinVault,
                    ref chunkBuildReadPinMask))
            {
                if (!TryReadVegetationMemoryBuffer(
                        in _nativeMemory.EcosystemThreatEchoHandle,
                        BufferID.VegetationEcosystemThreatEcho,
                        _ecosystemThreatGridCellCount,
                        out threatEchoFlags))
                {
                    ReleaseChunkBuildReadPins(chunkBuildReadPinVault, ChunkBuildPinThreatEcho);
                    chunkBuildReadPinMask &= ~ChunkBuildPinThreatEcho;
                    if (chunkBuildReadPinMask == 0u)
                        chunkBuildReadPinVault = null;
                    threatEchoFlags = default;
                }
            }
            bool hasBuiltAnyChunkSlice = false;
            bool scheduled = false;
            int jobSlot = -1;
            try
            {
                if (!IsJobStateCurrent(jobState) ||
                    !TryAcquireChunkBuildJobSlot(out jobSlot) ||
                    !TryAcquireChunkBuildRecordArrays(
                        jobSlot,
                        grassCountX * grassCountZ,
                        floatingCountX * floatingCountZ,
                        kelpCountX * kelpCountZ,
                        out grassRecords,
                        out floatingRecords,
                        out kelpRecords))
                {
                    return false;
                }

                JobHandle buildHandle = default;
                var sharedData = new ChunkJobSharedData
                {
                    SandMask = sandMaskForJob,
                    RockMask = rockMaskForJob,
                    HeightSamples = heightSamplesForJob,
                    TerrainHoles = terrainHoles,
                    TerrainHoleCount = terrainHoleCountForJob,
                    ArtificialStructures = artificialStructures,
                    ArtificialStructureCount = artificialStructureCountForJob,
                    ThreatEchoFlags = threatEchoFlags,
                    TerrainPosition = terrainPosition,
                    TerrainSize = terrainSize,
                    AlphamapResolution = state.AlphamapResolution,
                    HeightmapResolution = state.HeightmapResolution,
                    MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
                    TileX = key.TileX, TileZ = key.TileZ, ChunkX = key.ChunkX, ChunkZ = key.ChunkZ,
                    IsCorrupted = isCorrupted
                };

                if (grassRecords.IsCreated && grassRecords.Length > 0)
                {
                    var grassJob = CreateGrassJob(
                        in sharedData, grassRecords,
                        grassCountX, grassCountZ, chunkWidth, chunkDepth,
                        minimumNormalY, normalOffset, waterLevel, edgeDitherDistance,
                        (uint)proceduralScaleJitter);

                    buildHandle = CombineOptionalHandles(
                        buildHandle,
                        grassJob.Schedule(grassRecords.Length, DefaultJobBatchSize));
                    hasBuiltAnyChunkSlice = true;
                }

                if (kelpRecords.IsCreated && kelpRecords.Length > 0)
                {
                    var kelpJob = CreateKelpJob(
                        in sharedData, kelpRecords,
                        kelpCountX, kelpCountZ, chunkWidth, chunkDepth,
                        minimumNormalY, normalOffset, waterLevel, edgeDitherDistance,
                        (uint)proceduralScaleJitter);

                    buildHandle = CombineOptionalHandles(
                        buildHandle,
                        kelpJob.Schedule(kelpRecords.Length, DefaultJobBatchSize));
                    hasBuiltAnyChunkSlice = true;
                }

                if (floatingRecords.IsCreated && floatingRecords.Length > 0)
                {
                    var floatingJob = CreateFloatingJob(
                        in sharedData, floatingRecords,
                        floatingCountX, floatingCountZ, chunkWidth, chunkDepth,
                        minimumNormalY, waterLevel, edgeDitherDistance,
                        (uint)proceduralScaleJitter);

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
                    ReadPinVault = chunkBuildReadPinVault,
                    ReadPinMask = chunkBuildReadPinMask,
                    TileSandMaskBufferId = tileSandMaskBufferId,
                    TileRockMaskBufferId = tileRockMaskBufferId,
                    TileHeightSamplesBufferId = tileHeightSamplesBufferId,
                    Handle = buildHandle
                };
                scheduled = true;
                return true;
            }
            finally
            {
                if (!scheduled)
                {
                    ReleaseChunkBuildReadPins(
                        chunkBuildReadPinVault,
                        chunkBuildReadPinMask,
                        tileSandMaskBufferId,
                        tileRockMaskBufferId,
                        tileHeightSamplesBufferId);
                }
            }
        }

        private bool TryPinChunkBuildReadBuffer(
            BufferID bufferId,
            uint pinBit,
            ref IDataVault readPinVault,
            ref uint readPinMask)
        {
            if ((readPinMask & pinBit) != 0u)
                return true;

            IDataVault vault = _vegetationMemoryVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                (readPinVault != null && !ReferenceEquals(readPinVault, vault)) ||
                !vault.TryLockBuffer(bufferId, VegetationMemorySovereigntyConstants.OwnerSystemId))
            {
                return false;
            }

            readPinVault = vault;
            readPinMask |= pinBit;
            return true;
        }

        private bool TryPinActiveTileCacheForJob(
            TileRuntimeState state,
            ref IDataVault readPinVault,
            ref uint readPinMask,
            out NativeArray<byte> sandMaskForJob,
            out NativeArray<byte> rockMaskForJob,
            out NativeArray<ushort> heightSamplesForJob,
            out BufferID sandMaskBufferId,
            out BufferID rockMaskBufferId,
            out BufferID heightSamplesBufferId)
        {
            sandMaskForJob = default;
            rockMaskForJob = default;
            heightSamplesForJob = default;
            sandMaskBufferId = 0;
            rockMaskBufferId = 0;
            heightSamplesBufferId = 0;

            if (state == null ||
                state.TileCacheDisposalDeferred ||
                state.TileNativeCacheSlot < 0)
            {
                return false;
            }

            TileNativeCacheBuffer buffer = state.ActiveCacheBufferIndex == 0
                ? state.PrimaryCacheBuffer
                : state.SecondaryCacheBuffer;

            if (buffer.SampleCount <= 0 ||
                buffer.HeightSampleCount <= 0 ||
                buffer.SandMaskHandle.BufferID == 0u ||
                buffer.RockMaskHandle.BufferID == 0u ||
                buffer.HeightSamplesHandle.BufferID == 0u)
            {
                return false;
            }

            sandMaskBufferId = unchecked((BufferID)(int)buffer.SandMaskHandle.BufferID);
            rockMaskBufferId = unchecked((BufferID)(int)buffer.RockMaskHandle.BufferID);
            heightSamplesBufferId = unchecked((BufferID)(int)buffer.HeightSamplesHandle.BufferID);

            if (!TryPinChunkBuildReadBuffer(
                    sandMaskBufferId,
                    ChunkBuildPinTileSandMask,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryPinChunkBuildReadBuffer(
                    rockMaskBufferId,
                    ChunkBuildPinTileRockMask,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryPinChunkBuildReadBuffer(
                    heightSamplesBufferId,
                    ChunkBuildPinTileHeightSamples,
                    ref readPinVault,
                    ref readPinMask) ||
                !TryReadVegetationMemoryBuffer(
                    in buffer.SandMaskHandle,
                    sandMaskBufferId,
                    buffer.SampleCount,
                    out sandMaskForJob) ||
                !TryReadVegetationMemoryBuffer(
                    in buffer.RockMaskHandle,
                    rockMaskBufferId,
                    buffer.SampleCount,
                    out rockMaskForJob) ||
                !TryReadVegetationMemoryBuffer(
                    in buffer.HeightSamplesHandle,
                    heightSamplesBufferId,
                    buffer.HeightSampleCount,
                    out heightSamplesForJob))
            {
                ReleaseChunkBuildReadPins(
                    readPinVault,
                    readPinMask,
                    sandMaskBufferId,
                    rockMaskBufferId,
                    heightSamplesBufferId);
                readPinVault = null;
                readPinMask = 0u;
                sandMaskForJob = default;
                rockMaskForJob = default;
                heightSamplesForJob = default;
                sandMaskBufferId = 0;
                rockMaskBufferId = 0;
                heightSamplesBufferId = 0;
                return false;
            }

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

            return CopyChunkMatricesToAggregate(poolView.Matrices, sourceOffset, ref destinationBuffers.MatricesHandle, requiredCount, destinationOffset, copyCount) &&
                   CopyChunkMetadataToAggregate(poolView.Metadata, sourceOffset, ref destinationBuffers.MetadataHandle, requiredCount, destinationOffset, copyCount) &&
                   CopyChunkIntLaneToAggregate(poolView.Types, sourceOffset, ref destinationBuffers.TypesHandle, requiredCount, destinationOffset, copyCount) &&
                   CopyChunkIntLaneToAggregate(poolView.SemanticTypes, sourceOffset, ref destinationBuffers.SemanticTypesHandle, requiredCount, destinationOffset, copyCount) &&
                   CopyChunkBiomeLayersToAggregate(poolView.BiomeLayers, sourceOffset, ref destinationBuffers.BiomeLayersHandle, requiredCount, destinationOffset, copyCount) &&
                   CopyChunkFlowDirectionsToAggregate(poolView.FlowDirections, sourceOffset, ref destinationBuffers.FlowDirectionsHandle, requiredCount, destinationOffset, copyCount) &&
                   CopyChunkFlowVectorsToAggregate(poolView.FlowVectors, sourceOffset, ref destinationBuffers.FlowVectorsHandle, requiredCount, destinationOffset, copyCount);
        }

        private bool CopyChunkMatricesToAggregate(
            NativeArray<Matrix4x4> source,
            int sourceOffset,
            ref VaultGenerationHandle<Matrix4x4> destinationHandle,
            int requiredCount,
            int destinationOffset,
            int copyCount)
        {
            if (!TryAcquireAggregateWriteBuffer(ref destinationHandle, requiredCount, out IDataVault vault, out NativeArray<Matrix4x4> destination))
                return false;

            try
            {
                NativeArray<Matrix4x4>.Copy(source, sourceOffset, destination, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in destinationHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyChunkMetadataToAggregate(
            NativeArray<HectonVegetationInstanceData> source,
            int sourceOffset,
            ref VaultGenerationHandle<HectonVegetationInstanceData> destinationHandle,
            int requiredCount,
            int destinationOffset,
            int copyCount)
        {
            if (!TryAcquireAggregateWriteBuffer(ref destinationHandle, requiredCount, out IDataVault vault, out NativeArray<HectonVegetationInstanceData> destination))
                return false;

            try
            {
                NativeArray<HectonVegetationInstanceData>.Copy(source, sourceOffset, destination, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in destinationHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyChunkIntLaneToAggregate(
            NativeArray<int> source,
            int sourceOffset,
            ref VaultGenerationHandle<int> destinationHandle,
            int requiredCount,
            int destinationOffset,
            int copyCount)
        {
            if (!TryAcquireAggregateWriteBuffer(ref destinationHandle, requiredCount, out IDataVault vault, out NativeArray<int> destination))
                return false;

            try
            {
                NativeArray<int>.Copy(source, sourceOffset, destination, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in destinationHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyChunkBiomeLayersToAggregate(
            NativeArray<byte> source,
            int sourceOffset,
            ref VaultGenerationHandle<byte> destinationHandle,
            int requiredCount,
            int destinationOffset,
            int copyCount)
        {
            if (!TryAcquireAggregateWriteBuffer(ref destinationHandle, requiredCount, out IDataVault vault, out NativeArray<byte> destination))
                return false;

            try
            {
                NativeArray<byte>.Copy(source, sourceOffset, destination, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in destinationHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyChunkFlowDirectionsToAggregate(
            NativeArray<Vector2> source,
            int sourceOffset,
            ref VaultGenerationHandle<Vector2> destinationHandle,
            int requiredCount,
            int destinationOffset,
            int copyCount)
        {
            if (!TryAcquireAggregateWriteBuffer(ref destinationHandle, requiredCount, out IDataVault vault, out NativeArray<Vector2> destination))
                return false;

            try
            {
                NativeArray<Vector2>.Copy(source, sourceOffset, destination, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in destinationHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool CopyChunkFlowVectorsToAggregate(
            NativeArray<Vector3> source,
            int sourceOffset,
            ref VaultGenerationHandle<Vector3> destinationHandle,
            int requiredCount,
            int destinationOffset,
            int copyCount)
        {
            if (!TryAcquireAggregateWriteBuffer(ref destinationHandle, requiredCount, out IDataVault vault, out NativeArray<Vector3> destination))
                return false;

            try
            {
                NativeArray<Vector3>.Copy(source, sourceOffset, destination, destinationOffset, copyCount);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in destinationHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }

        private bool MarkActiveAggregateDirtyPagesOneLock(
            ref VaultGenerationHandle<byte> dirtyPagesHandle,
            int elementCount)
        {
            int requiredPages = GraphicsBufferUploadUtility.ResolveDirtyPageCount(
                elementCount,
                ActiveAggregateDirtyPageSize);
            if (requiredPages <= 0)
                return false;

            if (!TryAcquireAggregateWriteBuffer(ref dirtyPagesHandle, requiredPages, out IDataVault vault, out NativeArray<byte> dirtyPages))
                return false;

            try
            {
                GraphicsBufferUploadUtility.ClearDirtyPages(dirtyPages, elementCount, ActiveAggregateDirtyPageSize);
                GraphicsBufferUploadUtility.MarkDirtyPageRange(
                    dirtyPages,
                    0,
                    elementCount,
                    elementCount,
                    ActiveAggregateDirtyPageSize);
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in dirtyPagesHandle, VegetationMemorySovereigntyConstants.OwnerSystemId);
            }
        }
    }
}
