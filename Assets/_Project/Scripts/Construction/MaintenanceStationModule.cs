using System.Collections.Generic;
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
    public sealed class MaintenanceStationModule : MonoBehaviour, ITickable, IUpdatable, IPoolable, IPowerComponent, IInteractable
    {
        private const string DefaultTitaniumRepairItemId = "Data_TitaniumScrap";
        private const string DefaultLubricantItemId = "Comp_LubricantResin";
        private const string EmptyPrompt = "Maintenance Station Empty";
        private const string RepairingPrompt = "Tool Under Maintenance";
        private const string ReadyPrompt = "Retrieve Serviced Tool";

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

        // COLD ALLOC: Dictionary<string,int>[2] — maintenance session reservation cost buffer — owner: MaintenanceStationModule
        private readonly Dictionary<string, int> _reservationCostBuffer = new Dictionary<string, int>(2);

        private PowerNode _powerNode;
        private bool _registered;
        private bool _hasPower = true;
        private ItemData _slottedToolItem;
        private ToolMetadata _slottedToolMetadata;
        private BaseLogisticsNetwork.LogisticsReservation _activeReservation;
        private float _repairTargetDurability;
        private bool _isRepairing;

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
            _powerNode = GetComponent<PowerNode>();
            ResolveRuntimeReferences();
            ResolveFallbackItems();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            CancelActiveRepair();
            TryUnregister();
        }

        private void OnDestroy()
        {
            CancelActiveRepair();
            TryUnregister();
        }

        public void OnSpawn()
        {
            _hasPower = true;
            _debugHasPower = true;
            ClearSlotState();
            ResolveRuntimeReferences();
            ResolveFallbackItems();
            TryRegister();
        }

        public void OnDespawn()
        {
            CancelActiveRepair();
            ClearSlotState();
            _hasPower = true;
            _debugHasPower = true;
            TryUnregister();
        }

        public void Tick(float deltaTime)
        {
            if (_slottedToolItem == null || _slottedToolMetadata == null)
            {
                _debugIsRepairing = false;
                _debugDurabilityNormalized = 0f;
                return;
            }

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            if (durabilitySystem == null || string.IsNullOrEmpty(_slottedToolMetadata.toolID))
            {
                _debugIsRepairing = false;
                _debugDurabilityNormalized = 0f;
                return;
            }

            float maxDurability = Mathf.Max(1f, _slottedToolMetadata.maxDurability);
            float currentDurability = durabilitySystem.GetDurability(_slottedToolMetadata.toolID, maxDurability);
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

            if (_activeReservation == null && !TryPrepareRepairReservation(currentDurability, maxDurability))
            {
                _debugIsRepairing = false;
                return;
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
            durabilitySystem.RepairTool(_slottedToolMetadata.toolID, repairDelta, maxDurability);

            _isRepairing = true;
            _debugIsRepairing = true;

            if (durabilitySystem.GetDurability(_slottedToolMetadata.toolID, maxDurability) >= targetDurability - 0.001f)
                CompleteActiveRepair();
        }

        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

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
            if (interactor == null)
                return;

            PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>() ?? PlayerInventory.Instance;
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

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            if (durabilitySystem == null || string.IsNullOrEmpty(metadata.toolID))
                return false;

            float maxDurability = Mathf.Max(1f, metadata.maxDurability);
            float currentDurability = durabilitySystem.GetDurability(metadata.toolID, maxDurability);
            if (currentDurability >= maxDurability - 0.001f)
                return false;

            ResolveRuntimeReferences();
            if (playerToolManager != null &&
                playerToolManager.CurrentTool != null &&
                ReferenceEquals(playerToolManager.CurrentTool.ToolData, item))
            {
                playerToolManager.Holster();
            }

            if (!inventory.TryRemoveQuantity(Hecton.Localization.LocHash.Compute(item.PersistentId), 1))
                return false;

            _slottedToolItem = item;
            _slottedToolMetadata = metadata;
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

            if (!inventory.TryAddItem(Hecton.Localization.LocHash.Compute(_slottedToolItem.PersistentId), 1))
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

            CancelActiveRepair();
            _slottedToolItem = item;
            _slottedToolMetadata = metadata;
            _repairTargetDurability = Mathf.Max(1f, metadata.maxDurability);
            _debugToolId = metadata.toolID;

            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            if (durabilitySystem != null && !string.IsNullOrEmpty(metadata.toolID))
            {
                float maxDurability = Mathf.Max(1f, metadata.maxDurability);
                float currentDurability = durabilitySystem.GetDurability(metadata.toolID, maxDurability);
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
            itemHashId = _slottedToolItem != null
                ? Hecton.Localization.LocHash.Compute(_slottedToolItem.PersistentId)
                : 0;
            if (itemHashId == 0)
                return false;

            CancelActiveRepair();
            ClearSlotState();
            return true;
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void ResolveRuntimeReferences()
        {
            if (playerToolManager == null)
                playerToolManager = Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.ToolManager : null;
        }

        private void ResolveFallbackItems()
        {
            PlayerInventory inventory = PlayerInventory.Instance;
            ItemCatalog catalog = inventory != null ? inventory.ItemCatalog : null;
            if (catalog == null)
                return;

            if (fallbackStructuralRepairItem == null)
                fallbackStructuralRepairItem = catalog.FindById(DefaultTitaniumRepairItemId);

            if (lubricantRepairItem == null)
                lubricantRepairItem = catalog.FindById(DefaultLubricantItemId);
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
            ResolveRuntimeReferences();
            if (item == null || playerToolManager == null)
                return false;

            GameObject toolPrefab = playerToolManager.GetKnownToolPrefabForItem(item);
            if (toolPrefab == null || !toolPrefab.TryGetComponent(out PlayerTool tool))
                return false;

            metadata = tool.Metadata;
            return metadata != null && !string.IsNullOrEmpty(metadata.toolID);
        }

        private bool TryPrepareRepairReservation(float currentDurability, float maxDurability)
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            PlayerInventory inventory = PlayerInventory.Instance;
            ItemCatalog catalog = inventory != null ? inventory.ItemCatalog : null;
            ResolveFallbackItems();

            if (grid == null || catalog == null)
                return false;

            _reservationCostBuffer.Clear();
            PopulateRepairCosts(currentDurability, maxDurability, catalog);
            if (_reservationCostBuffer.Count <= 0)
                return false;

            if (!BaseLogisticsNetwork.TryReserveResources(grid, _reservationCostBuffer, catalog, out _activeReservation))
                return false;

            _repairTargetDurability = maxDurability;
            return true;
        }

        private void PopulateRepairCosts(float currentDurability, float maxDurability, ItemCatalog catalog)
        {
            float missingRatio = 1f - Mathf.Clamp01(currentDurability / Mathf.Max(1f, maxDurability));
            if (missingRatio <= 0.0001f)
                return;

            ItemData structuralItem = ResolveStructuralRepairItem(catalog);
            int structuralCost = Mathf.Max(
                minimumStructuralCost,
                Mathf.CeilToInt(Mathf.Max(1f, _slottedToolMetadata != null ? _slottedToolMetadata.repairCostFull : 1f) * missingRatio));

            if (structuralItem != null)
                _reservationCostBuffer[structuralItem.PersistentId] = structuralCost;

            if (lubricantRepairItem != null && lubricantCostPerSession > 0)
                _reservationCostBuffer[lubricantRepairItem.PersistentId] = lubricantCostPerSession;
        }

        private ItemData ResolveStructuralRepairItem(ItemCatalog catalog)
        {
            if (_slottedToolMetadata != null && !string.IsNullOrWhiteSpace(_slottedToolMetadata.repairResourceID) && catalog != null)
            {
                ItemData authoredItem = catalog.FindById(_slottedToolMetadata.repairResourceID);
                if (authoredItem != null)
                    return authoredItem;
            }

            return fallbackStructuralRepairItem;
        }

        private void CompleteActiveRepair()
        {
            ToolDurabilitySystem durabilitySystem = ToolDurabilitySystem.Instance;
            if (durabilitySystem != null && _slottedToolMetadata != null && !string.IsNullOrEmpty(_slottedToolMetadata.toolID))
                durabilitySystem.RepairToolFull(_slottedToolMetadata.toolID, Mathf.Max(1f, _slottedToolMetadata.maxDurability));

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
            _repairTargetDurability = 0f;
            _debugToolId = string.Empty;
            _debugDurabilityNormalized = 0f;
            _isRepairing = false;
            _debugIsRepairing = false;
        }

    }
}
