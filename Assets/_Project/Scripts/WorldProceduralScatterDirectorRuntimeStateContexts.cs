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
            public Vector3 Position;
            public int CenterCellX;
            public int CenterCellZ;
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
                Vector3 center,
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
                Center = center;
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
            public Vector3 Center { get; }
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
            public Vector3 Center;
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
