using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Controller for the main menu scene. Manages panel transitions,
    /// save slot generation, and async scene loading.
    /// All UI text is driven through LocalizationManager.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour, ITickable
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

        [Header("=== CONFIG ===")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private string targetSceneName = "02_HECTON_WORLD";

        private const int SlotCount = 3;
        private const float CancelInputDebounceSeconds = 0.35f;
        private static readonly string[] SlotNames = { "slot_1", "slot_2", "slot_3" };

        private bool _isTransitioning;
        private bool _isSceneLoadInFlight;
        private bool _registeredToTickManager;
        private bool _settingsAvailable;
        private bool _sceneActivationRequested;
        private bool _refreshSelectionRequested;
        private bool _isSaveLoadBusy;
        private bool _lastLoadUsedBackup;
        private int _lastLoadingPercent = -1;
        private float _lastUnscaledTickTime;
        private float _cancelInputBlockedUntil;
        private float _transitionElapsed;
        private float _transitionStartAlpha;
        private string _loadingPercentTemplate = "{0}%";
        private SaveManager _saveManager;
        private SaveSlotUI[] _slotUIs;
        private bool[] _slotButtonAvailability;
        private AsyncOperation _sceneLoadOperation;
        private CanvasGroup _transitionFromPanel;
        private CanvasGroup _transitionToPanel;
        private CanvasGroup _currentPanel;
        private PanelTransitionState _panelTransitionState;


        private void Awake()
        {
            if (!BootstrapRouteEnforcer.EnsureBootstrapRuntimeRoute(
                    gameObject.scene.name,
                    nameof(MainMenuController)))
            {
                enabled = false;
                return;
            }

            AutoWireSceneReferences();
            ConfigureAdaptiveLabels();
            ValidateReferences();
            BindButtons();
            InitializePanelStates();
            BlockCancelInputBriefly();
        }

        private void Start()
        {
            _saveManager = SaveManager.Instance;
            TryRegisterToTickManager();

#if UNITY_EDITOR
            if (_saveManager == null)
            {
                Debug.LogWarning(
                    "[MainMenuController] SaveManager.Instance is null. " +
                    "Save/Load features will be unavailable. " +
                    "Ensure SaveManager exists in scene or is DontDestroyOnLoad.");
            }
#endif
        }

        private void OnEnable()
        {
            TryRegisterToTickManager();
            _lastUnscaledTickTime = Time.unscaledTime;
            BlockCancelInputBriefly();
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            
            // Subscribe to save/load events for UI feedback
            SaveEvents.OnSaveStarted += OnSaveStarted;
            SaveEvents.OnSaveCompleted += OnSaveCompleted;
            SaveEvents.OnSaveFailed += OnSaveFailed;
            SaveEvents.OnLoadStarted += OnLoadStarted;
            SaveEvents.OnLoadCompleted += OnLoadCompleted;
            SaveEvents.OnLoadFailed += OnLoadFailed;
            
            RefreshLocalizedTexts();
        }

        private void OnDisable()
        {
            // TASK 31: Null-safe event unsubscription in OnDisable
            if (LocalizationManager.Instance != null)
                LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            
            // Unsubscribe from save/load events with null checks
            SaveEvents.OnSaveStarted -= OnSaveStarted;
            SaveEvents.OnSaveCompleted -= OnSaveCompleted;
            SaveEvents.OnSaveFailed -= OnSaveFailed;
            SaveEvents.OnLoadStarted -= OnLoadStarted;
            SaveEvents.OnLoadCompleted -= OnLoadCompleted;
            SaveEvents.OnLoadFailed -= OnLoadFailed;
            
            UnregisterFromTickManager();
            _lastUnscaledTickTime = 0f;
        }

        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshLocalizedTexts();
            RefreshLoadingLocalization();
        }

        private void RefreshLocalizedTexts()
        {
            LocalizationManager loc = LocalizationManager.Instance;
            if (loc == null)
                return;

            ConfigureAdaptiveLabels();
            if (labelNewGame != null) labelNewGame.SetText(loc.Get(LocalizationKeys.MENU_NEW_GAME));
            if (labelLoadGame != null) labelLoadGame.SetText(loc.Get(LocalizationKeys.MENU_LOAD_GAME));
            if (labelSettings != null) labelSettings.SetText(loc.Get(LocalizationKeys.MENU_SETTINGS));
            if (labelQuit != null) labelQuit.SetText(loc.Get(LocalizationKeys.MENU_QUIT));
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
            BindButton(btnNewGame, OnNewGameClicked);
            BindButton(btnLoadGame, OnLoadGameClicked);
            BindButton(btnSettings, OnSettingsClicked);
            BindButton(btnQuit, OnQuitClicked);
            BindButton(btnBackFromSaveLoad, OnBackFromSaveLoadClicked);
            BindButton(btnBackFromSettings, OnBackFromSettingsClicked);

            if (btnSettings != null)
                btnSettings.interactable = _settingsAvailable;
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
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
            LocalizationManager loc = LocalizationManager.Instance;
            ModalWindow.Show(
                loc != null ? loc.Get(LocalizationKeys.MODAL_NEW_GAME_TITLE) : "New Game",
                loc != null ? loc.Get(LocalizationKeys.MODAL_NEW_GAME_MESSAGE) : "Start a new game?",
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
            LocalizationManager loc = LocalizationManager.Instance;
            ModalWindow.Show(
                loc != null ? loc.Get(LocalizationKeys.MODAL_QUIT_TITLE) : "Quit",
                loc != null ? loc.Get(LocalizationKeys.MODAL_QUIT_MESSAGE) : "Quit the game?",
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

            return group.GetComponentInChildren<TMP_Text>(true);
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
            Debug.LogError(
                $"[MainMenuController] Required CanvasGroup missing on '{objectName}'. " +
                "Author the component in 01_MAIN_MENU instead of patching it at runtime.");
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

            return button.GetComponentInChildren<TMP_Text>(true);
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
                _saveManager = SaveManager.Instance;

            // TASK 31: Comprehensive null check for SaveManager
            if (_saveManager == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[MainMenuController] SaveManager.Instance is null. Save/Load features unavailable.");
#endif
                // Display error message to user
                LocalizationManager loc = LocalizationManager.Instance;
                string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_MANAGER_UNAVAILABLE) : "Save System Unavailable";
                string message = loc != null 
                    ? loc.Get(LocalizationKeys.ERROR_SAVE_SYSTEM_UNAVAILABLE_MESSAGE) 
                    : "The save system is currently unavailable.\n\nPlease restart the game or contact support if this persists.";

                ModalWindow.ShowWithCustomLabels(
                    title,
                    message,
                    () => SwitchPanel(saveLoadGroup, mainMenuGroup), // Return to main menu
                    null,
                    ResolveCommonLabel(loc, LocalizationKeys.UI_RETURN_TO_MENU, "Return to Menu"),
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
                Debug.LogWarning(
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
            Debug.LogError("[MainMenuController] Save shell requires three scene-owned SaveSlotUI entries.");
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

                SaveSlotUI slotUi = child.GetComponent<SaveSlotUI>();
                if (slotUi == null)
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
                Debug.LogWarning(
                    $"[MainMenuController] Save shell bound {found}/{SlotCount} slot instances. " +
                    "Fallback focus/back handling remains active, but the authored slot shell is incomplete.");
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
                Debug.LogWarning("[MainMenuController] Ignored empty slot click.");
#endif
                return;
            }

            LocalizationManager loc = LocalizationManager.Instance;
            string displaySlotName = BuildSlotDisplayName(loc, slotName);
            string title = loc != null ? loc.Get(LocalizationKeys.MODAL_LOAD_TITLE) : "Load Game";
            string message = loc != null
                ? loc.GetFormatted(LocalizationKeys.MODAL_LOAD_MESSAGE, displaySlotName)
                : string.Concat("Load save \"", displaySlotName, "\"?");

            ModalWindow.Show(title, message, () => StartGame(slotName));
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
                    _saveManager = SaveManager.Instance;

                if (_saveManager == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError("[MainMenuController] SaveManager.Instance is null. Cannot validate save file.");
#endif
                    LocalizationManager loc = LocalizationManager.Instance;
                    string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_MANAGER_UNAVAILABLE) : "Save System Unavailable";
                    string message = loc != null
                        ? loc.Get(LocalizationKeys.ERROR_SAVE_SYSTEM_UNAVAILABLE_MESSAGE)
                        : "The save system is currently unavailable.\n\nCannot load save file.";

                    ModalWindow.ShowWithCustomLabels(
                        title,
                        message,
                        () => OpenSaveLoadMenu(), // Return to save/load menu
                        null,
                        ResolveCommonLabel(loc, LocalizationKeys.UI_OK, "OK"),
                        null);
                    return;
                }

                if (!_saveManager.SaveExists(slotName))
                {
                    LocalizationManager loc = LocalizationManager.Instance;
                    string displaySlotName = BuildSlotDisplayName(loc, slotName);
                    string title = loc != null ? loc.Get(LocalizationKeys.MODAL_LOAD_ERROR_TITLE) : "Load Error";
                    string message = loc != null
                        ? loc.GetFormatted(LocalizationKeys.MODAL_LOAD_ERROR_MESSAGE, displaySlotName)
                        : $"Save file does not exist for {displaySlotName}.";

                    ModalWindow.ShowWithCustomLabels(
                        title,
                        message,
                        () => OpenSaveLoadMenu(), // Return to save/load menu
                        null,
                        ResolveCommonLabel(loc, LocalizationKeys.UI_OK, "OK"),
                        null);
                    return;
                }
            }

            _isSceneLoadInFlight = true;

            GameStartContext context = string.IsNullOrEmpty(slotName)
                ? GameStartContext.CreateNewGame()
                : GameStartContext.CreateLoadGame(slotName);

            GameStartContextHolder.SetCurrent(context);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameStartContextHolder.LogCurrent();
#endif

            TryRegisterToTickManager();
            SetPanelImmediate(mainMenuGroup, false);
            SetPanelImmediate(saveLoadGroup, false);
            SetPanelImmediate(settingsGroup, false);
            SetPanelImmediate(loadingGroup, true);
            _currentPanel = loadingGroup;
            RequestSelectionRefresh();

            // Start loading tips
            if (loadingTips != null)
                loadingTips.StartTipCycle();

            _loadingPercentTemplate = ResolveLoadingPercentTemplate();
            _sceneActivationRequested = false;
            UpdateLoadingProgressVisual(0);

            _sceneLoadOperation = SceneManager.LoadSceneAsync(targetSceneName);
            if (_sceneLoadOperation == null)
            {
                _isSceneLoadInFlight = false;

#if UNITY_EDITOR
                Debug.LogError(
                    $"[MainMenuController] Failed to load scene \"{targetSceneName}\". " +
                    "Ensure it is added to Build Settings!");
#endif

                LocalizationManager loc = LocalizationManager.Instance;
                string title = loc != null ? loc.Get(LocalizationKeys.MODAL_SCENE_LOAD_ERROR_TITLE) : "Scene Load Error";
                string message = loc != null
                    ? loc.GetFormatted(LocalizationKeys.MODAL_SCENE_LOAD_ERROR_MESSAGE, targetSceneName)
                    : $"Failed to load scene \"{targetSceneName}\". Check Build Settings.";

                ModalWindow.ShowWithCustomLabels(
                    title,
                    message,
                    () => StartGame(slotName), // Retry
                    () => { SetPanelImmediate(loadingGroup, false); SetPanelImmediate(mainMenuGroup, true); }, // Cancel
                    ResolveCommonLabel(loc, LocalizationKeys.UI_RETRY, "Retry"),
                    ResolveCommonLabel(loc, LocalizationKeys.UI_RETURN_TO_MENU, "Return to Menu"));

                return;
            }

            _sceneLoadOperation.allowSceneActivation = false;
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

            HandleCancelInput();
            UpdatePanelTransition(unscaledDeltaTime);
            UpdateSceneLoad();
            RefreshSelectionIfNeeded();
        }

        private void HandleCancelInput()
        {
            // Input spam protection: ignore input during transitions or scene loading
            if (_isTransitioning ||
                _isSceneLoadInFlight ||
                _isSaveLoadBusy ||
                Time.unscaledTime < _cancelInputBlockedUntil ||
                !Input.GetKeyDown(KeyCode.Escape))
                return;

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
            _cancelInputBlockedUntil = Time.unscaledTime + CancelInputDebounceSeconds;
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

        private static string BuildSlotDisplayName(LocalizationManager loc, string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return "?";

            string slotPrefix = loc != null
                ? loc.GetOrFallback(loc.CurrentLanguage, LocalizationKeys.SLOT_PREFIX, "SLOT")
                : "SLOT";

            return string.Concat(slotPrefix, " ", ExtractSlotNumber(slotName));
        }

        private static string ExtractSlotNumber(string slotName)
        {
            int underscoreIndex = slotName.LastIndexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < slotName.Length - 1)
                return slotName.Substring(underscoreIndex + 1);

            return slotName;
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

            float duration = Mathf.Max(0.0001f, fadeDuration);
            _transitionElapsed += unscaledDeltaTime;
            float t = Mathf.Clamp01(_transitionElapsed / duration);

            if (_panelTransitionState == PanelTransitionState.FadingOut)
            {
                _transitionFromPanel.alpha = Mathf.Lerp(_transitionStartAlpha, 0f, t);
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

        private void UpdateSceneLoad()
        {
            if (_sceneLoadOperation == null)
                return;

            if (_sceneLoadOperation.isDone)
            {
                // Stop loading tips when scene loads
                if (loadingTips != null)
                    loadingTips.StopTipCycle();

                _sceneLoadOperation = null;
                return;
            }

            float progress = Mathf.Clamp01(_sceneLoadOperation.progress / 0.9f);
            int percent = Mathf.Clamp(Mathf.RoundToInt(progress * 100f), 0, 100);
            UpdateLoadingProgressVisual(percent);

            if (_sceneActivationRequested || _sceneLoadOperation.progress < 0.9f)
                return;

            UpdateLoadingProgressVisual(100);
            _sceneActivationRequested = true;
            _sceneLoadOperation.allowSceneActivation = true;
        }

        /// <summary>
        /// Updates loading progress visual with zero-GC dirty flag pattern.
        /// Uses TMP_Text.SetText(string, object) which is allocation-free.
        /// </summary>
        private void UpdateLoadingProgressVisual(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);

            if (loadingProgressBar != null)
                loadingProgressBar.value = percent / 100f;

            // Dirty flag: only update text when value changes (zero-GC)
            if (loadingPercentText != null && _lastLoadingPercent != percent)
            {
                loadingPercentText.SetText(_loadingPercentTemplate, percent);
                _lastLoadingPercent = percent;
            }
        }

        private string ResolveLoadingPercentTemplate()
        {
            LocalizationManager loc = LocalizationManager.Instance;
            return loc != null ? loc.Get(LocalizationKeys.LOADING_PERCENT) : "{0}%";
        }

        private void RefreshLoadingLocalization()
        {
            _loadingPercentTemplate = ResolveLoadingPercentTemplate();

            if (loadingPercentText == null || _lastLoadingPercent < 0)
                return;

            loadingPercentText.SetText(_loadingPercentTemplate, _lastLoadingPercent);
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
        private void OnSaveStarted(string slotName)
        {
            _isSaveLoadBusy = true;
            SetSaveLoadButtonsInteractable(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MainMenuController] Save started: {slotName}");
#endif
        }

        /// <summary>
        /// Called when save operation completes successfully.
        /// Re-enables buttons and updates slot metadata.
        /// </summary>
        private void OnSaveCompleted(string slotName)
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
            Debug.Log($"[MainMenuController] Save completed: {slotName}");
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

            // Display error modal
            LocalizationManager loc = LocalizationManager.Instance;
            string displaySlotName = BuildSlotDisplayName(loc, slotName);
            string title = loc != null ? loc.Get(LocalizationKeys.ERROR_SAVE_FAILED_TITLE) : "Save Failed";
            string message = loc != null
                ? loc.GetFormatted(LocalizationKeys.ERROR_SAVE_FAILED_MESSAGE, displaySlotName, error)
                : $"Failed to save to {displaySlotName}.\n\n{error}";

            ModalWindow.ShowWithCustomLabels(
                title,
                message,
                null, // No retry in main menu (only in pause menu)
                null,
                ResolveCommonLabel(loc, LocalizationKeys.UI_OK, "OK"),
                null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[MainMenuController] Save failed: {slotName} - {error}");
#endif

            RequestSelectionRefresh();
        }

        /// <summary>
        /// Called when load operation starts. Sets busy flag and disables load buttons.
        /// </summary>
        private void OnLoadStarted(string slotName)
        {
            _isSaveLoadBusy = true;
            _lastLoadUsedBackup = false;
            SetSaveLoadButtonsInteractable(false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MainMenuController] Load started: {slotName}");
#endif
        }

        /// <summary>
        /// Called when load operation completes successfully.
        /// Re-enables buttons and proceeds with game start.
        /// </summary>
        private void OnLoadCompleted(string slotName)
        {
            _isSaveLoadBusy = false;

            // Check if backup was used (SaveManager should set this flag)
            if (_saveManager != null && _saveManager.LastLoadUsedBackup)
            {
                _lastLoadUsedBackup = true;

                // Display backup recovery notification
                LocalizationManager loc = LocalizationManager.Instance;
                string displaySlotName = BuildSlotDisplayName(loc, slotName);
                string title = loc != null ? loc.Get(LocalizationKeys.WARNING_BACKUP_USED_TITLE) : "Backup Loaded";
                string message = loc != null
                    ? loc.GetFormatted(LocalizationKeys.WARNING_BACKUP_USED_MESSAGE, displaySlotName)
                    : $"Primary save file was corrupt. Loaded from backup for {displaySlotName}.";

                ModalWindow.ShowWithCustomLabels(
                    title,
                    message,
                    null,
                    null,
                    ResolveCommonLabel(loc, LocalizationKeys.UI_OK, "OK"),
                    null);
            }

            SetSaveLoadButtonsInteractable(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MainMenuController] Load completed: {slotName} (backup used: {_lastLoadUsedBackup})");
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

            // Check if error indicates corrupt save with no backup
            bool isCorruptNoBackup = IsCorruptNoBackupError(error);

            LocalizationManager loc = LocalizationManager.Instance;
            string displaySlotName = BuildSlotDisplayName(loc, slotName);
            string title = loc != null ? loc.Get(LocalizationKeys.ERROR_LOAD_FAILED_TITLE) : "Load Failed";
            string message;

            if (isCorruptNoBackup)
            {
                message = loc != null
                    ? loc.GetFormatted(LocalizationKeys.ERROR_LOAD_CORRUPT_NO_BACKUP_MESSAGE, displaySlotName)
                    : $"No valid save data found for {displaySlotName}.\n\nThe save file is corrupt and no backup is available.";
            }
            else
            {
                message = loc != null
                    ? loc.GetFormatted(LocalizationKeys.ERROR_LOAD_FAILED_MESSAGE, displaySlotName, error)
                    : $"Failed to load {displaySlotName}.\n\n{error}";
            }

            ModalWindow.ShowWithCustomLabels(
                title,
                message,
                () => StartGame(slotName), // Retry
                () => SwitchPanel(saveLoadGroup, mainMenuGroup), // Return to menu
                ResolveCommonLabel(loc, LocalizationKeys.UI_RETRY, "Retry"),
                ResolveCommonLabel(loc, LocalizationKeys.UI_RETURN_TO_MENU, "Return to Menu"));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[MainMenuController] Load failed: {slotName} - {error}");
#endif

            RequestSelectionRefresh();
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
            float currentTime = Time.unscaledTime;
            if (_lastUnscaledTickTime <= 0f)
            {
                _lastUnscaledTickTime = currentTime;
                return 0f;
            }

            float delta = currentTime - _lastUnscaledTickTime;
            _lastUnscaledTickTime = currentTime;
            return delta > 0f ? delta : 0f;
        }

        private static string ResolveCommonLabel(LocalizationManager loc, string key, string fallback)
        {
            return loc != null
                ? loc.GetOrFallback(loc.CurrentLanguage, key, fallback)
                : fallback;
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Register(this);
            _registeredToTickManager = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTickManager || GameTickManager.Instance == null)
                return;

            GameTickManager.Instance.Unregister(this);
            _registeredToTickManager = false;
        }

        private void SetPanelImmediate(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (visible)
                _currentPanel = group;
        }
    }
}
