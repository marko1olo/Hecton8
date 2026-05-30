using System;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.SaveSystem;
using Hecton8.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Events;
using Unity.Mathematics;
using UnityEngine.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Controller for the main menu scene. Manages panel transitions,
    /// save slot generation, and async scene loading.
    /// All UI text is driven through LocalizationManager.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour, ITickable, IUpdatable, ISaveEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private enum PanelTransitionState
        {
            None,
            FadingOut,
            FadingIn
        }

        [Header("=== PANELS (CanvasGroup) ===")]
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private CanvasGroup saveLoadGroup;
        [SerializeField] private CanvasGroup settingsGroup;
        [SerializeField] private CanvasGroup loadingGroup;

        [Header("=== SAVE SLOTS ===")]
        [SerializeField] private Transform slotsContainer;

        [Header("=== MAIN MENU BUTTONS ===")]
        [SerializeField] private Button btnNewGame;
        [SerializeField] private Button btnLoadGame;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnQuit;

        [Header("=== BUTTON LABELS (auto-localized) ===")]
        [SerializeField] private TMP_Text labelNewGame;
        [SerializeField] private TMP_Text labelLoadGame;
        [SerializeField] private TMP_Text labelSettings;
        [SerializeField] private TMP_Text labelQuit;

        [Header("=== SAVE/LOAD PANEL ===")]
        [SerializeField] private Button btnBackFromSaveLoad;

        [Header("=== SETTINGS PANEL ===")]
        [SerializeField] private Button btnBackFromSettings;

        [Header("=== LOADING SCREEN ===")]
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TMP_Text loadingPercentText;
        [SerializeField] private LoadingTipsDisplay loadingTips;

        [Header("=== CINEMATIC TRANSITION ===")]
        [SerializeField, Tooltip("Scene-authored main-menu camera panned during the menu-to-world transition.")]
        private Camera mainMenuCamera;

        [Header("=== CONFIG ===")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private string targetSceneName = "02_HECTON_WORLD";
        [SerializeField] private string newGameTargetSceneName = "01_ORBIT";

        private const int SlotCount = SaveEvents.ManualSlotCount;
        private const float CancelInputDebounceSeconds = 0.35f;
        private const string UnknownSaveEventError = "Unknown error";
        private static readonly string[] SlotNames =
        {
            SaveEvents.ResolveManualSlotName(0),
            SaveEvents.ResolveManualSlotName(1),
            SaveEvents.ResolveManualSlotName(2)
        };
        private static readonly string[] SlotDisplayNames =
        {
            "SLOT 1",
            "SLOT 2",
            "SLOT 3"
        };

        private bool _isTransitioning;
        private bool _isSceneLoadInFlight;
        private bool _registeredToTickManager;
        private bool _settingsAvailable;
        private bool _refreshSelectionRequested;
        private bool _isSaveLoadBusy;
        private bool _lastLoadUsedBackup;
        private int _lastLoadingPercent = -1;
        private readonly char[] _loadingPercentBuffer = new char[32]; // COLD ALLOC: loading percent TMP staging buffer - owner: MainMenuController
        private readonly char[] _loadingPercentTemplateBuffer = new char[32]; // COLD ALLOC: loading percent localized template staging buffer - owner: MainMenuController
        private readonly char[] _modalMessageBuffer = new char[256]; // COLD ALLOC: main-menu modal message staging buffer copied directly into TMP - owner: MainMenuController
        private int _loadingPercentTemplateLength;
        private float _lastUnscaledTickTime;
        private float _cancelInputBlockedUntil;
        private float _transitionElapsed;
        private float _transitionStartAlpha;
        private SaveManager _saveManager;
        private SaveSlotUI[] _slotUIs;
        private bool[] _slotButtonAvailability;
        private CanvasGroup _transitionFromPanel;
        private CanvasGroup _transitionToPanel;
        private CanvasGroup _currentPanel;
        private INativeInputManagerRuntime _inputManager;
        private bool _cancelRequested;
        private uint _lastPlayerInputSignalSequence;
        private PanelTransitionState _panelTransitionState;
        private UnityAction _newGameClickAction;
        private UnityAction _loadGameClickAction;
        private UnityAction _settingsClickAction;
        private UnityAction _quitClickAction;
        private UnityAction _backFromSaveLoadClickAction;
        private UnityAction _backFromSettingsClickAction;
        private bool _runtimeBindingsReady;
        private bool _inputRoutingReady;
        private bool _menuInputBound;
        private bool _registeredHotSwapListener;
        private EventSystem _cachedEventSystem;
        private InputSystemUIInputModule _cachedUiInputModule;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;


        private void Awake()
        {
            if (!BootstrapRouteEnforcer.EnsureBootstrapRuntimeRoute(
                    gameObject.scene.name,
                    nameof(MainMenuController)))
            {
                enabled = false;
                return;
            }

            BootstrapStatus.MarkMainMenuReached();
            EnsureRuntimeMenuBindings(resetPanelState: true);
            BlockCancelInputBriefly();
        }

        private void Start()
        {
            _saveManager = Hecton8.Core.GlobalRegistry.Save as SaveManager;
            TryRegisterToTickManager();

#if UNITY_EDITOR
            if (_saveManager == null)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[MainMenuController] Hecton8.Core.GlobalRegistry.Save is null. " +
                    "Save/Load features will be unavailable. " +
                    "Ensure SaveManager exists in scene or is DontDestroyOnLoad.");
            }
