// ============================================================================
// HECTON-8 — PlayerPDA.cs  v2.0 ENTERPRISE
// Персональный дата-ассистент (inventory / loadout / construction / barter / data log).
// Назначить на Player root. Управляет Canvas-панелью PDA.
//
// v2.0 ENTERPRISE ADDITIONS:
//   [ADD] PDAEvents — queue-backed global PDA event lane (Opened, Closed, TabChanged)
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
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Input;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Глобальная шина событий PDA. Zero GC, thread-safe.
    /// Подписчики: HUD, аудио, аналитика, сохранения.
    /// </summary>
    public enum PDAEventType : byte
    {
        Opened = 0,
        Closed = 1,
        TabChanged = 2,
        LowBatteryShutdown = 3
    }

    /// <summary>
    /// Blittable PDA event payload queued by <see cref="PDAEvents"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PDAEventPayload
    {
        public float DurationSeconds;
        public int PreviousTab;
        public int CurrentTab;
        public ushort EventType;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for queue-drained PDA events.
    /// </summary>
    public interface IPDAEventListener
    {
        void OnPDAEvent(in PDAEventPayload payload);
    }

    /// <summary>
    /// Queue-backed PDA event lane flushed from SystemDispatcher.LateUpdate.
    /// </summary>
    public static class PDAEvents
    {
        // COLD ALLOC: RegistryBucket<IPDAEventListener>[32] - PDA listeners drained by SystemDispatcher LateUpdate - owner: PDAEvents
        private static readonly RegistryBucket<IPDAEventListener> _listeners = new RegistryBucket<IPDAEventListener>(32);
        private static NativeQueue<PDAEventPayload> _pendingEvents;

        public static void Register(IPDAEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IPDAEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (_pendingEvents.TryDequeue(out PDAEventPayload payload))
            {
                IPDAEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnPDAEvent(in payload);
            }
        }

        internal static void RaiseOpened(int tab)
        {
            Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = tab,
                EventType = (ushort)PDAEventType.Opened,
                Reserved = 0
            });
        }

        internal static void RaiseClosed(float duration)
        {
            Enqueue(new PDAEventPayload
            {
                DurationSeconds = duration,
                PreviousTab = -1,
                CurrentTab = -1,
                EventType = (ushort)PDAEventType.Closed,
                Reserved = 0
            });
        }

        internal static void RaiseTabChanged(int oldTab, int newTab)
        {
            Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = oldTab,
                CurrentTab = newTab,
                EventType = (ushort)PDAEventType.TabChanged,
                Reserved = 0
            });
        }

        internal static void RaiseLowBatteryShutdown()
        {
            Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = -1,
                EventType = (ushort)PDAEventType.LowBatteryShutdown,
                Reserved = 0
            });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents = new NativeQueue<PDAEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<PDAEventPayload>[32] - deferred PDA event lane flushed by SystemDispatcher LateUpdate - owner: PDAEvents
        }

        private static void Enqueue(in PDAEventPayload payload)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(payload);
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
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

        [Tooltip("Вкладки PDA. Порядок: 0=Inventory, 1=Loadout, 2=Construction, 3=Barter, 4=Data Log, 5=Spectrum, 6=Atlas Signal, 7=Diagnostics.")]
        [SerializeField] private GameObject[] tabs = new GameObject[8];

        [Tooltip("HectonSurvivalSystem для battery drain. Опционально.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Вкладка по умолчанию при открытии (0=Inventory, 1=Loadout, 2=Construction, 3=Barter, 4=Data Log, 5=Spectrum, 6=Atlas Signal, 7=Diagnostics).")]
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
        internal static PlayerPDA ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsOpen = false;
            ActiveRuntimeInstance = null;
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
        private CanvasGroup[] _tabCanvasGroups;

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
            ResolveTabReferences(createMissingTabs: false);
            IsOpen = false;
            _currentAlpha = 0f;
            _targetAlpha = 0f;

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

            PrepareRuntimeVisibility();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
                ActiveRuntimeInstance = this;

            TryRegister();
            SubscribeToInputManager();
        }

        private void Start()
        {
            ResolveTabReferences(createMissingTabs: false);
            TryRegister();

            SubscribeToInputManager();

            if (!_registered)
            {
                Debug.LogError(
                    "[PlayerPDA] PDA dispatcher registration failed at Start(). PDA tick loop will not run.");
            }

            if (InputManager.Instance == null)
            {
                Debug.LogError(
                    "[PlayerPDA] InputManager.Instance is null at Start(). " +
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
            ResolveEditorReferences();
        }

        private void ResolveEditorReferences()
        {
            EnsureTabArrayCapacity();

            if (pdaPanel == null)
            {
                ClearResolvedTabs();
            }
            else
            {
                Transform root = pdaPanel.transform;
                tabs[0] = ResolveExistingTab(root, "Tab_Inventory");
                tabs[1] = ResolveExistingTab(root, "Tab_Loadout");
                tabs[2] = ResolveExistingTab(root, "Tab_Construction");
                tabs[3] = ResolveExistingTab(root, "Tab_Barter");
                tabs[4] = ResolveExistingTab(root, "Tab_DataLog", "Tab_Reserved");
                tabs[5] = ResolveExistingTab(root, "Tab_Spectrum");
                tabs[6] = ResolveExistingTab(root, "Tab_AtlasSignal");
                tabs[7] = ResolveExistingTab(root, "Tab_Diagnostics");
            }

            if (pdaPanel != null && pdaCanvasGroup == null)
                pdaCanvasGroup = pdaPanel.GetComponent<CanvasGroup>();
        }

        private void ResolveTabReferences(bool createMissingTabs)
        {
            EnsureTabArrayCapacity();

            if (pdaPanel == null)
            {
                ClearResolvedTabs();
                return;
            }

            Transform root = pdaPanel.transform;
            GameObject inventory = ResolveExistingTab(root, "Tab_Inventory");
            GameObject loadout = ResolveExistingTab(root, "Tab_Loadout");
            GameObject construction = ResolveExistingTab(root, "Tab_Construction");
            GameObject barter = ResolveExistingTab(root, "Tab_Barter");
            GameObject dataLog = ResolveExistingTab(root, "Tab_DataLog", "Tab_Reserved");
            GameObject spectrum = ResolveExistingTab(root, "Tab_Spectrum");
            GameObject atlasSignal = ResolveExistingTab(root, "Tab_AtlasSignal");
            GameObject diagnostics = ResolveExistingTab(root, "Tab_Diagnostics");

            if (createMissingTabs)
            {
                if (barter == null)
                    barter = EnsureRuntimeTab(root, "Tab_Barter", typeof(PDABarterTab));

                if (spectrum == null)
                    spectrum = EnsureRuntimeTab(root, "Tab_Spectrum", typeof(Hecton8.UI.PDASpectrumTab));

                if (atlasSignal == null)
                    atlasSignal = EnsureRuntimeTab(root, "Tab_AtlasSignal", typeof(Hecton8.UI.PDAAtlasSignalTab));

                if (diagnostics == null)
                    diagnostics = EnsureRuntimeTab(root, "Tab_Diagnostics", typeof(Hecton8.UI.PDADiagnosticTerminal));
            }

            if (inventory == null &&
                loadout == null &&
                construction == null &&
                barter == null &&
                dataLog == null &&
                spectrum == null &&
                atlasSignal == null &&
                diagnostics == null)
            {
                ClearResolvedTabs();
                return;
            }

            ClearResolvedTabs();
            if (inventory != null)   tabs[0] = inventory;
            if (loadout != null)     tabs[1] = loadout;
            if (construction != null) tabs[2] = construction;
            if (barter != null)      tabs[3] = barter;
            if (dataLog != null)     tabs[4] = dataLog;
            if (spectrum != null)    tabs[5] = spectrum;
            if (atlasSignal != null) tabs[6] = atlasSignal;
            if (diagnostics != null) tabs[7] = diagnostics;
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

            CanvasGroup canvasGroup = tab.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = tab.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return tab;
        }

        private void EnsureTabArrayCapacity()
        {
            if (tabs == null || tabs.Length != 8)
                tabs = new GameObject[8]; // COLD ALLOC: GameObject[8] — PDA tab reference cache — owner: PlayerPDA
        }

        private void ClearResolvedTabs()
        {
            if (tabs == null)
                return;

            for (int i = 0; i < tabs.Length; i++)
                tabs[i] = null;
        }

        private static GameObject ResolveExistingTab(Transform root, string primaryName, string alternateName = null)
        {
            if (root == null)
                return null;

            Transform primary = root.Find(primaryName);
            if (primary != null)
                return primary.gameObject;

            if (!string.IsNullOrEmpty(alternateName))
            {
                Transform alternate = root.Find(alternateName);
                if (alternate != null)
                    return alternate.gameObject;
            }

            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild PDA")]
        private void RebuildPda()
        {
            ResolveTabReferences(createMissingTabs: true);
            ResolveEditorReferences();
        }
#endif

        private void OnDisable()
        {
            TryUnregister();
            UnsubscribeFromInputManager();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            // Закрываем при отключении компонента
            if (IsOpen) ForceClose();
        }

        private void OnDestroy()
        {
            TryUnregister();
            UnsubscribeFromInputManager();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
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

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registered = false;
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
            if (IsOpen && enableBatteryDrain)
            {
                if (survivalSystem == null)
                    TryResolveSurvivalSystemFromRuntimeContext();

                if (survivalSystem != null)
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

            PrepareRuntimeVisibility();
            IsOpen = true;
            _openStartTime = Time.time;
            _batteryDrainAccumulator = 0f;
            _lowBatteryWarningPlayed = false;

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
            ApplyTabVisibility(_activeTab);

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

            if (pdaCanvasGroup != null && !IsOpen)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            PrepareRuntimeVisibility();
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
            }
        }

        private void PrepareRuntimeVisibility()
        {
            if (!Application.isPlaying || pdaPanel == null)
                return;

            if (pdaCanvasGroup == null)
            {
                pdaCanvasGroup = pdaPanel.GetComponent<CanvasGroup>();
                if (pdaCanvasGroup == null)
                    pdaCanvasGroup = pdaPanel.AddComponent<CanvasGroup>();
            }

            EnsureTabCanvasGroups();

            pdaCanvasGroup.alpha = 0f;
            pdaCanvasGroup.interactable = false;
            pdaCanvasGroup.blocksRaycasts = false;

            ApplyTabVisibility(_activeTab);
        }

        private bool TryResolveSurvivalSystemFromRuntimeContext()
        {
            if (survivalSystem != null)
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return false;

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private void EnsureTabCanvasGroups()
        {
            if (tabs == null || tabs.Length == 0)
            {
                _tabCanvasGroups = Array.Empty<CanvasGroup>();
                return;
            }

            if (_tabCanvasGroups == null || _tabCanvasGroups.Length != tabs.Length)
                _tabCanvasGroups = new CanvasGroup[tabs.Length];

            for (int i = 0; i < tabs.Length; i++)
            {
                GameObject tab = tabs[i];
                if (tab == null)
                {
                    _tabCanvasGroups[i] = null;
                    continue;
                }

                CanvasGroup group = _tabCanvasGroups[i];
                if (group == null)
                {
                    group = tab.GetComponent<CanvasGroup>();
                    if (group == null)
                        group = tab.AddComponent<CanvasGroup>();
                    _tabCanvasGroups[i] = group;
                }

                SetCanvasGroupVisible(group, i == _activeTab);
            }
        }

        private void ApplyTabVisibility(int activeTab)
        {
            if (_tabCanvasGroups == null)
                return;

            for (int i = 0; i < _tabCanvasGroups.Length; i++)
                SetCanvasGroupVisible(_tabCanvasGroups[i], i == activeTab);
        }

        private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
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
            Hecton8.Core.IAudioService audioManager = Hecton8.Core.GlobalRegistry.Audio;
            if (audioManager == null) return;

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

    /// <summary>
    /// PDA diagnostics tab showing slow-tick memory and FPS state in a monospaced terminal layout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Diagnostic Terminal")]
    public sealed class PDADiagnosticTerminal : MonoBehaviour, ISlowTickable, IPDAEventListener
    {
        private const int DiagnosticsTabIndex = 7;
        private const string TitleText = "DIAGNOSTIC TERMINAL // PERF / HULL / OFFSET";
        private static readonly char[] TitleTextBuffer = TitleText.ToCharArray();

        private static readonly Color BackgroundColor = new Color(0.03f, 0.08f, 0.10f, 0.86f);
        private static readonly Color RuleColor = new Color(0.46f, 0.98f, 0.94f, 0.16f);
        private static readonly Color TitleColor = new Color(0.79f, 0.96f, 0.92f, 0.96f);
        private static readonly Color BodyColor = new Color(0.84f, 0.94f, 0.88f, 0.92f);

        [Header("References")]
        [SerializeField] private PlayerPDA playerPda;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        // COLD ALLOC: StringBuilder[192] — PDA diagnostics terminal text assembly — owner: PDADiagnosticTerminal
        private readonly StringBuilder _builder = new StringBuilder(192);

        private bool _built;
        private bool _registered;
        private CanvasGroup _group;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _bodyLabel;
        private HectonPlayerMovement _playerMovement;
        private SargassumMicroFaunaBoids _microFaunaBoids;
        private int _lastMemoryMb = int.MinValue;
        private int _lastFps = int.MinValue;
        private int _lastBoidCount = int.MinValue;
        private int _lastHullStressPercent = int.MinValue;
        private Vector3 _lastUniverseOffset = new Vector3(float.NaN, float.NaN, float.NaN);

        private void Awake()
        {
            if (playerPda == null)
                playerPda = GetComponentInParent<PlayerPDA>();

            ResolvePlayerMovementFromRuntimeContext();

            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            PDAEvents.Register(this);
            EvaluateTickRegistration();
            RefreshTerminal(force: true);
        }

        private void OnDisable()
        {
            PDAEvents.Unregister(this);
            UnregisterFromTickManager();
        }

        private void OnDestroy()
        {
            PDAEvents.Unregister(this);
            UnregisterFromTickManager();
        }

        public void SlowTick()
        {
            if (!IsDiagnosticsVisible())
                return;

            RefreshTerminal(force: false);
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePdaStateChanged(payload.CurrentTab);
                    break;
                case PDAEventType.Closed:
                    HandlePdaClosed(payload.DurationSeconds);
                    break;
                case PDAEventType.TabChanged:
                    HandlePdaTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void HandlePdaStateChanged(int initialTab)
        {
            EvaluateTickRegistration();
            if (initialTab == DiagnosticsTabIndex)
                RefreshTerminal(force: true);
        }

        private void HandlePdaClosed(float openDuration)
        {
            UnregisterFromTickManager();
        }

        private void HandlePdaTabChanged(int previousTab, int newTab)
        {
            EvaluateTickRegistration();
            if (newTab == DiagnosticsTabIndex)
                RefreshTerminal(force: true);
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            RectTransform root = GetComponent<RectTransform>();
            if (root == null)
                return;

            Image background = gameObject.GetComponent<Image>();
            if (background == null)
                background = gameObject.AddComponent<Image>();
            background.color = BackgroundColor;

            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            CreateRule(root, "RuleTop", -54f);
            CreateRule(root, "RuleBottom", -118f);

            _titleLabel = CreateText(root, "Title", labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.TopLeft, TitleColor);
            Anchor(_titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -14f), new Vector2(-16f, -42f));
            _titleLabel.SetCharArray(TitleTextBuffer, 0, TitleTextBuffer.Length);

            _bodyLabel = CreateText(root, "Body", numericFont != null ? numericFont : labelFont, 15f, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyColor);
            _bodyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(_bodyLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -72f), new Vector2(-16f, -236f));
            _bodyLabel.SetCharArray(Array.Empty<char>(), 0, 0);

            _built = true;
        }

        private void RefreshTerminal(bool force)
        {
            if (!_built || _bodyLabel == null)
                return;

            ResolveDiagnosticsSources();
            int fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            long totalMemoryBytes = GC.GetTotalMemory(false);
            int memoryMb = (int)(totalMemoryBytes / (1024L * 1024L));
            int boidCount = _microFaunaBoids != null ? _microFaunaBoids.BoidCount : 0;
            int hullStressPercent = _playerMovement != null
                ? Mathf.RoundToInt(Mathf.Clamp01(_playerMovement.CurrentHullStress01) * 100f)
                : 0;
            Vector3 universeOffset = HectonFloatingOrigin.Instance != null
                ? HectonFloatingOrigin.Instance.TotalUniverseOffset
                : HectonMapMagicVegetationBridge.GlobalTotalUniverseOffset;

            if (!force &&
                fps == _lastFps &&
                memoryMb == _lastMemoryMb &&
                boidCount == _lastBoidCount &&
                hullStressPercent == _lastHullStressPercent &&
                universeOffset == _lastUniverseOffset)
            {
                return;
            }

            _lastFps = fps;
            _lastMemoryMb = memoryMb;
            _lastBoidCount = boidCount;
            _lastHullStressPercent = hullStressPercent;
            _lastUniverseOffset = universeOffset;

            _builder.Clear();
            _builder.Append("GC RESERVED  ").Append(memoryMb).Append(" MB\n");
            _builder.Append("FRAME RATE   ").Append(fps).Append(" FPS\n");
            _builder.Append("BOIDS LIVE   ").Append(boidCount).Append('\n');
            _builder.Append("HULL STRESS  ").Append(hullStressPercent).Append("%\n");
            _builder.Append("UNIV OFFSET  ");
            AppendSignedRoundedVector(_builder, universeOffset);
            _builder.Append('\n');
            _builder.Append("SLOW TICK    2 HZ\n");
            _builder.Append("STATUS       ONLINE");
            _bodyLabel.SetText(_builder);
        }

        private void ResolveDiagnosticsSources()
        {
            ResolvePlayerMovementFromRuntimeContext();

            if (_microFaunaBoids == null)
                _microFaunaBoids = SargassumMicroFaunaBoids.ActiveRuntimeInstance;
        }

        private void ResolvePlayerMovementFromRuntimeContext()
        {
            if (_playerMovement != null)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool IsDiagnosticsVisible()
        {
            return isActiveAndEnabled &&
                   gameObject.activeInHierarchy &&
                   PlayerPDA.IsOpen &&
                   playerPda != null &&
                   playerPda.ActiveTab == DiagnosticsTabIndex;
        }

        private void EvaluateTickRegistration()
        {
            if (IsDiagnosticsVisible())
                RegisterToTickManager();
            else
                UnregisterFromTickManager();
        }

        private void RegisterToTickManager()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registered = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static void CreateRule(RectTransform parent, string name, float anchoredY)
        {
            // COLD ALLOC: GameObject[1] — PDA diagnostics divider rule — owner: PDADiagnosticTerminal
            GameObject ruleObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            ruleObject.layer = parent.gameObject.layer;
            RectTransform rect = ruleObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Anchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, anchoredY - 1f), new Vector2(-16f, anchoredY + 1f));
            Image image = ruleObject.GetComponent<Image>();
            image.color = RuleColor;
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateText(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color)
        {
            // COLD ALLOC: GameObject[1] — PDA diagnostics TMP label — owner: PDADiagnosticTerminal
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.layer = parent.gameObject.layer;
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            Hecton8.UI.TMP_TextRegistry.EnsureRegistered(text);
            return text;
        }

        private static void AppendSignedRoundedVector(StringBuilder builder, Vector3 value)
        {
            builder.Append('[');
            AppendSignedRounded(builder, value.x);
            builder.Append(',');
            AppendSignedRounded(builder, value.y);
            builder.Append(',');
            AppendSignedRounded(builder, value.z);
            builder.Append(']');
        }

        private static void AppendSignedRounded(StringBuilder builder, float value)
        {
            int rounded = Mathf.RoundToInt(value);
            if (rounded >= 0)
                builder.Append('+');

            builder.Append(rounded);
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}


