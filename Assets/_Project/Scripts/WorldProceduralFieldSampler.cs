using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4037)]
    public sealed class WorldProceduralFieldSampler : MonoBehaviour
    {
        public enum SeafloorSource
        {
            None,
            MapMagicHeight,
            SceneRaycast,
            FallbackSynthetic
        }

        public struct FieldSample
        {
            public Vector3 position;
            public float seafloorHeight;
            public float depthMeters;
            public float slopeDegrees;
            public int biomeIndex;
            public HectonBiomeMatrixProfile biomeProfile;
            public HectonBiomeFamilyProfile biomeFamily;
            public WorldZoneAnchor zone;
            public float zoneWeight;
            public WorldZoneAnchor.ZoneKind resolvedZoneKind;
            public WorldProceduralPattern resolvedPattern;
            public SeafloorSource seafloorSource;
            public bool isValid;
        }

        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;

        [Header("Fallback Biome Families")]
        [SerializeField] private HectonBiomeFamilyProfile littoralKarstFamily;
        [SerializeField] private HectonBiomeFamilyProfile fossilReefFamily;
        [SerializeField] private HectonBiomeFamilyProfile sedimentDriftFamily;
        [SerializeField] private HectonBiomeFamilyProfile abyssalSiltFamily;
        [SerializeField] private HectonBiomeFamilyProfile graniteEscarpmentFamily;
        [SerializeField] private HectonBiomeFamilyProfile tectonicSpineFamily;
        [SerializeField] private HectonBiomeFamilyProfile riftSpineFamily;
        [SerializeField] private HectonBiomeFamilyProfile riftVoidFamily;
        [SerializeField] private HectonBiomeFamilyProfile volcanicGlassFamily;
        [SerializeField] private HectonBiomeFamilyProfile volcanicHadalFamily;
        [SerializeField] private HectonBiomeFamilyProfile metallicHadalFamily;
        [SerializeField] private HectonBiomeFamilyProfile chemosyntheticBrineFamily;
        [SerializeField] private HectonBiomeFamilyProfile crystalGrowthFamily;

        [Header("Sampling")]
        [SerializeField] private float slopeProbeMeters = 4f;
        [SerializeField] private float fieldNoiseScale = 0.0035f;
        [SerializeField] private float detailNoiseScale = 0.0125f;

        [Header("Preview Overrides")]
        [SerializeField] private bool forcePatternPreviewOverride;
        [SerializeField] private WorldProceduralPattern previewPatternOverride = WorldProceduralPattern.SedimentResources;
        [SerializeField] private bool limitPatternOverrideToFallback = true;
        [SerializeField] private bool forceMatrixBiomePreviewOverride;
        [SerializeField] private HectonBiomeMatrixProfile previewMatrixBiomeOverride;
        [SerializeField] private bool limitMatrixBiomeOverrideToFallback = true;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugBridgeReady;
        [SerializeField] private bool _debugZoneDirectorReady;
        [SerializeField] private bool _debugBiomeDirectorReady;
        [SerializeField] private string _debugLastZone = "None";
        [SerializeField] private string _debugLastBiomeProfile = "None";
        [SerializeField] private string _debugLastBiomeFamily = "None";
        [SerializeField] private string _debugLastPattern = "None";
        [SerializeField] private string _debugPatternOverride = "None";
        [SerializeField] private string _debugPreviewBiomeOverride = "None";
        [SerializeField] private string _debugPreviewMatrixOverride = "None";
        [SerializeField] private string _debugPreviewZoneOverride = "None";
        [SerializeField] private string _debugLastHeatmap = "None";
        [SerializeField] private string _debugLastHeightSource = "None";
        [SerializeField] private float _debugLastHeatmapValue;
        [SerializeField] private float _debugLastDepth;
        [SerializeField] private float _debugLastSlope;

        private readonly List<WorldZoneAnchor> _anchors = new List<WorldZoneAnchor>(32);

        public bool TrySampleSeafloor(Vector3 position, out FieldSample sample)
        {
            sample = default;
            ResolveReferences();

            if (!TryResolveSeafloorHeight(position, out float seafloorHeight, out SeafloorSource seafloorSource))
            {
                UpdateDiagnostics(default, "None", 0f);
                return false;
            }

            int biomeIndex = 0;
            if (mapMagicBridge != null)
                mapMagicBridge.TryGetBiomeIndex(position.x, position.z, out biomeIndex);

            float waterSurface = mapMagicBridge != null
                ? mapMagicBridge.WaterSurfaceLevel
                : Mathf.Max(position.y + 120f, seafloorHeight + 50f);
            float depthMeters = Mathf.Max(0f, waterSurface - seafloorHeight);
            float slopeDegrees = EvaluateSlope(position.x, position.z, seafloorHeight);
            WorldZoneAnchor zone = ResolveZone(new Vector3(position.x, seafloorHeight, position.z), out float zoneWeight);
            HectonBiomeMatrixProfile biomeProfile = biomeMatrixDirector != null ? biomeMatrixDirector.CurrentProfile : null;
            HectonBiomeFamilyProfile biomeFamily = zone != null
                ? zone.DominantBiomeFamily
                : biomeMatrixDirector != null
                    ? biomeMatrixDirector.CurrentFamilyProfile
                    : null;
            WorldZoneAnchor.ZoneKind resolvedZoneKind = zone != null
                ? zone.Kind
                : ResolveFallbackZoneKind(position, depthMeters, slopeDegrees);
            if (biomeFamily == null)
                biomeFamily = ResolveFallbackBiomeFamily(position, depthMeters, slopeDegrees, resolvedZoneKind);
            WorldProceduralPattern resolvedPattern;
            if (!TryApplyPreviewPatternContextOverride(
                    seafloorSource,
                    depthMeters,
                    slopeDegrees,
                    ref biomeFamily,
                    ref resolvedZoneKind,
                    out resolvedPattern))
            {
                resolvedPattern = ResolvePattern(position, depthMeters, slopeDegrees, biomeFamily, zone, resolvedZoneKind);
                resolvedPattern = ResolvePreviewPatternOverride(resolvedPattern, seafloorSource);
            }

            HectonBiomeMatrixProfile previewMatrixProfile = ResolvePreviewMatrixBiomeOverride(seafloorSource);
            if (previewMatrixProfile != null)
            {
                biomeProfile = previewMatrixProfile;
                if (previewMatrixProfile.familyProfile != null)
                    biomeFamily = previewMatrixProfile.familyProfile;
            }
            else
            {
                biomeProfile = ResolveEffectiveBiomeProfile(
                    biomeProfile,
                    biomeFamily,
                    seafloorSource,
                    resolvedPattern);
            }

            sample = new FieldSample
            {
                position = new Vector3(position.x, seafloorHeight, position.z),
                seafloorHeight = seafloorHeight,
                depthMeters = depthMeters,
                slopeDegrees = slopeDegrees,
                biomeIndex = biomeIndex,
                biomeProfile = biomeProfile,
                biomeFamily = biomeFamily,
                zone = zone,
                zoneWeight = zoneWeight,
                resolvedZoneKind = resolvedZoneKind,
                resolvedPattern = resolvedPattern,
                seafloorSource = seafloorSource,
                isValid = true
            };

            UpdateDiagnostics(sample, "sample", 0f);
            return true;
        }

        public float EvaluateHeatmap(
            string heatmapChannel,
            in FieldSample sample,
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule)
        {
            string channel = string.IsNullOrWhiteSpace(heatmapChannel)
                ? family != null && !string.IsNullOrWhiteSpace(family.heatmapChannel) ? family.heatmapChannel : "generic"
                : heatmapChannel;

            float depth01 = Mathf.Clamp01(sample.depthMeters / 800f);
            float shallow01 = 1f - Mathf.Clamp01(sample.depthMeters / 220f);
            float midDepth01 = 1f - Mathf.Clamp01(Mathf.Abs(sample.depthMeters - 260f) / 320f);
            float deep01 = Mathf.Clamp01((sample.depthMeters - 180f) / 900f);
            float abyss01 = Mathf.Clamp01((sample.depthMeters - 900f) / 1800f);
            float flat01 = 1f - Mathf.Clamp01(sample.slopeDegrees / 28f);
            float steep01 = Mathf.Clamp01((sample.slopeDegrees - 8f) / 40f);
            float terrainNoise = EvaluateNoise01(sample.position.x, sample.position.z, fieldNoiseScale);
            float detailNoise = EvaluateNoise01(sample.position.x + 91.7f, sample.position.z - 33.4f, detailNoiseScale);
            float ruggedBias = EvaluateRuggedBiomeBias(sample.zone);
            float fertileBias = EvaluateFertileBiomeBias(sample.zone, sample.resolvedZoneKind, sample.biomeFamily);
            float hazardBias = EvaluateHazardBias(sample.zone, sample.resolvedZoneKind);
            float serviceBias = EvaluateServiceBias(sample.zone, sample.resolvedZoneKind);
            float resourceBias = EvaluateResourceBias(sample.zone, sample.resolvedZoneKind);
            float shelterBias = EvaluateShelterBias(sample.zone, sample.resolvedZoneKind);
            float landmarkBias = EvaluateLandmarkBias(sample.zone, sample.resolvedZoneKind);
            float biomeMatrixBonus = EvaluateBiomeMatrixChannelBonus(channel, sample.biomeProfile);

            float value = channel switch
            {
                "rock_density" => 0.24f + steep01 * 0.34f + deep01 * 0.16f + ruggedBias * 0.16f + terrainNoise * 0.16f,
                "kelp_density" => shallow01 * 0.44f + flat01 * 0.18f + fertileBias * 0.2f + terrainNoise * 0.18f,
                "flora_density" => shallow01 * 0.34f + flat01 * 0.12f + fertileBias * 0.3f + detailNoise * 0.24f,
                "coral_density" => shallow01 * 0.24f + midDepth01 * 0.24f + flat01 * 0.14f + fertileBias * 0.22f + terrainNoise * 0.16f,
                "bio_density" => fertileBias * 0.36f + shallow01 * 0.16f + shelterBias * 0.16f + detailNoise * 0.2f + (1f - hazardBias) * 0.12f,
                "debris_density" => serviceBias * 0.34f + midDepth01 * 0.16f + terrainNoise * 0.22f + detailNoise * 0.14f + ruggedBias * 0.14f,
                "ruin_density" => serviceBias * 0.38f + deep01 * 0.12f + terrainNoise * 0.2f + landmarkBias * 0.18f + flat01 * 0.12f,
                "cave_density" => steep01 * 0.34f + ruggedBias * 0.22f + deep01 * 0.18f + terrainNoise * 0.18f + hazardBias * 0.08f,
                "landmark_strength" => steep01 * 0.24f + landmarkBias * 0.34f + abyss01 * 0.1f + terrainNoise * 0.18f + ruggedBias * 0.14f,
                "fauna_density" => fertileBias * 0.34f + shallow01 * 0.16f + shelterBias * 0.22f + detailNoise * 0.16f + (1f - steep01) * 0.12f,
                "hazard_density" => hazardBias * 0.42f + deep01 * 0.12f + steep01 * 0.14f + terrainNoise * 0.18f + landmarkBias * 0.14f,
                "resource_density" => resourceBias * 0.34f + deep01 * 0.08f + terrainNoise * 0.2f + ruggedBias * 0.18f + detailNoise * 0.2f,
                "shelter_density" => shelterBias * 0.34f + flat01 * 0.26f + shallow01 * 0.08f + fertileBias * 0.12f + detailNoise * 0.2f,
                "service_density" => serviceBias * 0.44f + terrainNoise * 0.2f + ruggedBias * 0.1f + flat01 * 0.1f + landmarkBias * 0.16f,
                _ => terrainNoise * 0.55f + detailNoise * 0.45f
            };
            value = Mathf.Clamp01(value + biomeMatrixBonus);

            float patternShapedValue = EvaluatePatternShapedHeat(
                channel,
                sample,
                shallow01,
                midDepth01,
                deep01,
                abyss01,
                flat01,
                steep01,
                terrainNoise,
                detailNoise,
                ruggedBias,
                fertileBias,
                hazardBias,
                serviceBias,
                resourceBias,
                shelterBias,
                landmarkBias);
            patternShapedValue = Mathf.Clamp01(patternShapedValue + biomeMatrixBonus * 0.92f);
            value = Mathf.Lerp(value, patternShapedValue, ResolvePatternFieldBlend(sample.seafloorSource, sample.zone));

            if (family != null)
            {
                value *= family.placementMode switch
                {
                    WorldPrefabFamilyProfile.PlacementMode.Landmark => Mathf.Lerp(0.8f, 1.2f, landmarkBias),
                    WorldPrefabFamilyProfile.PlacementMode.Cluster => 1.05f,
                    WorldPrefabFamilyProfile.PlacementMode.Patch => 1.08f,
                    WorldPrefabFamilyProfile.PlacementMode.SpawnAnchor => Mathf.Lerp(0.85f, 1.15f, hazardBias),
                    _ => 1f
                };
            }

            if (rule != null && !string.IsNullOrWhiteSpace(rule.gameplayIntent))
                value *= 0.95f + Mathf.Clamp01(rule.densityScale * 0.12f);

            value = Mathf.Clamp01(value);
            UpdateDiagnostics(sample, channel, value);
            return value;
        }

        private float EvaluatePatternShapedHeat(
            string channel,
            in FieldSample sample,
            float shallow01,
            float midDepth01,
            float deep01,
            float abyss01,
            float flat01,
            float steep01,
            float terrainNoise,
            float detailNoise,
            float ruggedBias,
            float fertileBias,
            float hazardBias,
            float serviceBias,
            float resourceBias,
            float shelterBias,
            float landmarkBias)
        {
            float sedimentNoise = EvaluateNoise01(sample.position.x - 218.6f, sample.position.z + 57.4f, fieldNoiseScale * 0.74f);
            float fertileNoise = EvaluateNoise01(sample.position.x + 127.8f, sample.position.z - 146.2f, detailNoiseScale * 0.78f);
            float reefNoise = EvaluateNoise01(sample.position.x + 314.4f, sample.position.z + 88.5f, detailNoiseScale * 0.58f);
            float industrialNoise = EvaluateNoise01(sample.position.x - 401.1f, sample.position.z - 203.6f, fieldNoiseScale * 0.82f);
            float hazardNoise = EvaluateNoise01(sample.position.x + 261.7f, sample.position.z - 318.3f, detailNoiseScale * 0.94f);
            float landmarkNoise = EvaluateNoise01(sample.position.x - 83.2f, sample.position.z + 367.9f, fieldNoiseScale * 0.62f);
            float basinNoise = EvaluateNoise01(sample.position.x + 452.5f, sample.position.z + 121.3f, detailNoiseScale * 0.66f);

            float sedimentField = Mathf.Clamp01(
                resourceBias * 0.32f +
                shelterBias * 0.18f +
                flat01 * 0.16f +
                terrainNoise * 0.14f +
                sedimentNoise * 0.20f);
            float fertileField = Mathf.Clamp01(
                fertileBias * 0.34f +
                shallow01 * 0.16f +
                detailNoise * 0.12f +
                fertileNoise * 0.22f +
                shelterBias * 0.08f +
                (1f - hazardBias) * 0.08f);
            float reefField = Mathf.Clamp01(
                fertileBias * 0.24f +
                landmarkBias * 0.14f +
                shallow01 * 0.10f +
                reefNoise * 0.24f +
                flat01 * 0.08f +
                detailNoise * 0.12f +
                midDepth01 * 0.08f);
            float industrialField = Mathf.Clamp01(
                serviceBias * 0.34f +
                industrialNoise * 0.28f +
                terrainNoise * 0.10f +
                ruggedBias * 0.08f +
                deep01 * 0.08f +
                landmarkBias * 0.12f);
            float hazardField = Mathf.Clamp01(
                hazardBias * 0.38f +
                steep01 * 0.12f +
                deep01 * 0.12f +
                hazardNoise * 0.24f +
                ruggedBias * 0.14f);
            float landmarkField = Mathf.Clamp01(
                landmarkBias * 0.34f +
                steep01 * 0.16f +
                landmarkNoise * 0.26f +
                ruggedBias * 0.10f +
                deep01 * 0.08f +
                reefField * 0.06f);
            float shelterField = Mathf.Clamp01(
                shelterBias * 0.34f +
                flat01 * 0.18f +
                fertileField * 0.14f +
                basinNoise * 0.18f +
                detailNoise * 0.16f);
            float abyssField = Mathf.Clamp01(
                abyss01 * 0.44f +
                hazardField * 0.16f +
                ruggedBias * 0.12f +
                terrainNoise * 0.12f +
                industrialNoise * 0.08f +
                (1f - fertileField) * 0.08f);

            float shapedValue = sample.resolvedPattern switch
            {
                WorldProceduralPattern.FertileShallows => channel switch
                {
                    "rock_density" => 0.18f + sedimentField * 0.22f + ruggedBias * 0.12f + flat01 * 0.08f,
                    "kelp_density" => fertileField * 0.92f,
                    "flora_density" => fertileField * 0.84f,
                    "coral_density" => reefField * 0.90f,
                    "bio_density" => fertileField * 0.62f + shelterField * 0.24f,
                    "debris_density" => industrialField * 0.26f,
                    "ruin_density" => industrialField * 0.22f + landmarkField * 0.16f,
                    "cave_density" => landmarkField * 0.28f + hazardField * 0.16f,
                    "landmark_strength" => landmarkField * 0.48f + reefField * 0.12f,
                    "fauna_density" => fertileField * 0.56f + shelterField * 0.30f,
                    "hazard_density" => hazardField * 0.26f,
                    "resource_density" => sedimentField * 0.40f + fertileField * 0.18f,
                    "shelter_density" => shelterField * 0.78f,
                    "service_density" => industrialField * 0.22f,
                    _ => fertileField * 0.58f + sedimentField * 0.14f
                },
                WorldProceduralPattern.ReefNavigation => channel switch
                {
                    "rock_density" => 0.20f + sedimentField * 0.18f + ruggedBias * 0.12f,
                    "kelp_density" => fertileField * 0.72f + reefField * 0.14f,
                    "flora_density" => fertileField * 0.70f + reefField * 0.12f,
                    "coral_density" => reefField * 0.94f,
                    "bio_density" => fertileField * 0.44f + shelterField * 0.22f,
                    "debris_density" => industrialField * 0.24f,
                    "ruin_density" => industrialField * 0.20f + landmarkField * 0.18f,
                    "cave_density" => landmarkField * 0.38f + hazardField * 0.18f,
                    "landmark_strength" => landmarkField * 0.68f + reefField * 0.16f,
                    "fauna_density" => fertileField * 0.42f + shelterField * 0.18f,
                    "hazard_density" => hazardField * 0.28f,
                    "resource_density" => sedimentField * 0.32f + landmarkField * 0.12f,
                    "shelter_density" => shelterField * 0.54f + reefField * 0.12f,
                    "service_density" => industrialField * 0.22f,
                    _ => reefField * 0.56f + landmarkField * 0.18f
                },
                WorldProceduralPattern.SedimentResources => channel switch
                {
                    "rock_density" => 0.18f + sedimentField * 0.86f + ruggedBias * 0.12f,
                    "kelp_density" => fertileField * 0.24f + shelterField * 0.10f,
                    "flora_density" => fertileField * 0.14f + shelterField * 0.08f,
                    "coral_density" => reefField * 0.14f + fertileField * 0.06f,
                    "bio_density" => shelterField * 0.52f + fertileField * 0.12f,
                    "debris_density" => industrialField * 0.42f + hazardField * 0.08f,
                    "ruin_density" => industrialField * 0.44f + landmarkField * 0.22f + sedimentField * 0.08f,
                    "cave_density" => hazardField * 0.30f + landmarkField * 0.30f + ruggedBias * 0.18f + sedimentField * 0.06f,
                    "landmark_strength" => landmarkField * 0.58f + sedimentField * 0.14f + ruggedBias * 0.08f,
                    "fauna_density" => shelterField * 0.42f + fertileField * 0.14f,
                    "hazard_density" => hazardField * 0.34f,
                    "resource_density" => sedimentField * 0.92f,
                    "shelter_density" => shelterField * 0.88f,
                    "service_density" => industrialField * 0.48f + sedimentField * 0.08f + landmarkField * 0.06f,
                    _ => sedimentField * 0.62f + shelterField * 0.18f
                },
                WorldProceduralPattern.IndustrialService => channel switch
                {
                    "rock_density" => 0.18f + sedimentField * 0.34f + ruggedBias * 0.10f,
                    "kelp_density" => fertileField * 0.18f,
                    "flora_density" => fertileField * 0.16f,
                    "coral_density" => reefField * 0.14f,
                    "bio_density" => shelterField * 0.24f,
                    "debris_density" => industrialField * 0.90f,
                    "ruin_density" => industrialField * 0.76f + landmarkField * 0.12f,
                    "cave_density" => hazardField * 0.22f + landmarkField * 0.18f + industrialField * 0.12f,
                    "landmark_strength" => landmarkField * 0.44f + industrialField * 0.22f,
                    "fauna_density" => hazardField * 0.16f + shelterField * 0.14f,
                    "hazard_density" => hazardField * 0.46f + industrialField * 0.12f,
                    "resource_density" => sedimentField * 0.26f + industrialField * 0.12f,
                    "shelter_density" => shelterField * 0.22f,
                    "service_density" => industrialField * 0.96f,
                    _ => industrialField * 0.64f + landmarkField * 0.14f
                },
                WorldProceduralPattern.BrineToxic => channel switch
                {
                    "rock_density" => 0.16f + sedimentField * 0.28f + industrialField * 0.18f + ruggedBias * 0.08f,
                    "kelp_density" => fertileField * 0.08f,
                    "flora_density" => fertileField * 0.10f,
                    "coral_density" => reefField * 0.08f,
                    "bio_density" => fertileField * 0.16f + shelterField * 0.12f + hazardField * 0.08f,
                    "debris_density" => industrialField * 0.82f,
                    "ruin_density" => industrialField * 0.58f + landmarkField * 0.14f,
                    "cave_density" => hazardField * 0.24f + landmarkField * 0.18f + industrialField * 0.12f,
                    "landmark_strength" => landmarkField * 0.36f + industrialField * 0.18f,
                    "fauna_density" => fertileField * 0.12f + hazardField * 0.14f,
                    "hazard_density" => hazardField * 0.54f + industrialField * 0.12f,
                    "resource_density" => sedimentField * 0.24f + industrialField * 0.14f,
                    "shelter_density" => shelterField * 0.18f,
                    "service_density" => industrialField * 0.82f,
                    _ => industrialField * 0.62f + hazardField * 0.10f
                },
                WorldProceduralPattern.VolcanicPressure => channel switch
                {
                    "rock_density" => 0.20f + sedimentField * 0.46f + ruggedBias * 0.18f + hazardField * 0.10f,
                    "kelp_density" => fertileField * 0.06f,
                    "flora_density" => fertileField * 0.08f,
                    "coral_density" => reefField * 0.06f,
                    "bio_density" => fertileField * 0.10f + hazardField * 0.10f + abyssField * 0.06f,
                    "debris_density" => industrialField * 0.34f + hazardField * 0.16f,
                    "ruin_density" => industrialField * 0.42f + landmarkField * 0.18f + hazardField * 0.12f,
                    "cave_density" => landmarkField * 0.48f + hazardField * 0.28f + ruggedBias * 0.10f,
                    "landmark_strength" => landmarkField * 0.86f + hazardField * 0.10f,
                    "fauna_density" => hazardField * 0.18f + abyssField * 0.10f,
                    "hazard_density" => hazardField * 0.76f,
                    "resource_density" => sedimentField * 0.22f + hazardField * 0.10f,
                    "shelter_density" => shelterField * 0.14f,
                    "service_density" => industrialField * 0.42f + hazardField * 0.10f,
                    _ => landmarkField * 0.52f + hazardField * 0.16f + sedimentField * 0.12f
                },
                WorldProceduralPattern.RiftHazard => channel switch
                {
                    "rock_density" => 0.18f + hazardField * 0.36f + ruggedBias * 0.18f + sedimentField * 0.16f,
                    "kelp_density" => fertileField * 0.10f,
                    "flora_density" => fertileField * 0.12f,
                    "coral_density" => reefField * 0.10f,
                    "bio_density" => hazardField * 0.24f + abyssField * 0.10f,
                    "debris_density" => industrialField * 0.36f + hazardField * 0.12f,
                    "ruin_density" => industrialField * 0.42f + hazardField * 0.18f + landmarkField * 0.10f,
                    "cave_density" => hazardField * 0.82f,
                    "landmark_strength" => landmarkField * 0.52f + hazardField * 0.16f,
                    "fauna_density" => hazardField * 0.48f + abyssField * 0.18f,
                    "hazard_density" => hazardField * 0.98f,
                    "resource_density" => sedimentField * 0.24f + hazardField * 0.10f,
                    "shelter_density" => shelterField * 0.18f,
                    "service_density" => industrialField * 0.34f,
                    _ => hazardField * 0.64f + industrialField * 0.14f
                },
                WorldProceduralPattern.AbyssSparse => channel switch
                {
                    "rock_density" => 0.20f + abyssField * 0.44f + ruggedBias * 0.16f + sedimentField * 0.18f,
                    "kelp_density" => fertileField * 0.06f,
                    "flora_density" => fertileField * 0.08f,
                    "coral_density" => reefField * 0.08f,
                    "bio_density" => abyssField * 0.18f + shelterField * 0.10f,
                    "debris_density" => industrialField * 0.18f + abyssField * 0.08f,
                    "ruin_density" => industrialField * 0.22f + landmarkField * 0.18f,
                    "cave_density" => hazardField * 0.22f + landmarkField * 0.22f,
                    "landmark_strength" => landmarkField * 0.48f + abyssField * 0.12f,
                    "fauna_density" => abyssField * 0.16f,
                    "hazard_density" => hazardField * 0.24f + abyssField * 0.12f,
                    "resource_density" => sedimentField * 0.18f + abyssField * 0.08f,
                    "shelter_density" => shelterField * 0.14f,
                    "service_density" => industrialField * 0.16f,
                    _ => abyssField * 0.52f + landmarkField * 0.12f
                },
                WorldProceduralPattern.LandmarkCorridor => channel switch
                {
                    "rock_density" => 0.22f + sedimentField * 0.26f + ruggedBias * 0.18f,
                    "kelp_density" => fertileField * 0.24f,
                    "flora_density" => fertileField * 0.22f + landmarkField * 0.08f,
                    "coral_density" => reefField * 0.28f,
                    "bio_density" => shelterField * 0.22f + fertileField * 0.10f,
                    "debris_density" => industrialField * 0.26f,
                    "ruin_density" => industrialField * 0.34f + landmarkField * 0.24f,
                    "cave_density" => landmarkField * 0.84f,
                    "landmark_strength" => landmarkField * 0.98f,
                    "fauna_density" => shelterField * 0.18f + hazardField * 0.10f,
                    "hazard_density" => hazardField * 0.34f + landmarkField * 0.08f,
                    "resource_density" => sedimentField * 0.22f + landmarkField * 0.10f,
                    "shelter_density" => shelterField * 0.28f,
                    "service_density" => industrialField * 0.26f + landmarkField * 0.10f,
                    _ => landmarkField * 0.74f + sedimentField * 0.10f
                },
                _ => terrainNoise * 0.55f + detailNoise * 0.45f
            };

            return Mathf.Clamp01(shapedValue);
        }

        private static float ResolvePatternFieldBlend(SeafloorSource source, WorldZoneAnchor zone)
        {
            return source switch
            {
                SeafloorSource.FallbackSynthetic => zone == null ? 0.78f : 0.66f,
                SeafloorSource.SceneRaycast => zone == null ? 0.42f : 0.28f,
                SeafloorSource.MapMagicHeight => zone == null ? 0.34f : 0.18f,
                _ => 0.2f
            };
        }

        private bool TryResolveSeafloorHeight(Vector3 position, out float seafloorHeight, out SeafloorSource seafloorSource)
        {
            seafloorHeight = 0f;
            seafloorSource = SeafloorSource.None;

            if (mapMagicBridge != null && mapMagicBridge.TryGetHeight(position.x, position.z, out seafloorHeight))
            {
                seafloorSource = SeafloorSource.MapMagicHeight;
                return true;
            }

            float waterSurface = mapMagicBridge != null ? mapMagicBridge.WaterSurfaceLevel : Mathf.Max(position.y + 500f, 1000f);
            float rayOriginY = Mathf.Max(waterSurface + 1000f, position.y + 1000f);
            Vector3 origin = new Vector3(position.x, rayOriginY, position.z);
            if (UnityEngine.Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 40000f, ~0, QueryTriggerInteraction.Ignore))
            {
                seafloorHeight = hit.point.y;
                seafloorSource = SeafloorSource.SceneRaycast;
                return true;
            }

            float fallbackSurface = mapMagicBridge != null ? mapMagicBridge.WaterSurfaceLevel : Mathf.Max(position.y + 120f, 120f);
            seafloorHeight = fallbackSurface - EstimateFallbackDepth(position.x, position.z);
            seafloorSource = SeafloorSource.FallbackSynthetic;
            return true;
        }

        private float EstimateFallbackDepth(float x, float z)
        {
            float broad = EvaluateNoise01(x + 311.1f, z - 177.4f, fieldNoiseScale * 0.55f);
            float detail = EvaluateNoise01(x - 91.6f, z + 441.2f, detailNoiseScale * 0.7f);
            float depth = Mathf.Lerp(70f, 240f, (broad * 0.7f) + (detail * 0.3f));
            return Mathf.Clamp(depth, 40f, 320f);
        }

        private float EvaluateSlope(float x, float z, float centerHeight)
        {
            float probe = Mathf.Max(1f, slopeProbeMeters);
            float left = centerHeight;
            float right = centerHeight;
            float forward = centerHeight;
            float back = centerHeight;

            TryResolveSeafloorHeight(new Vector3(x - probe, centerHeight, z), out left, out _);
            TryResolveSeafloorHeight(new Vector3(x + probe, centerHeight, z), out right, out _);
            TryResolveSeafloorHeight(new Vector3(x, centerHeight, z + probe), out forward, out _);
            TryResolveSeafloorHeight(new Vector3(x, centerHeight, z - probe), out back, out _);

            float dx = (right - left) / (probe * 2f);
            float dz = (forward - back) / (probe * 2f);
            float gradient = Mathf.Sqrt(dx * dx + dz * dz);
            return Mathf.Atan(gradient) * Mathf.Rad2Deg;
        }

        private WorldZoneAnchor ResolveZone(Vector3 position, out float zoneWeight)
        {
            RefreshAnchorsIfNeeded();

            WorldZoneAnchor best = null;
            float bestWeight = 0f;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _anchors.Count; i++)
            {
                WorldZoneAnchor anchor = _anchors[i];
                if (anchor == null)
                    continue;

                float weight = anchor.EvaluateActivationWeight(position);
                float distance = anchor.GetFlatDistance(position);
                if (weight <= 0.001f)
                    continue;

                if (best == null ||
                    weight > bestWeight ||
                    (Mathf.Approximately(weight, bestWeight) && distance < bestDistance))
                {
                    best = anchor;
                    bestWeight = weight;
                    bestDistance = distance;
                }
            }

            if (best == null)
                best = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;

            zoneWeight = best != null ? Mathf.Max(bestWeight, best.EvaluateActivationWeight(position)) : 0f;
            return best;
        }

        private void RefreshAnchorsIfNeeded()
        {
            if (_anchors.Count > 0)
                return;

            _anchors.Clear();
            WorldZoneAnchor[] anchors = Resources.FindObjectsOfTypeAll<WorldZoneAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                WorldZoneAnchor anchor = anchors[i];
                if (anchor == null || anchor.gameObject == null || !anchor.gameObject.scene.IsValid())
                    continue;

                _anchors.Add(anchor);
            }
        }

        private float EvaluateNoise01(float x, float z, float scale)
        {
            float s = Mathf.Max(0.0001f, scale);
            float a = Mathf.PerlinNoise(x * s, z * s);
            float b = Mathf.PerlinNoise((x + 127.37f) * (s * 2.2f), (z - 93.11f) * (s * 2.2f));
            return Mathf.Clamp01((a * 0.65f) + (b * 0.35f));
        }

        private HectonBiomeFamilyProfile ResolveFallbackBiomeFamily(
            Vector3 position,
            float depthMeters,
            float slopeDegrees,
            WorldZoneAnchor.ZoneKind zoneKindHint)
        {
            float ruggedNoise = EvaluateNoise01(position.x + 173.4f, position.z - 117.2f, fieldNoiseScale * 0.9f);
            float fertileNoise = EvaluateNoise01(position.x - 91.6f, position.z + 44.3f, fieldNoiseScale * 1.15f);
            float thermalNoise = EvaluateNoise01(position.x + 304.2f, position.z + 281.4f, detailNoiseScale * 0.92f);
            float metallicNoise = EvaluateNoise01(position.x - 211.5f, position.z + 96.7f, detailNoiseScale * 0.88f);
            float crystalNoise = EvaluateNoise01(position.x + 67.4f, position.z - 248.6f, detailNoiseScale * 0.84f);
            float voidNoise = EvaluateNoise01(position.x - 403.1f, position.z - 365.8f, fieldNoiseScale * 0.66f);
            float reefNoise = EvaluateNoise01(position.x + 149.7f, position.z - 71.9f, detailNoiseScale * 0.9f);
            float basinMacroNoise = EvaluateNoise01(position.x - 512.4f, position.z + 188.6f, fieldNoiseScale * 0.22f);
            float reefMacroNoise = EvaluateNoise01(position.x + 417.2f, position.z - 153.3f, fieldNoiseScale * 0.24f);
            float serviceMacroNoise = EvaluateNoise01(position.x - 286.5f, position.z + 407.8f, fieldNoiseScale * 0.21f);
            float riftMacroNoise = EvaluateNoise01(position.x + 598.1f, position.z - 487.2f, fieldNoiseScale * 0.19f);

            float depth01 = Mathf.Clamp01(depthMeters / 1200f);
            float steep01 = Mathf.Clamp01((slopeDegrees - 8f) / 40f);
            float shallow01 = 1f - Mathf.Clamp01(depthMeters / 220f);
            float resourceZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Resources || zoneKindHint == WorldZoneAnchor.ZoneKind.Fabrication
                ? 1f
                : zoneKindHint == WorldZoneAnchor.ZoneKind.Navigation
                    ? 0.55f
                    : 0f;
            float serviceZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Service || zoneKindHint == WorldZoneAnchor.ZoneKind.Power
                ? 1f
                : 0f;
            float hazardZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Combat || zoneKindHint == WorldZoneAnchor.ZoneKind.Progression
                ? 1f
                : 0f;
            float navigationZoneBias = zoneKindHint == WorldZoneAnchor.ZoneKind.Navigation ? 1f : 0f;

            float fertileScore = Mathf.Clamp01(
                ((fertileNoise * 0.65f) + (reefNoise * 0.35f))
                - (resourceZoneBias * 0.08f)
                - (serviceZoneBias * 0.16f)
                - (hazardZoneBias * 0.18f)
                + (navigationZoneBias * 0.08f));
            float ruggedScore = Mathf.Clamp01((ruggedNoise * 0.55f) + (steep01 * 0.45f));
            float thermalScore = Mathf.Clamp01((thermalNoise * 0.75f) + (depth01 * 0.25f));
            float metallicScore = Mathf.Clamp01((metallicNoise * 0.7f) + (depth01 * 0.3f));
            float voidScore = Mathf.Clamp01((voidNoise * 0.7f) + (depth01 * 0.3f));
            float sedimentScore = Mathf.Clamp01(
                ((1f - ruggedScore) * 0.24f)
                + ((1f - thermalScore) * 0.14f)
                + (resourceZoneBias * 0.22f)
                + (shallow01 * 0.08f)
                + (fertileNoise * 0.12f)
                + (reefNoise * 0.04f));
            float serviceScore = Mathf.Clamp01(
                (thermalScore * 0.34f)
                + (metallicScore * 0.34f)
                + (serviceZoneBias * 0.24f)
                + (depth01 * 0.08f));
            float hazardScore = Mathf.Clamp01(
                (ruggedScore * 0.28f)
                + (thermalScore * 0.16f)
                + (voidScore * 0.18f)
                + (hazardZoneBias * 0.26f)
                + (depth01 * 0.12f));
            float reefScore = Mathf.Clamp01(
                (fertileScore * 0.46f)
                + (reefNoise * 0.28f)
                + (shallow01 * 0.14f)
                + (navigationZoneBias * 0.12f));
            float sedimentContinuity = Mathf.Clamp01(
                (resourceZoneBias * 0.28f)
                + (basinMacroNoise * 0.24f)
                + ((1f - ruggedScore) * 0.12f)
                + ((1f - thermalScore) * 0.1f)
                + (shallow01 * 0.08f)
                + (depth01 * 0.06f)
                - (serviceZoneBias * 0.08f)
                - (hazardZoneBias * 0.1f));
            float reefContinuity = Mathf.Clamp01(
                (reefScore * 0.42f)
                + (reefMacroNoise * 0.24f)
                + (fertileScore * 0.14f)
                + (navigationZoneBias * 0.08f)
                - (resourceZoneBias * 0.16f)
                - (serviceZoneBias * 0.08f)
                - (hazardZoneBias * 0.1f));
            float serviceContinuity = Mathf.Clamp01(
                (serviceScore * 0.46f)
                + (serviceMacroNoise * 0.22f)
                + (metallicScore * 0.12f)
                + (thermalScore * 0.08f));
            float hazardContinuity = Mathf.Clamp01(
                (hazardScore * 0.48f)
                + (riftMacroNoise * 0.24f)
                + (voidScore * 0.12f));

            if (depthMeters <= 180f)
            {
                if (serviceZoneBias > 0.58f && serviceContinuity > 0.62f)
                    return ChooseFamily(volcanicGlassFamily, tectonicSpineFamily, chemosyntheticBrineFamily);

                if (hazardZoneBias > 0.6f && hazardContinuity > 0.62f)
                    return ChooseFamily(riftSpineFamily, graniteEscarpmentFamily, volcanicGlassFamily);

                if (resourceZoneBias > 0.42f && sedimentContinuity > 0.56f)
                    return ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, littoralKarstFamily);

                if (reefContinuity > 0.82f && crystalNoise < 0.76f)
                    return ChooseFamily(fossilReefFamily, littoralKarstFamily, sedimentDriftFamily);

                if (crystalNoise > 0.82f && reefContinuity > 0.7f && resourceZoneBias < 0.38f)
                    return ChooseFamily(crystalGrowthFamily, fossilReefFamily, littoralKarstFamily);

                if (sedimentScore > 0.62f || sedimentContinuity > 0.58f)
                    return ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, littoralKarstFamily);

                if (ruggedScore > 0.7f)
                    return ChooseFamily(graniteEscarpmentFamily, tectonicSpineFamily, volcanicGlassFamily);

                if (resourceZoneBias > 0.35f)
                    return ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, littoralKarstFamily);

                return shallow01 > 0.55f
                    ? ChooseFamily(littoralKarstFamily, sedimentDriftFamily, fossilReefFamily)
                    : ChooseFamily(sedimentDriftFamily, graniteEscarpmentFamily, abyssalSiltFamily);
            }

            if (depthMeters <= 600f)
            {
                if (serviceContinuity > 0.72f)
                    return ChooseFamily(volcanicGlassFamily, chemosyntheticBrineFamily, tectonicSpineFamily);

                if (hazardContinuity > 0.72f)
                    return ChooseFamily(riftSpineFamily, tectonicSpineFamily, graniteEscarpmentFamily);

                if ((sedimentScore > 0.68f && resourceZoneBias > 0.4f) || sedimentContinuity > 0.6f)
                    return ChooseFamily(abyssalSiltFamily, sedimentDriftFamily, graniteEscarpmentFamily);

                if (fertileScore > 0.66f && reefContinuity > 0.7f && resourceZoneBias < 0.34f)
                    return ChooseFamily(crystalGrowthFamily, fossilReefFamily, sedimentDriftFamily);

                if (metallicScore > 0.72f)
                    return ChooseFamily(chemosyntheticBrineFamily, metallicHadalFamily, abyssalSiltFamily);

                return ChooseFamily(abyssalSiltFamily, sedimentDriftFamily, graniteEscarpmentFamily);
            }

            if (voidScore > 0.76f && ruggedScore > 0.62f)
                return ChooseFamily(riftVoidFamily, volcanicHadalFamily, riftSpineFamily);

            if (thermalScore > 0.74f)
                return ChooseFamily(volcanicHadalFamily, chemosyntheticBrineFamily, volcanicGlassFamily);

            if (metallicScore > 0.72f)
                return ChooseFamily(metallicHadalFamily, chemosyntheticBrineFamily, abyssalSiltFamily);

            if (ruggedScore > 0.66f)
                return ChooseFamily(riftSpineFamily, tectonicSpineFamily, graniteEscarpmentFamily);

            if (fertileScore > 0.6f && crystalNoise > 0.68f)
                return ChooseFamily(crystalGrowthFamily, chemosyntheticBrineFamily, abyssalSiltFamily);

            return ChooseFamily(abyssalSiltFamily, sedimentDriftFamily, riftVoidFamily);
        }

        private WorldZoneAnchor.ZoneKind ResolveFallbackZoneKind(Vector3 position, float depthMeters, float slopeDegrees)
        {
            float shallow01 = 1f - Mathf.Clamp01(depthMeters / 220f);
            float deep01 = Mathf.Clamp01((depthMeters - 180f) / 900f);
            float steep01 = Mathf.Clamp01((slopeDegrees - 10f) / 38f);
            float fertileNoise = EvaluateNoise01(position.x - 91.6f, position.z + 44.3f, fieldNoiseScale * 1.15f);
            float thermalNoise = EvaluateNoise01(position.x + 304.2f, position.z + 281.4f, detailNoiseScale * 0.92f);
            float metallicNoise = EvaluateNoise01(position.x - 211.5f, position.z + 96.7f, detailNoiseScale * 0.88f);
            float voidNoise = EvaluateNoise01(position.x - 403.1f, position.z - 365.8f, fieldNoiseScale * 0.66f);

            float resourceScore = Mathf.Clamp01((shallow01 * 0.4f) + (fertileNoise * 0.6f));
            float serviceScore = Mathf.Clamp01((metallicNoise * 0.55f) + (thermalNoise * 0.45f));
            float hazardScore = Mathf.Clamp01((deep01 * 0.4f) + (steep01 * 0.25f) + (voidNoise * 0.35f));

            if (serviceScore > 0.74f)
                return thermalNoise > 0.58f ? WorldZoneAnchor.ZoneKind.Power : WorldZoneAnchor.ZoneKind.Service;

            if (hazardScore > 0.72f)
                return deep01 > 0.6f ? WorldZoneAnchor.ZoneKind.Progression : WorldZoneAnchor.ZoneKind.Combat;

            if (resourceScore > 0.7f)
                return fertileNoise > 0.64f ? WorldZoneAnchor.ZoneKind.Resources : WorldZoneAnchor.ZoneKind.Fabrication;

            if (steep01 > 0.55f || deep01 > 0.38f)
                return WorldZoneAnchor.ZoneKind.Navigation;

            return WorldZoneAnchor.ZoneKind.Resources;
        }

        private WorldProceduralPattern ResolvePattern(
            Vector3 position,
            float depthMeters,
            float slopeDegrees,
            HectonBiomeFamilyProfile biomeFamily,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind resolvedZoneKind)
        {
            float shallow01 = 1f - Mathf.Clamp01(depthMeters / 220f);
            float deep01 = Mathf.Clamp01((depthMeters - 180f) / 900f);
            float steep01 = Mathf.Clamp01((slopeDegrees - 10f) / 36f);
            float fertileBias = EvaluateFertileBiomeBias(zone, resolvedZoneKind, biomeFamily);
            float hazardBias = EvaluateHazardBias(zone, resolvedZoneKind);
            float serviceBias = EvaluateServiceBias(zone, resolvedZoneKind);
            float resourceBias = EvaluateResourceBias(zone, resolvedZoneKind);
            float shelterBias = EvaluateShelterBias(zone, resolvedZoneKind);
            float landmarkBias = EvaluateLandmarkBias(zone, resolvedZoneKind);
            float coralNoise = EvaluateNoise01(position.x + 153.4f, position.z - 74.7f, detailNoiseScale * 0.86f);
            float sedimentTokenBias = ContainsFamilyToken(biomeFamily, "sediment", "drift", "silt", "granite");
            float brineTokenBias = ContainsFamilyToken(biomeFamily, "brine", "chemo", "saline");
            float volcanicTokenBias = ContainsFamilyToken(biomeFamily, "volcanic", "tectonic", "glass", "magma", "basalt");
            float industrialTokenBias = ContainsFamilyToken(biomeFamily, "metallic", "industrial", "service");
            float riftTokenBias = ContainsFamilyToken(biomeFamily, "rift", "void", "hadal");

            if (landmarkBias > 0.82f && (steep01 > 0.42f || resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation || resolvedZoneKind == WorldZoneAnchor.ZoneKind.Progression))
                return WorldProceduralPattern.LandmarkCorridor;

            if (brineTokenBias > 0.55f && (serviceBias > 0.46f || hazardBias > 0.42f))
                return WorldProceduralPattern.BrineToxic;

            if (volcanicTokenBias > 0.55f && (steep01 > 0.34f || landmarkBias > 0.5f || hazardBias > 0.42f))
                return WorldProceduralPattern.VolcanicPressure;

            if (serviceBias > 0.82f)
                return WorldProceduralPattern.IndustrialService;

            if (hazardBias > 0.82f)
                return volcanicTokenBias > 0.46f ? WorldProceduralPattern.VolcanicPressure : WorldProceduralPattern.RiftHazard;

            if (sedimentTokenBias > 0.5f && (resourceBias > 0.58f || shelterBias > 0.58f))
                return WorldProceduralPattern.SedimentResources;

            if (depthMeters > 820f && fertileBias < 0.44f && shelterBias < 0.5f && serviceBias < 0.62f)
                return WorldProceduralPattern.AbyssSparse;

            if (fertileBias > 0.74f)
            {
                if (resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation || landmarkBias > 0.72f || coralNoise > 0.72f)
                    return WorldProceduralPattern.ReefNavigation;

                return WorldProceduralPattern.FertileShallows;
            }

            if (resourceBias > 0.68f || shelterBias > 0.64f)
                return WorldProceduralPattern.SedimentResources;

            if (brineTokenBias > 0.5f)
                return WorldProceduralPattern.BrineToxic;

            if (volcanicTokenBias > 0.5f)
                return WorldProceduralPattern.VolcanicPressure;

            if (industrialTokenBias > 0.5f)
                return WorldProceduralPattern.IndustrialService;

            if (riftTokenBias > 0.5f)
                return hazardBias > 0.58f ? WorldProceduralPattern.RiftHazard : WorldProceduralPattern.LandmarkCorridor;

            if (ContainsFamilyToken(biomeFamily, "reef", "littoral", "crystal") > 0.5f)
                return resolvedZoneKind == WorldZoneAnchor.ZoneKind.Navigation ? WorldProceduralPattern.ReefNavigation : WorldProceduralPattern.FertileShallows;

            if (deep01 > 0.7f)
                return WorldProceduralPattern.AbyssSparse;

            if (landmarkBias > 0.68f)
                return WorldProceduralPattern.LandmarkCorridor;

            return shallow01 > 0.45f
                ? WorldProceduralPattern.SedimentResources
                : WorldProceduralPattern.AbyssSparse;
        }

        private static HectonBiomeFamilyProfile ChooseFamily(params HectonBiomeFamilyProfile[] options)
        {
            if (options == null)
                return null;

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] != null)
                    return options[i];
            }

            return null;
        }

        private static float EvaluateZoneBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint, params WorldZoneAnchor.ZoneKind[] kinds)
        {
            if (kinds == null || kinds.Length == 0)
                return 0.42f;

            WorldZoneAnchor.ZoneKind effectiveKind = zone != null
                ? zone.Kind
                : zoneKindHint ?? WorldZoneAnchor.ZoneKind.Generic;

            for (int i = 0; i < kinds.Length; i++)
            {
                if (effectiveKind == kinds[i])
                    return 1f;
            }

            return 0.26f;
        }

        private static float ContainsFamilyToken(HectonBiomeFamilyProfile family, params string[] tokens)
        {
            if (family == null || tokens == null || tokens.Length == 0)
                return 0f;

            string familyId = family.familyId != null ? family.familyId.ToLowerInvariant() : string.Empty;
            string familyLabel = family.familyLabel != null ? family.familyLabel.ToLowerInvariant() : string.Empty;
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (string.IsNullOrWhiteSpace(token))
                    continue;

                string lowered = token.ToLowerInvariant();
                if (familyId.Contains(lowered) || familyLabel.Contains(lowered))
                    return 1f;
            }

            return 0f;
        }

        private static float EvaluateRuggedBiomeBias(WorldZoneAnchor zone)
        {
            if (zone == null)
                return 0.38f;

            HectonBiomeMatrixProfile biome = zone.DominantMatrixBiome;
            float familyBias = ContainsFamilyToken(zone.DominantBiomeFamily, "rift", "granite", "tectonic", "volcanic", "glass");
            if (biome == null)
                return Mathf.Lerp(0.25f, 1f, familyBias);

            float rugged = Mathf.Clamp01((biome.landmarkStrength + biome.routePressure) / 10f);
            return Mathf.Clamp01((rugged * 0.65f) + (familyBias * 0.35f));
        }

        private static float EvaluateFertileBiomeBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint, HectonBiomeFamilyProfile family)
        {
            float familyBias = ContainsFamilyToken(family, "littoral", "reef", "fossil", "crystal", "coral", "kelp", "growth");
            float zoneBias = EvaluateZoneBias(zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Fabrication, WorldZoneAnchor.ZoneKind.Navigation);
            return Mathf.Clamp01((familyBias * 0.72f) + (zoneBias * 0.28f));
        }

        private static float EvaluateHazardBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            if (zone == null)
                return EvaluateZoneBias(null, zoneKindHint, WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power);

            HectonBiomeMatrixProfile biome = zone.DominantMatrixBiome;
            float zoneBias = EvaluateZoneBias(zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Combat, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power);
            if (biome == null)
                return zoneBias;

            float biomeBias = Mathf.Clamp01(Mathf.Max(biome.survivalPressure, biome.routePressure) / 5f);
            return Mathf.Clamp01((zoneBias * 0.55f) + (biomeBias * 0.45f));
        }

        private static float EvaluateServiceBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            return EvaluateZoneBias(
                zone,
                zoneKindHint,
                WorldZoneAnchor.ZoneKind.Service,
                WorldZoneAnchor.ZoneKind.Power,
                WorldZoneAnchor.ZoneKind.Construction,
                WorldZoneAnchor.ZoneKind.Progression);
        }

        private static float EvaluateResourceBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            if (zone == null)
                return EvaluateZoneBias(null, zoneKindHint, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication);

            HectonBiomeMatrixProfile biome = zone.DominantMatrixBiome;
            float zoneBias = EvaluateZoneBias(zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Fabrication);
            if (biome == null)
                return zoneBias;

            float biomeBias = Mathf.Clamp01(Mathf.Max(biome.commonResourceBias, biome.uncommonResourceBias) / 5f);
            return Mathf.Clamp01((zoneBias * 0.6f) + (biomeBias * 0.4f));
        }

        private static float EvaluateShelterBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            return EvaluateZoneBias(
                zone,
                zoneKindHint,
                WorldZoneAnchor.ZoneKind.Fabrication,
                WorldZoneAnchor.ZoneKind.Navigation,
                WorldZoneAnchor.ZoneKind.Resources,
                WorldZoneAnchor.ZoneKind.Service);
        }

        private static float EvaluateLandmarkBias(WorldZoneAnchor zone, WorldZoneAnchor.ZoneKind? zoneKindHint)
        {
            if (zone == null)
                return EvaluateZoneBias(null, zoneKindHint, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat);

            HectonBiomeMatrixProfile biome = zone.DominantMatrixBiome;
            float zoneBias = EvaluateZoneBias(zone, zoneKindHint, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat);
            if (biome == null)
                return zoneBias;

            float biomeBias = Mathf.Clamp01(Mathf.Max(biome.landmarkStrength, biome.rewardPull) / 5f);
            return Mathf.Clamp01((zoneBias * 0.45f) + (biomeBias * 0.55f));
        }

        private WorldProceduralPattern ResolvePreviewPatternOverride(
            WorldProceduralPattern resolvedPattern,
            SeafloorSource source)
        {
            if (!forcePatternPreviewOverride)
                return resolvedPattern;

            if (limitPatternOverrideToFallback && source != SeafloorSource.FallbackSynthetic)
                return resolvedPattern;

            return previewPatternOverride;
        }

        private bool TryApplyPreviewPatternContextOverride(
            SeafloorSource source,
            float depthMeters,
            float slopeDegrees,
            ref HectonBiomeFamilyProfile biomeFamily,
            ref WorldZoneAnchor.ZoneKind resolvedZoneKind,
            out WorldProceduralPattern resolvedPattern)
        {
            resolvedPattern = WorldProceduralPattern.SedimentResources;

            if (!forcePatternPreviewOverride)
                return false;

            if (limitPatternOverrideToFallback && source != SeafloorSource.FallbackSynthetic)
                return false;

            resolvedPattern = previewPatternOverride;
            resolvedZoneKind = ResolvePreviewPatternZoneKind(previewPatternOverride);
            biomeFamily = ResolvePreviewPatternBiomeFamily(previewPatternOverride, depthMeters, slopeDegrees, biomeFamily);
            return true;
        }

        private HectonBiomeFamilyProfile ResolvePreviewPatternBiomeFamily(
            WorldProceduralPattern pattern,
            float depthMeters,
            float slopeDegrees,
            HectonBiomeFamilyProfile currentBiomeFamily)
        {
            HectonBiomeFamilyProfile fallback = currentBiomeFamily;
            if (fallback == null)
                fallback = sedimentDriftFamily;

            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => littoralKarstFamily != null
                    ? littoralKarstFamily
                    : crystalGrowthFamily != null ? crystalGrowthFamily : fallback,
                WorldProceduralPattern.ReefNavigation => fossilReefFamily != null
                    ? fossilReefFamily
                    : crystalGrowthFamily != null ? crystalGrowthFamily : fallback,
                WorldProceduralPattern.SedimentResources => depthMeters > 220f && graniteEscarpmentFamily != null
                    ? graniteEscarpmentFamily
                    : sedimentDriftFamily != null ? sedimentDriftFamily : fallback,
                WorldProceduralPattern.IndustrialService => tectonicSpineFamily != null
                    ? tectonicSpineFamily
                    : metallicHadalFamily != null ? metallicHadalFamily : fallback,
                WorldProceduralPattern.BrineToxic => chemosyntheticBrineFamily != null
                    ? chemosyntheticBrineFamily
                    : metallicHadalFamily != null ? metallicHadalFamily : fallback,
                WorldProceduralPattern.VolcanicPressure => depthMeters > 240f && volcanicHadalFamily != null
                    ? volcanicHadalFamily
                    : volcanicGlassFamily != null ? volcanicGlassFamily : fallback,
                WorldProceduralPattern.RiftHazard => depthMeters > 240f && riftVoidFamily != null
                    ? riftVoidFamily
                    : riftSpineFamily != null ? riftSpineFamily : fallback,
                WorldProceduralPattern.AbyssSparse => abyssalSiltFamily != null
                    ? abyssalSiltFamily
                    : metallicHadalFamily != null ? metallicHadalFamily : fallback,
                WorldProceduralPattern.LandmarkCorridor => slopeDegrees > 10f && graniteEscarpmentFamily != null
                    ? graniteEscarpmentFamily
                    : fossilReefFamily != null ? fossilReefFamily : fallback,
                _ => fallback
            };
        }

        private static WorldZoneAnchor.ZoneKind ResolvePreviewPatternZoneKind(WorldProceduralPattern pattern)
        {
            return pattern switch
            {
                WorldProceduralPattern.FertileShallows => WorldZoneAnchor.ZoneKind.Resources,
                WorldProceduralPattern.ReefNavigation => WorldZoneAnchor.ZoneKind.Navigation,
                WorldProceduralPattern.SedimentResources => WorldZoneAnchor.ZoneKind.Resources,
                WorldProceduralPattern.IndustrialService => WorldZoneAnchor.ZoneKind.Service,
                WorldProceduralPattern.BrineToxic => WorldZoneAnchor.ZoneKind.Combat,
                WorldProceduralPattern.VolcanicPressure => WorldZoneAnchor.ZoneKind.Progression,
                WorldProceduralPattern.RiftHazard => WorldZoneAnchor.ZoneKind.Combat,
                WorldProceduralPattern.AbyssSparse => WorldZoneAnchor.ZoneKind.Progression,
                WorldProceduralPattern.LandmarkCorridor => WorldZoneAnchor.ZoneKind.Navigation,
                _ => WorldZoneAnchor.ZoneKind.Generic
            };
        }

        private static string ResolvePreviewBiomeLabel(HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
                return "None";

            if (!string.IsNullOrWhiteSpace(biomeFamily.familyLabel))
                return biomeFamily.familyLabel;

            if (!string.IsNullOrWhiteSpace(biomeFamily.familyId))
                return biomeFamily.familyId;

            return biomeFamily.name;
        }

        private HectonBiomeMatrixProfile ResolveEffectiveBiomeProfile(
            HectonBiomeMatrixProfile currentProfile,
            HectonBiomeFamilyProfile biomeFamily,
            SeafloorSource source,
            WorldProceduralPattern resolvedPattern)
        {
            if (currentProfile != null && (!forcePatternPreviewOverride || (limitPatternOverrideToFallback && source != SeafloorSource.FallbackSynthetic)))
                return currentProfile;

            if (forcePatternPreviewOverride && (!limitPatternOverrideToFallback || source == SeafloorSource.FallbackSynthetic))
            {
                HectonBiomeMatrixProfile previewProfile = ResolvePreviewPatternBiomeProfile(previewPatternOverride, biomeFamily);
                if (previewProfile != null)
                    return previewProfile;
            }

            HectonBiomeMatrixProfile representativeProfile = ResolveRepresentativeBiomeProfileForFamily(biomeFamily);
            return representativeProfile != null ? representativeProfile : currentProfile;
        }

        private HectonBiomeMatrixProfile ResolvePreviewMatrixBiomeOverride(SeafloorSource source)
        {
            if (!forceMatrixBiomePreviewOverride || previewMatrixBiomeOverride == null)
                return null;

            if (limitMatrixBiomeOverrideToFallback && source != SeafloorSource.FallbackSynthetic)
                return null;

            return previewMatrixBiomeOverride;
        }

        private HectonBiomeMatrixProfile ResolvePreviewPatternBiomeProfile(
            WorldProceduralPattern pattern,
            HectonBiomeFamilyProfile biomeFamily)
        {
            HectonBiomeFamilyProfile targetFamily = ResolvePreviewPatternBiomeFamily(pattern, 0f, 0f, biomeFamily);
            return ResolveRepresentativeBiomeProfileForFamily(targetFamily);
        }

        private HectonBiomeMatrixProfile ResolveRepresentativeBiomeProfileForFamily(HectonBiomeFamilyProfile targetFamily)
        {
            if (targetFamily == null || biomeMatrixDirector == null || biomeMatrixDirector.MatrixCatalog == null || biomeMatrixDirector.MatrixCatalog.Profiles == null)
                return null;

            HectonBiomeMatrixProfile best = null;
            int bestScore = int.MinValue;
            HectonBiomeMatrixProfile fallback = null;
            HectonBiomeMatrixProfile[] profiles = biomeMatrixDirector.MatrixCatalog.Profiles;
            for (int i = 0; i < profiles.Length; i++)
            {
                HectonBiomeMatrixProfile profile = profiles[i];
                if (profile == null)
                    continue;

                if (profile.familyProfile != targetFamily && !string.Equals(profile.familyId, targetFamily.familyId, System.StringComparison.Ordinal))
                    continue;

                int score = (profile.rewardPull * 3) + (profile.landmarkStrength * 2) + profile.commonResourceBias + profile.uncommonResourceBias + profile.rareResourceBias;
                if (!profile.isPlaceholder && score > bestScore)
                {
                    best = profile;
                    bestScore = score;
                }

                fallback ??= profile;
            }

            return best != null ? best : fallback;
        }

        private static float EvaluateBiomeMatrixChannelBonus(string channel, HectonBiomeMatrixProfile biomeProfile)
        {
            if (biomeProfile == null)
                return 0f;

            float loosePickup = NormalizeMatrixBias(biomeProfile.loosePickupBias);
            float node = NormalizeMatrixBias(biomeProfile.nodeExtractionBias);
            float salvage = NormalizeMatrixBias(biomeProfile.salvageBias);
            float common = NormalizeMatrixBias(biomeProfile.commonResourceBias);
            float uncommon = NormalizeMatrixBias(biomeProfile.uncommonResourceBias);
            float rare = NormalizeMatrixBias(biomeProfile.rareResourceBias);
            float route = NormalizeMatrixBias(biomeProfile.routePressure);
            float landmark = NormalizeMatrixBias(biomeProfile.landmarkStrength);
            float reward = NormalizeMatrixBias(biomeProfile.rewardPull);
            float survival = NormalizeMatrixBias(biomeProfile.survivalPressure);
            float resource = Mathf.Clamp01((common * 0.45f) + (uncommon * 0.35f) + (rare * 0.2f));
            float salvageRead = Mathf.Clamp01((salvage * 0.62f) + (node * 0.38f));
            float landmarkRead = Mathf.Clamp01((landmark * 0.64f) + (route * 0.36f));
            float hazardRead = Mathf.Clamp01((survival * 0.58f) + (route * 0.26f) + (rare * 0.16f));
            float shelterRead = Mathf.Clamp01((survival * 0.68f) + (loosePickup * 0.16f) + ((1f - hazardRead) * 0.16f));
            float faunaRead = Mathf.Clamp01((common * 0.34f) + (reward * 0.18f) + ((1f - survival) * 0.48f));

            return channel switch
            {
                "rock_density" => landmarkRead * 0.08f + node * 0.04f,
                "kelp_density" => faunaRead * 0.05f + shelterRead * 0.03f,
                "flora_density" => faunaRead * 0.06f + reward * 0.04f,
                "coral_density" => faunaRead * 0.07f + landmarkRead * 0.03f,
                "bio_density" => faunaRead * 0.11f + reward * 0.04f,
                "debris_density" => salvageRead * 0.12f,
                "ruin_density" => salvageRead * 0.10f + landmarkRead * 0.04f,
                "cave_density" => landmarkRead * 0.10f + hazardRead * 0.04f,
                "landmark_strength" => landmarkRead * 0.13f + reward * 0.04f,
                "fauna_density" => faunaRead * 0.12f - hazardRead * 0.03f,
                "hazard_density" => hazardRead * 0.11f,
                "resource_density" => resource * 0.12f + reward * 0.05f,
                "shelter_density" => shelterRead * 0.12f,
                "service_density" => salvageRead * 0.1f + node * 0.05f,
                _ => 0f
            };
        }

        private static float NormalizeMatrixBias(int value)
        {
            return Mathf.Clamp01(value / 5f);
        }

        private void ResolveReferences()
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null)
                    player = GameObject.Find("Player");

                if (player != null)
                    playerTransform = player.transform;
            }

            if (mapMagicBridge == null)
                mapMagicBridge = MapMagicBridge.Instance ?? FindAnyObjectByType<MapMagicBridge>();

            if (worldZoneDirector == null)
                worldZoneDirector = FindAnyObjectByType<WorldZoneDirector>();

            if (biomeMatrixDirector == null)
                biomeMatrixDirector = FindAnyObjectByType<BiomeMatrixDirector>();

            _debugBridgeReady = mapMagicBridge != null;
            _debugZoneDirectorReady = worldZoneDirector != null;
            _debugBiomeDirectorReady = biomeMatrixDirector != null;
        }

        private void UpdateDiagnostics(FieldSample sample, string channel, float value)
        {
            _debugLastZone = sample.zone != null ? sample.zone.ZoneLabel : $"Synthetic:{sample.resolvedZoneKind}";
            _debugLastBiomeProfile = sample.biomeProfile != null ? sample.biomeProfile.biomeName : "None";
            _debugLastBiomeFamily = sample.biomeFamily != null ? sample.biomeFamily.familyLabel : "None";
            _debugLastPattern = sample.isValid ? sample.resolvedPattern.ToString() : "None";
            _debugPatternOverride = forcePatternPreviewOverride
                ? limitPatternOverrideToFallback
                    ? $"{previewPatternOverride} (FallbackOnly)"
                    : $"{previewPatternOverride} (Forced)"
                : "None";
            _debugPreviewBiomeOverride = forcePatternPreviewOverride
                ? ResolvePreviewBiomeLabel(ResolvePreviewPatternBiomeFamily(previewPatternOverride, sample.depthMeters, sample.slopeDegrees, sample.biomeFamily))
                : "None";
            _debugPreviewMatrixOverride = forceMatrixBiomePreviewOverride && previewMatrixBiomeOverride != null
                ? limitMatrixBiomeOverrideToFallback
                    ? $"{previewMatrixBiomeOverride.biomeName} (FallbackOnly)"
                    : $"{previewMatrixBiomeOverride.biomeName} (Forced)"
                : forcePatternPreviewOverride
                    ? ResolvePreviewPatternBiomeProfile(previewPatternOverride, sample.biomeFamily) != null
                        ? ResolvePreviewPatternBiomeProfile(previewPatternOverride, sample.biomeFamily).biomeName
                        : "None"
                    : "None";
            _debugPreviewZoneOverride = forcePatternPreviewOverride
                ? ResolvePreviewPatternZoneKind(previewPatternOverride).ToString()
                : "None";
            _debugLastHeatmap = string.IsNullOrWhiteSpace(channel) ? "None" : channel;
            _debugLastHeightSource = sample.seafloorSource.ToString();
            _debugLastHeatmapValue = value;
            _debugLastDepth = sample.depthMeters;
            _debugLastSlope = sample.slopeDegrees;
        }
    }
}
