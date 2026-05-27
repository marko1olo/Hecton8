using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Pause Controls Panel")]
    public sealed class PauseControlsPanel : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.8f);
        private static readonly Color RuleColor = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color RowBg = new Color(0.05f, 0.12f, 0.14f, 0.62f);
        private static readonly Color BindingBg = new Color(0.08f, 0.18f, 0.2f, 0.75f);
        private static readonly Color LabelColor = new Color(0.8f, 0.95f, 0.92f, 0.92f);
        private static readonly Color BindingColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        private static readonly Color HintColor = new Color(0.58f, 0.78f, 0.74f, 0.72f);
        private static readonly Color SelectionColor = new Color(0.46f, 0.98f, 0.94f, 0.9f);

        [Serializable]
        private sealed class RebindRow
        {
            public string label = "Action";
            public string actionMap = "Player";
            public string actionName = "Interact";
            public int bindingIndex;
            public TextMeshProUGUI labelText;
            public TextMeshProUGUI bindingText;
            public GameObject selectedIndicator;
        }

        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset bindingFont;
        [SerializeField] private bool saveAfterRowReset = true;

        // TASK 17: Apply/Cancel/Reset buttons
        [Header("── Control Buttons ──────────────────")]
        [SerializeField] private Button applyButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button resetButton;

        private RebindRow[] _rows = Array.Empty<RebindRow>();
        private bool _built;
        private bool _subscribed;
        private bool _ownsActiveRebind;
        private INativeInputManagerRuntime _subscribedInput;
        private IInputBindingService _subscribedRebindingService;
        private bool _hotSwapListenerRegistered;
        private int _selectedIndex;
        private TextMeshProUGUI _statusText;
        private Image _statusBackground;
        private Image[] _rowBackgrounds = Array.Empty<Image>();
        private Image[] _rowAccentBars = Array.Empty<Image>();
        private Image[] _bindingBackgrounds = Array.Empty<Image>();
        private CanvasGroup[] _selectedIndicatorGroups = Array.Empty<CanvasGroup>();
        private UnityAction _applyClickAction;
        private UnityAction _cancelClickAction;
        private UnityAction _resetClickAction;
        private Action<Vector2> _navigateInputAction;
        private Action _submitInputAction;
        private Action<string, string, int> _rebindStartedAction;
        private Action<string, string, int, string> _rebindCompletedAction;
        private Action<string, string, int> _rebindCanceledAction;
        private Action<string, string, int> _rebindSaveFailedAction;
        private Action<string, string, string, Action, Action> _conflictDetectedAction;
        private uint _lastPlayerInputSignalSequence;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

        // ZERO-GC: Cached strings for status messages
        private static readonly string StatusRebindingUnavailable = "REBINDING SERVICE UNAVAILABLE.";
        private static readonly string StatusCannotResetWhileRebinding = "CANNOT RESET ALL WHILE REBINDING.";
        private static readonly string StatusAllBindingsReset = "ALL BINDINGS RESET TO DEFAULTS.";
        private static readonly string StatusRebindCanceled = "REBIND CANCELED.";
        private static readonly string StatusNoBindingsConfigured = "NO BINDINGS CONFIGURED.";
        private static readonly string StatusBindingsSaved = "BINDINGS SAVED.";
        private static readonly string StatusCannotSaveWhileRebinding = "CANNOT SAVE WHILE REBINDING.";
        private static readonly string StatusBindingsSaveFailed = "FAILED TO SAVE BINDINGS.";
        private static readonly string StatusBindingsLoadFailed = "FAILED TO LOAD SAVED BINDINGS.";
        private static readonly string StatusBindingsClearFailed = "FAILED TO CLEAR SAVED BINDINGS.";
        private static readonly string StatusBindingsReverted = "BINDINGS REVERTED TO SAVED STATE.";
        private static readonly string StatusBindingsResetToDefaults = "ALL BINDINGS RESET TO DEFAULTS.";
        private static readonly string StatusConflictTitle = "BINDING CONFLICT DETECTED";
        private static readonly string StatusFailedToStartPrefix = "FAILED TO START: ";
        private static readonly string StatusPressAKeyPrefix = "PRESS A KEY... [";
        private static readonly string StatusConflictPrefix = "CONFLICT: ";
        private static readonly string StatusConflictMiddle = " already used by ";
        private static readonly string StatusConflictModalUnavailable = "CONFLICT UI UNAVAILABLE; REBIND CANCELED.";
        private static readonly string StatusRebindPrefix = "REBIND: ";
        private static readonly string StatusRebindSuffix = "  |  TAB NEXT = RESET ONE  |  TAB PREV = RESET ALL";
        
        // ZERO-GC: Cached array for excluded control paths
        private static readonly string[] ExcludedControlPaths = { "<Pointer>/position", "<Pointer>/delta" };
        
        // ZERO-GC: Cached colors for status messages
        private static readonly Color StatusColorPressKey = new Color(0.82f, 0.98f, 1f, 0.96f);
        private static readonly Color StatusBgPressKey = new Color(0.08f, 0.22f, 0.34f, 0.9f);
        private static readonly Color StatusColorComplete = new Color(0.76f, 0.98f, 0.94f, 0.96f);
        private static readonly Color StatusBgComplete = new Color(0.08f, 0.2f, 0.18f, 0.88f);
        private static readonly Color StatusColorConflict = new Color(0.98f, 0.76f, 0.46f, 0.96f);
        private static readonly Color StatusBgConflict = new Color(0.34f, 0.18f, 0.08f, 0.9f);
        private static readonly Color StatusColorReverted = new Color(0.82f, 0.98f, 1f, 0.96f);
        private static readonly Color StatusBgReverted = new Color(0.08f, 0.22f, 0.34f, 0.9f);
        private static readonly Color StatusBgDefault = new Color(0.05f, 0.1f, 0.12f, 0.82f);
        
        // ZERO-GC: Cached colors for selection visuals (FIX: hardcoded colors in RefreshSelectionVisuals)
        private static readonly Color RowBgSelected = new Color(0.08f, 0.18f, 0.2f, 0.82f);
        private static readonly Color AccentDefault = new Color(0.18f, 0.32f, 0.34f, 0.78f);
        private static readonly Color AccentSelected = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color BindingBgSelected = new Color(0.1f, 0.24f, 0.28f, 0.86f);
        
        private readonly char[] _modalMessageBuffer = new char[256]; // COLD ALLOC: char[256] — modal conflict message staging buffer — owner: PauseControlsPanel
        private readonly char[] _statusBuffer = new char[256]; // COLD ALLOC: char[256] — status message staging buffer — owner: PauseControlsPanel
        private readonly char[] _bindingDisplayBuffer = new char[64]; // COLD ALLOC: char[64] — binding display text buffer — owner: PauseControlsPanel
        
        // ZERO-GC: Cached previous selection for optimized refresh
        private int _previousSelectedIndex = -1;

        private bool IsActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            pauseMenu != null &&
            pauseMenu.IsSettingsOpen;

        private void Awake()
        {
            _applyClickAction = OnApplyClicked; // COLD ALLOC: UnityAction[1] - cached controls apply listener - owner: PauseControlsPanel
            _cancelClickAction = OnCancelClicked; // COLD ALLOC: UnityAction[1] - cached controls cancel listener - owner: PauseControlsPanel
            _resetClickAction = OnResetToDefaultsClicked; // COLD ALLOC: UnityAction[1] - cached controls reset listener - owner: PauseControlsPanel
            EnsureCachedEventDelegates();

            if (pauseMenu == null)
            {
                for (Transform current = transform; current != null; current = current.parent)
                {
                    if (current.TryGetComponent(out pauseMenu))
                        break;
                }
            }

            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            if (bindingFont == null)
                bindingFont = labelFont;
            else
                bindingFont = LocalizedFontResolver.ResolveReadableFont(bindingFont);

            _rows = BuildDefaultRows(); // COLD ALLOC: RebindRow[15] — default rebinding rows — owner: PauseControlsPanel
            EnsureBuilt(); // COLD ALLOC: Image[45] + TextMeshProUGUI[30] — UI elements — owner: PauseControlsPanel

            // TASK 17: Wire button events
            if (applyButton != null)
            {
                applyButton.onClick.RemoveListener(_applyClickAction);
                applyButton.onClick.AddListener(_applyClickAction);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(_cancelClickAction);
                cancelButton.onClick.AddListener(_cancelClickAction);
            }

            if (resetButton != null)
            {
                resetButton.onClick.RemoveListener(_resetClickAction);
                resetButton.onClick.AddListener(_resetClickAction);
            }
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            RefreshAllBindingsIfActive();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            RefreshAllBindingsIfActive();
        }

        private void OnDisable()
        {
            // TASK 17: Save overrides when closing Settings section
            IInputBindingService rebinding = _subscribedRebindingService;
            CancelOwnedRebindIfNeeded(rebinding);
            if (ShouldSaveOverridesOnDisable(rebinding))
            {
                if (!rebinding.SaveOverrides())
                    SetStatus(StatusBindingsSaveFailed);
            }

            Unsubscribe();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            CancelOwnedRebindIfNeeded(_subscribedRebindingService);
            Unsubscribe();
            TryUnregisterHotSwapListener();

            if (applyButton != null)
                applyButton.onClick.RemoveListener(_applyClickAction);

            if (cancelButton != null)
                cancelButton.onClick.RemoveListener(_cancelClickAction);

            if (resetButton != null)
                resetButton.onClick.RemoveListener(_resetClickAction);
        }

        /// <summary>
        /// Configures the panel with owner reference and font assets.
        /// Called during initialization to set up panel dependencies.
        /// </summary>
        /// <param name="owner">Parent pause menu controller</param>
        /// <param name="labels">Font asset for action labels</param>
        /// <param name="bindings">Font asset for binding display text (uses labels if null)</param>
        public void Configure(PauseMenuController owner, TMP_FontAsset labels, TMP_FontAsset bindings)
        {
            pauseMenu = owner;
            labelFont = LocalizedFontResolver.ResolveReadableFont(labels);
            bindingFont = LocalizedFontResolver.ResolveReadableFont(bindings != null ? bindings : labelFont);
        }

        /// <summary>
        /// Refreshes all binding displays and UI state immediately.
        /// Subscribes to input events, rebuilds UI if needed, and updates all visual elements.
        /// Call this after changing input bindings or when panel becomes active.
        /// </summary>
        public void RefreshAllBindingsNow()
        {
            Subscribe();
            BaselinePlayerInputSignalSequence();
            EnsureBuilt();
            RefreshLabels();
            RefreshSelectionVisuals();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            EnsureCachedEventDelegates();
            INativeInputManagerRuntime input = ResolveInputManager();
            IInputBindingService rebinding = ResolveRebindingService();
            if (input == null || rebinding == null)
                return;

            input.OnNavigate += _navigateInputAction;
            input.OnSubmit += _submitInputAction;

            rebinding.OnRebindStarted += _rebindStartedAction;
            rebinding.OnRebindCompleted += _rebindCompletedAction;
            rebinding.OnRebindCanceled += _rebindCanceledAction;
            rebinding.OnRebindSaveFailed += _rebindSaveFailedAction;
            rebinding.OnConflictDetected += _conflictDetectedAction;

            _subscribedInput = input;
            _subscribedRebindingService = rebinding;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            INativeInputManagerRuntime input = _subscribedInput;
            if (input != null)
            {
                input.OnNavigate -= _navigateInputAction;
                input.OnSubmit -= _submitInputAction;
            }

            IInputBindingService rebinding = _subscribedRebindingService;
            if (rebinding != null)
            {
                rebinding.OnRebindStarted -= _rebindStartedAction;
                rebinding.OnRebindCompleted -= _rebindCompletedAction;
                rebinding.OnRebindCanceled -= _rebindCanceledAction;
                rebinding.OnRebindSaveFailed -= _rebindSaveFailedAction;
                rebinding.OnConflictDetected -= _conflictDetectedAction;
            }

            _subscribedInput = null;
            _subscribedRebindingService = null;
            _subscribed = false;
        }

        private void EnsureCachedEventDelegates()
        {
            _navigateInputAction ??= HandleNavigate; // COLD ALLOC: Action<Vector2>[1] - cached navigation input listener - owner: PauseControlsPanel
            _submitInputAction ??= HandleSubmit; // COLD ALLOC: Action[1] - cached submit input listener - owner: PauseControlsPanel
            _rebindStartedAction ??= HandleRebindStarted; // COLD ALLOC: Action<string,string,int>[1] - cached rebind-start listener - owner: PauseControlsPanel
            _rebindCompletedAction ??= HandleRebindCompleted; // COLD ALLOC: Action<string,string,int,string>[1] - cached rebind-complete listener - owner: PauseControlsPanel
            _rebindCanceledAction ??= HandleRebindCanceled; // COLD ALLOC: Action<string,string,int>[1] - cached rebind-cancel listener - owner: PauseControlsPanel
            _rebindSaveFailedAction ??= HandleRebindSaveFailed; // COLD ALLOC: Action<string,string,int>[1] - cached owner-local save-failed listener - owner: PauseControlsPanel
            _conflictDetectedAction ??= HandleConflictDetected; // COLD ALLOC: Action<string,string,string,Action,Action>[1] - cached conflict listener - owner: PauseControlsPanel
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input &&
                serviceSlot != GlobalRegistryServiceSlot.InputBinding)
            {
                return;
            }

            CancelOwnedRebindIfNeeded(_subscribedRebindingService);
            Unsubscribe();

            if (!isActiveAndEnabled)
                return;

            RefreshAllBindingsIfActive();
        }

        private void RefreshAllBindingsIfActive()
        {
            if (!IsActive)
                return;

            RefreshAllBindingsNow();
        }

        private bool ShouldSaveOverridesOnDisable(IInputBindingService rebinding)
        {
            return Application.isPlaying &&
                pauseMenu != null &&
                pauseMenu.IsSettingsOpen &&
                rebinding != null &&
                !rebinding.IsRebinding &&
                ResolveInputManager() != null;
        }

        private void CancelOwnedRebindIfNeeded(IInputBindingService rebinding)
        {
            if (!_ownsActiveRebind)
                return;

            if (rebinding == null || !rebinding.IsRebinding)
            {
                _ownsActiveRebind = false;
                return;
            }

            rebinding.CancelRebind();
            _ownsActiveRebind = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterHotSwapListener(this);
            _hotSwapListenerRegistered = GlobalRegistry.IsHotSwapListenerRegistered(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            if (GlobalRegistry.IsHotSwapListenerRegistered(this))
                GlobalRegistry.UnregisterHotSwapListener(this);

            _hotSwapListenerRegistered = false;
        }

        public bool ConsumePlayerInputSignals()
        {
            if (!IsActive)
            {
                CancelOwnedRebindIfNeeded(_subscribedRebindingService);
                return false;
            }

            bool cancelConsumed = false;
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                switch (signal.Command)
                {
                    case PlayerInputSignalCommands.Cancel:
                        cancelConsumed |= TryHandleCancelSignal();
                        break;
                    case PlayerInputSignalCommands.TabNext:
                        HandleTabNext();
                        break;
                    case PlayerInputSignalCommands.TabPrevious:
                        HandleTabPrevious();
                        break;
                }
            }

            return cancelConsumed;
        }

        private void BaselinePlayerInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void HandleNavigate(Vector2 direction)
        {
            if (!IsActive) return;
            if (_rows.Length == 0) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding != null && rebinding.IsRebinding) return;

            int delta = 0;
            if (direction.y > 0.35f) delta = -1;
            else if (direction.y < -0.35f) delta = 1;

            if (delta == 0) return;
            _selectedIndex = WrapIndex(_selectedIndex + delta, _rows.Length);
            RefreshSelectionVisuals();
            UpdateStatusForSelected();
        }

        private void HandleSubmit()
        {
            if (!IsActive) return;
            if (_rows.Length == 0) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding) return;

            RebindRow row = _rows[_selectedIndex];
            INativeInputManagerRuntime input = ResolveInputManager();
            if (!TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                SetStatus(resolutionMessage);
                return;
            }

            _ownsActiveRebind = true;
            bool started = rebinding.StartInteractiveRebind(
                row.actionName,
                row.actionMap,
                bindingIndex,
                expectedControlType: null,
                cancelPath: "<Keyboard>/escape",
                excludedControlPaths: ExcludedControlPaths); // ZERO-GC: Use cached array

            if (!started)
            {
                _ownsActiveRebind = false;
                int statusLength = 0;
                statusLength = AppendToBuffer(_statusBuffer, statusLength, StatusFailedToStartPrefix);
                statusLength = AppendToBuffer(_statusBuffer, statusLength, row.label);
                SetStatus(_statusBuffer, statusLength);
                return;
            }
        }

        private void HandleTabNext()
        {
            if (!IsActive) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding) return;
            ResetSelectedBinding();
        }

        private void HandleTabPrevious()
        {
            if (!IsActive) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding)
            {
                SetStatus(StatusCannotResetWhileRebinding);
                return;
            }

            if (rebinding.ClearOverrides())
            {
                RefreshAllBindingsNow();
                SetStatus(StatusAllBindingsReset);
                return;
            }

            SetStatus(StatusBindingsClearFailed);
        }

        private bool TryHandleCancelSignal()
        {
            if (!IsActive) return false;
            if (!_ownsActiveRebind) return false;

            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                _ownsActiveRebind = false;
                return false;
            }

            if (!rebinding.IsRebinding)
            {
                _ownsActiveRebind = false;
                UpdateStatusForSelected();
                return false;
            }

            rebinding.CancelRebind();
            _ownsActiveRebind = false;
            return true;
        }

        private void HandleRebindStarted(string actionName, string actionMap, int bindingIndex)
        {
            if (!_ownsActiveRebind) return;
            if (!IsActive) return;
            
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, StatusPressAKeyPrefix);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, actionMap);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, '/');
            statusLength = AppendToBuffer(_statusBuffer, statusLength, actionName);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, ']');
            
            SetStatus(_statusBuffer, statusLength, StatusColorPressKey, StatusBgPressKey);
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (!_ownsActiveRebind) return;
            _ownsActiveRebind = false;
            if (!IsActive) return;
            RefreshAllBindingsNow();
            
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, actionName);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, ": ");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, display);
            
            SetStatus(_statusBuffer, statusLength, StatusColorComplete, StatusBgComplete);
        }

        private void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            if (!_ownsActiveRebind) return;
            _ownsActiveRebind = false;
            if (!IsActive) return;
            RefreshAllBindingsNow();
            SetStatus(StatusRebindCanceled);
        }

        private void HandleRebindSaveFailed(string actionName, string actionMap, int bindingIndex)
        {
            if (!_ownsActiveRebind) return;
            _ownsActiveRebind = false;
            if (!IsActive) return;
            RefreshAllBindingsNow();
            SetStatus(StatusBindingsSaveFailed);
        }

        /// <summary>
        /// TASK 16: Handles conflict detection during rebinding.
        /// Displays modal window with conflict warning and confirm/cancel options.
        /// ModalWindow consumes the conflict body from the pooled char buffer.
        /// SAFETY: Validates ModalWindow availability before showing dialog.
        /// EXCEPTION-SAFE: Modal buffer length is rebuilt at method start to prevent stale data.
        /// </summary>
        private void HandleConflictDetected(string actionName, string conflictingAction, string newBinding, Action onConfirm, Action onCancel)
        {
            if (!_ownsActiveRebind) return;
            if (!IsActive) return;

            int messageLength = 0;
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, "The binding '");
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, newBinding);
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, "' is already assigned to '");
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, conflictingAction);
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, "'.\n\nDo you want to reassign it to '");
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, actionName);
            messageLength = AppendToBuffer(_modalMessageBuffer, messageLength, "'?");

            IModalWindowService modalWindow = GlobalRegistry.ModalWindow;
            if (modalWindow == null)
            {
                onCancel?.Invoke();
                SetStatus(StatusConflictModalUnavailable, StatusColorConflict, StatusBgConflict);
                return;
            }

            modalWindow.ShowModal(
                StatusConflictTitle,
                _modalMessageBuffer,
                messageLength,
                onConfirm,
                onCancel,
                null,
                null);

            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, StatusConflictPrefix);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, newBinding);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, StatusConflictMiddle);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, conflictingAction);

            SetStatus(_statusBuffer, statusLength, StatusColorConflict, StatusBgConflict);
        }

        /// <summary>
        /// TASK 17: Applies all binding changes and saves to controls.json.
        /// </summary>
        private void OnApplyClicked()
        {
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.IsRebinding)
            {
                SetStatus(StatusCannotSaveWhileRebinding);
                return;
            }

            if (rebinding.SaveOverrides())
            {
                SetStatus(StatusBindingsSaved, StatusColorComplete, StatusBgComplete);
                return;
            }

            SetStatus(StatusBindingsSaveFailed);
        }

        /// <summary>
        /// TASK 17: Cancels all binding changes and reloads saved controls.json overrides.
        /// </summary>
        private void OnCancelClicked()
        {
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.LoadOverrides())
            {
                RefreshAllBindingsNow();
                SetStatus(StatusBindingsReverted, StatusColorReverted, StatusBgReverted);
                return;
            }

            SetStatus(StatusBindingsLoadFailed);
        }

        /// <summary>
        /// TASK 17: Resets all bindings to defaults and clears saved controls.json overrides.
        /// </summary>
        private void OnResetToDefaultsClicked()
        {
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null)
            {
                SetStatus(StatusRebindingUnavailable);
                return;
            }

            if (rebinding.ClearOverrides())
            {
                RefreshAllBindingsNow();
                SetStatus(StatusBindingsResetToDefaults, StatusColorComplete, StatusBgComplete);
                return;
            }

            SetStatus(StatusBindingsClearFailed);
        }

        private void ResetSelectedBinding()
        {
            if (_rows.Length == 0)
                return;

            INativeInputManagerRuntime input = ResolveInputManager();
            if (input == null)
                return;

            RebindRow row = _rows[_selectedIndex];
            if (!TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                SetStatus(resolutionMessage);
                return;
            }

            string previousOverridePath = action.bindings[bindingIndex].overridePath;
            action.RemoveBindingOverride(bindingIndex);
            if (saveAfterRowReset)
            {
                IInputBindingService rebinding = ResolveRebindingService();
                if (rebinding == null || !rebinding.SaveOverrides())
                {
                    TryRestoreBindingOverride(action, bindingIndex, previousOverridePath);
                    RefreshRowBinding(row, input);
                    SetStatus(StatusBindingsSaveFailed);
                    return;
                }
            }

            RefreshRowBinding(row, input);
            UpdateStatusForSelected(input);
        }

        private static bool TryRestoreBindingOverride(InputAction action, int bindingIndex, string previousOverridePath)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            try
            {
                if (previousOverridePath == null)
                    action.RemoveBindingOverride(bindingIndex);
                else
                    action.ApplyBindingOverride(bindingIndex, previousOverridePath);

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform self = transform as RectTransform;
            if (self == null)
                return;

            if (_rows == null)
                _rows = Array.Empty<RebindRow>();

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            CreateRule(self, -48f);

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 16f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f), new Vector2(-18f, 24f));
            title.color = BindingColor;
            TmpTextNoAlloc.Set(title, "CONTROL MATRIX");

            TextMeshProUGUI hint = CreateText(self, "Hint", labelFont, 10f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -16f), new Vector2(-18f, 24f));
            hint.color = HintColor;
            TmpTextNoAlloc.Set(hint, "SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");

            RectTransform listRoot = CreateRect(self, "Rows");
            Stretch(listRoot, 18f, 18f, 58f, 66f);

            _rowBackgrounds = new Image[_rows.Length]; // COLD ALLOC: Image[15] — row backgrounds — owner: PauseControlsPanel
            _rowAccentBars = new Image[_rows.Length]; // COLD ALLOC: Image[15] — row accent bars — owner: PauseControlsPanel
            _bindingBackgrounds = new Image[_rows.Length]; // COLD ALLOC: Image[15] — binding backgrounds — owner: PauseControlsPanel
            _selectedIndicatorGroups = new CanvasGroup[_rows.Length]; // COLD ALLOC: CanvasGroup[15] — selection indicator cache — owner: PauseControlsPanel

            const float rowHeight = 28f;
            const float rowGap = 5f;
            for (int i = 0; i < _rows.Length; i++)
            {
                RebindRow row = _rows[i];

                RectTransform rowRoot = CreateRect(listRoot, "Row");
                rowRoot.anchorMin = new Vector2(0f, 1f);
                rowRoot.anchorMax = new Vector2(1f, 1f);
                rowRoot.pivot = new Vector2(0.5f, 1f);
                rowRoot.anchoredPosition = new Vector2(0f, -i * (rowHeight + rowGap));
                rowRoot.sizeDelta = new Vector2(0f, rowHeight);

                Image rowBg = EnsureImage(rowRoot.gameObject);
                rowBg.color = RowBg;
                rowBg.raycastTarget = false;
                _rowBackgrounds[i] = rowBg;

                RectTransform accent = CreateRect(rowRoot, "Accent");
                Anchor(accent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(4f, 0f));
                Image accentImg = EnsureImage(accent.gameObject);
                accentImg.color = AccentDefault;
                accentImg.raycastTarget = false;
                _rowAccentBars[i] = accentImg;

                RectTransform selected = CreateRect(rowRoot, "Selected");
                Anchor(selected, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(3f, 0f));
                Image selImg = EnsureImage(selected.gameObject);
                selImg.color = SelectionColor;
                selImg.raycastTarget = false;

                TextMeshProUGUI label = CreateText(rowRoot, "Label", labelFont, 11.5f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.56f, 1f), new Vector2(14f, 0f), new Vector2(-12f, 0f));
                label.color = LabelColor;

                RectTransform bindingBox = CreateRect(rowRoot, "BindingBox");
                Anchor(bindingBox, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(176f, 22f));
                Image bindingBg = EnsureImage(bindingBox.gameObject);
                bindingBg.color = BindingBg;
                bindingBg.raycastTarget = false;
                _bindingBackgrounds[i] = bindingBg;

                TextMeshProUGUI binding = CreateText(bindingBox, "Binding", bindingFont, 11f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(binding.rectTransform, 0f, 0f, 0f, 0f);
                binding.color = BindingColor;

                row.labelText = label;
                row.bindingText = binding;
                row.selectedIndicator = selected.gameObject;
                _selectedIndicatorGroups[i] = EnsureCanvasGroup(selected.gameObject);
            }

            RectTransform statusRoot = CreateRect(self, "Status");
            Anchor(statusRoot, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 18f), new Vector2(-18f, 32f));
            _statusBackground = EnsureImage(statusRoot.gameObject);
            _statusBackground.color = StatusBgDefault;
            _statusBackground.raycastTarget = false;

            _statusText = CreateText(statusRoot, "StatusText", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(_statusText.rectTransform, 12f, 12f, 0f, 0f);
            _statusText.color = HintColor;

            _built = true;
        }

        private void RefreshLabels()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                RebindRow row = _rows[i];
                if (row.labelText != null)
                    TmpTextNoAlloc.Set(row.labelText, row.label);
            }
        }

        private void RefreshAllBindings()
        {
            INativeInputManagerRuntime input = ResolveInputManager();
            for (int i = 0; i < _rows.Length; i++)
                RefreshRowBinding(_rows[i], input);
        }

        private void RefreshRowBinding(RebindRow row)
        {
            RefreshRowBinding(row, ResolveInputManager());
        }

        private void RefreshRowBinding(RebindRow row, INativeInputManagerRuntime input)
        {
            if (row == null || row.bindingText == null)
                return;

            if (!TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                TmpTextNoAlloc.Set(row.bindingText, resolutionMessage);
                return;
            }

            if (input.TryWriteBindingDisplayString(action, bindingIndex, _bindingDisplayBuffer, 0, out int charsWritten) &&
                charsWritten > 0)
            {
                row.bindingText.SetCharArray(_bindingDisplayBuffer, 0, charsWritten);
                return;
            }

            TmpTextNoAlloc.Set(row.bindingText, "--");
        }

        /// <summary>
        /// Refreshes selection visuals for all rows.
        /// OPTIMIZED: Only updates changed rows (previous and current selection).
        /// ZERO-GC: Uses cached static readonly colors to avoid allocations.
        /// </summary>
        private void RefreshSelectionVisuals()
        {
            // OPTIMIZATION: Only update previous and current selection to reduce native calls
            // from 15×3=45 to 2×3=6 per navigation
            
            if (_previousSelectedIndex >= 0 && _previousSelectedIndex < _rows.Length)
            {
                // Deselect previous
                if (_selectedIndicatorGroups != null &&
                    _previousSelectedIndex < _selectedIndicatorGroups.Length &&
                    _selectedIndicatorGroups[_previousSelectedIndex] != null)
                    SetIndicatorVisible(_selectedIndicatorGroups[_previousSelectedIndex], false);

                if (_rowBackgrounds[_previousSelectedIndex] != null)
                    _rowBackgrounds[_previousSelectedIndex].color = RowBg;

                if (_rowAccentBars[_previousSelectedIndex] != null)
                    _rowAccentBars[_previousSelectedIndex].color = AccentDefault; // FIXED: cached color

                if (_bindingBackgrounds[_previousSelectedIndex] != null)
                    _bindingBackgrounds[_previousSelectedIndex].color = BindingBg;
            }

            if (_selectedIndex >= 0 && _selectedIndex < _rows.Length)
            {
                // Select current
                if (_selectedIndicatorGroups != null &&
                    _selectedIndex < _selectedIndicatorGroups.Length &&
                    _selectedIndicatorGroups[_selectedIndex] != null)
                    SetIndicatorVisible(_selectedIndicatorGroups[_selectedIndex], true);

                if (_rowBackgrounds[_selectedIndex] != null)
                    _rowBackgrounds[_selectedIndex].color = RowBgSelected; // FIXED: cached color

                if (_rowAccentBars[_selectedIndex] != null)
                    _rowAccentBars[_selectedIndex].color = AccentSelected; // FIXED: cached color

                if (_bindingBackgrounds[_selectedIndex] != null)
                    _bindingBackgrounds[_selectedIndex].color = BindingBgSelected; // FIXED: cached color
            }

            _previousSelectedIndex = _selectedIndex;
        }

        private static void SetIndicatorVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject owner)
        {
            if (owner == null)
                return null;

            if (!owner.TryGetComponent(out CanvasGroup canvasGroup))
            {
                // COLD ALLOC: CanvasGroup[1] — missing selection indicator visibility proxy — owner: PauseControlsPanel
                canvasGroup = owner.AddComponent<CanvasGroup>();
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return canvasGroup;
        }

        private void UpdateStatusForSelected()
        {
            UpdateStatusForSelected(ResolveInputManager());
        }

        private void UpdateStatusForSelected(INativeInputManagerRuntime input)
        {
            if (_rows.Length == 0)
            {
                SetStatus(StatusNoBindingsConfigured);
                return;
            }

            RebindRow row = _rows[_selectedIndex];
            if (TryResolveRowBinding(input, row, out InputAction action, out int bindingIndex, out string resolutionMessage))
            {
                bool hasBindingDisplay = input.TryWriteBindingDisplayString(
                    action,
                    bindingIndex,
                    _bindingDisplayBuffer,
                    0,
                    out int bindingCharsWritten) &&
                    bindingCharsWritten > 0;

                int statusLength = 0;
                statusLength = AppendToBuffer(_statusBuffer, statusLength, StatusRebindPrefix);
                statusLength = AppendToBuffer(_statusBuffer, statusLength, row.label);
                statusLength = AppendToBuffer(_statusBuffer, statusLength, " [");
                if (hasBindingDisplay)
                    statusLength = AppendToBuffer(_statusBuffer, statusLength, _bindingDisplayBuffer, bindingCharsWritten);
                else
                    statusLength = AppendToBuffer(_statusBuffer, statusLength, "--");
                statusLength = AppendToBuffer(_statusBuffer, statusLength, ']');
                statusLength = AppendToBuffer(_statusBuffer, statusLength, StatusRebindSuffix);
                SetStatus(_statusBuffer, statusLength);
                return;
            }

            SetStatus(resolutionMessage);
        }

        private void SetStatus(string value)
        {
            SetStatus(value.AsSpan(), HintColor, StatusBgDefault);
        }

        private void SetStatus(char[] value, int length)
        {
            SetStatus(value, length, HintColor, StatusBgDefault);
        }

        private void SetStatus(ReadOnlySpan<char> value, Color textColor, Color backgroundColor)
        {
            if (_statusText != null)
            {
                int length = CopyToBuffer(_statusBuffer, value);
                _statusText.SetCharArray(_statusBuffer, 0, length);
                _statusText.color = textColor;
            }

            if (_statusBackground != null)
                _statusBackground.color = backgroundColor;
        }

        private void SetStatus(char[] value, int length, Color textColor, Color backgroundColor)
        {
            if (_statusText != null)
            {
                int safeLength = value != null ? Mathf.Clamp(length, 0, value.Length) : 0;
                _statusText.SetCharArray(value ?? _statusBuffer, 0, safeLength);
                _statusText.color = textColor;
            }

            if (_statusBackground != null)
                _statusBackground.color = backgroundColor;
        }

        private static int CopyToBuffer(char[] buffer, ReadOnlySpan<char> value)
        {
            if (buffer == null || value.IsEmpty)
                return 0;

            int length = Mathf.Min(buffer.Length, value.Length);
            value.Slice(0, length).CopyTo(buffer);
            return length;
        }

        private static int AppendToBuffer(char[] buffer, int index, string value)
        {
            return AppendToBuffer(buffer, index, value.AsSpan());
        }

        private static int AppendToBuffer(char[] buffer, int index, char value)
        {
            if (buffer == null || index < 0 || index >= buffer.Length)
                return index;

            buffer[index] = value;
            return index + 1;
        }

        private static int AppendToBuffer(char[] buffer, int index, char[] value, int valueLength)
        {
            if (value == null || valueLength <= 0)
                return index;

            int safeLength = Mathf.Min(valueLength, value.Length);
            return AppendToBuffer(buffer, index, value.AsSpan(0, safeLength));
        }

        private static int AppendToBuffer(char[] buffer, int index, ReadOnlySpan<char> value)
        {
            if (buffer == null || value.IsEmpty || index < 0 || index >= buffer.Length)
                return index;

            int length = Mathf.Min(value.Length, buffer.Length - index);
            value.Slice(0, length).CopyTo(buffer.AsSpan(index, length));
            return index + length;
        }

        private static int ResolveBindingIndex(InputAction action, int preferredIndex)
        {
            if (action == null)
                return -1;

            int bindingCount = action.bindings.Count;

            if (bindingCount == 0)
                return -1;

            if (preferredIndex >= 0 &&
                preferredIndex < bindingCount &&
                !action.bindings[preferredIndex].isComposite &&
                !action.bindings[preferredIndex].isPartOfComposite)
            {
                return preferredIndex;
            }

            for (int i = 0; i < bindingCount; i++)
            {
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                    return i;
            }

            return -1;
        }

        private static int WrapIndex(int value, int max)
        {
            if (max <= 0) return 0;
            if (value >= max) return 0;
            if (value < 0) return max - 1;
            return value;
        }

        private IInputBindingService ResolveRebindingService()
        {
            return _subscribedRebindingService != null
                ? _subscribedRebindingService
                : GlobalRegistry.InputBinding;
        }

        private INativeInputManagerRuntime ResolveInputManager()
        {
            return _subscribedInput != null
                ? _subscribedInput
                : GlobalRegistry.NativeInputRuntime;
        }

        private static RebindRow[] BuildDefaultRows()
        {
            return new[]
            {
                MakeRow("LOOK", "Player", "Look", 0),
                MakeRow("JUMP", "Player", "Jump", 0),
                MakeRow("SPRINT", "Player", "Sprint", 0),
                MakeRow("INTERACT", "Player", "Interact", 0),
                MakeRow("FLASHLIGHT", "Player", "Flashlight", 0),
                MakeRow("PDA", "Player", "PDA", 0),
                MakeRow("TOOL SLOT 1", "Player", "ToolSlot1", 0),
                MakeRow("TOOL SLOT 2", "Player", "ToolSlot2", 0),
                MakeRow("TOOL SLOT 3", "Player", "ToolSlot3", 0),
                MakeRow("TOOL SLOT 4", "Player", "ToolSlot4", 0),
                MakeRow("PRIMARY ACTION", "Player", "PrimaryAction", 0),
                MakeRow("SECONDARY ACTION", "Player", "SecondaryAction", 0),
                MakeRow("INVENTORY", "Player", "Inventory", 0),
                MakeRow("UI SUBMIT", "UI", "Submit", 0),
                MakeRow("UI CANCEL", "UI", "Cancel", 0)
            };
        }

        private static bool TryResolveRowBinding(
            INativeInputManagerRuntime input,
            RebindRow row,
            out InputAction action,
            out int bindingIndex,
            out string resolutionMessage)
        {
            action = null;
            bindingIndex = -1;
            resolutionMessage = "INPUT MANAGER UNAVAILABLE.";

            if (input == null)
                return false;

            if (row == null)
            {
                resolutionMessage = "INVALID BINDING ROW.";
                return false;
            }

            action = input.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                resolutionMessage = "MISSING ACTION.";
                return false;
            }

            bindingIndex = ResolveBindingIndex(action, row.bindingIndex);
            if (bindingIndex < 0)
            {
                resolutionMessage = "NO REBINDABLE BINDING.";
                return false;
            }

            resolutionMessage = string.Empty;
            return true;
        }

        private static RebindRow MakeRow(string label, string map, string action, int bindingIndex)
        {
            return new RebindRow
            {
                label = label,
                actionMap = map,
                actionName = action,
                bindingIndex = bindingIndex
            };
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image EnsureImage(GameObject target)
        {
            if (!target.TryGetComponent(out Image image))
                image = target.AddComponent<Image>();
            return image;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
            float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.TryGetComponent(out RectTransform rect);
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.color = LabelColor;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2((anchorMin.x + anchorMax.x) * 0.5f, (anchorMin.y + anchorMax.y) * 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void CreateRule(RectTransform parent, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = new Vector2(0.08f, 1f);
            rect.anchorMax = new Vector2(0.92f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(rect.gameObject);
            img.color = RuleColor;
            img.raycastTarget = false;
        }

        private static TMP_FontAsset ResolveReadableFont(TMP_FontAsset preferred)
        {
            if (preferred != null && !IsNumericOnlyFont(preferred))
                return preferred;

            TMP_FontAsset[] fonts = System.Array.Empty<TMP_FontAsset>();
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate == null)
                    continue;

                string name = candidate.name;
                if (name.IndexOf("tekst", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            return LocalizedFontResolver.ResolveReadableFont(preferred);
        }

        private static bool IsNumericOnlyFont(TMP_FontAsset font)
        {
            if (font == null)
                return false;

            string name = font.name;
            return name.IndexOf("tsif", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("digit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("number", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
