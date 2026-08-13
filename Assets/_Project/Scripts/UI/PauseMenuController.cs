using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Optimization;
using Hecton8.SaveSystem;
using Hecton8.Crafting;
using Hecton.Localization;
using Hecton.UI.MainMenu;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton8.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/Pause Menu Controller")]
    public sealed class PauseMenuController : MonoBehaviour, IUnscaledFastTickable, ILateFrameTickable, ISaveEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private const byte PauseMenuCommandPause = 1 << 0;
        private const byte PauseMenuCommandCancel = 1 << 1;
        internal static PauseMenuController ActiveRuntimeInstance { get; private set; }
        private const string PauseMenuRootName = "PauseMenu_Root";
        private const string DefaultMainMenuSceneName = "01_MAIN_MENU";
        private const uint DiegeticPauseHapticSourceHash = 0x504D3131u; // PM11
        private const float MaxPauseMenuPresentationDeltaSeconds = 0.1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            // Domain reload may be disabled (Editor Enter Play Mode Options).
            // Sticky open-count must not leak across play sessions - otherwise
            // IsAnyOpen stays true, HPM SampleGameplay zeros input without GetState (hop2 starve).
            ActiveRuntimeInstance = null;
            _openMenuCount = 0;
        }

        private enum PauseSection
        {
            Main = 0,
            Saves = 1,
            Help = 2,
            Settings = 3
        }

        private static readonly Color ShellBg = new Color(0.03f, 0.07f, 0.09f, 0.92f);
        private static readonly Color HeaderBg = new Color(0.07f, 0.16f, 0.18f, 0.86f);
        private static readonly Color PanelBg = new Color(0.05f, 0.11f, 0.13f, 0.82f);
        private static readonly Color Primary = new Color(0.46f, 0.98f, 0.94f, 0.96f);
        private static readonly Color Dim = new Color(0.78f, 0.96f, 0.93f, 0.84f);
        private static readonly Color DimLow = new Color(0.56f, 0.74f, 0.71f, 0.72f);
        private static readonly Color ButtonBg = new Color(0.08f, 0.16f, 0.18f, 0.84f);
        private static readonly Color ButtonHover = new Color(0.12f, 0.24f, 0.28f, 0.94f);
        private static readonly Color Rule = new Color(0.46f, 0.98f, 0.94f, 0.18f);
        private static readonly Action<AsyncOperation> _onMainMenuCleanupCompleted = HandleMainMenuCleanupCompleted;
        private const uint PauseMenuSignalSourceHash = 0x50415553u; // PAUS
        private static readonly int SettingsLanguageHintKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_LANGUAGE_HINT.AsSpan());
        private static readonly int SettingsCycleLanguageKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_CYCLE_LANGUAGE.AsSpan());
        private static readonly int ErrorSaveManagerUnavailableKeyHash = LocHash.Compute(LocalizationKeys.ERROR_SAVE_MANAGER_UNAVAILABLE.AsSpan());
        private static readonly int ErrorSaveCrashedMessageKeyHash = LocHash.Compute(LocalizationKeys.ERROR_SAVE_CRASHED_MESSAGE.AsSpan());
        private static readonly int ErrorSaveFailedMessageKeyHash = LocHash.Compute(LocalizationKeys.ERROR_SAVE_FAILED_MESSAGE.AsSpan());
        private static readonly int ErrorLoadFailedMessageKeyHash = LocHash.Compute(LocalizationKeys.ERROR_LOAD_FAILED_MESSAGE.AsSpan());
        private static readonly int WarningBackupUsedTitleKeyHash = LocHash.Compute(LocalizationKeys.WARNING_BACKUP_USED_TITLE.AsSpan());
        private static readonly int WarningBackupUsedMessageKeyHash = LocHash.Compute(LocalizationKeys.WARNING_BACKUP_USED_MESSAGE.AsSpan());
        private static readonly int WarningSaveRepairedTitleKeyHash = LocHash.Compute(LocalizationKeys.WARNING_SAVE_REPAIRED_TITLE.AsSpan());
        private static readonly int WarningSaveRepairedMessageKeyHash = LocHash.Compute(LocalizationKeys.WARNING_SAVE_REPAIRED_MESSAGE.AsSpan());
        private static readonly int SettingsLanguageOwnerUnavailableKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_LANGUAGE_OWNER_UNAVAILABLE.AsSpan());
        private static readonly int SettingsCurrentLanguageKeyHash = LocHash.Compute(LocalizationKeys.SETTINGS_CURRENT_LANGUAGE.AsSpan());

        [Header("References")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;
        [SerializeField] private RectTransform pauseMenuRoot;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = DefaultMainMenuSceneName;
        [SerializeField] private string[] saveSlots = { "slot_0", "slot_1", "slot_2" };
        [SerializeField] private bool pauseTimeScale = true;
        [SerializeField] private MenuVisualStyle visualStyle = MenuVisualStyle.PressureVesselNoir;
        [SerializeField] private MenuVisualConcept visualConcept = MenuVisualConcept.ModuleWindowOverlay;
        [SerializeField, Range(-1f, 1f)] private float visualStyleQualityOverride = -1f;
        [SerializeField, Range(-1f, 1f)] private float visualConceptQualityOverride = -1f;

        private static int _openMenuCount;
        private static bool _pendingMainMenuCleanup;
        private static bool _mainMenuCleanupHookRegistered;
        private static string _pendingMainMenuSceneName = string.Empty;

        private bool _registered;
        private bool _built;
        private bool _isOpen;
        private bool _exitToMainMenuInFlight;
        private bool _saveOperationInFlight;
        private bool _pauseRequested;
        private bool _cancelRequested;
        private bool _lateFrameRegistered;
        private byte _pendingMenuCommandMask;
        private bool _hasSaveStatusText;
        private PauseSection _activeSection;
        private float _cachedTimeDilationScalar = 1f;
        private uint _pauseSignalSequence;
        private uint _lastPlayerInputSignalSequence;
        private uint _lastConsumedFailureSnapshotSequence;
        private INativeInputManagerRuntime _inputManager;
        private IInputService _cachedInputService;
        private ITickDispatcher _cachedTickDispatcher;
        private bool _hotSwapListenerRegistered;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ISaveService _cachedSaveService;
        private ILocalizationLanguageControl _cachedLocalization;
        private EventSystem _cachedEventSystem;
        private string _pendingRetrySaveSlotName = string.Empty;
        private bool _pauseVisualStyleDirty;
        private bool _pauseVisualConceptDirty;
        private int _pendingMenuVisualStyleIndex = -1;
        private int _pendingMenuVisualConceptIndex = -1;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

        private RectTransform _root;
        private Canvas _diegeticCanvas;
        private RectTransform _diegeticCanvasRoot;
        private BoxCollider _diegeticPanelCollider;
        private DiegeticPanelController _diegeticPanelController;
        private DiegeticMenuRaycastReceiver _diegeticRaycastReceiver;
        private MenuCameraController _pauseMenuCameraController;
        private float _pauseMenuPresentationDeltaTime;
        private CanvasGroup _canvasGroup;
        private Image _background;
        private TextMeshProUGUI _headerTitle;
        private TextMeshProUGUI _headerSub;
        private TextMeshProUGUI _footerHint;
        private RectTransform _shell;
        private RectTransform _header;
        private RectTransform _content;
        private RectTransform _mainPanel;
        private RectTransform _savesPanel;
        private RectTransform _helpPanel;
        private RectTransform _settingsPanel;
        private CanvasGroup _mainPanelCanvasGroup;
        private CanvasGroup _savesPanelCanvasGroup;
        private CanvasGroup _helpPanelCanvasGroup;
        private CanvasGroup _settingsPanelCanvasGroup;
        private bool _pauseSectionInteractionGateActive;
        private bool _hasPendingPauseSelectionClear;
        private bool _hasPendingDefaultSelection;
        private PauseSection _pendingDefaultSelectionSection;
        private TextMeshProUGUI _saveStatus;
        private PauseControlsPanel _controlsPanel;
        private Button _mainResumeButton;
        private Button _savesFirstButton;
        private Button _savesBackButton;
        private Button[] _saveSlotButtons;
        private TextMeshProUGUI[] _saveSlotButtonLabels;
        private Button _helpBackButton;
        private Button _settingsBackButton;
        private Button _settingsLanguageButton;
        private Button _settingsMenuStyleButton;
        private Button _settingsMenuConceptButton;
        private TextMeshProUGUI _settingsLanguageStatus;
        private TextMeshProUGUI _settingsMenuStyleStatus;
        private TextMeshProUGUI _settingsMenuConceptStatus;
        private MenuVisualStyleApplier _visualStyleApplier;
        private MenuVisualConceptApplier _visualConceptApplier;
        private MenuVisualConceptDecorApplier _visualConceptDecorApplier;
        private SettingsManager _cachedSettings;
        private CharBufferPool.Lease _saveStatusBufferLease;
        // COLD ALLOC: char[128] - pause-menu save status fallback buffer when transient pool leases are exhausted - owner: PauseMenuController
        private readonly char[] _saveStatusFallbackBuffer = new char[128];
        // COLD ALLOC: char[96] - settings language status staging buffer - owner: PauseMenuController
        private readonly char[] _settingsLanguageBuffer = new char[96];
        // COLD ALLOC: char[128] - settings menu style status staging buffer - owner: PauseMenuController
        private readonly char[] _settingsMenuStyleBuffer = new char[128];
        // COLD ALLOC: char[128] - settings menu concept status staging buffer - owner: PauseMenuController
        private readonly char[] _settingsMenuConceptBuffer = new char[128];
        // COLD ALLOC: char[64] - save slot button label staging buffer - owner: PauseMenuController
        private readonly char[] _saveSlotLabelBuffer = new char[64];
        // COLD ALLOC: char[64] - pause-menu modal title staging buffer copied into ModalWindow title string - owner: PauseMenuController
        private readonly char[] _modalTitleBuffer = new char[64];
        // COLD ALLOC: char[192] - modal save-error staging buffer copied directly into TMP - owner: PauseMenuController
        private readonly char[] _modalMessageBuffer = new char[192];

        public bool IsOpen => _isOpen;
        public bool IsSettingsOpen => _isOpen && _activeSection == PauseSection.Settings;
        public static bool IsAnyOpen => _openMenuCount > 0;
        public MenuVisualStyle VisualStyle => visualStyle;
        public MenuVisualConcept VisualConcept => visualConcept;

        public void SetVisualStyle(MenuVisualStyle style)
        {
            if (visualStyle == style)
                return;

            visualStyle = style;
            _visualStyleApplier?.ForceNextApply();
            _visualConceptDecorApplier?.ForceNextApply();
        }

        public void SetVisualStyleQualityOverride(float qualityOverride)
        {
            visualStyleQualityOverride = math.clamp(qualityOverride, -1f, 1f);
            _visualStyleApplier?.ForceNextApply();
            _visualConceptDecorApplier?.ForceNextApply();
        }

        public void SetVisualConcept(MenuVisualConcept concept)
        {
            if (visualConcept == concept)
                return;

            visualConcept = concept;
            _visualConceptApplier?.ForceNextApply();
            _visualConceptDecorApplier?.ForceNextApply();
        }

        public void SetVisualConceptQualityOverride(float qualityOverride)
        {
            visualConceptQualityOverride = math.clamp(qualityOverride, -1f, 1f);
            _visualConceptApplier?.ForceNextApply();
            _visualConceptDecorApplier?.ForceNextApply();
        }

        // ----------------------------------------------------------
        // CACHED STRINGS (zero-GC)
        // ----------------------------------------------------------

        private void PublishPauseState(bool paused, float restoreScalar = 0f)
        {
            _pauseSignalSequence++;
            if (_pauseSignalSequence == 0u)
                _pauseSignalSequence = 1u;

            SimulationPauseSignal signal = new SimulationPauseSignal
            {
                SourceHash = PauseMenuSignalSourceHash,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                Sequence = _pauseSignalSequence,
                Paused = paused ? (byte)1 : (byte)0,
                Flags = 0,
                RestoreScalar = restoreScalar
            };
            SimulationSignalRoute.TryQueuePause(in signal);

            ITickDispatcher dispatcher = _cachedTickDispatcher;
            if (dispatcher != null)
            {
                dispatcher.RequestSimulationPause(paused, PauseMenuSignalSourceHash);
                if (!paused && restoreScalar > 0f)
                    dispatcher.RequestTimeDilation(restoreScalar, PauseMenuSignalSourceHash);
            }
        }

        private static readonly string _cachedWriting = "WRITING ";
        private static readonly string _cachedWritten = " WRITTEN.";
        private static readonly string _cachedLoading = "LOADING ";
        private static readonly string _cachedLoaded = " LOADED.";
        private static readonly string _cachedFailed = " FAILED. ";
        private static readonly string _cachedLoadFailed = " LOAD FAILED. ";
        private static readonly string _cachedFailedTerminal = " FAILED.";
        private static readonly string _cachedUnknownErrorStatus = "UNKNOWN ERROR";
        private static readonly string _cachedUnknownErrorModal = "Unknown error";
        private static readonly string _cachedSaveServiceUnavailable = "SAVE SERVICE UNAVAILABLE.";
        private static readonly string _cachedSaveInProgress = "SAVE ALREADY IN PROGRESS.";
        private static readonly string _cachedAwaitingSaveCommand = "Awaiting save command.";
        private static readonly string _cachedPreparingSceneTransition = "Preparing scene transition...";
        private static readonly string _cachedWritePrefix = "WRITE ";

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = SystemDispatcher.Register((IUnscaledFastTickable)this, PriorityLayer.UI);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            if (_registered)
            {
                SystemDispatcher.Unregister((IUnscaledFastTickable)this, PriorityLayer.UI);
                _registered = false;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
            _cachedSaveService = Hecton8.Core.GlobalRegistry.Save;
            _cachedLocalization = Hecton8.Core.GlobalRegistry.LocalizationLanguageControl;
            _cachedInputService = Hecton8.Core.GlobalRegistry.Input;
            _cachedTickDispatcher = Hecton8.Core.GlobalRegistry.TickDispatcher;
            CacheSettingsManagerCold(Hecton8.Core.GlobalRegistry.Settings);
            ApplyPersistedVisualStyleCold();
            ApplyPersistedVisualConceptCold();
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

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void Awake()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            CacheRegistryServicesCold();
            NormalizeSaveSlots();
            AutoResolve();
            EnsureBuilt();
            EnsureEventSystem();
            ApplyClosedState(restorePlayerInput: false);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            NormalizeSaveSlots();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryAcquireSaveStatusBuffer();
            TryRegister();
            EnsureEventSystem();
            BindInputActions();

            LocalizationEvents.RegisterLanguageListener(this);
            SaveEvents.Register(this);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            UnbindInputActions();

            LocalizationEvents.UnregisterLanguageListener(this);
            SaveEvents.Unregister(this);
            TryUnregisterHotSwapListener();

            TryUnregister();
            CommitPauseVisualSelectionIfNeeded();
            ReleaseSaveStatusBuffer();
            CacheSettingsManagerCold(null);

            if (_exitToMainMenuInFlight)
            {
                HandleMainMenuExitTransitionDisabled();
                return;
            }

            bool restorePlayerInput = _isOpen && ShouldRestorePlayerInputOnDisable();
            ApplyClosedState(restorePlayerInput: restorePlayerInput);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregister();
            TryUnregisterHotSwapListener();
            CommitPauseVisualSelectionIfNeeded();
            ReleaseSaveStatusBuffer();
            CacheSettingsManagerCold(null);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (playerPDA == null && _cachedPlayerContext != null)
                        playerPDA = _cachedPlayerContext.PlayerPDA;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    _cachedSaveService = currentService as ISaveService;
                    if (_activeSection == PauseSection.Saves)
                        RefreshSaveSectionState();
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedLocalization = currentService as ILocalizationLanguageControl;
                    if (_built)
                    {
                        RefreshSaveSlotButtonLabels();
                        RefreshLanguageSettingsStatus();
                        RefreshMenuVisualStyleStatus();
                        RefreshMenuVisualConceptStatus();
                        if (_activeSection == PauseSection.Saves)
                            RefreshSaveSectionState();
                    }
                    break;
                case GlobalRegistryServiceSlot.SettingsRuntime:
                    CacheSettingsManagerCold(currentService as SettingsManager);
                    ApplyPersistedVisualStyleCold();
                    ApplyPersistedVisualConceptCold();
                    RefreshMenuVisualStyleStatus();
                    RefreshMenuVisualConceptStatus();
                    break;
                case GlobalRegistryServiceSlot.Input:
                    _cachedInputService = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    _cachedTickDispatcher = currentService as ITickDispatcher;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    BindInputActions(currentService as INativeInputManagerRuntime);
                    break;
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif

            if (labelFont == null)
                labelFont = TMP_Settings.defaultFontAsset;
            if (numericFont == null)
                numericFont = labelFont;

            NormalizeSaveSlots();
        }

        private void NormalizeSaveSlots()
        {
            if (saveSlots == null)
                return;

            int count = math.min(saveSlots.Length, SaveEvents.ManualSlotCount);
            for (int i = 0; i < count; i++)
            {
                string canonicalSlotName = SaveEvents.ResolveManualSlotName(i);
                if (!string.Equals(saveSlots[i], canonicalSlotName, StringComparison.Ordinal))
                    saveSlots[i] = canonicalSlotName;
            }
        }

        private void AdvancePauseInputState(float deltaTime)
        {
            if (!Application.isPlaying)
                return;

            if (_exitToMainMenuInFlight)
            {
                return;
            }

            if (_saveOperationInFlight)
                return;

            bool controlsPanelConsumedCancel = _controlsPanel != null && _controlsPanel.ConsumePlayerInputSignals();
            ConsumePlayerInputSignals(controlsPanelConsumedCancel);

            if (_pauseRequested)
            {
                _pauseRequested = false;
                QueuePauseMenuCommand(PauseMenuCommandPause);
            }

            if (_cancelRequested)
            {
                _cancelRequested = false;
                QueuePauseMenuCommand(PauseMenuCommandCancel);
            }
        }

        public void UnscaledFastTick(float unscaledDeltaTime)
        {
            if (math.isfinite(unscaledDeltaTime) && unscaledDeltaTime > 0f)
            {
                _pauseMenuPresentationDeltaTime = math.min(
                    MaxPauseMenuPresentationDeltaSeconds,
                    _pauseMenuPresentationDeltaTime + unscaledDeltaTime);
            }

            AdvancePauseInputState(unscaledDeltaTime);
            ProcessPendingPauseMenuCommands();
        }

        public void LateFrameTick()
        {
            float presentationDeltaTime = _pauseMenuPresentationDeltaTime;
            _pauseMenuPresentationDeltaTime = 0f;
            if (presentationDeltaTime <= 0f)
                presentationDeltaTime = ResolveCurrentUnscaledFrameDeltaTime();

            if (presentationDeltaTime > 0f)
                _pauseMenuCameraController?.Advance(presentationDeltaTime);

            RefreshPauseSectionInteractionGate();
            FlushPendingPauseSelectionClear();
            FlushPendingDefaultSelection();
            SyncVisualStyleLateFrame();
            SyncVisualConceptLateFrame();
            _diegeticRaycastReceiver?.FlushPendingSelection();
        }

        private static float ResolveCurrentUnscaledFrameDeltaTime()
        {
            float deltaTime = SystemDispatcher.CurrentFrameUnscaledDeltaTime;
            return math.isfinite(deltaTime) ? math.min(MaxPauseMenuPresentationDeltaSeconds, math.max(0f, deltaTime)) : 0f;
        }

        private static float ResolveCurrentUnscaledTimeSeconds()
        {
            float currentTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            return math.isfinite(currentTime) && currentTime >= 0f ? currentTime : 0f;
        }

        private void ProcessPendingPauseMenuCommands()
        {
            byte pendingCommands = _pendingMenuCommandMask;
            if (pendingCommands != 0)
            {
                _pendingMenuCommandMask = 0;
                if ((pendingCommands & PauseMenuCommandPause) != 0)
                    HandlePauseRequested();
                if ((pendingCommands & PauseMenuCommandCancel) != 0)
                    HandleCancelRequested();
            }
        }

        private void QueuePauseMenuCommand(byte command)
        {
            _pendingMenuCommandMask |= command;
        }

        public void OnSaveEvent(in SaveEventPayload payload)
        {
            switch (payload.Type)
            {
                case SaveEventType.SaveStarted:
                    HandleSaveStarted(SaveEvents.ResolveSlotName(payload.SlotHash));
                    return;

                case SaveEventType.SaveCompleted:
                    HandleSaveCompleted(SaveEvents.ResolveSlotName(payload.SlotHash));
                    return;

                case SaveEventType.SaveFailed:
                    HandleSaveFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));
                    return;

                case SaveEventType.LoadStarted:
                    HandleLoadStarted(SaveEvents.ResolveSlotName(payload.SlotHash));
                    return;

                case SaveEventType.LoadCompleted:
                    HandleLoadCompleted(SaveEvents.ResolveSlotName(payload.SlotHash));
                    return;

                case SaveEventType.LoadFailed:
                    HandleLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));
                    return;
            }
        }

        public void Open()
        {
            if (_isOpen)
                return;

            // TASK 33: Close PDA before opening pause menu if PDA is open
            if (PlayerPDA.IsOpen && playerPDA != null)
            {
                playerPDA.ForceClose();
            }

            // TASK 33: Close Fabricator before opening pause menu if Fabricator is open
            if (HectonFabricatorUI.IsMenuOpen)
            {
                // Trigger the deferred fabricator-closed payload so HectonFabricatorUI closes through the event lane.
                CraftingEvents.TryRaiseFabricatorClosed();
            }

            if (!_built)
                return;

            _pauseRequested = false;
            _cancelRequested = false;
            _isOpen = true;
            RegisterOpenMenu();
            _activeSection = PauseSection.Main;

            if (pauseTimeScale)
            {
                ITickDispatcher dispatcher = _cachedTickDispatcher;
                _cachedTimeDilationScalar = dispatcher != null
                    ? dispatcher.TimeDilationScalar
                    : 1f;
                PublishPauseState(true);
            }

            // TASK 33: Ensure correct input mode restoration
            _cachedInputService?.SwitchToUIInput();
            SystemDispatcher.RequestPauseDepthOfField(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Audio feedback for pause menu open
            UIAudioFeedback.PlayPanelOpen();

            ShowSection(PauseSection.Main);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }

        public void Close()
        {
            if (!_isOpen)
                return;

            CommitPauseVisualSelectionIfNeeded();

            // Audio feedback for pause menu close
            UIAudioFeedback.PlayPanelClose();

            ApplyClosedState(restorePlayerInput: true);
        }

        internal void RefreshSettingsPanel()
        {
            if (_controlsPanel != null)
                _controlsPanel.RefreshAllBindingsNow();

            RefreshLanguageSettingsStatus();
            RefreshMenuVisualStyleStatus();
            RefreshMenuVisualConceptStatus();
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            OnLanguageChanged((GameLanguage)payload.Language);

        }


        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshLocalizedTexts();
        }

        private void RefreshLocalizedTexts()
        {
            // Refresh all visible text in pause menu
            // Section titles, button labels, help text, etc.
            // This ensures language changes are reflected immediately
            if (_isOpen)
            {
                // Rebuild current section to refresh localized text
                ShowSection(_activeSection);
            }
        }

        private void ApplyClosedState(bool restorePlayerInput)
        {
            bool wasOpen = _isOpen;
            _isOpen = false;
            _pauseRequested = false;
            _cancelRequested = false;
            if (wasOpen)
                UnregisterOpenMenu();
            SystemDispatcher.RequestPauseDepthOfField(false);
            _activeSection = PauseSection.Main;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            if (pauseTimeScale)
            {
                float restoreScalar = math.max(0.0001f, _cachedTimeDilationScalar);
                PublishPauseState(false, restoreScalar);
            }

            IInputService inputService = _cachedInputService;
            if (restorePlayerInput && inputService != null && inputService.IsInitialized)
                inputService.SwitchToPlayerInput();

            ClearPauseSelection();
            ApplyCursorState(restorePlayerInput);

        }

        private static void ApplyCursorState(bool restorePlayerInput)
        {
            if (restorePlayerInput)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
        }

        private static void RegisterOpenMenu()
        {
            if (_openMenuCount < int.MaxValue)
                _openMenuCount++;
        }

        private static void UnregisterOpenMenu()
        {
            if (_openMenuCount > 0)
                _openMenuCount--;
        }

        private bool ShouldRestorePlayerInputOnDisable()
        {
            if (!Application.isPlaying)
                return false;

            IInputService inputService = _cachedInputService;
            return inputService != null && inputService.IsInitialized;
        }

        private void AutoResolve()
        {
            if (playerPDA == null)
            {
                if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) &&
                    playerTransform != null)
                {
                    IPlayerRuntimeContext playerContext = _cachedPlayerContext;
                    if (playerContext != null && playerContext.PlayerPDA != null)
                    {
                        playerPDA = playerContext.PlayerPDA;
                    }
                    else
                    {
                        playerTransform.TryGetComponent(out playerPDA);
                    }
                }
            }
            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            if (numericFont == null)
                numericFont = labelFont;
            else if (IsNumericOnlyFont(labelFont) && !IsNumericOnlyFont(numericFont))
                labelFont = numericFont;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform self = transform as RectTransform;
            if (self == null)
                return;

            _root = ResolveOrCreateMenuRoot(self);
            if (_root == null)
                return;

            Stretch(_root, 0f, 0f, 0f, 0f);

            if (!TryGetComponent(out Canvas canvas))
                canvas = gameObject.AddComponent<Canvas>(); // COLD ALLOC: pause menu private world-space canvas.
            ConfigureDiegeticPauseRuntimeCold(canvas);

            if (!_root.TryGetComponent(out _canvasGroup))
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();

            if (!_root.TryGetComponent(out _background))
                _background = _root.gameObject.AddComponent<Image>();
            _background.color = ShellBg;
            _background.raycastTarget = true;

            NeutralizeHostCanvasArtifacts(self);

            ClearChildren(_root);

            RectTransform shell = CreateRect(_root, "PauseShell");
            shell.anchorMin = new Vector2(0.5f, 0.5f);
            shell.anchorMax = new Vector2(0.5f, 0.5f);
            shell.pivot = new Vector2(0.5f, 0.5f);
            shell.anchoredPosition = Vector2.zero;
            shell.sizeDelta = new Vector2(1240f, 720f);
            _shell = shell;
            Image shellBg = EnsureImage(shell.gameObject);
            shellBg.color = new Color(0.02f, 0.05f, 0.07f, 0.96f);
            shellBg.raycastTarget = false;

            RectTransform header = CreateRect(shell, "Header");
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -12f), new Vector2(-14f, 58f));
            _header = header;
            Image headerBg = EnsureImage(header.gameObject);
            headerBg.color = HeaderBg;
            headerBg.raycastTarget = false;

            _headerTitle = CreateText(header, "HeaderTitle", labelFont, 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(_headerTitle.rectTransform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(18f, 0f), new Vector2(-8f, 0f));
            _headerTitle.color = Primary;
            TmpTextNoAlloc.Set(_headerTitle, "MISSION PAUSE");

            _headerSub = CreateText(header, "HeaderSub", labelFont, 11f, FontStyles.Normal, TextAlignmentOptions.Right);
            Anchor(_headerSub.rectTransform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-18f, 0f));
            _headerSub.color = DimLow;

            CreateRule(shell, new Vector2(0.04f, 1f), new Vector2(0.96f, 1f), -76f);
            CreateRule(shell, new Vector2(0.04f, 0f), new Vector2(0.96f, 0f), 58f);

            RectTransform content = CreateRect(shell, "Content");
            Stretch(content, 22f, 22f, 92f, 74f);
            _content = content;

            _mainPanel = CreatePanel(content, "MainPanel");
            _savesPanel = CreatePanel(content, "SavesPanel");
            _helpPanel = CreatePanel(content, "HelpPanel");
            _settingsPanel = CreatePanel(content, "SettingsPanel");
            _mainPanelCanvasGroup = EnsurePanelCanvasGroup(_mainPanel);
            _savesPanelCanvasGroup = EnsurePanelCanvasGroup(_savesPanel);
            _helpPanelCanvasGroup = EnsurePanelCanvasGroup(_helpPanel);
            _settingsPanelCanvasGroup = EnsurePanelCanvasGroup(_settingsPanel);

            BuildMainPanel(_mainPanel);
            BuildSavesPanel(_savesPanel);
            BuildHelpPanel(_helpPanel);
            BuildSettingsPanel(_settingsPanel);

            RectTransform footer = CreateRect(shell, "Footer");
            Anchor(footer, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 12f), new Vector2(-14f, 34f));
            Image footerBg = EnsureImage(footer.gameObject);
            footerBg.color = new Color(0.06f, 0.11f, 0.13f, 0.86f);
            footerBg.raycastTarget = false;

            _footerHint = CreateText(footer, "FooterHint", labelFont, 10.5f, FontStyles.Italic, TextAlignmentOptions.Center);
            Stretch(_footerHint.rectTransform, 12f, 12f, 0f, 0f);
            _footerHint.color = DimLow;
            TmpTextNoAlloc.Set(_footerHint, "ESC = back / resume  |  SETTINGS hosts controls and rebinds");

            _diegeticRaycastReceiver?.RebuildButtonCache();
            RebuildVisualStyleCacheCold();
            RebuildVisualConceptCacheCold();
            _built = true;
        }

        private void ConfigureDiegeticPauseRuntimeCold(Canvas canvas)
        {
            _diegeticCanvas = canvas;
            Camera camera = null;
            if (_cachedPlayerContext != null)
                camera = _cachedPlayerContext.PlayerCamera;
            camera = DiegeticMenuCanvasUtility.ResolveCamera(camera);

            if (!DiegeticMenuCanvasUtility.ApplyWorldSpaceCanvas(
                    _diegeticCanvas,
                    camera,
                    out _diegeticCanvasRoot,
                    out _diegeticPanelCollider))
            {
                return;
            }

            if (_diegeticPanelController == null && !TryGetComponent(out _diegeticPanelController))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                _diegeticPanelController = gameObject.AddComponent<DiegeticPanelController>(); // COLD ALLOC: pause-menu diegetic panel projection owner.
            }

            if (_diegeticRaycastReceiver == null && !TryGetComponent(out _diegeticRaycastReceiver))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                _diegeticRaycastReceiver = gameObject.AddComponent<DiegeticMenuRaycastReceiver>(); // COLD ALLOC: pause-menu fixed button receiver.
            }

            if (_diegeticRaycastReceiver != null)
                _diegeticRaycastReceiver.Configure(_diegeticCanvasRoot, _cachedEventSystem ?? EventSystem.current, DiegeticPauseHapticSourceHash);

            if (_diegeticPanelController != null)
            {
                _diegeticPanelController.OverrideRenderTexturePresentation(false);
                _diegeticPanelController.OverrideInteractionMode(DiegeticPanelController.PanelInteractionMode.RaycastOnly);
                _diegeticPanelController.OverrideInteractionCamera(camera);
                _diegeticPanelController.OverrideReferenceResolution(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);
                _diegeticPanelController.OverrideMaxInteractionDistance(2f);
                _diegeticPanelController.OverridePanelInteractable(_diegeticRaycastReceiver);
            }

            if (camera == null)
                return;

            if (_pauseMenuCameraController == null)
                camera.TryGetComponent(out _pauseMenuCameraController);

            if (_pauseMenuCameraController != null)
                _pauseMenuCameraController.Configure(camera);
        }

        private void RebuildVisualStyleCacheCold()
        {
            if (_visualStyleApplier == null)
                _visualStyleApplier = new MenuVisualStyleApplier(); // COLD ALLOC: pause-menu visual style reference cache owner.

            _visualStyleApplier.RebuildCache(_root);
        }

        private void RebuildVisualConceptCacheCold()
        {
            if (_visualConceptApplier == null)
                _visualConceptApplier = new MenuVisualConceptApplier(); // COLD ALLOC: pause-menu visual concept transform cache owner.
            if (_visualConceptDecorApplier == null)
                _visualConceptDecorApplier = new MenuVisualConceptDecorApplier(); // COLD ALLOC: pause-menu concept decor cache owner.

            _visualConceptApplier.Clear();
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.Shell, _shell);
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.Header, _header);
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.Content, _content);
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.MainPanel, _mainPanel);
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.SavesPanel, _savesPanel);
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.HelpPanel, _helpPanel);
            _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.SettingsPanel, _settingsPanel);
            _visualConceptDecorApplier.Rebuild(_shell != null ? _shell : _root);
        }

        private void SyncVisualStyleLateFrame()
        {
            if (_visualStyleApplier == null)
                return;

            float now = ResolveCurrentUnscaledTimeSeconds();
            _visualStyleApplier.ApplyIfNeeded(visualStyle, ResolveMenuVisualQualityWeight(), now);
        }

        private void SyncVisualConceptLateFrame()
        {
            if (_visualConceptApplier == null)
                return;

            float now = ResolveCurrentUnscaledTimeSeconds();
            float quality = ResolveMenuVisualConceptQualityWeight();
            _visualConceptApplier.ApplyIfNeeded(visualConcept, quality, now);
            _visualConceptDecorApplier?.ApplyIfNeeded(visualConcept, visualStyle, quality, now);
        }

        private float ResolveMenuVisualQualityWeight()
        {
            if (visualStyleQualityOverride >= 0f)
                return math.saturate(visualStyleQualityOverride);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private float ResolveMenuVisualConceptQualityWeight()
        {
            if (visualConceptQualityOverride >= 0f)
                return math.saturate(visualConceptQualityOverride);

            return ResolveMenuVisualQualityWeight();
        }

        private RectTransform ResolveOrCreateMenuRoot(RectTransform self)
        {
            if (pauseMenuRoot != null)
                return pauseMenuRoot;

            if (self.name == PauseMenuRootName)
                return self;

            Transform existing = self.Find(PauseMenuRootName);
            if (existing is RectTransform existingRect)
                return existingRect;

            GameObject go = new GameObject(PauseMenuRootName, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.TryGetComponent(out RectTransform root);
            root.SetParent(self, false);
            root.localScale = Vector3.one;
            return root;
        }

        private void NeutralizeHostCanvasArtifacts(RectTransform self)
        {
            if (ReferenceEquals(_root, self))
                return;

            if (self.TryGetComponent(out CanvasGroup hostCanvasGroup))
            {
                hostCanvasGroup.alpha = 1f;
                hostCanvasGroup.interactable = false;
                hostCanvasGroup.blocksRaycasts = false;
            }

            if (self.TryGetComponent(out Image hostImage))
            {
                hostImage.enabled = false;
                hostImage.raycastTarget = false;
            }
        }

        private void BuildMainPanel(RectTransform panel)
        {
            TextMeshProUGUI title = CreateSectionTitle(panel, "MISSION CONTROL");
            TmpTextNoAlloc.Set(title, "MISSION CONTROL");

            RectTransform resumeButton = CreateButton(panel, "ResumeButton", "RESUME EXPEDITION",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -88f), new Vector2(420f, 42f), Close);
            resumeButton.TryGetComponent(out _mainResumeButton);
            TmpTextNoAlloc.Set(GetText(resumeButton, "Label"), "RESUME EXPEDITION");

            CreateButton(panel, "SaveStationButton", "SAVE STATION",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -146f), new Vector2(420f, 42f), ShowSavesSection);

            CreateButton(panel, "FieldGuideButton", "FIELD GUIDE",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -204f), new Vector2(420f, 42f), ShowHelpSection);

            CreateButton(panel, "SettingsButton", "SETTINGS",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -262f), new Vector2(420f, 42f), ShowSettingsSection);

            CreateButton(panel, "ExitMainMenuButton", "EXIT TO MAIN MENU",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -320f), new Vector2(420f, 42f), ExitToMainMenu);

            CreateButton(panel, "QuitApplicationButton", "QUIT APPLICATION",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -378f), new Vector2(420f, 42f), QuitApplication);
        }

        private void BuildSavesPanel(RectTransform panel)
        {
            TmpTextNoAlloc.Set(CreateSectionTitle(panel, "SAVE STATION"), "SAVE STATION");
            CreateSectionSub(panel, "Manual save points. Use these before risky dives or major construction changes.")
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            _saveSlotButtons = new Button[SaveEvents.ManualSlotCount];
            _saveSlotButtonLabels = new TextMeshProUGUI[SaveEvents.ManualSlotCount];
            for (int i = 0; i < SaveEvents.ManualSlotCount; i++)
            {
                RectTransform btn = CreateButton(panel, ResolveSaveSlotButtonName(i), "WRITE SLOT",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -108f - i * 56f), new Vector2(420f, 40f),
                    ResolveSaveSlotAction(i));
                btn.TryGetComponent(out Button slotButton);
                _saveSlotButtons[i] = slotButton;

                if (i == 0)
                    _savesFirstButton = slotButton;

                TextMeshProUGUI label = GetText(btn, "Label");
                _saveSlotButtonLabels[i] = label;
                if (label != null)
                    label.alignment = TextAlignmentOptions.Center;
            }

            RefreshSaveSlotButtonLabels();

            _saveStatus = CreateText(panel, "SaveStatus", numericFont, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_saveStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 66f), new Vector2(-22f, 22f));
            _saveStatus.color = Dim;
            ApplySaveStatusLiteral(_cachedAwaitingSaveCommand);

            _savesBackButton = CreateBackButton(panel, ShowMainSection);
            RefreshSaveSectionState();
        }

        private void BuildHelpPanel(RectTransform panel)
        {
            TmpTextNoAlloc.Set(CreateSectionTitle(panel, "FIELD GUIDE"), "FIELD GUIDE");

            TextMeshProUGUI body = CreateText(panel, "HelpBody", numericFont, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 68f), new Vector2(-28f, -74f));
            body.color = Dim;
            body.textWrappingMode = TextWrappingModes.Normal;
            TmpTextNoAlloc.Set(body, "CORE INPUTS\nTAB  // PDA shell\nI    // inventory direct open\n1-4  // quick slot arm/swap\nLMB/RMB // primary / secondary tool action\n\nMISSION RHYTHM\n1. Scan and classify unknowns.\n2. Repair and stabilize critical infrastructure.\n3. Keep loadout aligned with cargo before committing to depth.\n4. Save before hazardous traversal, fauna contact, or base edits.");

            _helpBackButton = CreateBackButton(panel, ShowMainSection);
        }

        private void BuildSettingsPanel(RectTransform panel)
        {
            TmpTextNoAlloc.Set(CreateSectionTitle(panel, "SETTINGS"), "SETTINGS");
            CreateSectionSub(panel, ResolveLocalizedSpan(SettingsLanguageHintKeyHash,
                "Controls were moved out of the PDA. Rebind them here. Language cycling is also available."))
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            RectTransform languageButton = CreateButton(panel, "LanguageButton",
                ResolveLocalizedSpan(SettingsCycleLanguageKeyHash, "CYCLE LANGUAGE"),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(420f, 38f), CycleLanguage);
            languageButton.TryGetComponent(out _settingsLanguageButton);

            _settingsLanguageStatus = CreateText(panel, "LanguageStatus", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_settingsLanguageStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -146f), new Vector2(-26f, -118f));
            _settingsLanguageStatus.color = Dim;

            RectTransform menuStyleButton = CreateButton(panel, "MenuStyleButton",
                "CYCLE MENU STYLE".AsSpan(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -176f), new Vector2(420f, 38f), CycleMenuVisualStyle);
            menuStyleButton.TryGetComponent(out _settingsMenuStyleButton);

            _settingsMenuStyleStatus = CreateText(panel, "MenuStyleStatus", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_settingsMenuStyleStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -224f), new Vector2(-26f, -196f));
            _settingsMenuStyleStatus.color = Dim;

            RectTransform menuConceptButton = CreateButton(panel, "MenuConceptButton",
                "CYCLE MENU CONCEPT".AsSpan(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -254f), new Vector2(420f, 38f), CycleMenuVisualConcept);
            menuConceptButton.TryGetComponent(out _settingsMenuConceptButton);

            _settingsMenuConceptStatus = CreateText(panel, "MenuConceptStatus", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_settingsMenuConceptStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -302f), new Vector2(-26f, -274f));
            _settingsMenuConceptStatus.color = Dim;

            RectTransform controlsRoot = CreateRect(panel, "ControlsPanel");
            Stretch(controlsRoot, 22f, 22f, 316f, 80f);
            PauseControlsPanel controls = controlsRoot.gameObject.AddComponent<PauseControlsPanel>();
            controls.Configure(this, labelFont, labelFont);
            _controlsPanel = controls;

            _settingsBackButton = CreateBackButton(panel, ShowMainSection);
            RefreshLanguageSettingsStatus();
            RefreshMenuVisualStyleStatus();
            RefreshMenuVisualConceptStatus();
        }

        private void ShowMainSection()
        {
            ShowSection(PauseSection.Main);
        }

        private void ShowSavesSection()
        {
            ShowSection(PauseSection.Saves);
        }

        private void ShowHelpSection()
        {
            ShowSection(PauseSection.Help);
        }

        private void ShowSettingsSection()
        {
            ShowSection(PauseSection.Settings);
        }

        private Action ResolveSaveSlotAction(int slotIndex)
        {
            return slotIndex switch
            {
                0 => SaveSlot0,
                1 => SaveSlot1,
                _ => SaveSlot2
            };
        }

        private static string ResolveSaveSlotButtonName(int slotIndex)
        {
            return slotIndex switch
            {
                0 => "SaveSlot0Button",
                1 => "SaveSlot1Button",
                _ => "SaveSlot2Button"
            };
        }

        private void SaveSlot0()
        {
            SaveSlot(ResolveConfiguredSaveSlotName(0));
        }

        private void SaveSlot1()
        {
            SaveSlot(ResolveConfiguredSaveSlotName(1));
        }

        private void SaveSlot2()
        {
            SaveSlot(ResolveConfiguredSaveSlotName(2));
        }

        private void ShowSection(PauseSection section)
        {
            PauseSection previousSection = _activeSection;
            if (previousSection == PauseSection.Settings && section != PauseSection.Settings)
                CommitPauseVisualSelectionIfNeeded();

            _activeSection = section;
            bool gateInteraction = BeginPauseCameraRoute(section);

            // Audio feedback for section transitions (not on initial open)
            if (previousSection != section && _isOpen)
            {
                UIAudioFeedback.PlayPanelOpen();
            }

            SetPanelVisible(_mainPanelCanvasGroup, section == PauseSection.Main);
            SetPanelVisible(_savesPanelCanvasGroup, section == PauseSection.Saves);
            SetPanelVisible(_helpPanelCanvasGroup, section == PauseSection.Help);
            SetPanelVisible(_settingsPanelCanvasGroup, section == PauseSection.Settings);
            if (gateInteraction)
                ApplyPauseSectionInteractionGate(locked: true);

            if (_headerSub == null)
                return;

            switch (section)
            {
                case PauseSection.Main:
                    TmpTextNoAlloc.Set(_headerSub, "resume, save, inspect field guidance, or move into settings");
                    break;
                case PauseSection.Saves:
                    TmpTextNoAlloc.Set(_headerSub, "manual persistence via SaveManager");
                    RefreshSaveSlotButtonLabels();
                    RefreshSaveSectionState();
                    break;
                case PauseSection.Help:
                    TmpTextNoAlloc.Set(_headerSub, "compact operational reference for current tool and inventory loop");
                    break;
                case PauseSection.Settings:
                    TmpTextNoAlloc.Set(_headerSub, "controls and interaction tuning are managed here now");
                    RefreshSettingsPanel();
                    break;
            }

            QueueDefaultSelectionForSection(section, gateInteraction);
        }

        private bool BeginPauseCameraRoute(PauseSection section)
        {
            if (_pauseMenuCameraController == null)
                return false;

            MenuCameraController.MenuCameraRoute route = MenuCameraController.MenuCameraRoute.Main;
            if (section == PauseSection.Saves || section == PauseSection.Help)
                route = MenuCameraController.MenuCameraRoute.Saves;
            else if (section == PauseSection.Settings)
                route = MenuCameraController.MenuCameraRoute.Settings;

            _pauseMenuCameraController.BeginRoute(route, 0.48f);
            return true;
        }

        private void RefreshPauseSectionInteractionGate()
        {
            if (!_pauseSectionInteractionGateActive)
                return;

            if (_pauseMenuCameraController != null && _pauseMenuCameraController.IsActive)
                return;

            ApplyPauseSectionInteractionGate(locked: false);
        }

        private void ApplyPauseSectionInteractionGate(bool locked)
        {
            CanvasGroup group = ResolveActiveSectionGroup();
            if (group == null)
            {
                _pauseSectionInteractionGateActive = false;
                return;
            }

            _pauseSectionInteractionGateActive = locked;
            group.interactable = !locked && group.alpha > 0.01f;
            group.blocksRaycasts = !locked && group.alpha > 0.01f;
        }

        private void QueueDefaultSelectionForSection(PauseSection section, bool gateInteraction)
        {
            if (gateInteraction && _pauseMenuCameraController != null && _pauseMenuCameraController.IsActive)
            {
                _pendingDefaultSelectionSection = section;
                _hasPendingDefaultSelection = true;
                return;
            }

            _pendingDefaultSelectionSection = section;
            _hasPendingDefaultSelection = true;
        }

        private void FlushPendingDefaultSelection()
        {
            if (!_hasPendingDefaultSelection)
                return;

            if (_pauseSectionInteractionGateActive)
                return;

            PauseSection section = _pendingDefaultSelectionSection;
            _hasPendingDefaultSelection = false;
            if (section != _activeSection)
                return;

            SelectDefaultButtonForSection(section);
        }

        private void FlushPendingPauseSelectionClear()
        {
            if (!_hasPendingPauseSelectionClear)
                return;

            _hasPendingPauseSelectionClear = false;

            EventSystem eventSystem = _cachedEventSystem;
            if (eventSystem == null)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null || _root == null)
                return;

            if (selected.transform.IsChildOf(_root))
                eventSystem.SetSelectedGameObject(null);
        }

        private CanvasGroup ResolveActiveSectionGroup()
        {
            switch (_activeSection)
            {
                case PauseSection.Saves:
                    return _savesPanelCanvasGroup;
                case PauseSection.Help:
                    return _helpPanelCanvasGroup;
                case PauseSection.Settings:
                    return _settingsPanelCanvasGroup;
                default:
                    return _mainPanelCanvasGroup;
            }
        }

        /// <summary>
        /// Initiates save operation for the specified slot.
        /// Uses an Awaitable state machine so the UI owner never relies on Task or async void.
        /// </summary>
        private void SaveSlot(string slotName)
        {
            if (_saveOperationInFlight)
            {
                if (_saveStatus != null)
                    ApplySaveStatusLiteral(_cachedSaveInProgress);
                return;
            }

            if (!SaveEvents.IsKnownManualSlotName(slotName))
            {
                const string reason = "Invalid save slot.";
                _pendingRetrySaveSlotName = string.Empty;
                if (_saveStatus != null)
                    ApplySaveFailedStatusText(slotName, reason);

                int messageLength = BuildSaveModalMessage(
                    ErrorSaveFailedMessageKeyHash,
                    "Failed to save.",
                    slotName,
                    reason,
                    true,
                    false);

                ModalWindow.ShowWithCustomLabels(
                    "Save Error",
                    _modalMessageBuffer,
                    messageLength,
                    null,
                    null,
                    "OK",
                    null);
                return;
            }

            _ = SaveSlotAsync(slotName);
        }

        /// <summary>
        /// Async save operation with proper exception handling and Awaitable-based lifetime safety.
        /// </summary>
        private async Awaitable SaveSlotAsync(string slotName)
        {
            string upperSlotName = ResolveSlotDisplayName(slotName);

            ISaveService saveService = _cachedSaveService;
            if (!IsSaveServiceUsable(saveService))
            {
                if (_saveStatus != null)
                    ApplySaveStatusLiteral(_cachedSaveServiceUnavailable);

                int messageLength = CopyLocalizedSpanToModalBuffer(
                    ErrorSaveManagerUnavailableKeyHash,
                    "Save system is unavailable. Cannot save game.");

                ModalWindow.ShowWithCustomLabels(
                    "Save Error",
                    _modalMessageBuffer,
                    messageLength,
                    null,
                    null,
                    "OK",
                    null);
                return;
            }

            try
            {
                if (saveService.IsBusy)
                {
                    if (_saveStatus != null)
                        ApplySaveStatusLiteral(_cachedSaveInProgress);
                    return;
                }

                if (_saveStatus != null)
                    ApplySaveStatusText(_cachedWriting, upperSlotName, "...");

                await saveService.SaveGameAsync(slotName);
            }
            catch (Exception ex)
            {
                LogSaveSlotFailed(slotName, ex);
                if (_saveStatus != null)
                    ApplySaveStatusText(string.Empty, upperSlotName, _cachedFailedTerminal);

                int messageLength = BuildSaveModalMessage(
                    ErrorSaveCrashedMessageKeyHash,
                    "Save operation crashed.",
                    slotName,
                    default,
                    false);

                ModalWindow.ShowWithCustomLabels(
                    "Save Error",
                    _modalMessageBuffer,
                    messageLength,
                    CacheRetrySaveSlot(slotName),
                    null, // Cancel just closes modal
                    "Retry",
                    "Cancel");
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSaveSlotFailed(string slotName, Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[PauseMenuController] Save failed.");
#endif
        }

        private void TryAcquireSaveStatusBuffer()
        {
            if (_saveStatusBufferLease.IsValid)
                return;

            CharBufferPool.TryAcquire(out _saveStatusBufferLease);
        }

        private void ReleaseSaveStatusBuffer()
        {
            if (!_saveStatusBufferLease.IsValid)
                return;

            CharBufferPool.Release(_saveStatusBufferLease);
            _saveStatusBufferLease = default;
        }

        private void ApplySaveStatusText(string prefix, string value, string suffix)
        {
            if (_saveStatus == null)
            {
                _hasSaveStatusText = false;
                return;
            }

            TryAcquireSaveStatusBuffer();
            char[] buffer = _saveStatusBufferLease.IsValid ? _saveStatusBufferLease.Buffer : _saveStatusFallbackBuffer;
            int cursor = 0;
            cursor += CopyStringToBuffer(prefix, buffer, cursor);
            cursor += CopyStringToBuffer(value, buffer, cursor);
            cursor += CopyStringToBuffer(suffix, buffer, cursor);
            _saveStatus.SetCharArray(buffer, 0, cursor);
            _hasSaveStatusText = cursor > 0;
        }

        private void ApplySaveStatusLiteral(string value)
        {
            ApplySaveStatusText(string.Empty, value, string.Empty);
        }

        private void ApplySaveFailedStatusText(string slotName, string error)
        {
            if (_saveStatus == null)
            {
                _hasSaveStatusText = false;
                return;
            }

            TryAcquireSaveStatusBuffer();
            char[] buffer = _saveStatusBufferLease.IsValid ? _saveStatusBufferLease.Buffer : _saveStatusFallbackBuffer;
            int cursor = 0;
            cursor += CopyStringToBuffer(ResolveSlotDisplayName(slotName), buffer, cursor);
            cursor += CopyStringToBuffer(_cachedFailed, buffer, cursor);

            if (!CopyUpperAsciiStringToBuffer(error, buffer, ref cursor))
            {
                cursor += CopyStringToBuffer(_cachedUnknownErrorStatus, buffer, cursor);
            }

            _saveStatus.SetCharArray(buffer, 0, cursor);
            _hasSaveStatusText = cursor > 0;
        }

        private void ApplyLoadFailedStatusText(string slotName, string error)
        {
            if (_saveStatus == null)
            {
                _hasSaveStatusText = false;
                return;
            }

            TryAcquireSaveStatusBuffer();
            char[] buffer = _saveStatusBufferLease.IsValid ? _saveStatusBufferLease.Buffer : _saveStatusFallbackBuffer;
            int cursor = 0;
            cursor += CopyStringToBuffer(ResolveSlotDisplayName(slotName), buffer, cursor);
            cursor += CopyStringToBuffer(_cachedLoadFailed, buffer, cursor);

            if (!CopyUpperAsciiStringToBuffer(error, buffer, ref cursor))
            {
                cursor += CopyStringToBuffer(_cachedUnknownErrorStatus, buffer, cursor);
            }

            _saveStatus.SetCharArray(buffer, 0, cursor);
            _hasSaveStatusText = cursor > 0;
        }

        private static int CopyStringToBuffer(string value, char[] buffer, int offset)
        {
            if (string.IsNullOrEmpty(value) || buffer == null || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            value.AsSpan(0, safeLength).CopyTo(buffer.AsSpan(offset, safeLength));
            return safeLength;
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> value, char[] buffer, int offset)
        {
            if (value.Length == 0 || buffer == null || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            value.Slice(0, safeLength).CopyTo(buffer.AsSpan(offset, safeLength));
            return safeLength;
        }

        private static bool CopyUpperAsciiStringToBuffer(
            string value,
            char[] buffer,
            ref int offset)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > 0x7F)
                    return false;
            }

            if (buffer == null || offset >= buffer.Length)
                return true;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            for (int i = 0; i < safeLength; i++)
            {
                char raw = value[i];
                if (raw >= 'a' && raw <= 'z')
                    raw = (char)(raw - 32);

                buffer[offset + i] = raw;
            }

            offset += safeLength;
            return true;
        }

        private void ExitToMainMenu()
        {
            if (_exitToMainMenuInFlight)
                return;

            EnsureBuilt();
            CommitPauseVisualSelectionIfNeeded();

            if (pauseTimeScale)
            {
                PublishPauseState(false, 1f);
            }

            _exitToMainMenuInFlight = true;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = true;
            }

            SetPanelVisible(_mainPanelCanvasGroup, false);
            SetPanelVisible(_savesPanelCanvasGroup, false);
            SetPanelVisible(_helpPanelCanvasGroup, false);
            SetPanelVisible(_settingsPanelCanvasGroup, false);
            ClearPauseSelection();

            if (_headerTitle != null)
                TmpTextNoAlloc.Set(_headerTitle, "RETURNING TO MAIN MENU");

            if (_headerSub != null)
                TmpTextNoAlloc.Set(_headerSub, "asynchronous scene handoff in progress");

            if (_footerHint != null)
                TmpTextNoAlloc.Set(_footerHint, "loading menu shell and releasing world memory");

            if (_saveStatus != null)
                ApplySaveStatusLiteral(_cachedPreparingSceneTransition);

            GameStartContextHolder.Reset();
            string resolvedMainMenuSceneName = ResolveMainMenuSceneName(mainMenuSceneName);
            RegisterMainMenuCleanup(resolvedMainMenuSceneName);

            ISceneService sceneService = GlobalRegistry.Scene;
            SceneRuntimeService runtimeSceneService = sceneService as SceneRuntimeService;
            if (sceneService == null)
            {
                runtimeSceneService = SceneRuntimeService.EnsureRuntimeInstance();
                if (runtimeSceneService != null)
                {
                    runtimeSceneService.InitializeService();
                    sceneService = runtimeSceneService;
                }
            }

            if (sceneService == null || !sceneService.CanLoadScene)
            {
                FailMainMenuExitTransition("Scene runtime service unavailable; main-menu activation aborted.");
                return;
            }

            sceneService.LoadScene(resolvedMainMenuSceneName);
        }

        private void FailMainMenuExitTransition(string message)
        {
            _exitToMainMenuInFlight = false;
            UnregisterMainMenuCleanup();

            if (pauseTimeScale)
                PublishPauseState(true);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }

            if (_headerTitle != null)
                TmpTextNoAlloc.Set(_headerTitle, "MISSION PAUSE");

            ShowSection(PauseSection.Main);

            if (_footerHint != null)
                TmpTextNoAlloc.Set(_footerHint, "ESC = back / resume  |  SETTINGS hosts controls and rebinds");

            if (_saveStatus != null)
                ApplySaveStatusLiteral(message);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[PauseMenuController] Fatal pause-menu state.");
