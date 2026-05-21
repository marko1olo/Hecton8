using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Placement record container split from the scatter director host to keep coordination and DTO storage separate.
    /// </summary>
    public sealed partial class WorldProceduralScatterDirector
    {
        internal sealed class ScatterPlacement
        {
            public void Initialize(
                long key,
                int stableHash,
                WorldPrefabFamilyProfile family,
                WorldProceduralPlacementRule rule,
                WorldZoneAnchor zone,
                HectonBiomeFamilyProfile biomeFamily,
                HectonBiomeMatrixProfile biomeProfile,
                WorldProceduralPattern pattern,
                string biomeContextLabel,
                WorldStreamingLayer streamingLayer,
                WorldGenerativeGeologyProfile geologyProfile,
                WorldPrefabFamilyProfile.VariantEntry variant,
                bool supportsFinalVariant,
                string heatmapChannel,
                float heat,
                WorldProceduralFieldSampler.SeafloorSource fieldSource,
                float seafloorHeight,
                float depthMeters,
                float slopeDegrees,
                float curvature,
                float caveProximity,
                float ridgeSignal,
                float canyonSignal,
                float compositionPotential,
                int heightLayerIndex,
                int cellX,
                int cellZ,
                WorldChunkCoordinate chunkCoord,
                bool hasMacroZone,
                WorldMacroZoneCoordinate macroZoneCoord,
                Vector3 position,
                Quaternion rotation,
                float scale,
                bool hasRuntimeStateResolved)
            {
                Key = key;
                StableHash = stableHash;
                Family = family;
                Rule = rule;
                Zone = zone;
                BiomeFamily = biomeFamily;
                BiomeProfile = biomeProfile;
                Pattern = pattern;
                BiomeContextLabel = biomeContextLabel;
                CachedBiomeProfileLabel = biomeProfile != null ? biomeProfile.biomeName : "None";
                CachedBiomeFamilyLabel = biomeFamily != null ? biomeFamily.familyLabel : "None";
                CachedPatternLabel = GetPatternLabel(pattern);
                IsTectonicSpineBiome = IsTectonicSpineBiomeFamily(biomeFamily);
                StreamingLayer = streamingLayer;
                GeologyProfile = geologyProfile;
                Variant = variant;
                SupportsFinalVariant = supportsFinalVariant;
                EffectiveSpacing = family != null && rule != null
                    ? GetEffectiveSpacing(family, rule)
                    : 0f;
                bool faunaLayerAnchor = streamingLayer == WorldStreamingLayer.Fauna;
                bool spawnLayerAnchor = family != null && family.scatterLayer == WorldPrefabFamilyProfile.ScatterLayer.Spawn;
                bool creatureSpawnAnchor = family != null && family.proceduralDomain == WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn;
                IsLargeThreatZone = streamingLayer == WorldStreamingLayer.LargeThreats
                    || (family != null && family.ResolveContributesLargeThreatZone());
                IsFaunaAnchor = IsLargeThreatZone || faunaLayerAnchor || spawnLayerAnchor || creatureSpawnAnchor;
                float familyRadius = family != null ? family.clusterRadiusMeters : 0f;
                float faunaSpacing = EffectiveSpacing > 0f ? EffectiveSpacing : Mathf.Max(8f, familyRadius);
                FaunaAnchorRadius = Mathf.Max(12f, Mathf.Max(familyRadius, faunaSpacing));
                HeatmapChannel = heatmapChannel;
                Heat = heat;
                FieldSource = fieldSource;
                SeafloorHeight = seafloorHeight;
                DepthMeters = depthMeters;
                SlopeDegrees = slopeDegrees;
                Curvature = curvature;
                CaveProximity = caveProximity;
                RidgeSignal = ridgeSignal;
                CanyonSignal = canyonSignal;
                CompositionPotential = compositionPotential;
                HeightLayerIndex = heightLayerIndex;
                CellX = cellX;
                CellZ = cellZ;
                ChunkCoord = chunkCoord;
                HasMacroZone = hasMacroZone;
                MacroZoneCoord = macroZoneCoord;
                Position = position;
                Rotation = rotation;
                Scale = scale;
                HasRuntimeStateResolved = hasRuntimeStateResolved;
            }

            public void Reset()
            {
                Key = 0L;
                StableHash = 0;
                Family = null;
                Rule = null;
                Zone = null;
                BiomeFamily = null;
                BiomeProfile = null;
                Pattern = default;
                BiomeContextLabel = null;
                CachedBiomeProfileLabel = null;
                CachedBiomeFamilyLabel = null;
                CachedPatternLabel = null;
                IsTectonicSpineBiome = false;
                StreamingLayer = default;
                GeologyProfile = null;
                Variant = null;
                SupportsFinalVariant = false;
                EffectiveSpacing = 0f;
                IsFaunaAnchor = false;
                IsLargeThreatZone = false;
                FaunaAnchorRadius = 0f;
                HeatmapChannel = null;
                Heat = 0f;
                FieldSource = default;
                SeafloorHeight = 0f;
                DepthMeters = 0f;
                SlopeDegrees = 0f;
                Curvature = 0f;
                CaveProximity = 0f;
                RidgeSignal = 0f;
                CanyonSignal = 0f;
                CompositionPotential = 0f;
                HeightLayerIndex = 0;
                CellX = 0;
                CellZ = 0;
                ChunkCoord = default;
                HasMacroZone = false;
                MacroZoneCoord = default;
                Position = default;
                Rotation = Quaternion.identity;
                Scale = 0f;
                HasRuntimeStateResolved = false;
                CachedResolvedVariant = null;
                CachedFinalVariantActive = false;
                CachedSupportsFinalVariant = false;
                HasResolvedVariantState = false;
                CachedReconcilePlanVersion = 0;
                CachedReconcileInstance = null;
                CachedReconcileVariant = null;
                CachedReconcileFinalVariantActive = false;
                CachedReconcileRequiresSpawn = false;
                CachedReconcileShouldApplyGeneratedGeology = false;
                CachedReconcileSyncSignature = 0;
                CachedReconcileAllowInitialWarmupCreate = false;
                ReferenceCount = 0;
            }

            public void CacheResolvedVariantState(
                WorldPrefabFamilyProfile.VariantEntry variant,
                bool finalVariantActive,
                bool supportsFinalVariant)
            {
                CachedResolvedVariant = variant;
                CachedFinalVariantActive = finalVariantActive;
                CachedSupportsFinalVariant = supportsFinalVariant;
                HasResolvedVariantState = true;
            }

            public void InvalidateResolvedVariantState()
            {
                CachedResolvedVariant = null;
                CachedFinalVariantActive = false;
                CachedSupportsFinalVariant = false;
                HasResolvedVariantState = false;
            }

            public void CacheReconcilePlan(
                int planVersion,
                WorldProceduralProxyInstance instance,
                WorldPrefabFamilyProfile.VariantEntry variant,
                bool finalVariantActive,
                bool requiresSpawn,
                bool shouldApplyGeneratedGeology,
                int syncSignature,
                bool allowInitialWarmupCreate)
            {
                CachedReconcilePlanVersion = planVersion;
                CachedReconcileInstance = instance;
                CachedReconcileVariant = variant;
                CachedReconcileFinalVariantActive = finalVariantActive;
                CachedReconcileRequiresSpawn = requiresSpawn;
                CachedReconcileShouldApplyGeneratedGeology = shouldApplyGeneratedGeology;
                CachedReconcileSyncSignature = syncSignature;
                CachedReconcileAllowInitialWarmupCreate = allowInitialWarmupCreate;
            }

            public void ResolveDeferredRuntimeState(
                WorldPrefabFamilyProfile.VariantEntry variant,
                Vector3 position,
                Quaternion rotation,
                float scale,
                WorldChunkCoordinate chunkCoord,
                WorldMacroZoneCoordinate macroZoneCoord)
            {
                Variant = variant;
                Position = position;
                Rotation = rotation;
                Scale = scale;
                ChunkCoord = chunkCoord;
                MacroZoneCoord = macroZoneCoord;
                HasRuntimeStateResolved = true;
                InvalidateResolvedVariantState();
            }

            public bool TryGetCachedReconcilePlan(
                int planVersion,
                out WorldProceduralProxyInstance instance,
                out WorldPrefabFamilyProfile.VariantEntry variant,
                out bool finalVariantActive,
                out bool requiresSpawn,
                out bool shouldApplyGeneratedGeology,
                out int syncSignature,
                out bool allowInitialWarmupCreate)
            {
                if (CachedReconcilePlanVersion != planVersion)
                {
                    instance = null;
                    variant = null;
                    finalVariantActive = false;
                    requiresSpawn = false;
                    shouldApplyGeneratedGeology = false;
                    syncSignature = 0;
                    allowInitialWarmupCreate = false;
                    return false;
                }

                instance = CachedReconcileInstance;
                variant = CachedReconcileVariant;
                finalVariantActive = CachedReconcileFinalVariantActive;
                requiresSpawn = CachedReconcileRequiresSpawn;
                shouldApplyGeneratedGeology = CachedReconcileShouldApplyGeneratedGeology;
                syncSignature = CachedReconcileSyncSignature;
                allowInitialWarmupCreate = CachedReconcileAllowInitialWarmupCreate;
                return true;
            }

            public long Key;
            public int StableHash;
            public WorldPrefabFamilyProfile Family;
            public WorldProceduralPlacementRule Rule;
            public WorldZoneAnchor Zone;
            public HectonBiomeFamilyProfile BiomeFamily;
            public HectonBiomeMatrixProfile BiomeProfile;
            public WorldProceduralPattern Pattern;
            public string BiomeContextLabel;
            public string CachedBiomeProfileLabel;
            public string CachedBiomeFamilyLabel;
            public string CachedPatternLabel;
            public bool IsTectonicSpineBiome;
            public WorldStreamingLayer StreamingLayer;
            public WorldGenerativeGeologyProfile GeologyProfile;
            public WorldPrefabFamilyProfile.VariantEntry Variant;
            public bool SupportsFinalVariant;
            public float EffectiveSpacing;
            public bool IsFaunaAnchor;
            public bool IsLargeThreatZone;
            public float FaunaAnchorRadius;
            public string HeatmapChannel;
            public float Heat;
            public WorldProceduralFieldSampler.SeafloorSource FieldSource;
            public float SeafloorHeight;
            public float DepthMeters;
            public float SlopeDegrees;
            public float Curvature;
            public float CaveProximity;
            public float RidgeSignal;
            public float CanyonSignal;
            public float CompositionPotential;
            public int HeightLayerIndex;
            public int CellX;
            public int CellZ;
            public WorldChunkCoordinate ChunkCoord;
            public bool HasMacroZone;
            public WorldMacroZoneCoordinate MacroZoneCoord;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Scale;
            public bool HasRuntimeStateResolved;
            public WorldPrefabFamilyProfile.VariantEntry CachedResolvedVariant;
            public bool CachedFinalVariantActive;
            public bool CachedSupportsFinalVariant;
            public bool HasResolvedVariantState;
            public int CachedReconcilePlanVersion;
            public WorldProceduralProxyInstance CachedReconcileInstance;
            public WorldPrefabFamilyProfile.VariantEntry CachedReconcileVariant;
            public bool CachedReconcileFinalVariantActive;
            public bool CachedReconcileRequiresSpawn;
            public bool CachedReconcileShouldApplyGeneratedGeology;
            public int CachedReconcileSyncSignature;
            public bool CachedReconcileAllowInitialWarmupCreate;
            public int ReferenceCount;
            public bool IsPooled;

            public Vector3 ReadRuntimePosition()
            {
                return ToRuntimeScatterPosition(Position);
            }
        }
    }
}
