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
        [SerializeField] private int clusterIndex;
        [SerializeField] private int instanceIndex;

        [Header("Field Scatter")]
        [SerializeField] private string placementSource = "Socket";
        [SerializeField] private string scatterLayer = "Ground";
        [SerializeField] private string fieldSource = "None";
        [SerializeField] private float seafloorHeight;
        [SerializeField] private float depthMeters;
        [SerializeField] private float slopeDegrees;
        [SerializeField] private string heatmapChannel = "None";
        [SerializeField] private float heatmapValue;
        [SerializeField] private string sourceBiomeMatrix = "None";
        [SerializeField] private string sourceBiomeFamily = "None";
        [SerializeField] private string sourceWaterPattern = "None";
        [SerializeField] private string sourceBiomeContext = "None";
        [SerializeField] private int cellX;
        [SerializeField] private int cellZ;

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
            clusterIndex = configuredClusterIndex;
            instanceIndex = configuredInstanceIndex;
            placementSource = "Socket";
            scatterLayer = family != null ? family.scatterLayer.ToString() : "Ground";
            fieldSource = "None";
            seafloorHeight = socket != null ? socket.transform.position.y : 0f;
            depthMeters = 0f;
            slopeDegrees = 0f;
            heatmapChannel = "None";
            heatmapValue = 0f;
            sourceBiomeMatrix = "None";
            sourceBiomeFamily = "None";
            sourceWaterPattern = "None";
            sourceBiomeContext = "None";
            cellX = 0;
            cellZ = 0;
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
            string configuredBiomeMatrix,
            string configuredBiomeFamily,
            string configuredWaterPattern,
            string configuredBiomeContext,
            int configuredCellX,
            int configuredCellZ)
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
            clusterIndex = configuredClusterIndex;
            instanceIndex = configuredInstanceIndex;
            placementSource = "FieldScatter";
            scatterLayer = family != null ? family.scatterLayer.ToString() : "Ground";
            fieldSource = configuredFieldSource.ToString();
            seafloorHeight = configuredSeafloorHeight;
            depthMeters = configuredDepthMeters;
            slopeDegrees = configuredSlopeDegrees;
            heatmapChannel = string.IsNullOrWhiteSpace(configuredHeatmapChannel) ? "None" : configuredHeatmapChannel;
            heatmapValue = Mathf.Clamp01(configuredHeatmapValue);
            sourceBiomeMatrix = string.IsNullOrWhiteSpace(configuredBiomeMatrix) ? "None" : configuredBiomeMatrix;
            sourceBiomeFamily = string.IsNullOrWhiteSpace(configuredBiomeFamily) ? "None" : configuredBiomeFamily;
            sourceWaterPattern = string.IsNullOrWhiteSpace(configuredWaterPattern) ? "None" : configuredWaterPattern;
            sourceBiomeContext = string.IsNullOrWhiteSpace(configuredBiomeContext) ? "None" : configuredBiomeContext;
            cellX = configuredCellX;
            cellZ = configuredCellZ;
        }
    }
}
