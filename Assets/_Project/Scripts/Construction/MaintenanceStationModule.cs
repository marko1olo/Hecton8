using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.Tools;
using UnityEngine;

namespace Hecton8.Construction
{
    /// <summary>
    /// Powered maintenance bay that repairs one inserted tool item over time using the existing tool durability owner
    /// plus transactional resource reservations from the base logistics network.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    public sealed class MaintenanceStationModule : MonoBehaviour, ITickable, IUpdatable, IPoolable, IPowerComponent, IInteractable, IInteractableTextProvider, IGlobalRegistryHotSwapListener
    {
        private const string DefaultTitaniumRepairItemId = "Data_TitaniumScrap";
        private const string DefaultLubricantItemId = "Comp_LubricantResin";
        private const string EmptyPrompt = "Maintenance Station Empty";
        private const string RepairingPrompt = "Tool Under Maintenance";
        private const string ReadyPrompt = "Retrieve Serviced Tool";
        private const float ReservationRetryIntervalSeconds = 0.5f;
        private const int RepairReservationCostCapacity = 4;

        [Header("── Repair Bay ─────────────────")]
        [Tooltip("Seconds required to fully restore a tool from zero durability to 100%.")]
        [SerializeField, Range(1f, 120f)] private float fullRepairDuration = 20f;

        [Tooltip("Normalized station standby draw while a tool is inserted but idle.")]
        [SerializeField, Range(0f, 100f)] private float standbyPowerDraw = 3f;

        [Tooltip("Additional power draw while the station is actively repairing a tool.")]
        [SerializeField, Range(0f, 200f)] private float activeRepairPowerDraw = 45f;

        [Tooltip("Priority used by the power grid while balancing this repair bay.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 40;

        [Header("── Resource Costs ─────────────")]
        [Tooltip("Fallback structural repair material used when tool metadata does not resolve a valid repair item.")]
        [SerializeField] private ItemData fallbackStructuralRepairItem;

        [Tooltip("Secondary lubricant consumed once per maintenance session.")]
        [SerializeField] private ItemData lubricantRepairItem;

        [Tooltip("Minimum structural repair units reserved for any non-trivial service cycle.")]
        [SerializeField, Range(1, 16)] private int minimumStructuralCost = 1;

        [Tooltip("Lubricant units reserved for each repair session.")]
        [SerializeField, Range(0, 8)] private int lubricantCostPerSession = 1;

        [Header("── References ─────────────────")]
        [Tooltip("Optional cached tool manager used to resolve item -> tool metadata and to holster an equipped tool before service.")]
        [SerializeField] private PlayerToolManager playerToolManager;

        [Header("── Diagnostics ───────────────")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugIsRepairing;
        [SerializeField] private string _debugToolId;
        [SerializeField] private float _debugDurabilityNormalized;

        // COLD ALLOC: int[4] — maintenance repair item hash cost buffer — owner: MaintenanceStationModule
        private readonly int[] _reservationCostHashIds = new int[RepairReservationCostCapacity];
        // COLD ALLOC: int[4] — maintenance repair item quantity cost buffer — owner: MaintenanceStationModule
        private readonly int[] _reservationCostAmounts = new int[RepairReservationCostCapacity];

        private PowerNode _powerNode;
        private bool _registered;
        private bool _hasPower = true;
        private ItemData _slottedToolItem;
        private ToolMetadata _slottedToolMetadata;
        private uint _slottedToolItemHashId;
        private int _fallbackStructuralRepairItemHashId;
        private int _lubricantRepairItemHashId;
        private int _slottedStructuralRepairItemHashId;
        private int _slottedLubricantRepairItemHashId;
        private BaseLogisticsNetwork.LogisticsReservation _activeReservation;
        private float _repairTargetDurability;
        private float _reservationRetryCooldownSeconds;
        private int _reservationCostCount;
        private bool _reservationCostOverflowed;
        private bool _isRepairing;
        private IToolDurabilityService _toolDurabilitySystem;
        private IPlayerInventoryService _playerInventoryService;
        private bool _toolManagerFromRegistry;
        private bool _hotSwapRegistered;

        /// <summary>Idle bay draw plus active repair draw when service is in progress.</summary>
        public float PowerRating
        {
            get
            {
                if (_slottedToolItem == null)
                    return 0f;

                float draw = standbyPowerDraw;
                if (_isRepairing)
                    draw += activeRepairPowerDraw;

                return -draw;
            }
        }

        /// <summary>Grid shedding priority for this maintenance bay.</summary>
        public int PowerPriority => powerPriority;

        /// <summary>Cached power state from the owning base grid.</summary>
        public bool HasPower => _hasPower;
        internal bool HasSlottedTool => _slottedToolItem != null;
        internal string SlottedToolPersistentId => _slottedToolItem != null ? _slottedToolItem.PersistentId : string.Empty;
        internal bool DebugIsRepairing => _debugIsRepairing;

        private void Awake()
        {
            TryGetComponent(out _powerNode);
            CacheRegistryServicesCold();
            ResolveFallbackItems();
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
            CancelActiveRepair();
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            CancelActiveRepair();
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            _reservationRetryCooldownSeconds = 0f;
            ClearSlotState();
            CacheRegistryServicesCold();
            ResolveFallbackItems();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        public void OnDespawn()
        {
            CancelActiveRepair();
            ClearSlotState();
            _hasPower = true;
            _debugHasPower = true;
            _reservationRetryCooldownSeconds = 0f;
            TryUnregister();
            TryUnregisterHotSwapListener();
        }

        public void Tick(float deltaTime)
        {
            if (_slottedToolItem == null || _slottedToolMetadata == null)
            {
                _debugIsRepairing = false;
                _debugDurabilityNormalized = 0f;
                return;
            }

            IToolDurabilityService durabilitySystem = _toolDurabilitySystem;
            if (durabilitySystem == null || string.IsNullOrWhiteSpace(_slottedToolMetadata.toolID))
            {
                _debugIsRepairing = false;
                _debugDurabilityNormalized = 0f;
                return;
            }

            float maxDurability = Mathf.Max(1f, _slottedToolMetadata.maxDurability);
            float currentDurability = ReadSlottedDurability(durabilitySystem, maxDurability);
            _debugDurabilityNormalized = currentDurability / maxDurability;

            if (currentDurability >= maxDurability - 0.001f)
            {
                if (_isRepairing)
                    CompleteActiveRepair();

                _debugIsRepairing = false;
                return;
            }

            if (!_hasPower)
            {
                _debugIsRepairing = false;
                return;
            }

            if (_activeReservation == null)
            {
                if (_reservationRetryCooldownSeconds > 0f)
                {
                    _reservationRetryCooldownSeconds = Mathf.Max(0f, _reservationRetryCooldownSeconds - deltaTime);
                    _debugIsRepairing = false;
                    return;
                }

                if (!TryPrepareRepairReservation(currentDurability, maxDurability))
                {
                    _reservationRetryCooldownSeconds = ReservationRetryIntervalSeconds;
                    _debugIsRepairing = false;
                    return;
                }
            }

            float targetDurability = _repairTargetDurability > currentDurability + 0.001f
                ? _repairTargetDurability
                : maxDurability;

            float missingDurability = targetDurability - currentDurability;
            if (missingDurability <= 0.001f)
            {
                CompleteActiveRepair();
                return;
            }

            float normalizedMissing = missingDurability / maxDurability;
            float duration = Mathf.Max(0.1f, fullRepairDuration * Mathf.Clamp(normalizedMissing, 0.1f, 1f));
            float repairDelta = maxDurability * (deltaTime / duration);
            if (_slottedToolItemHashId == 0u || !durabilitySystem.TryRepairTool(_slottedToolItemHashId, repairDelta, maxDurability))
            {
                _isRepairing = false;
                _debugIsRepairing = false;
                return;
            }

            _isRepairing = true;
            _debugIsRepairing = true;

            if (ReadSlottedDurability(durabilitySystem, maxDurability) >= targetDurability - 0.001f)
                CompleteActiveRepair();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            _reservationRetryCooldownSeconds = 0f;
            if (!hasPower)
                _debugIsRepairing = false;
        }

        void IInteractable.OnHoverStart()
        {
        }

        void IInteractable.OnHoverEnd()
        {
        }

        void IInteractable.Interact(Transform interactor)
        {
            PlayerInventory inventory = ResolvePlayerInventory();
            if (inventory == null)
                return;

            if (_slottedToolItem == null)
                TryInsertFirstRepairableTool(inventory);
            else
                TryReturnToolToInventory(inventory);
        }

        string IInteractable.GetInteractText()
        {
            if (_slottedToolItem == null)
                return EmptyPrompt;

            return _isRepairing ? RepairingPrompt : ReadyPrompt;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            string source = _slottedToolItem == null ? EmptyPrompt : (_isRepairing ? RepairingPrompt : ReadyPrompt);
            return InteractableTextCopy.TryCopy(source, destination, out length);
        }

        /// <summary>
        /// Explicit UI bridge for future station panels. Moves one tool item from the player inventory into the service slot.
        /// </summary>
        public bool TryInsertFromInventory(PlayerInventory inventory, ItemData item)
        {
            if (_slottedToolItem != null || inventory == null || item == null)
                return false;

            ToolMetadata metadata;
            if (!TryResolveToolMetadata(item, out metadata))
                return false;

            IToolDurabilityService durabilitySystem = _toolDurabilitySystem;
            if (durabilitySystem == null || string.IsNullOrWhiteSpace(metadata.toolID))
                return false;

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId == 0)
                return false;

            float maxDurability = Mathf.Max(1f, metadata.maxDurability);
            uint itemHash = unchecked((uint)itemHashId);
            durabilitySystem.RegisterCentralizedEquipmentMirror(metadata.toolID, itemHash, maxDurability);
            float currentDurability = durabilitySystem.GetDurability(itemHash, maxDurability);
            if (currentDurability >= maxDurability - 0.001f)
                return false;

            CacheRegistryServicesCold();
            if (playerToolManager != null &&
                playerToolManager.CurrentTool != null &&
                ReferenceEquals(playerToolManager.CurrentTool.ToolData, item))
            {
                playerToolManager.Holster();
            }

            if (!inventory.TryRemoveQuantity(itemHashId, 1))
                return false;

            _slottedToolItem = item;
            _slottedToolMetadata = metadata;
            _slottedToolItemHashId = itemHash;
            CacheSlottedRepairCostHashes(inventory.ItemCatalog);
            RegisterSlottedToolDurabilityMirror(durabilitySystem);
            _repairTargetDurability = maxDurability;
            _debugToolId = metadata.toolID;
            _debugDurabilityNormalized = currentDurability / maxDurability;
            return true;
        }

        /// <summary>
        /// Explicit UI bridge for future station panels. Returns the serviced tool back to player inventory.
        /// </summary>
        public bool TryReturnToolToInventory(PlayerInventory inventory)
        {
            if (_slottedToolItem == null || inventory == null)
                return false;

            CancelActiveRepair();

            int itemHashId = _slottedToolItemHashId != 0u
                ? unchecked((int)_slottedToolItemHashId)
                : ItemData.ResolvePersistentHashId(_slottedToolItem);
            if (itemHashId == 0 || !inventory.TryAddItem(itemHashId, 1))
                return false;

            ClearSlotState();
            return true;
        }

        /// <summary>
        /// Restores a serialized tool slot directly into the station without touching player inventory state.
        /// </summary>
        internal bool TryRestoreSlottedTool(ItemData item)
        {
            if (_slottedToolItem != null || item == null)
                return false;

            ToolMetadata metadata;
            if (!TryResolveToolMetadata(item, out metadata))
                return false;

            CacheRegistryServicesCold();
            ResolveFallbackItems();

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId == 0)
                return false;

            CancelActiveRepair();
            _slottedToolItem = item;
            _slottedToolMetadata = metadata;
            _slottedToolItemHashId = unchecked((uint)itemHashId);
            _repairTargetDurability = Mathf.Max(1f, metadata.maxDurability);
            _debugToolId = metadata.toolID;
            CacheSlottedRepairCostHashes(ResolveItemCatalog());

            IToolDurabilityService durabilitySystem = _toolDurabilitySystem;
            if (durabilitySystem != null && !string.IsNullOrWhiteSpace(metadata.toolID))
            {
                float maxDurability = Mathf.Max(1f, metadata.maxDurability);
                RegisterSlottedToolDurabilityMirror(durabilitySystem);
                float currentDurability = ReadSlottedDurability(durabilitySystem, maxDurability);
                _debugDurabilityNormalized = currentDurability / maxDurability;
            }
            else
            {
                _debugDurabilityNormalized = 1f;
            }

            _isRepairing = false;
            _debugIsRepairing = false;
            return true;
        }

        internal bool TryExtractSlottedToolHashForDeconstruct(out int itemHashId)
        {
            if (!TryPeekSlottedToolHashForDeconstruct(out itemHashId))
                return false;

            CancelActiveRepair();
            ClearSlotState();
            return true;
        }

        internal bool TryPeekSlottedToolHashForDeconstruct(out int itemHashId)
        {
            itemHashId = _slottedToolItemHashId != 0u
                ? unchecked((int)_slottedToolItemHashId)
                : ItemData.ResolvePersistentHashId(_slottedToolItem);
            return itemHashId != 0;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
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
                case GlobalRegistryServiceSlot.ToolDurabilityRuntime:
                    _toolDurabilitySystem = currentService as IToolDurabilityService;
                    RegisterSlottedToolDurabilityMirror(_toolDurabilitySystem);
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    _playerInventoryService = currentService as IPlayerInventoryService;
                    ResolveFallbackItems();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    RebindPlayerToolManager(currentService as IPlayerRuntimeContext);
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _toolDurabilitySystem = GlobalRegistry.ToolDurabilityService;
            _playerInventoryService = GlobalRegistry.PlayerInventory;
            RebindPlayerToolManager(GlobalRegistry.Player);
        }

        private void RebindPlayerToolManager(IPlayerRuntimeContext playerContext)
        {
            if (playerToolManager != null && !_toolManagerFromRegistry)
                return;

            playerToolManager = playerContext != null ? playerContext.ToolManager : null;
            _toolManagerFromRegistry = playerToolManager != null;
        }

        private void ResolveFallbackItems()
        {
            ItemCatalog catalog = ResolveItemCatalog();

            if (fallbackStructuralRepairItem == null && catalog != null)
                fallbackStructuralRepairItem = catalog.FindById(DefaultTitaniumRepairItemId);

            if (lubricantRepairItem == null && catalog != null)
                lubricantRepairItem = catalog.FindById(DefaultLubricantItemId);

            _fallbackStructuralRepairItemHashId = ResolveItemHash(fallbackStructuralRepairItem);
            _lubricantRepairItemHashId = ResolveItemHash(lubricantRepairItem);
            CacheSlottedRepairCostHashes(catalog);
        }

        private bool TryInsertFirstRepairableTool(PlayerInventory inventory)
        {
            InventoryGrid grid = inventory.Grid;
            if (grid == null)
                return false;

            int cols = grid.Columns;
            int rows = grid.Rows;
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int anchorIndex = grid.GetCellAnchorIndex(x, y);
                    if (anchorIndex < 0 || anchorIndex != y * cols + x)
                        continue;

                    int itemHashId = inventory.GetItemHashAt(x, y);
                    ItemData item = itemHashId != 0 && inventory.ItemCatalog != null
                        ? inventory.ItemCatalog.FindByHash(itemHashId)
                        : null;
                    if (item == null)
                        continue;

                    if (TryInsertFromInventory(inventory, item))
                        return true;
                }
            }

            return false;
        }

        private bool TryResolveToolMetadata(ItemData item, out ToolMetadata metadata)
        {
            metadata = null;
            CacheRegistryServicesCold();
            if (item == null || playerToolManager == null)
                return false;

            GameObject toolPrefab = playerToolManager.GetKnownToolPrefabForItem(item);
            if (toolPrefab == null || !toolPrefab.TryGetComponent(out PlayerTool tool))
                return false;

            metadata = tool.Metadata;
            return metadata != null && !string.IsNullOrWhiteSpace(metadata.toolID);
        }

        private bool TryPrepareRepairReservation(float currentDurability, float maxDurability)
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid == null)
                return false;

