// ============================================================================
// HECTON-8 — PlayerPDA.cs  v2.0 ENTERPRISE
// Персональный дата-ассистент (карта/журнал/управление).
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
//   • Вкладки (карта, журнал, управление) — дочерние GameObject'ы панели,
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
using Hecton8.Core;
using Hecton8.Gameplay;
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

        [Tooltip("Вкладки PDA (карта, журнал, управление). " +
                 "Порядок: 0=Map, 1=Log, 2=Controls.")]
        [SerializeField] private GameObject[] tabs = new GameObject[3];

        [Tooltip("ControlScheme asset. Если null — используется mapKey.")]
        [SerializeField] private ControlScheme controlScheme;

        [Tooltip("Fallback клавиша если нет ControlScheme.")]
        [SerializeField] private KeyCode mapKey = KeyCode.M;

        [Tooltip("HectonSurvivalSystem для battery drain. Опционально.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Вкладка по умолчанию при открытии (0=Map, 1=Log, 2=Controls).")]
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

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private int _activeTab = -1;
        private bool _registered;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
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
                survivalSystem = FindObjectOfType<HectonSurvivalSystem>();
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
        }

        private void Start()
        {
            if (_registered) return;
            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register(this);
                _registered = true;
            }
            else
            {
                Debug.LogError(
                    "[PlayerPDA] GameTickManager.Instance is null at Start(). " +
                    "PDA will not function.");
            }
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null && _registered)
            {
                GameTickManager.Instance.Unregister(this);
                _registered = false;
            }

            // Закрываем при отключении компонента
            if (IsOpen) ForceClose();
        }

        // ══════════════════════════════════════════════════════════
        //  TICK
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            // ── Input ──
            KeyCode key = controlScheme != null ? controlScheme.mapKey : mapKey;

            if (Input.GetKeyDown(key))
                Toggle();

            // Escape закрывает PDA
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();

            // Backspace = назад по tab history
            if (IsOpen && enableTabHistory && Input.GetKeyDown(KeyCode.Backspace))
                PopTabHistory();

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

            Cursor.lockState = CursorLockMode.Locked;
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

        /// <summary>Переключить вкладку (0=Map, 1=Log, 2=Controls).</summary>
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

            PDAEvents.RaiseClosed(duration);
            ClearTabHistory();
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
            if (SpatialAudioManager.Instance == null) return;

            SpatialAudioManager.Instance.PlayStatic2D(clip, audioVolume);
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
    }
}
