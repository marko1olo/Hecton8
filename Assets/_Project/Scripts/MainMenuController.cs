using System;
using System.Collections.Generic;
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
    public sealed class MainMenuController : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, ISaveEventListener, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
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
        [SerializeField] private SettingsPanel settingsPanel;

        [Header("=== LOADING SCREEN ===")]
        [SerializeField] private Slider loadingProgressBar;
        [SerializeField] private TMP_Text loadingPercentText;
        [SerializeField] private LoadingTipsDisplay loadingTips;

        [Header("=== CINEMATIC TRANSITION ===")]
        [SerializeField, Tooltip("Scene-authored main-menu camera panned during the menu-to-world transition.")]
        private Camera mainMenuCamera;

        [Header("=== CONFIG ===")]
        [SerializeField] private float fadeDuration = 0.2f;
        [SerializeField] private string targetSceneName = DefaultGameplaySceneName;
        [SerializeField] private string newGameTargetSceneName = DefaultGameplaySceneName;
        [SerializeField] private MenuVisualStyle visualStyle = MenuVisualStyle.PressureVesselNoir;
        [SerializeField] private MenuVisualConcept visualConcept = MenuVisualConcept.ModuleWindowOverlay;
        [SerializeField, Range(-1f, 1f)] private float visualStyleQualityOverride = -1f;
        [SerializeField, Range(-1f, 1f)] private float visualConceptQualityOverride = -1f;

        private const int SlotCount = SaveEvents.ManualSlotCount;
        private const string BootstrapSceneName = "00_BOOTSTRAP";
        private const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string OrbitSceneName = "01_ORBIT";
        private const string DefaultGameplaySceneName = "02_HECTON_WORLD";
        private const float CancelInputDebounceSeconds = 0.35f;
        private const float InputRoutingRetrySeconds = 0.25f;
        private const float MaxMenuPresentationDeltaSeconds = 0.1f;
        private const uint DiegeticMenuHapticSourceHash = 0x4D4D3131u; // MM11
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
        private bool _registeredLateFrameTickManager;
        private bool _settingsAvailable;
        private bool _refreshSelectionRequested;
        private bool _isSaveLoadBusy;
        private int _lastLoadingPercent = -1;
        private readonly char[] _loadingPercentBuffer = new char[32]; // COLD ALLOC: loading percent TMP staging buffer - owner: MainMenuController
        private readonly char[] _loadingPercentTemplateBuffer = new char[32]; // COLD ALLOC: loading percent localized template staging buffer - owner: MainMenuController
        private readonly char[] _modalTitleBuffer = new char[64]; // COLD ALLOC: main-menu modal title staging buffer copied into ModalWindow title string - owner: MainMenuController
        private readonly char[] _modalMessageBuffer = new char[256]; // COLD ALLOC: main-menu modal message staging buffer copied directly into TMP - owner: MainMenuController
        private int _loadingPercentTemplateLength;
        private float _lastUnscaledTickTime;
        private float _menuPresentationDeltaTime;
        private float _cancelInputBlockedUntil;
        private float _nextInputRoutingRetryTime;
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
        private uint _lastConsumedFailureSnapshotSequence;
        private ulong _lastFailureNotificationSignature;
        private PanelTransitionState _panelTransitionState;
        private UnityAction _newGameClickAction;
        private UnityAction _loadGameClickAction;
        private UnityAction _settingsClickAction;
        private UnityAction _quitClickAction;
        private UnityAction _backFromSaveLoadClickAction;
        private UnityAction _backFromSettingsClickAction;
        private Action _startNewGameModalAction;
        private Action _startPendingGameModalAction;
        private Action _quitApplicationModalAction;
        private Action _openSaveLoadMenuModalAction;
        private Action _returnSaveLoadToMainMenuAction;
        private Action _returnLoadingToMainMenuAction;
        private bool _runtimeBindingsReady;
        private bool _inputRoutingReady;
        private bool _menuInputBound;
        private bool _registeredHotSwapListener;
        private EventSystem _cachedEventSystem;
        private InputSystemUIInputModule _cachedUiInputModule;
        private MenuVisualStyleApplier _visualStyleApplier;
        private MenuVisualConceptApplier _visualConceptApplier;
        private MenuVisualConceptDecorApplier _visualConceptDecorApplier;
        private DiegeticPanelController _diegeticPanelController;
        private DiegeticMenuRaycastReceiver _diegeticRaycastReceiver;
        private MenuCameraController _menuCameraController;
        private MainMenuAtmosphereController _menuAtmosphereController;
        private Canvas _diegeticCanvas;
        private RectTransform _diegeticCanvasRoot;
        private BoxCollider _diegeticPanelCollider;
        private SettingsManager _settingsManager;
        private ILocalizationTextReadModel _localization;
        // COLD ALLOC: active scene camera search scratch - owner: MainMenuController setup.
        private readonly List<GameObject> _cameraRootSearchBuffer = new List<GameObject>(8);
        // COLD ALLOC: active scene camera search scratch - owner: MainMenuController setup.
        private readonly List<Camera> _cameraSearchBuffer = new List<Camera>(4);
        private string _pendingStartSlotName = string.Empty;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;


        private void Awake()
        {
            // Bootstrap routinely reaches 01_MAIN_MENU before its ordered phases finish. That is
            // the production route, so a still-initializing boot must not disable this menu:
            // disabling inside Awake also skips OnEnable and Start, so input binding and tick
            // registration never ran and nothing was left to re-enable the component. Late
            // services are already handled here by the idempotent OnEnable/Start caching and by
            // OnGlobalRegistryServiceReplaced.
            BootstrapRouteStatus routeStatus = BootstrapRouteEnforcer.EvaluateBootstrapRuntimeRoute(
                gameObject.scene.name,
                nameof(MainMenuController));

            if (routeStatus == BootstrapRouteStatus.Recovering ||
                routeStatus == BootstrapRouteStatus.Failed)
            {
                // No bootstrap ran at all. A recovery load owns this scene's fate.
                enabled = false;
                return;
            }

            BootstrapStatus.MarkMainMenuReached();
            EnsureRuntimeMenuBindings(resetPanelState: true);
            CacheLocalizationCold(GlobalRegistry.LocalizationText);
            CacheSaveManagerCold(Hecton8.Core.GlobalRegistry.Save as SaveManager);
            CacheSettingsManagerCold(GlobalRegistry.Settings);
            ApplyPersistedVisualStyleCold();
            ApplyPersistedVisualConceptCold();
            BlockCancelInputBriefly();
        }

        private void Start()
        {
            CacheSaveManagerCold(Hecton8.Core.GlobalRegistry.Save as SaveManager);
            CacheLocalizationCold(GlobalRegistry.LocalizationText);
            CacheSettingsManagerCold(GlobalRegistry.Settings);
            ApplyPersistedVisualStyleCold();
            ApplyPersistedVisualConceptCold();
            TryRegisterToTickManager();

#if UNITY_EDITOR
            if (!IsSaveManagerUsable(_saveManager))
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[MainMenuController] Hecton8.Core.GlobalRegistry.Save is unavailable or not initialized. Save/Load features will be unavailable until the runtime service is ready.");
            }
