using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Hecton.Localization;
using Hecton8.SaveSystem;    // ← ВАШ СУЩЕСТВУЮЩИЙ SaveManager
using Hecton8.Core;         // ← GameStartContext

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Controller for the main menu scene. Manages panel transitions,
    /// save slot generation, and async scene loading.
    /// All UI text is driven through LocalizationManager.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        // INSPECTOR — Panels (CanvasGroup)
        // ──────────────────────────────────────────────
        [Header("=== PANELS (CanvasGroup) ===")]
        [SerializeField] private CanvasGroup mainMenuGroup;
        [SerializeField] private CanvasGroup saveLoadGroup;
        [SerializeField] private CanvasGroup settingsGroup;
        [SerializeField] private CanvasGroup loadingGroup;

        // ──────────────────────────────────────────────
        // INSPECTOR — Save Slots
        // ──────────────────────────────────────────────
        [Header("=== SAVE SLOTS ===")]
        [SerializeField] private Transform slotsContainer;
        [SerializeField] private GameObject slotPrefab;

        // ──────────────────────────────────────────────
        // INSPECTOR — Main Menu Buttons
        // ──────────────────────────────────────────────
        [Header("=== MAIN MENU BUTTONS ===")]
        [SerializeField] private Button btnNewGame;
        [SerializeField] private Button btnLoadGame;
        [SerializeField] private Button btnSettings;
        [SerializeField] private Button btnQuit;

        // ──────────────────────────────────────────────
        // INSPECTOR — Main Menu Button Labels (TMP)
        // ──────────────────────────────────────────────
        [Header("=== BUTTON LABELS (auto-localized) ===")]
        [SerializeField] private TMP_Text labelNewGame;
        [SerializeField] private TMP_Text labelLoadGame;
        [SerializeField] private TMP_Text labelSettings;
        [SerializeField] private TMP_Text labelQuit;

        // ──────────────────────────────────────────────
        // INSPECTOR — Sub-panel Back Buttons
        // ──────────────────────────────────────────────
        [Header("=== SAVE/LOAD PANEL ===")]
        [SerializeField] private Button btnBackFromSaveLoad;

        [Header("=== SETTINGS PANEL ===")]
        [SerializeField] private Button btnBackFromSettings;

        // ──────────────────────────────────────────────
        // INSPECTOR — Loading Screen
        // ──────────────────────────────────────────────
        [Header("=== LOADING SCREEN ===")]
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TMP_Text loadingPercentText;

        // ──────────────────────────────────────────────
        // CONFIG
        // ──────────────────────────────────────────────
        [Header("=== CONFIG ===")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private string targetSceneName = "02_HECTON_WORLD";

        private const int SLOT_COUNT = 3;

        // Double-click protection
        private bool _isTransitioning;
        private bool _isSceneLoadInFlight;

        // Pre-allocated slot name array (zero-GC)
        private static readonly string[] SlotNames = { "slot_1", "slot_2", "slot_3" };

        // Cached reference to SaveManager (avoids repeated singleton access)
        private SaveManager _saveManager;
        private SaveSlotUI[] _slotUIs;
        private bool _slotPrefabValidated;
        private bool _slotPrefabHasSaveSlotUI = true;

        // ══════════════════════════════════════════════
        // LIFECYCLE
        // ══════════════════════════════════════════════

        private void Awake()
        {
            ValidateReferences();
            BindButtons();
            InitializePanelStates();
        }

        private void Start()
        {
            // Cache SaveManager reference after all Awake() calls
            _saveManager = SaveManager.Instance;

#if UNITY_EDITOR
            if (_saveManager == null)
            {
                Debug.LogWarning(
                    "[MainMenuController] SaveManager.Instance is null. " +
                    "Save/Load features will be unavailable. " +
                    "Ensure SaveManager exists in scene or is DontDestroyOnLoad."
                );
            }
#endif
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
            RefreshLocalizedTexts();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        // ══════════════════════════════════════════════
        // LOCALIZATION
        // ══════════════════════════════════════════════

        private void OnLanguageChanged(GameLanguage newLanguage)
        {
            RefreshLocalizedTexts();
        }

        private void RefreshLocalizedTexts()
        {
            LocalizationManager loc = LocalizationManager.Instance;
            if (loc == null) return;

            if (labelNewGame  != null) labelNewGame.SetText(loc.Get(LocalizationKeys.MENU_NEW_GAME));
            if (labelLoadGame != null) labelLoadGame.SetText(loc.Get(LocalizationKeys.MENU_LOAD_GAME));
            if (labelSettings != null) labelSettings.SetText(loc.Get(LocalizationKeys.MENU_SETTINGS));
            if (labelQuit     != null) labelQuit.SetText(loc.Get(LocalizationKeys.MENU_QUIT));
        }

        // ══════════════════════════════════════════════
        // INITIALIZATION
        // ══════════════════════════════════════════════

        private void ValidateReferences()
        {
#if UNITY_EDITOR
            Debug.Assert(mainMenuGroup  != null, "[MainMenuController] mainMenuGroup is not assigned!");
            Debug.Assert(saveLoadGroup  != null, "[MainMenuController] saveLoadGroup is not assigned!");
            Debug.Assert(settingsGroup  != null, "[MainMenuController] settingsGroup is not assigned!");
            Debug.Assert(loadingGroup   != null, "[MainMenuController] loadingGroup is not assigned!");
            Debug.Assert(slotsContainer != null, "[MainMenuController] slotsContainer is not assigned!");
            Debug.Assert(slotPrefab     != null, "[MainMenuController] slotPrefab is not assigned!");
#endif
        }

        private void BindButtons()
        {
            BindButton(btnNewGame,          OnNewGameClicked);
            BindButton(btnLoadGame,         OnLoadGameClicked);
            BindButton(btnSettings,         OnSettingsClicked);
            BindButton(btnQuit,             OnQuitClicked);
            BindButton(btnBackFromSaveLoad, OnBackFromSaveLoadClicked);
            BindButton(btnBackFromSettings, OnBackFromSettingsClicked);
        }

        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        private void InitializePanelStates()
        {
            SetPanelImmediate(mainMenuGroup, true);
            SetPanelImmediate(saveLoadGroup, false);
            SetPanelImmediate(settingsGroup, false);
            SetPanelImmediate(loadingGroup,  false);
        }

        // ══════════════════════════════════════════════
        // BUTTON CALLBACKS
        // ══════════════════════════════════════════════

        private void OnNewGameClicked()
        {
            LocalizationManager loc = LocalizationManager.Instance;

            ModalWindow.Show(
                loc != null ? loc.Get(LocalizationKeys.MODAL_NEW_GAME_TITLE)   : "New Game",
                loc != null ? loc.Get(LocalizationKeys.MODAL_NEW_GAME_MESSAGE) : "Start a new game?",
                () => StartGame(string.Empty)
            );
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
                loc != null ? loc.Get(LocalizationKeys.MODAL_QUIT_TITLE)   : "Quit",
                loc != null ? loc.Get(LocalizationKeys.MODAL_QUIT_MESSAGE) : "Quit the game?",
                () =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            );
        }

        private void OnBackFromSaveLoadClicked()
        {
            SwitchPanel(saveLoadGroup, mainMenuGroup);
        }

        private void OnBackFromSettingsClicked()
        {
            SwitchPanel(settingsGroup, mainMenuGroup);
        }

        // ══════════════════════════════════════════════
        // SAVE/LOAD — SLOT GENERATION
        // ══════════════════════════════════════════════

        /// <summary>
        /// Opens the Save/Load panel, clears the container, generates slots.
        /// Uses Hecton8.SaveSystem.SaveManager for metadata queries.
        /// </summary>
        public void OpenSaveLoadMenu()
        {
            EnsureSlotInstances();
            if (_slotUIs == null)
                return;

            // Re-cache in case SaveManager appeared after Start()
            if (_saveManager == null)
                _saveManager = SaveManager.Instance;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                string slotName = SlotNames[i];
                SaveSlotUI slotUI = _slotUIs[i];
                if (slotUI == null)
                    continue;

                if (_saveManager != null)
                {
                    if (_saveManager.TryGetSaveSlotInfo(slotName, out SaveSlotInfo slotInfo))
                    {
                        slotUI.Init(slotInfo, OnSlotClicked);
                    }
                    else
                    {
                        slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);
                    }
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
                    Debug.LogError(
                        "[MainMenuController] slotPrefab is missing SaveSlotUI component!"
                    );
#endif
                    return;
                }
            }

            _slotUIs = new SaveSlotUI[SLOT_COUNT]; // COLD ALLOC: fixed save-shell slot cache

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                GameObject slotGO = Instantiate(slotPrefab, slotsContainer);
                slotGO.name = SlotNames[i];
                _slotUIs[i] = slotGO.GetComponent<SaveSlotUI>();
            }
        }

        private void OnSlotClicked(string slotName)
        {
            LocalizationManager loc = LocalizationManager.Instance;

            string title = loc != null
                ? loc.Get(LocalizationKeys.MODAL_LOAD_TITLE)
                : "Load Game";

            string message = loc != null
                ? loc.GetFormatted(LocalizationKeys.MODAL_LOAD_MESSAGE, slotName)
                : string.Concat("Load save \"", slotName, "\"?");

            ModalWindow.Show(title, message, () => StartGame(slotName));
        }

        // ══════════════════════════════════════════════
        // ASYNC SCENE LOADING
        // ══════════════════════════════════════════════

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

            // Create GameStartContext
            GameStartContext context = string.IsNullOrEmpty(slotName)
                ? GameStartContext.CreateNewGame()
                : GameStartContext.CreateLoadGame(slotName);

            // Store in holder for SceneBootstrap to read
            GameStartContextHolder.SetCurrent(context);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameStartContextHolder.LogCurrent();
