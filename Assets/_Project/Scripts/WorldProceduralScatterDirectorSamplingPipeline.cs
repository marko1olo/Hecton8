using System.Collections.Generic;
using System.Diagnostics;
using Hecton8.Core;
using Hecton8.Environment;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private const int MaxSynchronousScatterReconcileIterations = 2048;

        private void ResetSamplingState(ScatterState nextState = ScatterState.Idle)
        {
            _samplingTotalCells = 0;
            _samplingCellDiameter = 0;
            _samplingRadiusCells = 0;
            _samplingCellSize = 0f;
            _samplingNow = 0f;
            _samplingGroundBudget = 0;
            _samplingClusterBudget = 0;
            _samplingStructureStride = 0;
            _samplingStructureBudget = 0;
            _samplingSpawnStride = 0;
            _samplingSpawnBudget = 0;
            _samplingRebuildStartTimestamp = 0L;
            _samplingInputsEndTimestamp = 0L;
            _samplingSnapshot = default;
            _scatterState = nextState;
        }

        private bool HandleScatterStateMachine()
        {
            using (_scatterStateMachineProfilerMarker.Auto())
            {
                switch (_scatterState)
                {
                    case ScatterState.Idle:
                        return false;
                    case ScatterState.Sampling:
                        if (!_isSamplingJobRunning)
                        {
                            _scatterState = ScatterState.Idle;
                            return false;
                        }

                        return true;
                    case ScatterState.Processing:
                        ProcessCompletedScatterSampling();
                        return true;
                    case ScatterState.Spawning:
                        if (HasPendingScatterReconcileWork())
                        {
                            ContinuePendingScatterReconcile();
                            if (!HasPendingScatterReconcileWork())
                                _scatterState = ScatterState.Idle;
                        }
                        else
                        {
                            _scatterState = ScatterState.Idle;
                        }

                        return true;
                    default:
                        _scatterState = ScatterState.Idle;
                        return false;
                }
            }
        }

        private bool TryBeginScatterSampling()
        {
            if (_scatterState != ScatterState.Idle || _isSamplingJobRunning)
                return true;

            using (_scatterSamplingBeginProfilerMarker.Auto())
            {
                if (!TryBuildScatterSamplingBeginContext(out ScatterSamplingBeginContext samplingContext))
                    return true;

                PrepareScatterSamplingBuffers(in samplingContext);
                if (_memory == null)
                {
                    ResetDiagnostics();
                    return true;
                }

                CacheScatterSamplingPass(in samplingContext);
                BuildScatterSamplingInputs(in samplingContext);
                using (_scatterSamplingScheduleProfilerMarker.Auto())
                {
                    JobHandle cellSamplingHandle = fieldSampler.ScheduleCellSamplingJob(
                        _memory.CellSamplingInputs,
                        _memory.CellSamplingOutputs,
                        _memory.BiomeInfluenceCells,
                        samplingContext.TotalCells);
                    _samplingJobHandle = ScheduleBiomeInfluencePackJob(samplingContext.TotalCells, cellSamplingHandle);
                }

                _isSamplingJobRunning = true;
                _scatterState = ScatterState.Sampling;
                return true;
            }
        }

        private bool TryBuildScatterSamplingBeginContext(out ScatterSamplingBeginContext context)
        {
            context = default;

            ResolveReferences();
            ForceRefreshProceduralContext();
            EnsureCandidateMapsInitialized();
            RefreshRuntimeStreamingSettings();

            IReadOnlyList<WorldProceduralPlacementRule> rules = proceduralFillDirector != null ? proceduralFillDirector.Rules : null;
            if (fieldSampler == null || rules == null || rules.Count == 0 ||
                !TryResolvePlayerAup(out AbsoluteUniversePosition centerAup))
            {
                HandleScatterSamplingUnavailableDependencies();
                return false;
            }

            float cellSize = math.max(6f, _runtimeStreamingState.CellSize);
            int radiusCells = ResolveActiveScatterSamplingRadiusCells(_runtimeStreamingState.RadiusCells);
            float now = Time.unscaledTime;
            int groundBudget = ResolveRuntimeBudget(groundPlacementsPerCell, WorldStreamingLayer.Flora, 0, 4);
            int clusterBudget = ResolveRuntimeBudget(clusterPlacementsPerCell, WorldStreamingLayer.Debris, 0, 3);
            int structureStride = math.max(2, structureCellStride);
            int structureBudget = ResolveRuntimeBudget(structurePlacementsPerWindow, WorldStreamingLayer.Construction, 0, 2);
            int spawnStride = math.max(2, spawnCellStride);
            int spawnBudget = ResolveRuntimeBudget(spawnPlacementsPerWindow, WorldStreamingLayer.Fauna, 0, 2);
            float3 runtimeCenter3 = centerAup.ToRuntimeFloat3();
            double3 absoluteCenter3 = centerAup.ToAbsoluteDouble3();
            Vector3 runtimeCenter = new Vector3(runtimeCenter3.x, runtimeCenter3.y, runtimeCenter3.z);
            Vector3 absoluteCenter = new Vector3((float)absoluteCenter3.x, (float)absoluteCenter3.y, (float)absoluteCenter3.z);
            int centerCellX = WorldToScatterCellIndex(absoluteCenter3.x, cellSize);
            int centerCellZ = WorldToScatterCellIndex(absoluteCenter3.z, cellSize);
            int cellDiameter = (radiusCells * 2) + 1;
            int totalCells = cellDiameter * cellDiameter;

            context = new ScatterSamplingBeginContext(
                rules,
                runtimeCenter,
                absoluteCenter,
                centerCellX,
                centerCellZ,
                cellSize,
                radiusCells,
                cellDiameter,
                totalCells,
                now,
                groundBudget,
                clusterBudget,
                structureStride,
                structureBudget,
                spawnStride,
                spawnBudget);
            return true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return playerAup.IsFinite();
            }

            var playerMovement = playerContext.PlayerMovement;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            return playerAup.IsFinite();
        }

        private void HandleScatterSamplingUnavailableDependencies()
        {
            PublishFaunaRegistrySnapshot();
            ResetDiagnostics();
            _samplingTotalCells = 0;
            _samplingSnapshot = default;
        }

        private void PrepareScatterSamplingBuffers(in ScatterSamplingBeginContext context)
        {
            ReleasePlacementDictionaryValues(_desiredPlacements);
            _faunaSnapshotDirty = true;
            _structureWindowCounts.Clear();
            _spawnWindowCounts.Clear();
            ReleaseCandidateListPlacements(_candidateBuffer);
            ResetPlacementGrid();
            PrepareRuntimeRuleBuffer(context.Rules);
            ClearScatterWorkingBuffers();

            ScatterRetentionEvictionContext retentionEvictionContext = new ScatterRetentionEvictionContext(
                _retainedPlacements,
                _placementLastSeenTimes,
                _removalBuffer,
                context.Now,
                math.max(0.25f, missingPlacementGraceSeconds) * 1.5f);
            EvictStaleRetainedPlacements(in retentionEvictionContext);

            EnsureScatterWindowBudgetCapacity(_structureWindowCounts, EstimateScatterWindowCapacity(context.CellDiameter, context.StructureStride));
            EnsureScatterWindowBudgetCapacity(_spawnWindowCounts, EstimateScatterWindowCapacity(context.CellDiameter, context.SpawnStride));
            EnsureCellSamplingArrayCapacity(context.TotalCells);
        }

        private void CacheScatterSamplingPass(in ScatterSamplingBeginContext context)
        {
            _samplingSnapshot = new SamplingSnapshot(
                context.RuntimeCenter,
                context.AbsoluteCenter,
                context.CenterCellX,
                context.CenterCellZ,
                context.Now);
            _samplingTotalCells = context.TotalCells;
            _samplingCellDiameter = context.CellDiameter;
            _samplingRadiusCells = context.RadiusCells;
            _samplingCellSize = context.CellSize;
            _samplingNow = context.Now;
            _samplingGroundBudget = context.GroundBudget;
            _samplingClusterBudget = context.ClusterBudget;
            _samplingStructureStride = context.StructureStride;
            _samplingStructureBudget = context.StructureBudget;
            _samplingSpawnStride = context.SpawnStride;
            _samplingSpawnBudget = context.SpawnBudget;
            _samplingRebuildStartTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;
        }

        private void BuildScatterSamplingInputs(in ScatterSamplingBeginContext context)
        {
            fieldSampler.BeginScatterSamplingFrame();
            using (_scatterSamplingInputBuildProfilerMarker.Auto())
            {
                int cellCursor = 0;
                for (int z = -context.RadiusCells; z <= context.RadiusCells; z++)
                {
                    for (int x = -context.RadiusCells; x <= context.RadiusCells; x++)
                    {
                        int cellXIndex = context.CenterCellX + x;
                        int cellZIndex = context.CenterCellZ + z;
                        Vector3 sampleOriginAbsolute = new Vector3(
                            (cellXIndex + 0.5f) * context.CellSize,
                            context.AbsoluteCenter.y,
                            (cellZIndex + 0.5f) * context.CellSize);
                        Vector3 sampleOrigin = ToRuntimeScatterPosition(sampleOriginAbsolute);
                        if (fieldSampler.TryBuildCellInput(sampleOrigin, cellXIndex, cellZIndex, out WorldProceduralFieldSampler.CellInputData cellInput))
                            _memory.CellSamplingInputs[cellCursor] = cellInput;
                        else
                            _memory.CellSamplingInputs[cellCursor] = new WorldProceduralFieldSampler.CellInputData
                            {
                                Position = new Unity.Mathematics.float3(sampleOrigin.x, sampleOrigin.y, sampleOrigin.z),
                                CellX = cellXIndex,
                                CellZ = cellZIndex,
                                IsValid = 0
                            };

                        cellCursor++;
                    }
                }
            }

            _samplingInputsEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;
        }

        private bool TryRunScatterSamplingSynchronously()
        {
            if (_scatterState == ScatterState.Processing)
            {
                ProcessCompletedScatterSampling();
                return true;
            }

            if (_scatterState == ScatterState.Spawning)
            {
                int reconcileWatchdog = MaxSynchronousScatterReconcileIterations;
                while (HasPendingScatterReconcileWork() && reconcileWatchdog-- > 0)
                    ContinuePendingScatterReconcile();

                if (HasPendingScatterReconcileWork())
                    return true;

                _scatterState = ScatterState.Idle;
            }

            if (_isSamplingJobRunning)
            {
                // COLD SYNC JOB: editor preview and bootstrap prime require immediate scatter output before continuing.
                DispatcherJobSwap.TryComplete(ref _samplingJobHandle, forceComplete: true);
                if (fieldSampler != null)
                    fieldSampler.MarkScatterSamplingJobCompleted();
                _isSamplingJobRunning = false;
                _scatterState = ScatterState.Processing;
                ProcessCompletedScatterSampling();
                return true;
            }

            if (!TryBeginScatterSampling())
                return true;

            if (!_isSamplingJobRunning)
                return true;

            // COLD SYNC JOB: editor preview and bootstrap prime require immediate scatter output before continuing.
            DispatcherJobSwap.TryComplete(ref _samplingJobHandle, forceComplete: true);
            if (fieldSampler != null)
                fieldSampler.MarkScatterSamplingJobCompleted();
            _isSamplingJobRunning = false;
            _scatterState = ScatterState.Processing;
            ProcessCompletedScatterSampling();
            return true;
        }

        private void CompleteScatterSamplingJobIfReady()
        {
            if (!_isSamplingJobRunning)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _samplingJobHandle, forceComplete: false))
                return;

            if (fieldSampler != null)
                fieldSampler.MarkScatterSamplingJobCompleted();
            _isSamplingJobRunning = false;
            _scatterState = ScatterState.Processing;
        }

        private void ProcessCompletedScatterSampling()
        {
            using (_scatterProcessingProfilerMarker.Auto())
            {
            if (!TryBuildScatterSamplingCompletionContext(out ScatterSamplingCompletionContext completionContext))
            {
                if (fieldSampler != null)
                    fieldSampler.EndScatterSamplingFrame();
                ResetSamplingState();
                return;
            }

            Vector3 center = completionContext.AbsoluteCenter;
            float size = completionContext.CellSize;
            float now = completionContext.Now;
            int totalCells = completionContext.TotalCells;
            int clusterBudget = completionContext.ClusterBudget;
            int structureStride = completionContext.StructureStride;
            int structureBudget = completionContext.StructureBudget;
            int spawnStride = completionContext.SpawnStride;
            int spawnBudget = completionContext.SpawnBudget;
            int groundBudget = completionContext.GroundBudget;
            long rebuildStartTimestamp = completionContext.RebuildStartTimestamp;
            long samplingInputsEndTimestamp = completionContext.SamplingInputsEndTimestamp;
            long samplingCompleteEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;
            PublishBiomeInfluenceGrid(totalCells);
            int evaluatedCells = 0;
            int biomeInfluenceTransitionCells = 0;
            ScatterCandidate topCandidate = default;
            bool hasTopCandidate = false;
            ScatterCandidate[] layerTopCandidates = completionContext.LayerTopCandidates;
            bool[] layerTopValid = completionContext.LayerTopValid;
            int[] layerPlacementCounts = completionContext.LayerPlacementCounts;
            int[] clusterAccentCounts = completionContext.ClusterAccentCounts;
            int[] structureAccentCounts = completionContext.StructureAccentCounts;
            Dictionary<string, int>[] layerFamilyCounts = completionContext.LayerFamilyCounts;
            Dictionary<string, int>[] layerBiomeCounts = completionContext.LayerBiomeCounts;
            ScatterPlacementRegistrationContext placementRegistrationContext = completionContext.PlacementRegistrationContext;
            ScatterRescueTrackingContext rescueTrackingContext = completionContext.RescueTrackingContext;
            Dictionary<HectonBiomeMatrixProfile, int> sampledMatrixProfileCounts = completionContext.SampledMatrixProfileCounts;
            Dictionary<string, int> sampledMatrixBiomeCounts = completionContext.SampledMatrixBiomeCounts;
            Dictionary<string, int> sampledBiomeCounts = completionContext.SampledBiomeCounts;
            Dictionary<string, int> sampledPatternCounts = completionContext.SampledPatternCounts;
            Dictionary<string, int> sampledZoneCounts = completionContext.SampledZoneCounts;
            int passiveSpawnCount = 0;
            int predatorSpawnCount = 0;
            int mapMagicSamples = 0;
            int sceneProbeLegacySamples = 0;
            int fallbackSamples = 0;
            int matchedScatterRules = 0;
            int heatPassedRules = 0;
            int gatePassedRules = 0;
            int residencyPassedCandidates = 0;
            int postBuildGateRejectedCandidates = 0;
            int queuedCandidates = 0;
            string rejectedResidencyFamily = "None";
            float rejectedResidencyDistance = 0f;
            float rejectedResidencyRadius = 0f;
            int maxCandidatesBeforePrunePerCell = 0;
            int maxCandidatesAfterPrunePerCell = 0;
            ScatterClassicParityAccumulator classicParityAccumulator = default;
            bool collectDetailedDiagnostics = completionContext.CollectDetailedDiagnostics != 0;
            WorldZoneAnchor debugZone = completionContext.DebugZone;
            WorldZoneAnchor.ZoneKind debugResolvedZoneKind = completionContext.DebugResolvedZoneKind;
            WorldProceduralPattern debugPattern = completionContext.DebugPattern;
            float debugGroundBudgetScale = completionContext.DebugGroundBudgetScale;
            float debugClusterBudgetScale = completionContext.DebugClusterBudgetScale;
            float debugStructureBudgetScale = completionContext.DebugStructureBudgetScale;
            float debugSpawnBudgetScale = completionContext.DebugSpawnBudgetScale;
            HectonBiomeMatrixProfile debugBiomeProfile = completionContext.DebugBiomeProfile;
            HectonBiomeFamilyProfile debugBiomeFamily = completionContext.DebugBiomeFamily;

            using (_scatterProcessingCellEvaluationProfilerMarker.Auto())
            {
                for (int cellIndex = 0; cellIndex < totalCells; cellIndex++)
                {
                    WorldProceduralFieldSampler.CellOutputData cellOutput = _memory.CellSamplingOutputs[cellIndex];
                    if (HectonBiomeVisualFamilyUtility.ExtractBlend255(cellOutput.BiomeInfluencePacked) != 0)
                        biomeInfluenceTransitionCells++;

                    ScatterSimulationCellState backendCellState = BuildScatterBackendCellState(cellOutput);
                    if (!fieldSampler.TryBuildFieldSample(cellOutput, out WorldProceduralFieldSampler.FieldSample fieldSample))
                    {
                        _memory.ScatterBackendCellStates[cellIndex] = backendCellState;
                        continue;
                    }

                int cellXIndex = cellOutput.CellX;
                int cellZIndex = cellOutput.CellZ;
                int domainCount = fieldSampler.GetFieldSampleDomainCount(cellOutput);
                evaluatedCells++;
                CountSeafloorSource(fieldSample.seafloorSource, ref mapMagicSamples, ref sceneProbeLegacySamples, ref fallbackSamples);
                debugZone = fieldSample.zone;
                debugResolvedZoneKind = fieldSample.resolvedZoneKind;
                debugPattern = fieldSample.resolvedPattern;
                debugBiomeProfile = fieldSample.biomeProfile;
                debugBiomeFamily = fieldSample.biomeFamily;
                RegisterProfileCount(sampledMatrixProfileCounts, fieldSample.biomeProfile);
#if UNITY_EDITOR
                if (collectDetailedDiagnostics)
                {
                    RegisterStringCount(sampledMatrixBiomeCounts, ResolveBiomeMatrixLabel(fieldSample.biomeProfile));
                    RegisterStringCount(sampledBiomeCounts, ResolveBiomeLabel(fieldSample.biomeFamily));
                    RegisterStringCount(sampledPatternCounts, GetPatternLabel(fieldSample.resolvedPattern));
                    RegisterStringCount(sampledZoneCounts, fieldSample.zone != null ? fieldSample.zone.ZoneLabel : ResolveSamplingSyntheticZoneDebugLabel(fieldSample.resolvedZoneKind));
                }
#endif
                WorldProceduralPatternProfile cellPatternProfile = ResolvePatternProfile(fieldSample.resolvedPattern, out _);
                WorldProceduralBiomeFamilyContextProfile cellBiomeContext = ResolveBiomeContextProfile(fieldSample.biomeFamily, out _);
                ScatterBiomeTransitionContext biomeTransitionContext = ResolveBiomeTransitionContext(fieldSample, cellBiomeContext);
                string cellBiomeContextLabel = cellBiomeContext != null ? cellBiomeContext.label : "None";
                bool usesPatternAccentQuotas = UsesPatternAccentQuotas(fieldSample.resolvedPattern);
                PopulatePatternQuotaCache(fieldSample.resolvedPattern, fieldSample.biomeProfile);
                int clusterRatioStart = _memory.CachedPatternClusterRatioStart;
                int minimumSpawnPlacements = ResolveMinimumSpawnPlacements(fieldSample.resolvedPattern, fieldSample.biomeProfile);
                int passiveSpawnMax = math.max(
                    _memory.CachedPatternPassiveSpawnMin,
                    _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn]);
                int predatorSpawnMax = _memory.CachedPatternPredatorSpawnMax;
                ScatterBiomeScoreContext biomeScoreContext = BuildScatterBiomeScoreContext(fieldSample.biomeProfile);
                ScatterPatternScoreContext patternScoreContext = BuildScatterPatternScoreContext(fieldSample.resolvedPattern);
                ResolveTransitionBudgetScales(
                    cellPatternProfile,
                    cellBiomeContext,
                    biomeTransitionContext,
                    out float localGroundBudgetScale,
                    out float localClusterBudgetScale,
                    out float localStructureBudgetScale,
                    out float localSpawnBudgetScale);
                int localGroundBudget = ResolveScaledBudget(groundBudget, localGroundBudgetScale, 4);
                int localClusterBudget = ResolveScaledBudget(clusterBudget, localClusterBudgetScale, 3);
                int localStructureBudget = ResolveScaledBudget(structureBudget, localStructureBudgetScale, 2);
                int localSpawnBudget = ResolveScaledBudget(spawnBudget, localSpawnBudgetScale, 2);
                RegisterScatterBackendCellBudgetState(
                    ref backendCellState,
                    localGroundBudget,
                    localClusterBudget,
                    localStructureBudget,
                    localSpawnBudget);
                int cellCandidateBufferLimit = ResolvePerCellCandidateBufferLimit(
                    localGroundBudget,
                    localClusterBudget,
                    localStructureBudget,
                    localSpawnBudget);
                debugGroundBudgetScale = localGroundBudgetScale;
                debugClusterBudgetScale = localClusterBudgetScale;
                debugStructureBudgetScale = localStructureBudgetScale;
                debugSpawnBudgetScale = localSpawnBudgetScale;

                    ScatterCellPlacementCounters cellPlacementCounters = default;
                int cellCandidatesBeforePrune = 0;
                int worstCandidateIndex = -1;
                float worstCandidateScore = float.MaxValue;
                GeologyBonusCache geologyBonusCache = default;
                ReleaseCandidateListPlacements(_candidateBuffer);
                for (int domainIndex = 0; domainIndex < domainCount; domainIndex++)
                {
                    WorldProceduralFieldSampler.FieldSample activeFieldSample = fieldSample;
                    if (domainIndex > 0 && !fieldSampler.TryBuildFieldSample(cellOutput, domainIndex, out activeFieldSample))
                        continue;

                    for (int i = 0; i < _runtimeRuleBuffer.Count; i++)
                    {
                        ScatterRuntimeRuleEntry runtimeRule = _runtimeRuleBuffer[i];
                        if (!ShouldEvaluateScatterDomain(activeFieldSample, runtimeRule))
                            continue;

                        WorldProceduralPlacementRule rule = runtimeRule.Rule;
                        WorldPrefabFamilyProfile family = runtimeRule.Family;
                        if (!MatchesScatter(
                                runtimeRule,
                                activeFieldSample.biomeFamily,
                                biomeTransitionContext.SecondaryFamily,
                                biomeTransitionContext.HasSecondary != 0,
                                activeFieldSample.zone,
                                activeFieldSample.resolvedZoneKind,
                                activeFieldSample.depthMeters,
                                activeFieldSample.slopeDegrees,
                                activeFieldSample.biomeFamilyFlags))
                        {
                            continue;
                        }
                        if (collectDetailedDiagnostics)
                            matchedScatterRules++;

                        bool deterministicClutter = IsDeterministicClutterFamily(family);
                        float heat = deterministicClutter
                            ? 1f
                            : fieldSampler.EvaluateHeatmap(
                                runtimeRule.HeatmapChannelIndex,
                                cellOutput,
                                runtimeRule.PlacementMode,
                                runtimeRule.DensityScaleFactor);
                        if (!deterministicClutter)
                        {
                            heat = math.saturate(
                                heat
                                * GetCombinedHeatScale(
                                    activeFieldSample.resolvedPattern,
                                    activeFieldSample.depthMeters,
                                    runtimeRule,
                                    biomeScoreContext,
                                    patternScoreContext));
                        }

                        bool needsPreviewRescue = NeedsPreviewRescue(activeFieldSample, family);
                        float effectiveMinHeat = ResolveEffectiveMinHeat(rule, family, activeFieldSample, needsPreviewRescue);
                        float effectiveDensityScale = ResolveEffectiveDensityScale(rule, family, activeFieldSample, needsPreviewRescue);
                        if (!deterministicClutter && heat < effectiveMinHeat)
                            continue;
                        if (collectDetailedDiagnostics)
                            heatPassedRules++;

                        float normalizedHeat = deterministicClutter
                            ? 1f
                            : math.saturate((heat - effectiveMinHeat) / math.max(0.0001f, 1f - effectiveMinHeat));
                        float spawnProbability = deterministicClutter
                            ? 1f
                            : math.saturate(normalizedHeat * (0.45f + math.clamp(effectiveDensityScale, 0.1f, 4f) * 0.18f));
                        bool needsSpawnRescue = minimumSpawnPlacements > 0 &&
                                                family != null &&
                                                family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
                        bool needsRescueTracking = needsPreviewRescue || needsSpawnRescue;
                        if (!TryPrepareScatterCandidateScoring(
                                activeFieldSample,
                                runtimeRule,
                                family,
                                cellPlacementCounters,
                                localGroundBudget,
                                localClusterBudget,
                                structureStride,
                                localStructureBudget,
                                spawnStride,
                                localSpawnBudget,
                                layerPlacementCounts,
                                clusterAccentCounts,
                                structureAccentCounts,
                                passiveSpawnCount,
                                predatorSpawnCount,
                                usesPatternAccentQuotas,
                                clusterRatioStart,
                                passiveSpawnMax,
                                predatorSpawnMax,
                                cellXIndex,
                                cellZIndex,
                                spawnProbability,
                                deterministicClutter,
                                needsRescueTracking,
                                size,
                                center,
                                collectDetailedDiagnostics,
                                ref gatePassedRules,
                                ref residencyPassedCandidates,
                                ref rejectedResidencyFamily,
                                ref rejectedResidencyDistance,
                                ref rejectedResidencyRadius,
                                out WorldPrefabFamilyProfile.ScatterLayer layer,
                                out int layerIndex,
                                out int layerPreferredFamilyIndex,
                                out bool rejectedByGate,
                                out ScatterCandidatePreview candidatePreview))
                        {
                            continue;
                        }

                        int secondaryLayerPreferredFamilyIndex = biomeTransitionContext.HasSecondary != 0
                            ? GetPreferredFamilyIndexForLayer(biomeTransitionContext.SecondaryProfile, family, layer)
                            : -1;
                        RegisterScatterBackendCellEligibility(ref backendCellState, layer);
                        RegisterScatterBackendCellSuppression(
                            ref backendCellState,
                            cellXIndex,
                            cellZIndex,
                            runtimeRule,
                            candidatePreview);

                        if (!TryResolveScatterCandidateScore(
                                activeFieldSample,
                                runtimeRule,
                                biomeScoreContext,
                                biomeTransitionContext,
                                patternScoreContext,
                                cellBiomeContext,
                                layerPreferredFamilyIndex,
                                secondaryLayerPreferredFamilyIndex,
                                spawnProbability,
                                heat,
                                needsRescueTracking,
                                cellCandidateBufferLimit,
                                ref worstCandidateIndex,
                                ref worstCandidateScore,
                                ref geologyBonusCache,
                                out float score))
                            continue;

                        if (!TryBuildCandidate(
                            cellXIndex,
                            cellZIndex,
                            activeFieldSample,
                            runtimeRule,
                            candidatePreview,
                            cellBiomeContextLabel,
                            heat,
                            score,
                            out ScatterCandidate candidate))
                            continue;

                        if (needsRescueTracking)
                        {
                            TrackRescueCandidate(
                                candidate,
                                needsPreviewRescue,
                                needsSpawnRescue,
                                ref rescueTrackingContext);
                        }

                        if (rejectedByGate)
                        {
                            if (collectDetailedDiagnostics)
                                postBuildGateRejectedCandidates++;
                            ReleasePlacement(candidate.Placement);
                            continue;
                        }

                        RetainPlacement(candidate.Placement);
                        cellCandidatesBeforePrune++;
                        if (collectDetailedDiagnostics)
                            queuedCandidates++;
                        RetainTopCandidate(
                            _candidateBuffer,
                            candidate,
                            cellCandidateBufferLimit,
                            ref worstCandidateIndex,
                            ref worstCandidateScore);

                        if (!hasTopCandidate || candidate.Score > topCandidate.Score)
                        {
                            topCandidate = candidate;
                            hasTopCandidate = true;
                        }

                        ReleasePlacement(candidate.Placement);
                    }
                }

                if (cellCandidatesBeforePrune > maxCandidatesBeforePrunePerCell)
                    maxCandidatesBeforePrunePerCell = cellCandidatesBeforePrune;

                if (_candidateBuffer.Count == 0)
                    continue;

                if (_candidateBuffer.Count > maxCandidatesAfterPrunePerCell)
                    maxCandidatesAfterPrunePerCell = _candidateBuffer.Count;

                if (_candidateBuffer.Count > 1)
                    SortCandidateBufferByScore(_candidateBuffer);

                    ScatterCellPlacementAcceptanceContext acceptanceContext = new ScatterCellPlacementAcceptanceContext
                    {
                        LocalGroundBudget = localGroundBudget,
                        LocalClusterBudget = localClusterBudget,
                        StructureStride = structureStride,
                        LocalStructureBudget = localStructureBudget,
                        SpawnStride = spawnStride,
                        LocalSpawnBudget = localSpawnBudget,
                        ClusterRatioStart = clusterRatioStart,
                        PassiveSpawnMax = passiveSpawnMax,
                        PredatorSpawnMax = predatorSpawnMax,
                        UsesPatternAccentQuotas = usesPatternAccentQuotas ? (byte)1 : (byte)0,
                        CollectDetailedDiagnostics = collectDetailedDiagnostics ? (byte)1 : (byte)0,
                        PlacementRegistrationContext = placementRegistrationContext
                    };
                    AcceptScatterCellCandidates(
                        ref cellPlacementCounters,
                        in acceptanceContext,
                        layerPlacementCounts,
                        clusterAccentCounts,
                        structureAccentCounts,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts,
                        ref classicParityAccumulator,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount);
                    _memory.ScatterBackendCellStates[cellIndex] = backendCellState;
                }
            }

            ReleaseCandidateListPlacements(_candidateBuffer);
            fieldSampler.EndScatterSamplingFrame();
            _debugBiomeInfluenceTransitionCells = biomeInfluenceTransitionCells;

            long samplingEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            HectonBiomeMatrixProfile dominantBiomeProfile = ResolveDominantBiomeMatrixProfile(sampledMatrixProfileCounts, debugBiomeProfile);

            int injectedSpawnRescuePlacements;
            int trackedSpawnRescueCandidates;
            using (_scatterProcessingRescueProfilerMarker.Auto())
            {
                ExecuteScatterRescuePass(
                    debugPattern,
                    dominantBiomeProfile,
                    clusterBudget,
                    structureStride,
                    spawnStride,
                    structureBudget,
                    spawnBudget,
                    layerPlacementCounts,
                    clusterAccentCounts,
                    structureAccentCounts,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts,
                    rescueTrackingContext,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    out injectedSpawnRescuePlacements,
                    out trackedSpawnRescueCandidates);
            }
            long rescueEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            using (_scatterProcessingRestoreProfilerMarker.Auto())
            {
                RestoreCompletedScatterSamplingPlacements(center, now);
            }
            long restoreEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            ScatterReconcileMetrics reconcileMetrics = ReconcileInstances(enableScatterRebuildProfiling);

            ApplyCompletedScatterSamplingDebugState(
                evaluatedCells,
                layerPlacementCounts,
                clusterAccentCounts,
                structureAccentCounts,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts,
                sampledMatrixBiomeCounts,
                sampledBiomeCounts,
                sampledPatternCounts,
                sampledZoneCounts,
                mapMagicSamples,
                sceneProbeLegacySamples,
                fallbackSamples,
                matchedScatterRules,
                heatPassedRules,
                gatePassedRules,
                residencyPassedCandidates,
                postBuildGateRejectedCandidates,
                queuedCandidates,
                rejectedResidencyFamily,
                rejectedResidencyDistance,
                rejectedResidencyRadius,
                maxCandidatesBeforePrunePerCell,
                maxCandidatesAfterPrunePerCell,
                trackedSpawnRescueCandidates,
                injectedSpawnRescuePlacements,
                debugPattern,
                dominantBiomeProfile,
                debugBiomeProfile,
                debugBiomeFamily,
                debugZone,
                debugResolvedZoneKind,
                debugGroundBudgetScale,
                debugClusterBudgetScale,
                debugStructureBudgetScale,
                debugSpawnBudgetScale,
                hasTopCandidate,
                topCandidate,
                passiveSpawnCount,
                predatorSpawnCount,
                collectDetailedDiagnostics);

            TryScheduleScatterBackendShadowPass(new ScatterBackendShadowScheduleContext(
                center,
                totalCells,
                groundBudget,
                clusterBudget,
                structureStride,
                spawnStride,
                classicParityAccumulator.ToReference()));

            RecordScatterRefreshSample();
            if (enableScatterRebuildProfiling)
            {
                long diagnosticsEndTimestamp = Stopwatch.GetTimestamp();
                CommitScatterRebuildProfile(
                    rebuildStartTimestamp,
                    samplingInputsEndTimestamp,
                    samplingCompleteEndTimestamp,
                    samplingEndTimestamp,
                    rescueEndTimestamp,
                    restoreEndTimestamp,
                    reconcileMetrics,
                    diagnosticsEndTimestamp,
                    evaluatedCells);
            }

            ResetSamplingState(HasPendingScatterReconcileWork() ? ScatterState.Spawning : ScatterState.Idle);
        }
        }

        private JobHandle ScheduleBiomeInfluencePackJob(int totalCells, JobHandle dependency)
        {
            if (_memory == null ||
                !_memory.BiomeInfluenceCells.IsCreated ||
                !_memory.BiomeInfluencePackedCells.IsCreated ||
                totalCells <= 0)
            {
                return dependency;
            }

            int packCellCount = math.min(totalCells, math.min(_memory.BiomeInfluenceCells.Length, _memory.BiomeInfluencePackedCells.Length));
            if (packCellCount <= 0)
                return dependency;

            BiomeInfluencePackJob packJob = new BiomeInfluencePackJob
            {
                Source = _memory.BiomeInfluenceCells,
                Destination = _memory.BiomeInfluencePackedCells,
                CellCount = packCellCount
            };

            return packJob.Schedule(packJob.CellCount, math.max(1, math.min(64, packJob.CellCount / 8)), dependency);
        }

        private void PublishBiomeInfluenceGrid(int totalCells)
        {
            if (_memory == null ||
                !_memory.BiomeInfluencePackedCells.IsCreated ||
                totalCells <= 0 ||
                fieldSampler == null ||
                !fieldSampler.TryUploadPackedBiomeInfluenceGrid(
                    _memory.BiomeInfluencePackedCells,
                    totalCells,
                    out GraphicsBuffer biomeInfluenceBuffer,
                    out int biomeInfluenceBufferCapacity))
            {
                Shader.SetGlobalInt(_ScatterBiomeInfluenceGridCountId, 0);
                _debugBiomeInfluenceGridCells = 0;
                _debugBiomeInfluenceGpuBufferCapacity = 0;
                return;
            }

            int originCellX = _samplingSnapshot.CenterCellX - _samplingRadiusCells;
            int originCellZ = _samplingSnapshot.CenterCellZ - _samplingRadiusCells;
            Shader.SetGlobalBuffer(_ScatterBiomeInfluenceGridId, biomeInfluenceBuffer);
            Shader.SetGlobalInt(_ScatterBiomeInfluenceGridCountId, totalCells);
            Shader.SetGlobalVector(
                _ScatterBiomeInfluenceGridOriginId,
                new Vector4(originCellX, originCellZ, _samplingCellDiameter, _samplingRadiusCells));
            Shader.SetGlobalVector(
                _ScatterBiomeInfluenceGridParamsId,
                new Vector4(_samplingCellSize, totalCells, biomeInfluenceBufferCapacity, 0f));

            _debugBiomeInfluenceGridCells = totalCells;
            _debugBiomeInfluenceGpuBufferCapacity = biomeInfluenceBufferCapacity;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct BiomeInfluencePackJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<WorldProceduralFieldSampler.BiomeInfluenceCell> Source;
            [WriteOnly, NoAlias] public NativeArray<uint> Destination;
            public int CellCount;

            public void Execute(int index)
            {
                if ((uint)index >= (uint)CellCount)
                    return;

                WorldProceduralFieldSampler.BiomeInfluenceCell cell = Source[index];
                Destination[index] = WorldProceduralFieldSampler.BiomeInfluenceCell.ExtractGpuPacked(in cell);
            }
        }

        private bool TryBuildScatterSamplingCompletionContext(out ScatterSamplingCompletionContext context)
        {
            context = default;
            if (_samplingTotalCells <= 0 || fieldSampler == null || _memory == null)
                return false;

            context.AbsoluteCenter = _samplingSnapshot.AbsoluteCenter;
            context.CellSize = _samplingCellSize;
            context.Now = _samplingNow;
            context.TotalCells = _samplingTotalCells;
            context.ClusterBudget = _samplingClusterBudget;
            context.StructureStride = _samplingStructureStride;
            context.StructureBudget = _samplingStructureBudget;
            context.SpawnStride = _samplingSpawnStride;
            context.SpawnBudget = _samplingSpawnBudget;
            context.GroundBudget = _samplingGroundBudget;
            context.RebuildStartTimestamp = _samplingRebuildStartTimestamp;
            context.SamplingInputsEndTimestamp = _samplingInputsEndTimestamp;
            context.LayerTopCandidates = _layerTopCandidatesBuffer;
            context.LayerTopValid = _layerTopValidBuffer;
            context.LayerPlacementCounts = _layerPlacementCountsBuffer;
            context.ClusterAccentCounts = _clusterAccentCountsBuffer;
            context.StructureAccentCounts = _structureAccentCountsBuffer;
            context.LayerFamilyCounts = _layerFamilyCountsBuffer;
            context.LayerBiomeCounts = _layerBiomeCountsBuffer;
            context.PlacementRegistrationContext = new ScatterPlacementRegistrationContext(
                _desiredPlacements,
                _retainedPlacements,
                _placementLastSeenTimes,
                context.Now);
            context.RescueTrackingContext = new ScatterRescueTrackingContext(
                context.StructureStride,
                context.SpawnStride,
                _groundRescueCandidates,
                _clusterRescueCandidates,
                _structureRescueCandidates,
                _spawnRescueCandidates,
                _clusterFertileCandidates,
                _clusterNestCandidates,
                _clusterResourceCandidates,
                _clusterShelterCandidates,
                _clusterHazardCandidates,
                _clusterDebrisCandidates,
                _clusterRockCandidates,
                _structureNaturalCandidates,
                _structureTechCandidates,
                _structureCaveCandidates,
                _structureBioCandidates,
                _passiveSpawnCandidates,
                _predatorSpawnCandidates);
            context.SampledMatrixProfileCounts = _sampledMatrixProfileCounts;
            context.SampledMatrixBiomeCounts = _sampledMatrixBiomeCounts;
            context.SampledBiomeCounts = _sampledBiomeCounts;
            context.SampledPatternCounts = _sampledPatternCounts;
            context.SampledZoneCounts = _sampledZoneCounts;
            context.RejectedResidencyFamily = "None";
            context.CollectDetailedDiagnostics = ShouldCollectScatterDetailedDiagnostics() ? (byte)1 : (byte)0;
            context.DebugPattern = WorldProceduralPattern.SedimentResources;
            context.DebugGroundBudgetScale = 1f;
            context.DebugClusterBudgetScale = 1f;
            context.DebugStructureBudgetScale = 1f;
            context.DebugSpawnBudgetScale = 1f;
            return true;
        }

        private void ExecuteScatterRescuePass(
            WorldProceduralPattern debugPattern,
            HectonBiomeMatrixProfile dominantBiomeProfile,
            int clusterBudget,
            int structureStride,
            int spawnStride,
            int structureBudget,
            int spawnBudget,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            ScatterRescueTrackingContext rescueTrackingContext,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            out int injectedSpawnRescuePlacements,
            out int trackedSpawnRescueCandidates)
        {
            ScatterRescueContext rescueContext = new ScatterRescueContext(
                debugPattern,
                dominantBiomeProfile,
                clusterBudget,
                structureStride,
                spawnStride,
                structureBudget,
                spawnBudget,
                layerPlacementCounts,
                clusterAccentCounts,
                structureAccentCounts,
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts,
                rescueTrackingContext);
            InjectRescuePlacementsIfNeeded(
                in rescueContext,
                ref passiveSpawnCount,
                ref predatorSpawnCount,
                out injectedSpawnRescuePlacements);
            trackedSpawnRescueCandidates = rescueTrackingContext.SpawnCandidates != null ? rescueTrackingContext.SpawnCandidates.Count : 0;
            ReleaseRescueCandidateBuffers();
        }

        private ScatterBiomeTransitionContext ResolveBiomeTransitionContext(
            in WorldProceduralFieldSampler.FieldSample fieldSample,
            WorldProceduralBiomeFamilyContextProfile primaryBiomeContext)
        {
            WorldProceduralFieldSampler.BiomeInfluenceCell influence = fieldSample.biomeInfluence;
            if (influence.Blend255 == 0)
                return default;

            HectonBiomeMatrixProfile secondaryProfile = fieldSample.secondaryBiomeProfile;
            if (secondaryProfile == null ||
                ReferenceEquals(secondaryProfile, fieldSample.biomeProfile))
            {
                return default;
            }

            HectonBiomeFamilyProfile secondaryFamily = secondaryProfile.familyProfile;
            WorldProceduralBiomeFamilyContextProfile secondaryBiomeContext = ResolveBiomeContextProfile(secondaryFamily, out _);
            ScatterBiomeScoreContext secondaryScoreContext = BuildScatterBiomeScoreContext(secondaryProfile);
            float secondaryWeight = influence.Blend255 * (1f / 255f);

            return new ScatterBiomeTransitionContext(
                true,
                secondaryProfile,
                secondaryFamily,
                secondaryBiomeContext,
                secondaryScoreContext,
                secondaryWeight);
        }

        private void ResolveTransitionBudgetScales(
            WorldProceduralPatternProfile cellPatternProfile,
            WorldProceduralBiomeFamilyContextProfile primaryBiomeContext,
            in ScatterBiomeTransitionContext biomeTransitionContext,
            out float groundBudgetScale,
            out float clusterBudgetScale,
            out float structureBudgetScale,
            out float spawnBudgetScale)
        {
            ResolveCombinedBudgetScales(
                cellPatternProfile,
                primaryBiomeContext,
                out groundBudgetScale,
                out clusterBudgetScale,
                out structureBudgetScale,
                out spawnBudgetScale);

            if (biomeTransitionContext.HasSecondary == 0)
                return;

            ResolveCombinedBudgetScales(
                cellPatternProfile,
                biomeTransitionContext.SecondaryBiomeContext,
                out float secondaryGroundBudgetScale,
                out float secondaryClusterBudgetScale,
                out float secondaryStructureBudgetScale,
                out float secondarySpawnBudgetScale);

            float transitionWeight = math.saturate(biomeTransitionContext.SecondaryWeight);
            groundBudgetScale = math.lerp(groundBudgetScale, secondaryGroundBudgetScale, transitionWeight);
            clusterBudgetScale = math.lerp(clusterBudgetScale, secondaryClusterBudgetScale, transitionWeight);
            structureBudgetScale = math.lerp(structureBudgetScale, secondaryStructureBudgetScale, transitionWeight);
            spawnBudgetScale = math.lerp(spawnBudgetScale, secondarySpawnBudgetScale, transitionWeight);
        }

        private static float ResolveTransitionBiomeMatrixScoreUpperBound(
            in ScatterRuntimeRuleEntry runtimeRule,
            bool hasPrimaryBiomeProfile,
            int primaryPreferredFamilyIndex,
            in ScatterBiomeTransitionContext biomeTransitionContext,
            int secondaryPreferredFamilyIndex,
            in ScatterPatternScoreContext patternScoreContext)
        {
            float primaryUpperBound = ResolveBiomeMatrixScoreUpperBound(
                runtimeRule,
                hasPrimaryBiomeProfile,
                primaryPreferredFamilyIndex,
                patternScoreContext);

            if (biomeTransitionContext.HasSecondary == 0)
                return primaryUpperBound;

            float secondaryUpperBound = ResolveBiomeMatrixScoreUpperBound(
                runtimeRule,
                biomeTransitionContext.SecondaryScoreContext.HasBiomeProfile != 0,
                secondaryPreferredFamilyIndex,
                patternScoreContext);
            return math.max(primaryUpperBound, secondaryUpperBound);
        }

        private bool TryPrepareScatterCandidateScoring(
            WorldProceduralFieldSampler.FieldSample activeFieldSample,
            ScatterRuntimeRuleEntry runtimeRule,
            WorldPrefabFamilyProfile family,
            ScatterCellPlacementCounters cellPlacementCounters,
            int localGroundBudget,
            int localClusterBudget,
            int structureStride,
            int localStructureBudget,
            int spawnStride,
            int localSpawnBudget,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount,
            bool usesPatternAccentQuotas,
            int clusterRatioStart,
            int passiveSpawnMax,
            int predatorSpawnMax,
            int cellXIndex,
            int cellZIndex,
            float spawnProbability,
            bool deterministicClutter,
            bool needsRescueTracking,
            float size,
            Vector3 center,
            bool collectDetailedDiagnostics,
            ref int gatePassedRules,
            ref int residencyPassedCandidates,
            ref string rejectedResidencyFamily,
            ref float rejectedResidencyDistance,
            ref float rejectedResidencyRadius,
            out WorldPrefabFamilyProfile.ScatterLayer layer,
            out int layerIndex,
            out int layerPreferredFamilyIndex,
            out bool rejectedByGate,
            out ScatterCandidatePreview candidatePreview)
        {
            candidatePreview = default;
            layer = runtimeRule.ScatterLayer;
            layerIndex = (int)layer;
            layerPreferredFamilyIndex = -1;
            rejectedByGate = false;

            int heightLayerIndex = ResolveHeightLayerIndex(activeFieldSample, runtimeRule);
            float gate = StablePlacementRandom01(cellXIndex, cellZIndex, runtimeRule.RuleIdHash, heightLayerIndex);
            rejectedByGate = deterministicClutter
                ? gate <= DeterministicClutterSpawnThreshold
                : gate > spawnProbability;
            if (rejectedByGate && (deterministicClutter || !needsRescueTracking))
                return false;

            if (!HasPatternLayerGlobalBudget(layer, layerPlacementCounts[layerIndex], _patternLayerTargetMaxBuffer))
                return false;

            int localStructureCount = heightLayerIndex == 0 ? cellPlacementCounters.StructureCountPrimary : cellPlacementCounters.StructureCountSecondary;
            int localSpawnCount = heightLayerIndex == 0 ? cellPlacementCounters.SpawnCountPrimary : cellPlacementCounters.SpawnCountSecondary;
            if (!HasLayerBudget(
                    activeFieldSample,
                    runtimeRule,
                    cellXIndex,
                    cellZIndex,
                    localGroundBudget,
                    localClusterBudget,
                    structureStride,
                    localStructureBudget,
                    spawnStride,
                    localSpawnBudget,
                    cellPlacementCounters.GroundCount,
                    cellPlacementCounters.ClusterCount,
                    localStructureCount,
                    localSpawnCount))
            {
                return false;
            }

            if (!CanAcceptPatternAccentBudget(
                    usesPatternAccentQuotas,
                    family,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount,
                    layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster],
                    layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure],
                    layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn],
                    _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster],
                    _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure],
                    _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn],
                    clusterRatioStart,
                    _clusterAccentRoleMaxRatioBuffer,
                    _structureAccentRoleMaxBuffer,
                    passiveSpawnMax,
                    predatorSpawnMax))
            {
                return false;
            }

            if (collectDetailedDiagnostics)
                gatePassedRules++;

            candidatePreview = BuildCandidatePreview(
                cellXIndex,
                cellZIndex,
                activeFieldSample,
                runtimeRule,
                size);
            if (!PassesEnvironmentalEnvelope(activeFieldSample, runtimeRule, in candidatePreview))
                return false;

            float residencyRadius = 0f;
            float residencyDistanceSqr = 0f;
            if (collectDetailedDiagnostics)
            {
                ResolveLayerRadii(runtimeRule.StreamingLayer, out _, out _, out residencyRadius);
                residencyDistanceSqr = GetHorizontalDistanceSqr(candidatePreview.Position, center);
            }

            if (!IsPlacementWithinResidency(candidatePreview.Position, runtimeRule.StreamingLayer, center))
            {
                if (collectDetailedDiagnostics && rejectedResidencyFamily == "None")
                {
                    rejectedResidencyFamily = family != null ? family.familyId : "None";
                    rejectedResidencyDistance = ResolveDiagnosticDistance(residencyDistanceSqr);
                    rejectedResidencyRadius = residencyRadius;
                }

                return false;
            }

            if (collectDetailedDiagnostics)
                residencyPassedCandidates++;

            layerPreferredFamilyIndex = GetPreferredFamilyIndexForLayer(activeFieldSample.biomeProfile, family, layer);
            return true;
        }

        private static float ResolveDiagnosticDistance(float distanceSqr)
        {
            if (!(distanceSqr > 0f))
                return 0f;

            if (!math.isfinite(distanceSqr))
                return float.PositiveInfinity;

            return distanceSqr * math.rsqrt(distanceSqr);
        }

        private ScatterSimulationCellState BuildScatterBackendCellState(
            in WorldProceduralFieldSampler.CellOutputData cellOutput)
        {
            ScatterSimulationDirtyFlags dirtyFlags = ScatterSimulationDirtyFlags.None;
            if (cellOutput.IsValid != 0)
                dirtyFlags = ScatterSimulationDirtyFlags.Heights | ScatterSimulationDirtyFlags.Candidates;

            return new ScatterSimulationCellState
            {
                CellKey = ComposeScatterGridKey(cellOutput.CellX, cellOutput.CellZ),
                CellX = cellOutput.CellX,
                CellZ = cellOutput.CellZ,
                Height = cellOutput.SeafloorHeight,
                HeightSource = cellOutput.SeafloorSource,
                BiomeInfluencePacked = cellOutput.BiomeInfluencePacked,
                Eligibility = ScatterSimulationEligibilityFlags.None,
                Suppression = ScatterSimulationSuppressionState.None,
                DirtyFlags = dirtyFlags
            };
        }

        private static void RegisterScatterBackendCellBudgetState(
            ref ScatterSimulationCellState cellState,
            int localGroundBudget,
            int localClusterBudget,
            int localStructureBudget,
            int localSpawnBudget)
        {
            if (localGroundBudget > 0 || localClusterBudget > 0 || localStructureBudget > 0 || localSpawnBudget > 0)
                cellState.DirtyFlags |= ScatterSimulationDirtyFlags.Quotas;
        }

        private static void RegisterScatterBackendCellEligibility(
            ref ScatterSimulationCellState cellState,
            WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            switch (layer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    cellState.Eligibility |= ScatterSimulationEligibilityFlags.Ground;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    cellState.Eligibility |= ScatterSimulationEligibilityFlags.Cluster;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    cellState.Eligibility |= ScatterSimulationEligibilityFlags.Structure;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    cellState.Eligibility |= ScatterSimulationEligibilityFlags.Spawn;
                    break;
            }

            if (cellState.Eligibility != ScatterSimulationEligibilityFlags.None)
                cellState.DirtyFlags |= ScatterSimulationDirtyFlags.Eligibility;
        }

        private void RegisterScatterBackendCellSuppression(
            ref ScatterSimulationCellState cellState,
            int cellXIndex,
            int cellZIndex,
            ScatterRuntimeRuleEntry runtimeRule,
            in ScatterCandidatePreview candidatePreview)
        {
            long placementKey = ComposePlacementKey(cellXIndex, cellZIndex, runtimeRule.RuleIdHash, candidatePreview.HeightLayerIndex);
            if (proceduralStateRegistry != null && proceduralStateRegistry.IsPlacementSuppressed(placementKey))
            {
                cellState.Suppression = ScatterSimulationSuppressionState.Suppressed;
                cellState.DirtyFlags |= ScatterSimulationDirtyFlags.Suppression;
                return;
            }

            if (cellState.Suppression == ScatterSimulationSuppressionState.None && _retainedPlacements.ContainsKey(placementKey))
            {
                cellState.Suppression = ScatterSimulationSuppressionState.Retained;
                cellState.DirtyFlags |= ScatterSimulationDirtyFlags.Suppression;
            }
        }

        private bool TryResolveScatterCandidateScore(
            WorldProceduralFieldSampler.FieldSample activeFieldSample,
            ScatterRuntimeRuleEntry runtimeRule,
            ScatterBiomeScoreContext biomeScoreContext,
            in ScatterBiomeTransitionContext biomeTransitionContext,
            ScatterPatternScoreContext patternScoreContext,
            WorldProceduralBiomeFamilyContextProfile cellBiomeContext,
            int layerPreferredFamilyIndex,
            int secondaryLayerPreferredFamilyIndex,
            float spawnProbability,
            float heat,
            bool needsRescueTracking,
            int cellCandidateBufferLimit,
            ref int worstCandidateIndex,
            ref float worstCandidateScore,
            ref GeologyBonusCache geologyBonusCache,
            out float score)
        {
            score = 0f;
            float baseScore = spawnProbability
                + heat
                + runtimeRule.ScoreBaseBonus
                + runtimeRule.AcceptedFamilyAffinityBonus
                + GetTectonicSpineRockBoulderScoreBonus(activeFieldSample, runtimeRule.Family);
            float combinedPatternScore = GetCombinedPatternScoreBonus(activeFieldSample.resolvedPattern, runtimeRule);
            float biomeContextScore = GetBiomeContextBonus(cellBiomeContext, runtimeRule);
            float biomeSignatureScore = GetBiomeSignatureScoreBonus(runtimeRule, layerPreferredFamilyIndex, patternScoreContext);
            float softWaterStructureScore = GetSoftWaterStructureFamilyBonus(runtimeRule, biomeScoreContext, layerPreferredFamilyIndex, patternScoreContext);
            float landmarkSoftWaterStructureScore = GetLandmarkSoftWaterStructureFamilyBonus(runtimeRule, biomeScoreContext, layerPreferredFamilyIndex, patternScoreContext);
            float scoreBeforeBiomeMatrixAndGeology = baseScore
                + combinedPatternScore
                + biomeContextScore
                + biomeSignatureScore
                + softWaterStructureScore
                + landmarkSoftWaterStructureScore;
            bool canPruneByScore = !needsRescueTracking && cellCandidateBufferLimit > 0 && _candidateBuffer.Count >= cellCandidateBufferLimit;
            if (canPruneByScore)
            {
                if (worstCandidateIndex < 0 || worstCandidateIndex >= _candidateBuffer.Count)
                    RefreshWorstCandidate(_candidateBuffer, out worstCandidateIndex, out worstCandidateScore);

                float scoreUpperBound = scoreBeforeBiomeMatrixAndGeology
                    + ResolveTransitionBiomeMatrixScoreUpperBound(
                        runtimeRule,
                        activeFieldSample.biomeProfile != null,
                        layerPreferredFamilyIndex,
                        biomeTransitionContext,
                        secondaryLayerPreferredFamilyIndex,
                        patternScoreContext)
                    + runtimeRule.GeologyScoreScale;
                if (scoreUpperBound <= worstCandidateScore)
                    return false;
            }

            float biomeMatrixScore = GetBiomeMatrixBonus(activeFieldSample.resolvedPattern, activeFieldSample.biomeProfile, runtimeRule, biomeScoreContext, layerPreferredFamilyIndex, patternScoreContext);
            if (biomeTransitionContext.HasSecondary != 0)
            {
                float secondaryBiomeMatrixScore = GetBiomeMatrixBonus(
                    activeFieldSample.resolvedPattern,
                    biomeTransitionContext.SecondaryProfile,
                    runtimeRule,
                    biomeTransitionContext.SecondaryScoreContext,
                    secondaryLayerPreferredFamilyIndex,
                    patternScoreContext);
                biomeMatrixScore = math.lerp(
                    biomeMatrixScore,
                    secondaryBiomeMatrixScore,
                    math.saturate(biomeTransitionContext.SecondaryWeight));
            }

            float scoreWithoutGeology = scoreBeforeBiomeMatrixAndGeology + biomeMatrixScore;
            if (canPruneByScore && scoreWithoutGeology + runtimeRule.GeologyScoreScale <= worstCandidateScore)
                return false;

            score = scoreWithoutGeology
                + GetCachedGenerativeGeologyContextBonus(
                    activeFieldSample,
                    runtimeRule,
                    ref geologyBonusCache);
            if (canPruneByScore && score <= worstCandidateScore)
                return false;

            return true;
        }

        private void AcceptScatterCellCandidates(
            ref ScatterCellPlacementCounters cellPlacementCounters,
            in ScatterCellPlacementAcceptanceContext acceptanceContext,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            ref ScatterClassicParityAccumulator classicParityAccumulator,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount)
        {
            if (TryEvaluateScatterCellCandidateAcceptanceBatch(
                    ref cellPlacementCounters,
                    in acceptanceContext,
                    layerPlacementCounts,
                    clusterAccentCounts,
                    structureAccentCounts,
                    passiveSpawnCount,
                    predatorSpawnCount))
            {
                NativeArray<byte> acceptanceResults = _memory.CandidateAcceptanceBatchResults.AsArray();
                int candidateCount = math.min(_candidateBuffer.Count, acceptanceResults.Length);
                for (int i = 0; i < candidateCount; i++)
                {
                    if (acceptanceResults[i] == 0)
                        continue;

                    ScatterCandidate candidate = _candidateBuffer[i];
                    WorldPrefabFamilyProfile.ScatterLayer layer = candidate.Family.scatterLayer;
                    int layerIndex = (int)layer;
                    if (!TryRegisterDesiredPlacement(candidate.Placement, in acceptanceContext.PlacementRegistrationContext))
                        continue;

                    ApplyAcceptedScatterCellCandidate(
                        ref cellPlacementCounters,
                        candidate,
                        layer,
                        layerIndex,
                        acceptanceContext.StructureStride,
                        acceptanceContext.SpawnStride,
                        layerPlacementCounts,
                        clusterAccentCounts,
                        structureAccentCounts,
                        layerTopCandidates,
                        layerTopValid,
                        layerFamilyCounts,
                        layerBiomeCounts,
                        ref classicParityAccumulator,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount,
                        acceptanceContext.CollectDetailedDiagnostics != 0);
                }

                return;
            }

            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                ScatterCandidate candidate = _candidateBuffer[i];
                WorldPrefabFamilyProfile.ScatterLayer layer = candidate.Family.scatterLayer;
                int layerIndex = (int)layer;
                if (!HasPatternLayerGlobalBudget(layer, layerPlacementCounts[layerIndex], _patternLayerTargetMaxBuffer))
                    continue;

                int candidateStructureCount = candidate.Placement.HeightLayerIndex == 0
                    ? cellPlacementCounters.StructureCountPrimary
                    : cellPlacementCounters.StructureCountSecondary;
                int candidateSpawnCount = candidate.Placement.HeightLayerIndex == 0
                    ? cellPlacementCounters.SpawnCountPrimary
                    : cellPlacementCounters.SpawnCountSecondary;
                if (!HasLayerBudget(
                        candidate,
                        acceptanceContext.LocalGroundBudget,
                        acceptanceContext.LocalClusterBudget,
                        acceptanceContext.StructureStride,
                        acceptanceContext.LocalStructureBudget,
                        acceptanceContext.SpawnStride,
                        acceptanceContext.LocalSpawnBudget,
                        cellPlacementCounters.GroundCount,
                        cellPlacementCounters.ClusterCount,
                        candidateStructureCount,
                        candidateSpawnCount))
                {
                    continue;
                }

                if (!CanAcceptPatternAccentBudget(
                        acceptanceContext.UsesPatternAccentQuotas != 0,
                        candidate,
                        clusterAccentCounts,
                        structureAccentCounts,
                        passiveSpawnCount,
                        predatorSpawnCount,
                        layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster],
                        layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure],
                        layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn],
                        _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster],
                        _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure],
                        _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn],
                        acceptanceContext.ClusterRatioStart,
                        _clusterAccentRoleMaxRatioBuffer,
                        _structureAccentRoleMaxBuffer,
                        acceptanceContext.PassiveSpawnMax,
                        acceptanceContext.PredatorSpawnMax))
                {
                    continue;
                }

                if (!CanAcceptCandidateNative(candidate))
                    continue;

                if (!TryRegisterDesiredPlacement(candidate.Placement, in acceptanceContext.PlacementRegistrationContext))
                    continue;

                ApplyAcceptedScatterCellCandidate(
                    ref cellPlacementCounters,
                    candidate,
                    layer,
                    layerIndex,
                    acceptanceContext.StructureStride,
                    acceptanceContext.SpawnStride,
                    layerPlacementCounts,
                    clusterAccentCounts,
                    structureAccentCounts,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts,
                    ref classicParityAccumulator,
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    acceptanceContext.CollectDetailedDiagnostics != 0);
            }
        }

        private bool HasFloraStreamCellBiomeQuota(ScatterPlacement placement)
        {
            if (!IsFloraQuotaPlacement(placement))
                return true;

            if (_memory == null)
                return true;

            long key = ComposeFloraStreamCellBiomeKey(
                placement.CellX,
                placement.CellZ,
                placement.BiomeProfile != null ? placement.BiomeProfile.matrixIndex : 0);
            if (!_memory.FloraStreamCellBiomeCounts.TryGetValue(key, out int count) ||
                count < MaxFloraInstancesPerStreamCellPerBiome)
            {
                return true;
            }

            _debugFloraQuotaRejectedCandidates++;
            return false;
        }

        private void RegisterFloraStreamCellBiomeQuota(ScatterPlacement placement)
        {
            if (!IsFloraQuotaPlacement(placement) || _memory == null)
                return;

            long key = ComposeFloraStreamCellBiomeKey(
                placement.CellX,
                placement.CellZ,
                placement.BiomeProfile != null ? placement.BiomeProfile.matrixIndex : 0);
            _memory.FloraStreamCellBiomeCounts.TryGetValue(key, out int count);
            _memory.FloraStreamCellBiomeCounts[key] = count + 1;
        }

        private static bool IsFloraQuotaPlacement(ScatterPlacement placement)
        {
            if (placement == null || placement.Family == null)
                return false;

            return placement.Family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Kelp ||
                   placement.Family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Plant ||
                   placement.Family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.Coral;
        }

        private static long ComposeFloraStreamCellBiomeKey(int cellX, int cellZ, int biomeId)
        {
            unchecked
            {
                ulong packedCellX = (uint)cellX & 0xFFFFFUL;
                ulong packedCellZ = (uint)cellZ & 0xFFFFFUL;
                ulong packedBiome = (uint)biomeId & 0xFFUL;
                return (long)(packedCellX | (packedCellZ << 20) | (packedBiome << 40));
            }
        }

        private void ApplyAcceptedScatterCellCandidate(
            ref ScatterCellPlacementCounters cellPlacementCounters,
            ScatterCandidate candidate,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int layerIndex,
            int structureStride,
            int spawnStride,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            ref ScatterClassicParityAccumulator classicParityAccumulator,
            ref int passiveSpawnCount,
            ref int predatorSpawnCount,
            bool collectDetailedDiagnostics)
        {
            layerPlacementCounts[layerIndex]++;
            classicParityAccumulator.Register(candidate, layer);
            if (collectDetailedDiagnostics)
            {
                RegisterLayerFamilyCount(layerFamilyCounts, layer, candidate.Family);
                RegisterLayerBiomeCount(layerBiomeCounts, layer, candidate.Placement.BiomeFamily);
            }

            if (!layerTopValid[layerIndex] || candidate.Score > layerTopCandidates[layerIndex].Score)
            {
                layerTopCandidates[layerIndex] = candidate;
                layerTopValid[layerIndex] = true;
            }

            RegisterAccentAndSpawnCounts(candidate.Family, clusterAccentCounts, structureAccentCounts, ref passiveSpawnCount, ref predatorSpawnCount);

            switch (layer)
            {
                case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                    cellPlacementCounters.GroundCount++;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                    cellPlacementCounters.ClusterCount++;
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                    if (candidate.Placement.HeightLayerIndex == 0)
                        cellPlacementCounters.StructureCountPrimary++;
                    else
                        cellPlacementCounters.StructureCountSecondary++;
                    RegisterWindowPlacement(candidate.Placement.CellX, candidate.Placement.CellZ, structureStride, candidate.Placement.HeightLayerIndex, _structureWindowCounts);
                    break;
                case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                    if (candidate.Placement.HeightLayerIndex == 0)
                        cellPlacementCounters.SpawnCountPrimary++;
                    else
                        cellPlacementCounters.SpawnCountSecondary++;
                    RegisterWindowPlacement(candidate.Placement.CellX, candidate.Placement.CellZ, spawnStride, candidate.Placement.HeightLayerIndex, _spawnWindowCounts);
                    break;
            }
        }

        private void RestoreCompletedScatterSamplingPlacements(Vector3 center, float now)
        {
            ScatterRetentionRestoreContext retentionRestoreContext = new ScatterRetentionRestoreContext(
                _desiredPlacements,
                _retainedPlacements,
                _placementLastSeenTimes,
                center,
                now,
                math.max(0.25f, missingPlacementGraceSeconds));
            RestoreRecentDesiredPlacements(in retentionRestoreContext);
        }

        private void ApplyCompletedScatterSamplingDebugState(
            int evaluatedCells,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            Dictionary<string, int> sampledMatrixBiomeCounts,
            Dictionary<string, int> sampledBiomeCounts,
            Dictionary<string, int> sampledPatternCounts,
            Dictionary<string, int> sampledZoneCounts,
            int mapMagicSamples,
            int sceneProbeLegacySamples,
            int fallbackSamples,
            int matchedScatterRules,
            int heatPassedRules,
            int gatePassedRules,
            int residencyPassedCandidates,
            int postBuildGateRejectedCandidates,
            int queuedCandidates,
            string rejectedResidencyFamily,
            float rejectedResidencyDistance,
            float rejectedResidencyRadius,
            int maxCandidatesBeforePrunePerCell,
            int maxCandidatesAfterPrunePerCell,
            int trackedSpawnRescueCandidates,
            int injectedSpawnRescuePlacements,
            WorldProceduralPattern debugPattern,
            HectonBiomeMatrixProfile dominantBiomeProfile,
            HectonBiomeMatrixProfile debugBiomeProfile,
            HectonBiomeFamilyProfile debugBiomeFamily,
            WorldZoneAnchor debugZone,
            WorldZoneAnchor.ZoneKind debugResolvedZoneKind,
            float debugGroundBudgetScale,
            float debugClusterBudgetScale,
            float debugStructureBudgetScale,
            float debugSpawnBudgetScale,
            bool hasTopCandidate,
            ScatterCandidate topCandidate,
            int passiveSpawnCount,
            int predatorSpawnCount,
            bool collectDetailedDiagnostics)
        {
            _debugReady = true;
            _debugEvaluatedCells = evaluatedCells;
            _debugDesiredPlacements = _desiredPlacements.Count;
            _debugActivePlacements = _activeInstances.Count + _activeGpuiFloraPlacements;
            _debugActiveGpuiFloraPlacements = _activeGpuiFloraPlacements;
            _debugFloraGpuiPrototypeCount = _floraGpuiKnownPrototypes.Count;
            _debugFloraGpuiReady = floraGpuiManager != null;
            _debugGroundPlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Ground];
            _debugClusterPlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Cluster];
            _debugStructurePlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Structure];
            _debugSpawnPlacements = layerPlacementCounts[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn];
            _debugMapMagicSamples = mapMagicSamples;
            _debugSceneProbeLegacySamples = sceneProbeLegacySamples;
            _debugFallbackSamples = fallbackSamples;
            _debugMatchedScatterRules = matchedScatterRules;
            _debugHeatPassedRules = heatPassedRules;
            _debugGatePassedRules = gatePassedRules;
            _debugResidencyPassedCandidates = residencyPassedCandidates;
            _debugPostBuildGateRejectedCandidates = postBuildGateRejectedCandidates;
            _debugQueuedCandidates = queuedCandidates;
            _debugRejectedResidencyFamily = rejectedResidencyFamily;
            _debugRejectedResidencyDistance = rejectedResidencyDistance;
            _debugRejectedResidencyRadius = rejectedResidencyRadius;
            _debugMaxCandidatesBeforePrunePerCell = maxCandidatesBeforePrunePerCell;
            _debugMaxCandidatesAfterPrunePerCell = maxCandidatesAfterPrunePerCell;
            _debugTrackedSpawnRescueCandidates = trackedSpawnRescueCandidates;
            _debugInjectedSpawnRescuePlacements = injectedSpawnRescuePlacements;
            _scatterRefreshSampleState.UsedFallbackOnly = evaluatedCells > 0 && fallbackSamples >= evaluatedCells ? (byte)1 : (byte)0;
            _debugTargetGroundMin = ResolveMinimumGroundPlacements(debugPattern, dominantBiomeProfile);
            _debugTargetGroundMax = ResolvePatternLayerTargetMax(debugPattern, dominantBiomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Ground);
            _debugTargetClusterMin = ResolveMinimumClusterPlacements(debugPattern, dominantBiomeProfile);
            _debugTargetClusterMax = ResolvePatternLayerTargetMax(debugPattern, dominantBiomeProfile, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
            _debugTargetStructureMin = ResolvePatternStructureTargetMin(debugPattern, dominantBiomeProfile);
            _debugTargetStructureMax = ResolvePatternStructureTargetMax(debugPattern, dominantBiomeProfile);
            _debugTargetSpawnMin = ResolvePatternSpawnTargetMin(debugPattern, dominantBiomeProfile);
            _debugTargetSpawnMax = ResolvePatternSpawnTargetMax(debugPattern, dominantBiomeProfile);
            _debugPatternGroundBudgetScale = debugGroundBudgetScale;
            _debugPatternClusterBudgetScale = debugClusterBudgetScale;
            _debugPatternStructureBudgetScale = debugStructureBudgetScale;
            _debugPatternSpawnBudgetScale = debugSpawnBudgetScale;
            _debugTopHeat = hasTopCandidate ? topCandidate.Heat : 0f;
            _debugTopScore = hasTopCandidate ? topCandidate.Score : 0f;
            _debugClusterFertileGrowthCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.FertileGrowth);
            _debugClusterBiologicalNestCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.BiologicalNest);
            _debugClusterResourcePocketCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.ResourcePocket);
            _debugClusterShelterPocketCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.ShelterPocket);
            _debugClusterHazardPocketCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.HazardPocket);
            _debugClusterDebrisFieldCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.DebrisField);
            _debugClusterRockCoverCount = GetClusterAccentCount(clusterAccentCounts, WorldPrefabFamilyProfile.ClusterAccentRole.RockCover);
            _debugStructureNaturalLandmarkCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.NaturalLandmark);
            _debugStructureTechFragmentCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.TechFragment);
            _debugStructureCaveReadCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.CaveRead);
            _debugStructureBiologicalSilhouetteCount = GetStructureAccentCount(structureAccentCounts, WorldPrefabFamilyProfile.StructureAccentRole.BiologicalSilhouette);
            _debugSpawnPassiveCount = passiveSpawnCount;
            _debugSpawnPredatorCount = predatorSpawnCount;