#endif
        }

        private void OnEnable()
        {
            EnsureRuntimeMenuBindings(resetPanelState: _currentPanel == null);
            TryRegisterHotSwapListener();
            CacheInputManagerCold(GlobalRegistry.NativeInputRuntime);
            CacheLocalizationCold(GlobalRegistry.LocalizationText);
            CacheSaveManagerCold(Hecton8.Core.GlobalRegistry.Save as SaveManager);
            CacheSettingsManagerCold(GlobalRegistry.Settings);
            ApplyPersistedVisualStyleCold();
            ApplyPersistedVisualConceptCold();
            TryRegisterToTickManager();
            _lastUnscaledTickTime = ResolveCurrentUnscaledTimeSeconds(0f);
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
            TryShowLatestFailureSnapshot();
            RefreshSelectionIfNeeded();
        }

        private void OnDisable()
        {
            UnbindMenuInput();
            TryUnregisterHotSwapListener();

            LocalizationEvents.UnregisterLanguageListener(this);

            // Unsubscribe from save/load events with null checks
            SaveEvents.Unregister(this);

            UnregisterFromTickManager();
            CacheLocalizationCold(null);
            CacheSaveManagerCold(null);
            CacheSettingsManagerCold(null);
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
                UnregisterFromTickManager();
                if (currentService != null)
                    TryRegisterToTickManager();
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Save)
            {
                CacheSaveManagerCold(currentService as SaveManager);
                if (_currentPanel == saveLoadGroup && !_isSaveLoadBusy && !_isSceneLoadInFlight)
                    RefreshSaveLoadSlotViewsFromCachedManager();
                RequestSelectionRefresh();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
            {
                CacheLocalizationCold(currentService as ILocalizationTextReadModel);
                RefreshLocalizedTexts();
                RefreshLoadingLocalization();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.SettingsRuntime)
            {
                CacheSettingsManagerCold(currentService as SettingsManager);
                ApplyPersistedVisualStyleCold();
                ApplyPersistedVisualConceptCold();
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.ModalWindowRuntime)
            {
                TryShowLatestFailureSnapshot();
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
                    if (IsDuplicateFailureNotification(in payload))
                        return;
                    OnSaveFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));
                    RememberFailureNotification(in payload);
                    return;

                case SaveEventType.LoadStarted:
                    OnLoadStarted(in payload);
                    return;

                case SaveEventType.LoadCompleted:
                    OnLoadCompleted(in payload);
                    return;

                case SaveEventType.LoadFailed:
                    if (IsDuplicateFailureNotification(in payload))
                        return;
                    OnLoadFailed(SaveEvents.ResolveSlotName(payload.SlotHash), ResolveSaveFailureMessage(in payload));
                    RememberFailureNotification(in payload);
                    return;
            }
        }

        private void TryShowLatestFailureSnapshot()
        {
            if (GlobalRegistry.ModalWindow == null)
                return;

            if (!SaveEvents.TryConsumeLatestFailureSnapshotForUi(
                    ref _lastConsumedFailureSnapshotSequence,
                    out SaveEventPayload payload,
                    out string failureMessage))
            {
                return;
            }

            if (IsDuplicateFailureNotification(in payload))
                return;

            string slotName = SaveEvents.ResolveSlotName(payload.SlotHash);
            switch (payload.Type)
            {
                case SaveEventType.SaveFailed:
                    OnSaveFailed(slotName, failureMessage);
                    RememberFailureNotification(in payload);
                    return;

                case SaveEventType.LoadFailed:
                    OnLoadFailed(slotName, failureMessage);
                    RememberFailureNotification(in payload);
                    return;
            }
        }

        private void RefreshLocalizedTexts()
        {
            ILocalizationTextReadModel loc = _localization;
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
            ConfigurePanelRaycastTargetsCold(mainMenuGroup);
            ConfigurePanelRaycastTargetsCold(saveLoadGroup);
            ConfigurePanelRaycastTargetsCold(settingsGroup);
            ConfigurePanelRaycastTargetsCold(loadingGroup);
            ConfigureDecorativeRaycastTargetsCold(transform);
            BindButtons();
            ConfigureDiegeticMenuRuntimeCold();
            RebuildVisualStyleCacheCold();
            RebuildVisualConceptCacheCold();

            if (resetPanelState || !_runtimeBindingsReady)
                InitializePanelStates();

            _runtimeBindingsReady = true;
        }

        private void ConfigureDiegeticMenuRuntimeCold()
        {
            if (_diegeticCanvas == null)
                TryGetComponent(out _diegeticCanvas);

            mainMenuCamera = ResolveMainMenuCameraCold(mainMenuCamera);
            if (!DiegeticMenuCanvasUtility.ApplyWorldSpaceCanvas(
                    _diegeticCanvas,
                    mainMenuCamera,
                    out _diegeticCanvasRoot,
                    out _diegeticPanelCollider))
            {
                return;
            }

            if (_diegeticPanelController == null && !TryGetComponent(out _diegeticPanelController))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _diegeticPanelController = gameObject.AddComponent<DiegeticPanelController>(); // COLD ALLOC: main-menu diegetic panel projection owner.
#endif
            }

            if (_diegeticRaycastReceiver == null && !TryGetComponent(out _diegeticRaycastReceiver))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _diegeticRaycastReceiver = gameObject.AddComponent<DiegeticMenuRaycastReceiver>(); // COLD ALLOC: fixed menu button receiver.
#endif
            }

            if (_diegeticRaycastReceiver != null)
                _diegeticRaycastReceiver.Configure(_diegeticCanvasRoot, _cachedEventSystem ?? EventSystem.current, DiegeticMenuHapticSourceHash);

            if (_diegeticPanelController != null)
            {
                _diegeticPanelController.OverrideRenderTexturePresentation(false);
                _diegeticPanelController.OverrideInteractionMode(DiegeticPanelController.PanelInteractionMode.RaycastOnly);
                _diegeticPanelController.OverrideInteractionCamera(mainMenuCamera);
                _diegeticPanelController.OverrideReferenceResolution(
                    DiegeticMenuCanvasUtility.ReferenceWidth,
                    DiegeticMenuCanvasUtility.ReferenceHeight);
                _diegeticPanelController.OverrideMaxInteractionDistance(2f);
                _diegeticPanelController.OverridePanelInteractable(_diegeticRaycastReceiver);
            }

            if (mainMenuCamera == null)
                return;

            if (_menuCameraController == null && !mainMenuCamera.TryGetComponent(out _menuCameraController))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _menuCameraController = mainMenuCamera.gameObject.AddComponent<MenuCameraController>(); // COLD ALLOC: main-menu spline camera driver.
#endif
            }

            if (_menuCameraController != null)
                _menuCameraController.Configure(mainMenuCamera);

            if (_menuAtmosphereController == null && !mainMenuCamera.TryGetComponent(out _menuAtmosphereController))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _menuAtmosphereController = mainMenuCamera.gameObject.AddComponent<MainMenuAtmosphereController>(); // COLD ALLOC: authored menu atmosphere binder.