#endif

            StartCoroutine(LoadSceneRoutine());
        }

        private IEnumerator LoadSceneRoutine()
        {
            SetPanelImmediate(mainMenuGroup, false);
            SetPanelImmediate(saveLoadGroup, false);
            SetPanelImmediate(settingsGroup, false);
            SetPanelImmediate(loadingGroup,  true);

            if (loadingProgressBar != null) loadingProgressBar.value = 0f;
            if (loadingPercentText != null) loadingPercentText.SetText("0%");

            AsyncOperation operation = SceneManager.LoadSceneAsync(targetSceneName);

            if (operation == null)
            {
                _isSceneLoadInFlight = false;

#if UNITY_EDITOR
                Debug.LogError(
                    $"[MainMenuController] Failed to load scene \"{targetSceneName}\". " +
                    "Ensure it is added to Build Settings!"
                );
#endif
                SetPanelImmediate(loadingGroup,  false);
                SetPanelImmediate(mainMenuGroup, true);
                yield break;
            }

            operation.allowSceneActivation = false;

            LocalizationManager loc = LocalizationManager.Instance;
            string percentTemplate = loc != null
                ? loc.Get(LocalizationKeys.LOADING_PERCENT)
                : "{0}%";

            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);

                if (loadingProgressBar != null)
                    loadingProgressBar.value = progress;

                if (loadingPercentText != null)
                {
                    int percent = Mathf.RoundToInt(progress * 100f);
                    loadingPercentText.SetText(percentTemplate, percent);
                }

                if (operation.progress >= 0.9f)
                {
                    if (loadingProgressBar != null) loadingProgressBar.value = 1f;
                    if (loadingPercentText != null)
                    {
                        loadingPercentText.SetText(percentTemplate, 100);
                    }

                    yield return null;
                    operation.allowSceneActivation = true;
                }

                yield return null;
            }
        }

        // ══════════════════════════════════════════════
        // PANEL TRANSITION SYSTEM (FADE)
        // ══════════════════════════════════════════════

        /// <summary>
        /// Smoothly fades out 'from' panel and fades in 'to' panel.
        /// Double-click protected via instant interactable/blocksRaycasts toggle.
        /// </summary>
        public void SwitchPanel(CanvasGroup from, CanvasGroup to)
        {
            if (_isTransitioning) return;
            if (from == null || to == null) return;

            StartCoroutine(SwitchPanelRoutine(from, to));
        }

        private IEnumerator SwitchPanelRoutine(CanvasGroup from, CanvasGroup to)
        {
            _isTransitioning = true;

            // Instantly block both panels
            from.interactable   = false;
            from.blocksRaycasts = false;
            to.interactable     = false;
            to.blocksRaycasts   = false;

            // FADE OUT
            float elapsed = 0f;
            float startAlpha = from.alpha;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                from.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }
            from.alpha = 0f;

            // FADE IN
            elapsed = 0f;
            to.alpha = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                to.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
                yield return null;
            }
            to.alpha = 1f;

            // Unblock target panel
            to.interactable   = true;
            to.blocksRaycasts = true;

            _isTransitioning = false;
        }

        // ══════════════════════════════════════════════
        // UTILITIES
        // ══════════════════════════════════════════════

        private static void SetPanelImmediate(CanvasGroup group, bool visible)
        {
            if (group == null) return;

            group.alpha          = visible ? 1f : 0f;
            group.interactable   = visible;
            group.blocksRaycasts = visible;
        }
    }
}