#endif
        }

        private void OnEnable()
        {
            EnsureRuntimeMenuBindings(resetPanelState: _currentPanel == null);
            TryRegisterHotSwapListener();
            CacheInputManagerCold(GlobalRegistry.NativeInputRuntime);
            TryRegisterToTickManager();
            _lastUnscaledTickTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            BlockCancelInputBriefly();
            MainMenuInputRoutingGuard.EnsureInputSystemEventRouting();
            CacheMenuInputRoutingCold();
            BindMenuInput();
            BaselineCancelInputSignalSequence();
            RefreshMenuInputRoutingReadyFromCache();
            LocalizationEvents.RegisterLanguageListener(this);
            
            // Subscribe to save/load events for UI feedback
            SaveEvents.Register(this);
            
            RefreshLocalizedTexts();
            RefreshSelectionIfNeeded();
        }

        private void OnDisable()
        {
            UnbindMenuInput();
            TryUnregisterHotSwapListener();

            // TASK 31: Null-safe event unsubscription in OnDisable
            if (Hecton8.Core.GlobalRegistry.LocalizationText != null)
                LocalizationEvents.UnregisterLanguageListener(this);
            
            // Unsubscribe from save/load events with null checks
            SaveEvents.Unregister(this);
            
            UnregisterFromTickManager();
            _lastUnscaledTickTime = 0f;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.NativeInputManagerRuntime)
            {
                CacheInputManagerCold(currentService as INativeInputManagerRuntime);
                if (isActiveAndEnabled)
                {
                    MainMenuInputRoutingGuard.EnsureInputSystemEventRouting();
                    CacheMenuInputRoutingCold();
                    BindMenuInput();
                    RefreshMenuInputRoutingReadyFromCache();
                }
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registeredToTickManager = false;
                TryRegisterToTickManager();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                _saveManager = currentService as SaveManager;
                RequestSelectionRefresh();
            }
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)

        {

            OnLanguageChanged((GameLanguage)payload.Language);

        }


        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshLocalizedTexts();
            RefreshLoadingLocalization();
        }

        public void OnSaveEvent(in SaveEventPayload payload)
        {
            switch (payload.Type)
            {
                case SaveEventType.SaveStarted:
                    OnSaveStarted(in payload);
                    return;

                case SaveEventType.SaveCompleted:
                    OnSaveCompleted(in payload);
                    return;

                case SaveEventType.SaveFailed:
                    OnSaveFailed(SaveEvents.ResolveSlotName(payload.SlotHash), SaveEvents.ResolveMessage(in payload));
                    return;

                case SaveEventType.LoadStarted:
                    OnLoadStarted(in payload);
                    return;

                case SaveEventType.LoadCompleted:
                    OnLoadCompleted(in payload);
                    return;

                case SaveEventType.LoadFailed:
                    OnLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), SaveEvents.ResolveMessage(in payload));
                    return;
            }
        }

        private void RefreshLocalizedTexts()
        {
            ILocalizationTextReadModel loc = Hecton8.Core.GlobalRegistry.LocalizationText;
            if (loc == null)
                return;

            ConfigureAdaptiveLabels();
            if (labelNewGame != null) TmpTextNoAlloc.Set(labelNewGame, ResolveLocalizedSpan(loc, LocalizationKeys.MENU_NEW_GAME, "NEW GAME"));
            if (labelLoadGame != null) TmpTextNoAlloc.Set(labelLoadGame, ResolveLocalizedSpan(loc, LocalizationKeys.MENU_LOAD_GAME, "LOAD GAME"));
            if (labelSettings != null) TmpTextNoAlloc.Set(labelSettings, ResolveLocalizedSpan(loc, LocalizationKeys.MENU_SETTINGS, "SETTINGS"));
            if (labelQuit != null) TmpTextNoAlloc.Set(labelQuit, ResolveLocalizedSpan(loc, LocalizationKeys.MENU_QUIT, "QUIT"));
        }

        private void ConfigureAdaptiveLabels()
        {
            ConfigureAdaptiveLabel(labelNewGame);
            ConfigureAdaptiveLabel(labelLoadGame);
            ConfigureAdaptiveLabel(labelSettings);
            ConfigureAdaptiveLabel(labelQuit);
        }

        private static void ConfigureAdaptiveLabel(TMP_Text label)
        {
            if (label == null)
                return;

            LocalizedTMPAutoSizer.Configure(
                label,
                label.fontSize * 0.72f,
                label.fontSize,
                TextOverflowModes.Ellipsis,
                TextWrappingModes.NoWrap);
        }

        private void ValidateReferences()
        {
#if UNITY_EDITOR
            Debug.Assert(mainMenuGroup != null, "[MainMenuController] mainMenuGroup is not assigned!");
            Debug.Assert(saveLoadGroup != null, "[MainMenuController] saveLoadGroup is not assigned!");
            Debug.Assert(loadingGroup != null, "[MainMenuController] loadingGroup is not assigned!");
            Debug.Assert(slotsContainer != null, "[MainMenuController] slotsContainer is not assigned!");
            Debug.Assert(
                HasExistingSlotInstances(),
                "[MainMenuController] Save shell requires three scene-owned SaveSlotUI entries.");
#endif
        }

        private void BindButtons()
        {
            BindButton(btnNewGame, _newGameClickAction);
            BindButton(btnLoadGame, _loadGameClickAction);
            BindButton(btnSettings, _settingsClickAction);
            BindButton(btnQuit, _quitClickAction);
            BindButton(btnBackFromSaveLoad, _backFromSaveLoadClickAction);
            BindButton(btnBackFromSettings, _backFromSettingsClickAction);

            if (btnSettings != null)
                btnSettings.interactable = _settingsAvailable;
        }

        private void EnsureRuntimeMenuBindings(bool resetPanelState)
        {
            CacheButtonActions();
            AutoWireSceneReferences();
            ConfigureAdaptiveLabels();
            ValidateReferences();
            EnsurePanelHierarchyActive(mainMenuGroup);
            EnsurePanelHierarchyActive(saveLoadGroup);
            EnsurePanelHierarchyActive(settingsGroup);
            EnsurePanelHierarchyActive(loadingGroup);
            BindButtons();

            if (resetPanelState || !_runtimeBindingsReady)
                InitializePanelStates();

            _runtimeBindingsReady = true;
        }

        private void CacheButtonActions()
        {
            if (_newGameClickAction == null)
                _newGameClickAction = OnNewGameClicked; // COLD ALLOC: UnityAction[1] — cached main menu new-game listener — owner: MainMenuController
            if (_loadGameClickAction == null)
                _loadGameClickAction = OnLoadGameClicked; // COLD ALLOC: UnityAction[1] — cached main menu load-game listener — owner: MainMenuController
            if (_settingsClickAction == null)
                _settingsClickAction = OnSettingsClicked; // COLD ALLOC: UnityAction[1] — cached main menu settings listener — owner: MainMenuController
            if (_quitClickAction == null)
                _quitClickAction = OnQuitClicked; // COLD ALLOC: UnityAction[1] — cached main menu quit listener — owner: MainMenuController
            if (_backFromSaveLoadClickAction == null)
                _backFromSaveLoadClickAction = OnBackFromSaveLoadClicked; // COLD ALLOC: UnityAction[1] — cached save-load back listener — owner: MainMenuController
            if (_backFromSettingsClickAction == null)
                _backFromSettingsClickAction = OnBackFromSettingsClicked; // COLD ALLOC: UnityAction[1] — cached settings back listener — owner: MainMenuController
        }

        private static void BindButton(Button button, UnityAction callback)
        {
            if (button == null)
                return;

            if (callback == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        private void InitializePanelStates()
        {
            SetPanelImmediate(mainMenuGroup, true);
            SetPanelImmediate(saveLoadGroup, false);
            SetPanelImmediate(settingsGroup, false);
            SetPanelImmediate(loadingGroup, false);
            _currentPanel = mainMenuGroup;
            RequestSelectionRefresh();
        }

        private void OnNewGameClicked()
        {
            int messageLength = CopyLocalizedModalMessage(LocalizationKeys.MODAL_NEW_GAME_MESSAGE, "Start a new game?");
            ModalWindow.Show(
                "New Game",
                _modalMessageBuffer,
                messageLength,
                () => StartGame(string.Empty));
        }

        private void OnLoadGameClicked()
        {
            OpenSaveLoadMenu();
        }

        private void OnSettingsClicked()
        {
            if (!_settingsAvailable)
                return;

            SwitchPanel(mainMenuGroup, settingsGroup);
        }

        private void OnQuitClicked()
        {
            int messageLength = CopyLocalizedModalMessage(LocalizationKeys.MODAL_QUIT_MESSAGE, "Quit the game?");
            ModalWindow.Show(
                "Quit",
                _modalMessageBuffer,
                messageLength,
                () =>
                {
#if UNITY_EDITOR
                    Hecton8.Dev.EditorPlayModeDiagnostics.RequestStopPlayMode(
                        nameof(MainMenuController),
                        "MainMenu Quit",
                        this);
#else
                    Application.Quit();
#endif
                });
        }

        private void OnBackFromSaveLoadClicked()
        {
            SwitchPanel(saveLoadGroup, mainMenuGroup);
        }

        private void OnBackFromSettingsClicked()
        {
            SwitchPanel(settingsGroup, mainMenuGroup);
        }

        private void AutoWireSceneReferences()
        {
            Transform root = transform;

            mainMenuGroup = ResolveCanvasGroup(mainMenuGroup, root, "Panel_MainMenu");
            saveLoadGroup = ResolveCanvasGroup(saveLoadGroup, root, "Panel_Sideload Popup");
            settingsGroup = ResolveCanvasGroup(settingsGroup, root, "Panel_Settings");
            loadingGroup = ResolveCanvasGroup(loadingGroup, root, "Panel_LoadingScreen");

            btnNewGame = ResolveButton(btnNewGame, root, "BTN_Start");
            btnLoadGame = ResolveButton(btnLoadGame, root, "BTN_ResumeLog");
            btnSettings = ResolveButton(btnSettings, root, "BTN_Settings");
            btnQuit = ResolveButton(btnQuit, root, "BTN_Abort");
            btnBackFromSaveLoad = ResolveButton(btnBackFromSaveLoad, root, "BTN_Back (\"RETURN\")");

            labelNewGame = ResolveButtonLabel(labelNewGame, btnNewGame);
            labelLoadGame = ResolveButtonLabel(labelLoadGame, btnLoadGame);
            labelSettings = ResolveButtonLabel(labelSettings, btnSettings);
            labelQuit = ResolveButtonLabel(labelQuit, btnQuit);

            slotsContainer = ResolveSlotsContainer(slotsContainer, root);
            loadingPercentText = ResolveLoadingPercentText(loadingPercentText, loadingGroup);
            _settingsAvailable = DetermineSettingsAvailability();
            if (!_settingsAvailable)
                btnBackFromSettings = null;
        }

        private bool DetermineSettingsAvailability()
        {
            if (settingsGroup == null)
                return false;

            if (settingsGroup.transform.childCount == 0)
                return false;

            if (btnBackFromSettings == null)
                return false;

            return true;
        }

        private Transform ResolveSlotsContainer(Transform current, Transform root)
        {
            if (current != null)
                return current;

            Transform panel = FindDeepChild(root, "Panel_Sideload Popup");
            if (panel == null)
                return null;

            Transform container = FindDeepChild(panel, "ScrollView_Slots");
            if (container == null)
                return null;

            return container.childCount > 0 ? container : null;
        }

        private TMP_Text ResolveLoadingPercentText(TMP_Text current, CanvasGroup group)
        {
            if (current != null)
                return current;

            if (group == null)
                return null;

            TMP_Text label;
            return TryResolveDescendantComponent(group.transform, out label) ? label : null;
        }

        private static CanvasGroup ResolveCanvasGroup(CanvasGroup current, Transform root, string objectName)
        {
            if (current != null)
                return current;

            Transform target = FindDeepChild(root, objectName);
            if (target == null)
                return null;

            if (target.TryGetComponent(out CanvasGroup group))
                return group;

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError(
                "[MainMenuController] Required CanvasGroup missing. Author the component in 01_MAIN_MENU instead of patching it at runtime.");
#endif
            return null;
        }

        private static Button ResolveButton(Button current, Transform root, string objectName)
        {
            if (current != null)
                return current;

            Transform target = FindDeepChild(root, objectName);
            if (target == null)
                return null;

            target.TryGetComponent(out Button button);
            return button;
        }

        private static TMP_Text ResolveButtonLabel(TMP_Text current, Button button)
        {
            if (current != null)
                return current;

            if (button == null)
                return null;

            TMP_Text label;
            return TryResolveDescendantComponent(button.transform, out label) ? label : null;
        }

        private static bool TryResolveDescendantComponent<T>(Transform root, out T component) where T : Component
        {
            component = null;
            if (root == null)
                return false;

            if (root.TryGetComponent(out component))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                if (TryResolveDescendantComponent(root.GetChild(i), out component))
                    return true;
            }

            return false;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null)
                return null;

            if (parent.name == childName)
                return parent;

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindDeepChild(parent.GetChild(i), childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private bool HasExistingSlotInstances()
        {
            if (slotsContainer == null)
                return false;

            int found = 0;
            for (int i = 0; i < slotsContainer.childCount; i++)
            {
                Transform child = slotsContainer.GetChild(i);
                if (child != null && child.TryGetComponent(out SaveSlotUI _))
                    found++;
            }

            return found >= SlotCount;
        }

        /// <summary>
        /// Opens the Save/Load panel, clears the container, generates slots.
        /// Uses Hecton8.SaveSystem.SaveManager for metadata queries.
        /// Displays "Save system unavailable" if SaveManager is null.
        /// </summary>
        public void OpenSaveLoadMenu()
        {
            if (_isSaveLoadBusy || _isSceneLoadInFlight)
                return;

            EnsureSlotInstances();

            if (_saveManager == null)
                _saveManager = Hecton8.Core.GlobalRegistry.Save as SaveManager;

            // TASK 31: Comprehensive null check for SaveManager
            if (_saveManager == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[MainMenuController] Hecton8.Core.GlobalRegistry.Save is null. Save/Load features unavailable.");
#endif
                // Display error message to user
                int messageLength = CopyLocalizedModalMessage(
                    LocalizationKeys.ERROR_SAVE_SYSTEM_UNAVAILABLE_MESSAGE,
                    "The save system is currently unavailable.\n\nPlease restart the game or contact support if this persists.");

                ModalWindow.ShowWithCustomLabels(
                    "Save System Unavailable",
                    _modalMessageBuffer,
                    messageLength,
                    () => SwitchPanel(saveLoadGroup, mainMenuGroup), // Return to main menu
                    null,
                    "Return to Menu",
                    null);

                // Disable load game button to prevent future attempts
                if (btnLoadGame != null)
                    btnLoadGame.interactable = false;

                return;
            }

            if (_slotUIs != null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    string slotName = SlotNames[i];
                    SaveSlotUI slotUI = _slotUIs[i];
                    if (slotUI == null)
                        continue;

                    if (_saveManager.TryGetSaveSlotInfo(slotName, out SaveSlotInfo slotInfo))
                    {
                        slotUI.Init(slotInfo, OnSlotClicked);
                        if (_slotButtonAvailability != null && i < _slotButtonAvailability.Length)
                            _slotButtonAvailability[i] = slotInfo != null && slotInfo.HasAnySaveData;
                    }
                    else
                    {
                        slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);
                        if (_slotButtonAvailability != null && i < _slotButtonAvailability.Length)
                            _slotButtonAvailability[i] = false;
                    }
                }
            }
            else
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning(
                    "[MainMenuController] Save shell is missing or incomplete. " +
                    "Opening save/load in fallback state so the player can still back out.");
#endif
            }

            SwitchPanel(mainMenuGroup, saveLoadGroup);
        }

        private void EnsureSlotInstances()
        {
            if (_slotUIs != null)
                return;

            if (TryBindExistingSlotInstances())
                return;

#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("[MainMenuController] Save shell requires three scene-owned SaveSlotUI entries.");
#endif
        }

        private bool TryBindExistingSlotInstances()
        {
            if (slotsContainer == null)
                return false;

            SaveSlotUI[] slotUis = new SaveSlotUI[SlotCount]; // COLD ALLOC: SaveSlotUI[3] — fixed save-shell slot cache — owner: MainMenuController
            int found = 0;

            for (int i = 0; i < slotsContainer.childCount && found < SlotCount; i++)
            {
                Transform child = slotsContainer.GetChild(i);
                if (child == null)
                    continue;

                if (!child.TryGetComponent(out SaveSlotUI slotUi))
                    continue;

                child.gameObject.name = SlotNames[found];
                slotUis[found] = slotUi;
                found++;
            }

            _slotUIs = slotUis;
            _slotButtonAvailability = new bool[SlotCount]; // COLD ALLOC: bool[3] — save-slot availability cache — owner: MainMenuController

#if UNITY_EDITOR
            if (found < SlotCount)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[MainMenuController] Save shell bound fewer slot instances than required. Fallback focus/back handling remains active.");
            }
