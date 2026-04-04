// ============================================================================
// HECTON-8 — PlayerPDA.cs  v2.0 ENTERPRISE
// Персональный дата-ассистент (inventory / loadout / construction / barter / data log).
// Назначить на Player root. Управляет Canvas-панелью PDA.
//
// v2.0 ENTERPRISE ADDITIONS:
//   [ADD] PDAEvents — глобальная шина событий (OnOpened, OnClosed, OnTabChanged)
//   [ADD] Audio feedback — open/close/tab switch sounds через SpatialAudioManager
//   [ADD] Panel slide animation — плавное появление/исчезновение Canvas
//   [ADD] Battery drain system — PDA потребляет энергию из HectonSurvivalSystem
//   [ADD] Low battery warning — автозакрытие при критическом заряде
//   [ADD] Tab history stack — возврат на предыдущую вкладку через Backspace
//   [ADD] Diagnostics — _debugIsOpen, _debugActiveTab, _debugBatteryDrain
//   [ADD] Null-safety — все ссылки проверяются, graceful degradation
//   [ADD] CanvasGroup fade — alpha transition для плавного появления
//
// АРХИТЕКТУРА:
//   • IsOpen — статическое свойство, читается HectonPlayerMovement
//     и PlayerInteraction для блокировки ввода (аналогично HectonFabricatorUI).
//   • Клавиша M (или из ControlScheme).
//   • Canvas-панель назначается в инспекторе — PDA не знает о содержимом.
//   • Вкладки (inventory, loadout, controls, data log) — дочерние GameObject'ы панели,
//     переключаются через SetActiveTab(int).
//   • Battery drain — опциональная интеграция с HectonSurvivalSystem.
//
// ZERO GC:
//   • Все события — делегаты без boxing
//   • Tab history — pre-allocated stack (max 8 entries)
//   • Audio clips — cached references, no string lookups
//   • CanvasGroup — cached component, no GetComponent per frame
//
// ИНТЕГРАЦИЯ:
//   HectonPlayerMovement.Tick() — гард: if (PlayerPDA.IsOpen) return;
//   PlayerInteraction.Tick()    — гард: if (PlayerPDA.IsOpen) return;
//   HectonSurvivalSystem        — опционально: DrainEnergy(batteryDrainRate * dt)
// ============================================================================

