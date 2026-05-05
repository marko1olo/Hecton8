using System.Collections.Generic;
using System.Text;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Crafting
{
    public enum FabricationGroup
    {
        Unspecified = 0,
        Materials = 1,
        Components = 2,
        Tools = 3,
        Suit = 4,
        Construction = 5,
        Power = 6
    }

    /// <summary>
    /// Data for a single fabrication recipe.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Hecton/Recipe", order = 20)]
    public sealed class RecipeData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Authoring fallback name for the recipe UI.")]
        public string recipeName = "New Recipe";

        [Tooltip("Localized display name override. Falls back to the result item localization when empty.")]
        [SerializeField] private LocalizedTextReference localizedRecipeName;

        [Tooltip("Optional override icon. Falls back to result item icon.")]
        public Sprite overrideIcon;

        [TextArea(2, 4)]
        [Tooltip("Authoring fallback description.")]
        public string description = string.Empty;

        [Tooltip("Localized description override. Falls back to the result item localization when empty.")]
        [SerializeField] private LocalizedTextReference localizedRecipeDescription;

        [Header("Result")]
        [Tooltip("Result item granted by the recipe.")]
        public ItemData resultItem;

        [Tooltip("How many result items are crafted.")]
        [Min(1)]
        public int resultQuantity = 1;

        [Header("Ingredients")]
        [Tooltip("Required ingredients and quantities.")]
        public List<InventoryCost> ingredients = new List<InventoryCost>();

        [Header("Timing")]
        [Tooltip("Craft duration in seconds.")]
        [Min(0.1f)]
        public float craftTime = 3f;

        [Header("Power")]
        [Tooltip("Power cost paid when crafting completes.")]
        [Min(0f)]
        public float powerCost = 5f;

        [Header("Unlock")]
        [Tooltip("Optional scan-log unlock dependency. Empty means always available.")]
        public string requiredScanEntryId = string.Empty;

        [Header("Biome Lock")]
        [Tooltip("When true, this recipe can only run from an anchored base module inside the required matrix biome/family.")]
        public bool requiresAnchoredBaseBiome;
        [Tooltip("Optional required matrix biome id. 0 disables exact matrix id gating.")]
        public int requiredAnchoredBiomeMatrixId;
        [Tooltip("Optional required biome family id, for example biome.family.crystal_growth.")]
        public string requiredAnchoredBiomeFamilyId = string.Empty;

        [Header("Fabricator Group")]
        [Tooltip("Optional explicit fabricator group override.")]
        public FabricationGroup fabricationGroup = FabricationGroup.Unspecified;

        private string _cachedCraftText;
        private string _cachedCostSummary;

        private void OnEnable()
        {
            RebuildCache();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (resultQuantity < 1)
                resultQuantity = 1;

            if (craftTime < 0.1f)
                craftTime = 0.1f;

            RebuildCache();
        }
#endif

        /// <summary>
        /// Localized display name used by Fabricator and PDA recipe surfaces.
        /// </summary>
        public string DisplayNameOrFallback => resultItem != null && !string.IsNullOrWhiteSpace(resultItem.itemName)
            ? ResolveLocalizedRecipeName(resultItem.itemName)
            : ResolveLocalizedRecipeName(recipeName);

        /// <summary>
        /// Localized description used by Fabricator detail surfaces.
        /// </summary>
        public string DescriptionOrFallback => resultItem != null && !string.IsNullOrWhiteSpace(resultItem.description)
            ? ResolveLocalizedRecipeDescription(resultItem.description)
            : ResolveLocalizedRecipeDescription(description);

        /// <summary>
        /// Localized craft CTA used by prompt-style UI.
        /// </summary>
        public string GetCraftText()
        {
            RebuildCache();
            return _cachedCraftText;
        }

        /// <summary>
        /// Localized cost summary used by prompt-style UI.
        /// </summary>
        public string GetCostSummary()
        {
            RebuildCache();
            return _cachedCostSummary;
        }

        /// <summary>
        /// Icon shown for the recipe.
        /// </summary>
        public Sprite Icon => overrideIcon != null
            ? overrideIcon
            : (resultItem != null ? resultItem.icon : null);

        public bool RequiresScanUnlock => !string.IsNullOrWhiteSpace(requiredScanEntryId);
        public bool RequiresAnchoredBiomeLock => requiresAnchoredBaseBiome &&
                                                 (requiredAnchoredBiomeMatrixId > 0 ||
                                                  !string.IsNullOrWhiteSpace(requiredAnchoredBiomeFamilyId));

        public string RequiredScanEntryId => string.IsNullOrWhiteSpace(requiredScanEntryId)
            ? string.Empty
            : requiredScanEntryId.Trim();

        public bool IsUnlocked(ScanLogSystem scanLogSystem)
        {
            if (!RequiresScanUnlock)
                return true;

            return scanLogSystem != null && scanLogSystem.ContainsEntry(RequiredScanEntryId);
        }

        public FabricationGroup GetResolvedFabricationGroup()
        {
            if (fabricationGroup != FabricationGroup.Unspecified)
                return fabricationGroup;

            if (resultItem == null)
                return FabricationGroup.Components;

            switch (resultItem.category)
            {
                case ItemCategory.Material:
                    return FabricationGroup.Materials;
                case ItemCategory.Component:
                    return FabricationGroup.Components;
                case ItemCategory.Tool:
                    return FabricationGroup.Tools;
                case ItemCategory.Equipment:
                case ItemCategory.Consumable:
                    return FabricationGroup.Suit;
                default:
                    return FabricationGroup.Components;
            }
        }

        public string GetFabricationGroupLabel()
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            switch (GetResolvedFabricationGroup())
            {
                case FabricationGroup.Materials:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_MATERIALS, "MATERIALS") : "MATERIALS";
                case FabricationGroup.Components:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_COMPONENTS, "COMPONENTS") : "COMPONENTS";
                case FabricationGroup.Tools:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_TOOLS, "TOOLS") : "TOOLS";
                case FabricationGroup.Suit:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_SUIT, "SUIT") : "SUIT";
                case FabricationGroup.Construction:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_CONSTRUCTION, "CONSTRUCTION") : "CONSTRUCTION";
                case FabricationGroup.Power:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_POWER, "POWER") : "POWER";
                default:
                    return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, LocalizationKeys.FAB_GROUP_ALL, "ALL") : "ALL";
            }
        }

        private void RebuildCache()
        {
            _cachedCraftText = "Create " + DisplayNameOrFallback;

            var sb = new StringBuilder(64);
            int ingredientCount = ingredients != null ? ingredients.Count : 0;
            for (int i = 0; i < ingredientCount; i++)
            {
                InventoryCost cost = ingredients[i];
                if (cost == null || cost.item == null)
                    continue;

                if (sb.Length > 0)
                    sb.Append(", ");

                if (LocalizedInlineIconResolver.TryResolveItemChip(cost.item, out string chip))
                {
                    sb.Append(chip);
                    sb.Append(' ');
                }

                sb.Append(cost.item.itemName);
                sb.Append(" x");
                sb.Append(cost.amount);
            }

            _cachedCostSummary = sb.Length > 0 ? sb.ToString() : "-";
        }

        private string ResolveLocalizedRecipeName(string fallback)
        {
            string resolved = localizedRecipeName.ResolveOrFallback(string.Empty);
            return !string.IsNullOrWhiteSpace(resolved) ? resolved : (fallback ?? string.Empty);
        }

        private string ResolveLocalizedRecipeDescription(string fallback)
        {
            string resolved = localizedRecipeDescription.ResolveOrFallback(string.Empty);
            return !string.IsNullOrWhiteSpace(resolved) ? resolved : (fallback ?? string.Empty);
        }
    }
}
