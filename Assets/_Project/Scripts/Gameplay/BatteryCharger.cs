// ============================================================================
// HECTON-8 — BatteryCharger.cs
// Wall-mounted device to recharge tool batteries.
//
// ARCHITECTURE:
//   • ITickable for charging logic (no Update)
//   • IPowerComponent integration for power grid awareness
//   • Slot system with per-slot charge tracking
//   • Zero GC in hot paths: MaterialPropertyBlock, cached arrays
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
    /// Implements ITickable for charging logic.
    /// Integrates with IPowerComponent for power grid awareness.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Gameplay/Battery Charger")]
    public sealed class BatteryCharger : MonoBehaviour, ITickable, IPowerComponent, IInteractable
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
                _cachedToolManager = Object.FindAnyObjectByType<PlayerToolManager>();

            return _cachedToolManager;
        }

        private PlayerInventory ResolvePlayerInventory(Transform interactor = null)
        {
            if (_cachedPlayerInventory != null)
                return _cachedPlayerInventory;

            if (interactor != null)
                _cachedPlayerInventory = interactor.GetComponentInParent<PlayerInventory>();

            if (_cachedPlayerInventory == null)
                _cachedPlayerInventory = Object.FindAnyObjectByType<PlayerInventory>();

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
                ItemData toolBattery = batteryTool.RemoveBattery();
                if (toolBattery != null)
                {
                    // Find empty slot
                    int emptySlot = FindEmptySlot();
                    if (emptySlot >= 0)
                    {
                        InsertBattery(emptySlot, toolBattery, batteryTool.BatteryCharge);
                        return true;
                    }
                    else
                    {
                        // Charger full, return battery to tool
                        batteryTool.InsertBattery(toolBattery, batteryTool.BatteryCharge);
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
                    ItemData item = grid.GetCell(x, y);
                    if (item == null)
                        continue;

                    if (!IsBatteryItem(item))
                        continue;

                    // Check if this is an anchor cell
                    if (x > 0 && ReferenceEquals(grid.GetCell(x - 1, y), item))
                        continue;
                    if (y > 0 && ReferenceEquals(grid.GetCell(x, y - 1), item))
                        continue;

                    // Insert battery
                    if (InsertBattery(emptySlot, item, 0f))
                    {
                        playerInventory.RemoveItem(item, x, y);
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

            ItemData battery = RemoveBattery(slotIndex);
            if (battery == null)
                return false;

            if (!playerInventory.TryAddItem(battery, 1))
            {
                // Inventory full, re-insert battery
                InsertBattery(slotIndex, battery, slots[slotIndex].currentCharge);
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
                if (slots[i].batteryItem != null && slots[i].IsFullyCharged)
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
        private int _emissionPropertyId;

        // Cached for zero GC
        private Transform _cachedTransform;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Track which slots were fully charged (to avoid repeated events)
        private bool[] _slotChargedFlags;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Power rating: negative when charging, zero when idle.
        /// </summary>
        public float PowerRating => _isCharging ? -powerConsumption : 0f;

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
            UpdateAllIndicators();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: BatteryCharger

            // Initialize slot charge flags
            int slotCount = slots?.Length ?? 0;
            _slotChargedFlags = new bool[slotCount]; // COLD ALLOC: bool[slotCount] — track charged state — owner: BatteryCharger
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            TryRegister();
            RebuildLocalizedTextCache();
            UpdateAllIndicators();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register((ITickable)this);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister((ITickable)this);

            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — CHARGING LOGIC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles charging logic.
        /// Zero GC: no allocations, uses cached arrays.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_hasPower)
            {
                _isCharging = false;
                return;
            }

            _isCharging = false;

            if (slots == null)
                return;

            int slotCount = slots.Length;

            for (int i = 0; i < slotCount; i++)
            {
                ref BatterySlot slot = ref slots[i];

                // Skip empty or fully charged slots
                if (slot.batteryItem == null || slot.IsFullyCharged)
                    continue;

                // Mark as charging
                _isCharging = true;

                // Charge the battery
                float chargeDelta = chargeRate * deltaTime;
                float newCharge = slot.currentCharge + (chargeDelta / slot.maxCharge);
                slot.currentCharge = Mathf.Clamp01(newCharge);

                // Fire progress event
                OnChargeProgress?.Invoke(i, slot.currentCharge);

                // Update indicator
                UpdateSlotIndicator(i);

                // Check if just became fully charged
                if (slot.IsFullyCharged && !_slotChargedFlags[i])
                {
                    _slotChargedFlags[i] = true;
                    OnChargeComplete?.Invoke(i);

                    // Play charge complete sound
                    if (chargeCompleteSound != null && Hecton8.Audio.SpatialAudioManager.TryGetInstance(out var audio))
                    {
                        audio.PlayAtPoint(chargeCompleteSound, _cachedTransform.position);
                    }
                }
            }
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
            _slotChargedFlags[slotIndex] = currentCharge >= 1f;

            // Play insert sound
            if (insertSound != null && Hecton8.Audio.SpatialAudioManager.TryGetInstance(out var audio))
            {
                audio.PlayAtPoint(insertSound, _cachedTransform.position);
            }

            OnBatteryInserted?.Invoke(slotIndex);
            UpdateSlotIndicator(slotIndex);

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
            _slotChargedFlags[slotIndex] = false;

            if (battery != null)
            {
                OnBatteryRemoved?.Invoke(slotIndex);
                UpdateSlotIndicator(slotIndex);
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

        private void UpdateAllIndicators()
        {
            if (slotIndicators == null)
                return;

            int indicatorCount = slotIndicators.Length;
            for (int i = 0; i < indicatorCount; i++)
            {
                UpdateSlotIndicator(i);
            }
        }

        /// <summary>
        /// Updates the indicator light for a slot.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void UpdateSlotIndicator(int slotIndex)
        {
            if (slotIndicators == null || slotIndex < 0 || slotIndex >= slotIndicators.Length)
                return;

            Renderer indicator = slotIndicators[slotIndex];
            if (indicator == null)
                return;

            Color indicatorColor;

            if (!_hasPower)
            {
                indicatorColor = noPowerColor;
            }
            else if (slots == null || slotIndex >= slots.Length || slots[slotIndex].batteryItem == null)
            {
                indicatorColor = Color.black; // No battery
            }
            else if (slots[slotIndex].IsFullyCharged)
            {
                indicatorColor = chargedColor;
            }
            else
            {
                indicatorColor = chargingColor;
            }

            indicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);
            indicator.SetPropertyBlock(_mpb);
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

        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = LocalizationManager.Instance;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }
    }
}
