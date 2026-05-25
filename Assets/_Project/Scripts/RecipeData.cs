using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
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
        public List<InventoryCost> ingredients = new List<InventoryCost>(4);

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
        private uint _requiredScanEntryHash;
        private int _requiredAnchoredBiomeFamilyHashId;
        private ulong _recipeMask;

        private void OnEnable()
        {
            RefreshRuntimeHashes();
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

            RefreshRuntimeHashes();
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
            return _cachedCraftText ?? string.Empty;
        }

        public bool TryWriteDisplayNameOrFallback(char[] destination, out int length)
        {
            string fallback = resultItem != null && !string.IsNullOrWhiteSpace(resultItem.itemName)
                ? resultItem.itemName
                : recipeName;
            return localizedRecipeName.TryCopyResolvedOrFallback(
                Hecton8.Core.GlobalRegistry.LocalizationText,
                destination,
                out length,
                fallback);
        }

        /// <summary>
        /// Localized cost summary used by prompt-style UI.
        /// </summary>
        public string GetCostSummary()
        {
            return _cachedCostSummary ?? "-";
        }

        /// <summary>
        /// Icon shown for the recipe.
        /// </summary>
        public Sprite Icon => overrideIcon != null
            ? overrideIcon
            : (resultItem != null ? resultItem.icon : null);

        public bool RequiresScanUnlock => _requiredScanEntryHash != 0u;
        public bool RequiresAnchoredBiomeLock => requiresAnchoredBaseBiome &&
                                                 (requiredAnchoredBiomeMatrixId > 0 ||
                                                  !string.IsNullOrWhiteSpace(requiredAnchoredBiomeFamilyId));

        public string RequiredScanEntryId => string.IsNullOrWhiteSpace(requiredScanEntryId)
            ? string.Empty
            : requiredScanEntryId.Trim();
        public uint RequiredScanEntryHash => _requiredScanEntryHash;
        public int RequiredAnchoredBiomeFamilyHashId => _requiredAnchoredBiomeFamilyHashId;
        public ulong RecipeMask => _recipeMask;

        public bool IsUnlocked(IScanLogService scanLogSystem)
        {
            if (!RequiresScanUnlock)
                return true;

            return scanLogSystem != null && scanLogSystem.ContainsEntry(_requiredScanEntryHash);
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
            switch (GetResolvedFabricationGroup())
            {
                case FabricationGroup.Materials:
                    return "MATERIALS";
                case FabricationGroup.Components:
                    return "COMPONENTS";
                case FabricationGroup.Tools:
                    return "TOOLS";
                case FabricationGroup.Suit:
                    return "SUIT";
                case FabricationGroup.Construction:
                    return "CONSTRUCTION";
                case FabricationGroup.Power:
                    return "POWER";
                default:
                    return "ALL";
            }
        }

        private void RebuildCache()
        {
            // Cold cache build only. Runtime UI getters return these references without rebuilding strings.
            _cachedCraftText = "Create";
            _cachedCostSummary = "-";
        }

        private void RefreshRuntimeHashes()
        {
            _requiredScanEntryHash = ScanEvents.ComputeEntryHash(requiredScanEntryId);
            _requiredAnchoredBiomeFamilyHashId = LocHash.ComputeAsciiLowerInvariant(requiredAnchoredBiomeFamilyId);
            _recipeMask = BuildRecipeMask();
        }

        private ulong BuildRecipeMask()
        {
            ulong mask = 0UL;
            int ingredientCount = ingredients != null ? ingredients.Count : 0;
            for (int ingredientIndex = 0; ingredientIndex < ingredientCount; ingredientIndex++)
            {
                InventoryCost cost = ingredients[ingredientIndex];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                mask |= InventoryMaterialMask.ResolveBit(cost.item.PersistentHashId);
            }

            return mask;
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
