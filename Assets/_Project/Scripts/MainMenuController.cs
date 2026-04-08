using Hecton.Localization;
using Hecton8.Core;
using Hecton8.SaveSystem;
using TMPro;
using UnityEngine;
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
        [SerializeField] private GameObject slotPrefab;

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

        [Header("=== CONFIG ===")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private string targetSceneName = "02_HECTON_WORLD";

        private const int SlotCount = 3;
        private static readonly string[] SlotNames = { "slot_1", "slot_2", "slot_3" };

        private bool _isTransitioning;
        private bool _isSceneLoadInFlight;
        private bool _registeredToTickManager;
        private bool _sceneActivationRequested;
        private bool _slotPrefabValidated;
        private bool _slotPrefabHasSaveSlotUI = true;
        private int _lastLoadingPercent = -1;
        private float _lastUnscaledTickTime;
        private float _transitionElapsed;
        private float _transitionStartAlpha;
        private string _loadingPercentTemplate = "{0}%";
        private SaveManager _saveManager;
        private SaveSlotUI[] _slotUIs;
        private AsyncOperation _sceneLoadOperation;
        private CanvasGroup _transitionFromPanel;
        private CanvasGroup _transitionToPanel;
        private PanelTransitionState _panelTransitionState;

        private void Awake()
        {
            ValidateReferences();
            BindButtons();
            InitializePanelStates();
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
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            RefreshLocalizedTexts();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            UnregisterFromTickManager();
            _lastUnscaledTickTime = 0f;
        }

        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshLocalizedTexts();
        }

        private void RefreshLocalizedTexts()
        {
            LocalizationManager loc = LocalizationManager.Instance;
            if (loc == null)
                return;

            if (labelNewGame != null) labelNewGame.SetText(loc.Get(LocalizationKeys.MENU_NEW_GAME));
            if (labelLoadGame != null) labelLoadGame.SetText(loc.Get(LocalizationKeys.MENU_LOAD_GAME));
            if (labelSettings != null) labelSettings.SetText(loc.Get(LocalizationKeys.MENU_SETTINGS));
            if (labelQuit != null) labelQuit.SetText(loc.Get(LocalizationKeys.MENU_QUIT));
        }

        private void ValidateReferences()
        {
#if UNITY_EDITOR
            Debug.Assert(mainMenuGroup != null, "[MainMenuController] mainMenuGroup is not assigned!");
            Debug.Assert(saveLoadGroup != null, "[MainMenuController] saveLoadGroup is not assigned!");
            Debug.Assert(settingsGroup != null, "[MainMenuController] settingsGroup is not assigned!");
            Debug.Assert(loadingGroup != null, "[MainMenuController] loadingGroup is not assigned!");
            Debug.Assert(slotsContainer != null, "[MainMenuController] slotsContainer is not assigned!");
            Debug.Assert(slotPrefab != null, "[MainMenuController] slotPrefab is not assigned!");
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
                    UnityEditor.EditorApplication.isPlaying = false;
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

        /// <summary>
        /// Opens the Save/Load panel, clears the container, generates slots.
        /// Uses Hecton8.SaveSystem.SaveManager for metadata queries.
        /// </summary>
        public void OpenSaveLoadMenu()
        {
            EnsureSlotInstances();
            if (_slotUIs == null)
                return;

            if (_saveManager == null)
                _saveManager = SaveManager.Instance;

            for (int i = 0; i < SlotCount; i++)
            {
                string slotName = SlotNames[i];
                SaveSlotUI slotUI = _slotUIs[i];
                if (slotUI == null)
                    continue;

                if (_saveManager != null && _saveManager.TryGetSaveSlotInfo(slotName, out SaveSlotInfo slotInfo))
                {
                    slotUI.Init(slotInfo, OnSlotClicked);
                }
                else
                {
                    slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);
                }
            }

            SwitchPanel(mainMenuGroup, saveLoadGroup);
        }

        private void EnsureSlotInstances()
        {
            if (_slotUIs != null)
                return;

            if (!_slotPrefabValidated)
            {
                _slotPrefabValidated = true;
                _slotPrefabHasSaveSlotUI = slotPrefab != null && slotPrefab.GetComponent<SaveSlotUI>() != null;

                if (!_slotPrefabHasSaveSlotUI)
                {
#if UNITY_EDITOR
                    Debug.LogError("[MainMenuController] slotPrefab is missing SaveSlotUI component!");
#endif
                    return;
                }
            }

            _slotUIs = new SaveSlotUI[SlotCount]; // COLD ALLOC: fixed save-shell slot cache

            for (int i = 0; i < SlotCount; i++)
            {
                GameObject slotGameObject = Instantiate(slotPrefab, slotsContainer);
                slotGameObject.name = SlotNames[i];
                _slotUIs[i] = slotGameObject.GetComponent<SaveSlotUI>();
            }
        }

        private void OnSlotClicked(string slotName)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            string title = loc != null ? loc.Get(LocalizationKeys.MODAL_LOAD_TITLE) : "Load Game";
            string message = loc != null
                ? loc.GetFormatted(LocalizationKeys.MODAL_LOAD_MESSAGE, slotName)
                : string.Concat("Load save \"", slotName, "\"?");

            ModalWindow.Show(title, message, () => StartGame(slotName));
        }

        /// <summary>
        /// Starts async loading of the game scene.
        /// Empty slotName = new game, otherwise = load save.
        /// Writes to GameStartContextHolder for inter-scene communication.
        /// Cold persistence is owned by the holder, not by MainMenuController.
        /// </summary>
        public void StartGame(string slotName)
        {
            if (_isSceneLoadInFlight)
                return;

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
                SetPanelImmediate(loadingGroup, false);
                SetPanelImmediate(mainMenuGroup, true);
                return;
            }

            _sceneLoadOperation.allowSceneActivation = false;
        }

        /// <summary>
        /// Smoothly fades out one panel and fades in the next.
        /// Double-click protected via instant interactable/blocksRaycasts toggle.
        /// </summary>
        public void SwitchPanel(CanvasGroup from, CanvasGroup to)
        {
            if (_isTransitioning || from == null || to == null)
                return;

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
        }

        public void Tick(float dt)
        {
            float unscaledDeltaTime = GetUnscaledDeltaTime();
            if (unscaledDeltaTime <= 0f)
                return;

            UpdatePanelTransition(unscaledDeltaTime);
            UpdateSceneLoad();
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
            _transitionFromPanel = null;
            _transitionToPanel = null;
            _isTransitioning = false;
        }

        private void UpdateSceneLoad()
        {
            if (_sceneLoadOperation == null)
                return;

            if (_sceneLoadOperation.isDone)
            {
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

        private void UpdateLoadingProgressVisual(int percent)
        {
            percent = Mathf.Clamp(percent, 0, 100);

            if (loadingProgressBar != null)
                loadingProgressBar.value = percent / 100f;

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

        private static void SetPanelImmediate(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
