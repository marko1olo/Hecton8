using Hecton8.Building;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Meta;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Official owner for dismantling inventory items into supported resource yields.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6260)]
    [AddComponentMenu("Hecton8/Economy/Scrap Manager")]
    public sealed class ScrapManager : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private const float MaterialRecoveryRatio = 0.50f;
        private const float ComponentRecoveryRatio = 0.40f;
        private const float EquipmentRecoveryRatio = 0.25f;
        internal const int MaxRecycleYieldSlots = 16;

        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private IPlayerInventoryService _playerInventoryService;
        private readonly ResourceStack[] _processYieldScratch = new ResourceStack[MaxRecycleYieldSlots];

        private static ScrapManager s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_activeRuntimeInstance = null;
        }

        private void Awake()
        {
            TryAbortForUsableExistingRuntime();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterToGlobalRegistry();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterFromGlobalRegistry();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterFromGlobalRegistry();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.PlayerInventory)
                _playerInventoryService = currentService as IPlayerInventoryService;
        }

        private void TryRegisterToGlobalRegistry()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterScrapRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Scrap, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            ScrapManager active = s_activeRuntimeInstance;
            if (!ReferenceEquals(active, null) && !ReferenceEquals(active, this))
            {
                if (IsScrapRuntimeUsable(active))
                {
                    Destroy(gameObject);
                    return true;
                }

                if (ReferenceEquals(s_activeRuntimeInstance, active))
                    s_activeRuntimeInstance = null;
                if (ReferenceEquals(GlobalRegistry.Scrap, active))
                    GlobalRegistry.UnregisterScrapRuntime(active);
            }

            ScrapManager registered = GlobalRegistry.Scrap;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsScrapRuntimeUsable(registered))
            {
                s_activeRuntimeInstance = registered;
                Destroy(gameObject);
                return true;
            }

            if (ReferenceEquals(s_activeRuntimeInstance, registered))
                s_activeRuntimeInstance = null;
            GlobalRegistry.UnregisterScrapRuntime(registered);
            return false;
        }

        private static bool IsScrapRuntimeUsable(ScrapManager manager)
        {
            return manager != null &&
                   manager._serviceRegistered &&
                   manager.isActiveAndEnabled;
        }

        private void TryUnregisterFromGlobalRegistry()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterScrapRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

        /// <summary>
        /// Attempts to recycle one unit of the specified item from player inventory.
        /// </summary>
        /// <param name="legacyItemId">Stable authoring identifier. Converted once at the seam.</param>
        public bool ProcessRecycle(string legacyItemId)
        {
            if (string.IsNullOrWhiteSpace(legacyItemId))
                return false;

            return ProcessRecycle(unchecked((uint)LocHash.Compute(legacyItemId.Trim())));
        }

        /// <summary>
        /// Attempts to recycle one unit of the specified item hash from player inventory.
        /// </summary>
        public bool ProcessRecycle(uint targetHashId)
        {
            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null || inventory.ItemCatalog == null || targetHashId == 0u)
                return false;

            ItemData item = inventory.ItemCatalog.FindByHash(unchecked((int)targetHashId));
            return ProcessRecycle(item);
        }

        /// <summary>
        /// Attempts to recycle one unit of the specified item from player inventory.
        /// </summary>
        public bool ProcessRecycle(ItemData sourceItem)
        {
            if (sourceItem == null)
                return false;

            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null)
                return false;

            if (!TryBuildRecycleYieldSnapshot(sourceItem, _processYieldScratch, out int resolvedYieldCount))
                return false;

            try
            {
                if (!inventory.TryRemoveQuantity(sourceItem.PersistentHashId, 1))
                    return false;

                int grantedStackCount = 0;
                if (!GrantYield(inventory, _processYieldScratch, resolvedYieldCount, ref grantedStackCount))
                {
                    RollbackYield(inventory, _processYieldScratch, resolvedYieldCount, grantedStackCount);
                    inventory.TryAddItem(sourceItem.PersistentHashId, 1);
                    return false;
                }

                ItemLifecycleSignalRoute.TryPublishRecycled(sourceItem, 1, CountYieldUnits(_processYieldScratch, resolvedYieldCount));
                return true;
            }
            finally
            {
                ClearYieldScratch(_processYieldScratch, resolvedYieldCount);
            }
        }

        private void CacheRegistryServicesCold()
        {
            _playerInventoryService = Hecton8.Core.GlobalRegistry.PlayerInventory;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        internal static bool TryBuildRecycleYieldSnapshot(ItemData sourceItem, ResourceStack[] destination, out int resolvedCount)
        {
            uint unusedOverlayOwnerHash;
            return TryBuildRecycleYieldSnapshot(sourceItem, destination, out resolvedCount, out unusedOverlayOwnerHash);
        }

        internal static bool TryBuildRecycleYieldSnapshot(
            ItemData sourceItem,
            ResourceStack[] destination,
            out int resolvedCount,
            out uint overlayOwnerHash)
        {
            bool unusedRegisteredOverlay;
            return TryBuildRecycleYieldSnapshot(
                sourceItem,
                destination,
                out resolvedCount,
                out overlayOwnerHash,
                out unusedRegisteredOverlay);
        }

        internal static bool TryBuildRecycleYieldSnapshot(
            ItemData sourceItem,
            ResourceStack[] destination,
            out int resolvedCount,
            out uint overlayOwnerHash,
            out bool usedRegisteredOverlay)
        {
            resolvedCount = 0;
            overlayOwnerHash = 0u;
            usedRegisteredOverlay = false;
            if (sourceItem == null || destination == null || destination.Length == 0)
                return false;

            if (RecyclingRegistry.TryGetYield(
                    unchecked((uint)sourceItem.PersistentHashId),
                    out ResourceStack[] registeredYield,
                    out overlayOwnerHash))
            {
                usedRegisteredOverlay = true;
                return CopyYieldSnapshotNonAlloc(registeredYield, destination, out resolvedCount);
            }

            RecipeData recipe;
            if (!Fabricator.TryResolveRecipeForResultItem(sourceItem, out recipe) ||
                recipe == null ||
                recipe.ingredients == null ||
                recipe.ingredients.Count == 0)
            {
                return false;
            }

            float recoveryRatio = ResolveRecoveryRatio(sourceItem);
            int ingredientCount = recipe.ingredients.Count;

            for (int i = 0; i < ingredientCount; i++)
            {
                InventoryCost cost = recipe.ingredients[i];
                if (cost == null || cost.item == null || cost.amount <= 0)
                    continue;

                int recoveredAmount = Mathf.Clamp(Mathf.FloorToInt(cost.amount * recoveryRatio), 0, cost.amount);
                if (recoveredAmount <= 0)
                    continue;

                if (resolvedCount >= destination.Length)
                {
                    ClearYieldScratch(destination, resolvedCount);
                    resolvedCount = 0;
                    return false;
                }

                destination[resolvedCount] = new ResourceStack
                {
                    Item = cost.item,
                    Amount = recoveredAmount
                };
                resolvedCount++;
            }

            return resolvedCount > 0;
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
            return GrantYield(inventory, resolvedYield, resolvedYield != null ? resolvedYield.Length : 0, ref grantedStackCount);
        }

        internal static bool GrantYield(PlayerInventory inventory, ResourceStack[] resolvedYield, int resolvedYieldCount, ref int grantedStackCount)
        {
            if (inventory == null || resolvedYield == null)
                return false;

            int count = Mathf.Min(Mathf.Max(0, resolvedYieldCount), resolvedYield.Length);
            for (int i = 0; i < count; i++)
            {
                ResourceStack stack = resolvedYield[i];
                if (stack.Item == null || stack.Amount <= 0)
                    continue;

                for (int amountIndex = 0; amountIndex < stack.Amount; amountIndex++)
                {
                    if (!inventory.TryAddItem(stack.Item.PersistentHashId, 1))
                        return false;

                    grantedStackCount++;
                }
            }

            return true;
        }

        internal static void RollbackYield(PlayerInventory inventory, ResourceStack[] resolvedYield, int grantedStackCount)
        {
            RollbackYield(inventory, resolvedYield, resolvedYield != null ? resolvedYield.Length : 0, grantedStackCount);
        }

        internal static void RollbackYield(PlayerInventory inventory, ResourceStack[] resolvedYield, int resolvedYieldCount, int grantedStackCount)
        {
            if (inventory == null || resolvedYield == null || grantedStackCount <= 0)
                return;

            int remaining = grantedStackCount;
            int count = Mathf.Min(Mathf.Max(0, resolvedYieldCount), resolvedYield.Length);
            for (int i = 0; i < count && remaining > 0; i++)
            {
                ResourceStack stack = resolvedYield[i];
                if (stack.Item == null || stack.Amount <= 0)
                    continue;

                int rollbackAmount = Mathf.Min(stack.Amount, remaining);
                inventory.TryRemoveQuantity(stack.Item.PersistentHashId, rollbackAmount);
                remaining -= rollbackAmount;
            }
        }

        internal static int CountYieldUnits(ResourceStack[] resolvedYield)
        {
            return CountYieldUnits(resolvedYield, resolvedYield != null ? resolvedYield.Length : 0);
        }

        internal static int CountYieldUnits(ResourceStack[] resolvedYield, int resolvedYieldCount)
        {
            if (resolvedYield == null)
                return 0;

            int total = 0;
            int count = Mathf.Min(Mathf.Max(0, resolvedYieldCount), resolvedYield.Length);
            for (int i = 0; i < count; i++)
            {
                if (resolvedYield[i].Amount > 0)
                    total += resolvedYield[i].Amount;
            }

            return total;
        }

        private static bool CopyYieldSnapshotNonAlloc(ResourceStack[] source, ResourceStack[] destination, out int copiedCount)
        {
            copiedCount = 0;
            if (source == null || destination == null || destination.Length == 0)
                return false;

            for (int i = 0; i < source.Length; i++)
            {
                ResourceStack stack = source[i];
                if (stack.Item == null || stack.Amount <= 0)
                    continue;

                if (copiedCount >= destination.Length)
                {
                    ClearYieldScratch(destination, copiedCount);
                    copiedCount = 0;
                    return false;
                }

                destination[copiedCount] = stack;
                copiedCount++;
            }

            return copiedCount > 0;
        }

        internal static void ClearYieldScratch(ResourceStack[] scratch, int count)
        {
            if (scratch == null || count <= 0)
                return;

            int safeCount = Mathf.Min(count, scratch.Length);
            for (int i = 0; i < safeCount; i++)
                scratch[i] = default;
        }
    }
}
