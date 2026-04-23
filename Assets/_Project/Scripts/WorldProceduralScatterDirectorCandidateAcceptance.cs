using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    public sealed partial class WorldProceduralScatterDirector
    {
        internal readonly struct ScatterPlacementSpatialMetadata
        {
            public ScatterPlacementSpatialMetadata(
                float3 position,
                float effectiveSpacing,
                int scatterLayer,
                int proceduralDomain)
            {
                Position = position;
                EffectiveSpacing = effectiveSpacing;
                ScatterLayer = scatterLayer;
                ProceduralDomain = proceduralDomain;
            }

            public readonly float3 Position;
            public readonly float EffectiveSpacing;
            public readonly int ScatterLayer;
            public readonly int ProceduralDomain;
        }

        [BurstCompile]
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
            public int CandidateLayer;
            public int CandidateDomain;

            public void Execute()
            {
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
                            if (math.distancesq(CandidatePosition, existingPosition) < MaxRelevantDistanceSq)
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
                            float minDistance = ResolveRequiredDistance(
                                CandidateSpacing,
                                CandidateLayer,
                                CandidateDomain,
                                in existing);
                            if (minDistance <= 0f)
                                continue;

                            if (math.distancesq(CandidatePosition, existing.Position) < minDistance * minDistance)
                            {
                                Result[0] = 0;
                                return;
                            }
                        }
                        while (MetadataBuckets.TryGetNextValue(out metadataIndex, ref metadataIterator));
                    }
                }
            }

            private static float ResolveRequiredDistance(
                float candidateSpacing,
                int candidateLayer,
                int candidateDomain,
                in ScatterPlacementSpatialMetadata existing)
            {
                float maxSpacing = math.max(candidateSpacing, existing.EffectiveSpacing);
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

                bool candidatePocket = IsPocket(candidateDomain);
                bool existingPocket = IsPocket(existing.ProceduralDomain);
                if (candidatePocket && existingPocket)
                    return math.max(10f, maxSpacing * 1.35f);

                bool candidateStructure = IsStructure(candidateLayer);
                bool existingStructure = IsStructure(existing.ScatterLayer);
                if (candidateStructure && existingStructure)
                    return math.max(14f, maxSpacing * 0.88f);

                bool candidateSpawn = candidateLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn;
                bool existingSpawn = existing.ScatterLayer == (int)WorldPrefabFamilyProfile.ScatterLayer.Spawn;
                if ((candidateSpawn && existingStructure) || (candidateStructure && existingSpawn))
                    return math.max(12f, math.max(candidateSpacing, existing.EffectiveSpacing) * 0.9f);

                return 0f;
            }

            private static bool IsPocket(int domain)
            {
                return domain == (int)WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket
                    || domain == (int)WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket
                    || domain == (int)WorldPrefabFamilyProfile.ProceduralDomain.SafePocket;
            }

            private static bool IsStructure(int layer)
            {
                return layer == (int)WorldPrefabFamilyProfile.ScatterLayer.Structure;
            }
        }

        private static int ComposeScatterGridNativeKey(int cellX, int cellZ)
        {
            unchecked
            {
                return (int)(((uint)cellX * 73856093u) ^ ((uint)cellZ * 19349663u));
            }
        }

        private bool CanAcceptCandidateManaged(in ScatterCandidate candidate)
        {
            if (_gridPlacements.Count == 0)
                return true;

            float candidateSpacing = candidate.Placement.EffectiveSpacing;
            float maxRelevantDistance = Mathf.Max(candidateSpacing, _maxRegisteredPlacementSpacingMeters) * 1.35f;
            int searchRadiusCells = Mathf.Max(1, Mathf.CeilToInt(maxRelevantDistance / Mathf.Max(1f, _runtimeStreamingState.CellSize)));
            int cellX = candidate.Placement.CellX;
            int cellZ = candidate.Placement.CellZ;

            for (int ox = -searchRadiusCells; ox <= searchRadiusCells; ox++)
            {
                for (int oz = -searchRadiusCells; oz <= searchRadiusCells; oz++)
                {
                    long cellKey = ComposeScatterGridKey(cellX + ox, cellZ + oz);
                    if (!_gridPlacements.TryGetValue(cellKey, out List<ScatterPlacement> localPlacements))
                        continue;

                    for (int i = 0; i < localPlacements.Count; i++)
                    {
                        ScatterPlacement existing = localPlacements[i];
                        float minDistance = ResolveRequiredDistance(candidate.Placement, existing);
                        if (minDistance <= 0f)
                            continue;

                        if ((candidate.Placement.Position - existing.Position).sqrMagnitude < minDistance * minDistance)
                            return false;
                    }
                }
            }

            return true;
        }

        private bool CanAcceptCandidateNative(in ScatterCandidate candidate)
        {
            EnsureWorkingMemory();
            if (_memory == null ||
                _memory.GridPlacementNativeOverflowed ||
                !_memory.GridPlacementPositionBuckets.IsCreated ||
                !_memory.GridPlacementMetadataBuckets.IsCreated ||
                !_memory.GridPlacementSpatialMetadata.IsCreated ||
                _memory.GridPlacementSpatialMetadata.Length == 0)
            {
                return CanAcceptCandidateManaged(candidate);
            }

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
                CandidateLayer = family != null ? (int)family.scatterLayer : 0,
                CandidateDomain = family != null ? (int)family.proceduralDomain : 0
            };

            job.Run();
            return _memory.CandidateAcceptanceResult[0] != 0;
        }
    }
}
