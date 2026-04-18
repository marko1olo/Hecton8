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

using Hecton.Localization;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.Inventory;
using Hecton8.Items;
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
    public sealed class StorageCrate : MonoBehaviour, IInteractable
    {
        private const string DefaultOpenText = "Open Crate";
        private const string DefaultAccessText = "Access Crate";
        private const string DefaultLockedText = "Locked";

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
        private string _cachedOpenText;
        private string _cachedAccessText;
        private string _cachedLockedText;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _collider = GetComponent<Collider>();
            _openTriggerHash = Animator.StringToHash(string.IsNullOrEmpty(openTriggerName) ? "Open" : openTriggerName);
            _closeTriggerHash = Animator.StringToHash(string.IsNullOrEmpty(closeTriggerName) ? "Close" : closeTriggerName);

            // Auto-find animator if not assigned
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            RebuildLocalizedTextCache();

            // Set initial state
            _state = initialState;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RebuildLocalizedTextCache();
            // Reset to initial state if needed
            if (_state == CrateState.Opening)
            {
                _state = CrateState.Closed;
            }
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        // ══════════════════════════════════════════════════════════
        //  IInteractable
        // ══════════════════════════════════════════════════════════

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
            switch (_state)
            {
                case CrateState.Closed:
                    return canBeOpened ? _cachedOpenText : _cachedLockedText;

                case CrateState.Open:
                    return _cachedAccessText;

                case CrateState.Locked:
                    return _cachedLockedText;

                default:
                    return null;
            }
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
            if (openSound != null && SpatialAudioManager.TryGetInstance(out var audio))
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

            // If no animator, immediately open
            if (animator == null)
            {
                CompleteOpen();
            }
        }

        /// <summary>
        /// Closes the crate (plays animation).
        /// </summary>
        public void CloseCrate()
        {
            if (_state != CrateState.Open) return;

            _state = CrateState.Closed;

            // Play close sound
            if (closeSound != null && SpatialAudioManager.TryGetInstance(out var audio))
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
            _cachedOpenText = ResolveConfiguredText(openText, DefaultOpenText, LocalizationKeys.INTERACT_OPEN_CRATE);
            _cachedAccessText = ResolveConfiguredText(accessText, DefaultAccessText, LocalizationKeys.INTERACT_ACCESS_CRATE);
            _cachedLockedText = ResolveConfiguredText(lockedText, DefaultLockedText, LocalizationKeys.INTERACT_LOCKED);
        }

        private static string ResolveConfiguredText(string configuredValue, string legacyDefault, string key)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue) &&
                !string.Equals(configuredValue, legacyDefault, System.StringComparison.Ordinal))
            {
                return configuredValue;
            }

            return ResolveLocalized(key, legacyDefault);
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

            ItemData item = containedItems[itemIndex];
            if (item == null) return false;

            // Check if player inventory exists
            if (playerInventory == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[StorageCrate] PlayerInventory is null. Cannot transfer item.");
#endif
                return false;
            }

            // Try to add to player inventory
            if (!playerInventory.TryAddItem(item, 1))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[StorageCrate] Player inventory full. Cannot take {item.itemName}.");
#endif
                return false;
            }

            // Fire item taken event
            OnItemTaken?.Invoke(item);

            // Remove item if configured
            if (removeItemsOnTake)
            {
                containedItems[itemIndex] = null;
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
        /// Takes an item from the crate.
        /// </summary>
        /// <param name="itemIndex">Index of the item to take.</param>
        /// <returns>The ItemData if successful, null otherwise.</returns>
        public ItemData TakeItem(int itemIndex)
        {
            if (_state != CrateState.Open) return null;
            if (containedItems == null || itemIndex < 0 || itemIndex >= containedItems.Length) return null;

            ItemData item = containedItems[itemIndex];
            if (item == null) return null;

            // Fire item taken event
            OnItemTaken?.Invoke(item);

            // Remove item if configured
            if (removeItemsOnTake)
            {
                containedItems[itemIndex] = null;
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

            // Find empty slot or expand array
            for (int i = 0; i < containedItems.Length; i++)
            {
                if (containedItems[i] == null)
                {
                    containedItems[i] = item;
                    return;
                }
            }

            // Expand array if no empty slot
            System.Array.Resize(ref containedItems, containedItems.Length + 1);
            containedItems[containedItems.Length - 1] = item;
        }

        /// <summary>
        /// Locks the crate.
        /// </summary>
        public void Lock()
        {
            _state = CrateState.Locked;
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