#endif
            }

            if (_menuAtmosphereController != null)
                _menuAtmosphereController.Configure(mainMenuCamera);
        }


        internal bool TryGetReadableOverlayCamera(out Camera camera)
        {
            camera = mainMenuCamera;
            return camera != null;
        }

        private Camera ResolveMainMenuCameraCold(Camera preferred)
        {
            Camera resolved = DiegeticMenuCanvasUtility.ResolveCamera(preferred);
            if (resolved != null)
                return resolved;

            UnityEngine.SceneManagement.Scene activeScene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
                return null;

            _cameraRootSearchBuffer.Clear();
            activeScene.GetRootGameObjects(_cameraRootSearchBuffer);
            for (int i = 0; i < _cameraRootSearchBuffer.Count; i++)
            {
                GameObject root = _cameraRootSearchBuffer[i];
                if (root == null)
                    continue;

                _cameraSearchBuffer.Clear();
                root.GetComponentsInChildren(false, _cameraSearchBuffer);
                for (int j = 0; j < _cameraSearchBuffer.Count; j++)
                {
                    Camera camera = _cameraSearchBuffer[j];
                    if (camera != null && camera.isActiveAndEnabled)
                    {
                        _cameraSearchBuffer.Clear();
                        _cameraRootSearchBuffer.Clear();
                        return camera;
                    }
                }
            }

            _cameraSearchBuffer.Clear();
            _cameraRootSearchBuffer.Clear();
            return null;
        }

        private static void ConfigurePanelRaycastTargetsCold(CanvasGroup group)
        {
            if (group == null)
                return;

            ConfigureDecorativeRaycastTargetsCold(group.transform);
        }

        private static readonly List<Graphic> s_GraphicBuffer = new List<Graphic>(128);
        private static readonly List<Selectable> s_SelectableBuffer = new List<Selectable>(128);
        private static readonly List<ScrollRect> s_ScrollRectBuffer = new List<ScrollRect>(32);
        private static readonly HashSet<Transform> s_InteractiveRoots = new HashSet<Transform>(128);

        private static void ConfigureDecorativeRaycastTargetsCold(Transform root)
        {
            if (root == null)
                return;

            root.GetComponentsInChildren(true, s_GraphicBuffer);
            root.GetComponentsInChildren(true, s_SelectableBuffer);
            root.GetComponentsInChildren(true, s_ScrollRectBuffer);

            s_InteractiveRoots.Clear();

            bool foundInteractiveParent = false;
            Transform p = root;
            while (p != null)
            {
                if (p.gameObject.activeInHierarchy && (p.TryGetComponent<Selectable>(out _) || p.TryGetComponent<ScrollRect>(out _)))
                {
                    foundInteractiveParent = true;
                    break;
                }
                p = p.parent;
            }

            if (foundInteractiveParent)
            {
                s_InteractiveRoots.Add(root);
            }
            else
            {
                for (int i = 0; i < s_SelectableBuffer.Count; i++)
                    s_InteractiveRoots.Add(s_SelectableBuffer[i].transform);
                for (int i = 0; i < s_ScrollRectBuffer.Count; i++)
                    s_InteractiveRoots.Add(s_ScrollRectBuffer[i].transform);
            }

            Transform lastGraphicTransform = null;
            for (int i = 0; i < s_GraphicBuffer.Count; i++)
            {
                Graphic graphic = s_GraphicBuffer[i];
                Transform current = graphic.transform;

                // Mimic TryGetComponent behavior (only process the first Graphic per GameObject)
                if (current == lastGraphicTransform)
                    continue;
                lastGraphicTransform = current;

                bool isOwned = false;
                bool skip = false;

                while (current != null)
                {
                    if (current.name == "Panel_ModalConfirm")
                    {
                        skip = true;
                        break;
                    }

                    if (s_InteractiveRoots.Contains(current))
                    {
                        isOwned = true;
                        break;
                    }

                    if (current == root)
                        break;

                    current = current.parent;
                }

                if (!skip && !isOwned)
                {
                    graphic.raycastTarget = false;
                }
            }

            s_GraphicBuffer.Clear();
            s_SelectableBuffer.Clear();
            s_ScrollRectBuffer.Clear();
            s_InteractiveRoots.Clear();
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
            if (_startNewGameModalAction == null)
                _startNewGameModalAction = StartNewGameFromModal; // COLD ALLOC: Action[1] - cached new-game modal confirm - owner: MainMenuController
            if (_startPendingGameModalAction == null)
                _startPendingGameModalAction = StartPendingGameFromModal; // COLD ALLOC: Action[1] - cached load/retry modal confirm - owner: MainMenuController
            if (_quitApplicationModalAction == null)
                _quitApplicationModalAction = QuitApplicationFromModal; // COLD ALLOC: Action[1] - cached quit modal confirm - owner: MainMenuController
            if (_openSaveLoadMenuModalAction == null)
                _openSaveLoadMenuModalAction = OpenSaveLoadMenu; // COLD ALLOC: Action[1] - cached save-load modal return - owner: MainMenuController
            if (_returnSaveLoadToMainMenuAction == null)
                _returnSaveLoadToMainMenuAction = ReturnSaveLoadToMainMenu; // COLD ALLOC: Action[1] - cached save-load modal cancel - owner: MainMenuController
            if (_returnLoadingToMainMenuAction == null)
                _returnLoadingToMainMenuAction = ReturnLoadingToMainMenu; // COLD ALLOC: Action[1] - cached loading modal cancel - owner: MainMenuController
        }

        private static void BindButton(Button button, UnityAction callback)
        {
            if (button == null)
                return;

            if (callback == null)
                return;

            button.onClick.RemoveListener(callback);
            button.onClick.AddListener(callback);
        }

        private void PublishPrimaryMenuActionFeedback(Selectable control)
        {
            _diegeticRaycastReceiver?.PublishProgrammaticPrimaryClick(control);
        }

        private void PublishSecondaryMenuActionFeedback(Selectable control)
        {
            _diegeticRaycastReceiver?.PublishProgrammaticSecondaryClick(control);
        }

        private void PublishDestructiveMenuActionFeedback(Selectable control)
        {
            _diegeticRaycastReceiver?.PublishProgrammaticDestructiveClick(control);
        }

        private void InitializePanelStates()
        {
            ClearPanelTransitionState();
            SetPanelImmediate(mainMenuGroup, true);
            SetPanelImmediate(saveLoadGroup, false);
            SetPanelImmediate(settingsGroup, false);
            SetPanelImmediate(loadingGroup, false);
            _currentPanel = mainMenuGroup;
            BeginCameraRouteForPanel(mainMenuGroup);
            RequestSelectionRefresh();
        }

        private void OnNewGameClicked()
        {
            PublishPrimaryMenuActionFeedback(btnNewGame);

            int messageLength = CopyLocalizedModalMessage(LocalizationKeys.MODAL_NEW_GAME_MESSAGE, "Start a new game?");
            ModalWindow.Show(
                "New Game",
                _modalMessageBuffer,
                messageLength,
                _startNewGameModalAction);
        }

        private void OnLoadGameClicked()
        {
            PublishSecondaryMenuActionFeedback(btnLoadGame);
            OpenSaveLoadMenu();
        }

        private void OnSettingsClicked()
        {
            if (!_settingsAvailable)
                return;

            PublishSecondaryMenuActionFeedback(btnSettings);
            SwitchPanel(ResolvePanelSwitchSource(), settingsGroup);
        }

        private void OnQuitClicked()
        {
            PublishDestructiveMenuActionFeedback(btnQuit);

            int messageLength = CopyLocalizedModalMessage(LocalizationKeys.MODAL_QUIT_MESSAGE, "Quit the game?");
            ModalWindow.Show(
                "Quit",
                _modalMessageBuffer,
                messageLength,
                _quitApplicationModalAction);
        }

        private void OnBackFromSaveLoadClicked()
        {
            PublishSecondaryMenuActionFeedback(btnBackFromSaveLoad);
            SwitchPanel(saveLoadGroup, mainMenuGroup);
        }

        private void OnBackFromSettingsClicked()
        {
            PublishSecondaryMenuActionFeedback(btnBackFromSettings);
            settingsPanel?.CancelPendingChanges();
            SwitchPanel(settingsGroup, mainMenuGroup);
        }

        private Action CacheStartGameAction(string slotName)
        {
            if (!string.IsNullOrEmpty(slotName) &&
                !SaveEvents.IsKnownManualSlotName(slotName))
            {
                _pendingStartSlotName = string.Empty;
                return null;
            }

            if (_startPendingGameModalAction == null)
                _startPendingGameModalAction = StartPendingGameFromModal;

            _pendingStartSlotName = slotName ?? string.Empty;
            return _startPendingGameModalAction;
        }

        private void StartNewGameFromModal()
        {
            _pendingStartSlotName = string.Empty;
            StartGame(string.Empty);
        }

        private void StartPendingGameFromModal()
        {
            string slotName = _pendingStartSlotName;
            _pendingStartSlotName = string.Empty;
            StartGame(slotName);
        }

        private void QuitApplicationFromModal()
        {
#if UNITY_EDITOR
            Hecton8.Dev.EditorPlayModeDiagnostics.RequestStopPlayMode(
                nameof(MainMenuController),
                "MainMenu Quit",
                this);
#else
            Application.Quit();
#endif
        }

        private void ReturnSaveLoadToMainMenu()
        {
            SwitchPanel(saveLoadGroup, mainMenuGroup);
        }

        private void ReturnLoadingToMainMenu()
        {
            SetExclusivePanelImmediate(mainMenuGroup);
            BeginCameraRouteForPanel(mainMenuGroup);
            RequestSelectionRefresh();
        }

        private void AutoWireSceneReferences()
        {
            Transform root = transform;

            mainMenuGroup = ResolveCanvasGroup(mainMenuGroup, root, "Panel_MainMenu");
            saveLoadGroup = ResolveCanvasGroup(saveLoadGroup, root, "Panel_Sideload Popup");
            settingsGroup = ResolveCanvasGroup(settingsGroup, root, "Panel_Settings");
            loadingGroup = ResolveCanvasGroup(loadingGroup, root, "Panel_LoadingScreen");
            settingsPanel = ResolveSettingsPanel(settingsPanel, settingsGroup);

            btnNewGame = ResolveButton(btnNewGame, root, "BTN_Start");
            btnLoadGame = ResolveButton(btnLoadGame, root, "BTN_ResumeLog");
            btnSettings = ResolveButton(btnSettings, root, "BTN_Settings");
            btnQuit = ResolveButton(btnQuit, root, "BTN_Abort");
            btnBackFromSaveLoad = ResolveButton(btnBackFromSaveLoad, root, "BTN_Back (\"RETURN\")");
            btnBackFromSettings = ResolveButton(btnBackFromSettings, root, "Btn_BackFromSettings");

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

        private static SettingsPanel ResolveSettingsPanel(SettingsPanel current, CanvasGroup group)
        {
            if (current != null)
                return current;

            if (group == null)
                return null;

            if (group.TryGetComponent(out SettingsPanel panel))
                return panel;

            return ComponentReferenceUtility.ResolveOwnedComponent<SettingsPanel>(group.transform);
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
        /// Displays "Save system unavailable" if SaveManager is not usable.
        /// </summary>
        public void OpenSaveLoadMenu()
        {
            if (_isSaveLoadBusy || _isSceneLoadInFlight)
                return;

            EnsureSlotInstances();
            RefreshSaveLoadSlotViewsFromCachedManager();

            SwitchPanel(ResolvePanelSwitchSource(), saveLoadGroup);
        }

        private void RefreshSaveLoadSlotViewsFromCachedManager()
        {
            SaveManager saveManager = _saveManager;
            if (!IsSaveManagerUsable(saveManager))
            {
                ApplyUnavailableSaveSlotViews();
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

                    if (saveManager.TryGetSaveSlotInfo(slotName, out SaveSlotInfo slotInfo))
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
                    "[MainMenuController] Save shell is missing or incomplete. Opening save/load in fallback state so the player can still back out.");
#endif
            }

            SetSaveLoadButtonsInteractable(!_isSaveLoadBusy && !_isSceneLoadInFlight);
        }

        private void ApplyUnavailableSaveSlotViews()
        {
            if (_slotButtonAvailability != null)
            {
                for (int i = 0; i < _slotButtonAvailability.Length; i++)
                    _slotButtonAvailability[i] = false;
            }

            if (_slotUIs != null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    string slotName = SlotNames[i];
                    SaveSlotUI slotUI = _slotUIs[i];
                    if (slotUI == null)
                        continue;

                    slotUI.Init(slotName, false, string.Empty, 0f, OnSlotClicked);
                }
            }

            SetSaveLoadButtonsInteractable(!_isSaveLoadBusy && !_isSceneLoadInFlight);
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
            _diegeticRaycastReceiver?.RebuildButtonCache();

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

            if (!SaveEvents.IsKnownManualSlotName(slotName))
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.LogWarning("[MainMenuController] Ignored unknown save slot click.");
#endif
                return;
            }

            PublishPrimaryMenuActionFeedback(ResolveSlotButtonByName(slotName));

            int messageLength = BuildSlotModalMessage(
                LocalizationKeys.MODAL_LOAD_MESSAGE,
                "Load selected save?",
                slotName,
                ReadOnlySpan<char>.Empty);

            ModalWindow.Show("Load Game", _modalMessageBuffer, messageLength, CacheStartGameAction(slotName));
        }

        private Button ResolveSlotButtonByName(string slotName)
        {
            if (string.IsNullOrEmpty(slotName))
                return null;

            for (int i = 0; i < SlotNames.Length; i++)
            {
                if (string.Equals(SlotNames[i], slotName, StringComparison.Ordinal))
                    return ResolveSlotButtonByIndex(i);
            }

            return null;
        }

        private Button ResolveSlotButtonByIndex(int slotIndex)
        {
            SaveSlotUI[] slotUis = _slotUIs;
            if (slotUis == null || (uint)slotIndex >= (uint)slotUis.Length)
                return null;

            SaveSlotUI slotUi = slotUis[slotIndex];
            return slotUi != null ? slotUi.ButtonComponent : null;
        }

        /// <summary>
        /// Starts async loading of the game scene.
        /// Empty slotName = new game, otherwise = load save.
        /// Writes to GameStartContextHolder for inter-scene communication.
        /// Cold persistence is owned by the holder, not by MainMenuController.
        /// TASK 31: Comprehensive availability checks for SaveManager before loading.
        /// </summary>
        public void StartGame(string slotName)
        {
            bool isNewGame = string.IsNullOrEmpty(slotName);
            StartGameWithScene(slotName, ResolveStartSceneName(isNewGame));
        }

        public void StartOrbitPrologue()
        {
            StartGameWithScene(string.Empty, OrbitSceneName);
        }

        public void ReadableStartNewGame()
        {
            PublishPrimaryMenuActionFeedback(btnNewGame);
            StartGame(string.Empty);
        }

        public void ReadableOpenLoadPanel()
        {
            PublishSecondaryMenuActionFeedback(btnLoadGame);
            OpenSaveLoadMenu();
            if (!_isSceneLoadInFlight && !_isSaveLoadBusy)
            {
                SetExclusivePanelImmediate(saveLoadGroup);
                BeginCameraRouteForPanel(saveLoadGroup);
                RequestSelectionRefresh();
            }
        }

        public void ReadableOpenSettingsPanel()
        {
            if (!_settingsAvailable)
                return;

            PublishSecondaryMenuActionFeedback(btnSettings);
            SetExclusivePanelImmediate(settingsGroup);
            BlockCancelInputBriefly();
            BeginCameraRouteForPanel(settingsGroup);
            RequestSelectionRefresh();
        }

        public void ReadableStartOrbitPrologue()
        {
            PublishPrimaryMenuActionFeedback(btnNewGame);
            StartOrbitPrologue();
        }

        public void ReadableQuit()
        {
            OnQuitClicked();
        }

        public void ReadableBackToMainMenu()
        {
            PublishSecondaryMenuActionFeedback(_currentPanel == settingsGroup ? btnBackFromSettings : btnBackFromSaveLoad);

            if (_currentPanel == settingsGroup)
                settingsPanel?.ReadableCancel();
            else
                settingsPanel?.CancelPendingChanges();

            SetExclusivePanelImmediate(mainMenuGroup);
            BlockCancelInputBriefly();
            BeginCameraRouteForPanel(mainMenuGroup);
            RequestSelectionRefresh();
        }

        public void ReadableLoadSlot(int slotIndex)
        {
            if ((uint)slotIndex >= SlotNames.Length)
                return;

            if (!TryGetReadableSaveSlotCanLoad(slotIndex, out bool canLoad) || !canLoad)
                return;

            PublishPrimaryMenuActionFeedback(ResolveSlotButtonByIndex(slotIndex));
            StartGame(SlotNames[slotIndex]);
        }

        private void StartGameWithScene(string slotName, string sceneName)
        {
            if (_isSceneLoadInFlight || _isSaveLoadBusy)
                return;

            if (string.IsNullOrWhiteSpace(sceneName))
                sceneName = ResolveConfiguredStartSceneName(targetSceneName);

            string safeSlotName = string.Empty;

            // Validate save exists before loading
            if (!string.IsNullOrEmpty(slotName))
            {
                SaveManager saveManager = _saveManager;
                if (!IsSaveManagerUsable(saveManager))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError("[MainMenuController] Hecton8.Core.GlobalRegistry.Save is unavailable or not initialized. Cannot validate save file.");
#endif
                    int messageLength = CopyLocalizedModalMessage(
                        LocalizationKeys.ERROR_SAVE_SYSTEM_UNAVAILABLE_MESSAGE,
                        "The save system is currently unavailable.\n\nCannot load save file.");

                    ModalWindow.ShowWithCustomLabels(
                        "Save System Unavailable",
                        _modalMessageBuffer,
                        messageLength,
                        _openSaveLoadMenuModalAction,
                        null,
                        "OK",
                        null);
                    return;
                }

                if (!SaveManager.TryResolveSafeSlotName(slotName, out safeSlotName))
                {
                    int messageLength = BuildSlotModalMessage(
                        LocalizationKeys.MODAL_LOAD_ERROR_MESSAGE,
                        "Invalid save slot.",
                        string.Empty,
                        ReadOnlySpan<char>.Empty);

                    ModalWindow.ShowWithCustomLabels(
                        "Load Error",
                        _modalMessageBuffer,
                        messageLength,
                        _openSaveLoadMenuModalAction,
                        null,
                        "OK",
                        null);
                    return;
                }

                if (!saveManager.SaveExists(safeSlotName))
                {
                    int messageLength = BuildSlotModalMessage(
                        LocalizationKeys.MODAL_LOAD_ERROR_MESSAGE,
                        "Save file does not exist.",
                        safeSlotName,
                        ReadOnlySpan<char>.Empty);

                    ModalWindow.ShowWithCustomLabels(
                        "Load Error",
                        _modalMessageBuffer,
                        messageLength,
                        _openSaveLoadMenuModalAction,
                        null,
                        "OK",
                        null);
                    return;
                }

                slotName = safeSlotName;
            }

            _isSceneLoadInFlight = true;

            bool isNewGame = string.IsNullOrEmpty(slotName);
            GameStartContext context = isNewGame
                ? GameStartContext.CreateNewGame()
                : GameStartContext.CreateLoadGame(slotName);

            GameStartContextHolder.SetCurrent(context, sceneName);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            GameStartContextHolder.LogCurrent();
