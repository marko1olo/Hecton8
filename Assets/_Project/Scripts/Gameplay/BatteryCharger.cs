// ============================================================================
// HECTON-8 — BatteryCharger.cs
// Wall-mounted device to recharge tool batteries.
//
// ARCHITECTURE:
//   • Passive ChargerLinkDTO registration for the Burst logistics kernel
//   • IPowerComponent integration for power grid awareness
//   • Slot system with per-slot charge tracking
//   • Zero GC in hot paths: DTO registration, cached arrays
//
// INTEGRATION:
//   • IPowerComponent.HasPower — checked before charging
//   • ItemData with charge field (battery items)
//   • UnityEvent for UI progress updates
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.Tools;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Represents a single battery slot in the charger.
    /// </summary>
    [System.Serializable]
    public class BatterySlot
    {
        [Tooltip("Transform where the battery visual is placed.")]
        public Transform slotTransform;

        [Tooltip("Current battery item in this slot (null if empty).")]
        public ItemData batteryItem;

        [Tooltip("Current charge level (0-1).")]
        [Range(0f, 1f)] public float currentCharge;

        [Tooltip("Maximum charge capacity.")]
        public float maxCharge = 100f;

        /// <summary>True if slot has a battery.</summary>
        public bool HasBattery => batteryItem != null;

        /// <summary>True if battery is fully charged.</summary>
        public bool IsFullyCharged => currentCharge >= 1f;
    }

    /// <summary>
    /// Wall-mounted battery charger for tool batteries.
    /// Registers passive DTO links for the charger logistics kernel.
    /// Integrates with IPowerComponent for power grid awareness.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Gameplay/Battery Charger")]
    public sealed class BatteryCharger : MonoBehaviour, IPowerComponent, IInteractable, ILocalizationLanguageChangedListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Slots ──────────────────────────────────────")]
        [Tooltip("Battery slots in this charger.")]
        [SerializeField] private BatterySlot[] slots = new BatterySlot[2];

        [Header("── Charging Settings ──────────────────────────")]
        [Tooltip("Charge rate per second (units per second).")]
        [SerializeField, Range(1f, 50f)] private float chargeRate = 10f;

        [Tooltip("Power consumption while charging (Watts).")]
        [SerializeField, Range(10f, 200f)] private float powerConsumption = 50f;

        [Header("── Power Integration ─────────────────────────")]
        [Tooltip("Reference to the power node for this charger.")]
        [SerializeField] private MonoBehaviour powerNodeReference;

        [Tooltip("First SOA inventory slot index owned by this charger.")]
        [SerializeField] private uint inventorySlotStartIndex;

        [Tooltip("CSR power graph node index supplying this charger.")]
        [SerializeField] private uint powerGraphNodeIndex;

        [Tooltip("Efficiency scalar consumed by the Burst charger transaction kernel.")]
        [SerializeField, Range(0.05f, 2f)] private float logisticsEfficiencyScalar = 1f;

        [Tooltip("Registers passive ChargerLinkDTO records instead of active charging ticks.")]
        [SerializeField] private bool registerLogisticsLinks = true;

        [Header("── Visuals ────────────────────────────────────")]
        [Tooltip("Renderer for charge indicator lights.")]
        [SerializeField] private Renderer[] slotIndicators;

        [Tooltip("Material property for charge indicator color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color when charging.")]
        [SerializeField] private Color chargingColor = new Color(0f, 0.8f, 1f);

        [Tooltip("Color when fully charged.")]
        [SerializeField] private Color chargedColor = new Color(0f, 1f, 0.3f);

        [Tooltip("Color when no power.")]
        [SerializeField] private Color noPowerColor = new Color(0.5f, 0.1f, 0.1f);

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Sound played when battery is inserted.")]
        [SerializeField] private AudioClip insertSound;

        [Tooltip("Sound played when battery is fully charged.")]
        [SerializeField] private AudioClip chargeCompleteSound;

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when a slot's charge progress changes. Parameters: (slotIndex, normalizedProgress).")]
        [SerializeField] private UnityEvent<int, float> OnChargeProgress;

        [Tooltip("Fired when a battery is fully charged. Parameter: slotIndex.")]
        [SerializeField] private UnityEvent<int> OnChargeComplete;

        [Tooltip("Fired when a battery is inserted. Parameter: slotIndex.")]
        [SerializeField] private UnityEvent<int> OnBatteryInserted;

        [Tooltip("Fired when a battery is removed. Parameter: slotIndex.")]
        [SerializeField] private UnityEvent<int> OnBatteryRemoved;

        // ══════════════════════════════════════════════════════════
        //  INTERACTION TEXT
        // ══════════════════════════════════════════════════════════

        private const string DefaultInteractText = "Access Charger";
        private const string DefaultSwapBatteryText = "Swap Battery";
        private string _cachedInteractText;
        private string _cachedSwapBatteryText;
        private PlayerToolManager _cachedToolManager;
        private PlayerInventory _cachedPlayerInventory;

        // ══════════════════════════════════════════════════════════
        //  IInteractable IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        void IInteractable.OnHoverStart()
        {
            // Future: highlight effect
        }

        void IInteractable.OnHoverEnd()
        {
            // Future: remove highlight
        }

        void IInteractable.Interact(Transform interactor)
        {
            // Try to swap battery with held tool
            PlayerToolManager toolManager = ResolveToolManager(interactor);

            if (toolManager != null && toolManager.CurrentTool != null)
            {
                TrySwapBatteryWithTool(toolManager);
                return;
            }

            // Try to insert battery from inventory
            PlayerInventory playerInventory = ResolvePlayerInventory(interactor);

            if (playerInventory != null)
            {
                InsertBatteryFromInventory(playerInventory);
            }
        }

        string IInteractable.GetInteractText()
        {
            PlayerToolManager toolManager = ResolveToolManager();
            if (toolManager != null && toolManager.CurrentTool != null)
            {
                return _cachedSwapBatteryText;
            }
            return _cachedInteractText;
        }

        private PlayerToolManager ResolveToolManager(Transform interactor = null)
        {
            if (_cachedToolManager != null)
                return _cachedToolManager;

            if (interactor != null)
                _cachedToolManager = interactor.GetComponentInParent<PlayerToolManager>();

            if (_cachedToolManager == null)
                _cachedToolManager = Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.ToolManager : null;

            return _cachedToolManager;
        }

        private PlayerInventory ResolvePlayerInventory(Transform interactor = null)
        {
            if (_cachedPlayerInventory != null)
                return _cachedPlayerInventory;

            if (interactor != null)
                _cachedPlayerInventory = interactor.GetComponentInParent<PlayerInventory>();

            if (_cachedPlayerInventory == null)
                _cachedPlayerInventory = Hecton8.Core.GlobalRegistry.Player != null ? Hecton8.Core.GlobalRegistry.Player.Inventory : null;

            return _cachedPlayerInventory;
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Attempts to swap a battery with the player's currently held tool.
        /// If the tool has a battery, removes it and inserts into charger.
        /// If the charger has a charged battery, inserts into tool.
        /// </summary>
        /// <param name="toolManager">Player's tool manager.</param>
        /// <returns>True if a swap occurred.</returns>
        public bool TrySwapBatteryWithTool(PlayerToolManager toolManager)
        {
            if (toolManager == null || toolManager.CurrentTool == null)
                return false;

            PlayerTool currentTool = toolManager.CurrentTool;

            // Check if tool has a battery component
            // Note: This assumes PlayerTool has a battery field or property
            // Adjust based on actual PlayerTool implementation
            var batteryTool = currentTool as IBatteryTool;
            if (batteryTool == null)
            {
                // Tool doesn't use batteries
                return false;
            }

            // If tool has a battery, try to remove and insert into charger
            if (batteryTool.HasBattery)
            {
                float toolBatteryCharge = batteryTool.BatteryCharge;
                ItemData toolBattery = batteryTool.RemoveBattery();
                if (toolBattery != null)
                {
                    // Find empty slot
                    int emptySlot = FindEmptySlot();
                    if (emptySlot >= 0)
                    {
                        InsertBattery(emptySlot, toolBattery, toolBatteryCharge);
                        return true;
                    }
                    else
                    {
                        // Charger full, return battery to tool
                        batteryTool.InsertBattery(toolBattery, toolBatteryCharge);
                        return false;
                    }
                }
            }

            // Tool has no battery, try to insert a charged one from charger
            int chargedSlot = FindChargedSlot();
            if (chargedSlot >= 0)
            {
                ItemData chargedBattery = RemoveBattery(chargedSlot);
                if (chargedBattery != null)
                {
                    batteryTool.InsertBattery(chargedBattery, 1f);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Inserts a battery from the player's inventory into the charger.
        /// </summary>
        /// <param name="playerInventory">Player's inventory.</param>
        /// <returns>True if a battery was inserted.</returns>
        public bool InsertBatteryFromInventory(PlayerInventory playerInventory)
        {
            if (playerInventory == null || playerInventory.Grid == null)
                return false;

            int emptySlot = FindEmptySlot();
            if (emptySlot < 0)
                return false; // Charger full

            InventoryGrid grid = playerInventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            // Search for battery items
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int anchorIndex = grid.GetCellAnchorIndex(x, y);
                    if (anchorIndex < 0 || anchorIndex != y * cols + x)
                        continue;

                    int itemHashId = playerInventory.GetItemHashAt(x, y);
                    ItemData item = itemHashId != 0 && playerInventory.ItemCatalog != null
                        ? playerInventory.ItemCatalog.FindByHash(itemHashId)
                        : null;
                    if (item == null)
                        continue;

                    if (!IsBatteryItem(item))
                        continue;

                    // Insert battery
                    if (InsertBattery(emptySlot, item, 0f))
                    {
                        playerInventory.RemoveItemAt(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes a charged battery and adds it to the player's inventory.
        /// </summary>
        /// <param name="slotIndex">Slot to remove from.</param>
        /// <param name="playerInventory">Player's inventory.</param>
        /// <returns>True if battery was removed and added to inventory.</returns>
        public bool RemoveBatteryToInventory(int slotIndex, PlayerInventory playerInventory)
        {
            if (playerInventory == null)
                return false;

            float previousCharge = slots != null && slotIndex >= 0 && slotIndex < slots.Length && slots[slotIndex] != null
                ? GetChargeProgress(slotIndex)
                : 0f;
            ItemData battery = RemoveBattery(slotIndex);
            if (battery == null)
                return false;

            if (!playerInventory.TryAddItem(Hecton.Localization.LocHash.Compute(battery.PersistentId), 1))
            {
                // Inventory full, re-insert battery
                InsertBattery(slotIndex, battery, previousCharge);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if an item is a battery.
        /// </summary>
        private bool IsBatteryItem(ItemData item)
        {
            if (item == null)
                return false;

            // Check by category or a custom flag
            // Adjust based on actual ItemData structure
            return item.category == ItemCategory.Tool || 
                   item.itemName != null && 
                   (item.itemName.Contains("Battery") || item.itemName.Contains("Cell"));
        }

        /// <summary>
        /// Finds the first empty slot.
        /// </summary>
        /// <returns>Slot index, or -1 if all slots are occupied.</returns>
        public int FindEmptySlot()
        {
            if (slots == null)
                return -1;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].batteryItem == null)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Finds the first fully charged slot.
        /// </summary>
        /// <returns>Slot index, or -1 if no charged battery.</returns>
        public int FindChargedSlot()
        {
            if (slots == null)
                return -1;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].batteryItem != null && GetChargeProgress(i) >= 0.999f)
                    return i;
            }

            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _hasPower = true;
        private bool _registered;
        private bool _isCharging;
        private PowerNode _powerNode;

        // Cached for zero GC
        private Transform _cachedTransform;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Power rating: negative when charging, zero when idle.
        /// </summary>
        public float PowerRating => 0f;

        /// <summary>
        /// Priority: normal for chargers.
        /// </summary>
        public int PowerPriority => 50;

        /// <summary>
        /// Current power state (cached from OnPowerStatusChanged).
        /// </summary>
        public bool HasPower => _hasPower;

        /// <summary>
        /// Called by PowerGrid when power status changes.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            RefreshChargingDemand();
            UpdateAllIndicators();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _powerNode = powerNodeReference as PowerNode;
            if (_powerNode == null && !TryGetComponent(out _powerNode))
                _powerNode = null;

            PreserveColdInspectorCompatibility();
        }

        private void OnEnable()
        {
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            RebuildLocalizedTextCache();
            RefreshChargingDemand();
            UpdateAllIndicators();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            SetChargingState(false);
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregister();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            SetChargingState(false);
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = RegisterLogisticsLinks();
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            UnregisterLogisticsLinks();
            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  LEGACY ENTRYPOINT - CHARGING DISABLED HERE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Legacy entrypoint retained for serialized call-sites. Charging is handled by BatteryChargerLogisticsRuntime.
        /// </summary>
        public void SlowTick()
        {
            SetChargingState(false);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Inserts a battery into the specified slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <param name="battery">Battery item data.</param>
        /// <param name="currentCharge">Current charge level (0-1).</param>
        /// <returns>True if battery was inserted successfully.</returns>
        public bool InsertBattery(int slotIndex, ItemData battery, float currentCharge = 0f)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
                return false;

            if (slots[slotIndex].batteryItem != null)
                return false; // Slot occupied

            slots[slotIndex].batteryItem = battery;
            slots[slotIndex].currentCharge = currentCharge;
            WriteInventorySlotState(slotIndex, battery, currentCharge);

            // Play insert sound
            if (insertSound != null && Hecton8.Core.GlobalRegistry.Audio is Hecton8.Core.IAudioService audio)
            {
                audio.PlayAtPoint(insertSound, _cachedTransform.position);
            }

            OnBatteryInserted?.Invoke(slotIndex);
            UpdateSlotIndicator(slotIndex);
            RefreshChargingDemand();

            return true;
        }

        /// <summary>
        /// Removes a battery from the specified slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <returns>The removed battery item, or null if slot was empty.</returns>
        public ItemData RemoveBattery(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
                return null;

            ItemData battery = slots[slotIndex].batteryItem;
            slots[slotIndex].batteryItem = null;
            slots[slotIndex].currentCharge = 0f;
            WriteInventorySlotState(slotIndex, null, 0f);

            if (battery != null)
            {
                OnBatteryRemoved?.Invoke(slotIndex);
                UpdateSlotIndicator(slotIndex);
                RefreshChargingDemand();
            }

            return battery;
        }

        /// <summary>
        /// Gets the charge progress for a slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <returns>Normalized charge (0-1), or 0 if invalid slot.</returns>
        public float GetChargeProgress(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
                return 0f;

            uint inventorySlot = ResolveSlotInventoryIndex(slotIndex);
            if (BatteryChargerLogisticsRuntime.TryReadCharge01(inventorySlot, out float vaultCharge))
            {
                slots[slotIndex].currentCharge = vaultCharge;
                return vaultCharge;
            }

            return slots[slotIndex].currentCharge;
        }

        /// <summary>
        /// Checks if a slot has a battery.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <returns>True if slot has a battery.</returns>
        public bool HasBatteryInSlot(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length)
                return false;

            return slots[slotIndex].HasBattery;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private bool RegisterLogisticsLinks()
        {
            if (!registerLogisticsLinks || slots == null)
                return false;

            bool anyRegistered = false;
            double3 chargerAup = ResolveChargerAup();
            for (int i = 0; i < slots.Length; i++)
            {
                BatterySlot slot = slots[i];
                float maxCharge = slot != null ? math.max(0.001f, slot.maxCharge) : 100f;
                float normalizedRate = math.max(0f, chargeRate) / maxCharge;
                if (BatteryChargerLogisticsRuntime.TryRegisterChargerLink(
                        ResolveSlotInventoryIndex(i),
                        ResolvePowerNodeIndex(i),
                        normalizedRate,
                        logisticsEfficiencyScalar,
                        chargerAup,
                        out _))
                {
                    anyRegistered = true;
                }

                if (slot != null)
                    WriteInventorySlotState(i, slot.batteryItem, slot.currentCharge);
            }

            return anyRegistered;
        }

        private void UnregisterLogisticsLinks()
        {
            int slotCount = slots?.Length ?? 0;
            if (slotCount > 0)
                BatteryChargerLogisticsRuntime.TryUnregisterChargerLinks(inventorySlotStartIndex, slotCount, powerGraphNodeIndex);
        }

        private void WriteInventorySlotState(int slotIndex, ItemData battery, float charge01)
        {
            uint itemHash = ComputeItemHash(battery);
            BatteryChargerLogisticsRuntime.TryWriteInventorySlotState(ResolveSlotInventoryIndex(slotIndex), itemHash, charge01);
        }

        private uint ResolveSlotInventoryIndex(int slotIndex)
        {
            return inventorySlotStartIndex + (uint)math.max(0, slotIndex);
        }

        private uint ResolvePowerNodeIndex(int slotIndex)
        {
            return powerGraphNodeIndex;
        }

        private double3 ResolveChargerAup()
        {
            Vector3 position = _cachedTransform != null ? _cachedTransform.position : transform.position;
            return HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(position);
        }

        private static uint ComputeItemHash(ItemData item)
        {
            if (item == null)
                return 0u;

            string key = !string.IsNullOrEmpty(item.PersistentId) ? item.PersistentId : item.itemName;
            return string.IsNullOrEmpty(key) ? 1u : unchecked((uint)Hecton.Localization.LocHash.Compute(key));
        }

        private void UpdateAllIndicators()
        {
        }

        /// <summary>
        /// Legacy renderer indicators are disabled. LED state is GPU-driven from ChargerVisualStateDTO.
        /// </summary>
        private void UpdateSlotIndicator(int slotIndex)
        {
        }

        private void PreserveColdInspectorCompatibility()
        {
            // Serialized fields stay on the prefab for editor migration; runtime LEDs are GPU-buffer driven.
            _ = powerConsumption;
            _ = slotIndicators;
            _ = emissionProperty;
            _ = chargingColor;
            _ = chargedColor;
            _ = noPowerColor;
            _ = chargeCompleteSound;
            _ = OnChargeProgress;
            _ = OnChargeComplete;
        }

        private void RefreshChargingDemand()
        {
            SetChargingState(_hasPower && HasChargeWork());
        }

        private bool HasChargeWork()
        {
            if (slots == null)
                return false;

            int slotCount = slots.Length;
            for (int i = 0; i < slotCount; i++)
            {
                BatterySlot slot = slots[i];
                if (slot != null && slot.batteryItem != null && !slot.IsFullyCharged)
                    return true;
            }

            return false;
        }

        private void SetChargingState(bool isCharging)
        {
            if (_isCharging == isCharging)
                return;

            _isCharging = isCharging;
            MarkPowerGridDirty();
        }

        private void MarkPowerGridDirty()
        {
            if (_powerNode != null && _powerNode.Grid != null)
                _powerNode.Grid.MarkDirty();
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (chargeRate < 1f) chargeRate = 1f;
            if (powerConsumption < 1f) powerConsumption = 1f;

            // Ensure slots array has at least one slot
            if (slots == null || slots.Length == 0)
            {
                slots = new BatterySlot[2];
            }

            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            if (slots == null)
                return;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].slotTransform != null)
                {
                    Gizmos.DrawWireSphere(slots[i].slotTransform.position, 0.1f);
                }
            }
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            _cachedInteractText = ResolveLocalized(LocalizationKeys.INTERACT_ACCESS_CHARGER, DefaultInteractText);
            _cachedSwapBatteryText = ResolveLocalized(LocalizationKeys.INTERACT_SWAP_BATTERY, DefaultSwapBatteryText);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}

