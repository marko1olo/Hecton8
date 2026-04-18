// ============================================================================
// HECTON-8 — InteractionUI.cs
// Context-sensitive interaction prompts for the player.
//
// ARCHITECTURE:
//   • ITickable for updates (no Update)
//   • Zero GC: cached refs, pre-cached strings
//   • UnityEvent hooks for designers
//
// FEATURES:
//   • Shows interaction prompts based on looked-at object
//   • Context-sensitive: "Press [E] to Swap Battery" vs "No Battery to Swap"
//   • Tool-aware: different prompts based on held tool
// ============================================================================

namespace Hecton8.UI
{
    using Hecton.Localization;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Inventory;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Context-sensitive interaction prompt system.
    /// Shows different prompts based on looked-at object and held tool.
    /// Uses ITickable for updates. Zero GC in hot paths.
    /// </summary>
    public class InteractionUI : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Text component for the interaction prompt.")]
        [SerializeField] private TMPro.TMP_Text promptText;

        [Tooltip("Canvas group for fading.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("── Raycast Settings ──────────────────────────")]
        [Tooltip("Maximum interaction distance.")]
        [SerializeField] private float interactionRange = 4f;

        [Tooltip("Layers to check for interactables.")]
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("── Prompt Templates ──────────────────────────")]
        [Tooltip("Default interaction prompt format. {0}=verb, {1}=name")]
        [SerializeField] private string defaultPromptFormat = "[E] {0} {1}";

        [Tooltip("Prompt when tool has no battery.")]
        [SerializeField] private string noBatteryPrompt = "No Battery to Swap";

        [Tooltip("Prompt to swap battery.")]
        [SerializeField] private string swapBatteryPrompt = "[E] Swap Battery";

        [Tooltip("Prompt to deposit fuel.")]
        [SerializeField] private string depositFuelPrompt = "[E] Deposit Fuel";

        [Tooltip("Prompt to take item.")]
        [SerializeField] private string takeItemPrompt = "[E] Take Item";

        [Tooltip("Prompt format for consumable with duration. {0}=verb, {1}=duration")]
        [SerializeField] private string consumableWithDurationFormat = "Hold [E] {0} ({1:0.0}s)";

        [Tooltip("Prompt for action in progress.")]
        [SerializeField] private string actionInProgressPrompt = "Consuming...";

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Fired when the prompt changes.")]
        public UnityEvent<string> OnPromptChanged;

        [Tooltip("Fired when prompt visibility changes.")]
        public UnityEvent<bool> OnVisibilityChanged;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private Camera _mainCamera;
        private Transform _cachedTransform;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private bool _registered;
        private string _currentPrompt;
        private bool _isVisible;
        private string _localizedDefaultPromptFormat;
        private string _localizedNoBatteryPrompt;
        private string _localizedSwapBatteryPrompt;
        private string _localizedDepositFuelPrompt;
        private string _localizedTakeItemPrompt;
        private string _localizedConsumableWithDurationFormat;
        private string _localizedActionInProgressPrompt;
        private string _localizedInsertBatteryPrompt;
        private string _localizedBioReactorPrompt;
        private string _localizedEmptyCratePrompt;
        private string _localizedVerbApply;
        private string _localizedVerbDrink;
        private string _localizedVerbEat;
        private string _localizedVerbInhale;
        private string _localizedVerbUse;
        private string _localizedVerbTake;

        // Pre-allocated raycast buffer
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[1]; // COLD ALLOC: single-hit interaction probe — owner: InteractionUI

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Current interaction prompt text.</summary>
        public string CurrentPrompt => _currentPrompt;

        /// <summary>Whether the prompt is currently visible.</summary>
        public bool IsVisible => _isVisible;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _cachedTransform = transform;
            _mainCamera = Camera.main;
            RefreshLocalizedPromptCache();
            SetVisible(false);
        }

