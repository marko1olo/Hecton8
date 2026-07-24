// ============================================================================
// HECTON-8 — BioReactor.cs
// Storage-based power generator that consumes organic materials.
//
// ARCHITECTURE:
//   • IPowerComponent for power grid integration
//   • ITickable for fuel consumption (no Update)
//   • MaterialPropertyBlock for fuel level indicator (zero GC)
//   • Slot-based fuel storage
//
// FUEL SYSTEM:
//   • Player inserts organic items (ItemData with organic tag/category)
//   • Each item has fuelValue (energy content)
//   • Reactor consumes fuel over time, producing constant power
//
// INTEGRATION:
//   • IPowerComponent.PowerRating returns current production
//   • UnityEvent for fuel level changes
// ============================================================================

using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.World;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Represents a fuel item in the reactor.
    /// </summary>
    [System.Serializable]
    public struct FuelItem
    {
        public ItemData itemData;
        public float fuelValue; // Energy content
        public float remainingFuel; // Current remaining fuel
    }

    /// <summary>
    /// Bio-reactor power generator.
    /// Consumes organic materials to produce power.
    /// Implements IPowerComponent for power grid integration.
    /// Implements IInteractable for player fuel deposit.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    [AddComponentMenu("Hecton/Gameplay/Bio Reactor")]
    public sealed class BioReactor : MonoBehaviour, IPowerComponent, ITickable, IUpdatable, ILateFrameTickable, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private const int ActiveReactorRegistryCapacity = 128;
        private static int s_x001BioReactorSignalPushDropCount;
        private static readonly BioReactor[] s_activeReactors = new BioReactor[ActiveReactorRegistryCapacity];
        private static int s_activeReactorCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_activeReactorCount; i++)
                s_activeReactors[i] = null;

            s_activeReactorCount = 0;
            s_x001BioReactorSignalPushDropCount = 0;
        }
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Power Settings ─────────────────────────────")]
        [Tooltip("Power output while fueled (Watts).")]
        [SerializeField, Range(10f, 500f)] private float powerOutput = 100f;

        [Tooltip("Fuel consumption rate (units per second).")]
        [SerializeField, Range(0.1f, 10f)] private float fuelConsumptionRate = 1f;

        [Header("── Fuel Storage ───────────────────────────────")]
        [Tooltip("Maximum number of fuel slots.")]
        [SerializeField, Range(1, 8)] private int maxFuelSlots = 4;

        [Tooltip("Item categories accepted as fuel.")]
        [SerializeField] private ItemCategory[] acceptedCategories = { ItemCategory.Material };

        [Tooltip("Default fuel value for items without explicit fuel value.")]
        [SerializeField, Range(10f, 100f)] private float defaultFuelValue = 50f;

        [Header("── Status Indicator ───────────────────────────")]
        [Tooltip("Renderer for the fuel level indicator.")]
        [SerializeField] private Renderer fuelIndicator;

        [Tooltip("Material property for indicator color.")]
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Color when fueled and producing.")]
        [SerializeField] private Color fueledColor = new Color(0.2f, 1f, 0.3f);

        [Tooltip("Color when low fuel.")]
        [SerializeField] private Color lowFuelColor = new Color(1f, 0.5f, 0.1f);

        [Tooltip("Color when empty.")]
        [SerializeField] private Color emptyColor = new Color(0.2f, 0.2f, 0.2f);

        [Tooltip("Fuel level threshold for low fuel warning (0-1).")]
        [SerializeField, Range(0.1f, 0.5f)] private float lowFuelThreshold = 0.25f;

        [Header("── Overheat ───────────────────────────────")]
        [Tooltip("Grid utilization threshold above which the reactor starts accumulating overheat.")]
        [SerializeField, Range(0.5f, 1f)] private float overheatUtilizationThreshold = 0.98f;

        [Tooltip("Seconds the reactor can sustain near-full utilization before damaging the host module.")]
        [SerializeField, Range(1f, 600f)] private float overheatGraceSeconds = 120f;

        [Tooltip("Integrity damage per second applied to the host module once overheat grace is exhausted.")]
        [SerializeField, Range(0f, 50f)] private float overheatIntegrityDamagePerSecond = 4f;

        [Tooltip("Rate at which stored overheat decays when the grid load drops.")]
        [SerializeField, Range(0.1f, 10f)] private float overheatCooldownRate = 1.5f;

        [Tooltip("Explosion radius applied when an overheated reactor catastrophically fails.")]
        [SerializeField, Range(1f, 40f)] private float meltdownRadius = 20f;

        [Tooltip("Peak module damage dealt at the center of the reactor meltdown.")]
        [SerializeField, Range(0f, 200f)] private float meltdownModuleDamage = 90f;

        [Tooltip("Peak player damage dealt at the center of the reactor meltdown.")]
        [SerializeField, Range(0f, 200f)] private float meltdownPlayerDamage = 65f;

        [Tooltip("Layers scanned during the reactor meltdown overlap pass.")]
        [SerializeField] private LayerMask meltdownMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Audio ──────────────────────────────────────")]
        [Tooltip("Sound played when fuel is inserted.")]
        [SerializeField] private AudioClip insertSound;

        [Tooltip("Sound played when fuel is depleted.")]
        [SerializeField] private AudioClip depletedSound;

        [Header("── Events ─────────────────────────────────────")]
        [Tooltip("Fired when fuel level changes. Parameter: normalized fuel level (0-1).")]
        [SerializeField] private UnityEvent<float> OnFuelLevelChanged;

        [Tooltip("Fired when reactor starts producing power.")]
        [SerializeField] private UnityEvent OnReactorStarted;

        [Tooltip("Fired when reactor stops (out of fuel).")]
        [SerializeField] private UnityEvent OnReactorStopped;

        [Tooltip("Fired when fuel is inserted. Parameter: slot index.")]
        [SerializeField] private UnityEvent<int> OnFuelInserted;

        [Tooltip("Fired when fuel is depleted. Parameter: slot index.")]
        [SerializeField] private UnityEvent<int> OnFuelDepleted;

        [Tooltip("Fired when player interacts with the reactor.")]
        [SerializeField] private UnityEvent OnInteract;

        // ══════════════════════════════════════════════════════════
        //  INTERACTION TEXT
        // ══════════════════════════════════════════════════════════

        private const string DefaultInteractText = "Deposit Fuel";
        private const string DefaultInteractFullText = "Reactor Full";
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedInteractTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedInteractFullTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedInteractTextLength;
        private int _cachedInteractFullTextLength;

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
            PlayerInventory playerInventory = _playerRuntime != null ? _playerRuntime.Inventory : null;

            if (playerInventory != null)
            {
                DepositFuelFromInventory(playerInventory);
            }

            OnInteract?.Invoke();
        }

        string IInteractable.GetInteractText()
        {
            return ActiveFuelCount >= maxFuelSlots ? DefaultInteractFullText : DefaultInteractText;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            ReadOnlySpan<char> text = ActiveFuelCount >= maxFuelSlots
                ? _cachedInteractFullTextBuffer.AsSpan(0, _cachedInteractFullTextLength)
                : _cachedInteractTextBuffer.AsSpan(0, _cachedInteractTextLength);
            return InteractableTextCopy.TryCopy(text, destination, out length);
        }

        // ══════════════════════════════════════════════════════════
        //  INVENTORY INTEGRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Deposits fuel from the player's inventory into the reactor.
        /// Searches for accepted fuel items and deposits the first one found.
        /// </summary>
        /// <param name="playerInventory">Player's inventory to search.</param>
        /// <returns>True if fuel was deposited.</returns>
        public bool DepositFuelFromInventory(PlayerInventory playerInventory)
        {
            if (playerInventory == null || playerInventory.Grid == null)
                return false;

            CompactFuelListCold();
            if (ActiveFuelCount >= maxFuelSlots)
                return false;

            InventoryGrid grid = playerInventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            // Search for accepted fuel items
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

                    if (!IsAcceptedFuel(item))
                        continue;

                    // Try to insert fuel
                    if (InsertFuel(item))
                    {
                        // Remove from player inventory
                        playerInventory.RemoveItemAt(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Deposits a specific fuel item from the player's inventory.
        /// </summary>
        /// <param name="playerInventory">Player's inventory.</param>
        /// <param name="item">The item to deposit.</param>
        /// <returns>True if the item was deposited.</returns>
        public bool DepositSpecificFuel(PlayerInventory playerInventory, ItemData item)
        {
            if (playerInventory == null || item == null)
                return false;

            CompactFuelListCold();
            if (ActiveFuelCount >= maxFuelSlots)
                return false;

            if (!IsAcceptedFuel(item))
                return false;

            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId == 0 || !playerInventory.ContainsItem(itemHashId))
                return false;

            if (!InsertFuel(item))
                return false;

            // Remove one unit from player inventory
            playerInventory.TryRemoveQuantity(itemHashId, 1);
            return true;
        }

        /// <summary>
        /// Gets the count of accepted fuel items in the player's inventory.
        /// </summary>
        public int CountFuelInInventory(PlayerInventory playerInventory)
        {
            if (playerInventory == null || playerInventory.Grid == null)
                return 0;

            int count = 0;
            InventoryGrid grid = playerInventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

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
                    if (item != null && IsAcceptedFuel(item))
                    {
                        count += playerInventory.GetStackCount(x, y);
                    }
                }
            }

            return count;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private List<FuelItem> _fuelItems;
        private int _fuelHeadIndex;
        private float _totalFuelCapacity;
        private float _currentFuelLevel;
        private bool _isProducing;
        private bool _wasProducing;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _hasPower = true; // IPowerComponent requirement
        private int _emissionPropertyId;
        private float _overheatTimer;
        private float _debugGridUtilization;
        private bool _meltdownTriggered;
        private bool _fuelIndicatorDirty;
        private bool _pendingInsertAudio;
        private bool _pendingDepletedAudio;
        private bool _pendingReactorStartedEvent;
        private bool _pendingReactorStoppedEvent;
        private bool _pendingFuelLevelChangedEvent;
        private float _pendingFuelLevelChangedValue;
        private int _pendingFuelDepletedEvents;
        private IAudioService _audioService;
        private IPlayerRuntimeContext _playerRuntime;
        private ILocalizationTextReadModel _localizationRuntime;

        // Cached references
        private Transform _cachedTransform;
        private PowerNode _powerNode;
        private BaseModule _hostModule;
        private MaterialPropertyBlock _mpb;
        private static readonly int _EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly Collider[] MeltdownOverlapBuffer = new Collider[48];
        private readonly int[] _damagedModuleIds = new int[24];
        private readonly int[] _damagedSurvivalIds = new int[8];
        private const float MeltdownBurnStatusDurationSeconds = 8f;
        private const float MeltdownRadiationStatusDurationSeconds = 12f;
        private const float MeltdownRadiationDoseScale = 0.1f;
        private const byte MeltdownRadiationDoseKind = 8;

        // ══════════════════════════════════════════════════════════
        //  IPowerComponent IMPLEMENTATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current power output (positive = generation).
        /// </summary>
        public float PowerRating => _isProducing ? powerOutput : 0f;

        /// <summary>
        /// Priority: generators are never disconnected.
        /// </summary>
        public int PowerPriority => 0;

        /// <summary>
        /// Always true for generators.
        /// </summary>
        public bool HasPower => _hasPower;

        /// <summary>
        /// Called by PowerGrid. Generators ignore this.
        /// </summary>
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>True if currently producing power.</summary>
        public bool IsProducing => _isProducing;

        /// <summary>Current fuel level (0-1).</summary>
        public float FuelLevel => _totalFuelCapacity > 0 ? _currentFuelLevel / _totalFuelCapacity : 0f;

        /// <summary>Number of fuel slots in use.</summary>
        public int FuelSlotCount => ActiveFuelCount;

        /// <summary>Maximum fuel slots.</summary>
        public int MaxFuelSlots => maxFuelSlots;

        private int ActiveFuelCount => _fuelItems != null ? math.max(0, _fuelItems.Count - _fuelHeadIndex) : 0;

        internal static int ActiveReactorCount => s_activeReactorCount;

        internal static BioReactor GetActiveReactorAt(int index)
        {
            return index >= 0 && index < s_activeReactorCount ? s_activeReactors[index] : null;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            RefreshColdRegistryReferences();
            _cachedTransform = transform;
            TryGetComponent(out _powerNode);
            TryGetComponent(out _hostModule);
            if (_hostModule == null)
                TryResolveParentComponent(_cachedTransform, out _hostModule);
            _emissionPropertyId = Shader.PropertyToID(string.IsNullOrEmpty(emissionProperty) ? "_EmissionColor" : emissionProperty);
            _mpb = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — per-renderer props — owner: BioReactor
            _fuelItems = new List<FuelItem>(maxFuelSlots); // COLD ALLOC: List<FuelItem>[maxFuelSlots] — fuel storage — owner: BioReactor

            if (fuelIndicator == null)
                TryGetComponent(out fuelIndicator);
        }

        private void OnEnable()
        {
            RefreshColdRegistryReferences();
            RegisterActiveReactor();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegister();
            TryRegisterHotSwap();
            RebuildLocalizedTextCache();
            QueueFuelIndicatorUpdate();
        }

        private void OnDisable()
        {
            UnregisterActiveReactor();
            InteractableRegistry.InvalidateTree(this);
            LocalizationEvents.UnregisterLanguageListener(this);
            TryUnregisterHotSwap();
            TryUnregister();
        }

        private void OnDestroy()
        {
            UnregisterActiveReactor();
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwap();
            TryUnregister();
        }

        private void RegisterActiveReactor()
        {
            for (int i = 0; i < s_activeReactorCount; i++)
            {
                if (ReferenceEquals(s_activeReactors[i], this))
                    return;
            }

            if (s_activeReactorCount >= s_activeReactors.Length)
                return;

            s_activeReactors[s_activeReactorCount] = this;
            s_activeReactorCount++;
        }

        private void UnregisterActiveReactor()
        {
            for (int i = s_activeReactorCount - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(s_activeReactors[i], this))
                    continue;

                int lastIndex = s_activeReactorCount - 1;
                s_activeReactors[i] = s_activeReactors[lastIndex];
                s_activeReactors[lastIndex] = null;
                s_activeReactorCount--;
                return;
            }
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            _fuelIndicatorDirty = false;
            _pendingInsertAudio = false;
            _pendingDepletedAudio = false;
            _pendingReactorStartedEvent = false;
            _pendingReactorStoppedEvent = false;
            _pendingFuelLevelChangedEvent = false;
            _pendingFuelDepletedEvents = 0;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void RefreshColdRegistryReferences()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _playerRuntime = GlobalRegistry.Player;
            _localizationRuntime = GlobalRegistry.LocalizationText;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntime = currentService as IPlayerRuntimeContext;
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationRuntime = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — FUEL CONSUMPTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// ITickable implementation. Handles fuel consumption.
        /// Zero GC: no allocations in hot path.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (ActiveFuelCount == 0)
            {
                if (_isProducing)
                {
                    _isProducing = false;
                    QueueReactorStoppedEvent();
                    NotifyGridBalanceChanged();
                    QueueFuelIndicatorUpdate();
                }

                UpdateOverheat(deltaTime);
                return;
            }

            // Consume fuel
            float fuelToConsume = fuelConsumptionRate * deltaTime;
            ConsumeFuel(fuelToConsume);

            // Update production state
            if (!_isProducing && ActiveFuelCount > 0)
            {
                _isProducing = true;
                QueueReactorStartedEvent();
                NotifyGridBalanceChanged();
            }

            UpdateOverheat(deltaTime);
            QueueFuelIndicatorUpdate();
        }

        public void LateFrameTick()
        {
            if (_fuelIndicatorDirty)
            {
                _fuelIndicatorDirty = false;
                UpdateFuelIndicator();
            }

            IAudioService audio = ResolveAudioService();
            Vector3 audioPosition = _cachedTransform != null ? _cachedTransform.position : transform.position;
            if (_pendingInsertAudio)
            {
                _pendingInsertAudio = false;
                if (insertSound != null && audio != null)
                    audio.PlayAtPoint(insertSound, audioPosition);
            }

            if (_pendingDepletedAudio)
            {
                _pendingDepletedAudio = false;
                if (depletedSound != null && audio != null)
                    audio.PlayAtPoint(depletedSound, audioPosition);
            }

            FlushPendingUnityEvents();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
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

        /// <summary>
        /// Inserts a fuel item into the reactor.
        /// </summary>
        /// <param name="item">The item to insert as fuel.</param>
        /// <returns>True if the item was accepted.</returns>
        public bool InsertFuel(ItemData item)
        {
            CompactFuelListCold();
            if (item == null || ActiveFuelCount >= maxFuelSlots)
                return false;

            // Check if item category is accepted
            if (!IsAcceptedFuel(item))
                return false;

            // Create fuel item
            float fuelValue = GetFuelValue(item);
            FuelItem fuelItem = default;
            fuelItem.itemData = item;
            fuelItem.fuelValue = fuelValue;
            fuelItem.remainingFuel = fuelValue;

            _fuelItems.Add(fuelItem);
            _totalFuelCapacity += fuelValue;
            _currentFuelLevel += fuelValue;

            _pendingInsertAudio = insertSound != null;

            OnFuelInserted?.Invoke(ActiveFuelCount - 1);
            QueueFuelLevelChangedEvent();

            return true;
        }

        /// <summary>
        /// Checks if an item can be used as fuel.
        /// </summary>
        /// <param name="item">The item to check.</param>
        /// <returns>True if the item is accepted as fuel.</returns>
        public bool IsAcceptedFuel(ItemData item)
        {
            if (item == null || acceptedCategories == null)
                return false;

            if (item.resourceFamily == ResourceFamily.Organic)
                return true;

            for (int i = 0; i < acceptedCategories.Length; i++)
            {
                if (item.category == acceptedCategories[i])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the fuel value for an item.
        /// </summary>
        private float GetFuelValue(ItemData item)
        {
            // Future: ItemData could have an explicit fuelValue field
            // For now, use default based on item properties
            return defaultFuelValue;
        }

        // ══════════════════════════════════════════════════════════
        //  FUEL CONSUMPTION LOGIC
        // ══════════════════════════════════════════════════════════

        private void ConsumeFuel(float amount)
        {
            float previousLevel = _currentFuelLevel;
            int depletedCount = 0;

            int activeCount = ActiveFuelCount;
            while (amount > 0 && depletedCount < activeCount)
            {
                FuelItem firstItem = _fuelItems[_fuelHeadIndex + depletedCount];

                if (firstItem.remainingFuel <= amount)
                {
                    // Item is depleted
                    amount -= firstItem.remainingFuel;
                    _currentFuelLevel -= firstItem.remainingFuel;
                    _totalFuelCapacity -= firstItem.fuelValue;
                    depletedCount++;

                    _pendingDepletedAudio = depletedSound != null;
                    QueueFuelDepletedEvent();
                }
                else
                {
                    // Partial consumption
                    firstItem.remainingFuel -= amount;
                    _fuelItems[_fuelHeadIndex + depletedCount] = firstItem;
                    _currentFuelLevel -= amount;
                    amount = 0;
                }
            }

            if (depletedCount > 0)
                _fuelHeadIndex += depletedCount;

            // Fire event if level changed significantly
            if (math.abs(_currentFuelLevel - previousLevel) > 0.1f)
            {
                QueueFuelLevelChangedEvent();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the fuel level indicator using MaterialPropertyBlock.
        /// Zero GC: uses cached MaterialPropertyBlock.
        /// </summary>
        private void CompactFuelListCold()
        {
            if (_fuelHeadIndex <= 0 || _fuelItems == null)
                return;

            if (_fuelHeadIndex >= _fuelItems.Count)
            {
                _fuelItems.Clear();
                _fuelHeadIndex = 0;
                return;
            }

            _fuelItems.RemoveRange(0, _fuelHeadIndex);
            _fuelHeadIndex = 0;
        }

        private void QueueReactorStartedEvent()
        {
            _pendingReactorStartedEvent = true;
        }

        private void QueueReactorStoppedEvent()
        {
            _pendingReactorStoppedEvent = true;
        }

        private void QueueFuelDepletedEvent()
        {
            if (_pendingFuelDepletedEvents < int.MaxValue)
                _pendingFuelDepletedEvents++;
        }

        private void QueueFuelLevelChangedEvent()
        {
            _pendingFuelLevelChangedValue = FuelLevel;
            _pendingFuelLevelChangedEvent = true;
        }

        private void FlushPendingUnityEvents()
        {
            if (_pendingReactorStoppedEvent)
            {
                _pendingReactorStoppedEvent = false;
                OnReactorStopped?.Invoke();
            }

            if (_pendingReactorStartedEvent)
            {
                _pendingReactorStartedEvent = false;
                OnReactorStarted?.Invoke();
            }

            int depletedEvents = _pendingFuelDepletedEvents;
            if (depletedEvents > 0)
            {
                _pendingFuelDepletedEvents = 0;
                for (int i = 0; i < depletedEvents; i++)
                    OnFuelDepleted?.Invoke(0);
            }

            if (_pendingFuelLevelChangedEvent)
            {
                _pendingFuelLevelChangedEvent = false;
                OnFuelLevelChanged?.Invoke(_pendingFuelLevelChangedValue);
            }
        }

        private void UpdateOverheat(float deltaTime)
        {
            if (_meltdownTriggered)
                return;

            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (!_isProducing || grid == null)
            {
                CoolOverheat(deltaTime);
                return;
            }

            float totalGeneration = grid.TotalGeneration;
            if (totalGeneration <= 0.0001f)
            {
                CoolOverheat(deltaTime);
                return;
            }

            float utilization = math.saturate(grid.TotalConsumption / totalGeneration);
            _debugGridUtilization = utilization;

            if (utilization < overheatUtilizationThreshold)
            {
                CoolOverheat(deltaTime);
                return;
            }

            float parasiteOverheatMultiplier = _hostModule != null
                ? math.max(1f, _hostModule.ParasiteBioReactorOverheatMultiplier)
                : 1f;
            _overheatTimer += deltaTime * parasiteOverheatMultiplier;
            if (_hostModule == null || _overheatTimer < overheatGraceSeconds)
                return;

            _hostModule.ApplyDamage(overheatIntegrityDamagePerSecond * deltaTime * parasiteOverheatMultiplier);
            PublishReactorGasLeak(math.saturate(_overheatTimer / math.max(0.001f, overheatGraceSeconds)));
            if (_hostModule.CurrentIntegrity <= 0f)
                TriggerMeltdown();
        }

        private void CoolOverheat(float deltaTime)
        {
            _debugGridUtilization = 0f;
            if (_overheatTimer <= 0f)
                return;

            _overheatTimer = math.max(0f, _overheatTimer - (deltaTime * math.max(0.1f, overheatCooldownRate)));
        }

        private void TriggerMeltdown()
        {
            if (_meltdownTriggered)
                return;

            _meltdownTriggered = true;
            _overheatTimer = 0f;
            _debugGridUtilization = 0f;
            _isProducing = false;
            _currentFuelLevel = 0f;
            _totalFuelCapacity = 0f;
            _fuelItems.Clear();
            _fuelHeadIndex = 0;
            QueueReactorStoppedEvent();
            QueueFuelIndicatorUpdate();
            NotifyGridBalanceChanged();

            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            PublishReactorGasLeak(1f);
            float safeMeltdownRadius = math.max(0.1f, meltdownRadius);
            float meltdownRadiusSq = safeMeltdownRadius * safeMeltdownRadius;
            float inverseMeltdownRadiusSq = math.rcp(meltdownRadiusSq);
            int damagedModuleCount = 0;
            int damagedSurvivalCount = 0;

            int activeModuleCount = BaseModule.ActiveModuleCount;
            for (int i = 0; i < activeModuleCount; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null)
                    continue;

                Vector3 moduleCenter = module.transform.position;
                float moduleRadius = 0f;
                if (module.TryGetInteriorHazardBounds(out Vector3 hazardCenter, out float hazardRadius))
                {
                    moduleCenter = hazardCenter;
                    moduleRadius = math.max(0f, hazardRadius);
                }

                float distanceSq = DistanceSqToSphereSurface(origin, moduleCenter, moduleRadius);
                if (distanceSq >= meltdownRadiusSq)
                    continue;

                float damage01 = 1f - math.saturate(distanceSq * inverseMeltdownRadiusSq);
                if (damage01 <= 0f)
                    continue;

                int moduleId = GetRuntimeId(module);
                if (TryRegisterUniqueId(_damagedModuleIds, ref damagedModuleCount, moduleId))
                    module.ApplyDamage(meltdownModuleDamage * damage01);
            }

            IPlayerRuntimeContext playerContext = _playerRuntime;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            HectonSurvivalSystem survival = playerTransform != null
                ? playerContext.SurvivalSystem
                : null;
            if (survival != null && playerTransform != null)
            {
                float distanceSq = (playerTransform.position - origin).sqrMagnitude;
                if (distanceSq < meltdownRadiusSq)
                {
                    float damage01 = 1f - math.saturate(distanceSq * inverseMeltdownRadiusSq);
                    if (damage01 > 0f)
                    {
                        int survivalId = GetRuntimeId(survival);
                        if (TryRegisterUniqueId(_damagedSurvivalIds, ref damagedSurvivalCount, survivalId))
                        {
                            QueueMeltdownPlayerStatus(survival, playerContext.PlayerHealth, damage01);
                            PublishMeltdownRadiationDose(origin, damage01);
                        }
                    }
                }
            }

            if (_hostModule != null && !_hostModule.IsFlooded)
                _hostModule.ForceFlood();
        }

        private static float DistanceSqToSphereSurface(Vector3 point, Vector3 sphereCenter, float sphereRadius)
        {
            float centerDistance = (point - sphereCenter).magnitude;
            float surfaceDistance = math.max(0f, centerDistance - math.max(0f, sphereRadius));
            return surfaceDistance * surfaceDistance;
        }

        private static float ResolveFiniteSeverity01(float severity01)
        {
            return math.isfinite(severity01) ? math.saturate(severity01) : 0f;
        }

        private void PublishReactorGasLeak(float severity01)
        {
            Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
            if (!TryResolveRuntimeAup(origin, out double3 damageAup))
                return;

            float safeSeverity01 = ResolveFiniteSeverity01(severity01);
            ReactorDamageSignal signal = new ReactorDamageSignal
            {
                DamageAup = damageAup,
                ReactorHash = unchecked((uint)GetRuntimeId(this)),
                Damage01 = safeSeverity01,
                ToxinLeak01 = safeSeverity01,
                Flags = 1
            };
            SignalBus<ReactorDamageSignal>.TryPushTracked(in signal, ref s_x001BioReactorSignalPushDropCount);
        }

        private void QueueMeltdownPlayerStatus(HectonSurvivalSystem survival, HectonPlayerHealth playerHealth, float damage01)
        {
            int targetId = ResolveSurvivalCombatTargetId(survival, playerHealth);
            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return;

            float severity01 = ResolveFiniteSeverity01(damage01);
            if (severity01 <= 0.0001f)
                return;

            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Burning64,
                MeltdownBurnStatusDurationSeconds * math.max(0.25f, severity01),
                DamageSourceIds.EnvironmentHazard,
                severity01);
            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Irradiated64,
                MeltdownRadiationStatusDurationSeconds * math.max(0.25f, severity01),
                DamageSourceIds.EnvironmentHazard,
                severity01);
        }

        private static int ResolveSurvivalCombatTargetId(HectonSurvivalSystem survival, HectonPlayerHealth playerHealth)
        {
            if (survival == null)
                return 0;

            if (playerHealth != null)
                return CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject);

            return CombatDamageRuntime.ResolveTargetId(survival.gameObject);
        }

        private void PublishMeltdownRadiationDose(Vector3 origin, float damage01)
        {
            float severity01 = ResolveFiniteSeverity01(damage01);
            if (severity01 <= 0.0001f || !TryResolveRuntimeAupPosition(origin, out AbsoluteUniversePosition positionAup))
                return;

            RadiationDoseSignal signal = default;
            signal.PositionAup = positionAup;
            signal.Dose = meltdownPlayerDamage * severity01 * MeltdownRadiationDoseScale;
            signal.Intensity01 = severity01;
            signal.SourceId = unchecked((uint)GetRuntimeId(this));
            signal.DoseKind = MeltdownRadiationDoseKind;
            signal.Flags = 1;
            SignalBus<RadiationDoseSignal>.TryPushTracked(in signal, ref s_x001BioReactorSignalPushDropCount);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out double3 positionAup)
        {
            positionAup = default;
            if (!TryResolveRuntimeAupPosition(runtimePosition, out AbsoluteUniversePosition resolvedAup))
                return false;

            positionAup = resolvedAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(positionAup));
        }

        private static bool TryResolveRuntimeAupPosition(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static bool TryRegisterUniqueId(int[] ids, ref int count, int value)
        {
            for (int i = 0; i < count; i++)
            {
                if (ids[i] == value)
                    return false;
            }

            if (count < ids.Length)
            {
                ids[count] = value;
                count++;
            }

            return true;
        }

        private static int GetRuntimeId(Component owner)
        {
            return owner == null
                ? 0
                : unchecked((int)EntityId.ToULong(owner.GetEntityId()));
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component)
            where T : Component
        {
            component = null;
            Transform current = start != null ? start.parent : null;
            while (current != null)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            return false;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = _powerNode != null ? _powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void UpdateFuelIndicator()
        {
            if (fuelIndicator == null)
                return;

            float level = FuelLevel;
            Color indicatorColor;

            if (level <= 0.01f)
            {
                indicatorColor = emptyColor;
            }
            else if (level < lowFuelThreshold)
            {
                indicatorColor = lowFuelColor;
            }
            else
            {
                indicatorColor = fueledColor;
            }

            fuelIndicator.GetPropertyBlock(_mpb);
            _mpb.SetColor(_emissionPropertyId != 0 ? _emissionPropertyId : _EmissionColorID, indicatorColor);
            fuelIndicator.SetPropertyBlock(_mpb);
        }

        private void QueueFuelIndicatorUpdate()
        {
            _fuelIndicatorDirty = true;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (powerOutput < 1f) powerOutput = 1f;
            if (fuelConsumptionRate < 0.1f) fuelConsumptionRate = 0.1f;
            if (maxFuelSlots < 1) maxFuelSlots = 1;
            if (defaultFuelValue < 1f) defaultFuelValue = 1f;
            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw fuel level indicator
            if (Application.isPlaying)
            {
                UnityEditor.Handles.Label(
                    transform.position + Vector3.up * 2f,
                    $"Fuel: {FuelLevel:P0}\nSlots: {FuelSlotCount}/{MaxFuelSlots}\nPower: {PowerRating:F0}W"
                );
            }
        }
#endif

        private void RebuildLocalizedTextCache()
        {
            _cachedInteractTextLength = InteractableTextCopy.CopyLocalizedTruncated(_localizationRuntime, LocalizationKeys.INTERACT_DEPOSIT_FUEL, DefaultInteractText, _cachedInteractTextBuffer);
            _cachedInteractFullTextLength = InteractableTextCopy.CopyLocalizedTruncated(_localizationRuntime, LocalizationKeys.INTERACT_REACTOR_FULL, DefaultInteractFullText, _cachedInteractFullTextBuffer);
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