#endif

            return found > 0;
        }

        private void OnSlotClicked(string slotName)
        {
            if (_isSaveLoadBusy || _isSceneLoadInFlight)
                return;

            if (string.IsNullOrEmpty(slotName))
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[MainMenuController] Ignored empty slot click.");
#endif
                return;
            }

            int messageLength = BuildSlotModalMessage(
                LocalizationKeys.MODAL_LOAD_MESSAGE,
                "Load selected save?",
                slotName,
                ReadOnlySpan<char>.Empty);

            ModalWindow.Show("Load Game", _modalMessageBuffer, messageLength, () => StartGame(slotName));
        }

        /// <summary>
        /// Starts async loading of the game scene.
        /// Empty slotName = new game, otherwise = load save.
        /// Writes to GameStartContextHolder for inter-scene communication.
        /// Cold persistence is owned by the holder, not by MainMenuController.
        /// TASK 31: Comprehensive null checks for SaveManager before loading.
        /// </summary>
        public void StartGame(string slotName)
        {
            if (_isSceneLoadInFlight || _isSaveLoadBusy)
                return;

            // Validate save exists before loading
            if (!string.IsNullOrEmpty(slotName))
            {
                // TASK 31: Null check for SaveManager before save validation
                if (_saveManager == null)
                    _saveManager = Hecton8.Core.GlobalRegistry.Save as SaveManager;

                if (_saveManager == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[MainMenuController] Hecton8.Core.GlobalRegistry.Save is null. Cannot validate save file.");
#endif
                    int messageLength = CopyLocalizedModalMessage(
                        LocalizationKeys.ERROR_SAVE_SYSTEM_UNAVAILABLE_MESSAGE,
                        "The save system is currently unavailable.\n\nCannot load save file.");

                    ModalWindow.ShowWithCustomLabels(
                        "Save System Unavailable",
                        _modalMessageBuffer,
                        messageLength,
                        () => OpenSaveLoadMenu(), // Return to save/load menu
                        null,
                        "OK",
                        null);
                    return;
                }

                if (!_saveManager.SaveExists(slotName))
                {
                    int messageLength = BuildSlotModalMessage(
                        LocalizationKeys.MODAL_LOAD_ERROR_MESSAGE,
                        "Save file does not exist.",
                        slotName,
                        ReadOnlySpan<char>.Empty);

                    ModalWindow.ShowWithCustomLabels(
                        "Load Error",
                        _modalMessageBuffer,
                        messageLength,
                        () => OpenSaveLoadMenu(), // Return to save/load menu
                        null,
                        "OK",
                        null);
                    return;
                }
            }

            _isSceneLoadInFlight = true;

            bool isNewGame = string.IsNullOrEmpty(slotName);
            string sceneName = ResolveStartSceneName(isNewGame);
            GameStartContext context = isNewGame
                ? GameStartContext.CreateNewGame()
                : GameStartContext.CreateLoadGame(slotName);

            GameStartContextHolder.SetCurrent(context);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameStartContextHolder.LogCurrent();
#endif

            TryRegisterToTickManager();
            CanvasGroup cinematicPanel = _currentPanel != null && _currentPanel != loadingGroup
                ? _currentPanel
                : mainMenuGroup;
            PreparePanelForCinematicSubmerge(cinematicPanel);
            if (mainMenuGroup != cinematicPanel)
                SetPanelImmediate(mainMenuGroup, false);
            if (saveLoadGroup != cinematicPanel)
                SetPanelImmediate(saveLoadGroup, false);
            if (settingsGroup != cinematicPanel)
                SetPanelImmediate(settingsGroup, false);
            if (loadingGroup != cinematicPanel)
                SetPanelImmediate(loadingGroup, false);
            _currentPanel = null;
            RequestSelectionRefresh();

            if (loadingTips != null)
                loadingTips.StopTipCycle();

            RefreshLoadingPercentTemplate();
            UpdateLoadingProgressVisual(0);

            ISceneService sceneService = Hecton8.Core.GlobalRegistry.Scene;
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
                _isSceneLoadInFlight = false;

#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogError(
                    "[MainMenuController] Failed to load scene. SceneRuntimeService is unavailable or bootstrap is incomplete.");
