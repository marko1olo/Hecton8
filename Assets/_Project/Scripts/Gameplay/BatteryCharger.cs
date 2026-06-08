// ============================================================================
// HECTON-8 — BatteryCharger.cs
// Wall-mounted facade for tool battery logistics.
//
// ARCHITECTURE:
//   • Passive ChargerLinkDTO registration for the Burst logistics kernel
//   • IPowerComponent integration for power grid awareness
//   • Cold slot facade; SOA ConditionFlags owns charge truth
//   • Zero GC in hot paths; no charger Update loop
//
// INTEGRATION:
//   • IPowerComponent.HasPower — cold presentation cache only
//   • ItemData identity hydrated into InventorySlotDTO
//   • UnityEvent compatibility for cold UI call-sites
// ============================================================================

using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.Tools;
using Hecton.Localization;
using System;
using System.Runtime.InteropServices;
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
    public sealed class BatteryCharger : MonoBehaviour, IPowerComponent, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener, ILateFrameTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Slots ──────────────────────────────────────")]
        [Tooltip("Battery slots in this charger.")]
        // COLD ALLOC: BatterySlot[2] - serialized facade slots for prefab migration - owner: BatteryCharger
        [SerializeField] private BatterySlot[] slots = new BatterySlot[2];

        [Header("── Charging Settings ──────────────────────────")]
        [Tooltip("Charge rate per second (units per second).")]
        [SerializeField, Range(1f, 50f)] private float chargeRate = 10f;

        [Tooltip("Legacy serialized wattage retained for prefab migration; runtime charge truth is SOA/CSR.")]
        [SerializeField, Range(10f, 200f)] private float powerConsumption = 50f;

        [Header("── Power Integration ─────────────────────────")]
        [Tooltip("Reference to the power node for this charger.")]
        [SerializeField] private MonoBehaviour powerNodeReference;

        [Tooltip("First SOA inventory slot index owned by this charger. 0 is unassigned and fails closed.")]
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
        private const uint InvalidInventorySlotStartIndex = 0u;
        private const int InteractTextBufferCapacity = 96;
        private const byte ChargerAudioClipNone = 0;
        private const byte ChargerAudioClipInsert = 1;

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ChargerAudioRequest
        {
            [FieldOffset(0)] public Vector3 Position;
            [FieldOffset(12)] public byte ClipKind;
            [FieldOffset(13)] public byte Dirty;
            [FieldOffset(14)] public ushort Reserved0;
        }

        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedSwapBatteryTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
        private int _cachedSwapBatteryTextLength;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private IAudioService _cachedAudioService;
        private PlayerToolManager _cachedToolManager;
        private PlayerInventory _cachedPlayerInventory;
        private bool _hotSwapListenerRegistered;
        private bool _registeredLateFrame;
        private ChargerAudioRequest _pendingChargerAudio;
        // COLD ALLOC: PlayerInventory.CraftReservation[1] - inventory-owner reservation fence for charger insert handoff - owner: BatteryCharger
        private readonly PlayerInventory.CraftReservation[] _inventoryReservationScratch = new PlayerInventory.CraftReservation[1];

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
            PlayerToolManager toolManager = BindToolManagerForInteraction(interactor);

            if (toolManager != null && toolManager.CurrentTool != null)
            {
                TrySwapBatteryWithTool(toolManager);
                return;
            }

            // Try to insert battery from inventory
            PlayerInventory playerInventory = BindPlayerInventoryForInteraction(interactor);

            if (playerInventory != null)
            {
                InsertBatteryFromInventory(playerInventory);
            }
        }

        string IInteractable.GetInteractText()
        {
            PlayerToolManager toolManager = _cachedToolManager;
            if (toolManager != null && toolManager.CurrentTool != null)
            {
                return DefaultSwapBatteryText;
            }
            return DefaultInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            PlayerToolManager toolManager = _cachedToolManager;
            ReadOnlySpan<char> source = toolManager != null && toolManager.CurrentTool != null
                ? _cachedSwapBatteryTextBuffer.AsSpan(0, _cachedSwapBatteryTextLength)
                : _cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength);
            return InteractableTextCopy.TryCopy(source, destination, out length);
        }

        private PlayerToolManager BindToolManagerForInteraction(Transform interactor = null)
        {
            if (_cachedToolManager != null)
                return _cachedToolManager;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            _cachedToolManager = playerContext != null ? playerContext.ToolManager : null;

            return _cachedToolManager;
        }

        private PlayerInventory BindPlayerInventoryForInteraction(Transform interactor = null)
        {
            if (_cachedPlayerInventory != null)
                return _cachedPlayerInventory;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            _cachedPlayerInventory = playerContext != null ? playerContext.Inventory : null;

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
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0 || !HasAuthoredInventorySlotRange())
                    return false;

                float toolBatteryCharge = batteryTool.BatteryCharge;
                ItemData toolBattery = batteryTool.RemoveBattery();
                if (toolBattery != null)
                {
                    if (InsertBattery(emptySlot, toolBattery, toolBatteryCharge))
                        return true;

                    if (!batteryTool.InsertBattery(toolBattery, toolBatteryCharge))
                        ReportBridgeRollbackFailure();

                    return false;
                }
            }

            // Tool has no battery, try to insert a charged one from charger
            int chargedSlot = FindChargedSlot();
            if (chargedSlot >= 0)
            {
                float previousCharge = GetChargeProgress(chargedSlot);
                ItemData chargedBattery = RemoveBattery(chargedSlot);
                if (chargedBattery != null)
                {
                    if (batteryTool.InsertBattery(chargedBattery, 1f))
                        return true;

                    if (!InsertBattery(chargedSlot, chargedBattery, previousCharge))
                        ReportBridgeRollbackFailure();

                    return false;
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
            if (playerInventory == null || playerInventory.Grid == null || !HasAuthoredInventorySlotRange())
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

                    int reservationCount = 0;
                    if (!playerInventory.TryReserveQuantityForCraft(itemHashId, 1, _inventoryReservationScratch, ref reservationCount))
                        continue;

                    if (!InsertBattery(emptySlot, item, 0f))
                    {
                        playerInventory.ReleaseCraftReservations(_inventoryReservationScratch, reservationCount);
                        return false;
                    }

                    if (playerInventory.CommitCraftReservations(_inventoryReservationScratch, reservationCount))
                        return true;

                    if (RemoveBattery(emptySlot) == null)
                        ReportBridgeRollbackFailure();

                    return false;
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

            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return false;

            ItemData candidateBattery = slots[slotIndex].batteryItem;
            int candidateHash = unchecked((int)ComputeItemHash(candidateBattery));
            if (candidateBattery == null || candidateHash == 0 || !playerInventory.CanAcceptItemQuantity(candidateHash, 1))
                return false;

            float previousCharge = slots != null && slotIndex >= 0 && slotIndex < slots.Length && slots[slotIndex] != null
                ? GetChargeProgress(slotIndex)
                : 0f;
            ItemData battery = RemoveBattery(slotIndex);
            if (battery == null)
                return false;

            if (!playerInventory.TryAddItem(candidateHash, 1))
            {
                // Inventory full, re-insert battery
                if (!InsertBattery(slotIndex, battery, previousCharge))
                    ReportBridgeRollbackFailure();

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
                BatterySlot slot = slots[i];
                if (slot == null || slot.batteryItem == null)
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
                BatterySlot slot = slots[i];
                if (slot != null && slot.batteryItem != null && GetChargeProgress(i) >= 0.999f)
                    return i;
            }

            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private bool _hasPower = true;
        private bool _registered;
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
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureSlotObjects();
            _cachedTransform = transform;
            _powerNode = powerNodeReference as PowerNode;
            if (_powerNode == null && !TryGetComponent(out _powerNode))
                _powerNode = null;

            PreserveColdInspectorCompatibility();
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            InteractableRegistry.RegisterTree(this);
            TryRegisterHotSwapListener();
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            RebuildLocalizedTextCache();
        }

        private void OnDisable()
        {
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
            TryUnregister();
        }

        private void OnDestroy()
        {
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
            TryUnregister();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext, forceAssign: true);
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterLateFrame();
                    if (currentService != null && isActiveAndEnabled && _pendingChargerAudio.Dirty != 0)
                        TryRegisterLateFrame();
                    break;
            }
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registered)
                _registered = RegisterLogisticsLinks();
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                UnregisterLogisticsLinks();
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            ClearQueuedChargerAudio();
        }

        // ══════════════════════════════════════════════════════════
        //  LEGACY ENTRYPOINT - CHARGING DISABLED HERE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Legacy entrypoint retained for serialized call-sites. Charging is handled by the charger logistics bridge.
        /// </summary>
        public void SlowTick()
        {
        }

        public void LateFrameTick()
        {
            FlushQueuedChargerAudio();
            TryUnregisterLateFrameWhenDormant();
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
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return false;

            if (slots[slotIndex].batteryItem != null)
                return false; // Slot occupied

            if (!WriteInventorySlotState(slotIndex, battery, currentCharge))
                return false;

            slots[slotIndex].batteryItem = battery;
            slots[slotIndex].currentCharge = currentCharge;

            if (insertSound != null)
                QueueChargerAudio(ChargerAudioClipInsert, _cachedTransform != null ? _cachedTransform.position : transform.position);

            OnBatteryInserted?.Invoke(slotIndex);

            return true;
        }

        private void QueueChargerAudio(byte clipKind, Vector3 position)
        {
            _pendingChargerAudio.Position = position;
            _pendingChargerAudio.ClipKind = clipKind;
            _pendingChargerAudio.Dirty = 1;
            _pendingChargerAudio.Reserved0 = 0;
            TryRegisterLateFrame();
        }

        private void FlushQueuedChargerAudio()
        {
            if (_pendingChargerAudio.Dirty == 0)
                return;

            ChargerAudioRequest request = _pendingChargerAudio;
            _pendingChargerAudio = default;

            IAudioService audio = ResolveAudioService();
            if (audio == null)
                return;

            AudioClip clip = ResolveChargerAudioClip(request.ClipKind);
            if (clip != null)
                audio.PlayAtPoint(clip, request.Position, ResolveChargerAudioVolume(), 1f);
        }

        private void ClearQueuedChargerAudio()
        {
            _pendingChargerAudio = default;
        }

        private void TryUnregisterLateFrameWhenDormant()
        {
            if (!_registeredLateFrame || _pendingChargerAudio.Dirty != 0)
                return;

            TryUnregisterLateFrame();
        }

        private AudioClip ResolveChargerAudioClip(byte clipKind)
        {
            return clipKind == ChargerAudioClipInsert ? insertSound : null;
        }

        private static float ResolveChargerAudioVolume()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.lerp(0.7f, 1f, quality);
        }

        /// <summary>
        /// Removes a battery from the specified slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <returns>The removed battery item, or null if slot was empty.</returns>
        public ItemData RemoveBattery(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return null;

            ItemData battery = slots[slotIndex].batteryItem;
            if (battery == null)
                return null;

            if (!WriteInventorySlotState(slotIndex, null, 0f))
                return null;

            slots[slotIndex].batteryItem = null;
            slots[slotIndex].currentCharge = 0f;

            if (battery != null)
                OnBatteryRemoved?.Invoke(slotIndex);

            return battery;
        }

        /// <summary>
        /// Gets the charge progress for a slot.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <returns>Normalized charge (0-1), or 0 if invalid slot.</returns>
        public float GetChargeProgress(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return 0f;

            if (!HasAuthoredInventorySlotRange())
                return slots[slotIndex].currentCharge;

            uint inventorySlot = ResolveSlotInventoryIndex(slotIndex);
            if (BatteryChargerLogisticsBridge.TryReadCharge01(inventorySlot, out float vaultCharge))
                return vaultCharge;

            return slots[slotIndex].currentCharge;
        }

        /// <summary>
        /// Checks if a slot has a battery.
        /// </summary>
        /// <param name="slotIndex">Slot index (0-based).</param>
        /// <returns>True if slot has a battery.</returns>
        public bool HasBatteryInSlot(int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null)
                return false;

            return slots[slotIndex].HasBattery;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private bool RegisterLogisticsLinks()
        {
            if (!registerLogisticsLinks || !HasAuthoredInventorySlotRange())
                return false;

            bool anyRegistered = false;
            double3 chargerAup = ResolveChargerAup();
            for (int i = 0; i < slots.Length; i++)
            {
                BatterySlot slot = slots[i];
                if (slot == null || !WriteInventorySlotState(i, slot.batteryItem, slot.currentCharge))
                    continue;

                float maxCharge = slot != null ? math.max(0.001f, slot.maxCharge) : 100f;
                float normalizedRate = math.max(0f, chargeRate) / maxCharge;
                if (BatteryChargerLogisticsBridge.TryRegisterChargerLink(
                        ResolveSlotInventoryIndex(i),
                        ResolvePowerNodeIndex(i),
                        normalizedRate,
                        logisticsEfficiencyScalar,
                        chargerAup,
                        out _))
                {
                    anyRegistered = true;
                }
            }

            return anyRegistered;
        }

        private void UnregisterLogisticsLinks()
        {
            int slotCount = slots?.Length ?? 0;
            if (slotCount > 0 && HasAuthoredInventorySlotRange())
                BatteryChargerLogisticsBridge.TryUnregisterChargerLinks(inventorySlotStartIndex, slotCount, powerGraphNodeIndex);
        }

        private bool WriteInventorySlotState(int slotIndex, ItemData battery, float charge01)
        {
            if (!HasAuthoredInventorySlotRange())
                return false;

            uint itemHash = ComputeItemHash(battery);
            return BatteryChargerLogisticsBridge.TryWriteInventorySlotState(ResolveSlotInventoryIndex(slotIndex), itemHash, charge01);
        }

        private bool HasAuthoredInventorySlotRange()
        {
            return inventorySlotStartIndex != InvalidInventorySlotStartIndex && slots != null && slots.Length > 0;
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
            if (!math.isfinite(position.x) ||
                !math.isfinite(position.y) ||
                !math.isfinite(position.z))
            {
                return double3.zero;
            }

            Hecton8.World.AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return double3.zero;

            double3 chargerAup = Hecton8.World.AbsoluteUniversePosition.OffsetAbsoluteMeters(
                in originAup,
                new double3(position.x, position.y, position.z));
            return math.all(math.isfinite(chargerAup)) ? chargerAup : double3.zero;
        }

        private void EnsureSlotObjects()
        {
            if (slots == null || slots.Length == 0)
            {
                // COLD ALLOC: BatterySlot[2] - fallback for legacy prefab migration - owner: BatteryCharger
                slots = new BatterySlot[2];
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    // COLD ALLOC: BatterySlot[1] - cold facade object for serialized slot metadata - owner: BatteryCharger
                    slots[i] = new BatterySlot();
                }
            }
        }

        private static void ReportBridgeRollbackFailure()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("BatteryCharger bridge rollback failed; Inventory-owner reservation route is required for a hard conservation proof.");
