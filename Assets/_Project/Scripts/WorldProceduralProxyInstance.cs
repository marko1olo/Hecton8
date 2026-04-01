using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldProceduralProxyInstance : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string familyId = "world.family.generic";
        [SerializeField] private string familyLabel = "Generic World Family";
        [SerializeField] private string ruleId = "procedural.rule.generic";
        [SerializeField] private string ruleLabel = "Generic Procedural Rule";

        [Header("Source")]
        [SerializeField] private string zoneId = "zone.generic";
        [SerializeField] private string socketId = "socket.generic";
        [SerializeField] private WorldContentSocket.ContentKind socketKind = WorldContentSocket.ContentKind.Generic;
        [SerializeField] private WorldSliceAnchor.SliceState preferredFidelity = WorldSliceAnchor.SliceState.Mid;

        [Header("Variant")]
        [SerializeField] private string variantId = "variant.generic";
        [SerializeField] private bool proxyOnly = true;
        [SerializeField] private bool supportsFinalVariant;
        [SerializeField] private bool finalVariantActive;
        [SerializeField] private int clusterIndex;
        [SerializeField] private int instanceIndex;

        [Header("Field Scatter")]
        [SerializeField] private long runtimeKey;
        [SerializeField] private WorldStreamingLayer streamingLayer = WorldStreamingLayer.Flora;
        [SerializeField] private string placementSource = "Socket";
        [SerializeField] private string scatterLayer = "Ground";
        [SerializeField] private string fieldSource = "None";
        [SerializeField] private float seafloorHeight;
        [SerializeField] private float depthMeters;
        [SerializeField] private float slopeDegrees;
        [SerializeField] private float curvature;
        [SerializeField] private float caveProximity;
        [SerializeField] private float ridgeSignal;
        [SerializeField] private float canyonSignal;
        [SerializeField] private float compositionPotential;
        [SerializeField] private string heatmapChannel = "None";
        [SerializeField] private float heatmapValue;
        [SerializeField] private string sourceBiomeMatrix = "None";
        [SerializeField] private string sourceBiomeFamily = "None";
        [SerializeField] private string sourceWaterPattern = "None";
        [SerializeField] private string sourceBiomeContext = "None";
        [SerializeField] private bool usesGenerativeGeology;
        [SerializeField] private string geologyProfileId = "None";
        [SerializeField] private string geologyArchetype = "None";
        [SerializeField] private int cellX;
        [SerializeField] private int cellZ;
        [SerializeField] private int chunkX;
        [SerializeField] private int chunkZ;
        [SerializeField] private bool hasMacroZone;
        [SerializeField] private int macroZoneX;
        [SerializeField] private int macroZoneZ;

        public string ActiveVariantId => variantId;
        public bool IsFinalVariantActive => finalVariantActive;
        public bool SupportsFinalVariant => supportsFinalVariant;
        public long RuntimeKey => runtimeKey;
        public string FamilyId => familyId;
        public string GeologyProfileId => geologyProfileId;
        public string GeologyArchetype => geologyArchetype;
        public bool UsesGenerativeGeology => usesGenerativeGeology;
        public WorldStreamingLayer ActiveStreamingLayer => streamingLayer;
        public WorldChunkCoordinate ChunkCoord => new WorldChunkCoordinate(chunkX, chunkZ);
        public bool HasMacroZone => hasMacroZone;
        public WorldMacroZoneCoordinate MacroZoneCoord => new WorldMacroZoneCoordinate(macroZoneX, macroZoneZ);
        public float DepthMeters => depthMeters;
        public float SlopeDegrees => slopeDegrees;
        public float Curvature => curvature;
        public float CaveProximity => caveProximity;
        public float RidgeSignal => ridgeSignal;
        public float CanyonSignal => canyonSignal;
        public float CompositionPotential => compositionPotential;

        public void Configure(
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule,
            WorldZoneAnchor zone,
            WorldContentSocket socket,
            string configuredVariantId,
            bool configuredProxyOnly,
            int configuredClusterIndex,
            int configuredInstanceIndex)
        {
            familyId = family != null ? family.familyId : "world.family.generic";
            familyLabel = family != null ? family.familyLabel : "Generic World Family";
            ruleId = rule != null ? rule.ruleId : "procedural.rule.generic";
            ruleLabel = rule != null ? rule.ruleLabel : "Generic Procedural Rule";
            zoneId = zone != null ? zone.ZoneId : "zone.generic";
            socketId = socket != null ? socket.SocketId : "socket.generic";
            socketKind = socket != null ? socket.Kind : WorldContentSocket.ContentKind.Generic;
            preferredFidelity = family != null ? family.defaultFidelity : WorldSliceAnchor.SliceState.Mid;
            variantId = string.IsNullOrWhiteSpace(configuredVariantId) ? "variant.generic" : configuredVariantId;
            proxyOnly = configuredProxyOnly;
            supportsFinalVariant = false;
            finalVariantActive = false;
            clusterIndex = configuredClusterIndex;
            instanceIndex = configuredInstanceIndex;
            runtimeKey = 0L;
            streamingLayer = family != null ? family.ResolveStreamingLayer() : WorldStreamingLayer.Flora;
            placementSource = "Socket";
            scatterLayer = family != null ? family.scatterLayer.ToString() : "Ground";
            fieldSource = "None";
            seafloorHeight = socket != null ? socket.transform.position.y : 0f;
            depthMeters = 0f;
            slopeDegrees = 0f;
            curvature = 0f;
            caveProximity = 0f;
            ridgeSignal = 0f;
            canyonSignal = 0f;
            compositionPotential = 0f;
            heatmapChannel = "None";
            heatmapValue = 0f;
            sourceBiomeMatrix = "None";
            sourceBiomeFamily = "None";
            sourceWaterPattern = "None";
            sourceBiomeContext = "None";
            usesGenerativeGeology = family != null && family.UsesGenerativeGeology();
            geologyProfileId = family != null && family.generativeGeologyProfile != null ? family.generativeGeologyProfile.profileId : "None";
            geologyArchetype = family != null && family.generativeGeologyProfile != null ? family.generativeGeologyProfile.shapeArchetype.ToString() : "None";
            cellX = 0;
            cellZ = 0;
            chunkX = 0;
            chunkZ = 0;
            hasMacroZone = false;
            macroZoneX = 0;
            macroZoneZ = 0;
        }

        public void ConfigureScatter(
            WorldPrefabFamilyProfile family,
            WorldProceduralPlacementRule rule,
            WorldZoneAnchor zone,
            string configuredVariantId,
            bool configuredProxyOnly,
            int configuredClusterIndex,
            int configuredInstanceIndex,
            string configuredHeatmapChannel,
            float configuredHeatmapValue,
            WorldProceduralFieldSampler.SeafloorSource configuredFieldSource,
            float configuredSeafloorHeight,
            float configuredDepthMeters,
            float configuredSlopeDegrees,
            float configuredCurvature,
            float configuredCaveProximity,
            float configuredRidgeSignal,
            float configuredCanyonSignal,
            float configuredCompositionPotential,
            string configuredBiomeMatrix,
            string configuredBiomeFamily,
            string configuredWaterPattern,
            string configuredBiomeContext,
            int configuredCellX,
            int configuredCellZ,
            long configuredRuntimeKey = 0L,
            WorldStreamingLayer configuredStreamingLayer = WorldStreamingLayer.Flora,
            WorldChunkCoordinate configuredChunkCoord = default,
            bool configuredHasMacroZone = false,
            WorldMacroZoneCoordinate configuredMacroZoneCoord = default,
            bool configuredSupportsFinalVariant = false,
            bool configuredFinalVariantActive = false)
        {
            familyId = family != null ? family.familyId : "world.family.generic";
            familyLabel = family != null ? family.familyLabel : "Generic World Family";
            ruleId = rule != null ? rule.ruleId : "procedural.rule.generic";
            ruleLabel = rule != null ? rule.ruleLabel : "Generic Procedural Rule";
            zoneId = zone != null ? zone.ZoneId : "zone.generic";
            socketId = "scatter.field";
            socketKind = rule != null ? rule.GetScatterContentKind() : WorldContentSocket.ContentKind.Generic;
            preferredFidelity = family != null ? family.defaultFidelity : WorldSliceAnchor.SliceState.Mid;
            variantId = string.IsNullOrWhiteSpace(configuredVariantId) ? "variant.generic" : configuredVariantId;
            proxyOnly = configuredProxyOnly;
            supportsFinalVariant = configuredSupportsFinalVariant;
            finalVariantActive = configuredFinalVariantActive;
            clusterIndex = configuredClusterIndex;
            instanceIndex = configuredInstanceIndex;
            runtimeKey = configuredRuntimeKey;
            streamingLayer = configuredStreamingLayer;
            placementSource = "FieldScatter";
            scatterLayer = family != null ? family.scatterLayer.ToString() : "Ground";
            fieldSource = configuredFieldSource.ToString();
            seafloorHeight = configuredSeafloorHeight;
            depthMeters = configuredDepthMeters;
            slopeDegrees = configuredSlopeDegrees;
            curvature = configuredCurvature;
            caveProximity = configuredCaveProximity;
            ridgeSignal = configuredRidgeSignal;
            canyonSignal = configuredCanyonSignal;
            compositionPotential = configuredCompositionPotential;
            heatmapChannel = string.IsNullOrWhiteSpace(configuredHeatmapChannel) ? "None" : configuredHeatmapChannel;
            heatmapValue = Mathf.Clamp01(configuredHeatmapValue);
            sourceBiomeMatrix = string.IsNullOrWhiteSpace(configuredBiomeMatrix) ? "None" : configuredBiomeMatrix;
            sourceBiomeFamily = string.IsNullOrWhiteSpace(configuredBiomeFamily) ? "None" : configuredBiomeFamily;
            sourceWaterPattern = string.IsNullOrWhiteSpace(configuredWaterPattern) ? "None" : configuredWaterPattern;
            sourceBiomeContext = string.IsNullOrWhiteSpace(configuredBiomeContext) ? "None" : configuredBiomeContext;
            usesGenerativeGeology = family != null && family.UsesGenerativeGeology();
            geologyProfileId = family != null && family.generativeGeologyProfile != null ? family.generativeGeologyProfile.profileId : "None";
            geologyArchetype = family != null && family.generativeGeologyProfile != null ? family.generativeGeologyProfile.shapeArchetype.ToString() : "None";
            cellX = configuredCellX;
            cellZ = configuredCellZ;
            chunkX = configuredChunkCoord.x;
            chunkZ = configuredChunkCoord.z;
            hasMacroZone = configuredHasMacroZone;
            macroZoneX = configuredMacroZoneCoord.x;
            macroZoneZ = configuredMacroZoneCoord.z;
        }
    }
}
