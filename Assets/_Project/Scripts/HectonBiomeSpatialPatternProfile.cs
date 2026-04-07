using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeSpatialPatternProfile", menuName = "Hecton/Environment/Biome Spatial Pattern Profile", order = 109)]
    public sealed class HectonBiomeSpatialPatternProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "biome.spatial_pattern.generic";
        public string profileLabel = "Generic Spatial Pattern";

        [Header("Content Pattern")]
        [TextArea(2, 4)] public string resourcePocketPattern = "Loose resources sit in readable nearby pockets.";
        [TextArea(2, 4)] public string nodeClusterPattern = "Nodes appear in small readable clusters.";
        [TextArea(2, 4)] public string safePocketPattern = "Short recovery pockets exist behind obvious cover.";
        [TextArea(2, 4)] public string routeAnchorPattern = "Routes chain from one readable anchor to another.";
        [TextArea(2, 4)] public string rareObjectivePattern = "Rare value sits one layer deeper than routine value.";

        [Header("Rhythm")]
        [TextArea(2, 4)] public string explorationLoop = "Read landmark, clear nearby pocket, commit deeper, then return.";
        [TextArea(2, 4)] public string spatialRead = "The biome should read clearly at near, mid, and far distance.";
        [TextArea(2, 4)] public string playerMemoryHook = "The player remembers this place by one repeated structural pattern.";
    }
}