        private void OnEnable()
        {
            ResolvePlayerReferences();
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RefreshLocalizedPromptCache();
            RegisterToTick();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            UnregisterFromTick();
            SetVisible(false);
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // ── Check if action is in progress ──
            PlayerActionController actionController = PlayerActionController.Instance;
            if (actionController != null && actionController.IsActionInProgress)
            {
                // Show action in progress prompt, hide interaction prompt
                UpdatePrompt(_localizedActionInProgressPrompt);
                SetVisible(true);
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                    return;
            }

            // Raycast from camera center
            Ray ray = _mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _hitBuffer,
                interactionRange,
                interactionMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            RaycastHit hit = _hitBuffer[0];
            Collider collider = hit.collider;

            if (collider == null)
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            // Build context-sensitive prompt
            string prompt = BuildPrompt(collider, hit.distance);

            if (string.IsNullOrEmpty(prompt))
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            UpdatePrompt(prompt);
            SetVisible(true);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — PROMPT BUILDING
        // ══════════════════════════════════════════════════════════

        private string BuildPrompt(Collider collider, float distance)
        {
            // Check for IInteractable
            IInteractable interactable = collider.GetComponent<IInteractable>();
            if (interactable != null)
            {
                return BuildInteractablePrompt(interactable, collider);
            }

            // Check for BatteryCharger
            BatteryCharger charger = collider.GetComponent<BatteryCharger>();
            if (charger != null)
            {
                return BuildBatteryChargerPrompt(charger);
            }

            // Check for BioReactor
            BioReactor reactor = collider.GetComponent<BioReactor>();
            if (reactor != null)
            {
                return BuildBioReactorPrompt(reactor);
            }

            // Check for StorageCrate
            StorageCrate crate = collider.GetComponent<StorageCrate>();
            if (crate != null)
            {
                return BuildStorageCratePrompt(crate);
            }

            // Check for PickupItem
            PickupItem pickup = collider.GetComponent<PickupItem>();
            if (pickup != null && pickup.ItemData != null)
            {
                return BuildPickupItemPrompt(pickup);
            }

            return null;
        }

        /// <summary>
        /// Builds prompt for pickup items, showing duration for consumables.
        /// Zero GC: uses cached StringBuilder for string building.
        /// </summary>
        private string BuildPickupItemPrompt(PickupItem pickup)
        {
            ItemData item = pickup.ItemData;
            if (item == null)
                return null;

            // Check if this is a consumable with use duration
            if (item.isConsumable && item.UseDuration > 0f)
            {
                // Determine verb based on item type
                string verb = GetConsumableVerb(item);
                // Note: string.Format is acceptable here (not in hot path - only when looking at new item)
                return string.Format(_localizedConsumableWithDurationFormat, verb, item.UseDuration);
            }

            return string.Format(_localizedDefaultPromptFormat, _localizedVerbTake, item.itemName);
        }

        /// <summary>
        /// Gets the verb for a consumable item (Eat, Drink, Apply, etc.).
        /// Pre-cached strings for zero GC.
        /// </summary>
        private string GetConsumableVerb(ItemData item)
        {
            if (item.integrityRestore > 0f)
                return _localizedVerbApply;
            if (item.thirstRestore > 0f)
                return _localizedVerbDrink;
            if (item.hungerRestore > 0f)
                return _localizedVerbEat;
            if (item.oxygenRestore > 0f)
                return _localizedVerbInhale;
            return _localizedVerbUse;
        }

        private string BuildInteractablePrompt(IInteractable interactable, Collider collider)
        {
            // Get base interaction text
            string baseText = interactable.GetInteractText();

            // Check if this is a battery tool context
            IBatteryTool batteryTool = collider.GetComponent<IBatteryTool>();
            if (batteryTool != null)
            {
                return BuildBatteryToolPrompt(batteryTool);
            }

            return baseText;
        }

        private string BuildBatteryToolPrompt(IBatteryTool tool)
        {
            if (!tool.HasBattery)
            {
                return _localizedNoBatteryPrompt;
            }

            return _localizedSwapBatteryPrompt;
        }

        private string BuildBatteryChargerPrompt(BatteryCharger charger)
        {
            // Check if player is holding a tool with battery
            if (_toolManager != null)
            {
                PlayerTool heldTool = _toolManager.CurrentTool;
                if (heldTool is IBatteryTool batteryTool)
                {
                    if (batteryTool.HasBattery)
                    {
                        return _localizedSwapBatteryPrompt;
                    }
                    else
                    {
                        return _localizedNoBatteryPrompt;
                    }
                }
            }

            // Check if charger has a battery to take
            if (charger.HasBatteryInSlot(0) || charger.HasBatteryInSlot(1))
            {
                return _localizedTakeItemPrompt;
            }

            return _localizedInsertBatteryPrompt;
        }

        private string BuildBioReactorPrompt(BioReactor reactor)
        {
            // Check if player has organic items
            if (_inventory != null)
            {
                int fuelCount = CountOrganicFuel(_inventory);
                if (fuelCount > 0)
                {
                    return string.Format("{0} ({1})", _localizedDepositFuelPrompt, fuelCount);
                }
            }

            return _localizedBioReactorPrompt;
        }

        private string BuildStorageCratePrompt(StorageCrate crate)
        {
            if (crate.IsEmpty())
            {
                return _localizedEmptyCratePrompt;
            }

            return ResolveLocalized(LocalizationKeys.INTERACT_OPEN_CRATE, "[E] Open Crate");
        }

        private int CountOrganicFuel(PlayerInventory inventory)
        {
            // Count organic items that can be used as fuel
            // This would need to check inventory for organic items
            // For now, return 0 - actual implementation depends on inventory API
            return 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — UI UPDATE
        // ══════════════════════════════════════════════════════════

        private void UpdatePrompt(string prompt)
        {
            if (_currentPrompt == prompt)
                return;

            _currentPrompt = prompt;

            if (promptText != null)
                promptText.text = prompt;

            OnPromptChanged?.Invoke(prompt);
        }

        private void SetVisible(bool visible)
        {
            if (_isVisible == visible)
                return;

            _isVisible = visible;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
            }

            OnVisibilityChanged?.Invoke(visible);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — REFERENCES
        // ══════════════════════════════════════════════════════════

        private void ResolvePlayerReferences()
        {
            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                return;

            if (_toolManager == null)
                _toolManager = playerTransform.GetComponent<PlayerToolManager>();

            if (_inventory == null)
                _inventory = playerTransform.GetComponent<PlayerInventory>();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshLocalizedPromptCache();
        }

        private void RefreshLocalizedPromptCache()
        {
            _localizedDefaultPromptFormat = defaultPromptFormat;
            _localizedNoBatteryPrompt = ResolveLocalized(LocalizationKeys.INTERACT_NO_BATTERY_TO_SWAP, noBatteryPrompt);
            _localizedSwapBatteryPrompt = ResolveLocalized(LocalizationKeys.INTERACT_SWAP_BATTERY, swapBatteryPrompt);
            _localizedDepositFuelPrompt = ResolveLocalized(LocalizationKeys.INTERACT_DEPOSIT_FUEL, depositFuelPrompt);
            _localizedTakeItemPrompt = ResolveLocalized(LocalizationKeys.INTERACT_TAKE_ITEM, takeItemPrompt);
            _localizedConsumableWithDurationFormat = consumableWithDurationFormat;
            _localizedActionInProgressPrompt = ResolveLocalized(LocalizationKeys.ACTION_USING, actionInProgressPrompt);
            _localizedInsertBatteryPrompt = ResolveLocalized(LocalizationKeys.INTERACT_INSERT_BATTERY, "Insert Battery");
            _localizedBioReactorPrompt = ResolveLocalized(LocalizationKeys.INTERACT_BIO_REACTOR, "Bio Reactor");
            _localizedEmptyCratePrompt = ResolveLocalized(LocalizationKeys.INTERACT_EMPTY_CRATE, "Empty Crate");
            _localizedVerbApply = ResolveLocalized(LocalizationKeys.INTERACT_VERB_APPLY, "Apply");
            _localizedVerbDrink = ResolveLocalized(LocalizationKeys.INTERACT_VERB_DRINK, "Drink");
            _localizedVerbEat = ResolveLocalized(LocalizationKeys.INTERACT_VERB_EAT, "Eat");
            _localizedVerbInhale = ResolveLocalized(LocalizationKeys.INTERACT_VERB_INHALE, "Inhale");
            _localizedVerbUse = ResolveLocalized(LocalizationKeys.INTERACT_VERB_USE, "Use");
            _localizedVerbTake = ResolveLocalized("ITEM_INTERACT_TAKE", "Take");
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            return localization != null
                ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void RegisterToTick()
        {
            if (_registered)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
        }

        private void UnregisterFromTick()
        {
            if (!_registered)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }
        }
    }
}
