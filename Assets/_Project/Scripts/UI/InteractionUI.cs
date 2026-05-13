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
    using Hecton8.Core.Signals;
    using Hecton8.Gameplay;
    using Hecton8.Input;
    using Hecton8.Inventory;
    using Hecton8.Interaction;
    using Hecton8.Items;
    using Hecton8.Tools;
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Events;

    /// <summary>
    /// Context-sensitive interaction prompt system.
    /// Shows different prompts based on looked-at object and held tool.
    /// Uses ITickable for updates. Zero GC in hot paths.
    /// </summary>
    public class InteractionUI : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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

        [Tooltip("Seconds between prompt ray probes. Kept short enough for UI feel, but not every render frame.")]
        [SerializeField, Range(0.016666668f, 0.2f)] private float promptProbeIntervalSeconds = 0.05f;

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
        private bool _registeredLateFrame;
        private string _currentPrompt;
        private string _currentPromptSource;
        private Collider _cachedPromptCollider;
        private string _cachedPrompt;
        private int _cachedPromptStateHash;
        private bool _hasCachedPrompt;
        private BioReactor _cachedFuelProbeReactor;
        private PlayerInventory _cachedFuelProbeInventory;
        private int _cachedFuelProbeInventoryVersion = int.MinValue;
        private bool _cachedFuelProbeResult;
        private InputManager _subscribedInputManager;
        private bool _hotSwapListenerRegistered;
        private bool _isVisible;
        private float _cameraRetryTimer;
        private const float CameraRetryInterval = 2f;
        private const float MinimumPromptProbeIntervalSeconds = 0.016666668f;
        private const int MaxPromptRaycastHits = 4;
        private static readonly Vector3 CenterViewportPoint = new Vector3(0.5f, 0.5f, 0f);
        private float _promptProbeTimer;
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
        private uint _lastInputSchemeHash;

        // Pre-allocated raycast buffer
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxPromptRaycastHits]; // COLD ALLOC: RaycastHit[4] - bounded prompt probe buffer - owner: InteractionUI
        // COLD ALLOC: char[256] â€” interaction prompt TMP staging buffer â€” owner: InteractionUI
        private readonly char[] _promptCharBuffer = new char[256];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _promptRaycastBufferSaturationLogged;