#endif
        }

        private void HandleMainMenuExitTransitionDisabled()
        {
            bool wasOpen = _isOpen;
            _isOpen = false;
            _activeSection = PauseSection.Main;

            if (wasOpen)
                UnregisterOpenMenu();

            ClearPauseSelection();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            _exitToMainMenuInFlight = false;
        }

        private static void RegisterMainMenuCleanup(string sceneName)
        {
            _pendingMainMenuCleanup = true;
            _pendingMainMenuSceneName = ResolveMainMenuSceneName(sceneName);

            if (_mainMenuCleanupHookRegistered)
                return;

                SceneManager.sceneLoaded -= HandlePendingMainMenuSceneLoaded;
                SceneManager.sceneLoaded += HandlePendingMainMenuSceneLoaded;
            _mainMenuCleanupHookRegistered = true;
        }

        private static void UnregisterMainMenuCleanup()
        {
            _pendingMainMenuCleanup = false;
            _pendingMainMenuSceneName = string.Empty;

            if (!_mainMenuCleanupHookRegistered)
                return;

            SceneManager.sceneLoaded -= HandlePendingMainMenuSceneLoaded;
            _mainMenuCleanupHookRegistered = false;
        }

        private static string ResolveMainMenuSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? DefaultMainMenuSceneName : sceneName.Trim();
        }

        private static void HandlePendingMainMenuSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_pendingMainMenuCleanup ||
                !string.Equals(scene.name, _pendingMainMenuSceneName, StringComparison.Ordinal))
            {
                return;
            }

            UnregisterMainMenuCleanup();

            AssetLoadDispatcher.ForceDrainDeferredReleases();
            _onMainMenuCleanupCompleted?.Invoke(null);
        }

        private static void HandleMainMenuCleanupCompleted(AsyncOperation unloadOperation)
        {
            if (unloadOperation != null)
                unloadOperation.completed -= _onMainMenuCleanupCompleted;
        }

        private void QuitApplication()
        {
            CommitPauseVisualSelectionIfNeeded();

            // TASK 33: Ensure all settings are saved before Application.Quit()
            // Save UserOptions (input overrides, etc.)
            Hecton8.Input.UserOptionsPersistence userOptions = Hecton8.Core.GlobalRegistry.UserOptions;
            if (userOptions != null && userOptions.IsServiceReady && userOptions.isActiveAndEnabled)
            {
                if (!userOptions.TrySave())
                    Hecton8.Core.H8Debug.LogError("[PauseMenuController] User options save failed during quit.");
            }

            // SettingsManager persists settings through UserOptionsPersistence; this path flushes the owner before quit.

            if (pauseTimeScale)
            {
                PublishPauseState(false, 1f);
            }

#if UNITY_EDITOR
            Hecton8.Dev.EditorPlayModeDiagnostics.RequestStopPlayMode(
                nameof(PauseMenuController),
                "PauseMenu Quit",
                this);
#else
            Application.Quit();
#endif
        }

        private static CanvasGroup EnsurePanelCanvasGroup(RectTransform panel)
        {
            if (panel == null)
                return null;

            if (!panel.TryGetComponent(out CanvasGroup group))
                group = panel.gameObject.AddComponent<CanvasGroup>();

            return group;
        }

        private static void SetPanelVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static RectTransform CreatePanel(Transform parent, string name)
        {
            RectTransform rect = CreateRect(parent, name);
            Stretch(rect, 0f, 0f, 0f, 0f);
            Image bg = EnsureImage(rect.gameObject);
            bg.color = PanelBg;
            bg.raycastTarget = false;
            return rect;
        }

        private RectTransform CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Action onClick)
        {
            return CreateButton(
                parent,
                name,
                string.IsNullOrEmpty(label) ? ReadOnlySpan<char>.Empty : label.AsSpan(),
                anchorMin,
                anchorMax,
                anchoredPosition,
                size,
                onClick);
        }

        private RectTransform CreateButton(Transform parent, string name, ReadOnlySpan<char> label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Action onClick)
        {
            RectTransform rect = CreateRect(parent, name);
            Anchor(rect, anchorMin, anchorMax, anchoredPosition, size);
            Image bg = EnsureImage(rect.gameObject);
            bg.color = ButtonBg;
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonBg;
            colors.highlightedColor = ButtonHover;
            colors.selectedColor = ButtonHover;
            colors.pressedColor = new Color(0.16f, 0.32f, 0.36f, 1f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.5f);
            button.colors = colors;
            button.transition = Selectable.Transition.ColorTint;
            if (onClick != null)
                button.onClick.AddListener(onClick.Invoke);

            TextMeshProUGUI text = CreateText(rect, "Label", labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            text.color = Primary;
            TmpTextNoAlloc.Set(text, label);

            return rect;
        }

        private Button CreateBackButton(Transform parent, Action onClick)
        {
            RectTransform buttonRoot = CreateButton(parent, "BackButton", "BACK", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-108f, 28f), new Vector2(180f, 34f), onClick);
            buttonRoot.TryGetComponent(out Button button);
            return button;
        }

        private TextMeshProUGUI CreateSectionTitle(Transform parent, string value)
        {
            TextMeshProUGUI text = CreateText(parent, "SectionTitle", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -18f), new Vector2(-24f, 24f));
            text.color = Primary;
            TmpTextNoAlloc.Set(text, value);
            return text;
        }

        private TextMeshProUGUI CreateSectionSub(Transform parent, string value)
        {
            return CreateSectionSub(parent, string.IsNullOrEmpty(value) ? ReadOnlySpan<char>.Empty : value.AsSpan());
        }

        private TextMeshProUGUI CreateSectionSub(Transform parent, ReadOnlySpan<char> value)
        {
            TextMeshProUGUI text = CreateText(parent, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -42f), new Vector2(-24f, 18f));
            text.color = DimLow;
            TmpTextNoAlloc.Set(text, value);
            return text;
        }

        private static TextMeshProUGUI GetText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null && child.TryGetComponent(out TextMeshProUGUI text) ? text : null;
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

        private static void CreateRule(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float y)
        {
            RectTransform rect = CreateRect(parent, "Rule");
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, anchorMin.y < 0.5f ? 0f : 1f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(0f, 1f);
            Image img = EnsureImage(rect.gameObject);
            img.color = Rule;
            img.raycastTarget = false;
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
            text.color = Dim;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            ConfigureGeneratedTextFit(text, size * 0.72f, size);
            return text;
        }

        private static void ConfigureGeneratedTextFit(TMP_Text text, float minSize, float maxSize)
        {
            if (text == null)
                return;

            float resolvedMin = math.max(6f, math.min(minSize, maxSize));
            float resolvedMax = math.max(resolvedMin, maxSize);
            text.enableAutoSizing = true;
            text.fontSizeMin = resolvedMin;
            text.fontSizeMax = resolvedMax;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.NoWrap;
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

        private void SelectDefaultButtonForSection(PauseSection section)
        {
            if (!_isOpen)
                return;

            EventSystem eventSystem = _cachedEventSystem;
            if (eventSystem == null)
                return;

            Button target = GetDefaultButtonForSection(section);
            if (!IsDefaultSelectionTargetEligible(section, target))
                return;

            GameObject targetObject = target.gameObject;
            if (eventSystem.currentSelectedGameObject == targetObject)
                return;

            eventSystem.SetSelectedGameObject(targetObject);
        }

        private bool IsDefaultSelectionTargetEligible(PauseSection section, Button target)
        {
            if (target == null || !target.interactable || !target.gameObject.activeInHierarchy)
                return false;

            CanvasGroup sectionGroup = ResolveSectionGroup(section);
            if (sectionGroup == null ||
                !sectionGroup.interactable ||
                !sectionGroup.blocksRaycasts ||
                sectionGroup.alpha < 0.999f)
            {
                return false;
            }

            Transform targetTransform = target.transform;
            Transform groupTransform = sectionGroup.transform;
            return targetTransform != null && groupTransform != null && targetTransform.IsChildOf(groupTransform);
        }

        private Button GetDefaultButtonForSection(PauseSection section)
        {
            switch (section)
            {
                case PauseSection.Main:
                    return _mainResumeButton;
                case PauseSection.Saves:
                    return GetFirstInteractableSaveButton() ?? _savesBackButton;
                case PauseSection.Help:
                    return _helpBackButton;
                case PauseSection.Settings:
                    return _settingsLanguageButton != null ? _settingsLanguageButton : _settingsBackButton;
                default:
                    return null;
            }
        }

        private CanvasGroup ResolveSectionGroup(PauseSection section)
        {
            switch (section)
            {
                case PauseSection.Saves:
                    return _savesPanelCanvasGroup;
                case PauseSection.Help:
                    return _helpPanelCanvasGroup;
                case PauseSection.Settings:
                    return _settingsPanelCanvasGroup;
                default:
                    return _mainPanelCanvasGroup;
            }
        }

        private Button GetFirstInteractableSaveButton()
        {
            if (_saveSlotButtons == null)
                return _savesFirstButton;

            for (int i = 0; i < _saveSlotButtons.Length; i++)
            {
                Button button = _saveSlotButtons[i];
                if (button != null && button.interactable && button.gameObject.activeInHierarchy)
                    return button;
            }

            return null;
        }

        private void RefreshSaveSectionState()
        {
            // TASK 31: Save service null check with user-facing error message
            ISaveService saveService = _cachedSaveService;

            // If save service is unavailable, disable all save buttons and display error
            if (!IsSaveServiceUsable(saveService))
            {
                SetSaveButtonsInteractable(false);

                if (_saveStatus != null)
                    ApplySaveStatusLiteral(_cachedSaveServiceUnavailable);

                return;
            }

            bool isBusy = _saveOperationInFlight || saveService.IsBusy;
            SetSaveButtonsInteractable(!isBusy);

            if (_saveStatus == null)
                return;

            if (isBusy)
            {
                ApplySaveStatusLiteral(_cachedSaveInProgress);
                return;
            }

            if (!_hasSaveStatusText)
                ApplySaveStatusLiteral(_cachedAwaitingSaveCommand);
        }

        private void RefreshSaveSlotButtonLabels()
        {
            if (_saveSlotButtonLabels == null || saveSlots == null)
                return;

            int count = math.min(_saveSlotButtonLabels.Length, SaveEvents.ManualSlotCount);
            for (int i = 0; i < count; i++)
            {
                TextMeshProUGUI label = _saveSlotButtonLabels[i];
                if (label == null)
                    continue;

                ApplyTemplatedText(label, _cachedWritePrefix, ResolveSlotDisplayName(ResolveConfiguredSaveSlotName(i)), string.Empty, _saveSlotLabelBuffer);
            }
        }

        private void SetSaveButtonsInteractable(bool interactable)
        {
            if (_saveSlotButtons == null)
            {
                if (_savesBackButton != null)
                    _savesBackButton.interactable = interactable;
                return;
            }

            for (int i = 0; i < _saveSlotButtons.Length; i++)
            {
                Button button = _saveSlotButtons[i];
                if (button == null)
                    continue;

                button.interactable = interactable;
            }

            if (_savesBackButton != null)
                _savesBackButton.interactable = interactable;
        }

        private void HandleSaveStarted(string slotName)
        {
            _saveOperationInFlight = true;
            SetSaveButtonsInteractable(false);

            if (_saveStatus != null)
                ApplySaveStatusText(_cachedWriting, ResolveSlotDisplayName(slotName), "...");
        }

        private void HandleSaveCompleted(string slotName)
        {
            _pendingRetrySaveSlotName = string.Empty;
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveStatusText(string.Empty, ResolveSlotDisplayName(slotName), _cachedWritten);

            if (_activeSection == PauseSection.Saves)
                QueueDefaultSelectionForSection(PauseSection.Saves, gateInteraction: false);
        }

        private void HandleSaveFailed(string slotName, string error)
        {
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveFailedStatusText(slotName, error);

            bool canRetry = SaveEvents.IsKnownManualSlotName(slotName);
            int messageLength = BuildSaveModalMessage(
                ErrorSaveFailedMessageKeyHash,
                "Failed to save.",
                slotName,
                error,
                true,
                canRetry);

            ModalWindow.ShowWithCustomLabels(
                "Save Failed",
                _modalMessageBuffer,
                messageLength,
                canRetry ? CacheRetrySaveSlot(slotName) : null,
                null,
                canRetry ? "Retry" : "OK",
                canRetry ? "Cancel" : null);

            if (_activeSection == PauseSection.Saves)
                QueueDefaultSelectionForSection(PauseSection.Saves, gateInteraction: false);
        }

        private void HandleLoadStarted(string slotName)
        {
            _saveOperationInFlight = true;
            SetSaveButtonsInteractable(false);

            if (_saveStatus != null)
                ApplySaveStatusText(_cachedLoading, ResolveSlotDisplayName(slotName), "...");
        }

        private void HandleLoadCompleted(string slotName)
        {
            _pendingRetrySaveSlotName = string.Empty;
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveStatusText(string.Empty, ResolveSlotDisplayName(slotName), _cachedLoaded);

            ShowLoadRecoveryModal(slotName);

            if (_activeSection == PauseSection.Saves)
                QueueDefaultSelectionForSection(PauseSection.Saves, gateInteraction: false);
        }

        private void ShowLoadRecoveryModal(string slotName)
        {
            if (!(_cachedSaveService is SaveManager saveManager) || !saveManager.IsInitialized)
                return;

            bool usedBackup = saveManager.LastLoadUsedBackup;
            bool selfRepaired = saveManager.LastLoadSelfRepaired;
            if (!usedBackup && !selfRepaired)
                return;

            int messageLength = usedBackup
                ? BuildSaveModalMessage(
                    WarningBackupUsedMessageKeyHash,
                    "Primary save file was corrupt. Loaded from backup.",
                    slotName,
                    default,
                    appendError: false,
                    appendRetryPrompt: false)
                : BuildSaveModalMessage(
                    WarningSaveRepairedMessageKeyHash,
                    "Primary save file was repaired before loading.",
                    slotName,
                    default,
                    appendError: false,
                    appendRetryPrompt: false);
            string title = usedBackup
                ? BuildLocalizedModalTitle(WarningBackupUsedTitleKeyHash, "BACKUP LOADED")
                : BuildLocalizedModalTitle(WarningSaveRepairedTitleKeyHash, "SAVE REPAIRED");

            ModalWindow.ShowWithCustomLabels(
                title,
                _modalMessageBuffer,
                messageLength,
                null,
                null,
                "OK",
                null);
        }

        private void HandleLoadFailed(string slotName, string error)
        {
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplyLoadFailedStatusText(slotName, error);

            int messageLength = BuildSaveModalMessage(
                ErrorLoadFailedMessageKeyHash,
                "Failed to load.",
                slotName,
                error,
                true,
                false);

            ModalWindow.ShowWithCustomLabels(
                "Load Failed",
                _modalMessageBuffer,
                messageLength,
                null,
                null,
                "OK",
                null);

            if (_activeSection == PauseSection.Saves)
                QueueDefaultSelectionForSection(PauseSection.Saves, gateInteraction: false);
        }

        private string ResolveSaveFailureMessage(in SaveEventPayload payload)
        {
            string message = SaveEvents.ResolveMessage(in payload);
            if (!string.IsNullOrEmpty(message))
                return message;

            return SaveEvents.TryConsumeMatchingFailureSnapshotForUi(
                ref _lastConsumedFailureSnapshotSequence,
                in payload,
                out string snapshotMessage)
                ? snapshotMessage
                : message;
        }

        private void CycleLanguage()
        {
            ILocalizationLanguageControl localization = _cachedLocalization;
            if (localization == null)
            {
                RefreshLanguageSettingsStatus();
                return;
            }

            localization.CycleLanguage();
            RefreshLanguageSettingsStatus();
        }

        private Action CacheRetrySaveSlot(string slotName)
        {
            if (!SaveEvents.IsKnownManualSlotName(slotName))
            {
                _pendingRetrySaveSlotName = string.Empty;
                return null;
            }

            _pendingRetrySaveSlotName = slotName ?? string.Empty;
            return RetryPendingSaveSlot;
        }

        private void RetryPendingSaveSlot()
        {
            string slotName = _pendingRetrySaveSlotName;
            if (string.IsNullOrEmpty(slotName))
                return;

            _pendingRetrySaveSlotName = string.Empty;
            SaveSlot(slotName);
        }

        private void CycleMenuVisualStyle()
        {
            int nextIndex = MenuVisualStyleCatalog.ToIndex(visualStyle) + 1;
            if (nextIndex >= MenuVisualStyleCatalog.StyleCount)
                nextIndex = 0;

            MenuVisualStyle nextStyle = MenuVisualStyleCatalog.FromIndex(nextIndex);
            SettingsManager settings = _cachedSettings;
            _pauseVisualStyleDirty = true;
            _pendingMenuVisualStyleIndex = nextIndex;
            SetVisualStyle(nextStyle);
            settings?.PreviewMenuVisualStyle(nextStyle);
            RefreshMenuVisualStyleStatus();
        }

        private void CycleMenuVisualConcept()
        {
            int nextIndex = MenuVisualConceptCatalog.ToIndex(visualConcept) + 1;
            if (nextIndex >= MenuVisualConceptCatalog.ConceptCount)
                nextIndex = 0;

            MenuVisualConcept nextConcept = MenuVisualConceptCatalog.FromIndex(nextIndex);
            SettingsManager settings = _cachedSettings;
            _pauseVisualConceptDirty = true;
            _pendingMenuVisualConceptIndex = nextIndex;
            SetVisualConcept(nextConcept);
            settings?.PreviewMenuVisualConcept(nextConcept);
            RefreshMenuVisualConceptStatus();
        }

        private void CommitPauseVisualSelectionIfNeeded()
        {
            if (!_pauseVisualStyleDirty && !_pauseVisualConceptDirty)
                return;

            SettingsManager settings = _cachedSettings;
            if (settings == null)
            {
                ClearPendingPauseVisualSelection();
                return;
            }

            settings.BeginPersistenceBatch();
            try
            {
                if (_pauseVisualStyleDirty)
                {
                    int styleIndex = _pendingMenuVisualStyleIndex >= 0
                        ? _pendingMenuVisualStyleIndex
                        : MenuVisualStyleCatalog.ToIndex(visualStyle);
                    settings.MenuVisualStyle = MenuVisualStyleCatalog.FromIndex(styleIndex);
                }

                if (_pauseVisualConceptDirty)
                {
                    int conceptIndex = _pendingMenuVisualConceptIndex >= 0
                        ? _pendingMenuVisualConceptIndex
                        : MenuVisualConceptCatalog.ToIndex(visualConcept);
                    settings.MenuVisualConcept = MenuVisualConceptCatalog.FromIndex(conceptIndex);
                }
            }
            finally
            {
                settings.EndPersistenceBatch();
                ClearPendingPauseVisualSelection();
            }
        }

        private void ClearPendingPauseVisualSelection()
        {
            _pauseVisualStyleDirty = false;
            _pauseVisualConceptDirty = false;
            _pendingMenuVisualStyleIndex = -1;
            _pendingMenuVisualConceptIndex = -1;
        }

        private void ApplyPersistedVisualStyleCold()
        {
            SettingsManager settings = _cachedSettings;
            if (settings == null)
                return;

            SetVisualStyle(settings.MenuVisualStyle);
        }

        private void ApplyPersistedVisualConceptCold()
        {
            SettingsManager settings = _cachedSettings;
            if (settings == null)
                return;

            SetVisualConcept(settings.MenuVisualConcept);
        }

        private void CacheSettingsManagerCold(SettingsManager settings)
        {
            if (ReferenceEquals(_cachedSettings, settings))
                return;

            CommitPauseVisualSelectionIfNeeded();

            if (_cachedSettings != null)
            {
                _cachedSettings.MenuVisualStyleChanged -= HandleMenuVisualStyleChanged;
                _cachedSettings.MenuVisualConceptChanged -= HandleMenuVisualConceptChanged;
            }

            _cachedSettings = settings;

            if (_cachedSettings != null)
            {
                _cachedSettings.MenuVisualStyleChanged += HandleMenuVisualStyleChanged;
                _cachedSettings.MenuVisualConceptChanged += HandleMenuVisualConceptChanged;
            }
        }

        private void HandleMenuVisualStyleChanged(MenuVisualStyle style)
        {
            SetVisualStyle(style);
            RefreshMenuVisualStyleStatus();
        }

        private void HandleMenuVisualConceptChanged(MenuVisualConcept concept)
        {
            SetVisualConcept(concept);
            RefreshMenuVisualConceptStatus();
        }

        private void RefreshMenuVisualStyleStatus()
        {
            if (_settingsMenuStyleStatus == null)
                return;

            SettingsManager settings = _cachedSettings;
            MenuVisualStyle currentStyle = _pauseVisualStyleDirty || settings == null
                ? visualStyle
                : settings.MenuVisualStyle;
            if (currentStyle != visualStyle)
                SetVisualStyle(currentStyle);

            ApplyIndexedSettingsStatus(
                _settingsMenuStyleStatus,
                "MENU STYLE ".AsSpan(),
                MenuVisualStyleCatalog.ToIndex(currentStyle) + 1,
                MenuVisualStyleCatalog.StyleCount,
                MenuVisualStyleCatalog.GetDisplayName(currentStyle),
                _settingsMenuStyleBuffer);
        }

        private void RefreshMenuVisualConceptStatus()
        {
            if (_settingsMenuConceptStatus == null)
                return;

            SettingsManager settings = _cachedSettings;
            MenuVisualConcept currentConcept = _pauseVisualConceptDirty || settings == null
                ? visualConcept
                : settings.MenuVisualConcept;
            if (currentConcept != visualConcept)
                SetVisualConcept(currentConcept);

            ApplyIndexedSettingsStatus(
                _settingsMenuConceptStatus,
                "MENU CONCEPT ".AsSpan(),
                MenuVisualConceptCatalog.ToIndex(currentConcept) + 1,
                MenuVisualConceptCatalog.ConceptCount,
                MenuVisualConceptCatalog.GetDisplayName(currentConcept),
                _settingsMenuConceptBuffer);
        }

        private void RefreshLanguageSettingsStatus()
        {
            if (_settingsLanguageStatus == null)
                return;

            ILocalizationLanguageControl localization = _cachedLocalization;
            if (localization == null)
            {
                SetSettingsLanguageStatus(ResolveLocalizedSpan(
                    SettingsLanguageOwnerUnavailableKeyHash,
                    "LANGUAGE OWNER UNAVAILABLE."));
                return;
            }

            ApplyFormattedSettingsLanguageStatus(
                ResolveLocalizedSpan(
                    SettingsCurrentLanguageKeyHash,
                    "CURRENT LANGUAGE: {0}"),
                GetLanguageDisplayName((GameLanguage)localization.ActiveLanguageId).AsSpan());
        }

        private void SetSettingsLanguageStatus(ReadOnlySpan<char> value)
        {
            if (_settingsLanguageStatus == null)
                return;

            ApplyTemplatedText(_settingsLanguageStatus, ReadOnlySpan<char>.Empty, value, ReadOnlySpan<char>.Empty, _settingsLanguageBuffer);
        }

        private void ApplyFormattedSettingsLanguageStatus(ReadOnlySpan<char> template, ReadOnlySpan<char> replacement)
        {
            if (_settingsLanguageStatus == null)
                return;

            int placeholderIndex = IndexOfPlaceholder(template);
            if (placeholderIndex < 0)
            {
                ApplyTemplatedText(_settingsLanguageStatus, ReadOnlySpan<char>.Empty, template, ReadOnlySpan<char>.Empty, _settingsLanguageBuffer);
                return;
            }

            ReadOnlySpan<char> prefix = placeholderIndex > 0
                ? template.Slice(0, placeholderIndex)
                : ReadOnlySpan<char>.Empty;
            ReadOnlySpan<char> suffix = placeholderIndex + 3 < template.Length
                ? template.Slice(placeholderIndex + 3)
                : ReadOnlySpan<char>.Empty;
            ApplyTemplatedText(_settingsLanguageStatus, prefix, replacement, suffix, _settingsLanguageBuffer, true);
        }

        private static string GetLanguageDisplayName(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.English: return "English";
                case GameLanguage.Russian: return "Russkiy";
                case GameLanguage.German: return "Deutsch";
                case GameLanguage.French: return "Français";
                case GameLanguage.Spanish: return "Español";
                case GameLanguage.Italian: return "Italiano";
                case GameLanguage.PortugueseBrazilian: return "Português (Brasil)";
                case GameLanguage.Polish: return "Polski";
                case GameLanguage.Turkish: return "Türkçe";
                case GameLanguage.Ukrainian: return "Ukra_nska";
                case GameLanguage.ChineseSimplified: return "简体中文";
                case GameLanguage.ChineseTraditional: return "繁體中文";
                case GameLanguage.Japanese: return "日本語";
                case GameLanguage.Korean: return "한국어";
                case GameLanguage.Hindi: return "हिन्दी";
                case GameLanguage.Indonesian: return "Bahasa Indonesia";
                case GameLanguage.Arabic: return "العربية";
                case GameLanguage.Hebrew: return "\u05E2\u05D1\u05E8\u05D9\u05EA";
                case GameLanguage.Dutch: return "Nederlands";
                default: return "English";
            }
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, string fallback)
        {
            ILocalizationLanguageControl manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(keyHash, fallback.AsSpan())
                : fallback.AsSpan();
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, ReadOnlySpan<char> fallback)
        {
            ILocalizationLanguageControl manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(keyHash, fallback)
                : fallback;
        }

        private static int IndexOfPlaceholder(ReadOnlySpan<char> template)
        {
            for (int i = 0; i < template.Length - 2; i++)
            {
                if (template[i] == '{' && template[i + 1] == '0' && template[i + 2] == '}')
                    return i;
            }

            return -1;
        }

        private static string ResolveSlotDisplayName(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return "?";

            int slotIndex = SaveEvents.ResolveKnownSlotIndex(slotName);
            switch (slotIndex)
            {
                case 0:
                    return "SLOT 1";
                case 1:
                    return "SLOT 2";
                case 2:
                    return "SLOT 3";
                default:
                    return "SLOT ?";
            }
        }

        private string ResolveConfiguredSaveSlotName(int slotIndex)
        {
            if (saveSlots != null &&
                (uint)slotIndex < (uint)saveSlots.Length &&
                SaveEvents.IsKnownManualSlotName(saveSlots[slotIndex]))
            {
                return saveSlots[slotIndex];
            }

            return SaveEvents.ResolveManualSlotName(slotIndex);
        }

        private int CopyLocalizedSpanToModalBuffer(int keyHash, ReadOnlySpan<char> fallback)
        {
            return CopySpanToBuffer(ResolveLocalizedSpan(keyHash, fallback), _modalMessageBuffer, 0);
        }

        private string BuildLocalizedModalTitle(int keyHash, ReadOnlySpan<char> fallback)
        {
            int length = CopySpanToBuffer(ResolveLocalizedSpan(keyHash, fallback), _modalTitleBuffer, 0);
            return length > 0 ? new string(_modalTitleBuffer, 0, length) : string.Empty;
        }

        private int BuildSaveModalMessage(
            int localizationKeyHash,
            ReadOnlySpan<char> fallback,
            string slotName,
            string error,
            bool appendError)
        {
            return BuildSaveModalMessage(localizationKeyHash, fallback, slotName, error, appendError, true);
        }

        private int BuildSaveModalMessage(
            int localizationKeyHash,
            ReadOnlySpan<char> fallback,
            string slotName,
            string error,
            bool appendError,
            bool appendRetryPrompt)
        {
            if (_modalMessageBuffer == null)
                return 0;

            int cursor = 0;
            cursor += CopySpanToBuffer(ResolveLocalizedSpan(localizationKeyHash, fallback), _modalMessageBuffer, cursor);
            cursor += CopySpanToBuffer(" // ".AsSpan(), _modalMessageBuffer, cursor);
            cursor += CopySpanToBuffer(ResolveSlotDisplayName(slotName).AsSpan(), _modalMessageBuffer, cursor);

            if (appendError)
            {
                cursor += CopySpanToBuffer("\n".AsSpan(), _modalMessageBuffer, cursor);
                if (!CopyUpperAsciiStringToBuffer(error, _modalMessageBuffer, ref cursor))
                    cursor += CopySpanToBuffer(_cachedUnknownErrorModal.AsSpan(), _modalMessageBuffer, cursor);
            }

            if (appendRetryPrompt)
                cursor += CopySpanToBuffer("\n\nRetry?".AsSpan(), _modalMessageBuffer, cursor);
            return cursor;
        }

        private static void ApplyTemplatedText(TMP_Text label, string prefix, string value, string suffix, char[] buffer)
        {
            ApplyTemplatedText(
                label,
                string.IsNullOrEmpty(prefix) ? ReadOnlySpan<char>.Empty : prefix.AsSpan(),
                string.IsNullOrEmpty(value) ? ReadOnlySpan<char>.Empty : value.AsSpan(),
                string.IsNullOrEmpty(suffix) ? ReadOnlySpan<char>.Empty : suffix.AsSpan(),
                buffer);
        }

        private static void ApplyTemplatedText(TMP_Text label, ReadOnlySpan<char> prefix, ReadOnlySpan<char> value, ReadOnlySpan<char> suffix, char[] buffer)
        {
            ApplyTemplatedText(label, prefix, value, suffix, buffer, false);
        }

        private static void ApplyIndexedSettingsStatus(
            TMP_Text label,
            ReadOnlySpan<char> prefix,
            int oneBasedIndex,
            int totalCount,
            ReadOnlySpan<char> value,
            char[] buffer)
        {
            if (label == null || buffer == null || buffer.Length == 0)
                return;

            int cursor = 0;
            cursor += CopySpanToBuffer(prefix, buffer, cursor);
            cursor += CopyTwoDigitPositiveIntToBuffer(oneBasedIndex, buffer, cursor);
            cursor += CopySpanToBuffer("/".AsSpan(), buffer, cursor);
            cursor += CopyTwoDigitPositiveIntToBuffer(totalCount, buffer, cursor);
            cursor += CopySpanToBuffer(": ".AsSpan(), buffer, cursor);
            cursor += CopySpanToBuffer(value, buffer, cursor);
            label.SetCharArray(buffer, 0, cursor);
        }

        private static int CopyTwoDigitPositiveIntToBuffer(int value, char[] buffer, int offset)
        {
            if (buffer == null || offset >= buffer.Length)
                return 0;

            int safeValue = math.clamp(value, 0, 99);
            int written = 0;
            if (safeValue < 10 && offset < buffer.Length)
            {
                buffer[offset] = '0';
                offset++;
                written++;
            }

            if (safeValue.TryFormat(buffer.AsSpan(offset), out int digitsWritten))
                return written + digitsWritten;

            return written;
        }

        private static void ApplyTemplatedText(TMP_Text label, ReadOnlySpan<char> prefix, ReadOnlySpan<char> value, ReadOnlySpan<char> suffix, char[] buffer, bool uppercaseValue)
        {
            if (label == null || buffer == null || buffer.Length == 0)
                return;

            int cursor = 0;
            cursor += CopySpanToBuffer(prefix, buffer, cursor);
            cursor += uppercaseValue
                ? CopyUpperAsciiSpanToBuffer(value, buffer, cursor)
                : CopySpanToBuffer(value, buffer, cursor);
            cursor += CopySpanToBuffer(suffix, buffer, cursor);
            label.SetCharArray(buffer, 0, cursor);
        }

        private static int CopyUpperAsciiSpanToBuffer(ReadOnlySpan<char> value, char[] buffer, int offset)
        {
            if (value.IsEmpty || buffer == null || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            for (int i = 0; i < safeLength; i++)
            {
                char c = value[i];
                buffer[offset + i] = c >= 'a' && c <= 'z' ? (char)(c - 32) : c;
            }

            return safeLength;
        }

        private void ClearPauseSelection()
        {
            _hasPendingDefaultSelection = false;
            _hasPendingPauseSelectionClear = true;

            if (!Application.isPlaying || !_lateFrameRegistered || !isActiveAndEnabled)
                FlushPendingPauseSelectionClear();
        }

        private void EnsureEventSystem()
        {
            EventSystem eventSystem = _cachedEventSystem;
            if (eventSystem == null)
                eventSystem = EventSystem.current;

            if (eventSystem == null)
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                GameObject eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem)); // COLD ALLOC: GameObject[1] - pause-menu fallback event system root - owner: PauseMenuController
                eventSystemRoot.hideFlags = HideFlags.DontSave;
                eventSystemRoot.TryGetComponent(out eventSystem);
            }

            if (eventSystem == null)
                return;

            _cachedEventSystem = eventSystem;

            eventSystem.TryGetComponent(out StandaloneInputModule legacyInputModule);
            if (!eventSystem.TryGetComponent(out InputSystemUIInputModule inputSystemModule))
            {
                // Player-build construction path: no authored/bootstrap instance reachable.
                // Must construct in player builds when bootstrap reorders or skips registration.
                if (legacyInputModule != null)
                {
                    legacyInputModule.enabled = false;
                    if (Application.isPlaying)
                        Destroy(legacyInputModule);
                    else
                        DestroyImmediate(legacyInputModule);
                }

                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            INativeInputManagerRuntime inputManager = GlobalRegistry.NativeInputRuntime;
            if (inputManager != null)
                inputManager.TryConfigureUiInputModule(inputSystemModule);

            if (_diegeticRaycastReceiver != null && _diegeticCanvasRoot != null)
                _diegeticRaycastReceiver.Configure(_diegeticCanvasRoot, _cachedEventSystem, DiegeticPauseHapticSourceHash);
        }

        private void BindInputActions()
        {
            BindInputActions(GlobalRegistry.NativeInputRuntime);
        }

        private void BindInputActions(INativeInputManagerRuntime inputManager)
        {
            if (ReferenceEquals(_inputManager, inputManager))
                return;

            UnbindInputActions();
            _inputManager = inputManager;
            if (_inputManager == null)
                return;

            _inputManager.OnPause += HandlePauseActionPerformed;
        }

        private void UnbindInputActions()
        {
            if (_inputManager != null)
            {
                _inputManager.OnPause -= HandlePauseActionPerformed;
            }

            _inputManager = null;
            _pauseRequested = false;
            _cancelRequested = false;
        }

        private void HandlePauseActionPerformed()
        {
            _pauseRequested = true;
        }

        private void ConsumePlayerInputSignals(bool suppressCancelRequest)
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    signal.Command != PlayerInputSignalCommands.Cancel ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                if (!suppressCancelRequest)
                    _cancelRequested = true;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void HandlePauseRequested()
        {
            if (_isOpen)
                return;

            if (PlayerPDA.IsOpen || HectonFabricatorUI.IsMenuOpen)
                return;

            Open();
        }

        private void HandleCancelRequested()
        {
            if (!_isOpen)
                return;

            if (_activeSection == PauseSection.Main)
            {
                Close();
                return;
            }

            ShowSection(PauseSection.Main);
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
