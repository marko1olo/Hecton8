using Hecton8.Building;
using Hecton8.Crafting;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton8.Modding;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Official owner for dismantling inventory items into supported resource yields.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6260)]
    [AddComponentMenu("Hecton8/Economy/Scrap Manager")]
    public sealed class ScrapManager : MonoBehaviour
    {
        private const float MaterialRecoveryRatio = 0.50f;
        private const float ComponentRecoveryRatio = 0.40f;
        private const float EquipmentRecoveryRatio = 0.25f;

        private static ScrapManager _instance;

        /// <summary>
        /// Active runtime owner while the gameplay scene is loaded.
        /// </summary>
        public static ScrapManager Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Attempts to recycle one unit of the specified item from player inventory.
        /// </summary>
        /// <param name="itemId">Stable item identifier stored in the runtime item catalog.</param>
        public bool ProcessRecycle(string itemId)
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null || inventory.ItemCatalog == null || string.IsNullOrWhiteSpace(itemId))
                return false;

            ItemData item = inventory.ItemCatalog.FindById(itemId.Trim());
            return ProcessRecycle(item);
        }

        /// <summary>
        /// Attempts to recycle one unit of the specified item from player inventory.
        /// </summary>
        public bool ProcessRecycle(ItemData sourceItem)
        {
            if (sourceItem == null)
                return false;

            PlayerInventory inventory = PlayerInventory.Instance;
            if (inventory == null)
                return false;

            if (!TryResolveRecycleYield(sourceItem, out ResourceStack[] resolvedYield))
                return false;

            if (!inventory.TryRemoveQuantity(Hecton.Localization.LocHash.Compute(sourceItem.PersistentId), 1))
                return false;

            int grantedStackCount = 0;
            if (!GrantYield(inventory, resolvedYield, ref grantedStackCount))
            {
                RollbackYield(inventory, resolvedYield, grantedStackCount);
                inventory.TryAddItem(Hecton.Localization.LocHash.Compute(sourceItem.PersistentId), 1);
                return false;
            }

            HectonEventBus.Publish(new ItemRecycledEvent(sourceItem, 1, CountYieldUnits(resolvedYield)));
            return true;
        }

        internal static bool TryResolveRecycleYield(ItemData sourceItem, out ResourceStack[] resolvedYield)
        {
            if (sourceItem == null)
            {
                resolvedYield = null;
                return false;
            }

            if (RecyclingRegistry.TryGetYield(sourceItem.PersistentId, out resolvedYield) &&
                resolvedYield != null &&
                resolvedYield.Length > 0)
            {
                return true;
            }

            RecipeData recipe;
            if (!Fabricator.TryResolveRecipeForResultItem(sourceItem, out recipe) ||
                recipe == null ||
                recipe.ingredients == null ||
                recipe.ingredients.Count == 0)
            {
                resolvedYield = null;
                return false;
            }

            float recoveryRatio = ResolveRecoveryRatio(sourceItem);
            int ingredientCount = recipe.ingredients.Count;
            ResourceStack[] autoYield = new ResourceStack[ingredientCount]; // COLD ALLOC: ResourceStack[ingredientCount] - cold-path recycle yield derivation - owner: ScrapManager
            int resolvedCount = 0;

            for (int i = 0; i < ingredientCount; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int recoveredAmount = Mathf.Clamp(Mathf.FloorToInt(cost.amount * recoveryRatio), 0, cost.amount);
                if (recoveredAmount <= 0)
                    continue;

                autoYield[resolvedCount] = new ResourceStack
                {
                    Item = cost.item,
                    Amount = recoveredAmount
                };
                resolvedCount++;
            }

            if (resolvedCount <= 0)
            {
                resolvedYield = null;
                return false;
            }

            if (resolvedCount == autoYield.Length)
            {
                resolvedYield = autoYield;
                return true;
            }

            ResourceStack[] compactYield = new ResourceStack[resolvedCount]; // COLD ALLOC: ResourceStack[resolvedCount] - compact recycle yield snapshot - owner: ScrapManager
            System.Array.Copy(autoYield, compactYield, resolvedCount);
            resolvedYield = compactYield;
            return true;
        }

        private static float ResolveRecoveryRatio(ItemData sourceItem)
        {
            float baseRatio;
            switch (sourceItem.category)
            {
                case ItemCategory.Material:
                    baseRatio = MaterialRecoveryRatio;
                    break;
                case ItemCategory.Component:
                    baseRatio = ComponentRecoveryRatio;
                    break;
                default:
                    baseRatio = EquipmentRecoveryRatio;
                    break;
            }

            int efficiencyLevel = MetaProfileUtility.ResolveUpgradeLevel(MetaUpgradeRegistry.EfficiencyExpertId);
            float bonusPerLevel = 0.05f;
            if (MetaUpgradeRegistry.TryGetDefinition(MetaUpgradeRegistry.EfficiencyExpertId, out MetaUpgradeRegistry.MetaUpgradeDefinition definition) &&
                definition.RecycleYieldBonusPerLevel > 0f)
            {
                bonusPerLevel = definition.RecycleYieldBonusPerLevel;
            }

            float bonus = efficiencyLevel * bonusPerLevel;
            return Mathf.Clamp(baseRatio + bonus, 0.25f, 0.80f);
        }

        internal static bool GrantYield(PlayerInventory inventory, ResourceStack[] resolvedYield, ref int grantedStackCount)
        {
            if (inventory == null || resolvedYield == null)
                return false;

            for (int i = 0; i < resolvedYield.Length; i++)
            {
                ResourceStack stack = resolvedYield[i];
                if (stack.Item == null || stack.Amount <= 0)
                    continue;

                for (int amountIndex = 0; amountIndex < stack.Amount; amountIndex++)
                {
                    if (!inventory.TryAddItem(Hecton.Localization.LocHash.Compute(stack.Item.PersistentId), 1))
                        return false;

                    grantedStackCount++;
                }
            }

            return true;
        }

        internal static void RollbackYield(PlayerInventory inventory, ResourceStack[] resolvedYield, int grantedStackCount)
        {
            if (inventory == null || resolvedYield == null || grantedStackCount <= 0)
                return;

            int remaining = grantedStackCount;
            for (int i = 0; i < resolvedYield.Length && remaining > 0; i++)
            {
                ResourceStack stack = resolvedYield[i];
                if (stack.Item == null || stack.Amount <= 0)
                    continue;

                int rollbackAmount = Mathf.Min(stack.Amount, remaining);
                inventory.TryRemoveQuantity(Hecton.Localization.LocHash.Compute(stack.Item.PersistentId), rollbackAmount);
                remaining -= rollbackAmount;
            }
        }

        internal static int CountYieldUnits(ResourceStack[] resolvedYield)
        {
            if (resolvedYield == null)
                return 0;

            int total = 0;
            for (int i = 0; i < resolvedYield.Length; i++)
            {
                if (resolvedYield[i].Amount > 0)
                    total += resolvedYield[i].Amount;
            }

            return total;
        }
    }
}