#endif

                int messageLength = BuildModalMessage(
                    LocalizationKeys.MODAL_SCENE_LOAD_ERROR_MESSAGE,
                    "Failed to load scene. Check Build Settings.",
                    sceneName.AsSpan(),
                    ReadOnlySpan<char>.Empty);

                ModalWindow.ShowWithCustomLabels(
                    "Scene Load Error",
                    _modalMessageBuffer,
                    messageLength,
                    () => StartGame(slotName), // Retry
                    () => { SetPanelImmediate(loadingGroup, false); SetPanelImmediate(mainMenuGroup, true); }, // Cancel
                    "Retry",
                    "Return to Menu");

                return;
            }

            if (runtimeSceneService != null)
                runtimeSceneService.ConfigureMainMenuCinematic(mainMenuCamera, cinematicPanel);

            sceneService.LoadScene(sceneName);
        }

        private string ResolveStartSceneName(bool isNewGame)
        {
            string sceneName = isNewGame ? newGameTargetSceneName : targetSceneName;
            return string.IsNullOrWhiteSpace(sceneName) ? targetSceneName : sceneName;
        }

        /// <summary>
        /// Smoothly fades out one panel and fades in the next.
        /// Double-click protected via instant interactable/blocksRaycasts toggle.
        /// </summary>
        private void SwitchPanel(CanvasGroup from, CanvasGroup to)
        {
            if (_isTransitioning || from == null || to == null || from == to)
                return;

            // Play panel transition sound
            if (to == mainMenuGroup)
                UIAudioFeedback.PlayPanelClose();
            else
                UIAudioFeedback.PlayPanelOpen();

            TryRegisterToTickManager();
            _isTransitioning = true;
            _transitionFromPanel = from;
            _transitionToPanel = to;
            _transitionElapsed = 0f;
            _transitionStartAlpha = from.alpha;
            _panelTransitionState = PanelTransitionState.FadingOut;

            from.interactable = false;
            from.blocksRaycasts = false;
            to.interactable = false;
            to.blocksRaycasts = false;
            BlockCancelInputBriefly();
        }

        public void Tick(float dt)
        {
            float unscaledDeltaTime = GetUnscaledDeltaTime();
            if (unscaledDeltaTime <= 0f)
                return;

            EnsureMenuInputRoutingReady();
            ConsumeCancelInputSignals();
            HandleCancelInput();
            UpdatePanelTransition(unscaledDeltaTime);
            RefreshSelectionIfNeeded();
        }

        private void ConsumeCancelInputSignals()
        {
            System.ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
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

        private void BaselineCancelInputSignalSequence()
        {
            System.ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
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

        private void EnsureMenuInputRoutingReady()
        {
            if (_inputRoutingReady)
                return;

            BindMenuInput();
            RefreshMenuInputRoutingReadyFromCache();
        }

        private void CacheMenuInputRoutingCold()
        {
            _cachedEventSystem = EventSystem.current;
            _cachedUiInputModule = null;
            _inputRoutingReady = false;

            if (_cachedEventSystem == null || !_cachedEventSystem.enabled)
                return;

            _cachedEventSystem.TryGetComponent(out _cachedUiInputModule);
        }

        private void RefreshMenuInputRoutingReadyFromCache()
        {
            InputSystemUIInputModule inputModule = _cachedUiInputModule;
            if (inputModule == null || !inputModule.enabled)
                return;

            _inputRoutingReady = MainMenuInputRoutingGuard.HasUsableUiModuleActions(inputModule);
            if (_inputRoutingReady)
                RequestSelectionRefresh();
        }

        private void HandleCancelInput()
        {
            // Input spam protection: ignore input during transitions or scene loading
            if (_isTransitioning ||
                _isSceneLoadInFlight ||
                _isSaveLoadBusy ||
                (float)SystemDispatcher.CurrentUnscaledTimeSeconds < _cancelInputBlockedUntil ||
                !_cancelRequested)
                return;

            _cancelRequested = false;

            if (_currentPanel == settingsGroup)
            {
                OnBackFromSettingsClicked();
                return;
            }

            if (_currentPanel == saveLoadGroup)
            {
                OnBackFromSaveLoadClicked();
                return;
            }

            if (_currentPanel == mainMenuGroup)
                OnQuitClicked();
        }

        private void RefreshSelectionIfNeeded()
        {
            if (!_refreshSelectionRequested)
                return;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            _refreshSelectionRequested = false;

            Button target = ResolveDefaultSelectionButton();
            GameObject selected = eventSystem.currentSelectedGameObject;

            if (target == null)
            {
                if (selected != null)
                    eventSystem.SetSelectedGameObject(null);

                return;
            }

            if (selected != target.gameObject)
                eventSystem.SetSelectedGameObject(target.gameObject);
        }

        private void BlockCancelInputBriefly()
        {
            _cancelInputBlockedUntil = (float)SystemDispatcher.CurrentUnscaledTimeSeconds + CancelInputDebounceSeconds;
            _cancelRequested = false;
        }

        private void BindMenuInput()
        {
            INativeInputManagerRuntime inputManager = _inputManager;
            if (inputManager == null)
            {
                _menuInputBound = false;
                return;
            }

            if (!_menuInputBound)
            {
                BaselineCancelInputSignalSequence();
                _menuInputBound = true;
            }

            if (inputManager.CanSwitchActionMaps)
                inputManager.SwitchToUIInput();
        }

        private void UnbindMenuInput()
        {
            _menuInputBound = false;
            _inputManager = null;
            _cancelRequested = false;
        }

        private void CacheInputManagerCold(INativeInputManagerRuntime inputManager)
        {
            if (ReferenceEquals(_inputManager, inputManager))
                return;

            _inputManager = inputManager;
            _menuInputBound = false;
            _inputRoutingReady = false;
            _cancelRequested = false;
        }

        private void RequestSelectionRefresh()
        {
            _refreshSelectionRequested = true;
        }

        private Button ResolveDefaultSelectionButton()
        {
            if (_currentPanel == saveLoadGroup)
            {
                Button slotButton = GetFirstInteractableSlotButton();
                if (slotButton != null)
                    return slotButton;

                return btnBackFromSaveLoad;
            }

            if (_currentPanel == settingsGroup)
                return btnBackFromSettings;

            if (_currentPanel == loadingGroup)
                return null;

            return FirstInteractableButton(btnNewGame, btnLoadGame, btnSettings, btnQuit);
        }

        private Button GetFirstInteractableSlotButton()
        {
            if (_slotUIs == null)
                return null;

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                SaveSlotUI slotUi = _slotUIs[i];
                if (slotUi == null)
                    continue;

                if (slotUi.IsInteractable)
                    return slotUi.ButtonComponent;
            }

            return null;
        }

        private static Button FirstInteractableButton(
            Button first,
            Button second,
            Button third,
            Button fourth)
        {
            if (first != null && first.interactable)
                return first;

            if (second != null && second.interactable)
                return second;

            if (third != null && third.interactable)
                return third;

            if (fourth != null && fourth.interactable)
                return fourth;

            return null;
        }

        private static string BuildSlotDisplayName(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return "?";

            int slotIndex = SaveEvents.ResolveKnownSlotIndex(slotName);
            return (uint)slotIndex < (uint)SlotDisplayNames.Length ? SlotDisplayNames[slotIndex] : slotName;
        }

        private static bool IsCorruptNoBackupError(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;

            return error.IndexOf("corrupt", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("checksum", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   error.IndexOf("No valid save data", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdatePanelTransition(float unscaledDeltaTime)
        {
            if (_panelTransitionState == PanelTransitionState.None ||
                _transitionFromPanel == null ||
                _transitionToPanel == null)
            {
                return;
            }

            float duration = math.max(0.0001f, fadeDuration);
            _transitionElapsed += unscaledDeltaTime;
            float t = math.saturate(_transitionElapsed / duration);

            if (_panelTransitionState == PanelTransitionState.FadingOut)
            {
                _transitionFromPanel.alpha = math.lerp(_transitionStartAlpha, 0f, t);
                if (t < 1f)
                    return;

                _transitionFromPanel.alpha = 0f;
                _transitionToPanel.alpha = 0f;
                _transitionElapsed = 0f;
                _panelTransitionState = PanelTransitionState.FadingIn;
                return;
            }

            _transitionToPanel.alpha = t;
            if (t < 1f)
                return;

            _transitionToPanel.alpha = 1f;
            _transitionToPanel.interactable = true;
            _transitionToPanel.blocksRaycasts = true;
            _panelTransitionState = PanelTransitionState.None;
            _currentPanel = _transitionToPanel;
            _transitionFromPanel = null;
            _transitionToPanel = null;
            _isTransitioning = false;
            RequestSelectionRefresh();
        }

        /// <summary>
        /// Updates loading progress visual with zero-GC dirty flag pattern.
        /// </summary>
        private void UpdateLoadingProgressVisual(int percent)
        {
            percent = math.clamp(percent, 0, 100);

            if (loadingProgressBar != null)
                loadingProgressBar.value = percent / 100f;

            // Dirty flag: only update text when value changes (zero-GC)
            if (loadingPercentText != null && _lastLoadingPercent != percent)
            {
                ApplyLoadingPercentText(percent);
                _lastLoadingPercent = percent;
            }
        }

        private void RefreshLoadingPercentTemplate()
        {
            ILocalizationTextReadModel loc = Hecton8.Core.GlobalRegistry.LocalizationText;
            ReadOnlySpan<char> template = ResolveLocalizedSpan(loc, LocalizationKeys.LOADING_PERCENT, "{0}%");
            _loadingPercentTemplateLength = CopySpanToBuffer(template, _loadingPercentTemplateBuffer, 0);
        }

        private void RefreshLoadingLocalization()
        {
            RefreshLoadingPercentTemplate();

            if (loadingPercentText == null || _lastLoadingPercent < 0)
                return;

            ApplyLoadingPercentText(_lastLoadingPercent);
        }

        private void ApplyLoadingPercentText(int percent)
        {
            if (loadingPercentText == null)
                return;

            percent = math.clamp(percent, 0, 100);
            System.Span<char> destination = _loadingPercentBuffer;
            ReadOnlySpan<char> template = _loadingPercentTemplateLength > 0
                ? _loadingPercentTemplateBuffer.AsSpan(0, _loadingPercentTemplateLength)
                : "{0}%".AsSpan();
            int cursor = 0;
            bool wroteValue = false;

            for (int i = 0; i < template.Length && cursor < destination.Length; i++)
            {
                if (i + 2 < template.Length &&
                    template[i] == '{' &&
                    template[i + 1] == '0' &&
                    template[i + 2] == '}')
                {
                    if (!ZeroGCFormatter.FastIntToChars(percent, destination, ref cursor))
                        break;

                    wroteValue = true;
                    i += 2;
                    continue;
                }

                destination[cursor++] = template[i];
            }

            if (!wroteValue)
            {
                cursor = 0;
                ZeroGCFormatter.FastIntToChars(percent, destination, ref cursor);
                ZeroGCFormatter.AppendChar('%', destination, ref cursor);
            }

            loadingPercentText.SetCharArray(_loadingPercentBuffer, 0, math.clamp(cursor, 0, _loadingPercentBuffer.Length));
        }

        // ══════════════════════════════════════════════════════════
        // SAVE/LOAD EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        // SAVE/LOAD EVENT HANDLERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called when save operation starts. Sets busy flag and disables save/load buttons.
        /// </summary>
        private void OnSaveStarted(in SaveEventPayload payload)
        {
            _isSaveLoadBusy = true;
            SetSaveLoadButtonsInteractable(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[MainMenuController] Save started.");
#endif
        }

        /// <summary>
        /// Called when save operation completes successfully.
        /// Re-enables buttons and updates slot metadata.
        /// </summary>
        private void OnSaveCompleted(in SaveEventPayload payload)
        {
            _isSaveLoadBusy = false;
            SetSaveLoadButtonsInteractable(true);

            // Refresh slot metadata to show updated save info
            if (_saveManager != null && _slotUIs != null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    string slotNameToRefresh = SlotNames[i];
                    SaveSlotUI slotUI = _slotUIs[i];
                    if (slotUI == null)
                        continue;

                    if (_saveManager.TryGetSaveSlotInfo(slotNameToRefresh, out SaveSlotInfo slotInfo))
                    {
                        slotUI.Init(slotInfo, OnSlotClicked);
                        if (_slotButtonAvailability != null && i < _slotButtonAvailability.Length)
                            _slotButtonAvailability[i] = slotInfo != null && slotInfo.HasAnySaveData;
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[MainMenuController] Save completed.");
#endif

            RequestSelectionRefresh();
        }

        /// <summary>
        /// Called when save operation fails. Displays error modal and re-enables buttons.
        /// </summary>
        private void OnSaveFailed(string slotName, string error)
        {
            _isSaveLoadBusy = false;
            SetSaveLoadButtonsInteractable(true);

            string errorText = ResolveSaveEventError(error);
            int messageLength = BuildSlotModalMessage(
                LocalizationKeys.ERROR_SAVE_FAILED_MESSAGE,
                "Failed to save selected slot.",
                slotName,
                errorText.AsSpan());

            ModalWindow.ShowWithCustomLabels(
                "Save Failed",
                _modalMessageBuffer,
                messageLength,
                null, // No retry in main menu (only in pause menu)
                null,
                "OK",
                null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[MainMenuController] Save failed.");
#endif

            RequestSelectionRefresh();
        }

        /// <summary>
        /// Called when load operation starts. Sets busy flag and disables load buttons.
        /// </summary>
        private void OnLoadStarted(in SaveEventPayload payload)
        {
            _isSaveLoadBusy = true;
            _lastLoadUsedBackup = false;
            SetSaveLoadButtonsInteractable(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[MainMenuController] Load started.");
#endif
        }

        /// <summary>
        /// Called when load operation completes successfully.
        /// Re-enables buttons and proceeds with game start.
        /// </summary>
        private void OnLoadCompleted(in SaveEventPayload payload)
        {
            _isSaveLoadBusy = false;

            // Check if backup was used (SaveManager should set this flag)
            if (_saveManager != null && _saveManager.LastLoadUsedBackup)
            {
                _lastLoadUsedBackup = true;

                string slotName = SaveEvents.ResolveSlotName(payload.SlotHash);
                int messageLength = BuildSlotModalMessage(
                    LocalizationKeys.WARNING_BACKUP_USED_MESSAGE,
                    "Primary save file was corrupt. Loaded from backup.",
                    slotName,
                    ReadOnlySpan<char>.Empty);

                ModalWindow.ShowWithCustomLabels(
                    "Backup Loaded",
                    _modalMessageBuffer,
                    messageLength,
                    null,
                    null,
                    "OK",
                    null);
            }

            SetSaveLoadButtonsInteractable(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[MainMenuController] Load completed.");
#endif

            RequestSelectionRefresh();
        }

        /// <summary>
        /// Called when load operation fails. Displays error modal with retry/return options.
        /// </summary>
        private void OnLoadFailed(string slotName, string error)
        {
            _isSaveLoadBusy = false;
            SetSaveLoadButtonsInteractable(true);

            string errorText = ResolveSaveEventError(error);
            // Check if error indicates corrupt save with no backup
            bool isCorruptNoBackup = IsCorruptNoBackupError(errorText);

            int messageLength = isCorruptNoBackup
                ? BuildSlotModalMessage(
                    LocalizationKeys.ERROR_LOAD_CORRUPT_NO_BACKUP_MESSAGE,
                    "No valid save data found. The save file is corrupt and no backup is available.",
                    slotName,
                    ReadOnlySpan<char>.Empty)
                : BuildSlotModalMessage(
                    LocalizationKeys.ERROR_LOAD_FAILED_MESSAGE,
                    "Failed to load selected save.",
                    slotName,
                    errorText.AsSpan());

            ModalWindow.ShowWithCustomLabels(
                "Load Failed",
                _modalMessageBuffer,
                messageLength,
                () => StartGame(slotName), // Retry
                () => SwitchPanel(saveLoadGroup, mainMenuGroup), // Return to menu
                "Retry",
                "Return to Menu");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[MainMenuController] Load failed.");
#endif

            RequestSelectionRefresh();
        }

        private static string ResolveSaveEventError(string error)
        {
            return string.IsNullOrWhiteSpace(error) ? UnknownSaveEventError : error;
        }

        private void SetSaveLoadButtonsInteractable(bool interactable)
        {
            if (btnBackFromSaveLoad != null)
                btnBackFromSaveLoad.interactable = interactable;

            if (_slotUIs == null)
                return;

            for (int i = 0; i < _slotUIs.Length; i++)
            {
                SaveSlotUI slotUI = _slotUIs[i];
                if (slotUI == null || slotUI.ButtonComponent == null)
                    continue;

                bool slotAvailable = slotUI.IsInteractable;
                if (_slotButtonAvailability != null && i < _slotButtonAvailability.Length)
                    slotAvailable = _slotButtonAvailability[i];

                slotUI.ButtonComponent.interactable = interactable && slotAvailable;
            }
        }

        private float GetUnscaledDeltaTime()
        {
            float currentTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (_lastUnscaledTickTime <= 0f)
            {
                _lastUnscaledTickTime = currentTime;
                return 0f;
            }

            float delta = currentTime - _lastUnscaledTickTime;
            _lastUnscaledTickTime = currentTime;
            return delta > 0f ? delta : 0f;
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(ILocalizationTextReadModel loc, string key, ReadOnlySpan<char> fallback)
        {
            return loc != null
                ? loc.GetRawSpanOrFallback(LocHash.Compute(key.AsSpan()), fallback)
                : fallback;
        }

        private int CopyLocalizedModalMessage(string key, ReadOnlySpan<char> fallback)
        {
            return BuildModalMessage(key, fallback, ReadOnlySpan<char>.Empty, ReadOnlySpan<char>.Empty);
        }

        private int BuildSlotModalMessage(string key, ReadOnlySpan<char> fallback, string slotName, ReadOnlySpan<char> detail)
        {
            return BuildModalMessage(key, fallback, BuildSlotDisplayName(slotName).AsSpan(), detail);
        }

        private int BuildModalMessage(string key, ReadOnlySpan<char> fallback, ReadOnlySpan<char> primary, ReadOnlySpan<char> detail)
        {
            int cursor = 0;
            ILocalizationTextReadModel loc = Hecton8.Core.GlobalRegistry.LocalizationText;
            cursor += CopySpanToBuffer(ResolveLocalizedSpan(loc, key, fallback), _modalMessageBuffer, cursor);

            if (!primary.IsEmpty)
            {
                cursor += CopySpanToBuffer(" // ".AsSpan(), _modalMessageBuffer, cursor);
                cursor += CopySpanToBuffer(primary, _modalMessageBuffer, cursor);
            }

            if (!detail.IsEmpty)
            {
                cursor += CopySpanToBuffer("\n".AsSpan(), _modalMessageBuffer, cursor);
                cursor += CopySpanToBuffer(detail, _modalMessageBuffer, cursor);
            }

            return cursor;
        }

        private static int CopySpanToBuffer(ReadOnlySpan<char> value, char[] buffer, int offset)
        {
            if (value.Length == 0 || buffer == null || offset >= buffer.Length)
                return 0;

            int safeLength = math.min(value.Length, buffer.Length - offset);
            value.Slice(0, safeLength).CopyTo(buffer.AsSpan(offset, safeLength));
            return safeLength;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void SetPanelImmediate(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            EnsurePanelHierarchyActive(group);
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (visible)
                _currentPanel = group;
        }

        private static void PreparePanelForCinematicSubmerge(CanvasGroup group)
        {
            if (group == null)
                return;

            EnsurePanelHierarchyActive(group);
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private static void EnsurePanelHierarchyActive(CanvasGroup group)
        {
            if (group == null)
                return;

            GameObject panelRoot = group.gameObject;
            if (!panelRoot.activeSelf)
                panelRoot.SetActive(true);
        }
    }
}
