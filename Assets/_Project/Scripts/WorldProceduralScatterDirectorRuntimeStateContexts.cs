using UnityEngine;
using System.Collections.Generic;
using Hecton8.Environment;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private struct ScatterRefreshSampleState
        {
            public bool HasSample;
            public bool UsedFallbackOnly;
            public Vector3 AbsolutePosition;
            public int CenterCellX;
            public int CenterCellZ;
            public int RadiusCells;
            public float Time;
        }

        private struct ScatterResolvedRuntimeSettings
        {
            public float CellSize;
            public int RadiusCells;
            public float ChunkSize;
            public float MacroZoneSize;
        }

        private struct ScatterStartupRuntimeState
        {
            public bool StabilizationPending;
            public float StartTime;
        }

        private struct ScatterReconcileRuntimeState
        {
            public bool HasPendingStartupPlacements;
            public bool HasPendingRuntimePlacements;
            public int PlanVersion;
            public bool HasObserverSample;
            public Vector3 LastObserverPosition;
        }

        private struct ScatterBootstrapRuntimeState
        {
            public bool PresenceResolved;
            public bool Present;
            public bool Failed;
            public bool AllowPrimePass;
            public bool SamplingPipelinePrewarmed;
        }

        private struct ScatterLifecycleRuntimeState
        {
            public bool RegisteredToTickManager;
            public bool SubscribedToBootstrap;
            public bool LoggedRuntimeStartState;
            public bool LoggedFirstSlowTick;
            public float NextTickDrivenScatterAttemptTime;
        }

        private readonly struct ScatterSamplingBeginContext
        {
            public ScatterSamplingBeginContext(
                IReadOnlyList<WorldProceduralPlacementRule> rules,
                Vector3 runtimeCenter,
                Vector3 absoluteCenter,
                int centerCellX,
                int centerCellZ,
                float cellSize,
                int radiusCells,
                int cellDiameter,
                int totalCells,
                float now,
                int groundBudget,
                int clusterBudget,
                int structureStride,
                int structureBudget,
                int spawnStride,
                int spawnBudget)
            {
                Rules = rules;
                RuntimeCenter = runtimeCenter;
                AbsoluteCenter = absoluteCenter;
                CenterCellX = centerCellX;
                CenterCellZ = centerCellZ;
                CellSize = cellSize;
                RadiusCells = radiusCells;
                CellDiameter = cellDiameter;
                TotalCells = totalCells;
                Now = now;
                GroundBudget = groundBudget;
                ClusterBudget = clusterBudget;
                StructureStride = structureStride;
                StructureBudget = structureBudget;
                SpawnStride = spawnStride;
                SpawnBudget = spawnBudget;
            }

            public IReadOnlyList<WorldProceduralPlacementRule> Rules { get; }
            public Vector3 RuntimeCenter { get; }
            public Vector3 AbsoluteCenter { get; }
            public int CenterCellX { get; }
            public int CenterCellZ { get; }
            public float CellSize { get; }
            public int RadiusCells { get; }
            public int CellDiameter { get; }
            public int TotalCells { get; }
            public float Now { get; }
            public int GroundBudget { get; }
            public int ClusterBudget { get; }
            public int StructureStride { get; }
            public int StructureBudget { get; }
            public int SpawnStride { get; }
            public int SpawnBudget { get; }
        }

        private struct ScatterSamplingCompletionContext
        {
            public Vector3 AbsoluteCenter;
            public float CellSize;
            public float Now;
            public int TotalCells;
            public int ClusterBudget;
            public int StructureStride;
            public int StructureBudget;
            public int SpawnStride;
            public int SpawnBudget;
            public int GroundBudget;
            public long RebuildStartTimestamp;
            public long SamplingInputsEndTimestamp;
            public int EvaluatedCells;
            public ScatterCandidate TopCandidate;
            public bool HasTopCandidate;
            public ScatterCandidate[] LayerTopCandidates;
            public bool[] LayerTopValid;
            public int[] LayerPlacementCounts;
            public int[] ClusterAccentCounts;
            public int[] StructureAccentCounts;
            public Dictionary<string, int>[] LayerFamilyCounts;
            public Dictionary<string, int>[] LayerBiomeCounts;
            public ScatterPlacementRegistrationContext PlacementRegistrationContext;
            public ScatterRescueTrackingContext RescueTrackingContext;
            public Dictionary<HectonBiomeMatrixProfile, int> SampledMatrixProfileCounts;
            public Dictionary<string, int> SampledMatrixBiomeCounts;
            public Dictionary<string, int> SampledBiomeCounts;
            public Dictionary<string, int> SampledPatternCounts;
            public Dictionary<string, int> SampledZoneCounts;
            public int PassiveSpawnCount;
            public int PredatorSpawnCount;
            public int MapMagicSamples;
            public int RaycastSamples;
            public int FallbackSamples;
            public int MatchedScatterRules;
            public int HeatPassedRules;
            public int GatePassedRules;
            public int ResidencyPassedCandidates;
            public int PostBuildGateRejectedCandidates;
            public int QueuedCandidates;
            public string RejectedResidencyFamily;
            public float RejectedResidencyDistance;
            public float RejectedResidencyRadius;
            public int MaxCandidatesBeforePrunePerCell;
            public int MaxCandidatesAfterPrunePerCell;
            public bool CollectDetailedDiagnostics;
            public WorldZoneAnchor DebugZone;
            public WorldZoneAnchor.ZoneKind DebugResolvedZoneKind;
            public WorldProceduralPattern DebugPattern;
            public float DebugGroundBudgetScale;
            public float DebugClusterBudgetScale;
            public float DebugStructureBudgetScale;
            public float DebugSpawnBudgetScale;
            public HectonBiomeMatrixProfile DebugBiomeProfile;
            public HectonBiomeFamilyProfile DebugBiomeFamily;

            public ScatterSamplingCompletionContext(bool initializeDefaults)
            {
                AbsoluteCenter = Vector3.zero;
                CellSize = 0f;
                Now = 0f;
                TotalCells = 0;
                ClusterBudget = 0;
                StructureStride = 0;
                StructureBudget = 0;
                SpawnStride = 0;
                SpawnBudget = 0;
                GroundBudget = 0;
                RebuildStartTimestamp = 0L;
                SamplingInputsEndTimestamp = 0L;
                EvaluatedCells = 0;
                TopCandidate = default;
                HasTopCandidate = false;
                LayerTopCandidates = null;
                LayerTopValid = null;
                LayerPlacementCounts = null;
                ClusterAccentCounts = null;
                StructureAccentCounts = null;
                LayerFamilyCounts = null;
                LayerBiomeCounts = null;
                PlacementRegistrationContext = default;
                RescueTrackingContext = default;
                SampledMatrixProfileCounts = null;
                SampledMatrixBiomeCounts = null;
                SampledBiomeCounts = null;
                SampledPatternCounts = null;
                SampledZoneCounts = null;
                PassiveSpawnCount = 0;
                PredatorSpawnCount = 0;
                MapMagicSamples = 0;
                RaycastSamples = 0;
                FallbackSamples = 0;
                MatchedScatterRules = 0;
                HeatPassedRules = 0;
                GatePassedRules = 0;
                ResidencyPassedCandidates = 0;
                PostBuildGateRejectedCandidates = 0;
                QueuedCandidates = 0;
                RejectedResidencyFamily = null;
                RejectedResidencyDistance = 0f;
                RejectedResidencyRadius = 0f;
                MaxCandidatesBeforePrunePerCell = 0;
                MaxCandidatesAfterPrunePerCell = 0;
                CollectDetailedDiagnostics = false;
                DebugZone = null;
                DebugResolvedZoneKind = default;
                DebugPattern = default;
                DebugGroundBudgetScale = 0f;
                DebugClusterBudgetScale = 0f;
                DebugStructureBudgetScale = 0f;
                DebugSpawnBudgetScale = 0f;
                DebugBiomeProfile = null;
                DebugBiomeFamily = null;
            }
        }

        private struct ScatterCellPlacementAcceptanceContext
        {
            public int LocalGroundBudget;
            public int LocalClusterBudget;
            public int StructureStride;
            public int LocalStructureBudget;
            public int SpawnStride;
            public int LocalSpawnBudget;
            public int ClusterRatioStart;
            public int PassiveSpawnMax;
            public int PredatorSpawnMax;
            public bool UsesPatternAccentQuotas;
            public bool CollectDetailedDiagnostics;
            public ScatterPlacementRegistrationContext PlacementRegistrationContext;
        }

        private readonly struct ScatterBiomeTransitionContext
        {
            public ScatterBiomeTransitionContext(
                bool hasSecondary,
                HectonBiomeMatrixProfile secondaryProfile,
                HectonBiomeFamilyProfile secondaryFamily,
                WorldProceduralBiomeFamilyContextProfile secondaryBiomeContext,
                ScatterBiomeScoreContext secondaryScoreContext,
                float secondaryWeight)
            {
                HasSecondary = hasSecondary && secondaryProfile != null && secondaryWeight > 0f;
                SecondaryProfile = HasSecondary ? secondaryProfile : null;
                SecondaryFamily = HasSecondary ? secondaryFamily : null;
                SecondaryBiomeContext = HasSecondary ? secondaryBiomeContext : null;
                SecondaryScoreContext = HasSecondary ? secondaryScoreContext : default;
                SecondaryWeight = HasSecondary ? Mathf.Clamp01(secondaryWeight) : 0f;
                PrimaryWeight = 1f - SecondaryWeight;
            }

            public bool HasSecondary { get; }
            public HectonBiomeMatrixProfile SecondaryProfile { get; }
            public HectonBiomeFamilyProfile SecondaryFamily { get; }
            public WorldProceduralBiomeFamilyContextProfile SecondaryBiomeContext { get; }
            public ScatterBiomeScoreContext SecondaryScoreContext { get; }
            public float PrimaryWeight { get; }
            public float SecondaryWeight { get; }
        }

        private struct ScatterCellPlacementCounters
        {
            public int GroundCount;
            public int ClusterCount;
            public int StructureCountPrimary;
            public int StructureCountSecondary;
            public int SpawnCountPrimary;
            public int SpawnCountSecondary;
        }

        private struct ScatterClassicParityAccumulator
        {
            private const ulong FnvOffset = 14695981039346656037UL;
            private const ulong FnvPrime = 1099511628211UL;

            public int CandidateCount;
            public int GroundCount;
            public int ClusterCount;
            public int StructureCount;
            public int SpawnCount;
            public ulong CandidateChecksum;

            public void Register(ScatterCandidate candidate, WorldPrefabFamilyProfile.ScatterLayer layer)
            {
                CandidateCount++;
                switch (layer)
                {
                    case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                        GroundCount++;
                        break;
                    case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                        ClusterCount++;
                        break;
                    case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                        StructureCount++;
                        break;
                    case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                        SpawnCount++;
                        break;
                }

                ulong hash = CandidateChecksum == 0UL ? FnvOffset : CandidateChecksum;
                hash = Combine(hash, (ulong)(uint)layer);
                long cellKey = ((long)(uint)(candidate.Placement.CellX & 0xFFFF) << 32) | (uint)(candidate.Placement.CellZ & 0xFFFF);
                hash = Combine(hash, (ulong)cellKey);
                CandidateChecksum = hash;
            }

            public ScatterBackendParityReference ToReference()
            {
                return new ScatterBackendParityReference(
                    CandidateCount,
                    GroundCount,
                    ClusterCount,
                    StructureCount,
                    SpawnCount,
                    CandidateChecksum == 0UL ? FnvOffset : CandidateChecksum);
            }

            private static ulong Combine(ulong hash, ulong value)
            {
                return (hash ^ value) * FnvPrime;
            }
        }
    }
}
