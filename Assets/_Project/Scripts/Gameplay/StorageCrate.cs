// ============================================================================
// HECTON-8 — StorageCrate.cs
// Small storage crate that the player can open to find items.
//
// ARCHITECTURE:
//   • Standalone prop — implements IInteractable.
//   • State machine: Locked → Opening → Open.
//   • Animator integration for opening animation.
//   • UnityEvents for inventory UI integration.
//
// ZERO GC:
//   • No Update() — event-driven via IInteractable.
//   • Cached Transform, Animator.
//   • Pre-cached interaction text.
//   • CompareTag for player detection.
//
// USAGE:
//   1. Place on crate GameObject with mesh and collider.
//   2. Assign Animator with opening animation.
//   3. Configure contained items array.
//   4. Connect OnOpenInventory to inventory UI system.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Power;
using Hecton8.SaveSystem;
using Hecton8.World;
using Hecton.Localization;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// State machine states for crate interaction.
    /// </summary>
    public enum CrateState
    {
        Locked,    // Cannot be opened
        Closed,    // Ready to be opened
        Opening,   // Playing opening animation
        Open       // Opened, inventory accessible
    }

    /// <summary>
    /// Small storage crate that the player can open to find items.
    /// Implements IInteractable for player interaction.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class StorageCrate : MonoBehaviour, IInteractable, IInteractableTextProvider, ILocalizationLanguageChangedListener, IStorageReservationCommitTarget, IGlobalRegistryHotSwapListener
    {
        private const int ActiveCrateRegistryCapacity = 512;
        private const string DefaultOpenText = "Open Crate";
        private const string DefaultAccessText = "Access Crate";
        private const string DefaultLockedText = "Locked";
        private static readonly StorageCrate[] s_activeCrates = new StorageCrate[ActiveCrateRegistryCapacity];
        private static int s_activeCrateCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < s_activeCrateCount; i++)
                s_activeCrates[i] = null;

            s_activeCrateCount = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — STATE
        // ══════════════════════════════════════════════════════════

        [Header("── State ───────────────────────────────────────")]
        [Tooltip("Initial state of the crate.")]
        [SerializeField] private CrateState initialState = CrateState.Closed;

        [Tooltip("Can the crate be opened?")]
        [SerializeField] private bool canBeOpened = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — ANIMATION
        // ══════════════════════════════════════════════════════════

        [Header("── Animation ────────────────────────────────────")]
        [Tooltip("Animator for opening animation.")]
        [SerializeField] private Animator animator;

        [Tooltip("Animation trigger name for opening.")]
        [SerializeField] private string openTriggerName = "Open";

        [Tooltip("Animation trigger name for closing.")]
        [SerializeField] private string closeTriggerName = "Close";

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CONTENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Contents ─────────────────────────────────────")]
        [Tooltip("Items contained in this crate.")]
        [SerializeField] private ItemData[] containedItems;

        [Tooltip("Should items be removed when taken?")]
        [SerializeField] private bool removeItemsOnTake = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played when crate opens.")]
        [SerializeField] private AudioClip openSound;

        [Tooltip("Sound played when crate closes.")]
        [SerializeField] private AudioClip closeSound;

        [Tooltip("Volume for crate sounds.")]
        [SerializeField, Range(0f, 1f)] private float crateVolume = 0.7f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — INTERACTION
        // ══════════════════════════════════════════════════════════

        [Header("── Interaction ──────────────────────────────────")]
        [Tooltip("Interaction text when crate is closed.")]
        [SerializeField] private string openText = DefaultOpenText;

        [Tooltip("Interaction text when crate is open.")]
        [SerializeField] private string accessText = DefaultAccessText;

        [Tooltip("Interaction text when crate is locked.")]
        [SerializeField] private string lockedText = DefaultLockedText;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Invoked when crate starts opening.")]
        [SerializeField] private UnityEvent OnOpening;

        [Tooltip("Invoked when crate is fully opened.")]
        [SerializeField] private UnityEvent OnOpened;

        [Tooltip("Invoked when player accesses the inventory.")]
        [SerializeField] private UnityEvent OnOpenInventory;

        [Tooltip("Invoked when crate is closed.")]
        [SerializeField] private UnityEvent OnClosed;

        [Tooltip("Invoked when an item is taken from the crate.")]
        [SerializeField] private UnityEvent<ItemData> OnItemTaken;

        [Tooltip("Invoked when all items have been taken.")]
        [SerializeField] private UnityEvent OnEmpty;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private Collider _collider;
        private CrateState _state;
        private bool _hotSwapRegistered;
        private bool _registeredActiveCrate;
        private IAudioService _audioService;
        private ILocalizationTextReadModel _localizationManager;
        private PowerNode _logisticsPowerNode;
        private int[] _reservedSlotIds;
        private int[] _containedItemHashIds;

        /// <summary>
        /// Cached animator hash for open trigger.
        /// </summary>
        private int _openTriggerHash;

        /// <summary>
        /// Cached animator hash for close trigger.
        /// </summary>
        private int _closeTriggerHash;

        /// <summary>
        /// Pre-cached interaction text to avoid runtime allocations.
        /// </summary>
        private const int InteractTextBufferCapacity = 96;
        private readonly char[] _cachedOpenTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedAccessTextBuffer = new char[InteractTextBufferCapacity];
        private readonly char[] _cachedLockedTextBuffer = new char[InteractTextBufferCapacity];
        private int _cachedOpenTextLength;
        private int _cachedAccessTextLength;
        private int _cachedLockedTextLength;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Current state of the crate.</summary>
        public CrateState State => _state;

        /// <summary>Is the crate open?</summary>
        public bool IsOpen => _state == CrateState.Open;

        /// <summary>Can the crate be opened?</summary>
        public bool CanBeOpened => canBeOpened && _state == CrateState.Closed;

        /// <summary>Items contained in this crate.</summary>
        public ItemData[] ContainedItems => containedItems;

        internal static int ActiveCrateCount => s_activeCrateCount;

        internal static StorageCrate GetActiveCrateAt(int index)
        {
            return index >= 0 && index < s_activeCrateCount ? s_activeCrates[index] : null;
        }

        internal Transform CachedTransform => _transform;

        internal PowerNode LogisticsPowerNode => _logisticsPowerNode;

        internal void PopulateSaveData(ref ModuleDTO dto)
        {
            dto.storageCrateContentsSerialized = true;
            dto.storageCrateSlotCount = 0;

            string[] itemIds = dto.storageCrateItemIds;
            int[] quantities = dto.storageCrateQuantities;
            if (itemIds == null || quantities == null)
                return;

            int clearCount = Mathf.Min(
                ModuleDTO.MaxStorageCrateSlots,
                Mathf.Min(itemIds.Length, quantities.Length));
            for (int i = 0; i < clearCount; i++)
            {
                itemIds[i] = string.Empty;
                quantities[i] = 0;
            }

            ItemData[] items = containedItems;
            if (items == null || items.Length == 0)
                return;

            EnsureReservationCapacity();

            int writeCount = 0;
            for (int i = 0; i < items.Length; i++)
            {
                ItemData item = items[i];
                if (item == null)
                    continue;

                if (IsReservedSlot(i))
                    continue;

                string persistentId = item.PersistentId;
                if (string.IsNullOrWhiteSpace(persistentId))
                    continue;

                if (!TryAppendStorageCrateSaveEntry(itemIds, quantities, ref writeCount, persistentId))
                    break;
            }

            dto.storageCrateSlotCount = writeCount;
        }

        internal void RestoreFromSaveData(ModuleDTO dto, ItemCatalog itemCatalog)
        {
            if (!dto.storageCrateContentsSerialized)
                return;

            if (!CanResolveStorageCrateRestoreState(in dto, itemCatalog))
                return;

            int requiredSlotCount = CountStorageCrateRestoreSlots(in dto);
            EnsureContainedItemStorageCapacityForRestore(requiredSlotCount);
            ClearContainedItemsForRestore();

            if (requiredSlotCount <= 0 ||
                dto.storageCrateItemIds == null ||
                dto.storageCrateQuantities == null ||
                containedItems == null ||
                containedItems.Length == 0)
            {
                return;
            }

            int entryCount = Mathf.Clamp(
                dto.storageCrateSlotCount,
                0,
                Mathf.Min(
                    ModuleDTO.MaxStorageCrateSlots,
                    Mathf.Min(dto.storageCrateItemIds.Length, dto.storageCrateQuantities.Length)));
            int writeIndex = 0;
            for (int entryIndex = 0; entryIndex < entryCount && writeIndex < containedItems.Length; entryIndex++)
            {
                string itemId = dto.storageCrateItemIds[entryIndex];
                int quantity = Mathf.Clamp(dto.storageCrateQuantities[entryIndex], 0, ModuleDTO.MaxStorageCrateSlots);
                if (quantity <= 0 || string.IsNullOrWhiteSpace(itemId))
                    continue;

                ItemData item = itemCatalog.FindById(itemId);
                if (item == null)
                    continue;

                for (int quantityIndex = 0; quantityIndex < quantity && writeIndex < containedItems.Length; quantityIndex++)
                {
                    containedItems[writeIndex] = item;
                    if (_reservedSlotIds != null && writeIndex < _reservedSlotIds.Length)
                        _reservedSlotIds[writeIndex] = 0;

                    SetContainedItemHash(writeIndex, item);
                    writeIndex++;
                }
            }
        }

        internal void ClearRuntimeContentsForLegacyLoad()
        {
            ClearContainedItemsForRestore();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        internal bool CanEjectContainedContents(
            BaseModule owner,
            PlayerInventory inventory,
            IObjectPoolService pool,
            Vector3 dropPosition)
        {
            if (owner == null || containedItems == null || containedItems.Length == 0)
                return true;

            EnsureReservationCapacity();

            PersistentWorldRegistry persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            int persistentWorldDropCandidateCount = 0;
            for (int i = 0; i < containedItems.Length; i++)
            {
                ItemData item = containedItems[i];
                if (item == null)
                    continue;

                if (IsReservedSlot(i))
                    continue;

                int itemHashId = ItemData.ResolvePersistentHashId(item);
                if (!owner.CanDropItemQuantityToInventoryOrWorld(itemHashId, 1, inventory, pool, dropPosition))
                    return false;

                if (persistentWorldRegistry != null &&
                    (inventory == null || !inventory.CanAcceptItemQuantity(itemHashId, 1)) &&
                    persistentWorldRegistry.CanRegisterDroppedItem(item, 1, dropPosition))
                {
                    persistentWorldDropCandidateCount++;
                }
            }

            return persistentWorldRegistry == null ||
                   persistentWorldRegistry.CanRegisterDroppedItemBatch(persistentWorldDropCandidateCount);
        }

        internal bool EjectContainedContents(
            BaseModule owner,
            PlayerInventory inventory,
            IObjectPoolService pool,
            ref Vector3 dropPosition)
        {
            if (owner == null || containedItems == null || containedItems.Length == 0)
                return true;

            EnsureReservationCapacity();

            bool anyRemoved = false;
            bool allDelivered = true;
            for (int i = 0; i < containedItems.Length; i++)
            {
                ItemData item = containedItems[i];
                if (item == null)
                    continue;

                if (IsReservedSlot(i))
                    continue;

                int itemHashId = ItemData.ResolvePersistentHashId(item);
                if (itemHashId == 0 ||
                    owner.DropItemQuantityToInventoryOrWorld(itemHashId, 1, inventory, pool, ref dropPosition) != 1)
                {
                    allDelivered = false;
                    continue;
                }

                containedItems[i] = null;
                if (_reservedSlotIds != null && i < _reservedSlotIds.Length)
                    _reservedSlotIds[i] = 0;

                SetContainedItemHash(i, null);
                anyRemoved = true;
            }

            if (anyRemoved)
                OnEmpty?.Invoke();

            return allDelivered;
        }

        private void Awake()
        {
            _transform = transform;
            TryGetComponent(out _collider);
            if (!TryGetComponent(out _logisticsPowerNode))
                TryResolveParentComponent(transform, out _logisticsPowerNode);

            _openTriggerHash = Animator.StringToHash(string.IsNullOrEmpty(openTriggerName) ? "Open" : openTriggerName);
            _closeTriggerHash = Animator.StringToHash(string.IsNullOrEmpty(closeTriggerName) ? "Close" : closeTriggerName);

            // Auto-find animator if not assigned
            if (animator == null)
            {
                animator = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Animator>(transform);
            }

            CacheRegistryServicesCold();
            RebuildLocalizedTextCache();

            // Set initial state
            _state = initialState;
            EnsureReservationCapacity();
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

        private void OnEnable()
        {
            RegisterActiveCrate();
            InteractableRegistry.RegisterTree(this);
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegisterHotSwap();
            CacheRegistryServicesCold();
            RebuildLocalizedTextCache();
            BaseLogisticsNetwork.RegisterStorage(this, _logisticsPowerNode);
            // Reset to initial state if needed
            if (_state == CrateState.Opening)
            {
                _state = CrateState.Closed;
            }
        }

        private void OnDisable()
        {
            UnregisterActiveCrate();
            InteractableRegistry.InvalidateTree(this);
            TryUnregisterHotSwap();
            LocalizationEvents.UnregisterLanguageListener(this);
            BaseLogisticsNetwork.UnregisterStorage(this);
        }

        private void OnDestroy()
        {
            UnregisterActiveCrate();
            InteractableRegistry.InvalidateTree(this);
        }

        private void RegisterActiveCrate()
        {
            if (_registeredActiveCrate)
                return;

            for (int i = 0; i < s_activeCrateCount; i++)
            {
                if (ReferenceEquals(s_activeCrates[i], this))
                {
                    _registeredActiveCrate = true;
                    return;
                }
            }

            if (s_activeCrateCount >= s_activeCrates.Length)
                return;

            s_activeCrates[s_activeCrateCount] = this;
            s_activeCrateCount++;
            _registeredActiveCrate = true;
        }

        private void UnregisterActiveCrate()
        {
            if (!_registeredActiveCrate)
                return;

            for (int i = s_activeCrateCount - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(s_activeCrates[i], this))
                    continue;

                int lastIndex = s_activeCrateCount - 1;
                s_activeCrates[i] = s_activeCrates[lastIndex];
                s_activeCrates[lastIndex] = null;
                s_activeCrateCount--;
                break;
            }

            _registeredActiveCrate = false;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _localizationManager = currentService as ILocalizationTextReadModel;
                    RebuildLocalizedTextCache();
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

        private void CacheRegistryServicesCold()
        {
            CacheAudioService(GlobalRegistry.Audio);
            _localizationManager = GlobalRegistry.LocalizationText;
        }

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

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
            {
                return;
            }

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
            {
                return;
            }

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        void IInteractable.OnHoverStart()
        {
            // Could trigger highlight effect here
        }

        void IInteractable.OnHoverEnd()
        {
            // Could disable highlight effect here
        }

        void IInteractable.Interact(Transform interactor)
        {
            switch (_state)
            {
                case CrateState.Closed:
                    OpenCrate();
                    break;

                case CrateState.Open:
                    AccessInventory();
                    break;

                case CrateState.Locked:
                    // Play locked sound or feedback
                    break;
            }
        }

        string IInteractable.GetInteractText()
        {
            return ResolveInteractTextLegacy();
        }

        private string ResolveInteractTextLegacy()
        {
            switch (_state)
            {
                case CrateState.Closed:
                    return canBeOpened ? ResolveLegacyConfigured(openText, DefaultOpenText) : ResolveLegacyConfigured(lockedText, DefaultLockedText);

                case CrateState.Open:
                    return ResolveLegacyConfigured(accessText, DefaultAccessText);

                case CrateState.Locked:
                    return ResolveLegacyConfigured(lockedText, DefaultLockedText);

                default:
                    return null;
            }
        }

        private ReadOnlySpan<char> ResolveInteractTextSpan()
        {
            switch (_state)
            {
                case CrateState.Closed:
                    return canBeOpened
                        ? _cachedOpenTextBuffer.AsSpan(0, _cachedOpenTextLength)
                        : _cachedLockedTextBuffer.AsSpan(0, _cachedLockedTextLength);
                case CrateState.Open:
                    return _cachedAccessTextBuffer.AsSpan(0, _cachedAccessTextLength);
                case CrateState.Locked:
                    return _cachedLockedTextBuffer.AsSpan(0, _cachedLockedTextLength);
                default:
                    return ReadOnlySpan<char>.Empty;
            }
        }

        private static string ResolveLegacyConfigured(string configuredValue, string legacyDefault)
        {
            return !string.IsNullOrWhiteSpace(configuredValue) &&
                   !string.Equals(configuredValue, legacyDefault, StringComparison.Ordinal)
                ? configuredValue
                : legacyDefault;
        }

        public bool TryCopyInteractText(System.Span<char> destination, out int length)
        {
            return InteractableTextCopy.TryCopy(ResolveInteractTextSpan(), destination, out length);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Opens the crate (plays animation).
        /// </summary>
        public void OpenCrate()
        {
            if (!canBeOpened) return;
            if (_state != CrateState.Closed) return;

            _state = CrateState.Opening;

            // Play open sound
            IAudioService audio = ResolveAudioService();
            if (openSound != null && audio != null)
            {
                audio.PlayAtPoint(openSound, _transform.position, crateVolume);
            }

            // Trigger animation
            if (animator != null)
            {
                animator.SetTrigger(_openTriggerHash);
            }

            // Fire opening event
            OnOpening?.Invoke();

            // Gameplay owns inventory access. Animation events are presentation-only.
            CompleteOpen();
        }

        /// <summary>
        /// Closes the crate (plays animation).
        /// </summary>
        public void CloseCrate()
        {
            if (_state != CrateState.Open) return;

            _state = CrateState.Closed;

            // Play close sound
            IAudioService audio = ResolveAudioService();
            if (closeSound != null && audio != null)
            {
                audio.PlayAtPoint(closeSound, _transform.position, crateVolume);
            }

            // Trigger animation
            if (animator != null)
            {
                animator.SetTrigger(_closeTriggerHash);
            }

            // Fire closed event
            OnClosed?.Invoke();
        }

        /// <summary>
        /// Accesses the crate inventory.
        /// </summary>
        public void AccessInventory()
        {
            if (_state != CrateState.Open) return;

            // Fire inventory event
            OnOpenInventory?.Invoke();
        }

        /// <summary>
        /// Called by animation event when opening animation completes.
        /// </summary>
        public void OnAnimationComplete()
        {
            if (_state == CrateState.Opening)
            {
                CompleteOpen();
            }
        }

        private void RebuildLocalizedTextCache()
        {
            _cachedOpenTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(openText, DefaultOpenText, LocalizationKeys.INTERACT_OPEN_CRATE, _localizationManager, _cachedOpenTextBuffer);
            _cachedAccessTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(accessText, DefaultAccessText, LocalizationKeys.INTERACT_ACCESS_CRATE, _localizationManager, _cachedAccessTextBuffer);
            _cachedLockedTextLength = InteractableTextCopy.CopyConfiguredOrLocalizedTruncated(lockedText, DefaultLockedText, LocalizationKeys.INTERACT_LOCKED, _localizationManager, _cachedLockedTextBuffer);
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RebuildLocalizedTextCache();
        }

        /// <summary>
        /// Takes an item from the crate and transfers it to the player's inventory.
        /// </summary>
        /// <param name="itemIndex">Index of the item to take.</param>
        /// <param name="playerInventory">Player's inventory to transfer to.</param>
        /// <returns>True if the item was successfully transferred.</returns>
        public bool TakeItemToInventory(int itemIndex, PlayerInventory playerInventory)
        {
            if (_state != CrateState.Open) return false;
            if (containedItems == null || itemIndex < 0 || itemIndex >= containedItems.Length) return false;
            EnsureReservationCapacity();
            if (IsReservedSlot(itemIndex)) return false;

            ItemData item = containedItems[itemIndex];
            if (item == null) return false;

            // Check if player inventory exists
            if (playerInventory == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[StorageCrate] PlayerInventory is null. Cannot transfer item.");
#endif
                return false;
            }

            // Try to add to player inventory
            int itemHashId = ItemData.ResolvePersistentHashId(item);
            if (itemHashId == 0 || !playerInventory.TryAddItem(itemHashId, 1))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.Log($"[StorageCrate] Player inventory full. Cannot take {item.itemName}.");
#endif
                return false;
            }

            // Fire item taken event
            OnItemTaken?.Invoke(item);

            // Remove item if configured
            if (removeItemsOnTake)
            {
                containedItems[itemIndex] = null;
                if (_reservedSlotIds != null && itemIndex < _reservedSlotIds.Length)
                    _reservedSlotIds[itemIndex] = 0;

                SetContainedItemHash(itemIndex, null);
            }

            // Check if crate is now empty
            if (removeItemsOnTake && IsEmpty())
            {
                OnEmpty?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Takes all items from the crate and transfers them to the player's inventory.
        /// </summary>
        /// <param name="playerInventory">Player's inventory to transfer to.</param>
        /// <returns>Number of items successfully transferred.</returns>
        public int TakeAllToInventory(PlayerInventory playerInventory)
        {
            if (_state != CrateState.Open) return 0;
            if (containedItems == null || playerInventory == null) return 0;

            int transferred = 0;

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] != null)
                {
                    if (TakeItemToInventory(i, playerInventory))
                    {
                        transferred++;
                    }
                }
            }

            return transferred;
        }

        /// <summary>
        /// Checks if the crate is empty.
        /// </summary>
        public bool IsEmpty()
        {
            if (containedItems == null) return true;

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] != null)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Counts how many units of the requested item are currently stored inside this crate.
        /// Used by the base logistics network; does not require the crate to be open.
        /// </summary>
        public int CountItem(ItemData item)
        {
            if (containedItems == null || item == null)
                return 0;

            int count = 0;
            for (int i = 0; i < containedItems.Length; i++)
            {
                if (ReferenceEquals(containedItems[i], item) && !IsReservedSlot(i))
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Counts unreserved items by deterministic runtime hash for alloc-free logistics queries.
        /// </summary>
        public int CountItemByHash(int itemHashId)
        {
            if (containedItems == null || itemHashId == 0)
                return 0;

            EnsureReservationCapacity();

            int count = 0;
            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null || IsReservedSlot(i))
                    continue;

                if (_containedItemHashIds != null &&
                    i < _containedItemHashIds.Length &&
                    _containedItemHashIds[i] == itemHashId)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Removes a single matching item for logistics consumption.
        /// This bypasses the open/closed interaction state because internal base routing is not a player action.
        /// </summary>
        public bool TryConsumeItem(ItemData item)
        {
            if (containedItems == null || item == null)
                return false;

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (!ReferenceEquals(containedItems[i], item) || IsReservedSlot(i))
                    continue;

                containedItems[i] = null;
                _reservedSlotIds[i] = 0;
                SetContainedItemHash(i, null);
                if (IsEmpty())
                    OnEmpty?.Invoke();

                return true;
            }

            return false;
        }

        public bool TryConsumeItemByHash(int itemHashId)
        {
            if (containedItems == null || itemHashId == 0)
                return false;

            EnsureContainedItemHashCache();
            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null || IsReservedSlot(i))
                    continue;

                if (_containedItemHashIds == null ||
                    i >= _containedItemHashIds.Length ||
                    _containedItemHashIds[i] != itemHashId)
                {
                    continue;
                }

                containedItems[i] = null;
                _reservedSlotIds[i] = 0;
                SetContainedItemHash(i, null);
                if (IsEmpty())
                    OnEmpty?.Invoke();

                return true;
            }

            return false;
        }

        /// <summary>
        /// Takes an item from the crate.
        /// </summary>
        /// <param name="itemIndex">Index of the item to take.</param>
        /// <returns>The ItemData if successful, null otherwise.</returns>
        public ItemData TakeItem(int itemIndex)
        {
            if (_state != CrateState.Open) return null;
            if (containedItems == null || itemIndex < 0 || itemIndex >= containedItems.Length) return null;
            if (IsReservedSlot(itemIndex)) return null;

            ItemData item = containedItems[itemIndex];
            if (item == null) return null;

            // Fire item taken event
            OnItemTaken?.Invoke(item);

            // Remove item if configured
            if (removeItemsOnTake)
            {
                containedItems[itemIndex] = null;
                SetContainedItemHash(itemIndex, null);
            }

            // Check if crate is now empty
            if (removeItemsOnTake && IsEmpty())
            {
                OnEmpty?.Invoke();
            }

            return item;
        }

        /// <summary>
        /// Adds an item to the crate.
        /// </summary>
        /// <param name="item">Item to add.</param>
        public void AddItem(ItemData item)
        {
            if (item == null) return;

            EnsureReservationCapacity();

            // Find empty slot or expand array
            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null)
                {
                    containedItems[i] = item;
                    _reservedSlotIds[i] = 0;
                    SetContainedItemHash(i, item);
                    return;
                }
            }

            // Expand array if no empty slot
            System.Array.Resize(ref containedItems, containedItems.Length + 1);
            System.Array.Resize(ref _reservedSlotIds, containedItems.Length);
            System.Array.Resize(ref _containedItemHashIds, containedItems.Length);
            containedItems[containedItems.Length - 1] = item;
            _reservedSlotIds[containedItems.Length - 1] = 0;
            SetContainedItemHash(containedItems.Length - 1, item);
        }

        /// <summary>
        /// Automated logistics insert that respects authored crate limits and never resizes storage at runtime.
        /// </summary>
        public bool TryAddAutomatedItem(ItemData item)
        {
            if (item == null || containedItems == null)
                return false;

            EnsureReservationCapacity();

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] != null)
                    continue;

                containedItems[i] = item;
                _reservedSlotIds[i] = 0;
                SetContainedItemHash(i, item);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether the crate still has a free slot available for logistics automation.
        /// </summary>
        public bool HasAutomatedCapacity()
        {
            if (containedItems == null)
                return false;

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Reserve one matching unreserved item slot for a logistics transaction.
        /// </summary>
        public bool TryReserveItem(ItemData item, int reservationId)
        {
            if (containedItems == null || item == null || reservationId <= 0)
                return false;

            EnsureReservationCapacity();

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (!ReferenceEquals(containedItems[i], item) || IsReservedSlot(i))
                    continue;

                _reservedSlotIds[i] = reservationId;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reserve one matching unreserved item slot by deterministic runtime hash.
        /// </summary>
        public bool TryReserveItemByHash(int itemHashId, int reservationId)
        {
            if (containedItems == null || itemHashId == 0 || reservationId <= 0)
                return false;

            EnsureReservationCapacity();

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null || IsReservedSlot(i))
                    continue;

                if (_containedItemHashIds == null ||
                    i >= _containedItemHashIds.Length ||
                    _containedItemHashIds[i] != itemHashId)
                {
                    continue;
                }

                _reservedSlotIds[i] = reservationId;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reserve the first unreserved item slot regardless of item type. Used by generalized automation links.
        /// </summary>
        public bool TryReserveAnyItem(int reservationId, out ItemData item)
        {
            item = null;

            if (containedItems == null || reservationId <= 0)
                return false;

            EnsureReservationCapacity();

            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null || IsReservedSlot(i))
                    continue;

                _reservedSlotIds[i] = reservationId;
                item = containedItems[i];
                return true;
            }

            return false;
        }

        /// <summary>
        /// Release all slot reservations belonging to the provided logistics transaction.
        /// </summary>
        public void ReleaseReservation(int reservationId)
        {
            if (_reservedSlotIds == null || reservationId <= 0)
                return;

            for (int i = 0; i < _reservedSlotIds.Length; i++)
            {
                if (_reservedSlotIds[i] == reservationId)
                    _reservedSlotIds[i] = 0;
            }
        }

        /// <summary>
        /// Commit all slot reservations belonging to the provided logistics transaction.
        /// </summary>
        public void CommitReservation(int reservationId)
        {
            TryCommitReservation(reservationId);
        }

        /// <summary>
        /// Commit all slot reservations belonging to the provided logistics transaction and report whether inventory changed.
        /// </summary>
        public bool TryCommitReservation(int reservationId)
        {
            if (containedItems == null || _reservedSlotIds == null || reservationId <= 0)
                return false;

            bool anyRemoved = false;
            for (int i = 0; i < containedItems.Length; i++)
            {
                if (_reservedSlotIds[i] != reservationId)
                    continue;

                if (containedItems[i] != null)
                {
                    containedItems[i] = null;
                    anyRemoved = true;
                }

                _reservedSlotIds[i] = 0;
                SetContainedItemHash(i, null);
            }

            if (anyRemoved && IsEmpty())
                OnEmpty?.Invoke();

            return anyRemoved;
        }

        /// <summary>
        /// Locks the crate.
        /// </summary>
        public void Lock()
        {
            _state = CrateState.Locked;
        }

        private void EnsureReservationCapacity()
        {
            int itemCount = containedItems != null ? containedItems.Length : 0;
            if (itemCount <= 0)
            {
                if (_reservedSlotIds == null)
                    _reservedSlotIds = new int[0];

                return;
            }

            if (_reservedSlotIds == null || _reservedSlotIds.Length != itemCount)
                System.Array.Resize(ref _reservedSlotIds, itemCount);

            if (_containedItemHashIds == null || _containedItemHashIds.Length != itemCount)
                System.Array.Resize(ref _containedItemHashIds, itemCount);

            for (int i = 0; i < itemCount; i++)
            {
                ItemData item = containedItems[i];
                _containedItemHashIds[i] = item != null ? ComputeContainedItemHash(item) : 0;
            }
        }

        private void EnsureContainedItemHashCache()
        {
            EnsureReservationCapacity();
        }

        private static bool TryAppendStorageCrateSaveEntry(
            string[] itemIds,
            int[] quantities,
            ref int writeCount,
            string persistentId)
        {
            int capacity = Mathf.Min(
                ModuleDTO.MaxStorageCrateSlots,
                Mathf.Min(itemIds != null ? itemIds.Length : 0, quantities != null ? quantities.Length : 0));
            for (int i = 0; i < writeCount && i < capacity; i++)
            {
                if (!string.Equals(itemIds[i], persistentId, StringComparison.Ordinal))
                    continue;

                quantities[i] = Mathf.Min(ModuleDTO.MaxStorageCrateSlots, quantities[i] + 1);
                return true;
            }

            if (writeCount >= capacity)
                return false;

            itemIds[writeCount] = persistentId;
            quantities[writeCount] = 1;
            writeCount++;
            return true;
        }

        private static int CountStorageCrateRestoreSlots(in ModuleDTO dto)
        {
            if (!dto.storageCrateContentsSerialized ||
                dto.storageCrateItemIds == null ||
                dto.storageCrateQuantities == null)
            {
                return 0;
            }

            int entryCount = Mathf.Clamp(
                dto.storageCrateSlotCount,
                0,
                Mathf.Min(
                    ModuleDTO.MaxStorageCrateSlots,
                    Mathf.Min(dto.storageCrateItemIds.Length, dto.storageCrateQuantities.Length)));
            int totalQuantity = 0;
            for (int i = 0; i < entryCount; i++)
            {
                if (string.IsNullOrWhiteSpace(dto.storageCrateItemIds[i]))
                    continue;

                int quantity = Mathf.Clamp(dto.storageCrateQuantities[i], 0, ModuleDTO.MaxStorageCrateSlots);
                if (quantity <= 0)
                    continue;

                totalQuantity = Mathf.Min(ModuleDTO.MaxStorageCrateSlots, totalQuantity + quantity);
            }

            return totalQuantity;
        }

        private static bool CanResolveStorageCrateRestoreState(in ModuleDTO dto, ItemCatalog itemCatalog)
        {
            if (!dto.storageCrateContentsSerialized)
                return true;

            if (dto.storageCrateSlotCount <= 0)
                return true;

            if (dto.storageCrateItemIds == null || dto.storageCrateQuantities == null)
                return false;

            int entryCount = Mathf.Clamp(
                dto.storageCrateSlotCount,
                0,
                ModuleDTO.MaxStorageCrateSlots);
            if (dto.storageCrateItemIds.Length < entryCount ||
                dto.storageCrateQuantities.Length < entryCount)
            {
                return false;
            }

            for (int i = 0; i < entryCount; i++)
            {
                int quantity = Mathf.Clamp(dto.storageCrateQuantities[i], 0, ModuleDTO.MaxStorageCrateSlots);
                if (quantity <= 0)
                    continue;

                string itemId = dto.storageCrateItemIds[i];
                if (string.IsNullOrWhiteSpace(itemId))
                    return false;

                if (itemCatalog == null || itemCatalog.FindById(itemId.Trim()) == null)
                    return false;
            }

            return true;
        }

        private void EnsureContainedItemStorageCapacityForRestore(int requiredSlotCount)
        {
            int safeRequiredSlotCount = Mathf.Clamp(requiredSlotCount, 0, ModuleDTO.MaxStorageCrateSlots);
            int currentSlotCount = containedItems != null ? containedItems.Length : 0;
            if (currentSlotCount < safeRequiredSlotCount)
                System.Array.Resize(ref containedItems, safeRequiredSlotCount);

            EnsureReservationCapacity();
        }

        private void ClearContainedItemsForRestore()
        {
            int itemCount = containedItems != null ? containedItems.Length : 0;
            if (itemCount <= 0)
            {
                EnsureReservationCapacity();
                if (_containedItemHashIds != null && _containedItemHashIds.Length != 0)
                    System.Array.Resize(ref _containedItemHashIds, 0);
                return;
            }

            EnsureReservationCapacity();
            for (int i = 0; i < itemCount; i++)
            {
                containedItems[i] = null;
                if (_reservedSlotIds != null && i < _reservedSlotIds.Length)
                    _reservedSlotIds[i] = 0;

                SetContainedItemHash(i, null);
            }
        }

        private bool IsReservedSlot(int index)
        {
            return _reservedSlotIds != null &&
                   index >= 0 &&
                   index < _reservedSlotIds.Length &&
                   _reservedSlotIds[index] != 0;
        }

        private void SetContainedItemHash(int index, ItemData item)
        {
            if (_containedItemHashIds == null || index < 0 || index >= _containedItemHashIds.Length)
                return;

            _containedItemHashIds[index] = item != null ? ComputeContainedItemHash(item) : 0;
        }

        private static int ComputeContainedItemHash(ItemData item)
        {
            return ItemData.ResolvePersistentHashId(item);
        }

        /// <summary>
        /// Unlocks the crate.
        /// </summary>
        public void Unlock()
        {
            if (_state == CrateState.Locked)
            {
                _state = CrateState.Closed;
            }
        }

        /// <summary>
        /// Resets the crate to closed state.
        /// </summary>
        public void ResetCrate()
        {
            _state = canBeOpened ? CrateState.Closed : CrateState.Locked;

            // Reset animator
            if (animator != null)
            {
                animator.Rebind();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE METHODS
        // ══════════════════════════════════════════════════════════

        private void CompleteOpen()
        {
            if (_state == CrateState.Open)
                return;

            _state = CrateState.Open;

            // Fire opened event
            OnOpened?.Invoke();
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            RebuildLocalizedTextCache();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw crate state indicator
            Gizmos.color = _state switch
            {
                CrateState.Open => new Color(0f, 1f, 0.5f, 0.3f),
                CrateState.Locked => new Color(1f, 0.3f, 0.3f, 0.3f),
                _ => new Color(0.8f, 0.6f, 0.2f, 0.3f)
            };
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
#endif
    }
}

