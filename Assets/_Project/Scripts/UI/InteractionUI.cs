// ============================================================================
// HECTON-8 â€” InteractionUI.cs
// Context-sensitive interaction prompts for the player.
//
// ARCHITECTURE:
//   â€¢ ITickable for updates (no Update)
//   â€¢ Zero GC: cached refs, pre-cached strings
//   â€¢ UnityEvent hooks for designers
//
// FEATURES:
//   â€¢ Shows interaction prompts based on looked-at object
//   â€¢ Context-sensitive: "Press [E] to Swap Battery" vs "No Battery to Swap"
//   â€¢ Tool-aware: different prompts based on held tool
// ============================================================================

namespace Hecton8.UI
{
    using Hecton.Localization;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Gameplay;
    using Hecton8.Input;
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
    public class InteractionUI : MonoBehaviour, ITickable, IUpdatable
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Text component for the interaction prompt.")]
        [SerializeField] private TMPro.TMP_Text promptText;

        [Tooltip("Canvas group for fading.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("â”€â”€ Raycast Settings â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Maximum interaction distance.")]
        [SerializeField] private float interactionRange = 4f;

        [Tooltip("Layers to check for interactables.")]
        [SerializeField] private LayerMask interactionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("â”€â”€ Prompt Templates â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Default interaction prompt format. {0}=verb, {1}=name")]
        [SerializeField] private string defaultPromptFormat = "<button:interact> {0} {1}";

        [Tooltip("Prompt when tool has no battery.")]
        [SerializeField] private string noBatteryPrompt = "No Battery to Swap";

        [Tooltip("Prompt to swap battery.")]
        [SerializeField] private string swapBatteryPrompt = "<button:interact> Swap Battery";

        [Tooltip("Prompt to deposit fuel.")]
        [SerializeField] private string depositFuelPrompt = "<button:interact> Deposit Fuel";

        [Tooltip("Prompt to take item.")]
        [SerializeField] private string takeItemPrompt = "<button:interact> Take Item";

        [Tooltip("Prompt format for consumable with duration. {0}=verb, {1}=duration")]
        [SerializeField] private string consumableWithDurationFormat = "Hold <button:interact> {0} ({1:0.0}s)";

        [Tooltip("Prompt for action in progress.")]
        [SerializeField] private string actionInProgressPrompt = "Consuming...";

        [Header("â”€â”€ Events â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Fired when the prompt changes.")]
        public UnityEvent<string> OnPromptChanged;

        [Tooltip("Fired when prompt visibility changes.")]
        public UnityEvent<bool> OnVisibilityChanged;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Camera _mainCamera;
        private Transform _cachedTransform;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private bool _registered;
        private string _currentPrompt;
        private bool _isVisible;
        private float _cameraRetryTime;
        private const float CameraRetryInterval = 2f;
        private string _localizedDefaultPromptFormat;
        private string _localizedNoBatteryPrompt;
        private string _localizedSwapBatteryPrompt;
        private string _localizedDepositFuelPrompt;
        private string _localizedTakeItemPrompt;
        private string _localizedConsumableWithDurationFormat;
        private string _localizedActionInProgressPrompt;
        private string _localizedInsertBatteryPrompt;
        private string _localizedBioReactorPrompt;
        private string _localizedOpenCratePrompt;
        private string _localizedEmptyCratePrompt;
        private string _localizedVerbApply;
        private string _localizedVerbDrink;
        private string _localizedVerbEat;
        private string _localizedVerbInhale;
        private string _localizedVerbUse;
        private string _localizedVerbTake;

        // Pre-allocated raycast buffer
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[1]; // COLD ALLOC: single-hit interaction probe â€” owner: InteractionUI
        // COLD ALLOC: char[256] â€” interaction prompt TMP staging buffer â€” owner: InteractionUI
        private readonly char[] _promptCharBuffer = new char[256];

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC PROPERTIES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Current interaction prompt text.</summary>
        public string CurrentPrompt => _currentPrompt;

        /// <summary>Whether the prompt is currently visible.</summary>
        public bool IsVisible => _isVisible;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            _cachedTransform = transform;
            _cameraRetryTime = 0f; // Allow immediate first resolve in Tick
            InteractableRegistry.WarmSceneCache();
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            SetVisible(false);
        }