            ClearReservationCostBuffer();
            PopulateRepairCosts(currentDurability, maxDurability);
            if (_reservationCostOverflowed || _reservationCostCount <= 0)
                return false;

            if (!BaseLogisticsNetwork.TryReserveResources(
                    grid,
                    _reservationCostHashIds,
                    _reservationCostAmounts,
                    _reservationCostCount,
                    out _activeReservation))
            {
                return false;
            }

            BaseLogisticsNetwork.CommitReserved(_activeReservation);
            _activeReservation = null;
            _repairTargetDurability = maxDurability;
            _reservationRetryCooldownSeconds = 0f;
            return true;
        }

        private PlayerInventory ResolvePlayerInventory()
        {
            IPlayerInventoryService inventoryService = _playerInventoryService;
            return inventoryService != null && inventoryService.IsInitialized
                ? inventoryService.Inventory
                : null;
        }

        private ItemCatalog ResolveItemCatalog()
        {
            PlayerInventory inventory = ResolvePlayerInventory();
            return inventory != null ? inventory.ItemCatalog : null;
        }

        private void PopulateRepairCosts(float currentDurability, float maxDurability)
        {
            float missingRatio = 1f - Mathf.Clamp01(currentDurability / Mathf.Max(1f, maxDurability));
            if (missingRatio <= 0.0001f)
                return;

            int structuralCost = Mathf.Max(
                minimumStructuralCost,
                Mathf.CeilToInt(Mathf.Max(1f, _slottedToolMetadata != null ? _slottedToolMetadata.repairCostFull : 1f) * missingRatio));

            AppendRepairCostHash(_slottedStructuralRepairItemHashId, structuralCost);

            if (_slottedLubricantRepairItemHashId != 0 && lubricantCostPerSession > 0)
                AppendRepairCostHash(_slottedLubricantRepairItemHashId, lubricantCostPerSession);
        }

        private void ClearReservationCostBuffer()
        {
            for (int i = 0; i < _reservationCostCount; i++)
            {
                _reservationCostHashIds[i] = 0;
                _reservationCostAmounts[i] = 0;
            }

            _reservationCostCount = 0;
            _reservationCostOverflowed = false;
        }

        private void AppendRepairCostHash(int itemHashId, int amount)
        {
            if (itemHashId == 0 || amount <= 0)
                return;

            for (int i = 0; i < _reservationCostCount; i++)
            {
                if (_reservationCostHashIds[i] != itemHashId)
                    continue;

                _reservationCostAmounts[i] += amount;
                return;
            }

            if (_reservationCostCount >= RepairReservationCostCapacity)
            {
                _reservationCostOverflowed = true;
                return;
            }

            _reservationCostHashIds[_reservationCostCount] = itemHashId;
            _reservationCostAmounts[_reservationCostCount] = amount;
            _reservationCostCount++;
        }

        private void CacheSlottedRepairCostHashes(ItemCatalog catalog)
        {
            _slottedStructuralRepairItemHashId = 0;
            _slottedLubricantRepairItemHashId = lubricantCostPerSession > 0 ? _lubricantRepairItemHashId : 0;

            if (_slottedToolMetadata == null)
                return;

            if (!string.IsNullOrWhiteSpace(_slottedToolMetadata.repairResourceID) && catalog != null)
            {
                ItemData authoredItem = catalog.FindById(_slottedToolMetadata.repairResourceID);
                _slottedStructuralRepairItemHashId = ResolveItemHash(authoredItem);
            }

            if (_slottedStructuralRepairItemHashId == 0)
                _slottedStructuralRepairItemHashId = _fallbackStructuralRepairItemHashId;
        }

        private static int ResolveItemHash(ItemData item)
        {
            return ItemData.ResolvePersistentHashId(item);
        }

        private void CompleteActiveRepair()
        {
            IToolDurabilityService durabilitySystem = _toolDurabilitySystem;
            if (durabilitySystem != null && _slottedToolMetadata != null && _slottedToolItemHashId != 0u)
                durabilitySystem.TryRepairToolFull(_slottedToolItemHashId, Mathf.Max(1f, _slottedToolMetadata.maxDurability));

            if (_activeReservation != null)
            {
                BaseLogisticsNetwork.CommitReserved(_activeReservation);
                _activeReservation = null;
            }

            _isRepairing = false;
            _debugIsRepairing = false;
            _debugDurabilityNormalized = 1f;
        }

        private void CancelActiveRepair()
        {
            if (_activeReservation != null)
            {
                BaseLogisticsNetwork.RollbackReserved(_activeReservation);
                _activeReservation = null;
            }

            _isRepairing = false;
            _debugIsRepairing = false;
        }

        private void ClearSlotState()
        {
            _slottedToolItem = null;
            _slottedToolMetadata = null;
            _slottedToolItemHashId = 0u;
            _slottedStructuralRepairItemHashId = 0;
            _slottedLubricantRepairItemHashId = 0;
            _repairTargetDurability = 0f;
            _debugToolId = string.Empty;
            _debugDurabilityNormalized = 0f;
            _reservationRetryCooldownSeconds = 0f;
            _isRepairing = false;
            _debugIsRepairing = false;
        }

        private float ReadSlottedDurability(IToolDurabilityService durabilitySystem, float maxDurability)
        {
            if (durabilitySystem == null || _slottedToolMetadata == null)
                return Mathf.Max(1f, maxDurability);

            float safeMaxDurability = Mathf.Max(1f, maxDurability);
            return _slottedToolItemHashId != 0u
                ? durabilitySystem.GetDurability(_slottedToolItemHashId, safeMaxDurability)
                : safeMaxDurability;
        }

        private void RegisterSlottedToolDurabilityMirror(IToolDurabilityService durabilitySystem)
        {
            if (durabilitySystem == null ||
                _slottedToolMetadata == null ||
                _slottedToolItemHashId == 0u ||
                string.IsNullOrWhiteSpace(_slottedToolMetadata.toolID))
            {
                return;
            }

            durabilitySystem.RegisterCentralizedEquipmentMirror(
                _slottedToolMetadata.toolID,
                _slottedToolItemHashId,
                Mathf.Max(1f, _slottedToolMetadata.maxDurability));
        }

    }
}