#endif

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
            _cameraRetryTimer = 0f; // Allow immediate first resolve in Tick
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            SetVisible(false);
        }

        private void OnEnable()
        {
            ResolvePlayerReferences();
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegisterHotSwapListener();
            SubscribeInputManagerIfAvailable();
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            RegisterToTick();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            SubscribeInputManagerIfAvailable();
            RefreshLocalizedPromptCache();
            ClearPromptBuildCache();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            UnsubscribeInputManager();
            TryUnregisterHotSwapListener();
            UnregisterFromTick();
            ClearPromptBuildCache();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            UnsubscribeInputManager();
            TryUnregisterHotSwapListener();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ITickable
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        public void Tick(float deltaTime)
        {
            ConsumeInputStateSignals();
            float safeDeltaTime = math.max(0f, deltaTime);
            _cameraRetryTimer = math.max(0f, _cameraRetryTimer - safeDeltaTime);
            // â”€â”€ Check if action is in progress â”€â”€
            PlayerActionController actionController = GlobalRegistry.PlayerActions;
            if (actionController != null && actionController.IsActionInProgress)
            {
                // Show action in progress prompt, hide interaction prompt
                _promptProbeTimer = 0f;
                UpdatePrompt(_localizedActionInProgressPrompt);
                SetVisible(true);
                return;
            }

            if (_promptProbeTimer > 0f)
            {
                _promptProbeTimer = math.max(0f, _promptProbeTimer - safeDeltaTime);
                return;
            }

            _promptProbeTimer = ResolvePromptProbeInterval();

            if (!TryResolveCamera())
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            // Raycast from camera center
            Ray ray = _mainCamera.ViewportPointToRay(CenterViewportPoint);

            int hitCount = Physics.RaycastNonAlloc(
                ray,
                _hitBuffer,
                interactionRange,
                interactionMask,
                QueryTriggerInteraction.Collide);

            if (!TryResolveNearestPromptTarget(hitCount, out Collider promptCollider, out InteractableRegistry.TargetInfo targetInfo, out float hitDistance))
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            // Build context-sensitive prompt
            string prompt = BuildPrompt(promptCollider, in targetInfo, hitDistance);

            if (string.IsNullOrEmpty(prompt))
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            UpdatePrompt(prompt);
            SetVisible(true);
        }

        public void LateFrameTick()
        {
            Tick(SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            HphiReactiveUiTelemetry.RecordActiveUiUpdate();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PRIVATE â€” PROMPT BUILDING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private bool TryResolveCamera()
        {
            if (_mainCamera != null && _mainCamera.isActiveAndEnabled)
                return true;

            _mainCamera = null;
            if (_cameraRetryTimer > 0f)
                return false;

            _cameraRetryTimer = CameraRetryInterval;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                _mainCamera = playerContext.PlayerCamera;

            return _mainCamera != null && _mainCamera.isActiveAndEnabled;
        }

        private bool TryResolveNearestPromptTarget(
            int hitCount,
            out Collider promptCollider,
            out InteractableRegistry.TargetInfo targetInfo,
            out float distance)
        {
            promptCollider = null;
            targetInfo = default;
            distance = float.MaxValue;
            if (hitCount <= 0)
            {
                ClearPromptBuildCache();
                return false;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hitCount >= _hitBuffer.Length && !_promptRaycastBufferSaturationLogged)
            {
                _promptRaycastBufferSaturationLogged = true;
                Debug.LogWarning(
                    "[InteractionUI] Prompt raycast buffer saturated. Increase MaxPromptRaycastHits or narrow interactionMask.",
                    this);
            }
#endif

            int count = math.min(hitCount, _hitBuffer.Length);
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _hitBuffer[i];
                _hitBuffer[i] = default;
                Collider candidateCollider = candidate.collider;
                if (candidateCollider == null ||
                    candidate.distance >= distance ||
                    !InteractableRegistry.TryResolve(candidateCollider, out InteractableRegistry.TargetInfo candidateInfo) ||
                    candidateInfo.Interactable == null)
                {
                    continue;
                }

                promptCollider = candidateCollider;
                targetInfo = candidateInfo;
                distance = candidate.distance;
            }

            if (promptCollider != null)
                return true;

            ClearPromptBuildCache();
            return false;
        }

        private string BuildPrompt(Collider collider, in InteractableRegistry.TargetInfo targetInfo, float distance)
        {
            if (targetInfo.Interactable == null)
            {
                ClearPromptBuildCache();
                return null;
            }

            bool canCachePrompt = CanCachePrompt(in targetInfo);
            int promptStateHash = canCachePrompt ? ComputePromptStateHash(in targetInfo) : 0;
            if (canCachePrompt &&
                _hasCachedPrompt &&
                ReferenceEquals(_cachedPromptCollider, collider) &&
                _cachedPromptStateHash == promptStateHash)
            {
                return _cachedPrompt;
            }

            string prompt = BuildPromptUncached(in targetInfo);
            if (canCachePrompt)
            {
                _cachedPromptCollider = collider;
                _cachedPromptStateHash = promptStateHash;
                _cachedPrompt = prompt;
                _hasCachedPrompt = true;
            }
            else
            {
                ClearPromptBuildCache();
            }

            return prompt;
        }

        private string BuildPromptUncached(in InteractableRegistry.TargetInfo targetInfo)
        {
            if (targetInfo.BatteryTool != null)
                return BuildBatteryToolPrompt(targetInfo.BatteryTool);

            if (targetInfo.Charger != null)
                return BuildBatteryChargerPrompt(targetInfo.Charger);

            if (targetInfo.Reactor != null)
                return BuildBioReactorPrompt(targetInfo.Reactor);

            if (targetInfo.Crate != null)
                return BuildStorageCratePrompt(targetInfo.Crate);

            if (targetInfo.Pickup != null && targetInfo.Pickup.ItemData != null)
                return BuildPickupItemPrompt(targetInfo.Pickup);

            if (targetInfo.Interactable != null)
                return BuildInteractablePrompt(targetInfo);

            return null;
        }

        private static bool CanCachePrompt(in InteractableRegistry.TargetInfo targetInfo)
        {
            return targetInfo.BatteryTool != null ||
                   targetInfo.Charger != null ||
                   targetInfo.Reactor != null ||
                   targetInfo.Crate != null ||
                   targetInfo.Pickup != null;
        }

        private int ComputePromptStateHash(in InteractableRegistry.TargetInfo targetInfo)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + ReferenceHash(targetInfo.Interactable);
                hash = hash * 31 + ReferenceHash(targetInfo.BatteryTool);
                hash = hash * 31 + ReferenceHash(targetInfo.Charger);
                hash = hash * 31 + ReferenceHash(targetInfo.Reactor);
                hash = hash * 31 + ReferenceHash(targetInfo.Crate);
                hash = hash * 31 + ReferenceHash(targetInfo.Pickup);

                if (targetInfo.BatteryTool != null)
                    hash = hash * 31 + (targetInfo.BatteryTool.HasBattery ? 1 : 0);

                if (targetInfo.Charger != null)
                {
                    hash = hash * 31 + (targetInfo.Charger.HasBatteryInSlot(0) ? 1 : 0);
                    hash = hash * 31 + (targetInfo.Charger.HasBatteryInSlot(1) ? 1 : 0);
                }

                PlayerTool heldTool = _toolManager != null ? _toolManager.CurrentTool : null;
                hash = hash * 31 + ReferenceHash(heldTool);
                if (heldTool is IBatteryTool heldBatteryTool)
                    hash = hash * 31 + (heldBatteryTool.HasBattery ? 1 : 0);

                if (targetInfo.Reactor != null)
                    hash = hash * 31 + (HasDepositableFuelCached(targetInfo.Reactor) ? 1 : 0);

                if (targetInfo.Crate != null)
                    hash = hash * 31 + (targetInfo.Crate.IsEmpty() ? 1 : 0);

                if (targetInfo.Pickup != null)
                {
                    ItemData item = targetInfo.Pickup.ItemData;
                    hash = hash * 31 + ReferenceHash(item);
                    if (item != null)
                    {
                        hash = hash * 31 + LocHash.Compute(item.PersistentId);
                        hash = hash * 31 + (item.isConsumable ? 1 : 0);
                        hash = hash * 31 + (int)math.round(item.UseDuration * 10f);
                        hash = hash * 31 + (item.integrityRestore > 0f ? 1 : 0);
                        hash = hash * 31 + (item.thirstRestore > 0f ? 1 : 0);
                        hash = hash * 31 + (item.hungerRestore > 0f ? 1 : 0);
                        hash = hash * 31 + (item.oxygenRestore > 0f ? 1 : 0);
                    }

                    string cachedInteractText = targetInfo.Pickup.GetInteractText();
                    hash = hash * 31 + (cachedInteractText != null ? cachedInteractText.GetHashCode() : 0);
                }

                return hash;
            }
        }

        private static int ReferenceHash(object value)
        {
            return value != null ? RuntimeHelpers.GetHashCode(value) : 0;
        }

        /// <summary>
        /// Builds prompt for pickup items from the pickup's cached interact text.
        /// </summary>
        private string BuildPickupItemPrompt(PickupItem pickup)
        {
            string cachedText = pickup.GetInteractText();
            return string.IsNullOrEmpty(cachedText) ? _localizedTakeItemPrompt : cachedText;
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
            return HasDepositableFuelCached(reactor) ? _localizedDepositFuelPrompt : _localizedBioReactorPrompt;
        }

        private bool HasDepositableFuelCached(BioReactor reactor)
        {
            int inventoryVersion = _inventory != null ? _inventory.InventoryVersion : int.MinValue;
            if (ReferenceEquals(_cachedFuelProbeReactor, reactor) &&
                ReferenceEquals(_cachedFuelProbeInventory, _inventory) &&
                _cachedFuelProbeInventoryVersion == inventoryVersion)
            {
                return _cachedFuelProbeResult;
            }

            _cachedFuelProbeReactor = reactor;
            _cachedFuelProbeInventory = _inventory;
            _cachedFuelProbeInventoryVersion = inventoryVersion;
            _cachedFuelProbeResult = HasDepositableFuelUncached(reactor);
            return _cachedFuelProbeResult;
        }

        private bool HasDepositableFuelUncached(BioReactor reactor)
        {
            if (reactor == null || _inventory == null || _inventory.Grid == null || _inventory.ItemCatalog == null)
                return false;

            InventoryGrid grid = _inventory.Grid;
            int cols = grid.Columns;
            int rows = grid.Rows;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int anchorIndex = grid.GetCellAnchorIndex(x, y);
                    if (anchorIndex < 0 || anchorIndex != y * cols + x)
                        continue;

                    int itemHashId = _inventory.GetItemHashAt(x, y);
                    if (itemHashId == 0)
                        continue;

                    ItemData item = _inventory.ItemCatalog.FindByHash(itemHashId);
                    if (item != null && reactor.IsAcceptedFuel(item))
                        return true;
                }
            }

            return false;
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
            if (string.Equals(_currentPromptSource, prompt, StringComparison.Ordinal))
                return;

            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            string expandedPrompt = localization != null ? localization.ExpandText(prompt) : prompt;
            _currentPromptSource = prompt;
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

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            _currentPrompt = null;
            _currentPromptSource = null;
            _promptProbeTimer = 0f;
            ClearPromptBuildCache();
        }

        private void HandleInputDisplayStyleChanged(InputDisplayStyle displayStyle)
        {
            RefreshLocalizedPromptCache();
            _currentPrompt = null;
            _currentPromptSource = null;
            _promptProbeTimer = 0f;
            ClearPromptBuildCache();
        }

        private void SubscribeInputManagerIfAvailable()
        {
            if (_subscribedInputManager != null)
                return;

            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager == null)
                return;

            _subscribedInputManager = inputManager;
            _subscribedInputManager.OnInputDisplayStyleChanged += HandleInputDisplayStyleChanged;
        }

        private void UnsubscribeInputManager()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnInputDisplayStyleChanged -= HandleInputDisplayStyleChanged;
            _subscribedInputManager = null;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input)
                return;

            UnsubscribeInputManager();

            if (!isActiveAndEnabled)
                return;

            SubscribeInputManagerIfAvailable();
            RefreshLocalizedPromptCache();
            _promptProbeTimer = 0f;
            ClearPromptBuildCache();
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

        private void ClearPromptBuildCache()
        {
            _cachedPromptCollider = null;
            _cachedPrompt = null;
            _cachedPromptStateHash = 0;
            _hasCachedPrompt = false;
            _cachedFuelProbeReactor = null;
            _cachedFuelProbeInventory = null;
            _cachedFuelProbeInventoryVersion = int.MinValue;
            _cachedFuelProbeResult = false;
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
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            return localization != null
                ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string ResolveLocalizedExpanded(string key, string fallback)
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            return localization != null
                ? localization.GetExpandedOrFallback(localization.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void RegisterToTick()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTick()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = false;
        }

        private float ResolvePromptProbeInterval()
        {
            return math.max(MinimumPromptProbeIntervalSeconds, promptProbeIntervalSeconds);
        }

        private void ConsumeInputStateSignals()
        {
            ReadOnlySpan<InputStateSignal> signals = SignalBus<InputStateSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                uint schemeHash = signals[i].CurrentInputSchemeHash;
                if (schemeHash == 0u || schemeHash == _lastInputSchemeHash)
                    continue;

                _lastInputSchemeHash = schemeHash;
                HandleInputDisplayStyleChanged(ResolveDisplayStyleFromSignal(schemeHash));
            }
        }

        private static InputDisplayStyle ResolveDisplayStyleFromSignal(uint schemeHash)
        {
            switch (schemeHash)
            {
                case 0x47504144u:
                    return InputDisplayStyle.Gamepad;
                case 0x5354444Bu:
                    return InputDisplayStyle.SteamDeck;
                case 0x58525443u:
                    return InputDisplayStyle.XRTouch;
                default:
                    return InputDisplayStyle.KeyboardMouse;
            }
        }
    }
}
