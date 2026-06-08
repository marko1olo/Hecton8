using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Local planter that converts stored organic matter into continuous CO2 scrubbing for a linked base module.
    /// It does not invent a separate air owner; it only feeds the existing BaseModule life-support loop.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BotanyPlanterModule : MonoBehaviour, ISlowTickable, IInteractable, IInteractableTextProvider, IGlobalRegistryHotSwapListener
    {
        private const string DefaultEmptyText = "Botany Planter Empty";
        private const string DefaultLoadedText = "Botany Planter Active";
        private const int MaxPlanterSlots = 4;
        private const float SlowTickDt = 0.5f;

        [Header("── Target ───────────────────────────────")]
        [Tooltip("Base module whose CO2 loop is being scrubbed. Falls back to the nearest parent BaseModule.")]
        [SerializeField] private BaseModule targetModule;

        [Header("── Scrubbing ─────────────────────────────")]
        [Tooltip("CO2 converted per occupied plant slot every slow tick.")]
        [SerializeField, Range(0.1f, 10f)] private float scrubAmountPerPlant = 1.5f;

        [Tooltip("Number of active plant slots.")]
        [SerializeField, Range(1, MaxPlanterSlots)] private int slotCount = MaxPlanterSlots;

        [Header("── Diagnostics ───────────────────────────")]
        [SerializeField] private int _debugPlantCount;
        [SerializeField] private float _debugLastScrubAmount;

        private readonly ItemData[] _plantedItems = new ItemData[MaxPlanterSlots];
        private readonly int[] _plantedQuantities = new int[MaxPlanterSlots];
        private CultivationManager _cultivationManager;
        private IPlayerInventoryService _cachedInventoryService;
        private bool _hotSwapListenerRegistered;
        private bool _registered;

        private void Awake()
        {
            if (targetModule == null)
                ConstructionParentLookup.TryCaptureSelfOrParent(this, out targetModule);

            if (_cultivationManager == null)
                TryGetComponent(out _cultivationManager);

            if (slotCount < 1)
                slotCount = 1;
            else if (slotCount > MaxPlanterSlots)
                slotCount = MaxPlanterSlots;
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            InteractableRegistry.RegisterTree(this);
            TryRegister();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _cachedInventoryService = currentService as IPlayerInventoryService;
                    break;
            }
        }

        public void SlowTick()
        {
            if (_cultivationManager != null)
            {
                _debugPlantCount = _cultivationManager.OccupiedSlotCount;
                _debugLastScrubAmount = 0f;
                return;
            }

            if (targetModule == null)
                return;

            int plantCount = CountPlantedItems();
            _debugPlantCount = plantCount;
            if (plantCount <= 0)
            {
                _debugLastScrubAmount = 0f;
                return;
            }

            float scrubAmount = plantCount * scrubAmountPerPlant * SlowTickDt;
            targetModule.ApplyBotanyScrub(scrubAmount);
            _debugLastScrubAmount = scrubAmount;
        }

        void IInteractable.OnHoverStart()
        {
        }

        void IInteractable.OnHoverEnd()
        {
        }

        void IInteractable.Interact(Transform interactor)
        {
        }

        string IInteractable.GetInteractText()
        {
            return CountPlantedItems() > 0 ? DefaultLoadedText : DefaultEmptyText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(CountPlantedItems() > 0 ? DefaultLoadedText : DefaultEmptyText, destination, out length);
        }

        /// <summary>
        /// UI / interaction bridge for future planter inventory UI. Moves organic items into planter slots.
        /// </summary>
        public bool TryInsertFromInventory(PlayerInventory inventory, ItemData item, int quantity = 1)
        {
            if (_cultivationManager != null)
                return _cultivationManager.TryInsertFromInventory(inventory, item, quantity);

            if (inventory == null || item == null || quantity <= 0 || !IsValidPlantItem(item))
                return false;

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId == 0)
                return false;

            int inserted = 0;
            int desired = Mathf.Max(1, quantity);
            for (int i = 0; i < desired; i++)
            {
                if (!TryPlantItem(item))
                    break;

                if (!inventory.TryRemoveQuantity(itemHashId, 1))
                {
                    RemoveLastPlantedItem(item);
                    break;
                }

                inserted++;
            }

            _debugPlantCount = CountPlantedItems();
            return inserted > 0;
        }

        /// <summary>
        /// Copies the current planted-item snapshot into caller-owned buffers for lightweight UI rendering.
        /// </summary>
        public int CopyBufferSnapshot(ItemData[] items, int[] quantities)
        {
            IPlayerInventoryService inventoryService = _cachedInventoryService;
            PlayerInventory inventory = inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
            if (_cultivationManager != null)
                return _cultivationManager.CopyBufferSnapshot(items, quantities, inventory != null ? inventory.ItemCatalog : null);

            if (items == null || quantities == null)
                return 0;

            int maxCount = items.Length < quantities.Length ? items.Length : quantities.Length;
            int copied = 0;
            for (int i = 0; i < slotCount && copied < maxCount; i++)
            {
                if (_plantedItems[i] == null || _plantedQuantities[i] <= 0)
                    continue;

                items[copied] = _plantedItems[i];
                quantities[copied] = _plantedQuantities[i];
                copied++;
            }

            return copied;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedInventoryService = GlobalRegistry.PlayerInventory;
        }

        private void ClearCachedRegistryServices()
        {
            _cachedInventoryService = null;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private int CountPlantedItems()
        {
            int count = 0;
            for (int i = 0; i < slotCount; i++)
            {
                count += _plantedQuantities[i];
            }

            return count;
        }

        private bool TryPlantItem(ItemData item)
        {
            for (int i = 0; i < slotCount; i++)
            {
                if (ReferenceEquals(_plantedItems[i], item))
                {
                    _plantedQuantities[i]++;
                    return true;
                }
            }

            for (int i = 0; i < slotCount; i++)
            {
                if (_plantedItems[i] != null)
                    continue;

                _plantedItems[i] = item;
                _plantedQuantities[i] = 1;
                return true;
            }

            return false;
        }

        private void RemoveLastPlantedItem(ItemData item)
        {
            for (int i = slotCount - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_plantedItems[i], item) || _plantedQuantities[i] <= 0)
                    continue;

                _plantedQuantities[i]--;
                if (_plantedQuantities[i] <= 0)
                {
                    _plantedQuantities[i] = 0;
                    _plantedItems[i] = null;
                }

                return;
            }
        }

        private static bool IsValidPlantItem(ItemData item)
        {
            if (item == null)
                return false;

            return item.category == ItemCategory.Organic ||
                   item.resourceFamily == ResourceFamily.Organic;
        }
    }
}