using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Input;
using System;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Глобальная шина событий PDA. Zero GC, thread-safe.
    /// Подписчики: HUD, аудио, аналитика, сохранения.
    /// </summary>
    public static class PDAEvents
    {
        /// <summary>Fired when PDA opens. Parameter: initial tab index.</summary>
        public static event Action<int> OnOpened;

        /// <summary>Fired when PDA closes. Parameter: was open for X seconds.</summary>
        public static event Action<float> OnClosed;

        /// <summary>Fired when tab changes. Parameters: (oldTab, newTab).</summary>
        public static event Action<int, int> OnTabChanged;

        /// <summary>Fired when battery critically low and PDA force-closes.</summary>
        public static event Action OnLowBatteryShutdown;

        internal static void RaiseOpened(int tab) => OnOpened?.Invoke(tab);
        internal static void RaiseClosed(float duration) => OnClosed?.Invoke(duration);
        internal static void RaiseTabChanged(int oldTab, int newTab) => OnTabChanged?.Invoke(oldTab, newTab);
        internal static void RaiseLowBatteryShutdown() => OnLowBatteryShutdown?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnOpened = null;
            OnClosed = null;
            OnTabChanged = null;
            OnLowBatteryShutdown = null;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player PDA")]
    public sealed class PlayerPDA : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("Корневой GameObject Canvas-панели PDA.")]
        [SerializeField] private GameObject pdaPanel;

        [Tooltip("CanvasGroup для fade-анимации. Если null — мгновенное появление.")]
        [SerializeField] private CanvasGroup pdaCanvasGroup;

        [Tooltip("Вкладки PDA. Порядок: 0=Inventory, 1=Loadout, 2=Construction, 3=Barter, 4=Data Log.")]
        [SerializeField] private GameObject[] tabs = new GameObject[5];

        [Tooltip("HectonSurvivalSystem для battery drain. Опционально.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Вкладка по умолчанию при открытии (0=Inventory, 1=Loadout, 2=Construction, 3=Barter, 4=Data Log).")]
        [SerializeField] private int defaultTab = 0;

        [Tooltip("Скорость fade-анимации (alpha/sec). 0 = мгновенно.")]
        [SerializeField, Range(0f, 10f)] private float fadeSpeed = 5f;

        [Tooltip("Включить battery drain. PDA потребляет энергию при открытии.")]
        [SerializeField] private bool enableBatteryDrain = true;

        [Tooltip("Энергия/сек при открытом PDA. 0.5 = 2 секунды на 1%.")]
        [SerializeField, Range(0f, 5f)] private float batteryDrainRate = 0.5f;

        [Tooltip("Критический уровень энергии (%). Ниже — PDA автозакрывается.")]
        [SerializeField, Range(0f, 20f)] private float lowBatteryThreshold = 5f;

        [Tooltip("Включить tab history (Backspace = назад).")]
        [SerializeField] private bool enableTabHistory = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────")]
        [Tooltip("Звук открытия PDA (holographic deploy).")]
        [SerializeField] private AudioClip openSound;

        [Tooltip("Звук закрытия PDA (holographic collapse).")]
        [SerializeField] private AudioClip closeSound;

        [Tooltip("Звук переключения вкладки (soft beep).")]
        [SerializeField] private AudioClip tabSwitchSound;

        [Tooltip("Звук low battery warning (alert tone).")]
        [SerializeField] private AudioClip lowBatterySound;

        [Tooltip("Громкость звуков PDA.")]
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ─────────────────────────────")]
        [SerializeField] private bool _debugIsOpen;
        [SerializeField] private int _debugActiveTab = -1;
        [SerializeField] private float _debugOpenDuration;
        [SerializeField] private float _debugCurrentAlpha;
        [SerializeField] private float _debugBatteryDrainAccum;
        [SerializeField] private int _debugTabHistoryDepth;

        // ══════════════════════════════════════════════════════════
        //  STATIC STATE — читается другими системами
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// True когда PDA открыт. Читается HectonPlayerMovement и
        /// PlayerInteraction для блокировки ввода.
        /// </summary>
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsOpen = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private int _activeTab = -1;
        private bool _registered;
        private bool _inputSubscribed;
        private InputManager _subscribedInputManager;

        // Fade animation
        private float _targetAlpha;
        private float _currentAlpha;
        private bool _isFading;

        // Battery drain
        private float _openStartTime;
        private float _batteryDrainAccumulator;
        private bool _lowBatteryWarningPlayed;

        // Tab history (pre-allocated stack, max 8 entries)
        private readonly int[] _tabHistory = new int[8];
        private int _tabHistoryCount;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public int ActiveTab => _activeTab;
        public bool IsFading => _isFading;
        public float CurrentAlpha => _currentAlpha;
        public GameObject PanelRoot => pdaPanel;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            AutoResolveTabs();
            IsOpen = false;
            _currentAlpha = 0f;
            _targetAlpha = 0f;

            if (pdaPanel != null) pdaPanel.SetActive(false);

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            // Auto-resolve CanvasGroup if not assigned
            if (pdaCanvasGroup == null && pdaPanel != null)
            {
                pdaCanvasGroup = pdaPanel.GetComponent<CanvasGroup>();
                if (pdaCanvasGroup == null)
                {
                    Debug.LogWarning(
                        "[PlayerPDA] No CanvasGroup found. Adding one for fade animation.");
                    pdaCanvasGroup = pdaPanel.AddComponent<CanvasGroup>();
                }
            }

            // Auto-resolve SurvivalSystem if not assigned
            if (survivalSystem == null && enableBatteryDrain)
            {
                if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
                    survivalSystem = playerTransform.GetComponent<HectonSurvivalSystem>();

                if (survivalSystem == null)
                {
                    Debug.LogWarning(
                        "[PlayerPDA] Battery drain enabled but no HectonSurvivalSystem found. " +
                        "Disabling battery drain.");
                    enableBatteryDrain = false;
                }
            }
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance == null) return;
            if (_registered) return;
            GameTickManager.Instance.Register(this);
            _registered = true;

            SubscribeToInputManager();
        }

        private void Start()
        {
            AutoResolveTabs();
            if (_registered) return;
            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }

            SubscribeToInputManager();

            if (InputManager.Instance == null)
            {
                Debug.LogError(
                    "[PlayerPDA] GameTickManager.Instance is null at Start(). " +
                    "PDA will not function.");
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
            AutoResolveTabs();

#if UNITY_EDITOR
            if (!Application.isPlaying && pdaPanel != null)
            {
                pdaPanel.SetActive(false);

                if (pdaCanvasGroup == null)
                    pdaCanvasGroup = pdaPanel.GetComponent<CanvasGroup>();

                if (pdaCanvasGroup != null)
                {
                    pdaCanvasGroup.alpha = 0f;
                    pdaCanvasGroup.interactable = false;
                    pdaCanvasGroup.blocksRaycasts = false;
                }
            }
#endif
        }

        private void AutoResolveTabs()
        {
            if (pdaPanel == null)
                return;

            Transform root = pdaPanel.transform;
            GameObject inventory = root.Find("Tab_Inventory")?.gameObject;
            GameObject loadout = root.Find("Tab_Loadout")?.gameObject;
            GameObject construction = root.Find("Tab_Construction")?.gameObject;
            GameObject barter = root.Find("Tab_Barter")?.gameObject;
            GameObject dataLog = root.Find("Tab_DataLog")?.gameObject ?? root.Find("Tab_Reserved")?.gameObject;

            if (inventory == null && loadout == null && construction == null && barter == null && dataLog == null)
                return;

            if (barter == null)
                barter = EnsureRuntimeTab(root, "Tab_Barter", typeof(PDABarterTab));

            if (tabs == null || tabs.Length != 5)
                tabs = new GameObject[5];

            if (inventory != null) tabs[0] = inventory;
            if (loadout != null) tabs[1] = loadout;
            if (construction != null) tabs[2] = construction;
            if (barter != null) tabs[3] = barter;
            if (dataLog != null) tabs[4] = dataLog;
        }

        private static GameObject EnsureRuntimeTab(Transform root, string name, Type tabComponentType)
        {
            if (root == null)
                return null;

            Transform existing = root.Find(name);
            if (existing != null)
            {
                if (tabComponentType != null && existing.GetComponent(tabComponentType) == null)
                    existing.gameObject.AddComponent(tabComponentType);
                return existing.gameObject;
            }

            GameObject tab = new GameObject(name, typeof(RectTransform));
            tab.layer = root.gameObject.layer;
            RectTransform rect = tab.GetComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 24f);
            rect.offsetMax = new Vector2(-24f, -72f);
            if (tabComponentType != null)
                tab.AddComponent(tabComponentType);
            tab.SetActive(false);
            return tab;
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            UnsubscribeFromInputManager();

            // Закрываем при отключении компонента
            if (IsOpen) ForceClose();
        }

        private void SubscribeToInputManager()
        {
            InputManager inputManager = InputManager.Instance;
            if (inputManager == null)
                return;

            if (_inputSubscribed && ReferenceEquals(_subscribedInputManager, inputManager))
                return;

            UnsubscribeFromInputManager();

            inputManager.OnPDA += HandlePDAInput;
            inputManager.OnInventory += HandleInventoryInput;
            inputManager.OnCancel += HandleCancelInput;
            inputManager.OnTabPrevious += HandleBackInput;
            inputManager.OnTabNext += HandleTabNextInput;
            _subscribedInputManager = inputManager;
            _inputSubscribed = true;
        }

        private void UnsubscribeFromInputManager()
        {
            if (!_inputSubscribed)
                return;

            if (_subscribedInputManager != null)
            {
                _subscribedInputManager.OnPDA -= HandlePDAInput;
                _subscribedInputManager.OnInventory -= HandleInventoryInput;
                _subscribedInputManager.OnCancel -= HandleCancelInput;
                _subscribedInputManager.OnTabPrevious -= HandleBackInput;
                _subscribedInputManager.OnTabNext -= HandleTabNextInput;
            }

            _subscribedInputManager = null;
            _inputSubscribed = false;
        }

        // ══════════════════════════════════════════════════════════
        //  TICK
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            SubscribeToInputManager();

            // Input is now handled via events in HandlePDAInput, etc.

            // ── Fade animation ──
            if (_isFading)
            {
                ProcessFadeAnimation(deltaTime);
            }

            // ── Battery drain ──
            if (IsOpen && enableBatteryDrain && survivalSystem != null)
            {
                ProcessBatteryDrain(deltaTime);
            }

            // ── Diagnostics ──
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open(int tab = -1)
        {
            if (IsOpen) return;

            IsOpen = true;
            _openStartTime = Time.time;
            _batteryDrainAccumulator = 0f;
            _lowBatteryWarningPlayed = false;

            if (pdaPanel != null) pdaPanel.SetActive(true);

            // Switch to UI input map
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SwitchToUIInput();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            int targetTab = tab >= 0 ? tab : defaultTab;
            SetActiveTab(targetTab);

            // Start fade-in animation
            _targetAlpha = 1f;
            _isFading = true;

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.interactable = false; // block until fade complete
                pdaCanvasGroup.blocksRaycasts = false;
            }

            PlaySound(openSound);
            PDAEvents.RaiseOpened(targetTab);
        }

        public void Close()
        {
            if (!IsOpen) return;

            float duration = Time.time - _openStartTime;

            IsOpen = false;

            // Switch back to Player input map
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SwitchToPlayerInput();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;

            // Start fade-out animation
            _targetAlpha = 0f;
            _isFading = true;

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            PlaySound(closeSound);
            PDAEvents.RaiseClosed(duration);

            ClearTabHistory();
        }

        /// <summary>Переключить вкладку (0=Inventory, 1=Controls, 2=Data Log).</summary>
        public void SetActiveTab(int index)
        {
            if (tabs == null || tabs.Length == 0) return;

            int newTab = Mathf.Clamp(index, 0, tabs.Length - 1);
            if (newTab == _activeTab) return;

            int oldTab = _activeTab;

            // Push old tab to history (if valid and history enabled)
            if (enableTabHistory && oldTab >= 0)
                PushTabHistory(oldTab);

            _activeTab = newTab;

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                    tabs[i].SetActive(i == _activeTab);
            }

            if (oldTab >= 0) // not initial open
            {
                PlaySound(tabSwitchSound);
                PDAEvents.RaiseTabChanged(oldTab, newTab);
            }
        }

        /// <summary>Программное закрытие без анимации (для OnDisable).</summary>
        public void ForceClose()
        {
            if (!IsOpen) return;

            float duration = Time.time - _openStartTime;

            IsOpen = false;
            _isFading = false;
            _currentAlpha = 0f;
            _targetAlpha = 0f;

            if (pdaPanel != null) pdaPanel.SetActive(false);

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            // Switch back to Player input map on force close
            if (InputManager.Instance != null)
            {
                InputManager.Instance.SwitchToPlayerInput();
            }

            PDAEvents.RaiseClosed(duration);
            ClearTabHistory();
        }

        /// <summary>
        /// Allows runtime-generated UI to wire the PDA shell without reflection hacks.
        /// </summary>
        public void ConfigureUI(GameObject panelRoot, CanvasGroup panelCanvasGroup, GameObject[] configuredTabs)
        {
            pdaPanel = panelRoot;
            pdaCanvasGroup = panelCanvasGroup;
            tabs = configuredTabs ?? Array.Empty<GameObject>();

            if (pdaPanel != null && !IsOpen)
                pdaPanel.SetActive(false);

            if (pdaCanvasGroup != null && !IsOpen)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FADE ANIMATION
        // ══════════════════════════════════════════════════════════

        private void ProcessFadeAnimation(float deltaTime)
        {
            if (pdaCanvasGroup == null || fadeSpeed <= 0f)
            {
                // No CanvasGroup or instant mode — snap to target
                _currentAlpha = _targetAlpha;
                _isFading = false;

                if (_targetAlpha <= 0f && pdaPanel != null)
                    pdaPanel.SetActive(false);

                return;
            }

            // Exponential lerp for smooth fade
            float t = 1f - Mathf.Exp(-fadeSpeed * deltaTime);
            _currentAlpha = Mathf.Lerp(_currentAlpha, _targetAlpha, t);

            pdaCanvasGroup.alpha = _currentAlpha;

            // Check completion
            if (Mathf.Abs(_currentAlpha - _targetAlpha) < 0.01f)
            {
                _currentAlpha = _targetAlpha;
                pdaCanvasGroup.alpha = _currentAlpha;
                _isFading = false;

                if (_targetAlpha >= 1f)
                {
                    // Fade-in complete — enable interaction
                    pdaCanvasGroup.interactable = true;
                    pdaCanvasGroup.blocksRaycasts = true;
                }
                else if (_targetAlpha <= 0f)
                {
                    // Fade-out complete — hide panel
                    if (pdaPanel != null)
                        pdaPanel.SetActive(false);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — BATTERY DRAIN
        // ══════════════════════════════════════════════════════════

        private void ProcessBatteryDrain(float deltaTime)
        {
            _batteryDrainAccumulator += batteryDrainRate * deltaTime;

            // Drain energy every 1.0 accumulated units
            if (_batteryDrainAccumulator >= 1f)
            {
                int drainAmount = Mathf.FloorToInt(_batteryDrainAccumulator);
                _batteryDrainAccumulator -= drainAmount;

                survivalSystem.DrainEnergy(drainAmount);
            }

            // Check low battery
            float energyPercent = survivalSystem.EnergyPercent;

            if (energyPercent <= lowBatteryThreshold)
            {
                if (!_lowBatteryWarningPlayed)
                {
                    PlaySound(lowBatterySound);
                    _lowBatteryWarningPlayed = true;
                }

                // Force close on critical
                if (energyPercent <= 1f)
                {
                    PDAEvents.RaiseLowBatteryShutdown();
                    Close();
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TAB HISTORY
        // ══════════════════════════════════════════════════════════

        private void PushTabHistory(int tab)
        {
            if (_tabHistoryCount >= _tabHistory.Length)
            {
                // Stack full — shift left (drop oldest)
                for (int i = 0; i < _tabHistory.Length - 1; i++)
                    _tabHistory[i] = _tabHistory[i + 1];

                _tabHistoryCount = _tabHistory.Length - 1;
            }

            _tabHistory[_tabHistoryCount++] = tab;
        }

        private void PopTabHistory()
        {
            if (_tabHistoryCount <= 0) return;

            int previousTab = _tabHistory[--_tabHistoryCount];
            SetActiveTab(previousTab);
        }

        private void ClearTabHistory()
        {
            _tabHistoryCount = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            if (clip == null) return;
            if (!SpatialAudioManager.TryGetInstance(out SpatialAudioManager audioManager)) return;

            audioManager.PlayStatic2D(clip, audioVolume);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        private void UpdateDiagnostics()
        {
            _debugIsOpen = IsOpen;
            _debugActiveTab = _activeTab;
            _debugOpenDuration = IsOpen ? Time.time - _openStartTime : 0f;
            _debugCurrentAlpha = _currentAlpha;
            _debugBatteryDrainAccum = _batteryDrainAccumulator;
            _debugTabHistoryDepth = _tabHistoryCount;
        }

        // ══════════════════════════════════════════════════════════
        //  INPUT CALLBACKS (ZERO GC)
        // ══════════════════════════════════════════════════════════

        private void HandlePDAInput()
        {
            // PDA toggle is usually a player-map action, but if PDA is open, 
            // the UI map might also have a toggle or the Player map is disabled.
            // In our case, Open() switches to UI, but UI map might not have "PDA" action.
            // If InputManager handles "PDA" in both maps or if we stay in Player map for toggle:
            Toggle();
        }

        private void HandleInventoryInput()
        {
            if (!IsOpen)
            {
                Open(0);
                return;
            }

            SetActiveTab(0);
        }

        private void HandleCancelInput()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        private void HandleBackInput()
        {
            if (IsOpen && enableTabHistory)
            {
                PopTabHistory();
            }
        }
        private void HandleTabNextInput()
        {
            if (!IsOpen) return;
            if (tabs == null || tabs.Length == 0) return;

            int next = _activeTab + 1;
            if (next >= tabs.Length) next = 0;
            SetActiveTab(next);
        }
    }
}
