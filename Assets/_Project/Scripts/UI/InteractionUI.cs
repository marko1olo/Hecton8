// ============================================================================
// HECTON-8 - InteractionUI.cs
// Context-sensitive interaction prompts for the player.
//
// ARCHITECTURE:
//   - ITickable for updates (no Update)
//   - Zero GC: cached refs, pre-cached strings
//   - UnityEvent hooks for designers
//
// FEATURES:
//   - Shows interaction prompts based on looked-at object
//   - Context-sensitive: "Press [E] to Swap Battery" vs "No Battery to Swap"
//   - Tool-aware: different prompts based on held tool
// ============================================================================

namespace Hecton8.UI
{
    using Hecton.Localization;
    using Hecton8.Bootstrap;
    using Hecton8.Core;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Gameplay;
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
    public class InteractionUI : MonoBehaviour, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private enum PromptSource : byte
        {
            None = 0,
            NoBattery = 1,
            SwapBattery = 2,
            DepositFuel = 3,
            TakeItem = 4,
            ActionInProgress = 5,
            InsertBattery = 6,
            BioReactor = 7,
            OpenCrate = 8,
            EmptyCrate = 9
        }

        // --------------------------------------------------------------------------
        //  INSPECTOR
        // --------------------------------------------------------------------------

        [Header("-- References --------------------------------")]
        [Tooltip("Text component for the interaction prompt.")]
        [SerializeField] private TMPro.TMP_Text promptText;

        [Tooltip("Canvas group for fading.")]
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Target Probe Settings")]
        [Tooltip("Maximum interaction distance.")]
        [SerializeField] private float interactionRange = 4f;

        [Tooltip("Layers to check for interactables.")]
        [SerializeField] private LayerMask interactionMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Seconds between prompt spatial target probes. Kept short enough for UI feel, but not every render frame.")]
        [SerializeField, Range(0.016666668f, 0.2f)] private float promptProbeIntervalSeconds = 0.05f;

        [Header("-- Prompt Templates --------------------------")]
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

        [Header("-- Events --------------------------------------")]
        [Tooltip("Fired when the prompt changes.")]
        public UnityEvent<string> OnPromptChanged;

        [Tooltip("Fired when prompt visibility changes.")]
        public UnityEvent<bool> OnVisibilityChanged;

        // --------------------------------------------------------------------------
        //  PRIVATE STATE
        // --------------------------------------------------------------------------

        private Camera _mainCamera;
        private Transform _cachedTransform;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private PlayerToolManager _toolManager;
        private PlayerInventory _inventory;
        private IPlayerActionInterruptSink _cachedPlayerActions;
        private ILocalizationTextExpansionReadModel _cachedLocalization;
        private INativeInputManagerRuntime _cachedInputManager;
        private bool _registeredLateFrame;
        private string _currentPrompt;
        private PromptSource _currentPromptSource;
        private Collider _cachedPromptCollider;
        private PromptSource _cachedPromptSource;
        private int _cachedPromptStateHash;
        private bool _hasCachedPrompt;
        private BioReactor _cachedFuelProbeReactor;
        private PlayerInventory _cachedFuelProbeInventory;
        private int _cachedFuelProbeInventoryVersion = int.MinValue;
        private bool _cachedFuelProbeResult;
        private INativeInputManagerRuntime _subscribedInputManager;
        private IInputBindingService _subscribedInputBindingService;
        private bool _hotSwapListenerRegistered;
        private bool _isVisible;
        private float _cameraRetryTimer;
        private const float CameraRetryInterval = 2f;
        private const float MinimumPromptProbeIntervalSeconds = 0.016666668f;
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
        private bool _promptPresentationDirty;
        private const string PlayerActionMapName = "Player";
        private const string InteractActionName = "Interact";
        private Action<string, string, int, string> _rebindCompletedAction;
        private Action<string, string, int> _rebindCanceledAction;
        private Action _bindingOverridesChangedAction;