        private void OnEnable()
        {
            InteractableRegistry.WarmSceneCache();
            ResolvePlayerReferences();
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInputDisplayStyleChanged += HandleInputDisplayStyleChanged;
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            RegisterToTick();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            if (InputManager.Instance != null)
                InputManager.Instance.OnInputDisplayStyleChanged -= HandleInputDisplayStyleChanged;
            UnregisterFromTick();
            SetVisible(false);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            // â”€â”€ Check if action is in progress â”€â”€
            PlayerActionController actionController = PlayerActionController.Instance;
            if (actionController != null && actionController.IsActionInProgress)
            {
                // Show action in progress prompt, hide interaction prompt
                UpdatePrompt(_localizedActionInProgressPrompt);
                SetVisible(true);
                return;
            }

            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
            {
                _mainCamera = null;
                if (Time.time < _cameraRetryTime)
                {
                    if (_isVisible)
                        SetVisible(false);
                    return;
                }

                _cameraRetryTime = Time.time + CameraRetryInterval;

                IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
                if (playerContext != null)
                    _mainCamera = playerContext.PlayerCamera;

                if (_mainCamera == null)
                {
                    if (_isVisible)
                        SetVisible(false);
                    return;
                }
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” PROMPT BUILDING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private string BuildPrompt(Collider collider, float distance)
        {
            if (!InteractableRegistry.TryResolve(collider, out InteractableRegistry.TargetInfo targetInfo))
                return null;

            if (targetInfo.Interactable != null)
                return BuildInteractablePrompt(targetInfo);

            if (targetInfo.Charger != null)
                return BuildBatteryChargerPrompt(targetInfo.Charger);

            if (targetInfo.Reactor != null)
                return BuildBioReactorPrompt(targetInfo.Reactor);

            if (targetInfo.Crate != null)
                return BuildStorageCratePrompt(targetInfo.Crate);

            if (targetInfo.Pickup != null && targetInfo.Pickup.ItemData != null)
                return BuildPickupItemPrompt(targetInfo.Pickup);

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

            string itemDisplay = LocalizedInlineIconResolver.BuildItemDisplay(item, item.itemName);

            // Check if this is a consumable with use duration
            if (item.isConsumable && item.UseDuration > 0f)
            {
                // Determine verb based on item type
                string verb = GetConsumableVerb(item);
                if (LocalizedInlineIconResolver.TryResolveItemChip(item, out string chip))
                    verb = chip + " " + verb;
                // Note: string.Format is acceptable here (not in hot path - only when looking at new item)
                return string.Format(_localizedConsumableWithDurationFormat, verb, item.UseDuration);
            }

            return string.Format(_localizedDefaultPromptFormat, _localizedVerbTake, itemDisplay);
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

        private string BuildInteractablePrompt(in InteractableRegistry.TargetInfo targetInfo)
        {
            IInteractable interactable = targetInfo.Interactable;
            if (interactable == null)
                return null;

            // Get base interaction text
            string baseText = interactable.GetInteractText();

            // Check if this is a battery tool context
            IBatteryTool batteryTool = targetInfo.BatteryTool;
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
                int fuelCount = reactor.CountFuelInInventory(_inventory);
                if (fuelCount > 0)
                {
                    LocalizationManager localization = LocalizationManager.Instance;
                    if (localization != null)
                    {
                        return localization.GetPluralFormatted(
                            LocalizationKeys.INTERACT_DEPOSIT_FUEL_COUNT,
                            fuelCount,
                            fuelCount);
                    }

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

            return _localizedOpenCratePrompt;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” UI UPDATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void UpdatePrompt(string prompt)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            string expandedPrompt = localization != null ? localization.ExpandText(prompt) : prompt;
            if (_currentPrompt == expandedPrompt)
                return;

            _currentPrompt = expandedPrompt;

            if (promptText != null)
                ApplyPromptText(expandedPrompt);

            OnPromptChanged?.Invoke(expandedPrompt);
        }

        private void ApplyPromptText(string prompt)
        {
            if (promptText == null)
                return;

            if (string.IsNullOrEmpty(prompt))
            {
                promptText.SetCharArray(_promptCharBuffer, 0, 0);
                return;
            }

            int charCount = prompt.Length;
            int copyLength = charCount <= _promptCharBuffer.Length ? charCount : _promptCharBuffer.Length;
            prompt.CopyTo(0, _promptCharBuffer, 0, copyLength);
            promptText.SetCharArray(_promptCharBuffer, 0, copyLength);
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

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” REFERENCES
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ResolvePlayerReferences()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext == null)
                return;

            if (_toolManager == null)
                _toolManager = playerContext.ToolManager;

            if (_inventory == null)
                _inventory = playerContext.Inventory;

            if (_mainCamera == null)
                _mainCamera = playerContext.PlayerCamera;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            _currentPrompt = null;
        }

        private void HandleInputDisplayStyleChanged(InputDisplayStyle displayStyle)
        {
            RefreshLocalizedPromptCache();
            _currentPrompt = null;
        }

        private void ConfigurePromptText()
        {
            if (promptText == null)
                return;

            LocalizedTMPAutoSizer.Configure(
                promptText,
                promptText.fontSize * 0.72f,
                promptText.fontSize,
                TMPro.TextOverflowModes.Ellipsis,
                TMPro.TextWrappingModes.Normal);
        }

        private void RefreshLocalizedPromptCache()
        {
            _localizedDefaultPromptFormat = ResolveLocalizedExpanded(
                LocalizationKeys.INTERACT_DEFAULT_PROMPT_FORMAT,
                defaultPromptFormat);
            _localizedNoBatteryPrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_NO_BATTERY_TO_SWAP, noBatteryPrompt);
            _localizedSwapBatteryPrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_SWAP_BATTERY, swapBatteryPrompt);
            _localizedDepositFuelPrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_DEPOSIT_FUEL, depositFuelPrompt);
            _localizedTakeItemPrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_TAKE_ITEM, takeItemPrompt);
            _localizedConsumableWithDurationFormat = ResolveLocalizedExpanded(
                LocalizationKeys.INTERACT_CONSUMABLE_WITH_DURATION_FORMAT,
                consumableWithDurationFormat);
            _localizedActionInProgressPrompt = ResolveLocalizedExpanded(LocalizationKeys.ACTION_USING, actionInProgressPrompt);
            _localizedInsertBatteryPrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_INSERT_BATTERY, "Insert Battery");
            _localizedBioReactorPrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_BIO_REACTOR, "Bio Reactor");
            _localizedOpenCratePrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_OPEN_CRATE, "<button:interact> Open Crate");
            _localizedEmptyCratePrompt = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_EMPTY_CRATE, "Empty Crate");
            _localizedVerbApply = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_VERB_APPLY, "Apply");
            _localizedVerbDrink = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_VERB_DRINK, "Drink");
            _localizedVerbEat = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_VERB_EAT, "Eat");
            _localizedVerbInhale = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_VERB_INHALE, "Inhale");
            _localizedVerbUse = ResolveLocalizedExpanded(LocalizationKeys.INTERACT_VERB_USE, "Use");
            _localizedVerbTake = ResolveLocalizedExpanded("ITEM_INTERACT_TAKE", "Take");
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            return localization != null
                ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string ResolveLocalizedExpanded(string key, string fallback)
        {
            LocalizationManager localization = LocalizationManager.Instance;
            return localization != null
                ? localization.GetExpandedOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void RegisterToTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void UnregisterFromTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
        }
    }
}