#if UNITY_EDITOR
            ApplyCompletedScatterSamplingEditorDebugState(
                layerTopCandidates,
                layerTopValid,
                layerFamilyCounts,
                layerBiomeCounts,
                sampledMatrixBiomeCounts,
                sampledBiomeCounts,
                sampledPatternCounts,
                sampledZoneCounts,
                debugPattern,
                dominantBiomeProfile,
                debugBiomeProfile,
                debugBiomeFamily,
                debugZone,
                debugResolvedZoneKind,
                hasTopCandidate,
                topCandidate,
                collectDetailedDiagnostics);
#endif
        }

#if UNITY_EDITOR
        private void ApplyCompletedScatterSamplingEditorDebugState(
            ScatterCandidate[] layerTopCandidates,
            bool[] layerTopValid,
            Dictionary<string, int>[] layerFamilyCounts,
            Dictionary<string, int>[] layerBiomeCounts,
            Dictionary<string, int> sampledMatrixBiomeCounts,
            Dictionary<string, int> sampledBiomeCounts,
            Dictionary<string, int> sampledPatternCounts,
            Dictionary<string, int> sampledZoneCounts,
            WorldProceduralPattern debugPattern,
            HectonBiomeMatrixProfile dominantBiomeProfile,
            HectonBiomeMatrixProfile debugBiomeProfile,
            HectonBiomeFamilyProfile debugBiomeFamily,
            WorldZoneAnchor debugZone,
            WorldZoneAnchor.ZoneKind debugResolvedZoneKind,
            bool hasTopCandidate,
            ScatterCandidate topCandidate,
            bool collectDetailedDiagnostics)
        {
            if (collectDetailedDiagnostics)
            {
                _debugZone = debugZone != null ? debugZone.ZoneLabel : ResolveSamplingSyntheticZoneDebugLabel(debugResolvedZoneKind);
                _debugBiomeMatrixProfile = dominantBiomeProfile != null ? dominantBiomeProfile.biomeName : ResolveBiomeMatrixLabel(debugBiomeProfile);
                _debugBiomeFamily = debugBiomeFamily != null ? debugBiomeFamily.familyLabel : "None";
                _debugPattern = GetPatternLabel(debugPattern);
                WorldProceduralPatternProfile debugPatternProfile = ResolvePatternProfile(debugPattern, out bool usedFallbackPatternProfile);
                WorldProceduralBiomeFamilyContextProfile debugBiomeContextProfile = ResolveBiomeContextProfile(debugBiomeFamily, out bool usedFallbackBiomeContextProfile);
                _debugResolvedPatternProfile = debugPatternProfile != null ? debugPatternProfile.label : "None";
                _debugUsedFallbackPatternProfile = usedFallbackPatternProfile;
                _debugResolvedBiomeContextProfile = debugBiomeContextProfile != null ? debugBiomeContextProfile.label : "None";
                _debugUsedFallbackBiomeContextProfile = usedFallbackBiomeContextProfile;
                _debugTopRule = hasTopCandidate && topCandidate.Rule != null ? topCandidate.Rule.ruleLabel : "None";
                _debugTopFamily = hasTopCandidate && topCandidate.Family != null ? topCandidate.Family.familyLabel : "None";
                _debugTopHeatmap = hasTopCandidate ? topCandidate.HeatmapChannel : "None";
                _debugGroundTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Ground);
                _debugClusterTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Cluster);
                _debugStructureTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Structure);
                _debugSpawnTopFamily = ResolveLayerTopFamily(layerTopCandidates, layerTopValid, WorldPrefabFamilyProfile.ScatterLayer.Spawn);
                _debugClusterDominantAccentRole = ResolveDominantClusterAccentRole(_clusterAccentCountsBuffer, out _debugClusterDominantAccentCount);
                _debugStructureDominantAccentRole = ResolveDominantStructureAccentRole(_structureAccentCountsBuffer, out _debugStructureDominantAccentCount);
                _debugSampleDominantMatrixBiome = ResolveDominantCounter(sampledMatrixBiomeCounts, out _debugSampleDominantMatrixCount);
                _debugGroundDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground, out _debugGroundDominantCount);
                _debugClusterDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, out _debugClusterDominantCount);
                _debugStructureDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Structure, out _debugStructureDominantCount);
                _debugSpawnDominantFamily = ResolveDominantLayerFamily(layerFamilyCounts, WorldPrefabFamilyProfile.ScatterLayer.Spawn, out _debugSpawnDominantCount);
                _debugGroundDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground, out _);
                _debugClusterDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster, out _);
                _debugStructureDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Structure, out _);
                _debugSpawnDominantBiomeFamily = ResolveDominantLayerFamily(layerBiomeCounts, WorldPrefabFamilyProfile.ScatterLayer.Spawn, out _);
                _debugSampleDominantBiomeFamily = ResolveDominantCounter(sampledBiomeCounts, out _debugSampleDominantBiomeCount);
                _debugSampleDominantPattern = ResolveDominantCounter(sampledPatternCounts, out _debugSampleDominantPatternCount);
                _debugSampleDominantZone = ResolveDominantCounter(sampledZoneCounts, out _debugSampleDominantZoneCount);
                return;
            }

            _debugZone = "Disabled";
            _debugBiomeMatrixProfile = "Disabled";
            _debugBiomeFamily = "Disabled";
            _debugPattern = "Disabled";
            _debugResolvedPatternProfile = "Disabled";
            _debugUsedFallbackPatternProfile = false;
            _debugResolvedBiomeContextProfile = "Disabled";
            _debugUsedFallbackBiomeContextProfile = false;
            _debugTopRule = "Disabled";
            _debugTopFamily = "Disabled";
            _debugTopHeatmap = "Disabled";
            _debugGroundTopFamily = "Disabled";
            _debugClusterTopFamily = "Disabled";
            _debugStructureTopFamily = "Disabled";
            _debugSpawnTopFamily = "Disabled";
            _debugClusterDominantAccentRole = "Disabled";
            _debugStructureDominantAccentRole = "Disabled";
            _debugClusterDominantAccentCount = 0;
            _debugStructureDominantAccentCount = 0;
            _debugSampleDominantMatrixBiome = "Disabled";
            _debugGroundDominantFamily = "Disabled";
            _debugClusterDominantFamily = "Disabled";
            _debugStructureDominantFamily = "Disabled";
            _debugSpawnDominantFamily = "Disabled";
            _debugGroundDominantBiomeFamily = "Disabled";
            _debugClusterDominantBiomeFamily = "Disabled";
            _debugStructureDominantBiomeFamily = "Disabled";
            _debugSpawnDominantBiomeFamily = "Disabled";
            _debugSampleDominantBiomeFamily = "Disabled";
            _debugSampleDominantPattern = "Disabled";
            _debugSampleDominantZone = "Disabled";
            _debugGroundDominantCount = 0;
            _debugClusterDominantCount = 0;
            _debugStructureDominantCount = 0;
            _debugSpawnDominantCount = 0;
            _debugSampleDominantMatrixCount = 0;
            _debugSampleDominantBiomeCount = 0;
            _debugSampleDominantPatternCount = 0;
            _debugSampleDominantZoneCount = 0;
        }

        private static string ResolveSamplingSyntheticZoneDebugLabel(WorldZoneAnchor.ZoneKind zoneKind)
        {
            switch (zoneKind)
            {
                case WorldZoneAnchor.ZoneKind.Resources:
                    return "Synthetic:Resources";
                case WorldZoneAnchor.ZoneKind.Fabrication:
                    return "Synthetic:Fabrication";
                case WorldZoneAnchor.ZoneKind.Trial:
                    return "Synthetic:Trial";
                case WorldZoneAnchor.ZoneKind.Construction:
                    return "Synthetic:Construction";
                case WorldZoneAnchor.ZoneKind.Power:
                    return "Synthetic:Power";
                case WorldZoneAnchor.ZoneKind.Service:
                    return "Synthetic:Service";
                case WorldZoneAnchor.ZoneKind.Progression:
                    return "Synthetic:Progression";
                case WorldZoneAnchor.ZoneKind.Combat:
                    return "Synthetic:Combat";
                case WorldZoneAnchor.ZoneKind.Navigation:
                    return "Synthetic:Navigation";
                default:
                    return "Synthetic:Generic";
            }
        }
#endif
    }
}
