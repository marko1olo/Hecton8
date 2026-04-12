using System.Collections.Generic;
using System.Diagnostics;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
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

                        if (!_samplingJobHandle.IsCompleted)
                            return true;

                        _samplingJobHandle.Complete();
                        _isSamplingJobRunning = false;
                        _scatterState = ScatterState.Processing;
                        ProcessCompletedScatterSampling();
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
                ResolveReferences();
                ForceRefreshProceduralContext();
                EnsureCandidateMapsInitialized();
                RefreshRuntimeStreamingSettings();

                IReadOnlyList<WorldProceduralPlacementRule> rules = proceduralFillDirector != null ? proceduralFillDirector.Rules : null;
                if (playerTransform == null || fieldSampler == null || rules == null || rules.Count == 0)
                {
                    PublishFaunaRegistrySnapshot();
                    ResetDiagnostics();
                    _samplingTotalCells = 0;
                    _samplingSnapshot = default;
                    return true;
                }

                ReleasePlacementDictionaryValues(_desiredPlacements);
                _faunaSnapshotDirty = true;
                _structureWindowCounts.Clear();
                _spawnWindowCounts.Clear();
                ReleaseCandidateListPlacements(_candidateBuffer);
                ResetPlacementGrid();
                PrepareRuntimeRuleBuffer(rules);
                ClearScatterWorkingBuffers();

                float size = Mathf.Max(6f, _runtimeCellSize);
                int radius = Mathf.Max(2, _runtimeRadiusCells);
                float now = Time.unscaledTime;
                EvictStaleRetainedPlacements(now);
                int groundBudget = ResolveRuntimeBudget(groundPlacementsPerCell, WorldStreamingLayer.Flora, 0, 4);
                int clusterBudget = ResolveRuntimeBudget(clusterPlacementsPerCell, WorldStreamingLayer.Debris, 0, 3);
                int structureStride = Mathf.Max(2, structureCellStride);
                int structureBudget = ResolveRuntimeBudget(structurePlacementsPerWindow, WorldStreamingLayer.Construction, 0, 2);
                int spawnStride = Mathf.Max(2, spawnCellStride);
                int spawnBudget = ResolveRuntimeBudget(spawnPlacementsPerWindow, WorldStreamingLayer.Fauna, 0, 2);
                Vector3 center = playerTransform.position;
                int centerX = WorldToScatterCellIndex(center.x, size);
                int centerZ = WorldToScatterCellIndex(center.z, size);
                int cellDiameter = (radius * 2) + 1;
                int totalCells = cellDiameter * cellDiameter;

                EnsureScatterWindowBudgetCapacity(_structureWindowCounts, EstimateScatterWindowCapacity(cellDiameter, structureStride));
                EnsureScatterWindowBudgetCapacity(_spawnWindowCounts, EstimateScatterWindowCapacity(cellDiameter, spawnStride));
                EnsureCellSamplingArrayCapacity(totalCells);
                if (_memory == null)
                {
                    ResetDiagnostics();
                    return true;
                }

                _samplingSnapshot = new SamplingSnapshot(center, centerX, centerZ, now);
                _samplingTotalCells = totalCells;
                _samplingCellDiameter = cellDiameter;
                _samplingRadiusCells = radius;
                _samplingCellSize = size;
                _samplingNow = now;
                _samplingGroundBudget = groundBudget;
                _samplingClusterBudget = clusterBudget;
                _samplingStructureStride = structureStride;
                _samplingStructureBudget = structureBudget;
                _samplingSpawnStride = spawnStride;
                _samplingSpawnBudget = spawnBudget;
                _samplingRebuildStartTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

                fieldSampler.BeginScatterSamplingFrame();
                using (_scatterSamplingInputBuildProfilerMarker.Auto())
                {
                    int cellCursor = 0;
                    for (int z = -radius; z <= radius; z++)
                    {
                        for (int x = -radius; x <= radius; x++)
                        {
                            int cellXIndex = centerX + x;
                            int cellZIndex = centerZ + z;
                            Vector3 sampleOrigin = new Vector3(
                                (cellXIndex + 0.5f) * size,
                                center.y,
                                (cellZIndex + 0.5f) * size);
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
                using (_scatterSamplingScheduleProfilerMarker.Auto())
                {
                    _samplingJobHandle = fieldSampler.ScheduleCellSamplingJob(_memory.CellSamplingInputs, _memory.CellSamplingOutputs, totalCells);
                }

                _isSamplingJobRunning = true;
                _scatterState = ScatterState.Sampling;
                return true;
            }
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
                while (HasPendingScatterReconcileWork())
                    ContinuePendingScatterReconcile();

                _scatterState = ScatterState.Idle;
            }

            if (_isSamplingJobRunning)
            {
                // COLD SYNC JOB: editor preview and bootstrap prime require immediate scatter output before continuing.
                _samplingJobHandle.Complete();
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
            _samplingJobHandle.Complete();
            _isSamplingJobRunning = false;
            _scatterState = ScatterState.Processing;
            ProcessCompletedScatterSampling();
            return true;
        }

        private void ProcessCompletedScatterSampling()
        {
            using (_scatterProcessingProfilerMarker.Auto())
            {
            if (_samplingTotalCells <= 0 || fieldSampler == null || _memory == null)
            {
                ResetSamplingState();
                return;
            }

            Vector3 center = _samplingSnapshot.PlayerPosition;
            float size = _samplingCellSize;
            float now = _samplingNow;
            int totalCells = _samplingTotalCells;
            int clusterBudget = _samplingClusterBudget;
            int structureStride = _samplingStructureStride;
            int structureBudget = _samplingStructureBudget;
            int spawnStride = _samplingSpawnStride;
            int spawnBudget = _samplingSpawnBudget;
            int groundBudget = _samplingGroundBudget;
            long rebuildStartTimestamp = _samplingRebuildStartTimestamp;
            long samplingInputsEndTimestamp = _samplingInputsEndTimestamp;
            long samplingCompleteEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;
            int evaluatedCells = 0;
            ScatterCandidate topCandidate = default;
            bool hasTopCandidate = false;
            ScatterCandidate[] layerTopCandidates = _layerTopCandidatesBuffer;
            bool[] layerTopValid = _layerTopValidBuffer;
            int[] layerPlacementCounts = _layerPlacementCountsBuffer;
            int[] clusterAccentCounts = _clusterAccentCountsBuffer;
            int[] structureAccentCounts = _structureAccentCountsBuffer;
            Dictionary<string, int>[] layerFamilyCounts = _layerFamilyCountsBuffer;
            Dictionary<string, int>[] layerBiomeCounts = _layerBiomeCountsBuffer;
            FastCandidateMap groundRescueCandidates = _groundRescueCandidates;
            FastCandidateMap clusterRescueCandidates = _clusterRescueCandidates;
            Dictionary<long, ScatterCandidate> structureRescueCandidates = _structureRescueCandidates;
            Dictionary<long, ScatterCandidate> spawnRescueCandidates = _spawnRescueCandidates;
            FastCandidateMap clusterFertileCandidates = _clusterFertileCandidates;
            FastCandidateMap clusterNestCandidates = _clusterNestCandidates;
            FastCandidateMap clusterResourceCandidates = _clusterResourceCandidates;
            FastCandidateMap clusterShelterCandidates = _clusterShelterCandidates;
            FastCandidateMap clusterHazardCandidates = _clusterHazardCandidates;
            FastCandidateMap clusterDebrisCandidates = _clusterDebrisCandidates;
            FastCandidateMap clusterRockCandidates = _clusterRockCandidates;
            FastCandidateMap structureNaturalCandidates = _structureNaturalCandidates;
            FastCandidateMap structureTechCandidates = _structureTechCandidates;
            FastCandidateMap structureCaveCandidates = _structureCaveCandidates;
            FastCandidateMap structureBioCandidates = _structureBioCandidates;
            FastCandidateMap passiveSpawnCandidates = _passiveSpawnCandidates;
            FastCandidateMap predatorSpawnCandidates = _predatorSpawnCandidates;
            Dictionary<HectonBiomeMatrixProfile, int> sampledMatrixProfileCounts = _sampledMatrixProfileCounts;
            Dictionary<string, int> sampledMatrixBiomeCounts = _sampledMatrixBiomeCounts;
            Dictionary<string, int> sampledBiomeCounts = _sampledBiomeCounts;
            Dictionary<string, int> sampledPatternCounts = _sampledPatternCounts;
            Dictionary<string, int> sampledZoneCounts = _sampledZoneCounts;
            int passiveSpawnCount = 0;
            int predatorSpawnCount = 0;
            int mapMagicSamples = 0;
            int raycastSamples = 0;
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
            bool collectDetailedDiagnostics = ShouldCollectScatterDetailedDiagnostics();
            WorldZoneAnchor debugZone = null;
            WorldZoneAnchor.ZoneKind debugResolvedZoneKind = default;
            WorldProceduralPattern debugPattern = WorldProceduralPattern.SedimentResources;
            float debugGroundBudgetScale = 1f;
            float debugClusterBudgetScale = 1f;
            float debugStructureBudgetScale = 1f;
            float debugSpawnBudgetScale = 1f;
            HectonBiomeMatrixProfile debugBiomeProfile = null;
            Hecton8.Environment.HectonBiomeFamilyProfile debugBiomeFamily = null;

            using (_scatterProcessingCellEvaluationProfilerMarker.Auto())
            {
                for (int cellIndex = 0; cellIndex < totalCells; cellIndex++)
                {
                    WorldProceduralFieldSampler.CellOutputData cellOutput = _memory.CellSamplingOutputs[cellIndex];
                    if (!fieldSampler.TryBuildFieldSample(cellOutput, out WorldProceduralFieldSampler.FieldSample fieldSample))
                        continue;

                int cellXIndex = cellOutput.CellX;
                int cellZIndex = cellOutput.CellZ;
                int domainCount = fieldSampler.GetFieldSampleDomainCount(cellOutput);
                evaluatedCells++;
                CountSeafloorSource(fieldSample.seafloorSource, ref mapMagicSamples, ref raycastSamples, ref fallbackSamples);
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
                    RegisterStringCount(sampledZoneCounts, fieldSample.zone != null ? fieldSample.zone.ZoneLabel : $"Synthetic:{fieldSample.resolvedZoneKind}");
                }
#endif
                WorldProceduralPatternProfile cellPatternProfile = ResolvePatternProfile(fieldSample.resolvedPattern, out _);
                WorldProceduralBiomeFamilyContextProfile cellBiomeContext = ResolveBiomeContextProfile(fieldSample.biomeFamily, out _);
                string cellBiomeContextLabel = cellBiomeContext != null ? cellBiomeContext.label : "None";
                bool usesPatternAccentQuotas = UsesPatternAccentQuotas(fieldSample.resolvedPattern);
                PopulatePatternQuotaCache(fieldSample.resolvedPattern, fieldSample.biomeProfile);
                int clusterRatioStart = _cachedPatternClusterRatioStart;
                int minimumSpawnPlacements = ResolveMinimumSpawnPlacements(fieldSample.resolvedPattern, fieldSample.biomeProfile);
                int passiveSpawnMax = Mathf.Max(
                    _cachedPatternPassiveSpawnMin,
                    _patternLayerTargetMaxBuffer[(int)WorldPrefabFamilyProfile.ScatterLayer.Spawn]);
                int predatorSpawnMax = _cachedPatternPredatorSpawnMax;
                ScatterBiomeScoreContext biomeScoreContext = BuildScatterBiomeScoreContext(fieldSample.biomeProfile);
                ScatterPatternScoreContext patternScoreContext = BuildScatterPatternScoreContext(fieldSample.resolvedPattern);
                ResolveCombinedBudgetScales(
                    cellPatternProfile,
                    cellBiomeContext,
                    out float localGroundBudgetScale,
                    out float localClusterBudgetScale,
                    out float localStructureBudgetScale,
                    out float localSpawnBudgetScale);
                int localGroundBudget = ResolveScaledBudget(groundBudget, localGroundBudgetScale, 4);
                int localClusterBudget = ResolveScaledBudget(clusterBudget, localClusterBudgetScale, 3);
                int localStructureBudget = ResolveScaledBudget(structureBudget, localStructureBudgetScale, 2);
                int localSpawnBudget = ResolveScaledBudget(spawnBudget, localSpawnBudgetScale, 2);
                int cellCandidateBufferLimit = ResolvePerCellCandidateBufferLimit(
                    localGroundBudget,
                    localClusterBudget,
                    localStructureBudget,
                    localSpawnBudget);
                debugGroundBudgetScale = localGroundBudgetScale;
                debugClusterBudgetScale = localClusterBudgetScale;
                debugStructureBudgetScale = localStructureBudgetScale;
                debugSpawnBudgetScale = localSpawnBudgetScale;

                int cellGroundCount = 0;
                int cellClusterCount = 0;
                int cellStructureCountPrimary = 0;
                int cellStructureCountSecondary = 0;
                int cellSpawnCountPrimary = 0;
                int cellSpawnCountSecondary = 0;
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
                        if (!MatchesScatter(runtimeRule, activeFieldSample.biomeFamily, activeFieldSample.zone, activeFieldSample.resolvedZoneKind, activeFieldSample.depthMeters, activeFieldSample.slopeDegrees))
                            continue;
                        if (collectDetailedDiagnostics)
                            matchedScatterRules++;

                        float heat = fieldSampler.EvaluateHeatmap(
                            runtimeRule.HeatmapChannelIndex,
                            cellOutput,
                            runtimeRule.PlacementMode,
                            runtimeRule.DensityScaleFactor);
                        heat = Mathf.Clamp01(
                            heat
                            * GetCombinedHeatScale(
                                activeFieldSample.resolvedPattern,
                                activeFieldSample.depthMeters,
                                runtimeRule,
                                biomeScoreContext,
                                patternScoreContext));
                        bool needsPreviewRescue = NeedsPreviewRescue(activeFieldSample, family);
                        float effectiveMinHeat = ResolveEffectiveMinHeat(rule, family, activeFieldSample, needsPreviewRescue);
                        float effectiveDensityScale = ResolveEffectiveDensityScale(rule, family, activeFieldSample, needsPreviewRescue);
                        if (heat < effectiveMinHeat)
                            continue;
                        if (collectDetailedDiagnostics)
                            heatPassedRules++;

                        float normalizedHeat = Mathf.InverseLerp(effectiveMinHeat, 1f, heat);
                        float spawnProbability = Mathf.Clamp01(normalizedHeat * (0.45f + Mathf.Clamp(effectiveDensityScale, 0.1f, 4f) * 0.18f));
                        bool needsSpawnRescue = minimumSpawnPlacements > 0 &&
                                                family != null &&
                                                family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
                        bool needsRescueTracking = needsPreviewRescue || needsSpawnRescue;
                        int heightLayerIndex = ResolveHeightLayerIndex(activeFieldSample, runtimeRule);
                        float gate = StablePlacementRandom01(cellXIndex, cellZIndex, runtimeRule.RuleIdHash, heightLayerIndex);
                        if (gate > spawnProbability && !needsRescueTracking)
                            continue;
                        WorldPrefabFamilyProfile.ScatterLayer layer = runtimeRule.ScatterLayer;
                        int layerIndex = (int)layer;
                        if (!HasPatternLayerGlobalBudget(layer, layerPlacementCounts[layerIndex], _patternLayerTargetMaxBuffer))
                            continue;

                        int localStructureCount = heightLayerIndex == 0 ? cellStructureCountPrimary : cellStructureCountSecondary;
                        int localSpawnCount = heightLayerIndex == 0 ? cellSpawnCountPrimary : cellSpawnCountSecondary;
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
                                cellGroundCount,
                                cellClusterCount,
                                localStructureCount,
                                localSpawnCount))
                        {
                            continue;
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
                            continue;
                        }

                        if (collectDetailedDiagnostics)
                            gatePassedRules++;

                        ScatterCandidatePreview candidatePreview = BuildCandidatePreview(
                            cellXIndex,
                            cellZIndex,
                            activeFieldSample,
                            runtimeRule,
                            size);
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
                                rejectedResidencyDistance = residencyDistanceSqr > 0f ? Mathf.Sqrt(residencyDistanceSqr) : 0f;
                                rejectedResidencyRadius = residencyRadius;
                            }
                            continue;
                        }
                        if (collectDetailedDiagnostics)
                            residencyPassedCandidates++;

                        int layerPreferredFamilyIndex = GetPreferredFamilyIndexForLayer(activeFieldSample.biomeProfile, family, layer);
                        float baseScore = spawnProbability
                            + heat
                            + runtimeRule.ScoreBaseBonus
                            + runtimeRule.AcceptedFamilyAffinityBonus;
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
                                + ResolveBiomeMatrixScoreUpperBound(runtimeRule, activeFieldSample.biomeProfile != null, layerPreferredFamilyIndex, patternScoreContext)
                                + runtimeRule.GeologyScoreScale;
                            if (scoreUpperBound <= worstCandidateScore)
                                continue;
                        }

                        float biomeMatrixScore = GetBiomeMatrixBonus(activeFieldSample.resolvedPattern, activeFieldSample.biomeProfile, runtimeRule, biomeScoreContext, layerPreferredFamilyIndex, patternScoreContext);
                        float scoreWithoutGeology = scoreBeforeBiomeMatrixAndGeology + biomeMatrixScore;

                        if (canPruneByScore)
                        {
                            if (scoreWithoutGeology + runtimeRule.GeologyScoreScale <= worstCandidateScore)
                                continue;
                        }

                        float score = scoreWithoutGeology
                            + GetCachedGenerativeGeologyContextBonus(
                                activeFieldSample,
                                runtimeRule,
                                ref geologyBonusCache);

                        if (canPruneByScore && score <= worstCandidateScore)
                            continue;

                        bool rejectedByGate = gate > spawnProbability;
                        ScatterCandidate candidate = BuildCandidate(
                            cellXIndex,
                            cellZIndex,
                            activeFieldSample,
                            runtimeRule,
                            candidatePreview,
                            cellBiomeContextLabel,
                            heat,
                            score);

                        if (needsRescueTracking)
                        {
                            TrackRescueCandidate(
                                candidate,
                                needsPreviewRescue,
                                needsSpawnRescue,
                                structureStride,
                                spawnStride,
                                ref groundRescueCandidates,
                                ref clusterRescueCandidates,
                                structureRescueCandidates,
                                spawnRescueCandidates,
                                ref clusterFertileCandidates,
                                ref clusterNestCandidates,
                                ref clusterResourceCandidates,
                                ref clusterShelterCandidates,
                                ref clusterHazardCandidates,
                                ref clusterDebrisCandidates,
                                ref clusterRockCandidates,
                                ref structureNaturalCandidates,
                                ref structureTechCandidates,
                                ref structureCaveCandidates,
                                ref structureBioCandidates,
                                ref passiveSpawnCandidates,
                                ref predatorSpawnCandidates);
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

                for (int i = 0; i < _candidateBuffer.Count; i++)
                {
                    ScatterCandidate candidate = _candidateBuffer[i];
                    WorldPrefabFamilyProfile.ScatterLayer layer = candidate.Family.scatterLayer;
                    int layerIndex = (int)layer;
                    if (!HasPatternLayerGlobalBudget(layer, layerPlacementCounts[layerIndex], _patternLayerTargetMaxBuffer))
                        continue;

                    int candidateStructureCount = candidate.Placement.HeightLayerIndex == 0 ? cellStructureCountPrimary : cellStructureCountSecondary;
                    int candidateSpawnCount = candidate.Placement.HeightLayerIndex == 0 ? cellSpawnCountPrimary : cellSpawnCountSecondary;
                    if (!HasLayerBudget(
                            candidate,
                            localGroundBudget,
                            localClusterBudget,
                            structureStride,
                            localStructureBudget,
                            spawnStride,
                            localSpawnBudget,
                            cellGroundCount,
                            cellClusterCount,
                            candidateStructureCount,
                            candidateSpawnCount))
                        continue;

                    if (!CanAcceptPatternAccentBudget(
                            usesPatternAccentQuotas,
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
                            clusterRatioStart,
                            _clusterAccentRoleMaxRatioBuffer,
                            _structureAccentRoleMaxBuffer,
                            passiveSpawnMax,
                            predatorSpawnMax))
                        continue;

                    if (!CanAcceptCandidate(candidate))
                        continue;

                    ScatterPlacement placement = candidate.Placement;
                    if (!TryRegisterDesiredPlacement(placement, now))
                        continue;
                    layerPlacementCounts[layerIndex]++;
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
                            cellGroundCount++;
                            break;
                        case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                            cellClusterCount++;
                            break;
                        case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                            if (candidate.Placement.HeightLayerIndex == 0)
                                cellStructureCountPrimary++;
                            else
                                cellStructureCountSecondary++;
                            RegisterWindowPlacement(candidate.Placement.CellX, candidate.Placement.CellZ, structureStride, candidate.Placement.HeightLayerIndex, _structureWindowCounts);
                            break;
                        case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                            if (candidate.Placement.HeightLayerIndex == 0)
                                cellSpawnCountPrimary++;
                            else
                                cellSpawnCountSecondary++;
                            RegisterWindowPlacement(candidate.Placement.CellX, candidate.Placement.CellZ, spawnStride, candidate.Placement.HeightLayerIndex, _spawnWindowCounts);
                            break;
                    }
                }
            }
            }

            ReleaseCandidateListPlacements(_candidateBuffer);
            fieldSampler.EndScatterSamplingFrame();

            long samplingEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            HectonBiomeMatrixProfile dominantBiomeProfile = ResolveDominantBiomeMatrixProfile(sampledMatrixProfileCounts, debugBiomeProfile);

            int injectedSpawnRescuePlacements;
            int trackedSpawnRescueCandidates;
            using (_scatterProcessingRescueProfilerMarker.Auto())
            {
                InjectRescuePlacementsIfNeeded(
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
                    ref passiveSpawnCount,
                    ref predatorSpawnCount,
                    layerTopCandidates,
                    layerTopValid,
                    layerFamilyCounts,
                    layerBiomeCounts,
                    groundRescueCandidates,
                    clusterRescueCandidates,
                    structureRescueCandidates,
                    spawnRescueCandidates,
                    clusterFertileCandidates,
                    clusterNestCandidates,
                    clusterResourceCandidates,
                    clusterShelterCandidates,
                    clusterHazardCandidates,
                    clusterDebrisCandidates,
                    clusterRockCandidates,
                    structureNaturalCandidates,
                    structureTechCandidates,
                    structureCaveCandidates,
                    structureBioCandidates,
                    passiveSpawnCandidates,
                    predatorSpawnCandidates,
                    out injectedSpawnRescuePlacements);
                trackedSpawnRescueCandidates = spawnRescueCandidates.Count;
                ReleaseRescueCandidateBuffers();
            }
            long rescueEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            using (_scatterProcessingRestoreProfilerMarker.Auto())
            {
                RestoreRecentDesiredPlacements(center, now);
            }
            long restoreEndTimestamp = enableScatterRebuildProfiling ? Stopwatch.GetTimestamp() : 0L;

            ScatterReconcileMetrics reconcileMetrics = ReconcileInstances(enableScatterRebuildProfiling);

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
            _debugRaycastSamples = raycastSamples;
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
            _lastScatterUsedFallbackOnly = evaluatedCells > 0 && fallbackSamples >= evaluatedCells;
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
            if (collectDetailedDiagnostics)
            {
                _debugZone = debugZone != null ? debugZone.ZoneLabel : $"Synthetic:{debugResolvedZoneKind}";
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
                _debugClusterDominantAccentRole = ResolveDominantClusterAccentRole(clusterAccentCounts, out _debugClusterDominantAccentCount);
                _debugStructureDominantAccentRole = ResolveDominantStructureAccentRole(structureAccentCounts, out _debugStructureDominantAccentCount);
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
            }
            else
            {
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
#endif
            TryScheduleScatterBackendShadowPass(
                center,
                totalCells,
                groundBudget,
                clusterBudget,
                structureStride,
                spawnStride);

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
    }
}