        // COLD ALLOC: char[256] - interaction prompt TMP staging buffer - owner: InteractionUI
        private readonly char[] _promptCharBuffer = new char[256];
        // --------------------------------------------------------------------------
        //  PUBLIC PROPERTIES
        // --------------------------------------------------------------------------

        /// <summary>Current interaction prompt text.</summary>
        public string CurrentPrompt => _currentPrompt;

        /// <summary>Whether the prompt is currently visible.</summary>
        public bool IsVisible => _isVisible;

        // --------------------------------------------------------------------------
        //  LIFECYCLE
        // --------------------------------------------------------------------------

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
            RefreshCachedRegistryServices();
            ResolvePlayerReferences();
            LocalizationEvents.RegisterLanguageListener(this);
            TryRegisterHotSwapListener();
            EnsureCachedBindingDelegates();
            SubscribeInputManagerIfAvailable();
            SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);
            ConfigurePromptText();
            RefreshLocalizedPromptCache();
            if (Application.isPlaying)
                InteractableRegistry.EnsureSceneRegistryCold();
            RegisterToTick();
        }

        private void Start()
        {
            RefreshCachedRegistryServices();
            TryRegisterHotSwapListener();
            EnsureCachedBindingDelegates();
            SubscribeInputManagerIfAvailable();
            SubscribeInputBindingServiceIfAvailable(GlobalRegistry.InputBinding);
            RefreshLocalizedPromptCache();
            ClearPromptBuildCache();
        }

        private void OnDisable()
        {
            LocalizationEvents.UnregisterLanguageListener(this);
            UnsubscribeInputManager();
            UnsubscribeInputBindingService();
            TryUnregisterHotSwapListener();
            UnregisterFromTick();
            ClearPromptBuildCache();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            UnsubscribeInputManager();
            UnsubscribeInputBindingService();
            TryUnregisterHotSwapListener();
        }

        // --------------------------------------------------------------------------
        //  ITickable
        // --------------------------------------------------------------------------

        private void SamplePromptState(float deltaTime)
        {
            ConsumeInputStateSignals();
            float safeDeltaTime = math.max(0f, deltaTime);
            _cameraRetryTimer = math.max(0f, _cameraRetryTimer - safeDeltaTime);
            // -- Check if action is in progress --
            IPlayerActionInterruptSink actionController = _cachedPlayerActions;
            if (actionController != null && actionController.IsActionInProgress)
            {
                // Show action in progress prompt, hide interaction prompt
                _promptProbeTimer = 0f;
                TryApplyPromptSource(PromptSource.ActionInProgress);
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

            Ray ray = _mainCamera.ViewportPointToRay(CenterViewportPoint);
            if (!InteractableRegistry.TryResolveSpatialTarget(
                    in ray,
                    interactionRange,
                    interactionMask.value,
                    QueryTriggerInteraction.Collide,
                    out InteractableRegistry.SpatialHit spatialHit) ||
                spatialHit.TargetInfo.Interactable == null)
            {
                ClearPromptBuildCache();
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            Collider promptCollider = spatialHit.Collider;
            InteractableRegistry.TargetInfo targetInfo = spatialHit.TargetInfo;

            if (TryUpdatePromptFromTextProvider(in targetInfo))
            {
                SetVisible(true);
                return;
            }

            if (!TryResolvePromptSource(promptCollider, in targetInfo, out PromptSource promptSource) ||
                !TryApplyPromptSource(promptSource))
            {
                if (_isVisible)
                    SetVisible(false);
                return;
            }

            SetVisible(true);
        }

        public void LateFrameTick()
        {
            ApplyPendingPromptPresentationRefresh();
            SamplePromptState(SystemDispatcher.CurrentFrameUnscaledDeltaTime);
            HphiReactiveUiTelemetry.RecordActiveUiUpdate();
        }

        // --------------------------------------------------------------------------
        //  PRIVATE - PROMPT BUILDING
        // --------------------------------------------------------------------------

        private bool TryResolveCamera()
        {
            if (_mainCamera != null && _mainCamera.isActiveAndEnabled)
                return true;

            _mainCamera = null;
            if (_cameraRetryTimer > 0f)
                return false;

            _cameraRetryTimer = CameraRetryInterval;
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null)
                _mainCamera = playerContext.PlayerCamera;

            return _mainCamera != null && _mainCamera.isActiveAndEnabled;
        }

        private bool TryResolvePromptSource(Collider collider, in InteractableRegistry.TargetInfo targetInfo, out PromptSource promptSource)
        {
            promptSource = PromptSource.None;
            if (targetInfo.Interactable == null)
            {
                ClearPromptBuildCache();
                return false;
            }

            bool canCachePrompt = CanCachePrompt(in targetInfo);
            int promptStateHash = canCachePrompt ? ComputePromptStateHash(in targetInfo) : 0;
            if (canCachePrompt &&
                _hasCachedPrompt &&
                ReferenceEquals(_cachedPromptCollider, collider) &&
                _cachedPromptStateHash == promptStateHash)
            {
                promptSource = _cachedPromptSource;
                return promptSource != PromptSource.None;
            }

            promptSource = ResolvePromptSourceUncached(in targetInfo);
            if (canCachePrompt)
            {
                _cachedPromptCollider = collider;
                _cachedPromptStateHash = promptStateHash;
                _cachedPromptSource = promptSource;
                _hasCachedPrompt = true;
            }
            else
            {
                ClearPromptBuildCache();
            }

            return promptSource != PromptSource.None;
        }

        private PromptSource ResolvePromptSourceUncached(in InteractableRegistry.TargetInfo targetInfo)
        {
            if (targetInfo.BatteryTool != null)
                return ResolveBatteryToolPromptSource(targetInfo.BatteryTool);

            if (targetInfo.Charger != null)
                return ResolveBatteryChargerPromptSource(targetInfo.Charger);

            if (targetInfo.Reactor != null)
                return ResolveBioReactorPromptSource(targetInfo.Reactor);

            if (targetInfo.Crate != null)
                return ResolveStorageCratePromptSource(targetInfo.Crate);

            if (targetInfo.Pickup != null && targetInfo.Pickup.ItemData != null)
                return PromptSource.TakeItem;

            if (targetInfo.Interactable != null)
                return ResolveInteractablePromptSource(targetInfo);

            return PromptSource.None;
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
                        hash = hash * 31 + item.PersistentHashId;
                        hash = hash * 31 + (item.isConsumable ? 1 : 0);
                        hash = hash * 31 + (int)math.round(item.UseDuration * 10f);
                        hash = hash * 31 + (item.integrityRestore > 0f ? 1 : 0);
                        hash = hash * 31 + (item.thirstRestore > 0f ? 1 : 0);
                        hash = hash * 31 + (item.hungerRestore > 0f ? 1 : 0);
                        hash = hash * 31 + (item.oxygenRestore > 0f ? 1 : 0);
                    }

                    if (targetInfo.Pickup is IInteractableTextProvider pickupTextProvider &&
                        pickupTextProvider.TryCopyInteractText(_promptCharBuffer, out int pickupTextLength) &&
                        pickupTextLength > 0)
                    {
                        int safeLength = math.min(pickupTextLength, _promptCharBuffer.Length);
                        hash = hash * 31 + ComputePromptBufferHash(_promptCharBuffer, safeLength);
                    }
                }

                return hash;
            }
        }

        private static int ComputePromptBufferHash(char[] buffer, int length)
        {
            if (buffer == null || length <= 0)
                return 0;

            unchecked
            {
                uint hash = 2166136261u;
                int safeLength = math.min(length, buffer.Length);
                for (int i = 0; i < safeLength; i++)
                    hash = (hash ^ buffer[i]) * 16777619u;

                return hash != 0u ? (int)hash : 1;
            }
        }

        private static int ReferenceHash(object value)
        {
            return value != null ? RuntimeHelpers.GetHashCode(value) : 0;
        }

        private PromptSource ResolveInteractablePromptSource(in InteractableRegistry.TargetInfo targetInfo)
        {
            IInteractable interactable = targetInfo.Interactable;
            if (interactable == null)
                return PromptSource.None;

            // Check if this is a battery tool context
            IBatteryTool batteryTool = targetInfo.BatteryTool;
            if (batteryTool != null)
            {
                return ResolveBatteryToolPromptSource(batteryTool);
            }

            return PromptSource.None;
        }

        private PromptSource ResolveBatteryToolPromptSource(IBatteryTool tool)
        {
            if (!tool.HasBattery)
            {
                return PromptSource.NoBattery;
            }

            return PromptSource.SwapBattery;
        }

        private PromptSource ResolveBatteryChargerPromptSource(BatteryCharger charger)
        {
            // Check if player is holding a tool with battery
            if (_toolManager != null)
            {
                PlayerTool heldTool = _toolManager.CurrentTool;
                if (heldTool is IBatteryTool batteryTool)
                {
                    if (batteryTool.HasBattery)
                    {
                        return PromptSource.SwapBattery;
                    }
                    else
                    {
                        return PromptSource.NoBattery;
                    }
                }
            }

            // Check if charger has a battery to take
            if (charger.HasBatteryInSlot(0) || charger.HasBatteryInSlot(1))
            {
                return PromptSource.TakeItem;
            }

            return PromptSource.InsertBattery;
        }

        private PromptSource ResolveBioReactorPromptSource(BioReactor reactor)
        {
            return HasDepositableFuelCached(reactor) ? PromptSource.DepositFuel : PromptSource.BioReactor;
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

        private PromptSource ResolveStorageCratePromptSource(StorageCrate crate)
        {
            if (crate.IsEmpty())
            {
                return PromptSource.EmptyCrate;
            }

            return PromptSource.OpenCrate;
        }

        private bool TryUpdatePromptFromTextProvider(in InteractableRegistry.TargetInfo targetInfo)
        {
            if (targetInfo.BatteryTool != null ||
                targetInfo.Charger != null ||
                targetInfo.Reactor != null ||
                targetInfo.Crate != null ||
                !(targetInfo.Interactable is IInteractableTextProvider textProvider))
            {
                return false;
            }

            if (!textProvider.TryCopyInteractText(_promptCharBuffer, out int length) || length <= 0)
                return false;

            int safeLength = math.min(length, _promptCharBuffer.Length);
            if (promptText != null)
                promptText.SetCharArray(_promptCharBuffer, 0, safeLength);

            _currentPrompt = null;
            _currentPromptSource = PromptSource.None;
            ClearPromptBuildCache();
            return true;
        }

        // --------------------------------------------------------------------------
        //  PRIVATE - UI UPDATE
        // --------------------------------------------------------------------------

        private bool TryApplyPromptSource(PromptSource promptSource)
        {
            if (promptSource == PromptSource.None)
                return false;

            ReadOnlySpan<char> prompt = ResolvePromptSourceSpan(promptSource, out string eventPrompt);
            if (prompt.Length <= 0)
                return false;

            if (_currentPromptSource == promptSource && ReferenceEquals(_currentPrompt, eventPrompt))
                return true;

            _currentPromptSource = promptSource;
            _currentPrompt = eventPrompt;
            ApplyPromptSpan(prompt);
            OnPromptChanged?.Invoke(eventPrompt);
            return true;
        }

        private ReadOnlySpan<char> ResolvePromptSourceSpan(PromptSource promptSource, out string eventPrompt)
        {
            switch (promptSource)
            {
                case PromptSource.NoBattery:
                    eventPrompt = _localizedNoBatteryPrompt;
                    break;
                case PromptSource.SwapBattery:
                    eventPrompt = _localizedSwapBatteryPrompt;
                    break;
                case PromptSource.DepositFuel:
                    eventPrompt = _localizedDepositFuelPrompt;
                    break;
                case PromptSource.TakeItem:
                    eventPrompt = _localizedTakeItemPrompt;
                    break;
                case PromptSource.ActionInProgress:
                    eventPrompt = _localizedActionInProgressPrompt;
                    break;
                case PromptSource.InsertBattery:
                    eventPrompt = _localizedInsertBatteryPrompt;
                    break;
                case PromptSource.BioReactor:
                    eventPrompt = _localizedBioReactorPrompt;
                    break;
                case PromptSource.OpenCrate:
                    eventPrompt = _localizedOpenCratePrompt;
                    break;
                case PromptSource.EmptyCrate:
                    eventPrompt = _localizedEmptyCratePrompt;
                    break;
                default:
                    eventPrompt = null;
                    return ReadOnlySpan<char>.Empty;
            }

            return PromptToSpan(eventPrompt);
        }

        private static ReadOnlySpan<char> PromptToSpan(string prompt)
        {
            return string.IsNullOrEmpty(prompt) ? ReadOnlySpan<char>.Empty : prompt.AsSpan();
        }

        private void ApplyPromptSpan(ReadOnlySpan<char> prompt)
        {
            if (promptText == null)
                return;

            if (prompt.Length <= 0)
            {
                promptText.SetCharArray(_promptCharBuffer, 0, 0);
                return;
            }

            ILocalizationTextExpansionReadModel localization = _cachedLocalization;
            if (localization != null &&
                localization.TryExpandText(prompt, _promptCharBuffer, out int expandedLength))
            {
                promptText.SetCharArray(_promptCharBuffer, 0, math.min(expandedLength, _promptCharBuffer.Length));
                return;
            }

            int copyLength = math.min(prompt.Length, _promptCharBuffer.Length);
            prompt.Slice(0, copyLength).CopyTo(_promptCharBuffer);
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

        // --------------------------------------------------------------------------
        //  PRIVATE - REFERENCES
        // --------------------------------------------------------------------------

        private void ResolvePlayerReferences()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
            {
                _toolManager = null;
                _inventory = null;
                _mainCamera = null;
                return;
            }

            _toolManager = playerContext.ToolManager;

            _inventory = playerContext.Inventory;

            if (_mainCamera == null || !_mainCamera.isActiveAndEnabled)
                _mainCamera = playerContext.PlayerCamera;
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            HandleLanguageChanged((GameLanguage)payload.Language);

        }


        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshLocalizedPromptCache();
            QueuePromptPresentationRefresh(resetPrompt: true);
        }

        private void HandleInputDisplayStyleChanged(byte displayStyleCode)
        {
            RefreshLocalizedPromptCache();
            QueuePromptPresentationRefresh(resetPrompt: true);
        }

        private void HandleBindingChanged(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (!string.Equals(actionMap, PlayerActionMapName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(actionName, InteractActionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HandleBindingOverridesChanged();
        }

        private void HandleBindingCanceled(string actionName, string actionMap, int bindingIndex)
        {
            if (!string.Equals(actionMap, PlayerActionMapName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(actionName, InteractActionName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            HandleBindingOverridesChanged();
        }

        private void HandleBindingOverridesChanged()
        {
            RefreshLocalizedPromptCache();
            QueuePromptPresentationRefresh(resetPrompt: true);
        }

        private void QueuePromptPresentationRefresh(bool resetPrompt)
        {
            _promptPresentationDirty = true;
            if (!resetPrompt)
                return;

            _currentPrompt = null;
            _currentPromptSource = PromptSource.None;
            _promptProbeTimer = 0f;
            ClearPromptBuildCache();
        }

        private void ApplyPendingPromptPresentationRefresh()
        {
            if (!_promptPresentationDirty)
                return;

            _promptPresentationDirty = false;
            ConfigurePromptText();
        }

        private void SubscribeInputManagerIfAvailable()
        {
            if (_subscribedInputManager != null)
                return;

            INativeInputManagerRuntime inputManager = _cachedInputManager;
            if (inputManager == null)
                return;

            _subscribedInputManager = inputManager;
            _subscribedInputManager.OnInputDisplayStyleCodeChanged += HandleInputDisplayStyleChanged;
        }

        private void UnsubscribeInputManager()
        {
            if (_subscribedInputManager == null)
                return;

            _subscribedInputManager.OnInputDisplayStyleCodeChanged -= HandleInputDisplayStyleChanged;
            _subscribedInputManager = null;
        }

        private void EnsureCachedBindingDelegates()
        {
            _rebindCompletedAction ??= HandleBindingChanged; // COLD ALLOC: Action<string,string,int,string>[1] - cached prompt binding listener - owner: InteractionUI
            _rebindCanceledAction ??= HandleBindingCanceled; // COLD ALLOC: Action<string,string,int>[1] - cached prompt binding listener - owner: InteractionUI
            _bindingOverridesChangedAction ??= HandleBindingOverridesChanged; // COLD ALLOC: Action[1] - cached prompt binding listener - owner: InteractionUI
        }

        private void SubscribeInputBindingServiceIfAvailable(IInputBindingService bindingService)
        {
            if (_subscribedInputBindingService != null || bindingService == null)
                return;

            EnsureCachedBindingDelegates();
            _subscribedInputBindingService = bindingService;
            _subscribedInputBindingService.OnRebindCompleted += _rebindCompletedAction;
            _subscribedInputBindingService.OnRebindCanceled += _rebindCanceledAction;
            _subscribedInputBindingService.OnOverridesLoaded += _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesSaved += _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesCleared += _bindingOverridesChangedAction;
        }

        private void UnsubscribeInputBindingService()
        {
            if (_subscribedInputBindingService == null)
                return;

            _subscribedInputBindingService.OnRebindCompleted -= _rebindCompletedAction;
            _subscribedInputBindingService.OnRebindCanceled -= _rebindCanceledAction;
            _subscribedInputBindingService.OnOverridesLoaded -= _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesSaved -= _bindingOverridesChangedAction;
            _subscribedInputBindingService.OnOverridesCleared -= _bindingOverridesChangedAction;
            _subscribedInputBindingService = null;
        }

        private void RefreshCachedRegistryServices()
        {
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedInputManager = GlobalRegistry.NativeInputRuntime;
            _cachedPlayerActions = GlobalRegistry.PlayerActionInterrupts;
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationTextExpansion;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input &&
                serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime &&
                serviceSlot != GlobalRegistryServiceSlot.InputBinding &&
                serviceSlot != GlobalRegistryServiceSlot.Player &&
                serviceSlot != GlobalRegistryServiceSlot.PlayerActionRuntime &&
                serviceSlot != GlobalRegistryServiceSlot.LocalizationRuntime &&
                serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
            {
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterFromTick();
                if (currentService != null && isActiveAndEnabled)
                    RegisterToTick();

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.InputBinding)
            {
                UnsubscribeInputBindingService();
                if (!isActiveAndEnabled)
                    return;

                SubscribeInputBindingServiceIfAvailable(currentService as IInputBindingService);
                RefreshLocalizedPromptCache();
                QueuePromptPresentationRefresh(resetPrompt: true);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Input ||
                serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
            {
                UnsubscribeInputManager();
            }

            RefreshCachedRegistryServices();
            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime ||
                serviceSlot == GlobalRegistryServiceSlot.Input ||
                serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
            {
                RefreshLocalizedPromptCache();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _mainCamera = null;
                ResolvePlayerReferences();
            }

            if (!isActiveAndEnabled)
                return;

            if (serviceSlot == GlobalRegistryServiceSlot.Input ||
                serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
            {
                SubscribeInputManagerIfAvailable();
            }
            QueuePromptPresentationRefresh(resetPrompt: true);
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
            _cachedPromptSource = PromptSource.None;
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
                LocKeys.INTERACT_DEFAULT_PROMPT_FORMAT,
                defaultPromptFormat);
            _localizedNoBatteryPrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_NO_BATTERY_TO_SWAP, noBatteryPrompt);
            _localizedSwapBatteryPrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_SWAP_BATTERY, swapBatteryPrompt);
            _localizedDepositFuelPrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_DEPOSIT_FUEL, depositFuelPrompt);
            _localizedTakeItemPrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_TAKE_ITEM, takeItemPrompt);
            _localizedConsumableWithDurationFormat = ResolveLocalizedExpanded(
                LocKeys.INTERACT_CONSUMABLE_WITH_DURATION_FORMAT,
                consumableWithDurationFormat);
            _localizedActionInProgressPrompt = ResolveLocalizedExpanded(LocKeys.ACTION_USING, actionInProgressPrompt);
            _localizedInsertBatteryPrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_INSERT_BATTERY, "Insert Battery");
            _localizedBioReactorPrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_BIO_REACTOR, "Bio Reactor");
            _localizedOpenCratePrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_OPEN_CRATE, "<button:interact> Open Crate");
            _localizedEmptyCratePrompt = ResolveLocalizedExpanded(LocKeys.INTERACT_EMPTY_CRATE, "Empty Crate");
            _localizedVerbApply = ResolveLocalizedExpanded(LocKeys.INTERACT_VERB_APPLY, "Apply");
            _localizedVerbDrink = ResolveLocalizedExpanded(LocKeys.INTERACT_VERB_DRINK, "Drink");
            _localizedVerbEat = ResolveLocalizedExpanded(LocKeys.INTERACT_VERB_EAT, "Eat");
            _localizedVerbInhale = ResolveLocalizedExpanded(LocKeys.INTERACT_VERB_INHALE, "Inhale");
            _localizedVerbUse = ResolveLocalizedExpanded(LocKeys.INTERACT_VERB_USE, "Use");
            _localizedVerbTake = ResolveLocalizedExpanded(LocKeys.ITEM_INTERACT_TAKE, "Take");
        }

        private string ResolveLocalizedExpanded(int keyHash, string fallback)
        {
            fallback ??= string.Empty;

            ILocalizationTextExpansionReadModel localization = _cachedLocalization;
            ReadOnlySpan<char> fallbackSpan = fallback.AsSpan();
            ReadOnlySpan<char> source = localization != null && keyHash != 0
                ? localization.GetRawSpanOrFallback(keyHash, fallbackSpan)
                : fallbackSpan;

            if (source.IsEmpty)
                return string.Empty;

            bool hasInlineTokens = ContainsInlineTokenStart(source);
            if (hasInlineTokens &&
                localization != null &&
                localization.TryExpandText(source, _promptCharBuffer, out int expandedLength))
            {
                // COLD ALLOC: string[<=256 chars] - cached localized prompt text - owner: InteractionUI
                return new string(_promptCharBuffer, 0, math.min(expandedLength, _promptCharBuffer.Length));
            }

            if (source.SequenceEqual(fallbackSpan))
                return fallback;

            return CreatePromptCacheString(source);
        }

        private string CreatePromptCacheString(ReadOnlySpan<char> source)
        {
            int safeLength = math.min(source.Length, _promptCharBuffer.Length);
            source.Slice(0, safeLength).CopyTo(_promptCharBuffer);
            // COLD ALLOC: string[<=256 chars] - cached localized prompt text - owner: InteractionUI
            return new string(_promptCharBuffer, 0, safeLength);
        }

        private static bool ContainsInlineTokenStart(ReadOnlySpan<char> text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<')
                    return true;
            }

            return false;
        }

        private void RegisterToTick()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLateFrame)
                _registeredLateFrame = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void UnregisterFromTick()
        {
            if (!_registeredLateFrame)
                return;

            SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
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

        private static byte ResolveDisplayStyleFromSignal(uint schemeHash)
        {
            switch (schemeHash)
            {
                case 0x47504144u:
                    return NativeInputDisplayStyle.Gamepad;
                case 0x5354444Bu:
                    return NativeInputDisplayStyle.SteamDeck;
                case 0x58525443u:
                    return NativeInputDisplayStyle.XRTouch;
                default:
                    return NativeInputDisplayStyle.KeyboardMouse;
            }
        }
    }
}
