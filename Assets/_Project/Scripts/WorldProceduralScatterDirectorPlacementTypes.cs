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

            public long Key { get; private set; }
            public int StableHash { get; private set; }
            public WorldPrefabFamilyProfile Family { get; private set; }
            public WorldProceduralPlacementRule Rule { get; private set; }
            public WorldZoneAnchor Zone { get; private set; }
            public HectonBiomeFamilyProfile BiomeFamily { get; private set; }
            public HectonBiomeMatrixProfile BiomeProfile { get; private set; }
            public WorldProceduralPattern Pattern { get; private set; }
            public string BiomeContextLabel { get; private set; }
            public string CachedBiomeProfileLabel { get; private set; }
            public string CachedBiomeFamilyLabel { get; private set; }
            public string CachedPatternLabel { get; private set; }
            public WorldStreamingLayer StreamingLayer { get; private set; }
            public WorldGenerativeGeologyProfile GeologyProfile { get; private set; }
            public WorldPrefabFamilyProfile.VariantEntry Variant { get; private set; }
            public bool SupportsFinalVariant { get; private set; }
            public float EffectiveSpacing { get; private set; }
            public bool IsFaunaAnchor { get; private set; }
            public bool IsLargeThreatZone { get; private set; }
            public float FaunaAnchorRadius { get; private set; }
            public string HeatmapChannel { get; private set; }
            public float Heat { get; private set; }
            public WorldProceduralFieldSampler.SeafloorSource FieldSource { get; private set; }
            public float SeafloorHeight { get; private set; }
            public float DepthMeters { get; private set; }
            public float SlopeDegrees { get; private set; }
            public float Curvature { get; private set; }
            public float CaveProximity { get; private set; }
            public float RidgeSignal { get; private set; }
            public float CanyonSignal { get; private set; }
            public float CompositionPotential { get; private set; }
            public int HeightLayerIndex { get; private set; }
            public int CellX { get; private set; }
            public int CellZ { get; private set; }
            public WorldChunkCoordinate ChunkCoord { get; private set; }
            public bool HasMacroZone { get; private set; }
            public WorldMacroZoneCoordinate MacroZoneCoord { get; private set; }
            public Vector3 Position { get; private set; }
            public Vector3 RuntimePosition => ToRuntimeScatterPosition(Position);
            public Quaternion Rotation { get; private set; }
            public float Scale { get; private set; }
            public bool HasRuntimeStateResolved { get; private set; }
            public WorldPrefabFamilyProfile.VariantEntry CachedResolvedVariant { get; private set; }
            public bool CachedFinalVariantActive { get; private set; }
            public bool CachedSupportsFinalVariant { get; private set; }
            public bool HasResolvedVariantState { get; private set; }
            public int CachedReconcilePlanVersion { get; private set; }
            public WorldProceduralProxyInstance CachedReconcileInstance { get; private set; }
            public WorldPrefabFamilyProfile.VariantEntry CachedReconcileVariant { get; private set; }
            public bool CachedReconcileFinalVariantActive { get; private set; }
            public bool CachedReconcileRequiresSpawn { get; private set; }
            public bool CachedReconcileShouldApplyGeneratedGeology { get; private set; }
            public int CachedReconcileSyncSignature { get; private set; }
            public bool CachedReconcileAllowInitialWarmupCreate { get; private set; }
            public int ReferenceCount { get; set; }
            public bool IsPooled { get; set; }
        }
    }
}
