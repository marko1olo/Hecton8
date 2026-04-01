using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldProceduralPlaceholderMarker : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string familyId = "world.family.generic";
        [SerializeField] private string familyLabel = "Generic Placeholder";
        [SerializeField] private string variantId = "variant.placeholder";
        [SerializeField] private WorldStreamingLayer streamingLayer = WorldStreamingLayer.Flora;

        [Header("Placeholder")]
        [SerializeField] private string placeholderRecipe = "Generic";
        [SerializeField] private string futurePrefabRoot = string.Empty;
        [SerializeField] private bool generatedPlaceholder = true;
        [SerializeField] private bool replaceVisualRootOnly = true;

        public string FamilyId => familyId;
        public string VariantId => variantId;
        public WorldStreamingLayer StreamingLayer => streamingLayer;
        public string PlaceholderRecipe => placeholderRecipe;
        public string FuturePrefabRoot => futurePrefabRoot;
        public bool GeneratedPlaceholder => generatedPlaceholder;
        public bool ReplaceVisualRootOnly => replaceVisualRootOnly;

        public void Configure(
            WorldPrefabFamilyProfile family,
            string configuredVariantId,
            string configuredRecipe,
            bool configuredReplaceVisualRootOnly = true)
        {
            familyId = family != null && !string.IsNullOrWhiteSpace(family.familyId)
                ? family.familyId
                : "world.family.generic";
            familyLabel = family != null && !string.IsNullOrWhiteSpace(family.familyLabel)
                ? family.familyLabel
                : "Generic Placeholder";
            variantId = string.IsNullOrWhiteSpace(configuredVariantId)
                ? $"{familyId}.final.placeholder"
                : configuredVariantId;
            streamingLayer = family != null ? family.ResolveStreamingLayer() : WorldStreamingLayer.Flora;
            placeholderRecipe = string.IsNullOrWhiteSpace(configuredRecipe) ? "Generic" : configuredRecipe;
            futurePrefabRoot = family != null ? family.futurePrefabRoot : string.Empty;
            generatedPlaceholder = true;
            replaceVisualRootOnly = configuredReplaceVisualRootOnly;
        }
    }
}