#endif

            if (ShouldUseBootstrapHandoffForStart(sceneName) &&
                TryRouteStartThroughBootstrap(sceneName))
            {
                return;
            }

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
                if (TryRouteStartThroughBootstrap(sceneName))
                    return;

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
                    CacheStartGameAction(slotName),
                    _returnLoadingToMainMenuAction,
                    "Retry",
                    "Return to Menu");

                return;
            }

            if (runtimeSceneService != null)
            {
                _menuCameraController?.BeginHandoff();
                runtimeSceneService.ConfigureMainMenuCinematic(mainMenuCamera, cinematicPanel);
            }

            sceneService.LoadScene(sceneName);
        }

        /// <summary>
        /// True only when this play session has no bootstrap owner at all, so a start
        /// request cannot be served by the scene service and has to recover through
        /// 00_BOOTSTRAP first.
        /// </summary>
        /// <remarks>
        /// This predicate previously returned true for 02_HECTON_WORLD and 01_ORBIT
        /// whenever the active scene was not 00_BOOTSTRAP - which is unconditionally the
        /// case while the player is standing in 01_MAIN_MENU. Every New Game and every
        /// Load Game therefore returned early into <see cref="TryRouteStartThroughBootstrap"/>
        /// and never reached <see cref="SceneRuntimeService"/>: no world residency gate,
        /// no floating-origin or GPU residency gate, no menu cinematic handoff, no loading
        /// screen, and scene activation left unconditionally enabled, which
        /// AGENTS.md Streaming/import defaults forbid.
        ///
        /// SceneRuntimeService owns the production menu route. The bootstrap reload is a
        /// recovery route only, and the caller keeps its own fallback into that route when
        /// the scene service reports it cannot load.
        ///
        /// The three facts read below are exactly the ones
        /// <see cref="BootstrapRouteEnforcer.EvaluateBootstrapRuntimeRoute"/> classifies
        /// on, read here without its side effects: that method calls
        /// GameStartContextHolder.Reset() and schedules its own Single load, which would
        /// wipe the pending target scene StartGameWithScene just wrote and double-load
        /// bootstrap.
        /// </remarks>
        /// <param name="sceneName">Requested start scene.</param>
        /// <returns>True only when bootstrap recovery must run before the start request.</returns>
        private static bool ShouldUseBootstrapHandoffForStart(string sceneName)
        {
            if (!Application.isPlaying || string.IsNullOrWhiteSpace(sceneName))
                return false;

            // Bootstrap finished its ordered phases. The scene service owns this route.
            if (GameBootstrapper.AreAllSystemsReady())
                return false;

            // A boot that already started still owns this session and is only mid-phase.
            // Reloading 00_BOOTSTRAP as LoadSceneMode.Single here would tear down the
            // in-flight bootstrapper along with the services it is registering.
            if (BootstrapStatus.BootStarted || BootstrapState.HasActiveInstance)
                return false;

            // No bootstrap ran at all. Recovery only makes sense from a scene that is not
            // already 00_BOOTSTRAP, otherwise the reload loops on itself.
            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return !string.Equals(activeSceneName, BootstrapSceneName, StringComparison.Ordinal);
        }

        private string ResolveStartSceneName(bool isNewGame)
        {
            string sceneName = isNewGame ? newGameTargetSceneName : targetSceneName;
            return ResolveConfiguredStartSceneName(sceneName);
        }

        private static string ResolveConfiguredStartSceneName(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName) ? DefaultGameplaySceneName : sceneName.Trim();
        }

        private bool TryRouteStartThroughBootstrap(string sceneName)
        {
            if (!Application.isPlaying)
                return false;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log(
                "[MainMenuController] Routing start through 00_BOOTSTRAP with pending target scene.");
#endif

            UnityEngine.AsyncOperation operation =
                UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                    BootstrapScenePath,
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            if (operation == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    "[MainMenuController] Failed to schedule async bootstrap recovery load.");
#endif
                return false;
            }

            return true;
        }

        /// <summary>
        /// Smoothly fades out one panel and fades in the next.
        /// Double-click protected via instant interactable/blocksRaycasts toggle.
        /// </summary>
        private void SwitchPanel(CanvasGroup from, CanvasGroup to)
        {
            if (_isTransitioning || to == null)
                return;

            if (from == null || from == to)
            {
                SetExclusivePanelImmediate(to);
                BlockCancelInputBriefly();
                BeginCameraRouteForPanel(to);
                RequestSelectionRefresh();
                return;
            }

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

            HidePanelsExcept(from, to);
            EnsurePanelHierarchyActive(to);
            from.interactable = false;
            from.blocksRaycasts = false;
            to.alpha = 0f;
            to.interactable = false;
            to.blocksRaycasts = false;
            BlockCancelInputBriefly();
            BeginCameraRouteForPanel(to);
        }

        private void BeginCameraRouteForPanel(CanvasGroup panel)
        {
            if (_menuCameraController == null)
                return;

            MenuCameraController.MenuCameraRoute route = MenuCameraController.MenuCameraRoute.Main;
            if (panel == saveLoadGroup)
                route = MenuCameraController.MenuCameraRoute.Saves;
            else if (panel == settingsGroup)
                route = MenuCameraController.MenuCameraRoute.Settings;
            else if (panel == loadingGroup)
                route = MenuCameraController.MenuCameraRoute.Loading;

            _menuCameraController.BeginRoute(route, math.max(0.35f, fadeDuration * 2.2f));
        }

        public void Tick(float dt)
        {
            float unscaledDeltaTime = GetUnscaledDeltaTime(dt);
            if (unscaledDeltaTime <= 0f)
                return;

            EnsureMenuInputRoutingReady();
            ConsumeCancelInputSignals();
            HandleCancelInput();
            _menuPresentationDeltaTime = unscaledDeltaTime;
        }

        public void LateFrameTick()
        {
            float menuPresentationDeltaTime = _menuPresentationDeltaTime;
            _menuPresentationDeltaTime = 0f;
            if (menuPresentationDeltaTime > 0f)
            {
                UpdatePanelTransition(menuPresentationDeltaTime);
                _menuCameraController?.Advance(menuPresentationDeltaTime);
            }

            DiegeticMenuCanvasUtility.SyncCameraRelativePose(_diegeticCanvasRoot, mainMenuCamera);
            SyncVisualStyleLateFrame();
            SyncVisualConceptLateFrame();
            _menuAtmosphereController?.Advance(
                menuPresentationDeltaTime,
                ResolveCurrentUnscaledTimeSeconds(0f),
                ResolveMenuVisualQualityWeight(),
                visualStyle,
                visualConcept);
            RefreshSelectionIfNeeded();
            _diegeticRaycastReceiver?.FlushPendingSelection();
        }

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

            float currentTime = ResolveCurrentUnscaledTimeSeconds(_nextInputRoutingRetryTime);
            if (currentTime < _nextInputRoutingRetryTime)
                return;

            _nextInputRoutingRetryTime = currentTime + InputRoutingRetrySeconds;
            BindMenuInput();
            RefreshMenuInputRoutingReadyFromCache();
        }

        private void CacheMenuInputRoutingCold()
        {
            _cachedEventSystem = EventSystem.current;
            _cachedUiInputModule = null;
            _inputRoutingReady = false;
            _nextInputRoutingRetryTime = 0f;

            if (_cachedEventSystem == null || !_cachedEventSystem.enabled)
                return;

            _cachedEventSystem.TryGetComponent(out _cachedUiInputModule);
            if (_diegeticRaycastReceiver != null && _diegeticCanvasRoot != null)
                _diegeticRaycastReceiver.Configure(_diegeticCanvasRoot, _cachedEventSystem, DiegeticMenuHapticSourceHash);
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
            if (!_cancelRequested)
                return;

            // Input spam protection: consume and drop stale cancel during transitions or scene loading.
            if (_isTransitioning ||
                _isSceneLoadInFlight ||
                _isSaveLoadBusy ||
                (math.isfinite(_cancelInputBlockedUntil) &&
                 ResolveCurrentUnscaledTimeSeconds(0f) < _cancelInputBlockedUntil))
            {
                _cancelRequested = false;
                return;
            }

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

            EventSystem eventSystem = _cachedEventSystem;
            if (eventSystem == null || !eventSystem.enabled)
            {
                _refreshSelectionRequested = false;
                return;
            }

            _refreshSelectionRequested = false;

            Button target = ResolveDefaultSelectionButton();
            if (!IsDefaultSelectionTargetEligible(target))
                target = null;

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

        private bool IsDefaultSelectionTargetEligible(Button target)
        {
            if (target == null || !target.interactable || !target.gameObject.activeInHierarchy)
                return false;

            CanvasGroup currentPanel = _currentPanel;
            if (currentPanel == null ||
                !currentPanel.interactable ||
                !currentPanel.blocksRaycasts ||
                currentPanel.alpha < 0.999f)
            {
                return false;
            }

            Transform targetTransform = target.transform;
            Transform panelTransform = currentPanel.transform;
            return targetTransform != null && panelTransform != null && targetTransform.IsChildOf(panelTransform);
        }

        private void BlockCancelInputBriefly()
        {
            _cancelInputBlockedUntil = ResolveCurrentUnscaledTimeSeconds(0f) + CancelInputDebounceSeconds;
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
            _nextInputRoutingRetryTime = 0f;
            _cancelRequested = false;
        }

        private void CacheLocalizationCold(ILocalizationTextReadModel localization)
        {
            _localization = localization;
        }

        private void CacheSaveManagerCold(SaveManager saveManager)
        {
            _saveManager = IsSaveManagerUsable(saveManager) ? saveManager : null;
        }

        private static bool IsSaveManagerUsable(SaveManager saveManager)
        {
            return saveManager != null && saveManager.IsInitialized;
        }

        private void CacheSettingsManagerCold(SettingsManager settingsManager)
        {
            if (ReferenceEquals(_settingsManager, settingsManager))
                return;

            if (_settingsManager != null)
            {
                _settingsManager.MenuVisualStyleChanged -= HandleMenuVisualStyleChanged;
                _settingsManager.MenuVisualConceptChanged -= HandleMenuVisualConceptChanged;
            }

            _settingsManager = settingsManager;

            if (_settingsManager != null)
            {
                _settingsManager.MenuVisualStyleChanged += HandleMenuVisualStyleChanged;
                _settingsManager.MenuVisualConceptChanged += HandleMenuVisualConceptChanged;
            }
        }

        private void ApplyPersistedVisualStyleCold()
        {
            SettingsManager settingsManager = _settingsManager;
            if (settingsManager == null)
                return;

            SetVisualStyle(settingsManager.MenuVisualStyle);
        }

        private void HandleMenuVisualStyleChanged(MenuVisualStyle style)
        {
            SetVisualStyle(style);
        }

        private void ApplyPersistedVisualConceptCold()
        {
            SettingsManager settingsManager = _settingsManager;
            if (settingsManager == null)
                return;

            SetVisualConcept(settingsManager.MenuVisualConcept);
        }

        private void HandleMenuVisualConceptChanged(MenuVisualConcept concept)
        {
            SetVisualConcept(concept);
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

        public bool TryGetReadableSaveSlotState(
            int slotIndex,
            Span<char> titleDestination,
            out int titleLength,
            Span<char> detailDestination,
            out int detailLength,
            out bool canLoad)
        {
            titleLength = 0;
            detailLength = 0;
            canLoad = false;
            AppendReadableSlotText(
                (uint)slotIndex < (uint)SlotDisplayNames.Length
                    ? SlotDisplayNames[slotIndex].AsSpan()
                    : "SLOT ?".AsSpan(),
                titleDestination,
                ref titleLength);
            AppendReadableSlotText("EMPTY".AsSpan(), detailDestination, ref detailLength);

            if (!TryResolveReadableSaveSlotInfo(
                    slotIndex,
                    out SaveSlotInfo slotInfo,
                    out canLoad,
                    out bool saveSystemReady))
            {
                return false;
            }

            if (!saveSystemReady)
            {
                detailLength = 0;
                AppendReadableSlotText("SAVE SYSTEM OFFLINE".AsSpan(), detailDestination, ref detailLength);
                return true;
            }

            if (!canLoad || slotInfo == null)
                return true;

            SaveMetadata metadata = slotInfo.metadata;
            string status = slotInfo.GetStatusLabel();
            detailLength = 0;
            if (metadata == null)
            {
                AppendReadableSlotText(
                    string.IsNullOrEmpty(status) ? "SAVE DATA PRESENT".AsSpan() : status.AsSpan(),
                    detailDestination,
                    ref detailLength);
                return true;
            }

            string normalizedSceneName = SaveMetadata.NormalizeSceneName(metadata.SceneName);
            ReadOnlySpan<char> sceneName = string.Equals(normalizedSceneName, SaveMetadata.UnknownSceneName, StringComparison.Ordinal)
                ? "UNKNOWN SCENE".AsSpan()
                : normalizedSceneName.AsSpan();
            int minutes = Mathf.Max(0, Mathf.RoundToInt(metadata.totalPlayTime / 60f));
            AppendReadableSlotText(sceneName, detailDestination, ref detailLength);
            AppendReadableSlotText(" / ".AsSpan(), detailDestination, ref detailLength);
            ZeroGCFormatter.AppendInt(minutes, detailDestination, ref detailLength);
            AppendReadableSlotText(" MIN".AsSpan(), detailDestination, ref detailLength);
            if (!string.IsNullOrEmpty(status))
            {
                AppendReadableSlotText(" / ".AsSpan(), detailDestination, ref detailLength);
                AppendReadableSlotText(status.AsSpan(), detailDestination, ref detailLength);
            }

            return true;
        }

        private bool TryGetReadableSaveSlotCanLoad(int slotIndex, out bool canLoad)
        {
            return TryResolveReadableSaveSlotInfo(slotIndex, out _, out canLoad, out _);
        }

        private bool TryResolveReadableSaveSlotInfo(
            int slotIndex,
            out SaveSlotInfo slotInfo,
            out bool canLoad,
            out bool saveSystemReady)
        {
            slotInfo = null;
            canLoad = false;
            saveSystemReady = false;

            if ((uint)slotIndex >= SlotNames.Length)
                return false;

            SaveManager saveManager = _saveManager;
            if (!IsSaveManagerUsable(saveManager))
                return true;

            saveSystemReady = true;
            string slotName = SlotNames[slotIndex];
            if (!saveManager.TryGetSaveSlotInfo(slotName, out slotInfo) ||
                slotInfo == null ||
                !slotInfo.HasAnySaveData)
            {
                slotInfo = null;
                return true;
            }

            canLoad = true;
            return true;
        }

        private static void AppendReadableSlotText(
            ReadOnlySpan<char> value,
            Span<char> destination,
            ref int cursor)
        {
            ZeroGCFormatter.AppendToSpanTruncated(value, destination, ref cursor, out _);
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
            float eased = SmoothStep01(t);

            if (_panelTransitionState == PanelTransitionState.FadingOut)
            {
                _transitionFromPanel.alpha = math.lerp(_transitionStartAlpha, 0f, eased);
                if (t < 1f)
                    return;

                _transitionFromPanel.alpha = 0f;
                _transitionToPanel.alpha = 0f;
                _transitionElapsed = 0f;
                _panelTransitionState = PanelTransitionState.FadingIn;
                return;
            }

            _transitionToPanel.alpha = eased;
            if (t < 1f)
                return;

            _transitionToPanel.alpha = 1f;
            _transitionToPanel.interactable = true;
            _transitionToPanel.blocksRaycasts = true;
            HidePanelsExcept(_transitionToPanel, null);
            _panelTransitionState = PanelTransitionState.None;
            _currentPanel = _transitionToPanel;
            _transitionFromPanel = null;
            _transitionToPanel = null;
            _isTransitioning = false;
            RequestSelectionRefresh();
        }

        private static float SmoothStep01(float value)
        {
            float x = math.saturate(math.isfinite(value) ? value : 0f);
            return x * x * (3f - (2f * x));
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
            ILocalizationTextReadModel loc = _localization;
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

        // SAVE/LOAD EVENT HANDLERS

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

            SaveManager saveManager = _saveManager;

            // Refresh slot metadata to show updated save info
            if (IsSaveManagerUsable(saveManager) && _slotUIs != null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    string slotNameToRefresh = SlotNames[i];
                    SaveSlotUI slotUI = _slotUIs[i];
                    if (slotUI == null)
                        continue;

                    if (saveManager.TryGetSaveSlotInfo(slotNameToRefresh, out SaveSlotInfo slotInfo))
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

            SaveManager saveManager = _saveManager;
            ShowLoadRecoveryModal(in payload, saveManager);

            SetSaveLoadButtonsInteractable(true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.Log("[MainMenuController] Load completed.");
#endif

            RequestSelectionRefresh();
        }

        private void ShowLoadRecoveryModal(in SaveEventPayload payload, SaveManager saveManager)
        {
            if (!IsSaveManagerUsable(saveManager))
                return;

            bool usedBackup = saveManager.LastLoadUsedBackup;
            bool selfRepaired = saveManager.LastLoadSelfRepaired;
            if (!usedBackup && !selfRepaired)
                return;

            string slotName = SaveEvents.ResolveSlotName(payload.SlotHash);
            int messageLength = usedBackup
                ? BuildSlotModalMessage(
                    LocalizationKeys.WARNING_BACKUP_USED_MESSAGE,
                    "Primary save file was corrupt. Loaded from backup.",
                    slotName,
                    ReadOnlySpan<char>.Empty)
                : BuildSlotModalMessage(
                    LocalizationKeys.WARNING_SAVE_REPAIRED_MESSAGE,
                    "Primary save file was repaired before loading.",
                    slotName,
                    ReadOnlySpan<char>.Empty);
            string title = usedBackup
                ? BuildLocalizedModalTitle(LocalizationKeys.WARNING_BACKUP_USED_TITLE, "BACKUP LOADED")
                : BuildLocalizedModalTitle(LocalizationKeys.WARNING_SAVE_REPAIRED_TITLE, "SAVE REPAIRED");

            ModalWindow.ShowWithCustomLabels(
                title,
                _modalMessageBuffer,
                messageLength,
                null,
                null,
                "OK",
                null);
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
            bool canRetry = SaveEvents.IsKnownManualSlotName(slotName);

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
                canRetry ? CacheStartGameAction(slotName) : null,
                canRetry ? _returnSaveLoadToMainMenuAction : null,
                canRetry ? "Retry" : "OK",
                canRetry ? "Return to Menu" : null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[MainMenuController] Load failed.");
#endif

            RequestSelectionRefresh();
        }

        private static string ResolveSaveEventError(string error)
        {
            return string.IsNullOrWhiteSpace(error) ? UnknownSaveEventError : error;
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

        private bool IsDuplicateFailureNotification(in SaveEventPayload payload)
        {
            ulong signature = BuildFailureNotificationSignature(in payload);
            return signature != 0UL && signature == _lastFailureNotificationSignature;
        }

        private void RememberFailureNotification(in SaveEventPayload payload)
        {
            ulong signature = BuildFailureNotificationSignature(in payload);
            if (signature != 0UL)
                _lastFailureNotificationSignature = signature;
        }

        private static ulong BuildFailureNotificationSignature(in SaveEventPayload payload)
        {
            if (!IsFailurePayload(in payload))
            {
                return 0UL;
            }

            ulong typePart = (ulong)(byte)payload.Type << 56;
            ulong slotPart = (ulong)payload.SlotHash << 24;
            return typePart ^ slotPart ^ payload.MessageHash ^ payload.TimestampTicks;
        }

        private static bool IsFailurePayload(in SaveEventPayload payload)
        {
            return payload.Type == SaveEventType.SaveFailed ||
                   payload.Type == SaveEventType.LoadFailed;
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

        private float GetUnscaledDeltaTime(float fallbackDeltaTime)
        {
            float dispatcherDelta = _registeredToTickManager ? SystemDispatcher.CurrentFrameUnscaledDeltaTime : 0f;
            if (math.isfinite(dispatcherDelta) && dispatcherDelta > 0f)
            {
                _lastUnscaledTickTime = ResolveCurrentUnscaledTimeSeconds(_lastUnscaledTickTime);
                return math.min(MaxMenuPresentationDeltaSeconds, dispatcherDelta);
            }

            if (math.isfinite(fallbackDeltaTime) && fallbackDeltaTime > 0f)
            {
                _lastUnscaledTickTime = ResolveCurrentUnscaledTimeSeconds(_lastUnscaledTickTime);
                return math.min(MaxMenuPresentationDeltaSeconds, fallbackDeltaTime);
            }

            float currentTime = ResolveCurrentUnscaledTimeSeconds(_lastUnscaledTickTime);
            if (!math.isfinite(_lastUnscaledTickTime) || _lastUnscaledTickTime <= 0f)
            {
                _lastUnscaledTickTime = currentTime;
                return 0f;
            }

            float delta = currentTime - _lastUnscaledTickTime;
            _lastUnscaledTickTime = currentTime;
            return math.isfinite(delta) && delta > 0f ? math.min(MaxMenuPresentationDeltaSeconds, delta) : 0f;
        }

        private static float ResolveCurrentUnscaledTimeSeconds(float fallback)
        {
            float currentTime = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (math.isfinite(currentTime) && currentTime >= 0f)
                return currentTime;

            return math.isfinite(fallback) && fallback >= 0f ? fallback : 0f;
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

        private string BuildLocalizedModalTitle(string key, ReadOnlySpan<char> fallback)
        {
            int length = CopySpanToBuffer(ResolveLocalizedSpan(_localization, key, fallback), _modalTitleBuffer, 0);
            return length > 0 ? new string(_modalTitleBuffer, 0, length) : string.Empty;
        }

        private int BuildSlotModalMessage(string key, ReadOnlySpan<char> fallback, string slotName, ReadOnlySpan<char> detail)
        {
            return BuildModalMessage(key, fallback, BuildSlotDisplayName(slotName).AsSpan(), detail);
        }

        private int BuildModalMessage(string key, ReadOnlySpan<char> fallback, ReadOnlySpan<char> primary, ReadOnlySpan<char> detail)
        {
            int cursor = 0;
            ILocalizationTextReadModel loc = _localization;
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
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredToTickManager)
                _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredLateFrameTickManager)
                _registeredLateFrameTickManager = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registeredLateFrameTickManager)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrameTickManager = false;
            }

            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
            _registeredToTickManager = false;
        }

        private void RebuildVisualStyleCacheCold()
        {
            if (_visualStyleApplier == null)
                _visualStyleApplier = new MenuVisualStyleApplier(); // COLD ALLOC: menu visual style reference cache owner.

            _visualStyleApplier.RebuildCache(transform);
        }

        private void RebuildVisualConceptCacheCold()
        {
            if (_visualConceptApplier == null)
                _visualConceptApplier = new MenuVisualConceptApplier(); // COLD ALLOC: menu visual concept transform cache owner.
            if (_visualConceptDecorApplier == null)
                _visualConceptDecorApplier = new MenuVisualConceptDecorApplier(); // COLD ALLOC: menu concept decor cache owner.

            _visualConceptApplier.Clear();
            RectTransform decorParent = null;
            if (transform is RectTransform shell)
            {
                _visualConceptApplier.AddTarget(MenuVisualConceptTargetRole.Shell, shell);
                decorParent = shell;
            }

            AddVisualConceptTarget(MenuVisualConceptTargetRole.MainPanel, mainMenuGroup);
            AddVisualConceptTarget(MenuVisualConceptTargetRole.SavesPanel, saveLoadGroup);
            AddVisualConceptTarget(MenuVisualConceptTargetRole.SettingsPanel, settingsGroup);
            AddVisualConceptTarget(MenuVisualConceptTargetRole.LoadingPanel, loadingGroup);

            if (decorParent == null && mainMenuGroup != null)
                decorParent = mainMenuGroup.transform as RectTransform;
            _visualConceptDecorApplier.Rebuild(decorParent);
        }

        private void AddVisualConceptTarget(MenuVisualConceptTargetRole role, CanvasGroup group)
        {
            if (group == null || !(group.transform is RectTransform rect))
                return;

            _visualConceptApplier.AddTarget(role, rect);
        }

        private void SyncVisualStyleLateFrame()
        {
            if (_visualStyleApplier == null)
                return;

            float now = ResolveCurrentUnscaledTimeSeconds(0f);
            _visualStyleApplier.ApplyIfNeeded(visualStyle, ResolveMenuVisualQualityWeight(), now);
        }

        private void SyncVisualConceptLateFrame()
        {
            if (_visualConceptApplier == null)
                return;

            float now = ResolveCurrentUnscaledTimeSeconds(0f);
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

        private void SetExclusivePanelImmediate(CanvasGroup target)
        {
            if (target == null)
                return;

            ClearPanelTransitionState();
            HidePanelsExcept(target, null);
            SetPanelImmediate(target, true);
        }

        private void ClearPanelTransitionState()
        {
            _transitionFromPanel = null;
            _transitionToPanel = null;
            _transitionElapsed = 0f;
            _transitionStartAlpha = 0f;
            _panelTransitionState = PanelTransitionState.None;
            _isTransitioning = false;
        }

        private CanvasGroup ResolvePanelSwitchSource()
        {
            CanvasGroup current = _currentPanel;
            if (IsPanelVisibleForSwitch(current))
                return current;

            if (IsPanelVisibleForSwitch(settingsGroup))
                return settingsGroup;
            if (IsPanelVisibleForSwitch(saveLoadGroup))
                return saveLoadGroup;
            if (IsPanelVisibleForSwitch(loadingGroup))
                return loadingGroup;

            return mainMenuGroup;
        }

        private static bool IsPanelVisibleForSwitch(CanvasGroup group)
        {
            return group != null &&
                   group.gameObject.activeInHierarchy &&
                   group.alpha > 0.001f;
        }

        private void HidePanelsExcept(CanvasGroup visible, CanvasGroup fadingFrom)
        {
            HidePanelIfNonTarget(mainMenuGroup, visible, fadingFrom);
            HidePanelIfNonTarget(saveLoadGroup, visible, fadingFrom);
            HidePanelIfNonTarget(settingsGroup, visible, fadingFrom);
            HidePanelIfNonTarget(loadingGroup, visible, fadingFrom);
        }

        private static void HidePanelIfNonTarget(
            CanvasGroup group,
            CanvasGroup visible,
            CanvasGroup fadingFrom)
        {
            if (group == null || group == visible || group == fadingFrom)
                return;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
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
