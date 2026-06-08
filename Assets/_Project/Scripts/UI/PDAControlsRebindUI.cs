using System;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Runtime rebinding controller for the PDA "Controls" tab.
    /// Event-driven, no Update polling, and resilient to missing references.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Controls Rebind UI")]
    public sealed class PDAControlsRebindUI : MonoBehaviour, IPDAEventListener, IGlobalRegistryHotSwapListener
    {
        private static readonly Color PanelBg = new Color(0.03f, 0.08f, 0.1f, 0.8f);
        private static readonly Color RuleColor = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Color RowBg = new Color(0.05f, 0.12f, 0.14f, 0.62f);
        private static readonly Color BindingBg = new Color(0.08f, 0.18f, 0.2f, 0.75f);
        private static readonly Color LabelColor = new Color(0.8f, 0.95f, 0.92f, 0.92f);
        private static readonly Color BindingColor = new Color(0.46f, 0.98f, 0.94f, 0.95f);
        private static readonly Color HintColor = new Color(0.58f, 0.78f, 0.74f, 0.72f);
        private static readonly Color SelectionColor = new Color(0.46f, 0.98f, 0.94f, 0.9f);
        private static readonly Color StatusBgDefault = new Color(0.05f, 0.1f, 0.12f, 0.82f);
        private static readonly Color StatusColorPressKey = new Color(0.82f, 0.98f, 1f, 0.96f);
        private static readonly Color StatusBgPressKey = new Color(0.08f, 0.22f, 0.34f, 0.9f);
        private static readonly Color StatusColorComplete = new Color(0.76f, 0.98f, 0.94f, 0.96f);
        private static readonly Color StatusBgComplete = new Color(0.08f, 0.2f, 0.18f, 0.88f);
        private static readonly Color StatusColorConflict = new Color(0.98f, 0.76f, 0.46f, 0.96f);
        private static readonly Color StatusBgConflict = new Color(0.34f, 0.18f, 0.08f, 0.9f);
        private static readonly Color RowBgSelected = new Color(0.08f, 0.18f, 0.2f, 0.82f);
        private static readonly Color AccentDefault = new Color(0.18f, 0.32f, 0.34f, 0.78f);
        private static readonly Color AccentSelected = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color BindingBgSelected = new Color(0.1f, 0.24f, 0.28f, 0.86f);
        private static readonly string StatusConflictTitle = "BINDING CONFLICT DETECTED";
        private static readonly string StatusConflictPrefix = "Conflict: ";
        private static readonly string StatusConflictMiddle = " already used by ";
        private static readonly string StatusBindingsSaveFailed = "Failed to save bindings.";
        private static readonly string StatusBindingsClearFailed = "Failed to clear saved bindings.";
        private static readonly string StatusConflictModalUnavailable = "Conflict UI unavailable; rebind canceled.";
        private static readonly string[] ExcludedControlPaths =
        {
            "<Pointer>/position",
            "<Pointer>/delta"
        };

        [Serializable]
        public sealed class RebindRow
        {
            [Tooltip("Display label shown in UI for this action.")]
            public string label = "Action";

            [Tooltip("Input action map name (Player/UI).")]
            public string actionMap = "Player";

            [Tooltip("Input action name inside map.")]
            public string actionName = "Interact";

            [Tooltip("Binding index for the action.")]
            public int bindingIndex;

            [Tooltip("Optional text label for action name.")]
            public TextMeshProUGUI labelText;

            [Tooltip("Binding text output (e.g. E, Left Shift, Mouse 0).")]
            public TextMeshProUGUI bindingText;

            [Tooltip("Optional visual indicator for currently selected row.")]
            public GameObject selectedIndicator;
        }

        [Header("References")]
        [SerializeField] private PlayerPDA playerPda;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset bindingFont;

        [Header("Settings")]
        [Tooltip("PDA tab index where controls panel is shown.")]
        [SerializeField] private int controlsTabIndex = 2;

        [Tooltip("Rows shown in controls rebinding panel.")]
        [SerializeField] private RebindRow[] rows = Array.Empty<RebindRow>();

        [Tooltip("Auto-generate a default controls list when rows are empty.")]
        [SerializeField] private bool autoGenerateRowsIfEmpty = true;

        [Tooltip("Auto-resolve row text references by child object naming convention.")]
        [SerializeField] private bool autoResolveRowReferences = true;

        [Tooltip("If true, SaveOverrides is called after per-row reset.")]
        [SerializeField] private bool saveAfterRowReset = true;

        [Header("Status Text")]
        [SerializeField] private string readyPrefix = "Rebind";
        [SerializeField] private string rebindingPrefix = "Press a key...";
        [SerializeField] private string resetHint = "TabNext = reset selected, TabPrevious = reset all";

        private bool _built;
        private int _selectedIndex;
        private bool _subscribed;
        private bool _ownsActiveRebind;
        private INativeInputManagerRuntime _cachedInput;
        private IInputBindingService _cachedRebindingService;
        private INativeInputManagerRuntime _subscribedInput;
        private IInputBindingService _subscribedRebindingService;
        private bool _hotSwapListenerRegistered;
        private bool _pdaEventsRegistered;
        private Image[] _rowBackgrounds = Array.Empty<Image>();
        private Image[] _rowAccentBars = Array.Empty<Image>();
        private Image[] _bindingBackgrounds = Array.Empty<Image>();
        private CanvasGroup[] _selectedIndicatorGroups = Array.Empty<CanvasGroup>();
        private Image _statusBackground;
        private TextMeshProUGUI _headerHintText;
        private bool _rowReferencesResolved;
        private Action<Vector2> _navigateInputAction;
        private Action _submitInputAction;
        private Action<byte> _displayStyleChangedAction;
        private Action<string, string, int> _rebindStartedAction;
        private Action<string, string, int, string> _rebindCompletedAction;
        private Action<string, string, int> _rebindCanceledAction;
        private Action<string, string, int> _rebindSaveFailedAction;
        private Action<string, string, string, Action, Action> _conflictDetectedAction;
        private Action _bindingOverridesChangedAction;
        private uint _lastPlayerInputSignalSequence;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private readonly char[] _headerHintBuffer = new char[128]; // COLD ALLOC: char[128] — controls header hint formatting buffer — owner: PDAControlsRebindUI
        private readonly char[] _statusBuffer = new char[192]; // COLD ALLOC: char[192] — controls status formatting buffer — owner: PDAControlsRebindUI
        private readonly char[] _modalMessageBuffer = new char[256]; // COLD ALLOC: char[256] — conflict modal staging buffer — owner: PDAControlsRebindUI
        private readonly char[] _bindingDisplayBuffer = new char[64]; // COLD ALLOC: char[64] — binding display text buffer — owner: PDAControlsRebindUI

        private bool IsControlsTabActive =>
            isActiveAndEnabled &&
            gameObject.activeInHierarchy &&
            PlayerPDA.IsOpen &&
            playerPda != null &&
            playerPda.ActiveTab == controlsTabIndex;

        private void Awake()
        {
            EnsureCachedEventDelegates();
            AutoResolveTabIndex();
            if (playerPda == null)
            {
                for (Transform current = transform; current != null; current = current.parent)
                {
                    if (current.TryGetComponent(out playerPda))
                        break;
                }
            }

            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            bindingFont = LocalizedFontResolver.ResolveReadableFont(bindingFont != null ? bindingFont : labelFont);

            if (rows == null) rows = Array.Empty<RebindRow>();
            EnsureRowsConfigured();
            EnsureBuilt();
            if (autoResolveRowReferences)
            {
                ResolveRowReferencesByName();
            }

            if (_selectedIndex >= rows.Length) _selectedIndex = 0;
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            AutoResolveTabIndex();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void AutoResolveTabIndex()
        {
            if (gameObject.name.Contains("Controls", StringComparison.OrdinalIgnoreCase))
                controlsTabIndex = 2;
        }

        private void OnEnable()
        {
            CacheInputServicesCold();
            TryRegisterHotSwapListener();
            Subscribe();
            RefreshAllIfControlsTabActive();
        }

        private void Start()
        {
            TryRegisterHotSwapListener();
            Subscribe();
            RefreshAllIfControlsTabActive();
        }

        private void OnDisable()
        {
            CancelOwnedRebindIfNeeded(_subscribedRebindingService);
            Unsubscribe();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            CancelOwnedRebindIfNeeded(_subscribedRebindingService);
            Unsubscribe();
            TryUnregisterHotSwapListener();
            PDAEvents.AssertUnregistered(this, nameof(PDAControlsRebindUI));
        }

        private void Subscribe()
        {
            if (_subscribed) return;

            EnsureCachedEventDelegates();
            INativeInputManagerRuntime input = ResolveInputManager();
            IInputBindingService rebinding = ResolveRebindingService();
            if (input == null || rebinding == null)
                return;

            input.OnNavigate += _navigateInputAction;
            input.OnSubmit += _submitInputAction;
            input.OnInputDisplayStyleCodeChanged += _displayStyleChangedAction;

            rebinding.OnRebindStarted += _rebindStartedAction;
            rebinding.OnRebindCompleted += _rebindCompletedAction;
            rebinding.OnRebindCanceled += _rebindCanceledAction;
            rebinding.OnRebindSaveFailed += _rebindSaveFailedAction;
            rebinding.OnConflictDetected += _conflictDetectedAction;
            rebinding.OnOverridesLoaded += _bindingOverridesChangedAction;
            rebinding.OnOverridesSaved += _bindingOverridesChangedAction;
            rebinding.OnOverridesCleared += _bindingOverridesChangedAction;

            _pdaEventsRegistered = PDAEvents.TryRegister(this);

            _subscribedInput = input;
            _subscribedRebindingService = rebinding;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            INativeInputManagerRuntime input = _subscribedInput;
            if (input != null)
            {
                input.OnNavigate -= _navigateInputAction;
                input.OnSubmit -= _submitInputAction;
                input.OnInputDisplayStyleCodeChanged -= _displayStyleChangedAction;
            }

            IInputBindingService rebinding = _subscribedRebindingService;
            if (rebinding != null)
            {
                rebinding.OnRebindStarted -= _rebindStartedAction;
                rebinding.OnRebindCompleted -= _rebindCompletedAction;
                rebinding.OnRebindCanceled -= _rebindCanceledAction;
                rebinding.OnRebindSaveFailed -= _rebindSaveFailedAction;
                rebinding.OnConflictDetected -= _conflictDetectedAction;
                rebinding.OnOverridesLoaded -= _bindingOverridesChangedAction;
                rebinding.OnOverridesSaved -= _bindingOverridesChangedAction;
                rebinding.OnOverridesCleared -= _bindingOverridesChangedAction;
            }

            if (_pdaEventsRegistered)
            {
                PDAEvents.Unregister(this);
                _pdaEventsRegistered = false;
            }

            _subscribedInput = null;
            _subscribedRebindingService = null;
            _subscribed = false;
        }

        private void EnsureCachedEventDelegates()
        {
            _navigateInputAction ??= HandleNavigate; // COLD ALLOC: Action<Vector2>[1] - cached navigation input listener - owner: PDAControlsRebindUI
            _submitInputAction ??= HandleSubmit; // COLD ALLOC: Action[1] - cached submit input listener - owner: PDAControlsRebindUI
            _displayStyleChangedAction ??= HandleInputDisplayStyleChanged; // COLD ALLOC: Action<byte>[1] - cached display-style listener - owner: PDAControlsRebindUI
            _rebindStartedAction ??= HandleRebindStarted; // COLD ALLOC: Action<string,string,int>[1] - cached rebind-start listener - owner: PDAControlsRebindUI
            _rebindCompletedAction ??= HandleRebindCompleted; // COLD ALLOC: Action<string,string,int,string>[1] - cached rebind-complete listener - owner: PDAControlsRebindUI
            _rebindCanceledAction ??= HandleRebindCanceled; // COLD ALLOC: Action<string,string,int>[1] - cached rebind-cancel listener - owner: PDAControlsRebindUI
            _rebindSaveFailedAction ??= HandleRebindSaveFailed; // COLD ALLOC: Action<string,string,int>[1] - cached owner-local save-failed listener - owner: PDAControlsRebindUI
            _conflictDetectedAction ??= HandleConflictDetected; // COLD ALLOC: Action<string,string,string,Action,Action>[1] - cached conflict listener - owner: PDAControlsRebindUI
            _bindingOverridesChangedAction ??= HandleBindingOverridesChanged; // COLD ALLOC: Action[1] - cached overrides-changed listener - owner: PDAControlsRebindUI
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Input &&
                serviceSlot != GlobalRegistryServiceSlot.NativeInputManagerRuntime &&
                serviceSlot != GlobalRegistryServiceSlot.InputBinding)
            {
                return;
            }

            CancelOwnedRebindIfNeeded(_subscribedRebindingService);
            Unsubscribe();
            if (serviceSlot == GlobalRegistryServiceSlot.InputBinding)
                _cachedRebindingService = currentService as IInputBindingService;
            else
                _cachedInput = currentService as INativeInputManagerRuntime ?? GlobalRegistry.NativeInputRuntime;

            if (!isActiveAndEnabled)
                return;

            Subscribe();
            RefreshAllIfControlsTabActive();
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

        public void ConsumePlayerInputSignals(
            out bool suppressCancel,
            out bool suppressTabNext,
            out bool suppressTabPrevious)
        {
            suppressCancel = false;
            suppressTabNext = false;
            suppressTabPrevious = false;

            if (!IsControlsTabActive)
            {
                CancelOwnedRebindIfNeeded(_subscribedRebindingService);
                return;
            }

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
                        suppressCancel |= TryHandleCancelSignal();
                        break;
                    case PlayerInputSignalCommands.TabNext:
                        suppressTabNext |= TryHandleTabNextSignal();
                        break;
                    case PlayerInputSignalCommands.TabPrevious:
                        suppressTabPrevious |= TryHandleTabPreviousSignal();
                        break;
                }
            }
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
            if (!IsControlsTabActive) return;
            if (rows == null || rows.Length == 0) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null || rebinding.IsRebinding) return;

            int delta = 0;
            if (direction.y > 0.35f) delta = -1;
            else if (direction.y < -0.35f) delta = 1;

            if (delta == 0) return;
            MoveSelection(delta);
        }

        private void HandleSubmit()
        {
            if (!IsControlsTabActive) return;
            if (rows == null || rows.Length == 0) return;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null || rebinding.IsRebinding) return;

            if (!TryGetSelectedRow(out RebindRow row, out _))
            {
                SetStatus("No bindings configured.");
                return;
            }

            INativeInputManagerRuntime inputManager = ResolveInputManager();
            if (inputManager == null)
            {
                SetStatus("Input manager unavailable.");
                return;
            }

            InputAction action = inputManager.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatusActionNotFound(row);
                return;
            }

            int bindingIndex = ResolveBindingIndex(inputManager, action, row.actionName, row.actionMap, row.bindingIndex);
            if (bindingIndex < 0)
            {
                SetStatusNoRebindableBinding(row.label);
                return;
            }

            _ownsActiveRebind = true;
            bool started = rebinding.StartInteractiveRebind(
                row.actionName,
                row.actionMap,
                bindingIndex,
                expectedControlType: null,
                excludedControlPaths: ExcludedControlPaths);

            if (!started)
            {
                _ownsActiveRebind = false;
                SetStatusFailedToStart(row.label);
                return;
            }
        }

        private bool TryHandleCancelSignal()
        {
            if (!PlayerPDA.IsOpen) return false;
            if (!_ownsActiveRebind) return false;

            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null || !rebinding.IsRebinding)
            {
                _ownsActiveRebind = false;
                return false;
            }

            rebinding.CancelRebind();
            _ownsActiveRebind = false;
            return true;
        }

        private bool TryHandleTabNextSignal()
        {
            if (!IsControlsTabActive) return false;
            if (rows == null || rows.Length == 0) return true;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null || rebinding.IsRebinding) return true;

            ResetSelectedBinding();
            return true;
        }

        private bool TryHandleTabPreviousSignal()
        {
            if (!IsControlsTabActive) return false;
            IInputBindingService rebinding = ResolveRebindingService();
            if (rebinding == null || rebinding.IsRebinding) return true;

            if (rebinding.ClearOverrides())
            {
                RefreshAllBindings();
                UpdateStatusForSelected();
                return true;
            }

            SetStatus(StatusBindingsClearFailed);
            return true;
        }

        private void HandleRebindStarted(string actionName, string actionMap, int bindingIndex)
        {
            if (!_ownsActiveRebind) return;
            if (!IsControlsTabActive) return;
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, rebindingPrefix);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, "  [");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, actionMap);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, '/');
            statusLength = AppendToBuffer(_statusBuffer, statusLength, actionName);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, ']');
            SetStatus(
                _statusBuffer,
                statusLength,
                StatusColorPressKey,
                StatusBgPressKey);
        }

        private void HandleRebindCompleted(string actionName, string actionMap, int bindingIndex, string display)
        {
            if (!_ownsActiveRebind) return;
            _ownsActiveRebind = false;
            if (!IsControlsTabActive) return;
            RefreshAllBindings();
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, actionName);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, ": ");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, display);
            SetStatus(
                _statusBuffer,
                statusLength,
                StatusColorComplete,
                StatusBgComplete);
        }

        private void HandleRebindCanceled(string actionName, string actionMap, int bindingIndex)
        {
            if (!_ownsActiveRebind) return;
            _ownsActiveRebind = false;
            if (!IsControlsTabActive) return;
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void HandleConflictDetected(string actionName, string conflictingAction, string newBinding, Action onConfirm, Action onCancel)
        {
            if (!_ownsActiveRebind) return;
            if (!IsControlsTabActive) return;

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

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePdaOpened(payload.CurrentTab);
                    break;
                case PDAEventType.Closed:
                    HandlePdaClosed();
                    break;
                case PDAEventType.TabChanged:
                    HandlePdaTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void HandlePdaTabChanged(int oldTab, int newTab)
        {
            if (oldTab == controlsTabIndex && newTab != controlsTabIndex)
                CancelOwnedRebindIfNeeded(_subscribedRebindingService);

            if (newTab != controlsTabIndex) return;
            RefreshAll();
        }

        private void HandlePdaOpened(int startTab)
        {
            if (startTab != controlsTabIndex) return;
            RefreshAll();
        }

        private void HandlePdaClosed()
        {
            CancelOwnedRebindIfNeeded(_subscribedRebindingService);
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

        private void HandleBindingOverridesChanged()
        {
            if (!IsControlsTabActive) return;
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void HandleRebindSaveFailed(string actionName, string actionMap, int bindingIndex)
        {
            if (!_ownsActiveRebind) return;
            _ownsActiveRebind = false;
            if (!IsControlsTabActive) return;
            RefreshAllBindings();
            SetStatus(StatusBindingsSaveFailed);
        }

        private void HandleInputDisplayStyleChanged(byte styleCode)
        {
            if (!IsControlsTabActive)
                return;

            UpdateHeaderHintText();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void ResetSelectedBinding()
        {
            if (!TryGetSelectedRow(out RebindRow row, out _))
            {
                SetStatus("No bindings configured.");
                return;
            }

            INativeInputManagerRuntime inputManager = ResolveInputManager();
            if (inputManager == null)
            {
                SetStatus("Input manager unavailable.");
                return;
            }

            InputAction action = inputManager.GetAction(row.actionName, row.actionMap);
            if (action == null)
            {
                SetStatusActionNotFound(row);
                return;
            }

            int bindingIndex = ResolveBindingIndex(inputManager, action, row.actionName, row.actionMap, row.bindingIndex);
            if (bindingIndex < 0)
            {
                SetStatusNoRebindableBinding(row.actionName);
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
                    RefreshRowBinding(row, inputManager);
                    SetStatus(StatusBindingsSaveFailed);
                    return;
                }
            }

            RefreshRowBinding(row, inputManager);
            UpdateStatusForSelected(inputManager);
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

        private void RefreshAll()
        {
            BaselinePlayerInputSignalSequence();
            EnsureBuilt();
            if (autoResolveRowReferences && !_rowReferencesResolved)
            {
                ResolveRowReferencesByName();
            }

            NormalizeSelectedIndex();
            UpdateHeaderHintText();
            RefreshLabels();
            RefreshSelectionVisuals();
            RefreshAllBindings();
            UpdateStatusForSelected();
        }

        private void RefreshAllIfControlsTabActive()
        {
            if (!IsControlsTabActive)
                return;

            RefreshAll();
        }

        private void EnsureBuilt()
        {
            if (_built) return;

            RectTransform self = transform as RectTransform;
            if (self == null) return;

            if (rows == null || rows.Length == 0)
                return;

            bool alreadyHasRowRefs = true;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null ||
                    rows[i].labelText == null ||
                    rows[i].bindingText == null ||
                    rows[i].selectedIndicator == null)
                {
                    alreadyHasRowRefs = false;
                    break;
                }
            }

            if (alreadyHasRowRefs && statusText != null)
            {
                RebuildSelectedIndicatorGroupCache();
                _built = true;
                return;
            }

            ClearChildren(self);

            Image bg = EnsureImage(self.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;

            CreateRule(self, new Vector2(0.08f, 1f), new Vector2(0.92f, 1f), -52f);

            TextMeshProUGUI title = CreateText(self, "Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            title.color = BindingColor;
            TmpTextNoAlloc.Set(title, "CONTROL MATRIX");

            TextMeshProUGUI hint = CreateText(self, "Hint", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(18f, -18f), new Vector2(-18f, 24f));
            hint.color = HintColor;
            _headerHintText = hint;
            UpdateHeaderHintText();

            RectTransform listRoot = CreateRect(self, "Rows");
            Anchor(listRoot, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(18f, 72f), new Vector2(-18f, -72f));

            _rowBackgrounds = new Image[rows.Length];
            _rowAccentBars = new Image[rows.Length];
            _bindingBackgrounds = new Image[rows.Length];
            _selectedIndicatorGroups = new CanvasGroup[rows.Length]; // COLD ALLOC: CanvasGroup[rows.Length] — selection indicator cache — owner: PDAControlsRebindUI

            const float rowHeight = 30f;
            const float rowGap = 6f;
            float totalHeight = rows.Length * rowHeight + Mathf.Max(0, rows.Length - 1) * rowGap;
            float startY = -Mathf.Max(0f, (listRoot.rect.height - totalHeight) * 0.5f);

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (row == null)
                    continue;

                RectTransform rowRoot = CreateRect(listRoot, "Row");
                Anchor(rowRoot, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, startY - i * (rowHeight + rowGap)),
                    new Vector2(0f, rowHeight));

                Image rowBg = EnsureImage(rowRoot.gameObject);
                rowBg.color = RowBg;
                rowBg.raycastTarget = false;
                _rowBackgrounds[i] = rowBg;

                RectTransform accent = CreateRect(rowRoot, "Accent");
                Anchor(accent, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(4f, 0f));
                Image accentImg = EnsureImage(accent.gameObject);
                accentImg.color = AccentDefault;
                accentImg.raycastTarget = false;
                _rowAccentBars[i] = accentImg;

                RectTransform selected = CreateRect(rowRoot, "Selected");
                Anchor(selected, new Vector2(0f, 0f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(3f, 0f));
                Image selImg = EnsureImage(selected.gameObject);
                selImg.color = SelectionColor;
                selImg.raycastTarget = false;

                TextMeshProUGUI label = CreateText(rowRoot, "Label",
                    labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.Left);
                Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f),
                    new Vector2(14f, 0f), new Vector2(-12f, 0f));
                label.color = LabelColor;

                RectTransform bindingBox = CreateRect(rowRoot, "BindingBox");
                Anchor(bindingBox, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    new Vector2(-12f, 0f), new Vector2(164f, 22f));
                Image bindingBg = EnsureImage(bindingBox.gameObject);
                bindingBg.color = BindingBg;
                bindingBg.raycastTarget = false;
                _bindingBackgrounds[i] = bindingBg;

                TextMeshProUGUI binding = CreateText(bindingBox, "Binding",
                    bindingFont, 11.5f, FontStyles.Bold, TextAlignmentOptions.Center);
                Stretch(binding.rectTransform, 0f, 0f, 0f, 0f);
                binding.color = BindingColor;

                row.labelText = label;
                row.bindingText = binding;
                row.selectedIndicator = selected.gameObject;
                _selectedIndicatorGroups[i] = EnsureCanvasGroup(selected.gameObject);
            }

            RectTransform statusRoot = CreateRect(self, "Status");
            Anchor(statusRoot, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(18f, 18f), new Vector2(-18f, 36f));
            Image statusBg = EnsureImage(statusRoot.gameObject);
            statusBg.color = StatusBgDefault;
            statusBg.raycastTarget = false;
            _statusBackground = statusBg;

            statusText = CreateText(statusRoot, "StatusText", labelFont, 11f, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(statusText.rectTransform, 12f, 0f, 12f, 0f);
            statusText.color = HintColor;

            _built = true;
            _rowReferencesResolved = true;
        }

        private void EnsureRowsConfigured()
        {
            if (rows != null && rows.Length > 0) return;
            if (!autoGenerateRowsIfEmpty) return;
            rows = BuildDefaultRows();
        }

        private void NormalizeSelectedIndex()
        {
            if (rows == null || rows.Length == 0)
            {
                _selectedIndex = 0;
                return;
            }

            if (_selectedIndex >= 0 && _selectedIndex < rows.Length && IsBindableRow(rows[_selectedIndex]))
                return;

            _selectedIndex = GetFirstValidRowIndex();
            if (_selectedIndex < 0)
                _selectedIndex = 0;
        }

        private void ResolveRowReferencesByName()
        {
            if (_headerHintText == null)
            {
                Transform hintTransform = FindDeepChild(transform, "Hint");
                if (hintTransform != null)
                    hintTransform.TryGetComponent(out _headerHintText);
            }

            if (rows == null || rows.Length == 0) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (row == null) continue;

                if (string.IsNullOrWhiteSpace(row.label))
                {
                    row.label = row.actionName;
                }

                if (row.labelText == null)
                {
                    Transform t = FindDeepChildByPrefixSuffix(transform, "Label_", row.actionName) ?? FindDeepChild(transform, "Label");
                    if (t != null) t.TryGetComponent(out row.labelText);
                }

                if (row.bindingText == null)
                {
                    Transform t = FindDeepChildByPrefixSuffix(transform, "Binding_", row.actionName) ?? FindDeepChild(transform, "Binding");
                    if (t != null) t.TryGetComponent(out row.bindingText);
                }

                if (row.selectedIndicator == null)
                {
                    Transform t = FindDeepChildByPrefixSuffix(transform, "Selected_", row.actionName) ?? FindDeepChild(transform, "Selected");
                    if (t != null) row.selectedIndicator = t.gameObject;
                }
            }

            RebuildSelectedIndicatorGroupCache();
            _rowReferencesResolved = true;
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null || string.IsNullOrEmpty(targetName)) return null;
            if (parent.name == targetName) return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                Transform result = FindDeepChild(child, targetName);
                if (result != null) return result;
            }

            return null;
        }

        private static Transform FindDeepChildByPrefixSuffix(Transform parent, string prefix, string suffix)
        {
            if (parent == null || string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
                return null;

            string name = parent.name;
            if (!string.IsNullOrEmpty(name) &&
                name.StartsWith(prefix, StringComparison.Ordinal) &&
                name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return parent;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeepChildByPrefixSuffix(parent.GetChild(i), prefix, suffix);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static RebindRow[] BuildDefaultRows()
        {
            return new[]
            {
                MakeRow("Look", "Player", "Look", 5),
                MakeRow("Jump", "Player", "Jump", 6),
                MakeRow("Sprint", "Player", "Sprint", 9),
                MakeRow("Interact", "Player", "Interact", 11),
                MakeRow("Flashlight", "Player", "Flashlight", 13),
                MakeRow("PDA", "Player", "PDA", 15),
                MakeRow("Tool Slot 1", "Player", "ToolSlot1", 17),
                MakeRow("Tool Slot 2", "Player", "ToolSlot2", 18),
                MakeRow("Tool Slot 3", "Player", "ToolSlot3", 19),
                MakeRow("Tool Slot 4", "Player", "ToolSlot4", 20),
                MakeRow("Primary Action", "Player", "PrimaryAction", 21),
                MakeRow("Secondary Action", "Player", "SecondaryAction", 23),
                MakeRow("Inventory", "Player", "Inventory", 28),
                MakeRow("UI Navigate", "UI", "Navigate", 5),
                MakeRow("UI Submit", "UI", "Submit", 7),
                MakeRow("UI Cancel", "UI", "Cancel", 10)
            };
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

        private void RefreshLabels()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (!IsBindableRow(row))
                    continue;

                if (row.labelText != null)
                {
                    TmpTextNoAlloc.Set(row.labelText, row.label);
                }
            }
        }

        private void RefreshAllBindings()
        {
            if (rows == null) return;

            INativeInputManagerRuntime inputManager = ResolveInputManager();
            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (!IsBindableRow(row))
                    continue;

                RefreshRowBinding(row, inputManager);
            }
        }

        private void RefreshRowBinding(RebindRow row)
        {
            RefreshRowBinding(row, ResolveInputManager());
        }

        private void RefreshRowBinding(RebindRow row, INativeInputManagerRuntime inputManager)
        {
            if (!IsBindableRow(row))
                return;

            if (row.bindingText == null) return;

            InputAction action = inputManager != null
                ? inputManager.GetAction(row.actionName, row.actionMap)
                : null;
            int bindingIndex = action != null
                ? ResolveBindingIndex(inputManager, action, row.actionName, row.actionMap, row.bindingIndex)
                : -1;
            if (action == null || bindingIndex < 0)
            {
                TmpTextNoAlloc.Set(row.bindingText, "--");
                return;
            }

            if (inputManager.TryWriteBindingDisplayString(action, bindingIndex, _bindingDisplayBuffer, 0, out int charsWritten) &&
                charsWritten > 0)
            {
                row.bindingText.SetCharArray(_bindingDisplayBuffer, 0, charsWritten);
                return;
            }

            TmpTextNoAlloc.Set(row.bindingText, "--");
        }

        private void RefreshSelectionVisuals()
        {
            if (rows == null) return;

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                if (!IsBindableRow(row))
                    continue;

                CanvasGroup indicatorGroup = _selectedIndicatorGroups != null && i < _selectedIndicatorGroups.Length
                    ? _selectedIndicatorGroups[i]
                    : null;
                if (indicatorGroup != null)
                {
                    SetIndicatorVisible(indicatorGroup, i == _selectedIndex);
                }

                if (_rowBackgrounds != null && i < _rowBackgrounds.Length && _rowBackgrounds[i] != null)
                {
                    _rowBackgrounds[i].color = i == _selectedIndex
                        ? RowBgSelected
                        : RowBg;
                }

                if (_rowAccentBars != null && i < _rowAccentBars.Length && _rowAccentBars[i] != null)
                {
                    _rowAccentBars[i].color = i == _selectedIndex
                        ? AccentSelected
                        : AccentDefault;
                }

                if (_bindingBackgrounds != null && i < _bindingBackgrounds.Length && _bindingBackgrounds[i] != null)
                {
                    _bindingBackgrounds[i].color = i == _selectedIndex
                        ? BindingBgSelected
                        : BindingBg;
                }
            }
        }

        private static void SetIndicatorVisible(CanvasGroup canvasGroup, bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void RebuildSelectedIndicatorGroupCache()
        {
            if (rows == null || rows.Length == 0)
            {
                _selectedIndicatorGroups = Array.Empty<CanvasGroup>();
                return;
            }

            if (_selectedIndicatorGroups == null || _selectedIndicatorGroups.Length != rows.Length)
                _selectedIndicatorGroups = new CanvasGroup[rows.Length]; // COLD ALLOC: CanvasGroup[rows.Length] — selection indicator cache rebuild — owner: PDAControlsRebindUI

            for (int i = 0; i < rows.Length; i++)
            {
                RebindRow row = rows[i];
                _selectedIndicatorGroups[i] = row != null && row.selectedIndicator != null
                    ? EnsureCanvasGroup(row.selectedIndicator)
                    : null;
            }
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject owner)
        {
            if (owner == null)
                return null;

            if (!owner.TryGetComponent(out CanvasGroup canvasGroup))
            {
                // COLD ALLOC: CanvasGroup[1] - missing selection indicator visibility proxy - owner: PDAControlsRebindUI
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

        private void UpdateStatusForSelected(INativeInputManagerRuntime inputManager)
        {
            if (!TryGetSelectedRow(out RebindRow row, out _))
            {
                SetStatus("No bindings configured.");
                return;
            }

            bool hasBindingDisplay = false;
            int bindingCharsWritten = 0;
            InputAction action = inputManager != null
                ? inputManager.GetAction(row.actionName, row.actionMap)
                : null;
            if (action != null)
            {
                int bindingIndex = ResolveBindingIndex(inputManager, action, row.actionName, row.actionMap, row.bindingIndex);
                if (bindingIndex >= 0)
                {
                    hasBindingDisplay = inputManager.TryWriteBindingDisplayString(
                        action,
                        bindingIndex,
                        _bindingDisplayBuffer,
                        0,
                        out bindingCharsWritten) &&
                        bindingCharsWritten > 0;
                }
            }

            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, readyPrefix);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, ": ");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, row.label);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, " [");
            if (hasBindingDisplay)
                statusLength = AppendToBuffer(_statusBuffer, statusLength, _bindingDisplayBuffer, bindingCharsWritten);
            else
                statusLength = AppendToBuffer(_statusBuffer, statusLength, "--");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, "]  |  ");
            AppendResetHintText(_statusBuffer, ref statusLength);
            SetStatus(_statusBuffer, statusLength);
        }

        private bool TryGetSelectedRow(out RebindRow row, out int rowIndex)
        {
            row = null;
            rowIndex = -1;

            if (rows == null || rows.Length == 0)
                return false;

            NormalizeSelectedIndex();

            if (_selectedIndex >= 0 && _selectedIndex < rows.Length)
            {
                row = rows[_selectedIndex];
                if (IsBindableRow(row))
                {
                    rowIndex = _selectedIndex;
                    return true;
                }
            }

            rowIndex = GetFirstValidRowIndex();
            if (rowIndex < 0)
                return false;

            _selectedIndex = rowIndex;
            row = rows[rowIndex];
            return IsBindableRow(row);
        }

        private static bool IsBindableRow(RebindRow row)
        {
            return row != null &&
                   !string.IsNullOrWhiteSpace(row.actionMap) &&
                   !string.IsNullOrWhiteSpace(row.actionName);
        }

        private int GetFirstValidRowIndex()
        {
            if (rows == null || rows.Length == 0)
                return -1;

            for (int i = 0; i < rows.Length; i++)
            {
                if (IsBindableRow(rows[i]))
                    return i;
            }

            return -1;
        }

        private void MoveSelection(int delta)
        {
            if (rows == null || rows.Length == 0)
                return;

            NormalizeSelectedIndex();
            int current = _selectedIndex;

            for (int i = 0; i < rows.Length; i++)
            {
                current = WrapIndex(current + delta, rows.Length);
                if (IsBindableRow(rows[current]))
                {
                    _selectedIndex = current;
                    RefreshSelectionVisuals();
                    UpdateStatusForSelected();
                    return;
                }
            }
        }

        private static int ResolveBindingIndex(
            INativeInputManagerRuntime inputManager,
            InputAction action,
            string actionName,
            string actionMap,
            int preferredIndex)
        {
            if (action == null)
                return -1;

            if (inputManager != null)
            {
                int displayPreferredIndex = inputManager.GetPreferredBindingIndex(actionName, actionMap);
                if (IsBindableActionIndex(action, displayPreferredIndex))
                    return displayPreferredIndex;
            }

            int bindingCount = action.bindings.Count;

            if (bindingCount == 0)
                return -1;

            if (IsBindableActionIndex(action, preferredIndex))
                return preferredIndex;

            for (int i = 0; i < bindingCount; i++)
            {
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite)
                    return i;
            }

            return -1;
        }

        private static bool IsBindableActionIndex(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0)
                return false;

            return bindingIndex < action.bindings.Count &&
                   !action.bindings[bindingIndex].isComposite &&
                   !action.bindings[bindingIndex].isPartOfComposite;
        }

        private void AppendResetHintText(char[] buffer, ref int index)
        {
            INativeInputManagerRuntime inputManager = ResolveInputManager();
            if (inputManager == null)
            {
                index = AppendToBuffer(buffer, index, resetHint);
                return;
            }

            int startLength = index;
            if (!TryAppendBindingDisplay(inputManager, "TabNext", "UI", buffer, ref index))
            {
                index = AppendToBuffer(buffer, index, resetHint);
                return;
            }

            index = AppendToBuffer(buffer, index, " = reset selected, ");
            if (!TryAppendBindingDisplay(inputManager, "TabPrevious", "UI", buffer, ref index))
            {
                index = startLength;
                index = AppendToBuffer(buffer, index, resetHint);
                return;
            }

            index = AppendToBuffer(buffer, index, " = reset all");
        }

        private void UpdateHeaderHintText()
        {
            if (_headerHintText == null)
                return;

            INativeInputManagerRuntime inputManager = ResolveInputManager();
            if (inputManager == null)
            {
                SetHeaderHint("SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");
                return;
            }

            int headerLength = 0;
            if (!TryAppendBindingDisplay(inputManager, "Submit", "UI", _headerHintBuffer, ref headerLength))
            {
                SetHeaderHint("SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");
                return;
            }

            headerLength = AppendToBuffer(_headerHintBuffer, headerLength, " = rebind  |  ");
            if (!TryAppendBindingDisplay(inputManager, "TabNext", "UI", _headerHintBuffer, ref headerLength))
            {
                SetHeaderHint("SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");
                return;
            }

            headerLength = AppendToBuffer(_headerHintBuffer, headerLength, " = reset one  |  ");
            if (!TryAppendBindingDisplay(inputManager, "TabPrevious", "UI", _headerHintBuffer, ref headerLength))
            {
                SetHeaderHint("SUBMIT = rebind  |  TAB NEXT = reset one  |  TAB PREV = reset all");
                return;
            }

            headerLength = AppendToBuffer(_headerHintBuffer, headerLength, " = reset all");
            SetHeaderHint(_headerHintBuffer, headerLength);
        }

        private bool TryAppendBindingDisplay(INativeInputManagerRuntime inputManager, string actionName, string actionMap, char[] buffer, ref int index)
        {
            if (inputManager == null || buffer == null || index < 0 || index >= buffer.Length)
                return false;

            InputAction action = inputManager.GetAction(actionName, actionMap);
            if (action == null)
                return false;

            int bindingIndex = ResolveBindingIndex(inputManager, action, actionName, actionMap, -1);
            if (bindingIndex < 0)
                return false;

            if (!inputManager.TryWriteBindingDisplayString(action, bindingIndex, buffer, index, out int charsWritten) ||
                charsWritten <= 0)
            {
                return false;
            }

            index += charsWritten;
            return true;
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
            if (statusText != null)
            {
                int length = CopyToBuffer(_statusBuffer, value);
                statusText.SetCharArray(_statusBuffer, 0, length);
                statusText.color = textColor;
            }

            if (_statusBackground != null)
            {
                _statusBackground.color = backgroundColor;
            }
        }

        private void SetStatus(char[] value, int length, Color textColor, Color backgroundColor)
        {
            if (statusText != null)
            {
                int safeLength = value != null ? Mathf.Clamp(length, 0, value.Length) : 0;
                statusText.SetCharArray(value ?? _statusBuffer, 0, safeLength);
                statusText.color = textColor;
            }

            if (_statusBackground != null)
            {
                _statusBackground.color = backgroundColor;
            }
        }

        private void SetStatusActionNotFound(RebindRow row)
        {
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, "Action not found: ");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, row.actionMap);
            statusLength = AppendToBuffer(_statusBuffer, statusLength, '/');
            statusLength = AppendToBuffer(_statusBuffer, statusLength, row.actionName);
            SetStatus(_statusBuffer, statusLength);
        }

        private void SetStatusNoRebindableBinding(string label)
        {
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, "No rebindable binding: ");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, label);
            SetStatus(_statusBuffer, statusLength);
        }

        private void SetStatusFailedToStart(string label)
        {
            int statusLength = 0;
            statusLength = AppendToBuffer(_statusBuffer, statusLength, "Failed to start: ");
            statusLength = AppendToBuffer(_statusBuffer, statusLength, label);
            SetStatus(_statusBuffer, statusLength);
        }

        private void SetHeaderHint(string value)
        {
            SetHeaderHint(value.AsSpan());
        }

        private void SetHeaderHint(ReadOnlySpan<char> value)
        {
            if (_headerHintText == null)
                return;

            int length = CopyToBuffer(_headerHintBuffer, value);
            _headerHintText.SetCharArray(_headerHintBuffer, 0, length);
        }

        private void SetHeaderHint(char[] value, int length)
        {
            if (_headerHintText == null)
                return;

            int safeLength = value != null ? Mathf.Clamp(length, 0, value.Length) : 0;
            _headerHintText.SetCharArray(value ?? _headerHintBuffer, 0, safeLength);
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
                : _cachedRebindingService;
        }

        private INativeInputManagerRuntime ResolveInputManager()
        {
            return _subscribedInput != null
                ? _subscribedInput
                : _cachedInput;
        }

        private void CacheInputServicesCold()
        {
            _cachedInput = GlobalRegistry.NativeInputRuntime;
            _cachedRebindingService = GlobalRegistry.InputBinding;
        }

        public void Configure(PlayerPDA pda, TextMeshProUGUI statusOutput, int tabIndex)
        {
            playerPda = pda;
            statusText = statusOutput;
            controlsTabIndex = tabIndex;
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

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
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
            text.textWrappingMode = TextWrappingModes.NoWrap;
            LocalizedTMPAutoSizer.Configure(text, size * 0.72f, size, TextOverflowModes.Truncate, TextWrappingModes.NoWrap);
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

        private static void CreateRule(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(rect.gameObject);
            img.color = RuleColor;
            img.raycastTarget = false;
        }
    }
}