#endif
        }

        private static uint ComputeItemHash(ItemData item)
        {
            return unchecked((uint)ItemData.ResolvePersistentHashId(item));
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

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (chargeRate < 1f) chargeRate = 1f;
            if (powerConsumption < 1f) powerConsumption = 1f;

            EnsureSlotObjects();

            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            if (slots == null)
                return;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].slotTransform != null)
                {
                    Gizmos.DrawWireSphere(slots[i].slotTransform.position, 0.1f);
                }
            }
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            ILocalizationTextReadModel manager = Hecton8.Core.GlobalRegistry.LocalizationText;
            _cachedInteractTextLength = InteractableTextCopy.CopyLocalizedTruncated(manager, LocalizationKeys.INTERACT_ACCESS_CHARGER, DefaultInteractText, _cachedInteractTextBuffer);
            _cachedSwapBatteryTextLength = InteractableTextCopy.CopyLocalizedTruncated(manager, LocalizationKeys.INTERACT_SWAP_BATTERY, DefaultSwapBatteryText, _cachedSwapBatteryTextBuffer);
        }

        private void CacheRegistryServicesCold()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player, forceAssign: true);
            CacheAudioService(GlobalRegistry.Audio);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _cachedAudioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _cachedAudioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _cachedAudioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerContext, bool forceAssign)
        {
            _cachedPlayerContext = playerContext;
            if (playerContext == null)
            {
                if (forceAssign)
                {
                    _cachedToolManager = null;
                    _cachedPlayerInventory = null;
                }
                return;
            }

            if (forceAssign || _cachedToolManager == null)
                _cachedToolManager = playerContext.ToolManager;
            if (forceAssign || _cachedPlayerInventory == null)
                _cachedPlayerInventory = playerContext.Inventory;
        }

        private void ClearCachedRegistryServices()
        {
            _cachedPlayerContext = null;
            _cachedAudioService = null;
            _cachedToolManager = null;
            _cachedPlayerInventory = null;
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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

    }
}

