using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Environment;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        private const int FloraChunkInstanceHardCap = 4096;
        private const int PoissonDiskMaxRejectionAttempts = 30;

        internal readonly struct ScatterPlacementSpatialMetadata
        {
            public ScatterPlacementSpatialMetadata(
                float3 position,
                float effectiveSpacing,
                int familyHash,
                int scatterLayer,
                int proceduralDomain,
                byte floraBudgetClass)
            {
                Position = position;
                EffectiveSpacing = effectiveSpacing;
                FamilyHash = familyHash;
                ScatterLayer = scatterLayer;
                ProceduralDomain = proceduralDomain;
                FloraBudgetClass = floraBudgetClass;
            }

            public readonly float3 Position;
            public readonly float EffectiveSpacing;
            public readonly int FamilyHash;
            public readonly int ScatterLayer;
            public readonly int ProceduralDomain;
            public readonly byte FloraBudgetClass;
        }

        internal struct ScatterCellCandidateAcceptanceInput
        {
            public float3 Position;
            public float EffectiveSpacing;
            public int CellX;
            public int CellZ;
            public int HeightLayerIndex;
            public int FamilyHash;
            public int ScatterLayer;
            public int ProceduralDomain;
            public int ClusterAccentRole;
            public int StructureAccentRole;
            public byte IsPassiveSpawnFamily;
            public byte IsPredatorSpawnFamily;
            public byte ExternalBlock;
            public byte FloraBudgetClass;
            public byte RequiresClusterPatch;
            public float ClusterNoiseScale;
            public float ClusterNoiseThreshold;
        }

        private readonly struct ScatterRescueCandidateFilter
        {
            public readonly WorldPrefabFamilyProfile ExactFamily;
            public readonly WorldPrefabFamilyProfile.ProceduralDomain Domain;
            public readonly WorldPrefabFamilyProfile.PlacementMode PlacementMode;
            public readonly WorldPrefabFamilyProfile.ClusterAccentRole ClusterAccentRole;
            public readonly WorldPrefabFamilyProfile.StructureAccentRole StructureAccentRole;
            public readonly byte UseExactFamily;
            public readonly byte UseDomain;
            public readonly byte UsePlacementMode;
            public readonly byte UseClusterAccentRole;
            public readonly byte UseStructureAccentRole;
            public readonly byte PassiveOnly;
            public readonly byte PredatorOnly;

            private ScatterRescueCandidateFilter(
                WorldPrefabFamilyProfile exactFamily,
                WorldPrefabFamilyProfile.ProceduralDomain domain,
                WorldPrefabFamilyProfile.PlacementMode placementMode,
                WorldPrefabFamilyProfile.ClusterAccentRole clusterAccentRole,
                WorldPrefabFamilyProfile.StructureAccentRole structureAccentRole,
                bool useExactFamily,
                bool useDomain,
                bool usePlacementMode,
                bool useClusterAccentRole,
                bool useStructureAccentRole,
                bool passiveOnly,
                bool predatorOnly)
            {
                ExactFamily = exactFamily;
                Domain = domain;
                PlacementMode = placementMode;
                ClusterAccentRole = clusterAccentRole;
                StructureAccentRole = structureAccentRole;
                UseExactFamily = useExactFamily ? (byte)1 : (byte)0;
                UseDomain = useDomain ? (byte)1 : (byte)0;
                UsePlacementMode = usePlacementMode ? (byte)1 : (byte)0;
                UseClusterAccentRole = useClusterAccentRole ? (byte)1 : (byte)0;
                UseStructureAccentRole = useStructureAccentRole ? (byte)1 : (byte)0;
                PassiveOnly = passiveOnly ? (byte)1 : (byte)0;
                PredatorOnly = predatorOnly ? (byte)1 : (byte)0;
            }

            public static ScatterRescueCandidateFilter None => default;

            public static ScatterRescueCandidateFilter ExactFamilyFilter(WorldPrefabFamilyProfile family)
            {
                return new ScatterRescueCandidateFilter(
                    family,
                    default,
                    default,
                    default,
                    default,
                    useExactFamily: true,
                    useDomain: false,
                    usePlacementMode: false,
                    useClusterAccentRole: false,
                    useStructureAccentRole: false,
                    passiveOnly: false,
                    predatorOnly: false);
            }

            public static ScatterRescueCandidateFilter DomainFilter(WorldPrefabFamilyProfile.ProceduralDomain domain)
            {
                return new ScatterRescueCandidateFilter(
                    null,
                    domain,
                    default,
                    default,
                    default,
                    useExactFamily: false,
                    useDomain: true,
                    usePlacementMode: false,
                    useClusterAccentRole: false,
                    useStructureAccentRole: false,
                    passiveOnly: false,
                    predatorOnly: false);
            }

            public static ScatterRescueCandidateFilter DomainPlacementModeFilter(
                WorldPrefabFamilyProfile.ProceduralDomain domain,
                WorldPrefabFamilyProfile.PlacementMode placementMode)
            {
                return new ScatterRescueCandidateFilter(
                    null,
                    domain,
                    placementMode,
                    default,
                    default,
                    useExactFamily: false,
                    useDomain: true,
                    usePlacementMode: true,
                    useClusterAccentRole: false,
                    useStructureAccentRole: false,
                    passiveOnly: false,
                    predatorOnly: false);
            }

            public static ScatterRescueCandidateFilter ClusterAccentFilter(WorldPrefabFamilyProfile.ClusterAccentRole accentRole)
            {
                return new ScatterRescueCandidateFilter(
                    null,
                    default,
                    default,
                    accentRole,
                    default,
                    useExactFamily: false,
                    useDomain: false,
                    usePlacementMode: false,
                    useClusterAccentRole: true,
                    useStructureAccentRole: false,
                    passiveOnly: false,
                    predatorOnly: false);
            }

            public static ScatterRescueCandidateFilter StructureAccentFilter(WorldPrefabFamilyProfile.StructureAccentRole accentRole)
            {
                return new ScatterRescueCandidateFilter(
                    null,
                    default,
                    default,
                    default,
                    accentRole,
                    useExactFamily: false,
                    useDomain: false,
                    usePlacementMode: false,
                    useClusterAccentRole: false,
                    useStructureAccentRole: true,
                    passiveOnly: false,
                    predatorOnly: false);
            }

            public static ScatterRescueCandidateFilter PassiveSpawnFilter()
            {
                return new ScatterRescueCandidateFilter(
                    null,
                    default,
                    default,
                    default,
                    default,
                    useExactFamily: false,
                    useDomain: false,
                    usePlacementMode: false,
                    useClusterAccentRole: false,
                    useStructureAccentRole: false,
                    passiveOnly: true,
                    predatorOnly: false);
            }

            public static ScatterRescueCandidateFilter PredatorSpawnFilter()
            {
                return new ScatterRescueCandidateFilter(
                    null,
                    default,
                    default,
                    default,
                    default,
                    useExactFamily: false,
                    useDomain: false,
                    usePlacementMode: false,
                    useClusterAccentRole: false,
                    useStructureAccentRole: false,
                    passiveOnly: false,
                    predatorOnly: true);
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CanAcceptCandidateJob : IJob
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, float3> PositionBuckets;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> MetadataBuckets;
            [ReadOnly] public NativeArray<ScatterPlacementSpatialMetadata> SpatialMetadata;
            public NativeArray<int> Result;
            public float3 CandidatePosition;
            public float CandidateSpacing;
            public float MaxRelevantDistanceSq;
            public int CandidateCellX;
            public int CandidateCellZ;
            public int SearchRadiusCells;
            public int CandidateFamilyHash;
            public int CandidateLayer;
            public int CandidateDomain;
            public float CandidateClusterNoiseScale;
            public float CandidateClusterNoiseThreshold;
            public float ChunkSize;
            public int FloraChunkHardCap;
            public byte CandidateFloraBudgetClass;
            public byte CandidateRequiresClusterPatch;

            public void Execute()
            {
                if (CandidateRequiresClusterPatch != 0 &&
                    !PassesFloraClusterPatch(
                        CandidatePosition,
                        CandidateCellX,
                        CandidateCellZ,
                        CandidateFamilyHash,
                        CandidateClusterNoiseScale,
                        CandidateClusterNoiseThreshold,
                        ChunkSize))
                {
                    Result[0] = 0;
                    return;
                }

                if (CandidateFloraBudgetClass != (byte)FloraBudgetClass.None &&
                    ExceedsFloraChunkHardCap(
                        CandidateCellX,
                        CandidateCellZ,
                        FloraChunkHardCap,
                        MetadataBuckets,
                        SpatialMetadata))
                {
                    Result[0] = 0;
                    return;
                }

                Result[0] = 1;

                for (int ox = -SearchRadiusCells; ox <= SearchRadiusCells; ox++)
                {
                    for (int oz = -SearchRadiusCells; oz <= SearchRadiusCells; oz++)
                    {
                        int cellKey = ComposeScatterGridNativeKey(CandidateCellX + ox, CandidateCellZ + oz);
                        if (!PositionBuckets.TryGetFirstValue(cellKey, out float3 existingPosition, out NativeParallelMultiHashMapIterator<int> positionIterator))
                            continue;

                        bool bucketWithinRange = false;
                        do
                        {
                            float3 bucketDelta = CandidatePosition - existingPosition;
                            if (math.lengthsq(bucketDelta) < MaxRelevantDistanceSq)
                            {
                                bucketWithinRange = true;
                                break;
                            }
                        }
                        while (PositionBuckets.TryGetNextValue(out existingPosition, ref positionIterator));

                        if (!bucketWithinRange)
                            continue;

                        if (!MetadataBuckets.TryGetFirstValue(cellKey, out int metadataIndex, out NativeParallelMultiHashMapIterator<int> metadataIterator))
                            continue;

                        do
                        {
                            ScatterPlacementSpatialMetadata existing = SpatialMetadata[metadataIndex];
                            float minDistance = ResolveRequiredDistanceNative(
                                CandidateSpacing,
                                CandidateFamilyHash,
                                CandidateLayer,
                                CandidateDomain,
                                in existing);
                            if (minDistance <= 0f)
                                continue;

                            float3 spacingDelta = CandidatePosition - existing.Position;
                            if (math.lengthsq(spacingDelta) < minDistance * minDistance)
                            {
                                Result[0] = 0;
                                return;
                            }
                        }
                        while (MetadataBuckets.TryGetNextValue(out metadataIndex, ref metadataIterator));
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateScatterCellCandidateBatchJob : IJob
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, float3> ExistingPositionBuckets;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ExistingMetadataBuckets;
            [ReadOnly] public NativeArray<ScatterPlacementSpatialMetadata> ExistingSpatialMetadata;
            [ReadOnly] public NativeArray<ScatterCellCandidateAcceptanceInput> Candidates;
            [ReadOnly] public NativeArray<float> ClusterAccentRoleMaxRatios;
            [ReadOnly] public NativeArray<int> StructureAccentRoleMaxCounts;
            public NativeArray<byte> Results;
            public NativeList<ScatterPlacementSpatialMetadata> PendingSpatialMetadata;
            public NativeParallelMultiHashMap<int, float3> PendingPositionBuckets;
            public NativeParallelMultiHashMap<int, int> PendingMetadataBuckets;
            public NativeArray<int> ClusterAccentCounts;
            public NativeArray<int> StructureAccentCounts;
            public float MaxRegisteredPlacementSpacing;
            public float CellSize;
            public float ChunkSize;
            public int GroundTargetMax;
            public int ClusterTargetMax;
            public int StructureTargetMax;
            public int SpawnTargetMax;
            public int GlobalGroundCount;
            public int GlobalClusterCount;
            public int GlobalStructureCount;
            public int GlobalSpawnCount;
            public int LocalGroundBudget;
            public int LocalClusterBudget;
            public int LocalStructureBudget;
            public int LocalSpawnBudget;
            public int ClusterCount;
            public int GroundCount;
            public int StructureCountPrimary;
            public int StructureCountSecondary;
            public int SpawnCountPrimary;
            public int SpawnCountSecondary;
            public int StructureWindowCountPrimary;
            public int StructureWindowCountSecondary;
            public int SpawnWindowCountPrimary;
            public int SpawnWindowCountSecondary;
            public int ClusterRatioStart;
            public int PassiveSpawnCount;
            public int PredatorSpawnCount;
            public int PassiveSpawnMax;
            public int PredatorSpawnMax;
            public byte UsesPatternAccentQuotas;
            public int FloraChunkHardCap;
            public int MaxPoissonRejectionAttempts;

            public void Execute()
            {
                for (int index = 0; index < Results.Length; index++)
                    Results[index] = 0;

                int globalGroundCount = GlobalGroundCount;
                int globalClusterCount = GlobalClusterCount;
                int globalStructureCount = GlobalStructureCount;
                int globalSpawnCount = GlobalSpawnCount;
                int groundCount = GroundCount;
                int clusterCount = ClusterCount;
                int structureCountPrimary = StructureCountPrimary;
                int structureCountSecondary = StructureCountSecondary;
                int spawnCountPrimary = SpawnCountPrimary;
                int spawnCountSecondary = SpawnCountSecondary;
                int structureWindowCountPrimary = StructureWindowCountPrimary;
                int structureWindowCountSecondary = StructureWindowCountSecondary;
                int spawnWindowCountPrimary = SpawnWindowCountPrimary;
                int spawnWindowCountSecondary = SpawnWindowCountSecondary;
                int passiveSpawnCount = PassiveSpawnCount;
                int predatorSpawnCount = PredatorSpawnCount;
                int poissonRejectionAttempts = 0;
                int maxPoissonRejectionAttempts = math.max(1, MaxPoissonRejectionAttempts);

                for (int candidateIndex = 0; candidateIndex < Candidates.Length; candidateIndex++)
                {
                    ScatterCellCandidateAcceptanceInput candidate = Candidates[candidateIndex];
                    if (candidate.ExternalBlock != 0)
                        continue;

                    if (!PassesFloraClusterPatch(in candidate, ChunkSize))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (candidate.FloraBudgetClass != (byte)FloraBudgetClass.None &&
                        ExceedsFloraChunkHardCap(in candidate, FloraChunkHardCap))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (!HasPatternLayerBudget(
                            candidate.ScatterLayer,
                            globalGroundCount,
                            globalClusterCount,
                            globalStructureCount,
                            globalSpawnCount,
                            GroundTargetMax,
                            ClusterTargetMax,
                            StructureTargetMax,
                            SpawnTargetMax))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    int localStructureCount = candidate.HeightLayerIndex == 0
                        ? structureCountPrimary
                        : structureCountSecondary;
                    int localSpawnCount = candidate.HeightLayerIndex == 0
                        ? spawnCountPrimary
                        : spawnCountSecondary;
                    int currentWindowCount = ResolveCurrentWindowCount(
                        candidate.ScatterLayer,
                        candidate.HeightLayerIndex,
                        structureWindowCountPrimary,
                        structureWindowCountSecondary,
                        spawnWindowCountPrimary,
                        spawnWindowCountSecondary);
                    if (!HasLocalLayerBudget(
                            in candidate,
                            LocalGroundBudget,
                            LocalClusterBudget,
                            LocalStructureBudget,
                            LocalSpawnBudget,
                            groundCount,
                            clusterCount,
                            localStructureCount,
                            localSpawnCount,
                            currentWindowCount))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (!CanAcceptPatternAccentBudget(
                            in candidate,
                            ClusterAccentCounts,
                            StructureAccentCounts,
                            UsesPatternAccentQuotas != 0,
                            clusterCount,
                            globalStructureCount,
                            globalSpawnCount,
                            ClusterTargetMax,
                            StructureTargetMax,
                            SpawnTargetMax,
                            ClusterRatioStart,
                            passiveSpawnCount,
                            predatorSpawnCount,
                            PassiveSpawnMax,
                            PredatorSpawnMax,
                            ClusterAccentRoleMaxRatios,
                            StructureAccentRoleMaxCounts))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (HasSpatialConflict(in candidate))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    Results[candidateIndex] = 1;
                    poissonRejectionAttempts = 0;
                    RegisterAcceptedCandidate(
                        in candidate,
                        ref globalGroundCount,
                        ref globalClusterCount,
                        ref globalStructureCount,
                        ref globalSpawnCount,
                        ref groundCount,
                        ref clusterCount,
                        ref structureCountPrimary,
                        ref structureCountSecondary,
                        ref spawnCountPrimary,
                        ref spawnCountSecondary,
                        ref structureWindowCountPrimary,
                        ref structureWindowCountSecondary,
                        ref spawnWindowCountPrimary,
                        ref spawnWindowCountSecondary,
                        ref passiveSpawnCount,
                        ref predatorSpawnCount);
                }
            }

            private bool HasSpatialConflict(in ScatterCellCandidateAcceptanceInput candidate)
            {
                float maxRelevantDistance = math.max(candidate.EffectiveSpacing, MaxRegisteredPlacementSpacing) * 1.35f;
                int searchRadiusCells = math.max(1, (int)math.ceil(maxRelevantDistance / math.max(1f, CellSize)));
                float maxRelevantDistanceSq = maxRelevantDistance * maxRelevantDistance;

                for (int ox = -searchRadiusCells; ox <= searchRadiusCells; ox++)
                {
                    for (int oz = -searchRadiusCells; oz <= searchRadiusCells; oz++)
                    {
                        int cellKey = ComposeScatterGridNativeKey(candidate.CellX + ox, candidate.CellZ + oz);
                        if (HasSpatialConflictInBuckets(
                                in candidate,
                                maxRelevantDistanceSq,
                                cellKey,
                                ExistingPositionBuckets,
                                ExistingMetadataBuckets,
                                ExistingSpatialMetadata))
                        {
                            return true;
                        }

                        if (HasSpatialConflictInBuckets(
                                in candidate,
                                maxRelevantDistanceSq,
                                cellKey,
                                PendingPositionBuckets,
                                PendingMetadataBuckets,
                                PendingSpatialMetadata.AsArray()))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private bool ExceedsFloraChunkHardCap(in ScatterCellCandidateAcceptanceInput candidate, int hardCap)
            {
                return WorldProceduralScatterDirector.ExceedsFloraChunkHardCap(
                    candidate.CellX,
                    candidate.CellZ,
                    hardCap,
                    ExistingMetadataBuckets,
                    ExistingSpatialMetadata,
                    PendingMetadataBuckets,
                    PendingSpatialMetadata.AsArray());
            }

            private static bool HasSpatialConflictInBuckets(
                in ScatterCellCandidateAcceptanceInput candidate,
                float maxRelevantDistanceSq,
                int cellKey,
                NativeParallelMultiHashMap<int, float3> positionBuckets,
                NativeParallelMultiHashMap<int, int> metadataBuckets,
                NativeArray<ScatterPlacementSpatialMetadata> spatialMetadata)
            {
                if (!positionBuckets.TryGetFirstValue(cellKey, out float3 existingPosition, out NativeParallelMultiHashMapIterator<int> positionIterator))
                    return false;

                bool bucketWithinRange = false;
                do
                {
                    float3 bucketDelta = candidate.Position - existingPosition;
                    if (math.lengthsq(bucketDelta) < maxRelevantDistanceSq)
                    {
                        bucketWithinRange = true;
                        break;
                    }
                }
                while (positionBuckets.TryGetNextValue(out existingPosition, ref positionIterator));

                if (!bucketWithinRange)
                    return false;

                if (!metadataBuckets.TryGetFirstValue(cellKey, out int metadataIndex, out NativeParallelMultiHashMapIterator<int> metadataIterator))
                    return false;

                do
                {
                    ScatterPlacementSpatialMetadata existing = spatialMetadata[metadataIndex];
                    float minDistance = ResolveRequiredDistanceNative(in candidate, in existing);
                    float3 spacingDelta = candidate.Position - existing.Position;
                    if (minDistance > 0f && math.lengthsq(spacingDelta) < minDistance * minDistance)
                        return true;
                }
                while (metadataBuckets.TryGetNextValue(out metadataIndex, ref metadataIterator));

                return false;
            }

            private void RegisterAcceptedCandidate(
                in ScatterCellCandidateAcceptanceInput candidate,
                ref int globalGroundCount,
                ref int globalClusterCount,
                ref int globalStructureCount,
                ref int globalSpawnCount,
                ref int groundCount,
                ref int clusterCount,
                ref int structureCountPrimary,
                ref int structureCountSecondary,
                ref int spawnCountPrimary,
                ref int spawnCountSecondary,
                ref int structureWindowCountPrimary,
                ref int structureWindowCountSecondary,
                ref int spawnWindowCountPrimary,
                ref int spawnWindowCountSecondary,
                ref int passiveSpawnCount,
                ref int predatorSpawnCount)
            {
                int metadataIndex = PendingSpatialMetadata.Length;
                PendingSpatialMetadata.AddNoResize(new ScatterPlacementSpatialMetadata(
                    candidate.Position,
                    candidate.EffectiveSpacing,
                    candidate.FamilyHash,
                    candidate.ScatterLayer,
                    candidate.ProceduralDomain,
                    candidate.FloraBudgetClass));

                int cellKey = ComposeScatterGridNativeKey(candidate.CellX, candidate.CellZ);
                PendingPositionBuckets.Add(cellKey, candidate.Position);
                PendingMetadataBuckets.Add(cellKey, metadataIndex);

                switch ((WorldPrefabFamilyProfile.ScatterLayer)candidate.ScatterLayer)
                {
                    case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                        globalGroundCount++;
                        groundCount++;
                        break;

                    case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                        globalClusterCount++;
                        clusterCount++;
                        IncrementCounter(ClusterAccentCounts, candidate.ClusterAccentRole);
                        break;

                    case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                        globalStructureCount++;
                        IncrementCounter(StructureAccentCounts, candidate.StructureAccentRole);
                        if (candidate.HeightLayerIndex == 0)
                        {
                            structureCountPrimary++;
                            structureWindowCountPrimary++;
                        }
                        else
                        {
                            structureCountSecondary++;
                            structureWindowCountSecondary++;
                        }
                        break;

                    case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                        globalSpawnCount++;
                        if (candidate.HeightLayerIndex == 0)
                        {
                            spawnCountPrimary++;
                            spawnWindowCountPrimary++;
                        }
                        else
                        {
                            spawnCountSecondary++;
                            spawnWindowCountSecondary++;
                        }

                        if (candidate.IsPassiveSpawnFamily != 0)
                            passiveSpawnCount++;
                        else if (candidate.IsPredatorSpawnFamily != 0)
                            predatorSpawnCount++;
                        break;
                }
            }

            private static bool HasPatternLayerBudget(
                int scatterLayer,
                int globalGroundCount,
                int globalClusterCount,
                int globalStructureCount,
                int globalSpawnCount,
                int groundTargetMax,
                int clusterTargetMax,
                int structureTargetMax,
                int spawnTargetMax)
            {
                return (WorldPrefabFamilyProfile.ScatterLayer)scatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Ground => groundTargetMax <= 0 || globalGroundCount < groundTargetMax,
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => clusterTargetMax <= 0 || globalClusterCount < clusterTargetMax,
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => structureTargetMax <= 0 || globalStructureCount < structureTargetMax,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => spawnTargetMax <= 0 || globalSpawnCount < spawnTargetMax,
                    _ => false
                };
            }

            private static bool HasLocalLayerBudget(
                in ScatterCellCandidateAcceptanceInput candidate,
                int localGroundBudget,
                int localClusterBudget,
                int localStructureBudget,
                int localSpawnBudget,
                int groundCount,
                int clusterCount,
                int structureCount,
                int spawnCount,
                int currentWindowCount)
            {
                return (WorldPrefabFamilyProfile.ScatterLayer)candidate.ScatterLayer switch
                {
                    WorldPrefabFamilyProfile.ScatterLayer.Ground => groundCount < localGroundBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Cluster => clusterCount < localClusterBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Structure => structureCount < localStructureBudget && currentWindowCount < localStructureBudget,
                    WorldPrefabFamilyProfile.ScatterLayer.Spawn => spawnCount < localSpawnBudget && currentWindowCount < localSpawnBudget,
                    _ => false
                };
            }

            private static int ResolveCurrentWindowCount(
                int scatterLayer,
                int heightLayerIndex,
                int structureWindowCountPrimary,
                int structureWindowCountSecondary,
                int spawnWindowCountPrimary,
                int spawnWindowCountSecondary)
            {
                if (scatterLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Structure)
                    return heightLayerIndex == 0 ? structureWindowCountPrimary : structureWindowCountSecondary;

                if (scatterLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                    return heightLayerIndex == 0 ? spawnWindowCountPrimary : spawnWindowCountSecondary;

                return 0;
            }

            private static bool CanAcceptPatternAccentBudget(
                in ScatterCellCandidateAcceptanceInput candidate,
                NativeArray<int> clusterAccentCounts,
                NativeArray<int> structureAccentCounts,
                bool usesPatternAccentQuotas,
                int clusterCount,
                int structureCount,
                int spawnCount,
                int clusterTargetMax,
                int structureTargetMax,
                int spawnTargetMax,
                int clusterRatioStart,
                int passiveSpawnCount,
                int predatorSpawnCount,
                int passiveSpawnMax,
                int predatorSpawnMax,
                NativeArray<float> clusterAccentRoleMaxRatios,
                NativeArray<int> structureAccentRoleMaxCounts)
            {
                if (!usesPatternAccentQuotas)
                    return true;

                if (candidate.ScatterLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                {
                    if (clusterCount >= clusterTargetMax)
                        return false;

                    if (candidate.ClusterAccentRole == (int)WorldPrefabFamilyProfile.ClusterAccentRole.None)
                        return true;

                    if (clusterCount < clusterRatioStart)
                        return true;

                    float maxRatio = ReadRatio(clusterAccentRoleMaxRatios, candidate.ClusterAccentRole);
                    if (maxRatio <= 0f)
                        return false;

                    int roleCount = ReadCounter(clusterAccentCounts, candidate.ClusterAccentRole);
                    int totalAfterPlacement = math.max(1, clusterCount + 1);
                    int allowed = math.max(1, (int)math.ceil(maxRatio * totalAfterPlacement));
                    return roleCount < allowed;
                }

                if (candidate.ScatterLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Structure)
                {
                    if (structureCount >= structureTargetMax)
                        return false;

                    int roleMax = ReadCounter(structureAccentRoleMaxCounts, candidate.StructureAccentRole);
                    if (roleMax <= 0)
                        return false;

                    int roleCount = ReadCounter(structureAccentCounts, candidate.StructureAccentRole);
                    return roleCount < roleMax;
                }

                if (candidate.ScatterLayer != (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                    return true;

                if (spawnCount >= spawnTargetMax)
                    return false;

                if (candidate.IsPredatorSpawnFamily != 0)
                    return predatorSpawnCount < predatorSpawnMax;

                if (candidate.IsPassiveSpawnFamily != 0)
                    return passiveSpawnCount < passiveSpawnMax;

                return true;
            }

            private static int ReadCounter(NativeArray<int> counters, int index)
            {
                return index >= 0 && index < counters.Length ? counters[index] : 0;
            }

            private static float ReadRatio(NativeArray<float> ratios, int index)
            {
                return index >= 0 && index < ratios.Length ? ratios[index] : 0f;
            }

            private static void IncrementCounter(NativeArray<int> counters, int index)
            {
                if (index < 0 || index >= counters.Length)
                    return;

                counters[index] = counters[index] + 1;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct EvaluateScatterRescueCandidateBatchJob : IJob
        {
            [ReadOnly] public NativeParallelMultiHashMap<int, float3> ExistingPositionBuckets;
            [ReadOnly] public NativeParallelMultiHashMap<int, int> ExistingMetadataBuckets;
            [ReadOnly] public NativeArray<ScatterPlacementSpatialMetadata> ExistingSpatialMetadata;
            [ReadOnly] public NativeArray<ScatterCellCandidateAcceptanceInput> Candidates;
            [ReadOnly] public NativeArray<float> ClusterAccentRoleMaxRatios;
            [ReadOnly] public NativeArray<int> StructureAccentRoleMaxCounts;
            public NativeArray<byte> Results;
            public NativeList<ScatterPlacementSpatialMetadata> PendingSpatialMetadata;
            public NativeParallelMultiHashMap<int, float3> PendingPositionBuckets;
            public NativeParallelMultiHashMap<int, int> PendingMetadataBuckets;
            public NativeArray<int> ClusterAccentCounts;
            public NativeArray<int> StructureAccentCounts;
            public float MaxRegisteredPlacementSpacing;
            public float CellSize;
            public float ChunkSize;
            public int LayerTargetMax;
            public int AcceptLimit;
            public int CurrentLayerCount;
            public int RescueLayer;
            public int ClusterRatioStart;
            public int PassiveSpawnCount;
            public int PredatorSpawnCount;
            public int PassiveSpawnMax;
            public int PredatorSpawnMax;
            public byte UsesPatternAccentQuotas;
            public int FloraChunkHardCap;
            public int MaxPoissonRejectionAttempts;

            public void Execute()
            {
                for (int index = 0; index < Results.Length; index++)
                    Results[index] = 0;

                int acceptedCount = 0;
                int layerCount = CurrentLayerCount;
                int passiveSpawnCount = PassiveSpawnCount;
                int predatorSpawnCount = PredatorSpawnCount;
                int poissonRejectionAttempts = 0;
                int maxPoissonRejectionAttempts = math.max(1, MaxPoissonRejectionAttempts);

                for (int candidateIndex = 0; candidateIndex < Candidates.Length; candidateIndex++)
                {
                    ScatterCellCandidateAcceptanceInput candidate = Candidates[candidateIndex];
                    if (candidate.ExternalBlock != 0)
                        continue;

                    if (acceptedCount >= AcceptLimit)
                        break;

                    if (!PassesFloraClusterPatch(in candidate, ChunkSize))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (candidate.FloraBudgetClass != (byte)FloraBudgetClass.None &&
                        ExceedsFloraChunkHardCap(in candidate, FloraChunkHardCap))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (LayerTargetMax > 0 && layerCount >= LayerTargetMax)
                        break;

                    if (!CanAcceptAccentBudget(in candidate, layerCount, passiveSpawnCount, predatorSpawnCount))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    if (HasSpatialConflict(in candidate))
                    {
                        if (ScatterCandidateEvaluator.RegisterPoissonRejection(ref poissonRejectionAttempts, maxPoissonRejectionAttempts))
                            break;

                        continue;
                    }

                    Results[candidateIndex] = 1;
                    poissonRejectionAttempts = 0;
                    acceptedCount++;
                    layerCount++;
                    RegisterAcceptedCandidate(in candidate, ref passiveSpawnCount, ref predatorSpawnCount);
                }
            }

            private bool CanAcceptAccentBudget(
                in ScatterCellCandidateAcceptanceInput candidate,
                int layerCount,
                int passiveSpawnCount,
                int predatorSpawnCount)
            {
                if (UsesPatternAccentQuotas == 0)
                    return true;

                if (RescueLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                {
                    if (candidate.ClusterAccentRole == (int)WorldPrefabFamilyProfile.ClusterAccentRole.None)
                        return true;

                    if (layerCount < ClusterRatioStart)
                        return true;

                    float maxRatio = ReadRatio(ClusterAccentRoleMaxRatios, candidate.ClusterAccentRole);
                    if (maxRatio <= 0f)
                        return false;

                    int roleCount = ReadCounter(ClusterAccentCounts, candidate.ClusterAccentRole);
                    int totalAfterPlacement = math.max(1, layerCount + 1);
                    int allowed = math.max(1, (int)math.ceil(maxRatio * totalAfterPlacement));
                    return roleCount < allowed;
                }

                if (RescueLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Structure)
                {
                    int roleMax = ReadCounter(StructureAccentRoleMaxCounts, candidate.StructureAccentRole);
                    if (roleMax <= 0)
                        return false;

                    int roleCount = ReadCounter(StructureAccentCounts, candidate.StructureAccentRole);
                    return roleCount < roleMax;
                }

                if (RescueLayer != (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                    return true;

                if (candidate.IsPredatorSpawnFamily != 0)
                    return predatorSpawnCount < PredatorSpawnMax;

                if (candidate.IsPassiveSpawnFamily != 0)
                    return passiveSpawnCount < PassiveSpawnMax;

                return true;
            }

            private bool HasSpatialConflict(in ScatterCellCandidateAcceptanceInput candidate)
            {
                float maxRelevantDistance = math.max(candidate.EffectiveSpacing, MaxRegisteredPlacementSpacing) * 1.35f;
                int searchRadiusCells = math.max(1, (int)math.ceil(maxRelevantDistance / math.max(1f, CellSize)));
                float maxRelevantDistanceSq = maxRelevantDistance * maxRelevantDistance;

                for (int ox = -searchRadiusCells; ox <= searchRadiusCells; ox++)
                {
                    for (int oz = -searchRadiusCells; oz <= searchRadiusCells; oz++)
                    {
                        int cellKey = ComposeScatterGridNativeKey(candidate.CellX + ox, candidate.CellZ + oz);
                        if (HasSpatialConflictInBuckets(
                                in candidate,
                                maxRelevantDistanceSq,
                                cellKey,
                                ExistingPositionBuckets,
                                ExistingMetadataBuckets,
                                ExistingSpatialMetadata))
                        {
                            return true;
                        }

                        if (HasSpatialConflictInBuckets(
                                in candidate,
                                maxRelevantDistanceSq,
                                cellKey,
                                PendingPositionBuckets,
                                PendingMetadataBuckets,
                                PendingSpatialMetadata.AsArray()))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            private bool ExceedsFloraChunkHardCap(in ScatterCellCandidateAcceptanceInput candidate, int hardCap)
            {
                return WorldProceduralScatterDirector.ExceedsFloraChunkHardCap(
                    candidate.CellX,
                    candidate.CellZ,
                    hardCap,
                    ExistingMetadataBuckets,
                    ExistingSpatialMetadata,
                    PendingMetadataBuckets,
                    PendingSpatialMetadata.AsArray());
            }

            private static bool HasSpatialConflictInBuckets(
                in ScatterCellCandidateAcceptanceInput candidate,
                float maxRelevantDistanceSq,
                int cellKey,
                NativeParallelMultiHashMap<int, float3> positionBuckets,
                NativeParallelMultiHashMap<int, int> metadataBuckets,
                NativeArray<ScatterPlacementSpatialMetadata> spatialMetadata)
            {
                if (!positionBuckets.TryGetFirstValue(cellKey, out float3 existingPosition, out NativeParallelMultiHashMapIterator<int> positionIterator))
                    return false;

                bool bucketWithinRange = false;
                do
                {
                    float3 bucketDelta = candidate.Position - existingPosition;
                    if (math.lengthsq(bucketDelta) < maxRelevantDistanceSq)
                    {
                        bucketWithinRange = true;
                        break;
                    }
                }
                while (positionBuckets.TryGetNextValue(out existingPosition, ref positionIterator));

                if (!bucketWithinRange)
                    return false;

                if (!metadataBuckets.TryGetFirstValue(cellKey, out int metadataIndex, out NativeParallelMultiHashMapIterator<int> metadataIterator))
                    return false;

                do
                {
                    ScatterPlacementSpatialMetadata existing = spatialMetadata[metadataIndex];
                    float minDistance = ResolveRequiredDistanceNative(in candidate, in existing);
                    float3 spacingDelta = candidate.Position - existing.Position;
                    if (minDistance > 0f && math.lengthsq(spacingDelta) < minDistance * minDistance)
                        return true;
                }
                while (metadataBuckets.TryGetNextValue(out metadataIndex, ref metadataIterator));

                return false;
            }

            private void RegisterAcceptedCandidate(
                in ScatterCellCandidateAcceptanceInput candidate,
                ref int passiveSpawnCount,
                ref int predatorSpawnCount)
            {
                int metadataIndex = PendingSpatialMetadata.Length;
                PendingSpatialMetadata.AddNoResize(new ScatterPlacementSpatialMetadata(
                    candidate.Position,
                    candidate.EffectiveSpacing,
                    candidate.FamilyHash,
                    candidate.ScatterLayer,
                    candidate.ProceduralDomain,
                    candidate.FloraBudgetClass));

                int cellKey = ComposeScatterGridNativeKey(candidate.CellX, candidate.CellZ);
                PendingPositionBuckets.Add(cellKey, candidate.Position);
                PendingMetadataBuckets.Add(cellKey, metadataIndex);

                if (RescueLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Cluster)
                {
                    IncrementCounter(ClusterAccentCounts, candidate.ClusterAccentRole);
                    return;
                }

                if (RescueLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Structure)
                {
                    IncrementCounter(StructureAccentCounts, candidate.StructureAccentRole);
                    return;
                }

                if (RescueLayer != (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn)
                    return;

                if (candidate.IsPassiveSpawnFamily != 0)
                    passiveSpawnCount++;
                else if (candidate.IsPredatorSpawnFamily != 0)
                    predatorSpawnCount++;
            }

            private static int ReadCounter(NativeArray<int> counters, int index)
            {
                return index >= 0 && index < counters.Length ? counters[index] : 0;
            }

            private static float ReadRatio(NativeArray<float> ratios, int index)
            {
                return index >= 0 && index < ratios.Length ? ratios[index] : 0f;
            }

            private static void IncrementCounter(NativeArray<int> counters, int index)
            {
                if (index < 0 || index >= counters.Length)
                    return;

                counters[index] = counters[index] + 1;
            }
        }

        private static int ComposeScatterGridNativeKey(int cellX, int cellZ)
        {
            unchecked
            {
                return (int)(((uint)cellX * 73856093u) ^ ((uint)cellZ * 19349663u));
            }
        }

        private static float ResolveRequiredDistanceNative(
            float candidateSpacing,
            int candidateFamilyHash,
            int candidateLayer,
            int candidateDomain,
            in ScatterPlacementSpatialMetadata existing)
        {
            float maxSpacing = math.max(candidateSpacing, existing.EffectiveSpacing);
            if (candidateFamilyHash != 0 && candidateFamilyHash == existing.FamilyHash)
                return maxSpacing;

            if (candidateLayer == existing.ScatterLayer)
            {
                switch ((WorldPrefabFamilyProfile.ScatterLayer)candidateLayer)
                {
                    case WorldPrefabFamilyProfile.ScatterLayer.Ground:
                        return math.max(1.25f, maxSpacing * 0.52f);
                    case WorldPrefabFamilyProfile.ScatterLayer.Cluster:
                        return math.max(3f, maxSpacing * 0.92f);
                    case WorldPrefabFamilyProfile.ScatterLayer.Structure:
                        return math.max(12f, maxSpacing);
                    case WorldPrefabFamilyProfile.ScatterLayer.Spawn:
                        return math.max(14f, maxSpacing * 1.08f);
                    default:
                        return maxSpacing;
                }
            }

            bool candidatePocket = IsPocketDomain(candidateDomain);
            bool existingPocket = IsPocketDomain(existing.ProceduralDomain);
            if (candidatePocket && existingPocket)
                return math.max(10f, maxSpacing * 1.35f);

            bool candidateStructure = IsStructureLayer(candidateLayer);
            bool existingStructure = IsStructureLayer(existing.ScatterLayer);
            if (candidateStructure && existingStructure)
                return math.max(14f, maxSpacing * 0.88f);

            bool candidateSpawn = candidateLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            bool existingSpawn = existing.ScatterLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn;
            if ((candidateSpawn && existingStructure) || (candidateStructure && existingSpawn))
                return math.max(12f, math.max(candidateSpacing, existing.EffectiveSpacing) * 0.9f);

            return 0f;
        }

        private static float ResolveRequiredDistanceNative(
            in ScatterCellCandidateAcceptanceInput candidate,
            in ScatterPlacementSpatialMetadata existing)
        {
            return ResolveRequiredDistanceNative(
                candidate.EffectiveSpacing,
                candidate.FamilyHash,
                candidate.ScatterLayer,
                candidate.ProceduralDomain,
                in existing);
        }

        private static bool IsPocketDomain(int domain)
        {
            return domain == (int)WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket
                || domain == (int)WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket
                || domain == (int)WorldPrefabFamilyProfile.ProceduralDomain.SafePocket;
        }

        private static bool IsStructureLayer(int layer)
        {
            return layer == (int)WorldPrefabFamilyProfile.ScatterLayer.Structure;
        }

        private static bool ExceedsFloraChunkHardCap(
            int candidateCellX,
            int candidateCellZ,
            int hardCap,
            NativeParallelMultiHashMap<int, int> metadataBuckets,
            NativeArray<ScatterPlacementSpatialMetadata> spatialMetadata)
        {
            if (hardCap <= 0)
                return false;

            int cellKey = ComposeScatterGridNativeKey(candidateCellX, candidateCellZ);
            return CountFloraInChunkCell(cellKey, metadataBuckets, spatialMetadata) >= hardCap;
        }

        private static bool ExceedsFloraChunkHardCap(
            int candidateCellX,
            int candidateCellZ,
            int hardCap,
            NativeParallelMultiHashMap<int, int> existingMetadataBuckets,
            NativeArray<ScatterPlacementSpatialMetadata> existingSpatialMetadata,
            NativeParallelMultiHashMap<int, int> pendingMetadataBuckets,
            NativeArray<ScatterPlacementSpatialMetadata> pendingSpatialMetadata)
        {
            if (hardCap <= 0)
                return false;

            int cellKey = ComposeScatterGridNativeKey(candidateCellX, candidateCellZ);
            int existingCount = CountFloraInChunkCell(cellKey, existingMetadataBuckets, existingSpatialMetadata);
            if (existingCount >= hardCap)
                return true;

            int pendingCount = CountFloraInChunkCell(cellKey, pendingMetadataBuckets, pendingSpatialMetadata);
            return existingCount + pendingCount >= hardCap;
        }

        private static int CountFloraInChunkCell(
            int cellKey,
            NativeParallelMultiHashMap<int, int> metadataBuckets,
            NativeArray<ScatterPlacementSpatialMetadata> spatialMetadata)
        {
            if (!metadataBuckets.IsCreated || !spatialMetadata.IsCreated)
                return 0;

            int count = 0;
            if (!metadataBuckets.TryGetFirstValue(cellKey, out int metadataIndex, out NativeParallelMultiHashMapIterator<int> metadataIterator))
                return 0;

            do
            {
                if (metadataIndex < 0 || metadataIndex >= spatialMetadata.Length)
                    continue;

                if (spatialMetadata[metadataIndex].FloraBudgetClass != (byte)FloraBudgetClass.None)
                    count++;
            }
            while (metadataBuckets.TryGetNextValue(out metadataIndex, ref metadataIterator));

            return count;
        }

        private bool TryEvaluateScatterRescueCandidateAcceptanceBatch(
            List<ScatterCandidate> orderedCandidates,
            in ScatterRescueCandidateFilter filter,
            WorldProceduralPattern pattern,
            HectonBiomeMatrixProfile biomeProfile,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int acceptLimit,
            int stride,
            int perWindowBudget,
            int layerTargetMax,
            int currentLayerCount,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount)
        {
            EnsureWorkingMemory();
            if (_memory == null || orderedCandidates == null || orderedCandidates.Count <= 0 || acceptLimit <= 0)
                return false;

            if (!_memory.PrepareScatterCellCandidateAcceptanceBatch(orderedCandidates.Count))
                return false;

            NativeArray<ScatterCellCandidateAcceptanceInput> inputs = _memory.CandidateAcceptanceBatchInputs.AsArray();
            NativeArray<byte> results = _memory.CandidateAcceptanceBatchResults.AsArray();
            ScatterPlacementRegistrationContext registrationContext = new ScatterPlacementRegistrationContext(
                _desiredPlacements,
                _retainedPlacements,
                _placementLastSeenTimes,
                Application.isPlaying ? Time.time : 0f);

            for (int i = 0; i < orderedCandidates.Count; i++)
            {
                ScatterCandidate candidate = orderedCandidates[i];
                ScatterCellCandidateAcceptanceInput input = BuildScatterCellCandidateAcceptanceInput(candidate, in registrationContext);
                if (input.ExternalBlock == 0 &&
                    (!MatchesRescueCandidateFilter(candidate, in filter) ||
                     IsRescueCandidateWindowOrCellBlocked(candidate, layer, stride, perWindowBudget)))
                {
                    input.ExternalBlock = 1;
                }

                inputs[i] = input;
                results[i] = 0;
            }

            CopyAccentCountsToScratch(clusterAccentCounts, _memory.CandidateAcceptanceClusterAccentCountsScratch);
            CopyAccentCountsToScratch(structureAccentCounts, _memory.CandidateAcceptanceStructureAccentCountsScratch);
            CopyAccentRatiosToScratch(_clusterAccentRoleMaxRatioBuffer, _memory.CandidateAcceptanceClusterAccentRoleMaxRatiosScratch);
            CopyAccentCountsToScratch(_structureAccentRoleMaxBuffer, _memory.CandidateAcceptanceStructureAccentRoleMaxCountsScratch);

            EvaluateScatterRescueCandidateBatchJob job = new EvaluateScatterRescueCandidateBatchJob
            {
                ExistingPositionBuckets = _memory.GridPlacementPositionBuckets,
                ExistingMetadataBuckets = _memory.GridPlacementMetadataBuckets,
                ExistingSpatialMetadata = _memory.GridPlacementSpatialMetadata.AsArray(),
                Candidates = inputs,
                ClusterAccentRoleMaxRatios = _memory.CandidateAcceptanceClusterAccentRoleMaxRatiosScratch,
                StructureAccentRoleMaxCounts = _memory.CandidateAcceptanceStructureAccentRoleMaxCountsScratch,
                Results = results,
                PendingSpatialMetadata = _memory.CandidateAcceptanceBatchPendingMetadata,
                PendingPositionBuckets = _memory.CandidateAcceptanceBatchPendingPositionBuckets,
                PendingMetadataBuckets = _memory.CandidateAcceptanceBatchPendingMetadataBuckets,
                ClusterAccentCounts = _memory.CandidateAcceptanceClusterAccentCountsScratch,
                StructureAccentCounts = _memory.CandidateAcceptanceStructureAccentCountsScratch,
                MaxRegisteredPlacementSpacing = _maxRegisteredPlacementSpacingMeters,
                CellSize = math.max(1f, _runtimeStreamingState.CellSize),
                ChunkSize = math.max(1f, _runtimeStreamingState.ChunkSize),
                LayerTargetMax = layerTargetMax <= 0 ? int.MaxValue : layerTargetMax,
                AcceptLimit = acceptLimit,
                CurrentLayerCount = currentLayerCount,
                RescueLayer = (int)layer,
                ClusterRatioStart = ResolvePatternClusterRatioStart(pattern),
                PassiveSpawnCount = passiveSpawnCount,
                PredatorSpawnCount = predatorSpawnCount,
                PassiveSpawnMax = Mathf.Max(
                    ResolvePatternPassiveSpawnMin(pattern, biomeProfile),
                    ResolvePatternSpawnTargetMax(pattern, biomeProfile)),
                PredatorSpawnMax = Mathf.Max(0, ResolvePatternPredatorSpawnMax(pattern, biomeProfile)),
                UsesPatternAccentQuotas = UsesPatternAccentQuotas(pattern) ? (byte)1 : (byte)0,
                FloraChunkHardCap = FloraChunkInstanceHardCap,
                MaxPoissonRejectionAttempts = PoissonDiskMaxRejectionAttempts
            };

            job.Execute();
            return true;
        }

        private bool MatchesRescueCandidateFilter(
            ScatterCandidate candidate,
            in ScatterRescueCandidateFilter filter)
        {
            WorldPrefabFamilyProfile family = candidate.Family;
            if (family == null)
                return false;

            if (filter.UseExactFamily != 0 && !IsSameFamily(family, filter.ExactFamily))
                return false;

            if (filter.UseDomain != 0 && family.proceduralDomain != filter.Domain)
                return false;

            if (filter.UsePlacementMode != 0 && family.placementMode != filter.PlacementMode)
                return false;

            if (filter.UseClusterAccentRole != 0 && GetClusterAccentRole(family) != filter.ClusterAccentRole)
                return false;

            if (filter.UseStructureAccentRole != 0 && GetStructureAccentRole(family) != filter.StructureAccentRole)
                return false;

            if (filter.PassiveOnly != 0 && !IsPassiveSpawnFamily(family))
                return false;

            if (filter.PredatorOnly != 0 && !IsPredatorSpawnFamily(family))
                return false;

            return true;
        }

        private bool IsRescueCandidateWindowOrCellBlocked(
            ScatterCandidate candidate,
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int stride,
            int perWindowBudget)
        {
            ScatterPlacement placement = candidate.Placement;
            if (placement == null)
                return true;

            if (layer == WorldPrefabFamilyProfile.ScatterLayer.Ground ||
                layer == WorldPrefabFamilyProfile.ScatterLayer.Cluster)
            {
                long cellKey = ComposeWindowKey(placement.CellX, placement.CellZ, 1, placement.HeightLayerIndex);
                return _occupiedCellBuffer.Contains(cellKey);
            }

            if (perWindowBudget <= 0)
                return true;

            Dictionary<long, int> windowCounts = layer == WorldPrefabFamilyProfile.ScatterLayer.Structure
                ? _structureWindowCounts
                : _spawnWindowCounts;
            long windowKey = ComposeWindowKey(placement.CellX, placement.CellZ, stride, placement.HeightLayerIndex);
            return GetWindowPlacementCount(windowKey, windowCounts) >= perWindowBudget;
        }

        private bool TryEvaluateScatterCellCandidateAcceptanceBatch(
            ref ScatterCellPlacementCounters cellPlacementCounters,
            in ScatterCellPlacementAcceptanceContext acceptanceContext,
            int[] layerPlacementCounts,
            int[] clusterAccentCounts,
            int[] structureAccentCounts,
            int passiveSpawnCount,
            int predatorSpawnCount)
        {
            EnsureWorkingMemory();
            if (_memory == null || _candidateBuffer.Count <= 0)
                return false;

            if (!_memory.PrepareScatterCellCandidateAcceptanceBatch(_candidateBuffer.Count))
                return false;

            if (!_memory.CandidateAcceptanceClusterAccentCountsScratch.IsCreated ||
                !_memory.CandidateAcceptanceStructureAccentCountsScratch.IsCreated)
            {
                return false;
            }

            NativeArray<ScatterCellCandidateAcceptanceInput> inputs = _memory.CandidateAcceptanceBatchInputs.AsArray();
            NativeArray<byte> results = _memory.CandidateAcceptanceBatchResults.AsArray();
            for (int i = 0; i < _candidateBuffer.Count; i++)
            {
                inputs[i] = BuildScatterCellCandidateAcceptanceInput(_candidateBuffer[i], in acceptanceContext.PlacementRegistrationContext);
                results[i] = 0;
            }

            CopyAccentCountsToScratch(clusterAccentCounts, _memory.CandidateAcceptanceClusterAccentCountsScratch);
            CopyAccentCountsToScratch(structureAccentCounts, _memory.CandidateAcceptanceStructureAccentCountsScratch);

            ResolveScatterCellWindowCounts(
                in acceptanceContext,
                out int structureWindowCountPrimary,
                out int structureWindowCountSecondary,
                out int spawnWindowCountPrimary,
                out int spawnWindowCountSecondary);

            CopyAccentRatiosToScratch(_clusterAccentRoleMaxRatioBuffer, _memory.CandidateAcceptanceClusterAccentRoleMaxRatiosScratch);
            CopyAccentCountsToScratch(_structureAccentRoleMaxBuffer, _memory.CandidateAcceptanceStructureAccentRoleMaxCountsScratch);

            EvaluateScatterCellCandidateBatchJob job = new EvaluateScatterCellCandidateBatchJob
            {
                ExistingPositionBuckets = _memory.GridPlacementPositionBuckets,
                ExistingMetadataBuckets = _memory.GridPlacementMetadataBuckets,
                ExistingSpatialMetadata = _memory.GridPlacementSpatialMetadata.AsArray(),
                Candidates = inputs,
                ClusterAccentRoleMaxRatios = _memory.CandidateAcceptanceClusterAccentRoleMaxRatiosScratch,
                StructureAccentRoleMaxCounts = _memory.CandidateAcceptanceStructureAccentRoleMaxCountsScratch,
                Results = results,
                PendingSpatialMetadata = _memory.CandidateAcceptanceBatchPendingMetadata,
                PendingPositionBuckets = _memory.CandidateAcceptanceBatchPendingPositionBuckets,
                PendingMetadataBuckets = _memory.CandidateAcceptanceBatchPendingMetadataBuckets,
                ClusterAccentCounts = _memory.CandidateAcceptanceClusterAccentCountsScratch,
                StructureAccentCounts = _memory.CandidateAcceptanceStructureAccentCountsScratch,
                MaxRegisteredPlacementSpacing = _maxRegisteredPlacementSpacingMeters,
                CellSize = math.max(1f, _runtimeStreamingState.CellSize),
                ChunkSize = math.max(1f, _runtimeStreamingState.ChunkSize),
                GroundTargetMax = GetLayerTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Ground, layerPlacementCounts, _patternLayerTargetMaxBuffer),
                ClusterTargetMax = GetLayerTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Cluster, layerPlacementCounts, _patternLayerTargetMaxBuffer),
                StructureTargetMax = GetLayerTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Structure, layerPlacementCounts, _patternLayerTargetMaxBuffer),
                SpawnTargetMax = GetLayerTargetMax(WorldPrefabFamilyProfile.ScatterLayer.Spawn, layerPlacementCounts, _patternLayerTargetMaxBuffer),
                GlobalGroundCount = ReadLayerPlacementCount(layerPlacementCounts, WorldPrefabFamilyProfile.ScatterLayer.Ground),
                GlobalClusterCount = ReadLayerPlacementCount(layerPlacementCounts, WorldPrefabFamilyProfile.ScatterLayer.Cluster),
                GlobalStructureCount = ReadLayerPlacementCount(layerPlacementCounts, WorldPrefabFamilyProfile.ScatterLayer.Structure),
                GlobalSpawnCount = ReadLayerPlacementCount(layerPlacementCounts, WorldPrefabFamilyProfile.ScatterLayer.Spawn),
                LocalGroundBudget = acceptanceContext.LocalGroundBudget,
                LocalClusterBudget = acceptanceContext.LocalClusterBudget,
                LocalStructureBudget = acceptanceContext.LocalStructureBudget,
                LocalSpawnBudget = acceptanceContext.LocalSpawnBudget,
                ClusterCount = cellPlacementCounters.ClusterCount,
                GroundCount = cellPlacementCounters.GroundCount,
                StructureCountPrimary = cellPlacementCounters.StructureCountPrimary,
                StructureCountSecondary = cellPlacementCounters.StructureCountSecondary,
                SpawnCountPrimary = cellPlacementCounters.SpawnCountPrimary,
                SpawnCountSecondary = cellPlacementCounters.SpawnCountSecondary,
                StructureWindowCountPrimary = structureWindowCountPrimary,
                StructureWindowCountSecondary = structureWindowCountSecondary,
                SpawnWindowCountPrimary = spawnWindowCountPrimary,
                SpawnWindowCountSecondary = spawnWindowCountSecondary,
                ClusterRatioStart = acceptanceContext.ClusterRatioStart,
                PassiveSpawnCount = passiveSpawnCount,
                PredatorSpawnCount = predatorSpawnCount,
                PassiveSpawnMax = acceptanceContext.PassiveSpawnMax,
                PredatorSpawnMax = acceptanceContext.PredatorSpawnMax,
                UsesPatternAccentQuotas = acceptanceContext.UsesPatternAccentQuotas ? (byte)1 : (byte)0,
                FloraChunkHardCap = FloraChunkInstanceHardCap,
                MaxPoissonRejectionAttempts = PoissonDiskMaxRejectionAttempts
            };

            job.Execute();
            return true;
        }

        private ScatterCellCandidateAcceptanceInput BuildScatterCellCandidateAcceptanceInput(
            ScatterCandidate candidate,
            in ScatterPlacementRegistrationContext registrationContext)
        {
            ScatterCellCandidateAcceptanceInput input = default;
            ScatterPlacement placement = candidate.Placement;
            WorldPrefabFamilyProfile family = candidate.Family;
            if (placement == null || family == null)
            {
                input.ExternalBlock = 1;
                return input;
            }

            input.Position = new float3(placement.Position.x, placement.Position.y, placement.Position.z);
            input.EffectiveSpacing = placement.EffectiveSpacing;
            input.CellX = placement.CellX;
            input.CellZ = placement.CellZ;
            input.HeightLayerIndex = placement.HeightLayerIndex;
            input.FamilyHash = family.FamilyHash;
            input.ScatterLayer = (int)family.scatterLayer;
            input.ProceduralDomain = (int)family.proceduralDomain;
            input.ClusterAccentRole = (int)GetClusterAccentRole(family);
            input.StructureAccentRole = (int)GetStructureAccentRole(family);
            input.IsPassiveSpawnFamily = IsPassiveSpawnFamily(family) ? (byte)1 : (byte)0;
            input.IsPredatorSpawnFamily = IsPredatorSpawnFamily(family) ? (byte)1 : (byte)0;
            input.RequiresClusterPatch = ShouldApplyFloraClusterPatch(family) ? (byte)1 : (byte)0;
            input.ClusterNoiseScale = ResolveEffectiveFloraClusterNoiseScale(placement.Rule);
            input.ClusterNoiseThreshold = ResolveEffectiveFloraClusterNoiseThreshold(family, placement.Rule);
            ScatterCandidatePreview shadePreview = new ScatterCandidatePreview(
                family.FamilyHash,
                placement.Position,
                placement.HeightLayerIndex,
                placement.CellX,
                placement.CellZ);
            input.ExternalBlock = IsPlacementRegistrationBlocked(placement, in registrationContext) ||
                                  ShouldRejectForMigratorySargassumShade(family, in shadePreview)
                ? (byte)1
                : (byte)0;
            input.FloraBudgetClass = (byte)ResolveFloraBudgetClass(family);
            return input;
        }

        private static bool ShouldApplyFloraClusterPatch(WorldPrefabFamilyProfile family)
        {
            if (family == null)
                return false;

            return ScatterMath.ResolveFloraBudgetClassId(family) != 0;
        }

        private static float ResolveEffectiveFloraClusterNoiseScale(WorldProceduralPlacementRule rule)
        {
            return rule != null && rule.clusterNoiseScale > 0f
                ? Mathf.Max(0.0001f, rule.clusterNoiseScale)
                : FloraFallbackClusterNoiseScale;
        }

        private static float ResolveEffectiveFloraClusterNoiseThreshold(
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule)
        {
            float authoredThreshold = rule != null ? rule.clusterNoiseThreshold : 0f;
            byte floraClass = ScatterMath.ResolveFloraBudgetClassId(family);
            float minimumThreshold = floraClass == (byte)FloraBudgetClass.Micro
                ? FloraMicroClusterPatchThreshold
                : floraClass == (byte)FloraBudgetClass.Macro
                    ? FloraMacroClusterPatchThreshold
                    : 0f;
            return Mathf.Clamp01(Mathf.Max(authoredThreshold, minimumThreshold));
        }

        private static bool PassesFloraClusterPatch(
            in ScatterCellCandidateAcceptanceInput candidate,
            float chunkSize)
        {
            if (candidate.RequiresClusterPatch == 0)
                return true;

            return PassesFloraClusterPatch(
                candidate.Position,
                candidate.CellX,
                candidate.CellZ,
                candidate.FamilyHash,
                candidate.ClusterNoiseScale,
                candidate.ClusterNoiseThreshold,
                chunkSize);
        }

        private static bool PassesFloraClusterPatch(
            float3 position,
            int cellX,
            int cellZ,
            int familyHash,
            float clusterNoiseScale,
            float clusterNoiseThreshold,
            float chunkSize)
        {
            if (clusterNoiseThreshold <= 0f)
                return true;

            float safeChunkSize = math.max(1f, chunkSize);
            int chunkX = (int)math.floor(position.x / safeChunkSize);
            int chunkZ = (int)math.floor(position.z / safeChunkSize);
            float mask = ScatterMath.EvaluateClusterPatchMask01(
                position.x,
                position.z,
                chunkX,
                chunkZ,
                clusterNoiseScale,
                familyHash ^ (cellX * 73856093) ^ (cellZ * 19349663),
                familyHash);
            return mask >= clusterNoiseThreshold;
        }

        private static int GetLayerTargetMax(
            WorldPrefabFamilyProfile.ScatterLayer layer,
            int[] layerPlacementCounts,
            int[] layerTargetMaxBuffer)
        {
            int layerIndex = (int)layer;
            if (layerTargetMaxBuffer == null || layerIndex < 0 || layerIndex >= layerTargetMaxBuffer.Length)
                return int.MaxValue;

            int targetMax = layerTargetMaxBuffer[layerIndex];
            return targetMax <= 0 ? int.MaxValue : targetMax;
        }

        private static int ReadLayerPlacementCount(int[] layerPlacementCounts, WorldPrefabFamilyProfile.ScatterLayer layer)
        {
            int layerIndex = (int)layer;
            if (layerPlacementCounts == null || layerIndex < 0 || layerIndex >= layerPlacementCounts.Length)
                return 0;

            return layerPlacementCounts[layerIndex];
        }

        private static void CopyAccentCountsToScratch(int[] source, NativeArray<int> destination)
        {
            int destinationLength = destination.Length;
            for (int i = 0; i < destinationLength; i++)
                destination[i] = source != null && i < source.Length ? source[i] : 0;
        }

        private static void CopyAccentRatiosToScratch(float[] source, NativeArray<float> destination)
        {
            int destinationLength = destination.Length;
            for (int i = 0; i < destinationLength; i++)
                destination[i] = source != null && i < source.Length ? source[i] : 0f;
        }

        private void ResolveScatterCellWindowCounts(
            in ScatterCellPlacementAcceptanceContext acceptanceContext,
            out int structureWindowCountPrimary,
            out int structureWindowCountSecondary,
            out int spawnWindowCountPrimary,
            out int spawnWindowCountSecondary)
        {
            structureWindowCountPrimary = 0;
            structureWindowCountSecondary = 0;
            spawnWindowCountPrimary = 0;
            spawnWindowCountSecondary = 0;

            if (_candidateBuffer.Count <= 0)
                return;

            ScatterPlacement placement = _candidateBuffer[0].Placement;
            if (placement == null)
                return;

            structureWindowCountPrimary = GetWindowPlacementCount(
                placement.CellX,
                placement.CellZ,
                acceptanceContext.StructureStride,
                0,
                _structureWindowCounts);
            structureWindowCountSecondary = GetWindowPlacementCount(
                placement.CellX,
                placement.CellZ,
                acceptanceContext.StructureStride,
                1,
                _structureWindowCounts);
            spawnWindowCountPrimary = GetWindowPlacementCount(
                placement.CellX,
                placement.CellZ,
                acceptanceContext.SpawnStride,
                0,
                _spawnWindowCounts);
            spawnWindowCountSecondary = GetWindowPlacementCount(
                placement.CellX,
                placement.CellZ,
                acceptanceContext.SpawnStride,
                1,
                _spawnWindowCounts);
        }

        private bool IsPlacementRegistrationBlocked(
            ScatterPlacement placement,
            in ScatterPlacementRegistrationContext registrationContext)
        {
            if (placement == null || placement.Key == 0L)
                return true;

            if (placement.IsPooled)
                return true;

            return proceduralStateRegistry != null && proceduralStateRegistry.IsPlacementSuppressed(placement.Key);
        }

        private bool CanAcceptCandidateNative(in ScatterCandidate candidate)
        {
            EnsureWorkingMemory();
            if (_memory == null ||
                !_memory.GridPlacementPositionBuckets.IsCreated ||
                !_memory.GridPlacementMetadataBuckets.IsCreated ||
                !_memory.GridPlacementSpatialMetadata.IsCreated)
            {
                return true;
            }

            if (_memory.GridPlacementSpatialMetadata.Length == 0)
                return true;

            ScatterPlacement placement = candidate.Placement;
            WorldPrefabFamilyProfile family = placement.Family;
            float candidateSpacing = placement.EffectiveSpacing;
            float maxRelevantDistance = Mathf.Max(candidateSpacing, _maxRegisteredPlacementSpacingMeters) * 1.35f;
            int searchRadiusCells = Mathf.Max(1, Mathf.CeilToInt(maxRelevantDistance / Mathf.Max(1f, _runtimeStreamingState.CellSize)));
            _memory.CandidateAcceptanceResult[0] = 1;

            CanAcceptCandidateJob job = new CanAcceptCandidateJob
            {
                PositionBuckets = _memory.GridPlacementPositionBuckets,
                MetadataBuckets = _memory.GridPlacementMetadataBuckets,
                SpatialMetadata = _memory.GridPlacementSpatialMetadata.AsArray(),
                Result = _memory.CandidateAcceptanceResult,
                CandidatePosition = new float3(placement.Position.x, placement.Position.y, placement.Position.z),
                CandidateSpacing = candidateSpacing,
                MaxRelevantDistanceSq = maxRelevantDistance * maxRelevantDistance,
                CandidateCellX = placement.CellX,
                CandidateCellZ = placement.CellZ,
                SearchRadiusCells = searchRadiusCells,
                CandidateFamilyHash = family != null ? family.FamilyHash : 0,
                CandidateLayer = family != null ? (int)family.scatterLayer : 0,
                CandidateDomain = family != null ? (int)family.proceduralDomain : 0,
                CandidateClusterNoiseScale = ResolveEffectiveFloraClusterNoiseScale(placement.Rule),
                CandidateClusterNoiseThreshold = ResolveEffectiveFloraClusterNoiseThreshold(family, placement.Rule),
                CandidateRequiresClusterPatch = ShouldApplyFloraClusterPatch(family) ? (byte)1 : (byte)0,
                ChunkSize = math.max(1f, _runtimeStreamingState.ChunkSize),
                FloraChunkHardCap = FloraChunkInstanceHardCap,
                CandidateFloraBudgetClass = (byte)ResolveFloraBudgetClass(family)
            };

            job.Execute();
            return _memory.CandidateAcceptanceResult[0] != 0;
        }
    }
}
