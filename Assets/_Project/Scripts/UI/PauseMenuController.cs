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
    public sealed class PauseMenuController : MonoBehaviour, ITickable, IUnscaledFastTickable, IUpdatable, ISaveEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        internal static PauseMenuController ActiveRuntimeInstance { get; private set; }
        private const string PauseMenuRootName = "PauseMenu_Root";

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

        [Header("References")]
        [SerializeField] private PlayerPDA playerPDA;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        [Header("Settings")]
        [SerializeField] private string mainMenuSceneName = "01_MAIN_MENU";
        [SerializeField] private string[] saveSlots = { "slot_0", "slot_1", "slot_2" };
        [SerializeField] private bool pauseTimeScale = true;

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
        private bool _hasSaveStatusText;
        private PauseSection _activeSection;
        private float _cachedTimeDilationScalar = 1f;
        private uint _pauseSignalSequence;
        private uint _lastPlayerInputSignalSequence;
        private INativeInputManagerRuntime _inputManager;
        private bool _hotSwapListenerRegistered;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ISaveService _cachedSaveService;
        private LocalizationManager _cachedLocalization;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;

        private RectTransform _root;
        private CanvasGroup _canvasGroup;
        private Image _background;
        private TextMeshProUGUI _headerTitle;
        private TextMeshProUGUI _headerSub;
        private TextMeshProUGUI _footerHint;
        private RectTransform _mainPanel;
        private RectTransform _savesPanel;
        private RectTransform _helpPanel;
        private RectTransform _settingsPanel;
        private CanvasGroup _mainPanelCanvasGroup;
        private CanvasGroup _savesPanelCanvasGroup;
        private CanvasGroup _helpPanelCanvasGroup;
        private CanvasGroup _settingsPanelCanvasGroup;
        private TextMeshProUGUI _saveStatus;
        private PauseControlsPanel _controlsPanel;
        private Button _mainResumeButton;
        private Button _savesFirstButton;
        private Button _savesBackButton;
        private Button[] _saveSlotButtons;
        private Button _helpBackButton;
        private Button _settingsBackButton;
        private Button _settingsLanguageButton;
        private TextMeshProUGUI _settingsLanguageStatus;
        private CharBufferPool.Lease _saveStatusBufferLease;
        // COLD ALLOC: char[128] — pause-menu save status fallback buffer when transient pool leases are exhausted — owner: PauseMenuController
        private readonly char[] _saveStatusFallbackBuffer = new char[128];
        // COLD ALLOC: char[96] — settings language status staging buffer — owner: PauseMenuController
        private readonly char[] _settingsLanguageBuffer = new char[96];
        // COLD ALLOC: char[64] — save slot button label staging buffer — owner: PauseMenuController
        private readonly char[] _saveSlotLabelBuffer = new char[64];
        // COLD ALLOC: char[192] — modal save-error staging buffer copied directly into TMP — owner: PauseMenuController
        private readonly char[] _modalMessageBuffer = new char[192];

        public bool IsOpen => _isOpen;
        public bool IsSettingsOpen => _isOpen && _activeSection == PauseSection.Settings;
        public static bool IsAnyOpen => _openMenuCount > 0;

        // ══════════════════════════════════════════════════════════
        // CACHED STRINGS (zero-GC)
        // ══════════════════════════════════════════════════════════

        private void PublishPauseState(bool paused, float restoreScalar = 0f)
        {
            _pauseSignalSequence++;
            if (_pauseSignalSequence == 0u)
                _pauseSignalSequence = 1u;

            SimulationPauseSignal signal = new SimulationPauseSignal
            {
                SourceHash = PauseMenuSignalSourceHash,
                Frame = unchecked((uint)Hecton8.Core.SystemDispatcher.CurrentFrameIndex),
                Sequence = _pauseSignalSequence,
                Paused = paused ? (byte)1 : (byte)0,
                Flags = 0,
                RestoreScalar = restoreScalar
            };
            SimulationSignalRoute.TryQueuePause(in signal);

            ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
            if (dispatcher != null)
            {
                dispatcher.RequestSimulationPause(paused, PauseMenuSignalSourceHash);
                if (!paused && restoreScalar > 0f)
                    dispatcher.RequestTimeDilation(restoreScalar, PauseMenuSignalSourceHash);
            }
        }

        private static readonly string _cachedWriting = "WRITING ";
        private static readonly string _cachedWritten = " WRITTEN.";
        private static readonly string _cachedFailed = " FAILED. ";
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

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterUnscaledFastTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUnscaledFastTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = Hecton8.Core.GlobalRegistry.Player;
            _cachedSaveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            _cachedLocalization = Hecton8.Core.GlobalRegistry.Localization;
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

        private void Awake()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            CacheRegistryServicesCold();
            NormalizeSaveSlots();
            AutoResolve();
            EnsureBuilt();
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
            ReleaseSaveStatusBuffer();

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
            ReleaseSaveStatusBuffer();
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
                    _cachedLocalization = currentService as LocalizationManager;
                    if (_built)
                    {
                        RefreshSaveSlotButtonLabels();
                        RefreshLanguageSettingsStatus();
                        if (_activeSection == PauseSection.Saves)
                            RefreshSaveSectionState();
                    }
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

        public void Tick(float deltaTime)
        {
            if (!Application.isPlaying)
                return;

            if (_exitToMainMenuInFlight)
            {
                return;
            }

            if (_saveOperationInFlight)
                return;

            ConsumePlayerInputSignals();

            if (_pauseRequested)
            {
                _pauseRequested = false;
                HandlePauseRequested();
            }

            if (_cancelRequested)
            {
                _cancelRequested = false;
                HandleCancelRequested();
            }
        }

        public void UnscaledFastTick(float unscaledDeltaTime)
        {
            Tick(unscaledDeltaTime);
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
                    HandleSaveFailed(SaveEvents.ResolveSlotName(payload.SlotHash), SaveEvents.ResolveMessage(in payload));
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

            EnsureBuilt();
            EnsureEventSystem();

            _pauseRequested = false;
            _cancelRequested = false;
            _isOpen = true;
            RegisterOpenMenu();
            _activeSection = PauseSection.Main;

            if (pauseTimeScale)
            {
                ITickDispatcher dispatcher = GlobalRegistry.TickDispatcher;
                _cachedTimeDilationScalar = dispatcher != null
                    ? dispatcher.TimeDilationScalar
                    : 1f;
                PublishPauseState(true);
            }

            // TASK 33: Ensure correct input mode restoration
            GlobalRegistry.Input.SwitchToUIInput();
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

            // Audio feedback for pause menu close
            UIAudioFeedback.PlayPanelClose();

            ApplyClosedState(restorePlayerInput: true);
        }

        internal void RefreshSettingsPanel()
        {
            if (_controlsPanel != null)
                _controlsPanel.RefreshAllBindingsNow();

            RefreshLanguageSettingsStatus();
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

            if (restorePlayerInput && GlobalRegistry.Input.IsInitialized)
                GlobalRegistry.Input.SwitchToPlayerInput();

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

        private static bool ShouldRestorePlayerInputOnDisable()
        {
            if (!Application.isPlaying)
                return false;

            return GlobalRegistry.Input.IsInitialized;
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

            Canvas canvas = null;
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out canvas))
                    break;
            }

            if (canvas == null)
            {
                if (!TryGetComponent(out canvas))
                    canvas = gameObject.AddComponent<Canvas>();

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // High order to appear on top

                if (!TryGetComponent(out UnityEngine.UI.CanvasScaler scaler))
                    scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();

                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                if (!TryGetComponent(out UnityEngine.UI.GraphicRaycaster _))
                    gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

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
            Image shellBg = EnsureImage(shell.gameObject);
            shellBg.color = new Color(0.02f, 0.05f, 0.07f, 0.96f);
            shellBg.raycastTarget = false;

            RectTransform header = CreateRect(shell, "Header");
            Anchor(header, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -12f), new Vector2(-14f, 58f));
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

            _built = true;
        }

        private RectTransform ResolveOrCreateMenuRoot(RectTransform self)
        {
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

            string[] labels =
            {
                "RESUME EXPEDITION",
                "SAVE STATION",
                "FIELD GUIDE",
                "SETTINGS",
                "EXIT TO MAIN MENU",
                "QUIT APPLICATION"
            };

            Action[] actions =
            {
                Close,
                () => ShowSection(PauseSection.Saves),
                () => ShowSection(PauseSection.Help),
                () => ShowSection(PauseSection.Settings),
                ExitToMainMenu,
                QuitApplication
            };

            for (int i = 0; i < labels.Length; i++)
            {
                RectTransform btn = CreateButton(panel, "MainButton", labels[i], new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -88f - i * 58f), new Vector2(420f, 42f), actions[i]);

                if (i == 0)
                {
                    btn.TryGetComponent(out _mainResumeButton);
                    TmpTextNoAlloc.Set(GetText(btn, "Label"), "RESUME EXPEDITION");
                }
            }
        }

        private void BuildSavesPanel(RectTransform panel)
        {
            TmpTextNoAlloc.Set(CreateSectionTitle(panel, "SAVE STATION"), "SAVE STATION");
            CreateSectionSub(panel, "Manual save points. Use these before risky dives or major construction changes.")
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            _saveSlotButtons = new Button[SaveEvents.ManualSlotCount];
            for (int i = 0; i < SaveEvents.ManualSlotCount; i++)
            {
                string slotName = ResolveConfiguredSaveSlotName(i);
                RectTransform btn = CreateButton(panel, "SaveSlot", "WRITE SLOT",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -108f - i * 56f), new Vector2(420f, 40f),
                    () => SaveSlot(slotName));
                btn.TryGetComponent(out Button slotButton);
                _saveSlotButtons[i] = slotButton;

                if (i == 0)
                    _savesFirstButton = slotButton;

                TextMeshProUGUI label = GetText(btn, "Label");
                if (label != null)
                    label.alignment = TextAlignmentOptions.Center;
            }

            RefreshSaveSlotButtonLabels();

            _saveStatus = CreateText(panel, "SaveStatus", numericFont, 11f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_saveStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(22f, 66f), new Vector2(-22f, 22f));
            _saveStatus.color = Dim;
            ApplySaveStatusLiteral(_cachedAwaitingSaveCommand);

            _savesBackButton = CreateBackButton(panel, () => ShowSection(PauseSection.Main));
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

            _helpBackButton = CreateBackButton(panel, () => ShowSection(PauseSection.Main));
        }

        private void BuildSettingsPanel(RectTransform panel)
        {
            TmpTextNoAlloc.Set(CreateSectionTitle(panel, "SETTINGS"), "SETTINGS");
            CreateSectionSub(panel, ResolveLocalizedSpan(LocalizationKeys.SETTINGS_LANGUAGE_HINT,
                "Controls were moved out of the PDA. Rebind them here. Language cycling is also available."))
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            RectTransform languageButton = CreateButton(panel, "LanguageButton",
                ResolveLocalizedSpan(LocalizationKeys.SETTINGS_CYCLE_LANGUAGE, "CYCLE LANGUAGE"),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(420f, 38f), CycleLanguage);
            languageButton.TryGetComponent(out _settingsLanguageButton);

            _settingsLanguageStatus = CreateText(panel, "LanguageStatus", numericFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Center);
            Anchor(_settingsLanguageStatus.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(26f, -146f), new Vector2(-26f, -118f));
            _settingsLanguageStatus.color = Dim;

            RectTransform controlsRoot = CreateRect(panel, "ControlsPanel");
            Anchor(controlsRoot, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(22f, 160f), new Vector2(-22f, -80f));
            PauseControlsPanel controls = controlsRoot.gameObject.AddComponent<PauseControlsPanel>();
            controls.Configure(this, labelFont, labelFont);
            _controlsPanel = controls;

            _settingsBackButton = CreateBackButton(panel, () => ShowSection(PauseSection.Main));
            RefreshLanguageSettingsStatus();
        }

        private void ShowSection(PauseSection section)
        {
            PauseSection previousSection = _activeSection;
            _activeSection = section;

            // Audio feedback for section transitions (not on initial open)
            if (previousSection != section && _isOpen)
            {
                UIAudioFeedback.PlayPanelOpen();
            }

            SetPanelVisible(_mainPanelCanvasGroup, section == PauseSection.Main);
            SetPanelVisible(_savesPanelCanvasGroup, section == PauseSection.Saves);
            SetPanelVisible(_helpPanelCanvasGroup, section == PauseSection.Help);
            SetPanelVisible(_settingsPanelCanvasGroup, section == PauseSection.Settings);

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

            SelectDefaultButtonForSection(section);
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

            _ = SaveSlotAsync(slotName);
        }

        /// <summary>
        /// Async save operation with proper exception handling and Awaitable-based lifetime safety.
        /// </summary>
        private async Awaitable SaveSlotAsync(string slotName)
        {
            string upperSlotName = ResolveSlotDisplayName(slotName);

            if (_saveStatus != null)
                ApplySaveStatusText(_cachedWriting, upperSlotName, "...");

            ISaveService saveService = _cachedSaveService;
            if (saveService == null)
            {
                if (_saveStatus != null)
                    ApplySaveStatusLiteral(_cachedSaveServiceUnavailable);

                int messageLength = CopyLocalizedSpanToModalBuffer(
                    LocalizationKeys.ERROR_SAVE_MANAGER_UNAVAILABLE,
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

                await saveService.SaveGameAsync(slotName);
            }
            catch (Exception ex)
            {
                LogSaveSlotFailed(slotName, ex);
                if (_saveStatus != null)
                    ApplySaveStatusText(string.Empty, upperSlotName, _cachedFailedTerminal);

                int messageLength = BuildSaveModalMessage(
                    LocalizationKeys.ERROR_SAVE_CRASHED_MESSAGE,
                    "Save operation crashed.",
                    slotName,
                    default,
                    false);

                ModalWindow.ShowWithCustomLabels(
                    "Save Error",
                    _modalMessageBuffer,
                    messageLength,
                    () => SaveSlot(slotName), // Retry
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
            Debug.LogError("[PauseMenuController] Save failed.");
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
            RegisterMainMenuCleanup(mainMenuSceneName);

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

            sceneService.LoadScene(mainMenuSceneName);
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
            Debug.LogError("[PauseMenuController] Fatal pause-menu state.");
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
            _pendingMainMenuSceneName = sceneName ?? string.Empty;

            if (_mainMenuCleanupHookRegistered)
                return;

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
            // TASK 33: Ensure all settings are saved before Application.Quit()
            // Save UserOptions (input overrides, etc.)
            if (Hecton8.Core.GlobalRegistry.UserOptions != null)
            {
                Hecton8.Core.GlobalRegistry.UserOptions.Save();
            }

            // SettingsManager saves settings individually via PlayerPrefs
            // PlayerPrefs.Save() is called by UserOptionsPersistence.Save()
            // No additional save needed here

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

        private void SelectDefaultButtonForSection(PauseSection section)
        {
            if (!_isOpen)
                return;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            Button target = GetDefaultButtonForSection(section);
            if (target == null)
                return;

            GameObject targetObject = target.gameObject;
            if (targetObject == null || !targetObject.activeInHierarchy)
                return;

            if (eventSystem.currentSelectedGameObject == targetObject)
                return;

            eventSystem.SetSelectedGameObject(targetObject);
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
            if (saveService == null)
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
            if (_saveSlotButtons == null || saveSlots == null)
                return;

            int count = math.min(_saveSlotButtons.Length, SaveEvents.ManualSlotCount);
            for (int i = 0; i < count; i++)
            {
                Button button = _saveSlotButtons[i];
                if (button == null)
                    continue;

                TextMeshProUGUI label = GetText(button.transform, "Label");
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
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveStatusText(string.Empty, ResolveSlotDisplayName(slotName), _cachedWritten);

            if (_activeSection == PauseSection.Saves)
                SelectDefaultButtonForSection(PauseSection.Saves);
        }

        private void HandleSaveFailed(string slotName, string error)
        {
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveFailedStatusText(slotName, error);

            int messageLength = BuildSaveModalMessage(
                LocalizationKeys.ERROR_SAVE_FAILED_MESSAGE,
                "Failed to save.",
                slotName,
                error,
                true);

            ModalWindow.ShowWithCustomLabels(
                "Save Failed",
                _modalMessageBuffer,
                messageLength,
                () => SaveSlot(slotName),
                null,
                "Retry",
                "Cancel");

            if (_activeSection == PauseSection.Saves)
                SelectDefaultButtonForSection(PauseSection.Saves);
        }

        private void CycleLanguage()
        {
            LocalizationManager localization = _cachedLocalization;
            if (localization == null)
            {
                RefreshLanguageSettingsStatus();
                return;
            }

            localization.CycleLanguage();
            RefreshLanguageSettingsStatus();
        }

        private void RefreshLanguageSettingsStatus()
        {
            if (_settingsLanguageStatus == null)
                return;

            LocalizationManager localization = _cachedLocalization;
            if (localization == null)
            {
                SetSettingsLanguageStatus(ResolveLocalizedSpan(
                    LocalizationKeys.SETTINGS_LANGUAGE_OWNER_UNAVAILABLE,
                    "LANGUAGE OWNER UNAVAILABLE."));
                return;
            }

            ApplyFormattedSettingsLanguageStatus(
                ResolveLocalizedSpan(
                    LocalizationKeys.SETTINGS_CURRENT_LANGUAGE,
                    "CURRENT LANGUAGE: {0}"),
                GetLanguageDisplayName(localization.CurrentLanguage).AsSpan());
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
                default: return "English";
            }
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            LocalizationManager manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, ReadOnlySpan<char> fallback)
        {
            LocalizationManager manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallback)
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

        private int CopyLocalizedSpanToModalBuffer(string key, ReadOnlySpan<char> fallback)
        {
            return CopySpanToBuffer(ResolveLocalizedSpan(key, fallback), _modalMessageBuffer, 0);
        }

        private int BuildSaveModalMessage(
            string localizationKey,
            ReadOnlySpan<char> fallback,
            string slotName,
            string error,
            bool appendError)
        {
            if (_modalMessageBuffer == null)
                return 0;

            int cursor = 0;
            cursor += CopySpanToBuffer(ResolveLocalizedSpan(localizationKey, fallback), _modalMessageBuffer, cursor);
            cursor += CopySpanToBuffer(" // ".AsSpan(), _modalMessageBuffer, cursor);
            cursor += CopySpanToBuffer(ResolveSlotDisplayName(slotName).AsSpan(), _modalMessageBuffer, cursor);

            if (appendError)
            {
                cursor += CopySpanToBuffer("\n".AsSpan(), _modalMessageBuffer, cursor);
                if (!CopyUpperAsciiStringToBuffer(error, _modalMessageBuffer, ref cursor))
                    cursor += CopySpanToBuffer(_cachedUnknownErrorModal.AsSpan(), _modalMessageBuffer, cursor);
            }

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
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null || _root == null)
                return;

            if (selected.transform.IsChildOf(_root))
                eventSystem.SetSelectedGameObject(null);
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem)); // COLD ALLOC: GameObject[1] — pause-menu fallback event system root — owner: PauseMenuController
                eventSystemRoot.hideFlags = HideFlags.DontSave;
                eventSystemRoot.TryGetComponent(out eventSystem);
            }

            if (eventSystem == null)
                return;

            eventSystem.TryGetComponent(out StandaloneInputModule legacyInputModule);
            if (!eventSystem.TryGetComponent(out InputSystemUIInputModule inputSystemModule))
            {
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
        }

        private void BindInputActions()
        {
            INativeInputManagerRuntime inputManager = GlobalRegistry.NativeInputRuntime;
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

        private void ConsumePlayerInputSignals()
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
