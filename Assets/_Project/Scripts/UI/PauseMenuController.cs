using System;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Signals;
using Hecton8.Input;
using Hecton8.Optimization;
using Hecton8.SaveSystem;
using Hecton8.Crafting;
using Hecton.Localization;
using Hecton.UI.MainMenu;
using TMPro;
using Unity.Collections;
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
    public sealed class PauseMenuController : MonoBehaviour, ITickable, IUnscaledFastTickable, IUpdatable, ISaveEventListener, ILocalizationLanguageChangedListener
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
        private InputManager _inputManager;

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
                Frame = unchecked((uint)Time.frameCount),
                Sequence = _pauseSignalSequence,
                Paused = paused ? (byte)1 : (byte)0,
                Flags = 0,
                RestoreScalar = restoreScalar
            };
            GlobalSignals.Publish(in signal);

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
        private static readonly string _cachedSaveManagerUnavailable = "SAVE MANAGER UNAVAILABLE.";
        private static readonly string _cachedSaveInProgress = "SAVE ALREADY IN PROGRESS.";
        private static readonly string _cachedAwaitingSaveCommand = "Awaiting save command.";
        private static readonly string _cachedPreparingSceneTransition = "Preparing scene transition...";
        private static readonly string _cachedWritePrefix = "WRITE ";

        // Simple cache for ToUpperInvariant to reduce allocations in UI strings
        private static readonly string[] _cachedUpperStrings = new string[16];
        // COLD ALLOC: string[3] — fixed save-slot uppercase labels reused across pause-menu save status updates — owner: PauseMenuController
        private static readonly string[] _cachedUpperSlotDisplayNames = new string[SaveEvents.ManualSlotCount];
        private static GameLanguage _cachedUpperSlotLanguage = (GameLanguage)(-1);

        private static string CachedToUpperInvariant(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            int hash = input.GetHashCode() & 0xF;
            string cached = _cachedUpperStrings[hash];
            if (cached != null && string.Equals(cached, input, StringComparison.OrdinalIgnoreCase))
                return cached;

            string upper = input.ToUpperInvariant();
            _cachedUpperStrings[hash] = upper;
            return upper;
        }

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

        private void Awake()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

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
            ReleaseSaveStatusBuffer();
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
            if (saveSlots == null || saveSlots.Length != SaveEvents.ManualSlotCount)
                saveSlots = new string[SaveEvents.ManualSlotCount]; // COLD ALLOC: string[3] — repair stale serialized save slot config — owner: PauseMenuController

            for (int i = 0; i < SaveEvents.ManualSlotCount; i++)
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
                    HandleSaveStarted(SaveEvents.ResolveSlotName(in payload.SlotName));
                    return;

                case SaveEventType.SaveCompleted:
                    HandleSaveCompleted(SaveEvents.ResolveSlotName(in payload.SlotName));
                    return;

                case SaveEventType.SaveFailed:
                    FixedString128Bytes failedMessage = payload.Message;
                    HandleSaveFailed(SaveEvents.ResolveSlotName(in payload.SlotName), in failedMessage);
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
                CraftingEvents.RaiseFabricatorClosed();
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
            ClearUpperSlotDisplayCache();
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
                    playerPDA = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerPDA != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerPDA : playerTransform.GetComponent<PlayerPDA>());
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

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.GetComponent<Canvas>();
                if (canvas == null)
                    canvas = gameObject.AddComponent<Canvas>();

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000; // High order to appear on top

                UnityEngine.UI.CanvasScaler scaler = gameObject.GetComponent<UnityEngine.UI.CanvasScaler>();
                if (scaler == null)
                    scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();

                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                UnityEngine.UI.GraphicRaycaster raycaster = gameObject.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                if (raycaster == null)
                    raycaster = gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            _canvasGroup = _root.gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = _root.gameObject.AddComponent<CanvasGroup>();

            _background = _root.gameObject.GetComponent<Image>();
            if (_background == null)
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
            _headerTitle.SetText("MISSION PAUSE");

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
            _footerHint.SetText("ESC = back / resume  |  SETTINGS hosts controls and rebinds");

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
            RectTransform root = go.GetComponent<RectTransform>();
            root.SetParent(self, false);
            root.localScale = Vector3.one;
            return root;
        }

        private void NeutralizeHostCanvasArtifacts(RectTransform self)
        {
            if (ReferenceEquals(_root, self))
                return;

            CanvasGroup hostCanvasGroup = self.GetComponent<CanvasGroup>();
            if (hostCanvasGroup != null)
            {
                hostCanvasGroup.alpha = 1f;
                hostCanvasGroup.interactable = false;
                hostCanvasGroup.blocksRaycasts = false;
            }

            Image hostImage = self.GetComponent<Image>();
            if (hostImage != null)
            {
                hostImage.enabled = false;
                hostImage.raycastTarget = false;
            }
        }

        private void BuildMainPanel(RectTransform panel)
        {
            TextMeshProUGUI title = CreateSectionTitle(panel, "MISSION CONTROL");
            title.SetText("MISSION CONTROL");

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
                RectTransform btn = CreateButton(panel, $"MainButton_{i}", labels[i], new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -88f - i * 58f), new Vector2(420f, 42f), actions[i]);

                if (i == 0)
                {
                    _mainResumeButton = btn.GetComponent<Button>();
                    GetText(btn, "Label")?.SetText("RESUME EXPEDITION");
                }
            }
        }

        private void BuildSavesPanel(RectTransform panel)
        {
            CreateSectionTitle(panel, "SAVE STATION").SetText("SAVE STATION");
            CreateSectionSub(panel, "Manual save points. Use these before risky dives or major construction changes.")
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            _saveSlotButtons = new Button[saveSlots.Length];
            for (int i = 0; i < saveSlots.Length; i++)
            {
                string slotName = saveSlots[i];
                RectTransform btn = CreateButton(panel, $"SaveSlot_{i}", string.Concat(_cachedWritePrefix, GetUpperSlotDisplayName(slotName)),
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -108f - i * 56f), new Vector2(420f, 40f),
                    () => SaveSlot(slotName));
                Button slotButton = btn.GetComponent<Button>();
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
            CreateSectionTitle(panel, "FIELD GUIDE").SetText("FIELD GUIDE");

            TextMeshProUGUI body = CreateText(panel, "HelpBody", numericFont, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Anchor(body.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(28f, 68f), new Vector2(-28f, -74f));
            body.color = Dim;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.SetText(
                "CORE INPUTS\n" +
                "TAB  // PDA shell\n" +
                "I    // inventory direct open\n" +
                "1-4  // quick slot arm/swap\n" +
                "LMB/RMB // primary / secondary tool action\n\n" +
                "MISSION RHYTHM\n" +
                "1. Scan and classify unknowns.\n" +
                "2. Repair and stabilize critical infrastructure.\n" +
                "3. Keep loadout aligned with cargo before committing to depth.\n" +
                "4. Save before hazardous traversal, fauna contact, or base edits.");

            _helpBackButton = CreateBackButton(panel, () => ShowSection(PauseSection.Main));
        }

        private void BuildSettingsPanel(RectTransform panel)
        {
            CreateSectionTitle(panel, "SETTINGS").SetText("SETTINGS");
            CreateSectionSub(panel, ResolveLocalized(LocalizationKeys.SETTINGS_LANGUAGE_HINT,
                "Controls were moved out of the PDA. Rebind them here. Language cycling is also available."))
                .rectTransform.anchoredPosition = new Vector2(0f, -42f);

            RectTransform languageButton = CreateButton(panel, "LanguageButton",
                ResolveLocalized(LocalizationKeys.SETTINGS_CYCLE_LANGUAGE, "CYCLE LANGUAGE"),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -98f), new Vector2(420f, 38f), CycleLanguage);
            _settingsLanguageButton = languageButton.GetComponent<Button>();

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
                    _headerSub.SetText("resume, save, inspect field guidance, or move into settings");
                    break;
                case PauseSection.Saves:
                    _headerSub.SetText("manual persistence via SaveManager");
                    RefreshSaveSlotButtonLabels();
                    RefreshSaveSectionState();
                    break;
                case PauseSection.Help:
                    _headerSub.SetText("compact operational reference for current tool and inventory loop");
                    break;
                case PauseSection.Settings:
                    _headerSub.SetText("controls and interaction tuning are managed here now");
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
            string upperSlotName = GetUpperSlotDisplayName(slotName);

            if (_saveStatus != null)
                ApplySaveStatusText(_cachedWriting, upperSlotName, "...");

            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            if (saveManager == null)
            {
                if (_saveStatus != null)
                    ApplySaveStatusLiteral(_cachedSaveManagerUnavailable);

                // Localized error message
                LocalizationManager loc = Hecton8.Core.GlobalRegistry.Localization;
                string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_MANAGER_UNAVAILABLE) : "Save Error";
                string message = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_MANAGER_UNAVAILABLE) : "Save system is unavailable. Cannot save game.";

                ModalWindow.ShowWithCustomLabels(
                    title,
                    message,
                    null,
                    null,
                    ResolveLocalized(LocalizationKeys.UI_OK, "OK"),
                    null);
                return;
            }

            try
            {
                if (saveManager.IsBusy)
                {
                    if (_saveStatus != null)
                        ApplySaveStatusLiteral(_cachedSaveInProgress);
                    return;
                }

                await saveManager.SaveGameAsync(slotName);
            }
            catch (Exception ex)
            {
                LogSaveSlotFailed(slotName, ex);
                if (_saveStatus != null)
                    ApplySaveStatusText(string.Empty, upperSlotName, _cachedFailedTerminal);

                // Localized error message
                LocalizationManager loc = Hecton8.Core.GlobalRegistry.Localization;
                string displaySlotName = BuildSlotDisplayName(loc, slotName);
                string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_CRASHED_TITLE) : "Save Error";
                string message = loc != null 
                    ? loc.GetFormatted(LocalizationKeys.ERROR_SAVE_CRASHED_MESSAGE, displaySlotName)
                    : $"Save operation crashed for {displaySlotName}.\n\nRetry?";

                // Show retry modal on exception
                ModalWindow.ShowWithCustomLabels(
                    title,
                    message,
                    () => SaveSlot(slotName), // Retry
                    null, // Cancel just closes modal
                    ResolveLocalized(LocalizationKeys.UI_RETRY, "Retry"),
                    "Cancel");
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSaveSlotFailed(string slotName, Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[PauseMenuController] Save failed for '{slotName}': {exception.Message}");
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

        private void ApplySaveFailedStatusText(string slotName, in FixedString128Bytes error)
        {
            if (_saveStatus == null)
            {
                _hasSaveStatusText = false;
                return;
            }

            TryAcquireSaveStatusBuffer();
            char[] buffer = _saveStatusBufferLease.IsValid ? _saveStatusBufferLease.Buffer : _saveStatusFallbackBuffer;
            int cursor = 0;
            cursor += CopyStringToBuffer(GetUpperSlotDisplayName(slotName), buffer, cursor);
            cursor += CopyStringToBuffer(_cachedFailed, buffer, cursor);

            if (!CopyFixedStringUpperAsciiToBuffer(in error, buffer, ref cursor))
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

        private static bool CopyFixedStringUpperAsciiToBuffer(
            in FixedString128Bytes value,
            char[] buffer,
            ref int offset)
        {
            if (value.Length <= 0)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] > (byte)0x7F)
                    return false;
            }

            if (buffer == null || offset >= buffer.Length)
                return true;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            for (int i = 0; i < safeLength; i++)
            {
                byte raw = value[i];
                if (raw >= (byte)'a' && raw <= (byte)'z')
                    raw = (byte)(raw - 32);

                buffer[offset + i] = (char)raw;
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
                _headerTitle.SetText("RETURNING TO MAIN MENU");

            if (_headerSub != null)
                _headerSub.SetText("asynchronous scene handoff in progress");

            if (_footerHint != null)
                _footerHint.SetText("loading menu shell and releasing world memory");

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
                _headerTitle.SetText("MISSION PAUSE");

            ShowSection(PauseSection.Main);

            if (_footerHint != null)
                _footerHint.SetText("ESC = back / resume  |  SETTINGS hosts controls and rebinds");

            if (_saveStatus != null)
                ApplySaveStatusLiteral(message);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[PauseMenuController] {message}");
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

            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group == null)
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
            text.SetText(label);

            return rect;
        }

        private Button CreateBackButton(Transform parent, Action onClick)
        {
            RectTransform buttonRoot = CreateButton(parent, "BackButton", "BACK", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-108f, 28f), new Vector2(180f, 34f), onClick);
            return buttonRoot.GetComponent<Button>();
        }

        private TextMeshProUGUI CreateSectionTitle(Transform parent, string value)
        {
            TextMeshProUGUI text = CreateText(parent, $"{value}_Title", labelFont, 18f, FontStyles.Bold, TextAlignmentOptions.Left);
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -18f), new Vector2(-24f, 24f));
            text.color = Primary;
            text.SetText(value);
            return text;
        }

        private TextMeshProUGUI CreateSectionSub(Transform parent, string value)
        {
            TextMeshProUGUI text = CreateText(parent, "Subtitle", labelFont, 10.5f, FontStyles.Normal, TextAlignmentOptions.Left);
            Anchor(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -42f), new Vector2(-24f, 18f));
            text.color = DimLow;
            text.SetText(value);
            return text;
        }

        private static TextMeshProUGUI GetText(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<TextMeshProUGUI>() : null;
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
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image EnsureImage(GameObject target)
        {
            Image image = target.GetComponent<Image>();
            if (image == null)
                image = target.AddComponent<Image>();
            return image;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
            float size, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.GetComponent<RectTransform>();
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
            // TASK 31: SaveManager null check with user-facing error message
            SaveManager saveManager = Hecton8.Core.GlobalRegistry.SaveRuntime;
            
            // If SaveManager is unavailable, disable all save buttons and display error
            if (saveManager == null)
            {
                SetSaveButtonsInteractable(false);
                
                if (_saveStatus != null)
                    ApplySaveStatusLiteral(_cachedSaveManagerUnavailable);
                
                return;
            }
            
            bool isBusy = _saveOperationInFlight || saveManager.IsBusy;
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

            int count = math.min(_saveSlotButtons.Length, saveSlots.Length);
            for (int i = 0; i < count; i++)
            {
                Button button = _saveSlotButtons[i];
                if (button == null)
                    continue;

                TextMeshProUGUI label = GetText(button.transform, "Label");
                if (label == null)
                    continue;

                ApplyTemplatedText(label, _cachedWritePrefix, GetUpperSlotDisplayName(saveSlots[i]), string.Empty, _saveSlotLabelBuffer);
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
                ApplySaveStatusText(_cachedWriting, GetUpperSlotDisplayName(slotName), "...");
        }

        private void HandleSaveCompleted(string slotName)
        {
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveStatusText(string.Empty, GetUpperSlotDisplayName(slotName), _cachedWritten);

            if (_activeSection == PauseSection.Saves)
                SelectDefaultButtonForSection(PauseSection.Saves);
        }

        private void HandleSaveFailed(string slotName, in FixedString128Bytes error)
        {
            _saveOperationInFlight = false;
            SetSaveButtonsInteractable(true);

            if (_saveStatus != null)
                ApplySaveFailedStatusText(slotName, in error);

            LocalizationManager loc = Hecton8.Core.GlobalRegistry.Localization;
            string normalizedError = ResolveSaveErrorForModal(in error);
            string displaySlotName = BuildSlotDisplayName(loc, slotName);
            string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_FAILED_TITLE) : "Save Failed";
            string message = loc != null
                ? loc.GetFormatted(LocalizationKeys.ERROR_SAVE_FAILED_MESSAGE, displaySlotName, normalizedError)
                : $"Failed to save to {displaySlotName}.\n\n{normalizedError}\n\nRetry?";

            ModalWindow.ShowWithCustomLabels(
                title,
                message,
                () => SaveSlot(slotName),
                null,
                ResolveLocalized(LocalizationKeys.UI_RETRY, "Retry"),
                "Cancel");

            if (_activeSection == PauseSection.Saves)
                SelectDefaultButtonForSection(PauseSection.Saves);
        }

        private static string ResolveSaveErrorForModal(in FixedString128Bytes error)
        {
            if (error.Length <= 0)
                return _cachedUnknownErrorModal;

            FixedString128Bytes errorCopy = error;
            return errorCopy.ConvertToString();
        }

        private void CycleLanguage()
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
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

            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            if (localization == null)
            {
                SetSettingsLanguageStatus(ResolveLocalized(
                    LocalizationKeys.SETTINGS_LANGUAGE_OWNER_UNAVAILABLE,
                    "LANGUAGE OWNER UNAVAILABLE."));
                return;
            }

            ApplyFormattedSettingsLanguageStatus(
                ResolveLocalized(
                    LocalizationKeys.SETTINGS_CURRENT_LANGUAGE,
                    "CURRENT LANGUAGE: {0}"),
                CachedToUpperInvariant(GetLanguageDisplayName(localization.CurrentLanguage)));
        }

        private void SetSettingsLanguageStatus(string value)
        {
            if (_settingsLanguageStatus == null)
                return;

            ApplyTemplatedText(_settingsLanguageStatus, string.Empty, value, string.Empty, _settingsLanguageBuffer);
        }

        private void ApplyFormattedSettingsLanguageStatus(string template, string replacement)
        {
            if (_settingsLanguageStatus == null)
                return;

            int placeholderIndex = string.IsNullOrEmpty(template)
                ? -1
                : template.IndexOf("{0}", StringComparison.Ordinal);
            if (placeholderIndex < 0)
            {
                ApplyTemplatedText(_settingsLanguageStatus, string.Empty, template, string.Empty, _settingsLanguageBuffer);
                return;
            }

            ReadOnlySpan<char> templateSpan = template.AsSpan();
            ReadOnlySpan<char> prefix = placeholderIndex > 0
                ? templateSpan.Slice(0, placeholderIndex)
                : ReadOnlySpan<char>.Empty;
            ReadOnlySpan<char> suffix = placeholderIndex + 3 < templateSpan.Length
                ? templateSpan.Slice(placeholderIndex + 3)
                : ReadOnlySpan<char>.Empty;
            ApplyTemplatedText(_settingsLanguageStatus, prefix, replacement.AsSpan(), suffix, _settingsLanguageBuffer);
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

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
        }

        private static string GetUpperSlotDisplayName(string slotName)
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            GameLanguage language = localization != null ? localization.CurrentLanguage : (GameLanguage)(-1);
            int slotIndex = SaveEvents.ResolveKnownSlotIndex(slotName);
            if (slotIndex < 0 || slotIndex >= _cachedUpperSlotDisplayNames.Length)
                return CachedToUpperInvariant(BuildSlotDisplayName(localization, slotName));

            if (_cachedUpperSlotLanguage != language)
            {
                ClearUpperSlotDisplayCache();
                _cachedUpperSlotLanguage = language;
            }

            string cached = _cachedUpperSlotDisplayNames[slotIndex];
            if (!string.IsNullOrEmpty(cached))
                return cached;

            string upper = CachedToUpperInvariant(BuildSlotDisplayName(localization, slotName));
            _cachedUpperSlotDisplayNames[slotIndex] = upper;
            return upper;
        }

        private static void ClearUpperSlotDisplayCache()
        {
            for (int i = 0; i < _cachedUpperSlotDisplayNames.Length; i++)
                _cachedUpperSlotDisplayNames[i] = null;
        }

        private static string BuildSlotDisplayName(LocalizationManager loc, string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return "?";

            string slotPrefix = loc != null
                ? loc.GetOrFallback(loc.CurrentLanguage, LocalizationKeys.SLOT_PREFIX, "SLOT")
                : "SLOT";

            return string.Concat(slotPrefix, " ", SaveEvents.ResolveSlotNumber(slotName));
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
            if (label == null || buffer == null || buffer.Length == 0)
                return;

            int cursor = 0;
            cursor += CopySpanToBuffer(prefix, buffer, cursor);
            cursor += CopySpanToBuffer(value, buffer, cursor);
            cursor += CopySpanToBuffer(suffix, buffer, cursor);
            label.SetCharArray(buffer, 0, cursor);
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
                eventSystem = eventSystemRoot.GetComponent<EventSystem>();
            }

            if (eventSystem == null)
                return;

            StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
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

            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (inputManager != null)
                inputManager.TryConfigureUiInputModule(inputSystemModule);
        }

        private void BindInputActions()
        {
            InputManager inputManager = GlobalRegistry.NativeInputManager;
            if (ReferenceEquals(_inputManager, inputManager))
                return;

            UnbindInputActions();
            _inputManager = inputManager;
            if (_inputManager == null)
                return;

            _inputManager.OnPause += HandlePauseActionPerformed;
            _inputManager.OnCancel += HandleCancelActionPerformed;
        }

        private void UnbindInputActions()
        {
            if (_inputManager != null)
            {
                _inputManager.OnPause -= HandlePauseActionPerformed;
                _inputManager.OnCancel -= HandleCancelActionPerformed;
            }

            _inputManager = null;
            _pauseRequested = false;
            _cancelRequested = false;
        }

        private void HandlePauseActionPerformed()
        {
            _pauseRequested = true;
        }

        private void HandleCancelActionPerformed()
        {
            _cancelRequested = true;
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
